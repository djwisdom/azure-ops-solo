# Roslyn Integration Plan — Hybrid Workspace Approach

## Architecture

```
┌─────────────────────────────────────────────────┐
│                 Form1 (editor)                   │
│  ┌─────────────┐ ┌──────────┐ ┌──────────────┐ │
│  │Highlighter  │ │LintEngine│ │SymbolIndex   │ │
│  │(Roslyn cls) │ │(Analyzers)│ │(SemanticModel)│ │
│  └──────┬──────┘ └────┬─────┘ └──────┬───────┘ │
│         │              │              │          │
│         └──────────────┴──────────────┘          │
│                        │                         │
│              ┌─────────▼──────────┐              │
│              │RoslynWorkspaceService│            │
│              │  (IRoslynWorkspace) │             │
│              └─────────┬──────────┘              │
│                        │                         │
│      ┌─────────────────┼─────────────────┐      │
│      ▼                 ▼                  ▼       │
│ AdhocWorkspace   MSBuildWorkspace    Reference    │
│ (instant start)  (lazy, on .csproj)  Assemblies   │
│                                        Pack      │
└─────────────────────────────────────────────────┘
```

## Hybrid Strategy

| Mode | Trigger | Startup | Memory | Semantic Accuracy |
|---|---|---|---|---|
| **Adhoc** | File open / no `.csproj` | ~50ms | ~50MB | BCL-only, no NuGet |
| **MSBuild** | `.csproj`/`.sln` detected | 2–10s (background) | ~200MB | Full build accuracy |
| **Hybrid** | Adhoc instantly; MSBuild loads in background, swaps when ready | ~50ms → swap | ~250MB peak | Full after swap |

### Upgrade Path
1. On any file open → create `AdhocWorkspace` immediately (instant).
2. Background task scans workspace for `.csproj` / `.sln`.
3. If found → start `MSBuildWorkspace.Create()` in background.
4. When MSBuild workspace is ready → swap all consumers to use it.
5. If MSBuild fails → stick with Adhoc (graceful degradation).

---

## Files to Create/Modify

### New Files

| File | Purpose |
|---|---|
| `Roslyn/IRoslynWorkspace.cs` | Interface for workspace abstraction |
| `Roslyn/ReferenceAssemblyProvider.cs` | Loads bundled reference assemblies |
| `Roslyn/RoslynWorkspaceService.cs` | Hybrid Adhoc+MSBuild workspace implementation |
| `Roslyn/RoslynHighlighter.cs` | Syntax highlighting via Roslyn classification |
| `Roslyn/RoslynSymbolIndex.cs` | Symbol index via Roslyn SemanticModel/SymbolFinder |
| `Roslyn/RoslynLintEngine.cs` | Lint diagnostics via Roslyn analyzers |
| `Roslyn/RoslynRefactoringProvider.cs` | Renamer, SignatureHelp, GoToDefinition wrappers |

### Modified Files

| File | Changes |
|---|---|
| `MyCrownJewelApp.Pfpad.csproj` | Add Roslyn NuGet packages |
| `MyCrownJewelApp.Core.csproj` | Add `Microsoft.CodeAnalysis.CSharp` reference |
| `Form1.cs` | Replace `IncrementalHighlighter` → `RoslynHighlighter`, `SymbolIndexService` → `RoslynSymbolIndex`, `LintEngine` → `RoslynLintEngine` |
| `IncrementalHighlighter.cs` | Keep as fallback for non-C# languages; delegate C# to `RoslynHighlighter` |
| `SymbolIndexService.cs` | Keep as fallback for non-C#; delegate C# to `RoslynSymbolIndex` |
| `LintEngine.cs` | Keep as fallback; delegate C# to `RoslynLintEngine` |
| `RenameDialog.cs` | Add `IRoslynWorkspace` parameter for Roslyn-based rename |
| `SignatureHelpForm.cs` | Connect to Roslyn `SignatureHelpProvider` |
| `QuickActionProvider.cs` | Use `IRoslynWorkspace` for "add using" suggestions |
| `GoToDefinitionPicker.cs` | No changes needed (UI only) |

---

## NuGet Packages

```xml
<!-- Core Roslyn packages (lightweight) -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.12.0" />

<!-- Features: Renamer, SignatureHelp, GoToDefinition -->
<PackageReference Include="Microsoft.CodeAnalysis.Features" Version="4.12.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Features" Version="4.12.0" />

<!-- MSBuild workspace (loaded lazily) -->
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.12.0" />
```

---

## Reference Assembly Bundling

The `ReferenceAssemblyProvider` locates .NET reference assemblies from:
1. `%DOTNET_ROOT%/packs/Microsoft.NETCore.App.Ref/<version>/ref/net10.0/` (SDK install)
2. `%ProgramFiles%/dotnet/packs/...` (fallback)
3. Embedded resource fallback in `dist/ref-assemblies/`

It loads these assemblies as `MetadataReference` objects and adds them to the `AdhocWorkspace` project:

```csharp
// Core reference assemblies needed for BCL resolution
System.Runtime.dll
System.Collections.dll
System.Linq.dll
System.Threading.dll
System.Text.RegularExpressions.dll
System.ComponentModel.dll
System.IO.dll
System.Console.dll
System.Net.Primitives.dll
System.ObjectModel.dll
// ~20 assemblies total (~15MB loaded)
```

---

## IncrementalHighlighter Rewrite Path

### Before (current)
```
Regex-based TokenizeLine() → TokenInfo[] → SendMessage(EM_SETCHARFORMAT)
```
- Hand-rolled keyword matching, string detection, comment detection
- Regex timeout after 16ms per line
- Catastrophic backtracking risk on malformed input
- No semantic understanding (can't distinguish `string` keyword from `string` type)

### After (Roslyn)
```
SyntaxTree.GetRoot().DescendantTokens() → ClassifiedSpan[] → SendMessage(EM_SETCHARFORMAT)
```
- Roslyn `ClassificationTypeNames` maps to `SyntaxTokenType`
- No regex, no timeout issues
- Semantic classification: `string` → Type, `var` → Keyword, identifiers → determined by context
- Sub-millisecond per line for already-parsed trees
- Incremental parsing: only re-parses changed lines

### Classification Mapping

| Roslyn Classification | SyntaxTokenType |
|---|---|
| `ClassificationTypeNames.Keyword` | `Keyword` |
| `ClassificationTypeNames.ClassName` / `StructName` / `InterfaceName` / `EnumName` / `TypeParameterName` | `Type` |
| `ClassificationTypeNames.Comment` | `Comment` |
| `ClassificationTypeNames.StringLiteral` / `VerbatimStringLiteral` | `String` |
| `ClassificationTypeNames.NumericLiteral` | `Number` |
| `ClassificationTypeNames.Operator` | `Operator` |
| `ClassificationTypeNames.PreprocessorKeyword` | `Preprocessor` |
| `ClassificationTypeNames.Identifier` | `Identifier` |

---

## SymbolIndexService Rewrite Path

### Before (current)
- Spawns `ctags` subprocess → parses ctags output
- Falls back to regex `ScanWithRegex` on text files
- Only finds *declarations*, no cross-references
- No type information, no namespace resolution

### After (Roslyn)
- `Compilation.GetSymbolsNamed(name)` → `ISymbol[]`
- `SymbolFinder.FindSourceDeclarations(project, name)` → cross-file resolution
- `SymbolFinder.FindReferences(symbol, solution)` → find all usages
- Rich type info: `ISymbol.ToDisplayString()`, `ContainingType`, `ContainingNamespace`
- XML doc comments via `symbol.GetDocumentationCommentXml()`

---

## LintEngine Rewrite Path

### Before (current)
- 5 hand-rolled rules (trailing whitespace, line length, magic numbers, semicolons, naming)
- Regex on text, no syntax awareness
- False positives on semicolons in strings/comments
- Magic number rule flags array indices and well-known values

### After (Roslyn)
- `CSharpCompilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(...))`
- Built-in analyzers: `IDE0001`-`IDE9999` (naming, simplification, etc.)
- Custom analyzers for editor-specific rules (trailing whitespace, line length) via `DiagnosticAnalyzer` subclass
- All diagnostics include `Location` (span), `Severity`, `Descriptor.Id`
- No false positives; all analysis is syntax-tree-aware

---

## RenameDialog Rewrite Path

### Before (current)
- Regex `\bname\b` find-and-replace across all text files
- Can't distinguish same-name symbols in different scopes
- No preview of type-safe conflicts
- Simple textual replace (breaks in strings, comments, etc.)

### After (Roslyn)
- `SymbolFinder.FindSymbolAtPosition(semanticModel, position, workspace)` → exact symbol
- `Renamer.RenameSymbolAsync(solution, symbol, newName, options)` → full semantic rename
- Handles overloads, shadowing, inheritance
- Preview shows exact symbol matches only
- `RenameOptionSet` controls what gets renamed (comments, strings, etc.)

---

## SignatureHelpForm Rewrite Path

### Before (current)
- Regex-based method name extraction
- XmlDocParser extracts signature from XML comments
- Can't handle overloads, generic methods, extension methods
- Manual parameter index counting (breaks with nested parens)

### After (Roslyn)
- `SignatureHelpProvider.GetItemsAsync(document, position, triggerInfo)`
- Built-in overload resolution
- Handles all parameter patterns (named, optional, params, generics)
- Rich display: `SignatureHelpItem.PrefixDisplayParts`, `SeparatorDisplayParts`, `SuffixDisplayParts`
- Current parameter highlighted via `SignatureHelpParameter.SelectedDisplayParts`

---

## Implementation Order

1. **csproj** — Add NuGet packages, restore
2. **IRoslynWorkspace.cs** — Define abstraction
3. **ReferenceAssemblyProvider.cs** — Load ref assemblies
4. **RoslynWorkspaceService.cs** — Hybrid workspace
5. **RoslynHighlighter.cs** — Classification-based highlighting
6. **RoslynSymbolIndex.cs** — Semantic model symbol queries
7. **RoslynLintEngine.cs** — Analyzer-based diagnostics
8. **RoslynRefactoringProvider.cs** — Rename, SignatureHelp, GoToDefinition
9. **Form1.cs** — Wire everything together
10. **QuickActionProvider.cs** — Use IRoslynWorkspace for using inference

---

## Threading Model

```
UI Thread                    BG Thread 1 (Workspace)        BG Thread 2 (MSBuild)
───────────                  ────────────────────           ─────────────────────
Form1.Load()
  └→ new RoslynHS()
      └→ AdhocWksp (sync)
         └→ classify lines
                               Scan for .csproj ──────────► MSBuildWksp.Create()
                               (if found)                      ↓
                                                              Swap workspace
                                                              ↓
                                                           Notify consumers
```

- **Highlighting** — Synchronous on UI thread (Roslyn is sub-ms for already-parsed trees). For first parse of a large file, yield every 16ms.
- **Linting** — Background thread via `Task.Run`, same as current. Roslyn analyzers run on a snapshot.
- **Symbol queries** — Adhoc queries are sync on UI thread (<1ms cached). Full rebuild runs on background thread.
- **MSBuild load** — Fully async background, progress reported via events.

---

## Fallback Behavior

| Scenario | Behavior |
|---|---|
| No Roslyn packages available | Graceful fallback to current regex-based implementations |
| MSBuild workspace fails to load | Continue with AdhocWorkspace (BCL-only) |
| File is not C# | Use existing `IncrementalHighlighter` / `SyntaxDefinition` |
| AdhocWorkspace has no ref assemblies | Highlight only keywords/strings/numbers (no type detection) |
| Large file on first parse | Yield every 16ms, same as current pattern |

---

## Memory Considerations

- **AdhocWorkspace** — ~50MB for workspace + loaded ref assemblies (~15MB DLLs in memory)
- **MSBuildWorkspace** — ~200MB (MSBuild evaluation graph + NuGet assets + full compilation)
- **Swap strategy** — When MSBuild workspace replaces Adhoc, dispose the Adhoc to free memory
- **Total peak** — ~250MB (both active during transition, ~5 seconds)

This is acceptable for a developer tool on modern hardware (16GB+ RAM typical).

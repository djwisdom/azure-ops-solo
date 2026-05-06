# Tree-sitter Integration Plan — Syntax Highlighting & Symbol Extraction for Non-C# Languages

## Why Tree-sitter

Roslyn handles only C#/VB. Tree-sitter adds incremental, editor-speed parsing for C, C++, JavaScript, Python, and 50+ other languages — filling the gap for the remaining `SyntaxDefinition` entries.

## Architecture

```
IncrementalHighlighter.WorkerLoop()
  ├── C# files → RoslynWorkspaceService.GetClassificationsAsync()
  ├── Supported TS → TreeSitterService.GetTokensAsync()
  └── Unsupported    → TokenizeLine() (regex fallback)

SymbolIndexService.Lookup()
  ├── C# files → RoslynWorkspaceService.FindSymbolsAsync()
  ├── Supported TS → TreeSitterService.GetSymbolsAsync()
  └── Unsupported    → ctags / ScanWithRegex() (fallback)
```

## NuGet Dependency

`TreeSitter.DotNet` v1.3.0 by mariusgreuel — includes 28+ language grammars as bundled native DLLs for win-x64/arm64, linux-x64/arm64, osx-x64/arm64.

## File Map

| File | Purpose |
|---|---|
| `TreeSitter/TreeSitterService.cs` | Language detection, parser lifecycle, token classification, symbol extraction |
| `TreeSitter/TsLanguage.cs` | Enum mapping file extensions → tree-sitter language names |
| `TreeSitter/TokenMapper.cs` | Tree-sitter AST node type → `SyntaxTokenType` |

## Language Coverage

Tree-sitter covers every language in the current `SyntaxDefinition`:

| SyntaxDefinition | Tree-sitter Language |
|---|---|
| C (.c, .h) | `C` |
| C++ (.cpp, .hpp, .cc, .cxx) | `C++` |
| JavaScript (.js, .jsx, .mjs) | `JavaScript` |
| TypeScript (.ts, .tsx) | `TypeScript` / `TSX` |
| Python (.py) | `Python` |
| Go (.go) | `Go` |
| Rust (.rs) | `Rust` |
| Bash (.sh, .bash) | `Bash` |
| HTML (.html, .htm) | `HTML` |
| CSS (.css, .scss) | `CSS` |
| JSON (.json) | `JSON` |
| Ruby (.rb) | `Ruby` |
| PHP (.php) | `PHP` |
| Java (.java) | `Java` |
| Terraform / Bicep / YAML / PowerShell | Not in standard TS bundle (fallback) |

## Token Classification Strategy

Two modes:

**Mode 1 — Query-based (preferred):** Use tree-sitter queries with named captures:
```
(comment) @comment
(string_literal) @string
(number_literal) @number
[(keyword_statement) (keyword_control)] @keyword
(type_identifier) @type
```

**Mode 2 — AST walk (fallback):** Recursively walk leaf `SyntaxNode` instances and map `Node.Kind` name to `SyntaxTokenType`:
```
comment → Comment
string_literal, char_literal → String
integer_literal, float_literal → Number
keyword_* → Keyword
type_identifier, primitive_type → Type
preproc_* → Preprocessor
```

Mode 1 is cleaner (tree-sitter queries are standardized). Mode 2 is simpler to implement and more robust across language grammars.

## Symbol Extraction Strategy

Tree-sitter queries for common constructs:

```
C: (function_definition declarator: (function_declarator declarator: (identifier) @name))
C++: (class_specifier name: (identifier) @name)
JS: (function_declaration name: (identifier) @name) @method
Python: (function_definition name: (identifier) @name)
```

## Installation & Distribution

`TreeSitter.DotNet` bundles all native DLLs. When the NuGet restore runs, the correct platform's DLLs are placed in `bin/runtimes/<rid>/native/`. The `TreeSitter.dll` managed assembly loads them automatically.

For the WiX installer: ensure the `runtimes/` folder from the build output is included in the MSI.

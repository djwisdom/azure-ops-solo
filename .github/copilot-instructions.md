# GitHub Copilot Instructions — Personal Flip Pad (Pfpad)

**Project:** Personal Flip Pad — a professional WinForms C# code editor with IDE features  
**Repo:** `azure-ops-solo` · **App version:** 1.0.33.0  
**Current TFM:** `net9.0-windows` (SDK 9.0.314 pinned via `global.json`)  
**Next TFM:** `net10.0-windows` — upgrade when .NET 10 SDK is installed  
**Author:** Solo developer + AI assistance

---

## Build, Test, and Lint

```powershell
# Full build
cd apps/MyCrownJewelApp
dotnet build

# Run tests (all)
dotnet test tests/MyCrownJewelApp.Tests.csproj

# Run a single test class
dotnet test tests/MyCrownJewelApp.Tests.csproj --filter "FullyQualifiedName~IncrementalHighlighterTests"

# Run a single test method
dotnet test tests/MyCrownJewelApp.Tests.csproj --filter "FullyQualifiedName~IncrementalHighlighterTests.SomeMethod"

# Run with code coverage
dotnet test tests/MyCrownJewelApp.Tests.csproj --collect:"XPlat Code Coverage"

# Release build
dotnet build --configuration Release

# Run the editor
bin\Release\net9.0-windows\MyCrownJewelApp.Pfpad.exe
# or with workspace folder:
MyCrownJewelApp.Pfpad.exe <folder_path>

# Lint / format check
dotnet format --verify-no-changes

# Check scripts (local validation)
bash scripts/csharp-check.sh
bash scripts/terraform-check.sh
bash scripts/bicep-check.sh
```

> **Current test health:** 125 passed, 1 skipped (STA thread issue), 0 failed. Run takes ~38s on net9.0-windows.

---

## Architecture Overview

### Repository Structure

```
azure-ops-solo/
├── apps/MyCrownJewelApp/          # C# WinForms editor (the main app)
│   ├── src/MyCrownJewelApp.Pfpad/ # Editor: ~100 .cs files, ~1.76 MB source
│   └── tests/MyCrownJewelApp.Tests/
├── infra/                         # Terraform (azurerm ~>3.0, local state)
├── bicep/                         # Bicep templates (resourceGroup, keyVault, appService)
├── pipelines/                     # Azure DevOps YAML
├── patching/                      # VM/AKS patching PowerShell scripts
└── docs/                          # Workflow docs, planning, analysis
```

### Editor Architecture (`apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/`)

The editor is a **single-project WinForms monolith**. There is no separate Core, Terminal, or Web assembly — all code lives in `MyCrownJewelApp.Pfpad.csproj` targeting `net8.0-windows`.

**Key architectural layers:**

```
Form1.cs (~6900 lines)   ← God-class shell: all UI layout, state, and coordination
    │
    ├── HighlightRichTextBox.cs   ← Custom RichTextBox: renders squiggles, rainbow brackets,
    │                                column guide, caret, fold indicators, hover highlight
    │
    ├── IncrementalHighlighter.cs ← Async syntax highlighter using System.Threading.Channels.
    │                                Worker loop tokenizes lines; emits HighlightPatch events.
    │                                Uses Roslyn when a .csproj is present; falls back to regex.
    │                                Time-bounded: 16ms max per line, 50ms per batch.
    │
    ├── Roslyn/                   ← Hybrid workspace: AdhocWorkspace (instant) → MSBuildWorkspace
    │   ├── IRoslynWorkspace.cs      (lazy background load when .csproj/.sln detected)
    │   ├── RoslynWorkspaceService.cs
    │   └── Features/RoslynControl/ ← RoslynService, AnalyzerController, CommandBindings
    │
    ├── Debugger/                 ← DAP (Debug Adapter Protocol) via netcoredbg
    │   ├── DebugAdapterClient.cs    JSON over stdin/stdout to netcoredbg
    │   ├── DebugSession.cs          State machine: Idle→Initializing→Running→Paused→Terminated
    │   └── BreakpointManager.cs     Conditional/logpoint/hit-count; JSON-persisted
    │
    ├── TreeSitter/TreeSitterService.cs ← Optional tree-sitter parsing (TreeSitter.DotNet 1.3.0)
    │
    ├── GitService.cs + GitPanel.cs ← LibGit2Sharp: branch, status, diff, commit, push/pull
    ├── ThemeManager.cs            ← Singleton; 22 named themes; fires ThemeChanged event
    ├── SessionManager.cs          ← Saves/restores open files, cursor, scroll to session.json
    ├── LintEngine.cs + LintRules.cs ← Regex-based linting; feeds ProblemsPanel
    ├── SymbolIndexService.cs      ← Background workspace scan for go-to-definition
    ├── VimEngine.cs               ← Full Vim state machine (Normal/Insert/Visual/Command/Search)
    ├── TerminalPanel.cs           ← WinForms terminal with ANSI color, multi-tab
    ├── MinimapControl.cs          ← Custom Control; polls RichTextBox lines, renders pixel map
    ├── FoldingManager.cs          ← Code folding with collapsed region tracking
    ├── RainbowBracketEngine.cs    ← Bracket pair matching + colorization
    ├── SnippetEngine.cs           ← Expandable code snippets
    └── SyntaxDefinition.cs        ← Immutable record: keyword lists + regex patterns per language
```

**Document model (multi-tab):**
- `Form1.Document` is a nested class (defined inside Form1) holding per-tab state: file path, content, dirty flag, encoding, bookmarks, modified lines, collapsed regions, cursor/scroll position, large-file degradation flags.
- `Form1.documents` is a `List<Document>`. `Form1.activeDocIndex` is the active tab index.
- Settings are persisted as `AppSettings` record → `%AppData%\MyCrownJewelApp\TextEditor\settings.json`.

**Roslyn workspace lifecycle:**
1. On file open → `AdhocWorkspace` created instantly (~50ms, BCL references only).
2. Background scan looks for `.csproj`/`.sln`.
3. If found → `MSBuildWorkspace` loads in background (2–10s).
4. When ready → all consumers (highlighter, lint, symbols) swap to MSBuild workspace.
5. If MSBuild fails → stays on AdhocWorkspace (graceful degradation).

**Highlighting pipeline:**
1. `TextChanged` → debounce timer (100–500ms, adaptive to typing burst).
2. Timer fires → `IncrementalHighlighter.RequestRange()` pushes lines to `Channel<(int Line, string Text)>` (capacity 2000, drops oldest).
3. Worker loop reads channel, tokenizes (Roslyn or regex), emits `PatchReady` event.
4. UI thread marshal: `ApplyHighlightPatch()` on `HighlightRichTextBox` using `BeginUpdate/EndUpdate`.

---

## Key Conventions

### Code Style

- **No inline comments** in method bodies unless clarifying non-obvious logic.
- **XML doc** (`/// <summary>`) on public APIs only; internal/private classes generally uncommented.
- PascalCase for public members. `_camelCase` for private fields.
- Prefer `record` for immutable data (e.g., `SyntaxDefinition`, `SessionData`, `DocumentSnapshot`, `AppSettings`).
- Nullable reference types enabled (`<Nullable>enable</Nullable>`). Non-null assertions via `null!` are acceptable for designer-initialized fields.
- `using` file-level namespaces (C# 10 style) in newer files; older files use block `namespace { }`.

### Testing

- Framework: **xUnit** (`net8.0-windows`), in `tests/MyCrownJewelApp.Tests/`.
- Test class names match `*Tests.cs`. Test file mirrors source class: `IncrementalHighlighterTests.cs` tests `IncrementalHighlighter.cs`.
- `[assembly: InternalsVisibleTo("MyCrownJewelApp.Tests")]` is declared in `Form1.cs` — tests access `internal` members directly.
- STA thread–dependent tests (e.g., anything creating WinForms controls) must be marked `[STAThread]` or are currently skipped.
- The skipped test `Highlighter_MarksDirty_AndTokenizes` needs STA setup to run.

### WinForms Patterns

- **`BeginUpdate()` / `EndUpdate()`** must wrap all batch `RichTextBox` modifications to prevent UI flicker and scroll jitter.
- **`_suspendSelectionChanged`** flag gates `SelectionChanged` handlers during internal text updates; always set + restore in try/finally.
- **P/Invoke** is used extensively (DWM, uxtheme, user32, shell32). New P/Invoke declarations go at the top of `Form1.cs` or the relevant control file.
- **Dark mode title bar** via `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)` — Windows 10 1809+ only, silently ignored on older systems.
- **Win32 scrollbar theming** via `SetWindowTheme(hWnd, "DarkMode_Explorer", null)`.
- Timers follow a debounce pattern: one `System.Windows.Forms.Timer` per concern (highlight, breadcrumb, elastic tabs, resize, etc.). Name them `_xyzDebounce` or `_xyzTimer`.

### Theme System

- `ThemeManager.Instance` is the singleton. Themes are in `ThemeManager.Themes` dictionary (22 entries).
- All dialogs and custom controls receive the current theme via `ThemeManager.ApplyTheme(form)` in their constructor or `Load` event.
- Light vs dark is determined by `Theme.IsLight` (not by name). Use `form1.isDarkTheme` (or `IsDarkTheme`) rather than checking the theme name.
- When adding new UI panels or dialogs: call `ThemeManager.Instance.ApplyTheme(this)` in the constructor.

### Feature Degradation (Large Files)

- Files >20MB trigger async load; >50MB show warning; >500MB are rejected.
- `Document.DisableSyntaxHighlighting`, `DisableMinimap`, `DisableWordWrap` flags are set by `ApplyLargeFileDegradation()`. Always check these before starting expensive operations.

### DI / Logging Pattern (added Phase 2)

- `Program.cs` builds an `IServiceProvider` (Microsoft.Extensions.DependencyInjection 9.x). Dispose it via `using` — it owns the 3 singletons.
- Three app-wide singletons are registered: `NotificationFeedService`, `UserProfileManager`, `SessionManager`.
- Form-scoped services (`GitService`, `LintEngine`, `SymbolIndexService`, `DebugSession`, `BreakpointManager`) are **not** in DI — they are `new()`-ed as field initializers and carry per-window mutable state.
- `Form1(bool skipInitialDocument, IServiceProvider? services)` is the primary constructor. Pass `services: null` when creating tear-away windows (no shared state needed).
- `StartupFileLoggerProvider` writes `ILogger` output to `%LocalAppData%\MyCrownJewelApp\Pfpad\startup.log`.



- Feature branches off `master`; squash-style commits.
- Terraform backend is **local-only** (backend config is commented out in `infra/` — do not uncomment without setting up the remote backend first).
- Azure resource names use standard abbreviations: `rg-`, `vnet-`, `kv-`, `app-`, etc.
- Bicep templates are in `bicep/`; Terraform is in `infra/`. They are separate stacks, not linked.
- `PersonalFlipPad-Setup-1.0.18.0.exe` in `deploy/` is untracked and must **not** be committed.

### Build Metadata

- The csproj runs `GenerateBuildMetadata.ps1` before compile to inject git commit hash and build date as `BuildMetadata.g.cs` into the intermediate output. Don't edit this generated file.

---

## Active Known Issues / TODOs

- `WFO1000` suppressed — 38 custom Control properties lack `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`. Long-term fix: add the attribute to each property in `GutterPanel`, `HighlightRichTextBox`, `ColumnGuidePanel`, `CollapsibleSection`, `WhitespaceOverlayPanel`, `SettingsDialog`.
- `WindowsPackageType=MSIX` removed (was conflicting with Inno Setup).

---

## .NET 10 Migration Status

The machine has .NET SDK `10.0.300-preview` installed but the app still targets `net8.0-windows`.  
See `docs/NET10_MIGRATION_ANALYSIS.md` for the full migration plan and re-implementation recommendations.

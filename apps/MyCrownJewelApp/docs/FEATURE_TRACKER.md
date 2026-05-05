# Personal Flip Pad — Feature Tracker

**Current version:** 1.0.10.0
**Last updated:** 2026-05-06

## Legend

| Icon | Meaning |
|------|---------|
| ✅ | Implemented and stable |
| 🟡 | Implemented with known issues |
| 🔄 | In progress / partially implemented |
| 📋 | Planned / not started |

---

## 1. File Operations

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| New Tab | `Ctrl+T` | `Form1.cs` | ✅ | 1.0.0.0 | Numbering starts at 1, increments |
| New Window | `Ctrl+Shift+N` | `Form1.cs` | ✅ | 1.0.0.0 | Launches new process |
| Open | `Ctrl+O` | `Form1.cs` | ✅ | 1.0.0.0 | Theme-aware dialog |
| Recent Files | — | `Form1.cs` | ✅ | 1.0.0.0 | Last 10, persisted, "Clear Recent" |
| Save | `Ctrl+S` | `Form1.cs` | ✅ | 1.0.0.0 | Falls back to Save As if no path |
| Save As | `Ctrl+Shift+S` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Save All | `Ctrl+Alt+S` | `Form1.cs` | ✅ | 1.0.0.0 | Currently saves active file only |
| Close Tab | `Ctrl+W` | `Form1.cs` | ✅ | 1.0.0.0 | Prompt if modified; never closes last tab |
| Close Window | `Ctrl+Shift+W` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Close All | `Ctrl+Alt+W` | `Form1.cs` | ✅ | 1.0.0.0 | Prompt for all modified |
| Exit | — | `Form1.cs` | ✅ | 1.0.0.0 | — |

## 2. Edit Operations

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Undo | `Ctrl+Z` | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | SHA-256 dirty-state tracking |
| Cut / Copy / Paste / Delete | `Ctrl+X/C/V`, `Del` | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | — |
| Find | `Ctrl+F` | `FindReplaceDialog.cs` | ✅ | 1.0.0.0 | Case-sensitive, regex, direction, wrap |
| Find Next | `F3` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Find Previous | `Shift+F3` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Replace | `Ctrl+H` | `FindReplaceDialog.cs` | ✅ | 1.0.0.0 | Replace + Replace All |
| Global Search dialog | `GlobalSearchDialog.cs`, `Form1.cs` | ✅ | 1.0.10.0 | Ctrl+Shift+F, async, file filters, exclude dirs, regex/case, Replace All |
| Global Search: keyboard shortcut | `Form1.cs` (`ProcessCmdKey`) | ✅ | 1.0.10.0 | `Ctrl+Shift+F` + Edit menu item |
| Global Search: Replace All | `GlobalSearchDialog.cs` | ✅ | 1.0.10.0 | Non-regex replace across all matched files |
| Global Search: file type filter | `GlobalSearchDialog.cs` | ✅ | 1.0.10.0 | Comma-separated glob patterns in UI |
| Global Search: exclude directories | `GlobalSearchDialog.cs` | ✅ | 1.0.10.0 | Comma-separated dir names in UI |
| Go To Line | `Ctrl+G` | `GoToDialog.cs` | ✅ | 1.0.0.0 | Clamps to last line |
| Select All | `Ctrl+A` | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | — |
| Insert Time/Date | `F5` | `Form1.cs` | ✅ | 1.0.0.0 | `HH:mm:ss yyyy-MM-dd` |
| Font | `Ctrl+Shift+F` | `Form1.cs` | ✅ | 1.0.0.0 | Standard FontDialog |

## 3. Bookmarks & Folding

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Toggle Bookmark | `Ctrl+F2` | `Form1.cs`, `GutterPanel.cs` | ✅ | 1.0.0.0 | Orange dot in gutter |
| Next Bookmark | `F2` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Previous Bookmark | `Shift+F2` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Clear Bookmarks | — | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Toggle Fold | `Ctrl+Shift+[` | `FoldingManager.cs`, `Form1.cs` | ✅ | 1.0.0.0 | Turns gutter arrows |
| Toggle All Folds | `Ctrl+Alt+[` | `FoldingManager.cs`, `Form1.cs` | ✅ | 1.0.0.0 | — |
| Fold Bracket Lines | — | `FoldingManager.cs` | ✅ | 1.0.0.0 | Dotted vertical scope lines |

## 4. Code Analysis & Navigation

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Go to Definition | `F12` | `Form1.cs`, `GoToDefinitionPicker.cs`, `SymbolIndexService.cs` | ✅ | 1.0.0.0 | ctags + regex fallback |
| Rename (project-wide) | `F2` | `RenameDialog.cs`, `Form1.cs` | ✅ | 1.0.0.0 | Regex-based, preview dialog |
| Call Hierarchy | `Ctrl+Shift+H` | `CallHierarchyDialog.cs`, `Form1.cs` | ✅ | 1.0.0.5 | Incoming + outgoing, lexical |
| Parse Stack Trace | `Ctrl+Shift+T` | `StackTraceDialog.cs`, `StackTraceParser.cs` | ✅ | 1.0.0.0 | .NET, JS, Python, generic |
| Hover Documentation | — | `HoverTooltipForm.cs`, `XmlDocParser.cs` | ✅ | 1.0.0.0 | 400ms trigger, symbol kind + XML doc |
| Signature Help | — | `SignatureHelpForm.cs`, `XmlDocParser.cs` | ✅ | 1.0.0.0 | On `(`, param highlight, overloads |
| Matching Braces | — | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | 2px white rectangle |

## 5. View Features

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Zoom In/Out/Reset | `Ctrl++`/`Ctrl+-`/`Ctrl+0` | `Form1.cs` | ✅ | 1.0.0.0 | 50%–500%, 10% steps |
| Status Bar | — | `Form1.cs` | ✅ | 1.0.0.0 | Toggle; 13 indicators |
| Word Wrap | — | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | — |
| Syntax Highlighting (toggle) | — | `Form1.cs`, `IncrementalHighlighter.cs` | ✅ | 1.0.0.0 | Full toggle |
| Current Line Highlight | — | `CurrentLineHighlightPanel.cs`, `GutterPanel.cs` | ✅ | 1.0.0.0 | Off / Line Number / Whole Line |
| Insert Spaces | — | `Form1.cs` | ✅ | 1.0.0.0 | Toggle, uses Tab Size |
| Tab Size | — | `Form1.cs` | ✅ | 1.0.0.0 | 2/4/6/8/10/12 |
| Auto Indent | — | `Form1.cs` | ✅ | 1.0.0.0 | Inherits leading whitespace |
| Smart Tabs | — | `Form1.cs`, `IndentationHelper.cs` | ✅ | 1.0.0.0 | Context-aware tab behavior |
| Elastic Tabs | — | `Form1.cs` | ✅ | 1.0.0.0 | Background thread, 250ms debounce |
| Column Guide | — | `ColumnGuidePanel.cs` | ✅ | 1.0.0.0 | 72/80/100/120/150/Custom |
| Gutter | — | `GutterPanel.cs` | ✅ | 1.0.0.0 | Line numbers |
| Show Whitespace | — | `WhitespaceOverlayPanel.cs` | ✅ | 1.0.0.0 | Dots/arrows/returns |
| Minimap | — | `MinimapControl.cs` | ✅ | 1.0.0.0 | Click/drag, background rebuild |
| Vim Mode | `Ctrl+Alt+V` | `VimEngine.cs` | ✅ | 1.0.0.0 | Full modal editing |
| Split Vertical | `Ctrl+Shift+V` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Split Horizontal | `Ctrl+Alt+H` | `Form1.cs` | ✅ | 1.0.0.0 | — |
| Drag Tab to Split | — | `Form1.cs` | ✅ | 1.0.0.0 | Drag toward edge |

## 6. Themes

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Dark (default) | `ThemeManager.cs` | ✅ | 1.0.0.0 | 21 color slots |
| Light | `ThemeManager.cs` | ✅ | 1.0.0.0 | — |
| Catppuccin Latte | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| Catppuccin Frappe | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Catppuccin Macchiato | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Catppuccin Mocha | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Dracula | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| One Dark Pro | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Tokyo Night | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Night Owl | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Shades of Purple | `ThemeManager.cs` | ✅ | 1.0.0.0 | Dark |
| Atom One Light | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| GitHub Light | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| Light Owl | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| Ayu Light | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| Bluloco Light | `ThemeManager.cs` | ✅ | 1.0.0.0 | Light |
| Theme persistence | `ThemeManager.cs`, `Form1.cs` | ✅ | 1.0.0.0 | `settings.json` in `%APPDATA%` |
| Theme-aware dialogs | `ThemedDialogs.cs` | ✅ | 1.0.0.0 | — |
| Theme-aware menu | `ThemeAwareMenuRenderer.cs` | ✅ | 1.0.0.0 | — |

## 7. Sidebar Panels

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Open Folder | `Ctrl+Alt+O` | `Form1.cs` | ✅ | 1.0.0.0 | Sets workspace root |
| Workspace Panel | `Ctrl+Shift+W` | `WorkspacePanel.cs` | ✅ | 1.0.0.0 | Lazy-load file tree, 5s auto-refresh |
| Source Control Panel | `Ctrl+Alt+G` | `GitPanel.cs`, `GitService.cs` | ✅ | 1.0.0.0 | LibGit2Sharp, stage/commit/push |
| Source Control Window | `Ctrl+Shift+G` | `GitForm.cs` | ✅ | 1.0.0.0 | Standalone git form |
| Symbols Panel | `Ctrl+Alt+S` | `SymbolPanel.cs`, `SymbolIndexService.cs` | ✅ | 1.0.0.0 | Background index, kind badges |
| Problems Panel | `Ctrl+Alt+P` | `ProblemsPanel.cs` | ✅ | 1.0.0.0 | Lint + TODO, severity badges |
| Terminal | `` Ctrl+` `` | `TerminalPanel.cs` | ✅ | 1.0.0.0 | Multi-tab, ANSI, shell auto-detect |
| Notification Center | `Ctrl+Shift+N` | `NotificationCenterForm.cs` | ✅ | 1.0.0.0 | RSS feed viewer |
| Notification Settings | — | `NotificationSettingsForm.cs` | ✅ | 1.0.0.0 | Feed config dialog |
| Dependencies | `Ctrl+Alt+D` | `DependencyGraphDialog.cs`, `ProjectDependencyAnalyzer.cs` | ✅ | 1.0.0.0 | Project + NuGet references |
| Impact Analysis | `Ctrl+Alt+I` | `ImpactAnalysisDialog.cs` | ✅ | 1.0.0.0 | Cross-file namespace usage |
| Run Configurations | `Ctrl+F5` | `RunConfigurationDialog.cs`, `LaunchProfileParser.cs` | ✅ | 1.0.0.0 | launchSettings.json + .env |
| Task List | `Ctrl+Alt+T` | `TodoScanner.cs`, `ProblemsPanel.cs` | ✅ | 1.0.0.0 | 8 tags, merged into Problems |

## 8. Syntax Highlighting Engine

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Incremental highlighter | `IncrementalHighlighter.cs` | ✅ | 1.0.0.0 | Background worker, Channel queue |
| Multi-line comment state tracking | `IncrementalHighlighter.cs` | ✅ | 1.0.0.0 | — |
| Dirty-line re-tokenization | `IncrementalHighlighter.cs` | ✅ | 1.0.0.0 | — |
| C# / C / C++ highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | keywords, strings, numbers, comments, preprocessor, types |
| Bicep highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| Terraform highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| YAML highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| HTML highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| CSS / SCSS / Less highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| JavaScript / JSX highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| JSON / JSONC highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| PowerShell highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| Bash highlighting | `SyntaxDefinition.cs` | ✅ | 1.0.0.0 | — |
| Token italic rendering | `IncrementalHighlighter.cs` | ✅ | 1.0.0.0 | Comments, types, preprocessor |

## 9. Live Diagnostics (Lint Engine)

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Lint engine (400ms debounce) | `LintEngine.cs` | ✅ | 1.0.0.0 | Background thread |
| PFP001 — Trailing whitespace | `LintRules.cs` | ✅ | 1.0.0.0 | — |
| PFP002 — Line length (>120) | `LintRules.cs` | ✅ | 1.0.0.0 | — |
| PFP003 — Magic numbers | `LintRules.cs` | ✅ | 1.0.0.0 | Excludes 0, 1, -1, array indices |
| PFP004 — Missing semicolon | `LintRules.cs` | ✅ | 1.0.0.0 | — |
| PFP005 — Naming convention | `LintRules.cs` | ✅ | 1.0.0.0 | camelCase/PascalCase/_camelCase |
| Squiggly underlines (WM_PAINT) | `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | Red/yellow/blue zigzag |
| Quick Action lightbulbs | `QuickActionProvider.cs`, `QuickAction.cs`, `GutterPanel.cs` | ✅ | 1.0.0.0 | Trim ws, insert `;`, add using |

## 10. Code Coverage

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Run Tests with Coverage | `Ctrl+Alt+R` | `CoverageParser.cs`, `Form1.cs` | ✅ | 1.0.0.0 | coverlet + Cobertura |
| Load Coverage File | — | `CoverageParser.cs`, `Form1.cs` | ✅ | 1.0.0.0 | `.cobertura.xml` or `.xml` |
| Coverage gutter bars | — | `CoverageParser.cs`, `HighlightRichTextBox.cs` | ✅ | 1.0.0.0 | Green/red bars |
| Coverage Summary dialog | — | `CoverageSummaryForm.cs` | ✅ | 1.0.0.0 | Per-file stats, color-coded |

## 11. Test Runner

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| Run Tests | `Ctrl+Alt+F5` | `TestResultParser.cs`, `TestResultsDialog.cs`, `Form1.cs` | ✅ | 1.0.0.0 | TRX parsing, 5-min timeout |
| Rerun Failed Tests | `Ctrl+Alt+F6` | `TestResultParser.cs`, `Form1.cs` | ✅ | 1.0.0.0 | `--filter` with FQNs |
| Test Results dialog | — | `TestResultsDialog.cs`, `TestResult.cs` | ✅ | 1.0.0.0 | Pass/fail/skip badges, stack trace navigation |

## 12. Git Integration

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Git service (LibGit2Sharp) | `GitService.cs` | ✅ | 1.0.0.0 | No git.exe required |
| Status bar branch + dirty | `Form1.cs`, `GitService.cs` | ✅ | 1.0.0.0 | — |
| Source Control Panel | `GitPanel.cs`, `GitService.cs` | ✅ | 1.0.0.0 | Stage/unstage/commit/push/pull/fetch |
| Branch switcher | `GitPanel.cs` | ✅ | 1.0.0.0 | Local branches |
| Recent commits (30) | `GitPanel.cs` | ✅ | 1.0.0.0 | SHA, author, date, message |
| Source Control Window | `GitForm.cs` | ✅ | 1.0.0.0 | Larger commit area, full log |

## 13. Notification System (RSS)

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Feed polling service | `NotificationFeedService.cs` | ✅ | 1.0.0.0 | Background HttpClient loop |
| 8 default feeds | `NotificationFeedService.cs`, `NotificationFeed.cs` | ✅ | 1.0.0.0 | HN, Reddit, GitHub, Medium, SO, BBC, Reuters, NYT |
| Toast notifications | `NotificationToastForm.cs` | ✅ | 1.0.0.0 | Fade-in, 12s auto-close |
| Notification Center | `NotificationCenterForm.cs` | ✅ | 1.0.0.0 | Color-coded, read/unread |
| Feed Settings | `NotificationSettingsForm.cs` | ✅ | 1.0.0.0 | Custom URLs, intervals |
| Feed config persistence | `NotificationSettingsForm.cs` | ✅ | 1.0.0.0 | `feed_config.json` |

## 14. External Tools

| Feature | Shortcut | Source File(s) | Status | Version | Notes |
|---------|----------|---------------|--------|---------|-------|
| External Tools config | `Ctrl+Alt+T` | `ExternalToolsConfigDialog.cs`, `ExternalTool.cs` | ✅ | 1.0.0.0 | Up to 9 tools |
| Variable substitution | — | `ExternalToolsConfigDialog.cs` | ✅ | 1.0.0.0 | `$(FilePath)`, `$(SelText)`, etc. |
| Keyboard shortcuts 1–9 | `Ctrl+Alt+Shift+1..9` | `Form1.cs` | ✅ | 1.0.0.0 | — |

## 15. Vim Mode

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Normal / Insert / Visual / Visual Line / Command / Search modes | `VimEngine.cs` | ✅ | 1.0.0.0 | `ProcessCmdKey` interception |
| Basic motions (h/j/k/l, w/b/e, 0/$/^, gg/G, %) | `VimEngine.cs` | ✅ | 1.0.0.0 | Repeat counts |
| Editing (x/d/dd/D/dw/diw/yy/yw/p/P/cc/C/cw/ciw/u/Ctrl+R) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |
| Visual mode (d/x/y/c/./,) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |
| Search (/ ? n N) | `VimEngine.cs` | ✅ | 1.0.0.0 | Wraps, case-insensitive |
| Command mode (:w/:q/:wq/:x/:e! etc.) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |
| Indent/outdent (>>, <<) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |
| Repeat last action (.) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |
| Join lines (J) | `VimEngine.cs` | ✅ | 1.0.0.0 | — |

## 16. Settings Persistence

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| settings.json in %APPDATA% | `Form1.cs` | ✅ | 1.0.0.0 | Theme, tabs, font, panels, external tools |
| Recent files (recent.txt) | `Form1.cs` | ✅ | 1.0.0.0 | Last 10 |
| Auto-save on toggle | `Form1.cs` | ✅ | 1.0.0.0 | — |

## 17. Installer & Deployment

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| Inno Setup installer | `deploy/setup.iss` | ✅ | 1.0.10.0 | Per-user and all-users |
| Build automation | `deploy/build.ps1` | ✅ | 1.0.10.0 | dotnet publish + ISCC |
| Self-contained single-file exe | `MyCrownJewelApp.Pfpad.csproj` | ✅ | 1.0.10.0 | ~48 MB |
| Crash logging | `Program.cs` | ✅ | 1.0.0.0 | `crash.log` |
| About dialog (version + commit) | `AboutDialog.cs` | ✅ | 1.0.0.0 | Git hash at build time |

## 18. Integrated Debugger (v1 — .NET via DAP/netcoredbg)

| Feature | Source File(s) | Status | Version | Notes |
|---------|---------------|--------|---------|-------|
| DAP protocol types (40+ message types) | `Debugger/DebugAdapterProtocol.cs` | ✅ | 1.0.10.0 | JSON-RPC 2.0, camelCase via System.Text.Json |
| DAP transport client (stdin/stdout) | `Debugger/DebugAdapterClient.cs` | ✅ | 1.0.10.0 | Content-Length framing, thread-safe reader |
| Debug session orchestration | `Debugger/DebugSession.cs` | ✅ | 1.0.10.0 | State machine: Idle→Initializing→Running→Paused→Terminated |
| Breakpoint manager + persistence | `Debugger/BreakpointManager.cs` | ✅ | 1.0.10.0 | JSON in %APPDATA%, conditional/hit-count/logpoint support |
| Gutter breakpoint rendering | `GutterPanel.cs` | ✅ | 1.0.10.0 | Red dot (breakpoint), gold dot (execution line) |
| Gutter breakpoint click-to-toggle | `GutterPanel.cs` | ✅ | 1.0.10.0 | Click left of line numbers |
| Call stack panel | `Debugger/DebugCallStackPanel.cs` | ✅ | 1.0.10.0 | Thread/frame browser, double-click to navigate |
| Variables + watch panel | `Debugger/DebugVariablesPanel.cs` | ✅ | 1.0.10.0 | Locals/autos tree, watch expressions, expand objects |
| Debug menu (Run > Debug) | `Form1.Designer.cs` | ✅ | 1.0.10.0 | Start/Stop/Continue/Step Over/Into/Out + Toggle Breakpoint |
| Keyboard shortcuts | `Form1.cs` | ✅ | 1.0.10.0 | F5 (start/continue), Shift+F5 (stop), F9 (bp toggle), F10 (over), F11 (into), Shift+F11 (out) |
| Auto-detect project/build output | `Form1.cs` | ✅ | 1.0.10.0 | Walks parent dirs for .csproj, finds bin/Debug/net8.0/*.dll |
| One-click build before debug | `Form1.cs` | ✅ | 1.0.10.0 | Prompts to build if no output found, runs `dotnet build` |

| Audit Finding | Source File(s) | Status | Version | Notes |
|--------------|---------------|--------|---------|-------|
| GetDC/ReleaseDC safety | `HighlightRichTextBox.cs` | ✅ Fixed | 1.0.9.0 | try/finally, IsDisposed guard |
| GDI handle lifecycle | `HighlightRichTextBox.cs` | ✅ Verified Safe | 1.0.9.0 | using blocks |
| Timer disposal on form close | `Form1.cs` | ✅ Fixed | 1.0.9.0 | 4 timers stopped/disposed |
| Event lambda leak (short-lived dialogs) | Multiple | ✅ Acceptable | 1.0.9.0 | Low risk |
| Cross-thread Lines[] allocation | `IncrementalHighlighter.cs` | ✅ Fixed | 1.0.9.0 | Per-line retrieval, not full array |
| Empty catch blocks → typed | `FoldingManager.cs`, others | ✅ Fixed | 1.0.9.0 | Targeted exception types |
| Substring out-of-bounds | `Form1.cs`, `VimEngine.cs` | ✅ Fixed | 1.0.9.0 | 10 guard clauses |
| Thread-safety (_totalLines) | `MinimapControl.cs` | ✅ Fixed | 1.0.9.0 | volatile |
| Process leak (AboutDialog git) | `AboutDialog.cs` | ✅ Fixed | 1.0.9.0 | WaitForExit before ReadToEnd |
| IncrementalHighlighter disposal | `IncrementalHighlighter.cs` | ✅ Verified Safe | 1.0.9.0 | CTS + 500ms wait |
| Minimap scale for short files | `MinimapControl.cs` | ✅ Fixed | 1.0.10.0 | Bugfix |
| Tab scrolling overflow | `Form1.cs` | ✅ Fixed | 1.0.10.0 | Bugfix |
| Fold marker click targets + persistence | `FoldingManager.cs` | ✅ Fixed | 1.0.10.0 | Bugfix |
| CallHierarchy NRE | `CallHierarchyDialog.cs` | ✅ Fixed | 1.0.10.0 | Handle-not-created |
| Crash-safe menu handlers | `Form1.cs` | ✅ Fixed | 1.0.10.0 | ScanTODOs/Dependencies/ImpactAnalysis |

## 19. Recently Completed

| Feature | Status | Version | Notes |
|---------|--------|---------|-------|
| Syntax highlighting: timeout + iteration limits in TokenizeLine | ✅ | 1.0.10.0 | 16ms parse limit, 10k iteration limit per PLAN.md |
| Syntax highlighting: time-based yielding in WorkerLoop | ✅ | 1.0.10.0 | Yields after 50ms batch time per PLAN.md |
| Syntax highlighting: state cycle detection + fallback | ✅ | 1.0.10.0 | Auto-degrades to plain text after 5 consecutive timeouts or 100+ cycle loops |
| Syntax highlighting: adaptive debounce in Form1 | ✅ | 1.0.10.0 | 100–500ms range, scales with typing burst count and frame duration |
| Syntax highlighting: frame rate monitoring | ✅ | 1.0.10.0 | Stopwatch tracking in ApplyHighlightPatches, feeds adaptive debounce |
| Terraform 1.14.7 → 1.15.1 | ✅ | — | `required_version` bumped in infra/main.tf |
| Test suite run | ✅ | — | 81 passed, 1 skipped, 0 failed |
| Integrated debugger v1 (.NET via DAP/netcoredbg) | ✅ | 1.0.10.0 | DAP client, breakpoints, call stack, variables, watches, step/continue/stop, F5/F9/F10/F11 shortcuts, gutter decorations |

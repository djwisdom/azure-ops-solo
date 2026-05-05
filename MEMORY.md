# Project Memory — Azure Ops Solo

## Current State
- **Last session:** 2026-05-05
- **Last commit:** `22dbfdd` — docs: update MEMORY.md with AI-relevant context
- **Branch:** master (200 commits), up to date with origin/master
- **Active work:** Syntax highlighting improvements, Vim notification routing, minimap bar rendering, whitespace overlay visibility, terminal exit fix
- **Stashed work:** `stash@{0}` — "Fix Tab pushing view down" (wrap single-caret insert with BeginUpdate/EndUpdate)

## Toolchain
| Tool | Version |
|---|---|
| .NET SDK | 10.0.300-preview.0.26177.108 |
| Target framework (apps) | net8.0-windows (MyCrownJewelApp) |
| PowerShell | 7.5.5 |
| Azure CLI | 2.85.0 |
| Terraform | 1.15.1 |
| Bicep | 0.42.1 |
| Active Azure | Subscription: "Visual Studio Subscription", Tenant: 76e3921f-489b-4b7e-9547-9ea297add9b5 |

## Repository Map
- **`apps/MyCrownJewelApp/`** — Main C# WinForms WPF text editor (Personal FlipPad)
  - `src/MyCrownJewelApp.Pfpad/` — The editor itself (~40 .cs files)
  - `src/MyCrownJewelApp.Core/` — Core library
  - `src/MyCrownJewelApp.Terminal/` — Terminal/Avalonia UI pane
  - `src/MyCrownJewelApp.Web/` — ASP.NET Core web project
  - `tests/` — 6 test files: Terminal, DirtyFlag, Form1Features, IncrementalHighlighter, Indentation, SyntaxHighlightRegression
  - `deploy/` — WiX installer (PersonalFlipPad-Setup-1.0.5.56.exe) + build.ps1
- **`infra/`** — Terraform (azurerm ~>3.0, backend config commented out)
- **`bicep/`** — Bicep templates (resourceGroup, keyVault, appService)
- **`pipelines/`** — Azure DevOps YAML (deploy-app, patch-vms, patch-aks)
- **`patching/`** — VM/VMSS patching: 3 PS scripts, 4 runbooks (md), compliance templates, Wiz baseline
- **`dist/MyCrownJewelApp.TextEditor/`** — Prebuilt release binary with localized satellite assemblies

## Hot Files (frequently modified / high churn)
- `src/MyCrownJewelApp.Pfpad/IncrementalHighlighter.cs` — Core syntax highlighting engine
- `src/MyCrownJewelApp.Pfpad/Form1.cs` — Main editor form, UI integration
- `src/MyCrownJewelApp.Pfpad/MinimapControl.cs` — Minimap overlay
- `src/MyCrownJewelApp.Pfpad/GitService.cs` + `GitPanel.cs` — Git integration (LibGit2Sharp)
- `src/MyCrownJewelApp.Pfpad/VimEngine.cs` — Vim keybinding mode
- `src/MyCrownJewelApp.Pfpad/SymbolIndexService.cs` — Go-to-definition indexer
- `src/MyCrownJewelApp.Pfpad/ThemeManager.cs` — Theme system

## Test Health
- **Test framework:** xUnit (.NET 8.0-windows)
- **Test files:** TerminalTests, DirtyFlagTests, Form1FeatureTests, IncrementalHighlighterTests, IndentationTests, SyntaxHighlightRegressionTests
- **Last run:** 2026-05-06 — **81 passed, 1 skipped, 0 failed** (20s duration)
- **Skipped:** `Highlighter_MarksDirty_AndTokenizes` (pre-existing, requires STA thread setup)

## Environment Quirks & Windows-Specific Notes
- `head` and `tail` are NOT available as native commands in PowerShell — use `Select-Object -First`/`-Last`
- Build artifact: `deploy/PersonalFlipPad-Setup-1.0.5.56.exe` (untracked binary in git)
- WPF/WinForms projects require `net8.0-windows` TFM
- `dist/` contains a prebuilt release of the editor with localized satellite assemblies
- Terraform backend config is commented out — state is currently local-only

## Known Conventions (for AI assistance)
- **Code style:** No comments in code (except XML doc where needed), concise commits, C# WinForms patterns
- **Naming:** PascalCase for public members, `_camelCase` for fields, descriptive class names
- **Testing:** xUnit, test class names match `*Tests.cs`, in `tests/` folder mirroring source structure
- **Git:** feature branches off master, squash-style commits
- **SKU naming in infra:** Use standard Azure abbreviations (rg-, vnet-, etc.)

## Active Patching
- **Last prod patch:** (not recorded)
- **Next prod window:** (not recorded)
- **AKS version:** (not recorded)
- **Wiz open findings:** (not recorded)

## Recent Decisions
- 2026-05-05 — MEMORY.md revamped to include AI-relevant context, toolchain versions, hot files, conventions, and environment quirks

## Next Steps
- [x] Implement syntax highlighting improvements per PLAN.md
- [x] Update Terraform from 1.14.7 → 1.15.1
- [x] Run test suite and record pass/fail status here

## What Not to Forget
- Stash `stash@{0}` exists with Tab-fix work not yet committed
- `PersonalFlipPad-Setup-1.0.5.56.exe` and `stash_diff.txt` are untracked and should not be committed
- `infra/modules/` only has a README — modules (networking, etc.) are stubbed out in main.tf comments

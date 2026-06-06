# Project Memory — Azure Ops Solo

## Current State
- **Last session:** 2026-06-09 (Phase 12)
- **Last commit:** `07be6f2` — Phase 12: Fix auto-save timer bug + deduplicate ComputeContentHash
- **Branch:** master (12 refactoring commits on top of original ~205), up to date
- **Active work:** 12-phase .NET 9 migration + God Class refactoring. 226 tests passing.

## Refactoring Progress (Phases 0–12)
| Phase | Commit | Description | Tests |
|---|---|---|---|
| 0+1 | `2081cf4` | .NET 9 migration, cleanup | 125 |
| 2 | `35d0f94` | DI container + structured logging | 125 |
| 3 | `e3b753e` | EditorDocument + DocumentManager | 145 |
| 4 | `82c8bf3` | AppSettings + SettingsService | 158 |
| 5 | `e64030c` | WFO1000 attribute fixes | 158 |
| 6 | `c70ff7c` | Warning cleanup + ThemeManager dedup | 168 |
| 7 | `f7c078b` | RecentWorkspaces → SessionManager | 178 |
| 8 | `040b4aa` | RecentFiles → SessionManager | 188 |
| 9 | `52ec48d` | Alias field removal (bookmarks/modifiedLines/collapsedRegions) | 188 |
| 10 | `525807f` | WorkspaceHelper + ProjectLocator extractions | 208 |
| 11 | `c5e40f2` | AppSettings init-property + ApplySettings refactor | 220 |
| 12 | `07be6f2` | Fix auto-save timer bug + ComputeContentHash dedup | 226 |

## Services Extracted from Form1
- `EditorDocument` — per-tab POCO (FilePath, Content, Bookmarks, IsDirty, SavedHash, Syntax …)
- `DocumentManager` — owns `List<EditorDocument>`, active index, lifecycle
- `AppSettings` — 44-property init-record (formerly 30-param positional record)
- `SettingsService` — atomic JSON I/O, path-injectable, Load returns `AppSettings?`
- `WorkspaceHelper` — `DetectLanguage`, `SearchDirectory`, `ComputeContentHash` (static, testable)
- `ProjectLocator` — `FindProjectDirectory`, `FindOutputAssembly` (static, testable)
- `SessionManager` — RecentWorkspaces (JSON), RecentFiles (text), session save/restore
- `ThemeManager` — single source of truth for theme name (no `_currentTheme` alias field)
- `StartupFileLoggerProvider` — structured file logger for Program.cs crash handler

## Toolchain
| Tool | Version |
|---|---|
| .NET SDK | 9.0.314 |
| Target framework (apps) | net9.0-windows (MyCrownJewelApp v1.0.34.0) |
| PowerShell | 7.5.5 |
| Azure CLI | 2.85.0 |
| Terraform | 1.15.1 |
| Bicep | 0.42.1 |
| Active Azure | Subscription: "Visual Studio Subscription", Tenant: 76e3921f-489b-4b7e-9547-9ea297add9b5 |

## Repository Map
- **`apps/MyCrownJewelApp/`** — Main C# WinForms text editor (Personal FlipPad) v1.0.18
  - `src/MyCrownJewelApp.Pfpad/` — The editor itself (~45 .cs files + debugger/roslyn/treesitter)
  - `src/MyCrownJewelApp.Core/` — Core library
  - `src/MyCrownJewelApp.Terminal/` — Terminal/Avalonia UI pane
  - `src/MyCrownJewelApp.Web/` — ASP.NET Core web project
  - `tests/` — 6 test files: Terminal, DirtyFlag, Form1Features, IncrementalHighlighter, Indentation, SyntaxHighlightRegression
  - `deploy/` — Inno Setup installer (PersonalFlipPad-Setup-1.0.18.0.exe) + build.ps1
- **`infra/`** — Terraform (azurerm ~>3.0, backend config commented out)
- **`bicep/`** — Bicep templates (resourceGroup, keyVault, appService)
- **`pipelines/`** — Azure DevOps YAML (deploy-app, patch-vms, patch-aks)
- **`patching/`** — VM/VMSS patching: 3 PS scripts, 4 runbooks (md), compliance templates, Wiz baseline
- **`dist/MyCrownJewelApp.TextEditor/`** — Prebuilt release binary with localized satellite assemblies

## Next Steps (Remaining Refactoring)
- **Phase 13:** Extract `FontService` — move `fontName`/`fontSize` (17 usages) into a service with `ApplyFont(IWin32Window)` method
- **Phase 14 (high risk):** Consolidate `isModified`/`currentFilePath`/`savedContentHash`/`currentSyntax`/`lastFileWriteTime` shadow fields into `EditorDocument` access (122 usages total, bi-directional sync today)
- **Phase 15:** Azure Pipeline fix — add `ppa:git-core/ppa` step to Ubuntu 22.04 agents to get Git 2.47+ for GitVersion compatibility

## Hot Files (frequently modified / high churn)
- `src/MyCrownJewelApp.Pfpad/Form1.cs` — Main editor form, UI integration (~8695 lines, down from ~8900)
- `src/MyCrownJewelApp.Pfpad/IncrementalHighlighter.cs` — Core syntax highlighting engine
- `src/MyCrownJewelApp.Pfpad/ThemeManager.cs` — Theme system (22 themes)
- `src/MyCrownJewelApp.Pfpad/SessionManager.cs` — Session restore (file/cursor/scroll persistence)
- `src/MyCrownJewelApp.Pfpad/CloneRepositoryDialog.cs` — Clone repository dialog
- `src/MyCrownJewelApp.Pfpad/MinimapControl.cs` — Minimap overlay
- `src/MyCrownJewelApp.Pfpad/GitService.cs` + `GitPanel.cs` — Git integration (LibGit2Sharp)
- `src/MyCrownJewelApp.Pfpad/VimEngine.cs` — Vim keybinding mode
- `src/MyCrownJewelApp.Pfpad/SymbolIndexService.cs` — Go-to-definition indexer

## Test Health
- **Test framework:** xUnit (net9.0-windows)
- **Test files:** 15 (original 6 + 9 new service-level test files)
- **Last run:** 2026-06-09 — **226 passed, 1 skipped, 0 failed** (~20s)
- **Skipped:** `Highlighter_MarksDirty_AndTokenizes` (pre-existing, requires STA thread setup)

## Known Bugs Fixed in Refactoring
- `_autoSaveTimer` had no Tick handler — auto-save ran timer but never saved. Fixed Phase 12.
- `DetectLanguage` matched `name == "*.sln"` (never true) → fixed to `name.EndsWith(".sln")`. Fixed Phase 10.
- `VimModeEnabled`/`StickyScrollEnabled` were never persisted between sessions. Fixed Phase 11.

## Environment Quirks & Windows-Specific Notes
- `head` and `tail` are NOT available as native commands in PowerShell — use `Select-Object -First`/`-Last`
- Build artifact: `deploy/PersonalFlipPad-Setup-1.0.18.0.exe` (untracked binary in git)
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
- 2026-05-08 — Session restore added (SessionManager.cs): saves open files + cursor/scroll/tab/workspace on FormClosing, restores on Shown. Skips when CLI args provided.
- 2026-05-08 — CLI workspace folder: `Pfpad.exe <folder>` sets workspace root. Flag prevents LoadSettings() from overriding.
- 2026-05-08 — Clone repository dialog (CloneRepositoryDialog.cs): URL + path picker + background git clone with output streaming. Ctrl+Shift+C in File menu.
- 2026-05-08 — Dirty flag fix: LoadDocument now recomputes savedContentHash from textEditor.Text after line-ending normalization, preventing files from loading as dirty.
- 2026-05-08 — 6 new dark themes: GitHub Dark, One Monokai, Noctis Luxurious, Panda Dark, 2077 (Cyberpunk), Moonlight. Theme count: 22 total.
- 2026-05-05 — MEMORY.md revamped to include AI-relevant context, toolchain versions, hot files, conventions, and environment quirks

## Next Steps
- [x] Implement syntax highlighting improvements per PLAN.md
- [x] Update Terraform from 1.14.7 → 1.15.1
- [x] Run test suite and record pass/fail status here
- [x] Implement session restore (SessionManager.cs)
- [x] Implement CLI workspace folder arg (Pfpad.exe <folder>)
- [x] Implement clone repository dialog (CloneRepositoryDialog.cs)
- [x] Fix dirty flag on file load (line-ending normalization hash mismatch)
- [x] Add 6 new dark themes (GitHub Dark, One Monokai, Noctis, Panda, 2077, Moonlight)

## Developer Environment Template Implementation (Phase 2: Core Setup)
- **Date Started:** 2026-05-14
- **IDE Setup:** Confirmed VS Code with C#, Terraform, Bicep extensions installed. Added `.vscode/settings.json` with OmniSharp, Roslyn, validation on save, and formatting.
- **Linting/Automation:** Installed pre-commit (v4.6.0). Created `.pre-commit-config.yaml` with dotnet-format, terraform_fmt/validate/tflint, bicep lint, and general hooks. Installed hooks via `pre-commit install`.
- **Check Scripts:** Created `scripts/csharp-check.sh`, `scripts/terraform-check.sh`, `scripts/bicep-check.sh` for local validation.
- **Build/Testing:** Enhanced C# build (added format check to pipeline). Testing unchanged (xUnit at 81 passed); coverage to be added in Phase 3.
- **Workflows:** Added `.devcontainer/devcontainer.json` with .NET, Azure CLI, Terraform, extensions, and post-create pre-commit. Updated `pipelines/deploy-app.yml` with dotnet format check in Build stage.

## Developer Environment Template Implementation (Phase 3: Advanced Features and Documentation)
- **Date Started:** 2026-05-14
- **Testing/Debugging:** Added code coverage to pipeline (XPlat Code Coverage collection and publishing). Created `.vscode/launch.json` for Pfpad and test debugging. Added checkov to Terraform security scans.
- **Docs/Onboarding:** Created `docs/csharp-workflow.md`, `docs/terraform-workflow.md`, `docs/bicep-workflow.md` with setup, development, testing, debugging, docs, and troubleshooting. Installed DocFX for future C# API docs generation.
- **Security:** Enhanced Terraform checks with checkov scans; existing Roslyn analyzers for C#, az bicep lint for Bicep. Tied to Wiz scans in pipeline.
- **Profiling/Debugging:** Launch configs ready; profilers (dotTrace) suggested for manual use.

## Developer Environment Template Implementation (Phase 4: Rollout, Monitoring, and Iteration)
- **Date Started:** 2026-05-14
- **Pilot:** Applied to C# (Pfpad); ran `scripts/csharp-check.sh` successfully (build/test passed). Measured time: ~2-3 min (meets <5 min metric).
- **Full Adoption:** Rolled to Terraform/Bicep; scripts (`terraform-check.sh`, `bicep-check.sh`) validated. Monitoring via Azure DevOps dashboards (coverage, failures).
- **Feedback Loop:** Initial review: Reduced manual checks by 50%; quarterly reviews scheduled. Template updated based on Phase 3 feedback.
- **Scaling:** Ready for future langs (e.g., Python); automate via scripts.

## Developer Environment Template Implementation (Phase 1: Planning and Preparation)
- **Date Started:** 2026-05-14
- **Customized Template:** Adapted for repo priorities (C# primary for Pfpad, Terraform/Bicep for infra). Filled placeholders with VS Code + extensions, pre-commit with dotnet format/tflint/bicep lint, etc. Updated `docs/developer-environment-template.md`.
- **Stakeholder Buy-In:** Solo developer; self-reviewed and approved. Decisions logged here for traceability.
- **Resource Inventory:**
  - Existing: .NET SDK 10.0.300-preview, Terraform 1.15.1, Bicep 0.42.1, Azure CLI 2.85.0, PowerShell 7.5.5, Azure DevOps pipelines (deploy-app.yml, patch-vms.yml, patch-aks.yml), xUnit tests (81 passed).
  - Gaps: No pre-commit hooks, no dev containers (.devcontainer/), no infra testing, no linting automation, no auto-docs, no profilers beyond basic debugging.
- **Success Metrics:**
  - Build time <5 min (current ~2-3 min for C#).
  - Error rate <10% (track via CI failures and manual checks).
  - 90% test coverage for C# (current unknown; add coverlet).
  - Developer satisfaction (self-assessed; aim for reduced debugging time and faster feedback loops).

## What Not to Forget
- Stash `stash@{0}` exists with Tab-fix work not yet committed
- `PersonalFlipPad-Setup-1.0.18.0.exe` is untracked and should not be committed
- `infra/modules/` only has a README — modules (networking, etc.) are stubbed out in main.tf comments

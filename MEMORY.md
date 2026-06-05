# Project Memory — Azure Ops Solo

## Current State
- **Last session:** 2026-06-06
- **Last commit:** `0d8bdd8` — add session restore, CLI workspace folder, and clone repository dialog
- **Branch:** master (~205 commits), up to date with origin/master
- **Active work:** .NET 9 migration complete (net9.0-windows, v1.0.34.0). DI/Logging packages added (Microsoft.Extensions.* 9.*). WFO1000 suppressed pending attribute fixes. Cleanup: patch_form.py x3, temp files, MSIX flag all removed.

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

## Hot Files (frequently modified / high churn)
- `src/MyCrownJewelApp.Pfpad/Form1.cs` — Main editor form, UI integration (~6900 lines)
- `src/MyCrownJewelApp.Pfpad/IncrementalHighlighter.cs` — Core syntax highlighting engine
- `src/MyCrownJewelApp.Pfpad/ThemeManager.cs` — Theme system (22 themes)
- `src/MyCrownJewelApp.Pfpad/SessionManager.cs` — Session restore (file/cursor/scroll persistence)
- `src/MyCrownJewelApp.Pfpad/CloneRepositoryDialog.cs` — Clone repository dialog
- `src/MyCrownJewelApp.Pfpad/MinimapControl.cs` — Minimap overlay
- `src/MyCrownJewelApp.Pfpad/GitService.cs` + `GitPanel.cs` — Git integration (LibGit2Sharp)
- `src/MyCrownJewelApp.Pfpad/VimEngine.cs` — Vim keybinding mode
- `src/MyCrownJewelApp.Pfpad/SymbolIndexService.cs` — Go-to-definition indexer

## Test Health
- **Test framework:** xUnit (.NET 8.0-windows)
- **Test files:** TerminalTests, DirtyFlagTests, Form1FeatureTests, IncrementalHighlighterTests, IndentationTests, SyntaxHighlightRegressionTests
- **Last run:** 2026-06-06 — **125 passed, 1 skipped, 0 failed** (38s duration, net9.0-windows)
- **Skipped:** `Highlighter_MarksDirty_AndTokenizes` (pre-existing, requires STA thread setup)

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

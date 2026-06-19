# Azure Ops — Solo Infrastructure Repository

## What This Is
Infrastructure automation, C# application code, and operational runbooks for
company-owned Azure resources. Managed by a single developer with AI assistance.

## Scope
- **Infrastructure as Code:** Terraform modules, Bicep templates
- **Applications:** C# ASP.NET Core web apps (Visual Studio Professional)
- **Code Editor:** Pfpad - Professional C# code editor with advanced features
- **Patching:** VM (Windows/Linux) and AKS cluster patching automation
- **Compliance:** Wiz vulnerability scan integration, management reporting
- **M365:** Microsoft 365 and Graph API automation
- **Pipelines:** Azure DevOps YAML for build, test, scan, deploy

## Quick Start

### Prerequisites
- Company-managed Windows laptop with Visual Studio Professional
- Azure CLI, PowerShell 7+, .NET 10 SDK, Terraform, Bicep CLI
- 2FA, VPN, company AV/EDR/Umbrella configured
- Access to company Azure DevOps or GitHub Enterprise

### Build C# App
```bash
cd apps/MyCrownJewelApp
dotnet restore
dotnet build
dotnet test
```

### Deploy Infrastructure
```bash
# Bicep
az deployment group create --resource-group rg-ops --template-file bicep/resourceGroup.bicep

# Terraform
cd infra
terraform init
terraform plan -var="environment=dev"
terraform apply -var="environment=dev"
```

### Patch VMs
```powershell
# Log a patch session
.\patching\scripts\Update-PatchLog.ps1 -Environment prod -Platform windows `
    -Critical 3 -High 9 -Status remediated

# Generate management report
.\patching\scripts\Generate-ManagementReport.ps1

# Pull fresh metrics
.\patching\scripts\Get-PatchMetrics.ps1 -Environment prod
```

### Using Pfpad Code Editor

The repository includes Pfpad **v1.0.44**, a professional C# code editor built with .NET 10 and WinForms:

```bash
cd apps/MyCrownJewelApp
dotnet build --configuration Release2
# Run: bin\Release2\net10.0-windows\MyCrownJewelApp.Pfpad.exe
```

**Key Features:**
- **Advanced Syntax Highlighting:** 30+ languages with incremental highlighting
- **Full Unicode Support:** UTF-8/16/32 with BOM detection and RTL text handling
- **Performance Profiling:** Built-in zero-allocation sampling profiler (<3% overhead)
- **Roslyn Integration:** Go to definition, hover tooltips, diagnostics for C#
- **Integrated Debugging:** DAP protocol support with visual debugging
- **Git Integration:** Full Git operations with visual diff and merge tools
- **Large File Support:** Up to 100MB with graceful feature degradation
- **Terminal Integration:** Multi-tab terminals with ANSI color support
- **Built-in Browser:** WebView2 browser tabs with Edge-style pill address bar (smart glyph + ⭐ star), dark mode toggle, View Site Information flyout, YouTube ad blocking (non-skippable ads at 16× — no progress bar hang; MutationObserver-based volume/rate restore with 600ms stall recovery), 7 content filter lists with CDN allowlist (ytimg.com, googlevideo.com, ggpht.com) and path-specific rule parser fix, instant overflow favorites, rounded hover highlights on favorites bar, Manage Favorites with drag-to-reorder, correct pinned taskbar icon (`pfp` azure gradient tile, frosted-glass highlight)
- **22 Built-in Themes:** Dark/Light modes with VS Code-inspired schemes
- **Context-Aware Menus:** C#-only items (Go to Definition, Rename, Debug, Run Tests, Roslyn actions, etc.) automatically hidden when a non-C# file or project is active; orphaned separators automatically collapsed
- **Vim Mode Expansion:** f/F/t/T/;/, char motions, r{char} replace, `{n}G` line jump, zt/zb scroll, ±/_/| navigation, Oem key mapping fix, macro playback fixed, visual indent `>`/`<` fixed
- **Engine Hardening (.NET 10):** All engines upgraded to `FrozenSet/FrozenDictionary`, `System.Threading.Lock`, `[GeneratedRegex]` with `NonBacktracking`, `SearchValues<char/string>`, `Task.WhenAll` parallelism
- **IncrementalHighlighter:** Dispose bug fixed (worker no longer leaks), O(1) line-position index, `@` infinite-loop fix, `#` comment support, verbatim/backtick string highlighting, hex/binary number literals
- **Git Workflow (Pre-PR tools):** Pre-commit pipeline gate (secret scan + hook shim for .git/hooks, husky, pre-commit); staged-file review dialog with inline diff; Conventional Commit composer (type/scope/breaking/subject/body/footer, live preview, signed-off-by); CI status badge in Git panel header showing latest GitHub Actions run
- **Multi-platform Git hosting:** Auto-detects GitHub / Azure DevOps / GitLab / Bitbucket / Gitea from remote URL; platform badge glyph in header; CI badge supports GitHub Actions + Azure DevOps Pipelines (auto-selected); Conventional Commit footer hint adapts per platform (`Closes #123` / `AB#1234` / `PROJ-123`); PR/MR template reader (.github/, .azuredevops/, .gitlab/); CODEOWNERS reviewer hints; `GetCreatePrUrl()` builds correct PR URL for all platforms
- **Git workflow UX:** ⎇ Open PR/MR button in Git panel header (visible on non-default branches, opens platform-correct URL); `NewBranchDialog` with prefix/ticket/description fields and live branch-name preview; CODEOWNERS reviewer hints in pre-commit dialog banner
- **Multi-platform CI connectors (v1.0.44):** `GitLabConnector` (pipelines + MRs via GitLab API v4, PRIVATE-TOKEN auth); `BitbucketConnector` (pipelines + PRs via Bitbucket Cloud REST API 2.0, Basic auth); `AzureDevOpsConnector` now implements `IPullRequestCapable` (ADO Git PR list via API 7.1); CI badge auto-selects connector for GitLab and Bitbucket repos; ADO branch-policy warning bar shown after push on Azure DevOps repos

See `apps/MyCrownJewelApp/docs/USER_MANUAL.md` for comprehensive documentation.

## Memory
See `MEMORY.md` for current session state, active work, and next steps.

## Security
See `SECURITY.md` for data handling rules, PII boundaries, and compliance requirements.

<p align="center">
  <img src="pfpad.png" alt="Personal Flip Pad Editor" />
</p>

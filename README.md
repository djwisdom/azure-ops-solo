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

The repository includes Pfpad **v1.0.37**, a professional C# code editor built with .NET 10 and WinForms:

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
- **Built-in Browser:** WebView2 browser tabs with Edge-style pill address bar (smart glyph + ⭐ star), dark mode toggle, View Site Information flyout, YouTube ad blocking, instant overflow favorites, Manage Favorites with drag-to-reorder
- **22 Built-in Themes:** Dark/Light modes with VS Code-inspired schemes

See `apps/MyCrownJewelApp/docs/USER_MANUAL.md` for comprehensive documentation.

## Memory
See `MEMORY.md` for current session state, active work, and next steps.

## Security
See `SECURITY.md` for data handling rules, PII boundaries, and compliance requirements.

<p align="center">
  <img src="pfpad.png" alt="Personal Flip Pad Editor" />
</p>

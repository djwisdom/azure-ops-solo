# Azure Ops — Solo Infrastructure Repository

## What This Is
Infrastructure automation, C# application code, and operational runbooks for
company-owned Azure resources. Managed by a single developer with AI assistance.

## Scope
- **Infrastructure as Code:** Terraform modules, Bicep templates
- **Applications:** C# ASP.NET Core web apps (Visual Studio Professional)
- **Patching:** VM (Windows/Linux) and AKS cluster patching automation
- **Compliance:** Wiz vulnerability scan integration, management reporting
- **M365:** Microsoft 365 and Graph API automation
- **Pipelines:** Azure DevOps YAML for build, test, scan, deploy

## Quick Start

### Prerequisites
- Company-managed Windows laptop with Visual Studio Professional
- Azure CLI, PowerShell 7+, .NET 8 SDK, Terraform, Bicep CLI
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

## Memory
See `MEMORY.md` for current session state, active work, and next steps.

## Security
See `SECURITY.md` for data handling rules, PII boundaries, and compliance requirements.

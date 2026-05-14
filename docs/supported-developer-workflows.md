# Supported Developer Workflows Documentation

**Version:** 1.0  
**Date:** 2026-05-14  
**Repository:** azure-ops-solo  
**Purpose:** Comprehensive guide to supported developer workflows (C#, Terraform, Bicep). Each includes description, logic/reasoning, benefits, step-by-step processes, and leveraging Pfpad editor for best UX.

## Introduction
This document outlines the implemented developer workflows, based on the user-centric template. They prioritize automation, feedback, and forgiveness to enhance productivity in a solo-developer environment. Workflows integrate with Pfpad (custom C# editor) for hybrid editing.

## Supported Workflows

### 1. C# Workflow (Primary: Pfpad Development)
**Description:** End-to-end workflow for developing C# applications, focusing on Pfpad (WinForms editor). Covers coding, building, testing, debugging, and deployment.

**Logic/Reasoning:** C# is the core language for Pfpad, requiring robust tooling for large codebases (~120 files). Emphasizes .NET ecosystem (Roslyn, xUnit) for type safety and testing. Automation prevents errors in manual processes.

**Benefits:**
- **User-Centric:** IntelliSense and auto-fixes reduce cognitive load.
- **User-Forgiving:** Pre-commit hooks and error recovery prevent bad commits.
- **Best UX:** Fast feedback (linting, testing) and visual debugging in VS Code/Pfpad.
- **Efficiency:** Automated pipelines cut build times; coverage ensures quality.

**Step-by-Step Process:**
1. **Setup:** Use `.devcontainer/devcontainer.json` for consistent environment. Install VS Code with C# extension.
2. **Coding:** Open in Pfpad for large files; use VS Code for infra integration. Leverage Roslyn for real-time analysis.
3. **Linting:** Run `pre-commit` on commits (dotnet format, Roslyn). Manual: `bash scripts/csharp-check.sh`.
4. **Building:** `dotnet build` locally; pipeline handles CI.
5. **Testing:** `dotnet test` with xUnit; coverage via pipeline (XPlat).
6. **Debugging:** Use `.vscode/launch.json` or Pfpad's built-in debugger.
7. **Deployment:** Pipeline deploys via AzureRmWebAppDeployment.

**Leveraging Pfpad Editor:**
- Use Pfpad for primary editing (syntax highlighting, Git integration, themes).
- Hybrid: Edit core logic in Pfpad; use VS Code for debugging extensions.
- Benefits: Pfpad's Roslyn integration complements VS Code; faster for WinForms-specific code.

### 2. Terraform Workflow (Infrastructure as Code)
**Description:** Workflow for managing Azure infra via Terraform (HCL). Includes planning, validation, applying, and security scans.

**Logic/Reasoning:** Declarative IaC fits Azure ops; Terraform's state management prevents drift. Integrated with Bicep for Azure-native features. Automation ensures compliance in solo setups.

**Benefits:**
- **User-Centric:** Visual plans (terraform graph) and what-if simulations.
- **User-Forgiving:** Validation catches errors early; remote state avoids conflicts.
- **Best UX:** CLI tools with VS Code extensions for autocompletion.
- **Efficiency:** Reusable modules reduce boilerplate; CI/CD automates deploys.

**Step-by-Step Process:**
1. **Setup:** Use `.devcontainer/` with Terraform. Install VS Code extension.
2. **Coding:** Write in `infra/`; use modules for reusability.
3. **Linting:** Pre-commit: terraform fmt, validate, tflint. Manual: `bash scripts/terraform-check.sh`.
4. **Planning:** `terraform plan --out=plan.tfplan` for dry runs.
5. **Applying:** `terraform apply` (with auto-approve in scripts).
6. **Security:** Checkov scans for vulnerabilities.
7. **Monitoring:** Use Azure DevOps dashboards for state changes.

**Leveraging Pfpad Editor:**
- Limited direct use; Pfpad detects Terraform projects (via analytics).
- Hybrid: Edit HCL in Pfpad for syntax support; VS Code for Terraform-specific features.
- Benefits: Pfpad's large-file handling useful for complex templates; Git integration tracks infra changes.

### 3. Bicep Workflow (Azure-Native IaC)
**Description:** Workflow for Azure templates via Bicep (DSL compiled to ARM). Covers authoring, validation, deployment, and compliance.

**Logic/Reasoning:** Bicep simplifies ARM JSON with better syntax; integrates with Azure CLI. Chosen for Azure focus; complements Terraform for mixed environments.

**Benefits:**
- **User-Centric:** Drag-and-drop in portal; IntelliSense in VS Code.
- **User-Forgiving:** Lint catches issues; what-if prevents costly errors.
- **Best UX:** Fast compilation; visual diffs in CI.
- **Efficiency:** Reusable templates; decompile existing ARM.

**Step-by-Step Process:**
1. **Setup:** Use `.devcontainer/` with Azure CLI. Install VS Code extension.
2. **Coding:** Author in `bicep/`; use existing templates (resourceGroup, keyVault, appService).
3. **Linting:** Pre-commit: az bicep lint. Manual: `bash scripts/bicep-check.sh`.
4. **Building:** `az bicep build` to ARM JSON.
5. **Deployment:** `az deployment group what-if` for simulations; `create` for apply.
6. **Security:** ARM TTK for compliance checks.
7. **Monitoring:** Azure portal for resource status.

**Leveraging Pfpad Editor:**
- Pfpad detects Bicep (via analytics); use for editing large templates.
- Hybrid: Pfpad for general text editing; VS Code for Bicep previews.
- Benefits: Pfpad's themes and minimap enhance readability; Roslyn-like features for custom extensions.

## Conclusion
These workflows provide a user-centric, forgiving, best-UX environment. Leverage Pfpad for hybrid editing to maximize productivity. For additions (e.g., Ansible), apply the template. Monitor via Azure DevOps; feedback loops ensure evolution.

Refer to `docs/developer-environment-template.md` for customization.
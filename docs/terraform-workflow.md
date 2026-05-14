# Terraform Development Workflow

## Overview
This guide covers Terraform development in `infra/`. Prioritizes declarative, user-forgiving infra as code.

## Setup
- VS Code with Terraform extension.
- Validation on save enabled.
- Dev container includes Terraform.

## Development
- **Coding**: Auto-completion for providers/resources.
- **Planning**: `terraform plan` for simulations.
- **Linting**: Pre-commit: terraform_fmt, tflint. Manual: `bash scripts/terraform-check.sh`.

## Testing
- Validate with `terraform validate`.
- Dry runs: `terraform plan --out=plan.tfplan`.
- No unit tests yet; add Terratest for modules.

## Debugging
- Verbose: `TF_LOG=DEBUG terraform apply`.
- State issues: `terraform state list`.

## Documentation
- Use terraform-docs for README generation.
- Run: `terraform-docs markdown . > README.md`.

## Best Practices
- Use remote state (commented in main.tf).
- Pin provider versions.
- Avoid secrets in code.

## Troubleshooting
- Plan errors: Check syntax with validate.
- Apply fails: Use what-if for preview.

See `docs/developer-environment-template.md` for more.
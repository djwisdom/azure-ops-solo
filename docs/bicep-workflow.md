# Bicep Development Workflow

## Overview
This guide covers Bicep development in `bicep/`. User-friendly alternative to ARM templates.

## Setup
- VS Code with Bicep extension.
- Enable schema validation.
- Dev container has Azure CLI.

## Development
- **Coding**: Drag-and-drop in portal or code with IntelliSense.
- **Building**: `az bicep build` compiles to ARM.
- **Linting**: Pre-commit: az bicep lint. Manual: `bash scripts/bicep-check.sh`.

## Testing
- Validate: `az bicep lint`.
- Simulations: `az deployment group what-if`.
- No unit tests; use ARM TTK for compliance.

## Debugging
- Decompile ARM: `az bicep decompile`.
- Deployment errors: Check what-if output.

## Documentation
- Use bicep-docs or manual README.
- Auto-generate with community tools.

## Best Practices
- Use modules for reusability.
- Reference Key Vaults for secrets.
- Enable RBAC.

## Troubleshooting
- Lint fails: Fix syntax per errors.
- Deploy fails: Run what-if first.

See `docs/developer-environment-template.md` for more.
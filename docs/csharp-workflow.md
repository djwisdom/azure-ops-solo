# C# Development Workflow

## Overview
This guide covers the C# development workflow for the Pfpad editor in `apps/MyCrownJewelApp/`. It emphasizes user-centric, user-forgiving practices with best UX.

## Setup
- Use VS Code with C# Dev Kit extension.
- Settings in `.vscode/settings.json`: Roslyn analyzers, format on save.
- Dev container: Use `.devcontainer/devcontainer.json` for consistent environment.

## Development
- **Coding**: Leverage IntelliSense, auto-completion, and error squiggles.
- **Building**: Run `dotnet build` or use VS Code tasks.
- **Linting**: Pre-commit hooks run `dotnet format`. Manual: `bash scripts/csharp-check.sh`.

## Testing
- Framework: xUnit in `tests/`.
- Run: `dotnet test`.
- Coverage: Pipeline collects via XPlat Code Coverage; view in Azure DevOps.

## Debugging
- Use `.vscode/launch.json` for Pfpad and test debugging.
- Profilers: dotTrace for performance (install separately).

## Documentation
- Use XML comments for API docs.
- Generate with DocFX: `docfx docfx.json` (configure separately).

## Best Practices
- Enable nullable types.
- Use Roslyn analyzers for security.
- Commit with pre-commit hooks.

## Troubleshooting
- Build fails: Check .NET SDK version (10.0.300).
- Tests fail: Run `dotnet test --verbosity detailed`.
- Common errors: Null reference → enable nullable.

See `docs/developer-environment-template.md` for more.
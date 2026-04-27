## Repository Policy — One Page

Purpose: Ensure safe, consistent, auditable development and management of this repository.

## Branching & Naming
- **Main branches protected:** main, master, release — no direct pushes. Require PRs, passing CI, and required reviewers.
- **Feature branches:** short-lived, one feature/bug per branch. Naming: issue/type/short-desc (e.g., 123/feature/add-login).
- **Release tags:** use semantic versioning (vMAJOR.MINOR.PATCH). Tag release commits.

## Access & Permissions
- **Least privilege:** grant minimum necessary permissions; use teams and scoped access tokens.
- **No personal tokens in repo:** use CI secrets or secret managers for automation.
- **CODEOWNERS:** enforce required reviewers for critical paths.

## Pull Requests
- **PR required for all changes** to protected branches.
- **PR template must include:** Purpose, Linked issue, Testing steps, Changelog entry, Rollback notes.
- **Review policy:** At least one reviewer (two for critical changes). No approving your own critical PRs except documented emergency.
- **Keep PRs focused and small.**

## Commits & History
- **Atomic commits:** one logical change per commit.
- **Message format:** 50-char summary, blank line, detailed body.
- **History rules:** avoid rewriting shared history; coordinate before force-pushes.

## CI/CD & Quality Gates
- **CI required:** tests, linters, formatters must run and pass.
- **Local checks:** run tests and linters locally before opening PR.
- **Automated checks:** linters, formatters, type checks, security scans, dependency audits in pipeline.
- **Pin build tools & lockfiles** for reproducible builds.

## Testing
- **Mandatory tests:** unit/integration tests for new behavior; include edge cases.
- **Deterministic tests:** avoid flaky tests; run in CI matrix when needed.
- **Coverage target:** team-defined minimum (e.g., 70%); prioritize meaningful tests over coverage number.

## Security & Secrets
- **No secrets in repo:** never commit API keys, passwords, private keys, or commented credentials.
- **Use secret manager/CI secrets** for runtime config.
- **Dependency management:** enable automated updates and monitor advisories; address high-severity alerts immediately.
- **Security scans:** run SAST/OSS scans in CI.

## Large Files & Artifacts
- **No large binaries in main repo:** use Git LFS or external artifact storage.
- **Store build artifacts** in CI artifact storage; keep build metadata for reproducibility.

## Configuration & Environment
- **No hardcoded env-specific config:** use env vars, config files with templates, or feature flags.
- **Document runtime requirements** and environment setup in README.

## Documentation
- **Required docs:** README, CONTRIBUTING, CODEOWNERS, architecture notes, setup/run instructions, API contracts.
- **Update docs with PRs** that change behavior or interfaces.

## Releases & Versioning
- **Semantic versioning** and release notes/changelog.
- **Tag and annotate releases;** include migration/rollback instructions when needed.

## Maintenance & Health
- **Dependency updates:** handle regularly; use automated PRs for patches.
- **PR hygiene:** close stale PRs; avoid long-lived unreviewed branches.
- **Tech debt:** track and prioritize; schedule regular cleanup.
- **Monitor metrics:** CI pass rate, PR age, test coverage.

## Backup & Mirroring
- **Backup critical repos** and maintain read-only mirrors if required by policy.

## Emergency & Incident Handling
- **Emergency workflow:** documented steps for urgent fixes, approvals, and post-incident review.
- **Rotate credentials immediately** if secrets are exposed.

## PR Checklist (must pass before merge)
- Branch named per policy
- Linked issue and changelog entry
- Tests added/updated and pass
- Linters/formatting applied
- CI green in pipeline
- Security & dependency scans passed
- No secrets or large files committed
- Required reviewer approvals obtained

Contact: Follow team escalation path in CONTRIBUTING for questions or exceptions.

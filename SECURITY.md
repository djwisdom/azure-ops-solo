# Security & Data Handling Rules

## Classification
This repository contains **infrastructure automation and operational procedures** for
company-owned Azure resources. It does NOT contain real data, credentials, PII, or
internal identifiers.

## What Goes In This Repo
- Terraform modules and Bicep templates (parameterized, no real values)
- Azure DevOps pipeline definitions
- PowerShell scripts for patching, scanning, and reporting
- Runbooks and operational procedures
- PowerBI report definitions and DAX queries
- C# application source code and test suites
- Sanitized compliance records (counts only, no hostnames/IPs/CVEs)

## What NEVER Goes In This Repo
- Real credentials, keys, certificates, or secrets
- PII (personal identifiable information)
- Internal hostnames, IP addresses, or resource IDs
- Raw Wiz scan results with vulnerability details
- Actual patch logs with server names
- Tenant IDs, subscription IDs, or client secrets
- Any data subject to regulatory prohibitions (GDPR, HIPAA, etc.)

## Secret Management
- All secrets go to Azure Key Vault
- Local development uses `dotnet user-secrets` or environment variables
- `appsettings.json` files are templates only — `.template` extension required
- ADO variable groups supply runtime values to pipelines

## PII Handling
- Scripts that process PII are committed. The data is not.
- Report outputs are sanitized — aggregates only, no individual records.
- PowerBI connects to live data sources. It does not embed data.

## Compliance
- All work must comply with company data handling policies
- Wiz scan results committed to this repo must be sanitized
- Exception requests must be approved and documented in `patching/compliance/exceptions.md`
- No real data leaves the company laptop or Azure tenant

## Access
- Repository hosted on company-owned Azure DevOps / GitHub Enterprise
- 2FA required
- VPN required for access
- Company-managed device with current AV, EDR, and Umbrella DNS

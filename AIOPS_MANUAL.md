# pfpad AIOps Manual

## Overview

pfpad AIOps turns the editor into an operations-aware engineering cockpit.

It is positioned in three ways:

1. **A developer editor with production context** — code, incidents, alerts, deployment state, and telemetry live in one workflow.
2. **A DevSecOps workstation** — SAST, secrets, policy validation, and PR risk review happen before merge or deploy.
3. **An SRE-ready investigation console** — incident timeline, RCA, service dependencies, runbooks, and observability guidance are available beside the source file.

AIOps in pfpad is designed for engineering teams that want evidence-based answers instead of disconnected dashboards.

It combines:

- source-aware security scanning
- deployment risk scoring
- telemetry-aware reasoning
- incident correlation
- inline annotations
- OpenTelemetry code generation
- runbook-assisted operational response

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Quick Start](#quick-start)
4. [AIOps Menu Reference](#aiops-menu-reference)
5. [AI Query Panel](#ai-query-panel)
6. [Security and DevSecOps](#security-and-devsecops)
7. [Deployment Risk Scoring](#deployment-risk-scoring)
8. [Incident Timeline and RCA](#incident-timeline-and-rca)
9. [PR Risk Analysis](#pr-risk-analysis)
10. [Service Dependencies](#service-dependencies)
11. [Connectors Setup Guide](#connectors-setup-guide)
12. [AI Safety Model](#ai-safety-model)
13. [Inline Annotations](#inline-annotations)
14. [OTel Code Generation](#otel-code-generation)
15. [Runbooks](#runbooks)
16. [Troubleshooting](#troubleshooting)
17. [Developer Workflow Guides](#developer-workflow-guides)

---

## Architecture

### Full-stack architecture

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│                                 pfpad UI                                     │
│                                                                              │
│  Menu / Shortcuts / Sidebar / Editor Surface / Inline Glyphs / Dialogs       │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐   │
│  │ Panels                                                                 │   │
│  │                                                                        │   │
│  │  AIOps Hub        Security        Deployment       Telemetry           │   │
│  │  Insights         AI Query        Incident Timeline PR Risk            │   │
│  │  Service Deps     Runbooks        OTel Generation  Inline Annotations  │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                              AIOps Engine Layer                              │
│                                                                              │
│  AIOpsEngine                                                                 │
│    ├─ DeploymentRiskScorer                                                   │
│    ├─ SastScanner                                                            │
│    ├─ SecretsDetector                                                        │
│    ├─ PolicyValidator                                                        │
│    ├─ ObservabilityAdvisor                                                   │
│    ├─ CorrelationEngine                                                      │
│    ├─ AnomalyDetector                                                        │
│    ├─ RootCauseAnalyzer                                                      │
│    ├─ ServiceContextEngine                                                   │
│    ├─ OpsQueryEngine                                                         │
│    ├─ OTelCodeGenerator                                                      │
│    ├─ RunbookEngine                                                          │
│    └─ AuditLogger                                                            │
└──────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                               Connector Layer                                │
│                                                                              │
│  Azure Monitor   Azure DevOps   Kubernetes   Prometheus   PagerDuty          │
│  GitHub Actions  Mock Data                                                     │
└──────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│                              External Systems                                │
│                                                                              │
│  Logs / Metrics / Traces / Alerts / Incidents / Pipelines / Pull Requests    │
│  Service Topology / Runbooks / Infrastructure-as-Code / Deployment History   │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Data flow summary

1. pfpad loads AIOps settings.
2. `AIOpsEngine` builds the enabled connectors.
3. Connectors poll external systems on the configured interval.
4. The engine normalizes incidents, deployments, pipelines, SLOs, alerts, PRs, and runbooks.
5. The engine raises strongly typed events to the WinForms UI.
6. Panels render those models in the sidebar.
7. File scans and context-aware actions enrich the current source file.
8. Audit logs record security findings, recommendations, and runbook executions.

### Core responsibilities by layer

| Layer | Responsibility |
|---|---|
| UI | Present operational state, accept actions, route navigation back into the editor |
| Engine | Aggregate data, score risk, analyze incidents, generate recommendations |
| Connector | Fetch external data from specific platforms |
| Model | Carry normalized facts such as incidents, findings, PRs, SLOs, and runbooks |
| Audit | Persist redacted records of key AI/ops actions |

---

## Quick Start

### 1. Enable AIOps

Open **AIOps > AIOps Settings...**.

Verify these baseline options:

- `Enabled = true`
- `Mock Data Enabled = true` for first-run exploration, or disabled if you already have real connectors
- `Poll Interval Seconds = 60` unless you need faster refresh
- `Inline Insights Enabled = true`
- `Auto Scan On Save = true`
- `Require Approval For Production = true`

### 2. Connect your first data source

For a first successful demo, choose one of these approaches:

- **Fastest:** enable Mock Data
- **Most common:** configure Azure Monitor
- **Dev workflow focused:** configure GitHub Actions or Azure DevOps

### 3. Start the first scan

Open a source file in the editor.

Then use:

- **AIOps > Scan Active File**
- or press **Ctrl+Alt+S**

pfpad will:

- inspect the active file
- run SAST checks
- detect secrets
- validate policy-as-code when applicable
- update AIOps panels with findings

### 4. Open the intelligence panels

Recommended first tour:

1. `Ctrl+Alt+A` — AIOps Hub
2. `Ctrl+Alt+E` — Security Panel
3. `Ctrl+Alt+Q` — AI Query Panel
4. `Ctrl+Alt+L` — Incident Timeline
5. `Ctrl+Alt+P` — PR Risk Analysis
6. `Ctrl+Alt+W` — Service Dependencies
7. `Ctrl+Alt+B` — Runbooks

### 5. Generate your first observability snippet

Open a `.cs`, `.py`, `.js`, `.ts`, `.go`, or `.java` file.

Then press **Ctrl+Alt+G**.

pfpad will infer the language from the file extension and generate an OpenTelemetry function snippet for the current file name / service context.

### First-run success checklist

| Check | Expected outcome |
|---|---|
| AIOps enabled | Menu actions respond and panels can open |
| Connector configured | Telemetry and deployment views populate |
| File scanned | Security findings or policy results appear |
| Query panel opened | Current file context shows in the header |
| Incident timeline opened | Incidents and correlation data display |
| OTel code generated | Example snippet appears in a dialog |

---

## AIOps Menu Reference

### Keyboard shortcuts

| Shortcut | Menu item | What it does |
|---|---|---|
| `Ctrl+Alt+A` | AIOps Hub | Opens the overview dashboard with high-level AIOps state |
| `Ctrl+Alt+E` | Security Panel | Opens SAST, secrets, and policy validation results |
| `Ctrl+Alt+D` | Deployment Panel | Opens CI/CD pipelines, deployments, and rollout history |
| `Ctrl+Alt+T` | Telemetry Panel | Opens logs, metrics, traces, and connector-backed telemetry |
| `Ctrl+Alt+I` | Insights Panel | Opens observability gaps and remediation guidance |
| `Ctrl+Alt+Q` | AI Query Panel | Opens the natural-language production query panel |
| `Ctrl+Alt+L` | Incident Timeline | Opens incidents, timeline view, and root cause analysis |
| `Ctrl+Alt+P` | PR Risk Analysis | Opens DevSecOps-style pull request risk review |
| `Ctrl+Alt+W` | Service Dependencies | Opens the service dependency graph and health view |
| `Ctrl+Alt+B` | Runbooks | Opens operational procedures and dry-run execution |
| `Ctrl+Alt+S` | Scan Active File | Scans the current file for security and policy issues |
| `Ctrl+Alt+R` | Score Deployment Risk | Scores the current working tree / diff for deploy risk |
| `Ctrl+Alt+O` | Analyze Observability Gaps | Reviews instrumentation and observability quality |
| `Ctrl+Alt+G` | Generate OTel Code | Generates OpenTelemetry code for the active file language |

### Panel notes

#### AIOps Hub

Use this as the starting dashboard.

Typical uses:

- spot active incidents
- review deployment health
- confirm whether the environment is stable enough for change

#### Security Panel

Use this after scanning the active file.

It shows:

- code findings
- secret exposure detections
- policy violations

#### Deployment Panel

Use this to inspect rollout history.

It helps answer:

- what was deployed
- when it was deployed
- whether the pipeline is healthy

#### Telemetry Panel

Use this to connect code with runtime signals.

It is best for:

- service-level troubleshooting
- operational state review
- quick telemetry lookups

#### Insights Panel

Use this when you want observability guidance.

It highlights:

- missing instrumentation
- likely telemetry blind spots
- suggested remediations

#### AI Query Panel

Use this for natural-language questions grounded in the active file and current service context.

#### Incident Timeline

Use this to review incidents chronologically and run RCA on demand.

#### PR Risk Analysis

Use this to understand deployment and review risk before merge or release.

#### Service Dependencies

Use this to inspect upstream and downstream relationships between services.

#### Runbooks

Use this to browse operational procedures and safely dry-run command steps.

#### Scan Active File

Use this whenever you want fast local feedback on source or infra files.

#### Score Deployment Risk

Use this before deployment, approval, or merge.

#### Analyze Observability Gaps

Use this when adding or reviewing telemetry instrumentation.

#### Generate OTel Code

Use this when you want a language-specific tracing starter snippet.

---

## AI Query Panel

### Purpose

The AI Query Panel lets you ask operational questions in plain language while keeping the current file path in scope.

It is designed for:

- developers troubleshooting a file they are editing
- SREs investigating live service behavior
- security engineers asking context-aware questions before merge

### How to use it

1. Open a file.
2. Press **Ctrl+Alt+Q**.
3. Confirm the file path shown in the panel header.
4. Ask a question.
5. Review the answer, evidence, confidence, and recommendations.

### What context the panel uses

The panel is fed by the active file path and service resolution logic.

The engine can combine:

- active incidents
- recent deployments
- recent anomalies
- SLO signals
- recent alerts
- inline annotations
- the last deployment risk report
- correlation chains

### Supported query intents

The current intent classifier supports these categories:

| Intent | Description |
|---|---|
| `incident_investigation` | Questions about breakage, outages, failures, and why something is down |
| `risk_analysis` | Questions about deployment safety, blast radius, and rollout risk |
| `log_query` | Questions about logs, exceptions, crashes, and traces |
| `observability` | Questions about tracing, metrics, performance, SLOs, and instrumentation |
| `security` | Questions about vulnerabilities, secrets, permissions, and exposure |
| `general` | Broad operational status questions that do not match a narrower intent |

### Example queries

#### Incident investigation

- `Why is checkout-service failing?`
- `What changed before the latest incident?`
- `Do we have evidence of a deployment regression?`

#### Risk analysis

- `Is this deployment safe right now?`
- `What is increasing the blast radius of this change?`
- `Should this release require approval?`

#### Log query

- `What recent errors should I inspect first?`
- `Are timeout patterns likely here?`
- `Show the likely exception focus area.`

#### Observability

- `Where should I add tracing in this file?`
- `Do we have enough telemetry for this service?`
- `Is there an SLO or latency risk signal attached here?`

#### Security

- `Is this file likely to introduce a security issue?`
- `Do we have secret exposure risk in this area?`
- `What auth-related changes need more review?`

#### General

- `What is the current operational state of this service?`
- `Summarize risk, incidents, and alerts for the active file.`

### Reading the response

Focus on four parts:

1. **Answer** — the short natural-language summary.
2. **Evidence** — structured facts attached to the answer.
3. **Recommendations** — next steps to take.
4. **Confidence / disclaimer** — how strongly the engine trusts the conclusion.

### Good usage patterns

- Ask specific questions tied to a service or file.
- Use it after scanning or after opening timeline / deployment views.
- Treat low-confidence answers as prompts for investigation, not automatic truth.

---

## Security and DevSecOps

### Scope

pfpad AIOps combines three layers of local DevSecOps review:

1. SAST pattern scanning
2. secrets detection
3. policy-as-code validation

### SAST rules

Current built-in rule examples include:

| Rule ID | Title | Meaning |
|---|---|---|
| `CWE-89` | SQL Injection | Dynamic SQL assembled with concatenation |
| `CWE-78` | Command Injection | Shell/process execution may include untrusted input |
| `CWE-22` | Path Traversal | Potential unsafe path composition |
| `CWE-338` | Insecure Random | Non-cryptographic randomness in sensitive context |
| `CWE-327` | Weak Crypto | Obsolete crypto primitives such as MD5, SHA1, DES, RC2 |
| `CWE-798` | Hardcoded Credentials | Credentials or API keys in source |
| `CWE-601` | Open Redirect | Redirect target influenced by request input |
| `CWE-476` | Null Deref Risk | Possible null dereference pattern |
| `SEC-K8S-001` | Privileged Container | Kubernetes workload runs privileged |
| `SEC-K8S-002` | Latest Image Tag | Container image pinned to `latest` |
| `SEC-TF-001` | Public Storage | Terraform indicates public storage exposure |
| `SEC-TF-002` | No HTTPS | HTTPS-only enforcement appears disabled |

### Secrets detection

Current secret detectors include patterns such as:

- AWS access keys
- AWS secret keys
- Azure SAS tokens
- Azure connection strings
- GitHub personal access tokens
- generic API keys
- PEM/private key blocks
- basic-auth URLs with embedded credentials

### Policy validation

The policy validator inspects infrastructure and container definitions.

Supported file families:

- Bicep
- Terraform
- Kubernetes YAML
- generic YAML / YML infra manifests
- Dockerfile

### Policy examples

| Policy ID | Policy | Meaning |
|---|---|---|
| `POL-001` | Required tags | Environment and owner tags/labels should be present |
| `POL-002` | No public exposure | Dynamic public IP without clear NSG evidence |
| `POL-003` | Encryption at rest | Storage resource appears not to enable encryption |
| `POL-004` | TLS minimum 1.2 | Legacy TLS detected |
| `POL-005` | No root containers | Dockerfile explicitly uses `USER root` |
| `POL-006` | Resource limits | Kubernetes workload missing CPU/memory limits |
| `POL-007` | Approved image registries | Container image not from an approved registry |
| `POL-008` | No wildcard RBAC | Kubernetes `ClusterRole` uses wildcard verbs/resources |

### Recommended security workflow

1. Open the changed file.
2. Press **Ctrl+Alt+S**.
3. Review findings in the Security Panel.
4. Fix critical and high severity items first.
5. If working in infra, confirm policy validation results.
6. Re-scan the file.
7. Run PR Risk Analysis before merge.

### Severity handling guidance

| Severity | Suggested response |
|---|---|
| Critical | Stop and fix before merge or release |
| High | Fix before release; require review if deferral is unavoidable |
| Medium | Address during current change if possible |
| Low | Track and resolve without blocking unless the area is sensitive |
| Info | Document or monitor |

---

## Deployment Risk Scoring

### What it does

Deployment risk scoring evaluates the current working set using changed files, current incidents, SLO state, and deployment-sensitive heuristics.

### The 7 scoring factors

The current scorer evaluates these seven factors:

| Factor | Description | Typical effect |
|---|---|---|
| Changed file count | Large change set increases blast radius | Higher risk for wide changes |
| Critical file changed | Infra, config, container, appsettings, Bicep, Terraform, migration-sensitive files changed | Higher risk |
| Active incidents | Shipping during an ongoing incident is dangerous | Higher risk, can become critical |
| SLO burn rate | Elevated burn rate means the service is already stressed | Higher risk |
| Security-sensitive files | Auth, identity, security, secrets, login, token areas changed | Higher review burden |
| Test coverage signal | No obvious tests in the change set increases uncertainty | Moderate risk increase |
| Database migrations | Schema or SQL migration changes increase operational complexity | High risk increase |

### Score bands

| Score | Band | Meaning |
|---|---|---|
| `0-25` | Low | Normal rollout posture |
| `26-50` | Medium | Elevated caution; monitor carefully |
| `51-75` | High | Approval strongly recommended |
| `76-100` | Critical | Stop and review before production rollout |

### Interpretation notes

- A high score does not always mean the code is wrong.
- A high score means the deployment conditions are risky.
- A medium score often means rollout strategy matters more than code correctness.
- A critical score usually means an incident, burn rate issue, or dangerous change combination exists.

### When to use it

Use **Ctrl+Alt+R**:

- before deployment
- before PR approval on risky branches
- during incident response when deciding whether to keep shipping
- after large config or schema changes

### Recommended actions by band

| Band | Action |
|---|---|
| Low | Proceed with normal smoke tests and monitoring |
| Medium | Use guarded rollout, confirm monitoring, review recommendations |
| High | Require explicit reviewer approval and rollback readiness |
| Critical | Pause production rollout until the risk drivers are mitigated |

---

## Incident Timeline and RCA

### What the panel does

The Incident Timeline panel brings together:

- active incidents
- correlation chains
- timeline ordering
- ranked root-cause hypotheses
- file navigation back into the editor

### How to trigger RCA

1. Open **Incident Timeline** with **Ctrl+Alt+L**.
2. Select an incident.
3. Click **Analyze RCA**.
4. Wait for the ranked hypotheses to populate.
5. Double-click related files to jump into the editor.

### Reading the correlation chain

The timeline combines operational events near incident onset.

Look for:

- recent deployments close to the incident start
- alert fan-out across one service or several services
- anomaly spikes near the same time window
- risky commits or PRs linked to the affected service
- ordering that suggests a clear trigger followed by cascading effects

### The 5 hypothesis types

The current RCA engine can generate five major hypothesis families:

| Hypothesis type | What it means |
|---|---|
| Deployment regression | A recent deployment likely introduced the issue |
| High-risk commit regression | A risky recent commit may have caused config, migration, or security fallout |
| Dependency failure | Downstream or upstream dependency failure aligns with incident timing |
| SLO burn / error-budget pressure | SLO burn indicators align with the incident window |
| Correlated multi-alert failure | Multiple alerts suggest a broader shared failure mode |

### How to interpret ranked hypotheses

- **Rank 1** is the strongest current hypothesis.
- **Confidence** reflects how much evidence supported that hypothesis.
- **Evidence** shows why the engine ranked it highly.
- **Remediation** suggests what to inspect or do next.

### Good RCA workflow

1. Start with the top-ranked hypothesis.
2. Review the evidence timestamps.
3. Compare against the deployment timeline.
4. Check affected files and recent commits.
5. Open the relevant file directly from the panel.
6. Confirm or reject the hypothesis with telemetry.

---

## PR Risk Analysis

### Purpose

The PR Risk Analysis panel is a lightweight DevSecOps review surface for recent pull requests.

It is useful for:

- assessing deployability
- reviewing changed-file blast radius
- understanding security-sensitive changes
- deciding whether approval is required

### How to analyze a PR

1. Open **PR Risk Analysis** with **Ctrl+Alt+P**.
2. Select a pull request from the drop-down.
3. Review the changed files and commit history.
4. Click **Analyze**.
5. Review the generated risk report.

### What the panel shows

- PR title, author, branches, created date, and status
- changed files
- risk score and band
- factor-by-factor explanation
- recommendations for mitigation
- space for security findings when available from upstream scans
- recent commits associated with the PR

### Understanding the risk report

Pay attention to:

- the overall risk band
- the highest-severity factors
- whether rollout approval is required
- whether infra, auth, or migration files are involved
- whether the environment is already degraded

### Acting on findings

If the report is **Low**:

- continue with normal review
- verify smoke tests

If the report is **Medium**:

- add reviewer focus to risky areas
- consider canary or staged rollout

If the report is **High**:

- require explicit reviewer sign-off
- validate rollback plan
- confirm monitoring and alert readiness

If the report is **Critical**:

- do not ship immediately
- stabilize incidents or burn rate first
- break apart the change or defer release

---

## Service Dependencies

### What it does

The Service Dependencies panel renders a service dependency graph and health-aware tree.

It helps answer:

- what depends on this service
- what this service depends on
- whether a dependency issue may explain a failure
- where documentation for the service lives

### Main elements

- service tree
- filter box
- upstream list
- downstream list
- documentation link
- health-indicator dots

### Reading health indicators

| State | Meaning |
|---|---|
| Healthy | Normal error rate, availability, and latency |
| Degraded | Elevated latency or weaker availability |
| Critical | Serious latency, error rate, or availability problem |
| Unknown | No current health data attached |

### Practical uses

- map blast radius before deployment
- confirm shared dependencies during incidents
- identify likely choke points in a request path
- onboard developers to service boundaries quickly

---

## Connectors Setup Guide

### General guidance

You can mix connectors.

Common pattern:

- Azure Monitor for telemetry
- Azure DevOps or GitHub Actions for delivery state
- Kubernetes for service state
- PagerDuty for incidents
- Prometheus for metrics

### Azure Monitor

Required fields in settings:

- Tenant ID
- Client ID
- Client Secret
- Subscription ID
- Resource Group
- Workspace ID
- App Insights Key

Use Azure Monitor when you want:

- logs and metrics from Log Analytics / Application Insights
- recent alerts
- service health snapshots

Checklist:

1. Create or reuse an app registration.
2. Grant the app access to the workspace and relevant telemetry resources.
3. Enter tenant, client, and secret values.
4. Fill in subscription, resource group, workspace ID, and Application Insights key.
5. Enable the connector.

### Azure DevOps

Required fields:

- Organization
- Project
- PAT

Use Azure DevOps when you want:

- pipeline runs
- deployment history
- CI/CD state in the Deployment Panel

Checklist:

1. Create a PAT with read access to builds and releases.
2. Enter the org name.
3. Enter the project name.
4. Paste the PAT.
5. Enable the connector.

### Kubernetes

Required fields:

- API server URL
- Bearer token
- Namespace

Optional field:

- Skip TLS verify

Use Kubernetes when you want:

- cluster service topology
- service health hints
- workload and namespace-aware context

Checklist:

1. Copy the cluster API server URL.
2. Use a token with the minimum read permissions you need.
3. Set the working namespace.
4. Only enable `Skip TLS Verify` in controlled lab situations.
5. Enable the connector.

### Prometheus

Required fields:

- Base URL

Optional fields:

- Bearer token
- Username
- Password
- Timeout seconds

Use Prometheus when you want:

- metric-backed latency and error-rate context
- SLO-style operational signals

Checklist:

1. Set the Prometheus base URL.
2. Add auth only if your deployment requires it.
3. Keep timeout conservative for interactive use.
4. Enable the connector.

### PagerDuty

Required fields:

- API token

Optional field:

- Service ID
- Timeout seconds

Use PagerDuty when you want:

- incident state
- recent alert history
- responder-oriented timeline context

Checklist:

1. Create an API token with read access.
2. Enter the token.
3. Optionally scope to a service ID.
4. Enable the connector.

### GitHub Actions

Required fields:

- Owner
- Repo
- PAT

Optional field:

- Timeout seconds

Use GitHub Actions when you want:

- workflow status
- recent delivery history
- PR and repository deployment context

Checklist:

1. Generate a PAT with repo and workflow read access as needed.
2. Enter owner and repository.
3. Paste the token.
4. Enable the connector.

### Mock Data

Use Mock Data when you want:

- immediate demo mode
- safe UI exploration without external systems
- predictable sample incidents, alerts, topology, and runbooks

---

## AI Safety Model

### Safety principles

pfpad AIOps is designed around evidence-backed, local, deterministic logic rather than unconstrained generative behavior.

### Confidence scoring

Every major AI-style output carries a confidence score.

Interpretation:

| Score | Meaning |
|---|---|
| `>= 0.80` | Strong evidence, high confidence |
| `0.50 - 0.79` | Useful but should be verified |
| `< 0.50` | Weak evidence; treat as a lead, not a conclusion |

### Evidence-based responses

Responses are built from structured models such as:

- incidents
- deployments
- alerts
- anomalies
- SLOs
- risk factors
- inline annotations

This means the answer should be traceable back to recorded facts.

### Data redaction

Before data is persisted or displayed in sensitive contexts, pfpad applies redaction rules.

Examples include:

- passwords
- bearer tokens
- AWS keys
- SAS signatures
- private keys
- generic long secret-like blobs

### Approval workflow

The platform supports approval-aware behavior in two places:

1. **Deployment risk** — high or critical risk should trigger review before production.
2. **Runbooks** — steps marked as requiring approval will stop real execution unless approved.

### Audit logging location

AIOps audit events are written under:

```text
%LocalAppData%\pfpad\aiops-audit\
```

Daily files follow the pattern:

```text
aiops-audit-YYYYMMDD.log
```

Logged event types include:

- recommendation
- security finding
- production action
- runbook execution

### Prompt injection protection

The current AIOps layer is deliberately conservative.

Key protections:

- the query layer classifies intents with deterministic code
- answers are assembled from structured operational context
- connector data is treated as input data, not executable instructions
- runbook commands are checked against an allow-list of safe prefixes
- unsafe commands are blocked during execution
- redaction runs before audit persistence

### Operator guidance

- never treat a low-confidence answer as automatic truth
- keep production approval enabled
- review audit logs for sensitive or operationally meaningful actions
- prefer dry-run for unfamiliar runbooks

---

## Inline Annotations

### What they are

Inline annotations attach operational meaning to files.

They are stored per file and can be updated as the engine generates context.

### Glyph legend

| Glyph | Meaning |
|---|---|
| `⚡` | Latency hotspot or hot path |
| `⚡⚡` | Multiple latency-style signals clustered near the same area |
| `💥` | Error-prone code path |
| `🚨` | Incident-linked code area |
| `🔒` | Security-sensitive or risk-bearing location |
| `👁` | Observability gap |
| `📊` | SLO at risk |
| `🚀` | Deployment risk attached to the file |
| `📈` | Anomaly detected |

### What inline annotations help with

- knowing where operational risk concentrates
- seeing production relevance while editing code
- moving from alert to file faster
- identifying instrumentation blind spots early

### How to see details

Look for annotation glyphs near the affected file context and use the relevant panel to inspect the attached evidence.

Details usually include:

- message
- severity
- evidence summary
- action label where available

### Annotation lifecycle

- the engine generates annotations from operational context
- the annotation service stores them per file
- annotations can be replaced for a file or cleared globally

---

## OTel Code Generation

### What it does

The OTel generator creates starter instrumentation snippets for the active file language.

### Supported languages

| Language | Generated from file extension |
|---|---|
| C# | `.cs` |
| Python | `.py` |
| JavaScript | `.js` |
| TypeScript | `.ts` |
| Go | `.go` |
| Java | `.java` |

### How to use it

1. Open a supported source file.
2. Press **Ctrl+Alt+G**.
3. Review the generated snippet in the dialog.
4. Paste or adapt it into your implementation.

### What the generator includes

Typical snippet features:

- tracer or activity source creation
- service name tag
- function or namespace tag
- success status
- exception capture
- error status tagging

### Example output for C#

```csharp
using System.Diagnostics;

private static readonly ActivitySource ActivitySource = new("checkout-service");

public async Task ValidateCartAsync(CancellationToken ct)
{
    using var activity = ActivitySource.StartActivity("ValidateCart", ActivityKind.Internal);
    activity?.SetTag("service.name", "checkout-service");
    activity?.SetTag("code.function", "ValidateCart");

    try
    {
        await Task.CompletedTask;
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

### Best practices

- rename the generated function to match your real method
- add business attributes that matter for your service
- avoid recording secrets or PII in span attributes
- pair span creation with good error handling

---

## Runbooks

### What the panel does

The Runbooks panel shows operational procedures grouped by service.

It is built for:

- incident response
- platform maintenance
- repetitive diagnostics
- safe operator onboarding

### How to browse runbooks

1. Open **Runbooks** with **Ctrl+Alt+B**.
2. Select a service.
3. Select a runbook.
4. Review steps, description, severity, and linked docs.

### Dry-run behavior

Dry-run is the safest first step.

In dry-run mode:

- steps are enumerated
- approval steps are surfaced
- commands are not executed live
- you can inspect expected behavior before taking action

### What commands are considered safe

The current runbook engine allow-lists these prefixes:

- `kubectl get`
- `kubectl describe`
- `kubectl logs`
- `kubectl top`
- `git log`
- `git status`
- `git diff`
- `dotnet --version`
- `docker ps`
- `docker images`

### Safety model for runbooks

- unsafe commands are blocked
- approval-required steps halt real execution when not approved
- dry-run can be used without production impact
- results are logged to the audit log

### Recommended operator workflow

1. Start with dry-run.
2. Review the step descriptions.
3. Confirm whether approval is required.
4. Validate command safety.
5. Only then consider real execution in a controlled environment.

---

## Troubleshooting

### AIOps panel opens but is empty

Possible causes:

- connector not configured
- AIOps disabled
- no mock data and no live data source
- panel opened before first poll completed

Fixes:

- enable Mock Data temporarily
- verify AIOps settings
- reduce poll interval for testing
- reopen the panel after refresh

### Security scan finds nothing when you expected results

Possible causes:

- file type is unsupported for the specific rule set
- the current content does not match a rule pattern
- the issue is in a different file than the active one

Fixes:

- scan the exact file containing the risky pattern
- confirm file extension
- re-run after saving changes

### Query answers are too generic

Possible causes:

- limited operational context
- no recent incident / alert / SLO data
- weak service resolution for the active file

Fixes:

- open the relevant file first
- run a file scan
- open timeline or deployment panels before asking
- connect telemetry and incident sources

### Incident Timeline has no RCA result

Possible causes:

- insufficient recent correlated data
- no matching deployment, alert, anomaly, commit, or PR evidence

Fixes:

- wait for connector refresh
- connect more data sources
- confirm the incident has service context

### PR Risk Analysis shows no pull requests

Possible causes:

- PR-capable connector is not enabled
- no recent PRs are available

Fixes:

- configure GitHub Actions or another PR source in your environment
- enable Mock Data for exploration

### Service dependency graph is sparse

Possible causes:

- topology source not connected
- environment does not expose dependency data

Fixes:

- connect Kubernetes or Mock Data
- verify service naming conventions

### OTel generation picks the wrong language

Possible causes:

- file extension is unusual or unsupported

Fixes:

- rename the file to a supported extension first
- use the generated snippet as a starting point and adapt manually

### Runbook execution is blocked

Possible causes:

- command is not on the safe allow-list
- approval is required

Fixes:

- use dry-run first
- move risky commands into a manually approved workflow

### Audit log review

If you need to verify what AIOps did, inspect:

```text
%LocalAppData%\pfpad\aiops-audit\
```

---

## Developer Workflow Guides

### C# / .NET developer

Typical workflow:

1. Open the target `.cs` file.
2. Run **Scan Active File**.
3. Review Security Panel output for injection, crypto, and null-risk issues.
4. Use **AI Query Panel** to ask whether the current service is unstable.
5. Use **Generate OTel Code** to add tracing to critical methods.
6. Run **Score Deployment Risk** before shipping auth, config, or migration changes.

Best uses in .NET:

- SQL injection detection in repository or data-access code
- weak crypto review
- activity source generation for `ActivitySource`
- deployment review for `appsettings.json` and migration changes

### Python developer

Typical workflow:

1. Open the `.py` file.
2. Scan it for shell, secret, and security-sensitive patterns.
3. Ask AI Query whether recent failures align with the current module.
4. Generate OTel code for async functions.
5. Review observability gaps before deploy.

Best uses in Python:

- command injection checks
- secret detection
- trace scaffolding for async handlers
- deployment caution for infra-adjacent scripts

### JavaScript / TypeScript developer

Typical workflow:

1. Open the `.js` or `.ts` file.
2. Scan the file.
3. Review auth, token, secret, and redirect risks.
4. Use PR Risk Analysis for frontend/backend integration PRs.
5. Generate OTel code for service handlers.

Best uses in JS / TS:

- token and secret review
- route or redirect safety review
- instrumentation starter code for Node services
- dependency-aware operational questions from the query panel

### Go developer

Typical workflow:

1. Open the `.go` file.
2. Use the query panel to understand current service health.
3. Generate OTel snippets for handlers or worker paths.
4. Score risk before merging infra or migration work.
5. Use Incident Timeline during rollout failures.

Best uses in Go:

- rapid trace scaffolding
- service health correlation
- rollout caution for backend services with strong SLOs

### DevSecOps engineer

Typical workflow:

1. Scan application and infra files.
2. Review Security Panel for SAST + secrets + policy findings.
3. Run PR Risk Analysis on risky changes.
4. Run deployment risk scoring before release approval.
5. Inspect runbooks and dry-run operator procedures.

Best uses for DevSecOps:

- pre-merge guardrails
- policy drift checks in Bicep, Terraform, K8s YAML, Dockerfile
- secret-exposure prevention
- risk-based release approval support

### SRE / Platform engineer

Typical workflow:

1. Open Incident Timeline.
2. Run RCA for the active incident.
3. Inspect Service Dependencies for blast radius.
4. Review Runbooks and dry-run the next safe command set.
5. Ask AI Query for an evidence-backed summary.
6. Check deployment risk before allowing new rollouts.

Best uses for SRE / Platform:

- incident triage
- deployment freeze decisions
- dependency-driven diagnosis
- safe operational command review

---

## Final Notes

pfpad AIOps is most effective when you use it as a loop, not a single feature.

Recommended loop:

1. scan the file
2. inspect risk
3. ask a focused question
4. review incidents and dependencies
5. add telemetry
6. confirm runbook safety
7. ship only with acceptable risk

That loop is what makes pfpad an AIOps-aware developer editor rather than a standard code editor with disconnected tooling.

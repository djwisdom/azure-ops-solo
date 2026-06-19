# AIOps Manual — Personal Flip Pad (Pfpad)

> **Version:** 1.0.46.0+  
> **Target framework:** net10.0-windows  
> **Last updated:** 2026-06-20

---

## Table of Contents

1. [What Is AIOps in Pfpad?](#1-what-is-aiops-in-pfpad)
2. [Getting Started](#2-getting-started)
3. [Settings & Connectors](#3-settings--connectors)
4. [Mock Data Mode](#4-mock-data-mode)
5. [UI Panels](#5-ui-panels)
   - [AIOps Hub (Overview)](#51-aiops-hub-overview)
   - [Security Panel](#52-security-panel)
   - [Deployment Panel](#53-deployment-panel)
   - [Telemetry Panel](#54-telemetry-panel)
   - [Insights Panel](#55-insights-panel)
   - [OpsQuery Panel](#56-opsquery-panel)
   - [Incident Timeline Panel](#57-incident-timeline-panel)
   - [Pull Request Risk Panel](#58-pull-request-risk-panel)
   - [Service Dependency Panel](#59-service-dependency-panel)
   - [Runbook Panel](#510-runbook-panel)
6. [Engine Layer](#6-engine-layer)
   - [DeploymentRiskScorer](#61-deploymentriskcorer)
   - [SastScanner](#62-sastscanner)
   - [SecretsDetector](#63-secretsdetector)
   - [PolicyValidator](#64-policyvalidator)
   - [ObservabilityAdvisor](#65-observabilityadvisor)
   - [RootCauseAnalyzer](#66-rootcauseanalyzer)
   - [AnomalyDetector](#67-anomalydetector)
   - [CorrelationEngine](#68-correlationengine)
   - [OpsQueryEngine (Chat)](#69-opsqueryengine-chat)
   - [OTelCodeGenerator](#610-otelcodegenerator)
   - [RunbookEngine](#611-runbookengine)
   - [ServiceContextEngine](#612-servicecontextengine)
   - [AuditLogger](#613-auditlogger)
7. [Connectors Reference](#7-connectors-reference)
8. [Inline Annotations](#8-inline-annotations)
9. [Security & Secret Management](#9-security--secret-management)
10. [AI Safety Model](#10-ai-safety-model)
11. [Architecture Diagram](#11-architecture-diagram)

---

## 1. What Is AIOps in Pfpad?

AIOps in Pfpad is an **operational intelligence layer** built directly into the editor. It connects your code to your live production environment — surfacing incidents, deployments, security findings, SLO burn rates, anomalies, and observability gaps without leaving the editor.

**Core philosophy:**
- Every insight carries a **confidence score** (0.0 – 1.0) and citable **evidence**.
- Sensitive data (tokens, passwords, PII) is **always redacted** before display.
- **Production actions** (remediations marked `ReviewRequired` or `ProductionImpact`) require explicit user approval.
- When evidence is insufficient, the engine returns *"Insufficient data"* rather than speculating.
- All scans run locally using non-backtracking regex (DFA — O(N) scan time, no ReDoS).

---

## 2. Getting Started

AIOps initialises automatically when you open Pfpad. No extra steps are needed in **Mock Data Mode** (default). To use live data sources, configure your connectors via **View → AIOps → Settings…**.

**To open any AIOps panel:**

- **View menu → AIOps** — lists all panels: Hub, Security, Deployment, Telemetry, Insights, OpsQuery, Incident Timeline, PR Risk, Service Dependency, Runbooks.
- Each panel has its own **✕ Close** button to dismiss it back to hidden.
- All panels dock into the sidebar (right split). Only one panel is shown at a time per Dock=Fill slot.

**To trigger an on-demand scan of the active file:**

- From the AIOps Hub panel, click **Scan File**.
- When `AutoScanOnSave` is enabled (Settings → General), scanning runs automatically on every save.

---

## 3. Settings & Connectors

Open **View → AIOps → Settings…** (or the gear icon inside the Hub panel) to access `AIOpsSettingsDialog`.

### General Tab

| Setting | Default | Description |
|---|---|---|
| AIOps Enabled | ✅ | Master switch. When off, all polling and scanning stop. |
| Mock Data | ✅ | Use built-in simulated data instead of live connectors. |
| Poll Interval | 60 s | How often the engine calls `RefreshAllAsync()` across all connectors. |
| Inline Insights | ✅ | Show AIOps annotations in the editor gutter next to relevant lines. |
| Auto-Scan on Save | ✅ | Run SAST + secrets scan automatically on file save. |
| Require Approval | ✅ | Prompt before applying any remediation with risk ≥ `ReviewRequired`. |

### Connector Tabs

Each connector has its own tab with an **Enabled** checkbox, connection fields, and a **Test Connection** button that runs a live connectivity check and updates the status label in real time.

Settings are saved to `%AppData%\MyCrownJewelApp\TextEditor\settings.json` under `AIOpsConfig`. All credentials are **DPAPI-encrypted** before write and decrypted in-memory on load — plaintext secrets are never persisted to disk.

---

## 4. Mock Data Mode

When **Mock Data Enabled** is checked, Pfpad's `MockDataConnector` provides a full set of simulated production data so every panel is immediately useful out of the box — no Azure subscription or API token required.

Mock data includes:
- 3 active incidents (checkout-service degradation, payment latency, order orchestration timeout)
- 5 recent deployments across dev/staging/production environments
- 3 pipeline runs (GitHub Actions style)
- 3 SLOs — one breached (`checkout-service`), one at-risk, one healthy
- 20 recent commits and 4 pull requests
- 3 service health records
- 6 anomaly events (latency increases, error rate spikes)
- 3 alerts (Prometheus-style)
- 5 runbooks with step-by-step remediation
- Inline annotations and correlation chains built from all of the above

To switch to live data, uncheck **Mock Data** and configure at least one real connector.

---

## 5. UI Panels

### 5.1 AIOps Hub (Overview)

**Access:** View → AIOps → AIOps Hub

The hub is the command center for AIOps. It shows:

- **Connector status bar** — each active connector listed with its `Connected`/`Error`/`Connecting` state and last sync time.
- **Active Incidents** — severity-ranked list of current incidents sourced from all connectors. Click an incident to open the Incident Timeline.
- **SLO Summary** — service name, target %, current %, burn rate, and a coloured status badge (🟢 Healthy / 🟡 At Risk / 🔴 Breached).
- **Last Risk Report** — risk level badge and top-line score from the most recent deployment risk scan.
- **Toolbar buttons:** Refresh (re-runs `RefreshAllAsync`), Scan File, Settings.

### 5.2 Security Panel

**Access:** View → AIOps → Security

Displays results from three engines working on the active file or workspace:

| Column | Meaning |
|---|---|
| Severity | Critical / High / Medium / Low / Info |
| Rule | CWE ID (link to mitre.org) or `SECRET-*` |
| Title | Short finding title |
| File | Path (click to navigate to file + line) |
| Line | Line number of the match |
| Remediation | Specific fix recommendation |

**Finding Navigate:** Double-click any row to jump to that file at the exact line in a new editor tab.

**Policy violations** are shown in a separate sub-tab with PolicyId, name, severity, and a link to the policy doc when available.

### 5.3 Deployment Panel

**Access:** View → AIOps → Deployments

Two sub-tabs:

**Deployments tab:**
- Service name, version, environment, status badge (✅/❌/⏳/↩), age, deployed-by, and pipeline link.
- Rows are sorted newest-first.
- Status colours: green = Succeeded, red = Failed, orange = RolledBack, gold = Running/Pending.

**Pipelines tab:**
- Pipeline name, branch, status, duration, and a clickable URL.
- Each stage is shown inline (name → status → duration).

### 5.4 Telemetry Panel

**Access:** View → AIOps → Telemetry

Live telemetry browser with three sub-tabs:

**Logs tab:** Time-ordered log entries with severity, message, TraceId, SpanId, and key/value properties. Sensitive values are automatically redacted.

**Metrics tab:** ListView showing metric name, value + unit, timestamp, and dimensions (service, env). Supports time range selection: Last 1h / 6h / 24h / 7d.

**Traces tab:** TreeView of distributed trace spans. Root span at top; child spans indented by parent-child relationship. Each node shows: service → operation → duration → error flag.

**Service selector:** Type a service name or click **Browse…** to list observable services from connectors that implement `IServiceDiscoveryCapable`. Then click **Query**.

### 5.5 Insights Panel

**Access:** View → AIOps → Insights

Shows two categories of AI-generated insights:

**Observability Gaps** — code locations where telemetry is missing or insufficient:
- File path + line number
- Category (Missing Trace, Missing Log, Missing Metric, etc.)
- Description of the gap
- Suggested code snippet to add the missing instrumentation

**Remediation Suggestions** — actionable fix suggestions for detected issues:
- Risk badge: Safe / ReviewRequired / ProductionImpact
- Old code snippet vs. new code snippet (diff view)
- `RequiresApproval` badge shown for anything ≥ ReviewRequired

### 5.6 OpsQuery Panel

**Access:** View → AIOps → OpsQuery

A **natural-language operations chat interface** powered by `OpsQueryEngine`. Ask questions about your production environment:

```
> What services are degraded right now?
> Which deployments happened in the last 24 hours?
> Is there an active incident affecting checkout-service?
> Why is the error rate high on payment-service?
```

The engine answers using the current operational context (active incidents, recent deployments, SLOs, alerts, anomalies, correlation chains). All responses include:
- A confidence score (0.0 – 1.0)
- Evidence citations with sources, descriptions, and timestamps
- A disclaimer when data is insufficient
- Redacted output (no credentials or PII leaked)

The chat is **stateless** — each question is answered independently against the current engine state.

### 5.7 Incident Timeline Panel

**Access:** View → AIOps → Incident Timeline

Chronological timeline of incidents and their correlation chains:

- Each incident row shows: severity badge, title, affected service, status, start time, resolution time (if resolved).
- **RCA button** — triggers `AIOpsEngine.AnalyzeIncidentAsync()` which runs the `RootCauseAnalyzer` and displays:
  - Top hypothesis with confidence score
  - Supporting evidence (commits, deployments, anomalies, alerts)
  - Remediation steps
  - Disclaimer when evidence is inconclusive
- **Navigate** button — for incidents linked to a code file, opens the file at the correlated line.
- **Correlation chains** are shown below incidents, visualising the code → commit → PR → pipeline → deployment → incident causal path.

### 5.8 Pull Request Risk Panel

**Access:** View → AIOps → PR Risk

Lists open and recent pull requests from connected Git hosting providers (GitHub Actions, GitLab, Bitbucket, Azure DevOps). For each PR:

- Title, author, target branch, status, age
- **Analyze Risk** button — runs `DeploymentRiskScorer` against the PR's changed file list and displays the full `DeploymentRiskReport`:
  - Overall risk level: Low / Medium / High / Critical
  - Risk score (0–100)
  - Individual risk factors with evidence
  - Prioritised recommendations
  - `RequiresApproval` flag when risk ≥ High

Security findings found during the risk scan appear inline and are navigable to source.

### 5.9 Service Dependency Panel

**Access:** View → AIOps → Service Dependency

Visualises the service graph from connectors that implement `IServiceCatalogCapable`:

- Service nodes with health indicators
- Dependency edges showing call direction
- `ServiceEntity` metadata: owner team, repository link, SLO reference, tech stack tags

When no catalog connector is available, a topology is inferred from incident and deployment data.

### 5.10 Runbook Panel

**Access:** View → AIOps → Runbooks

Browse and execute operational runbooks:

- Filter by service name
- Each runbook shows: name, service, description, severity, and step count
- **Steps view** — expand a runbook to see ordered steps with: action type (Manual/Automated/Verify), description, command, and timeout
- **Execute step** — for automated steps, the command is shown with a confirmation prompt (respects `RequireApprovalForProduction` setting)

---

## 6. Engine Layer

All engines are instantiated once inside `AIOpsEngine` and reused across all panels. They are synchronous or async value-returning — none have mutable shared state.

### 6.1 DeploymentRiskScorer

Scores a proposed deployment's risk as **Low / Medium / High / Critical** (0–100 scale).

**Risk factors evaluated:**
| Factor | Trigger | Score |
|---|---|---|
| Large footprint | > 50 changed files | +30 |
| Critical files changed | config/, Dockerfile, *.tf, *.bicep, *.yml | +20 |
| Active critical incident | Severity = Critical, not resolved | +40 |
| Active high incident | Severity = High, not resolved | +25 |
| Breached SLO | Any SLO at SloStatus.Breached | +15 |
| High-burn-rate SLO | BurnRate > 2.0 | +15 |

Final risk level: < 20 = Low, 20–49 = Medium, 50–74 = High, ≥ 75 = Critical.

### 6.2 SastScanner

Static application security testing using **non-backtracking** compiled regexes (O(N) DFA — ReDoS-safe). Scans C#, Python, Shell, YAML, Terraform, and SQL files.

**Built-in rules:**

| Rule ID | Title | Severity |
|---|---|---|
| CWE-89 | SQL Injection | High |
| CWE-78 | Command Injection | High |
| CWE-22 | Path Traversal | Medium |
| CWE-338 | Insecure Random | Medium |
| CWE-327 | Weak Crypto (MD5/SHA1/DES) | High |
| CWE-798 | Hardcoded Credentials | Critical |
| CWE-601 | Open Redirect | Medium |
| CWE-476 | Null Deref Risk | Low |
| SEC-K8S-001 | Privileged Container | High |
| SEC-K8S-002 | Latest Image Tag | Medium |
| SEC-TF-001 | Public Storage Account | High |
| SEC-TF-002 | HTTPS-only disabled | High |

Each finding links to `https://cwe.mitre.org/data/definitions/{id}.html`.

### 6.3 SecretsDetector

Detects hard-coded secret material using pattern matching.

**Patterns covered:**

| Type | Severity |
|---|---|
| AWS Access Key (`AKIA…`) | Critical |
| AWS Secret Access Key | Critical |
| Azure SAS Token (`sig=…`) | Critical |
| Azure Storage Connection String | High |
| GitHub Personal Access Token (`ghp_…`) | Critical |
| Generic API Key | High |
| PEM Private Key block | Critical |
| Credentials embedded in URL | High |

Matched snippets are redacted via `DataRedactor` before display — the actual secret value is never shown.

### 6.4 PolicyValidator

Enforces organisational policies against source files. Current built-in policies:

- **Kubernetes Pod without resource limits** (yaml) — Medium
- **Deployment without liveness probe** (yaml) — Low  
- **No retry policy on HttpClient** (cs) — Low
- **Logging sensitive data** (cs) — Medium

Violations are shown in the Security Panel's Policies sub-tab with a link to the policy document when configured.

### 6.5 ObservabilityAdvisor

Analyses C# source for missing instrumentation and suggests OpenTelemetry code to add. Detects:
- Public methods in service classes with no `Activity` or metrics
- `catch` blocks that swallow exceptions without logging
- `HttpClient` calls without distributed tracing headers
- Database calls without query span tracking

Suggestions include a ready-to-paste code snippet generated by `OTelCodeGenerator`.

### 6.6 RootCauseAnalyzer

Correlates an incident against the most recent deployments, anomalies, alerts, commits, and pull requests to generate ranked hypotheses.

**Hypothesis scoring:**
- Each hypothesis carries a confidence score (0.0–1.0)
- Hypothesis types: `DeploymentCorrelation`, `AnomalyCorrelation`, `AlertCorrelation`, `DependencyFailure`, `ConfigChange`
- Supporting evidence references exact commit SHAs, deployment IDs, alert timestamps, etc.
- Returns `HasSufficientData = false` and a disclaimer when less than 2 pieces of evidence are available

### 6.7 AnomalyDetector

Statistical anomaly detection over metric time series using an **IQR-fence** algorithm:
- Computes Q1, Q3, IQR across the time window
- Flags points outside `[Q1 − 1.5×IQR, Q3 + 1.5×IQR]` as anomalies
- Annotates anomaly type: `LatencyIncrease`, `ErrorRateSpike`, `ThroughputDrop`, `Generic`
- Minimum 6 data points required; returns empty when data is insufficient

### 6.8 CorrelationEngine

Builds **causal chain** graphs across: commits → pull requests → pipelines → deployments → incidents → alerts → anomalies.

Each `CorrelationChain` contains:
- An anchor incident
- A sequence of `CorrelationEvent` nodes (type, title, timestamp, confidence, evidence)
- A final confidence score and disclaimer

Chains are passed directly to the Incident Timeline Panel for visualisation.

### 6.9 OpsQueryEngine (Chat)

A rule-based natural-language question answering engine over the current `OperationalContext`. It matches questions to operational categories:

| Question pattern | Category |
|---|---|
| "degraded", "down", "unhealthy" | Service Health |
| "incident", "outage", "alert" | Incidents |
| "deploy", "release", "rollout" | Deployments |
| "slo", "burn rate", "error budget" | SLOs |
| "risk", "safe to deploy" | Deployment Risk |
| "why", "root cause" | Correlation / RCA |
| "anomaly", "spike" | Anomalies |

Answers include confidence score, evidence list, and a redacted, source-cited response. All queries are logged to the `AuditLogger`.

### 6.10 OTelCodeGenerator

Generates ready-to-paste OpenTelemetry instrumentation code for a given function name, service name, and target language (C#, Python, Go, JavaScript). Output includes:
- `ActivitySource` / `Tracer` setup
- Span creation with `using`/`with` pattern
- Status setting on exception
- Relevant metric recording stubs

### 6.11 RunbookEngine

Manages the runbook catalog returned by `IRunbookCapable` connectors. Provides:
- `GetForIncident(incident)` — returns runbooks whose tags or service name match the incident
- `GetForService(serviceName)` — filters by exact service name match
- Step sequencing and execution tracking

### 6.12 ServiceContextEngine

Infers the **service name** for the file currently open in the editor by scanning:
1. The file's directory for a `.csproj` / `package.json` / `pyproject.toml` whose name suggests a service
2. The path components for known service name patterns
3. Deployment and incident data — matches service names that appear in recent operational activity

Builds a full `OperationalContext` record by joining the resolved service name with all engine state.

### 6.13 AuditLogger

Writes an append-only JSON Lines audit trail to:
```
%LOCALAPPDATA%\Personal Flip Pad\aiops-audit\aiops-audit-YYYYMMDD.log
```

Entries include: timestamp, event type (`SecurityFinding`, `Recommendation`, `PolicyViolation`, `RcaGenerated`), severity, and confidence score. Credential/PII fields are redacted before logging.

---

## 7. Connectors Reference

| Connector | Protocol | Capability interfaces |
|---|---|---|
| **MockDataConnector** | In-process | All interfaces |
| **AzureMonitorConnector** | Azure Monitor REST + Log Analytics | `IAIOpsConnector`, `IAlertCapable`, `IServiceDiscoveryCapable` |
| **AzureDevOpsConnector** | Azure DevOps REST API | `IAIOpsConnector`, `IPullRequestCapable`, `IGitHistoryCapable` |
| **KubernetesConnector** | Kubernetes API Server (Bearer token) | `IAIOpsConnector`, `IServiceCatalogCapable` |
| **PrometheusConnector** | Prometheus HTTP API (`/api/v1/query_range`) | `IAIOpsConnector`, `IAlertCapable` |
| **PagerDutyConnector** | PagerDuty REST API v2 | `IAIOpsConnector`, `IAlertCapable`, `IRunbookCapable` |
| **GitHubActionsConnector** | GitHub REST API | `IAIOpsConnector`, `IPullRequestCapable`, `IGitHistoryCapable` |
| **GitLabConnector** | GitLab REST API v4 | `IAIOpsConnector`, `IPullRequestCapable`, `IGitHistoryCapable` |
| **BitbucketConnector** | Bitbucket Cloud REST API 2.0 | `IAIOpsConnector`, `IPullRequestCapable`, `IGitHistoryCapable` |

### Authentication modes

**Azure Monitor / Azure DevOps:**
- *Service Principal* — Tenant ID + Client ID + Client Secret (OAuth2 client credentials)
- *Azure CLI* — uses token from `az login` cache; no secret required for dev workstations

**Kubernetes:** Bearer token (service account token or kubeconfig)

**Prometheus:** Optional Bearer token or HTTP Basic (username + password)

**PagerDuty, GitHub, GitLab, Bitbucket:** Personal Access Token

### Adding a new connector

1. Implement `IAIOpsConnector` (and any optional capability interfaces).
2. Add a settings class to `AIOpsSettings` following the existing pattern.
3. Register in `AIOpsEngine.RebuildConnectors()`.
4. Add a tab to `AIOpsSettingsDialog`.

---

## 8. Inline Annotations

When `InlineInsightsEnabled` is on, AIOps gutter annotations appear next to lines in the editor for the currently open file. Annotation types:

| Type | Glyph | Trigger |
|---|---|---|
| `LatencyHotspot` | 🔥 | Anomaly of type `LatencyIncrease` on the file's service |
| `AnomalyDetected` | ⚠️ | Any other anomaly on the file's service |
| `IncidentLinked` | 🚨 | An active alert on the file's service |
| `DeploymentRisk` | 📦 | File is in the changed-files list of a high/critical risk report |

Annotations are updated whenever:
- `AIOpsEngine.InlineAnnotationsGenerated` fires (after each refresh cycle)
- A file is opened (context built for the newly active file)
- An on-demand scan completes

The `InlineAnnotationService` stores annotations keyed by file path and hands them to the editor for gutter rendering.

---

## 9. Security & Secret Management

### DPAPI encryption

All connector secrets (tokens, passwords, client secrets) are:
1. Held in plaintext only **in memory** during the application session.
2. **DPAPI-encrypted** (`DpapiSettingsProtector.Protect()`) before writing to `settings.json`.
3. Decrypted at startup via `AIOpsSettings.LoadSecretsFromEncrypted()`.

The `Protect` / `Unprotect` helpers use `System.Security.Cryptography.ProtectedData` with `DataProtectionScope.CurrentUser` — the ciphertext is only decryptable on the same Windows user account.

**Plaintext fields** (`ClientSecret`, `PersonalAccessToken`, etc.) are always serialised as empty strings to disk — only the `Encrypted*` counterpart is written.

### Data redaction

`DataRedactor.Redact(string)` is called on:
- All log messages before display in TelemetryPanel
- All code snippets in security findings before display
- All audit log entries before disk write

The redactor replaces tokens matching known patterns (AWS keys, Azure SAS, bearer tokens, passwords in key=value form) with `[REDACTED]`.

---

## 10. AI Safety Model

| Principle | How it is enforced |
|---|---|
| Confidence-scored | Every `AIOpsInsight` subclass carries `ConfidenceScore` (0.0–1.0) and `ConfidenceLevel` enum |
| Evidence-cited | `IReadOnlyList<Evidence>` required on every insight; each evidence entry has source, description, URL, and timestamp |
| Insufficient evidence | `HasSufficientEvidence = false` when `Evidence.Count == 0 \|\| ConfidenceScore < 0.3`; panels display a "Insufficient data" badge |
| No speculation | `OpsQueryEngine` returns an explicit disclaimer when no matching data is found; `RootCauseAnalyzer` sets `HasSufficientData = false` |
| Sensitive data redacted | `DataRedactor.Redact()` applied to all user-visible and disk-written content |
| Approval gates | `RemediationSuggestion.RequiresApproval` and `DeploymentRiskReport.RequiresApproval` block execution without confirmation when `RequireApprovalForProduction = true` |
| Audit trail | All recommendations, security findings, and RCA results are logged to `aiops-audit.jsonl` |

---

## 11. Architecture Diagram

```
┌───────────────────────────────────────────────────────────────────────┐
│                          Pfpad Editor (Form1)                         │
│                                                                       │
│  InitAIOps()  ←  AIOpsSettings (DPAPI-decrypted from settings.json)  │
│                                                                       │
│  ┌──────────────── AIOpsEngine ────────────────────────────────────┐  │
│  │  Poll timer (configurable, default 60s)                         │  │
│  │  RefreshAllAsync() → parallel connector fetch → merge + dedup   │  │
│  │                                                                 │  │
│  │  Analysis engines (all stateless, thread-safe):                 │  │
│  │    DeploymentRiskScorer  SastScanner      SecretsDetector        │  │
│  │    PolicyValidator       ObservabilityAdvisor  OTelCodeGenerator │  │
│  │    RootCauseAnalyzer     AnomalyDetector   CorrelationEngine     │  │
│  │    OpsQueryEngine        RunbookEngine     ServiceContextEngine  │  │
│  │    AuditLogger                                                   │  │
│  │                                                                 │  │
│  │  Events:                                                        │  │
│  │    IncidentsRefreshed   DeploymentsRefreshed  PipelinesRefreshed │  │
│  │    SlosRefreshed        AnomaliesDetected     AlertsRefreshed    │  │
│  │    SecurityFindingsFound  PolicyViolationsFound                  │  │
│  │    ObservabilityGapsFound  RiskReportGenerated                   │  │
│  │    CorrelationChainsUpdated  InlineAnnotationsGenerated          │  │
│  └────────────────────────────┬────────────────────────────────────┘  │
│                               │ events                                │
│  ┌────────────────────────────▼────────────────────────────────────┐  │
│  │                    UI Panels (sidebar)                          │  │
│  │  AIOpsPanel (Hub)      SecurityPanel     DeploymentPanel        │  │
│  │  TelemetryPanel        InsightsPanel     OpsQueryPanel          │  │
│  │  IncidentTimelinePanel PullRequestRiskPanel                     │  │
│  │  ServiceDependencyPanel RunbookPanel                            │  │
│  └────────────────────────────┬────────────────────────────────────┘  │
│                               │ navigate events                       │
│  ┌────────────────────────────▼────────────────────────────────────┐  │
│  │  Editor  ←  InlineAnnotationService  ←  InlineAnnotations[]    │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                       │
│  AIOpsSettingsDialog → AIOpsSettings → AIOpsEngine.Configure()       │
└───────────────────────────────────────────────────────────────────────┘

                          Connector layer
┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
│  MockData  │  │ AzureMonitor│  │AzureDevOps │  │ Kubernetes │
│ (built-in) │  │ (REST+LA)  │  │ (REST API) │  │ (API svr)  │
└────────────┘  └────────────┘  └────────────┘  └────────────┘
┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐
│ Prometheus │  │ PagerDuty  │  │GitHub Acts │  │   GitLab   │  │ Bitbucket  │
│ (/query)   │  │ (API v2)   │  │ (REST API) │  │ (API v4)   │  │ (API 2.0)  │
└────────────┘  └────────────┘  └────────────┘  └────────────┘  └────────────┘
```

---

*This document is part of the Pfpad project. Source: `docs/AIOPS_MANUAL.md`.*

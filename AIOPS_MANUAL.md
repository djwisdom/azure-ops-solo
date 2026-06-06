# pfpad AIOps Manual

## *An AIOps-Aware Developer Editor for DevSecOps, SRE, and AIOps-Driven Software Delivery*

---

## Table of Contents

1. [Vision and Positioning](#1-vision-and-positioning)
2. [Architecture Overview](#2-architecture-overview)
3. [AI Safety Model](#3-ai-safety-model)
4. [Quick Start](#4-quick-start)
5. [AIOps Panel (Hub)](#5-aiops-panel-hub)
6. [Deployment Risk Scoring](#6-deployment-risk-scoring)
7. [Security Scanning (DevSecOps)](#7-security-scanning-devsecops)
8. [Policy-as-Code Validation](#8-policy-as-code-validation)
9. [Telemetry Panel](#9-telemetry-panel)
10. [Incident Correlation](#10-incident-correlation)
11. [SLO Monitoring](#11-slo-monitoring)
12. [Observability Advisor](#12-observability-advisor)
13. [Deployment Panel (CI/CD)](#13-deployment-panel-cicd)
14. [Insights Panel](#14-insights-panel)
15. [Integrations Setup](#15-integrations-setup)
16. [Developer Workflow Guides](#16-developer-workflow-guides)
17. [Keyboard Reference](#17-keyboard-reference)
18. [Troubleshooting](#18-troubleshooting)

---

## 1. Vision and Positioning

### What is pfpad?

**pfpad** (Personal Flip Pad) is an AIOps-aware developer editor that connects code, deployments, observability, security, and incidents into one intelligent engineering workflow.

It is positioned for:
- **DevSecOps engineers** who need security scanning, policy-as-code validation, and secret detection inline while coding
- **SRE (Site Reliability Engineers)** who need SLO burn rates, incident correlation, and deployment risk scoring before pushing changes
- **AIOps-driven teams** who want AI-generated, evidence-based insights about how code changes affect reliability, security, and production health

### Core Differentiators

| Capability | Traditional IDE | pfpad |
|---|---|---|
| Security scanning | External plugin or CI step | **Inline as you type, before commit** |
| Deployment risk | Post-deploy monitoring | **Pre-deploy risk scoring from git diff** |
| Incident correlation | Post-mortem, external tool | **Inline: which function caused last incident** |
| SLO awareness | Dashboard in another tool | **Right next to your code** |
| Policy-as-code | CI/CD gate | **Inline in Bicep/Terraform/YAML files** |
| Observability | APM tool separately | **Code-level OTel instrumentation suggestions** |
| AI insights | Guess-based suggestions | **Evidence-based, confidence-scored, cited** |

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        pfpad AIOps Editor                             │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                         UI Layer                                 │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────────┐ │  │
│  │  │ AIOps    │  │Telemetry │  │ Security │  │  Deployment    │ │  │
│  │  │ Panel    │  │ Panel    │  │  Panel   │  │    Panel       │ │  │
│  │  │ (Hub)    │  │Log/Met/  │  │ SAST +   │  │ CI/CD +        │ │  │
│  │  │ Risk     │  │ Traces   │  │ Policy   │  │ Deployments    │ │  │
│  │  └──────────┘  └──────────┘  └──────────┘  └────────────────┘ │  │
│  │  ┌──────────┐                                                   │  │
│  │  │ Insights │                                                   │  │
│  │  │  Panel   │                                                   │  │
│  │  └──────────┘                                                   │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                      AIOps Engine Layer                          │  │
│  │                                                                  │  │
│  │  ┌────────────────┐  ┌──────────────┐  ┌─────────────────────┐ │  │
│  │  │ Deployment     │  │ SAST Scanner │  │ Policy Validator    │ │  │
│  │  │ Risk Scorer    │  │ + Secrets    │  │ (Bicep/TF/K8s/      │ │  │
│  │  │ (git diff →    │  │ Detector     │  │  Dockerfile)        │ │  │
│  │  │  risk report)  │  │              │  │                     │ │  │
│  │  └────────────────┘  └──────────────┘  └─────────────────────┘ │  │
│  │  ┌────────────────┐  ┌──────────────┐  ┌─────────────────────┐ │  │
│  │  │ Observability  │  │ Incident     │  │ SLO Monitor         │ │  │
│  │  │ Advisor (OTel  │  │ Analyzer     │  │ (burn rate,         │ │  │
│  │  │  gaps)         │  │ (RCA)        │  │  error budget)      │ │  │
│  │  └────────────────┘  └──────────────┘  └─────────────────────┘ │  │
│  │  ┌──────────────────────────────────────────────────────────┐   │  │
│  │  │             AIOpsEngine (Orchestrator)                    │   │  │
│  │  │  - Aggregates all connector data                         │   │  │
│  │  │  - Fires typed events to UI layer                        │   │  │
│  │  │  - Polls on configurable interval (default: 60s)         │   │  │
│  │  └──────────────────────────────────────────────────────────┘   │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                    Connector Layer                               │  │
│  │                                                                  │  │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌──────────┐ │  │
│  │  │   Azure    │  │   Azure    │  │Kubernetes  │  │   Mock   │ │  │
│  │  │  Monitor   │  │  DevOps    │  │  API Server│  │   Data   │ │  │
│  │  │(Log Analyt.│  │(Builds,    │  │(Deployments│  │(Demo/Dev │ │  │
│  │  │ Metrics,   │  │ Releases,  │  │ Pods, Health│  │  mode)  │ │  │
│  │  │ Alerts)    │  │ ADO Items) │  │)           │  │          │ │  │
│  │  └────────────┘  └────────────┘  └────────────┘  └──────────┘ │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │                      Model Layer                                 │  │
│  │  MetricPoint | LogEntry | TraceSpan | ServiceHealth             │  │
│  │  Incident | Deployment | Pipeline | Slo                         │  │
│  │  SecurityFinding | PolicyViolation | DeploymentRiskReport       │  │
│  │  ObservabilityGap | RemediationSuggestion | AIOpsInsight        │  │
│  └─────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### Data Flow

```
Developer edits code
       │
       ├── Auto-scan on save ──► SastScanner + SecretsDetector + PolicyValidator
       │                              │
       │                              └── SecurityPanel (inline findings)
       │
       ├── Before commit ──────► DeploymentRiskScorer (git diff analysis)
       │                              │
       │                              └── AIOpsPanel (risk score + factors)
       │
       ├── Background poll ────► AIOpsEngine.RefreshAllAsync()
       │                              │
       │                              ├── Azure Monitor → logs, metrics, alerts
       │                              ├── Azure DevOps  → builds, releases
       │                              ├── Kubernetes    → deployment state
       │                              └── MockData      → demo data (if enabled)
       │
       └── Connector events ───► IncidentsRefreshed, SlosRefreshed,
                                  DeploymentsRefreshed, RiskReportGenerated
```

---

## 3. AI Safety Model

pfpad's AIOps AI engine operates under strict safety principles:

### 3.1 Evidence-Based Only

Every insight includes:
- **ConfidenceScore** (0.0–1.0): numeric certainty
- **ConfidenceLevel** (Low/Medium/High): human-readable tier
- **Evidence[]**: cited sources — log line, metric data point, deployment ID, rule match

If there is insufficient evidence (`HasSufficientEvidence == false`), the engine returns "Insufficient data to generate insight" rather than guessing.

### 3.2 Confidence Thresholds

| Score | Level | Displayed as |
|---|---|---|
| ≥ 0.8 | High | Green badge — high confidence |
| 0.5–0.79 | Medium | Yellow badge — review recommended |
| < 0.5 | Low | Gray badge — treat as hypothesis |

SAST/Secrets findings: **0.7–0.9** (pattern match confidence)
Risk scoring: **0.6–0.85** (depends on available operational data)
RCA hypotheses: **0.4–0.75** (always shown as hypothesis, never as fact)

### 3.3 Sensitive Data Redaction

The `DataRedactor` class automatically redacts before any data is displayed:
- Passwords in connection strings
- AWS access/secret keys
- Azure SAS tokens and signing keys
- Bearer tokens
- Private key content
- Generic long base64 secrets (40+ chars)

Redaction is **irreversible** — pfpad never sends raw secrets to any external service.

### 3.4 Production Safety Gates

Actions that affect production **require explicit user approval**:
- Applying a remediation to a production file
- Triggering a deployment from the Deployment Panel
- Restarting a Kubernetes pod
- Approving a pipeline run

The setting `RequireApprovalForProduction = true` (default) enforces this. The UI shows a **[Review & Apply]** button (not auto-apply) for any `RemediationRisk >= ReviewRequired`.

### 3.5 No Telemetry Exfiltration

pfpad does **not** send your code, logs, or secrets to any AI cloud service. All analysis is local:
- SAST, secrets, and policy scanning: 100% local regex/pattern matching
- Risk scoring: local git diff analysis + locally-cached operational data
- Log/metric display: pulled from your configured endpoints, displayed locally, never forwarded

---

## 4. Quick Start

### 4.1 First Launch (Demo Mode)

By default, pfpad starts in **Mock Data Mode** — a realistic demo environment with sample incidents, deployments, SLOs, and security findings. This lets you explore all features without any configuration.

1. Open pfpad
2. Press `Ctrl+Alt+A` or go to **AIOps → AIOps Hub** to open the hub panel
3. Explore the risk gauge, active incidents, SLO status, and recent deployments
4. Open any `.cs`, `.tf`, `.bicep`, or `.yaml` file and press `Ctrl+Alt+S` to run a security scan
5. View findings in **AIOps → Security Panel**

### 4.2 Connect to Real Integrations

Go to **AIOps → Settings** (`Ctrl+Alt+,`) and configure:
1. Disable **Mock Data Mode**
2. Enable **Azure Monitor** and enter your tenant/workspace credentials
3. Enable **Azure DevOps** and enter your organization/PAT
4. Optionally enable **Kubernetes** with your API server URL and service account token
5. Click **Save** — pfpad connects and polls every 60 seconds (configurable)

### 4.3 Scan a File

- **Auto-scan**: Enabled by default on every save. Results appear in the Security Panel.
- **Manual scan**: `Ctrl+Alt+S` scans the active file immediately.
- **Scan project**: Open the Command Palette (`Ctrl+Shift+P`) and run **"AIOps: Scan All Files"**.

### 4.4 Score Deployment Risk

Before committing staged changes:
1. Open **AIOps → AIOps Hub**
2. Click **[↺ Scan Now]** (or press `Ctrl+Alt+R`)
3. pfpad reads your git diff, factors in active incidents + SLO burn rates, and generates a **DeploymentRiskReport**
4. Review the risk factors and recommendations
5. If risk is **High or Critical**, the report recommends approval before deploying

---

## 5. AIOps Panel (Hub)

**Location:** `AIOps → AIOps Hub` | Keyboard: `Ctrl+Alt+A`

The hub is your production health dashboard embedded in the editor.

### 5.1 Risk Gauge

The large color-coded panel at the top shows:
```
RISK: 72 / HIGH
Last scanned: 2 minutes ago  [↺ Scan Now]
```

Colors:
- 🟢 **Low (0–25)**: Safe to deploy
- 🟡 **Medium (26–50)**: Review recommendations before deploying
- 🔴 **High (51–75)**: Address factors or get approval
- 🔴 **Critical (76–100)**: Block deployment, resolve incidents first

### 5.2 Active Incidents

Real-time list of active incidents from your monitoring system.

| Column | Description |
|---|---|
| Severity | Critical / High / Medium / Low badge |
| Title | Incident name |
| Service | Affected service |
| Age | How long since triggered |

Double-click an incident to open its URL (PagerDuty, Azure Monitor Alert, etc.)

### 5.3 SLO Status

| Column | Description |
|---|---|
| Service | Service name |
| SLO Name | e.g., "Availability 99.9%" |
| Current% | Current achievement |
| Target% | SLO target |
| Status | 🟢 Healthy / 🟡 AtRisk / 🔴 Breached |

**Burn rate** is shown in the tooltip — a burn rate > 1.0 means the error budget is being consumed faster than it replenishes.

### 5.4 Recent Deployments

Last 20 deployments across environments:

| Column | Description |
|---|---|
| Service | Deployed service |
| Env | prod / staging / dev |
| Version | Semantic version or commit SHA |
| Status | ✅ Succeeded / ❌ Failed / ↩ RolledBack |
| Age | When deployed |

### 5.5 Top Security Findings

The 5 most severe unresolved security findings from the last scan. Click any to navigate to the line in the editor.

---

## 6. Deployment Risk Scoring

**Keyboard:** `Ctrl+Alt+R`

pfpad analyzes your staged git changes against live operational state to produce a risk score before you deploy.

### 6.1 Risk Factors

| Factor | Trigger | Points Added |
|---|---|---|
| **Active Critical Incident** | Any Critical incident open | +40 |
| **Active High Incident** | Any High incident open | +25 |
| **Database Migration** | `.sql`, `*migration*`, `*Migration*` files changed | +25 |
| **Active Incidents (any)** | Any active incident | +10 |
| **SLO Burn Rate > 2x** | Any SLO burning budget at 2× normal rate | +25 |
| **Critical File Changed** | `Dockerfile`, `*.tf`, `*.bicep`, `appsettings.Production.json` changed | +20 |
| **SLO Burn Rate > 1.5x** | Any SLO at elevated burn rate | +15 |
| **Security Files Changed** | `*auth*`, `*security*`, `*crypto*`, `*jwt*` files changed | +15 |
| **No Tests in Change Set** | Changed files include no test files | +10 |
| **Large Change Set** | > 50 files changed | +30 |

**Score** = sum of applicable factors, capped at 100.

**Risk Level:**
- 0–25: Low
- 26–50: Medium
- 51–75: High
- 76–100: Critical

### 6.2 Reading the Report

```
DEPLOYMENT RISK REPORT
Score: 65 / HIGH    Confidence: 0.78 (High)    Generated: 14:32:01

FACTORS:
  🔴 HIGH  Active High Incident (+25 pts)
           Source: Azure Monitor • "API latency spike — payments service"
           Started: 47 minutes ago

  🟡 MED   SLO Burn Rate Elevated (+15 pts)  
           Source: Azure Monitor SLO
           payments-api Availability: 1.8x burn rate (budget: 23% remaining)

  🟡 MED   Critical File Changed (+20 pts)
           Source: Git diff
           terraform/payments/main.tf — infrastructure change

RECOMMENDATIONS:
  1. Resolve the active 'API latency spike' incident before deploying
  2. Review terraform changes for payments infrastructure carefully
  3. Consider deploying to staging first and monitoring for 15 minutes
  4. Notify on-call SRE before production deployment

CHANGED FILES: 8 files
  ⚠ terraform/payments/main.tf  
  ⚠ src/PaymentsService/PaymentProcessor.cs
  + src/PaymentsService/PaymentProcessor.Tests.cs
  ... 5 more
```

### 6.3 Approval Workflow

If risk is **High or Critical**, the report shows:
```
⚠ THIS DEPLOYMENT REQUIRES APPROVAL
[Request Approval] [Override with Justification]
```

Approvals are logged to the audit trail with timestamp, user, risk score, and justification.

---

## 7. Security Scanning (DevSecOps)

**Keyboard:** `Ctrl+Alt+S` (scan active file)

pfpad includes a built-in SAST (Static Application Security Testing) scanner that runs on every save or on demand.

### 7.1 SAST Rules

| Rule ID | Title | Severity | Languages |
|---|---|---|---|
| CWE-89 | SQL Injection | High | C#, SQL |
| CWE-78 | Command Injection | High | C#, Python, Shell |
| CWE-22 | Path Traversal | Medium | C# |
| CWE-338 | Insecure Random (in security context) | Medium | C# |
| CWE-327 | Weak Cryptography (MD5, SHA1, DES) | High | C# |
| CWE-798 | Hardcoded Credentials | Critical | All |
| CWE-601 | Open Redirect | Medium | C# |

### 7.2 Secrets Detection Rules

| Rule | Pattern | Severity |
|---|---|---|
| SECRET-AWS-KEY | `AKIA[0-9A-Z]{16}` | Critical |
| SECRET-AZURE-SAS | `sv=YYYY-MM-DD...sig=` | Critical |
| SECRET-AZURE-CONN | DefaultEndpointsProtocol connection strings | High |
| SECRET-GITHUB-TOKEN | `ghp_[A-Za-z0-9]{36}` | Critical |
| SECRET-GENERIC-API | `api_key=<20+ char value>` | High |
| SECRET-PRIVATE-KEY | `-----BEGIN PRIVATE KEY-----` | Critical |

> **All secret values are redacted** in the display. pfpad shows `[REDACTED]` in place of any detected secret. The location (file + line) is shown so you can fix it.

### 7.3 Remediation Suggestions

Each finding includes a specific remediation:
- **Hardcoded secret**: "Move to Azure Key Vault / user secrets (`dotnet user-secrets set`) / environment variable"
- **SQL injection**: "Use parameterized queries: `command.Parameters.AddWithValue(\"@param\", value)`"
- **Weak crypto**: "Replace MD5 with SHA-256: `SHA256.Create()`"
- **Path traversal**: "Validate path with `Path.GetFullPath()` and verify it's within allowed directory"

### 7.4 Security Panel

**Location:** `AIOps → Security Panel` | Keyboard: `Ctrl+Alt+E`

The Security Panel shows all findings from the last scan:
- Filter by Severity (All / Critical / High / Medium / Low)
- Filter by Category (All / Secret / Injection / Policy / Infrastructure)
- Double-click any finding → jump to that line in the editor
- Bottom pane shows full description + remediation + CWE link

---

## 8. Policy-as-Code Validation

pfpad validates infrastructure files against built-in policies automatically when you open or save them.

### 8.1 Supported File Types

| File Type | Policies Applied |
|---|---|
| `*.bicep` | Required tags, no public exposure, encryption at rest, TLS 1.2+ |
| `*.tf`, `*.tfvars` | Required tags, no public storage access, HTTPS only |
| `*.yaml` (K8s) | No privileged containers, resource limits required, approved image registries, no wildcard RBAC |
| `Dockerfile` | No root USER, approved base images |

### 8.2 Built-in Policies

| Policy ID | Name | Severity | Description |
|---|---|---|---|
| POL-001 | Required Tags | Medium | Resources must have `environment` and `owner` tags |
| POL-002 | No Public Exposure | High | No public IP allocation without NSG |
| POL-003 | Encryption at Rest | High | Storage accounts must have encryption enabled |
| POL-004 | TLS Minimum 1.2 | High | No TLS 1.0 or 1.1 configurations |
| POL-005 | No Root Containers | High | Dockerfile must not run as root (`USER root`) |
| POL-006 | Resource Limits | Medium | K8s containers must define CPU and memory limits |
| POL-007 | Approved Image Registries | Medium | Images must come from `mcr.microsoft.com`, `registry.hub.docker.com`, or your private registry |
| POL-008 | No Wildcard RBAC | Critical | ClusterRole must not grant `*` verbs on `*` resources |

### 8.3 Policy Violation Example

```bicep
// main.bicep — line 12
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  // ⚠ POL-001 MEDIUM: Missing required tags 'environment' and 'owner'
  // ⚠ POL-003 HIGH: Encryption at rest not explicitly configured
  name: 'mystorageaccount'
  location: resourceGroup().location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}
```

---

## 9. Telemetry Panel

**Location:** `AIOps → Telemetry Panel` | Keyboard: `Ctrl+Alt+T`

The Telemetry Panel shows live logs, metrics, and traces from your configured monitoring backend.

### 9.1 Logs Tab

Shows recent log entries for the selected service:
```
[14:31:55] [INFO]  Processing payment request for order ORD-1234
[14:31:56] [WARN]  High latency detected: 450ms (threshold: 200ms)
[14:31:57] [ERROR] Payment gateway timeout after 30000ms — TraceId: abc123
```

> Sensitive data is redacted automatically. Passwords, tokens, and keys are never shown.

**Filtering:**
- Service name: type to filter by service
- Time range: Last 1h / 6h / 24h / 7 days
- Log level: All / Error / Warning / Info

### 9.2 Metrics Tab

Shows metric time series data:

| Metric | Value | Unit | Time | Dimensions |
|---|---|---|---|---|
| requests/duration | 452.3 | ms | 14:31:55 | service=payments |
| exceptions/count | 3 | count/min | 14:31:00 | |
| availability | 99.87 | % | 14:31:00 | region=eastus |

### 9.3 Traces Tab

Distributed trace tree:
```
TraceId: abc123def456
└── payments-api / POST /api/payments  [450ms] ❌
    ├── database / SELECT payments  [12ms] ✅
    ├── payments-gateway / POST /charge  [30001ms] ❌ TIMEOUT
    └── notifications / POST /send  [8ms] ✅
```

Double-click any span to see its full tags and attributes.

---

## 10. Incident Correlation

pfpad correlates active and recent incidents with the code you are editing.

### 10.1 How It Works

1. The AIOps Engine polls your incident management system (Azure Monitor Alerts, PagerDuty via webhook, or ADO work items)
2. When you open a file, pfpad checks if any open incidents mention the service name inferred from the file's namespace/directory
3. If a match is found, an indicator appears in the AIOps Hub and in the file's gutter

### 10.2 Incident Root Cause Analysis (RCA)

The `IncidentAnalyzer` generates a hypothesis for each active incident:
- Cross-correlates incident start time with recent deployments
- Checks if recently changed files overlap with the affected service
- Queries error logs at incident start time for exception patterns
- Scores each hypothesis by confidence

Example hypothesis:
```
INCIDENT: Payment API latency spike
RCA HYPOTHESIS (confidence: 0.67 — Medium):
  Most likely cause: Deployment #4521 (payments-api v1.4.2) at 14:15 UTC
  Evidence:
  - Incident started 3 minutes after deployment
  - Exception logs show "NullReferenceException in PaymentProcessor.ProcessAsync"
  - 3 recent deployments to payments-api in last 24h (elevated change frequency)
  
  NOTE: This is a hypothesis, not a confirmed root cause.
  Confidence is Medium due to circumstantial timing evidence only.
```

### 10.3 Correlation to Code

When a correlated incident is found for the active file, the AIOps Hub shows:
```
⚠ ACTIVE INCIDENT CORRELATED TO THIS FILE
  [HIGH] API latency spike — payments-api (47 min ago)
  Possibly related to: PaymentProcessor.cs (line 142)
  [View Incident] [View RCA]
```

---

## 11. SLO Monitoring

pfpad shows Service Level Objective (SLO) status and burn rates from your monitoring backend.

### 11.1 SLO Concepts

- **SLO**: A target for service reliability (e.g., "99.9% availability over 30 days")
- **Error Budget**: The allowed downtime within the SLO window (0.1% = ~43 min/month for 99.9%)
- **Burn Rate**: How fast the error budget is being consumed. `1.0x` = burning at normal rate. `2.0x` = burning twice as fast, will exhaust budget in half the time.

### 11.2 SLO Status in pfpad

The AIOps Hub SLO section shows:

| Service | SLO | Current | Target | Status | Burn Rate |
|---|---|---|---|---|---|
| payments-api | Availability | 99.93% | 99.9% | 🟢 Healthy | 0.7x |
| search-service | P99 Latency | 98.1% | 99.5% | 🔴 Breached | 3.2x |
| auth-service | Availability | 99.85% | 99.9% | 🟡 AtRisk | 1.4x |

### 11.3 SLO Impact on Deployment Risk

If **any SLO has a burn rate > 1.5x**, deployment risk is automatically elevated. The risk scorer will:
- Add +15 points (burn rate 1.5–2x) or +25 points (burn rate > 2x)
- Include SLO burn rate as a risk factor in the report with evidence citing the metric

---

## 12. Observability Advisor

**Keyboard:** `Ctrl+Alt+O` (analyze current file)

The Observability Advisor analyzes your C# code for missing OpenTelemetry (OTel) instrumentation and suggests specific code additions.

### 12.1 What It Detects

| Pattern | Detection | Suggested Fix |
|---|---|---|
| Missing Activity span | HTTP handler method without `Activity.StartActivity()` | Add span around the operation |
| Unlogged exception | `throw` statement without nearby `ILogger.LogError()` | Add structured log before throw |
| Missing metrics | Method named `Process*`, `Calculate*`, `Handle*` with no counter/histogram | Add OTel counter |
| Untracked HTTP call | `await _http.GetAsync()` without activity context propagation | Add activity tags |
| Missing correlation | `CancellationTokenSource` created without propagating activity context | Pass existing context |

### 12.2 Example Gap

```csharp
// Detected gap in PaymentProcessor.cs line 23:
// "Missing Activity span for HTTP handler"
// Category: Tracing | Confidence: 0.82 (High)
//
// Suggested code to add:
using var activity = ActivitySource.StartActivity("ProcessPayment");
activity?.SetTag("order.id", request.OrderId);
activity?.SetTag("amount", request.Amount);
```

### 12.3 Viewing Gaps

Gaps appear in the **Insights Panel** (`Ctrl+Alt+I`) with:
- Category badge (Tracing / Logging / Metrics)
- Description of what's missing
- Suggested code snippet (copy-pasteable)
- Confidence score

Gaps are sorted by severity — HTTP handlers without any tracing are highest priority.

---

## 13. Deployment Panel (CI/CD)

**Location:** `AIOps → Deployment Panel` | Keyboard: `Ctrl+Alt+D`

The Deployment Panel shows your CI/CD pipelines and deployment history.

### 13.1 Pipelines Tab

Real-time pipeline status from Azure DevOps or GitHub Actions:

```
🔵 main
└── ✅ Build-and-Deploy  Started: 14:20  Duration: 8m 32s
    ├── ✅ Restore & Build  [1m 12s]
    ├── ✅ Test             [3m 45s]
    ├── ✅ Security Scan    [45s]
    ├── ✅ Deploy to Dev    [1m 20s]
    └── ⏳ Deploy to Staging [running...]

🔴 feature/payment-refactor
└── ❌ Build-and-Deploy  Started: 13:55  Duration: 4m 20s
    ├── ✅ Restore & Build  [1m 10s]
    └── ❌ Test             [3m 10s]  ← 12 tests failed
```

### 13.2 Deployments Tab

| Environment | Service | Version | Status | Deployed By | When |
|---|---|---|---|---|---|
| prod | payments-api | v1.4.1 | ✅ Succeeded | ci-bot | 2h ago |
| staging | payments-api | v1.4.2 | ✅ Succeeded | ci-bot | 1h ago |
| dev | payments-api | v1.4.3 | ✅ Succeeded | john.doe | 30m ago |
| prod | auth-service | v2.1.0 | ↩ RolledBack | ci-bot | 4h ago |

### 13.3 Deployment Correlation

When a deployment matches your currently open file's service, pfpad highlights it and shows it in the AIOps Hub's incident correlation section.

---

## 14. Insights Panel

**Location:** `AIOps → Insights Panel` | Keyboard: `Ctrl+Alt+I`

The Insights Panel aggregates all AI-generated insights in one place with confidence scores and evidence citations.

### 14.1 Insight Cards

Each insight card shows:
```
┌─────────────────────────────────────────────────────────┐
│ 💡 Missing OpenTelemetry tracing in PaymentProcessor.cs │
│                                                          │
│ Method ProcessAsync handles HTTP requests but has no    │
│ Activity span. This makes it invisible in traces when   │
│ debugging latency issues.                               │
│                                                          │
│ Confidence: ████████░░ 0.82 (High)                      │
│ Evidence: Code analysis • 1 source                      │
│                                                          │
│                              [Copy Snippet] [Apply Fix] │
└─────────────────────────────────────────────────────────┘
```

### 14.2 Insight Types

| Type | Icon | Source |
|---|---|---|
| Observability Gap | 📡 | Code analysis |
| Deployment Risk Factor | ⚠️ | Git diff + operational data |
| Incident Correlation | 🚨 | Incident management system |
| Security Finding | 🔒 | SAST scanner |
| Remediation Suggestion | 💊 | Risk + security analysis |
| SLO Warning | 📊 | SLO monitoring |

### 14.3 Applying Remediations

Remediations are classified by risk:
- **Safe** (green): Code-only change, no external effects. [Apply Fix] button available.
- **Review Required** (yellow): May affect behavior. [Review & Apply] requires you to review diff first.
- **Production Impact** (red): Affects production infrastructure. Requires approval workflow.

---

## 15. Integrations Setup

### 15.1 Azure Monitor

**Prerequisites:**
- Azure subscription
- Log Analytics Workspace
- Service principal with `Monitoring Reader` role

**Steps:**
1. Go to **AIOps → Settings → Azure Monitor**
2. Enter **Tenant ID**: found in Azure Active Directory → Overview
3. Enter **Client ID** and **Client Secret**: create a service principal in AAD App Registrations
4. Enter **Subscription ID**: found in Subscriptions blade
5. Enter **Resource Group**: your application's resource group
6. Enter **Workspace ID**: Log Analytics workspace → Agents → Workspace ID
7. Click **[Test Connection]** — should show "Connected: {workspace name}"

**What pfpad reads from Azure Monitor:**
- Log Analytics: recent log entries via KQL queries
- Metrics API: error rates, latency, availability
- Alerts Management API: active alerts → incident list
- Application Insights: request traces, exceptions, dependencies

### 15.2 Azure DevOps

**Prerequisites:**
- Azure DevOps organization
- Personal Access Token (PAT) with `Build (Read)` and `Release (Read)` scopes

**Steps:**
1. Go to **AIOps → Settings → Azure DevOps**
2. Enter **Organization**: your ADO organization name (e.g., `mycompany`)
3. Enter **Project**: the project name (e.g., `MyApp`)
4. Enter **Personal Access Token**: create in ADO User Settings → Personal Access Tokens
   - Required scopes: `Build: Read`, `Release: Read`, `Work Items: Read`
5. Click **[Test Connection]**

**What pfpad reads from Azure DevOps:**
- Build API: recent pipeline runs, stage details
- Release API: release deployments and environments
- Work Items API: active bugs/incidents (mapped to Incident model)

### 15.3 Kubernetes

**Prerequisites:**
- Kubernetes API server accessible from developer machine (or via `kubectl proxy`)
- Service account token with read access to deployments and pods

**Steps:**
1. Go to **AIOps → Settings → Kubernetes**
2. Enter **API Server URL**: e.g., `https://myaks.eastus.azmk8s.io:443`
   - Or use `kubectl proxy` and set URL to `http://localhost:8001`
3. Enter **Bearer Token**: from `kubectl get secret <sa-secret> -o jsonpath='{.data.token}' | base64 -d`
4. Enter **Namespace**: your application namespace (e.g., `production`)
5. If using self-signed certs, check **Skip TLS Verify** (not recommended for production)
6. Click **[Test Connection]**

**What pfpad reads from Kubernetes:**
- Deployments: name, image version, replica status, conditions
- Pods: crash loops, OOMKilled, pending states → mapped to incidents

### 15.4 Mock Data Mode (Demo/Offline)

Mock Data Mode provides realistic demo data without any external connections. Useful for:
- Exploring pfpad features offline
- Developer demos and presentations
- CI/CD environments without live monitoring
- Testing your team's pfpad configuration

**To enable:** Go to **AIOps → Settings → General** and check **Enable Mock Data Mode**.

Mock data includes:
- 2 active incidents (1 Critical, 1 Medium)
- 3 SLOs (1 Healthy, 1 AtRisk, 1 Breached)
- 5 recent deployments across dev/staging/prod
- 4 recent pipeline runs
- Sample logs, metrics, and traces for `payments-api` and `auth-service`

---

## 16. Developer Workflow Guides

### 16.1 Workflow: Before You Commit

**Recommended workflow for every commit:**

1. **Save your file** — auto-scan runs SAST + secrets + policy scan
2. Check **Security Panel** (`Ctrl+Alt+E`) — resolve any Critical/High findings
3. **Score deployment risk** (`Ctrl+Alt+R`) — review risk report
4. If risk is High/Critical: address the listed factors or document justification
5. Open **Insights Panel** (`Ctrl+Alt+I`) — apply any Safe observability fixes
6. Commit only when security panel shows 0 Critical/High findings OR with documented exception

### 16.2 Workflow: Investigating a Production Incident

1. Open **AIOps Hub** (`Ctrl+Alt+A`) — check Active Incidents section
2. Double-click the incident to open it in your incident management system
3. Open **Telemetry Panel** (`Ctrl+Alt+T`) — query logs for the affected service around incident start time
4. Look at the Insights Panel (`Ctrl+Alt+I`) — check for RCA hypothesis
5. Navigate to the correlated file in the editor — the AIOps Hub will highlight any correlated functions
6. Review **recent deployments** — did a deploy precede the incident?
7. Make fix → **score risk** before deploying the fix (`Ctrl+Alt+R`)

### 16.3 Workflow: Infrastructure-as-Code Review (Bicep/Terraform)

1. Open your `.bicep` or `.tf` file
2. **Auto-policy scan** runs on open — check Security Panel for policy violations
3. Run `Ctrl+Alt+S` for a full scan including SAST + policy
4. Resolve all Critical/High policy violations before PR
5. Check **POL-001** (required tags) — ensure `environment` and `owner` tags present
6. Check **POL-003** (encryption) — verify storage encryption configured
7. Check **SEC-TF-001/002** (public access, HTTPS) for Azure resources

### 16.4 Workflow: Kubernetes Manifest Review

1. Open your K8s manifest `.yaml` file
2. pfpad auto-scans for:
   - **SEC-K8S-001**: Privileged containers (`privileged: true`)
   - **SEC-K8S-002**: `image: *:latest` tags (non-deterministic deployments)
   - **POL-006**: Missing CPU/memory resource limits
   - **POL-007**: Images from unapproved registries
   - **POL-008**: Wildcard RBAC (`verbs: ["*"]`)
3. Click any finding → jump to that line and apply remediation

### 16.5 Workflow: Adding Observability to New Code (C#)

1. Write your new service class / HTTP handler
2. Save the file — ObservabilityAdvisor runs automatically
3. Open **Insights Panel** (`Ctrl+Alt+I`) — look for observability gap cards
4. For each gap:
   - Review the suggested snippet
   - Click **[Apply Fix]** for Safe remediations OR copy the snippet manually
5. Ensure your class has:
   - `ActivitySource` field and `using var activity = ...` in public methods
   - `ILogger<T>` injected and used at Error/Warning level
   - Counter/histogram metrics for key operations

### 16.6 Workflow: SLO-Aware Development

1. Open **AIOps Hub** — check SLO Status section
2. If any SLO shows **AtRisk** or **Breached**:
   - Pause non-critical feature work in the affected service
   - Prioritize reliability work
   - Check error budget remaining — < 10% = freeze all non-SLO work
3. Before deploying to a service with elevated burn rate:
   - Risk scorer will automatically add +15–25 points
   - Consider deploying to staging and monitoring burn rate for 30+ minutes first
4. After a fix deploys:
   - Monitor SLO burn rate in Telemetry Panel
   - Confirm burn rate returning toward 1.0x before next deploy

---

## 17. Keyboard Reference

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+A` | Open AIOps Hub Panel |
| `Ctrl+Alt+T` | Open Telemetry Panel |
| `Ctrl+Alt+E` | Open Security Panel |
| `Ctrl+Alt+D` | Open Deployment Panel |
| `Ctrl+Alt+I` | Open Insights Panel |
| `Ctrl+Alt+S` | Scan active file (security + policy) |
| `Ctrl+Alt+R` | Score deployment risk (git diff) |
| `Ctrl+Alt+O` | Analyze observability gaps in active file |
| `Ctrl+Alt+,` | Open AIOps Settings |

---

## 18. Troubleshooting

### "Not connected" in AIOps Hub

- Check **AIOps → Settings** — verify at least one connector is enabled
- If all are disabled, enable **Mock Data Mode** for demo data
- Click **[Test Connection]** in each connector's settings tab
- Check firewall — pfpad needs outbound HTTPS to Azure/ADO endpoints

### Azure Monitor: "Failed to get token"

- Verify Tenant ID, Client ID, and Client Secret are correct
- Ensure the service principal has `Monitoring Reader` role on the subscription
- Check that the service principal is not expired (Azure AD → App Registrations → Certificates & Secrets)

### Azure DevOps: "Unauthorized (401)"

- PAT may have expired — create a new one (they default to 1-year expiry)
- Verify PAT has `Build: Read` and `Release: Read` scopes
- Ensure Organization and Project names are correct (case-sensitive)

### Kubernetes: "Connection refused"

- Verify API server URL is reachable from your machine
- If using kubectl proxy: run `kubectl proxy --port=8001` first, then set URL to `http://localhost:8001`
- Bearer token may have expired — rotate the service account token
- Try enabling **Skip TLS Verify** if using self-signed certificates (dev/staging only)

### Security scan shows no results

- Verify the file extension is supported (`.cs`, `.py`, `.sh`, `.tf`, `.bicep`, `.yaml`, `.json`)
- Check **AIOps → Settings → General** — confirm **Auto-scan on save** is enabled
- Run `Ctrl+Alt+S` to scan manually
- Check the Security Panel filter — it may be set to show only Critical/High (change to All)

### Risk score always shows 0

- Risk scoring requires git to be initialized in the workspace
- Ensure the workspace folder is opened (Workspace Panel shows files)
- Stage or commit some changes before running `Ctrl+Alt+R`
- If no git history exists, mock data provides a sample risk report

---

*pfpad AIOps Manual — v1.0*
*"Evidence-based, explainable, confidence-scored, and safe."*

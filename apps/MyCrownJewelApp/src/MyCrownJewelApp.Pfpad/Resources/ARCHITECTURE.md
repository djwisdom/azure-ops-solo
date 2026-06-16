# pfpad AIOps — Architecture & Design Reference

> **Audience:** developers extending pfpad, contributors adding new connectors or engines, and technically curious users who want to understand what happens under the hood when AIOps features run.

---

## Table of Contents

1. [Design Philosophy](#1-design-philosophy)
2. [Layer Diagram](#2-layer-diagram)
3. [Settings & Secret Storage](#3-settings--secret-storage)
4. [Connector Layer](#4-connector-layer)
   - 4.1 [IAIOpsConnector — the core contract](#41-iaiopconnector--the-core-contract)
   - 4.2 [Optional capability interfaces](#42-optional-capability-interfaces)
   - 4.3 [Connector catalogue](#43-connector-catalogue)
   - 4.4 [Connector priority and fallback](#44-connector-priority-and-fallback)
5. [AIOpsEngine — the Orchestration Hub](#5-aiopengine--the-orchestration-hub)
   - 5.1 [Startup and poll loop](#51-startup-and-poll-loop)
   - 5.2 [RefreshAllAsync — the data pipeline](#52-refreshallasync--the-data-pipeline)
   - 5.3 [Deduplication strategy](#53-deduplication-strategy)
   - 5.4 [On-demand operations](#54-on-demand-operations)
6. [Engine Subsystems](#6-engine-subsystems)
   - 6.1 [CorrelationEngine](#61-correlationengine)
   - 6.2 [AnomalyDetector](#62-anomalydetector)
   - 6.3 [RootCauseAnalyzer](#63-rootcauseanalyzer)
   - 6.4 [DeploymentRiskScorer](#64-deploymentriskscorerr)
   - 6.5 [OpsQueryEngine](#65-opsqueryengine)
   - 6.6 [SastScanner & SecretsDetector](#66-sastscanner--secretsdetector)
   - 6.7 [PolicyValidator](#67-policyvalidator)
   - 6.8 [ObservabilityAdvisor](#68-observabilityadvisor)
   - 6.9 [ServiceContextEngine](#69-servicecontextengine)
   - 6.10 [OTelCodeGenerator](#610-otelcodegenerator)
   - 6.11 [RunbookEngine](#611-runbookengine)
   - 6.12 [AuditLogger](#612-auditlogger)
7. [UI Layer](#7-ui-layer)
   - 7.1 [Panel inventory](#71-panel-inventory)
   - 7.2 [Sidebar layout hierarchy](#72-sidebar-layout-hierarchy)
   - 7.3 [Hint bar system](#73-hint-bar-system)
   - 7.4 [Theme propagation](#74-theme-propagation)
8. [End-to-End Data Flow](#8-end-to-end-data-flow)
9. [Security Model](#9-security-model)
10. [Adding a New Connector](#10-adding-a-new-connector)
11. [Adding a New Engine Subsystem](#11-adding-a-new-engine-subsystem)

---

## 1. Design Philosophy

pfpad's AIOps layer is built around three principles:

### Evidence before opinion
Every insight, score, or recommendation traces back to a concrete data source — a commit SHA, a deployment record, a metric series, an alert timestamp. Wherever data is absent, the response is labelled `isGuess = true` and carries an explicit disclaimer. There is no hidden interpolation.

### Connectors are optional and composable
pfpad works without any connector configured. The `MockDataConnector` provides rich sample data so users can explore all panels and workflows before connecting to a live environment. When real connectors are enabled, they stack on top — data from multiple connectors is merged and deduplicated, not last-write-wins.

### Secrets never touch disk in plaintext
All API tokens, client secrets, and passwords are encrypted using Windows DPAPI before being written to the settings file. Plaintext values live only in memory while connectors are running.

---

## 2. Layer Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         UI Layer (WinForms)                     │
│  AIOpsPanel · TelemetryPanel · SecurityPanel · InsightsPanel   │
│  IncidentTimelinePanel · DeploymentPanel · OpsQueryPanel       │
│  PullRequestRiskPanel · ServiceDependencyPanel · RunbookPanel  │
└───────────────────────────┬─────────────────────────────────────┘
                            │ events (IncidentsRefreshed, etc.)
                            │ method calls (ScanFileAsync, Chat, …)
┌───────────────────────────▼─────────────────────────────────────┐
│                       AIOpsEngine                               │
│  Orchestrates all subsystems. Owns the poll timer.             │
│                                                                 │
│  CorrelationEngine  AnomalyDetector   RootCauseAnalyzer        │
│  DeploymentRiskScorer  OpsQueryEngine  ServiceContextEngine    │
│  SastScanner  SecretsDetector  PolicyValidator                  │
│  ObservabilityAdvisor  OTelCodeGenerator  RunbookEngine        │
│  AuditLogger                                                    │
└───────────────────────────┬─────────────────────────────────────┘
                            │ IAIOpsConnector + optional interfaces
┌───────────────────────────▼─────────────────────────────────────┐
│                     Connector Layer                             │
│  AzureMonitorConnector   AzureDevOpsConnector                  │
│  GitHubActionsConnector  KubernetesConnector                   │
│  PrometheusConnector     PagerDutyConnector                    │
│  MockDataConnector                                              │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTPS / REST / az CLI token
┌───────────────────────────▼─────────────────────────────────────┐
│               External Services (internet / intranet)           │
│  Azure Monitor / App Insights  ·  Azure DevOps                 │
│  GitHub  ·  Kubernetes API  ·  Prometheus  ·  PagerDuty        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Settings & Secret Storage

`AIOpsSettings` is the root configuration object. It is serialised to JSON by `SettingsService` and lives in the user profile directory alongside other pfpad settings.

### DPAPI protection flow

```
User fills in API token in Settings dialog
        │
        ▼
AIOpsSettings.CreateEncryptedCopy()
  ├─ Plaintext field  → cleared ("")
  └─ EncryptedX field → DpapiSettingsProtector.Protect(plaintext)
        │
        ▼
JSON written to disk (EncryptedX fields only, plaintext = "")

On next launch:
AIOpsSettings.LoadSecretsFromEncrypted()
  ├─ DpapiSettingsProtector.Unprotect(EncryptedX)
  └─ Plaintext field restored in memory only
```

DPAPI encryption is machine- and user-scoped. The encrypted blob cannot be decrypted on a different machine or by a different Windows user account, even with access to the settings file.

### Settings fields per connector

| Connector | Credentials stored |
|---|---|
| Azure Monitor | `ClientSecret` (Service Principal) or `az login` token; `AppInsightsKey` |
| Azure DevOps | `PersonalAccessToken` |
| Kubernetes | `BearerToken` |
| Prometheus | `BearerToken` or `Password` |
| PagerDuty | `ApiToken` |
| GitHub Actions | `PersonalAccessToken` |

---

## 4. Connector Layer

### 4.1 `IAIOpsConnector` — the core contract

Every connector implements this interface:

```csharp
public interface IAIOpsConnector : IDisposable
{
    string Name { get; }
    ConnectorStatus Status { get; }        // Disconnected | Connecting | Connected | Error

    Task<ConnectorState> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    Task<IReadOnlyList<MetricPoint>>  GetMetricsAsync(…);
    Task<IReadOnlyList<LogEntry>>     GetLogsAsync(…);
    Task<IReadOnlyList<TraceSpan>>    GetTracesAsync(…);
    Task<ServiceHealth?>              GetServiceHealthAsync(…);
    Task<IReadOnlyList<Incident>>     GetActiveIncidentsAsync(…);
    Task<IReadOnlyList<Incident>>     GetRecentIncidentsAsync(…);
    Task<IReadOnlyList<Deployment>>   GetRecentDeploymentsAsync(…);
    Task<IReadOnlyList<Pipeline>>     GetPipelineRunsAsync(…);
    Task<IReadOnlyList<Slo>>          GetSlosAsync(…);
}
```

`ConnectAsync` is idempotent. If the connector is already connected it returns the current state immediately.

### 4.2 Optional capability interfaces

Capabilities that only some backends support are expressed as additional interfaces rather than bloating the core contract. `AIOpsEngine.RefreshAllAsync` checks each connector against these with `is`:

| Interface | Method | Implemented by |
|---|---|---|
| `IAlertCapable` | `GetRecentAlertsAsync` | AzureMonitor, PagerDuty, MockData |
| `IPullRequestCapable` | `GetRecentPullRequestsAsync` | AzureDevOps, GitHubActions, MockData |
| `IGitHistoryCapable` | `GetRecentCommitsAsync` | AzureDevOps, GitHubActions, MockData |
| `IServiceCatalogCapable` | `GetServiceDependencyGraphAsync`, `GetServiceCatalogAsync` | Kubernetes, MockData |
| `IRunbookCapable` | `GetRunbooksAsync` | AzureDevOps, MockData |
| `IFeatureFlagCapable` | `GetFeatureFlagsAsync` | (reserved for future connectors) |
| `IServiceDiscoveryCapable` | `GetObservableServicesAsync` | AzureMonitor, Prometheus, MockData |

Adding a capability to a connector without touching the engine is the intended extension point.

### 4.3 Connector catalogue

| Connector | Primary data | Auth |
|---|---|---|
| **AzureMonitorConnector** | Logs (KQL via Log Analytics), metrics, App Insights traces, alerts, SLOs | Service Principal (Client ID + Secret) or `az login` cached token |
| **AzureDevOpsConnector** | Pipelines, deployments, work items, commits, pull requests, runbooks | PAT |
| **GitHubActionsConnector** | Workflow runs, deployments, pull requests, commits | PAT |
| **KubernetesConnector** | Pod health, service dependency graph, resource metrics | Bearer token against API server |
| **PrometheusConnector** | Custom metric series, error-rate and latency queries | Optional Bearer/Basic |
| **PagerDutyConnector** | Active incidents, recent incidents, on-call schedules, alerts | REST API token |
| **MockDataConnector** | Deterministic synthetic data for all interfaces | None — always connected |

### 4.4 Connector priority and fallback

When `TelemetryPanel` calls `BrowseServicesAsync`, the connector it uses is resolved by `UpdateAIOpsTelemetryConnector` in `Form1.cs`:

```
Priority rule:
  If any non-MockDataConnector exists → use the first one (prefer real data)
  Else → fall back to MockDataConnector

Connection handling:
  If the chosen connector is Disconnected → auto-connect before querying
  If IServiceDiscoveryCapable → call GetObservableServicesAsync
  Else → prompt user to type service name manually
```

This guarantees that once a real connector is configured, mock data never silently wins.

---

## 5. AIOpsEngine — the Orchestration Hub

`AIOpsEngine` is instantiated once per `Form1` session and held as `_aiOpsEngine`. It owns:
- the connector list
- all engine subsystems
- the poll timer
- the cached last-known state (incidents, deployments, SLOs, etc.)
- a semaphore that prevents concurrent refresh runs

### 5.1 Startup and poll loop

```
Form1 loads AIOpsSettings from disk
  → LoadSecretsFromEncrypted()
  → _aiOpsEngine.Configure(settings)
      → RebuildConnectors()
          → disposes old connectors
          → creates new connectors from enabled settings
          → fires Task.Run to ConnectAsync all non-mock connectors in background
      → sets poll timer interval
  → _aiOpsEngine.Start()
      → starts poll timer
      → fires initial RefreshAllAsync in background
```

The poll interval defaults to 60 seconds and is configurable. The lock `_refreshLock` (semaphore, max=1) ensures that if a refresh is still running when the timer fires again, the second refresh is skipped rather than queued.

### 5.2 `RefreshAllAsync` — the data pipeline

```
For each connector:
  1. ConnectAsync if not already connected
  2. Raise ConnectorStateChanged event (UI updates status badge)
  3. Skip connector on error (non-fatal; other connectors continue)
  4. Fetch: incidents, deployments, pipelines, SLOs (core interface)
  5. Fetch optional: alerts, commits, PRs, runbooks (capability checks)

After all connectors:
  6. Deduplicate each collection (see §5.3)
  7. DetectRecentAnomaliesAsync → queries last 2h of error_rate + latency_ms metrics
  8. CorrelationEngine.BuildChains → links commits → PRs → builds → deployments → incidents
  9. Fire events: IncidentsRefreshed, DeploymentsRefreshed, PipelinesRefreshed,
                  SlosRefreshed, AlertsRefreshed, AnomaliesDetected,
                  CorrelationChainsUpdated
```

All UI updates from these events are marshalled to the UI thread by the individual panels (via `Invoke` / `BeginInvoke`).

### 5.3 Deduplication strategy

Multiple connectors may return overlapping data (e.g., Azure Monitor and PagerDuty both return the same incident). Each collection is deduplicated by a stable key before storage:

| Collection | Dedup key |
|---|---|
| Incidents | `Id` or `{Title}:{AffectedService}:{StartedAt:O}` |
| Deployments | `Id` or `{Service}:{Version}:{Environment}:{DeployedAt:O}` |
| Pipelines | `Id` or `{Name}:{Branch}:{StartedAt:O}` |
| SLOs | `Id` or `{ServiceName}:{Name}` |
| Alerts | `Id` or `{RuleName}:{ServiceName}:{FiredAt:O}` |
| Commits | `Sha` or `{Author}:{Timestamp:O}:{Message}` |
| Pull Requests | `Id` or `{Title}:{CreatedAt:O}` |
| Runbooks | `Id` or `{Name}:{ServiceName}` |

Within each duplicate group, the most recent record wins.

### 5.4 On-demand operations

Operations that are user-triggered (not part of the poll loop) are thin wrappers that call a subsystem and raise an event:

| Method | Subsystem called | Event raised |
|---|---|---|
| `ScanFileAsync` | `SastScanner` + `SecretsDetector` + `PolicyValidator` | `SecurityFindingsFound`, `PolicyViolationsFound` |
| `ScoreDeploymentRiskAsync` | `DeploymentRiskScorer` | `RiskReportGenerated` |
| `AnalyzeObservabilityAsync` | `ObservabilityAdvisor` | `ObservabilityGapsFound` |
| `AnalyzeIncidentAsync` | `RootCauseAnalyzer` | `RootCauseAnalyzed` |
| `Chat` | `ServiceContextEngine` + `OpsQueryEngine` | — (returns response directly) |
| `GenerateOTelCode` | `OTelCodeGenerator` | — (returns string directly) |
| `GetOperationalContext` | `ServiceContextEngine` | `InlineAnnotationsGenerated` |

---

## 6. Engine Subsystems

### 6.1 CorrelationEngine

**Purpose:** Builds `CorrelationChain` objects — temporal narratives that link causally-related events across different signal types.

**Algorithm:**
1. Assigns every event (commit, PR, build, deployment, incident, alert, anomaly) to a service bucket. Service name is either explicit (deployment has `Service`) or inferred from changed file paths and commit messages.
2. Within each service bucket, events are sorted by timestamp.
3. Events within a configurable **correlation window** (default 4 hours) are grouped into a chain.
4. Chain severity = highest severity of any contained event.

A chain with `commit → deployment (failed) → incident (critical)` appearing within 4 hours tells a clear regression story. The `IncidentTimelinePanel` renders this chain visually.

### 6.2 AnomalyDetector

**Purpose:** Detects statistical anomalies in metric time-series using a Z-score approach.

**Algorithm:**
- Fetches the last 2 hours of `error_rate` and `latency_ms` for each recently-active service.
- Computes mean and standard deviation of the series.
- Flags any point where `|value - mean| > 2 × stddev` as an anomaly.
- Anomaly type: `ErrorRateSpike` or `LatencyIncrease` depending on the metric.

Anomaly events feed into `CorrelationEngine` and `RootCauseAnalyzer`.

### 6.3 RootCauseAnalyzer

**Purpose:** Generates ranked hypotheses for why an incident occurred.

**Hypothesis sources (in confidence order):**

| Hypothesis | Trigger condition | Confidence |
|---|---|---|
| Deployment regression | A deployment to the same service occurred within 60 min before the incident | 0.78 – 0.88 |
| Risky commit | A commit touching high-risk files (infra, config, migrations) within 2h before incident | 0.65 – 0.72 |
| Anomaly correlation | An anomaly on the same service preceded the incident | 0.60 |
| Related alert | An alert on the same service fired before the incident | 0.55 |
| Dependency cascade | A related service has a concurrent critical incident | 0.50 |

Each hypothesis carries a list of `Evidence` records (with URLs and timestamps) and `RemediationSuggestion` records. `HasSufficientData` is `false` when no hypotheses were found; the caller should show a disclaimer.

### 6.4 DeploymentRiskScorer

**Purpose:** Assigns a 0–100 risk score to a pending deployment based on code and operational context.

**Scoring factors:**

| Factor | Score added | Trigger |
|---|---|---|
| Large footprint | +30 | > 50 changed files |
| Critical files changed | +20 | infra / config / migration / container files |
| Active critical incident | +40 | any open critical incident on the platform |
| Active high incident | +20 | any open high-severity incident |
| SLO burning fast | +20 | any SLO with status `Critical` or `AtRisk` |
| Test file changes only | −10 | only `*.test.*` / `spec` files changed |

Risk bands: 0–30 Low · 31–60 Medium · 61–80 High · 81–100 Critical.

Score > 60 triggers a visual warning badge in the Deployment panel.

### 6.5 OpsQueryEngine

**Purpose:** Answers natural-language operational questions using structured connector data.

**No LLM is involved.** Answers are assembled from the cached state in `AIOpsEngine`. This guarantees:
- Zero latency from model inference
- No network call for every question
- Fully auditable answer chains

**Intent classification** (keyword matching):

| Intent | Keywords | Answer builder |
|---|---|---|
| `incident_investigation` | why, failing, broken, down, incident, outage | Uses RCA hypotheses + correlation chains |
| `risk_analysis` | risk, deploy, safe, dangerous, blast, impact | Uses DeploymentRiskReport |
| `log_query` | log, exception, crash, stacktrace, trace | Uses recent log entries from context |
| `observability` | monitor, metric, alert, slo, gap | Uses ObservabilityGaps |
| `security` | secret, vulnerability, cve, injection | Uses SecurityFindings |
| `general` | (fallback) | Uses incident + deployment summary |

Conversation history is kept in `OpsQueryEngine.ConversationHistory` for the duration of the session. `ClearHistory()` resets it.

### 6.6 SastScanner & SecretsDetector

**SastScanner** performs static analysis on source files using regex pattern libraries:
- SQL injection patterns
- XSS sinks
- Unsafe deserialisation
- Path traversal
- Hardcoded credential literals

**SecretsDetector** scans specifically for high-entropy strings and known secret patterns:
- AWS/Azure/GCP key formats
- Connection string patterns
- JWT tokens
- `.env` / `appsettings.json` secret fields

Both run **entirely locally** — no file content leaves the machine. Results are emitted as `SecurityFinding` records with file path, line number, severity, and remediation hint.

### 6.7 PolicyValidator

**Purpose:** Enforces project-level coding and configuration policies defined in the engine's rule set.

Examples: detecting use of deprecated APIs, enforcing logging standards, flagging missing error handling in async code. Policy violations are separate from security findings; they appear in the Security panel's "Policies" tab.

### 6.8 ObservabilityAdvisor

**Purpose:** Identifies files that lack adequate instrumentation.

Analyses a source file and detects:
- Missing structured logging imports / statements
- Missing metric/counter registration
- Missing distributed tracing span calls
- Missing health-check endpoint registration

Produces `ObservabilityGap` records that the `InsightsPanel` and the "Analyze Observability Gaps" action render.

### 6.9 ServiceContextEngine

**Purpose:** Maps a file path to a service name and assembles an `OperationalContext` snapshot.

**Service name resolution** (in priority order):
1. Parse the file's directory tree for `appsettings.json`, `docker-compose.yml`, or `Chart.yaml` that contain a `serviceName` / `name` field.
2. Match the directory name against known service names in the SLO and incident lists.
3. Use the project name from the nearest `.csproj` / `package.json`.
4. Fall back to `null` (context is still valid; service-specific filters are skipped).

`OperationalContext` bundles the resolved service name, file path, current incidents/deployments/anomalies/SLOs/alerts filtered to that service, and any inline annotations into a single value that `OpsQueryEngine` and `InsightsPanel` consume.

### 6.10 OTelCodeGenerator

**Purpose:** Produces ready-to-paste OpenTelemetry instrumentation code for a named function in a given language.

Supported languages: C#, Python, JavaScript/TypeScript, Go, Java.

Each template emits:
- Tracer/meter initialisation
- A span wrapping the function body
- Structured log statements at entry/exit
- A counter increment for the function call

### 6.11 RunbookEngine

**Purpose:** Executes runbook steps with a mandatory approval gate for production-impacting actions.

Each `RunbookStep` carries a `RequiresApproval` flag. When `ExecuteStepAsync` is called:
- If `RequiresApproval == false` → executes immediately (typically read-only or staging steps)
- If `RequiresApproval == true` → raises a `RemediationWorkflowDialog` requiring explicit user confirmation before proceeding

This design ensures the editor can never autonomously execute production changes. The approval interaction is always synchronous on the UI thread.

### 6.12 AuditLogger

**Purpose:** Maintains an in-memory and optionally on-disk audit trail of every AI-generated recommendation and security finding that pfpad surfaced.

Every call to `ScanFileAsync`, `Chat`, `AnalyzeIncidentAsync` results in an `AuditLogger` entry with timestamp, operation, outcome, and evidence chain. This supports post-incident review of what pfpad recommended and when.

---

## 7. UI Layer

### 7.1 Panel inventory

| Panel | Class | Opens via |
|---|---|---|
| Hub (overview) | `AIOpsPanel` | AIOps > AIOps Hub (Ctrl+Alt+A) |
| Security Findings | `SecurityPanel` | AIOps > Security Panel (Ctrl+Alt+X) |
| Deployment Monitor | `DeploymentPanel` | AIOps > Deployment Monitor (Ctrl+Alt+D) |
| Telemetry Explorer | `TelemetryPanel` | AIOps > Telemetry Panel (Ctrl+Alt+T) |
| Ops Insights | `InsightsPanel` | AIOps > Ops Insights (Ctrl+Alt+I) |
| AI Query | `OpsQueryPanel` | AIOps > AI Ops Query (Ctrl+Alt+Q) |
| Incident Timeline | `IncidentTimelinePanel` | AIOps > Incident Timeline (Ctrl+Alt+L) |
| PR Risk | `PullRequestRiskPanel` | AIOps > PR Risk Panel (Ctrl+Alt+P) |
| Service Dependencies | `ServiceDependencyPanel` | AIOps > Service Dependencies (Ctrl+Alt+E) |
| Runbooks | `RunbookPanel` | AIOps > Runbooks (Ctrl+Alt+R) |

### 7.2 Sidebar layout hierarchy

```
_workspaceSplitContainer
  └─ Panel1 (left column, collapsed when no workspace/AIOps panel is open)
       └─ _sidebarSplit (vertical SplitContainer)
            ├─ Panel1: _sideTopTabs (Workspace / Explorer)
            │           ← collapsed when _workspaceVisible == false
            └─ Panel2: _botSidebarSplit (vertical SplitContainer)
                 ├─ Panel1: _gitPanel
                 │           ← collapsed when _gitPanelVisible == false
                 └─ Panel2: _problemsSplit
                      ├─ Panel1: AIOps panels (stacked, Dock=Fill)
                      └─ Panel2: Symbols / Problems
```

**Key invariant:** `_workspaceSplitContainer.Panel1` is only visible when at least one of `_workspaceVisible`, `_gitPanelVisible`, or any AIOps panel is open. Opening an AIOps panel never forces the Workspace Explorer to appear.

### 7.3 Hint bar system

Every AIOps panel contains a `_hintBar` (`Panel`, `DockStyle.Top`, height 22 px) hosting a `_hintLabel`. Created via `AIOpsUiHelper.CreateHintBar(text)`, themed via `AIOpsUiHelper.SetHintBarTheme(bar, label, theme)`.

**Insertion order in `Controls` collection** (WinForms DockStyle.Top stacking, reverse-add):

```
Controls.Add(contentArea);   // Dock = Fill  → rendered last (bottom of stack)
Controls.Add(_hintBar);      // Dock = Top   → rendered second from top
Controls.Add(_header);       // Dock = Top   → rendered topmost
```

Because WinForms processes `DockStyle.Top` controls in reverse collection order, the last-added `Top` control appears at the very top of the panel.

### 7.4 Theme propagation

`ThemeManager.Instance.ThemeChanged` is a static event. Each panel subscribes in its constructor and unsubscribes in `Dispose`. When the user switches theme, every open panel independently receives the new `Theme` object and repaints.

`AIOpsUiHelper.ApplyControlTheme(control, theme)` recursively walks a control tree applying background/foreground colours and is used for container panels that hold many child controls.

---

## 8. End-to-End Data Flow

Tracing the path from "user saves Azure Monitor settings" to "Telemetry panel shows real service list":

```
1. User fills AIOps Settings dialog → clicks Save
2. Form1.SaveAIOpsSettings()
   → settings.CreateEncryptedCopy() → write to disk
   → _aiOpsEngine.Configure(settings)
       → RebuildConnectors()
           → new AzureMonitorConnector(settings.AzureMonitor) added to list
           → Task.Run → AzureMonitorConnector.ConnectAsync() (background)
               → authenticates with az login token or client credentials
               → Status = Connected
3. Poll timer fires (or immediate background refresh started by Configure)
   → RefreshAllAsync()
       → AzureMonitorConnector.GetActiveIncidentsAsync() → returns real incidents
       → ... all other fetch calls ...
       → IncidentsRefreshed?.Invoke(...)
       → panels update on UI thread
4. User opens Telemetry panel → clicks Browse
   → TelemetryPanel.BrowseServicesAsync()
       → connector = Form1.UpdateAIOpsTelemetryConnector()
           → picks AzureMonitorConnector (non-mock preferred)
       → if connector.Status != Connected → await connector.ConnectAsync()
       → if connector is IServiceDiscoveryCapable
           → GetObservableServicesAsync() → returns ["order-service", "payment-api", …]
       → populates service ComboBox
5. User selects service, clicks Query
   → connector.GetLogsAsync(service, from, to)
   → connector.GetMetricsAsync(service, "error_rate", from, to)
   → Results rendered in Logs / Metrics / Traces tabs
```

---

## 9. Security Model

| Concern | Mitigation |
|---|---|
| API tokens on disk | DPAPI-encrypted; plaintext only in memory while connector active |
| Secrets in source files | `SecretsDetector` scans on save; findings surfaced immediately |
| Production runbook execution | `RequiresApprovalForProduction` setting; every production step shows confirmation dialog |
| Audit trail | `AuditLogger` records every AI recommendation, security finding, and runbook execution |
| Data leaving the machine | Only connector HTTP calls. `SastScanner`, `SecretsDetector`, `OpsQueryEngine` are fully local |
| Connector credential scope | Each connector type uses the minimum scope: PATs, not admin tokens; read-only Log Analytics queries |

---

## 10. Adding a New Connector

1. **Create** `AIOps\Connectors\MyConnector.cs` implementing `IAIOpsConnector`.
2. **Add optional interfaces** (`IAlertCapable`, etc.) for capabilities your backend supports.
3. **Add settings class** `MyConnectorSettings` to `AIOpsSettings.cs` with `EncryptedX` fields and `LoadSecretsFromEncrypted()` / `CreateEncryptedCopy()`.
4. **Add `MyConnector.Enabled` branch** in `AIOpsEngine.RebuildConnectors()`.
5. **Add UI controls** in `AIOpsSettingsDialog.cs` for the new settings fields.
6. Update `MANUAL.md` to describe the new connector under AIOps Settings.

The engine, panels, and deduplication logic require zero changes. Data from the new connector flows automatically into the refresh pipeline.

---

## 11. Adding a New Engine Subsystem

1. **Create** `AIOps\Engines\MyAnalyzer.cs` as a plain sealed class.
2. **Add a field** `private readonly MyAnalyzer _myAnalyzer = new();` to `AIOpsEngine`.
3. **Expose an event** `public event Action<MyResult>? MyResultReady;` on `AIOpsEngine`.
4. **Add a public method** to `AIOpsEngine` that calls `_myAnalyzer`, fires the event, and (if appropriate) logs to `_auditLogger`.
5. **Wire up in `Form1.cs`** — subscribe to the event, forward to the relevant panel.
6. **Add a hint bar** in the panel constructor using `AIOpsUiHelper.CreateHintBar("ⓘ …")`.

The subsystem does not need to know about connectors or UI. It receives plain model objects and returns plain model objects.

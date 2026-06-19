namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class AIOpsEngine : IDisposable
{
    private readonly List<IAIOpsConnector> _connectors = new();
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly DeploymentRiskScorer _riskScorer = new();
    private readonly SastScanner _sast = new();
    private readonly SecretsDetector _secrets = new();
    private readonly PolicyValidator _policy = new();
    private readonly ObservabilityAdvisor _obsAdvisor = new();
    private readonly CorrelationEngine _correlationEngine = new();
    private readonly AnomalyDetector _anomalyDetector = new();
    private readonly RootCauseAnalyzer _rcaAnalyzer = new();
    private readonly ServiceContextEngine _serviceContextEngine = new();
    private readonly AuditLogger _auditLogger = new();
    private readonly OTelCodeGenerator _otelGenerator = new();
    private readonly RunbookEngine _runbookEngine = new();
    private readonly OpsQueryEngine _opsQueryEngine = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private AIOpsSettings _settings = new();

    private IReadOnlyList<Incident> _activeIncidents = [];
    private IReadOnlyList<Deployment> _recentDeployments = [];
    private IReadOnlyList<Pipeline> _recentPipelines = [];
    private IReadOnlyList<Slo> _slos = [];
    private IReadOnlyList<AnomalyEvent> _recentAnomalies = [];
    private IReadOnlyList<CorrelationChain> _correlationChains = [];
    private IReadOnlyList<Alert> _recentAlerts = [];
    private IReadOnlyList<GitCommit> _recentCommits = [];
    private IReadOnlyList<GitPullRequest> _recentPullRequests = [];
    private IReadOnlyList<Runbook> _runbooks = [];
    private DeploymentRiskReport? _lastRiskReport;

    public event Action<IReadOnlyList<Incident>>? IncidentsRefreshed;
    public event Action<IReadOnlyList<Deployment>>? DeploymentsRefreshed;
    public event Action<IReadOnlyList<Pipeline>>? PipelinesRefreshed;
    public event Action<IReadOnlyList<Slo>>? SlosRefreshed;
    public event Action<DeploymentRiskReport>? RiskReportGenerated;
    public event Action<IReadOnlyList<SecurityFinding>>? SecurityFindingsFound;
    public event Action<IReadOnlyList<PolicyViolation>>? PolicyViolationsFound;
    public event Action<IReadOnlyList<ObservabilityGap>>? ObservabilityGapsFound;
    public event Action<ConnectorState>? ConnectorStateChanged;
    public event Action<IReadOnlyList<AnomalyEvent>>? AnomaliesDetected;
    public event Action<RootCauseAnalysis>? RootCauseAnalyzed;
    public event Action<IReadOnlyList<CorrelationChain>>? CorrelationChainsUpdated;
    public event Action<IReadOnlyList<InlineAnnotation>>? InlineAnnotationsGenerated;
    public event Action<IReadOnlyList<Alert>>? AlertsRefreshed;

    public IReadOnlyList<Incident> ActiveIncidents => _activeIncidents;
    public IReadOnlyList<Deployment> RecentDeployments => _recentDeployments;
    public IReadOnlyList<Pipeline> RecentPipelines => _recentPipelines;
    public IReadOnlyList<Slo> Slos => _slos;
    public IReadOnlyList<AnomalyEvent> RecentAnomalies => _recentAnomalies;
    public IReadOnlyList<CorrelationChain> CorrelationChains => _correlationChains;
    public IReadOnlyList<Alert> RecentAlerts => _recentAlerts;
    public IReadOnlyList<GitCommit> RecentCommits => _recentCommits;
    public IReadOnlyList<GitPullRequest> RecentPullRequests => _recentPullRequests;
    public IReadOnlyList<Runbook> Runbooks => _runbooks;
    public RunbookEngine RunbookEngine => _runbookEngine;
    public OpsQueryEngine OpsQueryEngine => _opsQueryEngine;
    public ServiceContextEngine ServiceContextEngine => _serviceContextEngine;
    public DeploymentRiskReport? LastRiskReport => _lastRiskReport;
    public IReadOnlyList<IAIOpsConnector> Connectors => _connectors;
    public AIOpsSettings CurrentSettings => _settings;

    public AIOpsEngine(AIOpsSettings? settings = null)
    {
        _settings = settings ?? new AIOpsSettings();
        _pollTimer = new System.Windows.Forms.Timer { Interval = _settings.PollIntervalSeconds * 1000 };
        _pollTimer.Tick += async (_, _) => await RefreshAllAsync();
    }

    public void Configure(AIOpsSettings settings)
    {
        _settings = settings;
        _pollTimer.Interval = Math.Max(5, settings.PollIntervalSeconds) * 1000;
        RebuildConnectors();
    }

    private void RebuildConnectors()
    {
        foreach (var connector in _connectors)
            connector.Dispose();

        _connectors.Clear();

        if (_settings.MockDataEnabled)
            _connectors.Add(new MockDataConnector());
        if (_settings.AzureMonitor.Enabled)
            _connectors.Add(new AzureMonitorConnector(_settings.AzureMonitor));
        if (_settings.AzureDevOps.Enabled)
            _connectors.Add(new AzureDevOpsConnector(_settings.AzureDevOps));
        if (_settings.Kubernetes.Enabled)
            _connectors.Add(new KubernetesConnector(_settings.Kubernetes));
        if (_settings.Prometheus.Enabled)
            _connectors.Add(new PrometheusConnector(_settings.Prometheus));
        if (_settings.PagerDuty.Enabled)
            _connectors.Add(new PagerDutyConnector(_settings.PagerDuty));
        if (_settings.GitHubActions.Enabled)
            _connectors.Add(new GitHubActionsConnector(_settings.GitHubActions));

        // Auto-connect non-mock connectors in the background so they are ready when panels open.
        var realConnectors = _connectors.Where(c => c is not MockDataConnector).ToList();
        if (realConnectors.Count > 0)
            _ = Task.Run(async () =>
            {
                foreach (var connector in realConnectors)
                {
                    try { await connector.ConnectAsync().ConfigureAwait(false); }
                    catch { /* status will show Error; non-fatal */ }
                }
            });
    }

    public void Start()
    {
        if (!_settings.Enabled)
            return;

        RebuildConnectors();
        _pollTimer.Start();
        _ = Task.Run(() => RefreshAllAsync());
    }

    public void Stop() => _pollTimer.Stop();

    public async Task RefreshAllAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;
        if (!await _refreshLock.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        try
        {
            var connectors = _connectors.ToList(); // snapshot for thread safety

            // Connect all connectors in parallel, collect those that are ready.
            var connectResults = await Task.WhenAll(connectors.Select(async connector =>
            {
                try
                {
                    ConnectorState state = connector.Status == ConnectorStatus.Connected
                        ? new ConnectorState(connector.Name, connector.Status, LastSyncAt: DateTimeOffset.UtcNow)
                        : await connector.ConnectAsync(ct).ConfigureAwait(false);
                    try { ConnectorStateChanged?.Invoke(state); } catch { }
                    return (connector, connected: state.Status == ConnectorStatus.Connected);
                }
                catch (Exception ex)
                {
                    try { ConnectorStateChanged?.Invoke(new ConnectorState(connector.Name, ConnectorStatus.Error, ex.Message, DateTimeOffset.UtcNow)); } catch { }
                    return (connector, connected: false);
                }
            })).ConfigureAwait(false);

            var ready = connectResults.Where(r => r.connected).Select(r => r.connector).ToList();

            // Fetch core data from all ready connectors in parallel.
            var coreResults = await Task.WhenAll(ready.Select(async connector =>
            {
                var incidents    = new List<Incident>();
                var deployments  = new List<Deployment>();
                var pipelines    = new List<Pipeline>();
                var slos         = new List<Slo>();
                var alerts       = new List<Alert>();
                var commits      = new List<GitCommit>();
                var prs          = new List<GitPullRequest>();
                var runbooks     = new List<Runbook>();

                ct.ThrowIfCancellationRequested();
                try { incidents.AddRange(await connector.GetActiveIncidentsAsync(ct).ConfigureAwait(false)); } catch { }
                try { deployments.AddRange(await connector.GetRecentDeploymentsAsync(count: 20, ct: ct).ConfigureAwait(false)); } catch { }
                try { pipelines.AddRange(await connector.GetPipelineRunsAsync(count: 10, ct: ct).ConfigureAwait(false)); } catch { }
                try { slos.AddRange(await connector.GetSlosAsync(ct: ct).ConfigureAwait(false)); } catch { }

                if (connector is IAlertCapable ac)
                    try { alerts.AddRange(await ac.GetRecentAlertsAsync(ct: ct).ConfigureAwait(false)); } catch { }
                if (connector is IGitHistoryCapable gc)
                    try { commits.AddRange(await gc.GetRecentCommitsAsync(ct: ct).ConfigureAwait(false)); } catch { }
                if (connector is IPullRequestCapable pc)
                    try { prs.AddRange(await pc.GetRecentPullRequestsAsync(ct: ct).ConfigureAwait(false)); } catch { }
                if (connector is IRunbookCapable rc)
                    try { runbooks.AddRange(await rc.GetRunbooksAsync(ct: ct).ConfigureAwait(false)); } catch { }

                try { ConnectorStateChanged?.Invoke(new ConnectorState(connector.Name, connector.Status, LastSyncAt: DateTimeOffset.UtcNow)); } catch { }
                return (incidents, deployments, pipelines, slos, alerts, commits, prs, runbooks);
            })).ConfigureAwait(false);

            // Merge results from all connectors.
            var allIncidents   = coreResults.SelectMany(r => r.incidents).ToList();
            var allDeployments = coreResults.SelectMany(r => r.deployments).ToList();
            var allPipelines   = coreResults.SelectMany(r => r.pipelines).ToList();
            var allSlos        = coreResults.SelectMany(r => r.slos).ToList();
            var allAlerts      = coreResults.SelectMany(r => r.alerts).ToList();
            var allCommits     = coreResults.SelectMany(r => r.commits).ToList();
            var allPrs         = coreResults.SelectMany(r => r.prs).ToList();
            var allRunbooks    = coreResults.SelectMany(r => r.runbooks).ToList();

            _activeIncidents = allIncidents
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Id) ? $"{i.Title}:{i.AffectedService}:{i.StartedAt:O}" : i.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.StartedAt).First())
                .OrderByDescending(i => i.Severity)
                .ThenByDescending(i => i.StartedAt)
                .ToList();

            _recentDeployments = allDeployments
                .GroupBy(d => string.IsNullOrWhiteSpace(d.Id) ? $"{d.Service}:{d.Version}:{d.Environment}:{d.DeployedAt:O}" : d.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.DeployedAt).First())
                .OrderByDescending(d => d.DeployedAt)
                .ToList();

            _recentPipelines = allPipelines
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Id) ? $"{p.Name}:{p.Branch}:{p.StartedAt:O}" : p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.StartedAt).First())
                .OrderByDescending(p => p.StartedAt)
                .ToList();

            _slos = allSlos
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Id) ? $"{s.ServiceName}:{s.Name}" : s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.WindowEnd).First())
                .OrderBy(s => s.Status)
                .ThenBy(s => s.ServiceName)
                .ToList();

            _recentAlerts = allAlerts
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Id) ? $"{a.RuleName}:{a.ServiceName}:{a.FiredAt:O}" : a.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.FiredAt).First())
                .OrderByDescending(a => a.FiredAt)
                .ToList();
            _recentCommits = allCommits
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Sha) ? $"{c.Author}:{c.Timestamp:O}:{c.Message}" : c.Sha, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Timestamp).First())
                .OrderByDescending(c => c.Timestamp)
                .ToList();
            _recentPullRequests = allPrs
                .GroupBy(pr => string.IsNullOrWhiteSpace(pr.Id) ? $"{pr.Title}:{pr.CreatedAt:O}" : pr.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .OrderByDescending(pr => pr.CreatedAt)
                .ToList();
            _runbooks = allRunbooks
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Id) ? $"{r.Name}:{r.ServiceName}" : r.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(r => r.ServiceName)
                .ThenBy(r => r.Name)
                .ToList();

            _recentAnomalies = await DetectRecentAnomaliesAsync(ct).ConfigureAwait(false);
            try { AnomaliesDetected?.Invoke(_recentAnomalies); } catch { }

            _correlationChains = _correlationEngine.BuildChains(
                _recentCommits, _recentPullRequests, _recentPipelines,
                _recentDeployments, _activeIncidents, _recentAlerts,
                _recentAnomalies);
            try { CorrelationChainsUpdated?.Invoke(_correlationChains); } catch { }
            try { AlertsRefreshed?.Invoke(_recentAlerts); } catch { }
            try { IncidentsRefreshed?.Invoke(_activeIncidents); } catch { }
            try { DeploymentsRefreshed?.Invoke(_recentDeployments); } catch { }
            try { PipelinesRefreshed?.Invoke(_recentPipelines); } catch { }
            try { SlosRefreshed?.Invoke(_slos); } catch { }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<(IReadOnlyList<SecurityFinding> findings, IReadOnlyList<PolicyViolation> policies)> ScanFileAsync(string filePath, string content, CancellationToken ct = default)
    {
        var findings = await _sast.ScanAsync(filePath, content, ct).ConfigureAwait(false);
        var secrets = await _secrets.ScanAsync(filePath, content, ct).ConfigureAwait(false);
        var policies = await _policy.ValidateAsync(filePath, content, ct).ConfigureAwait(false);
        var allFindings = findings.Concat(secrets).ToList();
        try { SecurityFindingsFound?.Invoke(allFindings); } catch { }
        foreach (var finding in allFindings)
            _auditLogger.LogSecurityFinding(finding);
        try { PolicyViolationsFound?.Invoke(policies); } catch { }
        return (allFindings, policies);
    }

    public async Task<DeploymentRiskReport> ScoreDeploymentRiskAsync(IReadOnlyList<string> changedFiles, string? gitDiff, CancellationToken ct = default)
    {
        var report = await _riskScorer.ScoreAsync(changedFiles, gitDiff, _activeIncidents, _slos, ct).ConfigureAwait(false);
        _lastRiskReport = report;
        RiskReportGenerated?.Invoke(report);
        return report;
    }

    public async Task<IReadOnlyList<ObservabilityGap>> AnalyzeObservabilityAsync(string filePath, string content, CancellationToken ct = default)
    {
        var gaps = await _obsAdvisor.AnalyzeAsync(filePath, content, ct).ConfigureAwait(false);
        ObservabilityGapsFound?.Invoke(gaps);
        return gaps;
    }

    public async Task<RootCauseAnalysis> AnalyzeIncidentAsync(Incident incident, CancellationToken ct = default)
    {
        var rca = await _rcaAnalyzer.AnalyzeAsync(
            incident, _recentDeployments, _recentAnomalies,
            _recentAlerts, _recentCommits, _recentPullRequests, ct).ConfigureAwait(false);
        RootCauseAnalyzed?.Invoke(rca);
        _auditLogger.LogRecommendation($"RCA for {incident.Id}", new OpsQueryResponse(
            rca.Hypotheses.FirstOrDefault()?.Cause ?? "No hypothesis",
            rca.Hypotheses.FirstOrDefault()?.ConfidenceScore ?? 0,
            rca.Hypotheses.FirstOrDefault()?.Evidence ?? [],
            rca.Hypotheses.FirstOrDefault()?.Remediation ?? [],
            !rca.HasSufficientData,
            rca.Disclaimer));
        return rca;
    }

    public OperationalContext GetOperationalContext(string filePath)
    {
        string? serviceName = _serviceContextEngine.ResolveServiceName(filePath);
        var annotations = BuildInlineAnnotations(filePath, serviceName);
        InlineAnnotationsGenerated?.Invoke(annotations);
        return _serviceContextEngine.BuildContext(
            filePath,
            serviceName,
            null,
            _activeIncidents, _recentDeployments, _recentAnomalies,
            _slos, _recentAlerts, annotations);
    }

    public OpsQueryResponse Chat(string question, string? filePath = null)
    {
        var context = filePath != null
            ? GetOperationalContext(filePath)
            : new OperationalContext(null, null, null,
                _activeIncidents, _recentDeployments, _recentAnomalies,
                [], _slos, _recentAlerts, DateTimeOffset.UtcNow);

        var response = _opsQueryEngine.Query(question, context,
            null, _lastRiskReport, null, _correlationChains);
        _auditLogger.LogRecommendation(question, response);
        return response;
    }

    public string GenerateOTelCode(string language, string functionName, string? serviceName = null)
        => _otelGenerator.GenerateForFunction(language, functionName, serviceName);

    public void Dispose()
    {
        _pollTimer.Dispose();
        _refreshLock.Dispose();
        foreach (var connector in _connectors)
            connector.Dispose();
    }

    private async Task<IReadOnlyList<AnomalyEvent>> DetectRecentAnomaliesAsync(CancellationToken ct)
    {
        string[] metricNames = ["error_rate", "latency_ms"];
        var services = _recentDeployments.Select(d => d.Service)
            .Concat(_activeIncidents.Select(i => i.AffectedService))
            .Concat(_recentAlerts.Select(a => a.ServiceName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (services.Count == 0 || _connectors.Count == 0)
            return [];

        DateTimeOffset toTime = DateTimeOffset.UtcNow;
        DateTimeOffset fromTime = toTime.AddHours(-2);

        // Parallelize metric fetching across all (connector × service × metric) combinations.
        var tasks = _connectors
            .SelectMany(connector => services.SelectMany(svc => metricNames
                .Select(metric => FetchAnomaliesAsync(connector, svc, metric, fromTime, toTime, ct))))
            .ToList();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results
            .SelectMany(static r => r)
            .GroupBy(a => $"{a.ServiceName}:{a.MetricName}:{a.DetectedAt:O}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.DetectedAt).First())
            .OrderByDescending(a => a.DetectedAt)
            .ToList();
    }

    private async Task<IReadOnlyList<AnomalyEvent>> FetchAnomaliesAsync(
        IAIOpsConnector connector, string service, string metricName,
        DateTimeOffset fromTime, DateTimeOffset toTime, CancellationToken ct)
    {
        try
        {
            var series = await connector.GetMetricsAsync(service, metricName, fromTime, toTime, ct).ConfigureAwait(false);
            return _anomalyDetector.DetectAnomalies(service, metricName, series);
        }
        catch { return []; }
    }

    private IReadOnlyList<InlineAnnotation> BuildInlineAnnotations(string filePath, string? serviceName)
    {
        var annotations = new List<InlineAnnotation>();
        string symbol = Path.GetFileNameWithoutExtension(filePath);

        foreach (var anomaly in _recentAnomalies.Where(a => string.IsNullOrWhiteSpace(serviceName) || a.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).Take(3))
        {
            annotations.Add(new InlineAnnotation(filePath, 1, symbol, anomaly.AnomalyType == AnomalyType.LatencyIncrease ? InlineAnnotationType.LatencyHotspot : InlineAnnotationType.AnomalyDetected, anomaly.Description, anomaly.Evidence, anomaly.Severity, "Review anomaly", null));
        }

        foreach (var alert in _recentAlerts.Where(a => string.IsNullOrWhiteSpace(serviceName) || a.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).Take(2))
        {
            annotations.Add(new InlineAnnotation(filePath, 1, symbol, InlineAnnotationType.IncidentLinked, $"Alert {alert.RuleName} is {alert.State}.", [new Evidence("alert", alert.Description ?? alert.RuleName, alert.Url, alert.FiredAt)], alert.Severity, "Open alert", alert.Url));
        }

        if (_lastRiskReport is not null && _lastRiskReport.ChangedFiles.Any(path => path.Equals(filePath, StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(path.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)))
        {
            annotations.Add(new InlineAnnotation(filePath, 1, symbol, InlineAnnotationType.DeploymentRisk, $"Deployment risk is {_lastRiskReport.OverallRisk} for this file.", _lastRiskReport.Evidence, _lastRiskReport.OverallRisk >= RiskLevel.High ? Severity.High : Severity.Medium));
        }

        return annotations;
    }
}

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
    private AIOpsSettings _settings = new();

    private IReadOnlyList<Incident> _activeIncidents = [];
    private IReadOnlyList<Deployment> _recentDeployments = [];
    private IReadOnlyList<Pipeline> _recentPipelines = [];
    private IReadOnlyList<Slo> _slos = [];
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

    public IReadOnlyList<Incident> ActiveIncidents => _activeIncidents;
    public IReadOnlyList<Deployment> RecentDeployments => _recentDeployments;
    public IReadOnlyList<Pipeline> RecentPipelines => _recentPipelines;
    public IReadOnlyList<Slo> Slos => _slos;
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
    }

    public void Start()
    {
        if (!_settings.Enabled)
            return;

        RebuildConnectors();
        _pollTimer.Start();
        _ = Task.Run(async () => await RefreshAllAsync().ConfigureAwait(false));
    }

    public void Stop() => _pollTimer.Stop();

    public async Task RefreshAllAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        var incidents = new List<Incident>();
        var deployments = new List<Deployment>();
        var pipelines = new List<Pipeline>();
        var slos = new List<Slo>();

        foreach (var connector in _connectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ConnectorState state = connector.Status == ConnectorStatus.Connected
                    ? new ConnectorState(connector.Name, connector.Status, LastSyncAt: DateTimeOffset.UtcNow)
                    : await connector.ConnectAsync(ct).ConfigureAwait(false);
                ConnectorStateChanged?.Invoke(state);
                if (state.Status != ConnectorStatus.Connected)
                    continue;
            }
            catch (Exception ex)
            {
                ConnectorStateChanged?.Invoke(new ConnectorState(connector.Name, ConnectorStatus.Error, ex.Message, DateTimeOffset.UtcNow));
                continue;
            }

            try
            {
                incidents.AddRange(await connector.GetActiveIncidentsAsync(ct).ConfigureAwait(false));
            }
            catch
            {
            }

            try
            {
                deployments.AddRange(await connector.GetRecentDeploymentsAsync(count: 20, ct: ct).ConfigureAwait(false));
            }
            catch
            {
            }

            try
            {
                pipelines.AddRange(await connector.GetPipelineRunsAsync(count: 10, ct: ct).ConfigureAwait(false));
            }
            catch
            {
            }

            try
            {
                slos.AddRange(await connector.GetSlosAsync(ct: ct).ConfigureAwait(false));
            }
            catch
            {
            }

            ConnectorStateChanged?.Invoke(new ConnectorState(connector.Name, connector.Status, LastSyncAt: DateTimeOffset.UtcNow));
        }

        _activeIncidents = incidents
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Id) ? $"{i.Title}:{i.AffectedService}:{i.StartedAt:O}" : i.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.StartedAt).First())
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.StartedAt)
            .ToList();

        _recentDeployments = deployments
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Id) ? $"{d.Service}:{d.Version}:{d.Environment}:{d.DeployedAt:O}" : d.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.DeployedAt).First())
            .OrderByDescending(d => d.DeployedAt)
            .ToList();

        _recentPipelines = pipelines
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Id) ? $"{p.Name}:{p.Branch}:{p.StartedAt:O}" : p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.StartedAt).First())
            .OrderByDescending(p => p.StartedAt)
            .ToList();

        _slos = slos
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Id) ? $"{s.ServiceName}:{s.Name}" : s.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.WindowEnd).First())
            .OrderBy(s => s.Status)
            .ThenBy(s => s.ServiceName)
            .ToList();

        IncidentsRefreshed?.Invoke(_activeIncidents);
        DeploymentsRefreshed?.Invoke(_recentDeployments);
        PipelinesRefreshed?.Invoke(_recentPipelines);
        SlosRefreshed?.Invoke(_slos);
    }

    public async Task<(IReadOnlyList<SecurityFinding> findings, IReadOnlyList<PolicyViolation> policies)> ScanFileAsync(string filePath, string content, CancellationToken ct = default)
    {
        var findings = await _sast.ScanAsync(filePath, content, ct).ConfigureAwait(false);
        var secrets = await _secrets.ScanAsync(filePath, content, ct).ConfigureAwait(false);
        var policies = await _policy.ValidateAsync(filePath, content, ct).ConfigureAwait(false);
        var allFindings = findings.Concat(secrets).ToList();
        SecurityFindingsFound?.Invoke(allFindings);
        PolicyViolationsFound?.Invoke(policies);
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

    public void Dispose()
    {
        _pollTimer.Dispose();
        foreach (var connector in _connectors)
            connector.Dispose();
    }
}

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>Core data-source integration contract.</summary>
public interface IAIOpsConnector : IDisposable
{
    string Name { get; }
    ConnectorStatus Status { get; }
    Task<ConnectorState> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();

    Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<IReadOnlyList<LogEntry>> GetLogsAsync(string serviceName, DateTimeOffset from, DateTimeOffset to, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default);
    Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default);
    Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default);
    Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default);
    Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default);
}

/// <summary>Optional: connector supports alert data.</summary>
public interface IAlertCapable
{
    Task<IReadOnlyList<Alert>> GetRecentAlertsAsync(string? serviceName = null, int days = 1, CancellationToken ct = default);
}

/// <summary>Optional: connector supports pull request data.</summary>
public interface IPullRequestCapable
{
    Task<IReadOnlyList<GitPullRequest>> GetRecentPullRequestsAsync(string? repository = null, int count = 20, CancellationToken ct = default);
}

/// <summary>Optional: connector supports commit/git history data.</summary>
public interface IGitHistoryCapable
{
    Task<IReadOnlyList<GitCommit>> GetRecentCommitsAsync(string? branch = null, int count = 30, CancellationToken ct = default);
}

/// <summary>Optional: connector supports service catalog data.</summary>
public interface IServiceCatalogCapable
{
    Task<ServiceDependencyGraph> GetServiceDependencyGraphAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ServiceEntity>> GetServiceCatalogAsync(CancellationToken ct = default);
}

/// <summary>Optional: connector supports runbook data.</summary>
public interface IRunbookCapable
{
    Task<IReadOnlyList<Runbook>> GetRunbooksAsync(string? serviceName = null, CancellationToken ct = default);
}

/// <summary>Optional: connector supports feature flag data.</summary>
public interface IFeatureFlagCapable
{
    Task<IReadOnlyList<FeatureFlag>> GetFeatureFlagsAsync(string? serviceName = null, CancellationToken ct = default);
}

/// <summary>Optional: connector supports infrastructure resource data.</summary>
public interface IInfrastructureCapable
{
    Task<IReadOnlyList<InfrastructureResource>> GetInfrastructureResourcesAsync(string? environment = null, CancellationToken ct = default);
}

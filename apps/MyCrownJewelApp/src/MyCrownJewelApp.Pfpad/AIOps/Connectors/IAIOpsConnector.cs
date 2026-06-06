namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>All AIOps data-source integrations implement this interface.</summary>
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

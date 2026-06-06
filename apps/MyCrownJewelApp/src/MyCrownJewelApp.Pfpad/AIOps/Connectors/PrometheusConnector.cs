using System.Net.Http.Headers;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class PrometheusConnector : IAIOpsConnector
{
    private readonly PrometheusSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public PrometheusConnector(PrometheusSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)) };
    }

    public string Name => "Prometheus";
    public ConnectorStatus Status => _status;

    public async Task<ConnectorState> ConnectAsync(CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            _status = ConnectorStatus.Disconnected;
            return new ConnectorState(Name, _status, "Disabled");
        }

        try
        {
            _status = ConnectorStatus.Connecting;
            using var request = CreateRequest($"/api/v1/query?query={Uri.EscapeDataString("up")}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            _status = response.IsSuccessStatusCode ? ConnectorStatus.Connected : ConnectorStatus.Error;
            return new ConnectorState(Name, _status, response.IsSuccessStatusCode ? null : response.ReasonPhrase, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _status = ConnectorStatus.Error;
            return new ConnectorState(Name, _status, ex.Message, DateTimeOffset.UtcNow);
        }
    }

    public Task DisconnectAsync()
    {
        _status = ConnectorStatus.Disconnected;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string query = BuildMetricQuery(serviceName, metricName);
            string path = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={from.ToUnixTimeSeconds()}&end={to.ToUnixTimeSeconds()}&step=300";
            using var request = CreateRequest(path);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseMetricSeries(metricName, doc.RootElement, serviceName);
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<LogEntry>> GetLogsAsync(string serviceName, DateTimeOffset from, DateTimeOffset to, int limit = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LogEntry>>([]);

    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TraceSpan>>([]);

    public async Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            var errorSeries = await GetMetricsAsync(serviceName, "error_rate", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            var latencySeries = await GetMetricsAsync(serviceName, "latency", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            if (errorSeries.Count == 0 && latencySeries.Count == 0)
                return null;

            double errorRate = errorSeries.Count > 0 ? errorSeries.Last().Value : 0;
            double p99Latency = latencySeries.Count > 0 ? latencySeries.Max(m => m.Value) : 0;
            double availability = Math.Clamp(100 - errorRate, 0, 100);
            return new ServiceHealth(serviceName, Math.Round(errorRate, 2), Math.Round(p99Latency, 2), Math.Round(availability, 2), DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    public Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Incident>>([]);

    public Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Incident>>([]);

    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Deployment>>([]);

    public Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Pipeline>>([]);

    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Slo>>([]);

    public void Dispose() => _httpClient.Dispose();

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_status == ConnectorStatus.Connected)
            return true;

        var state = await ConnectAsync(ct).ConfigureAwait(false);
        return state.Status == ConnectorStatus.Connected;
    }

    private HttpRequestMessage CreateRequest(string relativePath)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl.TrimEnd('/')}{relativePath}");
        if (!string.IsNullOrWhiteSpace(_settings.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.BearerToken);
        else if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            string value = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", value);
        }
        return request;
    }

    private static string BuildMetricQuery(string serviceName, string metricName)
    {
        if (metricName.Contains('{', StringComparison.Ordinal))
            return metricName;

        string normalized = metricName.ToLowerInvariant();
        return normalized switch
        {
            "error_rate" or "errorrate" => string.IsNullOrWhiteSpace(serviceName)
                ? "100 * sum(rate(http_requests_total{status=~\"5..\"}[5m])) / clamp_min(sum(rate(http_requests_total[5m])), 1)"
                : $"100 * sum(rate(http_requests_total{{service=\"{serviceName}\",status=~\"5..\"}}[5m])) / clamp_min(sum(rate(http_requests_total{{service=\"{serviceName}\"}}[5m])), 1)",
            "latency" or "latency_ms" or "p99_latency" => string.IsNullOrWhiteSpace(serviceName)
                ? "histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket[5m])) by (le)) * 1000"
                : $"histogram_quantile(0.99, sum(rate(http_request_duration_seconds_bucket{{service=\"{serviceName}\"}}[5m])) by (le)) * 1000",
            _ => string.IsNullOrWhiteSpace(serviceName) ? metricName : $"{metricName}{{service=\"{serviceName}\"}}"
        };
    }

    private static IReadOnlyList<MetricPoint> ParseMetricSeries(string metricName, JsonElement root, string serviceName)
    {
        if (!root.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("result", out JsonElement result)
            || result.ValueKind != JsonValueKind.Array)
            return [];

        var points = new List<MetricPoint>();
        foreach (var series in result.EnumerateArray())
        {
            if (!series.TryGetProperty("values", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var pair in values.EnumerateArray())
            {
                if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() < 2)
                    continue;
                if (!pair[0].TryGetDouble(out double ts))
                    continue;
                string rawValue = pair[1].GetString() ?? "0";
                if (!double.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
                    continue;
                points.Add(new MetricPoint(metricName, value, metricName.Contains("latency", StringComparison.OrdinalIgnoreCase) ? "ms" : "%", DateTimeOffset.FromUnixTimeSeconds((long)ts), new Dictionary<string, string> { ["service"] = serviceName }));
            }
        }

        return points.OrderBy(p => p.Timestamp).ToList();
    }
}

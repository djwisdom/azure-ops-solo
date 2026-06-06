using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class AzureMonitorConnector : IAIOpsConnector
{
    private readonly AzureMonitorSettings _settings;
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public AzureMonitorConnector(AzureMonitorSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public string Name => "Azure Monitor";
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
            string token = await GetAccessTokenAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                _status = ConnectorStatus.Error;
                return new ConnectorState(Name, _status, "Authentication failed");
            }

            if (!string.IsNullOrWhiteSpace(_settings.WorkspaceId))
            {
                string query = "Heartbeat | take 1";
                using var request = CreateLogAnalyticsRequest(query, TimeSpan.FromMinutes(5), token);
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }

            _status = ConnectorStatus.Connected;
            return new ConnectorState(Name, _status, LastSyncAt: DateTimeOffset.UtcNow);
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
        _accessToken = null;
        _tokenExpiresAt = default;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string token = _accessToken!;
            string url = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(_settings.SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(_settings.ResourceGroup)}/providers/Microsoft.Insights/components/{Uri.EscapeDataString(serviceName)}/metrics?api-version=2018-01-01&metricnames={Uri.EscapeDataString(metricName)}&timespan={Uri.EscapeDataString(from.UtcDateTime.ToString("O"))}/{Uri.EscapeDataString(to.UtcDateTime.ToString("O"))}&interval=PT5M";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseMetricPoints(metricName, doc.RootElement, serviceName);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<LogEntry>> GetLogsAsync(string serviceName, DateTimeOffset from, DateTimeOffset to, int limit = 100, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string query = $"{serviceName} | where TimeGenerated > ago(24h) | take {Math.Clamp(limit, 1, 500)}";
            using var request = CreateLogAnalyticsRequest(query, to - from, _accessToken!);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseLogs(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TraceSpan>>([]);

    public async Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            var availabilityMetrics = await GetMetricsAsync(serviceName, "availabilityResults/availabilityPercentage", DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            var exceptionMetrics = await GetMetricsAsync(serviceName, "exceptions/count", DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            var durationMetrics = await GetMetricsAsync(serviceName, "requests/duration", DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            if (availabilityMetrics.Count == 0 && exceptionMetrics.Count == 0 && durationMetrics.Count == 0)
                return null;

            double availability = availabilityMetrics.Count > 0 ? availabilityMetrics.Average(m => m.Value) : 99.9;
            double errorRate = exceptionMetrics.Count > 0 ? Math.Min(100, exceptionMetrics.Average(m => m.Value)) : 0;
            double p99 = durationMetrics.Count > 0 ? durationMetrics.Max(m => m.Value) : 0;
            return new ServiceHealth(serviceName, Math.Round(errorRate, 2), Math.Round(p99, 2), Math.Round(availability, 2), DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string url = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(_settings.SubscriptionId)}/providers/Microsoft.AlertsManagement/alerts?api-version=2019-03-01&$filter=alertState%20eq%20'New'%20or%20alertState%20eq%20'Acknowledged'";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseAlerts(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-days).UtcDateTime.ToString("O"));
            string url = $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(_settings.SubscriptionId)}/providers/Microsoft.AlertsManagement/alerts?api-version=2019-03-01&$filter=monitorCondition%20eq%20'Fired'%20and%20essentials/startDateTime%20ge%20{from}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseAlerts(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false) || string.IsNullOrWhiteSpace(_settings.WorkspaceId))
            return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(service) ? string.Empty : $" | where ServiceName =~ '{EscapeKql(service)}'";
            string query = $"AppTraces {filter} | where Message has 'deployment' or Message has 'release' | project TimeGenerated, Message, ServiceName=tostring(Properties['service']), Version=tostring(Properties['version']), Environment=tostring(Properties['environment']), Commit=tostring(Properties['commitSha']) | order by TimeGenerated desc | take {Math.Clamp(count, 1, 100)}";
            using var request = CreateLogAnalyticsRequest(query, TimeSpan.FromDays(14), _accessToken!);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseDeployments(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Pipeline>>([]);

    public async Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false) || string.IsNullOrWhiteSpace(_settings.WorkspaceId))
            return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(serviceName) ? string.Empty : $" | where ServiceName =~ '{EscapeKql(serviceName)}'";
            string query = $"AppMetrics {filter} | where Name in ('availability','latency') | summarize CurrentPercent=avg(Value) by ServiceName=tostring(Properties['service']), Name | take 20";
            using var request = CreateLogAnalyticsRequest(query, TimeSpan.FromDays(7), _accessToken!);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseSlos(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_status == ConnectorStatus.Connected && !string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return true;

        var state = await ConnectAsync(ct).ConfigureAwait(false);
        return state.Status == ConnectorStatus.Connected;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _accessToken;

        if (string.IsNullOrWhiteSpace(_settings.TenantId) || string.IsNullOrWhiteSpace(_settings.ClientId) || string.IsNullOrWhiteSpace(_settings.ClientSecret))
            return string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{Uri.EscapeDataString(_settings.TenantId)}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["scope"] = "https://management.azure.com/.default https://api.loganalytics.io/.default"
            })
        };

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return string.Empty;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        _accessToken = GetString(doc.RootElement, "access_token");
        int expiresIn = GetInt(doc.RootElement, "expires_in", 300);
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        return _accessToken ?? string.Empty;
    }

    private HttpRequestMessage CreateLogAnalyticsRequest(string query, TimeSpan timeSpan, string token)
    {
        string url = $"https://api.loganalytics.io/v1/workspaces/{Uri.EscapeDataString(_settings.WorkspaceId)}/query";
        var payload = JsonSerializer.Serialize(new { query, timespan = XmlConvert.ToString(timeSpan) });
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static IReadOnlyList<MetricPoint> ParseMetricPoints(string metricName, JsonElement root, string serviceName)
    {
        var results = new List<MetricPoint>();
        if (!root.TryGetProperty("value", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var metric in values.EnumerateArray())
        {
            if (!metric.TryGetProperty("timeseries", out JsonElement seriesArray) || seriesArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var series in seriesArray.EnumerateArray())
            {
                if (!series.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var point in data.EnumerateArray())
                {
                    string? timeStamp = GetString(point, "timeStamp");
                    double value = GetDouble(point, "average", GetDouble(point, "total", GetDouble(point, "maximum", 0)));
                    if (DateTimeOffset.TryParse(timeStamp, out DateTimeOffset parsed))
                    {
                        results.Add(new MetricPoint(metricName, value, "value", parsed, new Dictionary<string, string> { ["service"] = serviceName }));
                    }
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<LogEntry> ParseLogs(JsonElement root)
    {
        var logs = new List<LogEntry>();
        if (!TryGetFirstTable(root, out JsonElement table))
            return logs;

        var columns = table.GetProperty("columns").EnumerateArray().Select((c, i) => new { Name = GetString(c, "name") ?? string.Empty, Index = i }).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.GetProperty("rows").EnumerateArray())
        {
            var cells = row.EnumerateArray().ToArray();
            DateTimeOffset timestamp = DateTimeOffset.TryParse(GetCell(cells, columns, "TimeGenerated") ?? GetCell(cells, columns, "timestamp"), out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow;
            string message = DataRedactor.Redact(GetCell(cells, columns, "Message") ?? GetCell(cells, columns, "message") ?? string.Empty);
            string? level = GetCell(cells, columns, "Level") ?? GetCell(cells, columns, "severityLevel");
            logs.Add(new LogEntry(timestamp, ParseSeverity(level), message, GetCell(cells, columns, "TraceId"), GetCell(cells, columns, "SpanId")));
        }

        return logs;
    }

    private static IReadOnlyList<Incident> ParseAlerts(JsonElement root)
    {
        var incidents = new List<Incident>();
        if (!root.TryGetProperty("value", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return incidents;

        foreach (var alert in values.EnumerateArray())
        {
            string id = GetString(alert, "id") ?? Guid.NewGuid().ToString("N");
            var essentials = alert.TryGetProperty("properties", out JsonElement properties) && properties.TryGetProperty("essentials", out JsonElement e) ? e : default;
            string title = GetString(essentials, "alertRule") ?? GetString(essentials, "description") ?? "Azure alert";
            string? start = GetString(essentials, "startDateTime");
            string? target = GetString(essentials, "targetResourceName");
            incidents.Add(new Incident(
                id,
                title,
                ParseSeverity(GetString(essentials, "severity")),
                ParseIncidentStatus(GetString(essentials, "alertState")),
                DateTimeOffset.TryParse(start, out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow,
                null,
                target,
                GetString(essentials, "monitorCondition"),
                [GetString(essentials, "targetResource") ?? target ?? "unknown"],
                GetString(essentials, "alertRuleUrl")));
        }

        return incidents;
    }

    private static IReadOnlyList<Deployment> ParseDeployments(JsonElement root)
    {
        var deployments = new List<Deployment>();
        if (!TryGetFirstTable(root, out JsonElement table))
            return deployments;

        var columns = table.GetProperty("columns").EnumerateArray().Select((c, i) => new { Name = GetString(c, "name") ?? string.Empty, Index = i }).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.GetProperty("rows").EnumerateArray())
        {
            var cells = row.EnumerateArray().ToArray();
            DateTimeOffset deployedAt = DateTimeOffset.TryParse(GetCell(cells, columns, "TimeGenerated"), out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow;
            deployments.Add(new Deployment(
                Guid.NewGuid().ToString("N")[..8],
                GetCell(cells, columns, "ServiceName") ?? "unknown-service",
                GetCell(cells, columns, "Version") ?? "unknown",
                GetCell(cells, columns, "Environment") ?? "prod",
                DeploymentStatus.Succeeded,
                deployedAt,
                "Azure Monitor",
                GetCell(cells, columns, "Commit"),
                null,
                []));
        }

        return deployments;
    }

    private static IReadOnlyList<Slo> ParseSlos(JsonElement root)
    {
        var slos = new List<Slo>();
        if (!TryGetFirstTable(root, out JsonElement table))
            return slos;

        var columns = table.GetProperty("columns").EnumerateArray().Select((c, i) => new { Name = GetString(c, "name") ?? string.Empty, Index = i }).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.GetProperty("rows").EnumerateArray())
        {
            var cells = row.EnumerateArray().ToArray();
            string serviceName = GetCell(cells, columns, "ServiceName") ?? "unknown-service";
            double current = double.TryParse(GetCell(cells, columns, "CurrentPercent"), out double parsedValue) ? parsedValue : 99;
            slos.Add(new Slo(Guid.NewGuid().ToString("N")[..8], GetCell(cells, columns, "Name") ?? "Availability", serviceName, 99.9, current, current < 99.0 ? SloStatus.AtRisk : SloStatus.Healthy, current < 99.0 ? 1.7 : 0.7, DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow));
        }

        return slos;
    }

    private static bool TryGetFirstTable(JsonElement root, out JsonElement table)
    {
        table = default;
        if (!root.TryGetProperty("tables", out JsonElement tables) || tables.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in tables.EnumerateArray())
        {
            table = item;
            return true;
        }

        return false;
    }

    private static string? GetCell(JsonElement[] cells, IReadOnlyDictionary<string, int> columns, string columnName)
        => columns.TryGetValue(columnName, out int index) && index < cells.Length ? cells[index].ToString() : null;

    private static string? GetString(JsonElement element, string propertyName)
        => element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(propertyName, out JsonElement value) ? value.ToString() : null;

    private static int GetInt(JsonElement element, string propertyName, int defaultValue)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : defaultValue;

    private static double GetDouble(JsonElement element, string propertyName, double defaultValue)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDouble(out double parsed) ? parsed : defaultValue;

    private static Severity ParseSeverity(string? severity)
        => severity?.ToLowerInvariant() switch
        {
            "sev0" or "critical" or "4" => Severity.Critical,
            "sev1" or "high" or "3" => Severity.High,
            "sev2" or "medium" or "2" => Severity.Medium,
            "sev3" or "low" or "1" or "warning" => Severity.Low,
            _ => Severity.Info
        };

    private static IncidentStatus ParseIncidentStatus(string? status)
        => status?.ToLowerInvariant() switch
        {
            "acknowledged" => IncidentStatus.Acknowledged,
            "resolved" or "closed" => IncidentStatus.Resolved,
            _ => IncidentStatus.Triggered
        };

    private static string EscapeKql(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

using System.Net.Http.Headers;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class PagerDutyConnector : IAIOpsConnector, IAlertCapable
{
    private readonly PagerDutySettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public PagerDutyConnector(PagerDutySettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)) };
    }

    public string Name => "PagerDuty";
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
            using var request = CreateRequest(BuildIncidentsPath(1, null, 1, true));
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

    public Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MetricPoint>>([]);

    public Task<IReadOnlyList<LogEntry>> GetLogsAsync(string serviceName, DateTimeOffset from, DateTimeOffset to, int limit = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LogEntry>>([]);

    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TraceSpan>>([]);

    public Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
        => Task.FromResult<ServiceHealth?>(null);

    public async Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            using var request = CreateRequest(BuildIncidentsPath(100, null, 0, true));
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseIncidents(doc.RootElement, activeOnly: true);
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
            using var request = CreateRequest(BuildIncidentsPath(100, null, days, false));
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseIncidents(doc.RootElement, activeOnly: false);
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Deployment>>([]);

    public Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Pipeline>>([]);

    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Slo>>([]);

    public async Task<IReadOnlyList<Alert>> GetRecentAlertsAsync(string? serviceName = null, int days = 1, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            using var request = CreateRequest(BuildIncidentsPath(100, serviceName, days, false));
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseAlerts(doc.RootElement, serviceName);
        }
        catch
        {
            return [];
        }
    }

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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com{relativePath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", $"token={_settings.ApiToken}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.pagerduty+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("From", "pfpad@example.local");
        return request;
    }

    private string BuildIncidentsPath(int limit, string? serviceName, int days, bool activeOnly)
    {
        var parts = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 100)}",
            $"since={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-Math.Max(1, days)).ToString("O"))}",
            $"until={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}"
        };

        if (activeOnly)
        {
            parts.Add("statuses[]=triggered");
            parts.Add("statuses[]=acknowledged");
        }
        else
        {
            parts.Add("statuses[]=triggered");
            parts.Add("statuses[]=acknowledged");
            parts.Add("statuses[]=resolved");
        }

        if (!string.IsNullOrWhiteSpace(_settings.ServiceId))
            parts.Add($"service_ids[]={Uri.EscapeDataString(_settings.ServiceId)}");
        if (!string.IsNullOrWhiteSpace(serviceName))
            parts.Add($"query={Uri.EscapeDataString(serviceName)}");

        return $"/incidents?{string.Join("&", parts)}";
    }

    private static IReadOnlyList<Incident> ParseIncidents(JsonElement root, bool activeOnly)
    {
        if (!root.TryGetProperty("incidents", out JsonElement incidents) || incidents.ValueKind != JsonValueKind.Array)
            return [];

        var results = new List<Incident>();
        foreach (var item in incidents.EnumerateArray())
        {
            string status = item.TryGetProperty("status", out JsonElement statusValue) ? statusValue.GetString() ?? string.Empty : string.Empty;
            if (activeOnly && status.Equals("resolved", StringComparison.OrdinalIgnoreCase))
                continue;

            string serviceName = item.TryGetProperty("service", out JsonElement service)
                ? service.TryGetProperty("summary", out JsonElement summary) ? summary.GetString() ?? "pagerduty-service" : service.TryGetProperty("id", out JsonElement idValue) ? idValue.GetString() ?? "pagerduty-service" : "pagerduty-service"
                : "pagerduty-service";

            results.Add(new Incident(
                item.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                item.TryGetProperty("title", out JsonElement title) ? title.GetString() ?? "PagerDuty incident" : "PagerDuty incident",
                ParseSeverity(item),
                status.Equals("resolved", StringComparison.OrdinalIgnoreCase) ? IncidentStatus.Resolved : status.Equals("acknowledged", StringComparison.OrdinalIgnoreCase) ? IncidentStatus.Acknowledged : IncidentStatus.Triggered,
                ParseTimestamp(item, "created_at"),
                item.TryGetProperty("last_status_change_at", out JsonElement resolvedAt) && status.Equals("resolved", StringComparison.OrdinalIgnoreCase) && DateTimeOffset.TryParse(resolvedAt.GetString(), out DateTimeOffset resolved)
                    ? resolved : null,
                serviceName,
                item.TryGetProperty("summary", out JsonElement summaryText) ? summaryText.GetString() : null,
                [],
                item.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null));
        }

        return results.OrderByDescending(i => i.StartedAt).ToList();
    }

    private static IReadOnlyList<Alert> ParseAlerts(JsonElement root, string? filterService)
    {
        if (!root.TryGetProperty("incidents", out JsonElement incidents) || incidents.ValueKind != JsonValueKind.Array)
            return [];

        var alerts = new List<Alert>();
        foreach (var item in incidents.EnumerateArray())
        {
            string serviceName = item.TryGetProperty("service", out JsonElement service)
                ? service.TryGetProperty("summary", out JsonElement summary) ? summary.GetString() ?? "pagerduty-service" : service.TryGetProperty("id", out JsonElement idValue) ? idValue.GetString() ?? "pagerduty-service" : "pagerduty-service"
                : "pagerduty-service";
            if (!string.IsNullOrWhiteSpace(filterService) && !serviceName.Contains(filterService, StringComparison.OrdinalIgnoreCase))
                continue;

            string state = item.TryGetProperty("status", out JsonElement statusValue) ? statusValue.GetString() ?? "fired" : "fired";
            alerts.Add(new Alert(
                item.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                item.TryGetProperty("title", out JsonElement title) ? title.GetString() ?? "PagerDuty alert" : "PagerDuty alert",
                serviceName,
                ParseSeverity(item),
                state.Equals("resolved", StringComparison.OrdinalIgnoreCase) ? "resolved" : "fired",
                ParseTimestamp(item, "created_at"),
                state.Equals("resolved", StringComparison.OrdinalIgnoreCase) && item.TryGetProperty("last_status_change_at", out JsonElement resolvedAt) && DateTimeOffset.TryParse(resolvedAt.GetString(), out DateTimeOffset resolved)
                    ? resolved : null,
                item.TryGetProperty("summary", out JsonElement description) ? description.GetString() : null,
                item.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null));
        }

        return alerts.OrderByDescending(a => a.FiredAt).ToList();
    }

    private static DateTimeOffset ParseTimestamp(JsonElement item, string propertyName)
        => item.TryGetProperty(propertyName, out JsonElement value) && DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

    private static Severity ParseSeverity(JsonElement item)
    {
        string urgency = item.TryGetProperty("urgency", out JsonElement urgencyValue) ? urgencyValue.GetString() ?? string.Empty : string.Empty;
        return urgency.Equals("high", StringComparison.OrdinalIgnoreCase) ? Severity.High : Severity.Medium;
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class AzureDevOpsConnector : IAIOpsConnector
{
    private readonly AzureDevOpsSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public AzureDevOpsConnector(AzureDevOpsSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public string Name => "Azure DevOps";
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
            using var request = CreateRequest(HttpMethod.Get, "build/builds?api-version=7.1&$top=1");
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
            var wiqlBody = JsonSerializer.Serialize(new
            {
                query = "Select [System.Id], [System.Title], [System.State], [Microsoft.VSTS.Common.Severity] From WorkItems Where [System.TeamProject] = @project And [System.WorkItemType] In ('Bug','Incident') And [System.State] <> 'Closed' Order By [System.ChangedDate] Desc"
            });

            using var request = CreateRequest(HttpMethod.Post, "wit/wiql?api-version=7.1", wiqlBody);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var ids = doc.RootElement.TryGetProperty("workItems", out JsonElement workItems)
                ? workItems.EnumerateArray().Select(item => item.TryGetProperty("id", out JsonElement id) ? id.GetInt32() : 0).Where(id => id > 0).Take(25).ToArray()
                : [];

            if (ids.Length == 0)
                return [];

            using var detailsRequest = CreateRequest(HttpMethod.Get, $"wit/workitems?ids={string.Join(',', ids)}&api-version=7.1");
            using var detailsResponse = await _httpClient.SendAsync(detailsRequest, ct).ConfigureAwait(false);
            if (!detailsResponse.IsSuccessStatusCode)
                return [];

            using var detailsDoc = JsonDocument.Parse(await detailsResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseIncidents(detailsDoc.RootElement);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
    {
        var incidents = await GetActiveIncidentsAsync(ct).ConfigureAwait(false);
        return incidents.Where(i => i.StartedAt >= DateTimeOffset.UtcNow.AddDays(-days)).ToList();
    }

    public async Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"release/releases?api-version=7.1&$top={Math.Clamp(count, 1, 50)}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var deployments = ParseDeployments(doc.RootElement);
            if (!string.IsNullOrWhiteSpace(service))
                deployments = deployments.Where(d => d.Service.Equals(service, StringComparison.OrdinalIgnoreCase)).ToList();
            return deployments.Take(count).ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"&branchName={Uri.EscapeDataString(branch)}";
            using var request = CreateRequest(HttpMethod.Get, $"build/builds?api-version=7.1&$top={Math.Clamp(count, 1, 50)}{filter}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePipelines(doc.RootElement);
        }
        catch
        {
            return [];
        }
    }

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

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? body = null)
    {
        string baseUrl = $"https://dev.azure.com/{Uri.EscapeDataString(_settings.Organization)}/{Uri.EscapeDataString(_settings.Project)}/_apis/";
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), relativePath));
        string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static List<Pipeline> ParsePipelines(JsonElement root)
    {
        var pipelines = new List<Pipeline>();
        if (!root.TryGetProperty("value", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return pipelines;

        foreach (var item in values.EnumerateArray())
        {
            string id = item.TryGetProperty("id", out JsonElement idValue) ? idValue.ToString() : Guid.NewGuid().ToString("N");
            string name = item.TryGetProperty("definition", out JsonElement definition) && definition.TryGetProperty("name", out JsonElement nameValue) ? nameValue.ToString() : "Azure DevOps Build";
            string branch = item.TryGetProperty("sourceBranch", out JsonElement branchValue) ? branchValue.ToString().Replace("refs/heads/", string.Empty, StringComparison.OrdinalIgnoreCase) : "unknown";
            DateTimeOffset startedAt = item.TryGetProperty("startTime", out JsonElement start) && DateTimeOffset.TryParse(start.ToString(), out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow;
            DateTimeOffset? finishedAt = item.TryGetProperty("finishTime", out JsonElement finish) && DateTimeOffset.TryParse(finish.ToString(), out DateTimeOffset finished) ? finished : null;
            var status = ParseDeploymentStatus(item.TryGetProperty("result", out JsonElement result) ? result.ToString() : item.TryGetProperty("status", out JsonElement statusValue) ? statusValue.ToString() : null);
            pipelines.Add(new Pipeline(id, name, branch, status, startedAt, finishedAt.HasValue ? finishedAt - startedAt : null, item.TryGetProperty("_links", out JsonElement links) && links.TryGetProperty("web", out JsonElement web) && web.TryGetProperty("href", out JsonElement href) ? href.ToString() : null, []));
        }

        return pipelines.OrderByDescending(p => p.StartedAt).ToList();
    }

    private static List<Deployment> ParseDeployments(JsonElement root)
    {
        var deployments = new List<Deployment>();
        if (!root.TryGetProperty("value", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return deployments;

        foreach (var item in values.EnumerateArray())
        {
            string id = item.TryGetProperty("id", out JsonElement idValue) ? idValue.ToString() : Guid.NewGuid().ToString("N");
            string service = item.TryGetProperty("name", out JsonElement nameValue) ? nameValue.ToString() : "Azure DevOps Release";
            string environment = item.TryGetProperty("environments", out JsonElement environments) && environments.ValueKind == JsonValueKind.Array && environments.GetArrayLength() > 0 ? environments[0].TryGetProperty("name", out JsonElement envName) ? envName.ToString() : "unknown" : "unknown";
            string version = item.TryGetProperty("releaseDefinition", out JsonElement releaseDefinition) && releaseDefinition.TryGetProperty("name", out JsonElement releaseName) ? releaseName.ToString() : "release";
            DateTimeOffset deployedAt = item.TryGetProperty("createdOn", out JsonElement created) && DateTimeOffset.TryParse(created.ToString(), out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow;
            deployments.Add(new Deployment(id, service, version, environment, ParseDeploymentStatus(item.TryGetProperty("status", out JsonElement status) ? status.ToString() : null), deployedAt, item.TryGetProperty("createdBy", out JsonElement by) && by.TryGetProperty("displayName", out JsonElement displayName) ? displayName.ToString() : null, null, item.TryGetProperty("_links", out JsonElement links) && links.TryGetProperty("web", out JsonElement web) && web.TryGetProperty("href", out JsonElement href) ? href.ToString() : null, []));
        }

        return deployments.OrderByDescending(d => d.DeployedAt).ToList();
    }

    private IReadOnlyList<Incident> ParseIncidents(JsonElement root)
    {
        var incidents = new List<Incident>();
        if (!root.TryGetProperty("value", out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return incidents;

        foreach (var item in values.EnumerateArray())
        {
            if (!item.TryGetProperty("fields", out JsonElement fields))
                continue;

            string id = item.TryGetProperty("id", out JsonElement idValue) ? idValue.ToString() : Guid.NewGuid().ToString("N");
            string title = fields.TryGetProperty("System.Title", out JsonElement titleValue) ? titleValue.ToString() : "Active Azure DevOps incident";
            string state = fields.TryGetProperty("System.State", out JsonElement stateValue) ? stateValue.ToString() : "Active";
            string severity = fields.TryGetProperty("Microsoft.VSTS.Common.Severity", out JsonElement severityValue) ? severityValue.ToString() : "2 - High";
            string changedDate = fields.TryGetProperty("System.ChangedDate", out JsonElement changed) ? changed.ToString() : string.Empty;
            incidents.Add(new Incident(id, title, ParseSeverity(severity), state.Equals("Resolved", StringComparison.OrdinalIgnoreCase) || state.Equals("Closed", StringComparison.OrdinalIgnoreCase) ? IncidentStatus.Resolved : IncidentStatus.Triggered, DateTimeOffset.TryParse(changedDate, out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow, null, fields.TryGetProperty("System.AreaPath", out JsonElement area) ? area.ToString() : _settings.Project, "Imported from Azure DevOps work item", [], item.TryGetProperty("url", out JsonElement url) ? url.ToString() : null));
        }

        return incidents;
    }

    private static DeploymentStatus ParseDeploymentStatus(string? value)
        => value?.ToLowerInvariant() switch
        {
            "succeeded" or "completed" => DeploymentStatus.Succeeded,
            "inprogress" or "notstarted" => DeploymentStatus.Running,
            "canceled" or "cancelled" => DeploymentStatus.RolledBack,
            "failed" or "partiallysucceeded" => DeploymentStatus.Failed,
            _ => DeploymentStatus.Pending
        };

    private static Severity ParseSeverity(string? value)
        => value?.ToLowerInvariant() switch
        {
            var s when s is not null && s.Contains("1") => Severity.Critical,
            var s when s is not null && s.Contains("2") => Severity.High,
            var s when s is not null && s.Contains("3") => Severity.Medium,
            var s when s is not null && s.Contains("4") => Severity.Low,
            _ => Severity.Medium
        };
}

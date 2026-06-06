using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class KubernetesConnector : IAIOpsConnector
{
    private readonly KubernetesSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public KubernetesConnector(KubernetesSettings settings)
    {
        _settings = settings;
        var handler = new HttpClientHandler();
        if (settings.SkipTlsVerify)
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        }

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    public string Name => "Kubernetes";
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
            using var request = CreateRequest($"/api/v1/namespaces/{Uri.EscapeDataString(_settings.Namespace)}/pods");
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

    public async Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        try
        {
            var deployments = await GetRecentDeploymentsAsync(serviceName, 1, ct).ConfigureAwait(false);
            var incidents = await GetActiveIncidentsAsync(ct).ConfigureAwait(false);
            if (deployments.Count == 0 && incidents.Count == 0)
                return null;

            int relatedIncidentCount = incidents.Count(i => string.Equals(i.AffectedService, serviceName, StringComparison.OrdinalIgnoreCase));
            return new ServiceHealth(serviceName, relatedIncidentCount * 2.5, Math.Max(50, 70 + relatedIncidentCount * 40), Math.Max(90, 99.5 - relatedIncidentCount), DateTimeOffset.UtcNow);
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
            using var request = CreateRequest($"/api/v1/namespaces/{Uri.EscapeDataString(_settings.Namespace)}/pods");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePodIncidents(doc.RootElement, _settings.Namespace);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
        => await GetActiveIncidentsAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string path = string.IsNullOrWhiteSpace(service)
                ? $"/apis/apps/v1/namespaces/{Uri.EscapeDataString(_settings.Namespace)}/deployments"
                : $"/apis/apps/v1/namespaces/{Uri.EscapeDataString(_settings.Namespace)}/deployments/{Uri.EscapeDataString(service)}";
            using var request = CreateRequest(path);
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var deployments = ParseDeployments(doc.RootElement, _settings.Namespace);
            if (!string.IsNullOrWhiteSpace(service))
                deployments = deployments.Where(d => d.Service.Equals(service, StringComparison.OrdinalIgnoreCase)).ToList();
            return deployments.OrderByDescending(d => d.DeployedAt).Take(count).ToList();
        }
        catch
        {
            return [];
        }
    }

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
        string baseUrl = _settings.ApiServerUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{relativePath}");
        if (!string.IsNullOrWhiteSpace(_settings.BearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.BearerToken);
        return request;
    }

    private static List<Deployment> ParseDeployments(JsonElement root, string kubernetesNamespace)
    {
        var result = new List<Deployment>();
        IEnumerable<JsonElement> items = root.TryGetProperty("items", out JsonElement itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
            ? itemsElement.EnumerateArray()
            : [root];

        foreach (var item in items)
        {
            if (!item.TryGetProperty("metadata", out JsonElement metadata) || !item.TryGetProperty("spec", out JsonElement spec))
                continue;

            string name = metadata.TryGetProperty("name", out JsonElement nameValue) ? nameValue.ToString() : "deployment";
            string image = spec.TryGetProperty("template", out JsonElement template)
                && template.TryGetProperty("spec", out JsonElement templateSpec)
                && templateSpec.TryGetProperty("containers", out JsonElement containers)
                && containers.ValueKind == JsonValueKind.Array && containers.GetArrayLength() > 0
                && containers[0].TryGetProperty("image", out JsonElement imageValue)
                ? imageValue.ToString()
                : string.Empty;
            string version = image.Contains(':', StringComparison.Ordinal) ? image[(image.LastIndexOf(':') + 1)..] : "latest";
            DateTimeOffset deployedAt = metadata.TryGetProperty("creationTimestamp", out JsonElement created) && DateTimeOffset.TryParse(created.ToString(), out DateTimeOffset parsed) ? parsed : DateTimeOffset.UtcNow;
            DeploymentStatus status = ParseDeploymentStatus(item.TryGetProperty("status", out JsonElement statusElement) ? statusElement : default);
            result.Add(new Deployment(name, name, version, kubernetesNamespace, status, deployedAt, null, null, $"{name}@k8s", [image]));
        }

        return result;
    }

    private static IReadOnlyList<Incident> ParsePodIncidents(JsonElement root, string kubernetesNamespace)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var severities = new Dictionary<string, Severity>(StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            return [];

        foreach (var pod in items.EnumerateArray())
        {
            string podName = pod.TryGetProperty("metadata", out JsonElement metadata) && metadata.TryGetProperty("name", out JsonElement podNameElement) ? podNameElement.ToString() : "pod";
            string service = metadata.TryGetProperty("labels", out JsonElement labels) && labels.TryGetProperty("app", out JsonElement appLabel) ? appLabel.ToString() : podName;
            if (!pod.TryGetProperty("status", out JsonElement status))
                continue;

            bool unhealthy = false;
            string reason = status.TryGetProperty("phase", out JsonElement phase) ? phase.ToString() : "Unknown";
            if (status.TryGetProperty("containerStatuses", out JsonElement containerStatuses) && containerStatuses.ValueKind == JsonValueKind.Array)
            {
                foreach (var container in containerStatuses.EnumerateArray())
                {
                    if (container.TryGetProperty("restartCount", out JsonElement restartCount) && restartCount.TryGetInt32(out int count) && count > 2)
                    {
                        unhealthy = true;
                        reason = $"CrashLoopBackOff / restarts={count}";
                    }

                    if (container.TryGetProperty("ready", out JsonElement ready) && ready.ValueKind == JsonValueKind.False)
                        unhealthy = true;
                }
            }

            if (!unhealthy && !reason.Equals("Failed", StringComparison.OrdinalIgnoreCase) && !reason.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                continue;

            grouped.TryAdd(service, []);
            grouped[service].Add($"{podName}: {reason}");
            severities[service] = grouped[service].Count >= 3 ? Severity.Critical : grouped[service].Count == 2 ? Severity.High : Severity.Medium;
        }

        return grouped.Select(kvp => new Incident(
            $"k8s-{kvp.Key}",
            $"Kubernetes pods unhealthy for {kvp.Key}",
            severities[kvp.Key],
            IncidentStatus.Triggered,
            DateTimeOffset.UtcNow.AddMinutes(-15),
            null,
            kvp.Key,
            $"Detected unhealthy pods in namespace {kubernetesNamespace}",
            kvp.Value,
            null)).ToList();
    }

    private static DeploymentStatus ParseDeploymentStatus(JsonElement status)
    {
        if (status.ValueKind == JsonValueKind.Undefined)
            return DeploymentStatus.Pending;

        if (status.TryGetProperty("conditions", out JsonElement conditions) && conditions.ValueKind == JsonValueKind.Array)
        {
            foreach (var condition in conditions.EnumerateArray())
            {
                string type = condition.TryGetProperty("type", out JsonElement typeValue) ? typeValue.ToString() : string.Empty;
                string conditionStatus = condition.TryGetProperty("status", out JsonElement statusValue) ? statusValue.ToString() : string.Empty;
                if (type.Equals("Available", StringComparison.OrdinalIgnoreCase) && conditionStatus.Equals("True", StringComparison.OrdinalIgnoreCase))
                    return DeploymentStatus.Succeeded;
                if (type.Equals("Progressing", StringComparison.OrdinalIgnoreCase) && conditionStatus.Equals("True", StringComparison.OrdinalIgnoreCase))
                    return DeploymentStatus.Running;
            }
        }

        return DeploymentStatus.Pending;
    }
}

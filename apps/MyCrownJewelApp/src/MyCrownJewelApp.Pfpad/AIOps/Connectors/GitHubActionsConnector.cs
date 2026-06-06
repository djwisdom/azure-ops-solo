using System.Net.Http.Headers;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class GitHubActionsConnector : IAIOpsConnector, IPullRequestCapable, IGitHistoryCapable
{
    private readonly GitHubActionsSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    public GitHubActionsConnector(GitHubActionsSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)) };
    }

    public string Name => "GitHub Actions";
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
            using var request = CreateRequest($"/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repository)}/actions/runs?per_page=1");
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

    public Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Incident>>([]);

    public Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Incident>>([]);

    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Deployment>>([]);

    public async Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"&branch={Uri.EscapeDataString(branch)}";
            using var request = CreateRequest($"/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repository)}/actions/runs?per_page={Math.Clamp(count, 1, 50)}{filter}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePipelines(doc.RootElement).Take(count).ToList();
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Slo>>([]);

    public async Task<IReadOnlyList<GitCommit>> GetRecentCommitsAsync(string? branch = null, int count = 30, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(branch) ? string.Empty : $"&sha={Uri.EscapeDataString(branch)}";
            using var request = CreateRequest($"/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repository)}/commits?per_page={Math.Clamp(count, 1, 50)}{filter}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return await ParseCommitsAsync(doc.RootElement, ct).ConfigureAwait(false);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GitPullRequest>> GetRecentPullRequestsAsync(string? repository = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false))
            return [];

        try
        {
            using var request = CreateRequest($"/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repository)}/pulls?state=all&per_page={Math.Clamp(count, 1, 50)}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePullRequests(doc.RootElement).Take(count).ToList();
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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com{relativePath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.PersonalAccessToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("pfpad", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return request;
    }

    private async Task<IReadOnlyList<GitCommit>> ParseCommitsAsync(JsonElement root, CancellationToken ct)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return [];

        var commits = new List<GitCommit>();
        foreach (var item in root.EnumerateArray())
        {
            string sha = item.TryGetProperty("sha", out JsonElement shaValue) ? shaValue.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            commits.Add(await FetchCommitDetailsAsync(sha, item, ct).ConfigureAwait(false));
        }

        return commits.OrderByDescending(c => c.Timestamp).ToList();
    }

    private async Task<GitCommit> FetchCommitDetailsAsync(string sha, JsonElement summary, CancellationToken ct)
    {
        try
        {
            using var request = CreateRequest($"/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repository)}/commits/{Uri.EscapeDataString(sha)}");
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return ParseCommit(summary, sha, []);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseCommit(doc.RootElement, sha, ParseChangedFiles(doc.RootElement));
        }
        catch
        {
            return ParseCommit(summary, sha, []);
        }
    }

    private static GitCommit ParseCommit(JsonElement item, string sha, IReadOnlyList<string> changedFiles)
    {
        JsonElement commit = item.TryGetProperty("commit", out JsonElement commitValue) ? commitValue : default;
        JsonElement author = commit.ValueKind != JsonValueKind.Undefined && commit.TryGetProperty("author", out JsonElement authorValue) ? authorValue : default;
        string message = commit.ValueKind != JsonValueKind.Undefined && commit.TryGetProperty("message", out JsonElement messageValue) ? messageValue.GetString() ?? "Commit" : "Commit";
        string authorName = author.ValueKind != JsonValueKind.Undefined && author.TryGetProperty("name", out JsonElement authorNameValue) ? authorNameValue.GetString() ?? "unknown" : "unknown";
        string authorEmail = author.ValueKind != JsonValueKind.Undefined && author.TryGetProperty("email", out JsonElement authorEmailValue) ? authorEmailValue.GetString() ?? "unknown@example.local" : "unknown@example.local";
        DateTimeOffset timestamp = author.ValueKind != JsonValueKind.Undefined && author.TryGetProperty("date", out JsonElement dateValue) && DateTimeOffset.TryParse(dateValue.GetString(), out DateTimeOffset parsed)
            ? parsed : DateTimeOffset.UtcNow;
        return new GitCommit(sha, sha.Length > 7 ? sha[..7] : sha, message, authorName, authorEmail, timestamp, changedFiles, null, item.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null);
    }

    private static IReadOnlyList<string> ParseChangedFiles(JsonElement root)
    {
        if (!root.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
            return [];

        return files.EnumerateArray()
            .Select(f => f.TryGetProperty("filename", out JsonElement filename) ? filename.GetString() : null)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Cast<string>()
            .ToList();
    }

    private static IReadOnlyList<GitPullRequest> ParsePullRequests(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return [];

        var prs = new List<GitPullRequest>();
        foreach (var item in root.EnumerateArray())
        {
            string id = item.TryGetProperty("number", out JsonElement number) ? number.ToString() : Guid.NewGuid().ToString("N");
            bool isDraft = item.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True;
            DateTimeOffset? mergedAt = item.TryGetProperty("merged_at", out JsonElement mergedAtValue) && DateTimeOffset.TryParse(mergedAtValue.GetString(), out DateTimeOffset merged)
                ? merged : null;
            PullRequestStatus status = mergedAt.HasValue ? PullRequestStatus.Merged : isDraft ? PullRequestStatus.Draft : (item.TryGetProperty("state", out JsonElement state) && state.GetString()?.Equals("closed", StringComparison.OrdinalIgnoreCase) == true ? PullRequestStatus.Closed : PullRequestStatus.Open);
            prs.Add(new GitPullRequest(
                id,
                item.TryGetProperty("title", out JsonElement title) ? title.GetString() ?? "Pull request" : "Pull request",
                item.TryGetProperty("user", out JsonElement user) && user.TryGetProperty("login", out JsonElement login) ? login.GetString() ?? "unknown" : "unknown",
                item.TryGetProperty("head", out JsonElement head) && head.TryGetProperty("ref", out JsonElement sourceRef) ? sourceRef.GetString() ?? "unknown" : "unknown",
                item.TryGetProperty("base", out JsonElement baseRef) && baseRef.TryGetProperty("ref", out JsonElement targetRef) ? targetRef.GetString() ?? "main" : "main",
                status,
                item.TryGetProperty("created_at", out JsonElement createdAt) && DateTimeOffset.TryParse(createdAt.GetString(), out DateTimeOffset created) ? created : DateTimeOffset.UtcNow,
                mergedAt,
                item.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null,
                item.TryGetProperty("body", out JsonElement body) ? body.GetString() : null,
                [],
                [],
                item.TryGetProperty("html_url", out JsonElement reviewUrl) ? reviewUrl.GetString() : null));
        }

        return prs.OrderByDescending(pr => pr.CreatedAt).ToList();
    }

    private static IReadOnlyList<Pipeline> ParsePipelines(JsonElement root)
    {
        if (!root.TryGetProperty("workflow_runs", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
            return [];

        var pipelines = new List<Pipeline>();
        foreach (var item in runs.EnumerateArray())
        {
            DateTimeOffset startedAt = item.TryGetProperty("run_started_at", out JsonElement startedAtValue) && DateTimeOffset.TryParse(startedAtValue.GetString(), out DateTimeOffset started)
                ? started
                : item.TryGetProperty("created_at", out JsonElement createdAt) && DateTimeOffset.TryParse(createdAt.GetString(), out DateTimeOffset created)
                    ? created : DateTimeOffset.UtcNow;
            DateTimeOffset? updatedAt = item.TryGetProperty("updated_at", out JsonElement updatedAtValue) && DateTimeOffset.TryParse(updatedAtValue.GetString(), out DateTimeOffset updated)
                ? updated : null;
            pipelines.Add(new Pipeline(
                item.TryGetProperty("id", out JsonElement id) ? id.ToString() : Guid.NewGuid().ToString("N"),
                item.TryGetProperty("name", out JsonElement name) ? name.GetString() ?? "GitHub Actions" : "GitHub Actions",
                item.TryGetProperty("head_branch", out JsonElement branch) ? branch.GetString() ?? "unknown" : "unknown",
                ParseDeploymentStatus(item),
                startedAt,
                updatedAt.HasValue ? updatedAt - startedAt : null,
                item.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null,
                []));
        }

        return pipelines.OrderByDescending(p => p.StartedAt).ToList();
    }

    private static DeploymentStatus ParseDeploymentStatus(JsonElement item)
    {
        string status = item.TryGetProperty("status", out JsonElement statusValue) ? statusValue.GetString() ?? string.Empty : string.Empty;
        string conclusion = item.TryGetProperty("conclusion", out JsonElement conclusionValue) ? conclusionValue.GetString() ?? string.Empty : string.Empty;
        return (status, conclusion) switch
        {
            ("completed", "success") => DeploymentStatus.Succeeded,
            ("completed", "failure") => DeploymentStatus.Failed,
            ("completed", "cancelled") => DeploymentStatus.RolledBack,
            ("queued", _) or ("in_progress", _) or ("waiting", _) => DeploymentStatus.Running,
            _ => DeploymentStatus.Pending
        };
    }
}

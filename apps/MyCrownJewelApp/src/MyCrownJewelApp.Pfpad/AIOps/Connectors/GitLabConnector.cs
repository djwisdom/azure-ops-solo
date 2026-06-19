using System.Net.Http.Headers;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Connector for GitLab (cloud or self-hosted) using the GitLab REST API v4.
/// Supports CI pipeline runs, merge requests, and commit history.
/// Authentication: PRIVATE-TOKEN header (personal access token or project token).
/// </summary>
public sealed class GitLabConnector : IAIOpsConnector, IPullRequestCapable, IGitHistoryCapable
{
    private readonly GitLabSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    /// <summary>URL-encoded project path, e.g. "mygroup%2Fmyrepo".</summary>
    private string EncodedPath => Uri.EscapeDataString(_settings.NamespacePath);

    public GitLabConnector(GitLabSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.InstanceUrl.TrimEnd('/') + "/api/v4/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds))
        };
        _httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", settings.PersonalAccessToken);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("pfpad", "1.0"));
    }

    public string Name => "GitLab";
    public ConnectorStatus Status => _status;

    // ── IAIOpsConnector lifecycle ─────────────────────────────────────────────

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
            using var response = await _httpClient
                .GetAsync($"projects/{EncodedPath}", ct).ConfigureAwait(false);
            _status = response.IsSuccessStatusCode ? ConnectorStatus.Connected : ConnectorStatus.Error;
            return new ConnectorState(Name, _status,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}",
                DateTimeOffset.UtcNow);
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

    // ── IAIOpsConnector stubs (GitLab not an ops backend) ────────────────────

    public Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string s, string m, DateTimeOffset f, DateTimeOffset t, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MetricPoint>>([]);
    public Task<IReadOnlyList<LogEntry>> GetLogsAsync(string s, DateTimeOffset f, DateTimeOffset t, int l = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LogEntry>>([]);
    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TraceSpan>>([]);
    public Task<ServiceHealth?> GetServiceHealthAsync(string s, CancellationToken ct = default) => Task.FromResult<ServiceHealth?>(null);
    public Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Incident>>([]);
    public Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Incident>>([]);
    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Deployment>>([]);
    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? s = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Slo>>([]);

    // ── CI Pipeline runs ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(
        string? branch = null, int count = 10, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(branch)
                ? "" : $"&ref={Uri.EscapeDataString(branch)}";
            using var response = await _httpClient
                .GetAsync($"projects/{EncodedPath}/pipelines?per_page={Math.Clamp(count, 1, 50)}{filter}", ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePipelines(doc.RootElement).Take(count).ToList();
        }
        catch { return []; }
    }

    // ── IPullRequestCapable (Merge Requests) ──────────────────────────────────

    public async Task<IReadOnlyList<GitPullRequest>> GetRecentPullRequestsAsync(
        string? repository = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return [];

        try
        {
            using var response = await _httpClient
                .GetAsync($"projects/{EncodedPath}/merge_requests?state=all&per_page={Math.Clamp(count, 1, 50)}", ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseMergeRequests(doc.RootElement).Take(count).ToList();
        }
        catch { return []; }
    }

    // ── IGitHistoryCapable (Commits) ──────────────────────────────────────────

    public async Task<IReadOnlyList<GitCommit>> GetRecentCommitsAsync(
        string? branch = null, int count = 30, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return [];

        try
        {
            string filter = string.IsNullOrWhiteSpace(branch)
                ? "" : $"&ref_name={Uri.EscapeDataString(branch)}";
            using var response = await _httpClient
                .GetAsync($"projects/{EncodedPath}/repository/commits?per_page={Math.Clamp(count, 1, 50)}{filter}", ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParseCommits(doc.RootElement).Take(count).ToList();
        }
        catch { return []; }
    }

    public void Dispose() => _httpClient.Dispose();

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_status == ConnectorStatus.Connected) return true;
        return (await ConnectAsync(ct).ConfigureAwait(false)).Status == ConnectorStatus.Connected;
    }

    private static List<Pipeline> ParsePipelines(JsonElement root)
    {
        var list = new List<Pipeline>();
        if (root.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in root.EnumerateArray())
        {
            string id     = item.TryGetProperty("id", out var v) ? v.ToString() : Guid.NewGuid().ToString("N");
            string branch = item.TryGetProperty("ref", out var r) ? r.GetString() ?? "unknown" : "unknown";
            string glStatus = item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            string createdStr = item.TryGetProperty("created_at", out var c) ? c.GetString() ?? "" : "";
            string updatedStr = item.TryGetProperty("updated_at", out var u) ? u.GetString() ?? "" : "";
            DateTimeOffset created = DateTimeOffset.TryParse(createdStr, out var p) ? p : DateTimeOffset.UtcNow;
            DateTimeOffset? updated = DateTimeOffset.TryParse(updatedStr, out var q) ? q : (DateTimeOffset?)null;
            string? webUrl = item.TryGetProperty("web_url", out var w) ? w.GetString() : null;

            var status = glStatus switch
            {
                "success"             => DeploymentStatus.Succeeded,
                "failed"              => DeploymentStatus.Failed,
                "canceled"            => DeploymentStatus.RolledBack,
                "running" or "pending" => DeploymentStatus.Running,
                _                     => DeploymentStatus.Pending
            };

            list.Add(new Pipeline(id, $"Pipeline #{id}", branch, status, created,
                updated.HasValue ? updated - created : null, webUrl, []));
        }

        return list.OrderByDescending(p => p.StartedAt).ToList();
    }

    private static List<GitPullRequest> ParseMergeRequests(JsonElement root)
    {
        var list = new List<GitPullRequest>();
        if (root.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in root.EnumerateArray())
        {
            string id         = item.TryGetProperty("iid", out var v) ? v.ToString() : Guid.NewGuid().ToString("N");
            string title      = item.TryGetProperty("title", out var t) ? t.GetString() ?? "MR" : "MR";
            string authorName = item.TryGetProperty("author", out var a) && a.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
            string src        = item.TryGetProperty("source_branch", out var sb) ? sb.GetString() ?? "" : "";
            string tgt        = item.TryGetProperty("target_branch", out var tb) ? tb.GetString() ?? "" : "";
            string state      = item.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
            string desc       = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            string? url       = item.TryGetProperty("web_url", out var w) ? w.GetString() : null;
            string createdStr = item.TryGetProperty("created_at", out var c) ? c.GetString() ?? "" : "";
            string mergedStr  = item.TryGetProperty("merged_at", out var m) ? m.GetString() ?? "" : "";

            DateTimeOffset created = DateTimeOffset.TryParse(createdStr, out var p) ? p : DateTimeOffset.UtcNow;
            DateTimeOffset? merged = DateTimeOffset.TryParse(mergedStr, out var q) ? q : (DateTimeOffset?)null;

            var status = state switch
            {
                "merged" => PullRequestStatus.Merged,
                "closed" => PullRequestStatus.Closed,
                "opened" => PullRequestStatus.Open,
                _        => PullRequestStatus.Open
            };

            list.Add(new GitPullRequest(id, title, authorName, src, tgt, status, created, merged, url, desc, [], []));
        }

        return list.OrderByDescending(pr => pr.CreatedAt).ToList();
    }

    private static List<GitCommit> ParseCommits(JsonElement root)
    {
        var list = new List<GitCommit>();
        if (root.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in root.EnumerateArray())
        {
            string sha        = item.TryGetProperty("id", out var v) ? v.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            string msg        = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Commit" : "Commit";
            string author     = item.TryGetProperty("author_name", out var an) ? an.GetString() ?? "unknown" : "unknown";
            string email      = item.TryGetProperty("author_email", out var ae) ? ae.GetString() ?? "" : "";
            string dateStr    = item.TryGetProperty("authored_date", out var dt) ? dt.GetString() ?? "" : "";
            string? url       = item.TryGetProperty("web_url", out var w) ? w.GetString() : null;

            DateTimeOffset ts = DateTimeOffset.TryParse(dateStr, out var p) ? p : DateTimeOffset.UtcNow;
            string shortSha   = sha.Length >= 8 ? sha[..8] : sha;

            list.Add(new GitCommit(sha, shortSha, msg, author, email, ts, [], null, url));
        }

        return list.OrderByDescending(c => c.Timestamp).ToList();
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Connector for Bitbucket Cloud using the Atlassian REST API 2.0.
/// Supports CI pipeline runs and pull requests.
/// Authentication: HTTP Basic with app password (username + app password).
/// </summary>
public sealed class BitbucketConnector : IAIOpsConnector, IPullRequestCapable
{
    private const string ApiBase = "https://api.bitbucket.org/2.0/";

    private readonly BitbucketSettings _settings;
    private readonly HttpClient _httpClient;
    private ConnectorStatus _status = ConnectorStatus.Disconnected;

    private string WorkspaceSlug => Uri.EscapeDataString(_settings.Workspace);
    private string RepoSlug      => Uri.EscapeDataString(_settings.Repository);

    public BitbucketConnector(BitbucketSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(ApiBase),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds))
        };
        // Basic auth: username:app_password
        string creds = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{settings.Username}:{settings.AppPassword}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", creds);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("pfpad", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Name => "Bitbucket";
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
                .GetAsync($"repositories/{WorkspaceSlug}/{RepoSlug}", ct)
                .ConfigureAwait(false);
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

    // ── IAIOpsConnector stubs ─────────────────────────────────────────────────

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
            // Bitbucket Pipelines API does not support branch filter in basic GET.
            // Sort by creation descending and filter client-side if branch is specified.
            string url = $"repositories/{WorkspaceSlug}/{RepoSlug}/pipelines/" +
                         $"?sort=-created_on&pagelen={Math.Clamp(count * 2, 10, 50)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var all = ParsePipelines(doc.RootElement);
            return (string.IsNullOrWhiteSpace(branch)
                ? all
                : all.Where(p => string.Equals(p.Branch, branch, StringComparison.OrdinalIgnoreCase)).ToList()
            ).Take(count).ToList();
        }
        catch { return []; }
    }

    // ── IPullRequestCapable ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<GitPullRequest>> GetRecentPullRequestsAsync(
        string? repository = null, int count = 20, CancellationToken ct = default)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return [];

        try
        {
            // state=ALL returns open, merged, declined, superseded
            string url = $"repositories/{WorkspaceSlug}/{RepoSlug}/pullrequests" +
                         $"?state=ALL&sort=-created_on&pagelen={Math.Clamp(count, 1, 50)}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return ParsePullRequests(doc.RootElement).Take(count).ToList();
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
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in values.EnumerateArray())
        {
            string id = item.TryGetProperty("uuid", out var u) ? u.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            id = id.Trim('{', '}'); // Bitbucket UUIDs include braces

            // Branch: target.ref_name
            string branch = "unknown";
            if (item.TryGetProperty("target", out var target) && target.TryGetProperty("ref_name", out var rn))
                branch = rn.GetString() ?? "unknown";

            string? createdStr = item.TryGetProperty("created_on", out var c) ? c.GetString() : null;
            string? completedStr = item.TryGetProperty("completed_on", out var co) ? co.GetString() : null;
            DateTimeOffset created = DateTimeOffset.TryParse(createdStr, out var p) ? p : DateTimeOffset.UtcNow;
            DateTimeOffset? completed = DateTimeOffset.TryParse(completedStr, out var q) ? q : (DateTimeOffset?)null;

            // state.name: "IN_PROGRESS", "COMPLETED", "PENDING", "HALTED", "ERROR", "STOPPED"
            // state.result.name: "SUCCESSFUL", "FAILED", "ERROR", "STOPPED"
            string stateName = "PENDING";
            string resultName = "";
            if (item.TryGetProperty("state", out var state))
            {
                stateName = state.TryGetProperty("name", out var sn) ? sn.GetString() ?? "PENDING" : "PENDING";
                if (state.TryGetProperty("result", out var result))
                    resultName = result.TryGetProperty("name", out var rr) ? rr.GetString() ?? "" : "";
            }

            var status = (stateName, resultName) switch
            {
                ("COMPLETED", "SUCCESSFUL") => DeploymentStatus.Succeeded,
                ("COMPLETED", "FAILED") or ("COMPLETED", "ERROR") => DeploymentStatus.Failed,
                ("COMPLETED", "STOPPED") or ("HALTED", _) => DeploymentStatus.RolledBack,
                ("IN_PROGRESS", _) => DeploymentStatus.Running,
                _ => DeploymentStatus.Pending
            };

            // Link: links.self.href
            string? webUrl = null;
            if (item.TryGetProperty("links", out var links) && links.TryGetProperty("self", out var self))
                webUrl = self.TryGetProperty("href", out var href) ? href.GetString() : null;

            list.Add(new Pipeline(id, $"Pipeline {id[..8]}", branch, status, created,
                completed.HasValue ? completed - created : null, webUrl, []));
        }

        return list.OrderByDescending(p => p.StartedAt).ToList();
    }

    private static List<GitPullRequest> ParsePullRequests(JsonElement root)
    {
        var list = new List<GitPullRequest>();
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in values.EnumerateArray())
        {
            string id    = item.TryGetProperty("id", out var v) ? v.ToString() : Guid.NewGuid().ToString("N");
            string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "PR" : "PR";
            string desc  = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            string state = item.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";

            string authorName = "unknown";
            if (item.TryGetProperty("author", out var author) && author.TryGetProperty("display_name", out var dn))
                authorName = dn.GetString() ?? "unknown";

            string src = "", tgt = "";
            if (item.TryGetProperty("source", out var srcBranch) && srcBranch.TryGetProperty("branch", out var sb))
                src = sb.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
            if (item.TryGetProperty("destination", out var tgtBranch) && tgtBranch.TryGetProperty("branch", out var tb))
                tgt = tb.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";

            string? webUrl = item.TryGetProperty("links", out var links)
                && links.TryGetProperty("html", out var html)
                ? html.TryGetProperty("href", out var href) ? href.GetString() : null
                : null;

            string createdStr = item.TryGetProperty("created_on", out var c) ? c.GetString() ?? "" : "";
            string mergedStr  = item.TryGetProperty("updated_on", out var u) ? u.GetString() ?? "" : "";
            DateTimeOffset created = DateTimeOffset.TryParse(createdStr, out var p) ? p : DateTimeOffset.UtcNow;
            DateTimeOffset? merged = state == "MERGED" && DateTimeOffset.TryParse(mergedStr, out var q) ? q : (DateTimeOffset?)null;

            var prStatus = state switch
            {
                "MERGED"    => PullRequestStatus.Merged,
                "DECLINED"  => PullRequestStatus.Closed,
                "SUPERSEDED"=> PullRequestStatus.Closed,
                _           => PullRequestStatus.Open
            };

            list.Add(new GitPullRequest(id, title, authorName, src, tgt, prStatus, created, merged, webUrl, desc, [], []));
        }

        return list.OrderByDescending(pr => pr.CreatedAt).ToList();
    }
}

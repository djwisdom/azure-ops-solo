using System.Globalization;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class MockDataConnector : IAIOpsConnector
{
    private static readonly string[] Services = ["editor-api", "analysis-worker", "git-sync", "telemetry-gateway"];
    private static readonly string[] Environments = ["dev", "staging", "prod"];

    public string Name => "Mock Data";
    public ConnectorStatus Status => ConnectorStatus.Connected;

    public Task<ConnectorState> ConnectAsync(CancellationToken ct = default)
        => Task.FromResult(new ConnectorState(Name, ConnectorStatus.Connected, LastSyncAt: DateTimeOffset.UtcNow));

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var points = new List<MetricPoint>();
        string unit = metricName.Contains("latency", StringComparison.OrdinalIgnoreCase) ? "ms" : metricName.Contains("error", StringComparison.OrdinalIgnoreCase) ? "%" : "count";
        TimeSpan span = to - from;
        int buckets = Math.Clamp((int)Math.Ceiling(span.TotalHours), 6, 24);
        double stepMinutes = Math.Max(5, span.TotalMinutes / buckets);
        for (int i = 0; i < buckets; i++)
        {
            var timestamp = from.AddMinutes(stepMinutes * i);
            double value = GetMetricValue(serviceName, metricName, i);
            points.Add(new MetricPoint(metricName, value, unit, timestamp, new Dictionary<string, string>
            {
                ["service"] = serviceName,
                ["env"] = PickEnvironment(serviceName)
            }));
        }

        return Task.FromResult<IReadOnlyList<MetricPoint>>(points);
    }

    public Task<IReadOnlyList<LogEntry>> GetLogsAsync(string serviceName, DateTimeOffset from, DateTimeOffset to, int limit = 100, CancellationToken ct = default)
    {
        string env = PickEnvironment(serviceName);
        var severities = new[] { Severity.Info, Severity.Info, Severity.Low, Severity.Info, Severity.Medium, Severity.High };
        var messages = new[]
        {
            "HTTP 200 GET /api/documents completed in 84ms",
            "Background index refresh finished for workspace pfpad-demo",
            "Retrying Azure Monitor query after transient 429",
            "Cache warm-up completed for syntax trees",
            "Warning: p99 latency increased for /api/git/status",
            "Unhandled timeout while loading remote repository metadata"
        };

        var logs = new List<LogEntry>();
        for (int i = 0; i < Math.Min(limit, 18); i++)
        {
            int seed = StableSeed(serviceName, i);
            var severity = severities[i % severities.Length];
            var timestamp = to.AddMinutes(-(i * 7) - (seed % 4));
            if (timestamp < from)
                break;

            logs.Add(new LogEntry(
                timestamp,
                severity,
                messages[i % messages.Length],
                TraceId: $"trace-{seed:X8}",
                SpanId: $"span-{(seed * 17):X6}",
                Properties: new Dictionary<string, string>
                {
                    ["service"] = serviceName,
                    ["environment"] = env,
                    ["host"] = $"{serviceName}-{(seed % 3) + 1}",
                    ["requestId"] = $"req-{seed % 10000:D4}"
                }));
        }

        return Task.FromResult<IReadOnlyList<LogEntry>>(logs.OrderByDescending(l => l.Timestamp).ToList());
    }

    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default)
    {
        int seed = StableSeed(traceId, 0);
        var start = DateTimeOffset.UtcNow.AddSeconds(-30 - (seed % 30));
        var spans = new List<TraceSpan>
        {
            new(traceId, "root", null, "editor-api", "GET /api/deployments", start, TimeSpan.FromMilliseconds(142), false,
                new Dictionary<string, string> { ["http.method"] = "GET", ["http.route"] = "/api/deployments" }),
            new(traceId, "db01", "root", "analysis-worker", "LoadDeploymentMetadata", start.AddMilliseconds(12), TimeSpan.FromMilliseconds(64), false,
                new Dictionary<string, string> { ["db.system"] = "mssql", ["db.operation"] = "SELECT" }),
            new(traceId, "ext01", "root", "git-sync", "FetchCommitDiff", start.AddMilliseconds(30), TimeSpan.FromMilliseconds(55), seed % 5 == 0,
                new Dictionary<string, string> { ["peer.service"] = "github", ["retry.count"] = (seed % 2).ToString(CultureInfo.InvariantCulture) })
        };

        return Task.FromResult<IReadOnlyList<TraceSpan>>(spans);
    }

    public Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        int seed = StableSeed(serviceName, 99);
        double errorRate = 0.1 + (seed % 24) / 10.0;
        double p99 = 50 + (seed % 251);
        double availability = 97.5 + ((seed % 25) / 10.0);
        var health = new ServiceHealth(serviceName, Math.Round(errorRate, 2), Math.Round(p99, 1), Math.Min(99.99, Math.Round(availability, 2)), DateTimeOffset.UtcNow);
        return Task.FromResult<ServiceHealth?>(health);
    }

    public Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Incident> incidents =
        [
            new Incident(
                "inc-crit-001",
                "Production telemetry gateway dropping trace exports",
                Severity.Critical,
                IncidentStatus.Triggered,
                DateTimeOffset.UtcNow.AddMinutes(-37),
                null,
                "telemetry-gateway",
                "Recent deployment enabled aggressive batching causing memory pressure and dropped spans.",
                ["POST /v1/traces", "POST /v1/metrics"],
                "https://contoso.example/postmortems/telemetry-gateway-001"),
            new Incident(
                "inc-med-002",
                "Staging git status refresh latency elevated",
                Severity.Medium,
                IncidentStatus.Acknowledged,
                DateTimeOffset.UtcNow.AddHours(-2),
                null,
                "git-sync",
                "Large monorepo diff generation is saturating one worker instance.",
                ["GET /api/git/status", "GET /api/git/diff"])
        ];

        return Task.FromResult(incidents);
    }

    public Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
    {
        var incidents = new List<Incident>
        {
            new("inc-res-010", "Editor API brief 502 spike", Severity.High, IncidentStatus.Resolved, DateTimeOffset.UtcNow.AddDays(-1).AddHours(-3), DateTimeOffset.UtcNow.AddDays(-1).AddHours(-2), "editor-api", "Transient reverse proxy recycle", ["GET /api/documents"]),
            new("inc-res-011", "Background worker queue lag", Severity.Medium, IncidentStatus.Resolved, DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-3).AddHours(1), "analysis-worker", "Unexpected full reindex after schema drift", ["/jobs/index", "/jobs/symbols"])
        };
        incidents.AddRange(GetActiveIncidentsAsync(ct).Result);
        return Task.FromResult<IReadOnlyList<Incident>>(incidents.Where(i => i.StartedAt >= DateTimeOffset.UtcNow.AddDays(-days)).OrderByDescending(i => i.StartedAt).ToList());
    }

    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
    {
        var deployments = new List<Deployment>
        {
            BuildDeployment("dep-001", "editor-api", "1.0.34", "dev", DeploymentStatus.Succeeded, -1.2, "dennis", "a1b2c3d", ["Form1.cs", "AIOps/Engines/AIOpsEngine.cs"]),
            BuildDeployment("dep-002", "analysis-worker", "1.0.34-rc1", "staging", DeploymentStatus.Succeeded, -6.5, "copilot", "e4f5a6b", ["RepositoryAnalyticsEngine.cs", "AIOps/Engines/DeploymentRiskScorer.cs"]),
            BuildDeployment("dep-003", "telemetry-gateway", "1.0.33", "prod", DeploymentStatus.Failed, -13.3, "release-bot", "90ab12c", ["Dockerfile", "infra/main.tf", "AIOps/Connectors/AzureMonitorConnector.cs"]),
            BuildDeployment("dep-004", "git-sync", "1.0.34-hotfix", "prod", DeploymentStatus.RolledBack, -28.8, "release-bot", "13cd45e", ["GitService.cs", "GitPanel.cs"]),
            BuildDeployment("dep-005", "editor-api", "1.0.35-preview", "staging", DeploymentStatus.Running, -0.3, "dennis", "77aa88b", ["Form1.cs", "NotificationFeedService.cs", "AIOps/UI/AIOpsPanel.cs"])
        };

        if (!string.IsNullOrWhiteSpace(service))
            deployments = deployments.Where(d => d.Service.Equals(service, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<Deployment>>(deployments.OrderByDescending(d => d.DeployedAt).Take(count).ToList());
    }

    public Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
    {
        var pipelines = new List<Pipeline>
        {
            new("pipe-001", "pfpad-ci", "master", DeploymentStatus.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-95), TimeSpan.FromMinutes(11), "https://contoso.example/pipelines/pipe-001",
                [new PipelineStage("restore", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(1), null), new PipelineStage("build", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(4), null), new PipelineStage("test", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(6), null)]),
            new("pipe-002", "pfpad-release", "release/1.0.34", DeploymentStatus.Failed, DateTimeOffset.UtcNow.AddHours(-5), TimeSpan.FromMinutes(18), "https://contoso.example/pipelines/pipe-002",
                [new PipelineStage("package", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(7), null), new PipelineStage("deploy-prod", DeploymentStatus.Failed, TimeSpan.FromMinutes(11), "https://contoso.example/pipelines/pipe-002/logs/deploy-prod")]),
            new("pipe-003", "infra-plan", "feature/aiops-core", DeploymentStatus.Running, DateTimeOffset.UtcNow.AddMinutes(-16), null, "https://contoso.example/pipelines/pipe-003",
                [new PipelineStage("terraform-validate", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(2), null), new PipelineStage("terraform-plan", DeploymentStatus.Running, null, null)])
        };

        if (!string.IsNullOrWhiteSpace(branch))
            pipelines = pipelines.Where(p => p.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<Pipeline>>(pipelines.OrderByDescending(p => p.StartedAt).Take(count).ToList());
    }

    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
    {
        var start = DateTimeOffset.UtcNow.AddDays(-30);
        var end = DateTimeOffset.UtcNow;
        var slos = new List<Slo>
        {
            new("slo-001", "Availability", "editor-api", 99.90, 99.96, SloStatus.Healthy, 0.42, start, end),
            new("slo-002", "Request latency", "telemetry-gateway", 99.00, 98.74, SloStatus.AtRisk, 1.82, start, end),
            new("slo-003", "Background job success", "analysis-worker", 99.50, 99.62, SloStatus.Healthy, 0.63, start, end)
        };

        if (!string.IsNullOrWhiteSpace(serviceName))
            slos = slos.Where(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<Slo>>(slos);
    }

    public void Dispose()
    {
    }

    private static Deployment BuildDeployment(string id, string service, string version, string environment, DeploymentStatus status, double hoursAgo, string deployedBy, string commitSha, IReadOnlyList<string> changedFiles)
        => new(id, service, version, environment, status, DateTimeOffset.UtcNow.AddHours(hoursAgo), deployedBy, commitSha, $"https://contoso.example/deployments/{id}", changedFiles);

    private static string PickEnvironment(string serviceName) => Environments[Math.Abs(StableSeed(serviceName, 7)) % Environments.Length];

    private static int StableSeed(string text, int salt)
    {
        unchecked
        {
            int hash = 17 + salt;
            foreach (char c in text)
                hash = (hash * 31) + c;
            return Math.Abs(hash);
        }
    }

    private static double GetMetricValue(string serviceName, string metricName, int offset)
    {
        int seed = StableSeed(serviceName + metricName, offset);
        if (metricName.Contains("latency", StringComparison.OrdinalIgnoreCase))
            return 70 + (seed % 180) + Math.Sin(offset) * 12;
        if (metricName.Contains("error", StringComparison.OrdinalIgnoreCase))
            return Math.Round(0.15 + ((seed % 20) / 10.0), 2);
        if (metricName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
            return 35 + (seed % 50);
        return 100 + (seed % 900);
    }
}

using System.Globalization;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class MockDataConnector : IAIOpsConnector, IPullRequestCapable, IGitHistoryCapable, IAlertCapable, IServiceCatalogCapable, IRunbookCapable
{
    private readonly CorrelationEngine _correlationEngine = new();

    public string Name => "Mock Data";
    public ConnectorStatus Status => ConnectorStatus.Connected;

    public IReadOnlyList<AnomalyEvent> MockAnomalies => BuildAnomalies();
    public IReadOnlyList<InlineAnnotation> MockInlineAnnotations => BuildInlineAnnotations();
    public IReadOnlyList<CorrelationChain> MockCorrelationChains => _correlationEngine.BuildChains(BuildCommits(), BuildPullRequests(), BuildPipelines(), BuildDeployments(), BuildActiveIncidents(), BuildAlerts(), BuildAnomalies());

    public Task<ConnectorState> ConnectAsync(CancellationToken ct = default)
        => Task.FromResult(new ConnectorState(Name, ConnectorStatus.Connected, LastSyncAt: DateTimeOffset.UtcNow));

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<MetricPoint>> GetMetricsAsync(string serviceName, string metricName, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var points = new List<MetricPoint>();
        string unit = metricName.Contains("latency", StringComparison.OrdinalIgnoreCase) ? "ms" : metricName.Contains("error", StringComparison.OrdinalIgnoreCase) ? "%" : "count";
        TimeSpan span = to - from;
        int buckets = Math.Clamp((int)Math.Ceiling(span.TotalHours), 12, 24);
        double stepMinutes = Math.Max(5, span.TotalMinutes / buckets);
        for (int i = 0; i < buckets; i++)
        {
            var timestamp = from.AddMinutes(stepMinutes * i);
            double value = GetMetricValue(serviceName, metricName, i, buckets);
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
        var messages = new Dictionary<string, string[]>
        {
            ["checkout-service"] = ["Cart validation failed for promo bundle", "Retrying checkout dependency call", "Request completed with elevated error rate"],
            ["payment-service"] = ["Payment gateway latency exceeded SLO", "Circuit breaker opened for paymentClient.ts", "Charge authorization completed"],
            ["order-service"] = ["Order orchestration timed out waiting for checkout-service", "Dependency failure from checkout-service", "Order event published to queue"]
        };

        string[] serviceMessages = messages.TryGetValue(serviceName, out var value) ? value : ["Generic service log entry"];
        var logs = new List<LogEntry>();
        for (int i = 0; i < Math.Min(limit, 12); i++)
        {
            var timestamp = to.AddMinutes(-(i * 6));
            if (timestamp < from)
                break;
            logs.Add(new LogEntry(timestamp, i % 4 == 0 ? Severity.High : Severity.Info, serviceMessages[i % serviceMessages.Length], $"trace-{StableSeed(serviceName, i):X8}", $"span-{i:X4}", new Dictionary<string, string> { ["service"] = serviceName, ["env"] = PickEnvironment(serviceName) }));
        }

        return Task.FromResult<IReadOnlyList<LogEntry>>(logs.OrderByDescending(l => l.Timestamp).ToList());
    }

    public Task<IReadOnlyList<TraceSpan>> GetTracesAsync(string traceId, CancellationToken ct = default)
    {
        var start = DateTimeOffset.UtcNow.AddSeconds(-45);
        IReadOnlyList<TraceSpan> spans =
        [
            new(traceId, "root", null, "checkout-service", "POST /checkout", start, TimeSpan.FromMilliseconds(210), true, new Dictionary<string, string> { ["http.method"] = "POST" }),
            new(traceId, "pay01", "root", "payment-service", "ChargeCard", start.AddMilliseconds(42), TimeSpan.FromMilliseconds(165), true, new Dictionary<string, string> { ["peer.service"] = "payment-gateway" }),
            new(traceId, "gw01", "pay01", "payment-gateway", "POST /charge", start.AddMilliseconds(55), TimeSpan.FromMilliseconds(150), true, new Dictionary<string, string> { ["http.status_code"] = "504" })
        ];
        return Task.FromResult(spans);
    }

    public Task<ServiceHealth?> GetServiceHealthAsync(string serviceName, CancellationToken ct = default)
    {
        var health = serviceName switch
        {
            "checkout-service" => new ServiceHealth(serviceName, 4.8, 420, 97.7, DateTimeOffset.UtcNow),
            "payment-service" => new ServiceHealth(serviceName, 2.1, 680, 98.8, DateTimeOffset.UtcNow),
            "order-service" => new ServiceHealth(serviceName, 1.4, 310, 99.1, DateTimeOffset.UtcNow),
            _ => new ServiceHealth(serviceName, 0.2, 120, 99.9, DateTimeOffset.UtcNow)
        };
        return Task.FromResult<ServiceHealth?>(health);
    }

    public Task<IReadOnlyList<Incident>> GetActiveIncidentsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Incident>>(BuildActiveIncidents());

    public Task<IReadOnlyList<Incident>> GetRecentIncidentsAsync(int days = 7, CancellationToken ct = default)
    {
        var incidents = new List<Incident>(BuildActiveIncidents())
        {
            new("inc-100", "Order backlog increased after checkout retries", Severity.Medium, IncidentStatus.Resolved, DateTimeOffset.UtcNow.AddDays(-1).AddHours(-1), DateTimeOffset.UtcNow.AddDays(-1), "order-service", "Transient checkout dependency failure", ["POST /orders"]),
            new("inc-101", "Payment latency spike in production", Severity.High, IncidentStatus.Resolved, DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(1), "payment-service", "Gateway provider latency regression", ["POST /payments"])
        };
        return Task.FromResult<IReadOnlyList<Incident>>(incidents.Where(i => i.StartedAt >= DateTimeOffset.UtcNow.AddDays(-days)).OrderByDescending(i => i.StartedAt).ToList());
    }

    public Task<IReadOnlyList<Deployment>> GetRecentDeploymentsAsync(string? service = null, int count = 20, CancellationToken ct = default)
    {
        var deployments = BuildDeployments();
        if (!string.IsNullOrWhiteSpace(service))
            deployments = deployments.Where(d => d.Service.Equals(service, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Deployment>>(deployments.OrderByDescending(d => d.DeployedAt).Take(count).ToList());
    }

    public Task<IReadOnlyList<Pipeline>> GetPipelineRunsAsync(string? branch = null, int count = 10, CancellationToken ct = default)
    {
        var pipelines = BuildPipelines();
        if (!string.IsNullOrWhiteSpace(branch))
            pipelines = pipelines.Where(p => p.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Pipeline>>(pipelines.OrderByDescending(p => p.StartedAt).Take(count).ToList());
    }

    public Task<IReadOnlyList<Slo>> GetSlosAsync(string? serviceName = null, CancellationToken ct = default)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow.AddDays(-30);
        DateTimeOffset end = DateTimeOffset.UtcNow;
        var slos = new List<Slo>
        {
            new("slo-001", "Checkout availability", "checkout-service", 99.90, 98.94, SloStatus.AtRisk, 1.72, start, end),
            new("slo-002", "Payment latency", "payment-service", 99.00, 98.61, SloStatus.AtRisk, 2.10, start, end),
            new("slo-003", "Order processing success", "order-service", 99.50, 99.32, SloStatus.Healthy, 0.63, start, end)
        };
        if (!string.IsNullOrWhiteSpace(serviceName))
            slos = slos.Where(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Slo>>(slos);
    }

    public Task<IReadOnlyList<GitCommit>> GetRecentCommitsAsync(string? branch = null, int count = 30, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GitCommit>>(BuildCommits().Take(count).ToList());

    public Task<IReadOnlyList<GitPullRequest>> GetRecentPullRequestsAsync(string? repository = null, int count = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GitPullRequest>>(BuildPullRequests().Take(count).ToList());

    public Task<IReadOnlyList<Alert>> GetRecentAlertsAsync(string? serviceName = null, int days = 1, CancellationToken ct = default)
    {
        var alerts = BuildAlerts().Where(a => a.FiredAt >= DateTimeOffset.UtcNow.AddDays(-days)).ToList();
        if (!string.IsNullOrWhiteSpace(serviceName))
            alerts = alerts.Where(a => a.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Alert>>(alerts);
    }

    public Task<ServiceDependencyGraph> GetServiceDependencyGraphAsync(CancellationToken ct = default)
    {
        var services = BuildServiceCatalog();
        IReadOnlyList<ServiceDependency> dependencies =
        [
            new("checkout-service", "payment-service", "http", true, "https"),
            new("payment-service", "payment-gateway", "http", true, "https"),
            new("order-service", "checkout-service", "queue", true, "amqp")
        ];
        return Task.FromResult(new ServiceDependencyGraph(services, dependencies, DateTimeOffset.UtcNow));
    }

    public Task<IReadOnlyList<ServiceEntity>> GetServiceCatalogAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ServiceEntity>>(BuildServiceCatalog());

    public Task<IReadOnlyList<Runbook>> GetRunbooksAsync(string? serviceName = null, CancellationToken ct = default)
    {
        var runbooks = BuildRunbooks();
        if (!string.IsNullOrWhiteSpace(serviceName))
            runbooks = runbooks.Where(r => r.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<Runbook>>(runbooks);
    }

    public void Dispose()
    {
    }

    private static List<Incident> BuildActiveIncidents() =>
    [
        new Incident("inc-001", "Checkout errors increased after cart validation change", Severity.High, IncidentStatus.Triggered, DateTimeOffset.UtcNow.AddMinutes(-28), null, "checkout-service", "Recent validation change may be rejecting valid carts", ["POST /checkout"], "https://contoso.example/incidents/inc-001"),
        new Incident("inc-002", "Payment latency elevated in production", Severity.High, IncidentStatus.Acknowledged, DateTimeOffset.UtcNow.AddMinutes(-44), null, "payment-service", "Payment gateway latency is propagating to payment-service", ["POST /payments", "POST /charge"], "https://contoso.example/incidents/inc-002")
    ];

    private static List<Deployment> BuildDeployments() =>
    [
        BuildDeployment("dep-001", "checkout-service", "2.4.18", "prod", DeploymentStatus.Succeeded, -0.9, "release-bot", "a1b2c3d", ["src/checkout/cartValidator.ts", "src/checkout/checkoutController.ts"]),
        BuildDeployment("dep-002", "payment-service", "3.9.4", "prod", DeploymentStatus.Succeeded, -1.5, "release-bot", "b2c3d4e", ["src/payment/paymentClient.ts", "src/payment/gatewayConfig.json"]),
        BuildDeployment("dep-003", "order-service", "1.7.2", "staging", DeploymentStatus.Succeeded, -5.0, "dennis", "c3d4e5f", ["src/order/orderWorkflow.ts"]),
        BuildDeployment("dep-004", "payment-service", "3.9.3", "prod", DeploymentStatus.RolledBack, -26.0, "release-bot", "d4e5f6a", ["src/payment/retryPolicy.ts"])
    ];

    private static List<Pipeline> BuildPipelines() =>
    [
        new("pipe-001", "checkout-service-ci", "feature/cart-validation", DeploymentStatus.Succeeded, DateTimeOffset.UtcNow.AddHours(-3), TimeSpan.FromMinutes(12), "https://contoso.example/pipelines/pipe-001", [new PipelineStage("build", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(4), null), new PipelineStage("test", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(8), null)]),
        new("pipe-002", "payment-service-release", "main", DeploymentStatus.Failed, DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(18), "https://contoso.example/pipelines/pipe-002", [new PipelineStage("package", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(7), null), new PipelineStage("deploy", DeploymentStatus.Failed, TimeSpan.FromMinutes(11), "https://contoso.example/pipelines/pipe-002/logs")]),
        new("pipe-003", "order-service-ci", "main", DeploymentStatus.Running, DateTimeOffset.UtcNow.AddMinutes(-35), null, "https://contoso.example/pipelines/pipe-003", [new PipelineStage("build", DeploymentStatus.Succeeded, TimeSpan.FromMinutes(6), null), new PipelineStage("integration", DeploymentStatus.Running, null, null)])
    ];

    private static List<GitCommit> BuildCommits()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new GitCommit("a1b2c3d4e5f6", "a1b2c3d", "Tighten cart validation rules", "Dennis", "dennis@example.local", now.AddMinutes(-52), ["src/checkout/cartValidator.ts", "src/checkout/cartRules.json"], "101", "https://contoso.example/commit/a1b2c3d4e5f6"),
            new GitCommit("b2c3d4e5f6a7", "b2c3d4e", "Add retry handling to payment gateway client", "Dennis", "dennis@example.local", now.AddMinutes(-88), ["src/payment/paymentClient.ts", "src/payment/gatewayConfig.json"], "102", "https://contoso.example/commit/b2c3d4e5f6a7"),
            new GitCommit("c3d4e5f6a7b8", "c3d4e5f", "Refactor order workflow orchestration", "Copilot", "copilot@example.local", now.AddHours(-5), ["src/order/orderWorkflow.ts"], "103", "https://contoso.example/commit/c3d4e5f6a7b8"),
            new GitCommit("d4e5f6a7b8c9", "d4e5f6a", "Update payment feature flags", "Dennis", "dennis@example.local", now.AddDays(-1), ["src/payment/featureFlags.json"], null, "https://contoso.example/commit/d4e5f6a7b8c9"),
            new GitCommit("e5f6a7b8c9d0", "e5f6a7b", "Add order-service observability tags", "Copilot", "copilot@example.local", now.AddDays(-2), ["src/order/orderTelemetry.cs"], null, "https://contoso.example/commit/e5f6a7b8c9d0")
        ];
    }

    private static List<GitPullRequest> BuildPullRequests()
    {
        var commits = BuildCommits();
        return
        [
            new GitPullRequest("101", "Checkout: harden cart validation", "Dennis", "feature/cart-validation", "main", PullRequestStatus.Open, DateTimeOffset.UtcNow.AddHours(-3), null, "https://contoso.example/pr/101", "Tightens cart payload checks before submit.", [commits[0]], commits[0].ChangedFiles, "https://contoso.example/pr/101/review"),
            new GitPullRequest("102", "Payment: retry gateway timeouts", "Dennis", "feature/payment-retry", "main", PullRequestStatus.Merged, DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow.AddHours(-2), "https://contoso.example/pr/102", "Adds retries and timeout fallback for payment gateway.", [commits[1]], commits[1].ChangedFiles, "https://contoso.example/pr/102/review"),
            new GitPullRequest("103", "Order: workflow cleanup", "Copilot", "feature/order-refactor", "main", PullRequestStatus.Closed, DateTimeOffset.UtcNow.AddDays(-1), null, "https://contoso.example/pr/103", "Refactors order orchestration and telemetry annotations.", [commits[2]], commits[2].ChangedFiles, "https://contoso.example/pr/103/review")
        ];
    }

    private static List<Alert> BuildAlerts() =>
    [
        new Alert("alert-001", "ErrorRateHigh", "checkout-service", Severity.High, "fired", DateTimeOffset.UtcNow.AddMinutes(-25), null, "Checkout error rate exceeded 4%.", "https://contoso.example/alerts/alert-001"),
        new Alert("alert-002", "LatencyHigh", "payment-service", Severity.High, "fired", DateTimeOffset.UtcNow.AddMinutes(-40), null, "Payment p99 latency exceeded 650ms.", "https://contoso.example/alerts/alert-002"),
        new Alert("alert-003", "CheckoutSloBurn", "checkout-service", Severity.Medium, "resolved", DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow.AddHours(-5), "Checkout error budget burn rate recovered.", "https://contoso.example/alerts/alert-003"),
        new Alert("alert-004", "DependencyRestored", "order-service", Severity.Low, "resolved", DateTimeOffset.UtcNow.AddHours(-9), DateTimeOffset.UtcNow.AddHours(-8), "Order-service dependency health restored.", "https://contoso.example/alerts/alert-004")
    ];

    private static List<AnomalyEvent> BuildAnomalies() =>
    [
        new("an-001", "checkout-service", "error_rate", 0.8, 4.9, 512.5, AnomalyType.ErrorRateSpike, DateTimeOffset.UtcNow.AddMinutes(-24), "Checkout error rate spiked after recent validation changes.", [new Evidence("metric", "Baseline error rate 0.8%"), new Evidence("metric", "Observed error rate 4.9%")], Severity.High),
        new("an-002", "payment-service", "latency_ms", 210, 690, 228.6, AnomalyType.LatencyIncrease, DateTimeOffset.UtcNow.AddMinutes(-39), "Payment latency increased sharply during gateway retries.", [new Evidence("metric", "Baseline latency 210ms"), new Evidence("metric", "Observed latency 690ms")], Severity.High),
        new("an-003", "order-service", "dependency_checkout", 1, 7, 600, AnomalyType.DependencyFailure, DateTimeOffset.UtcNow.AddHours(-1), "Order-service dependency failures increased against checkout-service.", [new Evidence("metric", "Checkout dependency failures rose from 1 to 7")], Severity.Medium)
    ];

    private static List<InlineAnnotation> BuildInlineAnnotations() =>
    [
        new("src/checkout/cartValidator.ts", 42, "validateCart", InlineAnnotationType.ErrorProne, "Recent error spike linked to cart validation changes.", [new Evidence("anomaly", "Checkout error rate spike detected")], Severity.High, "Open incident", "https://contoso.example/incidents/inc-001"),
        new("src/payment/paymentClient.ts", 88, "chargeAsync", InlineAnnotationType.LatencyHotspot, "Payment latency increased after retry logic changes.", [new Evidence("alert", "LatencyHigh fired for payment-service")], Severity.High, "Review alert", "https://contoso.example/alerts/alert-002"),
        new("src/order/orderWorkflow.ts", 17, "ProcessOrder", InlineAnnotationType.IncidentLinked, "Dependency failures from checkout-service impacted order flow.", [new Evidence("anomaly", "DependencyFailure on order-service")], Severity.Medium, "Trace dependency", null)
    ];

    private static List<ServiceEntity> BuildServiceCatalog() =>
    [
        new ServiceEntity("svc-001", "checkout-service", "Commerce", "https://github.com/contoso/checkout-service", "prod", ["team-checkout@contoso.example"], ["checkout", "commerce"], "https://contoso.example/docs/checkout"),
        new ServiceEntity("svc-002", "payment-service", "Payments", "https://github.com/contoso/payment-service", "prod", ["team-payments@contoso.example"], ["payments", "critical-path"], "https://contoso.example/docs/payment"),
        new ServiceEntity("svc-003", "payment-gateway", "Payments", null, "prod", ["team-payments@contoso.example"], ["external", "gateway"]),
        new ServiceEntity("svc-004", "order-service", "Commerce", "https://github.com/contoso/order-service", "prod", ["team-orders@contoso.example"], ["orders", "queue"])
    ];

    private static List<Runbook> BuildRunbooks() =>
    [
        new Runbook("rb-001", "Rollback Deployment", "Safely rollback the most recent production deployment.", "checkout-service", [new RunbookStep(1, "Inspect current deployment rollout", "kubectl describe deployment checkout-service", false, "Read-only verification", TimeSpan.FromSeconds(30)), new RunbookStep(2, "Review git status before rollback", "git status", false, "Confirm repository state", TimeSpan.FromSeconds(10)), new RunbookStep(3, "Obtain production approval", null, true, "Rollback changes production behavior")], "https://contoso.example/runbooks/rollback", Severity.High, false),
        new Runbook("rb-002", "Scale Service", "Review scale indicators before adjusting service capacity.", "payment-service", [new RunbookStep(1, "Check pod counts", "kubectl get pods", false, "Read-only inventory", TimeSpan.FromSeconds(20)), new RunbookStep(2, "Inspect resource pressure", "kubectl top pods", false, "Read-only metrics", TimeSpan.FromSeconds(20)), new RunbookStep(3, "Obtain approval for scale change", null, true, "Scaling production requires approval")], "https://contoso.example/runbooks/scale", Severity.Medium, false),
        new Runbook("rb-003", "Investigate Logs", "Gather deployment and runtime evidence for the affected service.", "order-service", [new RunbookStep(1, "Inspect recent deployment differences", "git diff", false, "Read-only source comparison", TimeSpan.FromSeconds(15)), new RunbookStep(2, "Describe workload", "kubectl describe deployment order-service", false, "Read-only cluster metadata", TimeSpan.FromSeconds(20)), new RunbookStep(3, "Read recent pod logs", "kubectl logs order-service", false, "Review runtime failures", TimeSpan.FromSeconds(20))], "https://contoso.example/runbooks/logs", Severity.Info, true)
    ];

    private static Deployment BuildDeployment(string id, string service, string version, string environment, DeploymentStatus status, double hoursAgo, string deployedBy, string commitSha, IReadOnlyList<string> changedFiles)
        => new(id, service, version, environment, status, DateTimeOffset.UtcNow.AddHours(hoursAgo), deployedBy, commitSha, $"https://contoso.example/deployments/{id}", changedFiles);

    private static string PickEnvironment(string serviceName)
        => serviceName.Contains("prod", StringComparison.OrdinalIgnoreCase) ? "prod" : "prod";

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

    private static double GetMetricValue(string serviceName, string metricName, int offset, int buckets)
    {
        int seed = StableSeed(serviceName + metricName, offset);
        bool hotPath = offset >= buckets - 3;
        if (metricName.Contains("latency", StringComparison.OrdinalIgnoreCase))
            return (serviceName.Contains("payment", StringComparison.OrdinalIgnoreCase) && hotPath ? 620 : 180) + (seed % 40);
        if (metricName.Contains("error", StringComparison.OrdinalIgnoreCase))
            return serviceName.Contains("checkout", StringComparison.OrdinalIgnoreCase) && hotPath ? 4.6 + (seed % 10) / 10.0 : Math.Round(0.4 + ((seed % 5) / 10.0), 2);
        if (metricName.Contains("cpu", StringComparison.OrdinalIgnoreCase))
            return 35 + (seed % 50);
        return 100 + (seed % 900);
    }
}

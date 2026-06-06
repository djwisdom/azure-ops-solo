namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class CorrelationEngine
{
    public IReadOnlyList<CorrelationChain> BuildChains(
        IReadOnlyList<GitCommit> commits,
        IReadOnlyList<GitPullRequest> prs,
        IReadOnlyList<Pipeline> builds,
        IReadOnlyList<Deployment> deployments,
        IReadOnlyList<Incident> incidents,
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<AnomalyEvent> anomalies,
        TimeSpan? correlationWindow = null)
    {
        TimeSpan window = correlationWindow ?? TimeSpan.FromHours(4);
        var groupedEvents = new Dictionary<string, List<CorrelationEvent>>(StringComparer.OrdinalIgnoreCase);

        AddEvents(groupedEvents, commits.Select(commit => (InferService(commit.ChangedFiles, commit.Message), new CorrelationEvent("commit", commit.Sha, $"Commit {commit.ShortSha}: {commit.Message}", commit.Timestamp, commit.Url))));
        AddEvents(groupedEvents, prs.Select(pr => (InferService(pr.ChangedFiles, $"{pr.Title} {pr.Description}"), new CorrelationEvent("pullrequest", pr.Id, pr.Title, pr.CreatedAt, pr.Url))));
        AddEvents(groupedEvents, builds.Select(build => (InferService([], $"{build.Name} {build.Branch}"), new CorrelationEvent("build", build.Id, $"Pipeline {build.Name} on {build.Branch} is {build.Status}", build.StartedAt, build.Url, ToSeverity(build.Status)))));
        AddEvents(groupedEvents, deployments.Select(deployment => (deployment.Service, new CorrelationEvent("deployment", deployment.Id, $"Deployment {deployment.Version} to {deployment.Environment} is {deployment.Status}", deployment.DeployedAt, deployment.PipelineUrl, ToSeverity(deployment.Status)))));
        AddEvents(groupedEvents, incidents.Select(incident => (incident.AffectedService, new CorrelationEvent("incident", incident.Id, incident.Title, incident.StartedAt, incident.PostMortemUrl, incident.Severity))));
        AddEvents(groupedEvents, alerts.Select(alert => (alert.ServiceName, new CorrelationEvent("alert", alert.Id, alert.RuleName, alert.FiredAt, alert.Url, alert.Severity))));
        AddEvents(groupedEvents, anomalies.Select(anomaly => (anomaly.ServiceName, new CorrelationEvent("anomaly", anomaly.Id, anomaly.Description, anomaly.DetectedAt, null, anomaly.Severity))));

        var chains = new List<CorrelationChain>();
        foreach (var group in groupedEvents)
        {
            var sorted = group.Value.OrderBy(e => e.Timestamp).ToList();
            if (sorted.Count == 0)
                continue;

            var current = new List<CorrelationEvent> { sorted[0] };
            for (int i = 1; i < sorted.Count; i++)
            {
                CorrelationEvent next = sorted[i];
                if (next.Timestamp - current[^1].Timestamp <= window)
                {
                    current.Add(next);
                    continue;
                }

                chains.Add(CreateChain(group.Key, current));
                current = [next];
            }

            chains.Add(CreateChain(group.Key, current));
        }

        return chains
            .OrderByDescending(chain => chain.Events.Max(e => e.Timestamp))
            .ToList();
    }

    private static void AddEvents(Dictionary<string, List<CorrelationEvent>> grouped, IEnumerable<(string? service, CorrelationEvent evt)> items)
    {
        foreach (var (service, evt) in items)
        {
            if (string.IsNullOrWhiteSpace(service))
                continue;
            if (!grouped.TryGetValue(service, out List<CorrelationEvent>? events))
            {
                events = [];
                grouped[service] = events;
            }
            events.Add(evt);
        }
    }

    private static CorrelationChain CreateChain(string serviceName, List<CorrelationEvent> events)
    {
        string first = events[0].EntityType;
        string last = events[^1].EntityType;
        int incidentCount = events.Count(e => e.EntityType == "incident");
        int alertCount = events.Count(e => e.EntityType == "alert");
        string summary = $"{serviceName} had {events.Count} correlated events from {events[0].Timestamp:g} to {events[^1].Timestamp:g}, beginning with {first} and ending with {last}.";
        if (incidentCount > 0 || alertCount > 0)
            summary += $" Included {incidentCount} incident(s) and {alertCount} alert(s).";
        return new CorrelationChain(serviceName, [.. events], summary, events[0].Timestamp, events[^1].Timestamp);
    }

    private static string? InferService(IReadOnlyList<string> changedFiles, string? text)
    {
        foreach (string file in changedFiles)
        {
            string? fromFile = InferServiceFromText(file);
            if (!string.IsNullOrWhiteSpace(fromFile))
                return fromFile;
        }

        return InferServiceFromText(text);
    }

    private static string? InferServiceFromText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string text = value.Replace('\\', '/').ToLowerInvariant();
        if (text.Contains("checkout-service") || text.Contains("checkout") || text.Contains("cart"))
            return "checkout-service";
        if (text.Contains("payment-gateway"))
            return "payment-gateway";
        if (text.Contains("payment-service") || text.Contains("payment"))
            return "payment-service";
        if (text.Contains("order-service") || text.Contains("order"))
            return "order-service";

        string[] parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(part => part.EndsWith("-service", StringComparison.OrdinalIgnoreCase));
    }

    private static Severity? ToSeverity(DeploymentStatus status)
        => status switch
        {
            DeploymentStatus.Failed => Severity.High,
            DeploymentStatus.RolledBack => Severity.Medium,
            DeploymentStatus.Running => Severity.Info,
            _ => null
        };
}

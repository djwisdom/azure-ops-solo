namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class RootCauseAnalyzer
{
    public Task<RootCauseAnalysis> AnalyzeAsync(
        Incident incident,
        IReadOnlyList<Deployment> recentDeployments,
        IReadOnlyList<AnomalyEvent> anomalies,
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<GitCommit> recentCommits,
        IReadOnlyList<GitPullRequest> recentPrs,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var hypotheses = new List<RootCauseHypothesis>();

        Deployment? relatedDeployment = recentDeployments
            .Where(d => MatchesService(d.Service, incident.AffectedService) && d.DeployedAt <= incident.StartedAt)
            .OrderByDescending(d => d.DeployedAt)
            .FirstOrDefault(d => incident.StartedAt - d.DeployedAt <= TimeSpan.FromMinutes(60));
        if (relatedDeployment is not null)
        {
            hypotheses.Add(new RootCauseHypothesis(
                0,
                relatedDeployment.Status == DeploymentStatus.Failed ? 0.88 : 0.78,
                $"Deployment regression likely after deployment {relatedDeployment.Id} for {relatedDeployment.Service}.",
                [
                    new Evidence("deployment", $"Deployment {relatedDeployment.Id} occurred {(incident.StartedAt - relatedDeployment.DeployedAt).TotalMinutes:F0} minutes before the incident.", relatedDeployment.PipelineUrl, relatedDeployment.DeployedAt),
                    new Evidence("changes", $"Changed files: {string.Join(", ", relatedDeployment.ChangedFiles.Take(5))}")
                ],
                relatedDeployment.ChangedFiles,
                relatedDeployment.Id,
                relatedDeployment.CommitSha,
                ["Review the deployment diff.", "Validate rollback behavior in staging.", "Check health and latency immediately after deploy."],
                [CreateSuggestion("Rollback or halt rollout", $"Review deployment {relatedDeployment.Id} and consider rollback if impact continues.", relatedDeployment.ChangedFiles.FirstOrDefault() ?? string.Empty, RemediationRisk.ReviewRequired)]));
        }

        GitCommit? riskyCommit = recentCommits
            .Where(commit => commit.Timestamp <= incident.StartedAt && incident.StartedAt - commit.Timestamp <= TimeSpan.FromHours(2))
            .FirstOrDefault(commit => commit.ChangedFiles.Any(IsHighRiskFile));
        if (riskyCommit is not null)
        {
            GitPullRequest? relatedPr = recentPrs.FirstOrDefault(pr => pr.Commits.Any(c => c.Sha == riskyCommit.Sha) || pr.ChangedFiles.Intersect(riskyCommit.ChangedFiles, StringComparer.OrdinalIgnoreCase).Any());
            var evidence = new List<Evidence>
            {
                new Evidence("commit", $"Commit {riskyCommit.ShortSha} by {riskyCommit.Author} touched high-risk files.", riskyCommit.Url, riskyCommit.Timestamp),
                new Evidence("files", string.Join(", ", riskyCommit.ChangedFiles.Take(5)))
            };
            if (relatedPr is not null)
                evidence.Add(new Evidence("pullrequest", $"Related PR: {relatedPr.Title}", relatedPr.Url, relatedPr.CreatedAt));

            hypotheses.Add(new RootCauseHypothesis(
                0,
                0.60,
                $"Recent high-risk commit {riskyCommit.ShortSha} may have introduced a configuration, migration, or security regression.",
                evidence,
                riskyCommit.ChangedFiles,
                null,
                riskyCommit.Sha,
                ["Inspect the commit diff.", "Verify config and migration compatibility.", "Check feature flags or rollout conditions."] ,
                [CreateSuggestion("Review recent commit", $"Review commit {riskyCommit.ShortSha} for risky file changes.", riskyCommit.ChangedFiles.FirstOrDefault() ?? string.Empty, RemediationRisk.ReviewRequired)]));
        }

        AnomalyEvent? dependencyFailure = anomalies
            .Where(a => a.AnomalyType == AnomalyType.DependencyFailure && MatchesService(a.ServiceName, incident.AffectedService))
            .OrderBy(a => Math.Abs((a.DetectedAt - incident.StartedAt).TotalMinutes))
            .FirstOrDefault(a => Math.Abs((a.DetectedAt - incident.StartedAt).TotalMinutes) <= 60);
        if (dependencyFailure is not null)
        {
            hypotheses.Add(new RootCauseHypothesis(
                0,
                0.70,
                $"Dependency failure detected for {dependencyFailure.ServiceName} near incident onset.",
                [new Evidence("anomaly", dependencyFailure.Description, Timestamp: dependencyFailure.DetectedAt), .. dependencyFailure.Evidence],
                [],
                null,
                null,
                ["Inspect downstream dependencies and recent timeouts.", "Compare dependency SLIs before and after the incident."],
                [CreateSuggestion("Check dependency health", "Inspect dependent services and connection pools.", string.Empty, RemediationRisk.Safe)]));
        }

        Alert? sloAlert = alerts
            .Where(a => MatchesService(a.ServiceName, incident.AffectedService))
            .FirstOrDefault(a => a.RuleName.Contains("slo", StringComparison.OrdinalIgnoreCase) || a.RuleName.Contains("burn", StringComparison.OrdinalIgnoreCase) || (a.Description?.Contains("slo", StringComparison.OrdinalIgnoreCase) ?? false));
        if (sloAlert is not null)
        {
            hypotheses.Add(new RootCauseHypothesis(
                0,
                0.55,
                $"SLO burn rate or error budget alert ({sloAlert.RuleName}) aligns with the incident timeline.",
                [new Evidence("alert", $"Alert {sloAlert.RuleName} fired at {sloAlert.FiredAt:g}.", sloAlert.Url, sloAlert.FiredAt)],
                [],
                null,
                null,
                ["Check error-budget consumption.", "Review request latency and availability trends."],
                [CreateSuggestion("Stabilize SLO breach", "Reduce load or pause rollout until SLO burn normalizes.", string.Empty, RemediationRisk.ReviewRequired)]));
        }

        var correlatedAlerts = alerts
            .Where(a => MatchesService(a.ServiceName, incident.AffectedService))
            .Where(a => Math.Abs((a.FiredAt - incident.StartedAt).TotalMinutes) <= 30)
            .OrderBy(a => a.FiredAt)
            .ToList();
        if (correlatedAlerts.Count >= 2)
        {
            hypotheses.Add(new RootCauseHypothesis(
                0,
                0.50,
                $"Multiple alerts ({correlatedAlerts.Count}) fired around the incident, suggesting a wider correlated failure.",
                correlatedAlerts.Select(a => new Evidence("alert", $"{a.RuleName} fired for {a.ServiceName}", a.Url, a.FiredAt)).ToList(),
                [],
                null,
                null,
                ["Review alert fan-out.", "Check shared infrastructure or dependency changes."] ,
                [CreateSuggestion("Investigate common cause", "Look for shared dependencies, ingress, or config changes across the affected services.", string.Empty, RemediationRisk.Safe)]));
        }

        var ranked = hypotheses
            .OrderByDescending(h => h.ConfidenceScore)
            .Take(5)
            .Select((hypothesis, index) => hypothesis with { Rank = index + 1 })
            .ToList();

        bool hasData = ranked.Count > 0;
        var analysis = new RootCauseAnalysis(
            incident.Id,
            ranked,
            DateTimeOffset.UtcNow,
            hasData,
            hasData ? null : "Insufficient correlated telemetry, deployment, or code history was available to form a confident hypothesis.");
        return Task.FromResult(analysis);
    }

    private static bool MatchesService(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsHighRiskFile(string path)
    {
        string normalized = path.ToLowerInvariant();
        return normalized.Contains("security")
            || normalized.Contains("secret")
            || normalized.Contains("config")
            || normalized.Contains("migration")
            || normalized.Contains("terraform")
            || normalized.Contains(".sql");
    }

    private static RemediationSuggestion CreateSuggestion(string title, string description, string filePath, RemediationRisk risk)
        => new()
        {
            Title = title,
            Description = description,
            Risk = risk,
            FilePath = filePath,
            ConfidenceScore = risk == RemediationRisk.Safe ? 0.75 : 0.65,
            LineNumber = 0,
            Evidence = []
        };
}

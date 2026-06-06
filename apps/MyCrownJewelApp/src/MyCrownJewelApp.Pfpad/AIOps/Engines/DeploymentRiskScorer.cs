namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class DeploymentRiskScorer
{
    public Task<DeploymentRiskReport> ScoreAsync(IReadOnlyList<string> changedFiles, string? gitDiff, IReadOnlyList<Incident> activeIncidents, IReadOnlyList<Slo> slos, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var normalizedFiles = changedFiles.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var factors = new List<DeploymentRiskFactor>();
        var recommendations = new List<string>();
        int score = 0;

        if (normalizedFiles.Count > 50)
        {
            score += 30;
            factors.Add(new DeploymentRiskFactor(
                "ChangedFileCount",
                RiskLevel.High,
                $"Large deployment footprint detected across {normalizedFiles.Count} changed files.",
                [new Evidence("Git diff analysis", $"Changed files count = {normalizedFiles.Count}")])) ;
            recommendations.Add("Split the rollout into smaller batches or deploy behind a feature flag to reduce blast radius.");
        }

        var criticalFiles = normalizedFiles.Where(IsCriticalFile).ToList();
        if (criticalFiles.Count > 0)
        {
            score += 20;
            factors.Add(new DeploymentRiskFactor(
                "CriticalFileChanged",
                RiskLevel.High,
                "Critical deployment infrastructure files were modified.",
                criticalFiles.Select(file => new Evidence("Git diff analysis", $"Critical file changed: {file}")).ToList()));
            recommendations.Add("Require a second reviewer for infrastructure, config, or container changes before production deployment.");
        }

        var criticalIncident = activeIncidents.FirstOrDefault(i => i.Status != IncidentStatus.Resolved && i.Severity == Severity.Critical);
        if (criticalIncident is not null)
        {
            score += 40;
            factors.Add(new DeploymentRiskFactor(
                "ActiveIncidents",
                RiskLevel.Critical,
                $"Critical incident '{criticalIncident.Title}' is still active.",
                [new Evidence("Active incidents", criticalIncident.Title, criticalIncident.PostMortemUrl, criticalIncident.StartedAt)]));
            recommendations.Add("Pause production rollout until the active critical incident is mitigated or a rollback plan is approved.");
        }
        else
        {
            var highIncident = activeIncidents.FirstOrDefault(i => i.Status != IncidentStatus.Resolved && i.Severity == Severity.High);
            if (highIncident is not null)
            {
                score += 25;
                factors.Add(new DeploymentRiskFactor(
                    "ActiveIncidents",
                    RiskLevel.High,
                    $"High-severity incident '{highIncident.Title}' is active.",
                    [new Evidence("Active incidents", highIncident.Title, highIncident.PostMortemUrl, highIncident.StartedAt)]));
                recommendations.Add("Validate service health and rollback readiness before deploying while a high-severity incident is active.");
            }
            else if (activeIncidents.Any(i => i.Status != IncidentStatus.Resolved))
            {
                score += 10;
                factors.Add(new DeploymentRiskFactor(
                    "ActiveIncidents",
                    RiskLevel.Medium,
                    "Active incidents are present in the environment.",
                    activeIncidents.Where(i => i.Status != IncidentStatus.Resolved).Select(i => new Evidence("Active incidents", i.Title, i.PostMortemUrl, i.StartedAt)).ToList()));
                recommendations.Add("Coordinate deployment timing with the incident responder to avoid overlapping changes and diagnosis.");
            }
        }

        var breachedSlo = slos.Where(s => s.BurnRate > 2.0).ToList();
        if (breachedSlo.Count > 0)
        {
            score += 25;
            factors.Add(new DeploymentRiskFactor(
                "SLOBurnRate",
                RiskLevel.High,
                "One or more service level objectives are burning budget faster than 2x.",
                breachedSlo.Select(s => new Evidence("SLO data", $"{s.ServiceName}/{s.Name} burn rate = {s.BurnRate:F2}x", null, s.WindowEnd)).ToList()));
            recommendations.Add("Hold deployment until the elevated burn rate stabilizes or use canary rollout with live SLO monitoring.");
        }
        else
        {
            var elevatedSlo = slos.Where(s => s.BurnRate > 1.5).ToList();
            if (elevatedSlo.Count > 0)
            {
                score += 15;
                factors.Add(new DeploymentRiskFactor(
                    "SLOBurnRate",
                    RiskLevel.Medium,
                    "SLO burn rate is elevated above 1.5x.",
                    elevatedSlo.Select(s => new Evidence("SLO data", $"{s.ServiceName}/{s.Name} burn rate = {s.BurnRate:F2}x", null, s.WindowEnd)).ToList()));
                recommendations.Add("Increase monitoring during rollout and confirm alert thresholds before shipping.");
            }
        }

        var securityFiles = normalizedFiles.Where(IsSecurityRelatedFile).ToList();
        if (securityFiles.Count > 0)
        {
            score += 15;
            factors.Add(new DeploymentRiskFactor(
                "SecurityFiles",
                RiskLevel.Medium,
                "Authentication or security-sensitive files were modified.",
                securityFiles.Select(file => new Evidence("Git diff analysis", $"Security-sensitive file changed: {file}")).ToList()));
            recommendations.Add("Run focused auth/security regression tests and verify secrets/config are unchanged in target environment.");
        }

        bool hasTests = normalizedFiles.Any(IsTestFile) || (!string.IsNullOrWhiteSpace(gitDiff) && gitDiff.Contains("test", StringComparison.OrdinalIgnoreCase));
        if (!hasTests)
        {
            score += 10;
            factors.Add(new DeploymentRiskFactor(
                "TestCoverage",
                RiskLevel.Medium,
                "No test files were included in the change set.",
                [new Evidence("Git diff analysis", "No files matched common test naming conventions.")]));
            recommendations.Add("Run targeted automated tests or add/refresh coverage for the changed code paths before deployment.");
        }

        var migrationFiles = normalizedFiles.Where(IsDatabaseMigration).ToList();
        if (migrationFiles.Count > 0)
        {
            score += 25;
            factors.Add(new DeploymentRiskFactor(
                "DatabaseMigration",
                RiskLevel.High,
                "Database migration files were changed.",
                migrationFiles.Select(file => new Evidence("Git diff analysis", $"Database migration detected: {file}")).ToList()));
            recommendations.Add("Validate backward compatibility, rehearse rollback, and back up the database before applying schema changes.");
        }

        score = Math.Min(100, score);
        RiskLevel overallRisk = score switch
        {
            <= 25 => RiskLevel.Low,
            <= 50 => RiskLevel.Medium,
            <= 75 => RiskLevel.High,
            _ => RiskLevel.Critical
        };

        if (factors.Count == 0)
            recommendations.Add("Proceed with the deployment using standard smoke tests and normal monitoring.");

        var report = new DeploymentRiskReport
        {
            OverallRisk = overallRisk,
            Score = score,
            Factors = factors,
            Recommendations = recommendations.Distinct(StringComparer.Ordinal).ToList(),
            ChangedFiles = normalizedFiles,
            ConfidenceScore = factors.Count > 0 ? 0.82 : 0.65,
            Evidence = factors.SelectMany(f => f.Evidence).Take(10).ToList()
        };

        return Task.FromResult(report);
    }

    private static bool IsCriticalFile(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        return normalized.Contains("migration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("config", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("terraform", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".tf", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecurityRelatedFile(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        return normalized.Contains("auth", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("security", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("identity", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("login", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("token", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestFile(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        return normalized.Contains("/test", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("spec", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDatabaseMigration(string filePath)
    {
        string normalized = filePath.Replace('\\', '/');
        return normalized.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("migration", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("migrations/", StringComparison.OrdinalIgnoreCase);
    }
}

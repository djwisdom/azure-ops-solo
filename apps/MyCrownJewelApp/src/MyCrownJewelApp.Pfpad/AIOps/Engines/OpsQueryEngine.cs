namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class OpsQueryEngine
{
    private readonly List<OpsQueryMessage> _conversationHistory = new();
    public IReadOnlyList<OpsQueryMessage> ConversationHistory => _conversationHistory;

    public OpsQueryResponse Query(
        string question,
        OperationalContext context,
        IReadOnlyList<SecurityFinding>? securityFindings = null,
        DeploymentRiskReport? riskReport = null,
        IReadOnlyList<RootCauseHypothesis>? rcaHypotheses = null,
        IReadOnlyList<CorrelationChain>? correlationChains = null)
    {
        string intent = ClassifyIntent(question);
        var evidence = new List<Evidence>();
        var recommendations = new List<RemediationSuggestion>();
        string answer;
        double confidence;
        bool isGuess = false;
        string? disclaimer = null;

        switch (intent)
        {
            case "incident_investigation":
                answer = BuildIncidentAnswer(context, rcaHypotheses, correlationChains, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
            case "risk_analysis":
                answer = BuildRiskAnswer(context, riskReport, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
            case "log_query":
                answer = BuildLogAnswer(context, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
            case "observability":
                answer = BuildObservabilityAnswer(context, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
            case "security":
                answer = BuildSecurityAnswer(context, securityFindings, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
            default:
                answer = BuildGeneralAnswer(context, evidence, recommendations, out confidence, out isGuess, out disclaimer);
                break;
        }

        var response = new OpsQueryResponse(answer, confidence, evidence, recommendations, isGuess, disclaimer, intent);
        _conversationHistory.Add(new OpsQueryMessage(ChatRole.User, question, DateTimeOffset.UtcNow));
        _conversationHistory.Add(new OpsQueryMessage(ChatRole.Assistant, answer, DateTimeOffset.UtcNow, evidence, confidence));
        return response;
    }

    public void ClearHistory() => _conversationHistory.Clear();

    private static string ClassifyIntent(string question)
    {
        string text = question.ToLowerInvariant();
        if (ContainsAny(text, "why", "failing", "broken", "down", "incident", "error", "outage"))
            return "incident_investigation";
        if (ContainsAny(text, "risk", "deploy", "safe", "dangerous", "blast", "impact"))
            return "risk_analysis";
        if (ContainsAny(text, "log", "logs", "exception", "crash", "stacktrace", "trace"))
            return "log_query";
        if (ContainsAny(text, "tracing", "metric", "slo", "latency", "performance", "instrument"))
            return "observability";
        if (ContainsAny(text, "security", "vulnerability", "secret", "injection", "permission", "cve"))
            return "security";
        return "general";
    }

    private static string BuildIncidentAnswer(OperationalContext context, IReadOnlyList<RootCauseHypothesis>? rcaHypotheses, IReadOnlyList<CorrelationChain>? correlationChains, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        Incident? incident = context.ActiveIncidents.OrderByDescending(i => i.Severity).ThenByDescending(i => i.StartedAt).FirstOrDefault();
        RootCauseHypothesis? topHypothesis = rcaHypotheses?.OrderBy(h => h.Rank).FirstOrDefault();
        CorrelationChain? chain = correlationChains?.FirstOrDefault(c => string.IsNullOrWhiteSpace(context.ServiceName) || c.ServiceName.Equals(context.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (incident is null && topHypothesis is null)
        {
            confidence = 0.25;
            isGuess = true;
            disclaimer = "No active incident or RCA hypothesis is currently available.";
            return "No active incident context is available right now.";
        }

        if (incident is not null)
            evidence.Add(new Evidence("incident", incident.Title, incident.PostMortemUrl, incident.StartedAt));
        if (topHypothesis is not null)
            evidence.AddRange(topHypothesis.Evidence);
        if (chain is not null)
            evidence.Add(new Evidence("correlation", chain.Summary ?? $"{chain.Events.Count} correlated events", Timestamp: chain.WindowEnd));

        recommendations.Add(topHypothesis?.Remediation.FirstOrDefault() ?? CreateRecommendation("Investigate timeline", "Review recent deployment, alert, and anomaly history for the affected service.", context.FilePath, RemediationRisk.Safe));
        confidence = topHypothesis?.ConfidenceScore ?? 0.7;
        isGuess = topHypothesis is null;
        disclaimer = topHypothesis is null ? "Top hypothesis unavailable; summary is based on active incident context only." : null;
        string hypothesisText = topHypothesis is not null ? $"Top hypothesis: {topHypothesis.Cause}" : "No ranked RCA hypothesis is available yet.";
        string chainText = chain?.Summary is not null ? $" Correlation: {chain.Summary}" : string.Empty;
        return $"{incident?.Title ?? "Incident under investigation"}. {hypothesisText}{chainText}";
    }

    private static string BuildRiskAnswer(OperationalContext context, DeploymentRiskReport? riskReport, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        if (riskReport is null)
        {
            confidence = 0.3;
            isGuess = true;
            disclaimer = "No deployment risk report was provided.";
            recommendations.Add(CreateRecommendation("Run risk scoring", "Generate a deployment risk report before shipping changes.", context.FilePath, RemediationRisk.Safe));
            return "I do not have a current deployment risk report, so risk cannot be assessed confidently.";
        }

        evidence.AddRange(riskReport.Factors.SelectMany(f => f.Evidence));
        recommendations.AddRange(riskReport.Recommendations.Select(text => CreateRecommendation("Mitigate deployment risk", text, context.FilePath, riskReport.RequiresApproval ? RemediationRisk.ReviewRequired : RemediationRisk.Safe)));
        confidence = Math.Max(0.6, riskReport.ConfidenceScore);
        isGuess = false;
        disclaimer = null;
        string factors = riskReport.Factors.Count == 0 ? "No explicit risk factors were recorded." : string.Join("; ", riskReport.Factors.Select(f => $"{f.Name} ({f.Level})"));
        return $"Deployment risk is {riskReport.OverallRisk} (score {riskReport.Score}). Factors: {factors}";
    }

    private static string BuildLogAnswer(OperationalContext context, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        ServiceHealth? health = context.CurrentHealth;
        Alert? alert = context.RecentAlerts.OrderByDescending(a => a.FiredAt).FirstOrDefault();
        AnomalyEvent? anomaly = context.RecentAnomalies.OrderByDescending(a => a.DetectedAt).FirstOrDefault();

        if (health is null && alert is null && anomaly is null)
        {
            confidence = 0.2;
            isGuess = true;
            disclaimer = "Recent logs are not attached to the operational context.";
            recommendations.Add(CreateRecommendation("Fetch logs", "Query connector-backed logs for the affected service and time range.", context.FilePath, RemediationRisk.Safe));
            return "No recent log detail is attached to this context; fetch logs from the active connector for a precise answer.";
        }

        if (health is not null)
            evidence.Add(new Evidence("service_health", $"Error rate {health.ErrorRatePercent:F2}% and p99 latency {health.P99LatencyMs:F0}ms.", Timestamp: health.AsOf));
        if (alert is not null)
            evidence.Add(new Evidence("alert", alert.RuleName, alert.Url, alert.FiredAt));
        if (anomaly is not null)
            evidence.AddRange(anomaly.Evidence);
        recommendations.Add(CreateRecommendation("Inspect recent errors", "Focus on recent exceptions and timeout patterns around the latest alert/anomaly window.", context.FilePath, RemediationRisk.Safe));
        confidence = 0.55;
        isGuess = false;
        disclaimer = "This summary infers likely error patterns from health and alert signals, not raw log lines.";
        return $"Recent operational signals suggest elevated failures around {alert?.RuleName ?? anomaly?.MetricName ?? context.ServiceName ?? "the service"}. Review exception and timeout logs first.";
    }

    private static string BuildObservabilityAnswer(OperationalContext context, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        var obsAnnotations = context.Annotations.Where(a => a.AnnotationType == InlineAnnotationType.ObservabilityGap || a.AnnotationType == InlineAnnotationType.AnomalyDetected || a.AnnotationType == InlineAnnotationType.LatencyHotspot).ToList();
        evidence.AddRange(obsAnnotations.Select(a => new Evidence("annotation", a.Message, a.ActionUrl)));
        evidence.AddRange(context.Slos.Select(s => new Evidence("slo", $"{s.Name}: {s.CurrentPercent:F2}% vs target {s.TargetPercent:F2}%", Timestamp: s.WindowEnd)));
        recommendations.Add(CreateRecommendation("Add tracing", "Instrument hot paths with spans and error attributes using the OTel generator.", context.FilePath, RemediationRisk.Safe));
        confidence = obsAnnotations.Count > 0 || context.Slos.Count > 0 ? 0.7 : 0.35;
        isGuess = obsAnnotations.Count == 0 && context.Slos.Count == 0;
        disclaimer = isGuess ? "No explicit observability gaps were attached to the current context." : null;
        string gapText = obsAnnotations.Count == 0 ? "No explicit observability gaps were found." : string.Join("; ", obsAnnotations.Select(a => a.Message));
        return $"Observability status for {context.ServiceName ?? context.FilePath ?? "the current context"}: {gapText}";
    }

    private static string BuildSecurityAnswer(OperationalContext context, IReadOnlyList<SecurityFinding>? securityFindings, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        var findings = securityFindings?.OrderByDescending(f => f.Severity).Take(3).ToList() ?? [];
        evidence.AddRange(findings.SelectMany(f => f.Evidence));
        recommendations.AddRange(findings.Select(f => CreateRecommendation(f.Title, f.Remediation, f.FilePath, RemediationRisk.ReviewRequired)));
        confidence = findings.Count > 0 ? 0.8 : 0.25;
        isGuess = findings.Count == 0;
        disclaimer = findings.Count == 0 ? "No security findings were provided with the current query." : null;
        return findings.Count == 0
            ? "No current security findings were attached to this question."
            : $"Top security findings: {string.Join("; ", findings.Select(f => $"{f.Title} ({f.Severity})"))}.";
    }

    private static string BuildGeneralAnswer(OperationalContext context, List<Evidence> evidence, List<RemediationSuggestion> recommendations, out double confidence, out bool isGuess, out string? disclaimer)
    {
        if (context.CurrentHealth is not null)
            evidence.Add(new Evidence("service_health", $"Error rate {context.CurrentHealth.ErrorRatePercent:F2}%, p99 latency {context.CurrentHealth.P99LatencyMs:F0}ms.", Timestamp: context.CurrentHealth.AsOf));
        evidence.AddRange(context.ActiveIncidents.Select(i => new Evidence("incident", i.Title, i.PostMortemUrl, i.StartedAt)));
        evidence.AddRange(context.RecentAlerts.Select(a => new Evidence("alert", a.RuleName, a.Url, a.FiredAt)));
        recommendations.Add(CreateRecommendation("Review operational health", "Check active incidents, alerts, and the latest deployment before making changes.", context.FilePath, RemediationRisk.Safe));
        confidence = context.ActiveIncidents.Count > 0 || context.RecentAlerts.Count > 0 || context.CurrentHealth is not null ? 0.65 : 0.35;
        isGuess = context.ActiveIncidents.Count == 0 && context.RecentAlerts.Count == 0 && context.CurrentHealth is null;
        disclaimer = isGuess ? "Only limited operational context is available." : null;
        return $"Operational summary: {context.ActiveIncidents.Count} active incident(s), {context.RecentAlerts.Count} recent alert(s), {context.RecentDeployments.Count} recent deployment(s), and {context.Slos.Count} SLO record(s).";
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(text.Contains);

    private static RemediationSuggestion CreateRecommendation(string title, string description, string? filePath, RemediationRisk risk)
        => new()
        {
            Title = title,
            Description = description,
            Risk = risk,
            FilePath = filePath ?? string.Empty,
            LineNumber = 0,
            ConfidenceScore = risk == RemediationRisk.Safe ? 0.7 : 0.6,
            Evidence = []
        };
}

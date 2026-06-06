namespace MyCrownJewelApp.Pfpad.AIOps;

// ── Severity / Confidence ──────────────────────────────────────────────────────

public enum Severity { Info, Low, Medium, High, Critical }
public enum ConfidenceLevel { Low, Medium, High }

/// <summary>A single piece of operational evidence supporting an insight.</summary>
public record Evidence(
    string Source,
    string Description,
    string? Url = null,
    DateTimeOffset? Timestamp = null);

/// <summary>Base for all AI-generated insights. Always carries confidence + evidence.</summary>
public abstract record AIOpsInsight
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public double ConfidenceScore { get; init; }
    public ConfidenceLevel ConfidenceLevel => ConfidenceScore switch { >= 0.8 => ConfidenceLevel.High, >= 0.5 => ConfidenceLevel.Medium, _ => ConfidenceLevel.Low };
    public IReadOnlyList<Evidence> Evidence { get; init; } = Array.Empty<Evidence>();
    public bool HasSufficientEvidence => Evidence.Count > 0 && ConfidenceScore >= 0.3;
}

// ── Telemetry ─────────────────────────────────────────────────────────────────

public record MetricPoint(string Name, double Value, string Unit, DateTimeOffset Timestamp, IReadOnlyDictionary<string, string>? Dimensions = null);

public record LogEntry(DateTimeOffset Timestamp, Severity Severity, string Message, string? TraceId = null, string? SpanId = null, IReadOnlyDictionary<string, string>? Properties = null)
{
    /// <summary>Returns message with sensitive values redacted.</summary>
    public string RedactedMessage => DataRedactor.Redact(Message);
}

public record TraceSpan(string TraceId, string SpanId, string? ParentSpanId, string ServiceName, string OperationName, DateTimeOffset StartTime, TimeSpan Duration, bool IsError, IReadOnlyDictionary<string, string>? Tags = null);

public record ServiceHealth(string ServiceName, double ErrorRatePercent, double P99LatencyMs, double AvailabilityPercent, DateTimeOffset AsOf);

// ── Incidents ─────────────────────────────────────────────────────────────────

public enum IncidentStatus { Triggered, Acknowledged, Resolved }

public record Incident(
    string Id, string Title, Severity Severity, IncidentStatus Status,
    DateTimeOffset StartedAt, DateTimeOffset? ResolvedAt,
    string? AffectedService, string? RootCauseHypothesis,
    IReadOnlyList<string> AffectedEndpoints,
    string? PostMortemUrl = null);

public record IncidentCorrelation : AIOpsInsight
{
    public required Incident Incident { get; init; }
    public required string CodeSymbol { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
}

// ── Deployments ───────────────────────────────────────────────────────────────

public enum DeploymentStatus { Pending, Running, Succeeded, Failed, RolledBack }

public record Deployment(
    string Id, string Service, string Version, string Environment,
    DeploymentStatus Status, DateTimeOffset DeployedAt, string? DeployedBy,
    string? CommitSha, string? PipelineUrl,
    IReadOnlyList<string> ChangedFiles);

public record Pipeline(
    string Id, string Name, string Branch,
    DeploymentStatus Status, DateTimeOffset StartedAt, TimeSpan? Duration,
    string? Url, IReadOnlyList<PipelineStage> Stages);

public record PipelineStage(string Name, DeploymentStatus Status, TimeSpan? Duration, string? LogUrl);

// ── Deployment Risk ───────────────────────────────────────────────────────────

public enum RiskLevel { Low, Medium, High, Critical }

public record DeploymentRiskFactor(string Name, RiskLevel Level, string Description, IReadOnlyList<Evidence> Evidence);

public record DeploymentRiskReport : AIOpsInsight
{
    public required RiskLevel OverallRisk { get; init; }
    public int Score { get; init; }
    public required IReadOnlyList<DeploymentRiskFactor> Factors { get; init; }
    public required IReadOnlyList<string> Recommendations { get; init; }
    public required IReadOnlyList<string> ChangedFiles { get; init; }
    public bool RequiresApproval => OverallRisk >= RiskLevel.High;
}

// ── Security ──────────────────────────────────────────────────────────────────

public enum SecurityCategory { Secret, Injection, Insecure, Policy, Dependency, Infrastructure }

public record SecurityFinding : AIOpsInsight
{
    public required string RuleId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Severity Severity { get; init; }
    public required SecurityCategory Category { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public string? CodeSnippet { get; init; }
    public required string Remediation { get; init; }
    public string? CweUrl => RuleId.StartsWith("CWE-") ? $"https://cwe.mitre.org/data/definitions/{RuleId[4..]}.html" : null;
}

public record PolicyViolation : AIOpsInsight
{
    public required string PolicyId { get; init; }
    public required string PolicyName { get; init; }
    public required string Description { get; init; }
    public required Severity Severity { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public required string Remediation { get; init; }
    public string? PolicyDocUrl { get; init; }
}

// ── SLOs ──────────────────────────────────────────────────────────────────────

public enum SloStatus { Healthy, AtRisk, Breached }

public record Slo(
    string Id, string Name, string ServiceName,
    double TargetPercent, double CurrentPercent,
    SloStatus Status, double BurnRate,
    DateTimeOffset WindowStart, DateTimeOffset WindowEnd);

// ── Observability ─────────────────────────────────────────────────────────────

public record ObservabilityGap : AIOpsInsight
{
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public required string Description { get; init; }
    public required string SuggestedCode { get; init; }
    public required string Category { get; init; }
}

// ── Remediation ───────────────────────────────────────────────────────────────

public enum RemediationRisk { Safe, ReviewRequired, ProductionImpact }

public record RemediationSuggestion : AIOpsInsight
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required RemediationRisk Risk { get; init; }
    public required string FilePath { get; init; }
    public int LineNumber { get; init; }
    public string? OldCode { get; init; }
    public string? NewCode { get; init; }
    public bool RequiresApproval => Risk >= RemediationRisk.ReviewRequired;
}

// ── Connector state ───────────────────────────────────────────────────────────

public enum ConnectorStatus { Disconnected, Connecting, Connected, Error }

public record ConnectorState(string Name, ConnectorStatus Status, string? ErrorMessage = null, DateTimeOffset? LastSyncAt = null);

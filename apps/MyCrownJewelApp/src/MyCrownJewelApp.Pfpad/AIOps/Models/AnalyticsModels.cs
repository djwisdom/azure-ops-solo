namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>Classification of detected anomaly types.</summary>
public enum AnomalyType
{
    LatencyIncrease,
    ErrorRateSpike,
    TrafficDrop,
    TrafficSpike,
    MemoryLeak,
    CpuSaturation,
    DependencyFailure,
    SecurityEvent,
    DeploymentRegression,
    Unknown
}

/// <summary>A detected statistical anomaly in a metric series.</summary>
public record AnomalyEvent(
    string Id,
    string ServiceName,
    string MetricName,
    double BaselineValue,
    double ObservedValue,
    double DeviationPercent,
    AnomalyType AnomalyType,
    DateTimeOffset DetectedAt,
    string Description,
    IReadOnlyList<Evidence> Evidence,
    Severity Severity = Severity.Medium);

/// <summary>A single hypothesis explaining an incident's root cause.</summary>
public record RootCauseHypothesis(
    int Rank,
    double ConfidenceScore,
    string Cause,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> RelatedFiles,
    string? RelatedDeploymentId,
    string? RelatedCommitSha,
    IReadOnlyList<string> ValidationSteps,
    IReadOnlyList<RemediationSuggestion> Remediation);

/// <summary>Root cause analysis result for an incident.</summary>
public record RootCauseAnalysis(
    string IncidentId,
    IReadOnlyList<RootCauseHypothesis> Hypotheses,
    DateTimeOffset AnalyzedAt,
    bool HasSufficientData,
    string? Disclaimer = null);

/// <summary>A single event in a correlation timeline.</summary>
public record CorrelationEvent(
    string EntityType,
    string EntityId,
    string Description,
    DateTimeOffset Timestamp,
    string? Url = null,
    Severity? Severity = null);

/// <summary>A correlation chain linking events for a service over time.</summary>
public record CorrelationChain(
    string ServiceName,
    IReadOnlyList<CorrelationEvent> Events,
    string? Summary,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd);

/// <summary>Type of inline code annotation.</summary>
public enum InlineAnnotationType
{
    LatencyHotspot,
    ErrorProne,
    IncidentLinked,
    SecurityRisk,
    ObservabilityGap,
    SloAtRisk,
    DeploymentRisk,
    AnomalyDetected
}

/// <summary>An annotation to display near a line of code.</summary>
public record InlineAnnotation(
    string FilePath,
    int LineNumber,
    string Symbol,
    InlineAnnotationType AnnotationType,
    string Message,
    IReadOnlyList<Evidence> Evidence,
    Severity Severity,
    string? ActionLabel = null,
    string? ActionUrl = null);

/// <summary>Combined operational context snapshot for a file/service.</summary>
public record OperationalContext(
    string? ServiceName,
    string? FilePath,
    ServiceHealth? CurrentHealth,
    IReadOnlyList<Incident> ActiveIncidents,
    IReadOnlyList<Deployment> RecentDeployments,
    IReadOnlyList<AnomalyEvent> RecentAnomalies,
    IReadOnlyList<InlineAnnotation> Annotations,
    IReadOnlyList<Slo> Slos,
    IReadOnlyList<Alert> RecentAlerts,
    DateTimeOffset GeneratedAt);

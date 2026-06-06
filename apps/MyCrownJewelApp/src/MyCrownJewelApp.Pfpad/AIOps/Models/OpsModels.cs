namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>A step within a runbook.</summary>
public record RunbookStep(
    int Order,
    string Description,
    string? Command,
    bool RequiresApproval,
    string? SafetyNote = null,
    TimeSpan? Timeout = null);

/// <summary>A runbook — a documented procedure for operational tasks.</summary>
public record Runbook(
    string Id,
    string Name,
    string Description,
    string ServiceName,
    IReadOnlyList<RunbookStep> Steps,
    string? ExternalUrl = null,
    Severity Severity = Severity.Info,
    bool IsAutomatable = false);

/// <summary>Result of a runbook execution attempt.</summary>
public record RunbookExecutionResult(
    string RunbookId,
    bool Success,
    string? Error,
    IReadOnlyList<string> ExecutedSteps,
    DateTimeOffset ExecutedAt,
    bool RequiredApproval = false,
    bool ApprovalGranted = false);

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>Role of a chat participant.</summary>
public enum ChatRole { User, Assistant, System }

/// <summary>A single message in an ops Q&A conversation.</summary>
public record OpsQueryMessage(
    ChatRole Role,
    string Content,
    DateTimeOffset Timestamp,
    IReadOnlyList<Evidence>? SupportingEvidence = null,
    double? ConfidenceScore = null);

/// <summary>The structured response to an ops query.</summary>
public record OpsQueryResponse(
    string Answer,
    double ConfidenceScore,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<RemediationSuggestion> Recommendations,
    bool IsGuess,
    string? Disclaimer,
    string? QueryIntent = null);

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>A cloud or on-premises infrastructure resource.</summary>
public record InfrastructureResource(
    string Id,
    string Type,
    string Name,
    string Region,
    string? Environment,
    string State,
    DateTimeOffset LastChanged,
    string? Provider = null,
    string? Url = null);

/// <summary>A feature flag state.</summary>
public record FeatureFlag(
    string Id,
    string Name,
    bool Enabled,
    double RolloutPercent,
    string? Environment,
    DateTimeOffset LastChanged,
    string? Owner = null);

/// <summary>A monitoring alert (fired rule).</summary>
public record Alert(
    string Id,
    string RuleName,
    string ServiceName,
    Severity Severity,
    string State,
    DateTimeOffset FiredAt,
    DateTimeOffset? ResolvedAt,
    string? Description,
    string? Url = null);

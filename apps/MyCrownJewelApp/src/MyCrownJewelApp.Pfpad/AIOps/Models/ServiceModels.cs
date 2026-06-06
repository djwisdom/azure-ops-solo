namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>A service entry in the service catalog.</summary>
public record ServiceEntity(
    string Id,
    string Name,
    string? Team,
    string? RepositoryUrl,
    string? Environment,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Tags,
    string? DocumentationUrl = null);

/// <summary>A dependency relationship between two services.</summary>
public record ServiceDependency(
    string FromService,
    string ToService,
    string Type,
    bool IsCritical,
    string? Protocol = null);

/// <summary>Complete service dependency graph.</summary>
public record ServiceDependencyGraph(
    IReadOnlyList<ServiceEntity> Services,
    IReadOnlyList<ServiceDependency> Dependencies,
    DateTimeOffset GeneratedAt);

/// <summary>Service-to-file/repository mapping entry (for ServiceContextEngine).</summary>
public record ServiceFileMapping(
    string ServiceName,
    string FilePath,
    string? RepositoryRoot = null);

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class ServiceContextEngine
{
    private readonly List<ServiceFileMapping> _mappings = new();

    // Cache glob-pattern → compiled Regex to avoid repeated Regex.Escape + new Regex() per call.
    private static readonly ConcurrentDictionary<string, Regex> _globCache = new(StringComparer.Ordinal);

    public void AddMapping(ServiceFileMapping mapping)
    {
        if (mapping is null)
            throw new ArgumentNullException(nameof(mapping));
        _mappings.Add(mapping);
    }

    public void ClearMappings() => _mappings.Clear();

    public string? ResolveServiceName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        string normalizedPath = NormalizePath(filePath);
        foreach (ServiceFileMapping mapping in _mappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.RepositoryRoot))
            {
                string root = NormalizePath(mapping.RepositoryRoot);
                if (!normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (GlobMatches(normalizedPath, NormalizePath(mapping.FilePath)))
                return mapping.ServiceName;
        }

        string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("src", StringComparison.OrdinalIgnoreCase)
                || segments[i].Equals("services", StringComparison.OrdinalIgnoreCase)
                || segments[i].Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = segments[i + 1];
                if (LooksLikeServiceName(candidate))
                    return candidate;
            }
        }

        foreach (string segment in segments)
        {
            if (segment.Contains("checkout", StringComparison.OrdinalIgnoreCase) || segment.Contains("cart", StringComparison.OrdinalIgnoreCase))
                return "checkout-service";
            if (segment.Contains("payment", StringComparison.OrdinalIgnoreCase))
                return segment.Contains("gateway", StringComparison.OrdinalIgnoreCase) ? "payment-gateway" : "payment-service";
            if (segment.Contains("order", StringComparison.OrdinalIgnoreCase))
                return "order-service";
            if (LooksLikeServiceName(segment))
                return segment;
        }

        return null;
    }

    public OperationalContext BuildContext(
        string filePath,
        string? serviceNameOverride,
        ServiceHealth? health,
        IReadOnlyList<Incident> incidents,
        IReadOnlyList<Deployment> deployments,
        IReadOnlyList<AnomalyEvent> anomalies,
        IReadOnlyList<Slo> slos,
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<InlineAnnotation> annotations)
    {
        string? serviceName = serviceNameOverride ?? ResolveServiceName(filePath);
        IReadOnlyList<Incident> filteredIncidents = FilterByService(incidents, incident => incident.AffectedService, serviceName);
        IReadOnlyList<Deployment> filteredDeployments = FilterByService(deployments, deployment => deployment.Service, serviceName);
        IReadOnlyList<AnomalyEvent> filteredAnomalies = FilterByService(anomalies, anomaly => anomaly.ServiceName, serviceName);
        IReadOnlyList<Slo> filteredSlos = FilterByService(slos, slo => slo.ServiceName, serviceName);
        IReadOnlyList<Alert> filteredAlerts = FilterByService(alerts, alert => alert.ServiceName, serviceName);
        IReadOnlyList<InlineAnnotation> filteredAnnotations = annotations
            .Where(annotation => string.IsNullOrWhiteSpace(annotation.FilePath) || annotation.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new OperationalContext(serviceName, filePath, health, filteredIncidents, filteredDeployments, filteredAnomalies, filteredAnnotations, filteredSlos, filteredAlerts, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<T> FilterByService<T>(IReadOnlyList<T> items, Func<T, string?> selector, string? serviceName)
        => string.IsNullOrWhiteSpace(serviceName)
            ? items
            : items.Where(item => string.Equals(selector(item), serviceName, StringComparison.OrdinalIgnoreCase)).ToList();

    private static bool LooksLikeServiceName(string value)
        => value.EndsWith("-service", StringComparison.OrdinalIgnoreCase)
           || value.EndsWith("-gateway", StringComparison.OrdinalIgnoreCase)
           || value.Contains("service", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static bool GlobMatches(string input, string pattern)
    {
        var regex = _globCache.GetOrAdd(pattern, static p =>
        {
            string regexPattern = "^" + Regex.Escape(p)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".") + "$";
            return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
        });
        return regex.IsMatch(input);
    }
}

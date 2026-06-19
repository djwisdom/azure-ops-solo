using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed partial class ObservabilityAdvisor
{
    [GeneratedRegex(@"\bthrow\b")]
    private static partial Regex ThrowRegex();

    [GeneratedRegex(@"ILogger\.|_logger\.Log(Error|Warning)", RegexOptions.IgnoreCase)]
    private static partial Regex LoggerCallRegex();

    [GeneratedRegex(@"\b(Meter|Counter|Histogram)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MetricsApiRegex();

    [GeneratedRegex(@"await\s+_http\w*\.GetAsync", RegexOptions.IgnoreCase)]
    private static partial Regex HttpGetAsyncRegex();

    // Method-finder regex: compiled once at class load.
    [GeneratedRegex(@"(?ms)(?<attrs>(\s*\[[^\]]+\]\s*)*)(?<signature>(public|private|internal|protected)\s+(?:async\s+)?[^{;=]+?\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^\)]*\)\s*)\{")]
    private static partial Regex MethodFinderRegex();

    public ValueTask<IReadOnlyList<ObservabilityGap>> AnalyzeAsync(string filePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return ValueTask.FromResult<IReadOnlyList<ObservabilityGap>>([]);

        var gaps = new List<ObservabilityGap>();
        foreach (var method in FindMethods(content))
        {
            ct.ThrowIfCancellationRequested();
            string body = method.Body;
            if ((method.Attributes.Contains("HttpGet", StringComparison.OrdinalIgnoreCase)
                 || method.Attributes.Contains("HttpPost", StringComparison.OrdinalIgnoreCase)
                 || method.Signature.Contains("async Task<", StringComparison.OrdinalIgnoreCase)
                 || method.Signature.Contains("async Task", StringComparison.OrdinalIgnoreCase))
                && !body.Contains("StartActivity", StringComparison.Ordinal))
            {
                gaps.Add(CreateGap(filePath, method.LineNumber, "Tracing", "Request-handling method is missing an Activity span.",
                    "using var activity = _activitySource.StartActivity(\"" + method.Name + "\");\nactivity?.SetTag(\"code.function\", nameof(" + method.Name + "));"));
            }

            if (ThrowRegex().IsMatch(body) && !LoggerCallRegex().IsMatch(body))
            {
                gaps.Add(CreateGap(filePath, method.LineNumber, "Logging", "Exception path does not emit structured warning/error logs.",
                    "_logger.LogError(ex, \"" + method.Name + " failed for {OperationId}\", operationId);"));
            }

            if ((method.Name.StartsWith("Process", StringComparison.OrdinalIgnoreCase) || method.Name.StartsWith("Calculate", StringComparison.OrdinalIgnoreCase))
                && !MetricsApiRegex().IsMatch(body))
            {
                gaps.Add(CreateGap(filePath, method.LineNumber, "Metrics", "Processing method is missing explicit metric instrumentation.",
                    "_processedCounter.Add(1, new KeyValuePair<string, object?>(\"method\", nameof(" + method.Name + ")));"));
            }

            if (HttpGetAsyncRegex().IsMatch(body) && !body.Contains("StartActivity", StringComparison.Ordinal))
            {
                gaps.Add(CreateGap(filePath, method.LineNumber, "Tracing", "Outbound HttpClient call is not wrapped in an Activity span.",
                    "using var activity = _activitySource.StartActivity(\"http " + method.Name + "\");\nactivity?.SetTag(\"http.client\", true);\nvar response = await _http.GetAsync(url, cancellationToken);"));
            }

            if (body.Contains("new CancellationTokenSource()", StringComparison.Ordinal) && !body.Contains("Activity.Current", StringComparison.Ordinal))
            {
                gaps.Add(CreateGap(filePath, method.LineNumber, "Tracing", "CancellationTokenSource is created without correlating the current Activity.",
                    "using var activity = _activitySource.StartActivity(\"" + method.Name + " correlation\");\nvar traceId = Activity.Current?.TraceId.ToString();\n_logger.LogInformation(\"Starting {Method} with TraceId {TraceId}\", nameof(" + method.Name + "), traceId);"));
            }
        }

        IReadOnlyList<ObservabilityGap> result = gaps
            .GroupBy(g => $"{g.Category}:{g.FilePath}:{g.LineNumber}:{g.Description}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        return ValueTask.FromResult(result);
    }

    private static ObservabilityGap CreateGap(string filePath, int lineNumber, string category, string description, string suggestion)
        => new()
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            Category = category,
            Description = description,
            SuggestedCode = suggestion,
            ConfidenceScore = 0.75,
            Evidence = [new Evidence("Code analysis", description, null, DateTimeOffset.UtcNow)]
        };

    private static IEnumerable<MethodBlock> FindMethods(string content)
    {
        foreach (Match match in MethodFinderRegex().Matches(content))
        {
            int openBrace = match.Index + match.Length - 1;
            int closeBrace = FindMatchingBrace(content, openBrace);
            if (closeBrace <= openBrace)
                continue;

            yield return new MethodBlock(
                match.Groups["name"].Value,
                match.Groups["attrs"].Value,
                match.Groups["signature"].Value,
                content.Substring(openBrace + 1, closeBrace - openBrace - 1),
                GetLineNumber(content, match.Index));
        }
    }

    private static int FindMatchingBrace(string content, int openBrace)
    {
        int depth = 0;
        for (int i = openBrace; i < content.Length; i++)
        {
            if (content[i] == '{')
                depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int GetLineNumber(string content, int index)
        => content.AsSpan(0, Math.Min(index, content.Length)).Count('\n') + 1;

    private readonly record struct MethodBlock(string Name, string Attributes, string Signature, string Body, int LineNumber);
}

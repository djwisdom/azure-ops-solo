namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class AnomalyDetector
{
    private readonly Dictionary<string, DateTimeOffset> _lastEmitted = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AnomalyEvent> DetectAnomalies(
        string serviceName,
        string metricName,
        IReadOnlyList<MetricPoint> series,
        double sigmaThreshold = 2.5,
        int minimumSamples = 10)
    {
        if (series.Count < minimumSamples)
            return [];

        var ordered = series.OrderBy(point => point.Timestamp).ToList();
        var baselineSeries = ordered.Take(Math.Max(1, ordered.Count - 3)).ToList();
        if (baselineSeries.Count == 0)
            return [];

        double mean = baselineSeries.Average(point => point.Value);
        double variance = baselineSeries.Sum(point => Math.Pow(point.Value - mean, 2)) / baselineSeries.Count;
        double stddev = Math.Sqrt(variance);
        var anomalies = new List<AnomalyEvent>();

        foreach (MetricPoint point in ordered.Skip(Math.Max(0, ordered.Count - 3)))
        {
            double deviation = Math.Abs(point.Value - mean);
            bool isAnomaly = stddev <= double.Epsilon ? deviation > 0 : deviation > sigmaThreshold * stddev;
            if (!isAnomaly)
                continue;

            AnomalyType anomalyType = Classify(metricName, point.Value, mean);
            string key = $"{serviceName}|{metricName}|{anomalyType}";
            if (_lastEmitted.TryGetValue(key, out DateTimeOffset previous) && point.Timestamp - previous < TimeSpan.FromMinutes(5))
                continue;

            _lastEmitted[key] = point.Timestamp;
            double deviationPercent = mean == 0 ? 100 : Math.Abs((point.Value - mean) / mean) * 100;
            anomalies.Add(new AnomalyEvent(
                Guid.NewGuid().ToString("N"),
                serviceName,
                metricName,
                mean,
                point.Value,
                Math.Round(deviationPercent, 2),
                anomalyType,
                point.Timestamp,
                $"{metricName} deviated from baseline by {Math.Round(deviationPercent, 1)}% for {serviceName}.",
                [
                    new Evidence("baseline", $"Baseline value: {mean:F2}"),
                    new Evidence("observation", $"Observed value: {point.Value:F2}", Timestamp: point.Timestamp),
                    new Evidence("deviation", $"Deviation: {deviationPercent:F2}%")
                ],
                ToSeverity(anomalyType, deviationPercent)));
        }

        return anomalies;
    }

    private static AnomalyType Classify(string metricName, double observed, double baseline)
    {
        string normalized = metricName.ToLowerInvariant();
        if (normalized.Contains("dependency"))
            return AnomalyType.DependencyFailure;
        if (normalized.Contains("error"))
            return AnomalyType.ErrorRateSpike;
        if (normalized.Contains("latency"))
            return AnomalyType.LatencyIncrease;
        if (normalized.Contains("cpu"))
            return AnomalyType.CpuSaturation;
        if (normalized.Contains("memory"))
            return AnomalyType.MemoryLeak;
        if (normalized.Contains("rps") || normalized.Contains("traffic"))
            return observed < baseline ? AnomalyType.TrafficDrop : AnomalyType.TrafficSpike;
        return AnomalyType.Unknown;
    }

    private static Severity ToSeverity(AnomalyType anomalyType, double deviationPercent)
        => anomalyType switch
        {
            AnomalyType.DependencyFailure or AnomalyType.ErrorRateSpike => deviationPercent >= 50 ? Severity.High : Severity.Medium,
            AnomalyType.LatencyIncrease or AnomalyType.CpuSaturation or AnomalyType.MemoryLeak => deviationPercent >= 75 ? Severity.High : Severity.Medium,
            _ => Severity.Medium
        };
}

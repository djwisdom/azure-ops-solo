namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class InlineAnnotationService : IDisposable
{
    private readonly Dictionary<string, List<InlineAnnotation>> _annotationsByFile = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, IReadOnlyList<InlineAnnotation>>? AnnotationsChanged;

    public void SetAnnotations(string filePath, IReadOnlyList<InlineAnnotation> annotations)
    {
        _annotationsByFile[filePath] = annotations.ToList();
        AnnotationsChanged?.Invoke(filePath, annotations);
    }

    public IReadOnlyList<InlineAnnotation> GetAnnotations(string filePath)
        => _annotationsByFile.TryGetValue(filePath, out List<InlineAnnotation>? list) ? list : Array.Empty<InlineAnnotation>();

    public void ClearAnnotations(string filePath)
    {
        _annotationsByFile.Remove(filePath);
        AnnotationsChanged?.Invoke(filePath, Array.Empty<InlineAnnotation>());
    }

    public void ClearAll()
    {
        foreach (string key in _annotationsByFile.Keys.ToList())
            ClearAnnotations(key);
    }

    public InlineAnnotation? GetAnnotationNearLine(string filePath, int lineNumber)
    {
        IReadOnlyList<InlineAnnotation> annotations = GetAnnotations(filePath);
        return annotations
            .Where(a => Math.Abs(a.LineNumber - lineNumber) <= 2)
            .OrderByDescending(a => a.Severity)
            .FirstOrDefault();
    }

    public static string GetGlyph(InlineAnnotationType type) => type switch
    {
        InlineAnnotationType.LatencyHotspot => "⚡",
        InlineAnnotationType.ErrorProne => "💥",
        InlineAnnotationType.IncidentLinked => "🚨",
        InlineAnnotationType.SecurityRisk => "🔒",
        InlineAnnotationType.ObservabilityGap => "👁",
        InlineAnnotationType.SloAtRisk => "📊",
        InlineAnnotationType.DeploymentRisk => "🚀",
        InlineAnnotationType.AnomalyDetected => "📈",
        _ => "●"
    };

    public static string GetTooltip(InlineAnnotation annotation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{GetGlyph(annotation.AnnotationType)} [{annotation.Severity}] {annotation.Message}");
        if (annotation.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Evidence:");
            foreach (Evidence e in annotation.Evidence.Take(3))
                sb.AppendLine($"  • {e.Source}: {e.Description}");
        }
        if (annotation.ActionLabel != null)
            sb.AppendLine($"\n→ {annotation.ActionLabel}");
        return sb.ToString().TrimEnd();
    }

    public void Dispose() => ClearAll();
}
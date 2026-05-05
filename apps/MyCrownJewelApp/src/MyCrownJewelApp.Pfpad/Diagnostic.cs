using System.Drawing;

namespace MyCrownJewelApp.Pfpad;

public enum DiagnosticSeverity
{
    Hint,
    Suggestion,
    Warning,
    Error
}

public sealed record Diagnostic
{
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public int Length { get; init; }
    public string Message { get; init; } = "";
    public DiagnosticSeverity Severity { get; init; }
    public string RuleId { get; init; } = "";

    public Color Color => Severity switch
    {
        DiagnosticSeverity.Error => Color.FromArgb(220, 60, 60),
        DiagnosticSeverity.Warning => Color.FromArgb(220, 180, 50),
        DiagnosticSeverity.Suggestion => Color.FromArgb(60, 180, 220),
        DiagnosticSeverity.Hint => Color.FromArgb(140, 140, 140),
        _ => Color.Gray
    };

    public string SeverityLabel => Severity switch
    {
        DiagnosticSeverity.Error => "Error",
        DiagnosticSeverity.Warning => "Warning",
        DiagnosticSeverity.Suggestion => "Suggestion",
        DiagnosticSeverity.Hint => "Hint",
        _ => "Info"
    };
}

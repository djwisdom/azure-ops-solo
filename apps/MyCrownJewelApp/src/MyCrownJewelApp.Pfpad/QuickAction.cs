using System;

namespace MyCrownJewelApp.Pfpad;

public sealed record QuickAction
{
    public string Title { get; init; } = "";
    public string DiagnosticRuleId { get; init; } = "";
    public int Line { get; init; }
    public Func<string, string>? Apply { get; init; }
}

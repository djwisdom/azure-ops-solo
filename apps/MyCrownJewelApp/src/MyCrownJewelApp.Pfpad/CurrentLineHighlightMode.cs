namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Specifies how the current line is highlighted.
/// </summary>
public enum CurrentLineHighlightMode
{
    /// <summary>No highlighting.</summary>
    Off = 0,

    /// <summary>Highlight line number in gutter only.</summary>
    NumberOnly = 1,

    /// <summary>Highlight the entire line background.</summary>
    WholeLine = 2
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Owns the editor font name and size. Provides helpers to apply the font to
/// a <see cref="RichTextBox"/>, load values from <see cref="AppSettings"/>,
/// and apply profile overrides — eliminating the <c>fontName</c>/<c>fontSize</c>
/// fields that previously lived on Form1.
/// </summary>
internal sealed class FontService
{
    public const string DefaultFontName = "Consolas";
    public const float DefaultFontSize = 12f;
    public const float MinFontSize = 6f;
    public const float MaxFontSize = 72f;

    public string FontName { get; private set; } = DefaultFontName;
    public float FontSize { get; private set; } = DefaultFontSize;

    /// <summary>
    /// Applies the current font to <paramref name="editor"/>.
    /// Silently ignores GDI+ failures (bad font name, size out of range etc.).
    /// </summary>
    public void ApplyFont(RichTextBox editor)
    {
        try { editor.Font = new Font(FontName, FontSize); }
        catch { }
    }

    /// <summary>
    /// Loads font name and size from <paramref name="settings"/> when they are
    /// within the accepted bounds. If the settings values are invalid the
    /// current values are preserved.
    /// </summary>
    public void LoadFrom(AppSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.FontName)
            && settings.FontSize >= MinFontSize
            && settings.FontSize <= MaxFontSize)
        {
            FontName = settings.FontName;
            FontSize = settings.FontSize;
        }
    }

    /// <summary>
    /// Sets font name and size from a <see cref="Font"/> chosen by the user
    /// (e.g. via <see cref="FontDialog"/>).
    /// </summary>
    public void SetFont(Font font)
    {
        FontName = font.Name;
        FontSize = font.Size;
    }

    /// <summary>
    /// Attempts to apply a profile font-size override.
    /// Clamps the value to [<see cref="MinFontSize"/>, <see cref="MaxFontSize"/>].
    /// </summary>
    /// <returns><c>true</c> when the size changed and the editor font needs refreshing.</returns>
    public bool SetFontSize(float size)
    {
        float clamped = Math.Max(MinFontSize, Math.Min(MaxFontSize, size));
        if (Math.Abs(clamped - FontSize) <= 0.1f) return false;
        FontSize = clamped;
        return true;
    }
}

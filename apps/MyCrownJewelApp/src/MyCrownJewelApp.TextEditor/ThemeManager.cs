using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;

namespace MyCrownJewelApp.TextEditor;

public sealed class ThemeManager : IDisposable
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, Bitmap> _tintedIconCache = new();
    private readonly object _cacheLock = new();
    private bool _isDarkMode = true;
    private bool _disposed;

    public event Action<Theme>? ThemeChanged;

    public Theme CurrentTheme { get; set; }
    public bool IsDarkMode
    {
        get => _isDarkMode;
        set { if (_isDarkMode != value) { _isDarkMode = value; CurrentTheme = value ? Theme.Dark : Theme.Light; OnThemeChanged(); } }
    }

    private ThemeManager()
    {
        CurrentTheme = IsDarkMode ? Theme.Dark : Theme.Light;
    }

    public void ToggleTheme() => IsDarkMode = !IsDarkMode;

    public void ApplyTheme(Form form, bool applyToMenus = true)
    {
        if (form == null || _disposed) return;
        form.BackColor = CurrentTheme.Background;
        form.ForeColor = CurrentTheme.Text;
        if (applyToMenus) ApplyToControls(form.Controls);
    }

    private void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            if (c is MenuStrip ms)
            {
                ms.Renderer = new ThemeAwareMenuRenderer(CurrentTheme);
                ms.BackColor = CurrentTheme.MenuBackground;
                ms.ForeColor = CurrentTheme.Text;
            }
            else if (c is ContextMenuStrip cms)
            {
                cms.Renderer = new ThemeAwareMenuRenderer(CurrentTheme);
                cms.BackColor = CurrentTheme.MenuBackground;
                cms.ForeColor = CurrentTheme.Text;
            }
            else if (c is ToolStrip ts)
            {
                ts.Renderer = new ThemeAwareMenuRenderer(CurrentTheme);
                ts.BackColor = CurrentTheme.MenuBackground;
                ts.ForeColor = CurrentTheme.Text;
            }
            else if (c is RichTextBox rtb)
            {
                rtb.BackColor = CurrentTheme.EditorBackground;
                rtb.ForeColor = CurrentTheme.Text;
            }
            else if (c is TextBox tb && tb.Multiline)
            {
                tb.BackColor = CurrentTheme.EditorBackground;
                tb.ForeColor = CurrentTheme.Text;
            }
            else if (c is Panel p)
            {
                p.BackColor = CurrentTheme.PanelBackground;
            }

            if (c.HasChildren) ApplyToControls(c.Controls);
        }
    }

    public Color GetColor(string key) => key.ToLowerInvariant() switch
    {
        "background" => CurrentTheme.Background,
        "foreground" => CurrentTheme.Text,
        "menu_bg" => CurrentTheme.MenuBackground,
        "menu_fg" => CurrentTheme.Text,
        "panel" => CurrentTheme.PanelBackground,
        "border" => CurrentTheme.Border,
        "accent" => CurrentTheme.Accent,
        "editor_bg" => CurrentTheme.EditorBackground,
        "editor_fg" => CurrentTheme.Text,
        "highlight" => CurrentTheme.Highlight,
        "disabled" => CurrentTheme.Disabled,
        _ => CurrentTheme.Text
    };

    public static Bitmap? TintIcon(Bitmap source, Color tint) => source == null ? null! : TintIconImpl(source, tint);

    private static Bitmap TintIconImpl(Bitmap source, Color tint)
    {
        if (source == null) return null;
        var result = new Bitmap(source.Width, source.Height);
        float r = tint.R / 255f, gv = tint.G / 255f, b = tint.B / 255f;
        using (var g = Graphics.FromImage(result))
        using (var attrs = new System.Drawing.Imaging.ImageAttributes())
        {
            var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new[] { r, 0, 0, 0, 0 },
                new[] { 0, gv, 0, 0, 0 },
                new[] { 0, 0, b, 0, 0 },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f }
            });
            attrs.SetColorMatrix(matrix);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        }
        return result;
    }

    public void ClearIconCache()
    {
        lock (_cacheLock)
        {
            foreach (var bmp in _tintedIconCache.Values) bmp?.Dispose();
            _tintedIconCache.Clear();
        }
    }

    private void OnThemeChanged()
    {
        if (_disposed) return;
        ClearIconCache();
        ThemeChanged?.Invoke(CurrentTheme);
    }

    public void Dispose()
    {
        if (_disposed) return;
        ClearIconCache();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public readonly struct Theme
{
    public static readonly Theme Light = new(
        background: Color.FromArgb(248, 248, 248),
        text: Color.FromArgb(20, 20, 20),
        menuBg: Color.FromArgb(245, 245, 245),
        menuFg: Color.FromArgb(20, 20, 20),
        panel: Color.FromArgb(235, 235, 235),
        border: Color.FromArgb(180, 180, 180),
        accent: Color.FromArgb(0, 120, 215),
        editorBg: Color.White,
        highlight: Color.FromArgb(0, 120, 215, 40),
        disabled: Color.FromArgb(160, 160, 160)
    );

    public static readonly Theme Dark = new(
        background: Color.FromArgb(30, 30, 30),
        text: Color.FromArgb(220, 220, 220),
        menuBg: Color.FromArgb(45, 45, 45),
        menuFg: Color.FromArgb(220, 220, 220),
        panel: Color.FromArgb(38, 38, 38),
        border: Color.FromArgb(80, 80, 80),
        accent: Color.FromArgb(0, 120, 215),
        editorBg: Color.FromArgb(30, 30, 30),
        highlight: Color.FromArgb(0, 120, 215, 60),
        disabled: Color.FromArgb(100, 100, 100)
    );

    public Color Background { get; }
    public Color Text { get; }
    public Color MenuBackground { get; }
    public Color MenuText { get; }
    public Color PanelBackground { get; }
    public Color Border { get; }
    public Color Accent { get; }
    public Color EditorBackground { get; }
    public Color Highlight { get; }
    public Color Disabled { get; }

    public Theme(Color background, Color text, Color menuBg, Color menuFg, Color panel, Color border, Color accent, Color editorBg, Color highlight, Color disabled)
    {
        Background = background;
        Text = text;
        MenuBackground = menuBg;
        MenuText = menuFg;
        PanelBackground = panel;
        Border = border;
        Accent = accent;
        EditorBackground = editorBg;
        Highlight = highlight;
        Disabled = disabled;
    }
}
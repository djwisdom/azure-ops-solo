using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MyCrownJewelApp.Pfpad;

public sealed class ThemeManager : IDisposable
{
    private static readonly Lazy<ThemeManager> _instance = new(() => new ThemeManager());
    public static ThemeManager Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, Bitmap> _tintedIconCache = new();
    private readonly object _cacheLock = new();
    private bool _disposed;

    public event Action<Theme>? ThemeChanged;

    public static Dictionary<string, Theme> Themes { get; } = new()
    {
        ["Dark"] = Theme.Dark,
        ["Light"] = Theme.Light,
        ["Catppuccin Latte"] = Theme.CatppuccinLatte,
        ["Catppuccin Frappe"] = Theme.CatppuccinFrappe,
        ["Catppuccin Macchiato"] = Theme.CatppuccinMacchiato,
        ["Catppuccin Mocha"] = Theme.CatppuccinMocha,
        ["Dracula"] = Theme.Dracula,
        ["One Dark Pro"] = Theme.OneDarkPro,
        ["Tokyo Night"] = Theme.TokyoNight,
        ["Night Owl"] = Theme.NightOwl,
        ["Shades of Purple"] = Theme.ShadesOfPurple,
        ["Atom One Light"] = Theme.AtomOneLight,
        ["GitHub Light"] = Theme.GitHubLight,
        ["Light Owl"] = Theme.LightOwl,
        ["Ayu Light"] = Theme.AyuLight,
        ["Bluloco Light"] = Theme.BlulocoLight,
    };

    public static string[] ThemeNames => Themes.Keys.ToArray();

    public Theme CurrentTheme { get; private set; }

    private ThemeManager()
    {
        CurrentTheme = Theme.Dark;
    }

    public void SetTheme(string name)
    {
        if (_disposed) return;
        if (Themes.TryGetValue(name, out var theme) && !theme.Equals(CurrentTheme))
        {
            CurrentTheme = theme;
            ClearIconCache();
            ThemeChanged?.Invoke(CurrentTheme);
        }
    }

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

    private static Bitmap? TintIconImpl(Bitmap source, Color tint)
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
    public string Name { get; }
    public bool IsLight { get; }

    // UI colors
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
    public Color ButtonHoverBackground { get; }
    public Color Muted { get; }

    // Syntax highlighting colors
    public Color KeywordColor { get; }
    public Color StringColor { get; }
    public Color CommentColor { get; }
    public Color NumberColor { get; }
    public Color PreprocessorColor { get; }

    // Terminal colors
    public Color TerminalBackground { get; }
    public Color TerminalForeground { get; }
    public Color TerminalInputBackground { get; }
    public Color TerminalHeaderBackground { get; }

    public Theme(
        string name, bool isLight,
        Color background, Color text, Color menuBg, Color menuFg,
        Color panel, Color border, Color accent, Color editorBg,
        Color highlight, Color disabled, Color buttonHoverBackground, Color muted,
        Color keywordColor, Color stringColor, Color commentColor,
        Color numberColor, Color preprocessorColor,
        Color terminalBg, Color terminalFg, Color terminalInputBg, Color terminalHeaderBg)
    {
        Name = name;
        IsLight = isLight;
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
        ButtonHoverBackground = buttonHoverBackground;
        Muted = muted;
        KeywordColor = keywordColor;
        StringColor = stringColor;
        CommentColor = commentColor;
        NumberColor = numberColor;
        PreprocessorColor = preprocessorColor;
        TerminalBackground = terminalBg;
        TerminalForeground = terminalFg;
        TerminalInputBackground = terminalInputBg;
        TerminalHeaderBackground = terminalHeaderBg;
    }

    public static readonly Theme Light = new(
        name: "Light", isLight: true,
        background: Color.FromArgb(248, 248, 248),
        text: Color.FromArgb(20, 20, 20),
        menuBg: Color.FromArgb(245, 245, 245),
        menuFg: Color.FromArgb(20, 20, 20),
        panel: Color.FromArgb(235, 235, 235),
        border: Color.FromArgb(180, 180, 180),
        accent: Color.FromArgb(0, 120, 215),
        editorBg: Color.White,
        highlight: Color.FromArgb(0, 120, 215, 40),
        disabled: Color.FromArgb(160, 160, 160),
        buttonHoverBackground: Color.FromArgb(215, 215, 215),
        muted: Color.FromArgb(120, 120, 120),
        keywordColor: Color.Blue,
        stringColor: Color.FromArgb(163, 21, 21),
        commentColor: Color.Green,
        numberColor: Color.FromArgb(9, 134, 88),
        preprocessorColor: Color.FromArgb(121, 94, 38),
        terminalBg: Color.FromArgb(248, 248, 248),
        terminalFg: Color.FromArgb(20, 20, 20),
        terminalInputBg: Color.FromArgb(235, 235, 235),
        terminalHeaderBg: Color.FromArgb(245, 245, 245)
    );

    public static readonly Theme Dark = new(
        name: "Dark", isLight: false,
        background: Color.FromArgb(30, 30, 30),
        text: Color.FromArgb(220, 220, 220),
        menuBg: Color.FromArgb(45, 45, 45),
        menuFg: Color.FromArgb(220, 220, 220),
        panel: Color.FromArgb(38, 38, 38),
        border: Color.FromArgb(80, 80, 80),
        accent: Color.FromArgb(0, 120, 215),
        editorBg: Color.FromArgb(30, 30, 30),
        highlight: Color.FromArgb(0, 120, 215, 60),
        disabled: Color.FromArgb(100, 100, 100),
        buttonHoverBackground: Color.FromArgb(70, 70, 70),
        muted: Color.FromArgb(140, 140, 140),
        keywordColor: Color.FromArgb(86, 156, 214),
        stringColor: Color.FromArgb(206, 145, 120),
        commentColor: Color.FromArgb(106, 153, 85),
        numberColor: Color.FromArgb(181, 206, 168),
        preprocessorColor: Color.FromArgb(197, 134, 192),
        terminalBg: Color.FromArgb(30, 30, 30),
        terminalFg: Color.FromArgb(220, 220, 220),
        terminalInputBg: Color.FromArgb(60, 60, 60),
        terminalHeaderBg: Color.FromArgb(45, 45, 45)
    );

    public static readonly Theme CatppuccinLatte = new(
        name: "Catppuccin Latte", isLight: true,
        background: Color.FromArgb(230, 233, 239),    // mantle
        text: Color.FromArgb(76, 79, 105),             // text
        menuBg: Color.FromArgb(220, 224, 232),         // crust
        menuFg: Color.FromArgb(76, 79, 105),
        panel: Color.FromArgb(204, 208, 218),          // surface0
        border: Color.FromArgb(188, 192, 204),         // surface1
        accent: Color.FromArgb(30, 102, 245),          // blue
        editorBg: Color.FromArgb(239, 241, 245),       // base
        highlight: Color.FromArgb(30, 102, 245, 40),
        disabled: Color.FromArgb(156, 160, 176),       // overlay0
        buttonHoverBackground: Color.FromArgb(188, 192, 204), // surface1
        muted: Color.FromArgb(108, 111, 133),          // subtext0
        keywordColor: Color.FromArgb(136, 57, 239),    // mauve
        stringColor: Color.FromArgb(64, 160, 43),      // green
        commentColor: Color.FromArgb(156, 160, 176),   // overlay0
        numberColor: Color.FromArgb(254, 100, 11),     // peach
        preprocessorColor: Color.FromArgb(234, 118, 203), // pink
        terminalBg: Color.FromArgb(230, 233, 239),
        terminalFg: Color.FromArgb(76, 79, 105),
        terminalInputBg: Color.FromArgb(204, 208, 218),
        terminalHeaderBg: Color.FromArgb(220, 224, 232)
    );

    public static readonly Theme CatppuccinFrappe = new(
        name: "Catppuccin Frappe", isLight: false,
        background: Color.FromArgb(41, 44, 60),        // mantle
        text: Color.FromArgb(198, 208, 245),           // text
        menuBg: Color.FromArgb(35, 38, 52),            // crust
        menuFg: Color.FromArgb(198, 208, 245),
        panel: Color.FromArgb(65, 69, 89),             // surface0
        border: Color.FromArgb(81, 87, 109),           // surface1
        accent: Color.FromArgb(140, 170, 238),         // blue
        editorBg: Color.FromArgb(48, 52, 70),          // base
        highlight: Color.FromArgb(140, 170, 238, 60),
        disabled: Color.FromArgb(115, 121, 148),       // overlay0
        buttonHoverBackground: Color.FromArgb(81, 87, 109), // surface1
        muted: Color.FromArgb(165, 173, 206),          // subtext0
        keywordColor: Color.FromArgb(202, 158, 230),   // mauve
        stringColor: Color.FromArgb(166, 209, 137),    // green
        commentColor: Color.FromArgb(115, 121, 148),   // overlay0
        numberColor: Color.FromArgb(239, 159, 118),    // peach
        preprocessorColor: Color.FromArgb(244, 184, 228), // pink
        terminalBg: Color.FromArgb(41, 44, 60),
        terminalFg: Color.FromArgb(198, 208, 245),
        terminalInputBg: Color.FromArgb(65, 69, 89),
        terminalHeaderBg: Color.FromArgb(35, 38, 52)
    );

    public static readonly Theme CatppuccinMacchiato = new(
        name: "Catppuccin Macchiato", isLight: false,
        background: Color.FromArgb(36, 39, 58),        // mantle
        text: Color.FromArgb(202, 211, 245),           // text
        menuBg: Color.FromArgb(30, 32, 48),            // crust
        menuFg: Color.FromArgb(202, 211, 245),
        panel: Color.FromArgb(54, 58, 79),             // surface0
        border: Color.FromArgb(73, 77, 100),           // surface1
        accent: Color.FromArgb(138, 173, 244),         // blue
        editorBg: Color.FromArgb(54, 58, 79),          // base
        highlight: Color.FromArgb(138, 173, 244, 60),
        disabled: Color.FromArgb(108, 112, 134),       // overlay0
        buttonHoverBackground: Color.FromArgb(73, 77, 100), // surface1
        muted: Color.FromArgb(165, 173, 203),          // subtext0
        keywordColor: Color.FromArgb(198, 160, 246),   // mauve
        stringColor: Color.FromArgb(166, 218, 149),    // green
        commentColor: Color.FromArgb(108, 112, 134),   // overlay0
        numberColor: Color.FromArgb(245, 169, 127),    // peach
        preprocessorColor: Color.FromArgb(245, 189, 230), // pink
        terminalBg: Color.FromArgb(36, 39, 58),
        terminalFg: Color.FromArgb(202, 211, 245),
        terminalInputBg: Color.FromArgb(54, 58, 79),
        terminalHeaderBg: Color.FromArgb(30, 32, 48)
    );

    public static readonly Theme CatppuccinMocha = new(
        name: "Catppuccin Mocha", isLight: false,
        background: Color.FromArgb(24, 24, 37),        // mantle
        text: Color.FromArgb(205, 214, 244),           // text
        menuBg: Color.FromArgb(17, 17, 27),            // crust
        menuFg: Color.FromArgb(205, 214, 244),
        panel: Color.FromArgb(49, 50, 68),             // surface0
        border: Color.FromArgb(69, 71, 90),            // surface1
        accent: Color.FromArgb(137, 180, 250),         // blue
        editorBg: Color.FromArgb(30, 30, 46),          // base
        highlight: Color.FromArgb(137, 180, 250, 60),
        disabled: Color.FromArgb(108, 112, 134),       // overlay0
        buttonHoverBackground: Color.FromArgb(69, 71, 90), // surface1
        muted: Color.FromArgb(166, 173, 200),          // subtext0
        keywordColor: Color.FromArgb(203, 166, 247),   // mauve
        stringColor: Color.FromArgb(166, 227, 161),    // green
        commentColor: Color.FromArgb(108, 112, 134),   // overlay0
        numberColor: Color.FromArgb(250, 179, 135),    // peach
        preprocessorColor: Color.FromArgb(245, 194, 231), // pink
        terminalBg: Color.FromArgb(24, 24, 37),
        terminalFg: Color.FromArgb(205, 214, 244),
        terminalInputBg: Color.FromArgb(49, 50, 68),
        terminalHeaderBg: Color.FromArgb(17, 17, 27)
    );

    // ════════════════════════════════════════════
    // Top 5 Dark Themes for 2026
    // ════════════════════════════════════════════

    public static readonly Theme Dracula = new(
        name: "Dracula", isLight: false,
        background: Color.FromArgb(40, 42, 54),
        text: Color.FromArgb(248, 248, 242),
        menuBg: Color.FromArgb(33, 34, 44),
        menuFg: Color.FromArgb(248, 248, 242),
        panel: Color.FromArgb(68, 71, 90),
        border: Color.FromArgb(68, 71, 90),
        accent: Color.FromArgb(189, 147, 249),       // purple
        editorBg: Color.FromArgb(40, 42, 54),
        highlight: Color.FromArgb(189, 147, 249, 40),
        disabled: Color.FromArgb(98, 114, 164),
        buttonHoverBackground: Color.FromArgb(68, 71, 90),
        muted: Color.FromArgb(98, 114, 164),          // comment
        keywordColor: Color.FromArgb(255, 121, 198),  // pink
        stringColor: Color.FromArgb(80, 250, 123),    // green
        commentColor: Color.FromArgb(98, 114, 164),   // comment
        numberColor: Color.FromArgb(255, 184, 108),   // orange
        preprocessorColor: Color.FromArgb(139, 233, 253), // cyan
        terminalBg: Color.FromArgb(40, 42, 54),
        terminalFg: Color.FromArgb(248, 248, 242),
        terminalInputBg: Color.FromArgb(68, 71, 90),
        terminalHeaderBg: Color.FromArgb(33, 34, 44)
    );

    public static readonly Theme OneDarkPro = new(
        name: "One Dark Pro", isLight: false,
        background: Color.FromArgb(40, 44, 52),
        text: Color.FromArgb(171, 178, 191),
        menuBg: Color.FromArgb(33, 37, 43),
        menuFg: Color.FromArgb(171, 178, 191),
        panel: Color.FromArgb(33, 37, 43),
        border: Color.FromArgb(60, 64, 72),
        accent: Color.FromArgb(97, 175, 239),         // blue (malibu)
        editorBg: Color.FromArgb(40, 44, 52),
        highlight: Color.FromArgb(97, 175, 239, 40),
        disabled: Color.FromArgb(92, 99, 112),
        buttonHoverBackground: Color.FromArgb(50, 54, 62),
        muted: Color.FromArgb(92, 99, 112),
        keywordColor: Color.FromArgb(198, 120, 221),  // purple
        stringColor: Color.FromArgb(152, 195, 121),   // green
        commentColor: Color.FromArgb(92, 99, 112),    // lightDark
        numberColor: Color.FromArgb(209, 154, 102),   // whiskey (orange)
        preprocessorColor: Color.FromArgb(86, 182, 194), // fountainBlue (cyan)
        terminalBg: Color.FromArgb(40, 44, 52),
        terminalFg: Color.FromArgb(171, 178, 191),
        terminalInputBg: Color.FromArgb(50, 54, 62),
        terminalHeaderBg: Color.FromArgb(33, 37, 43)
    );

    public static readonly Theme TokyoNight = new(
        name: "Tokyo Night", isLight: false,
        background: Color.FromArgb(26, 27, 38),
        text: Color.FromArgb(169, 177, 214),
        menuBg: Color.FromArgb(20, 21, 31),
        menuFg: Color.FromArgb(169, 177, 214),
        panel: Color.FromArgb(36, 40, 59),
        border: Color.FromArgb(65, 72, 104),
        accent: Color.FromArgb(122, 162, 247),        // blue
        editorBg: Color.FromArgb(26, 27, 38),
        highlight: Color.FromArgb(122, 162, 247, 40),
        disabled: Color.FromArgb(86, 95, 137),
        buttonHoverBackground: Color.FromArgb(36, 40, 59),
        muted: Color.FromArgb(86, 95, 137),           // comment
        keywordColor: Color.FromArgb(187, 154, 247),  // magenta
        stringColor: Color.FromArgb(158, 206, 106),   // green
        commentColor: Color.FromArgb(86, 95, 137),    // comment
        numberColor: Color.FromArgb(255, 158, 100),   // orange
        preprocessorColor: Color.FromArgb(125, 207, 255), // cyan
        terminalBg: Color.FromArgb(26, 27, 38),
        terminalFg: Color.FromArgb(169, 177, 214),
        terminalInputBg: Color.FromArgb(36, 40, 59),
        terminalHeaderBg: Color.FromArgb(20, 21, 31)
    );

    public static readonly Theme NightOwl = new(
        name: "Night Owl", isLight: false,
        background: Color.FromArgb(1, 22, 39),
        text: Color.FromArgb(214, 222, 235),
        menuBg: Color.FromArgb(0, 12, 29),
        menuFg: Color.FromArgb(214, 222, 235),
        panel: Color.FromArgb(17, 38, 48),
        border: Color.FromArgb(16, 42, 68),
        accent: Color.FromArgb(130, 170, 255),        // blue
        editorBg: Color.FromArgb(1, 22, 39),
        highlight: Color.FromArgb(130, 170, 255, 40),
        disabled: Color.FromArgb(99, 119, 119),
        buttonHoverBackground: Color.FromArgb(17, 38, 48),
        muted: Color.FromArgb(99, 119, 119),          // slate
        keywordColor: Color.FromArgb(127, 219, 202),  // paleblue/teal (tags/keywords)
        stringColor: Color.FromArgb(173, 219, 103),   // green
        commentColor: Color.FromArgb(99, 119, 119),   // slate
        numberColor: Color.FromArgb(247, 140, 108),   // peach
        preprocessorColor: Color.FromArgb(199, 146, 234), // pink
        terminalBg: Color.FromArgb(1, 22, 39),
        terminalFg: Color.FromArgb(214, 222, 235),
        terminalInputBg: Color.FromArgb(17, 38, 48),
        terminalHeaderBg: Color.FromArgb(0, 12, 29)
    );

    public static readonly Theme ShadesOfPurple = new(
        name: "Shades of Purple", isLight: false,
        background: Color.FromArgb(45, 43, 85),
        text: Color.FromArgb(165, 153, 233),
        menuBg: Color.FromArgb(30, 30, 63),
        menuFg: Color.FromArgb(165, 153, 233),
        panel: Color.FromArgb(50, 48, 95),
        border: Color.FromArgb(70, 68, 120),
        accent: Color.FromArgb(250, 208, 0),          // yellow contrast
        editorBg: Color.FromArgb(45, 43, 85),
        highlight: Color.FromArgb(255, 114, 0, 40),   // highlight orange
        disabled: Color.FromArgb(179, 98, 255),
        buttonHoverBackground: Color.FromArgb(77, 33, 252),
        muted: Color.FromArgb(179, 98, 255),          // comment purple
        keywordColor: Color.FromArgb(255, 157, 0),    // orange
        stringColor: Color.FromArgb(165, 255, 144),   // green
        commentColor: Color.FromArgb(179, 98, 255),   // purple
        numberColor: Color.FromArgb(255, 98, 140),    // pink (constants)
        preprocessorColor: Color.FromArgb(158, 255, 255), // cyan (other)
        terminalBg: Color.FromArgb(45, 43, 85),
        terminalFg: Color.FromArgb(165, 153, 233),
        terminalInputBg: Color.FromArgb(50, 48, 95),
        terminalHeaderBg: Color.FromArgb(30, 30, 63)
    );

    // ════════════════════════════════════════════
    // Top 5 Light Themes for 2026
    // ════════════════════════════════════════════

    public static readonly Theme AtomOneLight = new(
        name: "Atom One Light", isLight: true,
        background: Color.FromArgb(250, 250, 250),
        text: Color.FromArgb(56, 58, 66),
        menuBg: Color.FromArgb(240, 240, 240),
        menuFg: Color.FromArgb(56, 58, 66),
        panel: Color.FromArgb(232, 232, 232),
        border: Color.FromArgb(200, 200, 210),
        accent: Color.FromArgb(74, 134, 232),         // blue
        editorBg: Color.FromArgb(250, 250, 250),
        highlight: Color.FromArgb(74, 134, 232, 30),
        disabled: Color.FromArgb(160, 161, 167),
        buttonHoverBackground: Color.FromArgb(224, 224, 228),
        muted: Color.FromArgb(160, 161, 167),
        keywordColor: Color.FromArgb(167, 84, 196),   // purple
        stringColor: Color.FromArgb(80, 161, 79),     // green
        commentColor: Color.FromArgb(160, 161, 167),  // gray
        numberColor: Color.FromArgb(208, 125, 56),    // orange
        preprocessorColor: Color.FromArgb(180, 142, 173), // pinkish
        terminalBg: Color.FromArgb(250, 250, 250),
        terminalFg: Color.FromArgb(56, 58, 66),
        terminalInputBg: Color.FromArgb(232, 232, 232),
        terminalHeaderBg: Color.FromArgb(240, 240, 240)
    );

    public static readonly Theme GitHubLight = new(
        name: "GitHub Light", isLight: true,
        background: Color.FromArgb(255, 255, 255),
        text: Color.FromArgb(36, 41, 47),
        menuBg: Color.FromArgb(246, 248, 250),
        menuFg: Color.FromArgb(36, 41, 47),
        panel: Color.FromArgb(234, 238, 242),
        border: Color.FromArgb(208, 215, 222),
        accent: Color.FromArgb(9, 105, 218),          // blue
        editorBg: Color.FromArgb(255, 255, 255),
        highlight: Color.FromArgb(9, 105, 218, 30),
        disabled: Color.FromArgb(149, 157, 165),
        buttonHoverBackground: Color.FromArgb(234, 238, 242),
        muted: Color.FromArgb(149, 157, 165),
        keywordColor: Color.FromArgb(207, 34, 46),    // red
        stringColor: Color.FromArgb(17, 135, 67),     // green
        commentColor: Color.FromArgb(149, 157, 165),  // gray
        numberColor: Color.FromArgb(9, 105, 218),     // blue
        preprocessorColor: Color.FromArgb(130, 80, 223), // purple
        terminalBg: Color.FromArgb(255, 255, 255),
        terminalFg: Color.FromArgb(36, 41, 47),
        terminalInputBg: Color.FromArgb(234, 238, 242),
        terminalHeaderBg: Color.FromArgb(246, 248, 250)
    );

    public static readonly Theme LightOwl = new(
        name: "Light Owl", isLight: true,
        background: Color.FromArgb(250, 248, 245),
        text: Color.FromArgb(64, 63, 83),
        menuBg: Color.FromArgb(240, 240, 240),
        menuFg: Color.FromArgb(64, 63, 83),
        panel: Color.FromArgb(230, 230, 230),
        border: Color.FromArgb(210, 210, 210),
        accent: Color.FromArgb(42, 162, 152),         // teal
        editorBg: Color.FromArgb(250, 248, 245),
        highlight: Color.FromArgb(42, 162, 152, 30),
        disabled: Color.FromArgb(147, 161, 161),
        buttonHoverBackground: Color.FromArgb(224, 232, 248),
        muted: Color.FromArgb(147, 161, 161),
        keywordColor: Color.FromArgb(153, 76, 195),   // purple
        stringColor: Color.FromArgb(8, 145, 106),     // green
        commentColor: Color.FromArgb(147, 161, 161),  // gray
        numberColor: Color.FromArgb(218, 170, 1),     // yellow
        preprocessorColor: Color.FromArgb(40, 158, 215), // cyan
        terminalBg: Color.FromArgb(246, 246, 246),
        terminalFg: Color.FromArgb(64, 63, 83),
        terminalInputBg: Color.FromArgb(230, 230, 230),
        terminalHeaderBg: Color.FromArgb(240, 240, 240)
    );

    public static readonly Theme AyuLight = new(
        name: "Ayu Light", isLight: true,
        background: Color.FromArgb(252, 252, 252),
        text: Color.FromArgb(92, 97, 102),
        menuBg: Color.FromArgb(242, 242, 242),
        menuFg: Color.FromArgb(92, 97, 102),
        panel: Color.FromArgb(230, 230, 230),
        border: Color.FromArgb(210, 210, 210),
        accent: Color.FromArgb(255, 143, 64),         // orange (keyword color in ayu)
        editorBg: Color.FromArgb(252, 252, 252),
        highlight: Color.FromArgb(255, 143, 64, 25),
        disabled: Color.FromArgb(153, 158, 163),
        buttonHoverBackground: Color.FromArgb(224, 224, 224),
        muted: Color.FromArgb(153, 158, 163),
        keywordColor: Color.FromArgb(255, 143, 64),   // orange
        stringColor: Color.FromArgb(170, 217, 76),    // green
        commentColor: Color.FromArgb(153, 158, 163),  // gray
        numberColor: Color.FromArgb(250, 157, 77),    // light orange
        preprocessorColor: Color.FromArgb(115, 175, 225), // blue
        terminalBg: Color.FromArgb(252, 252, 252),
        terminalFg: Color.FromArgb(92, 97, 102),
        terminalInputBg: Color.FromArgb(230, 230, 230),
        terminalHeaderBg: Color.FromArgb(242, 242, 242)
    );

    public static readonly Theme BlulocoLight = new(
        name: "Bluloco Light", isLight: true,
        background: Color.FromArgb(249, 249, 249),
        text: Color.FromArgb(56, 58, 66),
        menuBg: Color.FromArgb(238, 238, 238),
        menuFg: Color.FromArgb(56, 58, 66),
        panel: Color.FromArgb(228, 228, 228),
        border: Color.FromArgb(206, 206, 216),
        accent: Color.FromArgb(36, 137, 219),         // blue
        editorBg: Color.FromArgb(249, 249, 249),
        highlight: Color.FromArgb(36, 137, 219, 30),
        disabled: Color.FromArgb(158, 158, 168),
        buttonHoverBackground: Color.FromArgb(222, 222, 228),
        muted: Color.FromArgb(158, 158, 168),
        keywordColor: Color.FromArgb(209, 55, 77),    // red
        stringColor: Color.FromArgb(45, 147, 103),    // green
        commentColor: Color.FromArgb(158, 158, 168),  // gray
        numberColor: Color.FromArgb(207, 120, 53),    // orange
        preprocessorColor: Color.FromArgb(144, 94, 209), // purple
        terminalBg: Color.FromArgb(249, 249, 249),
        terminalFg: Color.FromArgb(56, 58, 66),
        terminalInputBg: Color.FromArgb(228, 228, 228),
        terminalHeaderBg: Color.FromArgb(238, 238, 238)
    );
}

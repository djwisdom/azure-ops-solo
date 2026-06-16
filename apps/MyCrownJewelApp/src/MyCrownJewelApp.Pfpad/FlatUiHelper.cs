namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Centralised helpers for applying a flat, theme-aware appearance to WinForms
/// controls that do not support it natively (TextBox, ListBox, ComboBox, etc.).
///
/// Design principles
/// -----------------
/// • A thin 1-px border panel wraps any borderless control so the border colour
///   always comes from <see cref="Theme.Border"/> rather than a system colour.
/// • Every helper works both at construction time and when the theme changes.
/// • Semantic status colours (error, warning, success, …) are derived from the
///   active theme so they stay visible on both light and dark backgrounds.
/// </summary>
internal static class FlatUiHelper
{
    // ── Flat border wrapper ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Panel"/> that draws a single-pixel themed border and
    /// hosts <paramref name="inner"/> with <see cref="DockStyle.Fill"/> and
    /// <see cref="BorderStyle.None"/>.
    ///
    /// Use the returned panel as the layout control; read/write <paramref name="inner"/>
    /// for content.
    /// </summary>
    public static Panel WrapFlat(Control inner, Theme theme, Padding? padding = null)
    {
        if (inner is TextBox tb) tb.BorderStyle = BorderStyle.None;
        else if (inner is RichTextBox rtb) rtb.BorderStyle = BorderStyle.None;
        else if (inner is ListBox lb) lb.BorderStyle = BorderStyle.None;
        else if (inner is ListView lv) lv.BorderStyle = BorderStyle.None;
        inner.Dock = DockStyle.Fill;

        var p = new FlatBorderPanel(theme)
        {
            Padding = padding ?? new Padding(2),
        };
        p.Controls.Add(inner);
        return p;
    }

    // ── Per-control theme application ────────────────────────────────────────

    /// <summary>Applies theme background and foreground to a <see cref="TextBox"/>.</summary>
    public static void ApplyTheme(this TextBox tb, Theme theme)
    {
        tb.BackColor = theme.EditorBackground;
        tb.ForeColor = theme.Text;
    }

    /// <summary>Applies theme background and foreground to a <see cref="RichTextBox"/>.</summary>
    public static void ApplyTheme(this RichTextBox rtb, Theme theme)
    {
        rtb.BackColor = theme.EditorBackground;
        rtb.ForeColor = theme.Text;
    }

    /// <summary>Applies theme background and foreground to a <see cref="ListBox"/>.</summary>
    public static void ApplyTheme(this ListBox lb, Theme theme)
    {
        lb.BackColor = theme.EditorBackground;
        lb.ForeColor = theme.Text;
        lb.BorderStyle = BorderStyle.None;
    }

    /// <summary>Applies theme background and foreground to a <see cref="ListView"/>.</summary>
    public static void ApplyTheme(this ListView lv, Theme theme)
    {
        lv.BackColor = theme.EditorBackground;
        lv.ForeColor = theme.Text;
        lv.BorderStyle = BorderStyle.None;
    }

    /// <summary>
    /// Applies flat theme colours to a <see cref="ComboBox"/>.
    /// Note: WinForms ComboBox background on Windows 11 is partially owner-drawn,
    /// so only the text area reliably reflects BackColor.
    /// </summary>
    public static void ApplyTheme(this ComboBox cb, Theme theme)
    {
        cb.BackColor = theme.EditorBackground;
        cb.ForeColor = theme.Text;
        cb.FlatStyle = FlatStyle.Flat;
    }

    /// <summary>Applies theme colours to a <see cref="NumericUpDown"/>.</summary>
    public static void ApplyTheme(this NumericUpDown nud, Theme theme)
    {
        nud.BackColor = theme.EditorBackground;
        nud.ForeColor = theme.Text;
        nud.BorderStyle = BorderStyle.FixedSingle; // None loses the spin buttons
    }

    /// <summary>
    /// Applies a full flat theme to a <see cref="DataGridView"/>.
    /// Removes all gradients, 3D effects, and system colours.
    /// </summary>
    public static void ApplyTheme(this DataGridView dgv, Theme theme)
    {
        dgv.BackgroundColor        = theme.EditorBackground;
        dgv.GridColor              = theme.Border;
        dgv.BorderStyle            = BorderStyle.None;
        dgv.CellBorderStyle        = DataGridViewCellBorderStyle.SingleHorizontal;
        dgv.RowHeadersBorderStyle  = DataGridViewHeaderBorderStyle.None;
        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        dgv.EnableHeadersVisualStyles = false;

        dgv.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor         = theme.EditorBackground,
            ForeColor         = theme.Text,
            SelectionBackColor = theme.ButtonHoverBackground,
            SelectionForeColor = theme.Text,
        };
        dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor          = Color.FromArgb(
                theme.IsLight ? 245 : 35,
                theme.IsLight ? 245 : 35,
                theme.IsLight ? 245 : 35),
            ForeColor          = theme.Text,
            SelectionBackColor = theme.ButtonHoverBackground,
            SelectionForeColor = theme.Text,
        };
        dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor         = theme.MenuBackground,
            ForeColor         = theme.Text,
            SelectionBackColor = theme.MenuBackground,
            SelectionForeColor = theme.Text,
            Font              = new Font(dgv.Font, FontStyle.Regular),
        };
        dgv.RowHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor          = theme.MenuBackground,
            ForeColor          = theme.Text,
            SelectionBackColor = theme.ButtonHoverBackground,
            SelectionForeColor = theme.Text,
        };
        dgv.RowsDefaultCellStyle = dgv.DefaultCellStyle;
    }

    /// <summary>
    /// Applies flat theme to a <see cref="ProgressBar"/> using the themed accent
    /// colour where supported.
    /// </summary>
    public static void ApplyTheme(this ProgressBar pb, Theme theme)
    {
        pb.BackColor  = theme.MenuBackground;
        pb.ForeColor  = theme.Accent;
        // Marquee bars don't honour ForeColor; the system draws them.
    }

    // ── Flat GroupBox replacement ─────────────────────────────────────────────

    /// <summary>
    /// Creates a flat-styled labelled section that replaces a <see cref="GroupBox"/>.
    /// Returns the outer <see cref="Panel"/> (equivalent to GroupBox) and the
    /// <see cref="Label"/> for the section title.
    ///
    /// Usage: add the returned panel where the GroupBox was; add child controls to
    /// <c>contentPanel</c> (also returned) instead of the GroupBox itself.
    /// </summary>
    public static (Panel outer, Panel contentPanel, Label titleLabel)
        CreateFlatSection(string title, Theme theme, DockStyle dock = DockStyle.None)
    {
        var titleLabel = new Label
        {
            Text      = title.ToUpperInvariant(),
            AutoSize  = true,
            Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = theme.IsLight
                ? Color.FromArgb(100, 100, 100)
                : Color.FromArgb(170, 170, 170),
            Margin    = new Padding(0, 0, 0, 4),
        };

        var contentPanel = new Panel { Dock = DockStyle.Fill };

        var titleRow = new Panel
        {
            Dock   = DockStyle.Top,
            Height = 20,
        };
        titleRow.Controls.Add(titleLabel);

        var outer = new Panel
        {
            Dock    = dock,
            Padding = new Padding(0, 4, 0, 4),
        };

        // Flat 1-px top border for visual grouping (no 3D chrome)
        outer.Paint += (s, e) =>
        {
            using var pen = new Pen(ThemeManager.Instance.CurrentTheme.Border, 1);
            e.Graphics.DrawLine(pen, 0, 0, outer.Width, 0);
        };

        outer.Controls.Add(contentPanel);
        outer.Controls.Add(titleRow);
        return (outer, contentPanel, titleLabel);
    }

    // ── Semantic status colours ───────────────────────────────────────────────

    /// <summary>Error / critical — visible on both light and dark themes.</summary>
    public static Color ErrorColor(Theme theme) =>
        theme.IsLight ? Color.FromArgb(196, 43, 28) : Color.FromArgb(255, 110, 100);

    /// <summary>Warning / high-risk — visible on both themes.</summary>
    public static Color WarningColor(Theme theme) =>
        theme.IsLight ? Color.FromArgb(190, 100, 0) : Color.FromArgb(255, 185, 80);

    /// <summary>Success / OK — visible on both themes.</summary>
    public static Color SuccessColor(Theme theme) =>
        theme.IsLight ? Color.FromArgb(0, 128, 55) : Color.FromArgb(80, 210, 130);

    /// <summary>Muted / informational — visible on both themes.</summary>
    public static Color MutedColor(Theme theme) =>
        theme.IsLight ? Color.FromArgb(100, 100, 100) : Color.FromArgb(170, 170, 170);

    /// <summary>Badge foreground — high contrast on coloured badge background.</summary>
    public static Color BadgeForeground(Theme theme) =>
        theme.IsLight ? Color.White : Color.FromArgb(240, 240, 240);

    // ── Button theming ───────────────────────────────────────────────────────

    /// <summary>
    /// Makes a <see cref="Button"/> fully flat with theme-aware hover/press states.
    /// Removes all borders and system-drawn glows.
    /// </summary>
    public static void ApplyFlatStyle(this Button btn, Theme theme,
        bool accentBackground = false)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize           = 0;
        // BorderSize = 0 hides the border; BorderColor = Transparent is not supported by WinForms

        bool isDark = !theme.IsLight;
        Color hoverBg = isDark
            ? Color.FromArgb(40, 255, 255, 255)
            : Color.FromArgb(30, 0, 0, 0);
        Color pressBg = isDark
            ? Color.FromArgb(70, 255, 255, 255)
            : Color.FromArgb(55, 0, 0, 0);

        if (accentBackground)
        {
            btn.BackColor = theme.Accent;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.MouseOverBackColor  = ControlPaint.Light(theme.Accent, 0.1f);
            btn.FlatAppearance.MouseDownBackColor  = ControlPaint.Dark(theme.Accent, 0.1f);
        }
        else
        {
            btn.BackColor = Color.Transparent;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.MouseOverBackColor  = hoverBg;
            btn.FlatAppearance.MouseDownBackColor  = pressBg;
        }
    }

    // ── Internal flat border panel ────────────────────────────────────────────

    /// <summary>
    /// A <see cref="Panel"/> that draws a single-pixel themed border around its
    /// client area. Subscribes to <see cref="ThemeManager.ThemeChanged"/> to keep
    /// the border colour current.
    /// </summary>
    public sealed class FlatBorderPanel : Panel
    {
        private Color _borderColor;

        public FlatBorderPanel(Theme theme)
        {
            _borderColor = theme.Border;
            ThemeManager.Instance.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(Theme theme)
        {
            _borderColor = theme.Border;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(_borderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }
    }
}

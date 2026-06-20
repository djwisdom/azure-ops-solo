using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Horizontal ruler drawn above the text editor.
///
/// Features:
///   • Column tick marks (minor/mid/major) that scroll with the editor.
///   • Cursor caret (▼) that follows the insertion point column.
///   • Column-guide marker: a coloured bar + label at <see cref="HighlightRichTextBox.GuideColumn"/>.
///   • Selection-span tint: subtle highlight over the selected column range.
///   • Left-click  → move the column guide to the clicked column.
///   • Right-click → preset menu (80 / 100 / 120 / hide guide).
///   • Hover tooltip showing the column number (1-based).
/// </summary>
public sealed class RulerPanel : Control
{
    private const int PanelHeight = 22;
    private HighlightRichTextBox? _editor;
    private int _hoverCol = -1;
    private readonly ToolTip _tip = new() { AutoPopDelay = 3000, InitialDelay = 500, ReshowDelay = 200 };

    public RulerPanel()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        TabStop     = false;
        DoubleBuffered = true;
        Height      = PanelHeight;
        Cursor      = Cursors.Default;
    }

    // ── Editor attachment ────────────────────────────────────────────────────

    public void AttachEditor(HighlightRichTextBox editor)
    {
        if (_editor != null)
        {
            _editor.HScroll          -= OnEditorChanged;
            _editor.VScroll          -= OnEditorChanged;
            _editor.FontChanged      -= OnEditorChanged;
            _editor.SelectionChanged -= OnEditorChanged;
        }
        _editor = editor;
        if (_editor != null)
        {
            _editor.HScroll          += OnEditorChanged;
            _editor.VScroll          += OnEditorChanged;
            _editor.FontChanged      += OnEditorChanged;
            _editor.SelectionChanged += OnEditorChanged;
        }
        Invalidate();
    }

    private void OnEditorChanged(object? sender, EventArgs e) => Invalidate();

    // ── Geometry helpers ─────────────────────────────────────────────────────

    private int MeasureCharWidth()
    {
        if (_editor == null) return 7;
        if (_editor.IsHandleCreated && _editor.TextLength >= 2)
        {
            try
            {
                int x0 = _editor.GetPositionFromCharIndex(0).X;
                int x1 = _editor.GetPositionFromCharIndex(1).X;
                int d  = x1 - x0;
                if (d > 0) return d;
            }
            catch { }
        }
        try
        {
            var sz = TextRenderer.MeasureText("W", _editor.Font,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            return Math.Max(1, sz.Width);
        }
        catch { return 7; }
    }

    private int GetOriginX()
    {
        if (_editor == null) return 1;
        try { return _editor.GetPositionFromCharIndex(0).X; }
        catch { return 1; }
    }

    /// <summary>0-based column of the insertion caret.</summary>
    private int GetCursorCol()
    {
        if (_editor == null) return 0;
        try
        {
            int sel   = _editor.SelectionStart;
            int line  = _editor.GetLineFromCharIndex(sel);
            int first = _editor.GetFirstCharIndexFromLine(line);
            return Math.Max(0, sel - first);
        }
        catch { return 0; }
    }

    /// <summary>0-based (startCol, endCol) of the current selection; (-1,-1) when empty.</summary>
    private (int start, int end) GetSelectionColRange()
    {
        if (_editor == null || _editor.SelectionLength == 0) return (-1, -1);
        try
        {
            int a  = _editor.SelectionStart;
            int b  = a + _editor.SelectionLength;
            int la = _editor.GetLineFromCharIndex(a);
            int lb = _editor.GetLineFromCharIndex(b);
            int ca = a - _editor.GetFirstCharIndexFromLine(la);
            int cb = b - _editor.GetFirstCharIndexFromLine(lb);
            // Multi-line: span from ca to end of visible ruler
            return (ca, la != lb ? int.MaxValue : cb);
        }
        catch { return (-1, -1); }
    }

    private int XFromCol(int col, int originX, int charWidth) => originX + col * charWidth;

    private int ColFromX(int screenX, int originX, int charWidth)
        => charWidth == 0 ? 0 : Math.Max(0, (screenX - originX + charWidth / 2) / charWidth);

    // ── Paint ────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g     = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        // Background + bottom separator
        using var bgBrush   = new SolidBrush(theme.EditorBackground);
        using var sepPen    = new Pen(Color.FromArgb(60, theme.Muted));
        g.FillRectangle(bgBrush, 0, 0, Width, Height);
        g.DrawLine(sepPen, 0, Height - 1, Width, Height - 1);

        if (_editor == null || !_editor.IsHandleCreated) return;

        int cw      = MeasureCharWidth();
        int originX = GetOriginX();

        // ── Selection span tint ──────────────────────────────────────────
        var (selA, selB) = GetSelectionColRange();
        if (selA >= 0)
        {
            int x1 = Math.Clamp(XFromCol(selA, originX, cw), 0, Width);
            int x2 = selB == int.MaxValue ? Width : Math.Clamp(XFromCol(selB, originX, cw), 0, Width);
            if (x2 > x1)
            {
                using var selBrush = new SolidBrush(Color.FromArgb(40,
                    theme.Accent.R, theme.Accent.G, theme.Accent.B));
                g.FillRectangle(selBrush, x1, 0, x2 - x1, Height - 1);
            }
        }

        // ── Hover column highlight ───────────────────────────────────────
        if (_hoverCol >= 0)
        {
            int hx = XFromCol(_hoverCol, originX, cw);
            if (hx >= 0 && hx + cw <= Width)
            {
                using var hoverBrush = new SolidBrush(Color.FromArgb(18, theme.Text));
                g.FillRectangle(hoverBrush, hx, 0, cw, Height - 1);
            }
        }

        // ── Tick marks + column labels ───────────────────────────────────
        int firstCol = originX >= 0 ? 0 : (int)Math.Ceiling(-originX / (double)cw);
        int lastCol  = firstCol + Width / cw + 2;

        using var minorPen   = new Pen(Color.FromArgb(70,  theme.Muted));
        using var midPen     = new Pen(Color.FromArgb(110, theme.Muted));
        using var majorPen   = new Pen(Color.FromArgb(150, theme.Muted));
        using var lblBrush   = new SolidBrush(Color.FromArgb(140, theme.Muted));
        using var lblFont    = new Font("Consolas", 7f);

        for (int col = firstCol; col <= lastCol; col++)
        {
            int x = XFromCol(col, originX, cw);
            if (x < 0 || x > Width) continue;

            if (col % 10 == 0)
            {
                g.DrawLine(majorPen, x, Height - 9, x, Height - 1);
                if (col > 0)
                    g.DrawString(col.ToString(), lblFont, lblBrush, x + 2, 1);
            }
            else if (col % 5 == 0)
                g.DrawLine(midPen,   x, Height - 5, x, Height - 1);
            else
                g.DrawLine(minorPen, x, Height - 3, x, Height - 1);
        }

        // ── Guide column marker ──────────────────────────────────────────
        if (_editor.ShowGuide)
        {
            int guideCol = _editor.GuideColumn;                 // 1-based
            int gx = XFromCol(guideCol - 1, originX, cw);      // to 0-based
            if (gx > 0 && gx < Width)
            {
                Color gc = _editor.GuideColor.A > 20
                    ? Color.FromArgb(220, _editor.GuideColor)
                    : Color.FromArgb(220, 90, 140, 210);

                using var guidePen = new Pen(gc, 1f);
                g.DrawLine(guidePen, gx, 0, gx, Height - 1);

                // Downward triangle at guide column (pointing down from top)
                DrawTriangle(g, gc, gx, 0, pointDown: true);

                // Floating label centred on gx
                using var gFont  = new Font("Consolas", 7f, FontStyle.Bold);
                using var gBrush = new SolidBrush(gc);
                string glbl = guideCol.ToString();
                var gsz = g.MeasureString(glbl, gFont);
                float lx = Math.Clamp(gx - gsz.Width / 2f, 1f, Width - gsz.Width - 1f);
                g.DrawString(glbl, gFont, gBrush, lx, 1f);
            }
        }

        // ── Cursor caret (▼ pointing down at bottom edge) ───────────────
        int cursorCol = GetCursorCol();
        int cx = XFromCol(cursorCol, originX, cw) + cw / 2;
        if (cx >= 0 && cx < Width)
        {
            var caretColor = Color.FromArgb(230, theme.Accent.R, theme.Accent.G, theme.Accent.B);
            DrawTriangle(g, caretColor, cx, Height - 1, pointDown: false);
        }
    }

    /// <summary>
    /// Draws a small filled equilateral-ish triangle.
    /// <paramref name="pointDown"/> = true  → apex points down, base at top (guide column marker).
    /// <paramref name="pointDown"/> = false → apex points down, base at top of bottom edge (cursor caret).
    /// Both are bottom-aligned to keep the ruler clean at the top.
    /// </summary>
    private static void DrawTriangle(Graphics g, Color color, int tipX, int baseY, bool pointDown)
    {
        const int Half = 4;
        const int H    = 5;
        Point[] pts = pointDown
            ? [new(tipX - Half, baseY),   new(tipX + Half, baseY),   new(tipX, baseY + H)]
            : [new(tipX - Half, baseY - H), new(tipX + Half, baseY - H), new(tipX, baseY)];
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, pts);
        g.SmoothingMode = SmoothingMode.None;
    }

    // ── Mouse ────────────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_editor == null) return;
        int col = ColFromX(e.X, GetOriginX(), MeasureCharWidth());
        if (col != _hoverCol)
        {
            _hoverCol = col;
            _tip.SetToolTip(this, $"Col {col + 1}");
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverCol = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_editor == null) return;
        int col1based = ColFromX(e.X, GetOriginX(), MeasureCharWidth()) + 1; // editor is 1-based

        if (e.Button == MouseButtons.Left)
        {
            _editor.GuideColumn = Math.Max(1, col1based);
            _editor.ShowGuide   = true;
            Invalidate();
        }
        else if (e.Button == MouseButtons.Right)
        {
            ShowGuideMenu(e.Location, col1based);
        }
    }

    private void ShowGuideMenu(Point loc, int clickedCol)
    {
        if (_editor == null) return;
        var menu  = new ContextMenuStrip();
        var theme = ThemeManager.Instance.CurrentTheme;

        void AddPreset(int c)
        {
            var item = new ToolStripMenuItem($"Guide at column {c}")
            {
                Checked = _editor.ShowGuide && _editor.GuideColumn == c
            };
            item.Click += (_, _) =>
            {
                _editor.GuideColumn = c;
                _editor.ShowGuide   = true;
                Invalidate();
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripMenuItem("Column guide") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        AddPreset(80);
        AddPreset(100);
        AddPreset(120);
        menu.Items.Add(new ToolStripSeparator());

        var hereItem = new ToolStripMenuItem($"Set here (col {clickedCol})");
        hereItem.Click += (_, _) =>
        {
            _editor.GuideColumn = Math.Max(1, clickedCol);
            _editor.ShowGuide   = true;
            Invalidate();
        };
        menu.Items.Add(hereItem);

        menu.Items.Add(new ToolStripSeparator());
        var hideItem = new ToolStripMenuItem("Hide guide") { Enabled = _editor.ShowGuide };
        hideItem.Click += (_, _) => { _editor.ShowGuide = false; Invalidate(); };
        menu.Items.Add(hideItem);

        menu.BackColor = theme.MenuBackground;
        menu.ForeColor = theme.Text;
        menu.Show(this, loc);
    }
}

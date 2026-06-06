using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class RulerPanel : Control
{
    private const int PanelHeight = 20;
    private HighlightRichTextBox? _editor;

    public RulerPanel()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        TabStop = false;
        DoubleBuffered = true;
        Height = PanelHeight;
    }

    public void AttachEditor(HighlightRichTextBox editor)
    {
        if (_editor != null)
        {
            _editor.HScroll -= OnEditorScroll;
            _editor.VScroll -= OnEditorScroll;
            _editor.FontChanged -= OnEditorFontChanged;
        }
        _editor = editor;
        if (_editor != null)
        {
            _editor.HScroll += OnEditorScroll;
            _editor.VScroll += OnEditorScroll;
            _editor.FontChanged += OnEditorFontChanged;
        }
        Invalidate();
    }

    private void OnEditorScroll(object? sender, EventArgs e) => Invalidate();
    private void OnEditorFontChanged(object? sender, EventArgs e) => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        // Always paint the background and bottom border — even when editor is empty
        using var bgBrush = new SolidBrush(theme.EditorBackground);
        g.FillRectangle(bgBrush, 0, 0, Width, Height);

        using var bottomPen = new Pen(Color.FromArgb(60, theme.Muted));
        g.DrawLine(bottomPen, 0, Height - 1, Width, Height - 1);

        if (_editor == null || !_editor.IsHandleCreated)
            return;

        // Measure one character width from the actual editor font.
        // When ≥2 chars exist, diff consecutive positions for pixel-perfect accuracy.
        // Falls back to TextRenderer measurement for empty or single-char files.
        int charWidth = MeasureCharWidth();

        // originX = screen X coordinate of column 0.
        // Positive when not scrolled (equals the editor's left margin, ~1–4 px).
        // Negative when scrolled right (col 0 is off-screen to the left).
        // GetPositionFromCharIndex(0) is safe for empty editors — returns cursor pos.
        int originX;
        try { originX = _editor.GetPositionFromCharIndex(0).X; }
        catch { originX = 1; }

        using var minorPen = new Pen(Color.FromArgb(80, theme.Muted));
        using var midPen = new Pen(Color.FromArgb(110, theme.Muted));
        using var majorPen = new Pen(Color.FromArgb(140, theme.Muted));
        using var labelBrush = new SolidBrush(Color.FromArgb(150, theme.Muted));
        using var labelFont = new Font("Consolas", 7);

        // Compute visible column range
        int firstCol = originX >= 0 ? 0 : (int)Math.Ceiling((double)(-originX) / charWidth);
        int lastCol = firstCol + (Width / charWidth) + 2;

        for (int col = firstCol; col <= lastCol; col++)
        {
            int x = originX + col * charWidth;
            if (x < 0 || x > Width) continue;

            if (col % 10 == 0)
            {
                g.DrawLine(majorPen, x, Height - 8, x, Height - 1);
                g.DrawString(col.ToString(), labelFont, labelBrush, x + 2, 2);
            }
            else if (col % 5 == 0)
            {
                g.DrawLine(midPen, x, Height - 5, x, Height - 1);
            }
            else
            {
                g.DrawLine(minorPen, x, Height - 3, x, Height - 1);
            }
        }
    }

    /// <summary>
    /// Returns the pixel width of one character in the attached editor.
    /// Prefers measuring from actual char positions when text is present.
    /// </summary>
    private int MeasureCharWidth()
    {
        if (_editor == null) return 7;

        // Most accurate: pixel distance between first two characters
        if (_editor.IsHandleCreated && _editor.TextLength >= 2)
        {
            try
            {
                int x0 = _editor.GetPositionFromCharIndex(0).X;
                int x1 = _editor.GetPositionFromCharIndex(1).X;
                int diff = x1 - x0;
                if (diff > 0) return diff;
            }
            catch { }
        }

        // Fallback: measure 'W' glyph width from the editor font
        try
        {
            var sz = TextRenderer.MeasureText(
                "W", _editor.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding);
            return Math.Max(1, sz.Width);
        }
        catch { return 7; }
    }
}

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
            _editor.HScroll -= OnEditorScroll;
        _editor = editor;
        if (_editor != null)
            _editor.HScroll += OnEditorScroll;
        Invalidate();
    }

    private void OnEditorScroll(object? sender, EventArgs e) => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        using var bgBrush = new SolidBrush(theme.EditorBackground);
        g.FillRectangle(bgBrush, 0, 0, Width, Height);

        using var bottomPen = new Pen(Color.FromArgb(60, theme.Muted));
        g.DrawLine(bottomPen, 0, Height - 1, Width, Height - 1);

        using var tickPen = new Pen(Color.FromArgb(100, theme.Muted));
        using var labelBrush = new SolidBrush(Color.FromArgb(140, theme.Muted));
        using var font = new Font("Consolas", 7);

        if (_editor == null || !_editor.IsHandleCreated || _editor.TextLength == 0)
            return;

        Point scrollPos = _editor.GetPositionFromCharIndex(0);
        int scrollOffset = Math.Max(0, scrollPos.X);
        int charWidth = 7;

        int firstCol = scrollOffset / charWidth;
        firstCol = (firstCol / 10) * 10;

        for (int col = firstCol; col <= firstCol + Width / charWidth + 10; col += 10)
        {
            int x = col * charWidth - scrollOffset;
            if (x < 0 || x > Width) continue;

            bool isMajor = col % 50 == 0;
            int tickHeight = isMajor ? 8 : 4;
            g.DrawLine(tickPen, x, Height - tickHeight, x, Height - 1);

            if (isMajor)
            {
                g.DrawString(col.ToString(), font, labelBrush, x + 2, 2);
            }
        }
    }
}
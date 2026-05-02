using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class ColumnGuidePanel : Control
{
    private RichTextBox? _editor;
    private int _guideColumn = 80;
    private bool _showGuide = true;

    private const int WM_ERASEBKGND = 0x0014;

    public ColumnGuidePanel()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        TabStop = false;
        Enabled = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    public RichTextBox? LinkedEditor
    {
        get => _editor;
        set
        {
            if (_editor != null)
                _editor.VScroll -= OnEditorScroll;
            _editor = value;
            if (_editor != null)
                _editor.VScroll += OnEditorScroll;
            Invalidate();
        }
    }

    private void OnEditorScroll(object? sender, EventArgs e) => Invalidate();

    public int GuideColumn
    {
        get => _guideColumn;
        set { if (value < 1) value = 1; _guideColumn = value; Invalidate(); }
    }

    public Color GuideColor { get; set; } = Color.FromArgb(100, 120, 120, 120);

    public bool ShowGuide
    {
        get => _showGuide;
        set { _showGuide = value; Visible = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!_showGuide || _editor == null || !_editor.IsHandleCreated) return;

        int charIndex = _editor.GetFirstCharIndexFromLine(0);
        if (charIndex < 0) return;
        int col = Math.Min(_guideColumn - 1, Math.Max(0, _editor.TextLength - 1));
        Point pos = _editor.GetPositionFromCharIndex(col);
        float x = pos.X;

        if (x < 0 || x > Width) return;

        using var pen = new Pen(GuideColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        e.Graphics.DrawLine(pen, x, 0, x, Height);
    }
}

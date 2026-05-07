using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class BreadcrumbPanel : Panel
{
    private string _filePath = "";
    private string _currentScope = "";
    private const int PanelHeight = 24;

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value ?? ""; Invalidate(); }
    }

    public string CurrentScope
    {
        get => _currentScope;
        set { _currentScope = value ?? ""; Invalidate(); }
    }

    public BreadcrumbPanel()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false;
        DoubleBuffered = true;
        Height = PanelHeight;
        Dock = DockStyle.Top;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        using var bgBrush = new SolidBrush(theme.EditorBackground);
        g.FillRectangle(bgBrush, 0, 0, Width, Height);

        using var bottomPen = new Pen(Color.FromArgb(60, theme.Muted));
        g.DrawLine(bottomPen, 0, Height - 1, Width, Height - 1);

        string fileName = string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);
        string display = fileName;
        if (!string.IsNullOrEmpty(_currentScope))
            display = fileName + " \u25B8 " + _currentScope;

        using var textBrush = new SolidBrush(Color.FromArgb(200, theme.Text));
        using var font = new System.Drawing.Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);
        g.DrawString(display, font, textBrush, 6, (Height - font.Height) / 2);
    }

    private const int WM_NCHITTEST = 0x0084;
    private static readonly IntPtr HTTRANSPARENT = new IntPtr(-1);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }
}

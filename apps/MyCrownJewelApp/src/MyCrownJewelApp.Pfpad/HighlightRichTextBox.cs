using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// RichTextBox that draws a full-width current line highlight.
/// </summary>
public class HighlightRichTextBox : RichTextBox
{
    private CurrentLineHighlightMode _highlightMode = CurrentLineHighlightMode.Off;
    private Color _highlightColor = Color.FromArgb(80, 60, 60, 60);
    private WinFormsTimer? _invalidateTimer;
    private int _lastLine = -1;

    private int _guideColumn = 80;
    private bool _showGuide = false;
    private Color _guideColor = Color.FromArgb(100, 120, 120, 120);

    public event EventHandler? CurrentLineHighlightModeChanged;

    public HighlightRichTextBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, false);
        BorderStyle = BorderStyle.None;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _invalidateTimer = new WinFormsTimer { Interval = 16 };
        _invalidateTimer.Tick += (s, e) => { _invalidateTimer.Stop(); Invalidate(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Set caret width to 2 pixels (0 = default, 1+ = custom width)
        const int EM_SETCARETWIDTH = 0x01F8;
        SendMessage(Handle, EM_SETCARETWIDTH, 0, 4);
    }

    [Category("Appearance")]
    [Description("Current line highlight mode.")]
    public CurrentLineHighlightMode CurrentLineHighlightMode
    {
        get => _highlightMode;
        set
        {
            if (_highlightMode != value)
            {
                _highlightMode = value;
                _lastLine = -1;
                Invalidate();
                CurrentLineHighlightModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [Category("Appearance")]
    [Description("Color of the current line highlight (supports alpha).")]
    public Color HighlightColor
    {
        get => _highlightColor;
        set { _highlightColor = value; Invalidate(); }
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        Invalidate();
    }

    private void DrawCurrentLineHighlight(Graphics g)
    {
        if (_highlightMode != CurrentLineHighlightMode.WholeLine || !Visible || IsDisposed || !Enabled)
            return;

        try
        {
            if (Lines == null || Lines.Length == 0 || SelectionStart < 0)
                return;

            if (_highlightMode != CurrentLineHighlightMode.WholeLine || !Focused)
                return;

            int currentLine = GetLineFromCharIndex(SelectionStart);
            _lastLine = currentLine;

            Point pt = GetPositionFromCharIndex(SelectionStart);
            if (pt.IsEmpty) return;

            int lineHeight = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
            if (lineHeight <= 0) lineHeight = 1;

            int lineY = pt.Y;
            if (lineY < 0) return;

            using var brush = new SolidBrush(_highlightColor);
            g.FillRectangle(brush, 0, lineY, ClientSize.Width, lineHeight);
        }
        catch { }
    }

    private void DrawColumnGuide(Graphics g)
    {
        if (!_showGuide || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int col = Math.Min(_guideColumn - 1, TextLength - 1);
            Point pos = GetPositionFromCharIndex(col);
            if (pos.X <= 0 || pos.X > ClientSize.Width) return;

            using var pen = new Pen(_guideColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawLine(pen, pos.X, 0, pos.X, ClientSize.Height);
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _invalidateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    [Category("Appearance")]
    public int GuideColumn
    {
        get => _guideColumn;
        set { _guideColumn = Math.Max(1, value); Invalidate(); }
    }

    [Category("Appearance")]
    public bool ShowGuide
    {
        get => _showGuide;
        set { _showGuide = value; Invalidate(); }
    }

    [Category("Appearance")]
    public Color GuideColor
    {
        get => _guideColor;
        set { _guideColor = value; Invalidate(); }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    protected override void WndProc(ref Message m)
    {
        const int WM_PAINT = 0x000F;
        const int WM_VSCROLL = 0x0115;
        const int WM_HSCROLL = 0x0114;
        const int WM_SIZE = 0x0005;
        const int WM_KEYUP = 0x0101;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_SETFOCUS = 0x0007;
        const int WM_KILLFOCUS = 0x0008;

        switch (m.Msg)
        {
            case WM_PAINT:
                {
                    base.WndProc(ref m);
                    IntPtr hdc = GetDC(Handle);
                    if (hdc != IntPtr.Zero)
                    {
                        using var g = Graphics.FromHdc(hdc);
                        DrawCurrentLineHighlight(g);
                        DrawColumnGuide(g);
                        ReleaseDC(Handle, hdc);
                    }
                    return;
                }
            case WM_VSCROLL:
            case WM_HSCROLL:
            case WM_SIZE:
            case WM_KEYUP:
            case WM_LBUTTONUP:
            case WM_MOUSEMOVE:
            case WM_MOUSEWHEEL:
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
                _invalidateTimer?.Stop();
                _invalidateTimer?.Start();
                break;
        }

        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}

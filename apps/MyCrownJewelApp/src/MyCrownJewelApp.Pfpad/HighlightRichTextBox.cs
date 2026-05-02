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
        SendMessage(Handle, EM_SETCARETWIDTH, 0, 2);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _invalidateTimer?.Dispose();
        }
        base.Dispose(disposing);
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

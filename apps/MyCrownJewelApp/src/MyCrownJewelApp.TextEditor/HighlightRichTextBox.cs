using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.TextEditor;

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

    [Category("Appearance"), Description("Current line highlight mode.")]
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
                    // Let base paint text first
                    base.WndProc(ref m);
                    // Overlay highlight using same DC
                    PAINTSTRUCT ps = new PAINTSTRUCT();
                    IntPtr hdc = BeginPaint(Handle, ref ps);
                    if (hdc != IntPtr.Zero)
                    {
                        using var g = Graphics.FromHdc(hdc);
                        DrawCurrentLineHighlight(g);
                        EndPaint(Handle, ref ps);
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

    private void DrawCurrentLineHighlight(Graphics g)
    {
        if (_highlightMode != CurrentLineHighlightMode.WholeLine || !Visible || IsDisposed || !Enabled)
            return;

        try
        {
            int currentLine = GetLineFromCharIndex(SelectionStart);
            if (currentLine < 0 || currentLine >= Lines.Length) return;

            _lastLine = currentLine;

            int lineHeight = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
            if (lineHeight <= 0) lineHeight = 1;

            // Compute base Y from first visible line (stable positioning)
            int firstVisible = SendMessage(Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (firstVisible < 0) firstVisible = 0;
            int baseY = 0;
            int firstCharIdx = GetFirstCharIndexFromLine(firstVisible);
            if (firstCharIdx >= 0)
            {
                Point pt0 = GetPositionFromCharIndex(firstCharIdx);
                if (pt0 != Point.Empty) baseY = pt0.Y;
            }

            int y = baseY + (currentLine - firstVisible) * lineHeight;
            if (y < 0) return;

            using var brush = new SolidBrush(_highlightColor);
            g.FillRectangle(brush, 0, y, ClientSize.Width, lineHeight);
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
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public string? lpszClass;
        public string? lpszString;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}

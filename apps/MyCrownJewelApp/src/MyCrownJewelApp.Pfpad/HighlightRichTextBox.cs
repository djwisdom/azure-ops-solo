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
    private FoldingManager? _foldingManager;
    private Color _foldLineColor = Color.FromArgb(80, 80, 80);

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
        UpdateCaretWidth();
    }

    private void UpdateCaretWidth()
    {
        if (!IsHandleCreated) return;
        const int EM_SETCARETWIDTH = 0x01F8;
        int width = Math.Max(1, (int)(4 * ZoomFactor));
        SendMessage(Handle, EM_SETCARETWIDTH, 0, width);
    }

    public void SyncCaretWidth() => UpdateCaretWidth();

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
            // Get first character index on line 0 — this is the reference column origin
            int firstCharIdx = GetFirstCharIndexFromLine(0);
            if (firstCharIdx < 0) return;

            Point basePos = GetPositionFromCharIndex(firstCharIdx);

            // Measure character width from two adjacent characters on line 0
            int charWidth = 8;
            if (firstCharIdx + 1 < TextLength)
            {
                Point nextPos = GetPositionFromCharIndex(firstCharIdx + 1);
                charWidth = Math.Max(1, nextPos.X - basePos.X);
            }

            int guideX = basePos.X + (_guideColumn - 1) * charWidth;
            if (guideX <= 0 || guideX > ClientSize.Width) return;

            using var pen = new Pen(_guideColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawLine(pen, guideX, 0, guideX, ClientSize.Height);
        }
        catch { }
    }

    private void DrawFoldBracketLines(Graphics g)
    {
        if (_foldingManager == null || !IsHandleCreated) return;
        try
        {
            int charWidth = 8;
            int firstCharIdx = TextLength > 0 ? GetFirstCharIndexFromLine(0) : -1;
            if (firstCharIdx >= 0 && firstCharIdx + 1 < TextLength)
            {
                Point p0 = GetPositionFromCharIndex(firstCharIdx);
                Point p1 = GetPositionFromCharIndex(firstCharIdx + 1);
                charWidth = Math.Max(1, p1.X - p0.X);
                if (charWidth < 1) charWidth = 8;
            }

            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));

            foreach (var region in _foldingManager.GetAllRegions())
            {
                if (region.IsCollapsed) continue;

                // Find the brace column on the open line
                string openText = GetLineTextFromLines(region.OpenLine);
                int braceCol = openText.LastIndexOf('{');
                if (braceCol < 0) continue;

                Point openPos = GetPositionFromCharIndex(
                    GetFirstCharIndexFromLine(region.OpenLine) + braceCol);
                Point closePos = GetPositionFromCharIndex(
                    GetFirstCharIndexFromLine(region.CloseLine));

                int x = openPos.X + charWidth / 2;
                if (x < 0 || x > ClientSize.Width) continue;

                int yTop = openPos.Y;
                int yBottom = closePos.Y + lineH - 1;

                using var pen = new Pen(_foldLineColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
                g.DrawLine(pen, x, yTop, x, yBottom);

                // Small horizontal cap at bottom
                g.DrawLine(pen, x, yBottom, x + charWidth / 2, yBottom);
            }
        }
        catch { }
    }

    private string GetLineTextFromLines(int lineIndex)
    {
        try
        {
            if (Lines != null && lineIndex < Lines.Length)
                return Lines[lineIndex] ?? "";
        }
        catch { }
        return "";
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

    [Browsable(false)]
    public FoldingManager? FoldingManager
    {
        get => _foldingManager;
        set { _foldingManager = value; Invalidate(); }
    }

    [Category("Appearance")]
    public Color FoldLineColor
    {
        get => _foldLineColor;
        set { _foldLineColor = value; Invalidate(); }
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
                        DrawFoldBracketLines(g);
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

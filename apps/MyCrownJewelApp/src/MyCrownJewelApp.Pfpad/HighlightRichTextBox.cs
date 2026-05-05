using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

public class HighlightRichTextBox : RichTextBox
{
    private CurrentLineHighlightMode _highlightMode = CurrentLineHighlightMode.Off;
    private Color _highlightColor = Color.FromArgb(80, 60, 60, 60);
    private WinFormsTimer? _invalidateTimer;
    private WinFormsTimer? _caretBlinkTimer;
    private bool _caretVisible = true;

    private int _guideColumn = 80;
    private bool _showGuide = false;
    private Color _guideColor = Color.FromArgb(100, 120, 120, 120);
    private FoldingManager? _foldingManager;
    private Color _foldLineColor = Color.FromArgb(80, 80, 80);

    private List<(int start, int length, Color color)> _squiggles = new();
    public void SetSquiggles(List<(int start, int length, Color color)> squiggles)
    {
        _squiggles = squiggles ?? new();
        Invalidate();
    }

    private static readonly HashSet<char> _openBraces = new() { '{', '[', '(' };
    private static readonly Dictionary<char, char> _bracePairs = new()
    {
        ['{'] = '}',
        ['['] = ']',
        ['('] = ')',
        ['}'] = '{',
        [']'] = '[',
        [')'] = '('
    };

    public event EventHandler? CurrentLineHighlightModeChanged;

    public HighlightRichTextBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, false);
        BorderStyle = BorderStyle.None;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _invalidateTimer = new WinFormsTimer { Interval = 16 };
        _invalidateTimer.Tick += (s, e) => { _invalidateTimer.Stop(); Invalidate(); };

        _caretBlinkTimer = new WinFormsTimer { Interval = 500 };
        _caretBlinkTimer.Tick += (s, e) => { _caretVisible = !_caretVisible; Invalidate(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HideCaret(Handle);
        _caretBlinkTimer?.Start();
    }

    private void UpdateCaretWidth()
    {
        if (!IsHandleCreated) return;
        HideCaret(Handle);
    }

    public void SyncCaretWidth() => UpdateCaretWidth();

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        HideCaret(Handle);
        _caretBlinkTimer?.Start();
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _caretBlinkTimer?.Stop();
        _caretVisible = false;
        Invalidate();
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        HideCaret(Handle);
        Invalidate();
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

    private Rectangle? GetCurrentLineRect()
    {
        if (_highlightMode != CurrentLineHighlightMode.WholeLine || !Visible || IsDisposed || !Enabled || !Focused)
            return null;
        if (Lines == null || Lines.Length == 0 || SelectionStart < 0)
            return null;

        Point pt = GetPositionFromCharIndex(SelectionStart);
        if (pt.IsEmpty || pt.Y < 0) return null;

        int lineHeight = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
        if (lineHeight <= 0) lineHeight = 1;

        return new Rectangle(0, pt.Y, ClientSize.Width, lineHeight);
    }

    private void DrawColumnGuide(Graphics g)
    {
        if (!_showGuide || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int firstCharIdx = GetFirstCharIndexFromLine(0);
            if (firstCharIdx < 0) return;

            Point basePos = GetPositionFromCharIndex(firstCharIdx);

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

    private void DrawBlockCursor(Graphics g)
    {
        try
        {
            int pos = SelectionStart;
            if (pos > TextLength) return;
            int line = GetLineFromCharIndex(pos);
            int lineStart = GetFirstCharIndexFromLine(line);
            Point pt = GetPositionFromCharIndex(pos);
            if (pt.IsEmpty) return;
            int charW = Math.Max(1, (int)(8 * ZoomFactor));
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            using var brush = new SolidBrush(ForeColor);
            g.FillRectangle(brush, pt.X, pt.Y, charW, lineH);
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

            int firstVisLine = GetLineFromCharIndex(GetCharIndexFromPosition(new Point(0, 0)));
            int lastVisLine = GetLineFromCharIndex(GetCharIndexFromPosition(new Point(0, ClientSize.Height)));

            using var pen = new Pen(_foldLineColor, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            foreach (var region in _foldingManager.GetAllRegions())
            {
                if (region.IsCollapsed) continue;
                if (region.CloseLine < firstVisLine || region.OpenLine > lastVisLine)
                    continue;

                string closeText = GetLineTextFromLines(region.CloseLine);
                int braceCol = closeText.LastIndexOf('}');
                if (braceCol < 0) continue;

                Point closePos = GetPositionFromCharIndex(
                    GetFirstCharIndexFromLine(region.CloseLine) + braceCol);
                Point openPos = GetPositionFromCharIndex(
                    GetFirstCharIndexFromLine(region.OpenLine));

                int x = closePos.X - 1;
                if (x < 0) x = 0;
                if (x < 0 || x > ClientSize.Width) continue;

                int yTop = openPos.Y + lineH;
                int yBottom = closePos.Y;

                g.DrawLine(pen, x, yTop, x, yBottom);
                g.DrawLine(pen, x, yBottom, closePos.X + charWidth / 2, yBottom);
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

    private static int? FindMatchingBrace(string text, int pos)
    {
        if (pos < 0 || pos >= text.Length) return null;
        char c = text[pos];
        if (!_bracePairs.ContainsKey(c)) return null;

        char open, close;
        int dir;
        if (_openBraces.Contains(c))
        {
            open = c;
            close = _bracePairs[c];
            dir = 1;
        }
        else
        {
            close = c;
            open = _bracePairs[c];
            dir = -1;
        }

        int depth = 0;
        int i = pos;
        while (i >= 0 && i < text.Length)
        {
            if (text[i] == open)
            {
                if (dir == 1) depth++;
                else { depth--; if (depth == 0) return i; }
            }
            else if (text[i] == close)
            {
                if (dir == -1) depth++;
                else { depth--; if (depth == 0) return i; }
            }
            i += dir;
        }
        return null;
    }

    private void DrawMatchingBraces(Graphics g)
    {
        if (!Focused || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int pos = SelectionStart;
            if (pos < 0 || pos >= TextLength) return;

            int? match = FindMatchingBrace(Text, pos);
            if (match == null || match.Value == pos) return;

            var theme = ThemeManager.Instance.CurrentTheme;
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));

            Color rectColor = theme.IsLight ? Color.FromArgb(180, 0, 0, 0) : Color.FromArgb(200, 255, 255, 255);

            DrawBraceRect(g, pos, lineH, rectColor);
            DrawBraceRect(g, match.Value, lineH, rectColor);
        }
        catch { }
    }

    private void DrawSquiggles(Graphics g)
    {
        if (_squiggles.Count == 0 || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            using var pen = new Pen(Color.Empty, 1);

            foreach (var (start, length, color) in _squiggles)
            {
                if (start < 0 || start >= TextLength) continue;
                int end = Math.Min(start + length, TextLength);
                if (end <= start) continue;

                Point startPt = GetPositionFromCharIndex(start);
                Point endPt = GetPositionFromCharIndex(end);
                if (startPt.IsEmpty) continue;

                int y = startPt.Y + lineH - 2;
                int x1 = startPt.X;
                int x2 = end != start ? endPt.X : startPt.X + 8;

                pen.Color = color;
                int waveLen = Math.Max(4, x2 - x1);
                int segments = Math.Max(1, waveLen / 4);
                for (int sx = 0; sx < segments && x1 + sx * 4 < x2; sx++)
                {
                    int px = x1 + sx * 4;
                    int py = y + (sx % 2 == 0 ? 0 : 2);
                    int nx = Math.Min(px + 4, x2);
                    g.DrawLine(pen, px, py, nx, py + (sx % 2 == 0 ? 2 : -2));
                }
            }
        }
        catch { }
    }

    private void DrawBraceRect(Graphics g, int pos, int lineH, Color color)
    {
        Point pt = GetPositionFromCharIndex(pos);
        if (pt.IsEmpty) return;

        int charW = 1;
        if (pos + 1 < TextLength)
        {
            Point nextPt = GetPositionFromCharIndex(pos + 1);
            charW = Math.Max(1, nextPt.X - pt.X);
        }

        using var pen = new Pen(color, 2);
        g.DrawRectangle(pen, pt.X, pt.Y, charW - 1, lineH - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _invalidateTimer?.Dispose();
            _caretBlinkTimer?.Dispose();
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

    [DllImport("gdi32.dll")]
    private static extern int BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);

    [DllImport("user32.dll")]
    private static extern bool CreateCaret(IntPtr hWnd, IntPtr hBitmap, int nWidth, int nHeight);

    [DllImport("user32.dll")]
    private static extern bool DestroyCaret();

    [DllImport("user32.dll")]
    private static extern bool ShowCaret(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool HideCaret(IntPtr hWnd);

    private const int SRCCOPY = 0x00CC0020;

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
                    if (IsDisposed || !IsHandleCreated) return;

                    IntPtr hdc = GetDC(Handle);
                    if (hdc == IntPtr.Zero) return;

                    try
                    {
                        using var g = Graphics.FromHdc(hdc);
                        var lineRect = GetCurrentLineRect();
                        if (lineRect.HasValue)
                        {
                            var r = lineRect.Value;
                            if (r.Height > 0 && r.Width > 0)
                            {
                                using var textBmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
                                using (var bmpG = Graphics.FromImage(textBmp))
                                {
                                    IntPtr bmpHdc = bmpG.GetHdc();
                                    BitBlt(bmpHdc, 0, 0, r.Width, r.Height, hdc, r.X, r.Y, SRCCOPY);
                                    bmpG.ReleaseHdc(bmpHdc);
                                }

                                using var hlBrush = new SolidBrush(_highlightColor);
                                g.FillRectangle(hlBrush, r);

                                var attrs = new ImageAttributes();
                                attrs.SetColorKey(BackColor, BackColor);
                                g.DrawImage(textBmp, r, 0, 0, r.Width, r.Height, GraphicsUnit.Pixel, attrs);
                            }
                        }

                        DrawColumnGuide(g);
                        DrawFoldBracketLines(g);
                        DrawMatchingBraces(g);
                        DrawSquiggles(g);

                        if (_caretVisible && Focused && IsHandleCreated && !IsDisposed)
                            DrawBlockCursor(g);
                    }
                    finally
                    {
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

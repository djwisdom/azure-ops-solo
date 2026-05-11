using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

public class HighlightRichTextBox : RichTextBox
{
    private CurrentLineHighlightMode _highlightMode = CurrentLineHighlightMode.Off;
    private Color _highlightColor = Color.FromArgb(80, 60, 60, 60);
    private bool _scrollInProgress;
    private System.Windows.Forms.Timer? _scrollDebounceTimer;
        private WinFormsTimer? _caretBlinkTimer;
        private bool _caretVisible = true;

        private int _guideColumn = 80;
        private bool _showGuide = false;
        private Color _guideColor = Color.FromArgb(100, 120, 120, 120);
        private FoldingManager? _foldingManager;
        private Color _foldLineColor = Color.FromArgb(80, 80, 80);
        private List<(int start, int length, Color color)> _squiggles = new();
        private Dictionary<int, string> _lineMessages = new();
        private List<int> _extraCarets = new();

        // Rainbow brackets
        private bool _rainbowBracketsEnabled;
        private RainbowBracketResult? _lastBracketResult;
        private Color[] _bracketPalette = RainbowBracketEngine.DefaultPalette;
        private bool _useHighContrastPalette;
        private bool _showAccessoryGuides;
        private int _bracketVersion;
        private int _lastAppliedVersion = -1;
        private WinFormsTimer? _bracketDebounceTimer;
        private RainbowBracketResult? _renderedResult;
        private bool _isApplyingBrackets;

        // Hover line highlight
        private int _hoverLine = -1;
        private bool _hoverLineHighlightEnabled = false;

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
        BorderStyle = BorderStyle.None;
        Margin = new Padding(0);
        Padding = new Padding(0);

        _scrollDebounceTimer = new WinFormsTimer { Interval = 80 };
        _scrollDebounceTimer.Tick += (s, e) =>
        {
            _scrollDebounceTimer.Stop();
            _scrollInProgress = false;
                        Invalidate();
        };

        _caretBlinkTimer = new WinFormsTimer { Interval = 500 };
        _caretBlinkTimer.Tick += (s, e) =>
        {
            _caretVisible = !_caretVisible;
            Invalidate();
        };

        _bracketDebounceTimer = new WinFormsTimer { Interval = 150 };
        _bracketDebounceTimer.Tick += (s, e) =>
        {
            _bracketDebounceTimer.Stop();
            if (TextLength > 100000)
            {
                _renderedResult = null;
                return;
            }
            _renderedResult = RainbowBracketEngine.Parse(Text, 0);
            Invalidate();
        };
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
        if (_scrollInProgress) return;
        HideCaret(Handle);
                Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
                if (_rainbowBracketsEnabled)
        {
            _bracketDebounceTimer?.Stop();
            _bracketDebounceTimer?.Start();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
                if (_rainbowBracketsEnabled)
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
        set
        {
            _highlightColor = value;
                        Invalidate();
        }
    }

    #region Rainbow Brackets API

    public void EnableRainbowBrackets(bool enable)
    {
        if (_rainbowBracketsEnabled == enable) return;
        _rainbowBracketsEnabled = enable;
        if (enable)
        {
            _bracketDebounceTimer?.Stop();
            ParseAndApplyBrackets();
        }
        else
        {
            _lastBracketResult = null;
            _renderedResult = null;
            _lastAppliedVersion = -1;
            Invalidate();
        }
    }

    public void SetPalette(Color[] palette)
    {
        _bracketPalette = palette ?? RainbowBracketEngine.DefaultPalette;
        if (_rainbowBracketsEnabled)
            RefreshBrackets();
    }

    public void SetHighContrastPalette(bool enable)
    {
        _useHighContrastPalette = enable;
        _bracketPalette = enable ? RainbowBracketEngine.HighContrastPalette : RainbowBracketEngine.DefaultPalette;
        if (_rainbowBracketsEnabled)
            RefreshBrackets();
    }

    public void SetAccessoryGuides(bool enable)
    {
        _showAccessoryGuides = enable;
        Invalidate();
    }

    public void RefreshBrackets()
    {
        if (!_rainbowBracketsEnabled || _isApplyingBrackets) return;
        _bracketDebounceTimer?.Stop();
        ParseAndApplyBrackets();
    }

    #endregion

    #region Rainbow Brackets Internal

    private void ParseAndApplyBrackets()
    {
        if (!IsHandleCreated || TextLength == 0 || _isApplyingBrackets) return;
        int version = Interlocked.Increment(ref _bracketVersion);
        var result = RainbowBracketEngine.Parse(Text, version);
        if (result.Version <= _lastAppliedVersion) return;
        _lastBracketResult = result;
        _renderedResult = result;
        _lastAppliedVersion = result.Version;
        _isApplyingBrackets = true;
        try
        {
            ApplyBracketColorsFormat(result);
        }
        finally
        {
            _isApplyingBrackets = false;
        }
    }

    private void ApplyBracketColorsFormat(RainbowBracketResult result)
    {
        if (!_rainbowBracketsEnabled || !IsHandleCreated || TextLength == 0) return;

        int firstVisLine = Math.Max(0, GetLineFromCharIndex(
            GetCharIndexFromPosition(new Point(0, 0))));
        int lastVisLine = Math.Min(Lines.Length - 1,
            GetLineFromCharIndex(GetCharIndexFromPosition(
                new Point(0, ClientSize.Height))) + 1);
        int startChar = GetFirstCharIndexFromLine(firstVisLine);
        int endChar = TextLength;
        if (lastVisLine + 1 < Lines.Length)
            endChar = Math.Min(TextLength, GetFirstCharIndexFromLine(lastVisLine + 1));
        if (startChar < 0) startChar = 0;

        int origSelStart = SelectionStart;
        int origSelLen = SelectionLength;
        int origFirstVis = (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE, 0, 0);

        SendMessage(Handle, WM_SETREDRAW, 0, 0);

        var cf = new CHARFORMAT2W
        {
            cbSize = Marshal.SizeOf<CHARFORMAT2W>(),
            dwMask = CFM_COLOR,
            dwEffects = 0
        };
        IntPtr cfPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CHARFORMAT2W>());

        try
        {
            foreach (var b in result.Brackets)
            {
                if (b.Position < startChar || b.Position >= endChar) continue;

                Color color = _bracketPalette[b.Depth % _bracketPalette.Length];
                cf.crTextColor = ColorTranslator.ToWin32(color);
                Marshal.StructureToPtr(cf, cfPtr, false);

                SendMessage(Handle, EM_SETSEL, b.Position, b.Position + 1);
                SendMessage(Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, cfPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(cfPtr);
        }

        SendMessage(Handle, WM_SETREDRAW, 1, 0);

        SendMessage(Handle, EM_SETSEL, origSelStart, origSelStart + origSelLen);
        int finalVis = (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        if (finalVis != origFirstVis)
            SendMessage(Handle, EM_LINESCROLL, 0, origFirstVis - finalVis);

        Invalidate();
    }

    private void DrawRainbowBracketOverlays(Graphics g)
    {
    }

    #endregion

    public void SetSquiggles(List<(int start, int length, Color color)> squiggles)
    {
        _squiggles = squiggles ?? new();
                Invalidate();
    }

    public void SetLineMessages(Dictionary<int, string> messages)
    {
        _lineMessages = messages ?? new Dictionary<int, string>();
                Invalidate();
    }

    public void SetExtraCarets(List<int> positions)
    {
        _extraCarets = positions ?? new List<int>();
                Invalidate();
    }

    private Rectangle? GetCurrentLineRect()
    {
        if (_highlightMode != CurrentLineHighlightMode.WholeLine && _highlightMode != CurrentLineHighlightMode.NumberAndWholeLine || !Visible || IsDisposed || !Enabled || !Focused)
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

            using var pen = new Pen(_guideColor, 1) { DashStyle = DashStyle.Dot };
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
            Point pt = GetPositionFromCharIndex(pos);
            if (pt.IsEmpty) return;
            int charW = Math.Max(1, (int)(8 * ZoomFactor));
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            using var brush = new SolidBrush(ForeColor);
            g.FillRectangle(brush, pt.X, pt.Y, charW, lineH);
        }
        catch { }
    }

    private void DrawExtraCarets(Graphics g)
    {
        if (_extraCarets.Count == 0 || !IsHandleCreated) return;
        try
        {
            int currentPrimary = SelectionStart;
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            int charW = Math.Max(1, (int)(8 * ZoomFactor));
            using var caretBrush = new SolidBrush(ForeColor);

            foreach (int pos in _extraCarets)
            {
                if (pos == currentPrimary || pos < 0 || pos >= TextLength) continue;
                Point pt = GetPositionFromCharIndex(pos);
                if (pt.IsEmpty) continue;
                g.FillRectangle(caretBrush, pt.X, pt.Y, charW, lineH);
            }
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

            using var pen = new Pen(_foldLineColor, 1);
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

    internal static int? FindMatchingBrace(string text, int pos)
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

            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));

            if (_rainbowBracketsEnabled && _renderedResult != null)
            {
                DrawRainbowBraceHighlights(g, pos, lineH);
            }
            else
            {
                // Check cursor position AND cursor-1 (cursor between a pair)
                int? match = FindMatchingBrace(Text, pos);
                int bracePos = pos;
                if (match == null && pos > 0)
                {
                    bracePos = pos - 1;
                    match = FindMatchingBrace(Text, bracePos);
                }
                if (match == null || match.Value == bracePos) return;

                Color rectColor = Color.FromArgb(200, 255, 255, 255);
                DrawBraceRect(g, bracePos, lineH, rectColor);
                DrawBraceRect(g, match.Value, lineH, rectColor);
            }
        }
        catch { }
    }

    private void DrawRainbowBraceHighlights(Graphics g, int caretPos, int lineH)
    {
        var result = _renderedResult;
        if (result == null) return;

        int? bracketIndex = result.FindBraceAt(caretPos);
        if (bracketIndex == null)
        {
            if (caretPos > 0) bracketIndex = result.FindBraceAt(caretPos - 1);
            if (bracketIndex == null)
            {
                // Find innermost enclosing bracket pair (smallest span)
                var brackets = result.Brackets;
                var sortedPairs = result.Pairs; // sorted by open position
                // Binary search for the last open <= caretPos
                int low = 0, high = sortedPairs.Count;
                while (low < high)
                {
                    int mid = (low + high) / 2;
                    var (openIdx, _) = sortedPairs[mid];
                    if (brackets[openIdx].Position <= caretPos) low = mid + 1;
                    else high = mid;
                }
                int candidateStart = low - 1;
                int minSpan = int.MaxValue;
                BracketInfo? bestOpen = null;
                BracketInfo? bestClose = null;
                for (int i = candidateStart; i >= 0; i--)
                {
                    var (openIdx, closeIdx) = sortedPairs[i];
                    var open = brackets[openIdx];
                    var close = brackets[closeIdx];
                    if (caretPos > open.Position && caretPos < close.Position)
                    {
                        int span = close.Position - open.Position;
                        if (span < minSpan)
                        {
                            minSpan = span;
                            bestOpen = open;
                            bestClose = close;
                        }
                    }
                    else if (open.Position < caretPos) break; // since sorted, earlier opens are smaller
                }
                if (bestOpen != null && bestClose != null)
                {
                    DrawBraceRect(g, bestOpen.Position, lineH, Color.Red);
                    DrawBraceRect(g, bestClose.Position, lineH, Color.Red);
                }
                return;
            }
        }

        var b = result.Brackets[bracketIndex.Value];
        Color pairColor = _bracketPalette[b.Depth % _bracketPalette.Length];

        if (b.PairState == BracketPairState.Matched && b.PairIndex.HasValue)
        {
            var pair = result.Brackets[b.PairIndex.Value];
            using var hlPen = new Pen(Color.FromArgb(220, 255, 255, 255), 2);
            DrawBraceRect(g, b.Position, lineH, Color.FromArgb(220, 255, 255, 255));
            DrawBraceRect(g, pair.Position, lineH, Color.FromArgb(220, 255, 255, 255));

            if (_showAccessoryGuides && b.Depth > 0)
            {
                using var guidePen = new Pen(Color.FromArgb(100, pairColor), 1) { DashStyle = DashStyle.Dot };
                int minPos = Math.Min(b.Position, pair.Position);
                int maxPos = Math.Max(b.Position, pair.Position);
                Point pt1 = GetPositionFromCharIndex(minPos);
                Point pt2 = GetPositionFromCharIndex(maxPos);
                if (!pt1.IsEmpty && !pt2.IsEmpty)
                {
                    int x = pt1.X;
                    g.DrawLine(guidePen, x, pt1.Y + lineH, x, pt2.Y);
                }
            }
        }
        else if (b.PairState == BracketPairState.Mismatched)
        {
            Point pt = GetPositionFromCharIndex(b.Position);
            if (!pt.IsEmpty)
            {
                using var pen = new Pen(Color.Red, 2);
                int charW = GetCharWidth(b.Position);
                int squigglyY = pt.Y + lineH - 2;
                DrawSquiggle(g, pen, pt.X, squigglyY, pt.X + charW, squigglyY + 2);
            }
        }
    }

    private int GetCharWidth(int pos)
    {
        if (pos + 1 < TextLength)
        {
            Point p0 = GetPositionFromCharIndex(pos);
            Point p1 = GetPositionFromCharIndex(pos + 1);
            return Math.Max(1, p1.X - p0.X);
        }
        return 8;
    }

    private static void DrawSquiggle(Graphics g, Pen pen, int x1, int y1, int x2, int y2)
    {
        int w = x2 - x1;
        int segments = Math.Max(1, w / 4);
        for (int i = 0; i < segments; i++)
        {
            int sx = x1 + (w * i / segments);
            int nx = x1 + (w * (i + 1) / segments);
            int sy = y1 + (i % 2 == 0 ? 0 : 2);
            int ny = y1 + (i % 2 == 0 ? 2 : 0);
            g.DrawLine(pen, sx, sy, nx, ny);
        }
    }

    private void DrawSquiggles(Graphics g)
    {
        if (_squiggles.Count == 0 || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));

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

                using var pen = new Pen(color, 1);
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

    private void DrawErrorLens(Graphics g)
    {
        if (_lineMessages.Count == 0 || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            int firstVisLine = GetLineFromCharIndex(GetCharIndexFromPosition(new Point(0, 0)));
            int lastVisLine = GetLineFromCharIndex(GetCharIndexFromPosition(new Point(0, ClientSize.Height)));

            using var lensFont = new Font("Consolas", 8);
            using var lensBrush = new SolidBrush(Color.FromArgb(160, Color.FromArgb(180, 160, 80)));

            foreach (var kvp in _lineMessages)
            {
                int line = kvp.Key;
                if (line < firstVisLine || line > lastVisLine) continue;

                int charIdx = GetFirstCharIndexFromLine(line);
                if (charIdx < 0 || charIdx >= TextLength) continue;

                int lineEnd = line + 1 < Lines.Length
                    ? GetFirstCharIndexFromLine(line + 1) - 1
                    : TextLength;

                if (lineEnd <= charIdx) continue;

                int lineLen = lineEnd - charIdx;
                Point lineEndPos = GetPositionFromCharIndex(Math.Min(charIdx + lineLen - 1, TextLength - 1));
                if (lineEndPos.IsEmpty) continue;

                int textX = lineEndPos.X + 24;
                if (textX + 10 >= ClientSize.Width) continue;

                int textY = lineEndPos.Y;
                string message = kvp.Value;
                if (message.Length > 60) message = message[..57] + "...";

                g.DrawString($"// {message}", lensFont, lensBrush, textX, textY);
            }
        }
        catch { }
    }

    private void DrawBraceRect(Graphics g, int pos, int lineH, Color color)
    {
        Point pt = GetPositionFromCharIndex(pos);
        if (pt.IsEmpty) return;

        int charW = GetCharWidth(pos);

        using var pen = new Pen(color, 2);
        g.DrawRectangle(pen, pt.X, pt.Y, charW - 1, lineH - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scrollDebounceTimer?.Dispose();
            _caretBlinkTimer?.Dispose();
            _bracketDebounceTimer?.Dispose();
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern int BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);

    [DllImport("user32.dll")]
    private static extern bool HideCaret(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_STYLE = -16;
    private const int WS_VSCROLL = 0x00200000;
    private const int WS_HSCROLL = 0x00100000;
    private const int SM_CXVSCROLL = 2;
    private const int SM_CYHSCROLL = 3;

    private const int SRCCOPY = 0x00CC0020;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_LINESCROLL = 0x00B6;
    private const int EM_SETSEL = 0x00B1;
    private const int EM_SETCHARFORMAT = 0x0444;
    private const int SCF_SELECTION = 0x0001;
    private const int CFM_COLOR = 0x40000000;

    private const int WM_SETREDRAW = 0x000B;
    private const int WM_NCPAINT = 0x0085;
    private const int WM_PRINTCLIENT = 0x0318;
    private const int PRF_CLIENT = 0x00000004;
    private const int PRF_ERASEBKGND = 0x00000008;
    private const int PRF_NONCLIENT = 0x00000002;

    private void PaintScrollbarCorner()
    {
        if (IsDisposed || !IsHandleCreated || !Visible) return;
        try
        {
            bool hasH = (GetWindowLong(Handle, GWL_STYLE) & WS_VSCROLL) != 0;
            bool hasV = (GetWindowLong(Handle, GWL_STYLE) & WS_VSCROLL) != 0;
            if (!hasH || !hasV) return;

            int scrollW = GetSystemMetrics(SM_CXVSCROLL);
            int scrollH = GetSystemMetrics(SM_CYHSCROLL);
            if (scrollW <= 0 || scrollH <= 0) return;

            int cx = ClientSize.Width - scrollW;
            int cy = ClientSize.Height - scrollH;
            if (cx < 0 || cy < 0) return;

            var theme = ThemeManager.Instance.CurrentTheme;
            using var g = Graphics.FromHwnd(Handle);
            using var brush = new SolidBrush(theme.EditorBackground);
            g.FillRectangle(brush, cx, cy, scrollW, scrollH);
        }
        catch { }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_PAINT = 0x000F;
        const int WM_VSCROLL = 0x0115;
        const int WM_HSCROLL = 0x0114;
        const int WM_SIZE = 0x0005;
        const int WM_KEYUP = 0x0101;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_SETFOCUS = 0x0007;
        const int WM_KILLFOCUS = 0x0008;

        switch (m.Msg)
        {
            case WM_NCPAINT:
                base.WndProc(ref m);
                if (!IsHandleCreated || DesignMode) return;
                try { PaintScrollbarCorner(); } catch { }
                return;
            case WM_PAINT:
                {
                    base.WndProc(ref m);
                    if (IsDisposed || !IsHandleCreated) return;

                    if (_scrollInProgress)
                    {
                                                return;
                    }

                    IntPtr hdc = GetDC(Handle);
                    if (hdc == IntPtr.Zero) return;

                    try
                    {
                        int w = ClientSize.Width;
                        int h = ClientSize.Height;
                        if (w <= 0 || h <= 0) return;

                        using var fullBmp = new Bitmap(w, h);
                        using (var g = Graphics.FromImage(fullBmp))
                        {
                            IntPtr bmpHdc = g.GetHdc();
                            BitBlt(bmpHdc, 0, 0, w, h, hdc, 0, 0, SRCCOPY);
                            g.ReleaseHdc(bmpHdc);
                        }

                         using (var g = Graphics.FromImage(fullBmp))
                         {
                             var lineRect = GetCurrentLineRect();
                             if (lineRect.HasValue)
                             {
                                 var r = lineRect.Value;
                                 using var hlBrush = new SolidBrush(_highlightColor);
                                 g.FillRectangle(hlBrush, r);

                                 using var textBmp = new Bitmap(r.Width, r.Height);
                                 using (var textG = Graphics.FromImage(textBmp))
                                 {
                                     IntPtr textHdc = textG.GetHdc();
                                     BitBlt(textHdc, 0, 0, r.Width, r.Height, hdc, r.X, r.Y, SRCCOPY);
                                     textG.ReleaseHdc(textHdc);
                                 }
                                 var attrs = new ImageAttributes();
                                 attrs.SetColorKey(BackColor, BackColor);
                                 g.DrawImage(textBmp, r, 0, 0, r.Width, r.Height, GraphicsUnit.Pixel, attrs);
                             }

                             // Draw hover line highlight
                             if (_hoverLineHighlightEnabled && _hoverLine >= 0)
                             {
                                 int lineHeight = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
                                 if (lineHeight <= 0) lineHeight = 1;
                                 
                                 int firstCharIdx = GetFirstCharIndexFromLine(_hoverLine);
                                 if (firstCharIdx >= 0)
                                 {
                                     Point pt = GetPositionFromCharIndex(firstCharIdx);
                                     if (!pt.IsEmpty)
                                     {
                                         int lineWidth = ClientSize.Width;
                                         using var hoverBrush = new SolidBrush(Color.FromArgb(60, 60, 60, 60));
                                         g.FillRectangle(hoverBrush, pt.X, pt.Y, lineWidth, lineHeight);
                                     }
                                 }
                             }

                             DrawColumnGuide(g);
                             DrawFoldBracketLines(g);
                             DrawMatchingBraces(g);
                             DrawSquiggles(g);
                             DrawErrorLens(g);

                             if (_caretVisible && Focused && IsHandleCreated && !IsDisposed && !DesignMode)
                                 DrawBlockCursor(g);

                             DrawExtraCarets(g);
                         }

                        using (var g = Graphics.FromHdc(hdc))
                            g.DrawImageUnscaled(fullBmp, 0, 0);
                    }
                    finally
                    {
                        ReleaseDC(Handle, hdc);
                    }
                    return;
                }
            case WM_VSCROLL:
            case WM_HSCROLL:
            case WM_MOUSEWHEEL:
                _scrollInProgress = true;
                _scrollDebounceTimer?.Stop();
                _scrollDebounceTimer?.Start();
                base.WndProc(ref m);
                return;
            case WM_SIZE:
            case WM_KEYUP:
            case WM_LBUTTONUP:
            case WM_SETFOCUS:
            case WM_KILLFOCUS:
                                break;
        }

        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct CHARFORMAT2W
    {
        public int cbSize;
        public int dwMask;
        public int dwEffects;
        public int yHeight;
        public int yOffset;
        public int crTextColor;
        public byte bCharSet;
        public byte bPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] szFaceName;
        public short wWeight;
        public short sSpacing;
        public int crBackColor;
        public int lcid;
        public int dwReserved;
        public short sStyle;
        public short wKerning;
        public byte bUnderlineType;
        public byte bAnimation;
        public byte bRevAuthor;
        public byte bUnderlineColor;
    }
}

using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad
{
    public partial class HighlightRichTextBox : RichTextBox
{
    private CurrentLineHighlightMode _highlightMode = CurrentLineHighlightMode.Off;
    private Color _highlightColor = Color.FromArgb(80, 60, 60, 60);
    private bool _scrollInProgress;
    private System.Windows.Forms.Timer? _scrollDebounceTimer;
        private System.Threading.PeriodicTimer? _blinkPeriodicTimer;
        private CancellationTokenSource? _blinkCts;
        private int _blinkIntervalMs = 530;
        private bool _caretVisible = true;
        private byte _caretAlpha = 255;
        private bool _smoothCaretBlink = true;
        private int _blinkStep;
        private static readonly byte[] _blinkSteps = [255, 0];

        private int _guideColumn = 80;
        private bool _showGuide = false;
        private Color _guideColor = Color.FromArgb(100, 120, 120, 120);
        private FoldingManager? _foldingManager;
        private Color _foldLineColor = Color.FromArgb(80, 80, 80);
        private List<(int start, int length, Color color)> _squiggles = new();
        private Dictionary<int, (string Message, int Severity)> _lineMessages = new();
        private List<int> _extraCarets = new();
        private bool _multiCursorEnabled = false;
        private readonly HashSet<int> _autoInsertedClosePositions = new();
        private bool _bracketTypeOverEnabled = false;

        // Word occurrence highlights
        private bool _showOccurrenceHighlights = true;
        private Color _occurrenceHighlightColor = Color.FromArgb(60, 180, 180, 180);
        private List<(int start, int length)> _wordOccurrenceRanges = new();
        private WinFormsTimer? _wordHighlightDebounce;

        // Whitespace glyphs
        private bool  _showWhitespace      = false;
        private Color _whitespaceGlyphColor = Color.FromArgb(110, 180, 180, 180);
        private bool _highlightTrailingWhitespace = false;
        private Color _trailingWhitespaceColor = Color.FromArgb(160, 255, 80, 80);
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

        // Cursor appearance
        private bool _lineHighlightWhenUnfocused = false;
        private string _cursorStyle = "line";
        private bool _cursorBlinkEnabled = true;
        private bool _showIndentGuides = false;
        private Color _indentGuideColor = Color.FromArgb(50, 120, 120, 120);
        private int _indentGuideTabSize = 4;
        private string _zoomIndicatorText = "";
        private byte _zoomIndicatorAlpha = 0;
        private WinFormsTimer? _zoomFadeTimer;

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

        _wordHighlightDebounce = new WinFormsTimer { Interval = 150 };
        _wordHighlightDebounce.Tick += (s, e) => { _wordHighlightDebounce!.Stop(); ComputeWordOccurrences(); };

        _zoomFadeTimer = new WinFormsTimer { Interval = 50 };
        _zoomFadeTimer.Tick += (s, e) =>
        {
            if (_zoomIndicatorAlpha <= 15) { _zoomFadeTimer!.Stop(); _zoomIndicatorAlpha = 0; }
            else _zoomIndicatorAlpha -= 15;
            Invalidate();
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HideCaret(Handle);
        StartBlinkTimer();
    }

    private void UpdateCaretWidth()
    {
        if (!IsHandleCreated) return;
        HideCaret(Handle);
    }

    public void SyncCaretWidth() => UpdateCaretWidth();

    private async Task BlinkLoopAsync(System.Threading.PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (IsDisposed || !IsHandleCreated) break;
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                        return;

                    if (_smoothCaretBlink)
                    {
                        _caretAlpha = _blinkSteps[_blinkStep % _blinkSteps.Length];
                        _blinkStep++;
                        _caretVisible = _caretAlpha > 0;
                    }
                    else
                    {
                        _caretVisible = !_caretVisible;
                        _caretAlpha = _caretVisible ? (byte)255 : (byte)0;
                    }

                    Invalidate();
                }));
            }
        }
        catch (OperationCanceledException) { }
    }

    private void StartBlinkTimer()
    {
        StopBlinkTimer();
        if (!_cursorBlinkEnabled)
        {
            _caretVisible = true;
            _caretAlpha = 255;
            _blinkStep = 0;
            return;
        }

        int interval = _smoothCaretBlink
            ? Math.Max(40, _blinkIntervalMs / Math.Max(1, _blinkSteps.Length))
            : _blinkIntervalMs;

        _blinkStep = 0;
        _caretVisible = true;
        _caretAlpha = 255;
        _blinkCts = new CancellationTokenSource();
        _blinkPeriodicTimer = new System.Threading.PeriodicTimer(TimeSpan.FromMilliseconds(interval));
        _ = BlinkLoopAsync(_blinkPeriodicTimer, _blinkCts.Token);
    }

    private void StopBlinkTimer()
    {
        _blinkCts?.Cancel();
        _blinkCts?.Dispose();
        _blinkCts = null;
        _blinkPeriodicTimer?.Dispose();
        _blinkPeriodicTimer = null;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        HideCaret(Handle);
        if (_cursorBlinkEnabled)
            StartBlinkTimer();
        else
        {
            _caretVisible = true;
            _caretAlpha = 255;
        }
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        StopBlinkTimer();
        _caretVisible = false;
        _caretAlpha = 0;
        Invalidate();
    }

    protected override void OnSelectionChanged(EventArgs e)
    {
        base.OnSelectionChanged(e);
        if (_scrollInProgress) return;
        HideCaret(Handle);
        if (_showOccurrenceHighlights)
        {
            _wordHighlightDebounce?.Stop();
            _wordHighlightDebounce?.Start();
        }
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        _autoInsertedClosePositions.RemoveWhere(position => position < 0 || position >= TextLength);
        if (_showOccurrenceHighlights)
        {
            _wordHighlightDebounce?.Stop();
            _wordHighlightDebounce?.Start();
        }
        if (_rainbowBracketsEnabled)
        {
            _bracketDebounceTimer?.Stop();
            _bracketDebounceTimer?.Start();
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (_bracketTypeOverEnabled && SelectionLength == 0)
        {
            char c = e.KeyChar;
            if (c == '}' || c == ')' || c == ']')
            {
                int pos = SelectionStart;
                if (_autoInsertedClosePositions.Contains(pos) && pos < TextLength && Text[pos] == c)
                {
                    _autoInsertedClosePositions.Remove(pos);
                    SelectionStart = pos + 1;
                    SelectionLength = 0;
                    e.Handled = true;
                    return;
                }
            }
        }

        base.OnKeyPress(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
                if (_rainbowBracketsEnabled)
            Invalidate();
    }

    [Category("Appearance")]
    [Description("Current line highlight mode.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HighlightColor
    {
        get => _highlightColor;
        set
        {
            _highlightColor = value;
                        Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Keep the current-line highlight visible when the editor loses keyboard focus.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool LineHighlightWhenUnfocused
    {
        get => _lineHighlightWhenUnfocused;
        set { _lineHighlightWhenUnfocused = value; Invalidate(); }
    }

    [Category("Appearance")]
    [Description("Caret shape: \"line\" (2 px bar), \"block\" (full character fill), or \"underline\" (2 px bar at bottom).")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CursorStyle
    {
        get => _cursorStyle;
        set { _cursorStyle = value ?? "line"; Invalidate(); }
    }

    [Category("Appearance")]
    [Description("Animate the caret by blinking. Disable for a steady non-blinking cursor.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CursorBlinkEnabled
    {
        get => _cursorBlinkEnabled;
        set
        {
            _cursorBlinkEnabled = value;
            if (Focused) StartBlinkTimer();
            else StopBlinkTimer();
            if (!value)
            {
                _caretVisible = true;
                _caretAlpha = 255;
            }
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Caret blink interval in milliseconds (200–1200).")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CursorBlinkRateMs
    {
        get => _blinkIntervalMs;
        set
        {
            _blinkIntervalMs = Math.Clamp(value, 200, 1200);
            if (Focused) StartBlinkTimer();
        }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SmoothCaretBlink
    {
        get => _smoothCaretBlink;
        set
        {
            _smoothCaretBlink = value;
            if (Focused) StartBlinkTimer();
        }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowOccurrenceHighlights
    {
        get => _showOccurrenceHighlights;
        set
        {
            _showOccurrenceHighlights = value;
            if (!value) _wordOccurrenceRanges.Clear();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OccurrenceHighlightColor
    {
        get => _occurrenceHighlightColor;
        set { _occurrenceHighlightColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowIndentGuides
    {
        get => _showIndentGuides;
        set { _showIndentGuides = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color IndentGuideColor
    {
        get => _indentGuideColor;
        set { _indentGuideColor = value; Invalidate(); }
    }

    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int IndentGuideTabSize
    {
        get => _indentGuideTabSize;
        set { _indentGuideTabSize = Math.Max(1, value); Invalidate(); }
    }

    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MultiCursorEnabled
    {
        get => _multiCursorEnabled;
        set
        {
            _multiCursorEnabled = value;
            if (!value) _extraCarets.Clear();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool BracketTypeOverEnabled
    {
        get => _bracketTypeOverEnabled;
        set => _bracketTypeOverEnabled = value;
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
                SendMessagePtr(Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, cfPtr);
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

    public void SetLineMessages(Dictionary<int, (string message, int severity)> messages)
    {
        _lineMessages = messages?.ToDictionary(kv => kv.Key, kv => (kv.Value.message, kv.Value.severity)) ?? new();
        Invalidate();
    }

    public void SetLineMessages(Dictionary<int, string> messages)
    {
        _lineMessages = messages?.ToDictionary(kv => kv.Key, kv => (kv.Value, 1)) ?? new();
        Invalidate();
    }

    public void SetExtraCarets(List<int> positions)
    {
        _extraCarets = positions ?? new List<int>();
        Invalidate();
    }

    public void RegisterAutoInsertedClose(int position)
    {
        if (position >= 0)
            _autoInsertedClosePositions.Add(position);
    }

    public void ClearAutoInsertedClose(int position)
    {
        _autoInsertedClosePositions.Remove(position);
    }

    public void ClearAutoInsertedCloses()
    {
        _autoInsertedClosePositions.Clear();
    }

    private Rectangle? GetCurrentLineRect()
    {
        if (_highlightMode != CurrentLineHighlightMode.WholeLine && _highlightMode != CurrentLineHighlightMode.NumberAndWholeLine || !Visible || IsDisposed || !Enabled)
            return null;
        if (!Focused && !_lineHighlightWhenUnfocused)
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
            using var brush = new SolidBrush(Color.FromArgb(_caretAlpha, ForeColor));
            g.FillRectangle(brush, pt.X, pt.Y, charW, lineH);
        }
        catch { }
    }

    private void DrawLineCursor(Graphics g)
    {
        try
        {
            int pos = SelectionStart;
            if (pos > TextLength) return;
            Point pt = GetPositionFromCharIndex(pos);
            if (pt.IsEmpty) return;
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            using var brush = new SolidBrush(Color.FromArgb(_caretAlpha, ForeColor));
            g.FillRectangle(brush, pt.X, pt.Y, 2, lineH);
        }
        catch { }
    }

    private void DrawUnderlineCursor(Graphics g)
    {
        try
        {
            int pos = SelectionStart;
            if (pos > TextLength) return;
            Point pt = GetPositionFromCharIndex(pos);
            if (pt.IsEmpty) return;
            int charW = Math.Max(4, (int)(8 * ZoomFactor));
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            using var brush = new SolidBrush(Color.FromArgb(_caretAlpha, ForeColor));
            g.FillRectangle(brush, pt.X, pt.Y + lineH - 2, charW, 2);
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

    private void ComputeWordOccurrences()
    {
        _wordOccurrenceRanges.Clear();
        if (!_showOccurrenceHighlights || TextLength == 0) { Invalidate(); return; }
        try
        {
            int selStart = SelectionStart;
            int selLen = SelectionLength;

            string word;
            if (selLen > 1 && selLen <= 100)
            {
                word = SelectedText.Trim();
                if (word.Length == 0 || word.Any(char.IsWhiteSpace)) { Invalidate(); return; }
            }
            else
            {
                string text = Text;
                if (selStart < 0 || selStart > text.Length) { Invalidate(); return; }
                int s = Math.Min(selStart, text.Length);
                while (s > 0 && (char.IsLetterOrDigit(text[s - 1]) || text[s - 1] == '_')) s--;
                int e2 = Math.Min(selStart, text.Length);
                while (e2 < text.Length && (char.IsLetterOrDigit(text[e2]) || text[e2] == '_')) e2++;
                if (e2 == s || e2 - s < 2) { Invalidate(); return; }
                word = text[s..e2];
            }

            string fullText = Text;
            int idx = 0;
            while ((idx = fullText.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                bool wordStart = idx == 0 || !(char.IsLetterOrDigit(fullText[idx - 1]) || fullText[idx - 1] == '_');
                bool wordEnd = idx + word.Length >= fullText.Length || !(char.IsLetterOrDigit(fullText[idx + word.Length]) || fullText[idx + word.Length] == '_');
                if (wordStart && wordEnd)
                    _wordOccurrenceRanges.Add((idx, word.Length));
                idx += word.Length;
                if (_wordOccurrenceRanges.Count > 500) break;
            }
        }
        catch { }

        Invalidate();
    }

    private void DrawWordOccurrences(Graphics g)
    {
        if (!_showOccurrenceHighlights || _wordOccurrenceRanges.Count == 0 || !IsHandleCreated) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            int selStart = SelectionStart;
            int selLen = SelectionLength;

            using var fillBrush = new SolidBrush(_occurrenceHighlightColor);
            using var borderPen = new Pen(Color.FromArgb(Math.Min(255, _occurrenceHighlightColor.A + 80),
                _occurrenceHighlightColor.R, _occurrenceHighlightColor.G, _occurrenceHighlightColor.B), 1);

            foreach (var (start, length) in _wordOccurrenceRanges)
            {
                if (start == selStart && length == selLen) continue;

                Point pt = GetPositionFromCharIndex(start);
                if (pt.IsEmpty || pt.Y < -lineH || pt.Y > ClientSize.Height) continue;
                int charW = length > 0 && start + length <= TextLength
                    ? Math.Max(1, GetPositionFromCharIndex(start + length).X - pt.X)
                    : GetCharWidth(start);
                g.FillRectangle(fillBrush, pt.X, pt.Y, charW, lineH);
                g.DrawRectangle(borderPen, pt.X, pt.Y, Math.Max(0, charW - 1), Math.Max(0, lineH - 1));
            }
        }
        catch { }
    }

    private void DrawIndentGuides(Graphics g)
    {
        if (!_showIndentGuides || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));
            int firstVis = (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (firstVis < 0) firstVis = 0;
            int visCount = ClientSize.Height / lineH + 2;
            int lastVis = Math.Min(Lines.Length - 1, firstVis + visCount);

            int charW = 8;
            int fi = GetFirstCharIndexFromLine(0);
            if (fi >= 0 && fi + 1 < TextLength)
            {
                Point p0 = GetPositionFromCharIndex(fi);
                Point p1 = GetPositionFromCharIndex(fi + 1);
                charW = Math.Max(1, p1.X - p0.X);
            }

            using var pen = new Pen(_indentGuideColor, 1) { DashStyle = DashStyle.Dot };

            for (int li = firstVis; li <= lastVis; li++)
            {
                if (li < 0 || li >= Lines.Length) continue;
                string line = Lines[li];
                if (line.Length == 0) continue;

                int indentCol = 0;
                foreach (char c in line)
                {
                    if (c == ' ') indentCol++;
                    else if (c == '\t') indentCol = ((indentCol / _indentGuideTabSize) + 1) * _indentGuideTabSize;
                    else break;
                }
                if (indentCol < _indentGuideTabSize) continue;

                int lineStart = GetFirstCharIndexFromLine(li);
                if (lineStart < 0) continue;
                Point lineOrigin = GetPositionFromCharIndex(lineStart);
                if (lineOrigin.IsEmpty) continue;

                for (int stop = _indentGuideTabSize; stop < indentCol; stop += _indentGuideTabSize)
                {
                    int x = lineOrigin.X + stop * charW;
                    if (x <= 0 || x >= ClientSize.Width) continue;
                    g.DrawLine(pen, x, lineOrigin.Y, x, lineOrigin.Y + lineH);
                }
            }
        }
        catch { }
    }

    public void ShowZoomIndicator(float zoomPercent)
    {
        _zoomIndicatorText = $"{(int)(zoomPercent * 100)}%";
        _zoomIndicatorAlpha = 220;
        _zoomFadeTimer?.Stop();
        _zoomFadeTimer?.Start();
        Invalidate();
    }

    private void DrawZoomIndicator(Graphics g)
    {
        if (_zoomIndicatorAlpha == 0 || string.IsNullOrEmpty(_zoomIndicatorText)) return;
        try
        {
            using var font = new Font(Font.FontFamily, 20f, FontStyle.Bold);
            var size = TextRenderer.MeasureText(_zoomIndicatorText, font);
            int padding = 12;
            int x = ClientSize.Width - size.Width - padding * 2 - 8;
            int y = ClientSize.Height - size.Height - padding * 2 - 8;
            if (x < 0 || y < 0) return;

            using var bgBrush = new SolidBrush(Color.FromArgb(_zoomIndicatorAlpha / 2, 0, 0, 0));
            using var textBrush = new SolidBrush(Color.FromArgb(_zoomIndicatorAlpha, 255, 255, 255));
            var rect = new Rectangle(x - padding, y - padding, size.Width + padding * 2, size.Height + padding * 2);
            g.FillRectangle(bgBrush, rect);
            g.DrawString(_zoomIndicatorText, font, textBrush, x, y);
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

            using var lensFont = new Font(Font.FontFamily, Math.Max(7f, Font.Size * 0.85f * ZoomFactor));

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
                if (textX + 30 >= ClientSize.Width) continue;

                int textY = lineEndPos.Y;
                var (msg, severity) = kvp.Value;
                string glyph = severity switch { 0 => "● ", 2 => "ℹ ", 3 => "· ", _ => "⚠ " };
                Color glyphColor = severity switch
                {
                    0 => Color.FromArgb(200, 220, 80, 80),
                    2 => Color.FromArgb(180, 100, 160, 220),
                    3 => Color.FromArgb(140, 160, 160, 160),
                    _ => Color.FromArgb(200, 220, 180, 80)
                };
                Color textColor = Color.FromArgb(150, glyphColor.R, glyphColor.G, glyphColor.B);

                int availW = ClientSize.Width - textX - 4;
                string renderMsg = msg.Length > 80 ? msg[..77] + "..." : msg;
                string display = glyph + renderMsg;
                if (TextRenderer.MeasureText(display, lensFont).Width > availW)
                {
                    while (display.Length > 4 && TextRenderer.MeasureText(display + "…", lensFont).Width > availW)
                        display = display[..^1];
                    display += "…";
                }

                using var glyphBrush = new SolidBrush(glyphColor);
                using var textBrush = new SolidBrush(textColor);
                var glyphSize = TextRenderer.MeasureText(glyph, lensFont);
                g.DrawString(glyph, lensFont, glyphBrush, textX, textY);
                g.DrawString(display[glyph.Length..], lensFont, textBrush, textX + glyphSize.Width - 4, textY);
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
            StopBlinkTimer();
            _bracketDebounceTimer?.Dispose();
            _wordHighlightDebounce?.Dispose();
            _zoomFadeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GuideColumn
    {
        get => _guideColumn;
        set { _guideColumn = Math.Max(1, value); Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGuide
    {
        get => _showGuide;
        set { _showGuide = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color GuideColor
    {
        get => _guideColor;
        set { _guideColor = value; Invalidate(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public FoldingManager? FoldingManager
    {
        get => _foldingManager;
        set { _foldingManager = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FoldLineColor
    {
        get => _foldLineColor;
        set { _foldLineColor = value; Invalidate(); }
    }

    // ── Whitespace glyph rendering ────────────────────────────────────────────

    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CtrlWheelZoomEnabled { get; set; } = true;

    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MouseWheelScrollLines { get; set; } = 0;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowWhitespace
    {
        get => _showWhitespace;
        set { if (_showWhitespace == value) return; _showWhitespace = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color WhitespaceGlyphColor
    {
        get => _whitespaceGlyphColor;
        set { _whitespaceGlyphColor = value; if (_showWhitespace) Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HighlightTrailingWhitespace
    {
        get => _highlightTrailingWhitespace;
        set { _highlightTrailingWhitespace = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrailingWhitespaceColor
    {
        get => _trailingWhitespaceColor;
        set { _trailingWhitespaceColor = value; Invalidate(); }
    }

    /// <summary>
    /// Draws crisp geometric whitespace glyphs directly onto the editor surface.
    /// Called from WM_PAINT after the RichTextBox has rendered its text.
    ///   Space → small anti-aliased dot centred in the character cell
    ///   Tab   → horizontal arrow (line + arrowhead) spanning the tab-stop cell
    /// </summary>
    private void DrawWhitespaceGlyphs(Graphics g)
    {
        if (!_showWhitespace || !IsHandleCreated || TextLength == 0) return;
        try
        {
            int lineH = Math.Max(1, (int)Math.Ceiling(Font.GetHeight() * ZoomFactor));

            int firstVis = (int)SendMessage(Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (firstVis < 0) firstVis = 0;
            int visCount  = ClientSize.Height / lineH + 2;
            int lastVis   = Math.Min(Lines.Length - 1, firstVis + visCount);

            // Character advance width for dot centering (monospace — measure once).
            float scaledSz = Font.Size * ZoomFactor;
            int charW;
            using (var sf = new Font(Font.FontFamily, scaledSz, Font.Style))
                charW = Math.Max(1, TextRenderer.MeasureText("X", sf, Size.Empty, TextFormatFlags.NoPadding).Width);

            float dotR   = Math.Max(0.9f, lineH * 0.09f);   // dot radius for spaces
            float headSz = Math.Max(2.5f, lineH * 0.22f);   // arrowhead size for tabs

            var prevSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var dotBrush  = new SolidBrush(_whitespaceGlyphColor);
            using var arrowPen  = new Pen(_whitespaceGlyphColor, 1.0f);

            for (int li = firstVis; li <= lastVis; li++)
            {
                if (li < 0 || li >= Lines.Length) continue;
                string line = Lines[li];
                if (line.Length == 0) continue;

                int lineStart = GetFirstCharIndexFromLine(li);
                if (lineStart < 0) continue;

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c != ' ' && c != '\t') continue;

                    Point pt = GetPositionFromCharIndex(lineStart + i);
                    if (pt.IsEmpty) continue;
                    // Skip characters outside the visible client area
                    if (pt.Y < 0 || pt.Y >= ClientSize.Height) continue;
                    if (pt.X < -charW * 2 || pt.X >= ClientSize.Width) continue;

                    float cy = pt.Y + lineH * 0.5f;

                    if (c == ' ')
                    {
                        // Small filled circle centred inside the character cell
                        float cx = pt.X + charW * 0.5f;
                        g.FillEllipse(dotBrush, cx - dotR, cy - dotR, dotR * 2f, dotR * 2f);
                    }
                    else // tab
                    {
                        // Arrow spanning left-edge → right-edge of the tab stop cell
                        int    nextIdx  = lineStart + i + 1;
                        Point  nextPt   = nextIdx <= TextLength
                                          ? GetPositionFromCharIndex(nextIdx)
                                          : new Point(pt.X + charW * 4, pt.Y);
                        if (nextPt.IsEmpty) nextPt = new Point(pt.X + charW * 4, pt.Y);

                        float arrowL = pt.X + 2f;
                        float arrowR = nextPt.X - 2f;
                        if (arrowR - arrowL < 3f) continue;

                        float hs = Math.Min(headSz, (arrowR - arrowL) * 0.40f);
                        g.DrawLine(arrowPen, arrowL, cy, arrowR, cy);
                        g.DrawLine(arrowPen, arrowR - hs, cy - hs * 0.55f, arrowR, cy);
                        g.DrawLine(arrowPen, arrowR - hs, cy + hs * 0.55f, arrowR, cy);
                    }
                }

                if (_highlightTrailingWhitespace && line.Length > 0)
                {
                    int trailStart = line.Length;
                    while (trailStart > 0 && (line[trailStart - 1] == ' ' || line[trailStart - 1] == '\t'))
                        trailStart--;
                    if (trailStart < line.Length)
                    {
                        Point tPt = GetPositionFromCharIndex(lineStart + trailStart);
                        Point tEnd = GetPositionFromCharIndex(lineStart + line.Length - 1);
                        if (!tPt.IsEmpty && !tEnd.IsEmpty)
                        {
                            int underY = tPt.Y + lineH - 1;
                            using var twPen = new Pen(_trailingWhitespaceColor, 1);
                            g.DrawLine(twPen, tPt.X, underY, tEnd.X + charW, underY);
                        }
                    }
                }
            }

            g.SmoothingMode = prevSmoothing;
        }
        catch { }
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessagePtr(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [LibraryImport("gdi32.dll")]
    private static partial int BitBlt(IntPtr hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, int rop);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr ho);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HideCaret(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public int fErase;
        public int rcPaint_left, rcPaint_top, rcPaint_right, rcPaint_bottom;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

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
    private const int WM_ERASEBKGND = 0x0014;
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
        const int WM_LBUTTONDOWN = 0x0201;
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

            case WM_ERASEBKGND:
                // Suppress default background erase — we fill the background ourselves
                // in WM_PAINT via a single off-screen blit. Prevents the white/black flash
                // that occurs when OS erases the client area before our paint runs.
                m.Result = (IntPtr)1;
                return;

            case WM_PAINT:
            {
                if (IsDisposed || !IsHandleCreated || DesignMode)
                {
                    base.WndProc(ref m);
                    return;
                }

                int w = ClientSize.Width;
                int h = ClientSize.Height;
                if (w <= 0 || h <= 0) { base.WndProc(ref m); return; }

                // Use BeginPaint/EndPaint so Windows validates the update region
                // and does NOT call the default WndProc (which would paint to screen directly).
                var ps = new PAINTSTRUCT();
                IntPtr hdc = BeginPaint(Handle, ref ps);
                if (hdc == IntPtr.Zero) return;

                IntPtr memDC = IntPtr.Zero;
                IntPtr hBmp  = IntPtr.Zero;
                IntPtr oldBmp = IntPtr.Zero;
                try
                {
                    // Create off-screen memory DC + bitmap
                    memDC  = CreateCompatibleDC(hdc);
                    hBmp   = CreateCompatibleBitmap(hdc, w, h);
                    oldBmp = SelectObject(memDC, hBmp);

                    // Fill background (prevents garbage from uninitialised bitmap)
                    using (var gBg = Graphics.FromHdc(memDC))
                    using (var bgBrush = new SolidBrush(BackColor))
                        gBg.FillRectangle(bgBrush, 0, 0, w, h);

                    // Tell the RichEdit control to render its content into our memory DC.
                    // WM_PRINTCLIENT renders without touching the real screen HDC at all.
                    SendMessagePtr(Handle, WM_PRINTCLIENT, memDC,
                        (IntPtr)(PRF_CLIENT | PRF_ERASEBKGND));

                    // Draw all overlays into the same memory DC
                    if (!_scrollInProgress)
                    {
                        using var g = Graphics.FromHdc(memDC);

                        var lineRect = GetCurrentLineRect();
                        if (lineRect.HasValue)
                        {
                            var r = lineRect.Value;
                            using var hlBrush = new SolidBrush(_highlightColor);
                            g.FillRectangle(hlBrush, r);
                            // Re-blit text into the highlighted row so it stays readable
                            using var textBmp = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height));
                            using (var textG = Graphics.FromImage(textBmp))
                            {
                                IntPtr textHdc = textG.GetHdc();
                                BitBlt(textHdc, 0, 0, r.Width, r.Height, memDC, r.X, r.Y, SRCCOPY);
                                textG.ReleaseHdc(textHdc);
                            }
                            using var attrs = new ImageAttributes();
                            attrs.SetColorKey(BackColor, BackColor);
                            g.DrawImage(textBmp, r, 0, 0, r.Width, r.Height, GraphicsUnit.Pixel, attrs);
                        }

                        if (_hoverLineHighlightEnabled && _hoverLine >= 0)
                        {
                            int lh = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
                            if (lh > 0)
                            {
                                int fc = GetFirstCharIndexFromLine(_hoverLine);
                                if (fc >= 0)
                                {
                                    Point pt = GetPositionFromCharIndex(fc);
                                    if (!pt.IsEmpty)
                                        using (var hb = new SolidBrush(Color.FromArgb(60, 60, 60, 60)))
                                            g.FillRectangle(hb, pt.X, pt.Y, ClientSize.Width, lh);
                                }
                            }
                        }

                        DrawWordOccurrences(g);
                        DrawIndentGuides(g);
                        DrawColumnGuide(g);
                        DrawFoldBracketLines(g);
                        DrawMatchingBraces(g);
                        DrawSquiggles(g);
                        DrawErrorLens(g);
                        DrawWhitespaceGlyphs(g);

                        if (_caretVisible && Focused)
                        {
                            switch (_cursorStyle)
                            {
                                case "block":     DrawBlockCursor(g);     break;
                                case "underline": DrawUnderlineCursor(g); break;
                                default:          DrawLineCursor(g);      break;
                            }
                        }

                        DrawExtraCarets(g);
                        DrawZoomIndicator(g);
                    }

                    // Single blit — the only time pixels reach the screen
                    BitBlt(hdc, 0, 0, w, h, memDC, 0, 0, SRCCOPY);
                }
                finally
                {
                    if (oldBmp != IntPtr.Zero) SelectObject(memDC, oldBmp);
                    if (hBmp   != IntPtr.Zero) DeleteObject(hBmp);
                    if (memDC  != IntPtr.Zero) DeleteDC(memDC);
                    EndPaint(Handle, ref ps);
                }
                return;
            }
            case WM_VSCROLL:
            case WM_HSCROLL:
                _scrollInProgress = true;
                _scrollDebounceTimer?.Stop();
                _scrollDebounceTimer?.Start();
                base.WndProc(ref m);
                return;
            case WM_MOUSEWHEEL:
                {
                    _scrollInProgress = true;
                    _scrollDebounceTimer?.Stop();
                    _scrollDebounceTimer?.Start();
                    int keyState = (int)(m.WParam.ToInt64() & 0xFFFF);
                    bool ctrlDown = (keyState & 0x0008) != 0;
                    if (ctrlDown && !CtrlWheelZoomEnabled)
                        return; // suppress Ctrl+Wheel font zoom
                    if (!ctrlDown && MouseWheelScrollLines > 0)
                    {
                        int rawDelta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                        int clicks = Math.Abs(rawDelta) / 120;
                        int lines = clicks * MouseWheelScrollLines;
                        SendMessage(Handle, EM_LINESCROLL, 0, rawDelta > 0 ? -lines : lines);
                        return;
                    }
                    base.WndProc(ref m);
                    return;
                }
            case WM_LBUTTONDOWN:
                {
                    int keyState = (int)(m.WParam.ToInt64() & 0xFFFF);
                    bool ctrlDown = (keyState & 0x0008) != 0;
                    if (ctrlDown && _multiCursorEnabled)
                    {
                        int x = (short)(m.LParam.ToInt64() & 0xFFFF);
                        int y = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                        int charIdx = GetCharIndexFromPosition(new Point(x, y));
                        if (charIdx >= 0)
                        {
                            if (_extraCarets.Contains(charIdx)) _extraCarets.Remove(charIdx);
                            else _extraCarets.Add(charIdx);
                            Invalidate();
                            return;
                        }
                    }
                    break;
                }
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
}

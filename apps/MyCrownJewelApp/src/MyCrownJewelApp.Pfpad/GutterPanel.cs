using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Gutter panel drawn to the left of the text editor.
///
/// Column layout (left → right):
///   [GLYPH=14px] [LINE_NUM=snug] [BOOKMARK=14px?] [CHANGE=5px?] [COVERAGE=5px?] [FOLD=16px]
///
/// GLYPH column: breakpoint dot, active-debug arrow, or quick-action lightbulb.
/// LINE_NUM  : right-aligned absolute or relative line numbers, width auto-sized to digits.
/// FOLD      : always the rightmost column; click zone extends 8px left for usability.
/// </summary>
public class GutterPanel : Panel
{
    private Form1 mainForm;

    // ── Column widths ────────────────────────────────────────────────────────
    private const int GlyphColWidth    = 14;   // BP / debug / QA
    private const int FoldColWidth     = 16;   // fold markers
    private const int FoldClickExtra   =  8;   // extra click zone left of fold column
    private const int BookmarkColWidth = 14;
    private const int ChangeColWidth   =  5;
    private const int CoverageColWidth =  5;

    private int LineNumberColWidth = 36;        // recomputed in UpdateLineNumberWidth()

    // ── State ────────────────────────────────────────────────────────────────
    private bool _showFoldMarkers;
    private int  _hoveredGlyphLine    = -1;    // 1-based: line with hovered QA icon
    private int  _hoveredBpLine       = -1;    // 1-based: line hovered in BP column (no QA)
    private int  _hoveredBookmarkLine = -1;    // 1-based: line hovered in bookmark column

    private List<(int line, string title, Func<string, string>? apply)> _quickActions = new();
    private Dictionary<int, int>?  _coverageHits;
    private Dictionary<int, byte>  _lineDiffs = new();

#pragma warning disable CS0414
    private bool _gutterDataDirty  = true;
    private bool _breakpointsDirty = false;
#pragma warning restore CS0414

    private ContextMenuStrip? _debugMenu;

    // ── Public surface ───────────────────────────────────────────────────────
    public event Action<int>? BreakpointClicked;
    public event Action<int>? BookmarkClicked;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TopOffset { get; set; }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set { if (_showLineNumbers == value) return; _showLineNumbers = value; _gutterDataDirty = true; Invalidate(); }
    }
    private bool _showLineNumbers = true;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool RelativeNumbers
    {
        get => _relativeNumbers;
        set { if (_relativeNumbers == value) return; _relativeNumbers = value; _gutterDataDirty = true; Invalidate(); }
    }
    private bool _relativeNumbers = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int  CurrentLine      { get; set; } = 1;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowBookmarks
    {
        get => _showBookmarks;
        set { if (_showBookmarks == value) return; _showBookmarks = value; UpdateLineNumberWidth(); }
    }
    private bool _showBookmarks = true;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowChangeHistory{ get; set; } = false;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowCodeFolds    { get; set; } = true;

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowBreakpoints
    {
        get => _showBreakpoints;
        set { if (_showBreakpoints == value) return; _showBreakpoints = value; UpdateLineNumberWidth(); }
    }
    private bool _showBreakpoints = true;

    // ── P/Invoke ─────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_GETLINECOUNT        = 0x00BA;
    private const int EM_LINEINDEX           = 0x00BB;

    // ── Constructor ──────────────────────────────────────────────────────────
    public GutterPanel(Form1 form)
    {
        mainForm      = form;
        Dock          = DockStyle.Fill;
        Width         = GetTotalMarginWidth();
        BackColor     = Color.FromArgb(45, 45, 45);
        DoubleBuffered = true;
        ResizeRedraw   = true;
        MouseClick += GutterPanel_MouseClick;
        MouseMove  += GutterPanel_MouseMove;
        MouseLeave += GutterPanel_MouseLeave;
    }

    // ── Public helpers ───────────────────────────────────────────────────────
    public void SetQuickActions(List<(int line, string title, Func<string, string>? apply)> actions)
    {
        _quickActions      = actions;
        _gutterDataDirty   = true;
        Invalidate();
    }

    public void MarkDataDirty()
    {
        _gutterDataDirty = true;
        Refresh();
    }

    public void SetCoverage(Dictionary<int, int>? lineHits)
    {
        _coverageHits    = lineHits;
        _gutterDataDirty = true;
        Invalidate();
    }

    public void SetLineDiffs(Dictionary<int, byte> diffs)
    {
        _lineDiffs       = diffs ?? new Dictionary<int, byte>();
        _gutterDataDirty = true;
        Invalidate();
    }

    public void SetBreakpointsDirty() => _breakpointsDirty = true;

    public void InvalidateBreakpointArea()
        => Invalidate(new Rectangle(0, 0, GlyphColWidth, Height));

    public void RefreshGutter()
    {
        _gutterDataDirty = true;
        Invalidate();
    }

    public int DesiredWidth => GetTotalMarginWidth();

    // ── Width management ─────────────────────────────────────────────────────
    public void UpdateLineNumberWidth()
    {
        if (!ShowLineNumbers)
        {
            Width = GetTotalMarginWidth();
            _gutterDataDirty = true;
            Invalidate();
            return;
        }

        var editor = mainForm?.textEditor;
        if (editor == null || !editor.IsHandleCreated)
        {
            LineNumberColWidth = 36;
            Width = GetTotalMarginWidth();
            return;
        }

        int maxLine  = Math.Max(1, GetTotalLineCount());
        int digits   = Math.Max(2, maxLine.ToString().Length);
        float scaledPt = editor.Font.Size * editor.ZoomFactor;

        // Measure with bold font — current-line number is bold, worst-case width.
        using var boldFont = new Font(editor.Font.FontFamily, scaledPt, FontStyle.Bold);
        string sample = new string('9', digits);
        int textWidth = TextRenderer.MeasureText(
            sample, boldFont,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding).Width;

        // 4px left pad + 6px right gap before the next column
        LineNumberColWidth = textWidth + 10;

        Width = GetTotalMarginWidth();
        _gutterDataDirty = true;
        Invalidate();
    }

    private int GetTotalMarginWidth()
    {
        int w = 0;
        if (ShowBreakpoints)        w += GlyphColWidth;
        if (ShowLineNumbers)        w += LineNumberColWidth;
        if (ShowBookmarks)          w += BookmarkColWidth;
        if (ShowChangeHistory)      w += ChangeColWidth;
        if (_coverageHits != null)  w += CoverageColWidth;
        if (ShowCodeFolds)          w += FoldColWidth;
        return w;
    }

    private int GetTotalLineCount()
    {
        var ed = mainForm?.textEditor;
        if (ed == null || !ed.IsHandleCreated) return 1;
        return Math.Max(1, SendMessage(ed.Handle, EM_GETLINECOUNT, 0, 0));
    }

    // ── Column layout helper ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the x-bounds of the Glyph (BP) and Bookmark columns.
    /// Any column that is hidden has start == end (zero width).
    /// </summary>
    private (int glyphStart, int glyphEnd, int bookmarkStart, int bookmarkEnd) GetColumnBounds()
    {
        int x = 0;
        int glyphStart = x, glyphEnd = x;
        if (ShowBreakpoints)   { glyphEnd = x + GlyphColWidth; x += GlyphColWidth; }
        if (ShowLineNumbers)   x += LineNumberColWidth;
        int bookmarkStart = x, bookmarkEnd = x;
        if (ShowBookmarks)     { bookmarkEnd = x + BookmarkColWidth; }
        return (glyphStart, glyphEnd, bookmarkStart, bookmarkEnd);
    }

    // ── Mouse events ─────────────────────────────────────────────────────────
    private int LineIndexFromClick(MouseEventArgs e)
    {
        var editor = mainForm!.textEditor;
        int adjustedY = Math.Max(0, e.Y - TopOffset);
        int charIndex = editor.GetCharIndexFromPosition(new Point(0, adjustedY));
        return editor.GetLineFromCharIndex(charIndex);
    }

    private void GutterPanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null) return;
        int lineIndex = LineIndexFromClick(e);
        var (glyphStart, glyphEnd, bookmarkStart, bookmarkEnd) = GetColumnBounds();

        if (e.Button == MouseButtons.Right)
        {
            if (ShowBreakpoints) ShowDebugMenu(e.Location, lineIndex);
            return;
        }

        // ── Glyph column (BP / QA) ────────────────────────────────────────
        if (ShowBreakpoints && e.X >= glyphStart && e.X < glyphEnd)
        {
            int fileLine = lineIndex + 1;
            var actions  = _quickActions.Where(a => a.line == fileLine).ToList();
            if (actions.Count > 0)
            {
                var menu = new ContextMenuStrip();
                foreach (var act in actions)
                {
                    var capture = act;
                    menu.Items.Add(capture.title, null, (s, args) =>
                    {
                        if (capture.apply != null)
                        {
                            var editor = mainForm.textEditor;
                            string newText = capture.apply(editor.Text);
                            int sel = editor.SelectionStart;
                            editor.Text = newText;
                            if (sel <= editor.TextLength) editor.SelectionStart = sel;
                        }
                    });
                }
                menu.Show(this, e.Location);
            }
            else
            {
                BreakpointClicked?.Invoke(lineIndex);
            }
            return;
        }

        // ── Bookmark column ──────────────────────────────────────────────
        if (ShowBookmarks && e.X >= bookmarkStart && e.X < bookmarkEnd)
        {
            BookmarkClicked?.Invoke(lineIndex);
            return;
        }

        // ── Fold column (rightmost) ────────────────────────────────────────
        int foldHitX = Width - FoldColWidth - FoldClickExtra;
        if (e.X >= foldHitX && mainForm?.FoldingManager != null)
        {
            var region = mainForm.FoldingManager.GetRegionAtLine(lineIndex);
            if (region.HasValue)
            {
                mainForm.ToggleFold(lineIndex);
                _showFoldMarkers = true;
                _gutterDataDirty = true;
                Invalidate();
            }
        }
    }

    private void GutterPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null) return;
        var editor = mainForm.textEditor;
        var (glyphStart, glyphEnd, bookmarkStart, bookmarkEnd) = GetColumnBounds();

        bool inFoldMargin     = e.X >= Width - FoldColWidth - FoldClickExtra;
        int  newHoverGlyph    = -1;
        int  newHoverBp       = -1;
        int  newHoverBookmark = -1;

        if (ShowBreakpoints && e.X >= glyphStart && e.X < glyphEnd)
        {
            int adjustedY = Math.Max(0, e.Y - TopOffset);
            int charIndex = editor.GetCharIndexFromPosition(new Point(0, adjustedY));
            int lineIndex = editor.GetLineFromCharIndex(charIndex);
            int fileLine  = lineIndex + 1;
            if (_quickActions.Any(a => a.line == fileLine))
                newHoverGlyph = fileLine;
            else
                newHoverBp = fileLine;
        }

        if (ShowBookmarks && e.X >= bookmarkStart && e.X < bookmarkEnd)
        {
            int adjustedY = Math.Max(0, e.Y - TopOffset);
            int charIndex = editor.GetCharIndexFromPosition(new Point(0, adjustedY));
            int lineIndex = editor.GetLineFromCharIndex(charIndex);
            newHoverBookmark = lineIndex + 1;
        }

        if (inFoldMargin != _showFoldMarkers || newHoverGlyph != _hoveredGlyphLine
            || newHoverBp != _hoveredBpLine || newHoverBookmark != _hoveredBookmarkLine)
        {
            _showFoldMarkers     = inFoldMargin;
            _hoveredGlyphLine    = newHoverGlyph;
            _hoveredBpLine       = newHoverBp;
            _hoveredBookmarkLine = newHoverBookmark;
            _gutterDataDirty     = true;
            Invalidate();
        }
    }

    private void GutterPanel_MouseLeave(object? sender, EventArgs e)
    {
        if (_showFoldMarkers || _hoveredGlyphLine >= 0 || _hoveredBpLine >= 0 || _hoveredBookmarkLine >= 0)
        {
            _showFoldMarkers     = false;
            _hoveredGlyphLine    = -1;
            _hoveredBpLine       = -1;
            _hoveredBookmarkLine = -1;
            _gutterDataDirty     = true;
            Invalidate();
        }
    }

    private void ShowDebugMenu(Point location, int lineIndex)
    {
        // Rebuild each time so the captured lineIndex is always current.
        _debugMenu?.Dispose();
        _debugMenu = new ContextMenuStrip();
        _debugMenu.Items.Add("🔴 Toggle Breakpoint Here",   null, (s, e2) => BreakpointClicked?.Invoke(lineIndex));
        _debugMenu.Items.Add("✅ Enable All Breakpoints",   null, (s, e2) => mainForm?.DebugBreakpointManager?.EnableAll());
        _debugMenu.Items.Add("❌ Disable All Breakpoints",  null, (s, e2) => mainForm?.DebugBreakpointManager?.DisableAll());
        _debugMenu.Items.Add("🗑️ Clear All Breakpoints",  null, (s, e2) => mainForm?.DebugBreakpointManager?.ClearAll());

        // Append quick actions if any
        var actions = _quickActions.Where(a => a.line == lineIndex + 1).ToList();
        if (actions.Count > 0)
        {
            _debugMenu.Items.Add(new ToolStripSeparator());
            foreach (var act in actions)
            {
                var capture = act;
                _debugMenu.Items.Add("💡 " + capture.title, null, (s, e2) =>
                {
                    if (capture.apply != null)
                    {
                        var editor = mainForm.textEditor;
                        string newText = capture.apply(editor.Text);
                        int sel = editor.SelectionStart;
                        editor.Text = newText;
                        if (sel <= editor.TextLength) editor.SelectionStart = sel;
                    }
                });
            }
        }

        _debugMenu.Show(this, location);
    }

    // ── Paint ────────────────────────────────────────────────────────────────
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Suppressed: DrawGutter calls g.Clear() at the top of every frame.
        // A separate background erase produces a blank intermediate frame (flicker).
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        GetVisibleLineRange(out int firstLine, out int visibleCount);
        _lastFirstLine   = firstLine;
        _lastVisibleCount = visibleCount;
        _gutterDataDirty  = false;
        _breakpointsDirty = false;
        base.OnPaint(e);
        DrawGutter(e.Graphics);
    }
    private int _lastFirstLine   = -1;
    private int _lastVisibleCount = -1;

    private void DrawGutter(Graphics g)
    {
        if (mainForm?.textEditor == null) return;
        var editor = mainForm.textEditor;
        if (editor.IsDisposed || !editor.Visible) return;

        GetVisibleLineRange(out int firstVisibleLine, out int visibleLines);
        g.Clear(BackColor);

        int lineH      = Math.Max(1, (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor));
        int totalLines = GetTotalLineCount();

        // Pass 1: fold scope guide lines (drawn under per-line markers)
        if (ShowCodeFolds)
            DrawFoldScopeLines(g, editor, firstVisibleLine, firstVisibleLine + visibleLines - 1, lineH);

        // Pass 2: per-line elements
        for (int i = 0; i < visibleLines; i++)
        {
            int lineIndex = firstVisibleLine + i;
            if (lineIndex >= totalLines) break;

            int lineY = GetLineY(editor, lineIndex);
            if (lineY < 0) continue;
            if (lineY + lineH <= TopOffset)                           continue;
            if (lineY > editor.ClientSize.Height + TopOffset + 2)     break;

            int colX = 0;

            // Glyph column: BP / debug active / QA
            if (ShowBreakpoints)
            {
                DrawGlyphColumn(g, lineIndex, colX, lineY, lineH);
                colX += GlyphColWidth;
            }

            // Line numbers
            if (ShowLineNumbers)
            {
                DrawLineNumber(g, lineIndex + 1, colX, lineY);
                colX += LineNumberColWidth;
            }

            // Bookmarks
            if (ShowBookmarks)
            {
                DrawBookmark(g, lineIndex, colX, lineY, lineH);
                colX += BookmarkColWidth;
            }

            // Change history
            if (ShowChangeHistory)
            {
                DrawChangeIndicator(g, lineIndex, colX, lineY, lineH);
                colX += ChangeColWidth;
            }

            // Coverage
            if (_coverageHits != null)
            {
                DrawCoverageIndicator(g, lineIndex + 1, colX, lineY, lineH);
                colX += CoverageColWidth;
            }

            // Fold markers — always at rightmost position
            if (ShowCodeFolds)
                DrawFoldMarker(g, lineIndex, Width - FoldColWidth, lineY, lineH);
        }
    }

    // ── Visible range ────────────────────────────────────────────────────────
    private void GetVisibleLineRange(out int firstLine, out int lineCount)
    {
        var editor = mainForm.textEditor;
        firstLine  = 0;
        lineCount  = 0;
        if (editor.IsDisposed || !editor.Visible) return;

        int nativeFirst = SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        if (nativeFirst < 0) nativeFirst = 0;
        firstLine = nativeFirst;

        int lineH      = Math.Max(1, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));
        int clientH    = editor.ClientSize.Height;
        lineCount = (int)Math.Ceiling((clientH - TopOffset) / (double)lineH) + 3;
        if (lineCount < 1) lineCount = 1;
    }

    private int GetLineY(RichTextBox editor, int lineIndex)
    {
        if (lineIndex >= Math.Max(1, GetTotalLineCount())) return -1;
        int lineH = Math.Max(1, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));
        try
        {
            int charIndex = SendMessage(editor.Handle, EM_LINEINDEX, lineIndex, 0);
            if (charIndex >= 0)
            {
                Point pos = editor.GetPositionFromCharIndex(charIndex);
                return pos.Y + TopOffset;
            }
        }
        catch { }
        return lineIndex * lineH;
    }

    // ── Per-column drawers ────────────────────────────────────────────────────

    /// <summary>
    /// Glyph column: draws whichever indicator has highest priority for this line.
    /// Priority: active-debug > breakpoint > quick-action lightbulb.
    /// </summary>
    private void DrawGlyphColumn(Graphics g, int lineIndex, int x, int y, int lineH)
    {
        int fileLine = lineIndex + 1;
        int cx = x + GlyphColWidth / 2;
        int cy = y + lineH / 2;
        int r  = Math.Max(4, Math.Min(6, lineH / 3));

        bool isActive = mainForm.DebugActiveLine == fileLine;
        bool hasBp    = mainForm.DebugBreakpointManager != null
                     && mainForm.CurrentFilePath != null
                     && mainForm.DebugBreakpointManager.HasBreakpoint(mainForm.CurrentFilePath, fileLine);
        bool hasQa    = _quickActions.Any(a => a.line == fileLine);
        bool bpHover  = _hoveredBpLine == fileLine;

        if (isActive)
        {
            using var outer = new SolidBrush(Color.Gold);
            g.FillEllipse(outer, cx - r, cy - r, r * 2, r * 2);
            using var inner = new SolidBrush(Color.FromArgb(220, 180, 0));
            g.FillEllipse(inner, cx - r / 2, cy - r / 2, r, r);
        }
        else if (hasBp)
        {
            using var brush = new SolidBrush(Color.FromArgb(220, 40, 40));
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
        }
        else if (hasQa)
        {
            bool hovered = _hoveredGlyphLine == fileLine;
            Color qc = hovered ? Color.Gold : Color.FromArgb(180, 180, 100);
            using var outer = new SolidBrush(qc);
            g.FillEllipse(outer, cx - r, cy - r, r * 2, r * 2);
            using var dot = new SolidBrush(BackColor);
            g.FillEllipse(dot, cx - 2, cy - 2, 4, 4);
        }
        else if (bpHover)
        {
            // Faint BP hover hint: outline circle
            using var pen = new Pen(Color.FromArgb(120, 200, 60, 60), 1.5f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            g.SmoothingMode = SmoothingMode.Default;
        }
    }

    private void DrawLineNumber(Graphics g, int lineNumber, int x, int y)
    {
        var editor = mainForm.textEditor;
        int currentLineNum = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
        bool isCurrent = lineNumber == currentLineNum;

        string text = (RelativeNumbers && !isCurrent)
            ? Math.Abs(lineNumber - currentLineNum).ToString()
            : lineNumber.ToString();

        bool highlightCurrent =
            mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberOnly ||
            mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberAndWholeLine;

        FontStyle style = editor.Font.Style;
        Color     color = mainForm.IsDarkTheme ? Color.LightGray : Color.DarkGray;

        if (highlightCurrent && isCurrent)
        {
            style |= FontStyle.Bold;
            color  = mainForm.IsDarkTheme ? Color.Yellow : Color.Black;
        }

        float scaledPt = editor.Font.Size * editor.ZoomFactor;
        using var font = new Font(editor.Font.FontFamily, scaledPt, style);

        var sz = TextRenderer.MeasureText(g, text, font,
            new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);

        // Right-align within the line-number zone; 4px right gap toward the next column
        int textX = x + LineNumberColWidth - sz.Width - 4;
        TextRenderer.DrawText(g, text, font, new Point(textX, y), color, TextFormatFlags.NoPadding);
    }

    private void DrawBookmark(Graphics g, int lineIndex, int x, int y, int lineH)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;
        bool hasBookmark = mainForm.Bookmarks.Contains(lineIndex);
        bool hovered     = _hoveredBookmarkLine == lineIndex + 1;

        if (!hasBookmark && !hovered) return;

        int colCenter = x + BookmarkColWidth / 2;
        int size  = Math.Max(7, Math.Min(11, lineH - 3));
        int bx    = colCenter - size / 2;
        int by    = y + (lineH - size) / 2;
        int notch = Math.Max(2, size / 3);

        Color fillColor = hasBookmark
            ? Color.FromArgb(80, 130, 230)        // cornflower blue
            : Color.FromArgb(60, 80, 130, 230);   // faint when just hovering

        // Bookmark ribbon: rectangle with V-notch cut into the bottom
        using var path = new GraphicsPath();
        path.AddPolygon(new Point[]
        {
            new(bx,             by),
            new(bx + size,      by),
            new(bx + size,      by + size),
            new(bx + size / 2,  by + size - notch),
            new(bx,             by + size),
        });

        var prevSm = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(fillColor))
            g.FillPath(brush, path);
        g.SmoothingMode = prevSm;
    }

    private void DrawChangeIndicator(Graphics g, int lineIndex, int x, int y, int lineH)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;

        Color? barColor = null;
        if (_lineDiffs.TryGetValue(lineIndex + 1, out byte diffType))
        {
            barColor = diffType == 1
                ? Color.FromArgb(80, 200, 80)
                : Color.FromArgb(220, 180, 60);
        }
        else if (mainForm.ModifiedLines.Contains(lineIndex))
        {
            barColor = Color.Orange;
        }

        if (barColor == null) return;

        // 3px bar, full line height
        int barH = Math.Max(4, lineH - 4);
        using var brush = new SolidBrush(barColor.Value);
        g.FillRectangle(brush, x + 1, y + 2, 3, barH);
    }

    private void DrawCoverageIndicator(Graphics g, int lineNumber, int x, int y, int lineH)
    {
        if (_coverageHits == null || !_coverageHits.TryGetValue(lineNumber, out int hits)) return;
        Color c = hits > 0 ? Color.FromArgb(80, 200, 80) : Color.FromArgb(220, 60, 60);
        using var brush = new SolidBrush(c);
        g.FillRectangle(brush, x, y + 2, CoverageColWidth - 1, Math.Max(4, lineH - 4));
    }

    // ── Fold drawing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Pass 1 — 1px vertical guide lines for all open fold regions visible on screen.
    /// Drawn before per-line markers so the box (+/−) covers the line at intersections.
    /// </summary>
    private void DrawFoldScopeLines(Graphics g, RichTextBox editor,
                                    int firstLine, int lastLine, int lineH)
    {
        if (mainForm?.FoldingManager == null) return;

        // Guide line runs down the centre of the fold column
        int lineX   = Width - FoldColWidth + FoldColWidth / 2;
        int areaTop = TopOffset;
        int areaBot = editor.ClientSize.Height + TopOffset;

        Color lineCol = mainForm.IsDarkTheme
            ? Color.FromArgb(80, 160, 160, 160)
            : Color.FromArgb(80, 90, 90, 90);

        using var pen = new Pen(lineCol, 1f);

        foreach (var region in mainForm.FoldingManager.GetAllRegions())
        {
            if (region.IsCollapsed)             continue;
            if (region.CloseLine <= region.OpenLine + 1) continue;
            if (region.OpenLine  > lastLine)    continue;
            if (region.CloseLine < firstLine)   continue;

            int yStart = region.OpenLine >= firstLine
                ? (GetLineY(editor, region.OpenLine) is int oy && oy >= 0 ? oy + lineH : areaTop)
                : areaTop;

            int yEnd = region.CloseLine <= lastLine
                ? (GetLineY(editor, region.CloseLine) is int cy && cy >= 0 ? cy : areaBot)
                : areaBot;

            yStart = Math.Max(areaTop, yStart);
            yEnd   = Math.Min(areaBot, yEnd);
            if (yEnd <= yStart + 1) continue;

            g.DrawLine(pen, lineX, yStart, lineX, yEnd);
        }
    }

    private void DrawFoldMarker(Graphics g, int lineIndex, int x, int y, int lineH)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;
        if (mainForm?.FoldingManager == null) return;
        if (!mainForm.FoldingManager.IsFoldStart(lineIndex)) return;

        bool folded  = mainForm.FoldingManager.IsCollapsed(lineIndex);
        int  boxSize = Math.Max(8, Math.Min(12, lineH - 4));
        int  bx      = x + (FoldColWidth - boxSize) / 2;
        int  by      = y + (lineH - boxSize) / 2;

        int alpha  = _showFoldMarkers ? 220 : 140;
        Color border = mainForm.IsDarkTheme
            ? Color.FromArgb(alpha, 160, 160, 160)
            : Color.FromArgb(alpha, 80,  80,  80);

        // Fill the box to mask the scope guide line underneath
        using (var fill = new SolidBrush(BackColor))
            g.FillRectangle(fill, bx, by, boxSize, boxSize);

        // 1px border
        using (var pen = new Pen(border, 1f))
            g.DrawRectangle(pen, bx, by, boxSize - 1, boxSize - 1);

        // +/− symbol
        int midX = bx + boxSize / 2;
        int midY = by + boxSize / 2;
        int arm  = boxSize / 2 - 2;
        using (var pen = new Pen(border, 1f))
        {
            g.DrawLine(pen, midX - arm, midY, midX + arm, midY);    // horizontal (both states)
            if (folded)
                g.DrawLine(pen, midX, midY - arm, midX, midY + arm); // vertical (+) when collapsed
        }
    }
}

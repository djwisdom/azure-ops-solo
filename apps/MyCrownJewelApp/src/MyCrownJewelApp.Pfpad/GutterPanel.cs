using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MyCrownJewelApp.Pfpad;

public class GutterPanel : Panel
{
    private static readonly Font _foldFont = new Font("Segoe UI", 8, FontStyle.Regular);

    private const string ChevronDown = "\u25BC"; // ▼ expanded (open fold)
    private const string ChevronRight = "\u25B6"; // ▶ collapsed (folded fold)

    private Form1 mainForm;
    private int LineNumberMarginWidth = 60;
    private const int BookmarkMarginWidth = 20;
    private const int ChangeMarginWidth = 20;
    private const int FoldMarginWidth = 18;
    private const int FoldClickWidth = 28;
    private const int QuickActionWidth = 18;
    private const int CoverageMarginWidth = 8;
    private const int BreakpointMarginWidth = 16;

    private int totalMarginWidth;
    private bool _showFoldMarkers;
    private int _hoveredActionLine = -1;

    private List<(int line, string title, Func<string, string>? apply)> _quickActions = new();
    private Dictionary<int, int>? _coverageHits; // line → hit count
    private Dictionary<int, byte> _lineDiffs = new(); // 1=added, 2=modified

    // Dirty-region tracking
    private int _lastFirstLine = -1;
    private int _lastVisibleCount = -1;
    private bool _gutterDataDirty = true;
    private bool _breakpointsDirty = false;

    public event Action<int>? BreakpointClicked;

    public int TopOffset { get; set; }

    public void SetQuickActions(List<(int line, string title, Func<string, string>? apply)> actions)
    {
        _quickActions = actions;
        _gutterDataDirty = true;
        Invalidate();
    }

    public void MarkDataDirty()
    {
        _gutterDataDirty = true;
        Refresh();
    }

    public void SetCoverage(Dictionary<int, int>? lineHits)
    {
        _coverageHits = lineHits;
        _gutterDataDirty = true;
        Invalidate();
    }

    public void SetLineDiffs(Dictionary<int, byte> diffs)
    {
        _lineDiffs = diffs ?? new Dictionary<int, byte>();
        _gutterDataDirty = true;
        Invalidate();
    }

    public void SetBreakpointsDirty()
    {
        _breakpointsDirty = true;
    }

    public void InvalidateBreakpointArea()
    {
        int bpX = QuickActionWidth;
        if (ShowLineNumbers) bpX += LineNumberMarginWidth;
        if (ShowBookmarks) bpX += BookmarkMarginWidth;
        Invalidate(new Rectangle(bpX, 0, BreakpointMarginWidth, Height));
    }

    private void ShowDebugMenu(Point location, int lineIndex)
    {
        if (_debugMenu == null)
        {
            _debugMenu = new ContextMenuStrip();
            _debugMenu.Items.Add("🔴 Toggle Breakpoint Here", null, (s, e) => BreakpointClicked?.Invoke(lineIndex));
            _debugMenu.Items.Add("✅ Enable All Breakpoints", null, (s, e) => mainForm?.DebugBreakpointManager?.EnableAll());
            _debugMenu.Items.Add("❌ Disable All Breakpoints", null, (s, e) => mainForm?.DebugBreakpointManager?.DisableAll());
            _debugMenu.Items.Add("🗑️ Clear All Breakpoints", null, (s, e) => mainForm?.DebugBreakpointManager?.ClearAll());
        }
        _debugMenu.Show(this, location);
    }

    [Category("Appearance")]
    public bool ShowLineNumbers { get; set; } = true;
    public bool RelativeNumbers { get; set; } = false;
    public int CurrentLine { get; set; } = 1;

    [Category("Appearance")]
    public bool ShowBookmarks { get; set; } = false;

    [Category("Appearance")]
    public bool ShowChangeHistory { get; set; } = false;

    [Category("Appearance")]
    public bool ShowCodeFolds { get; set; } = true;

    [Category("Appearance")]
    public bool ShowBreakpoints { get; set; } = true;

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_GETLINECOUNT = 0x00BA;

    public GutterPanel(Form1 form)
    {
        mainForm = form;
        Dock = DockStyle.Fill;
        Width = GetTotalMarginWidth();
        BackColor = Color.FromArgb(45, 45, 45);
        DoubleBuffered = true;
        ResizeRedraw = true;
        MouseClick += GutterPanel_MouseClick;
        MouseMove += GutterPanel_MouseMove;
        MouseLeave += GutterPanel_MouseLeave;
    }

    private ContextMenuStrip? _debugMenu;

    private void GutterPanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null) return;
        var editor = mainForm.textEditor;
        // Get the exact line at the clicked Y position
        Point pt = new Point(0, e.Y);
        int charIndex = editor.GetCharIndexFromPosition(pt);
        int line = editor.GetLineFromCharIndex(charIndex);
        int lineIndex = line;

        if (e.Button == MouseButtons.Right)
        {
            ShowDebugMenu(e.Location, lineIndex);
            return;
        }

        // Breakpoint click (right of line numbers)
        int bpX = QuickActionWidth;
        if (ShowLineNumbers) bpX += LineNumberMarginWidth;
        if (ShowBookmarks) bpX += BookmarkMarginWidth;
        int bpEnd = bpX + BreakpointMarginWidth;
        if (e.X >= bpX && e.X < bpEnd)
        {
            BreakpointClicked?.Invoke(lineIndex);
            return;
        }

        // Quick action click (left side)
        if (e.X >= 0 && e.X < QuickActionWidth)
        {
            var actions = _quickActions.Where(a => a.line == lineIndex + 1).ToList();
            if (actions.Count > 0)
            {
                var menu = new ContextMenuStrip();
                foreach (var act in actions)
                {
                    menu.Items.Add(act.title, null, (s, args) =>
                    {
                        if (act.apply != null)
                        {
                            string text = editor.Text;
                            string newText = act.apply(text);
                            int selStart = editor.SelectionStart;
                            editor.Text = newText;
                            if (selStart <= editor.TextLength)
                                editor.SelectionStart = selStart;
                        }
                    });
                }
                menu.Show(this, e.Location);
                return;
            }
        }

        // Fold click (right side)
        if (mainForm?.FoldingManager == null) return;
        int foldX = Width - FoldClickWidth;
        if (e.X < foldX || e.X > Width) return;

        var region = mainForm.FoldingManager.GetRegionAtLine(lineIndex);
        if (region.HasValue)
        {
            mainForm.ToggleFold(lineIndex);
            _showFoldMarkers = true;
            _gutterDataDirty = true;
            Invalidate();
        }
    }

    private void GutterPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null) return;

        int foldX = Width - FoldClickWidth;
        bool inFoldMargin = e.X >= foldX && e.X <= Width;

        int newHoverLine = -1;
        if (e.X < QuickActionWidth)
        {
            var editor = mainForm.textEditor;
            int lineHeight = Math.Max(1, (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor));
            int firstVis = (int)SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        int yOffset = Math.Max(0, e.Y - TopOffset);
        int lineIndex = firstVis + (yOffset + lineHeight / 2) / lineHeight;
            if (_quickActions.Any(a => a.line == lineIndex + 1))
                newHoverLine = lineIndex + 1;
        }

        if (inFoldMargin != _showFoldMarkers || newHoverLine != _hoveredActionLine)
        {
            _showFoldMarkers = inFoldMargin;
            _hoveredActionLine = newHoverLine;
            _gutterDataDirty = true;
            Invalidate();
        }
    }

    private void GutterPanel_MouseLeave(object? sender, EventArgs e)
    {
        if (_showFoldMarkers || _hoveredActionLine >= 0)
        {
            _showFoldMarkers = false;
            _hoveredActionLine = -1;
            _gutterDataDirty = true;
            Invalidate();
        }
    }

    private int GetTotalLineCount()
    {
        var editor = mainForm?.textEditor;
        if (editor == null || !editor.IsHandleCreated) return 1;
        return Math.Max(1, (int)SendMessage(editor.Handle, EM_GETLINECOUNT, 0, 0));
    }

    public void UpdateLineNumberWidth()
    {
        int lineNumberWidth = ShowLineNumbers ? LineNumberMarginWidth : 0;
        if (mainForm?.textEditor != null && ShowLineNumbers)
        {
            var editor = mainForm.textEditor;
            if (editor.IsHandleCreated)
            {
                int maxLineNumber = Math.Max(1, GetTotalLineCount());
                int digitCount = maxLineNumber.ToString().Length;
                float scaledSize = editor.Font.Size * editor.ZoomFactor;
                using var measureFont = new Font(editor.Font.FontFamily, scaledSize);
                string sample = new string('8', digitCount);
                using var g = Graphics.FromImage(new Bitmap(1, 1));
                int textWidth = (int)Math.Ceiling(g.MeasureString(sample, measureFont).Width);
                lineNumberWidth = textWidth + 15;
            }
        }
        if (lineNumberWidth < 60) lineNumberWidth = 60;
        if (lineNumberWidth > 400) lineNumberWidth = 400;

        lineNumberWidth += 8; // left edge padding
        LineNumberMarginWidth = lineNumberWidth;
        Width = GetTotalMarginWidth();
        _gutterDataDirty = true;
        Invalidate();
    }

    private int GetTotalMarginWidth()
    {
        totalMarginWidth = QuickActionWidth;
        if (ShowLineNumbers) totalMarginWidth += LineNumberMarginWidth;
        if (ShowBookmarks) totalMarginWidth += BookmarkMarginWidth;
        if (ShowChangeHistory) totalMarginWidth += ChangeMarginWidth;
        if (_coverageHits != null) totalMarginWidth += CoverageMarginWidth;
        if (ShowBreakpoints) totalMarginWidth += BreakpointMarginWidth;
        if (ShowCodeFolds) totalMarginWidth += FoldMarginWidth;
        return totalMarginWidth;
    }

    public int DesiredWidth => GetTotalMarginWidth();

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!_breakpointsDirty)
            base.OnPaintBackground(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        GetVisibleLineRange(out int firstLine, out int visibleCount);

        if (firstLine == _lastFirstLine && visibleCount == _lastVisibleCount && !_gutterDataDirty)
            return;

        _lastFirstLine = firstLine;
        _lastVisibleCount = visibleCount;
        _gutterDataDirty = false;

        base.OnPaint(e);
        DrawGutter(e.Graphics);
        _breakpointsDirty = false;
    }

    private void DrawGutter(Graphics g)
    {
        if (mainForm?.textEditor == null) return;

        RichTextBox editor = mainForm.textEditor;
        if (editor.IsDisposed || !editor.Visible) return;

        GetVisibleLineRange(out int firstVisibleLine, out int visibleLines);

        g.Clear(BackColor);

        int lineHeight = (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor);
        if (lineHeight <= 0) lineHeight = 1;

        int totalLines = GetTotalLineCount();

        for (int i = 0; i < visibleLines; i++)
        {
            int lineIndex = firstVisibleLine + i;
            if (lineIndex >= totalLines) break;

            int lineY = GetLineY(editor, lineIndex);
            if (lineY < 0) continue;

            if (lineY + lineHeight <= TopOffset) continue;
            if (lineY > editor.ClientSize.Height + TopOffset + 2) break;

            int currentX = 0;

            // Quick action lightbulb
            DrawQuickAction(g, lineIndex + 1, currentX, lineY);
            currentX += QuickActionWidth;

            // Draw line numbers first
            if (ShowLineNumbers)
            {
                int lineNumberX = QuickActionWidth;
                DrawLineNumber(g, lineIndex + 1, lineNumberX, lineY);
                currentX += LineNumberMarginWidth;
            }

            if (ShowBookmarks)
            {
                DrawBookmark(g, lineIndex, currentX, lineY);
                currentX += BookmarkMarginWidth;
            }

            if (ShowBreakpoints)
            {
                DrawBreakpoint(g, lineIndex, currentX, lineY);
                currentX += BreakpointMarginWidth;
            }

            if (ShowChangeHistory)
            {
                DrawChangeIndicator(g, lineIndex, currentX, lineY);
                currentX += ChangeMarginWidth;
            }

            if (_coverageHits != null)
            {
                DrawCoverageIndicator(g, lineIndex + 1, currentX, lineY);
                currentX += CoverageMarginWidth;
            }

            if (ShowCodeFolds)
            {
                DrawFoldMarker(g, lineIndex, currentX, lineY);
            }
        }
    }

    private void GetVisibleLineRange(out int firstLine, out int lineCount)
    {
        RichTextBox editor = mainForm.textEditor;
        firstLine = 0;
        lineCount = 0;

        if (editor.IsDisposed || !editor.Visible) return;

        int nativeFirst = SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        if (nativeFirst < 0) nativeFirst = 0;

        int lineHeight = Math.Max(1, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));

        firstLine = nativeFirst;

        int clientHeight = editor.ClientSize.Height;
        lineCount = (int)Math.Ceiling((clientHeight - TopOffset) / (double)lineHeight) + 3;
        if (lineCount < 1) lineCount = 1;
    }

    private int GetLineY(RichTextBox editor, int lineIndex)
    {
        int totalLines = Math.Max(1, GetTotalLineCount());
        if (lineIndex >= totalLines) return -1;

        int lineHeight = Math.Max(1, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));

        try
        {
            int charIndex = (int)SendMessage(editor.Handle, 0x00BB, lineIndex, 0);
            if (charIndex >= 0)
            {
                Point charPos = editor.GetPositionFromCharIndex(charIndex);
                return charPos.Y + TopOffset;
            }
        }
        catch { }

        return lineIndex * lineHeight;
    }

    private void DrawVerticalLine(Graphics g, int x, Color color)
    {
        using var pen = new Pen(color);
        g.DrawLine(pen, x, 0, x, Height);
    }

    private void DrawLineNumber(Graphics g, int lineNumber, int x, int y)
    {
        RichTextBox editor = mainForm.textEditor;
        int currentLineNum = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
        bool isCurrent = lineNumber == currentLineNum;
        string text;
        if (RelativeNumbers && !isCurrent)
        {
            text = Math.Abs(lineNumber - currentLineNum).ToString();
        }
        else
        {
            text = lineNumber.ToString();
        }

        bool isCurrentLine = false;
        if (mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberOnly || mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberAndWholeLine)
        {
            isCurrentLine = (lineNumber == currentLineNum);
        }

        FontStyle style = editor.Font.Style;
        Color color = mainForm.IsDarkTheme ? Color.LightGray : Color.DarkGray;
        if (isCurrentLine)
        {
            style |= FontStyle.Bold;
            color = mainForm.IsDarkTheme ? Color.Yellow : Color.Black;
        }

        using var font = new Font(editor.Font.FontFamily, editor.Font.Size * editor.ZoomFactor, style);
        Size textSize = TextRenderer.MeasureText(text, font);
        int textX = x + LineNumberMarginWidth - textSize.Width - 4;
        int textY = y;

        TextRenderer.DrawText(g, text, font, new Point(textX, textY), color);
    }

    private void DrawBookmark(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;

        bool hasBookmark = mainForm.Bookmarks.Contains(lineIndex);
        int centerX = x + BookmarkMarginWidth / 2;
        int centerY = y + 10;
        int radius = 5;

        if (hasBookmark)
        {
            using var brush = new SolidBrush(Color.Orange);
            g.FillEllipse(brush, centerX - radius, centerY - radius, radius * 2, radius * 2);
        }
    }

    private void DrawBreakpoint(Graphics g, int lineIndex, int x, int y)
    {
        if (mainForm?.DebugBreakpointManager == null) return;
        int fileLine = lineIndex + 1;
        string? filePath = mainForm.CurrentFilePath;
        if (filePath == null) return;

        bool hasBp = mainForm.DebugBreakpointManager.HasBreakpoint(filePath, fileLine);
        bool isActive = mainForm.DebugActiveLine == fileLine;
        if (!hasBp && !isActive) return;

        int cx = x + BreakpointMarginWidth / 2;
        int cy = y + 8;
        int r = 6;

        if (isActive)
        {
            using var brush = new SolidBrush(Color.Gold);
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
            using var innerBrush = new SolidBrush(Color.FromArgb(220, 180, 0));
            g.FillEllipse(innerBrush, cx - r / 2, cy - r / 2, r, r);
        }
        else if (hasBp)
        {
            using var brush = new SolidBrush(Color.FromArgb(220, 40, 40));
            g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
        }
    }

    private void DrawChangeIndicator(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;

        bool modified = mainForm.ModifiedLines.Contains(lineIndex);
        if (_lineDiffs.TryGetValue(lineIndex + 1, out byte diffType))
        {
            int barWidth = 4;
            int barX = x + (ChangeMarginWidth - barWidth) / 2;
            int lineH = Math.Max(1, (int)Math.Ceiling(mainForm.textEditor.Font.GetHeight() * mainForm.textEditor.ZoomFactor));
            Color c = diffType switch
            {
                1 => Color.FromArgb(80, 200, 80),
                _ => Color.FromArgb(220, 180, 60),
            };
            using var brush = new SolidBrush(c);
            g.FillRectangle(brush, barX, y + 2, barWidth, lineH - 4);
        }
        else if (modified)
        {
            int barWidth = 4;
            int barX = x + (ChangeMarginWidth - barWidth) / 2;
            using var brush = new SolidBrush(Color.Orange);
            g.FillRectangle(brush, barX, y + 2, barWidth, 10);
        }
    }

    private void DrawCoverageIndicator(Graphics g, int lineNumber, int x, int y)
    {
        if (_coverageHits == null) return;
        if (!_coverageHits.TryGetValue(lineNumber, out int hits)) return;

        int barWidth = CoverageMarginWidth - 2;
        int barX = x + 1;
        Color c = hits > 0 ? Color.FromArgb(80, 200, 80) : Color.FromArgb(220, 60, 60);
        using var brush = new SolidBrush(c);
        g.FillRectangle(brush, barX, y + 2, barWidth, 10);
    }

    private void DrawFoldMarker(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;

        bool isFoldStart;
        bool folded;

        if (mainForm.FoldingManager != null)
        {
            isFoldStart = mainForm.FoldingManager.IsFoldStart(lineIndex);
            folded = mainForm.FoldingManager.IsCollapsed(lineIndex);
        }
        else
        {
            if (lineIndex >= mainForm.textEditor.Lines.Length) return;
            string line = mainForm.textEditor.Lines[lineIndex];
            string trimmed = line.TrimStart();
            isFoldStart = trimmed.StartsWith("#region") || trimmed.EndsWith("{") || line.Contains("{");
            if (isFoldStart && lineIndex + 1 < mainForm.textEditor.Lines.Length)
            {
                string nextLine = mainForm.textEditor.Lines[lineIndex + 1];
                isFoldStart = !string.IsNullOrWhiteSpace(nextLine);
            }
            folded = false;
        }

        if (!isFoldStart) return;

        string symbol = folded ? ChevronRight : ChevronDown;
        Size sz = Size.Ceiling(g.MeasureString(symbol, _foldFont));
        int tx = x + (FoldMarginWidth - sz.Width) / 2;
        int ty = y + 2;
        int alpha = folded || _showFoldMarkers ? 220 : 80;
        Color col = mainForm.IsDarkTheme
            ? Color.FromArgb(alpha, 180, 180, 180)
            : Color.FromArgb(alpha, 80, 80, 80);
        using var brush = new SolidBrush(col);
        g.DrawString(symbol, _foldFont, brush, tx, ty);
    }

    private void DrawQuickAction(Graphics g, int lineNumber, int x, int y)
    {
        var actions = _quickActions.Where(a => a.line == lineNumber).ToList();
        if (actions.Count == 0) return;

        int centerX = x + QuickActionWidth / 2;
        int centerY = y + 8;
        int radius = 5;

        Color c = (_hoveredActionLine == lineNumber)
            ? Color.Gold
            : Color.FromArgb(180, 180, 100);

        using var brush = new SolidBrush(c);
        g.FillEllipse(brush, centerX - radius, centerY - radius, radius * 2, radius * 2);

        using var insideBrush = new SolidBrush(Color.FromArgb(45, 45, 45));
        g.FillEllipse(insideBrush, centerX - 2, centerY - 2, 4, 4);
    }

    public void RefreshGutter()
    {
        _gutterDataDirty = true;
        Invalidate();
    }
}

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
    private const int FoldMarginWidth = 14;
    private const int FoldClickWidth = 25;

    private int totalMarginWidth;
    private bool _showFoldMarkers;

    [Category("Appearance")]
    public bool ShowLineNumbers { get; set; } = true;

    [Category("Appearance")]
    public bool ShowBookmarks { get; set; } = false;

    [Category("Appearance")]
    public bool ShowChangeHistory { get; set; } = false;

    [Category("Appearance")]
    public bool ShowCodeFolds { get; set; } = true;

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

    private void GutterPanel_MouseClick(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null || mainForm.FoldingManager == null) return;
        int foldX = Width - FoldClickWidth;
        if (e.X < foldX || e.X > Width) return;

        var editor = mainForm.textEditor;
        int lineHeight = Math.Max(1, (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor));
        int firstVis = (int)SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        int lineIndex = firstVis + e.Y / lineHeight;

        var region = mainForm.FoldingManager.GetRegionAtLine(lineIndex);
        if (region.HasValue)
        {
            mainForm.ToggleFold(lineIndex);
        }
    }

    private void GutterPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (mainForm?.textEditor == null || !ShowCodeFolds || mainForm.FoldingManager == null) return;

        int foldX = Width - FoldClickWidth;
        bool inFoldMargin = e.X >= foldX && e.X <= Width;

        if (inFoldMargin != _showFoldMarkers)
        {
            _showFoldMarkers = inFoldMargin;
            Invalidate();
        }
    }

    private void GutterPanel_MouseLeave(object? sender, EventArgs e)
    {
        if (_showFoldMarkers)
        {
            _showFoldMarkers = false;
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
        int lineNumberWidth = LineNumberMarginWidth;
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
                int textWidth = TextRenderer.MeasureText(sample, measureFont).Width;
                lineNumberWidth = textWidth + 6;
            }
        }
        if (lineNumberWidth < 20) lineNumberWidth = 20;
        if (lineNumberWidth > 400) lineNumberWidth = 400;

        LineNumberMarginWidth = lineNumberWidth;
        Width = GetTotalMarginWidth();
        Invalidate();
    }

    private int GetTotalMarginWidth()
    {
        totalMarginWidth = 0;
        if (ShowLineNumbers) totalMarginWidth += LineNumberMarginWidth;
        if (ShowBookmarks) totalMarginWidth += BookmarkMarginWidth;
        if (ShowChangeHistory) totalMarginWidth += ChangeMarginWidth;
        if (ShowCodeFolds) totalMarginWidth += FoldMarginWidth;
        return totalMarginWidth;
    }

    public int DesiredWidth => GetTotalMarginWidth();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawGutter(e.Graphics);
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

            if (lineY + lineHeight <= 0) continue;
            if (lineY > editor.ClientSize.Height + 2) break;

            int currentX = 0;

            if (ShowLineNumbers)
            {
                DrawLineNumber(g, lineIndex + 1, currentX, lineY);
                currentX += LineNumberMarginWidth;
            }

            if (ShowBookmarks)
            {
                DrawBookmark(g, lineIndex, currentX, lineY);
                currentX += BookmarkMarginWidth;
            }

            if (ShowChangeHistory)
            {
                DrawChangeIndicator(g, lineIndex, currentX, lineY);
                currentX += ChangeMarginWidth;
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
        lineCount = (int)Math.Ceiling(clientHeight / (double)lineHeight) + 3;
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
                return charPos.Y;
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
        string text = lineNumber.ToString();
        RichTextBox editor = mainForm.textEditor;

        bool isCurrentLine = false;
        if (mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberOnly)
        {
            int currentLineNum = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
            isCurrentLine = (lineNumber == currentLineNum);
        }

        FontStyle style = editor.Font.Style;
        Color color = Color.FromArgb(120, 120, 120);
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

    private void DrawChangeIndicator(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;

        bool modified = mainForm.ModifiedLines.Contains(lineIndex);
        if (modified)
        {
            int barWidth = 4;
            int barX = x + (ChangeMarginWidth - barWidth) / 2;
            using var brush = new SolidBrush(Color.Orange);
            g.FillRectangle(brush, barX, y + 2, barWidth, 10);
        }
    }

    private void DrawFoldMarker(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= GetTotalLineCount()) return;
        if (!_showFoldMarkers) return;

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
        Color col = mainForm.IsDarkTheme ? Color.FromArgb(180, 180, 180) : Color.FromArgb(80, 80, 80);
        using var brush = new SolidBrush(col);
        g.DrawString(symbol, _foldFont, brush, tx, ty);
    }

    public void RefreshGutter()
    {
        Invalidate();
    }
}

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MyCrownJewelApp.TextEditor;

public class GutterPanel : Panel
{
    private Form1 mainForm;
    private const int LineNumberMarginWidth = 60;
    private const int BookmarkMarginWidth = 20;
    private const int ChangeMarginWidth = 20;
    private const int FoldMarginWidth = 20;

    private int totalMarginWidth;

    [Category("Appearance")]
    public bool ShowLineNumbers { get; set; } = true;

    [Category("Appearance")]
    public bool ShowBookmarks { get; set; } = true;

    [Category("Appearance")]
    public bool ShowChangeHistory { get; set; } = true;

    [Category("Appearance")]
    public bool ShowCodeFolds { get; set; } = true;

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

    public GutterPanel(Form1 form)
    {
        mainForm = form;
        Dock = DockStyle.Left;
        Width = GetTotalMarginWidth();
        BackColor = Color.FromArgb(45, 45, 45);
        DoubleBuffered = true;
        ResizeRedraw = true;
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

        // Determine visible lines
        GetVisibleLineRange(out int firstVisibleLine, out int visibleLines);

        g.Clear(BackColor);

        // Compute line height for partial-line clipping
        int lineHeight = (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor);
        if (lineHeight <= 0) lineHeight = 1;

        // Draw separator vertical lines between margin sections
        int x = 0;
        if (ShowLineNumbers)
        {
            x += LineNumberMarginWidth;
            DrawVerticalLine(g, x, Color.FromArgb(60, 60, 60));
        }
        if (ShowBookmarks)
        {
            x += BookmarkMarginWidth;
            DrawVerticalLine(g, x, Color.FromArgb(60, 60, 60));
        }
        if (ShowChangeHistory)
        {
            x += ChangeMarginWidth;
            DrawVerticalLine(g, x, Color.FromArgb(60, 60, 60));
        }
        if (ShowCodeFolds)
        {
            x += FoldMarginWidth;
            DrawVerticalLine(g, x, Color.FromArgb(60, 60, 60));
        }

        // Draw content for each visible line
        for (int i = 0; i < visibleLines; i++)
        {
            int lineIndex = firstVisibleLine + i;
            if (lineIndex >= editor.Lines.Length) break;

            int lineY = GetLineY(editor, lineIndex);
            if (lineY == -1) continue;

            // Skip lines that are completely above the viewport (lineY + lineHeight <= 0)
            if (lineY + lineHeight <= 0) continue;
            // Stop if line starts at or beyond bottom edge
            // Stop when we've passed the visible client area
            if (lineY >= editor.ClientSize.Height) break;

            int currentX = 0;

            // Line Numbers
            if (ShowLineNumbers)
            {
                DrawLineNumber(g, lineIndex + 1, currentX, lineY);
                currentX += LineNumberMarginWidth;
            }

            // Bookmarks
            if (ShowBookmarks)
            {
                DrawBookmark(g, lineIndex, currentX, lineY);
                currentX += BookmarkMarginWidth;
            }

            // Change History
            if (ShowChangeHistory)
            {
                DrawChangeIndicator(g, lineIndex, currentX, lineY);
                currentX += ChangeMarginWidth;
            }

            // Code Folds
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

        if (editor.Lines.Length == 0) return;
        if (editor.IsDisposed || !editor.Visible) return;

        // Use native message: returns first fully visible line index
        int nativeFirst = SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        if (nativeFirst < 0) nativeFirst = 0;

        // Compute line height from font's Height property (line spacing) with zoom
        int lineHeight = Math.Max(1, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));

        // Find the actual first visible line (including partially visible at top)
        firstLine = nativeFirst;
        if (nativeFirst > 0)
        {
            int yAbove = GetLineY(editor, nativeFirst - 1);
            if (yAbove != -1 && (yAbove + lineHeight) > 0)
            {
                firstLine = nativeFirst - 1;
            }
        }

        // Compute visible line count from client height (include partial at bottom)
        int clientHeight = editor.ClientSize.Height;
        lineCount = (clientHeight / lineHeight) + 2; // +2 for partial top+bottom safety
        if (lineCount < 1) lineCount = 1;
    }

    private int GetLineY(RichTextBox editor, int lineIndex)
    {
        if (lineIndex >= editor.Lines.Length) return -1;
        int charIndex = editor.GetFirstCharIndexFromLine(lineIndex);
        if (charIndex < 0) return -1;
        Point charPos = editor.GetPositionFromCharIndex(charIndex);
        return charPos.Y;
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

        // Determine if this line is current and mode is NumberOnly
        bool isCurrentLine = false;
        if (mainForm.LineHighlightMode == CurrentLineHighlightMode.NumberOnly)
        {
            int currentLineNum = editor.GetLineFromCharIndex(editor.SelectionStart) + 1;
            isCurrentLine = (lineNumber == currentLineNum);
        }

        // Use editor's font scaled by zoom; bold+yellow (dark) or black (light) if current line in NumberOnly mode
        FontStyle style = editor.Font.Style;
        Color color = Color.FromArgb(120, 120, 120); // default gray
        if (isCurrentLine)
        {
            style |= FontStyle.Bold;
            color = mainForm.IsDarkTheme ? Color.Yellow : Color.Black;
        }

        using var font = new Font(editor.Font.FontFamily, editor.Font.Size * editor.ZoomFactor, style);
        Size textSize = TextRenderer.MeasureText(text, font);
        int textX = x + (LineNumberMarginWidth - textSize.Width) / 2;
        int textY = y;

        TextRenderer.DrawText(g, text, font, new Point(textX, textY), color);
    }

    private void DrawBookmark(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= mainForm.textEditor.Lines.Length) return;

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
        if (lineIndex < 0 || lineIndex >= mainForm.textEditor.Lines.Length) return;

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
        if (lineIndex < 0 || lineIndex >= mainForm.textEditor.Lines.Length) return;

        bool folded = mainForm.CollapsedRegions.Contains(lineIndex);
        int size = 12;
        int centerX = x + FoldMarginWidth / 2 - size / 2;
        int centerY = y + 4;

        using var pen = new Pen(Color.Gray);
        using var brush = new SolidBrush(Color.FromArgb(60, 60, 60));
        g.FillRectangle(brush, centerX, centerY, size, size);
        g.DrawRectangle(pen, centerX, centerY, size, size);

        // Draw minus/plus
        using var textBrush = new SolidBrush(Color.White);
        string symbol = folded ? "+" : "-";
        using var font = new Font("Marlett", 8);
        Size sz = TextRenderer.MeasureText(symbol, font);
        int tx = centerX + (size - sz.Width) / 2;
        int ty = centerY + (size - sz.Height) / 2;
        TextRenderer.DrawText(g, symbol, font, new Point(tx, ty), Color.White);
    }

    public void RefreshGutter()
    {
        Invalidate();
    }
}

using System.ComponentModel;
using System.Drawing;

namespace MyCrownJewelApp.TextEditor;

public class GutterPanel : Panel
{
    private Form1 mainForm;
    private const int LineNumberMarginWidth = 60;
    private const int BookmarkMarginWidth = 20;
    private const int ChangeMarginWidth = 20;
    private const int FoldMarginWidth = 20;

    private int totalMarginWidth;
    private int firstVisibleLine;
    private int visibleLines;

    [Category("Appearance")]
    public bool ShowLineNumbers { get; set; } = true;

    [Category("Appearance")]
    public bool ShowBookmarks { get; set; } = true;

    [Category("Appearance")]
    public bool ShowChangeHistory { get; set; } = true;

    [Category("Appearance")]
    public bool ShowCodeFolds { get; set; } = true;

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

        g.Clear(BackColor);

        RichTextBox editor = mainForm.textEditor;
        int yOffset = 0;

        // Calculate visible line range
        GetVisibleLineRange(out firstVisibleLine, out visibleLines);

        // Draw separator lines between sections
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
            if (lineY < 0) continue;

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

        // Get first visible line based on scroll position
        int firstChar = editor.GetCharIndexFromPosition(new Point(0, 0));
        firstLine = editor.GetLineFromCharIndex(firstChar);
        if (firstLine < 0) firstLine = 0;

        // Count lines until their Y position exceeds editor height
        for (int i = firstLine; i < editor.Lines.Length; i++)
        {
            int y = GetLineY(editor, i);
            if (y >= editor.Height) break;
            lineCount++;
        }
    }

    private int GetLineY(RichTextBox editor, int lineIndex)
    {
        if (lineIndex >= editor.Lines.Length) return -1;
        int charIndex = editor.GetFirstCharIndexFromLine(lineIndex);
        if (charIndex < 0) return -1;
        Point charPos = editor.GetPositionFromCharIndex(charIndex);
        return charPos.Y - editor.AutoScrollOffset.Y;
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
        
        // Use editor's font scaled by zoom factor
        using var font = new Font(editor.Font.FontFamily, editor.Font.Size * editor.ZoomFactor, editor.Font.Style);
        Size textSize = TextRenderer.MeasureText(text, font);
        int textX = x + (LineNumberMarginWidth - textSize.Width) / 2;
        int textY = y;

        using var brush = new SolidBrush(Color.FromArgb(120, 120, 120));
        TextRenderer.DrawText(g, text, font, new Point(textX, textY), Color.FromArgb(120, 120, 120));
    }

    private void DrawBookmark(Graphics g, int lineIndex, int x, int y)
    {
        // Defensive: ensure lineIndex is valid
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
        else
        {
            using var pen = new Pen(Color.FromArgb(80, 80, 80));
            g.DrawEllipse(pen, centerX - radius, centerY - radius, radius * 2, radius * 2);
        }
    }

    private void DrawChangeIndicator(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= mainForm.textEditor.Lines.Length) return;

        bool hasChanges = mainForm.ModifiedLines.Contains(lineIndex);
        int barWidth = 4;
        int barX = x + (ChangeMarginWidth - barWidth) / 2;
        int barHeight = 12;
        int barY = y + 2;

        if (hasChanges)
        {
            using var brush = new SolidBrush(Color.Orange);
            g.FillRectangle(brush, barX, barY, barWidth, barHeight);
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(60, 60, 60));
            g.DrawRectangle(pen, barX, barY, barWidth, barHeight);
        }
    }

    private void DrawFoldMarker(Graphics g, int lineIndex, int x, int y)
    {
        if (lineIndex < 0 || lineIndex >= mainForm.textEditor.Lines.Length) return;

        string line = mainForm.textEditor.Lines[lineIndex];
        bool isRegionStart = line.TrimStart().StartsWith("#region");
        bool isRegionEnd = line.TrimStart().StartsWith("#endregion");

        if (!isRegionStart && !isRegionEnd) return;

        int boxSize = 12;
        int boxX = x + (FoldMarginWidth - boxSize) / 2;
        int boxY = y + 2;

        using var pen = new Pen(Color.FromArgb(120, 120, 120));
        using var brush = new SolidBrush(Color.FromArgb(60, 60, 60));

        // Draw +/- box
        g.FillRectangle(brush, boxX, boxY, boxSize, boxSize);
        g.DrawRectangle(pen, boxX, boxY, boxSize, boxSize);

        // Draw minus/plus sign
        using var signPen = new Pen(Color.White, 2);
        g.DrawLine(signPen, boxX + 3, boxY + boxSize / 2, boxX + boxSize - 3, boxY + boxSize / 2);
        if (isRegionStart)
        {
            g.DrawLine(signPen, boxX + boxSize / 2, boxY + 3, boxX + boxSize / 2, boxY + boxSize - 3);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        HandleMouseClick(e.X, e.Y);
    }

    private void HandleMouseClick(int x, int y)
    {
        int sectionX = 0;

        // Line numbers - no action
        if (ShowLineNumbers) sectionX += LineNumberMarginWidth;

        // Bookmarks - toggle on click
        if (ShowBookmarks && x >= sectionX && x < sectionX + BookmarkMarginWidth)
        {
            int line = GetLineAtY(y);
            if (line >= 0)
            {
                mainForm.ToggleBookmark(line);
                Invalidate();
            }
            return;
        }

        sectionX += BookmarkMarginWidth;

        // Change history - no action
        if (ShowChangeHistory) sectionX += ChangeMarginWidth;

        // Code folds
        if (ShowCodeFolds && x >= sectionX && x < sectionX + FoldMarginWidth)
        {
            int line = GetLineAtY(y);
            if (line >= 0)
            {
                string lineText = mainForm.textEditor.Lines[line];
                if (lineText.TrimStart().StartsWith("#region"))
                {
                    mainForm.ToggleFold(line);
                }
            }
        }
    }

    private int GetLineAtY(int y)
    {
        RichTextBox editor = mainForm.textEditor;
        if (editor.Lines.Length == 0) return -1;

        // Compute first visible line
        int firstChar = editor.GetCharIndexFromPosition(new Point(0, 0));
        int firstLine = editor.GetLineFromCharIndex(firstChar);
        if (firstLine < 0) firstLine = 0;

        // Determine actual line height from editor's rendered lines
        int lineHeight;
        if (firstLine < editor.Lines.Length - 1)
        {
            int y0 = GetLineY(editor, firstLine);
            int y1 = GetLineY(editor, firstLine + 1);
            lineHeight = y1 - y0;
            if (lineHeight <= 0) lineHeight = (int)(TextRenderer.MeasureText("A", editor.Font).Height * editor.ZoomFactor);
        }
        else
        {
            lineHeight = (int)(TextRenderer.MeasureText("A", editor.Font).Height * editor.ZoomFactor);
        }

        int line = firstLine + (y / lineHeight);
        return (line >= 0 && line < editor.Lines.Length) ? line : -1;
    }

    public void RefreshGutter()
    {
        Invalidate();
    }
}

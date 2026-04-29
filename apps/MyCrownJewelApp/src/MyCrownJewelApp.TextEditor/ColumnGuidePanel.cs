using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.TextEditor;

/// <summary>
/// Transparent overlay panel that draws a vertical column guide at a configurable column.
/// Sits on top of the editor and tracks scrolling/zoom automatically.
/// </summary>
public sealed class ColumnGuidePanel : Panel
{
    private RichTextBox? linkedEditor;
    private int guideColumn = 80;
    private Color guideColor = Color.Gray;
    private bool showGuide = true;

    public ColumnGuidePanel()
    {
        // Transparent support
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        // Make panel non-interactive so mouse/keyboard goes to editor
        Enabled = false;
        TabStop = false;
    }

    /// <summary>
    /// The RichTextBox editor to track. The guide aligns to its text area.
    /// </summary>
    public RichTextBox? LinkedEditor
    {
        get => linkedEditor;
        set
        {
            if (linkedEditor != null)
            {
                linkedEditor.VScroll -= Editor_ViewChanged;
                linkedEditor.HScroll -= Editor_ViewChanged;
                linkedEditor.Resize -= Editor_ViewChanged;
                linkedEditor.FontChanged -= Editor_ViewChanged;
            }
            linkedEditor = value;
            if (linkedEditor != null)
            {
                linkedEditor.VScroll += Editor_ViewChanged;
                linkedEditor.HScroll += Editor_ViewChanged;
                linkedEditor.Resize += Editor_ViewChanged;
                linkedEditor.FontChanged += Editor_ViewChanged;
            }
            Invalidate();
        }
    }

    /// <summary>
    /// Column number (1-based) to draw the vertical line. Default 80.
    /// </summary>
    public int GuideColumn
    {
        get => guideColumn;
        set
        {
            if (value < 1) value = 1;
            guideColumn = value;
            Invalidate();
        }
    }

    /// <summary>
    /// Color of the guide line.
    /// </summary>
    public Color GuideColor
    {
        get => guideColor;
        set { guideColor = value; Invalidate(); }
    }

    /// <summary>
    /// Whether the guide is shown.
    /// </summary>
    public bool ShowGuide
    {
        get => showGuide;
        set { showGuide = value; Invalidate(); }
    }

    private void Editor_ViewChanged(object? sender, EventArgs e)
    {
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!showGuide || linkedEditor == null || !linkedEditor.Visible)
            return;

        try
        {
            // Get origin of first character (includes left margin, padding, and horizontal scroll offset)
            Point origin = linkedEditor.GetPositionFromCharIndex(0);

            // Compute exact character advance using the editor's own positioning
            int charWidth;
            if (linkedEditor.TextLength >= 1)
            {
                Point p1 = linkedEditor.GetPositionFromCharIndex(1);
                charWidth = p1.X - origin.X;
            }
            else
            {
                // Editor is empty — fall back to font measurement
                using var g = CreateGraphics();
                charWidth = (int)Math.Ceiling(g.MeasureString("W", linkedEditor.Font).Width);
            }

            // Sanity check
            if (charWidth <= 0) charWidth = 8; // sensible fallback

            // Compute guide X position: origin + (column - 1) * charWidth
            int x = origin.X + (guideColumn - 1) * charWidth;

            // Draw within panel bounds
            if (x >= 0 && x < Width)
            {
                using Pen pen = new Pen(guideColor);
                e.Graphics.DrawLine(pen, x, 0, x, Height);
            }
        }
        catch { }
    }
}

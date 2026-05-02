using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Transparent overlay panel that draws a full-width highlight on the current line.
/// Sits behind the text in the editor and updates on scroll/typing/selection changes.
/// </summary>
public sealed class CurrentLineHighlightPanel : Panel
{
    private RichTextBox? linkedEditor;
    private Color highlightColor = Color.FromArgb(60, 60, 60);
    private bool showHighlight = true;
        private WinFormsTimer? _updateTimer;

    public CurrentLineHighlightPanel()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Enabled = false;
        TabStop = false;

        // Debounced updates to avoid flicker during fast typing/scrolling
        _updateTimer = new WinFormsTimer { Interval = 20 }; // ~50fps max
        _updateTimer.Tick += (s, e) => { _updateTimer.Stop(); Invalidate(); };
    }

    /// <summary>
    /// The RichTextBox editor to track.
    /// </summary>
    public RichTextBox? LinkedEditor
    {
        get => linkedEditor;
        set
        {
            if (linkedEditor != null)
            {
                linkedEditor.SelectionChanged -= Editor_SelectionChanged;
                linkedEditor.VScroll -= Editor_ViewChanged;
                linkedEditor.HScroll -= Editor_ViewChanged;
                linkedEditor.Resize -= Editor_ViewChanged;
                linkedEditor.TextChanged -= Editor_ViewChanged;
                linkedEditor.FontChanged -= Editor_FontChanged;
            }

            linkedEditor = value;

            if (linkedEditor != null)
            {
                linkedEditor.SelectionChanged += Editor_SelectionChanged;
                linkedEditor.VScroll += Editor_ViewChanged;
                linkedEditor.HScroll += Editor_ViewChanged;
                linkedEditor.Resize += Editor_ViewChanged;
                linkedEditor.TextChanged += Editor_ViewChanged;
                linkedEditor.FontChanged += Editor_FontChanged;
            }

            Invalidate();
        }
    }

    /// <summary>
    /// Color of the highlight line.
    /// </summary>
    public Color HighlightColor
    {
        get => highlightColor;
        set { highlightColor = value; Invalidate(); }
    }

    /// <summary>
    /// Whether the highlight is shown.
    /// </summary>
    public bool ShowHighlight
    {
        get => showHighlight;
        set { showHighlight = value; Invalidate(); }
    }

    private void Editor_SelectionChanged(object? sender, EventArgs e)
    {
        RequestUpdate();
    }

    private void Editor_ViewChanged(object? sender, EventArgs e)
    {
        RequestUpdate();
    }

    private void Editor_FontChanged(object? sender, EventArgs e)
    {
        Invalidate();
    }

    private void RequestUpdate()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Start();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Skip — keep fully transparent
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!showHighlight || linkedEditor == null || !linkedEditor.Visible || linkedEditor.IsDisposed)
            return;

        try
        {
            int currentLine = linkedEditor.GetLineFromCharIndex(linkedEditor.SelectionStart);
            if (currentLine < 0 || currentLine >= linkedEditor.Lines.Length) return;

            int charIndex = linkedEditor.GetFirstCharIndexFromLine(currentLine);
            if (charIndex < 0) return;

            Point pt = linkedEditor.GetPositionFromCharIndex(charIndex);
            if (pt == Point.Empty) return;

            int lineHeight = (int)Math.Ceiling(linkedEditor.Font.GetHeight() * linkedEditor.ZoomFactor);
            if (lineHeight <= 0) lineHeight = 1;

            int width = linkedEditor.ClientSize.Width;
            if (width <= 0) return;

            using var brush = new SolidBrush(highlightColor);
            e.Graphics.FillRectangle(brush, 0, pt.Y, width, lineHeight);
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (linkedEditor != null && !linkedEditor.IsDisposed)
            {
                linkedEditor.SelectionChanged -= Editor_SelectionChanged;
                linkedEditor.VScroll -= Editor_ViewChanged;
                linkedEditor.HScroll -= Editor_ViewChanged;
                linkedEditor.Resize -= Editor_ViewChanged;
                linkedEditor.TextChanged -= Editor_ViewChanged;
                linkedEditor.FontChanged -= Editor_FontChanged;
            }

            _updateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

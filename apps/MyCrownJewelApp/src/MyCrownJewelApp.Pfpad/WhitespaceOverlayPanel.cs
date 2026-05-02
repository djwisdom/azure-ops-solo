using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Transparent overlay panel that draws whitespace glyphs (spaces, tabs, newlines)
/// on top of the editor. Maintains stable line positions to prevent jitter.
/// </summary>
public sealed class WhitespaceOverlayPanel : Panel
{
    private RichTextBox? linkedEditor;
    private Color glyphColor = Color.Gray;
    private bool showGlyphs = true;
    private WinFormsTimer? _updateTimer;

    public WhitespaceOverlayPanel()
    {
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
        Enabled = false;
        TabStop = false;

        _updateTimer = new WinFormsTimer { Interval = 16 };
        _updateTimer.Tick += (s, e) => { _updateTimer.Stop(); Invalidate(); };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Skip background painting to maintain transparency
    }

    public RichTextBox? LinkedEditor
    {
        get => linkedEditor;
        set
        {
            if (linkedEditor != null)
            {
                linkedEditor.Resize -= Editor_ViewChanged;
                linkedEditor.VScroll -= Editor_ViewChanged;
                linkedEditor.HScroll -= Editor_ViewChanged;
                linkedEditor.TextChanged -= Editor_ViewChanged;
                linkedEditor.SelectionChanged -= Editor_SelectionChanged;
                linkedEditor.FontChanged -= Editor_FontChanged;
            }

            linkedEditor = value;

            if (linkedEditor != null)
            {
                linkedEditor.Resize += Editor_ViewChanged;
                linkedEditor.VScroll += Editor_ViewChanged;
                linkedEditor.HScroll += Editor_ViewChanged;
                linkedEditor.TextChanged += Editor_ViewChanged;
                linkedEditor.SelectionChanged += Editor_SelectionChanged;
                linkedEditor.FontChanged += Editor_FontChanged;
            }

            Invalidate();
        }
    }

    public Color GlyphColor
    {
        get => glyphColor;
        set { glyphColor = value; Invalidate(); }
    }

    public bool ShowGlyphs
    {
        get => showGlyphs;
        set { showGlyphs = value; Invalidate(); }
    }

    private void Editor_ViewChanged(object? sender, EventArgs e) => RequestUpdate();
    private void Editor_SelectionChanged(object? sender, EventArgs e) => RequestUpdate();
    private void Editor_FontChanged(object? sender, EventArgs e) => Invalidate();

    private void RequestUpdate()
    {
        _updateTimer?.Stop();
        _updateTimer?.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (!showGlyphs || linkedEditor == null || !linkedEditor.Visible || linkedEditor.IsDisposed)
            return;

        try
        {
            var g = e.Graphics;
            var editor = linkedEditor;

            int lineHeight = (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor);
            if (lineHeight <= 0) return;

            // Get visible range via RichTextBox message
            int firstVisible = SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (firstVisible < 0) firstVisible = 0;
            int visibleCount = (editor.ClientSize.Height / lineHeight) + 2;
            int lastVisible = Math.Min(editor.Lines.Length - 1, firstVisible + visibleCount);

            // Base Y from first visible line
            int baseY = 0;
            int firstCharIdx = editor.GetFirstCharIndexFromLine(firstVisible);
            if (firstCharIdx >= 0)
            {
                Point pt0 = editor.GetPositionFromCharIndex(firstCharIdx);
                if (pt0 != Point.Empty) baseY = pt0.Y;
            }

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            for (int lineIdx = firstVisible; lineIdx <= lastVisible; lineIdx++)
            {
                if (lineIdx < 0 || lineIdx >= editor.Lines.Length) continue;
                string line = editor.Lines[lineIdx];
                int lineStartIdx = editor.GetFirstCharIndexFromLine(lineIdx);
                if (lineStartIdx < 0) continue;

                int y = baseY + (lineIdx - firstVisible) * lineHeight;

                // Spaces and tabs
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ' ' || c == '\t')
                    {
                        Point p = editor.GetPositionFromCharIndex(lineStartIdx + i);
                        if (p == Point.Empty) continue;
                        p.Y = y; // lock Y
                        string symbol = c == ' ' ? "·" : "→";
                        TextRenderer.DrawText(g, symbol, editor.Font, p, glyphColor, flags);
                    }
                }

                // Newline arrow
                int newlineIdx = lineStartIdx + line.Length;
                if (newlineIdx < editor.TextLength)
                {
                    Point pNew = editor.GetPositionFromCharIndex(newlineIdx);
                    if (pNew != Point.Empty)
                    {
                        pNew.Y = y;
                        TextRenderer.DrawText(g, "↵", editor.Font, pNew, glyphColor, flags);
                    }
                }
            }
        }
        catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (linkedEditor != null && !linkedEditor.IsDisposed)
            {
                linkedEditor.Resize -= Editor_ViewChanged;
                linkedEditor.VScroll -= Editor_ViewChanged;
                linkedEditor.HScroll -= Editor_ViewChanged;
                linkedEditor.TextChanged -= Editor_ViewChanged;
                linkedEditor.SelectionChanged -= Editor_SelectionChanged;
                linkedEditor.FontChanged -= Editor_FontChanged;
            }
            _updateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
}

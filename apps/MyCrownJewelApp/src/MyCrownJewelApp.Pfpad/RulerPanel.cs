using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// A thin ruler strip displayed above the text editor showing per-character-width
/// tick marks.  Every character position gets a small tick, every 5th character
/// gets a 2×-height tick, and every 10th character is labelled with its number.
/// Syncs horizontal scroll with the editor and follows the current theme.
/// </summary>
public class RulerPanel : Panel
{
    private RichTextBox? _editor;
    private int _charWidthPx;
    private int _scrollOffsetPx;

    // Tick intervals in character units
    private const int MajorTickEvery = 5;   // 2× height
    private const int LabelEvery    = 10;   // numeric label

    private const int MinorTickH = 8;
    private const int MajorTickH = 14;

    private readonly System.Windows.Forms.Timer _scrollPollTimer;

    public RulerPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        TabStop = false;
        Height = 26;
        BackColor = Color.Transparent;
        _charWidthPx = 8;

        _scrollPollTimer = new System.Windows.Forms.Timer { Interval = 160 };
        _scrollPollTimer.Tick += (s, e) => RefreshScrollAndRepaint();
    }

    public void AttachEditor(RichTextBox editor)
    {
        _editor = editor;
        editor.FontChanged += (s, e) => { MeasureCharWidth(); Invalidate(); };
        editor.HandleDestroyed += (s, e) => _scrollPollTimer.Stop();
        MeasureCharWidth();
        _scrollPollTimer.Start();
        Invalidate();
    }

    public void DetachEditor()
    {
        _scrollPollTimer.Stop();
        _editor = null;
        Invalidate();
    }

    private void MeasureCharWidth()
    {
        if (_editor == null || _editor.Font == null) { _charWidthPx = 8; return; }
        using var g = _editor.CreateGraphics();
        _charWidthPx = Math.Max(4, (int)Math.Ceiling(g.MeasureString("M", _editor.Font).Width));
    }

    private void RefreshScrollAndRepaint()
    {
        int newPos = GetHScrollOffset();
        if (newPos != _scrollOffsetPx)
        {
            _scrollOffsetPx = newPos;
            Invalidate();
        }
    }

    private int GetHScrollOffset()
    {
        if (_editor == null || !_editor.IsHandleCreated) return 0;
        try { return GetScrollPos(_editor.Handle, SB_HORZ); }
        catch { return 0; }
    }

    [DllImport("user32.dll")]
    private static extern int GetScrollPos(IntPtr hWnd, int nBar);
    private const int SB_HORZ = 0;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_editor == null || _editor.Font == null || !IsHandleCreated) return;

        var g = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        // Background matching the editor
        using var backBrush = new SolidBrush(theme.EditorBackground);
        g.FillRectangle(backBrush, ClientRectangle);

        if (_charWidthPx <= 0) return;

        // Get current scroll offset
        _scrollOffsetPx = GetHScrollOffset();

        int clientW = ClientSize.Width;
        int bottom = ClientSize.Height;

        // Character range visible
        int firstChar = Math.Max(0, (int)Math.Floor((double)_scrollOffsetPx / _charWidthPx));
        int lastChar = Math.Max(firstChar, (int)Math.Ceiling((double)(_scrollOffsetPx + clientW) / _charWidthPx));

        // Round up to include next label boundary
        lastChar = ((lastChar / LabelEvery) + 1) * LabelEvery;

        using var minorPen = new Pen(theme.Border, 1);
        using var majorPen = new Pen(theme.Muted, 1.5f);
        using var labelBrush = new SolidBrush(theme.Muted);

        using var labelFont = new Font(_editor.Font.FontFamily,
            Math.Max(6, _editor.Font.Size - 3f), FontStyle.Regular);

        for (int ch = firstChar; ch <= lastChar; ch++)
        {
            int x = (ch * _charWidthPx) - _scrollOffsetPx;
            if (x < -_charWidthPx || x > clientW + 5) continue;

            bool isLabel = (ch > 0 && ch % LabelEvery == 0);
            bool isMajor = (ch > 0 && ch % MajorTickEvery == 0);

            if (isLabel)
            {
                // Full-height labeled tick
                g.DrawLine(majorPen, x, bottom - 1, x, bottom - MajorTickH);

                string label = ch.ToString();
                SizeF labelSize = g.MeasureString(label, labelFont);
                float labelX = x + 1;
                if (labelX + labelSize.Width > clientW - 2)
                    labelX = clientW - labelSize.Width - 2;
                if (labelX < 1) labelX = 1;
                g.DrawString(label, labelFont, labelBrush, labelX, 1);
            }
            else if (isMajor)
            {
                // 2× height tick
                g.DrawLine(majorPen, x, bottom - 1, x, bottom - MajorTickH);
            }
            else
            {
                // Small minor tick
                g.DrawLine(minorPen, x, bottom - 1, x, bottom - MinorTickH);
            }
        }

        // Bottom border line
        using var borderPen = new Pen(theme.Border);
        g.DrawLine(borderPen, 0, bottom - 1, clientW, bottom - 1);
    }
}
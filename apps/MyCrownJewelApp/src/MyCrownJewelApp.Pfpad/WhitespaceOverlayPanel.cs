using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class WhitespaceOverlayForm : Form
{
    private RichTextBox? linkedEditor;
    private Color glyphColor = Color.FromArgb(180, 180, 180);
    private bool showGlyphs = false;
    private Form? _ownerForm;

    public Form? OwnerForm
    {
        get => _ownerForm;
        set
        {
            _ownerForm = value;
            if (value != null)
                value.LocationChanged += (s, e) => SyncPosition();
        }
    }

    public WhitespaceOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        Visible = false;
        Text = "";

        SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public RichTextBox? LinkedEditor
    {
        get => linkedEditor;
        set
        {
            if (linkedEditor != null)
            {
                linkedEditor.Resize -= OnEditorChanged;
                linkedEditor.VScroll -= OnEditorChanged;
                linkedEditor.HScroll -= OnEditorChanged;
                linkedEditor.TextChanged -= OnEditorChanged;
                linkedEditor.Disposed -= OnEditorDisposed;
            }
            linkedEditor = value;
            if (linkedEditor != null)
            {
                linkedEditor.Resize += OnEditorChanged;
                linkedEditor.VScroll += OnEditorChanged;
                linkedEditor.HScroll += OnEditorChanged;
                linkedEditor.TextChanged += OnEditorChanged;
                linkedEditor.Disposed += OnEditorDisposed;
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
        set
        {
            showGlyphs = value;
            if (value && linkedEditor != null && !linkedEditor.IsDisposed)
            {
                SyncPosition();
                if (_ownerForm != null && !_ownerForm.IsDisposed)
                    Show(_ownerForm);
                else
                    Visible = true;
                Invalidate();
            }
            else
            {
                Visible = false;
            }
        }
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (!showGlyphs) return;
        SyncPosition();
        Invalidate();
    }
    private void OnEditorDisposed(object? sender, EventArgs e) { ShowGlyphs = false; }

    private void SyncPosition()
    {
        if (linkedEditor == null || linkedEditor.IsDisposed || !linkedEditor.Visible || !showGlyphs)
        {
            Visible = false;
            return;
        }

        var parent = linkedEditor.Parent;
        if (parent is null) return;
        var screenPos = parent.RectangleToScreen(linkedEditor.Bounds);
        Bounds = screenPos;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!showGlyphs || linkedEditor == null || linkedEditor.IsDisposed || !linkedEditor.Visible)
        {
            e.Graphics.Clear(Color.Fuchsia);
            return;
        }

        try
        {
            var g = e.Graphics;
            var editor = linkedEditor;

            int lineHeight = (int)Math.Ceiling(editor.Font.GetHeight() * editor.ZoomFactor);
            if (lineHeight <= 0) return;

            int firstVisible = SendMessage(editor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (firstVisible < 0) firstVisible = 0;
            int visibleCount = (editor.ClientSize.Height / lineHeight) + 2;
            int lastVisible = Math.Min(editor.Lines.Length - 1, firstVisible + visibleCount);

            int baseY = 0;
            int firstCharIdx = editor.GetFirstCharIndexFromLine(firstVisible);
            if (firstCharIdx >= 0)
            {
                Point pt0 = editor.GetPositionFromCharIndex(firstCharIdx);
                if (pt0 != Point.Empty) baseY = pt0.Y;
            }

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            using var glyphFont = new Font(editor.Font.FontFamily, editor.Font.SizeInPoints * 0.75f, FontStyle.Regular);

            for (int lineIdx = firstVisible; lineIdx <= lastVisible; lineIdx++)
            {
                if (lineIdx < 0 || lineIdx >= editor.Lines.Length) continue;
                string line = editor.Lines[lineIdx];
                int lineStartIdx = editor.GetFirstCharIndexFromLine(lineIdx);
                if (lineStartIdx < 0) continue;

                int y = baseY + (lineIdx - firstVisible) * lineHeight;

                int spaceCount = 0;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ' ' || c == '\t')
                        spaceCount++;
                }
                bool hasNewline = lineStartIdx + line.Length < editor.TextLength;

                if (spaceCount == 0 && !hasNewline)
                    continue;

                int[] positions = null;
                if (spaceCount > 0)
                {
                    positions = new int[spaceCount];
                    int idx = 0;
                    for (int i = 0; i < line.Length; i++)
                    {
                        char c = line[i];
                        if (c == ' ' || c == '\t')
                        {
                            Point p = editor.GetPositionFromCharIndex(lineStartIdx + i);
                            positions[idx++] = p.IsEmpty ? -1 : p.X;
                        }
                    }
                }

                int posIdx = 0;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == ' ' || c == '\t')
                    {
                        int x = positions?[posIdx++] ?? -1;
                        if (x < 0) continue;
                        string symbol = c == ' ' ? "\u00B7" : "\u2192";
                        TextRenderer.DrawText(g, symbol, glyphFont, new Point(x, y), glyphColor, flags);
                    }
                }

                if (hasNewline)
                {
                    int newlineIdx = lineStartIdx + line.Length;
                    Point pNew = editor.GetPositionFromCharIndex(newlineIdx);
                    if (pNew != Point.Empty)
                    {
                        pNew.Y = y;
                        TextRenderer.DrawText(g, "\u21B5", glyphFont, pNew, glyphColor, flags);
                    }
                }
            }
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (linkedEditor != null && !linkedEditor.IsDisposed)
                linkedEditor.Disposed -= OnEditorDisposed;
        }
        base.Dispose(disposing);
    }
}

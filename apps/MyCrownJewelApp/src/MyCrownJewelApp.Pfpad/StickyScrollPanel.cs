using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class StickyScrollPanel : Panel
{
    private List<StickyScope> _scopes = new();
    private int _highlightLine = -1;
    private const int HeaderHeight = 22;
    private int _currentWidth;

    public StickyScrollPanel()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = SystemColors.Control;
        TabStop = false;
        DoubleBuffered = true;
    }

    public void SetScopes(List<StickyScope> scopes, int highlightLine)
    {
        _scopes = scopes ?? new();
        _highlightLine = highlightLine;
        Height = _scopes.Count * HeaderHeight;
        Visible = _scopes.Count > 0;
        _currentWidth = 0;
        Invalidate();
    }

    public void SyncWidth(int editorWidth)
    {
        if (_currentWidth != editorWidth)
        {
            _currentWidth = editorWidth;
            Width = editorWidth;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var theme = ThemeManager.Instance.CurrentTheme;

        for (int i = 0; i < _scopes.Count; i++)
        {
            var scope = _scopes[i];
            int y = i * HeaderHeight;

            Color bg = scope.ScopeKind switch
            {
                "Class" or "Struct" or "Interface" or "Record" => theme.ButtonHoverBackground,
                "Method" or "Lambda" => theme.MenuBackground,
                "Namespace" => theme.EditorBackground,
                _ => theme.MenuBackground
            };

            using var bgBrush = new SolidBrush(bg);
            g.FillRectangle(bgBrush, 0, y, Width, HeaderHeight);

            using var sepPen = new Pen(Color.FromArgb(60, theme.Muted));
            g.DrawLine(sepPen, 0, y + HeaderHeight - 1, Width, y + HeaderHeight - 1);

            string label = scope.HeaderText;
            if (label.Length > 100)
                label = label[..97] + "...";

            string prefix = scope.ScopeKind switch
            {
                "Class" => "class ",
                "Struct" => "struct ",
                "Interface" => "interface ",
                "Enum" => "enum ",
                "Record" => "record ",
                "Namespace" => "ns ",
                "Method" => "",
                "Region" => "",
                _ => ""
            };

            string display = !string.IsNullOrEmpty(prefix)
                ? (label.StartsWith(prefix.Trim()) ? label : prefix + label)
                : label;

            using var textBrush = new SolidBrush(theme.Text);
            using var labelFont = new System.Drawing.Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);
            g.DrawString(display, labelFont, textBrush, 6, y + 2);
        }
    }

    private const int WM_NCHITTEST = 0x0084;
    private static readonly IntPtr HTTRANSPARENT = new IntPtr(-1);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }
}

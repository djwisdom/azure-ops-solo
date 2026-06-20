using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>A single segment in the breadcrumb trail.</summary>
public sealed class BreadcrumbSegment
{
    public string Label    { get; init; } = "";
    /// <summary>True for path-folder segments (rendered in muted color).</summary>
    public bool   IsPath   { get; init; }
    /// <summary>ScopeKind icon hint (Class, Method, Constructor, Lambda, …).</summary>
    public string KindHint { get; init; } = "";
}

public sealed class BreadcrumbPanel : Panel
{
    private IReadOnlyList<BreadcrumbSegment> _segments = Array.Empty<BreadcrumbSegment>();
    private const int PanelHeight = 24;
    private const string Separator = "  ›  ";

    // ── Backward-compat shims (Form1 still sets FilePath + CurrentScope) ─────
    private string _filePath    = "";
    private string _currentScope = "";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string FilePath
    {
        get => _filePath;
        set { _filePath = value ?? ""; /* rebuilt via SetSegments */ }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string CurrentScope
    {
        get => _currentScope;
        set { _currentScope = value ?? ""; /* rebuilt via SetSegments */ }
    }

    /// <summary>Set the full breadcrumb chain: path segments first, then symbol segments.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<BreadcrumbSegment> Segments
    {
        get => _segments;
        set { _segments = value ?? Array.Empty<BreadcrumbSegment>(); Invalidate(); }
    }

    public BreadcrumbPanel()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false;
        DoubleBuffered = true;
        Height = PanelHeight;
        Dock = DockStyle.Top;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var theme = ThemeManager.Instance.CurrentTheme;

        using var bgBrush = new SolidBrush(theme.EditorBackground);
        g.FillRectangle(bgBrush, 0, 0, Width, Height);

        using var bottomPen = new Pen(Color.FromArgb(60, theme.Muted));
        g.DrawLine(bottomPen, 0, Height - 1, Width, Height - 1);

        if (_segments.Count == 0)
        {
            // Fallback: legacy FilePath display
            string fn = string.IsNullOrEmpty(_filePath) ? "Untitled" : Path.GetFileName(_filePath);
            string display = fn;
            if (!string.IsNullOrEmpty(_currentScope))
                display = fn + " \u203A " + _currentScope;
            using var fb = new SolidBrush(Color.FromArgb(200, theme.Text));
            using var ff = new Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);
            g.DrawString(display, ff, fb, 6f, (Height - ff.Height) / 2f);
            return;
        }

        // ── Build render list ────────────────────────────────────────────────
        using var pathFont   = new Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);
        using var symFont    = new Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);
        using var sepFont    = new Font(Font.FontFamily, Font.Size - 1, FontStyle.Regular);

        using var pathBrush  = new SolidBrush(Color.FromArgb(130, theme.Muted));
        using var symBrush   = new SolidBrush(Color.FromArgb(220, theme.Text));
        using var sepBrush   = new SolidBrush(Color.FromArgb(80, theme.Muted));
        using var ellipBrush = new SolidBrush(Color.FromArgb(100, theme.Muted));

        // Measure each part
        var parts = new List<(string text, bool isPath, float width)>();
        float sepW = g.MeasureString(Separator, sepFont).Width;

        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            string label = KindPrefix(seg.KindHint) + seg.Label;
            float w = g.MeasureString(label, seg.IsPath ? pathFont : symFont).Width;
            parts.Add((label, seg.IsPath, w));
        }

        // Calculate total width with separators
        float totalW = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            totalW += parts[i].width;
            if (i < parts.Count - 1) totalW += sepW;
        }

        const float leftPad = 8f;
        float available = Width - leftPad - 4f;

        // If overflow: drop path segments from left, keep rightmost symbols
        // Show "… ›" prefix when truncated
        int startIdx = 0;
        bool truncated = false;
        if (totalW > available)
        {
            float ellipW = g.MeasureString("…", sepFont).Width + sepW;
            float used = ellipW;
            // Walk from the right, include as many segments as fit
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                float needed = parts[i].width + (i < parts.Count - 1 ? sepW : 0);
                if (used + needed <= available)
                {
                    used += needed;
                    startIdx = i;
                }
                else
                {
                    startIdx = i + 1;
                    truncated = true;
                    break;
                }
            }
        }

        float x = leftPad;
        float y = (Height - pathFont.Height) / 2f;

        if (truncated)
        {
            g.DrawString("…", sepFont, ellipBrush, x, y);
            x += g.MeasureString("…", sepFont).Width;
            g.DrawString(Separator, sepFont, sepBrush, x, y);
            x += sepW;
        }

        for (int i = startIdx; i < parts.Count; i++)
        {
            var (text, isPath, _) = parts[i];
            var brush = isPath ? pathBrush : symBrush;
            var font  = isPath ? pathFont  : symFont;
            g.DrawString(text, font, brush, x, y);
            x += g.MeasureString(text, font).Width;
            if (i < parts.Count - 1)
            {
                g.DrawString(Separator, sepFont, sepBrush, x, y);
                x += sepW;
            }
        }
    }

    /// <summary>Returns a compact unicode prefix icon for the given ScopeKind.</summary>
    private static string KindPrefix(string kind) => kind switch
    {
        "Class"       => "○ ",
        "Struct"      => "◆ ",
        "Interface"   => "◯ ",
        "Enum"        => "№ ",
        "Record"      => "⊕ ",
        "Namespace"   => "⌂ ",
        "Method"      => "⚙ ",
        "Constructor" => "⚙ ",
        "Lambda"      => "λ ",
        _             => ""
    };

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

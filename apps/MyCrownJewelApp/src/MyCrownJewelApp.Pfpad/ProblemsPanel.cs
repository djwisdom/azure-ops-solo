using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class ProblemsPanel : UserControl
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _headerLabel;
    private readonly ToolStripButton _closeButton;
    private readonly ListBox _problemList;
    private List<Diagnostic> _diagnostics = new();

    public event Action<string, int>? ProblemSelected;
    public event Action? CloseRequested;

    public ProblemsPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _headerLabel = new ToolStripLabel("Problems")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Margin = new Padding(4, 0, 0, 0)
        };

        _closeButton = new ToolStripButton
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right,
            AutoSize = false,
            Width = 22,
            Height = 22
        };
        _closeButton.Click += (s, e) => CloseRequested?.Invoke();

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(2, 0, 0, 0),
            AutoSize = false,
            Height = 24,
            Renderer = new FlatToolStripRenderer()
        };
        _headerStrip.Items.Add(_headerLabel);
        _headerStrip.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
        _headerStrip.Items.Add(_closeButton);

        _problemList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            ItemHeight = 20,
            Cursor = Cursors.Hand
        };
        _problemList.DrawItem += ProblemList_DrawItem;
        _problemList.MeasureItem += (s, e) => e.ItemHeight = 22;
        _problemList.MouseDoubleClick += ProblemList_DoubleClick;
        _problemList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && _problemList.SelectedItem != null) OpenSelectedProblem(); };
        _problemList.HandleCreated += (s, e) => ApplyScrollbarTheme(_problemList.Handle);

        Controls.Add(_problemList);
        Controls.Add(_headerStrip);

        SetTheme(ThemeManager.Instance.CurrentTheme);
    }

    public void SetDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        if (InvokeRequired) { BeginInvoke(() => SetDiagnostics(diagnostics)); return; }
        _diagnostics = new List<Diagnostic>(diagnostics);

        int errorCount = 0, warningCount = 0, otherCount = 0;
        foreach (var d in _diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error) errorCount++;
            else if (d.Severity == DiagnosticSeverity.Warning) warningCount++;
            else otherCount++;
        }

        string label = "Problems";
        if (errorCount > 0 || warningCount > 0 || otherCount > 0)
        {
            var parts = new List<string>();
            if (errorCount > 0) parts.Add($"{errorCount} error{(errorCount != 1 ? "s" : "")}");
            if (warningCount > 0) parts.Add($"{warningCount} warning{(warningCount != 1 ? "s" : "")}");
            if (otherCount > 0) parts.Add($"{otherCount} other");
            label += $" ({string.Join(", ", parts)})";
        }
        _headerLabel.Text = label;

        _problemList.BeginUpdate();
        _problemList.Items.Clear();
        foreach (var d in _diagnostics)
            _problemList.Items.Add(d);
        _problemList.EndUpdate();
    }

    private void ProblemList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _problemList.Items.Count) return;
        if (_problemList.Items[e.Index] is not Diagnostic d) return;

        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        using var severityFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var msgFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var locFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var ruleFont = new Font("Segoe UI", 7f, FontStyle.Regular);

        int x = rect.X + 4;

        // Severity badge
        var badgeRect = new Rectangle(x, rect.Y + 3, 14, 14);
        using var badgeBrush = new SolidBrush(d.Color);
        e.Graphics.FillRectangle(badgeBrush, badgeRect);
        TextRenderer.DrawText(e.Graphics, d.SeverityLabel[..1], severityFont, badgeRect,
            Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        x += 20;
        int msgMaxWidth = rect.Width - x - 80;

        // Message
        TextRenderer.DrawText(e.Graphics, d.Message, msgFont,
            new Rectangle(x, rect.Y + 2, Math.Max(msgMaxWidth, 20), 18), theme.Text,
            TextFormatFlags.NoPadding | TextFormatFlags.WordEllipsis);

        // Location
        string loc = $"Ln {d.Line}";
        int locWidth = TextRenderer.MeasureText(loc, locFont).Width;
        TextRenderer.DrawText(e.Graphics, loc, locFont,
            new Point(rect.Right - locWidth - 4, rect.Y + 2), theme.Muted,
            TextFormatFlags.NoPadding | TextFormatFlags.WordEllipsis);
    }

    private void ProblemList_DoubleClick(object? sender, MouseEventArgs e)
    {
        int idx = _problemList.IndexFromPoint(e.Location);
        if (idx >= 0 && idx < _problemList.Items.Count && _problemList.Items[idx] is Diagnostic d)
            ProblemSelected?.Invoke(d.File, d.Line);
    }

    private void OpenSelectedProblem()
    {
        if (_problemList.SelectedItem is Diagnostic d)
            ProblemSelected?.Invoke(d.File, d.Line);
    }

    public IReadOnlyList<Diagnostic> GetDiagnostics() => _diagnostics.AsReadOnly();

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _headerLabel.ForeColor = theme.Muted;
        _closeButton.ForeColor = theme.Text;
        _problemList.BackColor = theme.MenuBackground;
        _problemList.ForeColor = theme.Text;
        ApplyScrollbarTheme(_problemList.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : null, null);
    }
}

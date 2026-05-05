using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class SymbolPanel : UserControl
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _headerLabel;
    private readonly ToolStripButton _closeButton;
    private readonly TextBox _searchBox;
    private readonly ListBox _symbolList;
    private readonly SymbolIndexService _index;
    private List<SymbolLocation> _allSymbols = new();
    private List<SymbolLocation> _filtered = new();

    public event Action<string, int>? SymbolSelected;
    public event Action? CloseRequested;

    public SymbolPanel(SymbolIndexService index)
    {
        _index = index;
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _headerLabel = new ToolStripLabel("Symbols")
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

        _searchBox = new TextBox
        {
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Text = "",
            Height = 22
        };
        _searchBox.TextChanged += SearchBox_TextChanged;

        _symbolList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            ItemHeight = 20,
            Cursor = Cursors.Hand
        };
        _symbolList.DrawItem += SymbolList_DrawItem;
        _symbolList.MeasureItem += (s, e) => e.ItemHeight = 22;
        _symbolList.MouseDoubleClick += SymbolList_DoubleClick;
        _symbolList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && _symbolList.SelectedItem != null) OpenSelectedSymbol(); };
        _symbolList.HandleCreated += (s, e) => ApplyScrollbarTheme(_symbolList.Handle);

        Controls.Add(_symbolList);
        Controls.Add(_searchBox);
        Controls.Add(_headerStrip);

        _index.OnIndexUpdated += RefreshSymbols;
        SetTheme(ThemeManager.Instance.CurrentTheme);
    }

    public void RefreshSymbols()
    {
        if (InvokeRequired) { BeginInvoke(RefreshSymbols); return; }
        _allSymbols = _index.GetAllSymbols().ToList();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string filter = _searchBox.Text.Trim();
        if (string.IsNullOrEmpty(filter))
            _filtered = _allSymbols;
        else
        {
            _filtered = _allSymbols
                .Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || s.Context.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || s.Kind.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _symbolList.BeginUpdate();
        _symbolList.Items.Clear();
        foreach (var s in _filtered)
            _symbolList.Items.Add(s);
        _symbolList.EndUpdate();

        _headerLabel.Text = $"Symbols ({_filtered.Count})";
    }

    private void SymbolList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _symbolList.Items.Count) return;
        if (_symbolList.Items[e.Index] is not SymbolLocation sym) return;

        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        Color kindColor = sym.Kind switch
        {
            SymbolKind.Class => Color.FromArgb(78, 201, 176),
            SymbolKind.Interface => Color.FromArgb(184, 147, 217),
            SymbolKind.Enum => Color.FromArgb(184, 147, 217),
            SymbolKind.Struct => Color.FromArgb(78, 201, 176),
            SymbolKind.Method => Color.FromArgb(130, 170, 255),
            SymbolKind.Property => Color.FromArgb(220, 198, 124),
            SymbolKind.Field => Color.FromArgb(220, 198, 124),
            SymbolKind.Function => Color.FromArgb(130, 170, 255),
            SymbolKind.Variable => Color.FromArgb(220, 198, 124),
            _ => theme.Muted
        };

        using var kindFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var fileFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);

        string kindLabel = sym.Kind switch
        {
            SymbolKind.Class => "C",
            SymbolKind.Interface => "I",
            SymbolKind.Enum => "E",
            SymbolKind.Struct => "S",
            SymbolKind.Method => "M",
            SymbolKind.Property => "P",
            SymbolKind.Field => "F",
            SymbolKind.Function => "F",
            SymbolKind.Variable => "V",
            _ => "?"
        };

        int x = rect.X + 4;
        var kindRect = new Rectangle(x, rect.Y + 3, 16, 16);
        using var kindBg = new SolidBrush(kindColor);
        e.Graphics.FillRectangle(kindBg, kindRect);
        TextRenderer.DrawText(e.Graphics, kindLabel, kindFont, kindRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        x += 22;
        TextRenderer.DrawText(e.Graphics, sym.Name, nameFont,
            new Point(x, rect.Y + 2), theme.Text,
            TextFormatFlags.NoPadding | TextFormatFlags.WordEllipsis);

        int nameWidth = TextRenderer.MeasureText(sym.Name, nameFont).Width;
        x += Math.Min(nameWidth, rect.Width - x - 120);

        TextRenderer.DrawText(e.Graphics, Path.GetFileName(sym.File), fileFont,
            new Point(x + 4, rect.Y + 2), theme.Muted,
            TextFormatFlags.NoPadding | TextFormatFlags.WordEllipsis);
    }

    private void SymbolList_DoubleClick(object? sender, MouseEventArgs e)
    {
        int idx = _symbolList.IndexFromPoint(e.Location);
        if (idx >= 0 && idx < _symbolList.Items.Count && _symbolList.Items[idx] is SymbolLocation sym)
        {
            SymbolSelected?.Invoke(sym.File, sym.Line);
        }
    }

    private void OpenSelectedSymbol()
    {
        if (_symbolList.SelectedItem is SymbolLocation sym)
            SymbolSelected?.Invoke(sym.File, sym.Line);
    }

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _headerLabel.ForeColor = theme.Muted;
        _closeButton.ForeColor = theme.Text;
        _searchBox.BackColor = theme.EditorBackground;
        _searchBox.ForeColor = theme.Text;
        _symbolList.BackColor = theme.MenuBackground;
        _symbolList.ForeColor = theme.Text;
        ApplyScrollbarTheme(_symbolList.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : null, null);
    }
}

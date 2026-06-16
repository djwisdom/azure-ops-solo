using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class CommandPaletteForm : Form
{
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly List<CommandEntry> _allCommands;
    private List<CommandEntry> _filtered;

    public sealed record CommandEntry(string Name, string Shortcut, Action Execute);

    public CommandPaletteForm(List<CommandEntry> commands)
    {
        _allCommands = commands;
        _filtered = commands;

        var theme = ThemeManager.Instance.CurrentTheme;

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = theme.MenuBackground;
        ClientSize = new Size(500, 350);
        ShowInTaskbar = false;
        TopMost = true;

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 30,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 11),
        };
        _searchBox.TextChanged += OnSearchChanged;
        _searchBox.KeyDown += OnSearchKeyDown;

        _resultsList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10),
            IntegralHeight = false,
            ItemHeight = 22,
        };
        _resultsList.DrawMode = DrawMode.OwnerDrawFixed;
        _resultsList.DrawItem += OnDrawItem;
        _resultsList.MouseDoubleClick += (_, _) => ExecuteSelected();
        _resultsList.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.IsInputKey = true; }
        };
        _resultsList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { ExecuteSelected(); e.Handled = true; }
        };

        var searchBoxWrapper = FlatUiHelper.WrapFlat(_searchBox, theme);
        searchBoxWrapper.Dock = DockStyle.Top;
        searchBoxWrapper.Height = 30;

        Controls.Add(_resultsList);
        Controls.Add(searchBoxWrapper);

        PopulateList();

        // Escape to close
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _searchBox.Focus();
    }

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        string filter = _searchBox.Text.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            _filtered = _allCommands;
        }
        else
        {
            var lower = filter.ToLowerInvariant();
            _filtered = _allCommands
                .Where(c => c.Name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                         || c.Shortcut.Contains(lower, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c =>
                {
                    int idx = c.Name.IndexOf(lower, StringComparison.OrdinalIgnoreCase);
                    return idx < 0 ? 999 : idx;
                })
                .ThenBy(c => c.Name.Length)
                .ToList();
        }
        PopulateList();
    }

    private void PopulateList()
    {
        _resultsList.BeginUpdate();
        _resultsList.Items.Clear();
        foreach (var cmd in _filtered)
            _resultsList.Items.Add(cmd);
        _resultsList.EndUpdate();
        if (_resultsList.Items.Count > 0)
            _resultsList.SelectedIndex = 0;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            if (_resultsList.SelectedIndex < _resultsList.Items.Count - 1)
                _resultsList.SelectedIndex++;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_resultsList.SelectedIndex > 0)
                _resultsList.SelectedIndex--;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ExecuteSelected()
    {
        if (_resultsList.SelectedItem is CommandEntry cmd)
        {
            Close();
            cmd.Execute();
        }
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _resultsList.Items.Count) return;
        var cmd = (CommandEntry)_resultsList.Items[e.Index];

        e.DrawBackground();

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color textColor = selected ? Color.White : Color.FromArgb(200, 200, 200);
        Color shortcutColor = selected ? Color.FromArgb(180, 180, 180) : Color.FromArgb(120, 120, 120);

        if (selected)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(60, 100, 180));
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        using var nameFont = new Font("Segoe UI", 9);
        using var shortcutFont = new Font("Consolas", 8);
        using var nameBrush = new SolidBrush(textColor);
        using var shortcutBrush = new SolidBrush(shortcutColor);

        var nameBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 100, e.Bounds.Height);
        e.Graphics.DrawString(cmd.Name, nameFont, nameBrush, nameBounds);

        if (!string.IsNullOrEmpty(cmd.Shortcut))
        {
            var shortcutBounds = new Rectangle(e.Bounds.Right - 110, e.Bounds.Y + 3, 100, e.Bounds.Height - 4);
            var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(cmd.Shortcut, shortcutFont, shortcutBrush, shortcutBounds, format);
        }

        e.DrawFocusRectangle();
    }
}

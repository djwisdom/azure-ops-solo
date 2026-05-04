using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GoToDefinitionPicker : Form
{
    private readonly ListView _listView;
    private string? _selectedFile;
    private int _selectedLine;

    public string? SelectedFile => _selectedFile;
    public int SelectedLine => _selectedLine;

    public GoToDefinitionPicker(string symbolName, IReadOnlyList<SymbolLocation> locations)
    {
        Text = $"Go to Definition: {symbolName}";
        Size = new Size(700, 350);
        MinimumSize = new Size(400, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            MultiSelect = false
        };
        _listView.Columns.Add("Symbol", 140);
        _listView.Columns.Add("Kind", 80);
        _listView.Columns.Add("File", 200);
        _listView.Columns.Add("Line", 50);
        _listView.Columns.Add("Context", 200);
        _listView.DoubleClick += (s, e) => SelectAndClose();
        _listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { SelectAndClose(); e.Handled = true; } };

        foreach (var loc in locations)
        {
            var item = new ListViewItem(new[]
            {
                loc.Name,
                loc.Kind.ToString(),
                System.IO.Path.GetFileName(loc.File),
                loc.Line.ToString(),
                loc.Context
            })
            {
                Tag = loc
            };
            _listView.Items.Add(item);
        }

        if (_listView.Items.Count > 0)
            _listView.Items[0].Selected = true;

        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 24,
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Text,
            Renderer = new FlatToolStripRenderer()
        };

        var countLabel = new ToolStripLabel($"{locations.Count} definition(s) for '{symbolName}'")
        {
            Font = new Font("Segoe UI", 8.25f),
            Margin = new Padding(4, 0, 0, 0),
            ForeColor = theme.Muted
        };

        var openBtn = new ToolStripButton("Go to Definition")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right
        };
        openBtn.Click += (s, e) => SelectAndClose();

        var cancelBtn = new ToolStripButton("Cancel")
        {
            Font = new Font("Segoe UI", 8.25f),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right
        };
        cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;

        toolbar.Items.Add(countLabel);
        toolbar.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
        toolbar.Items.Add(openBtn);
        toolbar.Items.Add(cancelBtn);

        Controls.Add(_listView);
        Controls.Add(toolbar);
    }

    private void SelectAndClose()
    {
        if (_listView.SelectedItems.Count > 0 && _listView.SelectedItems[0].Tag is SymbolLocation loc)
        {
            _selectedFile = loc.File;
            _selectedLine = loc.Line;
            DialogResult = DialogResult.OK;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var theme = ThemeManager.Instance.CurrentTheme;
        using var p = new Pen(theme.Border, 1);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

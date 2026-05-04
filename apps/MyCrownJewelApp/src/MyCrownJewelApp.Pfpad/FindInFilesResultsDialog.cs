namespace MyCrownJewelApp.Pfpad;

internal sealed class FindInFilesResultsDialog : Form
{
    private readonly ListView _listView;

    public FindInFilesResultsDialog(List<(string File, int Line, string Text)> results, string rootDir)
    {
        Text = $"Find in Files - {results.Count} results";
        Size = new Size(800, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;

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
            GridLines = false,
            MultiSelect = false
        };
        _listView.Columns.Add("File", 300);
        _listView.Columns.Add("Line", 60);
        _listView.Columns.Add("Content", 400);
        _listView.DoubleClick += (s, e) => OpenSelectedResult(rootDir);
        _listView.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { OpenSelectedResult(rootDir); e.Handled = true; } };

        foreach (var (file, line, text) in results)
        {
            var item = new ListViewItem(new[] { file, line.ToString(), text })
            {
                Tag = (file, line)
            };
            _listView.Items.Add(item);
        }

        var header = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 24,
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Text,
            Renderer = new FlatToolStripRenderer()
        };

        var countLabel = new ToolStripLabel($"{results.Count} result(s) in {rootDir}")
        {
            Font = new Font("Segoe UI", 8.25f),
            Margin = new Padding(4, 0, 0, 0),
            ForeColor = theme.Muted
        };

        var openButton = new ToolStripButton("Open")
        {
            Font = new Font("Segoe UI", 8.25f),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right
        };
        openButton.Click += (s, e) => OpenSelectedResult(rootDir);

        header.Items.Add(countLabel);
        header.Items.Add(openButton);

        Controls.Add(_listView);
        Controls.Add(header);
    }

    private void OpenSelectedResult(string rootDir)
    {
        if (_listView.SelectedItems.Count == 0) return;
        if (_listView.SelectedItems[0].Tag is ValueTuple<string, int> tag)
        {
            string fullPath = Path.GetFullPath(Path.Combine(rootDir, tag.Item1));
            if (File.Exists(fullPath))
            {
                Form1? form = Owner as Form1 ?? Application.OpenForms["Form1"] as Form1;
                form?.OpenFileInNewTab(fullPath);
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

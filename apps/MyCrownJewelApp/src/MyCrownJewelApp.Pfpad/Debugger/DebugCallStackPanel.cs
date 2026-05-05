namespace MyCrownJewelApp.Pfpad.Debugger;

internal sealed class DebugCallStackPanel : Form
{
    private readonly ListView _list;
    private readonly DebugSession _session;
    private Dap.StackFrame[]? _frames;
    private readonly Form1 _mainForm;

    public event Action<string, int>? NavigateToFile;

    public DebugCallStackPanel(DebugSession session, Form1 mainForm)
    {
        _session = session;
        _mainForm = mainForm;
        Text = "Call Stack";
        Size = new Size(400, 200);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _list = new ListView
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
        _list.Columns.Add("Level", 50);
        _list.Columns.Add("Function", 200);
        _list.Columns.Add("File", 250);
        _list.Columns.Add("Line", 60);
        _list.DoubleClick += (s, e) => NavigateSelected();
        _list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { NavigateSelected(); e.Handled = true; } };

        Controls.Add(_list);

        var header = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 24,
            BackColor = theme.TerminalHeaderBackground,
            Renderer = new FlatToolStripRenderer()
        };
        header.Items.Add(new ToolStripLabel("Call Stack") { Font = new Font("Segoe UI", 8.25f) });
        Controls.Add(header);
        header.BringToFront();
    }

    public async Task RefreshAsync(int? threadId = null)
    {
        _frames = await _session.GetStackTraceAsync(threadId);

        _list.BeginUpdate();
        _list.Items.Clear();

        if (_frames != null)
        {
            for (int i = 0; i < _frames.Length; i++)
            {
                var f = _frames[i];
                string file = f.Source?.Path ?? f.Source?.Name ?? "";
                var item = new ListViewItem(new[]
                {
                    $"#{i}",
                    f.Name,
                    Path.GetFileName(file),
                    f.Line.ToString()
                })
                {
                    Tag = (file, f.Line, f.Id)
                };
                if (i == 0)
                    item.Font = new Font(_list.Font, FontStyle.Bold);
                _list.Items.Add(item);
            }
        }

        _list.EndUpdate();

        if (_frames is { Length: > 0 })
            _session.EvaluateAsync("0+0", _frames[0].Id, "hover");
    }

    private void NavigateSelected()
    {
        if (_list.SelectedItems.Count == 0) return;
        var item = _list.SelectedItems[0];
        if (item.Tag is ValueTuple<string, int, int> tag)
        {
            string filePath = tag.Item1;
            int line = tag.Item2;
            int frameId = tag.Item3;

            _mainForm.BeginInvoke(() =>
            {
                if (File.Exists(filePath))
                    _mainForm.NavigateToFileLine(filePath, line);
            });
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

using System.Diagnostics;

namespace MyCrownJewelApp.Pfpad;

internal sealed class ExternalToolsConfigDialog : Form
{
    private readonly List<ExternalTool> _tools;
    private ListBox _toolListBox = null!;
    private TextBox _nameTextBox = null!;
    private TextBox _commandTextBox = null!;
    private TextBox _argumentsTextBox = null!;
    private TextBox _initialDirTextBox = null!;
    private CheckBox _promptArgsCheckBox = null!;
    private CheckBox _useShellCheckBox = null!;
    private Button _addButton = null!;
    private Button _removeButton = null!;
    private Button _okButton = null!;
    private Button _cancelButton = null!;
    private Button _browseCommandButton = null!;
    private Button _browseDirButton = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Label _argsHintLabel = null!;

    public ExternalToolsConfigDialog(List<ExternalTool> tools)
    {
        _tools = tools;
        Text = "External Tools";
        Size = new Size(700, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        InitializeForm();
        ApplyTheme();
        LoadTools();
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        foreach (Control c in Controls)
            StyleControl(c, theme);

        _argsHintLabel.ForeColor = theme.Muted;
    }

    private static void StyleControl(Control c, Theme theme)
    {
        if (c is Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = theme.PanelBackground;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.BorderColor = theme.Border;
            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        }
        else if (c is TextBox tb)
        {
            tb.BackColor = theme.EditorBackground;
            tb.ForeColor = theme.Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is ListBox lb)
        {
            lb.BackColor = theme.EditorBackground;
            lb.ForeColor = theme.Text;
            lb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (c is CheckBox cb)
        {
            cb.BackColor = Color.Transparent;
            cb.ForeColor = theme.Text;
        }
        else if (c is Label lbl && lbl != c) // skip hint labels with special color
        {
            lbl.BackColor = Color.Transparent;
            lbl.ForeColor = theme.Text;
        }

        if (c.HasChildren)
        {
            foreach (Control child in c.Controls)
                StyleControl(child, theme);
        }
    }

    private void InitializeForm()
    {
        var theme = ThemeManager.Instance.CurrentTheme;

        // Left panel: tool list
        _toolListBox = new ListBox
        {
            Location = new Point(12, 12),
            Size = new Size(200, 280),
            IntegralHeight = false
        };
        _toolListBox.SelectedIndexChanged += ToolListBox_SelectedIndexChanged;

        _addButton = new Button { Text = "&Add", Location = new Point(12, 298), Width = 60 };
        _addButton.Click += (s, e) => AddTool();

        _removeButton = new Button { Text = "&Remove", Location = new Point(78, 298), Width = 70, Enabled = false };
        _removeButton.Click += (s, e) => RemoveTool();

        _moveUpButton = new Button { Text = "\u25B2", Location = new Point(154, 298), Width = 26, Height = 26, Enabled = false };
        _moveUpButton.Click += (s, e) => MoveTool(-1);

        _moveDownButton = new Button { Text = "\u25BC", Location = new Point(186, 298), Width = 26, Height = 26, Enabled = false };
        _moveDownButton.Click += (s, e) => MoveTool(1);

        // Right panel: properties
        var propsGroup = new GroupBox
        {
            Text = "Tool Properties",
            Location = new Point(224, 12),
            Size = new Size(460, 310),
            BackColor = Color.Transparent,
            ForeColor = theme.Text
        };

        var nameLabel = new Label { Text = "&Title:", Location = new Point(10, 25), AutoSize = true };
        _nameTextBox = new TextBox { Location = new Point(80, 22), Width = 360 };

        var cmdLabel = new Label { Text = "&Command:", Location = new Point(10, 55), AutoSize = true };
        _commandTextBox = new TextBox { Location = new Point(80, 52), Width = 320 };
        _browseCommandButton = new Button { Text = "...", Location = new Point(406, 50), Width = 30, Height = 24 };
        _browseCommandButton.Click += (s, e) => BrowseCommand();

        var argsLabel = new Label { Text = "&Arguments:", Location = new Point(10, 85), AutoSize = true };
        _argumentsTextBox = new TextBox { Location = new Point(80, 82), Width = 360 };

        _argsHintLabel = new Label
        {
            Text = "Variables: $(FilePath), $(FileDir), $(FileName), $(FileExt), $(SelText), $(CurLine), $(CurCol)",
            Location = new Point(80, 105),
            AutoSize = true,
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8)
        };

        var dirLabel = new Label { Text = "&Init Dir:", Location = new Point(10, 130), AutoSize = true };
        _initialDirTextBox = new TextBox { Location = new Point(80, 127), Width = 320 };
        _browseDirButton = new Button { Text = "...", Location = new Point(406, 125), Width = 30, Height = 24 };
        _browseDirButton.Click += (s, e) => BrowseDirectory();

        _promptArgsCheckBox = new CheckBox { Text = "&Prompt for arguments", Location = new Point(80, 160), AutoSize = true };
        _useShellCheckBox = new CheckBox { Text = "Use &shell execute (run as separate window)", Location = new Point(80, 185), AutoSize = true };
        _useShellCheckBox.Checked = true;

        propsGroup.Controls.AddRange(new Control[]
        {
            nameLabel, _nameTextBox,
            cmdLabel, _commandTextBox, _browseCommandButton,
            argsLabel, _argumentsTextBox, _argsHintLabel,
            dirLabel, _initialDirTextBox, _browseDirButton,
            _promptArgsCheckBox, _useShellCheckBox
        });

        // Bottom buttons
        _okButton = new Button { Text = "OK", Location = new Point(530, 410), Width = 75, DialogResult = DialogResult.OK };
        _okButton.Click += (s, e) => SaveTools();

        _cancelButton = new Button { Text = "Cancel", Location = new Point(613, 410), Width = 75, DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[]
        {
            _toolListBox, _addButton, _removeButton, _moveUpButton, _moveDownButton,
            propsGroup, _okButton, _cancelButton
        });

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void LoadTools()
    {
        _toolListBox.Items.Clear();
        foreach (var tool in _tools)
            _toolListBox.Items.Add(tool);
    }

    private void SaveTools()
    {
        SaveCurrentTool();
    }

    private void AddTool()
    {
        SaveCurrentTool();
        var tool = new ExternalTool { Name = "New Tool" };
        _tools.Add(tool);
        _toolListBox.Items.Add(tool);
        _toolListBox.SelectedIndex = _toolListBox.Items.Count - 1;
    }

    private void RemoveTool()
    {
        if (_toolListBox.SelectedItem is not ExternalTool tool) return;
        int idx = _toolListBox.SelectedIndex;
        _tools.Remove(tool);
        _toolListBox.Items.Remove(tool);
        if (_tools.Count > 0)
            _toolListBox.SelectedIndex = Math.Min(idx, _tools.Count - 1);
    }

    private void MoveTool(int direction)
    {
        int idx = _toolListBox.SelectedIndex;
        int target = idx + direction;
        if (idx < 0 || target < 0 || target >= _tools.Count) return;

        SaveCurrentTool();

        (_tools[idx], _tools[target]) = (_tools[target], _tools[idx]);
        object item = _toolListBox.Items[idx];
        _toolListBox.Items[idx] = _toolListBox.Items[target];
        _toolListBox.Items[target] = item;
        _toolListBox.SelectedIndex = target;
    }

    private void SaveCurrentTool()
    {
        if (_toolListBox.SelectedItem is not ExternalTool tool) return;
        tool.Name = _nameTextBox.Text;
        tool.Command = _commandTextBox.Text;
        tool.Arguments = _argumentsTextBox.Text;
        tool.InitialDirectory = _initialDirTextBox.Text;
        tool.PromptForArguments = _promptArgsCheckBox.Checked;
        tool.UseShellExecute = _useShellCheckBox.Checked;
        int idx = _toolListBox.SelectedIndex;
        _toolListBox.Items[idx] = _toolListBox.Items[idx]; // force redraw
    }

    private void ToolListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        SaveCurrentTool();
        bool hasSelection = _toolListBox.SelectedItem is ExternalTool;
        _removeButton.Enabled = hasSelection;
        _moveUpButton.Enabled = hasSelection && _toolListBox.SelectedIndex > 0;
        _moveDownButton.Enabled = hasSelection && _toolListBox.SelectedIndex < _toolListBox.Items.Count - 1;

        if (_toolListBox.SelectedItem is ExternalTool tool)
        {
            _nameTextBox.Text = tool.Name;
            _commandTextBox.Text = tool.Command;
            _argumentsTextBox.Text = tool.Arguments;
            _initialDirTextBox.Text = tool.InitialDirectory;
            _promptArgsCheckBox.Checked = tool.PromptForArguments;
            _useShellCheckBox.Checked = tool.UseShellExecute;
            _nameTextBox.Enabled = true;
            _commandTextBox.Enabled = true;
            _argumentsTextBox.Enabled = true;
            _initialDirTextBox.Enabled = true;
            _browseCommandButton.Enabled = true;
            _browseDirButton.Enabled = true;
            _promptArgsCheckBox.Enabled = true;
            _useShellCheckBox.Enabled = true;
        }
        else
        {
            _nameTextBox.Text = "";
            _commandTextBox.Text = "";
            _argumentsTextBox.Text = "";
            _initialDirTextBox.Text = "";
            _promptArgsCheckBox.Checked = false;
            _useShellCheckBox.Checked = true;
            _nameTextBox.Enabled = false;
            _commandTextBox.Enabled = false;
            _argumentsTextBox.Enabled = false;
            _initialDirTextBox.Enabled = false;
            _browseCommandButton.Enabled = false;
            _browseDirButton.Enabled = false;
            _promptArgsCheckBox.Enabled = false;
            _useShellCheckBox.Enabled = false;
        }
    }

    private void BrowseCommand()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Tool Executable",
            Filter = "Executable files (*.exe;*.cmd;*.bat;*.ps1;*.com)|*.exe;*.cmd;*.bat;*.ps1;*.com|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _commandTextBox.Text = dlg.FileName;
    }

    private void BrowseDirectory()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select initial directory for tool",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _initialDirTextBox.Text = dlg.SelectedPath;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

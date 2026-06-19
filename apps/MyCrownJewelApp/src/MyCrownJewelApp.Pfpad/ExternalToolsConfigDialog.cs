using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class ExternalToolsConfigDialog : Form
{
    private readonly List<ExternalTool> _tools;
    private readonly Theme _theme;
    private ExternalTool? _selectedTool;

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
    private Button _testRunButton = null!;
    private Button _browseCommandButton = null!;
    private Button _browseDirButton = null!;
    private Button _moveUpButton = null!;
    private Button _moveDownButton = null!;
    private Button _insertVariableButton = null!;
    private Label _argsHintLabel = null!;
    private Panel _propertiesPanel = null!;
    private ContextMenuStrip _variableMenu = null!;

    public ExternalToolsConfigDialog(List<ExternalTool> tools)
    {
        _tools = tools;
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "External Tools";
        Size = new Size(760, 520);
        MinimumSize = new Size(660, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        InitializeForm();
        ApplyTheme();
        LoadTools();

        Load += (_, _) => NativeThemed.ApplyThemeToChildScrollbars(this, !_theme.IsLight);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_theme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }

    private void InitializeForm()
    {
        _variableMenu = CreateVariableMenu();

        // Use a fixed-width left panel + Splitter + fill right panel to avoid SplitContainer
        // SplitterDistance validation failures (throws when Width=0 during construction).
        var leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = _theme.Background,
            Padding = new Padding(12)
        };
        var divider = new Splitter
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = _theme.Border,
            MinExtra = 260,
            MinSize = 160
        };
        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.Background,
            Padding = new Padding(14, 12, 14, 12)
        };

        InitializeLeftPanel(leftPanel);
        InitializeRightPanel(rightPanel);

        var bottomBar = CreateBottomActionBar();

        // Order matters for docking: Bottom first, then Left, Splitter, Fill (last added = first docked).
        Controls.Add(rightPanel);
        Controls.Add(divider);
        Controls.Add(leftPanel);
        Controls.Add(bottomBar);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private void InitializeLeftPanel(Control host)
    {
        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = "TOOLS",
            Font = new Font("Segoe UI", 7f, FontStyle.Bold),
            ForeColor = _theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _toolListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 24,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text
        };
        _toolListBox.DrawItem += ToolListBox_DrawItem;
        _toolListBox.SelectedIndexChanged += ToolListBox_SelectedIndexChanged;

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = _theme.Border
        };
        listHost.Controls.Add(_toolListBox);

        var actionStrip = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = Color.Transparent
        };

        _addButton = CreateFlatButton("+ Add", 60);
        _addButton.Click += (_, _) => AddTool();

        _removeButton = CreateFlatButton("🗑 Remove", 86);
        _removeButton.Click += (_, _) => RemoveTool();

        _moveUpButton = CreateFlatButton("▲", 36);
        _moveUpButton.Click += (_, _) => MoveTool(-1);

        _moveDownButton = CreateFlatButton("▼", 36);
        _moveDownButton.Click += (_, _) => MoveTool(1);

        var actionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent
        };
        actionsFlow.Controls.AddRange(new Control[] { _addButton, _removeButton, _moveUpButton, _moveDownButton });
        actionStrip.Controls.Add(actionsFlow);

        host.Controls.Add(listHost);
        host.Controls.Add(actionStrip);
        host.Controls.Add(titleLabel);
    }

    private void InitializeRightPanel(Control host)
    {
        var headerLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "TOOL PROPERTIES",
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = _theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _propertiesPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Enabled = false
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _nameTextBox = CreateTextBox();
        _commandTextBox = CreateTextBox();
        _argumentsTextBox = CreateTextBox();
        _initialDirTextBox = CreateTextBox();

        _browseCommandButton = CreateFlatButton("...", 40);
        _browseCommandButton.Click += (_, _) => BrowseCommand();

        _insertVariableButton = CreateFlatButton("{…}", 48);
        _insertVariableButton.Click += (_, _) => _variableMenu.Show(_insertVariableButton, new Point(0, _insertVariableButton.Height));

        _browseDirButton = CreateFlatButton("...", 40);
        _browseDirButton.Click += (_, _) => BrowseDirectory();

        _argsHintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Variables: $(FilePath), $(FileDir), $(FileName), $(FileExt), $(SelText), $(CurLine), $(CurCol)",
            Font = new Font("Segoe UI", 8f),
            ForeColor = _theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _promptArgsCheckBox = new CheckBox { Text = "☐ Prompt for arguments", AutoSize = true };
        _useShellCheckBox = new CheckBox { Text = "☐ Use shell execute", AutoSize = true, Checked = true };

        HookFieldEvents(_nameTextBox, _commandTextBox, _argumentsTextBox, _initialDirTextBox, _promptArgsCheckBox, _useShellCheckBox);

        AddLabeledRow(table, 0, "&Title:", CreateFieldHost(_nameTextBox));
        AddLabeledRow(table, 1, "&Command:", CreateFieldHost(_commandTextBox, _browseCommandButton));
        AddLabeledRow(table, 2, "&Arguments:", CreateFieldHost(_argumentsTextBox, _insertVariableButton));

        table.Controls.Add(_argsHintLabel, 1, 3);

        AddLabeledRow(table, 4, "&Init Dir:", CreateFieldHost(_initialDirTextBox, _browseDirButton));

        table.Controls.Add(_promptArgsCheckBox, 1, 5);
        table.Controls.Add(_useShellCheckBox, 1, 6);

        _propertiesPanel.Controls.Add(table);

        host.Controls.Add(_propertiesPanel);
        host.Controls.Add(headerLabel);
    }

    private Panel CreateBottomActionBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(14, 7, 14, 7),
            BackColor = _theme.PanelBackground
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
        };

        _testRunButton = CreateFlatButton("Test Run", 100);
        _testRunButton.Click += (_, _) => TestRunCurrentTool();

        _okButton = CreateFlatButton("OK", 80);
        _okButton.DialogResult = DialogResult.OK;
        _okButton.Click += (_, _) => SaveTools();

        _cancelButton = CreateFlatButton("Cancel", 80);
        _cancelButton.DialogResult = DialogResult.Cancel;

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent
        };
        flow.Controls.AddRange(new Control[] { _testRunButton, _okButton, _cancelButton });
        panel.Controls.Add(flow);
        return panel;
    }

    private void ApplyTheme()
    {
        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        foreach (Control control in Controls)
            StyleControl(control, _theme);

        _argsHintLabel.ForeColor = _theme.Muted;
        _variableMenu.BackColor = _theme.PanelBackground;
        _variableMenu.ForeColor = _theme.Text;
    }

    private static void StyleControl(Control c, Theme theme)
    {
        switch (c)
        {
            case Button btn:
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = theme.PanelBackground;
                btn.ForeColor = theme.Text;
                btn.FlatAppearance.BorderColor = theme.Border;
                btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
                btn.FlatAppearance.MouseDownBackColor = theme.ButtonHoverBackground;
                break;
            case TextBox tb:
                tb.BackColor = theme.EditorBackground;
                tb.ForeColor = theme.Text;
                tb.BorderStyle = BorderStyle.None;
                break;
            case ListBox lb:
                lb.BackColor = theme.EditorBackground;
                lb.ForeColor = theme.Text;
                lb.BorderStyle = BorderStyle.None;
                break;
            case CheckBox cb:
                cb.BackColor = Color.Transparent;
                cb.ForeColor = theme.Text;
                break;
            case Label lbl:
                lbl.BackColor = Color.Transparent;
                lbl.ForeColor = theme.Text;
                break;
        }

        foreach (Control child in c.Controls)
            StyleControl(child, theme);
    }

    private Button CreateFlatButton(string text, int width)
    {
        return new Button
        {
            Text = text,
            Width = width,
            Height = 28,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            Cursor = Cursors.Hand
        };
    }

    private TextBox CreateTextBox() => new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = _theme.EditorBackground,
        ForeColor = _theme.Text,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 6, 0, 0)
    };

    private Panel CreateFieldHost(TextBox textBox, params Button[] buttons)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.Border,
            Padding = new Padding(1),
            Margin = new Padding(0, 4, 0, 4)
        };

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.EditorBackground,
            Padding = new Padding(8, 6, 6, 6)
        };

        foreach (var button in buttons.Reverse())
        {
            button.Dock = DockStyle.Right;
            button.Margin = new Padding(6, 0, 0, 0);
            inner.Controls.Add(button);
        }

        inner.Controls.Add(textBox);
        host.Controls.Add(inner);
        return host;
    }

    private void AddLabeledRow(TableLayoutPanel table, int row, string text, Control editor)
    {
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false
        };
        table.Controls.Add(label, 0, row);
        table.Controls.Add(editor, 1, row);
    }

    private ContextMenuStrip CreateVariableMenu()
    {
        var menu = new ContextMenuStrip();
        foreach (var variable in new[] { "$(FilePath)", "$(FileDir)", "$(FileName)", "$(FileExt)", "$(SelText)", "$(CurLine)", "$(CurCol)" })
        {
            var item = new ToolStripMenuItem(variable);
            item.Click += (_, _) => InsertArgumentVariable(variable);
            menu.Items.Add(item);
        }
        return menu;
    }

    private void HookFieldEvents(params Control[] controls)
    {
        foreach (var control in controls)
        {
            switch (control)
            {
                case TextBox tb:
                    tb.TextChanged += (_, _) => SaveCurrentTool();
                    break;
                case CheckBox cb:
                    cb.CheckedChanged += (_, _) => SaveCurrentTool();
                    break;
            }
        }
    }

    private void LoadTools()
    {
        _toolListBox.BeginUpdate();
        _toolListBox.Items.Clear();
        foreach (var tool in _tools)
            _toolListBox.Items.Add(tool);
        _toolListBox.EndUpdate();

        if (_toolListBox.Items.Count > 0)
            _toolListBox.SelectedIndex = 0;
        else
            UpdateSelection(null);
    }

    private void SaveTools()
    {
        SaveCurrentTool();
    }

    private void AddTool()
    {
        SaveCurrentTool();
        var tool = new ExternalTool { Name = "New Tool", UseShellExecute = true };
        _tools.Add(tool);
        _toolListBox.Items.Add(tool);
        _toolListBox.SelectedIndex = _toolListBox.Items.Count - 1;
    }

    private void RemoveTool()
    {
        if (_toolListBox.SelectedItem is not ExternalTool tool)
            return;

        int idx = _toolListBox.SelectedIndex;
        _tools.Remove(tool);
        _toolListBox.Items.Remove(tool);

        if (_toolListBox.Items.Count > 0)
            _toolListBox.SelectedIndex = Math.Min(idx, _toolListBox.Items.Count - 1);
        else
            UpdateSelection(null);
    }

    private void MoveTool(int direction)
    {
        int idx = _toolListBox.SelectedIndex;
        int target = idx + direction;
        if (idx < 0 || target < 0 || target >= _tools.Count)
            return;

        SaveCurrentTool();
        (_tools[idx], _tools[target]) = (_tools[target], _tools[idx]);
        LoadTools();
        _toolListBox.SelectedIndex = target;
    }

    private void SaveCurrentTool()
    {
        if (_selectedTool == null)
            return;

        _selectedTool.Name = _nameTextBox.Text;
        _selectedTool.Command = _commandTextBox.Text;
        _selectedTool.Arguments = _argumentsTextBox.Text;
        _selectedTool.InitialDirectory = _initialDirTextBox.Text;
        _selectedTool.PromptForArguments = _promptArgsCheckBox.Checked;
        _selectedTool.UseShellExecute = _useShellCheckBox.Checked;
        _toolListBox.Invalidate();
    }

    private void ToolListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        SaveCurrentTool();
        UpdateSelection(_toolListBox.SelectedItem as ExternalTool);
    }

    private void UpdateSelection(ExternalTool? tool)
    {
        _selectedTool = tool;
        bool hasSelection = tool != null;

        _removeButton.Enabled = hasSelection;
        _moveUpButton.Enabled = hasSelection && _toolListBox.SelectedIndex > 0;
        _moveDownButton.Enabled = hasSelection && _toolListBox.SelectedIndex >= 0 && _toolListBox.SelectedIndex < _toolListBox.Items.Count - 1;
        _propertiesPanel.Enabled = hasSelection;
        _testRunButton.Enabled = hasSelection;

        if (tool == null)
        {
            _nameTextBox.Text = string.Empty;
            _commandTextBox.Text = string.Empty;
            _argumentsTextBox.Text = string.Empty;
            _initialDirTextBox.Text = string.Empty;
            _promptArgsCheckBox.Checked = false;
            _useShellCheckBox.Checked = true;
            return;
        }

        _nameTextBox.Text = tool.Name;
        _commandTextBox.Text = tool.Command;
        _argumentsTextBox.Text = tool.Arguments;
        _initialDirTextBox.Text = tool.InitialDirectory;
        _promptArgsCheckBox.Checked = tool.PromptForArguments;
        _useShellCheckBox.Checked = tool.UseShellExecute;
    }

    private void ToolListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _toolListBox.Items.Count)
            return;

        var tool = _toolListBox.Items[e.Index] as ExternalTool;
        string name = string.IsNullOrWhiteSpace(tool?.Name) ? "(Unnamed Tool)" : tool!.Name;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected ? Blend(_theme.Accent, _theme.EditorBackground, 0.2f) : _theme.EditorBackground;
        var iconColor = selected ? _theme.Accent : _theme.Muted;

        using var backBrush = new SolidBrush(background);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        var iconBounds = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, 20, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, "⚙", new Font("Segoe UI Symbol", 10f), iconBounds, iconColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        var textBounds = new Rectangle(e.Bounds.Left + 28, e.Bounds.Top, e.Bounds.Width - 32, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, name, _toolListBox.Font, textBounds, _theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            e.DrawFocusRectangle();
    }

    private void BrowseCommand()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Tool Executable",
            Filter = "Executable files (*.exe;*.cmd;*.bat;*.ps1;*.com)|*.exe;*.cmd;*.bat;*.ps1;*.com|All files (*.*)|*.*"
        };

        if (dlg.ShowThemed() == DialogResult.OK)
            _commandTextBox.Text = dlg.FileName;
    }

    private void BrowseDirectory()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select initial directory for tool",
            UseDescriptionForTitle = true
        };

        if (NativeThemed.ShowDialogThemed(() => dlg.ShowDialog(this)) == DialogResult.OK)
            _initialDirTextBox.Text = dlg.SelectedPath;
    }

    private void InsertArgumentVariable(string variable)
    {
        int caret = _argumentsTextBox.SelectionStart;
        _argumentsTextBox.Text = _argumentsTextBox.Text.Insert(caret, variable);
        _argumentsTextBox.SelectionStart = caret + variable.Length;
        _argumentsTextBox.Focus();
    }

    private void TestRunCurrentTool()
    {
        SaveCurrentTool();
        string cmd = _commandTextBox.Text.Trim();
        string args = _argumentsTextBox.Text.Trim();
        string preview = string.IsNullOrWhiteSpace(args)
            ? $"Would execute:\n{cmd}"
            : $"Would execute:\n{cmd} {args}";
        ThemedMessageBox.Show(this, preview, "Test Run Preview");
    }

    private static Color Blend(Color blendColor, Color baseColor, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        int r = (int)(baseColor.R + ((blendColor.R - baseColor.R) * amount));
        int g = (int)(baseColor.G + ((blendColor.G - baseColor.G) * amount));
        int b = (int)(baseColor.B + ((blendColor.B - baseColor.B) * amount));
        return Color.FromArgb(r, g, b);
    }
}

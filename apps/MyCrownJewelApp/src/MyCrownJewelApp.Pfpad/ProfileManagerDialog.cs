using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class ProfileManagerDialog : Form
{
    private readonly UserProfileManager _manager;
    private readonly Form1 _mainForm;
    private ListBox _profileList = null!;
    private TextBox _nameBox = null!;
    private TextBox _workspaceBox = null!;
    private TextBox _buildBox = null!;
    private TextBox _runBox = null!;
    private TextBox _testBox = null!;
    private Button _addButton = null!;
    private Button _deleteButton = null!;
    private Button _saveButton = null!;
    private Button _browseButton = null!;
    private readonly Theme _theme;

    // Override controls
    private CheckBox _overrideTabSizeCheck = null!;
    private NumericUpDown _overrideTabSizeVal = null!;
    private CheckBox _overrideInsertSpacesCheck = null!;
    private CheckBox _overrideInsertSpacesVal = null!;
    private CheckBox _overrideFontSizeCheck = null!;
    private NumericUpDown _overrideFontSizeVal = null!;
    private CheckBox _overrideThemeCheck = null!;
    private ComboBox _overrideThemeVal = null!;
    private CheckBox _overrideWordWrapCheck = null!;
    private CheckBox _overrideWordWrapVal = null!;

    public ProfileManagerDialog(UserProfileManager manager, Form1 mainForm)
    {
        _manager = manager;
        _mainForm = mainForm;
        _theme = ThemeManager.Instance.CurrentTheme;
        Text = "Manage Profiles";
        Size = new Size(560, 640);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        Font = new Font("Segoe UI", 9);

        InitializeForm();
        LoadProfileList();
    }

    private void InitializeForm()
    {
        _profileList = new ListBox
        {
            Location = new Point(12, 12), Size = new Size(180, 300),
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        _profileList.SelectedIndexChanged += ProfileSelected;

        _addButton = CreateButton("New", 12, 318, 55);
        _addButton.Click += AddProfile;

        _deleteButton = CreateButton("Delete", 73, 318, 55);
        _deleteButton.Click += DeleteProfile;

        Controls.Add(_profileList); Controls.Add(_addButton); Controls.Add(_deleteButton);

        int x = 210; int y = 12; int lw = 110; int cw = 310;

        AddDetailLabel("Name:", x, y); _nameBox = AddDetailTextBox(x + lw, y, cw); y += 28;
        AddDetailLabel("Workspace:", x, y);
        _workspaceBox = AddDetailTextBox(x + lw, y, cw - 34);
        _browseButton = CreateButton("…", x + lw + cw - 30, y, 28);
        _browseButton.Click += (s, e) => { using var dlg = new FolderBrowserDialog(); if (dlg.ShowDialog() == DialogResult.OK) _workspaceBox.Text = dlg.SelectedPath; };
        y += 28;
        AddDetailLabel("Build:", x, y); _buildBox = AddDetailTextBox(x + lw, y, cw); y += 28;
        AddDetailLabel("Run:", x, y); _runBox = AddDetailTextBox(x + lw, y, cw); y += 28;
        AddDetailLabel("Test:", x, y); _testBox = AddDetailTextBox(x + lw, y, cw); y += 28;
        Controls.Add(_workspaceBox); Controls.Add(_browseButton);

        // Settings Overrides group
        y += 6;
        var overridesGroup = new GroupBox
        {
            Text = "Settings Overrides (leave unchecked = use global)",
            Location = new Point(x - 2, y), Size = new Size(cw + lw + 4, 200),
            ForeColor = _theme.Text, BackColor = _theme.Background,
            Font = new Font("Segoe UI", 8)
        };
        int ox = 8; int oy = 22; int oLeft = 130;

        _overrideTabSizeCheck = new CheckBox { Text = "Tab Size:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideTabSizeVal = new NumericUpDown { Location = new Point(oLeft, oy), Width = 60, Minimum = 2, Maximum = 12, Increment = 1, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, Enabled = false };
        _overrideTabSizeCheck.CheckedChanged += (s, e) => _overrideTabSizeVal.Enabled = _overrideTabSizeCheck.Checked;
        overridesGroup.Controls.Add(_overrideTabSizeCheck); overridesGroup.Controls.Add(_overrideTabSizeVal); oy += 26;

        _overrideInsertSpacesCheck = new CheckBox { Text = "Insert Spaces:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideInsertSpacesVal = new CheckBox { Text = "", Location = new Point(oLeft, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent, Enabled = false };
        _overrideInsertSpacesCheck.CheckedChanged += (s, e) => _overrideInsertSpacesVal.Enabled = _overrideInsertSpacesCheck.Checked;
        var insertLbl = new Label { Text = "Use spaces", Location = new Point(oLeft + 22, oy + 2), AutoSize = true, ForeColor = _theme.Muted, BackColor = Color.Transparent };
        overridesGroup.Controls.Add(_overrideInsertSpacesCheck); overridesGroup.Controls.Add(_overrideInsertSpacesVal); overridesGroup.Controls.Add(insertLbl); oy += 26;

        _overrideFontSizeCheck = new CheckBox { Text = "Font Size:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideFontSizeVal = new NumericUpDown { Location = new Point(oLeft, oy), Width = 60, Minimum = 6, Maximum = 72, Increment = 1, DecimalPlaces = 0, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, Enabled = false };
        _overrideFontSizeCheck.CheckedChanged += (s, e) => _overrideFontSizeVal.Enabled = _overrideFontSizeCheck.Checked;
        overridesGroup.Controls.Add(_overrideFontSizeCheck); overridesGroup.Controls.Add(_overrideFontSizeVal); oy += 26;

        _overrideThemeCheck = new CheckBox { Text = "Theme:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideThemeVal = new ComboBox { Location = new Point(oLeft, oy), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat, Enabled = false };
        _overrideThemeVal.Items.AddRange(ThemeManager.ThemeNames);
        _overrideThemeCheck.CheckedChanged += (s, e) => _overrideThemeVal.Enabled = _overrideThemeCheck.Checked;
        overridesGroup.Controls.Add(_overrideThemeCheck); overridesGroup.Controls.Add(_overrideThemeVal); oy += 26;

        _overrideWordWrapCheck = new CheckBox { Text = "Word Wrap:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideWordWrapVal = new CheckBox { Text = "", Location = new Point(oLeft, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent, Enabled = false };
        _overrideWordWrapCheck.CheckedChanged += (s, e) => _overrideWordWrapVal.Enabled = _overrideWordWrapCheck.Checked;
        var wrapLbl = new Label { Text = "Wrap lines", Location = new Point(oLeft + 22, oy + 2), AutoSize = true, ForeColor = _theme.Muted, BackColor = Color.Transparent };
        overridesGroup.Controls.Add(_overrideWordWrapCheck); overridesGroup.Controls.Add(_overrideWordWrapVal); overridesGroup.Controls.Add(wrapLbl);

        Controls.Add(overridesGroup);
        y = overridesGroup.Bottom + 6;

        _saveButton = new Button
        {
            Text = "Save", Location = new Point(x + lw + cw - 160, y), Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _saveButton.Click += SaveProfile;

        var closeButton = new Button
        {
            Text = "Close", Location = new Point(x + lw + cw - 74, y), Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        closeButton.Click += (s, e) => Close();

        Controls.Add(_saveButton); Controls.Add(closeButton);

        SetDetailsEnabled(false);
    }

    private Button CreateButton(string text, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, 26),
            FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        return b;
    }

    private Label AddDetailLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y + 4), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        Controls.Add(lbl); return lbl;
    }

    private TextBox AddDetailTextBox(int x, int y, int width)
    {
        var tb = new TextBox { Location = new Point(x, y), Width = width, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        return tb;
    }

    private void SetDetailsEnabled(bool enabled)
    {
        foreach (var c in new Control[] { _nameBox, _workspaceBox, _buildBox, _runBox, _testBox, _browseButton, _saveButton, _deleteButton })
            c.Enabled = enabled;
        bool editable = enabled && _profileList.SelectedItem is string sel && sel != "Default";
        foreach (var c in new Control[] { _overrideTabSizeCheck, _overrideInsertSpacesCheck, _overrideFontSizeCheck, _overrideThemeCheck, _overrideWordWrapCheck })
            c.Enabled = editable;
        if (!editable)
        {
            _overrideTabSizeVal.Enabled = false; _overrideInsertSpacesVal.Enabled = false;
            _overrideFontSizeVal.Enabled = false; _overrideThemeVal.Enabled = false; _overrideWordWrapVal.Enabled = false;
        }
    }

    private void LoadProfileList()
    {
        _profileList.Items.Clear();
        _profileList.Items.AddRange(_manager.ProfileNames);
        _profileList.SelectedItem = _manager.ActiveProfileName ?? "Default";
    }

    private void ProfileSelected(object? sender, EventArgs e)
    {
        if (_profileList.SelectedItem is string name)
        {
            var profile = _manager.LoadProfile(name);
            if (profile != null)
            {
                _nameBox.Text = profile.Name;
                _workspaceBox.Text = profile.WorkspaceRoot ?? "";
                _buildBox.Text = profile.BuildCommand;
                _runBox.Text = profile.RunCommand;
                _testBox.Text = profile.TestCommand;
                LoadOverrides(profile);
                SetDetailsEnabled(true);
                _deleteButton.Enabled = profile.Name != "Default";
                return;
            }
        }
        SetDetailsEnabled(false);
    }

    private void LoadOverrides(UserProfile profile)
    {
        _overrideTabSizeCheck.Checked = profile.OverrideTabSize.HasValue;
        _overrideTabSizeVal.Value = profile.OverrideTabSize ?? 4;
        _overrideTabSizeVal.Enabled = profile.OverrideTabSize.HasValue;

        _overrideInsertSpacesCheck.Checked = profile.OverrideInsertSpaces.HasValue;
        _overrideInsertSpacesVal.Checked = profile.OverrideInsertSpaces ?? true;
        _overrideInsertSpacesVal.Enabled = profile.OverrideInsertSpaces.HasValue;

        _overrideFontSizeCheck.Checked = profile.OverrideFontSize.HasValue;
        _overrideFontSizeVal.Value = (decimal)(profile.OverrideFontSize ?? 12);
        _overrideFontSizeVal.Enabled = profile.OverrideFontSize.HasValue;

        _overrideThemeCheck.Checked = !string.IsNullOrEmpty(profile.OverrideThemeName);
        if (!string.IsNullOrEmpty(profile.OverrideThemeName)) _overrideThemeVal.Text = profile.OverrideThemeName;
        _overrideThemeVal.Enabled = !string.IsNullOrEmpty(profile.OverrideThemeName);

        _overrideWordWrapCheck.Checked = profile.OverrideWordWrap.HasValue;
        _overrideWordWrapVal.Checked = profile.OverrideWordWrap ?? false;
        _overrideWordWrapVal.Enabled = profile.OverrideWordWrap.HasValue;
    }

    private void AddProfile(object? sender, EventArgs e)
    {
        string baseName = "New Profile"; string name = baseName; int count = 1;
        while (Array.Exists(_manager.ProfileNames, n => n == name)) name = $"{baseName} {++count}";
        var profile = new UserProfile(Name: name, WorkspaceRoot: null);
        _manager.SaveProfile(profile);
        LoadProfileList();
        _profileList.SelectedItem = name;
    }

    private void DeleteProfile(object? sender, EventArgs e)
    {
        if (_profileList.SelectedItem is string name && name != "Default")
        {
            _manager.DeleteProfile(name);
            LoadProfileList();
            _profileList.SelectedItem = _manager.ActiveProfileName ?? "Default";
        }
    }

    private void SaveProfile(object? sender, EventArgs e)
    {
        string newName = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName)) return;

        string? oldName = _profileList.SelectedItem as string;
        if (oldName != null && oldName != "Default" && oldName != newName)
            _manager.DeleteProfile(oldName);

        var profile = new UserProfile(
            Name: newName,
            WorkspaceRoot: string.IsNullOrWhiteSpace(_workspaceBox.Text) ? null : _workspaceBox.Text.Trim(),
            BuildCommand: string.IsNullOrWhiteSpace(_buildBox.Text) ? "dotnet build" : _buildBox.Text.Trim(),
            RunCommand: string.IsNullOrWhiteSpace(_runBox.Text) ? "dotnet run" : _runBox.Text.Trim(),
            TestCommand: string.IsNullOrWhiteSpace(_testBox.Text) ? "dotnet test" : _testBox.Text.Trim(),
            OverrideTabSize: _overrideTabSizeCheck.Checked ? (int)_overrideTabSizeVal.Value : null,
            OverrideInsertSpaces: _overrideInsertSpacesCheck.Checked ? _overrideInsertSpacesVal.Checked : null,
            OverrideFontSize: _overrideFontSizeCheck.Checked ? (float)_overrideFontSizeVal.Value : null,
            OverrideThemeName: _overrideThemeCheck.Checked ? _overrideThemeVal.Text : null,
            OverrideWordWrap: _overrideWordWrapCheck.Checked ? _overrideWordWrapVal.Checked : null
        );
        _manager.SaveProfile(profile);
        _mainForm.ApplyProfile(profile);
        LoadProfileList();
        _profileList.SelectedItem = profile.Name;
    }
}
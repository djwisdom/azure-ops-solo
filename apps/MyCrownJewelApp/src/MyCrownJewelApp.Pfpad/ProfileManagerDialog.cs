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

    public ProfileManagerDialog(UserProfileManager manager, Form1 mainForm)
    {
        _manager = manager;
        _mainForm = mainForm;
        _theme = ThemeManager.Instance.CurrentTheme;
        Text = "Manage Profiles";
        Size = new Size(520, 440);
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
        // Profile list (left side)
        _profileList = new ListBox
        {
            Location = new Point(12, 12),
            Size = new Size(180, 320),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        _profileList.SelectedIndexChanged += ProfileSelected;

        _addButton = new Button
        {
            Text = "New",
            Location = new Point(12, 340),
            Size = new Size(55, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _addButton.Click += AddProfile;

        _deleteButton = new Button
        {
            Text = "Delete",
            Location = new Point(73, 340),
            Size = new Size(55, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _deleteButton.Click += DeleteProfile;

        Controls.Add(_profileList);
        Controls.Add(_addButton);
        Controls.Add(_deleteButton);

        // Profile details (right side)
        int x = 210;
        int y = 12;
        int labelWidth = 100;
        int controlWidth = 270;

        AddDetailLabel("Name:", x, y); _nameBox = AddDetailTextBox(x + labelWidth, y, controlWidth); y += 30;
        AddDetailLabel("Workspace Root:", x, y);
        _workspaceBox = AddDetailTextBox(x + labelWidth, y, controlWidth - 34);
        _browseButton = new Button
        {
            Text = "…",
            Location = new Point(x + labelWidth + controlWidth - 30, y),
            Size = new Size(28, 22),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _browseButton.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                _workspaceBox.Text = dlg.SelectedPath;
        };
        y += 30;
        AddDetailLabel("Build Command:", x, y); _buildBox = AddDetailTextBox(x + labelWidth, y, controlWidth); y += 30;
        AddDetailLabel("Run Command:", x, y); _runBox = AddDetailTextBox(x + labelWidth, y, controlWidth); y += 30;
        AddDetailLabel("Test Command:", x, y); _testBox = AddDetailTextBox(x + labelWidth, y, controlWidth); y += 30;

        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(x + labelWidth + controlWidth - 160, y + 10),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _saveButton.Click += SaveProfile;

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(x + labelWidth + controlWidth - 74, y + 10),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        closeButton.Click += (s, e) => Close();

        Controls.Add(_nameBox);
        Controls.Add(_workspaceBox);
        Controls.Add(_browseButton);
        Controls.Add(_buildBox);
        Controls.Add(_runBox);
        Controls.Add(_testBox);
        Controls.Add(_saveButton);
        Controls.Add(closeButton);

        SetDetailsEnabled(false);
    }

    private Label AddDetailLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y + 4),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent
        };
        Controls.Add(lbl);
        return lbl;
    }

    private TextBox AddDetailTextBox(int x, int y, int width)
    {
        var tb = new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        return tb;
    }

    private void SetDetailsEnabled(bool enabled)
    {
        _nameBox.Enabled = enabled;
        _workspaceBox.Enabled = enabled;
        _buildBox.Enabled = enabled;
        _runBox.Enabled = enabled;
        _testBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _saveButton.Enabled = enabled;
        _deleteButton.Enabled = enabled;
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
                SetDetailsEnabled(true);
                _deleteButton.Enabled = profile.Name != "Default";
                return;
            }
        }
        SetDetailsEnabled(false);
    }

    private void AddProfile(object? sender, EventArgs e)
    {
        string baseName = "New Profile";
        string name = baseName;
        int count = 1;
        while (Array.Exists(_manager.ProfileNames, n => n == name))
            name = $"{baseName} {++count}";

        var profile = new UserProfile(
            Name: name,
            WorkspaceRoot: null,
            BuildCommand: "dotnet build",
            RunCommand: "dotnet run",
            TestCommand: "dotnet test"
        );
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
            TestCommand: string.IsNullOrWhiteSpace(_testBox.Text) ? "dotnet test" : _testBox.Text.Trim()
        );
        _manager.SaveProfile(profile);

        _mainForm.ApplyProfile(profile);

        LoadProfileList();
        _profileList.SelectedItem = profile.Name;
    }
}
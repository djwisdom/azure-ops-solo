using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Enhanced profile manager dialog with VS Code-style UX.
/// Supports colored circle avatars, color selection, search, profile metadata,
/// and comprehensive overrides.
/// </summary>
internal sealed class ProfileManagerDialog : Form
{
    private readonly UserProfileManager _manager;
    private readonly Form1 _mainForm;
    private readonly Theme _theme;

    // Left panel: profile list
    private Panel _leftPanel = null!;
    private TextBox _searchBox = null!;
    private ListBox _profileList = null!;
    private Button _newButton = null!;
    private ContextMenuStrip _newMenu = null!;
    private ToolStripMenuItem _newBlankItem = null!;
    private ToolStripMenuItem _newFromWindowItem = null!;
    private ToolStripMenuItem _newFromWorkspaceItem = null!;
    private Button _duplicateButton = null!;
    private Button _renameButton = null!;
    private Button _deleteButton = null!;

    // Right panel: details
    private Panel _rightPanel = null!;
    private TextBox _nameBox = null!;
    private TextBox _descriptionBox = null!;
    private PictureBox _colorPreviewBox = null!;
    private Button _colorButton = null!;
    private Label _colorLabel = null!;
    private TextBox _workspaceBox = null!;
    private Button _browseButton = null!;
    private ListBox _recentWorkspacesList = null!;
    private Button _addWorkspaceButton = null!;
    private Button _removeWorkspaceButton = null!;
    private TextBox _buildBox = null!;
    private TextBox _runBox = null!;
    private TextBox _testBox = null!;
    private CheckBox _overrideTabSizeCheck = null!;
    private NumericUpDown _overrideTabSizeVal = null!;
    private CheckBox _overrideInsertSpacesCheck = null!;
    private CheckBox _overrideInsertSpacesVal = null!;
    private CheckBox _overrideFontSizeCheck = null!;
    private NumericUpDown _overrideFontSizeVal = null!;
    private CheckBox _overrideFontNameCheck = null!;
    private ComboBox _overrideFontNameVal = null!;
    private CheckBox _overrideThemeCheck = null!;
    private ComboBox _overrideThemeVal = null!;
    private CheckBox _overrideWordWrapCheck = null!;
    private CheckBox _overrideWordWrapVal = null!;
    private CheckBox _defaultOnStartupCheck = null!;
    private Label _lastUsedLabel = null!;
    private Label _usageLabel = null!;

    private Button _saveButton = null!;
    private Button _exportButton = null!;
    private Button _importButton = null!;
    private Button _closeButton = null!;

    private GroupBox? _appearanceGroup;
    private CollapsibleSection? _workspaceSection = null;
    private CollapsibleSection? _commandsSection = null;
    private CollapsibleSection? _overridesSection = null;

    private Button _showAllButton = null!;
    private Panel? _actionPanel;

    private UserProfile? _currentProfile;

    public ProfileManagerDialog(UserProfileManager manager, Form1 mainForm)
    {
        try
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _theme = ThemeManager.Instance?.CurrentTheme ?? throw new InvalidOperationException("ThemeManager or CurrentTheme is not available.");

        Text = "Manage Profiles";
        ClientSize = new Size(720, 560);
        StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = _theme.Background;
            ForeColor = _theme.Text;
            Font = new Font("Segoe UI", 9);
            AutoScroll = false;

            InitializeComponents();
            LoadProfileList();
            RegisterEvents();

            // Subscribe to profile change events
            _manager.ProfileChanged += OnProfileChanged;
            _manager.ProfileDeleted += OnProfileDeleted;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Profile Manager: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private void InitializeComponents()
    {
        // === LEFT PANEL ===
        _leftPanel = new Panel
        {
            Location = new Point(12, 12),
            Size = new Size(210, this.ClientSize.Height - 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
            BackColor = _theme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Compute layout metrics
        const int buttonHeight = 26;
        const int buttonRows = 2;
        const int buttonGap = 4;
        const int bottomMargin = 12;
        int buttonAreaHeight = buttonRows * buttonHeight + (buttonRows - 1) * buttonGap; // 56
        int topRowY = _leftPanel.Height - buttonAreaHeight - bottomMargin;
        int bottomRowY = topRowY + buttonHeight + buttonGap;
        int listHeight = topRowY - 36 - 8;

        // Search box
        _searchBox = new TextBox
        {
            Location = new Point(8, 8),
            Size = new Size(194, 23),
            PlaceholderText = "Search profiles...",
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Profile list (owner-drawn)
        _profileList = new ListBox
        {
            Location = new Point(8, 36),
            Size = new Size(194, listHeight),
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 24,
            IntegralHeight = false,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        _profileList.DrawItem += ProfileList_DrawItem;

        // New button with dropdown
        _newButton = new Button
        {
            Text = "New",
            Location = new Point(8, topRowY),
            Size = new Size(60, buttonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _newMenu = new ContextMenuStrip { RenderMode = ToolStripRenderMode.Professional };
        _newMenu.Renderer = new ThemeAwareMenuRenderer(_theme);
        _newBlankItem = new ToolStripMenuItem("Blank Profile");
        _newFromWindowItem = new ToolStripMenuItem("From Current Window");
        _newFromWorkspaceItem = new ToolStripMenuItem("From Current Workspace");
        _newMenu.Items.AddRange(new ToolStripItem[] { _newBlankItem, _newFromWindowItem, _newFromWorkspaceItem });

        _duplicateButton = new Button
        {
            Text = "Duplicate",
            Location = new Point(74, topRowY),
            Size = new Size(60, buttonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };

        _renameButton = new Button
        {
            Text = "Rename",
            Location = new Point(140, topRowY),
            Size = new Size(60, buttonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };

        _deleteButton = new Button
        {
            Text = "Delete",
            Location = new Point(8, bottomRowY),
            Size = new Size(60, buttonHeight),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };

        _leftPanel.Controls.AddRange(new Control[] { _searchBox, _profileList, _newButton, _duplicateButton, _renameButton, _deleteButton });
        Controls.Add(_leftPanel);

        // === RIGHT PANEL ===
        _rightPanel = new Panel
        {
            Location = new Point(230, 12),
            Size = new Size(478, this.ClientSize.Height - 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = _theme.Background,
            AutoScroll = true
        };

        int y = 0;

        // Title
        var titleLbl = new Label
        {
            Text = "Profile Details",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        _rightPanel.Controls.Add(titleLbl);
        y += 30;

        // Name
        var nameLbl = new Label { Text = "Name:", Location = new Point(0, y + 4), AutoSize = true, ForeColor = _theme.Text };
        _rightPanel.Controls.Add(nameLbl);
        _nameBox = new TextBox { Location = new Point(70, y), Width = 300, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _rightPanel.Controls.Add(_nameBox);
        y += 30;

        // Description
        var descLbl = new Label { Text = "Description:", Location = new Point(0, y + 4), AutoSize = true, ForeColor = _theme.Text };
        _rightPanel.Controls.Add(descLbl);
        _descriptionBox = new TextBox { Location = new Point(70, y), Width = 300, Height = 50, Multiline = true, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle, ScrollBars = ScrollBars.Vertical };
        _rightPanel.Controls.Add(_descriptionBox);
        y += 60;

        // Appearance group
        _appearanceGroup = new GroupBox
        {
            Text = "Appearance",
            Location = new Point(0, y),
            Size = new Size(380, 60),
            ForeColor = _theme.Text,
            BackColor = _theme.Background,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        int ax = 10, ay = 22;
        _appearanceGroup.Controls.Add(new Label { Text = "Color:", Location = new Point(ax, ay), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent });
        _colorPreviewBox = new PictureBox
        {
            Location = new Point(ax + 45, ay),
            Size = new Size(24, 24),
            BackColor = _theme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        _colorLabel = new Label { Text = "#0078D4", Location = new Point(ax + 75, ay + 4), AutoSize = true, ForeColor = _theme.Muted, BackColor = Color.Transparent };
        _colorButton = new Button
        {
            Text = "Change...",
            Location = new Point(ax + 135, ay - 2),
            Size = new Size(70, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _appearanceGroup.Controls.AddRange(new Control[] { _colorPreviewBox, _colorLabel, _colorButton });
        _rightPanel.Controls.Add(_appearanceGroup);
        y += 70;

        // Show All Settings toggle button
        _showAllButton = new Button
        {
            Text = "▼ Show All Settings",
            Location = new Point(0, y),
            Size = new Size(180, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatAppearance = { BorderColor = _theme.Border, BorderSize = 1 },
            Cursor = Cursors.Hand
        };
        _showAllButton.Click += (s, e) => ToggleAllAdvancedSections();
        _showAllButton.Margin = new Padding(0);
        _rightPanel.Controls.Add(_showAllButton);
        y += 28;

        // Workspace collapsible section
        _workspaceSection = new CollapsibleSection(_theme, "Workspace", startExpanded: false)
        {
            Location = new Point(0, y),
            Width = 380
        };
        int wx = 10, wy = 22;
        var workspaceLbl = new Label { Text = "Root Folder:", Location = new Point(wx, wy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _workspaceBox = new TextBox { Location = new Point(wx + 85, wy - 3), Width = 220, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _workspaceSection.ContentPanel.Controls.Add(workspaceLbl);
        _workspaceSection.ContentPanel.Controls.Add(_workspaceBox);
        _browseButton = new Button
        {
            Text = "...",
            Location = new Point(wx + 315, wy - 3),
            Width = 30,
            Height = 23,
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _workspaceSection.ContentPanel.Controls.Add(_browseButton);
        wy += 30;
        var recentLbl = new Label { Text = "Recent Workspaces:", Location = new Point(wx, wy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _workspaceSection.ContentPanel.Controls.Add(recentLbl);
        _recentWorkspacesList = new ListBox { Location = new Point(wx, wy + 18), Width = 340, Height = 40, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _workspaceSection.ContentPanel.Controls.Add(_recentWorkspacesList);
        var wsBtnPanel = new Panel { Location = new Point(wx + 280, wy + 20), Size = new Size(60, 18) };
        _addWorkspaceButton = new Button { Text = "+", Width = 18, Height = 18, FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text };
        _removeWorkspaceButton = new Button { Text = "-", Width = 18, Height = 18, FlatStyle = FlatStyle.Flat, Location = new Point(22, 0), BackColor = _theme.PanelBackground, ForeColor = _theme.Text };
        wsBtnPanel.Controls.Add(_addWorkspaceButton);
        wsBtnPanel.Controls.Add(_removeWorkspaceButton);
        _workspaceSection.ContentPanel.Controls.Add(wsBtnPanel);
        _rightPanel.Controls.Add(_workspaceSection);
        y += _workspaceSection.Height + 6;

        // Commands collapsible section
        _commandsSection = new CollapsibleSection(_theme, "Commands", startExpanded: false)
        {
            Location = new Point(0, y),
            Width = 380
        };
        int cx = 10, cy = 22;
        _commandsSection.ContentPanel.Controls.Add(new Label { Text = "Build:", Location = new Point(cx, cy), AutoSize = true, ForeColor = _theme.Text });
        _buildBox = new TextBox { Location = new Point(cx + 70, cy - 3), Width = 270, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _commandsSection.ContentPanel.Controls.Add(_buildBox);
        cy += 26;
        _commandsSection.ContentPanel.Controls.Add(new Label { Text = "Run:", Location = new Point(cx, cy), AutoSize = true, ForeColor = _theme.Text });
        _runBox = new TextBox { Location = new Point(cx + 70, cy - 3), Width = 270, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _commandsSection.ContentPanel.Controls.Add(_runBox);
        cy += 26;
        _commandsSection.ContentPanel.Controls.Add(new Label { Text = "Test:", Location = new Point(cx, cy), AutoSize = true, ForeColor = _theme.Text });
        _testBox = new TextBox { Location = new Point(cx + 70, cy - 3), Width = 270, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle };
        _commandsSection.ContentPanel.Controls.Add(_testBox);
        _rightPanel.Controls.Add(_commandsSection);
        y += _commandsSection.Height + 6;

        // Settings Overrides collapsible section
        _overridesSection = new CollapsibleSection(_theme, "Settings Overrides", startExpanded: false)
        {
            Location = new Point(0, y),
            Width = 380
        };
        int ox = 12, oy = 22;
        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Tab Size:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideTabSizeCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideTabSizeVal = new NumericUpDown { Location = new Point(ox + 180, oy - 4), Width = 50, Minimum = 2, Maximum = 12, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, Enabled = false };
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideTabSizeCheck, _overrideTabSizeVal });
        oy += 26;

        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Insert Spaces:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideInsertSpacesCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideInsertSpacesVal = new CheckBox { Location = new Point(ox + 180, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent, Enabled = false };
        var insertLbl = new Label { Text = "Use spaces", Location = new Point(ox + 205, oy + 2), AutoSize = true, ForeColor = _theme.Muted, BackColor = Color.Transparent };
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideInsertSpacesCheck, _overrideInsertSpacesVal, insertLbl });
        oy += 26;

        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Font Size:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideFontSizeCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideFontSizeVal = new NumericUpDown { Location = new Point(ox + 180, oy - 4), Width = 50, Minimum = 6, Maximum = 72, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, Enabled = false };
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideFontSizeCheck, _overrideFontSizeVal });
        oy += 26;

        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Font:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideFontNameCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideFontNameVal = new ComboBox { Location = new Point(ox + 180, oy - 3), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat, Enabled = false };
        _overrideFontNameVal.Items.AddRange(new[] { "Consolas", "Courier New", "Lucida Console", "Menlo", "Monaco", "Source Code Pro", "Fira Code", "Cascadia Code", "JetBrains Mono" });
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideFontNameCheck, _overrideFontNameVal });
        oy += 26;

        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Theme:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideThemeCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideThemeVal = new ComboBox { Location = new Point(ox + 180, oy - 3), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat, Enabled = false };
        _overrideThemeVal.Items.AddRange(ThemeManager.ThemeNames);
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideThemeCheck, _overrideThemeVal });
        oy += 26;

        _overridesSection.ContentPanel.Controls.Add(new Label { Text = "Word Wrap:", Location = new Point(ox, oy), AutoSize = true, ForeColor = _theme.Text });
        _overrideWordWrapCheck = new CheckBox { Location = new Point(ox + 120, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent };
        _overrideWordWrapVal = new CheckBox { Location = new Point(ox + 180, oy), AutoSize = true, ForeColor = _theme.Text, BackColor = Color.Transparent, Enabled = false };
        var wrapLbl = new Label { Text = "Wrap lines", Location = new Point(ox + 205, oy + 2), AutoSize = true, ForeColor = _theme.Muted, BackColor = Color.Transparent };
        _overridesSection.ContentPanel.Controls.AddRange(new Control[] { _overrideWordWrapCheck, _overrideWordWrapVal, wrapLbl });
        // oy += 26; // final row

        _rightPanel.Controls.Add(_overridesSection);
        y += _overridesSection.Height + 6;

        // Default on startup checkbox
        _defaultOnStartupCheck = new CheckBox
        {
            Text = "Set as default profile on startup",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = _theme.Background
        };
        _rightPanel.Controls.Add(_defaultOnStartupCheck);
        y += 26;

        // Usage info
        _lastUsedLabel = new Label { Text = "Last used: Never", Location = new Point(0, y), AutoSize = true, ForeColor = _theme.Muted };
        _usageLabel = new Label { Text = "Used 0 times", Location = new Point(120, y), AutoSize = true, ForeColor = _theme.Muted };
        _rightPanel.Controls.Add(_lastUsedLabel);
        _rightPanel.Controls.Add(_usageLabel);
        y += 26;

        // Action buttons panel (horizontal layout)
        _actionPanel = new Panel
        {
            Location = new Point(0, y),
            Height = 28,
            Width = 380,
            BackColor = _theme.Background
        };
        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(0, 0),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent }
        };
        _exportButton = new Button
        {
            Text = "Export...",
            Location = new Point(90, 0),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _importButton = new Button
        {
            Text = "Import...",
            Location = new Point(180, 0),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _closeButton = new Button
        {
            Text = "Close",
            Location = new Point(270, 0),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _actionPanel.Controls.AddRange(new Control[] { _saveButton, _exportButton, _importButton, _closeButton });
        _rightPanel.Controls.Add(_actionPanel);
        y += _actionPanel.Height + 6;

        Controls.AddRange(new Control[] { _leftPanel, _rightPanel });
    }

    private void RegisterEvents()
    {
        _searchBox.TextChanged += (s, e) => FilterProfiles(_searchBox.Text);
        _profileList.SelectedIndexChanged += ProfileList_SelectedIndexChanged;
        _newButton.Click += (s, e) => _newMenu.Show(_newButton, new Point(0, _newButton.Height));
        _newBlankItem.Click += (s, e) => CreateBlankProfile();
        _newFromWindowItem.Click += (s, e) => CreateFromCurrentWindow();
        _newFromWorkspaceItem.Click += (s, e) => CreateFromCurrentWorkspace();
        _duplicateButton.Click += (s, e) => DuplicateCurrentProfile();
        _renameButton.Click += (s, e) => RenameCurrentProfile();
        _deleteButton.Click += (s, e) => DeleteCurrentProfile();
        _browseButton.Click += (s, e) => BrowseWorkspace();
        _addWorkspaceButton.Click += (s, e) => AddCurrentWorkspace();
        _removeWorkspaceButton.Click += (s, e) => RemoveSelectedWorkspace();
        _colorButton.Click += (s, e) => ChooseColor();
        _saveButton.Click += (s, e) => SaveCurrentProfile();
        _exportButton.Click += (s, e) => ExportCurrentProfile();
        _importButton.Click += (s, e) => ImportProfile();
        _closeButton.Click += (s, e) => Close();

        _overrideTabSizeCheck.CheckedChanged += (s, e) => _overrideTabSizeVal.Enabled = _overrideTabSizeCheck.Checked;
        _overrideInsertSpacesCheck.CheckedChanged += (s, e) => _overrideInsertSpacesVal.Enabled = _overrideInsertSpacesCheck.Checked;
        _overrideFontSizeCheck.CheckedChanged += (s, e) => _overrideFontSizeVal.Enabled = _overrideFontSizeCheck.Checked;
        _overrideFontNameCheck.CheckedChanged += (s, e) => _overrideFontNameVal.Enabled = _overrideFontNameCheck.Checked;
        _overrideThemeCheck.CheckedChanged += (s, e) => _overrideThemeVal.Enabled = _overrideThemeCheck.Checked;
        _overrideWordWrapCheck.CheckedChanged += (s, e) => _overrideWordWrapVal.Enabled = _overrideWordWrapCheck.Checked;

        // Collapsible section events
        _workspaceSection!.ExpandedChanged += Section_ExpandedChanged;
        _commandsSection!.ExpandedChanged += Section_ExpandedChanged;
        _overridesSection!.ExpandedChanged += Section_ExpandedChanged;
    }

    // ========================================
    // Profile List Management
    // ========================================

    private void LoadProfileList()
    {
        _profileList.Items.Clear();
        foreach (string name in _manager.ProfileNames.OrderBy(n => n))
        {
            _profileList.Items.Add(name);
        }
        string active = _manager.ActiveProfileName ?? "Default";
        if (_profileList.Items.Contains(active))
            _profileList.SelectedItem = active;
    }

    private void FilterProfiles(string query)
    {
        string lower = query.Trim().ToLowerInvariant();
        _profileList.BeginUpdate();
        _profileList.Items.Clear();
        foreach (string name in _manager.ProfileNames)
        {
            if (string.IsNullOrEmpty(lower) || name.ToLowerInvariant().Contains(lower))
                _profileList.Items.Add(name);
        }
        _profileList.EndUpdate();
    }

    private void ProfileList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _profileList.Items[e.Index] is not string name) return;
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var rect = e.Bounds;
        var theme = ThemeManager.Instance.CurrentTheme;

        // Background
        if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
        {
            using var selBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
            g.FillRectangle(selBrush, rect);
        }
        else
        {
            using var backBrush = new SolidBrush(theme.EditorBackground);
            g.FillRectangle(backBrush, rect);
        }

        // Draw colored circle + first letter
        Color circleColor = theme.Accent;
        var profile = _manager.LoadProfile(name);
        if (profile?.ColorHex != null && profile.ColorHex.Length == 7)
        {
            try { circleColor = ColorTranslator.FromHtml(profile.ColorHex); } catch { }
        }
        int circleSize = 14;
        int circlePad = 4;
        Rectangle circleRect = new Rectangle(rect.X + circlePad, rect.Y + (rect.Height - circleSize) / 2, circleSize, circleSize);
        using var brush = new SolidBrush(circleColor);
        g.FillEllipse(brush, circleRect);
        using var pen = new Pen(theme.Border);
        g.DrawEllipse(pen, circleRect);

        // Draw profile name (and maybe description? skip for now)
        int textX = circleRect.Right + 6;
        int textWidth = rect.Width - textX - 10;
        using var textBrush = new SolidBrush(theme.Text);
        using var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        var textRect = new Rectangle(textX, rect.Y, textWidth, rect.Height);
        g.DrawString(name, Font, textBrush, textRect, fmt);
    }

    private void ProfileList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_profileList.SelectedItem is not string name) return;
        _currentProfile = _manager.LoadProfile(name);
        if (_currentProfile == null) return;
        PopulateDetails(_currentProfile);
        UpdateButtonStates();
    }

    private void PopulateDetails(UserProfile profile)
    {
        _nameBox.Text = profile.Name;
        _descriptionBox.Text = profile.Description ?? "";
        _workspaceBox.Text = profile.WorkspaceRoot ?? "";
        _buildBox.Text = profile.BuildCommand;
        _runBox.Text = profile.RunCommand;
        _testBox.Text = profile.TestCommand;

        // Overrides
        _overrideTabSizeCheck.Checked = profile.OverrideTabSize.HasValue;
        _overrideTabSizeVal.Value = profile.OverrideTabSize ?? 4;
        _overrideTabSizeVal.Enabled = profile.OverrideTabSize.HasValue;

        _overrideInsertSpacesCheck.Checked = profile.OverrideInsertSpaces.HasValue;
        _overrideInsertSpacesVal.Checked = profile.OverrideInsertSpaces ?? true;
        _overrideInsertSpacesVal.Enabled = profile.OverrideInsertSpaces.HasValue;

        _overrideFontSizeCheck.Checked = profile.OverrideFontSize.HasValue;
        _overrideFontSizeVal.Value = decimal.Parse((profile.OverrideFontSize ?? 12).ToString("0.##"));
        _overrideFontSizeVal.Enabled = profile.OverrideFontSize.HasValue;

        _overrideFontNameCheck.Checked = !string.IsNullOrEmpty(profile.OverrideFontName);
        _overrideFontNameVal.Text = profile.OverrideFontName ?? "Consolas";
        _overrideFontNameVal.Enabled = _overrideFontNameCheck.Checked;

        _overrideThemeCheck.Checked = !string.IsNullOrEmpty(profile.OverrideThemeName);
        _overrideThemeVal.Text = profile.OverrideThemeName ?? "Dark";
        _overrideThemeVal.Enabled = _overrideThemeCheck.Checked;

        _overrideWordWrapCheck.Checked = profile.OverrideWordWrap.HasValue;
        _overrideWordWrapVal.Checked = profile.OverrideWordWrap ?? false;
        _overrideWordWrapVal.Enabled = _overrideWordWrapCheck.Checked;

        // Color
        _colorLabel.Text = profile.ColorHex ?? "#0078D4";
        try { _colorPreviewBox.BackColor = ColorTranslator.FromHtml(_colorLabel.Text); } catch { _colorPreviewBox.BackColor = _theme.Accent; }

        // Recent workspaces
        _recentWorkspacesList.Items.Clear();
        if (profile.Workspaces != null)
            _recentWorkspacesList.Items.AddRange(profile.Workspaces.ToArray());

        // Default on startup
        _defaultOnStartupCheck.Checked = _manager.ActiveProfileName == profile.Name;

        // Usage
        _lastUsedLabel.Text = $"Last used: {profile.LastUsedDisplay}";
        _usageLabel.Text = $"Used {profile.UsageCount} times";
    }

    private void UpdateButtonStates()
    {
        bool isDefault = _currentProfile?.Name == "Default";
        _deleteButton.Enabled = !isDefault;
        _duplicateButton.Enabled = !isDefault;
        _renameButton.Enabled = !isDefault;
    }

    // ========================================
    // Actions
    // ========================================

    private void CreateBlankProfile()
    {
        string baseName = "New Profile"; string name = baseName; int count = 1;
        while (_manager.ProfileNames.Any(n => n == name)) name = $"{baseName} {count++}";
        var profile = new UserProfile
        {
            Name = name,
            WorkspaceRoot = null,
            BuildCommand = "dotnet build",
            RunCommand = "dotnet run",
            TestCommand = "dotnet test",
            IconId = 0,
            ColorHex = "#0078D4",
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.MinValue,
            UsageCount = 0
        };
        _manager.SaveProfile(profile);
        LoadProfileList();
        _profileList.SelectedItem = name;
    }

    private void CreateFromCurrentWindow()
    {
        string baseName = "Profile " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        string name = baseName; int count = 1;
        while (_manager.ProfileNames.Any(n => n == name)) name = $"{baseName} ({count++})";

        var profile = new UserProfile
        {
            Name = name,
            WorkspaceRoot = _mainForm.WorkspaceRoot,
            BuildCommand = "dotnet build",
            RunCommand = "dotnet run",
            TestCommand = "dotnet test",
            OverrideTabSize = _mainForm.CurrentTabSize,
            OverrideInsertSpaces = _mainForm.CurrentInsertSpaces,
            OverrideFontSize = _mainForm.CurrentFontSize,
            OverrideFontName = _mainForm.CurrentFontName,
            OverrideThemeName = _mainForm.CurrentThemeName,
            OverrideWordWrap = _mainForm.CurrentWordWrap,
            ColorHex = "#0078D4",
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.MinValue,
            UsageCount = 0
        };

        _manager.SaveProfile(profile);
        LoadProfileList();
        _profileList.SelectedItem = name;
        MessageBox.Show($"Created profile \"{name}\" from current window settings.", "Profile Created",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CreateFromCurrentWorkspace()
    {
        string wsRoot = _mainForm.WorkspaceRoot ?? "";
        string baseName = "Workspace " + (string.IsNullOrEmpty(wsRoot) ? "Unknown" : Path.GetFileName(wsRoot));
        string name = baseName; int count = 1;
        while (_manager.ProfileNames.Any(n => n == name)) name = $"{baseName} ({count++})";

        var profile = new UserProfile
        {
            Name = name,
            WorkspaceRoot = wsRoot,
            BuildCommand = "dotnet build",
            RunCommand = "dotnet run",
            TestCommand = "dotnet test",
            ColorHex = "#0078D4",
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.MinValue,
            UsageCount = 0
        };

        _manager.SaveProfile(profile);
        LoadProfileList();
        _profileList.SelectedItem = name;
    }

    private void DuplicateCurrentProfile()
    {
        if (_currentProfile == null || _currentProfile.Name == "Default") return;
        string newName = _currentProfile.Name + " Copy"; int count = 1;
        while (_manager.ProfileNames.Any(n => n == newName)) newName = $"{_currentProfile.Name} Copy ({count++})";
        var dup = _manager.DuplicateProfile(_currentProfile.Name, newName);
        if (dup != null)
        {
            LoadProfileList();
            _profileList.SelectedItem = newName;
        }
    }

    private void RenameCurrentProfile()
    {
        if (_currentProfile == null || _currentProfile.Name == "Default") return;
        string? newName = SimpleInputDialog.Show(this, "Rename Profile", "New name:", _currentProfile.Name);
        if (!string.IsNullOrWhiteSpace(newName))
        {
            newName = newName.Trim();
            if (_manager.ProfileNames.Contains(newName))
            {
                MessageBox.Show($"A profile named \"{newName}\" already exists.", "Rename Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_manager.RenameProfile(_currentProfile.Name, newName))
            {
                LoadProfileList();
                _profileList.SelectedItem = newName;
            }
        }
    }

    private void DeleteCurrentProfile()
    {
        if (_currentProfile == null || _currentProfile.Name == "Default") return;
        var result = MessageBox.Show($"Delete profile \"{_currentProfile.Name}\"? This cannot be undone.", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _manager.DeleteProfile(_currentProfile.Name);
            _profileList.SelectedIndex = -1;
            LoadProfileList();
        }
    }

    private void BrowseWorkspace()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select Workspace Root Folder" };
        if (dlg.ShowDialog() == DialogResult.OK)
            _workspaceBox.Text = dlg.SelectedPath;
    }

    private void AddCurrentWorkspace()
    {
        string? ws = _workspaceBox.Text?.Trim();
        if (!string.IsNullOrEmpty(ws) && !_recentWorkspacesList.Items.Contains(ws))
        {
            _recentWorkspacesList.Items.Add(ws);
        }
    }

    private void RemoveSelectedWorkspace()
    {
        if (_recentWorkspacesList.SelectedItem != null)
            _recentWorkspacesList.Items.Remove(_recentWorkspacesList.SelectedItem);
    }

    private void ChooseColor()
    {
        using var dlg = new ColorPickerDialog(_colorPreviewBox.BackColor);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _colorPreviewBox.BackColor = dlg.SelectedColor;
            _colorLabel.Text = $"#{dlg.SelectedColor.R:X2}{dlg.SelectedColor.G:X2}{dlg.SelectedColor.B:X2}";
        }
    }

    private void SaveCurrentProfile()
    {
        if (_currentProfile == null) return;
        string newName = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            MessageBox.Show("Profile name cannot be empty.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (newName != _currentProfile.Name && _manager.ProfileNames.Contains(newName))
        {
            MessageBox.Show($"A profile named \"{newName}\" already exists.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Build workspaces list
        var workspaces = new List<string>();
        foreach (var item in _recentWorkspacesList.Items)
            workspaces.Add(item.ToString() ?? "");

        var profile = new UserProfile
        {
            Name = newName,
            Description = _descriptionBox.Text.Trim(),
            WorkspaceRoot = string.IsNullOrWhiteSpace(_workspaceBox.Text) ? null : _workspaceBox.Text.Trim(),
            Workspaces = workspaces,
            BuildCommand = string.IsNullOrWhiteSpace(_buildBox.Text) ? "dotnet build" : _buildBox.Text.Trim(),
            RunCommand = string.IsNullOrWhiteSpace(_runBox.Text) ? "dotnet run" : _runBox.Text.Trim(),
            TestCommand = string.IsNullOrWhiteSpace(_testBox.Text) ? "dotnet test" : _testBox.Text.Trim(),
            IconId = _currentProfile.IconId,
            ColorHex = _colorLabel.Text,
            CreatedAt = _currentProfile.CreatedAt,
            LastUsed = _currentProfile.LastUsed,
            UsageCount = _currentProfile.UsageCount,
            OverrideTabSize = _overrideTabSizeCheck.Checked ? (int)_overrideTabSizeVal.Value : null,
            OverrideInsertSpaces = _overrideInsertSpacesCheck.Checked ? _overrideInsertSpacesVal.Checked : null,
            OverrideFontSize = _overrideFontSizeCheck.Checked ? (float)_overrideFontSizeVal.Value : null,
            OverrideFontName = _overrideFontNameCheck.Checked ? _overrideFontNameVal.Text : null,
            OverrideThemeName = _overrideThemeCheck.Checked ? _overrideThemeVal.Text : null,
            OverrideWordWrap = _overrideWordWrapCheck.Checked ? _overrideWordWrapVal.Checked : null
        };

        // Save
        _manager.SaveProfile(profile);
        _mainForm.ApplyProfile(profile);
        if (_defaultOnStartupCheck.Checked)
            _manager.ActiveProfileName = profile.Name;

        LoadProfileList();
        _profileList.SelectedItem = profile.Name;
        MessageBox.Show($"Profile \"{profile.Name}\" saved.", "Save Complete",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportCurrentProfile()
    {
        if (_currentProfile == null) return;
        using var dlg = new SaveFileDialog
        {
            Title = "Export Profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"{_currentProfile.Name}.json"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(_currentProfile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"Profile exported to {dlg.FileName}", "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ImportProfile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import Profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = File.ReadAllText(dlg.FileName);
                var profile = System.Text.Json.JsonSerializer.Deserialize<UserProfile>(json);
                if (profile == null) throw new Exception("Invalid profile format.");

                string originalName = profile.Name;
                string newName = originalName;
                int count = 1;
                while (_manager.ProfileNames.Any(n => n == newName))
                    newName = $"{originalName} (imported {count++})";
                if (newName != originalName)
                {
                    profile = profile with { Name = newName };
                }

                _manager.SaveProfile(profile);
                LoadProfileList();
                _profileList.SelectedItem = newName;
                MessageBox.Show($"Profile \"{newName}\" imported.", "Import Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnProfileChanged(UserProfile profile)
    {
        if (InvokeRequired) { Invoke(new Action(() => OnProfileChanged(profile))); return; }
        LoadProfileList();
        if (_currentProfile != null && profile.Name == _currentProfile.Name)
            PopulateDetails(profile);
    }

    private void OnProfileDeleted(string name)
    {
        if (InvokeRequired) { Invoke(new Action(() => OnProfileDeleted(name))); return; }
        _currentProfile = null;
        LoadProfileList();
    }

    private void Section_ExpandedChanged(object? sender, EventArgs e)
    {
        LayoutRightPanel();
        UpdateShowAllButtonText();
    }

    private void ToggleAllAdvancedSections()
    {
        bool allExpanded = _workspaceSection!.Expanded && _commandsSection!.Expanded && _overridesSection!.Expanded;
        bool newState = !allExpanded; // toggle
        _workspaceSection.Expanded = newState;
        _commandsSection!.Expanded = newState;
        _overridesSection!.Expanded = newState;
    }

    private void UpdateShowAllButtonText()
    {
        bool allExpanded = _workspaceSection!.Expanded && _commandsSection!.Expanded && _overridesSection!.Expanded;
        _showAllButton.Text = allExpanded ? "▲ Hide Advanced Settings" : "▼ Show All Settings";
    }

    private void LayoutRightPanel()
    {
        // Controls that should move when sections expand/collapse.
        // Static detail fields (title, name, description, appearance) stay at their designed positions.
        Control[] repositionable = new Control[] { _showAllButton, _workspaceSection!, _commandsSection!, _overridesSection!, _defaultOnStartupCheck, _lastUsedLabel, _usageLabel, _actionPanel! };

        // Start after the appearance group
        int y = _appearanceGroup!.Bottom + 6;

        foreach (Control ctrl in repositionable)
        {
            if (ctrl != null)
            {
                ctrl.Location = new Point(ctrl.Location.X, y);
                y = ctrl.Bottom + 6;
            }
        }
        _rightPanel.AutoScrollMinSize = new Size(0, y);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _manager.ProfileChanged -= OnProfileChanged;
        _manager.ProfileDeleted -= OnProfileDeleted;
        base.OnFormClosed(e);
    }
}

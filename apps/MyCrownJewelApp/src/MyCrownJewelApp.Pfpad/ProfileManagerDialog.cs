using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class ProfileManagerDialog : Form
{
    private readonly UserProfileManager _manager;
    private readonly Form1 _mainForm;
    private readonly Theme _theme;

    private readonly List<string> _projectTypes = new() { "", "dotnet", "node", "python", "java", "go", "rust", "cpp", "web" };

    private List<UserProfile> _profiles = new();
    private UserProfile? _loadedProfile;
    private WorkspaceInfo? _primaryWorkspace;
    private List<WorkspaceInfo> _recentWorkspaces = new();
    private Color _selectedColor;
    private bool _isDirty;
    private bool _suppressDirtyTracking;
    private bool _suppressSelectionChange;

    private Panel _leftPanel = null!;
    private TextBox _searchBox = null!;
    private ListBox _profileList = null!;
    private Label _profilePlaceholderLabel = null!;
    private ContextMenuStrip _newMenu = null!;
    private Button _newButton = null!;
    private Button _duplicateButton = null!;
    private Button _deleteButton = null!;

    private Panel _headerDot = null!;
    private Label _titleLabel = null!;
    private Label _dirtyBadgeLabel = null!;
    private TabControl _tabControl = null!;

    private TextBox _nameBox = null!;
    private TextBox _descriptionBox = null!;
    private Label _colorHexLabel = null!;
    private Button _colorButton = null!;
    private CheckBox _defaultOnStartupCheck = null!;
    private Label _lastUsedLabel = null!;
    private Label _usageLabel = null!;
    private Label _createdLabel = null!;

    private TextBox _workspaceBox = null!;
    private Button _browseButton = null!;
    private Button _loadWorkspaceButton = null!;
    private ComboBox _projectTypeCombo = null!;
    private ListBox _recentWorkspacesList = null!;
    private Label _recentPlaceholderLabel = null!;
    private Button _removeWorkspaceButton = null!;

    private TextBox _buildBox = null!;
    private Button _resetBuildButton = null!;
    private TextBox _runBox = null!;
    private Button _resetRunButton = null!;
    private TextBox _testBox = null!;
    private Button _resetTestButton = null!;

    private CheckBox _overrideTabSizeCheck = null!;
    private NumericUpDown _overrideTabSizeValue = null!;
    private CheckBox _overrideInsertSpacesCheck = null!;
    private CheckBox _overrideInsertSpacesValue = null!;
    private CheckBox _overrideFontSizeCheck = null!;
    private NumericUpDown _overrideFontSizeValue = null!;
    private CheckBox _overrideFontNameCheck = null!;
    private TextBox _overrideFontNameBox = null!;
    private Button _overrideFontNameButton = null!;
    private CheckBox _overrideThemeCheck = null!;
    private ComboBox _overrideThemeCombo = null!;
    private CheckBox _overrideWordWrapCheck = null!;
    private CheckBox _overrideWordWrapValue = null!;
    private CheckBox _overrideVimModeCheck = null!;
    private CheckBox _overrideVimModeValue = null!;
    private CheckBox _overrideAutoSaveCheck = null!;
    private CheckBox _overrideAutoSaveValue = null!;

    private CheckBox _overrideGutterCheck = null!;
    private CheckBox _overrideGutterValue = null!;
    private CheckBox _overrideStatusBarCheck = null!;
    private CheckBox _overrideStatusBarValue = null!;
    private CheckBox _overrideGuideCheck = null!;
    private CheckBox _overrideGuideValue = null!;
    private CheckBox _overrideGuideColumnCheck = null!;
    private NumericUpDown _overrideGuideColumnValue = null!;
    private CheckBox _overrideMinimapCheck = null!;
    private CheckBox _overrideMinimapValue = null!;
    private CheckBox _overrideRainbowBracketsCheck = null!;
    private CheckBox _overrideRainbowBracketsValue = null!;
    private CheckBox _overrideBreadcrumbsCheck = null!;
    private CheckBox _overrideBreadcrumbsValue = null!;
    private CheckBox _overrideHoverHighlightCheck = null!;
    private CheckBox _overrideHoverHighlightValue = null!;
    private CheckBox _overrideSyntaxHighlightingCheck = null!;
    private CheckBox _overrideSyntaxHighlightingValue = null!;
    private CheckBox _overrideAnalyzersCheck = null!;
    private CheckBox _overrideAnalyzersValue = null!;
    private CheckBox _overrideStickyScrollCheck = null!;
    private CheckBox _overrideStickyScrollValue = null!;
    private CheckBox _overrideTerminalShellCheck = null!;
    private TextBox _overrideTerminalShellBox = null!;
    private CheckBox _overrideTerminalHeightCheck = null!;
    private NumericUpDown _overrideTerminalHeightValue = null!;

    private Button _activateButton = null!;
    private Button _saveButton = null!;
    private Button _exportButton = null!;
    private Button _importButton = null!;
    private Button _closeButton = null!;

    public ProfileManagerDialog(UserProfileManager manager, Form1 mainForm)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
        _theme = ThemeManager.Instance.CurrentTheme;
        _selectedColor = _theme.Accent;

        Text = "Manage Profiles";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(700, 500);
        Size = new Size(860, 620);
        MaximizeBox = true;
        MinimizeBox = false;
        KeyPreview = true;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        RegisterEvents();
        RefreshProfileList(_manager.ActiveProfileName ?? "Default");
        UpdateHeader();
        UpdateActionStates();
        UpdateRecentWorkspacePlaceholder();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_theme.IsLight)
        {
            NativeThemed.ApplyDarkModeToWindow(Handle);
        }
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = _theme.PanelBackground,
            Padding = new Padding(12, 10, 12, 10)
        };

        var actionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        _activateButton = CreateButton("Activate", 92);
        _saveButton = CreateButton("Save", 92);
        _exportButton = CreateButton("Export", 92);
        _importButton = CreateButton("Import", 92);
        _closeButton = CreateButton("Close", 92);

        actionFlow.Controls.AddRange(new Control[] { _activateButton, _saveButton, _exportButton, _importButton, _closeButton });
        bottomBar.Controls.Add(actionFlow);

        var contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = _theme.Background
        };

        _leftPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = _theme.PanelBackground,
            Padding = new Padding(12)
        };

        var searchLabel = CreateSectionLabel("Profiles");
        searchLabel.Dock = DockStyle.Top;

        _searchBox = CreateTextBox();
        _searchBox.PlaceholderText = "Search profiles";
        var searchHost = WrapControl(_searchBox);
        searchHost.Dock = DockStyle.Top;
        searchHost.Height = _searchBox.PreferredHeight + 8;

        var listHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.EditorBackground,
            Padding = new Padding(1),
            Margin = new Padding(0, 10, 0, 10)
        };
        listHost.Paint += (s, e) => DrawBorder(e.Graphics, ((Control)s!).ClientRectangle);

        _profileList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 24,
            IntegralHeight = false,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        _profileList.DrawItem += ProfileList_DrawItem;

        _profilePlaceholderLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "No profiles available.",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            Visible = false
        };

        listHost.Controls.Add(_profileList);
        listHost.Controls.Add(_profilePlaceholderLabel);

        var leftButtons = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        leftButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _newButton = CreateButton("+ New▼", 0);
        _duplicateButton = CreateButton("Duplicate", 0);
        _deleteButton = CreateButton("Delete", 0);

        leftButtons.Controls.Add(_newButton, 0, 0);
        leftButtons.Controls.Add(_duplicateButton, 1, 0);
        leftButtons.Controls.Add(_deleteButton, 2, 0);

        _newMenu = new ContextMenuStrip { Renderer = new ThemeAwareMenuRenderer(_theme) };
        _newMenu.Items.Add("Blank Profile", null, (_, _) => CreateBlankProfile());
        _newMenu.Items.Add("From Current Window", null, (_, _) => CreateFromCurrentWindow());
        _newMenu.Items.Add("From Current Workspace", null, (_, _) => CreateFromCurrentWorkspace());

        _leftPanel.Controls.Add(listHost);
        _leftPanel.Controls.Add(leftButtons);
        _leftPanel.Controls.Add(searchHost);
        _leftPanel.Controls.Add(searchLabel);

        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.Background,
            Padding = new Padding(12, 0, 0, 0)
        };

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = _theme.Background
        };

        _headerDot = new Panel
        {
            Location = new Point(0, 12),
            Size = new Size(18, 18),
            BackColor = Color.Transparent
        };
        _headerDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_selectedColor);
            using var pen = new Pen(_theme.Border);
            var rect = new Rectangle(1, 1, _headerDot.Width - 3, _headerDot.Height - 3);
            e.Graphics.FillEllipse(brush, rect);
            e.Graphics.DrawEllipse(pen, rect);
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            Location = new Point(28, 8),
            Size = new Size(360, 26),
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = _theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "No profile selected"
        };

        _dirtyBadgeLabel = new Label
        {
            AutoSize = true,
            Visible = false,
            Location = new Point(396, 12),
            Padding = new Padding(8, 3, 8, 3),
            BackColor = FlatUiHelper.WarningColor(_theme),
            ForeColor = FlatUiHelper.BadgeForeground(_theme),
            Text = "Unsaved"
        };

        headerPanel.Controls.AddRange(new Control[] { _headerDot, _titleLabel, _dirtyBadgeLabel });

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(120, 28),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(14, 6)
        };
        _tabControl.DrawItem += TabControl_DrawItem;

        _tabControl.TabPages.Add(CreateGeneralTab());
        _tabControl.TabPages.Add(CreateWorkspaceTab());
        _tabControl.TabPages.Add(CreateCommandsTab());
        _tabControl.TabPages.Add(CreateOverridesTab());

        rightPanel.Controls.Add(_tabControl);
        rightPanel.Controls.Add(headerPanel);

        contentHost.Controls.Add(rightPanel);
        contentHost.Controls.Add(_leftPanel);

        Controls.Add(contentHost);
        Controls.Add(bottomBar);

        ResumeLayout();
    }

    private TabPage CreateGeneralTab()
    {
        var page = CreateTabPage("General");
        var content = CreateTabContentPanel();
        var layout = CreateFormLayout();

        AddRow(layout, "Name", CreateFieldPanel(out _nameBox));
        _nameBox.PlaceholderText = "Profile name";

        var descriptionHost = CreateFieldPanel(out _descriptionBox, true, 92);
        _descriptionBox.PlaceholderText = "What is this profile for?";
        AddRow(layout, "Description", descriptionHost);

        var colorRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        var colorDot = new Panel { Size = new Size(24, 24), Margin = new Padding(0, 3, 8, 0) };
        colorDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_selectedColor);
            using var pen = new Pen(_theme.Border);
            e.Graphics.FillEllipse(brush, 1, 1, 21, 21);
            e.Graphics.DrawEllipse(pen, 1, 1, 21, 21);
        };
        _colorHexLabel = new Label
        {
            AutoSize = true,
            ForeColor = _theme.Muted,
            Padding = new Padding(0, 5, 0, 0)
        };
        _colorButton = CreateButton("Change", 88);
        _colorButton.Margin = new Padding(8, 0, 0, 0);
        colorRow.Controls.AddRange(new Control[] { colorDot, _colorHexLabel, _colorButton });
        AddRow(layout, "Color", colorRow);

        _defaultOnStartupCheck = CreateCheckBox("Use this profile on startup");
        AddRow(layout, "Startup", _defaultOnStartupCheck);

        var statsSection = CreateSectionLabel("Usage");
        statsSection.Margin = new Padding(0, 20, 0, 10);
        layout.Controls.Add(statsSection, 0, layout.RowCount);
        layout.SetColumnSpan(statsSection, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        _lastUsedLabel = CreateMutedLabel();
        _usageLabel = CreateMutedLabel();
        _createdLabel = CreateMutedLabel();
        AddRow(layout, "Last used", _lastUsedLabel);
        AddRow(layout, "Usage count", _usageLabel);
        AddRow(layout, "Created", _createdLabel);

        content.Controls.Add(layout);
        page.Controls.Add(content);
        return page;
    }

    private TabPage CreateWorkspaceTab()
    {
        var page = CreateTabPage("Workspace");
        var content = CreateTabContentPanel();
        var layout = CreateFormLayout();

        var workspaceRow = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 3
        };
        workspaceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspaceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        workspaceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var workspaceHost = CreateFieldPanel(out _workspaceBox);
        workspaceHost.Dock = DockStyle.Fill;
        _workspaceBox.PlaceholderText = "Primary workspace path";

        _browseButton = CreateButton("Browse", 88);
        _loadWorkspaceButton = CreateButton("Load .code-workspace", 150);

        workspaceRow.Controls.Add(workspaceHost, 0, 0);
        workspaceRow.Controls.Add(_browseButton, 1, 0);
        workspaceRow.Controls.Add(_loadWorkspaceButton, 2, 0);

        AddRow(layout, "Primary workspace", workspaceRow);

        _projectTypeCombo = CreateComboBox();
        foreach (var projectType in _projectTypes)
        {
            _projectTypeCombo.Items.Add(string.IsNullOrEmpty(projectType) ? "Auto detect" : projectType);
        }
        AddRow(layout, "Project type", _projectTypeCombo);

        var recentHeader = CreateSectionLabel("Recent workspaces");
        recentHeader.Margin = new Padding(0, 20, 0, 10);
        layout.Controls.Add(recentHeader, 0, layout.RowCount);
        layout.SetColumnSpan(recentHeader, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        var recentHost = new Panel
        {
            Height = 220,
            Dock = DockStyle.Top,
            BackColor = _theme.EditorBackground,
            Padding = new Padding(1)
        };
        recentHost.Paint += (s, e) => DrawBorder(e.Graphics, ((Control)s!).ClientRectangle);

        _recentWorkspacesList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 40,
            IntegralHeight = false,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        _recentWorkspacesList.DrawItem += RecentWorkspacesList_DrawItem;

        _recentPlaceholderLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "No recent workspaces.",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            Visible = false
        };

        recentHost.Controls.Add(_recentWorkspacesList);
        recentHost.Controls.Add(_recentPlaceholderLabel);

        var recentRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 220,
            ColumnCount = 2
        };
        recentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        recentRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _removeWorkspaceButton = CreateButton("Remove", 88);
        recentRow.Controls.Add(recentHost, 0, 0);
        recentRow.Controls.Add(_removeWorkspaceButton, 1, 0);

        AddRow(layout, "Recent list", recentRow);

        content.Controls.Add(layout);
        page.Controls.Add(content);
        return page;
    }

    private TabPage CreateCommandsTab()
    {
        var page = CreateTabPage("Commands");
        var content = CreateTabContentPanel();
        var layout = CreateFormLayout();

        AddCommandRow(layout, "Build", out _buildBox, out _resetBuildButton);
        AddCommandRow(layout, "Run", out _runBox, out _resetRunButton);
        AddCommandRow(layout, "Test", out _testBox, out _resetTestButton);

        content.Controls.Add(layout);
        page.Controls.Add(content);
        return page;
    }

    private TabPage CreateOverridesTab()
    {
        var page = CreateTabPage("Overrides");
        var content = CreateTabContentPanel();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 6, 0, 0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var left = CreateOverrideColumn("Editor");
        var right = CreateOverrideColumn("Workspace + UI");

        AddNumericOverride(left, "Tab size", 2, 12, 1, out _overrideTabSizeCheck, out _overrideTabSizeValue);
        AddBoolOverride(left, "Insert spaces", out _overrideInsertSpacesCheck, out _overrideInsertSpacesValue);
        AddDecimalOverride(left, "Font size", 6, 36, 0.5m, 1, out _overrideFontSizeCheck, out _overrideFontSizeValue);
        AddTextOverride(left, "Font family", out _overrideFontNameCheck, out _overrideFontNameBox, out _overrideFontNameButton, true);
        AddComboOverride(left, "Theme", ThemeManager.Themes.Keys.OrderBy(x => x), out _overrideThemeCheck, out _overrideThemeCombo);
        AddBoolOverride(left, "Word wrap", out _overrideWordWrapCheck, out _overrideWordWrapValue);
        AddBoolOverride(left, "Vim mode", out _overrideVimModeCheck, out _overrideVimModeValue);
        AddBoolOverride(left, "Auto save", out _overrideAutoSaveCheck, out _overrideAutoSaveValue);

        AddBoolOverride(right, "Gutter", out _overrideGutterCheck, out _overrideGutterValue);
        AddBoolOverride(right, "Status bar", out _overrideStatusBarCheck, out _overrideStatusBarValue);
        AddBoolOverride(right, "Guide", out _overrideGuideCheck, out _overrideGuideValue);
        AddNumericOverride(right, "Guide column", 40, 240, 1, out _overrideGuideColumnCheck, out _overrideGuideColumnValue);
        AddBoolOverride(right, "Minimap", out _overrideMinimapCheck, out _overrideMinimapValue);
        AddBoolOverride(right, "Rainbow brackets", out _overrideRainbowBracketsCheck, out _overrideRainbowBracketsValue);
        AddBoolOverride(right, "Breadcrumbs", out _overrideBreadcrumbsCheck, out _overrideBreadcrumbsValue);
        AddBoolOverride(right, "Hover highlight", out _overrideHoverHighlightCheck, out _overrideHoverHighlightValue);
        AddBoolOverride(right, "Syntax highlighting", out _overrideSyntaxHighlightingCheck, out _overrideSyntaxHighlightingValue);
        AddBoolOverride(right, "Analyzers", out _overrideAnalyzersCheck, out _overrideAnalyzersValue);
        AddBoolOverride(right, "Sticky scroll", out _overrideStickyScrollCheck, out _overrideStickyScrollValue);
        AddTextOverride(right, "Terminal shell", out _overrideTerminalShellCheck, out _overrideTerminalShellBox, out _, false);
        AddNumericOverride(right, "Terminal height", 120, 600, 1, out _overrideTerminalHeightCheck, out _overrideTerminalHeightValue);

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);

        content.Controls.Add(root);
        page.Controls.Add(content);
        return page;
    }

    private void RegisterEvents()
    {
        Load += (_, _) => NativeThemed.ApplyThemeToChildScrollbars(this, !_theme.IsLight);
        FormClosing += ProfileManagerDialog_FormClosing;
        FormClosed += (_, _) =>
        {
            _manager.ProfileChanged -= Manager_ProfileChanged;
            _manager.ProfileDeleted -= Manager_ProfileDeleted;
        };
        KeyDown += ProfileManagerDialog_KeyDown;

        _manager.ProfileChanged += Manager_ProfileChanged;
        _manager.ProfileDeleted += Manager_ProfileDeleted;

        _searchBox.TextChanged += (_, _) => RefreshProfileList(GetSelectedProfileName());
        _profileList.SelectedIndexChanged += ProfileList_SelectedIndexChanged;
        _newButton.Click += (_, _) => _newMenu.Show(_newButton, new Point(0, _newButton.Height));
        _duplicateButton.Click += (_, _) => DuplicateCurrentProfile();
        _deleteButton.Click += (_, _) => DeleteCurrentProfile();

        _colorButton.Click += (_, _) => ChooseColor();
        _browseButton.Click += (_, _) => BrowseWorkspace();
        _loadWorkspaceButton.Click += (_, _) => LoadCodeWorkspace();
        _removeWorkspaceButton.Click += (_, _) => RemoveSelectedRecentWorkspace();

        _resetBuildButton.Click += (_, _) => ResetCommand(_buildBox, ResolveCommandsForCurrentProjectType().build);
        _resetRunButton.Click += (_, _) => ResetCommand(_runBox, ResolveCommandsForCurrentProjectType().run);
        _resetTestButton.Click += (_, _) => ResetCommand(_testBox, ResolveCommandsForCurrentProjectType().test);

        _overrideFontNameButton.Click += (_, _) => ChooseOverrideFont();

        _activateButton.Click += (_, _) => ActivateCurrentProfile();
        _saveButton.Click += (_, _) => SaveCurrentProfile();
        _exportButton.Click += (_, _) => ExportCurrentProfile();
        _importButton.Click += (_, _) => ImportProfile();
        _closeButton.Click += (_, _) => Close();

        HookDirty(_nameBox, _descriptionBox, _workspaceBox, _buildBox, _runBox, _testBox,
            _defaultOnStartupCheck, _projectTypeCombo,
            _overrideTabSizeCheck, _overrideTabSizeValue,
            _overrideInsertSpacesCheck, _overrideInsertSpacesValue,
            _overrideFontSizeCheck, _overrideFontSizeValue,
            _overrideFontNameCheck, _overrideFontNameBox,
            _overrideThemeCheck, _overrideThemeCombo,
            _overrideWordWrapCheck, _overrideWordWrapValue,
            _overrideVimModeCheck, _overrideVimModeValue,
            _overrideAutoSaveCheck, _overrideAutoSaveValue,
            _overrideGutterCheck, _overrideGutterValue,
            _overrideStatusBarCheck, _overrideStatusBarValue,
            _overrideGuideCheck, _overrideGuideValue,
            _overrideGuideColumnCheck, _overrideGuideColumnValue,
            _overrideMinimapCheck, _overrideMinimapValue,
            _overrideRainbowBracketsCheck, _overrideRainbowBracketsValue,
            _overrideBreadcrumbsCheck, _overrideBreadcrumbsValue,
            _overrideHoverHighlightCheck, _overrideHoverHighlightValue,
            _overrideSyntaxHighlightingCheck, _overrideSyntaxHighlightingValue,
            _overrideAnalyzersCheck, _overrideAnalyzersValue,
            _overrideStickyScrollCheck, _overrideStickyScrollValue,
            _overrideTerminalShellCheck, _overrideTerminalShellBox,
            _overrideTerminalHeightCheck, _overrideTerminalHeightValue);
    }

    private void ProfileManagerDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            e.SuppressKeyPress = true;
            SaveCurrentProfile();
        }
    }

    private void ProfileManagerDialog_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!EnsurePendingChangesHandled())
        {
            e.Cancel = true;
        }
    }

    private void Manager_ProfileChanged(UserProfile profile)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<UserProfile>(Manager_ProfileChanged), profile);
            return;
        }

        var selectedName = GetSelectedProfileName() ?? _loadedProfile?.Name ?? profile.Name;
        RefreshProfileList(selectedName);
        if (!_isDirty && _loadedProfile?.Name == profile.Name)
        {
            LoadProfile(profile);
        }
    }

    private void Manager_ProfileDeleted(string profileName)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Manager_ProfileDeleted), profileName);
            return;
        }

        if (string.Equals(_loadedProfile?.Name, profileName, StringComparison.OrdinalIgnoreCase))
        {
            _loadedProfile = null;
            _primaryWorkspace = null;
            _recentWorkspaces.Clear();
            _isDirty = false;
        }

        RefreshProfileList(_manager.ActiveProfileName ?? _profiles.FirstOrDefault()?.Name);
    }

    private void RefreshProfileList(string? preferredSelectionName)
    {
        _profiles = _manager.GetAllProfiles()
            .OrderBy(profile => profile.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(profile => profile.Name)
            .ToList();

        var filter = (_searchBox.Text ?? string.Empty).Trim();
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _profiles
            : _profiles.Where(profile => profile.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (profile.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        _suppressSelectionChange = true;
        _profileList.BeginUpdate();
        _profileList.Items.Clear();
        foreach (var profile in filtered)
        {
            _profileList.Items.Add(profile);
        }
        _profileList.EndUpdate();
        _suppressSelectionChange = false;

        _profilePlaceholderLabel.Visible = filtered.Count == 0;
        _profileList.Visible = filtered.Count > 0;

        if (filtered.Count == 0)
        {
            ClearEditor();
            return;
        }

        var selectionName = preferredSelectionName ?? _loadedProfile?.Name ?? _manager.ActiveProfileName ?? filtered[0].Name;
        SelectProfileInList(selectionName);
        if (_profileList.SelectedItem == null && _profileList.Items.Count > 0)
        {
            _profileList.SelectedIndex = 0;
        }
    }

    private void SelectProfileInList(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        _suppressSelectionChange = true;
        for (var index = 0; index < _profileList.Items.Count; index++)
        {
            if (_profileList.Items[index] is UserProfile profile
                && string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase))
            {
                _profileList.SelectedIndex = index;
                _suppressSelectionChange = false;
                return;
            }
        }
        _suppressSelectionChange = false;
    }

    private string? GetSelectedProfileName() => (_profileList.SelectedItem as UserProfile)?.Name;

    private void ProfileList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressSelectionChange || _profileList.SelectedItem is not UserProfile selectedProfile)
        {
            return;
        }

        if (_loadedProfile != null && string.Equals(_loadedProfile.Name, selectedProfile.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!EnsurePendingChangesHandled())
        {
            SelectProfileInList(_loadedProfile?.Name);
            return;
        }

        var freshProfile = _manager.LoadProfile(selectedProfile.Name) ?? selectedProfile;
        LoadProfile(freshProfile);
    }

    private void LoadProfile(UserProfile profile)
    {
        _suppressDirtyTracking = true;
        try
        {
            _loadedProfile = profile;
            _primaryWorkspace = profile.PrimaryWorkspace != null ? CloneWorkspace(profile.PrimaryWorkspace) : null;
            _recentWorkspaces = profile.RecentWorkspaces.Select(CloneWorkspace).ToList();
            _selectedColor = profile.DisplayColor;

            _nameBox.Text = profile.Name;
            _descriptionBox.Text = profile.Description ?? string.Empty;
            _workspaceBox.Text = _primaryWorkspace?.Path ?? string.Empty;
            _projectTypeCombo.SelectedIndex = FindProjectTypeIndex(_primaryWorkspace?.ProjectType);
            _buildBox.Text = profile.BuildCommand;
            _runBox.Text = profile.RunCommand;
            _testBox.Text = profile.TestCommand;
            _defaultOnStartupCheck.Checked = profile.DefaultOnStartup || string.Equals(_manager.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase);
            _lastUsedLabel.Text = profile.LastUsedDisplay;
            _usageLabel.Text = profile.UsageCount.ToString();
            _createdLabel.Text = profile.CreatedAt == default ? "Unknown" : profile.CreatedAt.ToLocalTime().ToString("g");
            _colorHexLabel.Text = profile.ColorHex ?? $"#{profile.DisplayColor.R:X2}{profile.DisplayColor.G:X2}{profile.DisplayColor.B:X2}";

            SetNullableNumeric(_overrideTabSizeCheck, _overrideTabSizeValue, profile.OverrideTabSize, 4);
            SetNullableBool(_overrideInsertSpacesCheck, _overrideInsertSpacesValue, profile.OverrideInsertSpaces, true);
            SetNullableDecimal(_overrideFontSizeCheck, _overrideFontSizeValue, profile.OverrideFontSize, 10m);
            SetNullableText(_overrideFontNameCheck, _overrideFontNameBox, profile.OverrideFontName);
            SetNullableCombo(_overrideThemeCheck, _overrideThemeCombo, profile.OverrideThemeName, ThemeManager.Instance.CurrentTheme.Name);
            SetNullableBool(_overrideWordWrapCheck, _overrideWordWrapValue, profile.OverrideWordWrap, false);
            SetNullableBool(_overrideVimModeCheck, _overrideVimModeValue, profile.OverrideVimMode, false);
            SetNullableBool(_overrideAutoSaveCheck, _overrideAutoSaveValue, profile.OverrideAutoSave, false);
            SetNullableBool(_overrideGutterCheck, _overrideGutterValue, profile.OverrideGutterVisible, true);
            SetNullableBool(_overrideStatusBarCheck, _overrideStatusBarValue, profile.OverrideStatusBarVisible, true);
            SetNullableBool(_overrideGuideCheck, _overrideGuideValue, profile.OverrideShowGuide, false);
            SetNullableNumeric(_overrideGuideColumnCheck, _overrideGuideColumnValue, profile.OverrideGuideColumn, 80);
            SetNullableBool(_overrideMinimapCheck, _overrideMinimapValue, profile.OverrideMinimapVisible, false);
            SetNullableBool(_overrideRainbowBracketsCheck, _overrideRainbowBracketsValue, profile.OverrideRainbowBrackets, false);
            SetNullableBool(_overrideBreadcrumbsCheck, _overrideBreadcrumbsValue, profile.OverrideBreadcrumbs, false);
            SetNullableBool(_overrideHoverHighlightCheck, _overrideHoverHighlightValue, profile.OverrideHoverLineHighlight, false);
            SetNullableBool(_overrideSyntaxHighlightingCheck, _overrideSyntaxHighlightingValue, profile.OverrideSyntaxHighlighting, true);
            SetNullableBool(_overrideAnalyzersCheck, _overrideAnalyzersValue, profile.OverrideAnalyzersEnabled, true);
            SetNullableBool(_overrideStickyScrollCheck, _overrideStickyScrollValue, profile.OverrideStickyScroll, true);
            SetNullableText(_overrideTerminalShellCheck, _overrideTerminalShellBox, profile.OverrideTerminalShell);
            SetNullableNumeric(_overrideTerminalHeightCheck, _overrideTerminalHeightValue, profile.OverrideTerminalHeight, _mainForm.CurrentTerminalHeight);

            RefreshRecentWorkspaceList();
            _isDirty = false;
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        UpdateHeader();
        UpdateActionStates();
        InvalidatePreviewDots();
    }

    private void ClearEditor()
    {
        _loadedProfile = null;
        _primaryWorkspace = null;
        _recentWorkspaces.Clear();
        _selectedColor = _theme.Accent;
        _isDirty = false;
        _tabControl.Enabled = false;
        UpdateHeader();
        UpdateActionStates();
        RefreshRecentWorkspaceList();
    }

    private void UpdateHeader()
    {
        _headerDot.Visible = _loadedProfile != null;
        _titleLabel.Text = _loadedProfile?.Name ?? "No profile selected";
        _dirtyBadgeLabel.Visible = _isDirty && _loadedProfile != null;
        _tabControl.Enabled = _loadedProfile != null;
        _headerDot.Invalidate();
        InvalidatePreviewDots();
    }

    private void UpdateActionStates()
    {
        var hasProfile = _loadedProfile != null;
        var isDefault = string.Equals(_loadedProfile?.Name, "Default", StringComparison.OrdinalIgnoreCase);

        _duplicateButton.Enabled = hasProfile;
        _deleteButton.Enabled = hasProfile && !isDefault;
        _activateButton.Enabled = hasProfile;
        _saveButton.Enabled = hasProfile && !isDefault;
        _exportButton.Enabled = hasProfile;
    }

    private void MarkDirty()
    {
        if (_suppressDirtyTracking || _loadedProfile == null)
        {
            return;
        }

        _isDirty = true;
        UpdateHeader();
    }

    private bool EnsurePendingChangesHandled()
    {
        if (!_isDirty || _loadedProfile == null)
        {
            return true;
        }

        if (string.Equals(_loadedProfile.Name, "Default", StringComparison.OrdinalIgnoreCase))
        {
            _isDirty = false;
            UpdateHeader();
            return true;
        }

        var result = ThemedMessageBox.Show(this,
            $"Save changes to \"{_loadedProfile.Name}\" before continuing?",
            "Unsaved Changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        return result switch
        {
            DialogResult.Yes => SaveCurrentProfile(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void CreateBlankProfile() => CreateProfileFromSeed(BuildProfileSeedFromWorkspace(null, false), "New Profile");

    private void CreateFromCurrentWindow() => CreateProfileFromSeed(BuildProfileSeedFromWorkspace(_mainForm.WorkspaceRoot, true), "Current Window");

    private void CreateFromCurrentWorkspace() => CreateProfileFromSeed(BuildProfileSeedFromWorkspace(_mainForm.WorkspaceRoot, false), "Workspace Profile");

    private void CreateProfileFromSeed(UserProfile seed, string baseName)
    {
        if (!EnsurePendingChangesHandled())
        {
            return;
        }

        var suggestedName = GetUniqueProfileName(baseName);
        var name = SimpleInputDialog.Show(this, "Enter a name for the new profile.", "New Profile", suggestedName)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_manager.ProfileNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            ThemedMessageBox.Show(this, $"A profile named \"{name}\" already exists.", "Profile Exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var profile = seed with
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            LastUsed = default,
            UsageCount = 0
        };

        _manager.SaveProfile(profile);
        RefreshProfileList(profile.Name);
        LoadProfile(profile);
        SelectProfileInList(profile.Name);
        _isDirty = true;
        UpdateHeader();
    }

    private UserProfile BuildProfileSeedFromWorkspace(string? workspacePath, bool captureCurrentWindowOverrides)
    {
        var workspace = BuildWorkspaceInfo(workspacePath, DetectProjectType(workspacePath));
        var commands = ResolveBuildCommandsForType(workspace?.ProjectType);

        return new UserProfile
        {
            Name = string.Empty,
            Description = string.Empty,
            Workspaces = workspace != null ? new List<WorkspaceInfo> { workspace } : new List<WorkspaceInfo>(),
            RecentWorkspaces = new List<WorkspaceInfo>(),
            BuildCommand = commands.build,
            RunCommand = commands.run,
            TestCommand = commands.test,
            ColorHex = $"#{_theme.Accent.R:X2}{_theme.Accent.G:X2}{_theme.Accent.B:X2}",
            OverrideTabSize = captureCurrentWindowOverrides ? _mainForm.CurrentTabSize : null,
            OverrideInsertSpaces = captureCurrentWindowOverrides ? _mainForm.CurrentInsertSpaces : null,
            OverrideFontSize = captureCurrentWindowOverrides ? _mainForm.CurrentFontSize : null,
            OverrideFontName = captureCurrentWindowOverrides ? _mainForm.CurrentFontName : null,
            OverrideThemeName = captureCurrentWindowOverrides ? _mainForm.CurrentThemeName : null,
            OverrideWordWrap = captureCurrentWindowOverrides ? _mainForm.CurrentWordWrap : null,
            OverrideGutterVisible = captureCurrentWindowOverrides ? _mainForm.CurrentGutterVisible : null,
            OverrideStatusBarVisible = captureCurrentWindowOverrides ? _mainForm.CurrentStatusBarVisible : null,
            OverrideShowGuide = captureCurrentWindowOverrides ? _mainForm.CurrentShowGuide : null,
            OverrideGuideColumn = captureCurrentWindowOverrides ? _mainForm.CurrentGuideColumn : null,
            OverrideMinimapVisible = captureCurrentWindowOverrides ? _mainForm.CurrentMinimapVisible : null,
            OverrideRainbowBrackets = captureCurrentWindowOverrides ? _mainForm.CurrentRainbowBrackets : null,
            OverrideBreadcrumbs = captureCurrentWindowOverrides ? _mainForm.CurrentBreadcrumbs : null,
            OverrideHoverLineHighlight = captureCurrentWindowOverrides ? _mainForm.CurrentHoverLineHighlight : null,
            OverrideAutoSave = captureCurrentWindowOverrides ? _mainForm.CurrentAutoSave : null,
            OverrideAnalyzersEnabled = captureCurrentWindowOverrides ? _mainForm.CurrentAnalyzersEnabled : null,
            OverrideTerminalShell = captureCurrentWindowOverrides ? _mainForm.CurrentTerminalShell : null,
            OverrideTerminalHeight = captureCurrentWindowOverrides ? _mainForm.CurrentTerminalHeight : null,
            OverrideVimMode = captureCurrentWindowOverrides ? _mainForm.CurrentVimMode : null,
            OverrideStickyScroll = captureCurrentWindowOverrides ? _mainForm.CurrentStickyScroll : null,
            OverrideSyntaxHighlighting = captureCurrentWindowOverrides ? _mainForm.CurrentSyntaxHighlighting : null
        };
    }

    private void DuplicateCurrentProfile()
    {
        if (_loadedProfile == null)
        {
            return;
        }

        if (!EnsurePendingChangesHandled())
        {
            return;
        }

        var suggestedName = GetUniqueCopyName(_loadedProfile.Name);
        var name = SimpleInputDialog.Show(this, "Enter a name for the duplicated profile.", "Duplicate Profile", suggestedName)?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_manager.ProfileNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            ThemedMessageBox.Show(this, $"A profile named \"{name}\" already exists.", "Profile Exists", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var duplicate = _manager.DuplicateProfile(_loadedProfile.Name, name);
        if (duplicate == null)
        {
            ThemedMessageBox.Show(this, "The profile could not be duplicated.", "Duplicate Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        RefreshProfileList(duplicate.Name);
        LoadProfile(duplicate);
        SelectProfileInList(duplicate.Name);
    }

    private void DeleteCurrentProfile()
    {
        if (_loadedProfile == null || string.Equals(_loadedProfile.Name, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var result = ThemedMessageBox.Show(this,
            $"Delete \"{_loadedProfile.Name}\"? This cannot be undone.",
            "Delete Profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        var deletedName = _loadedProfile.Name;
        _manager.DeleteProfile(deletedName);
        _isDirty = false;
        RefreshProfileList(_manager.ActiveProfileName ?? _profiles.FirstOrDefault(profile => !string.Equals(profile.Name, deletedName, StringComparison.OrdinalIgnoreCase))?.Name);
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog
        {
            AllowFullOpen = true,
            FullOpen = true,
            Color = _selectedColor
        };

        if (NativeThemed.ShowDialogThemed(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            _selectedColor = dialog.Color;
            _colorHexLabel.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            MarkDirty();
        }
    }

    private void BrowseWorkspace()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the primary workspace folder.",
            SelectedPath = Directory.Exists(_workspaceBox.Text) ? _workspaceBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (NativeThemed.ShowDialogThemed(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            SetPrimaryWorkspace(dialog.SelectedPath);
        }
    }

    private void LoadCodeWorkspace()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Load .code-workspace file",
            Filter = "VS Code workspace (*.code-workspace)|*.code-workspace|JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowThemed() != DialogResult.OK)
        {
            return;
        }

        var workspaces = UserProfile.LoadFromCodeWorkspace(dialog.FileName);
        if (workspaces.Count == 0)
        {
            ThemedMessageBox.Show(this, "No workspace folders were found in the selected file.", "Workspace Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _primaryWorkspace = CloneWorkspace(workspaces[0]);
        _workspaceBox.Text = _primaryWorkspace.Path;
        _projectTypeCombo.SelectedIndex = FindProjectTypeIndex(_primaryWorkspace.ProjectType);
        _recentWorkspaces = workspaces.Skip(1).Select(CloneWorkspace).ToList();
        RefreshRecentWorkspaceList();
        MarkDirty();
    }

    private void RemoveSelectedRecentWorkspace()
    {
        if (_recentWorkspacesList.SelectedItem is not WorkspaceInfo workspace)
        {
            return;
        }

        _recentWorkspaces.RemoveAll(item => string.Equals(item.Path, workspace.Path, StringComparison.OrdinalIgnoreCase));
        RefreshRecentWorkspaceList();
        MarkDirty();
    }

    private void ResetCommand(TextBox target, string value)
    {
        target.Text = value;
        target.SelectionStart = target.TextLength;
        MarkDirty();
    }

    private void ChooseOverrideFont()
    {
        using var dialog = new FontDialog
        {
            Font = new Font(string.IsNullOrWhiteSpace(_overrideFontNameBox.Text) ? _mainForm.CurrentFontName : _overrideFontNameBox.Text, _mainForm.CurrentFontSize)
        };

        if (NativeThemed.ShowDialogThemed(() => dialog.ShowDialog(this)) == DialogResult.OK)
        {
            _overrideFontNameCheck.Checked = true;
            _overrideFontNameBox.Text = dialog.Font.Name;
            _overrideFontNameBox.Enabled = true;
            MarkDirty();
        }
    }

    private bool SaveCurrentProfile()
    {
        if (_loadedProfile == null)
        {
            return true;
        }

        if (string.Equals(_loadedProfile.Name, "Default", StringComparison.OrdinalIgnoreCase))
        {
            ThemedMessageBox.Show(this, "The built-in Default profile cannot be edited. Duplicate it to make changes.", "Read-only Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        try
        {
            var updated = BuildProfileFromEditor();
            if (updated == null)
            {
                return false;
            }

            var previousName = _loadedProfile.Name;
            var renamed = !string.Equals(previousName, updated.Name, StringComparison.OrdinalIgnoreCase);

            _manager.SaveProfile(updated);
            if (renamed)
            {
                _manager.DeleteProfile(previousName);
            }

            SynchronizeStartupDefault(updated);
            _loadedProfile = updated;
            _isDirty = false;
            RefreshProfileList(updated.Name);
            LoadProfile(updated);
            SelectProfileInList(updated.Name);
            return true;
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Unable to save the profile: {ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private UserProfile? BuildProfileFromEditor()
    {
        if (_loadedProfile == null)
        {
            return null;
        }

        var name = (_nameBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ThemedMessageBox.Show(this, "Profile name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tabControl.SelectedIndex = 0;
            _nameBox.Focus();
            return null;
        }

        if (!string.Equals(name, _loadedProfile.Name, StringComparison.OrdinalIgnoreCase)
            && _manager.ProfileNames.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            ThemedMessageBox.Show(this, $"A profile named \"{name}\" already exists.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tabControl.SelectedIndex = 0;
            _nameBox.Focus();
            return null;
        }

        var primaryWorkspace = ResolvePrimaryWorkspaceFromEditor();
        var recent = DeduplicateWorkspaces(_recentWorkspaces.Where(item => primaryWorkspace == null || !PathsEqual(item.Path, primaryWorkspace.Path)));
        var projectType = GetSelectedProjectType();
        if (primaryWorkspace != null && !string.IsNullOrWhiteSpace(projectType))
        {
            primaryWorkspace = primaryWorkspace with { ProjectType = projectType };
        }

        return _loadedProfile with
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim(),
            Workspaces = primaryWorkspace != null ? new List<WorkspaceInfo> { primaryWorkspace } : new List<WorkspaceInfo>(),
            RecentWorkspaces = recent,
            BuildCommand = NormalizeCommand(_buildBox.Text, ResolveCommandsForCurrentProjectType().build),
            RunCommand = NormalizeCommand(_runBox.Text, ResolveCommandsForCurrentProjectType().run),
            TestCommand = NormalizeCommand(_testBox.Text, ResolveCommandsForCurrentProjectType().test),
            ColorHex = _colorHexLabel.Text,
            OverrideTabSize = _overrideTabSizeCheck.Checked ? (int)_overrideTabSizeValue.Value : null,
            OverrideInsertSpaces = _overrideInsertSpacesCheck.Checked ? _overrideInsertSpacesValue.Checked : null,
            OverrideFontSize = _overrideFontSizeCheck.Checked ? (float)_overrideFontSizeValue.Value : null,
            OverrideFontName = _overrideFontNameCheck.Checked ? NormalizeNullableText(_overrideFontNameBox.Text) : null,
            OverrideThemeName = _overrideThemeCheck.Checked ? NormalizeNullableText(_overrideThemeCombo.Text) : null,
            OverrideWordWrap = _overrideWordWrapCheck.Checked ? _overrideWordWrapValue.Checked : null,
            OverrideGutterVisible = _overrideGutterCheck.Checked ? _overrideGutterValue.Checked : null,
            OverrideStatusBarVisible = _overrideStatusBarCheck.Checked ? _overrideStatusBarValue.Checked : null,
            OverrideShowGuide = _overrideGuideCheck.Checked ? _overrideGuideValue.Checked : null,
            OverrideGuideColumn = _overrideGuideColumnCheck.Checked ? (int)_overrideGuideColumnValue.Value : null,
            OverrideMinimapVisible = _overrideMinimapCheck.Checked ? _overrideMinimapValue.Checked : null,
            OverrideRainbowBrackets = _overrideRainbowBracketsCheck.Checked ? _overrideRainbowBracketsValue.Checked : null,
            OverrideBreadcrumbs = _overrideBreadcrumbsCheck.Checked ? _overrideBreadcrumbsValue.Checked : null,
            OverrideHoverLineHighlight = _overrideHoverHighlightCheck.Checked ? _overrideHoverHighlightValue.Checked : null,
            OverrideAutoSave = _overrideAutoSaveCheck.Checked ? _overrideAutoSaveValue.Checked : null,
            OverrideAnalyzersEnabled = _overrideAnalyzersCheck.Checked ? _overrideAnalyzersValue.Checked : null,
            OverrideTerminalShell = _overrideTerminalShellCheck.Checked ? NormalizeNullableText(_overrideTerminalShellBox.Text) : null,
            OverrideTerminalHeight = _overrideTerminalHeightCheck.Checked ? (int)_overrideTerminalHeightValue.Value : null,
            OverrideVimMode = _overrideVimModeCheck.Checked ? _overrideVimModeValue.Checked : null,
            OverrideStickyScroll = _overrideStickyScrollCheck.Checked ? _overrideStickyScrollValue.Checked : null,
            OverrideSyntaxHighlighting = _overrideSyntaxHighlightingCheck.Checked ? _overrideSyntaxHighlightingValue.Checked : null,
            DefaultOnStartup = _defaultOnStartupCheck.Checked
        };
    }

    private void SynchronizeStartupDefault(UserProfile profile)
    {
        foreach (var otherProfile in _manager.GetAllProfiles()
                     .Where(item => !string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase) && item.DefaultOnStartup))
        {
            _manager.SaveProfile(otherProfile with { DefaultOnStartup = false });
        }

        if (profile.DefaultOnStartup)
        {
            _manager.ActiveProfileName = profile.Name;
        }
        else if (string.Equals(_manager.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            _manager.ActiveProfileName = null;
        }
    }

    private void ActivateCurrentProfile()
    {
        if (_loadedProfile == null)
        {
            return;
        }

        if (_isDirty && !SaveCurrentProfile())
        {
            return;
        }

        var profile = _manager.LoadProfile(_loadedProfile.Name) ?? _loadedProfile;
        _mainForm.ApplyProfile(profile);
        _manager.IncrementUsage(profile.Name);
        RefreshProfileList(profile.Name);
        LoadProfile(_manager.LoadProfile(profile.Name) ?? profile);
        SelectProfileInList(profile.Name);
    }

    private void ExportCurrentProfile()
    {
        var profile = _loadedProfile;
        if (profile == null)
        {
            return;
        }

        var exportProfile = _isDirty && !string.Equals(profile.Name, "Default", StringComparison.OrdinalIgnoreCase)
            ? BuildProfileFromEditor() ?? profile
            : profile;

        using var dialog = new SaveFileDialog
        {
            Title = "Export Profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"{exportProfile.Name}.json"
        };

        if (dialog.ShowThemed() != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(exportProfile, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Unable to export the profile: {ex.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportProfile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import Profile",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowThemed() != DialogResult.OK)
        {
            return;
        }

        try
        {
            var imported = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(dialog.FileName));
            if (imported == null)
            {
                throw new InvalidOperationException("The selected file is not a valid profile.");
            }

            if (_manager.ProfileNames.Any(existing => string.Equals(existing, imported.Name, StringComparison.OrdinalIgnoreCase)))
            {
                imported = imported with { Name = GetUniqueCopyName(imported.Name) };
            }

            _manager.SaveProfile(imported);
            if (imported.DefaultOnStartup)
            {
                SynchronizeStartupDefault(imported);
            }

            RefreshProfileList(imported.Name);
            LoadProfile(imported);
            SelectProfileInList(imported.Name);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Unable to import the profile: {ex.Message}", "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshRecentWorkspaceList()
    {
        _recentWorkspacesList.BeginUpdate();
        _recentWorkspacesList.Items.Clear();
        foreach (var workspace in _recentWorkspaces)
        {
            _recentWorkspacesList.Items.Add(workspace);
        }
        _recentWorkspacesList.EndUpdate();
        UpdateRecentWorkspacePlaceholder();
    }

    private void UpdateRecentWorkspacePlaceholder()
    {
        var hasItems = _recentWorkspacesList.Items.Count > 0;
        _recentWorkspacesList.Visible = hasItems;
        _recentPlaceholderLabel.Visible = !hasItems;
        _removeWorkspaceButton.Enabled = hasItems;
    }

    private void SetPrimaryWorkspace(string path)
    {
        var detectedProjectType = DetectProjectType(path);
        _primaryWorkspace = BuildWorkspaceInfo(path, detectedProjectType);
        _workspaceBox.Text = path;
        _projectTypeCombo.SelectedIndex = FindProjectTypeIndex(detectedProjectType);
        MarkDirty();
    }

    private WorkspaceInfo? ResolvePrimaryWorkspaceFromEditor()
    {
        var path = (_workspaceBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            _primaryWorkspace = null;
            return null;
        }

        if (!Directory.Exists(path))
        {
            var result = ThemedMessageBox.Show(this,
                $"The folder \"{path}\" does not exist. Create it now?",
                "Create Folder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                throw new InvalidOperationException("Choose an existing workspace folder or create a new one.");
            }

            Directory.CreateDirectory(path);
        }

        var projectType = GetSelectedProjectType();
        _primaryWorkspace = BuildWorkspaceInfo(path, projectType ?? DetectProjectType(path));
        return _primaryWorkspace;
    }

    private string NormalizeCommand(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeNullableText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left).TrimEnd('\\'), Path.GetFullPath(right).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private List<WorkspaceInfo> DeduplicateWorkspaces(IEnumerable<WorkspaceInfo> workspaces)
    {
        var result = new List<WorkspaceInfo>();
        foreach (var workspace in workspaces)
        {
            if (!result.Any(existing => PathsEqual(existing.Path, workspace.Path)))
            {
                result.Add(CloneWorkspace(workspace));
            }
        }
        return result;
    }

    private static WorkspaceInfo CloneWorkspace(WorkspaceInfo workspace) => workspace with { };

    private WorkspaceInfo? BuildWorkspaceInfo(string? path, string? projectType)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return new WorkspaceInfo
        {
            Name = Path.GetFileName(path.TrimEnd('\\')),
            Path = path,
            LastOpened = DateTime.UtcNow,
            IsTrusted = true,
            ProjectType = projectType
        };
    }

    private (string build, string run, string test) ResolveCommandsForCurrentProjectType() => ResolveBuildCommandsForType(GetSelectedProjectType() ?? DetectProjectType(_workspaceBox.Text));

    private static (string build, string run, string test) ResolveBuildCommandsForType(string? projectType) => projectType?.ToLowerInvariant() switch
    {
        "node" => ("npm run build", "npm start", "npm test"),
        "python" => ("python -m build", "python main.py", "pytest"),
        "java" => ("mvn package", "mvn exec:java", "mvn test"),
        "go" => ("go build ./...", "go run .", "go test ./..."),
        "rust" => ("cargo build", "cargo run", "cargo test"),
        "cpp" => ("cmake --build .", ".\\bin\\app.exe", "ctest"),
        "web" => ("npm run build", "npm run dev", "npm test"),
        _ => ("dotnet build", "dotnet run", "dotnet test")
    };

    private static string? DetectProjectType(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return null;
            }

            var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Select(file => Path.GetFileName(file).ToLowerInvariant())
                .ToArray();

            if (files.Any(file => file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) || files.Contains("global.json") || files.Contains("project.json") || files.Any(file => file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))) return "dotnet";
            if (files.Contains("package.json")) return "node";
            if (files.Contains("requirements.txt") || files.Contains("pyproject.toml") || files.Contains("setup.py")) return "python";
            if (files.Contains("pom.xml") || files.Contains("build.gradle")) return "java";
            if (files.Contains("go.mod")) return "go";
            if (files.Contains("cargo.toml")) return "rust";
            if (files.Contains("cmakelists.txt") || files.Any(file => file.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".h", StringComparison.OrdinalIgnoreCase))) return "cpp";
            if (files.Contains("index.html") || files.Contains("vite.config.js") || files.Contains("vite.config.ts")) return "web";
        }
        catch
        {
        }

        return null;
    }

    private string? GetSelectedProjectType()
    {
        if (_projectTypeCombo.SelectedIndex <= 0)
        {
            return null;
        }

        return _projectTypes[_projectTypeCombo.SelectedIndex];
    }

    private int FindProjectTypeIndex(string? projectType)
    {
        if (string.IsNullOrWhiteSpace(projectType))
        {
            return 0;
        }

        var index = _projectTypes.FindIndex(value => string.Equals(value, projectType, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }

    private string GetUniqueProfileName(string baseName)
    {
        var candidate = baseName;
        var suffix = 2;
        while (_manager.ProfileNames.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }
        return candidate;
    }

    private string GetUniqueCopyName(string sourceName)
    {
        var baseName = $"{sourceName} (Copy)";
        var candidate = baseName;
        var suffix = 2;
        while (_manager.ProfileNames.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }
        return candidate;
    }

    private void ProfileList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _profileList.Items[e.Index] is not UserProfile profile)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var backBrush = new SolidBrush(selected ? Color.FromArgb(_theme.IsLight ? 225 : 50, _theme.Accent) : _theme.EditorBackground);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var avatarRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 5, 14, 14);
        using var avatarBrush = new SolidBrush(profile.DisplayColor);
        using var avatarPen = new Pen(_theme.Border);
        e.Graphics.FillEllipse(avatarBrush, avatarRect);
        e.Graphics.DrawEllipse(avatarPen, avatarRect);

        using var textBrush = new SolidBrush(_theme.Text);
        using var textFormat = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        var textRect = new Rectangle(e.Bounds.X + 28, e.Bounds.Y, e.Bounds.Width - 48, e.Bounds.Height);
        e.Graphics.DrawString(profile.Name, Font, textBrush, textRect, textFormat);

        if (string.Equals(_manager.ActiveProfileName, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            using var activeBrush = new SolidBrush(_theme.Accent);
            e.Graphics.FillEllipse(activeBrush, e.Bounds.Right - 16, e.Bounds.Y + 8, 8, 8);
        }

        if (_isDirty && string.Equals(_loadedProfile?.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            using var dirtyBrush = new SolidBrush(FlatUiHelper.WarningColor(_theme));
            e.Graphics.FillEllipse(dirtyBrush, e.Bounds.Right - 28, e.Bounds.Y + 8, 8, 8);
        }
    }

    private void RecentWorkspacesList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _recentWorkspacesList.Items[e.Index] is not WorkspaceInfo workspace)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using var backBrush = new SolidBrush(selected ? Color.FromArgb(_theme.IsLight ? 225 : 50, _theme.Accent) : _theme.EditorBackground);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        using var titleBrush = new SolidBrush(_theme.Text);
        using var metaBrush = new SolidBrush(_theme.Muted);
        using var titleFont = new Font(Font, FontStyle.Bold);
        var titleRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 4, e.Bounds.Width - 16, 16);
        var metaRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 20, e.Bounds.Width - 16, 16);
        e.Graphics.DrawString(workspace.DisplayName, titleFont, titleBrush, titleRect);
        e.Graphics.DrawString(workspace.Path, Font, metaBrush, metaRect);
    }

    private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
    {
        var tabPage = _tabControl.TabPages[e.Index];
        var selected = e.Index == _tabControl.SelectedIndex;
        var rect = e.Bounds;

        using var backBrush = new SolidBrush(selected ? _theme.PanelBackground : _theme.Background);
        e.Graphics.FillRectangle(backBrush, rect);
        using var pen = new Pen(_theme.Border);
        e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        if (selected)
        {
            using var accentBrush = new SolidBrush(_theme.Accent);
            e.Graphics.FillRectangle(accentBrush, rect.X + 1, rect.Bottom - 3, rect.Width - 2, 3);
        }

        TextRenderer.DrawText(e.Graphics, tabPage.Text, Font, rect, selected ? _theme.Text : _theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void HookDirty(params Control[] controls)
    {
        foreach (var control in controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.TextChanged += (_, _) =>
                    {
                        if (ReferenceEquals(textBox, _workspaceBox) && !_suppressDirtyTracking)
                        {
                            _projectTypeCombo.SelectedIndex = FindProjectTypeIndex(DetectProjectType(_workspaceBox.Text));
                        }
                        MarkDirty();
                    };
                    break;
                case CheckBox checkBox:
                    checkBox.CheckedChanged += (_, _) =>
                    {
                        SyncEnabledState(checkBox);
                        MarkDirty();
                    };
                    break;
                case ComboBox comboBox:
                    comboBox.SelectedIndexChanged += (_, _) => MarkDirty();
                    comboBox.TextChanged += (_, _) => MarkDirty();
                    break;
                case NumericUpDown numericUpDown:
                    numericUpDown.ValueChanged += (_, _) => MarkDirty();
                    break;
            }
        }
    }

    private void SyncEnabledState(CheckBox overrideCheck)
    {
        if (ReferenceEquals(overrideCheck, _overrideTabSizeCheck)) _overrideTabSizeValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideInsertSpacesCheck)) _overrideInsertSpacesValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideFontSizeCheck)) _overrideFontSizeValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideFontNameCheck)) { _overrideFontNameBox.Enabled = overrideCheck.Checked; _overrideFontNameButton.Enabled = overrideCheck.Checked; }
        else if (ReferenceEquals(overrideCheck, _overrideThemeCheck)) _overrideThemeCombo.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideWordWrapCheck)) _overrideWordWrapValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideVimModeCheck)) _overrideVimModeValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideAutoSaveCheck)) _overrideAutoSaveValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideGutterCheck)) _overrideGutterValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideStatusBarCheck)) _overrideStatusBarValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideGuideCheck)) _overrideGuideValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideGuideColumnCheck)) _overrideGuideColumnValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideMinimapCheck)) _overrideMinimapValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideRainbowBracketsCheck)) _overrideRainbowBracketsValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideBreadcrumbsCheck)) _overrideBreadcrumbsValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideHoverHighlightCheck)) _overrideHoverHighlightValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideSyntaxHighlightingCheck)) _overrideSyntaxHighlightingValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideAnalyzersCheck)) _overrideAnalyzersValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideStickyScrollCheck)) _overrideStickyScrollValue.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideTerminalShellCheck)) _overrideTerminalShellBox.Enabled = overrideCheck.Checked;
        else if (ReferenceEquals(overrideCheck, _overrideTerminalHeightCheck)) _overrideTerminalHeightValue.Enabled = overrideCheck.Checked;
    }

    private void SetNullableNumeric(CheckBox overrideCheck, NumericUpDown valueControl, int? value, int defaultValue)
    {
        overrideCheck.Checked = value.HasValue;
        valueControl.Value = Math.Max(valueControl.Minimum, Math.Min(valueControl.Maximum, value ?? defaultValue));
        valueControl.Enabled = value.HasValue;
    }

    private void SetNullableDecimal(CheckBox overrideCheck, NumericUpDown valueControl, float? value, decimal defaultValue)
    {
        overrideCheck.Checked = value.HasValue;
        var effective = value.HasValue ? (decimal)value.Value : defaultValue;
        valueControl.Value = Math.Max(valueControl.Minimum, Math.Min(valueControl.Maximum, effective));
        valueControl.Enabled = value.HasValue;
    }

    private void SetNullableBool(CheckBox overrideCheck, CheckBox valueControl, bool? value, bool defaultValue)
    {
        overrideCheck.Checked = value.HasValue;
        valueControl.Checked = value ?? defaultValue;
        valueControl.Enabled = value.HasValue;
    }

    private void SetNullableText(CheckBox overrideCheck, TextBox valueControl, string? value)
    {
        overrideCheck.Checked = !string.IsNullOrWhiteSpace(value);
        valueControl.Text = value ?? string.Empty;
        valueControl.Enabled = overrideCheck.Checked;
        if (ReferenceEquals(valueControl, _overrideFontNameBox))
        {
            _overrideFontNameButton.Enabled = overrideCheck.Checked;
        }
    }

    private void SetNullableCombo(CheckBox overrideCheck, ComboBox valueControl, string? value, string fallback)
    {
        overrideCheck.Checked = !string.IsNullOrWhiteSpace(value);
        valueControl.Text = value ?? fallback;
        valueControl.Enabled = overrideCheck.Checked;
    }

    private void InvalidatePreviewDots()
    {
        _profileList.Invalidate();
        _headerDot.Invalidate();
    }

    private Button CreateButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = width == 0,
            Width = width == 0 ? 0 : width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = _theme.Border;
        button.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        button.FlatAppearance.MouseDownBackColor = _theme.ButtonHoverBackground;
        return button;
    }

    private CheckBox CreateCheckBox(string text) => new()
    {
        AutoSize = true,
        Text = text,
        ForeColor = _theme.Text,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 6, 0, 0)
    };

    private Label CreateSectionLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font(Font, FontStyle.Bold),
        ForeColor = _theme.Text,
        BackColor = Color.Transparent,
        Margin = new Padding(0, 0, 0, 8)
    };

    private Label CreateMutedLabel() => new()
    {
        AutoSize = true,
        ForeColor = _theme.Muted,
        BackColor = Color.Transparent
    };

    private TextBox CreateTextBox(bool multiline = false)
    {
        return new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Multiline = multiline,
            AcceptsReturn = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            Dock = DockStyle.Fill
        };
    }

    private ComboBox CreateComboBox()
    {
        return new ComboBox
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240
        };
    }

    private NumericUpDown CreateNumeric(decimal minimum, decimal maximum, decimal increment, int decimalPlaces)
    {
        return new NumericUpDown
        {
            Minimum = minimum,
            Maximum = maximum,
            Increment = increment,
            DecimalPlaces = decimalPlaces,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Width = 100
        };
    }

    private Panel WrapControl(Control control, Padding? padding = null)
    {
        var wrapper = FlatUiHelper.WrapFlat(control, _theme, padding ?? new Padding(4));
        wrapper.BackColor = _theme.EditorBackground;
        return wrapper;
    }

    private Control CreateFieldPanel(out TextBox textBox, bool multiline = false, int height = 30)
    {
        textBox = CreateTextBox(multiline);
        var wrapper = WrapControl(textBox, new Padding(6, multiline ? 6 : 4, 6, 4));
        wrapper.Height = height;
        wrapper.Width = 340;
        return wrapper;
    }

    private TabPage CreateTabPage(string title) => new()
    {
        Text = title,
        BackColor = _theme.Background,
        ForeColor = _theme.Text
    };

    private Panel CreateTabContentPanel() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        BackColor = _theme.Background,
        Padding = new Padding(18)
    };

    private TableLayoutPanel CreateFormLayout()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2
        };
    }

    private void AddRow(TableLayoutPanel layout, string labelText, Control control)
    {
        var rowIndex = layout.RowCount;
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (layout.ColumnStyles.Count == 0)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        var label = new Label
        {
            AutoSize = true,
            Text = labelText,
            ForeColor = _theme.Muted,
            Margin = new Padding(0, 8, 12, 0)
        };

        control.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(label, 0, rowIndex);
        layout.Controls.Add(control, 1, rowIndex);
    }

    private void AddCommandRow(TableLayoutPanel layout, string labelText, out TextBox textBox, out Button resetButton)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 2
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var textHost = CreateFieldPanel(out textBox);
        textHost.Dock = DockStyle.Fill;
        resetButton = CreateButton("Reset to default", 126);
        row.Controls.Add(textHost, 0, 0);
        row.Controls.Add(resetButton, 1, 0);

        AddRow(layout, labelText, row);
    }

    private Panel CreateOverrideColumn(string title)
    {
        var container = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 12, 0),
            BackColor = Color.Transparent
        };

        var header = CreateSectionLabel(title);
        header.Dock = DockStyle.Top;
        container.Controls.Add(header);

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        container.Controls.Add(stack);
        container.Tag = stack;
        return container;
    }

    private FlowLayoutPanel GetOverrideStack(Panel panel) => (FlowLayoutPanel)panel.Tag!;

    private void AddBoolOverride(Panel container, string labelText, out CheckBox overrideCheck, out CheckBox valueCheck)
    {
        var row = CreateOverrideRow(container, labelText, out overrideCheck);
        valueCheck = CreateCheckBox("Enabled");
        valueCheck.Margin = new Padding(0, 6, 0, 0);
        valueCheck.Enabled = false;
        row.Controls.Add(valueCheck);
    }

    private void AddNumericOverride(Panel container, string labelText, decimal minimum, decimal maximum, decimal increment, out CheckBox overrideCheck, out NumericUpDown valueControl)
    {
        var row = CreateOverrideRow(container, labelText, out overrideCheck);
        valueControl = CreateNumeric(minimum, maximum, increment, 0);
        valueControl.Enabled = false;
        row.Controls.Add(valueControl);
    }

    private void AddDecimalOverride(Panel container, string labelText, decimal minimum, decimal maximum, decimal increment, int decimalPlaces, out CheckBox overrideCheck, out NumericUpDown valueControl)
    {
        var row = CreateOverrideRow(container, labelText, out overrideCheck);
        valueControl = CreateNumeric(minimum, maximum, increment, decimalPlaces);
        valueControl.Enabled = false;
        row.Controls.Add(valueControl);
    }

    private void AddTextOverride(Panel container, string labelText, out CheckBox overrideCheck, out TextBox textBox, out Button actionButton, bool chooseButton)
    {
        var row = CreateOverrideRow(container, labelText, out overrideCheck);
        var inputHost = CreateFieldPanel(out textBox);
        inputHost.Width = chooseButton ? 170 : 230;
        inputHost.Height = 28;
        textBox.Enabled = false;
        row.Controls.Add(inputHost);

        if (chooseButton)
        {
            actionButton = CreateButton("Choose", 78);
            actionButton.Enabled = false;
            actionButton.Margin = new Padding(8, 0, 0, 0);
            row.Controls.Add(actionButton);
        }
        else
        {
            actionButton = CreateButton(string.Empty, 0);
            actionButton.Visible = false;
        }
    }

    private void AddComboOverride(Panel container, string labelText, IEnumerable<string> items, out CheckBox overrideCheck, out ComboBox comboBox)
    {
        var row = CreateOverrideRow(container, labelText, out overrideCheck);
        comboBox = CreateComboBox();
        comboBox.Enabled = false;
        comboBox.Width = 190;
        foreach (var item in items)
        {
            comboBox.Items.Add(item);
        }
        row.Controls.Add(comboBox);
    }

    private FlowLayoutPanel CreateOverrideRow(Panel container, string labelText, out CheckBox overrideCheck)
    {
        var stack = GetOverrideStack(container);
        var rowContainer = new Panel
        {
            Width = 340,
            Height = 54,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = _theme.PanelBackground,
            Padding = new Padding(10, 8, 10, 8)
        };
        rowContainer.Paint += (s, e) => DrawBorder(e.Graphics, ((Control)s!).ClientRectangle);

        var title = new Label
        {
            AutoSize = true,
            Text = labelText,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = _theme.Text,
            Location = new Point(10, 8)
        };

        overrideCheck = CreateCheckBox("Override");
        overrideCheck.Location = new Point(10, 26);
        overrideCheck.Margin = Padding.Empty;

        var valueRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Location = new Point(102, 22),
            BackColor = Color.Transparent
        };

        rowContainer.Controls.Add(title);
        rowContainer.Controls.Add(overrideCheck);
        rowContainer.Controls.Add(valueRow);
        stack.Controls.Add(rowContainer);
        return valueRow;
    }

    private void DrawBorder(Graphics graphics, Rectangle bounds)
    {
        var rect = bounds;
        rect.Width -= 1;
        rect.Height -= 1;
        using var pen = new Pen(_theme.Border);
        graphics.DrawRectangle(pen, rect);
    }
}

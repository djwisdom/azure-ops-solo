using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public record KeyBinding(
    string Command,
    string Key,
    string When = "",
    bool IsCustom = false
);

internal sealed class SettingsDialog : Form
{
    private readonly Form1 _mainForm;
    private Theme _theme;

    // UI Components
    private TextBox _searchBox = null!;
    private TreeView _settingsTree = null!;
    private CustomSplitter _customSplitter = null!;
    private Panel _treePanel = null!;
    private Panel _contentPanel = null!;
    private Button _okButton = null!;
    private Button _cancelButton = null!;

    // Custom title bar
    private Panel _titleBarPanel = null!;
    private Label _titleLabel = null!;
    private Button _minimizeButton = null!;
    private Button _maximizeButton = null!;
    private Button _closeButton = null!;
    private bool _isDragging = false;

    // Preview Components
    private TextBox _fontPreviewText = null!;
    private Panel _themePreviewPanel = null!;
    private TextBox _tabPreviewText = null!;

    // Setting Controls (organized by category)
    private Dictionary<string, Control> _settingControls = new();

    // Current values storage
    private Dictionary<string, object> _originalValues = new();
    private Dictionary<string, object> _currentValues = new();

    // Settings scopes
    public enum SettingScope { Default, User, Workspace, Profile }
    private Dictionary<string, SettingScope> _settingScopes = new();

    // Splitter initialization flag
    private bool _splitterInitialized = false;

    // Keyboard shortcuts data
    private Dictionary<string, KeyBinding> _keyBindings = new();
    private ListView _shortcutsListView = null!;
    private Button _recordShortcutButton = null!;
    private Label _recordingLabel = null!;
    private bool _isRecordingShortcut = false;

    // Workspace settings data
    private Dictionary<string, object> _workspaceSettings = new();
    private TextBox _workspacePathText = null!;
    private ListView _workspaceSettingsList = null!;
    private Button _saveWorkspaceSettingsButton = null!;

    public SettingsDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _theme = ThemeManager.Instance.CurrentTheme;
        Text = "Settings";
        Size = new Size(900, 700);
        MinimumSize = new Size(650, 500); // Increased minimum width to accommodate split container
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        Font = new Font("Segoe UI", 9);
        AutoScroll = false; // Prevent Windows from adding form-level scrollbars

        InitializeForm();
        LoadCurrentValues();
        BuildSettingsTree();
        ShowCategory("editor");
    }

    private void InitializeForm()
    {
        // Create custom title bar
        CreateTitleBar();

        // Search box below title bar
        int yOffset = 45; // Title bar height + spacing

        _searchBox = new TextBox
        {
            Location = new Point(12, yOffset),
            Size = new Size(200, 25),
            PlaceholderText = "Search settings...",
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        _searchBox.TextChanged += SearchBox_TextChanged;
        Controls.Add(_searchBox);

        // Custom splitter for reliable layout control
        yOffset += 40;
        _customSplitter = new CustomSplitter(_theme);
        _customSplitter.Location = new Point(0, yOffset);
        _customSplitter.Size = new Size(Width, Height - yOffset);
        _customSplitter.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _customSplitter.SplitterMoved += (s, e) => UpdateLayout();
        Controls.Add(_customSplitter);

        // Tree panel on left
        _treePanel = new Panel
        {
            BackColor = _theme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle
        };
        _customSplitter.SetLeftPanel(_treePanel);

        _settingsTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None,
            ShowLines = false,
            FullRowSelect = true,
            ItemHeight = 24
        };
        _settingsTree.AfterSelect += SettingsTree_AfterSelect;
        _treePanel.Controls.Add(_settingsTree);

        // Content panel on right
        _contentPanel = new ThemeAwareScrollablePanel(_theme)
        {
            BackColor = _theme.Background,
            Padding = new Padding(16),
            BorderStyle = BorderStyle.FixedSingle
        };
        _customSplitter.SetRightPanel(_contentPanel);

        // Buttons at bottom
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = _theme.Background,
            Padding = new Padding(16, 8, 16, 8)
        };

        var importButton = new Button
        {
            Text = "Import",
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border },
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(16, 8)
        };
        importButton.Click += ImportButton_Click;

        var exportButton = new Button
        {
            Text = "Export",
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border },
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(104, 8)
        };
        exportButton.Click += ExportButton_Click;

        _okButton = new Button
        {
            Text = "OK",
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent },
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(buttonPanel.Width - 176, 8)
        };
        _okButton.Click += OkButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border },
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(buttonPanel.Width - 88, 8)
        };
        _cancelButton.Click += (s, e) => Close();

        buttonPanel.Controls.Add(importButton);
        buttonPanel.Controls.Add(exportButton);
        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(_cancelButton);
        Controls.Add(buttonPanel);

        UpdateLayout();
    }

    private void BuildSettingsTree()
    {
        _settingsTree.Nodes.Clear();

        // Editor category
        var editorNode = new TreeNode("Editor") { Tag = "editor" };
        editorNode.Nodes.AddRange(new[]
        {
            new TreeNode("Text Editor") { Tag = "editor.text" },
            new TreeNode("Cursor") { Tag = "editor.cursor" },
            new TreeNode("Find") { Tag = "editor.find" },
            new TreeNode("Font") { Tag = "editor.font" },
            new TreeNode("Formatting") { Tag = "editor.formatting" }
        });

        // Workbench category
        var workbenchNode = new TreeNode("Workbench") { Tag = "workbench" };
        workbenchNode.Nodes.AddRange(new[]
        {
            new TreeNode("Appearance") { Tag = "workbench.appearance" },
            new TreeNode("Editor Management") { Tag = "workbench.editor" }
        });

        // Features category
        var featuresNode = new TreeNode("Features") { Tag = "features" };
        featuresNode.Nodes.AddRange(new[]
        {
            new TreeNode("Terminal") { Tag = "features.terminal" },
            new TreeNode("Git") { Tag = "features.git" },
            new TreeNode("Keyboard Shortcuts") { Tag = "features.keyboard" },
            new TreeNode("Extensions") { Tag = "features.extensions" }
        });

        // Application category
        var applicationNode = new TreeNode("Application") { Tag = "application" };
        applicationNode.Nodes.AddRange(new[]
        {
            new TreeNode("Window") { Tag = "application.window" },
            new TreeNode("Workspace") { Tag = "application.workspace" },
            new TreeNode("Security") { Tag = "application.security" }
        });

        _settingsTree.Nodes.AddRange(new[] { editorNode, workbenchNode, featuresNode, applicationNode });
        _settingsTree.ExpandAll();
    }



    private void SettingsTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is string category)
        {
            ShowCategory(category);
        }
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        string query = _searchBox.Text.ToLower();
        if (string.IsNullOrEmpty(query))
        {
            BuildSettingsTree();
            return;
        }

        // Implement search filtering
        FilterSettingsTree(query);
    }

    private void FilterSettingsTree(string query)
    {
        _settingsTree.Nodes.Clear();

        // Search through all settings and show matching categories
        var matchingNodes = new List<TreeNode>();

        foreach (var category in GetAllCategories())
        {
            var settings = GetSettingsForCategory(category);
            var matchingSettings = settings.Where(s =>
                s.Key.ToLower().Contains(query) ||
                GetSettingDescription(s.Key).ToLower().Contains(query)).ToList();

            if (matchingSettings.Any())
            {
                var categoryNode = new TreeNode(GetCategoryDisplayName(category)) { Tag = category };
                matchingNodes.Add(categoryNode);
            }
        }

        _settingsTree.Nodes.AddRange(matchingNodes.ToArray());
        _settingsTree.ExpandAll();

        // Show search results if we have matches
        if (matchingNodes.Any())
        {
            ShowSearchResults(query);
        }
    }

    private void ShowCategory(string category)
    {
        _contentPanel.Controls.Clear();
        int y = 0;

        // Add category header
        y = AddCategoryHeader(_contentPanel, y, GetCategoryDisplayName(category));

        // Add settings for this category
        if (category == "features.keyboard")
        {
            y = AddKeyboardShortcutsUI(_contentPanel, y);
        }
        else if (category == "application.workspace")
        {
            y = AddWorkspaceSettingsUI(_contentPanel, y);
        }
        else if (category == "features.extensions")
        {
            y = AddExtensionsSettingsUI(_contentPanel, y);
        }
        else if (category == "features.git")
        {
            y = AddGitSettingsUI(_contentPanel, y);
        }
        else if (category == "application.security")
        {
            y = AddSecuritySettingsUI(_contentPanel, y);
        }
        else
        {
            var settings = GetSettingsForCategory(category);
            foreach (var setting in settings)
            {
                y = AddSettingRow(_contentPanel, y, setting.Key, setting.Value);
            }
        }

        _contentPanel.AutoScrollMinSize = new Size(0, y);
    }

    private void ShowSearchResults(string query)
    {
        _contentPanel.Controls.Clear();
        int y = 0;

        // Header
        var header = new Label
        {
            Text = $"Search results for \"{query}\"",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        _contentPanel.Controls.Add(header);
        y += 40;

        // Find all matching settings
        var allSettings = GetAllSettings();
        var matchingSettings = allSettings.Where(s =>
            s.Key.ToLower().Contains(query.ToLower()) ||
            GetSettingDescription(s.Key).ToLower().Contains(query.ToLower())).ToList();

        foreach (var setting in matchingSettings)
        {
            y = AddSettingRow(_contentPanel, y, setting.Key, setting.Value);
        }

        _contentPanel.AutoScrollMinSize = new Size(0, y);
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        ApplyCurrentSettings();
        Close();
    }

    private void ImportButton_Click(object? sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Import Settings",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string json = File.ReadAllText(openFileDialog.FileName);
                var importedSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (importedSettings != null)
                {
                    // Merge imported settings with current values
                    foreach (var kvp in importedSettings)
                    {
                        if (_currentValues.ContainsKey(kvp.Key))
                        {
                            _currentValues[kvp.Key] = kvp.Value;
                        }
                    }

                    ThemedMessageBox.Show("Settings imported successfully!", "Import Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to import settings: {ex.Message}", "Import Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void CreateTitleBar()
    {
        // Title bar panel
        _titleBarPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(Width, 35),
            Dock = DockStyle.Top,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text
        };
        _titleBarPanel.Paint += TitleBarPanel_Paint;
        _titleBarPanel.MouseDown += TitleBarPanel_MouseDown;
        _titleBarPanel.MouseMove += TitleBarPanel_MouseMove;
        _titleBarPanel.MouseUp += TitleBarPanel_MouseUp;
        Controls.Add(_titleBarPanel);

        // Title label
        _titleLabel = new Label
        {
            Text = "Settings",
            Location = new Point(10, 8),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        _titleBarPanel.Controls.Add(_titleLabel);

        // Minimize button
        _minimizeButton = CreateTitleBarButton("─", (s, e) => WindowState = FormWindowState.Minimized);
        _minimizeButton.Location = new Point(Width - 120, 0);

        // Maximize button
        _maximizeButton = CreateTitleBarButton("□", (s, e) => ToggleMaximize());
        _maximizeButton.Location = new Point(Width - 80, 0);

        // Close button
        _closeButton = CreateTitleBarButton("✕", (s, e) => Close());
        _closeButton.Location = new Point(Width - 40, 0);

        // Add buttons to title bar
        _titleBarPanel.Controls.Add(_minimizeButton);
        _titleBarPanel.Controls.Add(_maximizeButton);
        _titleBarPanel.Controls.Add(_closeButton);
    }

    private Button CreateTitleBarButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(40, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderSize = 0 },
            TabStop = false,
            Font = new Font("Segoe UI", 10)
        };
        button.Click += onClick;
        button.MouseEnter += (s, e) => button.BackColor = _theme.Accent;
        button.MouseLeave += (s, e) => button.BackColor = Color.Transparent;
        return button;
    }

    private void TitleBarPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw title bar background
        using var bgBrush = new SolidBrush(_theme.PanelBackground);
        g.FillRectangle(bgBrush, _titleBarPanel.ClientRectangle);

        // Draw bottom border
        using var borderPen = new Pen(_theme.Border);
        g.DrawLine(borderPen, 0, _titleBarPanel.Height - 1, _titleBarPanel.Width, _titleBarPanel.Height - 1);
    }

    private Point _dragStartPoint;

    private void TitleBarPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.Location;
        }
    }

    private void TitleBarPanel_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Point offset = new Point(e.X - _dragStartPoint.X, e.Y - _dragStartPoint.Y);
            Location = new Point(Left + offset.X, Top + offset.Y);
        }
    }

    private void TitleBarPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Normal ? FormWindowState.Maximized : FormWindowState.Normal;
    }

    private void UpdateTitleBarTheme()
    {
        if (_titleBarPanel != null)
        {
            _titleBarPanel.BackColor = _theme.PanelBackground;
            _titleLabel.ForeColor = _theme.Text;
            if (_minimizeButton != null) _minimizeButton.ForeColor = _theme.Text;
            if (_maximizeButton != null) _maximizeButton.ForeColor = _theme.Text;
            if (_closeButton != null) _closeButton.ForeColor = _theme.Text;
            _titleBarPanel.Invalidate();
        }
    }

    private void ExportButton_Click(object? sender, EventArgs e)
    {
        using var saveFileDialog = new SaveFileDialog
        {
            Title = "Export Settings",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "pfpad_settings.json"
        };

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                // Export current settings (not including unmodified defaults)
                var settingsToExport = new Dictionary<string, object>();
                foreach (var kvp in _currentValues)
                {
                    if (_originalValues.TryGetValue(kvp.Key, out var original) && !Equals(kvp.Value, original))
                    {
                        settingsToExport[kvp.Key] = kvp.Value;
                    }
                }

                string json = System.Text.Json.JsonSerializer.Serialize(settingsToExport,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(saveFileDialog.FileName, json);

                ThemedMessageBox.Show("Settings exported successfully!", "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to export settings: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ApplyCurrentSettings()
    {
        var settings = new AppSettings
        {
            FontName = GetSettingValue<string>("editor.font.name", _mainForm.CurrentFontName),
            FontSize = GetSettingValue<float>("editor.font.size", _mainForm.CurrentFontSize),
            TabSize = GetSettingValue<int>("editor.formatting.tabSize", _mainForm.CurrentTabSize),
            InsertSpaces = GetSettingValue<bool>("editor.formatting.insertSpaces", _mainForm.CurrentInsertSpaces),
            WordWrapEnabled = GetSettingValue<bool>("editor.text.wordWrap", _mainForm.CurrentWordWrap),
            ShowGuide = GetSettingValue<bool>("editor.text.columnGuide", _mainForm.CurrentShowGuide),
            GuideColumn = GetSettingValue<int>("editor.text.guideColumn", _mainForm.CurrentGuideColumn),
            ThemeName = GetSettingValue<string>("workbench.appearance.theme", _mainForm.CurrentThemeName),
            GutterVisible = GetSettingValue<bool>("editor.text.gutter", _mainForm.CurrentGutterVisible),
            StatusBarVisible = GetSettingValue<bool>("workbench.appearance.statusBar", _mainForm.CurrentStatusBarVisible),
            MinimapVisible = GetSettingValue<bool>("editor.appearance.minimap", _mainForm.CurrentMinimapVisible),
            ShowWhitespace = GetSettingValue<bool>("editor.appearance.showWhitespace", _mainForm.CurrentShowWhitespace),
            CurrentLineHighlightMode = GetSettingValue<string>("editor.cursor.lineHighlight", _mainForm.CurrentLineHighlightName) switch
            {
                "NumberOnly" => MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.NumberOnly,
                "WholeLine" => MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.WholeLine,
                "NumberAndWholeLine" => MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.NumberAndWholeLine,
                _ => MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.Off
            },
            SyntaxHighlightingEnabled = GetSettingValue<bool>("editor.text.syntaxHighlighting", _mainForm.CurrentSyntaxHighlighting),
            AutoIndentEnabled = GetSettingValue<bool>("editor.formatting.autoIndent", _mainForm.CurrentAutoIndent),
            SmartTabsEnabled = GetSettingValue<bool>("editor.formatting.smartTabs", _mainForm.CurrentSmartTabs),
            ElasticTabsEnabled = GetSettingValue<bool>("editor.formatting.elasticTabs", _mainForm.CurrentElasticTabs),
            RainbowBracketsEnabled = GetSettingValue<bool>("editor.appearance.rainbowBrackets", _mainForm.CurrentRainbowBrackets),
            BreadcrumbsEnabled = GetSettingValue<bool>("editor.appearance.breadcrumbs", _mainForm.CurrentBreadcrumbs),
            HoverLineHighlightEnabled = GetSettingValue<bool>("editor.cursor.hoverLineHighlight", _mainForm.CurrentHoverLineHighlight),
            AutoSaveEnabled = GetSettingValue<bool>("features.behavior.autoSave", _mainForm.CurrentAutoSave),
            WorkspaceVisible = true,
            SymbolPanelVisible = false,
            ProblemsPanelVisible = false,
            TerminalVisible = true,
            TerminalHeight = 200,
            AnalyzersEnabled = GetSettingValue<bool>("features.behavior.analyzers", _mainForm.CurrentAnalyzersEnabled),
            TerminalShellPath = GetSettingValue<string>("features.terminal.shell", _mainForm.CurrentTerminalShell),
            TerminalStartingDirectory = GetSettingValue<string>("features.terminal.startingDirectory", _mainForm.CurrentTerminalStartingDirectory),
            TerminalFontFace = GetSettingValue<string>("features.terminal.fontFace", _mainForm.CurrentTerminalFontFace),
            TerminalFontSize = GetSettingValue<float>("features.terminal.fontSize", _mainForm.CurrentTerminalFontSize),
            TerminalFontBold = GetSettingValue<bool>("features.terminal.fontWeight", _mainForm.CurrentTerminalFontBold),
            TerminalWordWrap = GetSettingValue<bool>("features.terminal.wordWrap", _mainForm.CurrentTerminalWordWrap),
            TerminalScrollbarVisible = GetSettingValue<bool>("features.terminal.scrollbar", _mainForm.CurrentTerminalScrollbarVisible),
            TerminalPadding = GetSettingValue<int>("features.terminal.padding", _mainForm.CurrentTerminalPadding),
            TerminalMaxScrollback = GetSettingValue<int>("features.terminal.maxScrollback", _mainForm.CurrentTerminalMaxScrollback),
            TerminalTabTitle = GetSettingValue<string>("features.terminal.tabTitle", _mainForm.CurrentTerminalTabTitle),
            GitAuthorName = GetSettingValue<string>("features.git.authorName", _mainForm.CurrentGitAuthorName),
            GitAuthorEmail = GetSettingValue<string>("features.git.authorEmail", _mainForm.CurrentGitAuthorEmail),
            GitDefaultBranch = GetSettingValue<string>("features.git.defaultBranch", _mainForm.CurrentGitDefaultBranch),
            GitConfirmRemoveRepo = GetSettingValue<bool>("features.git.confirmRemoveRepo", _mainForm.CurrentGitConfirmRemoveRepo),
            GitConfirmDiscardChanges = GetSettingValue<bool>("features.git.confirmDiscardChanges", _mainForm.CurrentGitConfirmDiscardChanges),
            GitConfirmDiscardPermanent = GetSettingValue<bool>("features.git.confirmDiscardPermanent", _mainForm.CurrentGitConfirmDiscardPermanent),
            GitConfirmDiscardStash = GetSettingValue<bool>("features.git.confirmDiscardStash", _mainForm.CurrentGitConfirmDiscardStash),
            GitConfirmCheckoutCommit = GetSettingValue<bool>("features.git.confirmCheckoutCommit", _mainForm.CurrentGitConfirmCheckoutCommit),
            GitConfirmForcePush = GetSettingValue<bool>("features.git.confirmForcePush", _mainForm.CurrentGitConfirmForcePush),
            GitConfirmUndoCommit = GetSettingValue<bool>("features.git.confirmUndoCommit", _mainForm.CurrentGitConfirmUndoCommit),
            GitConfirmOverrideCommitMsg = GetSettingValue<bool>("features.git.confirmOverrideCommitMsg", _mainForm.CurrentGitConfirmOverrideCommitMsg),
            GitConfirmHiddenChanges = GetSettingValue<bool>("features.git.confirmHiddenChanges", _mainForm.CurrentGitConfirmHiddenChanges),
            GitBranchSwitchBehavior = GetSettingValue<string>("features.git.branchSwitchBehavior", _mainForm.CurrentGitBranchSwitchBehavior),
            GitCommitLengthWarning = GetSettingValue<bool>("features.git.commitLengthWarning", _mainForm.CurrentGitCommitLengthWarning),
            SecPromptUntrustedWorkspace = GetSettingValue<bool>("application.security.promptUntrustedWorkspace", _mainForm.CurrentSecPromptUntrustedWorkspace),
            SecTrustedWorkspacePaths = GetSettingValue<string>("application.security.trustedWorkspacePaths", _mainForm.CurrentSecTrustedWorkspacePaths),
            SecConfirmUrlOpen = GetSettingValue<bool>("application.security.confirmUrlOpen", _mainForm.CurrentSecConfirmUrlOpen),
            SecAllowHttpUrls = GetSettingValue<bool>("application.security.allowHttpUrls", _mainForm.CurrentSecAllowHttpUrls),
            SecConfirmExternalTools = GetSettingValue<bool>("application.security.confirmExternalTools", _mainForm.CurrentSecConfirmExternalTools),
            SecConfirmDebugStart = GetSettingValue<bool>("application.security.confirmDebugStart", _mainForm.CurrentSecConfirmDebugStart),
            SecDebugAdapterPathOnly = GetSettingValue<bool>("application.security.debugAdapterPathOnly", _mainForm.CurrentSecDebugAdapterPathOnly),
            SecConfirmGitClone = GetSettingValue<bool>("application.security.confirmGitClone", _mainForm.CurrentSecConfirmGitClone),
            SecTrustedGitHosts = GetSettingValue<string>("application.security.trustedGitHosts", _mainForm.CurrentSecTrustedGitHosts),
            SecSastEnabled = GetSettingValue<bool>("application.security.sastEnabled", _mainForm.CurrentSecSastEnabled),
            SecSastSeverityThreshold = GetSettingValue<string>("application.security.sastSeverityThreshold", _mainForm.CurrentSecSastSeverityThreshold),
            SecWriteCrashLog = GetSettingValue<bool>("application.security.writeCrashLog", _mainForm.CurrentSecWriteCrashLog),
            SecWriteStartupLog = GetSettingValue<bool>("application.security.writeStartupLog", _mainForm.CurrentSecWriteStartupLog),
            SecLogRetentionDays = GetSettingValue<int>("application.security.logRetentionDays", _mainForm.CurrentSecLogRetentionDays),
            SecHighlightHardcodedSecrets = GetSettingValue<bool>("application.security.highlightSecrets", _mainForm.CurrentSecHighlightHardcodedSecrets),
            VimModeEnabled = GetSettingValue<bool>("features.behavior.vimMode", _mainForm.CurrentVimMode),
            StickyScrollEnabled = GetSettingValue<bool>("editor.appearance.stickyScroll", _mainForm.CurrentStickyScroll),
        };
        _mainForm.ApplySettings(settings);
    }

    private T GetSettingValue<T>(string key, T defaultValue)
    {
        return _currentValues.TryGetValue(key, out var value) && value is T typedValue ? typedValue : defaultValue;
    }

    private void LoadCurrentValues()
    {
        _currentValues = new Dictionary<string, object>
        {
            // Editor - Text
            ["editor.text.wordWrap"] = _mainForm.CurrentWordWrap,
            ["editor.text.syntaxHighlighting"] = _mainForm.CurrentSyntaxHighlighting,
            ["editor.text.columnGuide"] = _mainForm.CurrentShowGuide,
            ["editor.text.guideColumn"] = _mainForm.CurrentGuideColumn,
            ["editor.text.gutter"] = _mainForm.CurrentGutterVisible,
            ["editor.text.statusBar"] = _mainForm.CurrentStatusBarVisible,

            // Editor - Cursor
            ["editor.cursor.lineHighlight"] = _mainForm.CurrentLineHighlightName,
            ["editor.cursor.hoverLineHighlight"] = _mainForm.CurrentHoverLineHighlight,

            // Editor - Font
            ["editor.font.name"] = _mainForm.CurrentFontName,
            ["editor.font.size"] = _mainForm.CurrentFontSize,

            // Editor - Formatting
            ["editor.formatting.insertSpaces"] = _mainForm.CurrentInsertSpaces,
            ["editor.formatting.tabSize"] = _mainForm.CurrentTabSize,
            ["editor.formatting.autoIndent"] = _mainForm.CurrentAutoIndent,
            ["editor.formatting.smartTabs"] = _mainForm.CurrentSmartTabs,
            ["editor.formatting.elasticTabs"] = _mainForm.CurrentElasticTabs,

            // Editor - Appearance
            ["editor.appearance.showWhitespace"] = _mainForm.CurrentShowWhitespace,
            ["editor.appearance.minimap"] = _mainForm.CurrentMinimapVisible,
            ["editor.appearance.stickyScroll"] = _mainForm.CurrentStickyScroll,
            ["editor.appearance.rainbowBrackets"] = _mainForm.CurrentRainbowBrackets,
            ["editor.appearance.breadcrumbs"] = _mainForm.CurrentBreadcrumbs,

            // Workbench - Appearance
            ["workbench.appearance.theme"] = _mainForm.CurrentThemeName,

            // Features - Behavior
            ["features.behavior.vimMode"] = _mainForm.CurrentVimMode,
            ["features.behavior.autoSave"] = _mainForm.CurrentAutoSave,
            ["features.behavior.analyzers"] = _mainForm.CurrentAnalyzersEnabled,

            // Features - Terminal
            ["features.terminal.shell"] = _mainForm.CurrentTerminalShell,
            ["features.terminal.startingDirectory"] = _mainForm.CurrentTerminalStartingDirectory,
            ["features.terminal.fontFace"] = _mainForm.CurrentTerminalFontFace,
            ["features.terminal.fontSize"] = _mainForm.CurrentTerminalFontSize,
            ["features.terminal.fontWeight"] = _mainForm.CurrentTerminalFontBold,
            ["features.terminal.wordWrap"] = _mainForm.CurrentTerminalWordWrap,
            ["features.terminal.scrollbar"] = _mainForm.CurrentTerminalScrollbarVisible,
            ["features.terminal.padding"] = _mainForm.CurrentTerminalPadding,
            ["features.terminal.maxScrollback"] = _mainForm.CurrentTerminalMaxScrollback,
            ["features.terminal.tabTitle"] = _mainForm.CurrentTerminalTabTitle,

            // Features - Git
            ["features.git.authorName"] = _mainForm.CurrentGitAuthorName,
            ["features.git.authorEmail"] = _mainForm.CurrentGitAuthorEmail,
            ["features.git.defaultBranch"] = _mainForm.CurrentGitDefaultBranch,
            ["features.git.confirmRemoveRepo"] = _mainForm.CurrentGitConfirmRemoveRepo,
            ["features.git.confirmDiscardChanges"] = _mainForm.CurrentGitConfirmDiscardChanges,
            ["features.git.confirmDiscardPermanent"] = _mainForm.CurrentGitConfirmDiscardPermanent,
            ["features.git.confirmDiscardStash"] = _mainForm.CurrentGitConfirmDiscardStash,
            ["features.git.confirmCheckoutCommit"] = _mainForm.CurrentGitConfirmCheckoutCommit,
            ["features.git.confirmForcePush"] = _mainForm.CurrentGitConfirmForcePush,
            ["features.git.confirmUndoCommit"] = _mainForm.CurrentGitConfirmUndoCommit,
            ["features.git.confirmOverrideCommitMsg"] = _mainForm.CurrentGitConfirmOverrideCommitMsg,
            ["features.git.confirmHiddenChanges"] = _mainForm.CurrentGitConfirmHiddenChanges,
            ["features.git.branchSwitchBehavior"] = _mainForm.CurrentGitBranchSwitchBehavior,
            ["features.git.commitLengthWarning"] = _mainForm.CurrentGitCommitLengthWarning,

            // Application Security
            ["application.security.promptUntrustedWorkspace"] = _mainForm.CurrentSecPromptUntrustedWorkspace,
            ["application.security.trustedWorkspacePaths"] = _mainForm.CurrentSecTrustedWorkspacePaths,
            ["application.security.confirmUrlOpen"] = _mainForm.CurrentSecConfirmUrlOpen,
            ["application.security.allowHttpUrls"] = _mainForm.CurrentSecAllowHttpUrls,
            ["application.security.confirmExternalTools"] = _mainForm.CurrentSecConfirmExternalTools,
            ["application.security.confirmDebugStart"] = _mainForm.CurrentSecConfirmDebugStart,
            ["application.security.debugAdapterPathOnly"] = _mainForm.CurrentSecDebugAdapterPathOnly,
            ["application.security.confirmGitClone"] = _mainForm.CurrentSecConfirmGitClone,
            ["application.security.trustedGitHosts"] = _mainForm.CurrentSecTrustedGitHosts,
            ["application.security.sastEnabled"] = _mainForm.CurrentSecSastEnabled,
            ["application.security.sastSeverityThreshold"] = _mainForm.CurrentSecSastSeverityThreshold,
            ["application.security.writeCrashLog"] = _mainForm.CurrentSecWriteCrashLog,
            ["application.security.writeStartupLog"] = _mainForm.CurrentSecWriteStartupLog,
            ["application.security.logRetentionDays"] = _mainForm.CurrentSecLogRetentionDays,
            ["application.security.highlightSecrets"] = _mainForm.CurrentSecHighlightHardcodedSecrets
        };

        _originalValues = new Dictionary<string, object>(_currentValues);

        // Initialize setting scopes (simplified - in real implementation, this would check workspace settings files)
        InitializeSettingScopes();
    }

    private void InitializeSettingScopes()
    {
        _settingScopes = new Dictionary<string, SettingScope>();

        // Most settings are user-level by default
        foreach (var key in _currentValues.Keys)
        {
            _settingScopes[key] = SettingScope.User;
        }

        // Profile overrides - Note: In a full implementation, Form1 would expose current profile
        // For now, we'll show all settings as User scope
        // TODO: Add public CurrentProfile property to Form1 or pass profile info to SettingsDialog

        // Workspace overrides would be loaded from .pfpad/settings.json
        // For now, we'll assume no workspace overrides
    }

    private void UpdateLayout()
    {
        if (_okButton != null && _cancelButton != null && _okButton.Parent != null)
        {
            var panel = _okButton.Parent;
            _okButton.Location = new Point(panel.Width - 176, 8);
            _cancelButton.Location = new Point(panel.Width - 88, 8);
        }
    }

    private void SetInitialSplitterDistance()
    {
        if (_splitterInitialized || _customSplitter == null || _customSplitter.Width <= 0) return;

        try
        {
            // Calculate safe splitter position based on form size
            int minDistance = 200; // Left panel minimum
            int maxDistance = _customSplitter.Width - 400 - 4; // Right panel minimum + splitter width

            // Target 35% of width for tree panel, but respect min/max constraints
            int targetDistance = Math.Max(minDistance, (int)(_customSplitter.Width * 0.35));

            int safeDistance = Math.Max(minDistance, Math.Min(maxDistance, targetDistance));

            // Set the splitter position
            _customSplitter.SplitterPosition = safeDistance;
            _splitterInitialized = true;
        }
        catch (Exception)
        {
            // Fallback to minimum safe distance
            try
            {
                _customSplitter.SplitterPosition = 200;
                _splitterInitialized = true;
            }
            catch
            {
                // If all else fails, ignore the error
            }
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        // Set initial splitter position after layout is complete
        if (!_splitterInitialized && _customSplitter != null)
        {
            SetInitialSplitterDistance();
        }
    }

    private void UpdateSplitterDistance()
    {
        // Custom splitter handles its own bounds checking
    }

    private IEnumerable<string> GetAllCategories()
    {
        return new[]
        {
            "editor.text", "editor.cursor", "editor.find", "editor.font", "editor.formatting",
            "editor.appearance", "workbench.appearance", "workbench.editor",
            "features.terminal", "features.git", "features.extensions",
            "features.behavior", "application.window", "application.security"
        };
    }

    private string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            "editor" => "Editor",
            "editor.text" => "Text Editor",
            "editor.cursor" => "Cursor",
            "editor.find" => "Find",
            "editor.font" => "Font",
            "editor.formatting" => "Formatting",
            "editor.appearance" => "Appearance",
            "workbench" => "Workbench",
            "workbench.appearance" => "Appearance",
            "workbench.editor" => "Editor Management",
            "features" => "Features",
            "features.terminal" => "Terminal",
            "features.git" => "Git",
            "features.extensions" => "Extensions",
            "features.behavior" => "Behavior",
            "application" => "Application",
            "application.window" => "Window",
            "application.security" => "Security",
            _ => category
        };
    }

    private Dictionary<string, object> GetSettingsForCategory(string category)
    {
        var allSettings = GetAllSettings();
        return allSettings.Where(kvp => kvp.Key.StartsWith(category)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private Dictionary<string, object> GetAllSettings()
    {
        return new Dictionary<string, object>(_currentValues);
    }

    private string GetSettingDescription(string key)
    {
        return key switch
        {
            "editor.text.wordWrap" => "Controls whether long lines wrap to the next line or show horizontal scrollbar.",
            "editor.text.syntaxHighlighting" => "Controls whether syntax highlighting is enabled for supported languages.",
            "editor.text.columnGuide" => "Show a vertical line at a specific column to help align text.",
            "editor.text.guideColumn" => "Column number where the vertical guide line is displayed.",
            "editor.text.gutter" => "Show line numbers and other indicators in the left margin.",
            "editor.text.statusBar" => "Show the status bar at the bottom of the editor window.",
            "editor.cursor.lineHighlight" => "Highlight the current line where the cursor is positioned.",
            "editor.cursor.hoverLineHighlight" => "Highlight lines when hovering over them with the mouse.",
            "editor.font.name" => "Font family used in the editor.",
            "editor.font.size" => "Font size in pixels.",
            "editor.formatting.insertSpaces" => "Insert spaces instead of tabs when pressing Tab.",
            "editor.formatting.tabSize" => "Number of spaces a tab represents.",
            "editor.formatting.autoIndent" => "Automatically indent new lines based on context.",
            "editor.formatting.smartTabs" => "Use smart tab behavior for better indentation.",
            "editor.formatting.elasticTabs" => "Adapt tab size based on context.",
            "editor.appearance.showWhitespace" => "Show whitespace characters (spaces, tabs) in the editor.",
            "editor.appearance.minimap" => "Show a miniature overview of the file on the right side.",
            "editor.appearance.stickyScroll" => "Keep the current scope visible at the top when scrolling.",
            "editor.appearance.rainbowBrackets" => "Color-code matching brackets with different colors.",
            "editor.appearance.breadcrumbs" => "Show navigation breadcrumbs at the top of the editor.",
            "workbench.appearance.theme" => "Color theme for the entire application.",
            "features.behavior.vimMode" => "Enable Vim-style keyboard navigation and editing.",
            "features.behavior.autoSave" => "Automatically save files after 30 seconds of inactivity.",
            "features.behavior.analyzers" => "Enable Roslyn analyzers for code analysis and suggestions.",
            "features.terminal.shell" => "Shell to use in the integrated terminal.",
            "features.terminal.startingDirectory" => "Starting directory for the shell. Empty = workspace folder (or current directory).",
            "features.terminal.fontFace" => "Font for the terminal (empty = auto: Cascadia Code → Cascadia Mono → Consolas → Courier New).",
            "features.terminal.fontSize" => "Terminal font size in pt. 0 = use default (10pt).",
            "features.terminal.fontWeight" => "Use bold font weight in the terminal.",
            "features.terminal.wordWrap" => "Wrap long lines in the terminal output.",
            "features.terminal.scrollbar" => "Show the scrollbar in the terminal output panel.",
            "features.terminal.padding" => "Padding (px) inside the terminal output area. 0–20.",
            "features.terminal.maxScrollback" => "Maximum lines to keep in scrollback. 500–50000.",
            "features.terminal.tabTitle" => "Custom tab title for terminal tabs. Empty = \"Terminal N\".",
            "features.git.authorName" => "Overrides git config user.name for commits made in Pfpad.",
            "features.git.authorEmail" => "Overrides git config user.email for commits made in Pfpad.",
            "features.git.defaultBranch" => "Branch name used when initialising a new repository.",
            _ => "No description available."
        };
    }

    private int AddGitSettingsUI(Panel parent, int y)
    {
        y = AddGitSectionHeader(parent, y, "Accounts");

        foreach (var (provider, url) in new[]
        {
            ("GitHub.com", "https://github.com/login"),
            ("GitHub Enterprise", "https://github.com/enterprise"),
            ("GitLab", "https://gitlab.com/users/sign_in"),
            ("Azure DevOps", "https://dev.azure.com/")
        })
        {
            y = AddAccountRow(parent, y, provider, url);
        }

        y += 16;
        y = AddGitSectionHeader(parent, y, "Git");

        y = AddGitTextRow(parent, y, "Author Name",
            "Overrides git config user.name for commits made in Pfpad. Leave empty to use global git config.",
            "features.git.authorName");

        y = AddGitTextRow(parent, y, "Author Email",
            "Overrides git config user.email for commits made in Pfpad. Leave empty to use global git config.",
            "features.git.authorEmail");

        y = AddGitRadioRow(parent, y, "Default Branch",
            "Branch name used when initialising a new repository.",
            "features.git.defaultBranch",
            new[] { ("main", "main"), ("master", "master") });

        y += 16;
        y = AddGitSectionHeader(parent, y, "Prompts");

        var promptLabel = new Label
        {
            Text = "Show a confirmation dialog before...",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(promptLabel);
        y += 22;

        var confirmChecks = new[]
        {
            ("Removing repositories", "features.git.confirmRemoveRepo"),
            ("Discarding changes", "features.git.confirmDiscardChanges"),
            ("Discarding changes permanently", "features.git.confirmDiscardPermanent"),
            ("Discarding stash", "features.git.confirmDiscardStash"),
            ("Checking out a commit", "features.git.confirmCheckoutCommit"),
            ("Force pushing", "features.git.confirmForcePush"),
            ("Undo commit", "features.git.confirmUndoCommit"),
            ("Overriding commit message with generated message", "features.git.confirmOverrideCommitMsg"),
            ("Committing changes hidden by filter", "features.git.confirmHiddenChanges"),
        };

        foreach (var (label, key) in confirmChecks)
        {
            y = AddGitCheckRow(parent, y, label, key);
        }

        y += 16;

        var switchLabel = new Label
        {
            Text = "If I have changes and I switch branches...",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(switchLabel);
        y += 22;

        var switchOptions = new[]
        {
            ("ask", "Ask me where I want the changes to go"),
            ("bring", "Always bring my changes to my new branch"),
            ("stash", "Always stash and leave my changes on the current branch"),
        };

        string currentSwitch = _currentValues.TryGetValue("features.git.branchSwitchBehavior", out var sv)
            ? sv.ToString()! : "ask";

        foreach (var (value, text) in switchOptions)
        {
            var rb = new RadioButton
            {
                Text = text,
                Location = new Point(16, y),
                AutoSize = true,
                Checked = currentSwitch == value,
                ForeColor = _theme.Text,
                BackColor = Color.Transparent,
                Tag = (ValueTuple<string, string>)("features.git.branchSwitchBehavior", value)
            };
            rb.CheckedChanged += (s, e) =>
            {
                if (rb.Checked && rb.Tag is ValueTuple<string, string> t)
                {
                    _currentValues[t.Item1] = t.Item2;
                }
            };
            parent.Controls.Add(rb);
            y += 22;
        }

        y += 16;
        y = AddGitSectionHeader(parent, y, "Commit Length");

        y = AddGitCheckRow(parent, y, "Show commit length warning (≤50 subject line is recommended)", "features.git.commitLengthWarning");

        y += 16;
        return y;
    }

    private int AddGitSectionHeader(Panel parent, int y, string title)
    {
        var lbl = new Label
        {
            Text = title,
            Location = new Point(0, y),
            Size = new Size(parent.Width, 24),
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        lbl.Paint += (s, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, 0, lbl.Height - 1, lbl.Width, lbl.Height - 1);
        };
        parent.Controls.Add(lbl);
        return y + 30;
    }

    private int AddAccountRow(Panel parent, int y, string providerName, string authUrl)
    {
        var nameLbl = new Label
        {
            Text = providerName,
            Location = new Point(0, y + 4),
            AutoSize = true,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(nameLbl);

        var statusLbl = new Label
        {
            Text = "Not connected",
            Location = new Point(180, y + 4),
            AutoSize = true,
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(statusLbl);

        var signInBtn = new Button
        {
            Text = "Sign In",
            Location = new Point(parent.Width - 80, y),
            Size = new Size(72, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 8.5f)
        };
        signInBtn.FlatAppearance.BorderColor = _theme.Border;
        signInBtn.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authUrl) { UseShellExecute = true });
        parent.Controls.Add(signInBtn);

        return y + 32;
    }

    private int AddGitTextRow(Panel parent, int y, string displayName, string description, string key)
    {
        var lbl = new Label
        {
            Text = displayName,
            Location = new Point(0, y + 4),
            Size = new Size(160, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);

        var descLbl = new Label
        {
            Text = description,
            Location = new Point(0, y + 22),
            Size = new Size(parent.Width - 280, 32),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(descLbl);

        string current = _currentValues.TryGetValue(key, out var v) ? v.ToString()! : "";
        var txt = new TextBox
        {
            Text = current,
            Location = new Point(parent.Width - 260, y + 4),
            Size = new Size(252, 22),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = key
        };
        txt.TextChanged += (s, e) => _currentValues[key] = txt.Text;
        parent.Controls.Add(txt);

        return y + 58;
    }

    private int AddGitRadioRow(Panel parent, int y, string displayName, string description, string key, (string value, string label)[] options)
    {
        var lbl = new Label
        {
            Text = displayName,
            Location = new Point(0, y + 4),
            Size = new Size(160, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);

        var descLbl = new Label
        {
            Text = description,
            Location = new Point(0, y + 22),
            Size = new Size(parent.Width - 280, 32),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(descLbl);

        string current = _currentValues.TryGetValue(key, out var v) ? v.ToString()! : options[0].value;

        int rx = parent.Width - 260;
        foreach (var (value, label) in options)
        {
            var rb = new RadioButton
            {
                Text = label,
                Location = new Point(rx, y + 2),
                AutoSize = true,
                Checked = current == value,
                ForeColor = _theme.Text,
                BackColor = Color.Transparent,
                Tag = (ValueTuple<string, string>)(key, value)
            };
            rb.CheckedChanged += (s, e) =>
            {
                if (rb.Checked && rb.Tag is ValueTuple<string, string> t)
                {
                    _currentValues[t.Item1] = t.Item2;
                }
            };
            parent.Controls.Add(rb);
            rx += rb.Width + 16;
        }

        return y + 58;
    }

    private int AddGitCheckRow(Panel parent, int y, string label, string key)
    {
        bool current = _currentValues.TryGetValue(key, out var v) && v is bool b && b;
        var cb = new CheckBox
        {
            Text = label,
            Location = new Point(16, y),
            AutoSize = true,
            Checked = current,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Tag = key
        };
        cb.CheckedChanged += (s, e) => _currentValues[key] = cb.Checked;
        parent.Controls.Add(cb);
        return y + 24;
    }

    private int AddSecuritySettingsUI(Panel parent, int y)
    {
        y = AddSecSectionHeader(parent, y, "Workspace Trust");

        y = AddSecCheckRow(parent, y,
            "Prompt when opening an untrusted folder",
            "Warn before loading build files (MSBuild/Roslyn) from an unknown directory.",
            "application.security.promptUntrustedWorkspace");

        y = AddSecMultilineTextRow(parent, y,
            "Trusted workspace paths",
            "One path per line. These folders are always opened without prompting.",
            "application.security.trustedWorkspacePaths",
            height: 60);

        y += 8;
        y = AddSecSectionHeader(parent, y, "Terminal");

        y = AddSecCheckRow(parent, y,
            "Confirm before opening links in browser",
            "Ask for confirmation when you click a URL in the terminal output.",
            "application.security.confirmUrlOpen");

        y = AddSecCheckRow(parent, y,
            "Allow http:// links (not just https://)",
            "When disabled, http:// links in terminal output are blocked.",
            "application.security.allowHttpUrls");

        y += 8;
        y = AddSecSectionHeader(parent, y, "External Tools");

        y = AddSecCheckRow(parent, y,
            "Confirm before running an external tool",
            "Show tool name and command before executing Tools menu items.",
            "application.security.confirmExternalTools");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Debugger");

        y = AddSecCheckRow(parent, y,
            "Confirm before starting a debug session",
            "Ask for confirmation each time F5 / Start Debug is pressed.",
            "application.security.confirmDebugStart");

        y = AddSecCheckRow(parent, y,
            "Only load debug adapter from PATH",
            "Prevent loading a debug adapter from a workspace-relative or untrusted path.",
            "application.security.debugAdapterPathOnly");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Git");

        y = AddSecCheckRow(parent, y,
            "Confirm cloning from unknown hosts",
            "Warn before cloning from a host not on the trusted list below.",
            "application.security.confirmGitClone");

        y = AddSecTextRow(parent, y,
            "Trusted Git hosts",
            "Comma-separated list. Clone from these hosts without prompting.",
            "application.security.trustedGitHosts",
            width: 340);

        y += 8;
        y = AddSecSectionHeader(parent, y, "Code Scanning (SAST)");

        y = AddSecCheckRow(parent, y,
            "Enable SAST scanning",
            "Run static analysis rules (CWE-78 command injection, CWE-89 SQL injection, CWE-312 secrets, etc.) while editing.",
            "application.security.sastEnabled");

        y = AddSecCheckRow(parent, y,
            "Highlight hardcoded credentials",
            "Flag API keys, passwords and tokens detected in source code.",
            "application.security.highlightSecrets");

        var threshLbl = new Label
        {
            Text = "Minimum severity to report",
            Location = new Point(0, y + 4),
            Size = new Size(220, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(threshLbl);

        string currentThresh = _currentValues.TryGetValue("application.security.sastSeverityThreshold", out var tv)
            ? tv.ToString()! : "Medium";
        var threshCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Location = new Point(parent.Width - 174, y + 2),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            FlatStyle = FlatStyle.Flat,
            Tag = "application.security.sastSeverityThreshold"
        };
        threshCombo.Items.AddRange(new[] { "Low", "Medium", "High", "Critical" });
        threshCombo.Text = currentThresh;
        threshCombo.SelectedIndexChanged += (s, e) =>
            _currentValues["application.security.sastSeverityThreshold"] = threshCombo.Text;
        parent.Controls.Add(threshCombo);
        y += 32;

        y += 8;
        y = AddSecSectionHeader(parent, y, "Logging & Crash Reports");

        y = AddSecCheckRow(parent, y,
            "Write crash log on fatal error",
            $"Save crash details to %LocalAppData%\\MyCrownJewelApp\\Pfpad\\crash.log. Takes effect on next launch.",
            "application.security.writeCrashLog");

        y = AddSecCheckRow(parent, y,
            "Write startup log",
            "Record startup and lifecycle events to startup.log. Disable on shared machines. Takes effect on next launch.",
            "application.security.writeStartupLog");

        var retLbl = new Label
        {
            Text = "Log retention (days)",
            Location = new Point(0, y + 4),
            Size = new Size(220, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(retLbl);

        int currentRet = _currentValues.TryGetValue("application.security.logRetentionDays", out var rv) && rv is int ri ? ri : 30;
        var retUpDown = new NumericUpDown
        {
            Width = 70,
            Minimum = 1,
            Maximum = 365,
            Increment = 7,
            Value = Math.Clamp(currentRet, 1, 365),
            Location = new Point(parent.Width - 124, y),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Tag = "application.security.logRetentionDays"
        };
        retUpDown.ValueChanged += (s, e) =>
            _currentValues["application.security.logRetentionDays"] = decimal.ToInt32(retUpDown.Value);
        parent.Controls.Add(retUpDown);
        y += 32;

        y += 8;
        var dpapiLbl = new Label
        {
            Text = "Connector credential encryption",
            Location = new Point(0, y + 4),
            Size = new Size(260, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(dpapiLbl);
        var dpapiStatus = new Label
        {
            Text = "Protected by Windows DPAPI (user account)",
            Location = new Point(270, y + 4),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 200, 120),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(dpapiStatus);
        y += 28;

        return y + 16;
    }

    private int AddSecSectionHeader(Panel parent, int y, string title)
    {
        var lbl = new Label
        {
            Text = title,
            Location = new Point(0, y),
            Size = new Size(parent.Width, 24),
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        lbl.Paint += (s, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, 0, lbl.Height - 1, lbl.Width, lbl.Height - 1);
        };
        parent.Controls.Add(lbl);
        return y + 30;
    }

    private int AddSecCheckRow(Panel parent, int y, string labelText, string description, string key)
    {
        bool current = _currentValues.TryGetValue(key, out var v) && v is bool b && b;
        var cb = new CheckBox
        {
            Text = labelText,
            Location = new Point(0, y),
            AutoSize = true,
            Checked = current,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9),
            Tag = key
        };
        cb.CheckedChanged += (s, e) => _currentValues[key] = cb.Checked;
        parent.Controls.Add(cb);

        var desc = new Label
        {
            Text = description,
            Location = new Point(20, y + 20),
            Size = new Size(parent.Width - 24, 28),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(desc);
        return y + 50;
    }

    private int AddSecTextRow(Panel parent, int y, string labelText, string description, string key, int width = 260)
    {
        var lbl = new Label
        {
            Text = labelText,
            Location = new Point(0, y + 4),
            Size = new Size(200, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);

        var desc = new Label
        {
            Text = description,
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - width - 20, 20),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(desc);

        string current = _currentValues.TryGetValue(key, out var v) ? v.ToString()! : "";
        var txt = new TextBox
        {
            Text = current,
            Location = new Point(parent.Width - width - 8, y + 4),
            Size = new Size(width, 22),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = key
        };
        txt.TextChanged += (s, e) => _currentValues[key] = txt.Text;
        parent.Controls.Add(txt);
        return y + 52;
    }

    private int AddSecMultilineTextRow(Panel parent, int y, string labelText, string description, string key, int height = 60)
    {
        var lbl = new Label
        {
            Text = labelText,
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);

        var desc = new Label
        {
            Text = description,
            Location = new Point(0, y + 20),
            Size = new Size(parent.Width, 16),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(desc);

        string current = _currentValues.TryGetValue(key, out var v) ? v.ToString()! : "";
        var txt = new TextBox
        {
            Text = current.Replace("\n", Environment.NewLine),
            Location = new Point(0, y + 38),
            Size = new Size(parent.Width - 8, height),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = key
        };
        txt.TextChanged += (s, e) => _currentValues[key] = txt.Text.Replace(Environment.NewLine, "\n");
        parent.Controls.Add(txt);
        return y + 38 + height + 8;
    }

    private int AddCategoryHeader(Panel parent, int y, string title)
    {
        var lbl = new Label
        {
            Text = title,
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 32),
            ForeColor = _theme.Accent,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };
        parent.Controls.Add(lbl);
        y += 40;

        var line = new Label
        {
            Location = new Point(0, y - 1),
            Size = new Size(parent.Width - 32, 1),
            BackColor = _theme.Border
        };
        parent.Controls.Add(line);
        y += 16;

        return y;
    }

    private int AddSettingRow(Panel parent, int y, string key, object value)
    {
        int baseHeight = 70;
        bool hasPreview = HasPreview(key);

        // Setting label and scope indicator
        var scope = _settingScopes.TryGetValue(key, out var s) ? s : SettingScope.User;
        var scopeText = GetScopeText(scope);
        var scopeColor = GetScopeColor(scope);

        var label = new Label
        {
            Text = GetSettingDisplayName(key),
            Location = new Point(0, y + 6),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        parent.Controls.Add(label);

        // Scope indicator
        var scopeLabel = new Label
        {
            Text = scopeText,
            Location = new Point(label.Right + 8, y + 8),
            AutoSize = true,
            ForeColor = scopeColor,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 7, FontStyle.Bold)
        };
        parent.Controls.Add(scopeLabel);

        // Description
        var desc = new Label
        {
            Text = GetSettingDescription(key),
            Location = new Point(0, y + 28),
            Size = new Size(parent.Width - 120, 40),
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8)
        };
        parent.Controls.Add(desc);

        // Control based on setting type
        Control control = CreateControlForSetting(key, value);
        // Leave 44px gap at the right for the reset button (24px wide + 8px gap + 12px margin)
        control.Location = new Point(parent.Width - control.Width - 48, y + 4);
        parent.Controls.Add(control);

        // Reset button
        var resetBtn = new Button
        {
            Text = "⟲",
            Size = new Size(24, 24),
            Location = new Point(parent.Width - 40, y + 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border },
            Font = new Font("Segoe UI", 8)
        };
        resetBtn.Click += (s, e) => ResetSetting(key);
        parent.Controls.Add(resetBtn);

        // Add preview component if applicable
        if (hasPreview)
        {
            Control previewControl = CreatePreviewControl(key);
            previewControl.Location = new Point(0, y + 70);
            previewControl.Size = new Size(parent.Width - 32, 80);
            parent.Controls.Add(previewControl);
            baseHeight += 90;
        }

        return y + baseHeight;
    }

    private bool HasPreview(string key)
    {
        return key switch
        {
            "editor.font.name" or "editor.font.size" => true,
            "workbench.appearance.theme" => true,
            "editor.formatting.tabSize" => true,
            _ => false
        };
    }

    private Control CreatePreviewControl(string key)
    {
        return key switch
        {
            "editor.font.name" or "editor.font.size" => CreateFontPreview(),
            "workbench.appearance.theme" => CreateThemePreview(),
            "editor.formatting.tabSize" => CreateTabPreview(),
            _ => new Panel() // Fallback
        };
    }

    private int AddKeyboardShortcutsUI(Panel parent, int y)
    {
        // Header
        var header = new Label
        {
            Text = "Keyboard Shortcuts",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(header);
        y += 40;

        // Recording status label
        _recordingLabel = new Label
        {
            Text = "",
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 30),
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9),
            Visible = false
        };
        parent.Controls.Add(_recordingLabel);
        y += 35;

        // ListView for shortcuts
        _shortcutsListView = new ThemeAwareListView(_theme)
        {
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 300),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        _shortcutsListView.Columns.Add("Command", 200);
        _shortcutsListView.Columns.Add("Key", 150);
        _shortcutsListView.Columns.Add("When", 100);
        _shortcutsListView.Columns.Add("Source", 80);

        LoadDefaultKeyBindings();
        PopulateShortcutsList();

        parent.Controls.Add(_shortcutsListView);
        y += 310;

        // Buttons
        var recordBtn = new Button
        {
            Text = "Record Shortcut",
            Location = new Point(0, y),
            Size = new Size(120, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent }
        };
        recordBtn.Click += RecordShortcutButton_Click;
        parent.Controls.Add(recordBtn);

        var resetBtn = new Button
        {
            Text = "Reset to Defaults",
            Location = new Point(130, y),
            Size = new Size(120, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        resetBtn.Click += ResetShortcutsButton_Click;
        parent.Controls.Add(resetBtn);

        y += 40;

        return y;
    }

    private void LoadDefaultKeyBindings()
    {
        // Load default keybindings (simplified set)
        _keyBindings = new Dictionary<string, KeyBinding>
        {
            ["editor.action.copy"] = new KeyBinding("Copy", "Ctrl+C"),
            ["editor.action.paste"] = new KeyBinding("Paste", "Ctrl+V"),
            ["editor.action.cut"] = new KeyBinding("Cut", "Ctrl+X"),
            ["editor.action.selectAll"] = new KeyBinding("Select All", "Ctrl+A"),
            ["editor.action.find"] = new KeyBinding("Find", "Ctrl+F"),
            ["editor.action.replace"] = new KeyBinding("Replace", "Ctrl+H"),
            ["editor.action.save"] = new KeyBinding("Save", "Ctrl+S"),
            ["editor.action.saveAll"] = new KeyBinding("Save All", "Ctrl+K, S"),
            ["editor.action.undo"] = new KeyBinding("Undo", "Ctrl+Z"),
            ["editor.action.redo"] = new KeyBinding("Redo", "Ctrl+Y"),
            ["workbench.action.newFile"] = new KeyBinding("New File", "Ctrl+N"),
            ["workbench.action.openFile"] = new KeyBinding("Open File", "Ctrl+O"),
            ["workbench.action.closeEditor"] = new KeyBinding("Close Editor", "Ctrl+W"),
            ["workbench.action.toggleTerminal"] = new KeyBinding("Toggle Terminal", "Ctrl+`"),
            ["editor.action.commentLine"] = new KeyBinding("Toggle Line Comment", "Ctrl+/"),
            ["editor.action.formatDocument"] = new KeyBinding("Format Document", "Shift+Alt+F"),
            ["editor.action.goToDefinition"] = new KeyBinding("Go to Definition", "F12"),
            ["editor.action.peekDefinition"] = new KeyBinding("Peek Definition", "Alt+F12"),
            ["editor.action.rename"] = new KeyBinding("Rename Symbol", "F2"),
            ["workbench.action.showCommandPalette"] = new KeyBinding("Show Command Palette", "Ctrl+Shift+P")
        };
    }

    private void PopulateShortcutsList()
    {
        if (_shortcutsListView == null) return;

        _shortcutsListView.Items.Clear();
        foreach (var binding in _keyBindings.Values.OrderBy(b => b.Command))
        {
            var item = new ListViewItem(binding.Command);
            item.SubItems.Add(binding.Key);
            item.SubItems.Add(binding.When);
            item.SubItems.Add(binding.IsCustom ? "User" : "Default");
            item.Tag = binding;
            _shortcutsListView.Items.Add(item);
        }
    }

    private void RecordShortcutButton_Click(object? sender, EventArgs e)
    {
        if (_isRecordingShortcut)
        {
            StopRecordingShortcut();
        }
        else
        {
            StartRecordingShortcut();
        }
    }

    private void StartRecordingShortcut()
    {
        if (_shortcutsListView.SelectedItems.Count == 0)
        {
            ThemedMessageBox.Show("Please select a command first.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _isRecordingShortcut = true;
        _recordShortcutButton.Text = "Stop Recording";
        _recordingLabel.Text = "Recording shortcut... Press the key combination you want to assign.";
        _recordingLabel.Visible = true;
        _recordingLabel.ForeColor = _theme.Accent;

        // Focus the form to capture key events
        Focus();
        KeyPreview = true;
        KeyDown += SettingsDialog_KeyDown;
    }

    private void StopRecordingShortcut()
    {
        _isRecordingShortcut = false;
        _recordShortcutButton.Text = "Record Shortcut";
        _recordingLabel.Visible = false;
        KeyPreview = false;
        KeyDown -= SettingsDialog_KeyDown;
    }

    private void SettingsDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecordingShortcut) return;

        string keyString = BuildKeyString(e);
        if (!string.IsNullOrEmpty(keyString))
        {
            AssignShortcutToSelectedCommand(keyString);
            StopRecordingShortcut();
        }
        e.Handled = true;
    }

    private string BuildKeyString(KeyEventArgs e)
    {
        var parts = new List<string>();

        if (e.Control) parts.Add("Ctrl");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");

        if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Alt && e.KeyCode != Keys.ShiftKey)
        {
            parts.Add(e.KeyCode.ToString());
        }

        return parts.Count > 1 ? string.Join("+", parts) : "";
    }

    private void AssignShortcutToSelectedCommand(string keyString)
    {
        if (_shortcutsListView.SelectedItems.Count == 0) return;

        var selectedItem = _shortcutsListView.SelectedItems[0];
        if (selectedItem.Tag is KeyBinding binding)
        {
            // Check for conflicts
            var conflictingBinding = _keyBindings.Values.FirstOrDefault(b => b.Key == keyString && b != binding);
            if (conflictingBinding != null)
            {
                var result = ThemedMessageBox.Show(
                    $"This shortcut is already assigned to '{conflictingBinding.Command}'. Replace it?",
                    "Shortcut Conflict",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No) return;

                // Remove the old binding
                _keyBindings.Remove(conflictingBinding.Command);
            }

            // Update the binding
            var newBinding = binding with { Key = keyString, IsCustom = true };
            _keyBindings[binding.Command] = newBinding;

            // Update the list
            PopulateShortcutsList();

            _recordingLabel.Text = $"Shortcut assigned: {keyString}";
            _recordingLabel.ForeColor = Color.Green;
            _recordingLabel.Visible = true;

            // Hide the message after 2 seconds
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 2000 };
            timer.Tick += (s, e) => { _recordingLabel.Visible = false; timer.Stop(); };
            timer.Start();
        }
    }

    private void ResetShortcutsButton_Click(object? sender, EventArgs e)
    {
        var result = ThemedMessageBox.Show(
            "This will reset all custom shortcuts to their defaults. Continue?",
            "Reset Shortcuts",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            LoadDefaultKeyBindings();
            PopulateShortcutsList();
        }
    }

    private int AddWorkspaceSettingsUI(Panel parent, int y)
    {
        // Header
        var header = new Label
        {
            Text = "Workspace Settings",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(header);
        y += 40;

        // Workspace path
        var pathLabel = new Label
        {
            Text = "Workspace Root:",
            Location = new Point(0, y + 3),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent
        };
        parent.Controls.Add(pathLabel);

        _workspacePathText = new TextBox
        {
            Location = new Point(120, y),
            Size = new Size(parent.Width - 152, 25),
            Text = _mainForm.WorkspaceRoot ?? "",
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None,
            ReadOnly = true
        };
        parent.Controls.Add(_workspacePathText);

        var browseBtn = new Button
        {
            Text = "Browse...",
            Location = new Point(parent.Width - 32 - 80, y - 1),
            Size = new Size(80, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        browseBtn.Click += BrowseWorkspaceButton_Click;
        parent.Controls.Add(browseBtn);
        y += 35;

        // Settings file status
        var statusLabel = new Label
        {
            Text = GetWorkspaceSettingsStatus(),
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8)
        };
        parent.Controls.Add(statusLabel);
        y += 25;

        // Workspace settings list
        var settingsLabel = new Label
        {
            Text = "Workspace-Specific Settings:",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        parent.Controls.Add(settingsLabel);
        y += 25;

        _workspaceSettingsList = new ThemeAwareListView(_theme)
        {
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 200),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        _workspaceSettingsList.Columns.Add("Setting", 200);
        _workspaceSettingsList.Columns.Add("Value", 150);
        _workspaceSettingsList.Columns.Add("Description", 200);

        LoadWorkspaceSettings();
        PopulateWorkspaceSettingsList();

        parent.Controls.Add(_workspaceSettingsList);
        y += 210;

        // Buttons
        var addBtn = new Button
        {
            Text = "Add Setting",
            Location = new Point(0, y),
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        addBtn.Click += AddWorkspaceSettingButton_Click;
        parent.Controls.Add(addBtn);

        var removeBtn = new Button
        {
            Text = "Remove",
            Location = new Point(110, y),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        removeBtn.Click += RemoveWorkspaceSettingButton_Click;
        parent.Controls.Add(removeBtn);

        _saveWorkspaceSettingsButton = new Button
        {
            Text = "Save to Workspace",
            Location = new Point(parent.Width - 32 - 140, y),
            Size = new Size(140, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent }
        };
        _saveWorkspaceSettingsButton.Click += SaveWorkspaceSettingsButton_Click;
        parent.Controls.Add(_saveWorkspaceSettingsButton);

        y += 40;

        return y;
    }

    private string GetWorkspaceSettingsStatus()
    {
        string workspaceRoot = _mainForm.WorkspaceRoot ?? "";
        if (string.IsNullOrEmpty(workspaceRoot))
        {
            return "No workspace opened. Workspace settings apply to the current project folder.";
        }

        string settingsPath = Path.Combine(workspaceRoot, ".pfpad", "settings.json");
        if (File.Exists(settingsPath))
        {
            return $"Workspace settings file: {settingsPath}";
        }
        else
        {
            return $"No workspace settings file found. Will be created at: {settingsPath}";
        }
    }

    private void LoadWorkspaceSettings()
    {
        _workspaceSettings.Clear();
        string workspaceRoot = _mainForm.WorkspaceRoot ?? "";

        if (!string.IsNullOrEmpty(workspaceRoot))
        {
            string settingsPath = Path.Combine(workspaceRoot, ".pfpad", "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    if (settings != null)
                    {
                        _workspaceSettings = settings;
                    }
                }
                catch
                {
                    // Ignore load errors
                }
            }
        }
    }

    private void PopulateWorkspaceSettingsList()
    {
        if (_workspaceSettingsList == null) return;

        _workspaceSettingsList.Items.Clear();
        foreach (var kvp in _workspaceSettings.OrderBy(k => k.Key))
        {
            var item = new ListViewItem(kvp.Key);
            item.SubItems.Add(kvp.Value?.ToString() ?? "");
            item.SubItems.Add(GetSettingDescription(kvp.Key));
            item.Tag = kvp.Key;
            _workspaceSettingsList.Items.Add(item);
        }
    }

    private void BrowseWorkspaceButton_Click(object? sender, EventArgs e)
    {
        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Select Workspace Root Folder",
            SelectedPath = _mainForm.WorkspaceRoot ?? ""
        };

        if (folderDialog.ShowDialog() == DialogResult.OK)
        {
            _workspacePathText.Text = folderDialog.SelectedPath;
            _mainForm.OpenWorkspaceFolder(folderDialog.SelectedPath);
            LoadWorkspaceSettings();
            PopulateWorkspaceSettingsList();
        }
    }

    private void AddWorkspaceSettingButton_Click(object? sender, EventArgs e)
    {
        using var addDialog = new AddWorkspaceSettingDialog(_theme);
        if (addDialog.ShowDialog() == DialogResult.OK)
        {
            _workspaceSettings[addDialog.SettingKey] = addDialog.SettingValue;
            PopulateWorkspaceSettingsList();
            _saveWorkspaceSettingsButton.Enabled = true;
        }
    }

    private void RemoveWorkspaceSettingButton_Click(object? sender, EventArgs e)
    {
        if (_workspaceSettingsList.SelectedItems.Count == 0) return;

        var selectedItem = _workspaceSettingsList.SelectedItems[0];
        if (selectedItem.Tag is string key)
        {
            _workspaceSettings.Remove(key);
            PopulateWorkspaceSettingsList();
            _saveWorkspaceSettingsButton.Enabled = true;
        }
    }

    private void SaveWorkspaceSettingsButton_Click(object? sender, EventArgs e)
    {
        string workspaceRoot = _workspacePathText.Text;
        if (string.IsNullOrEmpty(workspaceRoot))
        {
            ThemedMessageBox.Show("No workspace selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            string pfpadDir = Path.Combine(workspaceRoot, ".pfpad");
            Directory.CreateDirectory(pfpadDir);

            string settingsPath = Path.Combine(pfpadDir, "settings.json");
            string json = System.Text.Json.JsonSerializer.Serialize(_workspaceSettings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);

            _saveWorkspaceSettingsButton.Enabled = false;
            ThemedMessageBox.Show("Workspace settings saved successfully!", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Failed to save workspace settings: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private int AddExtensionsSettingsUI(Panel parent, int y)
    {
        // Header
        var header = new Label
        {
            Text = "Extensions",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Accent,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(header);
        y += 40;

        // Status message
        var statusLabel = new Label
        {
            Text = "Extension settings allow you to configure installed plugins and add-ons.",
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 40),
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8)
        };
        parent.Controls.Add(statusLabel);
        y += 50;

        // Installed extensions list
        var extensionsLabel = new Label
        {
            Text = "Installed Extensions:",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        parent.Controls.Add(extensionsLabel);
        y += 25;

        var extensionsList = new ThemeAwareListView(_theme)
        {
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 200),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        extensionsList.Columns.Add("Extension", 150);
        extensionsList.Columns.Add("Version", 80);
        extensionsList.Columns.Add("Description", 250);
        extensionsList.Columns.Add("Settings", 80);

        // Add some sample extensions (in a real implementation, this would come from a plugin system)
        var sampleExtensions = new[]
        {
            new { Name = "C# Language Support", Version = "1.0.0", Description = "Provides C# syntax highlighting and IntelliSense", HasSettings = true },
            new { Name = "Git Integration", Version = "1.2.0", Description = "Git source control integration", HasSettings = true },
            new { Name = "Terminal", Version = "1.1.0", Description = "Integrated terminal support", HasSettings = false },
            new { Name = "File Explorer", Version = "1.0.5", Description = "File and folder navigation", HasSettings = false }
        };

        foreach (var ext in sampleExtensions)
        {
            var item = new ListViewItem(ext.Name);
            item.SubItems.Add(ext.Version);
            item.SubItems.Add(ext.Description);
            item.SubItems.Add(ext.HasSettings ? "Available" : "None");
            extensionsList.Items.Add(item);
        }

        parent.Controls.Add(extensionsList);
        y += 210;

        // Extension settings area
        var extSettingsLabel = new Label
        {
            Text = "Extension Settings:",
            Location = new Point(0, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        parent.Controls.Add(extSettingsLabel);
        y += 25;

        var extSettingsPanel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(parent.Width - 32, 150),
            BackColor = _theme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle
        };

        var placeholderLabel = new Label
        {
            Text = "Select an extension from the list above to view its settings.\n\nExtension settings will appear here in a future update.",
            Location = new Point(20, 20),
            Size = new Size(extSettingsPanel.Width - 40, 100),
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        extSettingsPanel.Controls.Add(placeholderLabel);

        parent.Controls.Add(extSettingsPanel);
        y += 160;

        // Buttons
        var installBtn = new Button
        {
            Text = "Install Extension",
            Location = new Point(0, y),
            Size = new Size(120, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        installBtn.Click += (s, e) => ThemedMessageBox.Show("Extension marketplace coming soon!", "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        parent.Controls.Add(installBtn);

        var manageBtn = new Button
        {
            Text = "Manage Extensions",
            Location = new Point(130, y),
            Size = new Size(130, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        manageBtn.Click += (s, e) => ThemedMessageBox.Show("Extension manager coming soon!", "Coming Soon", MessageBoxButtons.OK, MessageBoxIcon.Information);
        parent.Controls.Add(manageBtn);

        y += 40;

        return y;
    }

    private Control CreateFontPreview()
    {
        _fontPreviewText = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Font = new Font("Consolas", 12f),
            Text = "The quick brown fox jumps over the lazy dog\r\n0123456789\r\nfunction Main() { return true; }"
        };
        UpdateFontPreview();
        return _fontPreviewText;
    }

    private Control CreateThemePreview()
    {
        _themePreviewPanel = new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = _theme.Background
        };

        // Add some sample UI elements to show theme
        var titleBar = new Label
        {
            Text = "Window Title",
            Location = new Point(5, 5),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = _theme.Background
        };
        _themePreviewPanel.Controls.Add(titleBar);

        var button = new Button
        {
            Text = "Button",
            Location = new Point(5, 25),
            Size = new Size(60, 25),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        _themePreviewPanel.Controls.Add(button);

        var editor = new TextBox
        {
            Text = "Sample code...",
            Location = new Point(5, 55),
            Size = new Size(150, 20),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        _themePreviewPanel.Controls.Add(editor);

        UpdateThemePreview();
        return _themePreviewPanel;
    }

    private Control CreateTabPreview()
    {
        _tabPreviewText = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Font = new Font("Consolas", 10f)
        };
        UpdateTabPreview();
        return _tabPreviewText;
    }

    private string GetSettingDisplayName(string key)
    {
        return key.Split('.').Last() switch
        {
            "wordWrap" => "Word Wrap",
            "syntaxHighlighting" => "Syntax Highlighting",
            "columnGuide" => "Column Guide",
            "guideColumn" => "Guide Column",
            "gutter" => "Gutter",
            "statusBar" => "Status Bar",
            "lineHighlight" => "Line Highlight",
            "hoverLineHighlight" => "Hover Line Highlight",
            "name" => "Font Family",
            "size" => "Font Size",
            "insertSpaces" => "Insert Spaces",
            "tabSize" => "Tab Size",
            "autoIndent" => "Auto Indent",
            "smartTabs" => "Smart Tabs",
            "elasticTabs" => "Elastic Tabs",
            "showWhitespace" => "Show Whitespace",
            "minimap" => "Minimap",
            "stickyScroll" => "Sticky Scroll",
            "rainbowBrackets" => "Rainbow Brackets",
            "breadcrumbs" => "Breadcrumbs",
            "theme" => "Theme",
            "vimMode" => "Vim Mode",
            "autoSave" => "Auto Save",
            "analyzers" => "Roslyn Analyzers",
            "shell" => "Terminal Shell",
            "startingDirectory" => "Starting Directory",
            "fontFace" => "Font Face",
            "fontSize" => "Font Size",
            "fontWeight" => "Font Weight",
            "scrollbar" => "Scrollbar",
            "padding" => "Padding",
            "maxScrollback" => "Max Scrollback",
            "tabTitle" => "Tab Title",
            _ => key.Split('.').Last()
        };
    }

    private Control CreateControlForSetting(string key, object value)
    {
        switch (key)
        {
            case var k when k.Contains("wordWrap") || k.Contains("syntaxHighlighting") ||
                           k.Contains("columnGuide") || k.Contains("gutter") || k.Contains("statusBar") ||
                           k.Contains("hoverLineHighlight") || k.Contains("insertSpaces") ||
                           k.Contains("autoIndent") || k.Contains("smartTabs") || k.Contains("elasticTabs") ||
                           k.Contains("showWhitespace") || k.Contains("minimap") || k.Contains("stickyScroll") ||
                           k.Contains("rainbowBrackets") || k.Contains("breadcrumbs") ||
                           k.Contains("vimMode") || k.Contains("autoSave") || k.Contains("analyzers") ||
                           k.Contains("fontWeight") || k.Contains("scrollbar"):
                return new ToggleSwitch(_theme) { Checked = (bool)value, Tag = key };

            case var k when k.Contains("lineHighlight"):
                var combo = new ComboBox
                {
                    Width = 140,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                combo.Items.AddRange(new[] { "None", "NumberOnly", "WholeLine", "NumberAndWholeLine" });
                combo.Text = value.ToString();
                combo.Tag = key;
                combo.SelectedIndexChanged += SettingControl_ValueChanged;
                return combo;

            case var k when k.Contains("tabSize"):
                var tabCombo = new ComboBox
                {
                    Width = 60,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                tabCombo.Items.AddRange(new[] { "2", "4", "6", "8", "10", "12" });
                tabCombo.Text = value.ToString();
                tabCombo.Tag = key;
                tabCombo.SelectedIndexChanged += SettingControl_ValueChanged;
                return tabCombo;

            case var k when k.Contains("guideColumn"):
                var guideCombo = new ComboBox
                {
                    Width = 60,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                guideCombo.Items.AddRange(new[] { "72", "80", "100", "120", "150" });
                guideCombo.Text = value.ToString();
                guideCombo.Tag = key;
                guideCombo.SelectedIndexChanged += SettingControl_ValueChanged;
                return guideCombo;

            case var k when k.Contains("font.name"):
                var fontCombo = new ComboBox
                {
                    Width = 140,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                fontCombo.Items.AddRange(new[] { "Consolas", "Courier New", "Lucida Console", "Menlo",
                    "Monaco", "Source Code Pro", "Fira Code", "Cascadia Code", "JetBrains Mono",
                    "DejaVu Sans Mono", "Inconsolata" });
                fontCombo.Text = value.ToString();
                fontCombo.Tag = key;
                fontCombo.SelectedIndexChanged += SettingControl_ValueChanged;
                return fontCombo;

            case var k when k.Contains("font.size"):
                var sizeUpDown = new NumericUpDown
                {
                    Width = 60,
                    Minimum = 6,
                    Maximum = 72,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text
                };
                sizeUpDown.Value = (decimal)(float)value;
                sizeUpDown.Tag = key;
                sizeUpDown.ValueChanged += SettingControl_ValueChanged;
                return sizeUpDown;

            case var k when k.Contains("terminal.fontSize"):
                var terminalFontSize = new NumericUpDown
                {
                    Width = 70,
                    Minimum = 0,
                    Maximum = 72,
                    DecimalPlaces = 1,
                    Increment = 0.5m,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text
                };
                terminalFontSize.Value = (decimal)(float)value;
                terminalFontSize.Tag = key;
                terminalFontSize.ValueChanged += SettingControl_ValueChanged;
                return terminalFontSize;

            case var k when k.Contains("terminal.padding"):
                var paddingUpDown = new NumericUpDown
                {
                    Width = 70,
                    Minimum = 0,
                    Maximum = 20,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text
                };
                paddingUpDown.Value = (int)value;
                paddingUpDown.Tag = key;
                paddingUpDown.ValueChanged += SettingControl_ValueChanged;
                return paddingUpDown;

            case var k when k.Contains("terminal.maxScrollback"):
                var scrollbackUpDown = new NumericUpDown
                {
                    Width = 90,
                    Minimum = 500,
                    Maximum = 50000,
                    Increment = 500,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text
                };
                scrollbackUpDown.Value = (int)value;
                scrollbackUpDown.Tag = key;
                scrollbackUpDown.ValueChanged += SettingControl_ValueChanged;
                return scrollbackUpDown;

            case var k when k.Contains("theme"):
                var themeCombo = new ComboBox
                {
                    Width = 140,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                themeCombo.Items.AddRange(ThemeManager.ThemeNames);
                themeCombo.Text = value.ToString();
                themeCombo.Tag = key;
                themeCombo.SelectedIndexChanged += SettingControl_ValueChanged;
                return themeCombo;

            case var k when k.Contains("terminal.fontFace"):
                var fontFaceCombo = new ComboBox
                {
                    Width = 220,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    FlatStyle = FlatStyle.Flat
                };
                fontFaceCombo.Items.Add("(default)");
                fontFaceCombo.Items.AddRange(new[]
                {
                    "Cascadia Code", "Cascadia Mono", "Consolas", "Courier New",
                    "DejaVu Sans Mono", "Fira Code", "Hack", "Inconsolata",
                    "JetBrains Mono", "Lucida Console", "Monaco", "Source Code Pro"
                });
                fontFaceCombo.Text = string.IsNullOrEmpty(value?.ToString()) ? "(default)" : value.ToString()!;
                fontFaceCombo.Tag = key;
                fontFaceCombo.SelectedIndexChanged += SettingControl_ValueChanged;
                return fontFaceCombo;

            case var k when k.Contains("shell") || k.Contains("startingDirectory") || k.Contains("tabTitle"):
                var pathText = new TextBox
                {
                    Width = 220,
                    Height = 22,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = value?.ToString() ?? string.Empty
                };
                pathText.Tag = key;
                pathText.TextChanged += SettingControl_ValueChanged;
                return pathText;

            default:
                return new Label { Text = "Not implemented", ForeColor = _theme.Text };
        }
    }

    private void SettingControl_ValueChanged(object? sender, EventArgs e)
    {
        if (sender is Control control && control.Tag is string key)
        {
            object? value = control switch
            {
                ToggleSwitch ts => ts.Checked,
                ComboBox cb when key.Contains("terminal.fontFace") => cb.Text == "(default)" ? "" : cb.Text,
                ComboBox cb => cb.Text,
                NumericUpDown nud when _currentValues.TryGetValue(key, out var currentValue) && currentValue is int => decimal.ToInt32(nud.Value),
                NumericUpDown nud => (float)nud.Value,
                TextBox tb => tb.Text,
                _ => null
            };

            if (value != null)
            {
                _currentValues[key] = value;
                UpdateLivePreview(key, value);
            }
        }
    }

    private void ResetSetting(string key)
    {
        if (_originalValues.TryGetValue(key, out var originalValue))
        {
            _currentValues[key] = originalValue;
            // Update the UI control
            if (_settingControls.TryGetValue(key, out var control))
            {
                UpdateControlValue(control, originalValue);
            }
        }
    }

    private void UpdateControlValue(Control control, object value)
    {
        switch (control)
        {
            case ToggleSwitch ts when value is bool b:
                ts.Checked = b;
                break;
            case ComboBox cb when value is string s:
                cb.Text = s;
                break;
            case NumericUpDown nud when value is float f:
                nud.Value = (decimal)f;
                break;
            case NumericUpDown nud when value is int i:
                nud.Value = i;
                break;
            case TextBox tb when value is string s:
                tb.Text = s;
                break;
        }
    }

    private string GetScopeText(SettingScope scope)
    {
        return scope switch
        {
            SettingScope.Default => "Default",
            SettingScope.User => "User",
            SettingScope.Workspace => "Workspace",
            SettingScope.Profile => "Profile",
            _ => "User"
        };
    }

    private Color GetScopeColor(SettingScope scope)
    {
        return scope switch
        {
            SettingScope.Default => Color.Gray,
            SettingScope.User => Color.FromArgb(0, 120, 215), // Blue
            SettingScope.Workspace => Color.FromArgb(0, 180, 0), // Green
            SettingScope.Profile => Color.FromArgb(180, 0, 180), // Purple
            _ => Color.Gray
        };
    }

    private void UpdateLivePreview(string key, object value)
    {
        switch (key)
        {
            case "editor.font.name":
            case "editor.font.size":
                UpdateFontPreview();
                break;
            case "workbench.appearance.theme":
                UpdateThemePreview();
                UpdateDialogTheme(); // Update the entire dialog theme
                break;
            case "editor.formatting.tabSize":
                UpdateTabPreview();
                break;
        }
    }

    private void UpdateDialogTheme()
    {
        // Get the new theme
        string themeName = GetSettingValue<string>("workbench.appearance.theme", "Dark");
        var newTheme = ThemeManager.Themes.TryGetValue(themeName, out var t) ? t : Theme.Dark;

        // Update dialog theme
        _theme = newTheme;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        // Update all controls that are theme-aware
        if (_searchBox != null)
        {
            _searchBox.BackColor = _theme.EditorBackground;
            _searchBox.ForeColor = _theme.Text;
        }

        UpdateTitleBarTheme();

        if (_customSplitter != null)
        {
            _customSplitter.BackColor = _theme.Background;
        }

        if (_treePanel != null)
        {
            _treePanel.BackColor = _theme.PanelBackground;
        }

        if (_settingsTree != null)
        {
            _settingsTree.BackColor = _theme.PanelBackground;
            _settingsTree.ForeColor = _theme.Text;
        }

        if (_contentPanel is ThemeAwareScrollablePanel scrollablePanel)
        {
            scrollablePanel.UpdateTheme(_theme);
        }

        if (_shortcutsListView is ThemeAwareListView shortcutsList)
        {
            shortcutsList.UpdateTheme(_theme);
        }

        if (_workspacePathText != null)
        {
            _workspacePathText.BackColor = _theme.EditorBackground;
            _workspacePathText.ForeColor = _theme.Text;
        }

        if (_workspaceSettingsList is ThemeAwareListView workspaceList)
        {
            workspaceList.UpdateTheme(_theme);
        }

        // Refresh the current category display with new theme
        if (_settingsTree?.SelectedNode?.Tag is string category)
        {
            ShowCategory(category);
        }

        Invalidate(); // Force repaint
    }

    private void UpdateFontPreview()
    {
        if (_fontPreviewText != null)
        {
            string fontName = GetSettingValue<string>("editor.font.name", "Consolas");
            float fontSize = GetSettingValue<float>("editor.font.size", 12f);

            try
            {
                _fontPreviewText.Font = new Font(fontName, Math.Min(fontSize, 16f)); // Limit preview size
                _fontPreviewText.Text = "The quick brown fox jumps over the lazy dog\r\n0123456789\r\nfunction Main() { return true; }";
            }
            catch
            {
                // Fallback if font is not available
                _fontPreviewText.Font = new Font("Consolas", 12f);
            }
        }
    }

    private void UpdateThemePreview()
    {
        if (_themePreviewPanel != null)
        {
            string themeName = GetSettingValue<string>("workbench.appearance.theme", "Dark");
            var theme = ThemeManager.Themes.TryGetValue(themeName, out var t) ? t : Theme.Dark;

            _themePreviewPanel.BackColor = theme.Background;
            _themePreviewPanel.Invalidate(); // Trigger repaint
        }
    }

    private void UpdateTabPreview()
    {
        if (_tabPreviewText != null)
        {
            int tabSize = GetSettingValue<int>("editor.formatting.tabSize", 4);
            bool insertSpaces = GetSettingValue<bool>("editor.formatting.insertSpaces", true);

            string indent = insertSpaces ? new string(' ', tabSize) : "\t";
            _tabPreviewText.Text = $"function example() {{\n{indent}console.log(\"Hello World\");\n{indent}return true;\n}}";
        }
    }
}

/// <summary>
/// Custom scrollable panel with theme-aware scrollbars.
/// </summary>
internal sealed class ThemeAwareScrollablePanel : Panel
{
    private Theme _theme;
    private VScrollBar? _vScrollBar;
    private HScrollBar? _hScrollBar;

    public ThemeAwareScrollablePanel(Theme theme)
    {
        _theme = theme;
        AutoScroll = true;
        DoubleBuffered = true;

        InitializeScrollbars();
    }

    private void InitializeScrollbars()
    {
        // Create custom themed scrollbars
        _vScrollBar = new ThemeAwareVScrollBar(_theme)
        {
            Minimum = 0,
            Maximum = 100,
            SmallChange = 1,
            LargeChange = 10
        };

        _hScrollBar = new ThemeAwareHScrollBar(_theme)
        {
            Minimum = 0,
            Maximum = 100,
            SmallChange = 1,
            LargeChange = 10
        };

        _vScrollBar.ValueChanged += VScrollBar_ValueChanged;
        _hScrollBar.ValueChanged += HScrollBar_ValueChanged;

        Controls.Add(_vScrollBar);
        Controls.Add(_hScrollBar);

        UpdateScrollbarVisibility();
    }

    public void UpdateTheme(Theme theme)
    {
        _theme = theme;
        BackColor = _theme.Background;

        if (_vScrollBar is ThemeAwareVScrollBar vScroll)
        {
            vScroll.UpdateTheme(_theme);
        }

        if (_hScrollBar is ThemeAwareHScrollBar hScroll)
        {
            hScroll.UpdateTheme(_theme);
        }

        Invalidate();
    }

    private void UpdateScrollbarVisibility()
    {
        if (_vScrollBar == null || _hScrollBar == null) return;

        bool needsVScroll = VerticalScroll.Visible;
        bool needsHScroll = HorizontalScroll.Visible;

        _vScrollBar.Visible = needsVScroll;
        _hScrollBar.Visible = needsHScroll;

        if (needsVScroll)
        {
            _vScrollBar.Location = new Point(Width - _vScrollBar.Width, 0);
            _vScrollBar.Height = Height - (needsHScroll ? _hScrollBar.Height : 0);
            if (_vScrollBar is ThemeAwareVScrollBar vScroll)
            {
                vScroll.Minimum = VerticalScroll.Minimum;
                vScroll.Maximum = VerticalScroll.Maximum;
                vScroll.Value = VerticalScroll.Value;
                vScroll.LargeChange = VerticalScroll.LargeChange;
            }
        }

        if (needsHScroll)
        {
            _hScrollBar.Location = new Point(0, Height - _hScrollBar.Height);
            _hScrollBar.Width = Width - (needsVScroll ? _vScrollBar.Width : 0);
            if (_hScrollBar is ThemeAwareHScrollBar hScroll)
            {
                hScroll.Minimum = HorizontalScroll.Minimum;
                hScroll.Maximum = HorizontalScroll.Maximum;
                hScroll.Value = HorizontalScroll.Value;
                hScroll.LargeChange = HorizontalScroll.LargeChange;
            }
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateScrollbarVisibility();
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        UpdateScrollbarVisibility();

        // Sync our custom scrollbars with the built-in ones
        if (_vScrollBar != null && se.ScrollOrientation == ScrollOrientation.VerticalScroll)
        {
            _vScrollBar.Value = VerticalScroll.Value;
        }
        if (_hScrollBar != null && se.ScrollOrientation == ScrollOrientation.HorizontalScroll)
        {
            _hScrollBar.Value = HorizontalScroll.Value;
        }
    }

    private void VScrollBar_ValueChanged(object? sender, EventArgs e)
    {
        if (_vScrollBar is ThemeAwareVScrollBar vScroll)
        {
            VerticalScroll.Value = vScroll.Value;
            Invalidate();
        }
    }

    private void HScrollBar_ValueChanged(object? sender, EventArgs e)
    {
        if (_hScrollBar is ThemeAwareHScrollBar hScroll)
        {
            HorizontalScroll.Value = hScroll.Value;
            Invalidate();
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        // Handle scrollbar messages to keep our custom scrollbars in sync
        const int WM_VSCROLL = 0x0115;
        const int WM_HSCROLL = 0x0114;

        if (m.Msg == WM_VSCROLL && _vScrollBar is ThemeAwareVScrollBar vScroll)
        {
            vScroll.Value = VerticalScroll.Value;
        }
        else if (m.Msg == WM_HSCROLL && _hScrollBar is ThemeAwareHScrollBar hScroll)
        {
            hScroll.Value = HorizontalScroll.Value;
        }
    }
}

/// <summary>
/// Custom scrollbar control with theme-aware rendering.
/// </summary>
internal sealed class ThemeAwareVScrollBar : VScrollBar
{
    private Theme _theme;

    public ThemeAwareVScrollBar(Theme theme)
    {
        _theme = theme;
        // Enable custom drawing
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    }

    public void UpdateTheme(Theme theme)
    {
        _theme = theme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // We'll do custom painting
        DrawScrollbar(e.Graphics);
    }

    private void DrawScrollbar(Graphics g)
    {
        // Get scroll info
        int trackWidth = Width - 4;
        int thumbWidth = Math.Max(16, trackWidth - 8);
        int thumbHeight = Math.Max(20, (int)(Height * (LargeChange / (float)(Maximum - Minimum + LargeChange))));
        int thumbPos = (int)((Value - Minimum) / (float)(Maximum - Minimum + LargeChange) * (Height - thumbHeight));
        if (thumbPos > Height - thumbHeight) thumbPos = Height - thumbHeight;

        // Draw track (background)
        using var trackBrush = new SolidBrush(_theme.PanelBackground);
        using var trackPen = new Pen(_theme.Border);
        g.FillRectangle(trackBrush, 2, 0, trackWidth, Height);
        g.DrawRectangle(trackPen, 2, 0, trackWidth, Height);

        // Draw thumb
        Rectangle thumbRect = new Rectangle(2, thumbPos, thumbWidth, thumbHeight);
        using var thumbBrush = new SolidBrush(_theme.Border);
        using var thumbPen = new Pen(_theme.Accent);
        g.FillRectangle(thumbBrush, thumbRect);
        g.DrawRectangle(thumbPen, thumbRect);

        // Draw arrows (decrement/decrement)
        int arrowSize = 16;
        // Up arrow
        Rectangle upArrowRect = new Rectangle(2, 0, trackWidth, arrowSize);
        DrawArrow(g, upArrowRect, true);
        // Down arrow
        Rectangle downArrowRect = new Rectangle(2, Height - arrowSize, trackWidth, arrowSize);
        DrawArrow(g, downArrowRect, false);
    }

    private void DrawArrow(Graphics g, Rectangle rect, bool isUp)
    {
        using var brush = new SolidBrush(_theme.Text);
        using var pen = new Pen(_theme.Text);
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        int size = 4;

        Point[] points;
        if (isUp)
        {
            points = new[]
            {
                new Point(cx - size, cy + 1),
                new Point(cx + size, cy + 1),
                new Point(cx, cy - size)
            };
        }
        else
        {
            points = new[]
            {
                new Point(cx - size, cy - 1),
                new Point(cx + size, cy - 1),
                new Point(cx, cy + size)
            };
        }
        g.FillPolygon(brush, points);
    }

    protected override void WndProc(ref Message m)
    {
        // Let base handle the scrolling logic but we handle painting
        base.WndProc(ref m);
        // Request repaint after scroll operations
        if (m.Msg == 0x0115 || m.Msg == 0x0114) // WM_VSCROLL, WM_HSCROLL
        {
            Invalidate();
        }
    }
}

/// <summary>
/// Custom horizontal scrollbar control with theme-aware rendering.
/// </summary>
internal sealed class ThemeAwareHScrollBar : HScrollBar
{
    private Theme _theme;

    public ThemeAwareHScrollBar(Theme theme)
    {
        _theme = theme;
        // Enable custom drawing
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    }

    public void UpdateTheme(Theme theme)
    {
        _theme = theme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawScrollbar(e.Graphics);
    }

    private void DrawScrollbar(Graphics g)
    {
        // Get scroll info
        int trackHeight = Height - 4;
        int thumbHeight = Math.Max(16, trackHeight - 8);
        int thumbWidth = Math.Max(20, (int)(Width * (LargeChange / (float)(Maximum - Minimum + LargeChange))));
        int thumbPos = (int)((Value - Minimum) / (float)(Maximum - Minimum + LargeChange) * (Width - thumbWidth));
        if (thumbPos > Width - thumbWidth) thumbPos = Width - thumbWidth;

        // Draw track (background)
        using var trackBrush = new SolidBrush(_theme.PanelBackground);
        using var trackPen = new Pen(_theme.Border);
        g.FillRectangle(trackBrush, 0, 2, Width, trackHeight);
        g.DrawRectangle(trackPen, 0, 2, Width, trackHeight);

        // Draw thumb
        Rectangle thumbRect = new Rectangle(thumbPos, 2, thumbWidth, trackHeight);
        using var thumbBrush = new SolidBrush(_theme.Border);
        using var thumbPen = new Pen(_theme.Accent);
        g.FillRectangle(thumbBrush, thumbRect);
        g.DrawRectangle(thumbPen, thumbRect);

        // Draw arrows
        int arrowSize = 16;
        // Left arrow
        Rectangle leftArrowRect = new Rectangle(0, 2, arrowSize, trackHeight);
        DrawArrow(g, leftArrowRect, true);
        // Right arrow
        Rectangle rightArrowRect = new Rectangle(Width - arrowSize, 2, arrowSize, trackHeight);
        DrawArrow(g, rightArrowRect, false);
    }

    private void DrawArrow(Graphics g, Rectangle rect, bool isLeft)
    {
        using var brush = new SolidBrush(_theme.Text);
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        int size = 4;

        Point[] points;
        if (isLeft)
        {
            points = new[]
            {
                new Point(cx + 1, cy - size),
                new Point(cx + 1, cy + size),
                new Point(cx - size, cy)
            };
        }
        else
        {
            points = new[]
            {
                new Point(cx - 1, cy - size),
                new Point(cx - 1, cy + size),
                new Point(cx + size, cy)
            };
        }
        g.FillPolygon(brush, points);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0115 || m.Msg == 0x0114) // WM_VSCROLL, WM_HSCROLL
        {
            Invalidate();
        }
    }
}

/// <summary>
/// Custom splitter control that avoids SplitContainer layout issues.
/// </summary>
internal sealed class CustomSplitter : Control
{
    private readonly Theme _theme;
    private Panel? _leftPanel;
    private Panel? _rightPanel;
    private int _splitterPosition = 250;
    private int _splitterWidth = 4;
    private bool _isDragging;
    private int _leftMinWidth = 200;
    private int _rightMinWidth = 400;

    public event EventHandler? SplitterMoved;

    public CustomSplitter(Theme theme)
    {
        _theme = theme;
        Dock = DockStyle.Fill;
        BackColor = theme.Background;
        DoubleBuffered = true;
        _splitterPosition = 250; // Initial safe position
    }

    public void SetLeftPanel(Panel panel)
    {
        panel.Cursor = Cursors.Default;
        _leftPanel = panel;
        Controls.Add(panel);
        UpdateLayout();
    }

    public void SetRightPanel(Panel panel)
    {
        panel.Cursor = Cursors.Default;
        _rightPanel = panel;
        Controls.Add(panel);
        UpdateLayout();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SplitterPosition
    {
        get => _splitterPosition;
        set
        {
            if (value != _splitterPosition)
            {
                _splitterPosition = Math.Max(_leftMinWidth, Math.Min(Width - _rightMinWidth - _splitterWidth, value));
                UpdateLayout();
                SplitterMoved?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void UpdateLayout()
    {
        if (_leftPanel == null || _rightPanel == null || Width <= 0) return;

        int leftWidth = _splitterPosition;
        int rightStart = _splitterPosition + _splitterWidth;

        _leftPanel.SetBounds(0, 0, leftWidth, Height);
        _rightPanel.SetBounds(rightStart, 0, Width - rightStart, Height);

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Ensure splitter position stays within bounds
        int maxPosition = Width - _rightMinWidth - _splitterWidth;
        if (_splitterPosition > maxPosition)
        {
            _splitterPosition = Math.Max(_leftMinWidth, maxPosition);
        }

        UpdateLayout();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Left && e.X >= _splitterPosition && e.X <= _splitterPosition + _splitterWidth)
        {
            _isDragging = true;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isDragging)
        {
            int newPosition = e.X - _splitterWidth / 2;
            SplitterPosition = Math.Max(_leftMinWidth, Math.Min(Width - _rightMinWidth - _splitterWidth, newPosition));
        }
        else
        {
            // Update cursor
            Cursor = (e.X >= _splitterPosition && e.X <= _splitterPosition + _splitterWidth) ?
                Cursors.VSplit : Cursors.Default;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isDragging)
        {
            _isDragging = false;
            Capture = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // Draw splitter
        using var brush = new SolidBrush(_theme.Border);
        e.Graphics.FillRectangle(brush, _splitterPosition, 0, _splitterWidth, Height);
    }
}

/// <summary>
/// Theme-aware ListView with custom scrollbars.
/// </summary>
internal sealed class ThemeAwareListView : ListView
{
    private readonly Theme _theme;

    public ThemeAwareListView(Theme theme)
    {
        _theme = theme;
        BackColor = _theme.EditorBackground;
        ForeColor = _theme.Text;
        BorderStyle = BorderStyle.None;
        DoubleBuffered = true;

        // Enable owner drawing for custom scrollbar theming
        OwnerDraw = true;
        DrawColumnHeader += ThemeAwareListView_DrawColumnHeader;
        DrawItem += ThemeAwareListView_DrawItem;
        DrawSubItem += ThemeAwareListView_DrawSubItem;
    }

    private void ThemeAwareListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        if (e?.Graphics == null || e.Header == null) return;

        using var brush = new SolidBrush(_theme.PanelBackground);
        using var textBrush = new SolidBrush(_theme.Text);
        using var borderPen = new Pen(_theme.Border);

        e.Graphics.FillRectangle(brush, e.Bounds);
        e.Graphics.DrawRectangle(borderPen, e.Bounds);
        var safeFont = Font ?? SystemFonts.DefaultFont;
        e.Graphics.DrawString(e.Header.Text, safeFont, textBrush, e.Bounds, new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        });
    }

    private void ThemeAwareListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        var brush = e.Item.Selected ?
            new SolidBrush(_theme.Accent) :
            new SolidBrush(_theme.EditorBackground);

        e.Graphics.FillRectangle(brush, e.Bounds);

        if (e.Item.Selected)
        {
            using var borderPen = new Pen(_theme.Border);
            e.Graphics.DrawRectangle(borderPen, e.Bounds);
        }
    }

    private void ThemeAwareListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e?.Graphics == null || e.Item == null || e.SubItem == null) return;

        var textColor = e.Item.Selected ? Color.White : _theme.Text;
        using var textBrush = new SolidBrush(textColor);

        var textFormat = new StringFormat
        {
            Alignment = e.ColumnIndex == 0 ? StringAlignment.Near : StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        var safeFont = Font ?? SystemFonts.DefaultFont;
        var textRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
        e.Graphics.DrawString(e.SubItem.Text, safeFont, textBrush, textRect, textFormat);
    }

    public void UpdateTheme(Theme theme)
    {
        BackColor = theme.EditorBackground;
        ForeColor = theme.Text;
        Invalidate();
    }
}

/// <summary>
/// Dialog for adding workspace settings.
/// </summary>
internal sealed class AddWorkspaceSettingDialog : Form
{
    private readonly Theme _theme;
    private TextBox _keyTextBox = null!;
    private TextBox _valueTextBox = null!;
    private ComboBox _typeComboBox = null!;
    private Button _okButton = null!;
    private Button _cancelButton = null!;

    public string SettingKey => _keyTextBox.Text;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object SettingValue { get; private set; } = "";

    public AddWorkspaceSettingDialog(Theme theme)
    {
        _theme = theme;
        Text = "Add Workspace Setting";
        Size = new Size(400, 200);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        InitializeDialog();
    }

    private void InitializeDialog()
    {
        int y = 20;

        // Key label and textbox
        var keyLabel = new Label
        {
            Text = "Setting Key:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = _theme.Text
        };
        Controls.Add(keyLabel);

        _keyTextBox = new TextBox
        {
            Location = new Point(120, y - 3),
            Size = new Size(240, 25),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        Controls.Add(_keyTextBox);
        y += 35;

        // Type label and combobox
        var typeLabel = new Label
        {
            Text = "Type:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = _theme.Text
        };
        Controls.Add(typeLabel);

        _typeComboBox = new ComboBox
        {
            Location = new Point(120, y - 3),
            Size = new Size(100, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            FlatStyle = FlatStyle.Flat
        };
        _typeComboBox.Items.AddRange(new[] { "String", "Number", "Boolean" });
        _typeComboBox.SelectedIndex = 0;
        Controls.Add(_typeComboBox);
        y += 35;

        // Value label and textbox
        var valueLabel = new Label
        {
            Text = "Value:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = _theme.Text
        };
        Controls.Add(valueLabel);

        _valueTextBox = new TextBox
        {
            Location = new Point(120, y - 3),
            Size = new Size(240, 25),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None
        };
        Controls.Add(_valueTextBox);
        y += 40;

        // Buttons
        _okButton = new Button
        {
            Text = "OK",
            Location = new Point(280, y),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent },
            DialogResult = DialogResult.OK
        };
        _okButton.Click += OkButton_Click;
        Controls.Add(_okButton);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(190, y),
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border },
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(_cancelButton);
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_keyTextBox.Text))
        {
            ThemedMessageBox.Show("Please enter a setting key.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            SettingValue = _typeComboBox.Text switch
            {
                "Number" => double.Parse(_valueTextBox.Text),
                "Boolean" => bool.Parse(_valueTextBox.Text),
                _ => _valueTextBox.Text
            };
        }
        catch
        {
            ThemedMessageBox.Show("Invalid value for the selected type.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}

/// <summary>
/// Custom toggle switch control drawn with GDI+.
/// </summary>
internal sealed class ToggleSwitch : Control
{
    private readonly Theme _theme;
    private bool _checked;
    private bool _hover;
    private const int TrackWidth = 42;
    private const int TrackHeight = 22;
    private const int ThumbSize = 18;
    private const int ThumbMargin = 2;

    public ToggleSwitch(Theme theme)
    {
        _theme = theme;
        Size = new Size(TrackWidth, TrackHeight);
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Checked
    {
        get => _checked;
        set { _checked = value; Invalidate(); }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs e)
    {
        _checked = !_checked;
        CheckedChanged?.Invoke(this, e);
        Invalidate();
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int thumbX = _checked ? TrackWidth - ThumbSize - ThumbMargin : ThumbMargin;

        // Track
        Color trackColor = _checked
            ? (_hover ? Color.FromArgb(80, 160, 80) : Color.FromArgb(70, 140, 70))
            : (_hover ? Color.FromArgb(130, 130, 130) : Color.FromArgb(100, 100, 100));

        using var trackPath = new GraphicsPath();
        trackPath.AddArc(0, 0, TrackHeight, TrackHeight, 180, 90);
        trackPath.AddArc(TrackWidth - TrackHeight, 0, TrackHeight, TrackHeight, 270, 90);
        trackPath.AddArc(TrackWidth - TrackHeight, TrackHeight - TrackHeight, TrackHeight, TrackHeight, 0, 90);
        trackPath.AddArc(0, TrackHeight - TrackHeight, TrackHeight, TrackHeight, 90, 90);
        trackPath.CloseFigure();
        using var trackBrush = new SolidBrush(trackColor);
        g.FillPath(trackBrush, trackPath);

        // Thumb
        Color thumbColor = _checked
            ? Color.FromArgb(240, 240, 240)
            : Color.FromArgb(200, 200, 200);
        using var thumbBrush = new SolidBrush(thumbColor);
        g.FillEllipse(thumbBrush, thumbX, ThumbMargin, ThumbSize, ThumbSize);

        // Thumb subtle shadow
        using var thumbPen = new Pen(Color.FromArgb(30, 0, 0, 0), 1);
        g.DrawEllipse(thumbPen, thumbX, ThumbMargin, ThumbSize, ThumbSize);
    }
}
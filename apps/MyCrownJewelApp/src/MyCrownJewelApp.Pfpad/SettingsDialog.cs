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
        const int buttonPanelHeight = 50;
        _customSplitter = new CustomSplitter(_theme);
        _customSplitter.Location = new Point(0, yOffset);
        _customSplitter.Size = new Size(ClientSize.Width, ClientSize.Height - yOffset - buttonPanelHeight);
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
        buttonPanel.BringToFront();  // Must be in front of the splitter
        Shown += (s, e) => UpdateLayout();  // Re-run once the form is fully laid out

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
        else if (category == "application.window")
        {
            y = AddWindowSettingsUI(_contentPanel, y);
        }
        else if (category == "workbench.editor")
        {
            y = AddEditorManagementUI(_contentPanel, y);
        }
        else if (category == "editor.find")
        {
            y = AddFindSettingsUI(_contentPanel, y);
        }
        else if (category == "editor.text")
        {
            y = AddTextEditorSettingsUI(_contentPanel, y);
        }
        else if (category == "editor.cursor")
        {
            y = AddCursorSettingsUI(_contentPanel, y);
        }
        else if (category == "editor.font")
        {
            y = AddFontSettingsUI(_contentPanel, y);
        }
        else if (category == "editor.formatting")
        {
            y = AddFormattingSettingsUI(_contentPanel, y);
        }
        else if (category == "features.behavior")
        {
            y = AddBehaviorSettingsUI(_contentPanel, y);
        }
        else if (category == "workbench.appearance")
        {
            y = AddWorkbenchAppearanceUI(_contentPanel, y);
        }
        else if (category is "editor" or "workbench" or "features" or "application")
        {
            // Parent node — redirect to the appropriate first child with settings
            string redirect = category switch
            {
                "editor" => "editor.text",
                "workbench" => "workbench.appearance",
                "features" => "features.terminal",
                "application" => "application.security",
                _ => category
            };
            ShowCategory(redirect);
            return;
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
            AutoCloseBracesOnEnter = GetSettingValue<bool>("editor.formatting.autoCloseBracesOnEnter", _mainForm.CurrentAutoCloseBracesOnEnter),
            FormatOnSave = GetSettingValue<bool>("editor.formatting.formatOnSave", _mainForm.CurrentFormatOnSave),
            DetectIndentationFromFile = GetSettingValue<bool>("editor.formatting.detectIndentation", _mainForm.CurrentDetectIndentationFromFile),
            RainbowBracketsEnabled = GetSettingValue<bool>("editor.appearance.rainbowBrackets", _mainForm.CurrentRainbowBrackets),
            BreadcrumbsEnabled = GetSettingValue<bool>("editor.appearance.breadcrumbs", _mainForm.CurrentBreadcrumbs),
            HoverLineHighlightEnabled = GetSettingValue<bool>("editor.cursor.hoverLineHighlight", _mainForm.CurrentHoverLineHighlight),
            LineHighlightWhenUnfocused = GetSettingValue<bool>("editor.cursor.lineHighlightWhenUnfocused", _mainForm.CurrentLineHighlightWhenUnfocused),
            CursorStyle = GetSettingValue<string>("editor.cursor.cursorStyle", _mainForm.CurrentCursorStyle),
            CursorBlinkEnabled = GetSettingValue<bool>("editor.cursor.blinkEnabled", _mainForm.CurrentCursorBlinkEnabled),
            CursorBlinkRateMs = GetSettingValue<int>("editor.cursor.blinkRateMs", _mainForm.CurrentCursorBlinkRateMs),
            ShowOccurrenceHighlights = GetSettingValue<bool>("editor.advanced.showOccurrenceHighlights", _mainForm.CurrentShowOccurrenceHighlights),
            ShowIndentGuides = GetSettingValue<bool>("editor.advanced.showIndentGuides", _mainForm.CurrentShowIndentGuides),
            SmoothenCaretBlink = GetSettingValue<bool>("editor.advanced.smoothenCaretBlink", _mainForm.CurrentSmoothenCaretBlink),
            HighlightTrailingWhitespace = GetSettingValue<bool>("editor.advanced.highlightTrailingWhitespace", _mainForm.CurrentHighlightTrailingWhitespace),
            MultiCursorEnabled = GetSettingValue<bool>("editor.advanced.multiCursorEnabled", _mainForm.CurrentMultiCursorEnabled),
            SmartHomeKey = GetSettingValue<bool>("editor.advanced.smartHomeKey", _mainForm.CurrentSmartHomeKey),
            BracketTypeOver = GetSettingValue<bool>("editor.advanced.bracketTypeOver", _mainForm.CurrentBracketTypeOver),
            AutoSaveEnabled = GetSettingValue<bool>("features.behavior.autoSave", _mainForm.CurrentAutoSave),
            TrimTrailingWhitespace = GetSettingValue<bool>("editor.text.trimTrailingWhitespace", _mainForm.CurrentTrimTrailingWhitespace),
            InsertFinalNewline = GetSettingValue<bool>("editor.text.insertFinalNewline", _mainForm.CurrentInsertFinalNewline),
            DefaultLineEnding = GetSettingValue<string>("editor.text.defaultLineEnding", _mainForm.CurrentDefaultLineEnding),
            CtrlWheelZoom = GetSettingValue<bool>("editor.text.ctrlWheelZoom", _mainForm.CurrentCtrlWheelZoom),
            MouseWheelScrollLines = GetSettingValue<int>("editor.text.mouseWheelScrollLines", _mainForm.CurrentMouseWheelScrollLines),
            AutoSaveIntervalSeconds = GetSettingValue<int>("editor.text.autoSaveIntervalSeconds", _mainForm.CurrentAutoSaveIntervalSeconds),
            ShowLineNumbers = GetSettingValue<bool>("workbench.appearance.showLineNumbers", _mainForm.CurrentShowLineNumbers),
            RelativeLineNumbers = GetSettingValue<bool>("workbench.appearance.relativeLineNumbers", _mainForm.CurrentRelativeLineNumbers),
            MinimapWidth = GetSettingValue<int>("workbench.appearance.minimapWidth", _mainForm.CurrentMinimapWidth),
            ShowGitStatusBar = GetSettingValue<bool>("workbench.appearance.showGitStatusBar", _mainForm.CurrentShowGitStatusBar),
            ShowRoslynStatusBar = GetSettingValue<bool>("workbench.appearance.showRoslynStatusBar", _mainForm.CurrentShowRoslynStatusBar),
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
            WinRememberBounds = GetSettingValue<bool>("application.window.rememberBounds", _mainForm.CurrentWinRememberBounds),
            WinLaunchMaximized = GetSettingValue<bool>("application.window.launchMaximized", _mainForm.CurrentWinLaunchMaximized),
            WinRestoreSession = GetSettingValue<bool>("application.window.restoreSession", _mainForm.CurrentWinRestoreSession),
            WinDarkTitleBar = GetSettingValue<string>("application.window.darkTitleBar", _mainForm.CurrentWinDarkTitleBar),
            WinOpacity = GetSettingValue<int>("application.window.opacity", _mainForm.CurrentWinOpacity),
            MaxFileSizeMB = GetSettingValue<int>("application.window.maxFileSizeMB", _mainForm.CurrentMaxFileSizeMB),
            LargeFileWarningMB = GetSettingValue<int>("application.window.largeFileWarningMB", _mainForm.CurrentLargeFileWarningMB),
            AsyncFileWarningMB = GetSettingValue<int>("application.window.asyncFileWarningMB", _mainForm.CurrentAsyncFileWarningMB),
            DisableSyntaxHighlightingForLargeFiles = GetSettingValue<bool>("application.window.disableSyntaxForLargeFiles", _mainForm.CurrentDisableSyntaxHighlightingForLargeFiles),
            DisableMinimapForLargeFiles = GetSettingValue<bool>("application.window.disableMinimapForLargeFiles", _mainForm.CurrentDisableMinimapForLargeFiles),
            DisableWordWrapForLargeFiles = GetSettingValue<bool>("application.window.disableWordWrapForLargeFiles", _mainForm.CurrentDisableWordWrapForLargeFiles),
            WsShowAllFiles = GetSettingValue<bool>("application.workspace.showAllFiles", _mainForm.CurrentWsShowAllFiles),
            WsExcludedDirs = GetSettingValue<string>("application.workspace.excludedDirs", _mainForm.CurrentWsExcludedDirs),
            WsTreeItemHeight = GetSettingValue<int>("application.workspace.treeItemHeight", _mainForm.CurrentWsTreeItemHeight),
            WsTreeIndent = GetSettingValue<int>("application.workspace.treeIndent", _mainForm.CurrentWsTreeIndent),
            WsAutoCollapse = GetSettingValue<bool>("application.workspace.autoCollapse", _mainForm.CurrentWsAutoCollapse),
            WsWatcherDebounceMs = GetSettingValue<int>("application.workspace.watcherDebounceMs", _mainForm.CurrentWsWatcherDebounceMs),
            WsDisableFileWatcher = GetSettingValue<bool>("application.workspace.disableFileWatcher", _mainForm.CurrentWsDisableFileWatcher),
            WsMaxRecentWorkspaces = GetSettingValue<int>("application.workspace.maxRecentWorkspaces", _mainForm.CurrentWsMaxRecentWorkspaces),
            WsMaxRecentFiles = GetSettingValue<int>("application.workspace.maxRecentFiles", _mainForm.CurrentWsMaxRecentFiles),
            VimModeEnabled = GetSettingValue<bool>("features.behavior.vimMode", _mainForm.CurrentVimMode),
            StickyScrollEnabled = GetSettingValue<bool>("editor.appearance.stickyScroll", _mainForm.CurrentStickyScroll),
            TabWidth = GetSettingValue<int>("workbench.editor.tabWidth", _mainForm.CurrentTabWidth),
            TabHeight = GetSettingValue<int>("workbench.editor.tabHeight", _mainForm.CurrentTabHeight),
            TabShowFileIcons = GetSettingValue<bool>("workbench.editor.showFileIcons", _mainForm.CurrentTabShowFileIcons),
            TabDirtyIndicator = GetSettingValue<string>("workbench.editor.dirtyIndicator", _mainForm.CurrentTabDirtyIndicator),
            TabCloseButtonVisibility = GetSettingValue<string>("workbench.editor.closeButtonVisibility", _mainForm.CurrentTabCloseButtonVisibility),
            TabMiddleClickClose = GetSettingValue<bool>("workbench.editor.middleClickClose", _mainForm.CurrentTabMiddleClickClose),
            TabMouseWheelScroll = GetSettingValue<bool>("workbench.editor.mouseWheelScroll", _mainForm.CurrentTabMouseWheelScroll),
            TabMaxOpen = GetSettingValue<int>("workbench.editor.maxOpenTabs", _mainForm.CurrentTabMaxOpen),
            TabConfirmCloseUnsaved = GetSettingValue<bool>("workbench.editor.confirmCloseUnsaved", _mainForm.CurrentTabConfirmCloseUnsaved),
            TabRememberRecentlyClosed = GetSettingValue<bool>("workbench.editor.rememberRecentlyClosed", _mainForm.CurrentTabRememberRecentlyClosed),
            TabMaxRecentlyClosed = GetSettingValue<int>("workbench.editor.maxRecentlyClosed", _mainForm.CurrentTabMaxRecentlyClosed),
            FindCaseSensitive = GetSettingValue<bool>("editor.find.caseSensitive", _mainForm.CurrentFindCaseSensitive),
            FindUseRegex = GetSettingValue<bool>("editor.find.useRegex", _mainForm.CurrentFindUseRegex),
            FindWrapAround = GetSettingValue<bool>("editor.find.wrapAround", _mainForm.CurrentFindWrapAround),
            FindSeedFromSelection = GetSettingValue<bool>("editor.find.seedFromSelection", _mainForm.CurrentFindSeedFromSelection),
            FindNotFoundNotification = GetSettingValue<string>("editor.find.notFoundNotification", _mainForm.CurrentFindNotFoundNotification),
            FindInFilesFilter = GetSettingValue<string>("editor.find.inFilesFilter", _mainForm.CurrentFindInFilesFilter),
            FindInFilesExclude = GetSettingValue<string>("editor.find.inFilesExclude", _mainForm.CurrentFindInFilesExclude),
            FindInFilesMaxResults = GetSettingValue<int>("editor.find.inFilesMaxResults", _mainForm.CurrentFindInFilesMaxResults),
            SnippetsEnabled = GetSettingValue<bool>("features.extensions.snippetsEnabled", _mainForm.CurrentSnippetsEnabled),
            SnippetTriggerKey = GetSettingValue<string>("features.extensions.snippetTriggerKey", _mainForm.CurrentSnippetTriggerKey),
            LintEnabled = GetSettingValue<bool>("features.extensions.lintEnabled", _mainForm.CurrentLintEnabled),
            LintMaxLineLength = GetSettingValue<int>("features.extensions.lintMaxLineLength", _mainForm.CurrentLintMaxLineLength),
            LintFlagMagicNumbers = GetSettingValue<bool>("features.extensions.lintFlagMagicNumbers", _mainForm.CurrentLintFlagMagicNumbers),
            LintFlagNamingConventions = GetSettingValue<bool>("features.extensions.lintFlagNamingConventions", _mainForm.CurrentLintFlagNamingConventions),
            TreeSitterEnabled = GetSettingValue<bool>("features.extensions.treeSitterEnabled", _mainForm.CurrentTreeSitterEnabled),
            TodoScanEnabled = GetSettingValue<bool>("features.extensions.todoScanEnabled", _mainForm.CurrentTodoScanEnabled),
            TodoScanPatterns = GetSettingValue<string>("features.extensions.todoScanPatterns", _mainForm.CurrentTodoScanPatterns),
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
            ["editor.text.trimTrailingWhitespace"] = _mainForm.CurrentTrimTrailingWhitespace,
            ["editor.text.insertFinalNewline"] = _mainForm.CurrentInsertFinalNewline,
            ["editor.text.defaultLineEnding"] = _mainForm.CurrentDefaultLineEnding,
            ["editor.text.ctrlWheelZoom"] = _mainForm.CurrentCtrlWheelZoom,
            ["editor.text.mouseWheelScrollLines"] = _mainForm.CurrentMouseWheelScrollLines,
            ["editor.text.autoSaveIntervalSeconds"] = _mainForm.CurrentAutoSaveIntervalSeconds,

            // Editor - Cursor
            ["editor.cursor.lineHighlight"] = _mainForm.CurrentLineHighlightName,
            ["editor.cursor.hoverLineHighlight"] = _mainForm.CurrentHoverLineHighlight,
            ["editor.cursor.lineHighlightWhenUnfocused"] = _mainForm.CurrentLineHighlightWhenUnfocused,
            ["editor.cursor.cursorStyle"] = _mainForm.CurrentCursorStyle,
            ["editor.cursor.blinkEnabled"] = _mainForm.CurrentCursorBlinkEnabled,
            ["editor.cursor.blinkRateMs"] = _mainForm.CurrentCursorBlinkRateMs,
            ["editor.advanced.showOccurrenceHighlights"] = _mainForm.CurrentShowOccurrenceHighlights,
            ["editor.advanced.showIndentGuides"] = _mainForm.CurrentShowIndentGuides,
            ["editor.advanced.smoothenCaretBlink"] = _mainForm.CurrentSmoothenCaretBlink,
            ["editor.advanced.highlightTrailingWhitespace"] = _mainForm.CurrentHighlightTrailingWhitespace,
            ["editor.advanced.multiCursorEnabled"] = _mainForm.CurrentMultiCursorEnabled,
            ["editor.advanced.smartHomeKey"] = _mainForm.CurrentSmartHomeKey,
            ["editor.advanced.bracketTypeOver"] = _mainForm.CurrentBracketTypeOver,

            // Editor - Font
            ["editor.font.name"] = _mainForm.CurrentFontName,
            ["editor.font.size"] = _mainForm.CurrentFontSize,

            // Editor - Formatting
            ["editor.formatting.insertSpaces"] = _mainForm.CurrentInsertSpaces,
            ["editor.formatting.tabSize"] = _mainForm.CurrentTabSize,
            ["editor.formatting.autoIndent"] = _mainForm.CurrentAutoIndent,
            ["editor.formatting.smartTabs"] = _mainForm.CurrentSmartTabs,
            ["editor.formatting.elasticTabs"] = _mainForm.CurrentElasticTabs,
            ["editor.formatting.autoCloseBracesOnEnter"] = _mainForm.CurrentAutoCloseBracesOnEnter,
            ["editor.formatting.formatOnSave"] = _mainForm.CurrentFormatOnSave,
            ["editor.formatting.detectIndentation"] = _mainForm.CurrentDetectIndentationFromFile,

            // Editor - Appearance
            ["editor.appearance.showWhitespace"] = _mainForm.CurrentShowWhitespace,
            ["editor.appearance.minimap"] = _mainForm.CurrentMinimapVisible,
            ["editor.appearance.stickyScroll"] = _mainForm.CurrentStickyScroll,
            ["editor.appearance.rainbowBrackets"] = _mainForm.CurrentRainbowBrackets,
            ["editor.appearance.breadcrumbs"] = _mainForm.CurrentBreadcrumbs,

            // Workbench - Appearance
            ["workbench.appearance.theme"] = _mainForm.CurrentThemeName,
            ["workbench.appearance.statusBar"] = _mainForm.CurrentStatusBarVisible,
            ["workbench.appearance.showLineNumbers"] = _mainForm.CurrentShowLineNumbers,
            ["workbench.appearance.relativeLineNumbers"] = _mainForm.CurrentRelativeLineNumbers,
            ["workbench.appearance.minimapWidth"] = _mainForm.CurrentMinimapWidth,
            ["workbench.appearance.showGitStatusBar"] = _mainForm.CurrentShowGitStatusBar,
            ["workbench.appearance.showRoslynStatusBar"] = _mainForm.CurrentShowRoslynStatusBar,

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
            ["application.security.highlightSecrets"] = _mainForm.CurrentSecHighlightHardcodedSecrets,

            // Application Window
            ["application.window.rememberBounds"] = _mainForm.CurrentWinRememberBounds,
            ["application.window.launchMaximized"] = _mainForm.CurrentWinLaunchMaximized,
            ["application.window.restoreSession"] = _mainForm.CurrentWinRestoreSession,
            ["application.window.darkTitleBar"] = _mainForm.CurrentWinDarkTitleBar,
            ["application.window.opacity"] = _mainForm.CurrentWinOpacity,
            ["application.window.maxFileSizeMB"] = _mainForm.CurrentMaxFileSizeMB,
            ["application.window.largeFileWarningMB"] = _mainForm.CurrentLargeFileWarningMB,
            ["application.window.asyncFileWarningMB"] = _mainForm.CurrentAsyncFileWarningMB,
            ["application.window.disableSyntaxForLargeFiles"] = _mainForm.CurrentDisableSyntaxHighlightingForLargeFiles,
            ["application.window.disableMinimapForLargeFiles"] = _mainForm.CurrentDisableMinimapForLargeFiles,
            ["application.window.disableWordWrapForLargeFiles"] = _mainForm.CurrentDisableWordWrapForLargeFiles,

            // Application Workspace
            ["application.workspace.showAllFiles"] = _mainForm.CurrentWsShowAllFiles,
            ["application.workspace.excludedDirs"] = _mainForm.CurrentWsExcludedDirs,
            ["application.workspace.treeItemHeight"] = _mainForm.CurrentWsTreeItemHeight,
            ["application.workspace.treeIndent"] = _mainForm.CurrentWsTreeIndent,
            ["application.workspace.autoCollapse"] = _mainForm.CurrentWsAutoCollapse,
            ["application.workspace.watcherDebounceMs"] = _mainForm.CurrentWsWatcherDebounceMs,
            ["application.workspace.disableFileWatcher"] = _mainForm.CurrentWsDisableFileWatcher,
            ["application.workspace.maxRecentWorkspaces"] = _mainForm.CurrentWsMaxRecentWorkspaces,
            ["application.workspace.maxRecentFiles"] = _mainForm.CurrentWsMaxRecentFiles,

            // Workbench - Editor Management
            ["workbench.editor.tabWidth"] = _mainForm.CurrentTabWidth,            ["workbench.editor.tabHeight"] = _mainForm.CurrentTabHeight,
            ["workbench.editor.showFileIcons"] = _mainForm.CurrentTabShowFileIcons,
            ["workbench.editor.dirtyIndicator"] = _mainForm.CurrentTabDirtyIndicator,
            ["workbench.editor.closeButtonVisibility"] = _mainForm.CurrentTabCloseButtonVisibility,
            ["workbench.editor.middleClickClose"] = _mainForm.CurrentTabMiddleClickClose,
            ["workbench.editor.mouseWheelScroll"] = _mainForm.CurrentTabMouseWheelScroll,
            ["workbench.editor.maxOpenTabs"] = _mainForm.CurrentTabMaxOpen,
            ["workbench.editor.confirmCloseUnsaved"] = _mainForm.CurrentTabConfirmCloseUnsaved,
            ["workbench.editor.rememberRecentlyClosed"] = _mainForm.CurrentTabRememberRecentlyClosed,
            ["workbench.editor.maxRecentlyClosed"] = _mainForm.CurrentTabMaxRecentlyClosed,

            // Editor - Find
            ["editor.find.caseSensitive"] = _mainForm.CurrentFindCaseSensitive,
            ["editor.find.useRegex"] = _mainForm.CurrentFindUseRegex,
            ["editor.find.wrapAround"] = _mainForm.CurrentFindWrapAround,
            ["editor.find.seedFromSelection"] = _mainForm.CurrentFindSeedFromSelection,
            ["editor.find.notFoundNotification"] = _mainForm.CurrentFindNotFoundNotification,
            ["editor.find.inFilesFilter"] = _mainForm.CurrentFindInFilesFilter,
            ["editor.find.inFilesExclude"] = _mainForm.CurrentFindInFilesExclude,
            ["editor.find.inFilesMaxResults"] = _mainForm.CurrentFindInFilesMaxResults,

            // Features - Extensions
            ["features.extensions.snippetsEnabled"] = _mainForm.CurrentSnippetsEnabled,
            ["features.extensions.snippetTriggerKey"] = _mainForm.CurrentSnippetTriggerKey,
            ["features.extensions.lintEnabled"] = _mainForm.CurrentLintEnabled,
            ["features.extensions.lintMaxLineLength"] = _mainForm.CurrentLintMaxLineLength,
            ["features.extensions.lintFlagMagicNumbers"] = _mainForm.CurrentLintFlagMagicNumbers,
            ["features.extensions.lintFlagNamingConventions"] = _mainForm.CurrentLintFlagNamingConventions,
            ["features.extensions.treeSitterEnabled"] = _mainForm.CurrentTreeSitterEnabled,
            ["features.extensions.todoScanEnabled"] = _mainForm.CurrentTodoScanEnabled,
            ["features.extensions.todoScanPatterns"] = _mainForm.CurrentTodoScanPatterns
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
        if (_okButton != null && _cancelButton != null && _okButton.Parent is Panel panel)
        {
            int w = panel.Width > 0 ? panel.Width : ClientSize.Width;
            _okButton.Location    = new Point(w - 176, 8);
            _cancelButton.Location = new Point(w - 88,  8);
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
            "editor.cursor.lineHighlightWhenUnfocused" => "Keep the current-line highlight visible even when the editor does not have keyboard focus.",
            "editor.cursor.cursorStyle" => "Caret shape: Line (2 px vertical bar), Block (full character fill), or Underline (2 px bar at bottom).",
            "editor.cursor.blinkEnabled" => "Animate the caret by blinking. Disable for a steady non-blinking cursor.",
            "editor.cursor.blinkRateMs" => "Caret blink interval in milliseconds (200–1200). Default 530 ms matches the Windows system default.",
            "editor.advanced.showOccurrenceHighlights" => "Highlight other whole-word occurrences of the current symbol or selection in the visible editor.",
            "editor.advanced.showIndentGuides" => "Draw dotted vertical guides at each indentation tab stop for indented lines.",
            "editor.advanced.smoothenCaretBlink" => "Use a soft pulsing caret animation instead of a simple on/off blink.",
            "editor.advanced.highlightTrailingWhitespace" => "Underline trailing spaces and tabs at the end of lines.",
            "editor.advanced.multiCursorEnabled" => "Allow Ctrl+Click to add or remove extra carets for multi-cursor editing experiments.",
            "editor.advanced.smartHomeKey" => "Press Home to jump to first non-whitespace, then to column 0 when already there.",
            "editor.advanced.bracketTypeOver" => "When a matching closing bracket was auto-inserted, typing that same bracket moves over it instead of inserting a duplicate.",
            "editor.font.name" => "Monospace font family used in the editor. Applies immediately.",
            "editor.font.size" => "Editor font size in points (6–72). Default 12 pt. Applies immediately.",
            "editor.formatting.insertSpaces" => "Insert spaces instead of a tab character when pressing Tab.",
            "editor.formatting.tabSize" => "Number of spaces per indent level. Also controls the visual width of real tab characters.",
            "editor.formatting.autoIndent" => "Automatically indent the new line to match the previous line; adds extra indent after opening braces.",
            "editor.formatting.smartTabs" => "Align continuation lines to the nearest tab stop based on surrounding context.",
            "editor.formatting.elasticTabs" => "Dynamically widen tab columns so tabular content stays visually aligned as you type.",
            "editor.formatting.autoCloseBracesOnEnter" => "When Enter is pressed on a line ending with '{', insert a matching '}' on the line below and place the cursor between them.",
            "editor.formatting.formatOnSave" => "Run the Roslyn document formatter before saving a C# file. Requires an open workspace (.csproj or .sln).",
            "editor.formatting.detectIndentation" => "Scan the first 100 lines of each opened file to auto-detect tabs vs spaces and indent width. Overrides global settings per-document.",
            "features.behavior.vimMode" => "Enable Vim-style modal editing (Normal, Insert, Visual, Command, Search modes). Press Esc to return to Normal mode.",
            "features.behavior.autoSave" => "Automatically save modified files after the configured idle interval.",
            "features.behavior.analyzers" => "Run Roslyn diagnostic analyzers when a .csproj or .sln workspace is loaded. Diagnostics appear as squiggles and in the Problems panel.",
            "editor.appearance.showWhitespace" => "Show whitespace characters (spaces, tabs) in the editor.",
            "editor.appearance.minimap" => "Show a miniature overview of the file on the right side.",
            "editor.appearance.stickyScroll" => "Keep the current scope visible at the top when scrolling.",
            "editor.appearance.rainbowBrackets" => "Color-code matching brackets with different colors.",
            "editor.appearance.breadcrumbs" => "Show navigation breadcrumbs at the top of the editor.",
            "workbench.appearance.theme" => "Color theme for the entire application.",
            "workbench.appearance.statusBar" => "Show the status bar at the bottom of the window.",
            "workbench.appearance.showLineNumbers" => "Show line numbers in the gutter alongside the editor.",
            "workbench.appearance.relativeLineNumbers" => "Show line numbers as distance from the current cursor line (Vim-style).",
            "workbench.appearance.minimapWidth" => "Width of the minimap panel in pixels (60–200).",
            "workbench.appearance.showGitStatusBar" => "Show git branch, dirty indicator and sync status in the status bar.",
            "workbench.appearance.showRoslynStatusBar" => "Show the Roslyn workspace indicator and analyzer toggle in the status bar.",
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
            "features.extensions.snippetsEnabled" => "Expand built-in code snippets by typing a prefix then pressing Tab.",
            "features.extensions.snippetTriggerKey" => "Key that triggers snippet expansion: Tab or Ctrl+Space.",
            "features.extensions.lintEnabled" => "Run the regex-based lint rules (line length, magic numbers, naming). Does not affect Roslyn/SAST.",
            "features.extensions.lintMaxLineLength" => "Lines exceeding this character count are flagged by the LineTooLong rule.",
            "features.extensions.lintFlagMagicNumbers" => "Warn when a hardcoded number appears outside a constant or enum declaration.",
            "features.extensions.lintFlagNamingConventions" => "Hint when identifiers do not follow common naming conventions.",
            "features.extensions.treeSitterEnabled" => "Use Tree-sitter for symbol indexing in JS/TS/Python/Go/Rust and other non-C# files.",
            "features.extensions.todoScanEnabled" => "Scan files for TODO/FIXME/HACK comments and show them in the Problems panel.",
            "features.extensions.todoScanPatterns" => "Comma-separated comment tags to scan for (case-insensitive).",
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

    private int AddWindowSettingsUI(Panel parent, int y)
    {
        y = AddSecSectionHeader(parent, y, "Startup");

        y = AddSecCheckRow(parent, y,
            "Remember window size and position",
            "Re-apply the last saved window bounds on next launch.",
            "application.window.rememberBounds");

        y = AddSecCheckRow(parent, y,
            "Launch maximized",
            "Always start the editor in a maximized window (overrides remembered bounds).",
            "application.window.launchMaximized");

        y = AddSecCheckRow(parent, y,
            "Restore previous session on launch",
            "Re-open files, cursor positions and scroll state from the last editing session.",
            "application.window.restoreSession");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Appearance");

        // Dark title bar — combo Auto / Always On / Always Off
        var dtbLbl = new Label
        {
            Text = "Dark title bar",
            Location = new Point(0, y + 4),
            Size = new Size(220, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(dtbLbl);
        var dtbDesc = new Label
        {
            Text = "Override the title bar colour. \"Auto\" follows the active theme.",
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - 174, 20),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(dtbDesc);
        string curDtb = _currentValues.TryGetValue("application.window.darkTitleBar", out var dtbv) ? dtbv.ToString()! : "auto";
        var dtbCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Location = new Point(parent.Width - 174, y + 2),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            FlatStyle = FlatStyle.Flat,
            Tag = "application.window.darkTitleBar"
        };
        dtbCombo.Items.AddRange(new[] { "auto", "on", "off" });
        dtbCombo.Text = curDtb;
        dtbCombo.SelectedIndexChanged += (s, e) => _currentValues["application.window.darkTitleBar"] = dtbCombo.Text;
        parent.Controls.Add(dtbCombo);
        y += 50;

        // Opacity — NumericUpDown 50–100
        var opLbl = new Label
        {
            Text = "Window opacity (%)",
            Location = new Point(0, y + 4),
            Size = new Size(220, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(opLbl);
        var opDesc = new Label
        {
            Text = "Transparency of the main window. 100 = fully opaque. Min 50.",
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - 124, 20),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(opDesc);
        int curOp = _currentValues.TryGetValue("application.window.opacity", out var opv) && opv is int opi ? opi : 100;
        var opSpinner = new NumericUpDown
        {
            Width = 70,
            Minimum = 50,
            Maximum = 100,
            Increment = 5,
            Value = Math.Clamp(curOp, 50, 100),
            Location = new Point(parent.Width - 124, y),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Tag = "application.window.opacity"
        };
        opSpinner.ValueChanged += (s, e) => _currentValues["application.window.opacity"] = decimal.ToInt32(opSpinner.Value);
        parent.Controls.Add(opSpinner);
        y += 50;

        y += 8;
        y = AddSecSectionHeader(parent, y, "Large File Handling");

        // Max file size
        y = AddWinNumericRow(parent, y,
            "Maximum file size (MB)",
            "Files larger than this are refused. Prevents out-of-memory crashes on huge logs.",
            "application.window.maxFileSizeMB", min: 10, max: 2000, step: 50,
            defaultVal: 500);

        // Warn threshold
        y = AddWinNumericRow(parent, y,
            "Warn before opening (MB)",
            "Show a warning dialog before opening files larger than this threshold.",
            "application.window.largeFileWarningMB", min: 1, max: 500, step: 10,
            defaultVal: 50);

        // Async load threshold
        y = AddWinNumericRow(parent, y,
            "Load asynchronously above (MB)",
            "Files above this size are streamed in on a background thread to keep the UI responsive.",
            "application.window.asyncFileWarningMB", min: 1, max: 200, step: 5,
            defaultVal: 20);

        y = AddSecCheckRow(parent, y,
            "Disable syntax highlighting for large files",
            "Prevents the highlighter from tokenising very large files where it would be too slow.",
            "application.window.disableSyntaxForLargeFiles");

        y = AddSecCheckRow(parent, y,
            "Disable minimap for large files",
            "Skip the minimap pixel-render pass for files that exceed the async-load threshold.",
            "application.window.disableMinimapForLargeFiles");

        y = AddSecCheckRow(parent, y,
            "Disable word wrap for large files",
            "Word wrap is expensive on wide files; disabling it avoids layout recalculation.",
            "application.window.disableWordWrapForLargeFiles");

        return y + 16;
    }

    private int AddWinNumericRow(Panel parent, int y, string labelText, string description,
        string key, int min, int max, int step, int defaultVal)
    {
        var lbl = new Label
        {
            Text = labelText,
            Location = new Point(0, y + 4),
            Size = new Size(260, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);
        var desc = new Label
        {
            Text = description,
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - 124, 20),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(desc);
        int current = _currentValues.TryGetValue(key, out var v) && v is int iv ? iv : defaultVal;
        var spinner = new NumericUpDown
        {
            Width = 80,
            Minimum = min,
            Maximum = max,
            Increment = step,
            Value = Math.Clamp(current, min, max),
            Location = new Point(parent.Width - 124, y),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Tag = key
        };
        spinner.ValueChanged += (s, e) => _currentValues[key] = decimal.ToInt32(spinner.Value);
        parent.Controls.Add(spinner);
        return y + 50;
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
        // ── Explorer Behavior ─────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Explorer Behavior");

        y = AddSecCheckRow(parent, y,
            "Show all files by default",
            "Display every file, not just recognized text/code types. Equivalent to the toolbar toggle.",
            "application.workspace.showAllFiles");

        // Excluded directories text row
        var exLbl = new Label
        {
            Text = "Additional excluded directories",
            Location = new Point(0, y + 4),
            Size = new Size(260, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(exLbl);
        var exDesc = new Label
        {
            Text = "Comma-separated names (e.g. dist,__pycache__,.cache). Added on top of the built-in list (bin, obj, node_modules, .vs…).",
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - 20, 28),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(exDesc);
        string curEx = _currentValues.TryGetValue("application.workspace.excludedDirs", out var exv) ? exv.ToString()! : "";
        var exTxt = new TextBox
        {
            Width = parent.Width - 20,
            Height = 22,
            Location = new Point(0, y + 54),
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Text = curEx,
            Tag = "application.workspace.excludedDirs"
        };
        exTxt.TextChanged += (s, e) => _currentValues["application.workspace.excludedDirs"] = exTxt.Text;
        parent.Controls.Add(exTxt);
        y += 82;

        y = AddSecCheckRow(parent, y,
            "Auto-collapse folders when opening a file",
            "Collapse other open top-level folders when you open a file, keeping the tree tidy.",
            "application.workspace.autoCollapse");

        // Tree item height
        y = AddWinNumericRow(parent, y,
            "Tree row height (px)",
            "Height of each row in the workspace tree. 20 = compact, 26 = normal, 32 = spacious.",
            "application.workspace.treeItemHeight", min: 16, max: 40, step: 2, defaultVal: 26);

        // Tree indent
        y = AddWinNumericRow(parent, y,
            "Tree indent per level (px)",
            "Horizontal indentation per nesting level. Default 24.",
            "application.workspace.treeIndent", min: 8, max: 48, step: 4, defaultVal: 24);

        y += 8;
        // ── File Watcher ──────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "File Watcher");

        y = AddSecCheckRow(parent, y,
            "Disable file watcher (manual refresh only)",
            "Stop watching the workspace folder for changes. Use the ↺ refresh button instead. Reduces CPU on large repos.",
            "application.workspace.disableFileWatcher");

        y = AddWinNumericRow(parent, y,
            "Refresh debounce (ms)",
            "How long to wait after a file-system event before refreshing the tree. Default 500 ms.",
            "application.workspace.watcherDebounceMs", min: 100, max: 5000, step: 100, defaultVal: 500);

        y += 8;
        // ── Recent Lists ──────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Recent Lists");

        y = AddWinNumericRow(parent, y,
            "Maximum recent workspaces",
            "How many folders appear in File > Recent Workspaces.",
            "application.workspace.maxRecentWorkspaces", min: 5, max: 50, step: 5, defaultVal: 10);

        y = AddWinNumericRow(parent, y,
            "Maximum recent files",
            "How many files appear in File > Recent Files.",
            "application.workspace.maxRecentFiles", min: 5, max: 50, step: 5, defaultVal: 10);

        y += 16;
        // ── Workspace Root ────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Workspace Root");

        var pathLabel = new Label
        {
            Text = "Current root:",
            Location = new Point(0, y + 3),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9)
        };
        parent.Controls.Add(pathLabel);

        _workspacePathText = new TextBox
        {
            Location = new Point(100, y),
            Size = new Size(parent.Width - 200, 25),
            Text = _mainForm.WorkspaceRoot ?? "",
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };
        parent.Controls.Add(_workspacePathText);

        var browseBtn = new Button
        {
            Text = "Browse…",
            Location = new Point(parent.Width - 90, y - 1),
            Size = new Size(80, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        browseBtn.Click += BrowseWorkspaceButton_Click;
        parent.Controls.Add(browseBtn);
        y += 35;

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

        // ── Workspace-specific overrides (JSON list) ──────────────────────────
        y = AddSecSectionHeader(parent, y, "Per-workspace Overrides (.pfpad/settings.json)");

        _workspaceSettingsList = new ThemeAwareListView(_theme)
        {
            Location = new Point(0, y),
            Size = new Size(parent.Width - 20, 160),
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
        y += 170;

        var addBtn = new Button
        {
            Text = "Add Setting",
            Location = new Point(0, y),
            Size = new Size(100, 28),
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
            Location = new Point(108, y),
            Size = new Size(80, 28),
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
            Location = new Point(parent.Width - 160, y),
            Size = new Size(140, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderColor = _theme.Accent }
        };
        _saveWorkspaceSettingsButton.Click += SaveWorkspaceSettingsButton_Click;
        parent.Controls.Add(_saveWorkspaceSettingsButton);

        return y + 40;
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

    private int AddWorkbenchAppearanceUI(Panel parent, int y)
    {
        // Theme — render with the full generic preview renderer
        y = AddSettingRow(parent, y, "workbench.appearance.theme",
            GetSettingValue<string>("workbench.appearance.theme", _mainForm.CurrentThemeName));

        y += 8;
        y = AddSecSectionHeader(parent, y, "Editor Chrome");

        y = AddSecCheckRow(parent, y,
            "Show line numbers",
            "Show line numbers in the gutter alongside the editor.",
            "workbench.appearance.showLineNumbers");

        y = AddSecCheckRow(parent, y,
            "Relative line numbers",
            "Show each line's distance from the current cursor line instead of its absolute number (useful with Vim mode).",
            "workbench.appearance.relativeLineNumbers");

        y = AddSecCheckRow(parent, y,
            "Show ruler",
            "Show the character-position ruler bar between the breadcrumb and the editor surface.",
            "editor.text.columnGuide");

        y = AddSecCheckRow(parent, y,
            "Minimap visible",
            "Show a scaled-down overview of the file on the right edge of the editor.",
            "editor.appearance.minimap");

        y = AddWinNumericRow(parent, y,
            "Minimap width (px)",
            "Width of the minimap panel in pixels.",
            "workbench.appearance.minimapWidth", 60, 200, 10, 100);

        y += 8;
        y = AddSecSectionHeader(parent, y, "Navigation Overlays");

        y = AddSecCheckRow(parent, y,
            "Breadcrumbs",
            "Show the file path and scope breadcrumb bar at the top of the editor.",
            "editor.appearance.breadcrumbs");

        y = AddSecCheckRow(parent, y,
            "Sticky scroll",
            "Keep the enclosing scope header visible at the top of the editor while scrolling.",
            "editor.appearance.stickyScroll");

        y = AddSecCheckRow(parent, y,
            "Rainbow brackets",
            "Colorize matching bracket pairs with distinct colors.",
            "editor.appearance.rainbowBrackets");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Status Bar");

        y = AddSecCheckRow(parent, y,
            "Show status bar",
            "Show the status bar at the bottom of the window.",
            "workbench.appearance.statusBar");

        y = AddSecCheckRow(parent, y,
            "Show git status",
            "Show git branch, dirty indicator and sync status in the status bar.",
            "workbench.appearance.showGitStatusBar");

        y = AddSecCheckRow(parent, y,
            "Show Roslyn indicator",
            "Show the Roslyn workspace dropdown and analyzer toggle in the status bar.",
            "workbench.appearance.showRoslynStatusBar");

        return y + 16;
    }

    private int AddTextEditorSettingsUI(Panel parent, int y)
    {
        y = AddSecSectionHeader(parent, y, "On Save");

        y = AddSecCheckRow(parent, y,
            "Trim trailing whitespace",
            "Remove trailing spaces and tabs from every line when the file is saved.",
            "editor.text.trimTrailingWhitespace");

        y = AddSecCheckRow(parent, y,
            "Insert final newline",
            "Append a newline at the end of the file when saved if one is not already present.",
            "editor.text.insertFinalNewline");

        y = AddEditorComboRow(parent, y,
            "Line ending style",
            "Normalize line endings on save. 'Auto' preserves the file's existing endings.",
            "editor.text.defaultLineEnding",
            new[] { ("auto", "Auto (preserve existing)"), ("CRLF", "CRLF (Windows \\r\\n)"), ("LF", "LF (Unix \\n)"), ("CR", "CR (Classic Mac \\r)") });

        y += 8;
        y = AddSecSectionHeader(parent, y, "Auto Save");

        y = AddWinNumericRow(parent, y,
            "Auto-save interval (seconds)",
            "How often to write the current file when Auto Save is enabled (File → Auto Save). Range: 10–300.",
            "editor.text.autoSaveIntervalSeconds", 10, 300, 10, 30);

        y += 8;
        y = AddSecSectionHeader(parent, y, "Mouse Interaction");

        y = AddSecCheckRow(parent, y,
            "Ctrl+Wheel font zoom",
            "Allow Ctrl+MouseWheel to zoom the editor font size. Disable to prevent accidental zoom.",
            "editor.text.ctrlWheelZoom");

        y = AddWinNumericRow(parent, y,
            "Mouse wheel scroll lines (0 = system default)",
            "Number of lines to scroll per mouse wheel tick. Set to 0 to use the Windows system setting.",
            "editor.text.mouseWheelScrollLines", 0, 20, 1, 3);

        y += 8;
        y = AddSecSectionHeader(parent, y, "Advanced Rendering");

        y = AddSecCheckRow(parent, y,
            "Highlight word occurrences",
            "Highlight other whole-word occurrences of the current symbol or selection.",
            "editor.advanced.showOccurrenceHighlights");

        y = AddSecCheckRow(parent, y,
            "Show indent guides",
            "Draw dotted indent guides at each tab stop for indented lines.",
            "editor.advanced.showIndentGuides");

        y = AddSecCheckRow(parent, y,
            "Smooth caret blink",
            "Use a pulsing caret animation instead of a simple on/off blink.",
            "editor.advanced.smoothenCaretBlink");

        y = AddSecCheckRow(parent, y,
            "Highlight trailing whitespace",
            "Underline trailing spaces and tabs at the ends of lines.",
            "editor.advanced.highlightTrailingWhitespace");

        y = AddSecCheckRow(parent, y,
            "Enable multi-cursor Ctrl+Click",
            "Allow Ctrl+Click in the editor to add or remove extra carets.",
            "editor.advanced.multiCursorEnabled");

        y = AddSecCheckRow(parent, y,
            "Smart Home key",
            "Home jumps to first non-whitespace, then to column 0 on the next press.",
            "editor.advanced.smartHomeKey");

        y = AddSecCheckRow(parent, y,
            "Bracket type-over",
            "Typing a closing bracket over an auto-inserted one moves the caret past it.",
            "editor.advanced.bracketTypeOver");

        return y + 16;
    }

    private int AddFindSettingsUI(Panel parent, int y)
    {
        y = AddSecSectionHeader(parent, y, "Default Options");

        y = AddSecCheckRow(parent, y,
            "Case sensitive by default",
            "Pre-check 'Match case' each time the Find/Replace dialog opens.",
            "editor.find.caseSensitive");

        y = AddSecCheckRow(parent, y,
            "Use regular expressions by default",
            "Pre-check 'Use regex' each time the Find/Replace dialog opens.",
            "editor.find.useRegex");

        y = AddSecCheckRow(parent, y,
            "Wrap around by default",
            "Pre-check 'Wrap' so searches continue from the top/bottom when no further match is found.",
            "editor.find.wrapAround");

        y = AddSecCheckRow(parent, y,
            "Seed find field from current selection",
            "Automatically fill the Find field with the current editor selection (up to 200 chars) when opening the dialog.",
            "editor.find.seedFromSelection");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Search Behaviour");

        y = AddEditorComboRow(parent, y,
            "Not-found notification",
            "How to notify you when the search term cannot be found in the current document.",
            "editor.find.notFoundNotification",
            new[] { ("messagebox", "Message box (blocking)"), ("statusbar", "Status bar (2 s flash)"), ("silent", "Silent") });

        y += 8;
        y = AddSecSectionHeader(parent, y, "Find in Files Defaults");

        y = AddSecTextRow(parent, y,
            "File filter",
            "Comma-separated glob patterns for Find in Files (e.g. *.cs, *.ts, *.json).",
            "editor.find.inFilesFilter", 360);

        y = AddSecTextRow(parent, y,
            "Exclude directories",
            "Comma-separated folder names to skip during Find in Files.",
            "editor.find.inFilesExclude", 360);

        y = AddWinNumericRow(parent, y,
            "Maximum results (0 = unlimited)",
            "Stop collecting matches after this many hits to protect performance on large repos.",
            "editor.find.inFilesMaxResults", 0, 100000, 500, 5000);

        return y + 16;
    }

    private int AddEditorManagementUI(Panel parent, int y)
    {
        y = AddSecSectionHeader(parent, y, "Tab Appearance");

        y = AddWinNumericRow(parent, y,
            "Tab width (px)",
            "Width of each editor tab in pixels (60–300). Default: 140.",
            "workbench.editor.tabWidth", 60, 300, 10, 140);

        y = AddWinNumericRow(parent, y,
            "Tab height (px)",
            "Height of the editor tab bar in pixels (22–40). Default: 26.",
            "workbench.editor.tabHeight", 22, 40, 1, 26);

        y = AddSecCheckRow(parent, y,
            "Show file-type icons in tabs",
            "Display a small file-type icon to the left of the filename in each editor tab.",
            "workbench.editor.showFileIcons");

        y = AddEditorComboRow(parent, y,
            "Dirty (unsaved) indicator",
            "How to mark a tab that contains unsaved changes.",
            "workbench.editor.dirtyIndicator",
            new[] { ("asterisk", "* Asterisk prefix"), ("dot", "● Dot suffix"), ("both", "Both"), ("none", "None") });

        y = AddEditorComboRow(parent, y,
            "Close button visibility",
            "When to show the × close button on each tab.",
            "workbench.editor.closeButtonVisibility",
            new[] { ("always", "Always"), ("active", "Active tab only"), ("hover", "On hover"), ("never", "Never") });

        y += 8;
        y = AddSecSectionHeader(parent, y, "Tab Behavior");

        y = AddSecCheckRow(parent, y,
            "Close tab on middle-mouse click",
            "Click the mouse wheel button on a tab to close it.",
            "workbench.editor.middleClickClose");

        y = AddSecCheckRow(parent, y,
            "Scroll through tabs on mouse wheel",
            "Roll the mouse wheel over the tab bar to cycle between editor tabs.",
            "workbench.editor.mouseWheelScroll");

        y = AddWinNumericRow(parent, y,
            "Maximum open tabs (0 = unlimited)",
            "When the limit is reached, the oldest clean tab is closed automatically to make room.",
            "workbench.editor.maxOpenTabs", 0, 100, 1, 0);

        y = AddSecCheckRow(parent, y,
            "Confirm when closing a tab with unsaved changes",
            "Show a save/discard dialog before closing a modified tab. Uncheck to silently discard.",
            "workbench.editor.confirmCloseUnsaved");

        y += 8;
        y = AddSecSectionHeader(parent, y, "Tab History");

        y = AddSecCheckRow(parent, y,
            "Remember recently closed tabs (Ctrl+Shift+T to reopen)",
            "Keep a stack of recently closed tabs so you can reopen the last-closed tab.",
            "workbench.editor.rememberRecentlyClosed");

        y = AddWinNumericRow(parent, y,
            "Max recently closed to remember",
            "How many closed tabs to keep in the reopen history (5–20).",
            "workbench.editor.maxRecentlyClosed", 5, 20, 1, 10);

        return y + 16;
    }

    private int AddEditorComboRow(Panel parent, int y, string labelText, string description,
        string key, (string value, string label)[] options)
    {
        var lbl = new Label
        {
            Text = labelText,
            Location = new Point(0, y + 4),
            Size = new Size(260, 20),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(lbl);

        var desc = new Label
        {
            Text = description,
            Location = new Point(0, y + 24),
            Size = new Size(parent.Width - 204, 20),
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 7.5f),
            BackColor = Color.Transparent
        };
        parent.Controls.Add(desc);

        string current = _currentValues.TryGetValue(key, out var v) ? v.ToString()! : options[0].value;
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(parent.Width - 200, y + 2),
            Width = 196,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            FlatStyle = FlatStyle.Flat,
            Tag = key
        };
        foreach (var (val, lbl2) in options)
            combo.Items.Add(new ComboItem(val, lbl2));
        combo.SelectedItem = combo.Items.Cast<ComboItem>().FirstOrDefault(x => x.Value == current) ?? combo.Items[0];
        combo.SelectedIndexChanged += (s, e) =>
        {
            if (combo.SelectedItem is ComboItem item)
                _currentValues[key] = item.Value;
        };
        parent.Controls.Add(combo);
        return y + 54;
    }

    private sealed class ComboItem
    {
        public string Value { get; }
        private string Label { get; }
        public ComboItem(string value, string label) { Value = value; Label = label; }
        public override string ToString() => Label;
    }

    private int AddExtensionsSettingsUI(Panel parent, int y)
    {
        // ── Section 1: Snippets ───────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Snippets");

        y = AddSecCheckRow(parent, y,
            "Enable snippets",
            "Expand built-in code snippets by typing a prefix then pressing the trigger key.",
            "features.extensions.snippetsEnabled");

        y = AddEditorComboRow(parent, y,
            "Trigger key",
            "Key that expands a snippet prefix. Tab fires when the cursor is right after a known prefix.",
            "features.extensions.snippetTriggerKey",
            new[] { ("Tab", "Tab"), ("CtrlSpace", "Ctrl+Space") });

        y += 8;
        // ── Section 2: Linting ────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Linting");

        y = AddSecCheckRow(parent, y,
            "Enable linting",
            "Run the regex-based lint rules (line length, magic numbers, naming). " +
            "Does not affect Roslyn/SAST diagnostics (controlled under Security settings).",
            "features.extensions.lintEnabled");

        y = AddWinNumericRow(parent, y,
            "Max line length",
            "Lines exceeding this character count are flagged by the LineTooLong rule. Range: 60–300.",
            "features.extensions.lintMaxLineLength", 60, 300, 10, 120);

        y = AddSecCheckRow(parent, y,
            "Flag magic numbers",
            "Warn when a hardcoded numeric literal appears outside a constant or enum declaration.",
            "features.extensions.lintFlagMagicNumbers");

        y = AddSecCheckRow(parent, y,
            "Flag naming convention violations",
            "Show hints when public members or parameters do not follow common naming conventions.",
            "features.extensions.lintFlagNamingConventions");

        y += 8;
        // ── Section 3: Syntax Analysis ────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Syntax Analysis");

        y = AddSecCheckRow(parent, y,
            "Enable Tree-sitter symbol indexing",
            "Use Tree-sitter for go-to-definition and symbol indexing in JS, TS, Python, Go, " +
            "Rust, Ruby, Java, and other non-C# files. Disable to fall back to regex-only scanning.",
            "features.extensions.treeSitterEnabled");

        y += 8;
        // ── Section 4: TODO Scanner ───────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "TODO Scanner");

        y = AddSecCheckRow(parent, y,
            "Enable TODO scanner",
            "Scan open files for tagged comments and surface them in the Problems panel.",
            "features.extensions.todoScanEnabled");

        y = AddSecTextRow(parent, y,
            "Scan patterns",
            "Comma-separated comment tags to scan for (case-insensitive).",
            "features.extensions.todoScanPatterns", 280);

        return y + 16;
    }

    private int AddCursorSettingsUI(Panel parent, int y)
    {
        // ── Section 1: Current Line Highlight ─────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Current Line Highlight");

        y = AddEditorComboRow(parent, y,
            "Highlight mode",
            "How to mark the line containing the cursor. Number only tints the gutter number; " +
            "Whole line draws a band across the full editor width.",
            "editor.cursor.lineHighlight",
            new[] {
                ("Off",                  "Off"),
                ("NumberOnly",           "Number only"),
                ("WholeLine",            "Whole line"),
                ("NumberAndWholeLine",   "Number + whole line")
            });

        y = AddSecCheckRow(parent, y,
            "Show when unfocused",
            "Keep the current-line highlight visible even when the editor does not have keyboard focus " +
            "(e.g. when using the terminal or workspace panel).",
            "editor.cursor.lineHighlightWhenUnfocused");

        y = AddSecCheckRow(parent, y,
            "Hover line highlight",
            "Draw a subtle highlight on the line the mouse pointer is hovering over.",
            "editor.cursor.hoverLineHighlight");

        y += 8;
        // ── Section 2: Caret Appearance ───────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Caret Appearance");

        y = AddEditorComboRow(parent, y,
            "Cursor style",
            "Shape of the text insertion cursor. Line: 2 px vertical bar (VS Code default). " +
            "Block: full character fill (Vim style). Underline: 2 px bar at the bottom of the character cell.",
            "editor.cursor.cursorStyle",
            new[] {
                ("line",       "Line (2 px bar)"),
                ("block",      "Block (full character)"),
                ("underline",  "Underline (2 px bar)")
            });

        y += 8;
        // ── Section 3: Caret Blinking ─────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Caret Blinking");

        y = AddSecCheckRow(parent, y,
            "Enable blinking",
            "Animate the caret by toggling its visibility at the blink rate. " +
            "Disable for a steady non-blinking cursor.",
            "editor.cursor.blinkEnabled");

        y = AddWinNumericRow(parent, y,
            "Blink rate (ms)",
            "How long (in milliseconds) between each blink on/off cycle. Range: 200–1200. " +
            "530 ms matches the Windows system default.",
            "editor.cursor.blinkRateMs", 200, 1200, 50, 530);

        return y + 16;
    }

    private int AddFontSettingsUI(Panel parent, int y)
    {
        // ── Font Family ────────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Font Family");

        if (_currentValues.TryGetValue("editor.font.name", out var fontNameVal))
            y = AddSettingRow(parent, y, "editor.font.name", fontNameVal);

        y += 8;
        // ── Font Size ──────────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Font Size");

        if (_currentValues.TryGetValue("editor.font.size", out var fontSizeVal))
            y = AddSettingRow(parent, y, "editor.font.size", fontSizeVal);

        y += 8;
        // ── Preview ────────────────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Preview");

        var preview = CreateFontPreview();
        preview.Location = new Point(0, y);
        preview.Size = new Size(parent.Width - 32, 80);
        parent.Controls.Add(preview);
        y += 88;

        return y + 16;
    }

    private int AddFormattingSettingsUI(Panel parent, int y)
    {
        // ── Section 1: Indentation ─────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Indentation");

        y = AddSecCheckRow(parent, y,
            "Insert spaces",
            "Press Tab to insert spaces. Uncheck to insert a real tab character (\\t).",
            "editor.formatting.insertSpaces");

        if (_currentValues.TryGetValue("editor.formatting.tabSize", out var tabSizeVal))
            y = AddSettingRow(parent, y, "editor.formatting.tabSize", tabSizeVal);

        y = AddSecCheckRow(parent, y,
            "Auto-indent",
            "Automatically indent the new line to match the indentation of the previous line " +
            "and add extra indentation after opening braces.",
            "editor.formatting.autoIndent");

        y = AddSecCheckRow(parent, y,
            "Smart tabs",
            "Align continuation lines to the nearest tab stop based on context rather than inserting a full indent.",
            "editor.formatting.smartTabs");

        y = AddSecCheckRow(parent, y,
            "Elastic tab stops",
            "Dynamically adjust tab column widths so that tabular content stays aligned " +
            "as you type across multiple lines.",
            "editor.formatting.elasticTabs");

        y += 8;
        // ── Section 2: On Save ─────────────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "On Save");

        y = AddSecCheckRow(parent, y,
            "Trim trailing whitespace",
            "Remove spaces and tabs at the end of every line when the file is saved.",
            "editor.text.trimTrailingWhitespace");

        y = AddSecCheckRow(parent, y,
            "Insert final newline",
            "Append a newline character at the end of the file on save if one is not already present.",
            "editor.text.insertFinalNewline");

        y = AddEditorComboRow(parent, y,
            "Line endings",
            "End-of-line sequence written when saving. \"Auto\" preserves whatever the file already uses.",
            "editor.text.defaultLineEnding",
            new[] {
                ("auto", "Auto (preserve)"),
                ("CRLF", "CRLF  (Windows \\r\\n)"),
                ("LF",   "LF    (Unix \\n)"),
                ("CR",   "CR    (Classic Mac \\r)")
            });

        y += 8;
        // ── Section 3: Smart Formatting ───────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Smart Formatting");

        y = AddSecCheckRow(parent, y,
            "Auto-close braces on Enter",
            "When Enter is pressed on a line ending with an opening brace '{', " +
            "automatically insert a matching closing brace '}' on the next empty line " +
            "and position the cursor between them.",
            "editor.formatting.autoCloseBracesOnEnter");

        y = AddSecCheckRow(parent, y,
            "Format on save",
            "Run the Roslyn document formatter before saving a C# file. " +
            "Requires an open workspace (.csproj or .sln). Has no effect on non-C# files.",
            "editor.formatting.formatOnSave");

        y = AddSecCheckRow(parent, y,
            "Detect indentation from file",
            "Scan the first 100 lines of a newly opened file to detect whether it uses " +
            "tabs or spaces and what the indent width is. The detected settings override the " +
            "global Tab Size and Insert Spaces for that document while it is active.",
            "editor.formatting.detectIndentation");

        return y + 16;
    }

    private int AddBehaviorSettingsUI(Panel parent, int y)
    {
        // ── Section 1: Editor Behavior ─────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Editor Behavior");

        y = AddSecCheckRow(parent, y,
            "Vim keybindings",
            "Enable Vim-style modal editing (Normal, Insert, Visual, Command, Search). " +
            "Press Esc to return to Normal mode. Caret automatically switches to block style in Normal mode.",
            "features.behavior.vimMode");

        y = AddSecCheckRow(parent, y,
            "Auto save",
            "Automatically save modified files after the configured interval. " +
            "Files are saved silently without showing a dialog.",
            "features.behavior.autoSave");

        y = AddWinNumericRow(parent, y,
            "Auto save interval (s)",
            "Seconds of idle time before an unsaved file is written to disk automatically (10–300). " +
            "Only active when Auto Save is enabled.",
            "editor.text.autoSaveIntervalSeconds", 10, 300, 10, 30);

        y += 8;
        // ── Section 2: Code Analysis ───────────────────────────────────────────
        y = AddSecSectionHeader(parent, y, "Code Analysis");

        y = AddSecCheckRow(parent, y,
            "Roslyn analyzers",
            "Run Roslyn diagnostic analyzers in the background when a .csproj or .sln workspace is loaded. " +
            "Diagnostics appear as squiggles and in the Problems panel. " +
            "Disable to reduce CPU usage on large solutions.",
            "features.behavior.analyzers");

        return y + 16;
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

            // Catch-all: bool → toggle, int → numeric, string → text
            case var k when value is bool bv:
                return new ToggleSwitch(_theme) { Checked = bv, Tag = k };

            case var k when value is int iv:
                var intSpinner = new NumericUpDown
                {
                    Width = 80,
                    Minimum = 0,
                    Maximum = 9999,
                    Value = iv,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    Tag = k
                };
                intSpinner.ValueChanged += SettingControl_ValueChanged;
                return intSpinner;

            case var k when value is string sv:
                var genericText = new TextBox
                {
                    Width = 220,
                    Height = 22,
                    BackColor = _theme.EditorBackground,
                    ForeColor = _theme.Text,
                    BorderStyle = BorderStyle.FixedSingle,
                    Text = sv,
                    Tag = k
                };
                genericText.TextChanged += SettingControl_ValueChanged;
                return genericText;

            default:
                return new Label { Text = "Not implemented", AutoSize = true, ForeColor = _theme.Text };
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
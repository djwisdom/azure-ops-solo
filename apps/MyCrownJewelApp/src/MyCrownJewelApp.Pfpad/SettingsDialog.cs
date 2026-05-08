using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class SettingsDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly Theme _theme;

    // Editor section
    private ComboBox _fontNameCombo = null!;
    private NumericUpDown _fontSizeUpDown = null!;
    private ComboBox _tabSizeCombo = null!;
    private CheckBox _insertSpacesCheck = null!;
    private CheckBox _wordWrapCheck = null!;
    private CheckBox _showGuideCheck = null!;
    private NumericUpDown _guideColumnUpDown = null!;

    // Appearance section
    private ComboBox _themeCombo = null!;
    private CheckBox _gutterCheck = null!;
    private CheckBox _statusBarCheck = null!;
    private CheckBox _minimapCheck = null!;
    private CheckBox _showWhitespaceCheck = null!;
    private ComboBox _lineHighlightCombo = null!;

    // Features section
    private CheckBox _syntaxHighlightCheck = null!;
    private CheckBox _autoIndentCheck = null!;
    private CheckBox _smartTabsCheck = null!;
    private CheckBox _elasticTabsCheck = null!;
    private CheckBox _rainbowBracketsCheck = null!;
    private CheckBox _breadcrumbsCheck = null!;
    private CheckBox _autoSaveCheck = null!;

    // Panels section
    private CheckBox _workspacePanelCheck = null!;
    private CheckBox _symbolPanelCheck = null!;
    private CheckBox _problemsPanelCheck = null!;
    private CheckBox _terminalCheck = null!;
    private NumericUpDown _terminalHeightUpDown = null!;

    // Advanced section
    private CheckBox _analyzersCheck = null!;
    private TextBox _terminalShellText = null!;

    public SettingsDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _theme = ThemeManager.Instance.CurrentTheme;
        Text = "Settings";
        Size = new Size(620, 620);
        MinimumSize = new Size(500, 450);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        Font = new Font("Segoe UI", 9);

        InitializeForm();
        LoadCurrentValues();
        ApplyTheme();
    }

    private void InitializeForm()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            AutoScroll = true,
            BackColor = _theme.Background
        };

        int y = 0;
        const int groupWidth = 580;
        const int labelLeft = 16;
        const int controlLeft = 180;
        const int controlWidth = 260;

        // ─── Editor Section ───
        var editorGroup = CreateGroup("Editor", ref y, groupWidth);
        y += 28;

        AddLabel("Font:", labelLeft, y); _fontNameCombo = AddCombo(controlLeft, y, controlWidth, GetMonospaceFonts()); y += 26;
        AddLabel("Font Size:", labelLeft, y); _fontSizeUpDown = AddNumericUpDown(controlLeft, y, 60, 6, 72, 1); y += 26;
        AddLabel("Tab Size:", labelLeft, y); _tabSizeCombo = AddCombo(controlLeft, y, 80, new[] { "2", "4", "6", "8", "10", "12" }); y += 26;
        _insertSpacesCheck = AddCheck("Insert Spaces", labelLeft, ref y);
        _wordWrapCheck = AddCheck("Word Wrap", labelLeft, ref y);
        _showGuideCheck = AddCheck("Show Guide Line", labelLeft, ref y);
        AddLabel("Guide Column:", labelLeft, y); _guideColumnUpDown = AddNumericUpDown(controlLeft, y, 60, 40, 200, 1); y += 30;
        editorGroup.Height = y - editorGroup.Top + 8;
        panel.Controls.Add(editorGroup);

        // ─── Appearance Section ───
        y += 4;
        var appearanceGroup = CreateGroup("Appearance", ref y, groupWidth);
        y += 28;

        AddLabel("Theme:", labelLeft, y); _themeCombo = AddCombo(controlLeft, y, 160, ThemeManager.Themes.Keys.ToArray()); y += 26;
        _gutterCheck = AddCheck("Show Gutter", labelLeft, ref y);
        _statusBarCheck = AddCheck("Show Status Bar", labelLeft, ref y);
        _minimapCheck = AddCheck("Show Minimap", labelLeft, ref y);
        _showWhitespaceCheck = AddCheck("Show Whitespace Glyphs", labelLeft, ref y);
        AddLabel("Line Highlight:", labelLeft, y); _lineHighlightCombo = AddCombo(controlLeft, y, 140, new[] { "None", "NumberOnly", "WholeLine" }); y += 30;
        appearanceGroup.Height = y - appearanceGroup.Top + 8;
        panel.Controls.Add(appearanceGroup);

        // ─── Features Section ───
        y += 4;
        var featuresGroup = CreateGroup("Editor Features", ref y, groupWidth);
        y += 28;

        _syntaxHighlightCheck = AddCheck("Syntax Highlighting", labelLeft, ref y);
        _autoIndentCheck = AddCheck("Auto Indent", labelLeft, ref y);
        _smartTabsCheck = AddCheck("Smart Tabs", labelLeft, ref y);
        _elasticTabsCheck = AddCheck("Elastic Tabs", labelLeft, ref y);
        _rainbowBracketsCheck = AddCheck("Rainbow Brackets", labelLeft, ref y);
        _breadcrumbsCheck = AddCheck("Breadcrumbs", labelLeft, ref y);
        _autoSaveCheck = AddCheck("Auto-Save (30s)", labelLeft, ref y);
        featuresGroup.Height = y - featuresGroup.Top + 8;
        panel.Controls.Add(featuresGroup);

        // ─── Panels Section ───
        y += 4;
        var panelsGroup = CreateGroup("Panels", ref y, groupWidth);
        y += 28;

        _workspacePanelCheck = AddCheck("Workspace Panel (Ctrl+Alt+O)", labelLeft, ref y);
        _symbolPanelCheck = AddCheck("Symbol Panel (Ctrl+Shift+W)", labelLeft, ref y);
        _problemsPanelCheck = AddCheck("Problems Panel", labelLeft, ref y);
        _terminalCheck = AddCheck("Terminal Panel", labelLeft, ref y);
        AddLabel("Terminal Height:", labelLeft, y); _terminalHeightUpDown = AddNumericUpDown(controlLeft, y, 60, 60, 600, 10); y += 30;
        panelsGroup.Height = y - panelsGroup.Top + 8;
        panel.Controls.Add(panelsGroup);

        // ─── Advanced Section ───
        y += 4;
        var advancedGroup = CreateGroup("Advanced", ref y, groupWidth);
        y += 28;

        _analyzersCheck = AddCheck("Roslyn Analyzers", labelLeft, ref y);
        AddLabel("Terminal Shell:", labelLeft, y);
        _terminalShellText = new TextBox
        {
            Location = new Point(controlLeft, y),
            Width = controlWidth + 40,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9)
        };
        _terminalShellText.TextChanged += (s, e) => { };
        y += 26;
        advancedGroup.Height = y - advancedGroup.Top + 8;
        panel.Controls.Add(advancedGroup);

        // ─── Buttons ───
        y += 12;
        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(groupWidth - 172, y),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        okButton.Click += Ok_Click;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(groupWidth - 86, y),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        cancelButton.Click += (s, e) => Close();

        panel.Controls.Add(okButton);
        panel.Controls.Add(cancelButton);

        panel.Size = new Size(groupWidth + 24, y + 50);
        Controls.Add(panel);
    }

    private GroupBox CreateGroup(string title, ref int y, int width)
    {
        var gb = new GroupBox
        {
            Text = title,
            Location = new Point(8, y),
            Size = new Size(width, 40),
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = _theme.Background
        };
        return gb;
    }

    private Label AddLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y + 3),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent
        };
        return lbl;
    }

    private CheckBox AddCheck(string text, int x, ref int y)
    {
        var cb = new CheckBox
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Standard
        };
        y += 24;
        return cb;
    }

    private ComboBox AddCombo(int x, int y, int width, string[] items)
    {
        var cb = new ComboBox
        {
            Location = new Point(x, y),
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            FlatStyle = FlatStyle.Flat
        };
        cb.Items.AddRange(items);
        return cb;
    }

    private NumericUpDown AddNumericUpDown(int x, int y, int width, decimal min, decimal max, decimal inc)
    {
        var nud = new NumericUpDown
        {
            Location = new Point(x, y),
            Width = width,
            Minimum = min,
            Maximum = max,
            Increment = inc,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text
        };
        return nud;
    }

    private void LoadCurrentValues()
    {
        // Editor
        _fontNameCombo.Text = _mainForm.CurrentFontName;
        _fontSizeUpDown.Value = (decimal)_mainForm.CurrentFontSize;
        _tabSizeCombo.Text = _mainForm.CurrentTabSize.ToString();
        _insertSpacesCheck.Checked = _mainForm.CurrentInsertSpaces;
        _wordWrapCheck.Checked = _mainForm.CurrentWordWrap;
        _showGuideCheck.Checked = _mainForm.CurrentShowGuide;
        _guideColumnUpDown.Value = _mainForm.CurrentGuideColumn;

        // Appearance
        _themeCombo.Text = _mainForm.CurrentThemeName;
        _gutterCheck.Checked = _mainForm.CurrentGutterVisible;
        _statusBarCheck.Checked = _mainForm.CurrentStatusBarVisible;
        _minimapCheck.Checked = _mainForm.CurrentMinimapVisible;
        _showWhitespaceCheck.Checked = _mainForm.CurrentShowWhitespace;
        _lineHighlightCombo.Text = _mainForm.CurrentLineHighlightName;

        // Features
        _syntaxHighlightCheck.Checked = _mainForm.CurrentSyntaxHighlighting;
        _autoIndentCheck.Checked = _mainForm.CurrentAutoIndent;
        _smartTabsCheck.Checked = _mainForm.CurrentSmartTabs;
        _elasticTabsCheck.Checked = _mainForm.CurrentElasticTabs;
        _rainbowBracketsCheck.Checked = _mainForm.CurrentRainbowBrackets;
        _breadcrumbsCheck.Checked = _mainForm.CurrentBreadcrumbs;
        _autoSaveCheck.Checked = _mainForm.CurrentAutoSave;

        // Panels
        _workspacePanelCheck.Checked = _mainForm.CurrentWorkspaceVisible;
        _symbolPanelCheck.Checked = _mainForm.CurrentSymbolPanelVisible;
        _problemsPanelCheck.Checked = _mainForm.CurrentProblemsPanelVisible;
        _terminalCheck.Checked = _mainForm.CurrentTerminalVisible;
        _terminalHeightUpDown.Value = Math.Max(60, Math.Min(600, _mainForm.CurrentTerminalHeight));

        // Advanced
        _analyzersCheck.Checked = _mainForm.CurrentAnalyzersEnabled;
        _terminalShellText.Text = _mainForm.CurrentTerminalShell;
    }

    private void Ok_Click(object? sender, EventArgs e)
    {
        _mainForm.ApplySettings(
            fontName: _fontNameCombo.Text,
            fontSize: (float)_fontSizeUpDown.Value,
            tabSize: int.TryParse(_tabSizeCombo.Text, out int ts) ? ts : 4,
            insertSpaces: _insertSpacesCheck.Checked,
            wordWrap: _wordWrapCheck.Checked,
            showGuide: _showGuideCheck.Checked,
            guideColumn: (int)_guideColumnUpDown.Value,
            themeName: _themeCombo.Text,
            gutterVisible: _gutterCheck.Checked,
            statusBarVisible: _statusBarCheck.Checked,
            minimapVisible: _minimapCheck.Checked,
            showWhitespace: _showWhitespaceCheck.Checked,
            lineHighlightMode: _lineHighlightCombo.Text,
            syntaxHighlighting: _syntaxHighlightCheck.Checked,
            autoIndent: _autoIndentCheck.Checked,
            smartTabs: _smartTabsCheck.Checked,
            elasticTabs: _elasticTabsCheck.Checked,
            rainbowBrackets: _rainbowBracketsCheck.Checked,
            breadcrumbs: _breadcrumbsCheck.Checked,
            autoSave: _autoSaveCheck.Checked,
            workspaceVisible: _workspacePanelCheck.Checked,
            symbolPanelVisible: _symbolPanelCheck.Checked,
            problemsPanelVisible: _problemsPanelCheck.Checked,
            terminalVisible: _terminalCheck.Checked,
            terminalHeight: (int)_terminalHeightUpDown.Value,
            analyzersEnabled: _analyzersCheck.Checked,
            terminalShell: _terminalShellText.Text
        );
        Close();
    }

    private void ApplyTheme()
    {
        foreach (Control c in Controls)
        {
            ApplyThemeToControl(c);
        }
    }

    private void ApplyThemeToControl(Control c)
    {
        c.BackColor = _theme.Background;
        c.ForeColor = _theme.Text;
        foreach (Control child in c.Controls)
            ApplyThemeToControl(child);
    }

    private static string[] GetMonospaceFonts()
    {
        return new[]
        {
            "Consolas", "Courier New", "Lucida Console", "Menlo",
            "Monaco", "Source Code Pro", "Fira Code", "Cascadia Code",
            "JetBrains Mono", "DejaVu Sans Mono", "Inconsolata"
        };
    }
}
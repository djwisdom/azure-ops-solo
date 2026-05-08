using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class SettingsDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly Theme _theme;

    private ComboBox _fontNameCombo = null!;
    private NumericUpDown _fontSizeUpDown = null!;
    private ComboBox _tabSizeCombo = null!;
    private ComboBox _lineHighlightCombo = null!;
    private ComboBox _guideColumnCombo = null!;
    private ComboBox _themeCombo = null!;
    private TextBox _terminalShellText = null!;

    // Toggle switches (stored by name for ApplySettings)
    private ToggleSwitch _togStatusBar = null!;
    private ToggleSwitch _togWordWrap = null!;
    private ToggleSwitch _togSyntaxHighlight = null!;
    private ToggleSwitch _togInsertSpaces = null!;
    private ToggleSwitch _togAutoIndent = null!;
    private ToggleSwitch _togSmartTabs = null!;
    private ToggleSwitch _togElasticTabs = null!;
    private ToggleSwitch _togColumnGuide = null!;
    private ToggleSwitch _togGutter = null!;
    private ToggleSwitch _togShowWhitespace = null!;
    private ToggleSwitch _togMinimap = null!;
    private ToggleSwitch _togStickyScroll = null!;
    private ToggleSwitch _togRainbowBrackets = null!;
    private ToggleSwitch _togBreadcrumbs = null!;
    private ToggleSwitch _togVimMode = null!;
    private ToggleSwitch _togAutoSave = null!;
    private ToggleSwitch _togAnalyzers = null!;

    public SettingsDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _theme = ThemeManager.Instance.CurrentTheme;
        Text = "Settings";
        Size = new Size(680, 620);
        MinimumSize = new Size(480, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        Font = new Font("Segoe UI", 9);

        InitializeForm();
        LoadCurrentValues();
    }

    private void InitializeForm()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            AutoScroll = true,
            BackColor = _theme.Background
        };

        int y = 0;

        y = AddSectionHeader(panel, y, "Editor");
        y = AddToggleRow(panel, y, "Status Bar", out _togStatusBar);
        y = AddToggleRow(panel, y, "Word Wrap", out _togWordWrap);
        y = AddToggleRow(panel, y, "Syntax Highlighting", out _togSyntaxHighlight);
        y = AddChoiceRow(panel, y, "Current Line Highlight", out _lineHighlightCombo, "None", "NumberOnly", "WholeLine", "NumberAndWholeLine");
        y = AddToggleRow(panel, y, "Insert Spaces", out _togInsertSpaces);
        y = AddChoiceRow(panel, y, "Tab Size", out _tabSizeCombo, "2", "4", "6", "8", "10", "12");
        y = AddToggleRow(panel, y, "Auto Indent", out _togAutoIndent);
        y = AddToggleRow(panel, y, "Smart Tabs", out _togSmartTabs);
        y = AddToggleRow(panel, y, "Elastic Tabs", out _togElasticTabs);
        y = AddToggleRow(panel, y, "Column Guide", out _togColumnGuide);
        y = AddChoiceRow(panel, y, "Guide Column", out _guideColumnCombo, "72", "80", "100", "120", "150");
        y = AddFontRow(panel, y);

        y = AddSectionHeader(panel, y, "Appearance");
        y = AddToggleRow(panel, y, "Gutter", out _togGutter);
        y = AddToggleRow(panel, y, "Show Whitespace", out _togShowWhitespace);
        y = AddToggleRow(panel, y, "Minimap", out _togMinimap);
        y = AddToggleRow(panel, y, "Sticky Scroll", out _togStickyScroll);
        y = AddToggleRow(panel, y, "Rainbow Brackets", out _togRainbowBrackets);
        y = AddToggleRow(panel, y, "Breadcrumbs", out _togBreadcrumbs);
        y = AddThemeRow(panel, y);

        y = AddSectionHeader(panel, y, "Behavior");
        y = AddToggleRow(panel, y, "Vim Mode", out _togVimMode);
        y = AddToggleRow(panel, y, "Auto-Save (30s)", out _togAutoSave);
        y = AddToggleRow(panel, y, "Roslyn Analyzers", out _togAnalyzers);
        y = AddShellRow(panel, y);

        panel.Size = new Size(680, y + 60);
        Controls.Add(panel);

        // Buttons
        var okBtn = new Button
        {
            Text = "OK", Location = new Point(panel.Width - 192, y + 12), Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        okBtn.Click += Ok_Click;

        var cancelBtn = new Button
        {
            Text = "Cancel", Location = new Point(panel.Width - 106, y + 12), Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat, BackColor = _theme.PanelBackground, ForeColor = _theme.Text,
            FlatAppearance = { BorderColor = _theme.Border }
        };
        cancelBtn.Click += (s, e) => Close();

        Controls.Add(okBtn);
        Controls.Add(cancelBtn);

        // Summary at bottom
        var summary = new Label
        {
            Text = "Changes apply immediately on OK.",
            Location = new Point(16, y + 16), AutoSize = true,
            ForeColor = _theme.Muted, BackColor = Color.Transparent
        };
        Controls.Add(summary);
    }

    private int AddSectionHeader(Panel parent, int y, string title)
    {
        var lbl = new Label
        {
            Text = title, Location = new Point(0, y), Size = new Size(parent.Width - 32, 28),
            ForeColor = _theme.Accent, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };
        parent.Controls.Add(lbl);
        y += 30;

        var line = new Label
        {
            Location = new Point(0, y - 1), Size = new Size(parent.Width - 32, 1),
            BackColor = _theme.Border
        };
        parent.Controls.Add(line);

        return y;
    }

    private int AddToggleRow(Panel parent, int y, string label, out ToggleSwitch toggle)
    {
        var lbl = new Label
        {
            Text = label, Location = new Point(4, y + 3), AutoSize = true,
            ForeColor = _theme.Text, BackColor = Color.Transparent
        };
        toggle = new ToggleSwitch(_theme) { Location = new Point(parent.Width - 32 - 48 - 16, y + 1) };
        parent.Controls.Add(lbl);
        parent.Controls.Add(toggle);
        return y + 30;
    }

    private int AddChoiceRow(Panel parent, int y, string label, out ComboBox combo, params string[] items)
    {
        var lbl = new Label
        {
            Text = label, Location = new Point(4, y + 3), AutoSize = true,
            ForeColor = _theme.Text, BackColor = Color.Transparent
        };
        combo = new ComboBox
        {
            Location = new Point(parent.Width - 32 - 100 - 16, y), Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat
        };
        combo.Items.AddRange(items);
        parent.Controls.Add(lbl);
        parent.Controls.Add(combo);
        return y + 30;
    }

    private int AddFontRow(Panel parent, int y)
    {
        var lbl = new Label
        {
            Text = "Font", Location = new Point(4, y + 3), AutoSize = true,
            ForeColor = _theme.Text, BackColor = Color.Transparent
        };
        _fontNameCombo = new ComboBox
        {
            Location = new Point(parent.Width - 32 - 160 - 80 - 16, y), Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat
        };
        _fontNameCombo.Items.AddRange(new[] { "Consolas", "Courier New", "Lucida Console", "Menlo",
            "Monaco", "Source Code Pro", "Fira Code", "Cascadia Code", "JetBrains Mono",
            "DejaVu Sans Mono", "Inconsolata" });

        _fontSizeUpDown = new NumericUpDown
        {
            Location = new Point(parent.Width - 32 - 80 - 16, y), Width = 60, Minimum = 6, Maximum = 72,
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text
        };
        parent.Controls.Add(lbl);
        parent.Controls.Add(_fontNameCombo);
        parent.Controls.Add(_fontSizeUpDown);
        return y + 30;
    }

    private int AddThemeRow(Panel parent, int y)
    {
        var lbl = new Label
        {
            Text = "Theme", Location = new Point(4, y + 3), AutoSize = true,
            ForeColor = _theme.Text, BackColor = Color.Transparent
        };
        _themeCombo = new ComboBox
        {
            Location = new Point(parent.Width - 32 - 180 - 16, y), Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text, FlatStyle = FlatStyle.Flat
        };
        _themeCombo.Items.AddRange(ThemeManager.ThemeNames);
        parent.Controls.Add(lbl);
        parent.Controls.Add(_themeCombo);
        return y + 30;
    }

    private int AddShellRow(Panel parent, int y)
    {
        var lbl = new Label
        {
            Text = "Terminal Shell", Location = new Point(4, y + 3), AutoSize = true,
            ForeColor = _theme.Text, BackColor = Color.Transparent
        };
        _terminalShellText = new TextBox
        {
            Location = new Point(parent.Width - 32 - 200 - 16, y), Width = 200,
            BackColor = _theme.EditorBackground, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle
        };
        parent.Controls.Add(lbl);
        parent.Controls.Add(_terminalShellText);
        return y + 30;
    }

    private void LoadCurrentValues()
    {
        _togStatusBar.Checked = _mainForm.CurrentStatusBarVisible;
        _togWordWrap.Checked = _mainForm.CurrentWordWrap;
        _togSyntaxHighlight.Checked = _mainForm.CurrentSyntaxHighlighting;
        _lineHighlightCombo.Text = _mainForm.CurrentLineHighlightName;
        _togInsertSpaces.Checked = _mainForm.CurrentInsertSpaces;
        _tabSizeCombo.Text = _mainForm.CurrentTabSize.ToString();
        _togAutoIndent.Checked = _mainForm.CurrentAutoIndent;
        _togSmartTabs.Checked = _mainForm.CurrentSmartTabs;
        _togElasticTabs.Checked = _mainForm.CurrentElasticTabs;
        _togColumnGuide.Checked = _mainForm.CurrentShowGuide;
        _guideColumnCombo.Text = _mainForm.CurrentGuideColumn.ToString();
        _fontNameCombo.Text = _mainForm.CurrentFontName;
        _fontSizeUpDown.Value = (decimal)_mainForm.CurrentFontSize;
        _togGutter.Checked = _mainForm.CurrentGutterVisible;
        _togShowWhitespace.Checked = _mainForm.CurrentShowWhitespace;
        _togMinimap.Checked = _mainForm.CurrentMinimapVisible;
        _togStickyScroll.Checked = _mainForm.CurrentStickyScroll;
        _togRainbowBrackets.Checked = _mainForm.CurrentRainbowBrackets;
        _togBreadcrumbs.Checked = _mainForm.CurrentBreadcrumbs;
        _themeCombo.Text = _mainForm.CurrentThemeName;
        _togVimMode.Checked = _mainForm.CurrentVimMode;
        _togAutoSave.Checked = _mainForm.CurrentAutoSave;
        _togAnalyzers.Checked = _mainForm.CurrentAnalyzersEnabled;
        _terminalShellText.Text = _mainForm.CurrentTerminalShell;
    }

    private void Ok_Click(object? sender, EventArgs e)
    {
        _mainForm.ApplySettings(
            fontName: _fontNameCombo.Text,
            fontSize: (float)_fontSizeUpDown.Value,
            tabSize: int.TryParse(_tabSizeCombo.Text, out int ts) ? ts : 4,
            insertSpaces: _togInsertSpaces.Checked,
            wordWrap: _togWordWrap.Checked,
            showGuide: _togColumnGuide.Checked,
            guideColumn: int.TryParse(_guideColumnCombo.Text, out int gc) ? gc : 80,
            themeName: _themeCombo.Text,
            gutterVisible: _togGutter.Checked,
            statusBarVisible: _togStatusBar.Checked,
            minimapVisible: _togMinimap.Checked,
            showWhitespace: _togShowWhitespace.Checked,
            lineHighlightMode: _lineHighlightCombo.Text,
            syntaxHighlighting: _togSyntaxHighlight.Checked,
            autoIndent: _togAutoIndent.Checked,
            smartTabs: _togSmartTabs.Checked,
            elasticTabs: _togElasticTabs.Checked,
            rainbowBrackets: _togRainbowBrackets.Checked,
            breadcrumbs: _togBreadcrumbs.Checked,
            autoSave: _togAutoSave.Checked,
            workspaceVisible: true,
            symbolPanelVisible: false,
            problemsPanelVisible: false,
            terminalVisible: true,
            terminalHeight: 200,
            analyzersEnabled: _togAnalyzers.Checked,
            terminalShell: _terminalShellText.Text,
            vimMode: _togVimMode.Checked,
            stickyScroll: _togStickyScroll.Checked
        );
        Close();
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
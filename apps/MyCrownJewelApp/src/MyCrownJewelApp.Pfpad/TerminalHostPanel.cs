using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class TerminalHostPanel : UserControl
{
    private sealed record CachedTerminalSettings(
        string? ShellPath,
        Theme Theme,
        string FontFace,
        float FontSize,
        bool FontBold,
        bool WordWrap,
        bool ScrollbarVisible,
        int Padding,
        int MaxScrollback,
        TerminalPanel.SecuritySettings Security,
        string? StartDir);

    private readonly Panel _tabStrip;
    private readonly FlowLayoutPanel _tabsPanel;
    private readonly FlowLayoutPanel _actionsPanel;
    private readonly Panel _content;
    private readonly Button _stopBtn;
    private readonly Button _copyBtn;
    private readonly Button _clearBtn;
    private readonly Button _newTabBtn;
    private readonly List<TerminalPanel> _terminals = new();
    private readonly Dictionary<TerminalPanel, Panel> _terminalTabButtons = new();
    private readonly Dictionary<Panel, Button> _tabTextButtons = new();
    private readonly Dictionary<Panel, Button> _tabCloseButtons = new();
    private readonly BuildOutputPanel _buildOutput;
    private Theme _theme;
    private Panel? _buildOutputTab;
    private Panel? _activeTab;
    private Control? _activeContent;
    private TerminalPanel? _activeTerminal;
    private CachedTerminalSettings? _cachedTerminalSettings;
    private string? _customTabTitle;

    public event Action<int>? TabCountChanged;
    public event Action<TerminalPanel>? TabAdded;
    public event Action? NewTabRequested;

    public TerminalPanel? ActiveTerminal => _activeTerminal;
    public IReadOnlyList<TerminalPanel> AllTerminals => _terminals;
    public BuildOutputPanel BuildOutput => _buildOutput;
    public bool IsBuildOutputVisible => _buildOutputTab != null;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CustomTabTitle
    {
        get => _customTabTitle;
        set
        {
            _customTabTitle = value;
            foreach (var terminal in _terminals)
                terminal.CustomTabTitle = value;
            RefreshTabTitles();
        }
    }

    public TerminalHostPanel()
    {
        _theme = ThemeManager.Instance.CurrentTheme;
        Dock = DockStyle.Fill;
        BackColor = _theme.EditorBackground;

        _tabStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = _theme.TerminalHeaderBackground
        };
        _tabStrip.Paint += TabStrip_Paint;

        _actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            Height = 30,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(4, 3, 4, 3),
            Margin = Padding.Empty,
            BackColor = _theme.TerminalHeaderBackground
        };

        _tabsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = _theme.TerminalHeaderBackground
        };

        _stopBtn = CreateActionButton("■ Stop", "Interrupt the active terminal");
        _copyBtn = CreateActionButton("⧉ Copy", "Copy terminal selection or output");
        _clearBtn = CreateActionButton("⌫ Clear", "Clear terminal or build output");
        _newTabBtn = CreateActionButton("+ New", "Open a new terminal tab");

        _content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = _theme.EditorBackground
        };

        _buildOutput = new BuildOutputPanel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _content.Controls.Add(_buildOutput);

        _stopBtn.Click += (_, _) => _activeTerminal?.SendCtrlC();
        _copyBtn.Click += (_, _) => _activeTerminal?.CopyOutputSelection();
        _clearBtn.Click += (_, _) =>
        {
            if (_activeTerminal != null)
                _activeTerminal.ClearOutput();
            else if (_activeContent == _buildOutput)
                _buildOutput.Clear();
        };
        _newTabBtn.Click += (_, _) => HandleNewTabRequested();

        _actionsPanel.Controls.Add(_newTabBtn);
        _actionsPanel.Controls.Add(_clearBtn);
        _actionsPanel.Controls.Add(_copyBtn);
        _actionsPanel.Controls.Add(_stopBtn);

        _tabStrip.Controls.Add(_tabsPanel);
        _tabStrip.Controls.Add(_actionsPanel);

        Controls.Add(_content);
        Controls.Add(_tabStrip);

        SetTheme(_theme);
        UpdateActionButtons();
    }

    public TerminalPanel AddTerminalTab(string? shellPath, Theme theme,
        string fontFace, float fontSize, bool fontBold, bool wordWrap,
        bool scrollbarVisible, int padding, int maxScrollback,
        TerminalPanel.SecuritySettings security, string? startDir)
    {
        _cachedTerminalSettings = new CachedTerminalSettings(
            shellPath, theme, fontFace, fontSize, fontBold, wordWrap,
            scrollbarVisible, padding, maxScrollback, security, startDir);

        var terminal = new TerminalPanel(shellPath)
        {
            Dock = DockStyle.Fill,
            Visible = false,
            CustomTabTitle = _customTabTitle,
            StartingDirectory = startDir ?? string.Empty
        };
        terminal.SetTheme(theme);
        terminal.ApplyTerminalSettings(fontFace, fontSize, fontBold, wordWrap, scrollbarVisible, padding);
        terminal.SetMaxScrollback(maxScrollback);
        terminal.ApplySecuritySettings(security);

        _terminals.Add(terminal);
        _content.Controls.Add(terminal);

        var tab = CreateTabButton(terminal, isBuildOutput: false);
        _terminalTabButtons[terminal] = tab;
        _tabsPanel.Controls.Add(tab);

        RefreshTabTitles();
        ActivateTerminal(terminal);
        TabAdded?.Invoke(terminal);
        TabCountChanged?.Invoke(_terminals.Count);
        return terminal;
    }

    public void CloseTerminalTab(TerminalPanel terminal)
    {
        if (!_terminalTabButtons.TryGetValue(terminal, out var tab))
            return;

        Control? nextContent = null;
        if (_activeTerminal == terminal)
            nextContent = GetNextTerminalContent(terminal);

        _terminalTabButtons.Remove(terminal);
        _tabTextButtons.Remove(tab);
        _tabCloseButtons.Remove(tab);
        _tabsPanel.Controls.Remove(tab);
        tab.Dispose();

        _terminals.Remove(terminal);
        _content.Controls.Remove(terminal);
        terminal.Kill();
        terminal.Dispose();

        RefreshTabTitles();

        if (_activeTerminal == terminal)
        {
            if (nextContent is TerminalPanel nextTerminal)
                ActivateTerminal(nextTerminal);
            else if (nextContent == _buildOutput)
                ShowBuildOutput();
            else
                DeactivateContent();
        }

        TabCountChanged?.Invoke(_terminals.Count);
        UpdateActionButtons();
    }

    public void ShowBuildOutput()
    {
        EnsureBuildOutputTab();
        ActivateBuildOutput();
    }

    public void HideBuildOutput()
    {
        if (_buildOutputTab == null)
            return;

        bool wasActive = _activeContent == _buildOutput;

        _tabsPanel.Controls.Remove(_buildOutputTab);
        _tabTextButtons.Remove(_buildOutputTab);
        _tabCloseButtons.Remove(_buildOutputTab);
        _buildOutputTab.Dispose();
        _buildOutputTab = null;
        _buildOutput.Visible = false;

        if (wasActive)
        {
            if (_terminals.Count > 0)
                ActivateTerminal(_terminals[0]);
            else
                DeactivateContent();
        }

        UpdateActionButtons();
        _tabStrip.Invalidate();
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.EditorBackground;
        _tabStrip.BackColor = theme.TerminalHeaderBackground;
        _tabsPanel.BackColor = theme.TerminalHeaderBackground;
        _actionsPanel.BackColor = theme.TerminalHeaderBackground;
        _content.BackColor = theme.EditorBackground;
        _buildOutput.SetTheme(theme);

        foreach (var terminal in _terminals)
            terminal.SetTheme(theme);

        foreach (var tab in _terminalTabButtons.Values)
            ApplyTabVisuals(tab, tab == _activeTab);

        if (_buildOutputTab != null)
            ApplyTabVisuals(_buildOutputTab, _buildOutputTab == _activeTab);

        UpdateActionButtonVisuals(_stopBtn, enabled: _activeTerminal != null, isStopButton: true);
        UpdateActionButtonVisuals(_copyBtn, enabled: _activeTerminal != null, isStopButton: false);
        UpdateActionButtonVisuals(_clearBtn, enabled: _activeContent != null, isStopButton: false);
        UpdateActionButtonVisuals(_newTabBtn, enabled: true, isStopButton: false);
        _tabStrip.Invalidate();
    }

    public void ApplyTerminalSettingsToAll(string fontFace, float fontSize, bool fontBold,
        bool wordWrap, bool scrollbarVisible, int padding)
    {
        if (_cachedTerminalSettings is { } cached)
        {
            _cachedTerminalSettings = cached with
            {
                FontFace = fontFace,
                FontSize = fontSize,
                FontBold = fontBold,
                WordWrap = wordWrap,
                ScrollbarVisible = scrollbarVisible,
                Padding = padding
            };
        }

        foreach (var terminal in _terminals)
            terminal.ApplyTerminalSettings(fontFace, fontSize, fontBold, wordWrap, scrollbarVisible, padding);
    }

    public void ApplySecuritySettingsToAll(TerminalPanel.SecuritySettings settings)
    {
        if (_cachedTerminalSettings is { } cached)
            _cachedTerminalSettings = cached with { Security = settings };

        foreach (var terminal in _terminals)
            terminal.ApplySecuritySettings(settings);
    }

    public void SetMaxScrollbackAll(int lines)
    {
        if (_cachedTerminalSettings is { } cached)
            _cachedTerminalSettings = cached with { MaxScrollback = lines };

        foreach (var terminal in _terminals)
            terminal.SetMaxScrollback(lines);
    }

    public void RefreshTabTitles()
    {
        foreach (var terminal in _terminals)
        {
            if (_terminalTabButtons.TryGetValue(terminal, out var tab))
                UpdateTabText(tab, GetTerminalTabTitle(terminal));
        }

        if (_buildOutputTab != null)
            UpdateTabText(_buildOutputTab, "📋 Output");

        _tabStrip.Invalidate();
    }

    public void StartAllVisible()
    {
        foreach (var terminal in _terminals)
            terminal.Start();
    }

    public void FocusActiveTerminal()
    {
        _activeTerminal?.FocusInput();
    }

    private void HandleNewTabRequested()
    {
        var handler = NewTabRequested;
        handler?.Invoke();
        if (handler != null || _cachedTerminalSettings == null)
            return;

        var settings = _cachedTerminalSettings;
        AddTerminalTab(settings.ShellPath, _theme,
            settings.FontFace, settings.FontSize, settings.FontBold, settings.WordWrap,
            settings.ScrollbarVisible, settings.Padding, settings.MaxScrollback,
            settings.Security, settings.StartDir);
    }

    private void EnsureBuildOutputTab()
    {
        if (_buildOutputTab != null)
            return;

        _buildOutputTab = CreateTabButton(_buildOutput, isBuildOutput: true);
        UpdateTabText(_buildOutputTab, "📋 Output");
        _tabsPanel.Controls.Add(_buildOutputTab);
    }

    private Control? GetNextTerminalContent(TerminalPanel closingTerminal)
    {
        int index = _terminals.IndexOf(closingTerminal);
        if (_terminals.Count > 1)
        {
            int nextIndex = index >= _terminals.Count - 1 ? index - 1 : index + 1;
            if (nextIndex >= 0 && nextIndex < _terminals.Count)
                return _terminals[nextIndex];
        }

        return _buildOutputTab != null ? _buildOutput : null;
    }

    private void ActivateTerminal(TerminalPanel terminal)
    {
        if (_terminalTabButtons.TryGetValue(terminal, out var tab))
            ActivateContent(terminal, tab);
    }

    private void ActivateBuildOutput()
    {
        if (_buildOutputTab != null)
            ActivateContent(_buildOutput, _buildOutputTab);
    }

    private void ActivateContent(Control content, Panel tab)
    {
        _activeContent = content;
        _activeTerminal = content as TerminalPanel;
        _activeTab = tab;

        foreach (var terminal in _terminals)
            terminal.Visible = terminal == _activeTerminal;

        _buildOutput.Visible = ReferenceEquals(content, _buildOutput);
        content.BringToFront();
        ApplyAllTabVisuals();
        UpdateActionButtons();
        _tabStrip.Invalidate();
    }

    private void DeactivateContent()
    {
        _activeContent = null;
        _activeTerminal = null;
        _activeTab = null;

        foreach (var terminal in _terminals)
            terminal.Visible = false;

        _buildOutput.Visible = false;
        ApplyAllTabVisuals();
        UpdateActionButtons();
        _tabStrip.Invalidate();
    }

    private Panel CreateTabButton(object tag, bool isBuildOutput)
    {
        var tab = new Panel
        {
            Height = 30,
            Width = MeasureTabWidth(isBuildOutput ? "📋 Output" : "🖥 Terminal"),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Tag = tag,
            BackColor = _theme.TerminalHeaderBackground
        };

        var textButton = new Button
        {
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            UseVisualStyleBackColor = false,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Margin = Padding.Empty,
            TabStop = false,
            Cursor = Cursors.Hand,
            BackColor = _theme.TerminalHeaderBackground
        };

        var closeButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 24,
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            UseVisualStyleBackColor = false,
            Font = new Font("Segoe UI", 9f),
            Margin = Padding.Empty,
            TabStop = false,
            Cursor = Cursors.Hand,
            BackColor = _theme.TerminalHeaderBackground
        };

        textButton.Click += (_, _) =>
        {
            if (isBuildOutput)
                ActivateBuildOutput();
            else if (tag is TerminalPanel terminal)
                ActivateTerminal(terminal);
        };
        closeButton.Click += (_, _) =>
        {
            if (isBuildOutput)
                HideBuildOutput();
            else if (tag is TerminalPanel terminal)
                CloseTerminalTab(terminal);
        };

        foreach (var control in new Control[] { tab, textButton, closeButton })
        {
            control.MouseEnter += (_, _) => ApplyHoverVisuals(tab);
            control.MouseLeave += (_, _) => ApplyTabVisuals(tab, tab == _activeTab);
        }

        tab.Controls.Add(textButton);
        tab.Controls.Add(closeButton);
        _tabTextButtons[tab] = textButton;
        _tabCloseButtons[tab] = closeButton;

        // Draw 2px accent bar at the bottom when this tab is active.
        // Must be on the tab Panel itself — painting on _tabStrip is hidden by _tabsPanel (Dock=Fill).
        tab.Paint += (_, e) =>
        {
            if (tab != _activeTab) return;
            using var pen = new Pen(_theme.Accent, 2);
            e.Graphics.DrawLine(pen, 0, tab.Height - 1, tab.Width, tab.Height - 1);
        };

        ApplyTabVisuals(tab, active: false);
        return tab;
    }

    private Button CreateActionButton(string text, string toolTip)
    {
        var button = new Button
        {
            AutoSize = true,
            Height = 24,
            MinimumSize = new Size(58, 24),
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(4, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            UseVisualStyleBackColor = false,
            Font = new Font("Segoe UI", 8.5f),
            Text = text,
            TabStop = false,
            Cursor = Cursors.Hand,
            BackColor = _theme.TerminalHeaderBackground,
            ForeColor = _theme.Text
        };
        button.MouseEnter += (_, _) =>
        {
            if (button.Enabled)
                button.BackColor = GetHoverColor(_theme.TerminalHeaderBackground);
        };
        button.MouseLeave += (_, _) =>
        {
            button.BackColor = _theme.TerminalHeaderBackground;
        };
        new ToolTip().SetToolTip(button, toolTip);
        return button;
    }

    private void ApplyAllTabVisuals()
    {
        foreach (var tab in _terminalTabButtons.Values)
            ApplyTabVisuals(tab, tab == _activeTab);

        if (_buildOutputTab != null)
            ApplyTabVisuals(_buildOutputTab, _buildOutputTab == _activeTab);
    }

    private void ApplyHoverVisuals(Panel tab)
    {
        Color baseColor = tab == _activeTab ? _theme.EditorBackground : _theme.TerminalHeaderBackground;
        Color hoverColor = GetHoverColor(baseColor);

        tab.BackColor = hoverColor;
        _tabTextButtons[tab].BackColor = hoverColor;
        _tabCloseButtons[tab].BackColor = hoverColor;
    }

    private void ApplyTabVisuals(Panel tab, bool active)
    {
        Color backColor = active ? _theme.EditorBackground : _theme.TerminalHeaderBackground;
        Color foreColor = active ? _theme.Text : _theme.Muted;
        FontStyle style = active ? FontStyle.Bold : FontStyle.Regular;

        tab.BackColor = backColor;
        _tabTextButtons[tab].BackColor = backColor;
        _tabTextButtons[tab].ForeColor = foreColor;
        _tabTextButtons[tab].Font = new Font("Segoe UI", 9f, style);
        _tabCloseButtons[tab].BackColor = backColor;
        _tabCloseButtons[tab].ForeColor = foreColor;
        tab.Invalidate(); // trigger Paint to redraw/clear the accent bar
    }

    private void UpdateActionButtons()
    {
        bool terminalActive = _activeTerminal != null;
        bool canClear = _activeContent != null;

        UpdateActionButtonVisuals(_stopBtn, terminalActive, isStopButton: true);
        UpdateActionButtonVisuals(_copyBtn, terminalActive, isStopButton: false);
        UpdateActionButtonVisuals(_clearBtn, canClear, isStopButton: false);
        UpdateActionButtonVisuals(_newTabBtn, true, isStopButton: false);
    }

    private void UpdateActionButtonVisuals(Button button, bool enabled, bool isStopButton)
    {
        button.Enabled = enabled;
        button.BackColor = _theme.TerminalHeaderBackground;
        button.ForeColor = !enabled
            ? _theme.Muted
            : isStopButton
                ? Color.FromArgb(200, 50, 50)
                : _theme.Text;
    }

    private void UpdateTabText(Panel tab, string text)
    {
        _tabTextButtons[tab].Text = text;
        tab.Width = MeasureTabWidth(text);
    }

    private string GetTerminalTabTitle(TerminalPanel terminal)
    {
        if (!string.IsNullOrWhiteSpace(terminal.CustomTabTitle))
            return $"🖥 {terminal.CustomTabTitle}";

        int index = _terminals.IndexOf(terminal) + 1;
        return $"🖥 Terminal {index}";
    }

    private int MeasureTabWidth(string text)
    {
        using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
        int width = TextRenderer.MeasureText(text, font).Width + 28;
        return Math.Max(120, Math.Min(240, width));
    }

    private Color GetHoverColor(Color baseColor) =>
        _theme.IsLight ? ControlPaint.Light(baseColor) : ControlPaint.LightLight(baseColor);

    private void TabStrip_Paint(object? sender, PaintEventArgs e)
    {
        // Accent underline is drawn per-tab in each tab Panel's Paint event.
        // Drawing here was hidden by _tabsPanel (Dock=Fill) covering the strip.
    }
}

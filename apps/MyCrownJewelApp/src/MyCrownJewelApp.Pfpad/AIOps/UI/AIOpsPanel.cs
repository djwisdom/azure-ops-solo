using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

internal sealed record AIOpsEventSubscription(object Source, EventInfo EventInfo, Delegate Handler);

internal static class AIOpsUiHelper
{
    public static Color SecondaryText(Theme theme) => theme.Muted;

    public static Color CurrentLineBackground(Theme theme) => theme.Highlight;

    public static Color Accent(Theme theme) => theme.KeywordColor;

    public static Color Comments(Theme theme) => theme.CommentColor;

    public static Color SeverityColor(Severity severity) => severity switch
    {
        Severity.Critical => Color.DarkRed,
        Severity.High => Color.IndianRed,
        Severity.Medium => Color.Goldenrod,
        Severity.Low => Color.Gray,
        _ => Color.SteelBlue
    };

    public static Color RiskColor(RiskLevel level) => level switch
    {
        RiskLevel.Critical => Color.DarkRed,
        RiskLevel.High => Color.Red,
        RiskLevel.Medium => Color.Goldenrod,
        _ => Color.ForestGreen
    };

    public static Color SloColor(SloStatus status) => status switch
    {
        SloStatus.Breached => Color.Red,
        SloStatus.AtRisk => Color.Goldenrod,
        _ => Color.ForestGreen
    };

    public static Color DeploymentStatusColor(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Failed => Color.Red,
        DeploymentStatus.RolledBack => Color.DarkOrange,
        DeploymentStatus.Running => Color.Goldenrod,
        DeploymentStatus.Pending => Color.Gray,
        _ => Color.ForestGreen
    };

    public static string StatusIcon(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Succeeded => "✅",
        DeploymentStatus.Failed => "❌",
        DeploymentStatus.Running => "⏳",
        DeploymentStatus.Pending => "⏸",
        DeploymentStatus.RolledBack => "↩",
        _ => "•"
    };

    public static string FormatAge(DateTimeOffset timestamp)
    {
        TimeSpan age = DateTimeOffset.UtcNow - timestamp;
        if (age.TotalDays >= 1)
            return $"{(int)age.TotalDays}d";
        if (age.TotalHours >= 1)
            return $"{(int)age.TotalHours}h";
        if (age.TotalMinutes >= 1)
            return $"{(int)age.TotalMinutes}m";
        return $"{Math.Max(0, (int)age.TotalSeconds)}s";
    }

    public static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "n/a";
        if (duration.Value.TotalHours >= 1)
            return duration.Value.ToString(@"h\:mm\:ss");
        return duration.Value.ToString(@"m\:ss");
    }

    public static void SafeBeginInvoke(Control control, Action action)
    {
        if (control.IsDisposed)
            return;

        if (control.IsHandleCreated)
        {
            try
            {
                control.BeginInvoke(action);
            }
            catch
            {
            }

            return;
        }

        if (!control.InvokeRequired)
            action();
    }

    public static Button CreateHeaderButton(string text, EventHandler onClick, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 }
        };
        button.Click += onClick;
        return button;
    }

    /// <summary>
    /// Creates a slim contextual hint bar (DockStyle.Top) that explains what powers a panel.
    /// Add it to Controls immediately before the header so it appears just below the title bar.
    /// Call SetHintBarTheme in SetTheme to keep colors in sync.
    /// </summary>
    public static (Panel bar, Label label) CreateHintBar(string hintText)
    {
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = hintText,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 4, 0)
        };
        var bar = new Panel { Dock = DockStyle.Top, Height = 20 };
        bar.Controls.Add(label);
        return (bar, label);
    }

    public static void SetHintBarTheme(Panel bar, Label label, Theme theme)
    {
        bar.BackColor = theme.MenuBackground;
        label.ForeColor = theme.Muted;
    }

    /// <summary>
    /// Adds a 2 px accent underline to a hand-rolled button tab strip.
    /// Call <c>strip.Invalidate()</c> whenever the active tab changes.
    /// </summary>
    public static void AttachTabStripAccent(Panel strip, Func<Button?> getActive, Func<Color> getAccent)
    {
        strip.Paint += (_, e) =>
        {
            var active = getActive();
            if (active == null) return;
            using var pen = new Pen(getAccent(), 2);
            int y = strip.Height - 2;
            e.Graphics.DrawLine(pen, active.Left, y, active.Right, y);
        };
    }

    /// <summary>
    /// Applies owner-draw to a TabControl so the selected tab gets the theme's
    /// accent-colour underline and inactive tabs use the muted text colour.
    /// </summary>
    public static void AttachAccentTabControl(TabControl tc, Func<Theme> getTheme)
    {
        tc.DrawMode = TabDrawMode.OwnerDrawFixed;
        tc.SizeMode = TabSizeMode.Normal;
        tc.DrawItem += (s, e) =>
        {
            var ctrl  = (TabControl)s!;
            var theme = getTheme();
            if (e.Index < 0 || e.Index >= ctrl.TabPages.Count) return;

            bool selected = e.Index == ctrl.SelectedIndex;
            var bounds = ctrl.GetTabRect(e.Index);

            // Background
            using (var bg = new SolidBrush(selected ? theme.PanelBackground : theme.MenuBackground))
                e.Graphics.FillRectangle(bg, bounds);

            // Label
            TextRenderer.DrawText(e.Graphics, ctrl.TabPages[e.Index].Text,
                new Font(ctrl.Font, selected ? FontStyle.Bold : FontStyle.Regular),
                new Rectangle(bounds.X + 4, bounds.Y + 2, bounds.Width - 8, bounds.Height - 4),
                selected ? theme.Text : theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // Accent underline
            if (selected)
            {
                using var pen = new Pen(theme.Accent, 2);
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 2, bounds.Right, bounds.Bottom - 2);
            }
        };
    }

    public static void ApplyListTheme(ListView listView, Theme theme)
    {
        listView.BackColor = theme.EditorBackground;
        listView.ForeColor = theme.Text;
        listView.BorderStyle = BorderStyle.None;
    }

    public static void ApplyTreeTheme(TreeView treeView, Theme theme)
    {
        treeView.BackColor = theme.EditorBackground;
        treeView.ForeColor = theme.Text;
        treeView.LineColor = theme.Muted;
    }

    public static void ApplyControlTheme(Control control, Theme theme)
    {
        switch (control)
        {
            case Form form:
                form.BackColor = theme.MenuBackground;
                form.ForeColor = theme.Text;
                break;
            case UserControl userControl:
                userControl.BackColor = theme.MenuBackground;
                userControl.ForeColor = theme.Text;
                break;
            case FlowLayoutPanel flow:
                flow.BackColor = theme.MenuBackground;
                flow.ForeColor = theme.Text;
                break;
            case TabPage tabPage:
                tabPage.BackColor = theme.MenuBackground;
                tabPage.ForeColor = theme.Text;
                break;
            case Panel panel:
                panel.BackColor = theme.MenuBackground;
                panel.ForeColor = theme.Text;
                break;
            case SplitContainer split:
                split.BackColor = theme.MenuBackground;
                split.ForeColor = theme.Text;
                break;
            case Label label:
                label.ForeColor = theme.Text;
                break;
            case CheckBox checkBox:
                checkBox.ForeColor = theme.Text;
                checkBox.BackColor = theme.MenuBackground;
                break;
            case TextBox textBox:
                textBox.BackColor = theme.EditorBackground;
                textBox.ForeColor = theme.Text;
                break;
            case RichTextBox richTextBox:
                richTextBox.BackColor = theme.EditorBackground;
                richTextBox.ForeColor = theme.Text;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = theme.EditorBackground;
                comboBox.ForeColor = theme.Text;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = theme.EditorBackground;
                numeric.ForeColor = theme.Text;
                break;
            case Button button:
                button.BackColor = theme.PanelBackground;
                button.ForeColor = theme.Text;
                button.FlatAppearance.BorderColor = theme.Border;
                break;
            case TabControl tabControl:
                tabControl.BackColor = theme.MenuBackground;
                tabControl.ForeColor = theme.Text;
                break;
            case ListView listView:
                ApplyListTheme(listView, theme);
                break;
            case TreeView treeView:
                ApplyTreeTheme(treeView, theme);
                break;
        }

        foreach (Control child in control.Controls)
            ApplyControlTheme(child, theme);
    }

    public static bool TrySubscribeEvent(object source, string eventName, Action<object?[]> callback, ICollection<AIOpsEventSubscription> subscriptions)
    {
        EventInfo? eventInfo = source.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (eventInfo?.EventHandlerType is null)
            return false;

        MethodInfo? invoke = eventInfo.EventHandlerType.GetMethod("Invoke");
        if (invoke is null)
            return false;

        ParameterExpression[] parameters = invoke
            .GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();

        NewArrayExpression argumentArray = Expression.NewArrayInit(
            typeof(object),
            parameters.Select(p => Expression.Convert(p, typeof(object))));

        MethodCallExpression body = Expression.Call(
            typeof(AIOpsUiHelper),
            nameof(DispatchEvent),
            null,
            Expression.Constant(callback),
            argumentArray);

        Delegate handler = Expression.Lambda(eventInfo.EventHandlerType, body, parameters).Compile();
        eventInfo.AddEventHandler(source, handler);
        subscriptions.Add(new AIOpsEventSubscription(source, eventInfo, handler));
        return true;
    }

    public static void ClearSubscriptions(ICollection<AIOpsEventSubscription> subscriptions)
    {
        foreach (AIOpsEventSubscription subscription in subscriptions.ToArray())
        {
            try
            {
                subscription.EventInfo.RemoveEventHandler(subscription.Source, subscription.Handler);
            }
            catch
            {
            }
        }

        subscriptions.Clear();
    }

    public static T? FindValue<T>(IEnumerable<object?> args) where T : class
    {
        foreach (object? value in ExpandValues(args))
        {
            if (value is T typed)
                return typed;
        }

        return null;
    }

    public static IReadOnlyList<T> FindList<T>(IEnumerable<object?> args)
    {
        foreach (object? value in ExpandValues(args))
        {
            switch (value)
            {
                case IReadOnlyList<T> typedList:
                    return typedList;
                case IEnumerable<T> enumerable:
                    return enumerable.ToList();
                case T typed:
                    return [typed];
            }
        }

        return Array.Empty<T>();
    }

    public static int SeverityRank(Severity severity) => severity switch
    {
        Severity.Critical => 5,
        Severity.High => 4,
        Severity.Medium => 3,
        Severity.Low => 2,
        _ => 1
    };

    public static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private static void DispatchEvent(Action<object?[]> callback, object?[] args) => callback(args);

    private static IEnumerable<object?> ExpandValues(IEnumerable<object?> args)
    {
        foreach (object? arg in args)
        {
            if (arg is null)
                continue;

            yield return arg;

            Type type = arg.GetType();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                object? value;
                try
                {
                    value = property.GetValue(arg);
                }
                catch
                {
                    continue;
                }

                if (value is not null)
                    yield return value;
            }
        }
    }
}

/// <summary>
/// Displays the AIOps dashboard hub.
/// </summary>
public sealed class AIOpsPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _settingsButton;
    private readonly Button _closeButton;
    private readonly FlowLayoutPanel _contentPanel;
    private readonly Panel _riskPanel;
    private readonly Label _riskLabel;
    private readonly Label _riskDetailLabel;
    private readonly Button _scanButton;
    private readonly Panel _incidentsPanel;
    private readonly Label _incidentsHeaderLabel;
    private readonly Button _incidentsToggleButton;
    private readonly ListView _incidentsList;
    private readonly Panel _slosPanel;
    private readonly Label _slosHeaderLabel;
    private readonly ListView _slosList;
    private readonly Panel _deploymentsPanel;
    private readonly Label _deploymentsHeaderLabel;
    private readonly ListView _deploymentsList;
    private readonly Panel _securityPanel;
    private readonly Label _securityHeaderLabel;
    private readonly ListView _securityList;
    private readonly Panel _footer;
    private readonly Label _footerLabel;
    private readonly Button _refreshAllButton;
    private readonly List<AIOpsEventSubscription> _subscriptions = [];
    private readonly Panel _hintBar;
    private readonly Label _hintLabel;

    private Theme _theme;
    private AIOpsEngine? _engine;
    private bool _incidentsExpanded = true;

    /// <summary>
    /// Raised when the panel should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Raised when settings should be opened.
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Raised when a scan is requested.
    /// </summary>
    public event Action? ScanRequested;

    /// <summary>
    /// Initializes a new dashboard panel.
    /// </summary>
    public AIOpsPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(320, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "⚡ AIOPS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _settingsButton = AIOpsUiHelper.CreateHeaderButton("⚙ Settings", (s, e) => SettingsRequested?.Invoke(), 92);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Resize += (s, e) => LayoutHeader();
        _header.Controls.AddRange([_titleLabel, _settingsButton, _closeButton]);

        _footer = new Panel { Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _footerLabel = new Label { AutoSize = true, Text = "Last refreshed: never", Location = new Point(6, 8) };
        _refreshAllButton = AIOpsUiHelper.CreateHeaderButton("Refresh All", (s, e) => ScanRequested?.Invoke(), 90);
        _refreshAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _footer.Resize += (s, e) => _refreshAllButton.Location = new Point(_footer.ClientSize.Width - _refreshAllButton.Width - 4, 4);
        _footer.Controls.AddRange([_footerLabel, _refreshAllButton]);

        _contentPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(6),
            Margin = Padding.Empty
        };
        _contentPanel.Resize += (s, e) => LayoutSections();

        _riskPanel = CreateSectionPanel(80);
        _riskLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold), Location = new Point(10, 10), Text = "RISK: -- / NOT CONFIGURED" };
        _riskDetailLabel = new Label { AutoSize = true, Location = new Point(12, 46), Text = "Connect an engine to view risk reports." };
        _scanButton = AIOpsUiHelper.CreateHeaderButton("↺ Scan Now", (s, e) => ScanRequested?.Invoke(), 92);
        _scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _riskPanel.Resize += (s, e) => _scanButton.Location = new Point(_riskPanel.ClientSize.Width - _scanButton.Width - 10, 40);
        _riskPanel.Controls.AddRange([_riskLabel, _riskDetailLabel, _scanButton]);

        _incidentsPanel = CreateSectionPanel(148);
        _incidentsHeaderLabel = CreateSectionHeader("🔴 ACTIVE INCIDENTS (0)");
        _incidentsToggleButton = AIOpsUiHelper.CreateHeaderButton("▾", (s, e) => ToggleIncidents(), 28);
        _incidentsToggleButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _incidentsList = CreateListView();
        _incidentsList.Columns.Add("Severity", 80);
        _incidentsList.Columns.Add("Title", 220);
        _incidentsList.Columns.Add("Service", 140);
        _incidentsList.Columns.Add("Age", 70);
        _incidentsList.Location = new Point(8, 30);
        _incidentsList.Size = new Size(500, 108);
        _incidentsList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _incidentsPanel.Resize += (s, e) =>
        {
            _incidentsToggleButton.Location = new Point(_incidentsPanel.ClientSize.Width - _incidentsToggleButton.Width - 8, 4);
            _incidentsList.Size = new Size(_incidentsPanel.ClientSize.Width - 16, _incidentsPanel.ClientSize.Height - 38);
        };
        _incidentsPanel.Controls.AddRange([_incidentsHeaderLabel, _incidentsToggleButton, _incidentsList]);

        _slosPanel = CreateSectionPanel(128);
        _slosHeaderLabel = CreateSectionHeader("📊 SLO STATUS");
        _slosList = CreateListView();
        _slosList.Columns.Add("Service", 140);
        _slosList.Columns.Add("SLO Name", 180);
        _slosList.Columns.Add("Current%", 90);
        _slosList.Columns.Add("Target%", 90);
        _slosList.Columns.Add("Status", 90);
        _slosList.Location = new Point(8, 30);
        _slosList.Size = new Size(500, 88);
        _slosList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _slosPanel.Resize += (s, e) => _slosList.Size = new Size(_slosPanel.ClientSize.Width - 16, _slosPanel.ClientSize.Height - 38);
        _slosPanel.Controls.AddRange([_slosHeaderLabel, _slosList]);

        _deploymentsPanel = CreateSectionPanel(128);
        _deploymentsHeaderLabel = CreateSectionHeader("🚀 RECENT DEPLOYMENTS");
        _deploymentsList = CreateListView();
        _deploymentsList.Columns.Add("Service", 140);
        _deploymentsList.Columns.Add("Env", 80);
        _deploymentsList.Columns.Add("Version", 120);
        _deploymentsList.Columns.Add("Status", 90);
        _deploymentsList.Columns.Add("Age", 70);
        _deploymentsList.Location = new Point(8, 30);
        _deploymentsList.Size = new Size(500, 88);
        _deploymentsList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _deploymentsPanel.Resize += (s, e) => _deploymentsList.Size = new Size(_deploymentsPanel.ClientSize.Width - 16, _deploymentsPanel.ClientSize.Height - 38);
        _deploymentsPanel.Controls.AddRange([_deploymentsHeaderLabel, _deploymentsList]);

        _securityPanel = CreateSectionPanel(128);
        _securityHeaderLabel = CreateSectionHeader("🔒 SECURITY FINDINGS (0)");
        _securityList = CreateListView();
        _securityList.Columns.Add("Severity", 80);
        _securityList.Columns.Add("File", 180);
        _securityList.Columns.Add("Rule", 100);
        _securityList.Columns.Add("Title", 240);
        _securityList.Location = new Point(8, 30);
        _securityList.Size = new Size(500, 88);
        _securityList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        _securityPanel.Resize += (s, e) => _securityList.Size = new Size(_securityPanel.ClientSize.Width - 16, _securityPanel.ClientSize.Height - 38);
        _securityPanel.Controls.AddRange([_securityHeaderLabel, _securityList]);

        _contentPanel.Controls.AddRange([_riskPanel, _incidentsPanel, _slosPanel, _deploymentsPanel, _securityPanel]);

        Controls.Add(_contentPanel);
        Controls.Add(_footer);
        (_hintBar, _hintLabel) = AIOpsUiHelper.CreateHintBar("ⓘ Aggregates all configured connectors. No connector? Enable Mock Data or configure one in AIOps > Settings (Ctrl+Alt+,).");
        Controls.Add(_hintBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeader();
        LayoutSections();
    }

    /// <summary>
    /// Wires engine events into the dashboard.
    /// </summary>
    public void SetEngine(AIOpsEngine engine)
    {
        _engine = engine;
        AIOpsUiHelper.ClearSubscriptions(_subscriptions);

        if (_engine is null)
        {
            UpdateRisk(null);
            UpdateIncidents(Array.Empty<Incident>());
            UpdateSlos(Array.Empty<Slo>());
            UpdateDeployments(Array.Empty<Deployment>());
            UpdateSecurityFindings(Array.Empty<SecurityFinding>());
            return;
        }

        AIOpsUiHelper.TrySubscribeEvent(engine, "IncidentsRefreshed", args => UpdateIncidents(AIOpsUiHelper.FindList<Incident>(args)), _subscriptions);
        AIOpsUiHelper.TrySubscribeEvent(engine, "SlosRefreshed", args => UpdateSlos(AIOpsUiHelper.FindList<Slo>(args)), _subscriptions);
        AIOpsUiHelper.TrySubscribeEvent(engine, "DeploymentsRefreshed", args => UpdateDeployments(AIOpsUiHelper.FindList<Deployment>(args)), _subscriptions);
        AIOpsUiHelper.TrySubscribeEvent(engine, "RiskReportGenerated", args => UpdateRisk(AIOpsUiHelper.FindValue<DeploymentRiskReport>(args)), _subscriptions);
        AIOpsUiHelper.TrySubscribeEvent(engine, "SecurityFindingsFound", args => UpdateSecurityFindings(AIOpsUiHelper.FindList<SecurityFinding>(args)), _subscriptions);
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        AIOpsUiHelper.SetHintBarTheme(_hintBar, _hintLabel, theme);        _footer.BackColor = theme.MenuBackground;
        _contentPanel.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _footerLabel.ForeColor = AIOpsUiHelper.SecondaryText(theme);

        foreach (Button button in new[] { _settingsButton, _closeButton, _scanButton, _incidentsToggleButton, _refreshAllButton })
        {
            button.BackColor = Color.Transparent;
            button.ForeColor = theme.Text;
        }

        foreach (Panel panel in new[] { _riskPanel, _incidentsPanel, _slosPanel, _deploymentsPanel, _securityPanel })
        {
            panel.BackColor = theme.MenuBackground;
        }

        foreach (Label label in new[] { _incidentsHeaderLabel, _slosHeaderLabel, _deploymentsHeaderLabel, _securityHeaderLabel, _riskDetailLabel })
            label.ForeColor = theme.Text;

        AIOpsUiHelper.ApplyListTheme(_incidentsList, theme);
        AIOpsUiHelper.ApplyListTheme(_slosList, theme);
        AIOpsUiHelper.ApplyListTheme(_deploymentsList, theme);
        AIOpsUiHelper.ApplyListTheme(_securityList, theme);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= SetTheme;
            AIOpsUiHelper.ClearSubscriptions(_subscriptions);
        }

        base.Dispose(disposing);
    }

    private static Panel CreateSectionPanel(int height)
    {
        Panel panel = new()
        {
            Height = height,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty,
            BorderStyle = BorderStyle.None
        };
        panel.Paint += (_, e) =>
        {
            Rectangle bounds = panel.ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            using Pen pen = new(ThemeManager.Instance.CurrentTheme.Border);
            e.Graphics.DrawRectangle(pen, bounds);
        };
        return panel;
    }

    private static Label CreateSectionHeader(string text) => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Location = new Point(8, 8),
        Text = text
    };

    private static ListView CreateListView() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        GridLines = false,
        HeaderStyle = ColumnHeaderStyle.Clickable,
        BorderStyle = BorderStyle.None
    };

    private void LayoutHeader()
    {
        _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _settingsButton.Location = new Point(_closeButton.Left - _settingsButton.Width - 4, 4);
    }

    private void LayoutSections()
    {
        int width = Math.Max(200, _contentPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 16);
        foreach (Control control in _contentPanel.Controls)
            control.Width = width;
    }

    private void ToggleIncidents()
    {
        _incidentsExpanded = !_incidentsExpanded;
        _incidentsList.Visible = _incidentsExpanded;
        _incidentsToggleButton.Text = _incidentsExpanded ? "▾" : "▸";
        _incidentsPanel.Height = _incidentsExpanded ? 148 : 34;
        LayoutSections();
    }

    private void UpdateRisk(DeploymentRiskReport? report)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            if (report is null)
            {
                _riskLabel.Text = "RISK: -- / NOT CONFIGURED";
                _riskLabel.ForeColor = _theme.Text;
                _riskDetailLabel.Text = _engine is null
                    ? "Connect an engine to view risk reports."
                    : "No risk report available yet.";
                return;
            }

            _riskLabel.Text = $"RISK: {report.Score} / {report.OverallRisk.ToString().ToUpperInvariant()}";
            _riskLabel.ForeColor = AIOpsUiHelper.RiskColor(report.OverallRisk);
            _riskDetailLabel.Text = $"Last scanned: {report.GeneratedAt.LocalDateTime:g} • {report.Factors.Count} factors • {report.Recommendations.Count} recommendations";
            UpdateLastRefreshed(report.GeneratedAt);
        });
    }

    private void UpdateIncidents(IReadOnlyList<Incident> incidents)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            _incidentsList.BeginUpdate();
            try
            {
                _incidentsList.Items.Clear();
                foreach (Incident incident in incidents.OrderByDescending(i => AIOpsUiHelper.SeverityRank(i.Severity)).ThenBy(i => i.StartedAt))
                {
                    var item = new ListViewItem(incident.Severity.ToString())
                    {
                        ForeColor = AIOpsUiHelper.SeverityColor(incident.Severity)
                    };
                    item.SubItems.Add(incident.Title);
                    item.SubItems.Add(incident.AffectedService ?? "-");
                    item.SubItems.Add(AIOpsUiHelper.FormatAge(incident.StartedAt));
                    _incidentsList.Items.Add(item);
                }
            }
            finally
            {
                _incidentsList.EndUpdate();
            }

            _incidentsHeaderLabel.Text = $"🔴 ACTIVE INCIDENTS ({incidents.Count})";
            UpdateLastRefreshed(DateTimeOffset.Now);
        });
    }

    private void UpdateSlos(IReadOnlyList<Slo> slos)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            _slosList.BeginUpdate();
            try
            {
                _slosList.Items.Clear();
                foreach (Slo slo in slos.OrderBy(s => s.ServiceName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var item = new ListViewItem(slo.ServiceName)
                    {
                        ForeColor = AIOpsUiHelper.SloColor(slo.Status)
                    };
                    item.SubItems.Add(slo.Name);
                    item.SubItems.Add($"{slo.CurrentPercent:0.##}%");
                    item.SubItems.Add($"{slo.TargetPercent:0.##}%");
                    item.SubItems.Add(slo.Status.ToString());
                    _slosList.Items.Add(item);
                }
            }
            finally
            {
                _slosList.EndUpdate();
            }

            UpdateLastRefreshed(DateTimeOffset.Now);
        });
    }

    private void UpdateDeployments(IReadOnlyList<Deployment> deployments)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            _deploymentsList.BeginUpdate();
            try
            {
                _deploymentsList.Items.Clear();
                foreach (Deployment deployment in deployments.OrderByDescending(d => d.DeployedAt))
                {
                    var item = new ListViewItem(deployment.Service)
                    {
                        ForeColor = AIOpsUiHelper.DeploymentStatusColor(deployment.Status)
                    };
                    item.SubItems.Add(deployment.Environment);
                    item.SubItems.Add(deployment.Version);
                    item.SubItems.Add(deployment.Status.ToString());
                    item.SubItems.Add(AIOpsUiHelper.FormatAge(deployment.DeployedAt));
                    _deploymentsList.Items.Add(item);
                }
            }
            finally
            {
                _deploymentsList.EndUpdate();
            }

            UpdateLastRefreshed(DateTimeOffset.Now);
        });
    }

    private void UpdateSecurityFindings(IReadOnlyList<SecurityFinding> findings)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            IReadOnlyList<SecurityFinding> topFindings = findings
                .OrderByDescending(f => AIOpsUiHelper.SeverityRank(f.Severity))
                .ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            _securityList.BeginUpdate();
            try
            {
                _securityList.Items.Clear();
                foreach (SecurityFinding finding in topFindings)
                {
                    var item = new ListViewItem(finding.Severity.ToString())
                    {
                        ForeColor = AIOpsUiHelper.SeverityColor(finding.Severity)
                    };
                    item.SubItems.Add(Path.GetFileName(finding.FilePath));
                    item.SubItems.Add(finding.RuleId);
                    item.SubItems.Add(finding.Title);
                    _securityList.Items.Add(item);
                }
            }
            finally
            {
                _securityList.EndUpdate();
            }

            _securityHeaderLabel.Text = $"🔒 SECURITY FINDINGS ({findings.Count})";
            UpdateLastRefreshed(DateTimeOffset.Now);
        });
    }

    private void UpdateLastRefreshed(DateTimeOffset timestamp) => _footerLabel.Text = $"Last refreshed: {timestamp.LocalDateTime:g}";
}

using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Displays live logs, metrics, and traces for a service.
/// </summary>
public sealed class TelemetryPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Label _serviceLabel;
    private readonly Button _refreshButton;
    private readonly Button _closeButton;
    private readonly Panel _filterBar;
    private readonly TextBox _serviceTextBox;
    private readonly ComboBox _timeRangeCombo;
    private readonly Button _queryButton;
    private readonly Panel _tabStrip;
    private readonly Button _logsTabButton;
    private readonly Button _metricsTabButton;
    private readonly Button _tracesTabButton;
    private readonly Panel _contentPanel;
    private readonly Panel _logsPanel;
    private readonly RichTextBox _logsBox;
    private readonly Panel _metricsPanel;
    private readonly ListView _metricsList;
    private readonly Panel _tracesPanel;
    private readonly TreeView _tracesTree;

    private Theme _theme;
    private IAIOpsConnector? _connector;
    private string _selectedTab = "Logs";

    /// <summary>
    /// Raised when the panel should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Initializes a new telemetry panel.
    /// </summary>
    public TelemetryPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(440, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "📡 TELEMETRY", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _serviceLabel = new Label { AutoSize = true, Text = "Service: Not configured", Location = new Point(110, 9) };
        _refreshButton = AIOpsUiHelper.CreateHeaderButton("Refresh", async (s, e) => await QueryAsync(), 68);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Resize += (s, e) =>
        {
            _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
            _refreshButton.Location = new Point(_closeButton.Left - _refreshButton.Width - 4, 4);
        };
        _header.Controls.AddRange([_titleLabel, _serviceLabel, _refreshButton, _closeButton]);

        _filterBar = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 4, 6, 4) };
        _serviceTextBox = new TextBox { Location = new Point(6, 5), Width = 180, PlaceholderText = "Service name" };
        _timeRangeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(194, 5), Width = 110 };
        _timeRangeCombo.Items.AddRange(["Last 1h", "Last 6h", "Last 24h", "Last 7d"]);
        _timeRangeCombo.SelectedIndex = 1;
        _queryButton = new Button { Text = "Query", Width = 70, Height = 24, Location = new Point(312, 4), FlatStyle = FlatStyle.Flat };
        _queryButton.Click += async (s, e) => await QueryAsync();
        _filterBar.Controls.AddRange([_serviceTextBox, _timeRangeCombo, _queryButton]);

        _tabStrip = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 5, 6, 5) };
        _logsTabButton = new Button { Text = "Logs", Width = 70, Height = 24, FlatStyle = FlatStyle.Flat, Location = new Point(6, 5) };
        _metricsTabButton = new Button { Text = "Metrics", Width = 70, Height = 24, FlatStyle = FlatStyle.Flat, Location = new Point(82, 5) };
        _tracesTabButton = new Button { Text = "Traces", Width = 70, Height = 24, FlatStyle = FlatStyle.Flat, Location = new Point(158, 5) };
        _logsTabButton.Click += (s, e) => ShowTab("Logs");
        _metricsTabButton.Click += (s, e) => ShowTab("Metrics");
        _tracesTabButton.Click += (s, e) => ShowTab("Traces");
        _tabStrip.Controls.AddRange([_logsTabButton, _metricsTabButton, _tracesTabButton]);

        _contentPanel = new Panel { Dock = DockStyle.Fill };

        _logsPanel = new Panel { Dock = DockStyle.Fill };
        _logsBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false, WordWrap = false };
        _logsPanel.Controls.Add(_logsBox);

        _metricsPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        _metricsList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
        _metricsList.Columns.Add("Metric Name", 180);
        _metricsList.Columns.Add("Value", 80);
        _metricsList.Columns.Add("Unit", 70);
        _metricsList.Columns.Add("Timestamp", 120);
        _metricsList.Columns.Add("Dimensions", 280);
        _metricsPanel.Controls.Add(_metricsList);

        _tracesPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        _tracesTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = TreeViewDrawMode.OwnerDrawText,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = false,
            ShowRootLines = false,
            ShowPlusMinus = true
        };
        _tracesTree.DrawNode += TracesTree_DrawNode;
        _tracesPanel.Controls.Add(_tracesTree);

        _contentPanel.Controls.Add(_logsPanel);
        _contentPanel.Controls.Add(_metricsPanel);
        _contentPanel.Controls.Add(_tracesPanel);

        Controls.Add(_contentPanel);
        Controls.Add(_tabStrip);
        Controls.Add(_filterBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        ShowNotConfigured();
    }

    /// <summary>
    /// Sets the active telemetry connector.
    /// </summary>
    public void SetConnector(IAIOpsConnector? connector)
    {
        _connector = connector;
        if (_connector is null)
            ShowNotConfigured();
    }

    /// <summary>
    /// Sets the service name used by telemetry queries.
    /// </summary>
    public void SetServiceName(string serviceName)
    {
        _serviceTextBox.Text = serviceName;
        _serviceLabel.Text = string.IsNullOrWhiteSpace(serviceName) ? "Service: Not configured" : $"Service: {serviceName}";
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        _filterBar.BackColor = theme.MenuBackground;
        _tabStrip.BackColor = theme.MenuBackground;
        _contentPanel.BackColor = theme.MenuBackground;
        _logsPanel.BackColor = theme.MenuBackground;
        _metricsPanel.BackColor = theme.MenuBackground;
        _tracesPanel.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _serviceLabel.ForeColor = AIOpsUiHelper.SecondaryText(theme);
        _refreshButton.BackColor = Color.Transparent;
        _refreshButton.ForeColor = theme.Text;
        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = theme.Text;
        _serviceTextBox.BackColor = theme.EditorBackground;
        _serviceTextBox.ForeColor = theme.Text;
        _timeRangeCombo.BackColor = theme.EditorBackground;
        _timeRangeCombo.ForeColor = theme.Text;
        _queryButton.BackColor = theme.PanelBackground;
        _queryButton.ForeColor = theme.Text;
        _queryButton.FlatAppearance.BorderColor = theme.Border;
        ApplyTabTheme();
        _logsBox.BackColor = theme.EditorBackground;
        _logsBox.ForeColor = theme.Text;
        AIOpsUiHelper.ApplyListTheme(_metricsList, theme);
        AIOpsUiHelper.ApplyTreeTheme(_tracesTree, theme);
        _tracesTree.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private async Task QueryAsync()
    {
        if (_connector is null)
        {
            ShowNotConfigured();
            return;
        }

        string serviceName = _serviceTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            ShowMessage("Enter a service name to query telemetry.");
            return;
        }

        TimeSpan range = ParseRange();
        DateTimeOffset to = DateTimeOffset.UtcNow;
        DateTimeOffset from = to - range;

        try
        {
            if (_connector.Status != ConnectorStatus.Connected)
                await _connector.ConnectAsync();

            IReadOnlyList<LogEntry> logs = await _connector.GetLogsAsync(serviceName, from, to, 200);
            IReadOnlyList<MetricPoint> metrics = await _connector.GetMetricsAsync(serviceName, string.Empty, from, to);

            List<TraceSpan> traces = [];
            IEnumerable<string> traceIds = logs
                .Select(l => l.TraceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10);

            foreach (string traceId in traceIds)
            {
                IReadOnlyList<TraceSpan> spans = await _connector.GetTracesAsync(traceId);
                traces.AddRange(spans);
            }

            AIOpsUiHelper.SafeBeginInvoke(this, () =>
            {
                RenderLogs(logs);
                RenderMetrics(metrics);
                RenderTraces(traces);
                _serviceLabel.Text = $"Service: {serviceName}";
            });
        }
        catch (Exception ex)
        {
            ShowMessage($"Telemetry query failed: {ex.Message}");
        }
    }

    private void ShowTab(string tabName)
    {
        _selectedTab = tabName;
        _logsPanel.Visible = string.Equals(tabName, "Logs", StringComparison.OrdinalIgnoreCase);
        _metricsPanel.Visible = string.Equals(tabName, "Metrics", StringComparison.OrdinalIgnoreCase);
        _tracesPanel.Visible = string.Equals(tabName, "Traces", StringComparison.OrdinalIgnoreCase);
        ApplyTabTheme();
    }

    private void ApplyTabTheme()
    {
        ApplyTabButton(_logsTabButton, _selectedTab == "Logs");
        ApplyTabButton(_metricsTabButton, _selectedTab == "Metrics");
        ApplyTabButton(_tracesTabButton, _selectedTab == "Traces");
    }

    private void ApplyTabButton(Button button, bool selected)
    {
        button.BackColor = selected ? AIOpsUiHelper.CurrentLineBackground(_theme) : _theme.PanelBackground;
        button.ForeColor = selected ? _theme.Text : AIOpsUiHelper.SecondaryText(_theme);
        button.FlatAppearance.BorderColor = _theme.Border;
    }

    private TimeSpan ParseRange() => _timeRangeCombo.SelectedItem as string switch
    {
        "Last 1h" => TimeSpan.FromHours(1),
        "Last 6h" => TimeSpan.FromHours(6),
        "Last 24h" => TimeSpan.FromHours(24),
        "Last 7d" => TimeSpan.FromDays(7),
        _ => TimeSpan.FromHours(6)
    };

    private void RenderLogs(IReadOnlyList<LogEntry> logs)
    {
        _logsBox.Clear();
        if (logs.Count == 0)
        {
            _logsBox.SelectionColor = AIOpsUiHelper.SecondaryText(_theme);
            _logsBox.AppendText("No logs returned.\n");
            return;
        }

        foreach (LogEntry log in logs.OrderBy(l => l.Timestamp))
        {
            _logsBox.SelectionColor = GetLogColor(log.Severity);
            _logsBox.AppendText($"[{log.Timestamp.LocalDateTime:HH:mm:ss}] [{log.Severity.ToString().ToUpperInvariant()}] {log.RedactedMessage}{Environment.NewLine}");
        }

        _logsBox.SelectionStart = _logsBox.TextLength;
        _logsBox.ScrollToCaret();
    }

    private void RenderMetrics(IReadOnlyList<MetricPoint> metrics)
    {
        _metricsList.BeginUpdate();
        try
        {
            _metricsList.Items.Clear();
            _metricsList.Groups.Clear();

            Dictionary<string, ListViewGroup> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach (MetricPoint metric in metrics.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(m => m.Timestamp))
            {
                if (!groups.TryGetValue(metric.Name, out ListViewGroup? group))
                {
                    group = new ListViewGroup(metric.Name, HorizontalAlignment.Left);
                    groups[metric.Name] = group;
                    _metricsList.Groups.Add(group);
                }

                string dimensions = metric.Dimensions is null || metric.Dimensions.Count == 0
                    ? "-"
                    : string.Join(", ", metric.Dimensions.Select(kv => $"{kv.Key}={kv.Value}"));

                var item = new ListViewItem(metric.Name, group);
                item.SubItems.Add(metric.Value.ToString("0.###"));
                item.SubItems.Add(metric.Unit);
                item.SubItems.Add(metric.Timestamp.LocalDateTime.ToString("g"));
                item.SubItems.Add(dimensions);
                _metricsList.Items.Add(item);
            }
        }
        finally
        {
            _metricsList.EndUpdate();
        }
    }

    private void RenderTraces(IReadOnlyList<TraceSpan> traces)
    {
        _tracesTree.BeginUpdate();
        try
        {
            _tracesTree.Nodes.Clear();
            if (traces.Count == 0)
            {
                _tracesTree.Nodes.Add(new TreeNode("No traces returned.") { ForeColor = AIOpsUiHelper.SecondaryText(_theme) });
                return;
            }

            foreach (IGrouping<string, TraceSpan> traceGroup in traces.GroupBy(t => t.TraceId).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode root = new(traceGroup.Key);
                Dictionary<string, List<TraceSpan>> children = traceGroup
                    .GroupBy(span => span.ParentSpanId ?? string.Empty)
                    .ToDictionary(group => group.Key, group => group.OrderBy(s => s.StartTime).ToList(), StringComparer.OrdinalIgnoreCase);

                if (children.TryGetValue(string.Empty, out List<TraceSpan>? roots))
                {
                    foreach (TraceSpan span in roots)
                        root.Nodes.Add(BuildTraceNode(span, children));
                }

                _tracesTree.Nodes.Add(root);
            }

            _tracesTree.ExpandAll();
        }
        finally
        {
            _tracesTree.EndUpdate();
        }
    }

    private TreeNode BuildTraceNode(TraceSpan span, IReadOnlyDictionary<string, List<TraceSpan>> children)
    {
        TreeNode node = new($"{span.ServiceName} / {span.OperationName} [{span.Duration.TotalMilliseconds:0}ms]")
        {
            ForeColor = span.IsError ? Color.IndianRed : _theme.Text
        };

        if (children.TryGetValue(span.SpanId, out List<TraceSpan>? nested))
        {
            foreach (TraceSpan child in nested)
                node.Nodes.Add(BuildTraceNode(child, children));
        }

        return node;
    }

    private void TracesTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        e.DrawDefault = false;
        Color backColor = e.Node.IsSelected ? AIOpsUiHelper.CurrentLineBackground(_theme) : _tracesTree.BackColor;
        using SolidBrush backBrush = new(backColor);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, _tracesTree.Font, e.Bounds, e.Node.ForeColor.IsEmpty ? _theme.Text : e.Node.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private Color GetLogColor(Severity severity) => severity switch
    {
        Severity.Critical or Severity.High => Color.IndianRed,
        Severity.Medium => Color.Goldenrod,
        Severity.Low => AIOpsUiHelper.SecondaryText(_theme),
        _ => _theme.Text
    };

    private void ShowNotConfigured() => ShowMessage("Not configured.");

    private void ShowMessage(string message)
    {
        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            _logsBox.Clear();
            _logsBox.SelectionColor = AIOpsUiHelper.SecondaryText(_theme);
            _logsBox.AppendText(message + Environment.NewLine);
            _metricsList.Items.Clear();
            _metricsList.Groups.Clear();
            _tracesTree.Nodes.Clear();
            _tracesTree.Nodes.Add(new TreeNode(message) { ForeColor = AIOpsUiHelper.SecondaryText(_theme) });
        });
    }
}

using System.Diagnostics;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class ServiceDependencyPanel : UserControl
{
    private enum ServiceHealthState { Unknown, Healthy, Degraded, Critical }

    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _refreshButton;
    private readonly TextBox _filterTextBox;
    private readonly Button _closeButton;
    private readonly SplitContainer _splitContainer;
    private readonly TreeView _servicesTree;
    private readonly ImageList _imageList;
    private readonly Label _serviceNameLabel;
    private readonly Label _teamLabel;
    private readonly Label _environmentLabel;
    private readonly LinkLabel _docsLinkLabel;
    private readonly ListBox _upstreamList;
    private readonly ListBox _downstreamList;
    private readonly Button _openDocsButton;

    private readonly Dictionary<string, ServiceHealthState> _healthStates = new(StringComparer.OrdinalIgnoreCase);
    private ServiceDependencyGraph? _graph;
    private Theme _theme;
    private readonly Panel _hintBar;
    private readonly Label _hintLabel;

    public event Action<string>? ServiceOpenRequested;
    public event Action? CloseRequested;

    public ServiceDependencyPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(460, 340);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🔗 DEPENDENCIES", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _refreshButton = AIOpsUiHelper.CreateHeaderButton("Refresh", (s, e) => RefreshTree(), 68);
        _filterTextBox = new TextBox { Width = 160, Height = 24, PlaceholderText = "Filter services", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _header.Resize += (_, _) => LayoutHeader();
        _filterTextBox.TextChanged += (_, _) => RefreshTree();
        _header.Controls.AddRange([_titleLabel, _refreshButton, _filterTextBox, _closeButton]);

        _splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 260, BorderStyle = BorderStyle.None };

        _imageList = new ImageList { ImageSize = new Size(12, 12), ColorDepth = ColorDepth.Depth32Bit };
        BuildImageList();
        _servicesTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = false,
            ShowRootLines = false,
            ShowPlusMinus = true,
            ImageList = _imageList
        };
        _servicesTree.AfterSelect += ServicesTree_AfterSelect;
        _servicesTree.NodeMouseDoubleClick += ServicesTree_NodeMouseDoubleClick;
        _splitContainer.Panel1.Controls.Add(_servicesTree);

        Panel detailsPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(10) };
        _serviceNameLabel = new Label { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
        _teamLabel = new Label { Dock = DockStyle.Top, Height = 20 };
        _environmentLabel = new Label { Dock = DockStyle.Top, Height = 20 };
        _docsLinkLabel = new LinkLabel { Dock = DockStyle.Top, Height = 20 };
        _docsLinkLabel.LinkClicked += (_, _) => AIOpsUiHelper.OpenUrl(_docsLinkLabel.Tag as string);
        Label upstreamLabel = new() { Dock = DockStyle.Top, Height = 18, Text = "Upstream", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(0, 8, 0, 0) };
        _upstreamList = new ListBox { Dock = DockStyle.Top, Height = 110, IntegralHeight = false, BorderStyle = BorderStyle.None };
        Label downstreamLabel = new() { Dock = DockStyle.Top, Height = 18, Text = "Downstream", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(0, 8, 0, 0) };
        _downstreamList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
        _openDocsButton = new Button { Dock = DockStyle.Bottom, Height = 30, Text = "Open Docs", FlatStyle = FlatStyle.Flat };
        _openDocsButton.Click += (_, _) => AIOpsUiHelper.OpenUrl(_docsLinkLabel.Tag as string);

        detailsPanel.Controls.Add(_downstreamList);
        detailsPanel.Controls.Add(downstreamLabel);
        detailsPanel.Controls.Add(_upstreamList);
        detailsPanel.Controls.Add(upstreamLabel);
        detailsPanel.Controls.Add(_docsLinkLabel);
        detailsPanel.Controls.Add(_environmentLabel);
        detailsPanel.Controls.Add(_teamLabel);
        detailsPanel.Controls.Add(_serviceNameLabel);
        detailsPanel.Controls.Add(_openDocsButton);
        _splitContainer.Panel2.Controls.Add(detailsPanel);

        Controls.Add(_splitContainer);
        (_hintBar, _hintLabel) = AIOpsUiHelper.CreateHintBar("ⓘ Dependency graph from Azure Monitor · Kubernetes metrics. Configure connectors in AIOps > Settings (Ctrl+Alt+,).");
        Controls.Add(_hintBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeader();
        ShowServiceDetails(null);
    }

    public void SetGraph(ServiceDependencyGraph graph)
    {
        _graph = graph;
        RefreshTree();
    }

    public void SetServiceHealth(string serviceName, ServiceHealth health)
    {
        _healthStates[serviceName] = EvaluateHealth(health);
        RefreshTree();
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        _header.BackColor = theme.MenuBackground;
        AIOpsUiHelper.SetHintBarTheme(_hintBar, _hintLabel, theme);
        _titleLabel.ForeColor = theme.Text;
        _filterTextBox.BackColor = theme.EditorBackground;
        _filterTextBox.ForeColor = theme.Text;
        _refreshButton.BackColor = Color.Transparent;
        _closeButton.BackColor = Color.Transparent;
        _refreshButton.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        _openDocsButton.BackColor = theme.PanelBackground;
        _openDocsButton.ForeColor = theme.Text;
        _openDocsButton.FlatAppearance.BorderColor = theme.Border;
        _docsLinkLabel.LinkColor = AIOpsUiHelper.Accent(theme);
        _docsLinkLabel.ActiveLinkColor = AIOpsUiHelper.Accent(theme);
        _docsLinkLabel.VisitedLinkColor = AIOpsUiHelper.Accent(theme);
        _upstreamList.ApplyTheme(theme);
        _downstreamList.ApplyTheme(theme);
        AIOpsUiHelper.ApplyTreeTheme(_servicesTree, theme);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= SetTheme;
            _imageList.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LayoutHeader()
    {
        _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _filterTextBox.Location = new Point(_closeButton.Left - _filterTextBox.Width - 4, 4);
        _refreshButton.Location = new Point(_filterTextBox.Left - _refreshButton.Width - 4, 4);
    }

    private void RefreshTree()
    {
        string filter = _filterTextBox.Text.Trim();
        string? selectedService = GetSelectedServiceName();
        _servicesTree.BeginUpdate();
        try
        {
            _servicesTree.Nodes.Clear();
            if (_graph is null)
            {
                _servicesTree.Nodes.Add(new TreeNode("No dependency graph loaded") { ForeColor = _theme.Muted, ImageKey = nameof(ServiceHealthState.Unknown), SelectedImageKey = nameof(ServiceHealthState.Unknown) });
                return;
            }

            foreach (ServiceEntity service in _graph.Services.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!MatchesFilter(service, filter))
                    continue;

                TreeNode serviceNode = CreateServiceNode(service.Name, service);
                IEnumerable<ServiceDependency> dependencies = _graph.Dependencies
                    .Where(d => string.Equals(d.FromService, service.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d.ToService, StringComparer.OrdinalIgnoreCase);
                foreach (ServiceDependency dependency in dependencies)
                {
                    ServiceEntity? target = _graph.Services.FirstOrDefault(s => string.Equals(s.Name, dependency.ToService, StringComparison.OrdinalIgnoreCase));
                    object childTag = target is not null ? target : dependency.ToService;
                    TreeNode child = CreateServiceNode($"{dependency.ToService} [{dependency.Type}]", childTag);
                    serviceNode.Nodes.Add(child);
                }
                _servicesTree.Nodes.Add(serviceNode);
            }
        }
        finally
        {
            _servicesTree.EndUpdate();
        }

        _servicesTree.ExpandAll();
        if (!string.IsNullOrWhiteSpace(selectedService))
            SelectTreeNodeByService(selectedService);
        if (_servicesTree.SelectedNode is null && _servicesTree.Nodes.Count > 0)
            _servicesTree.SelectedNode = _servicesTree.Nodes[0];
        if (_servicesTree.SelectedNode is null)
            ShowServiceDetails(null);
    }

    private TreeNode CreateServiceNode(string text, object tag)
    {
        string serviceName = tag switch
        {
            ServiceEntity entity => entity.Name,
            string name => name,
            _ => text
        };
        string imageKey = GetImageKey(serviceName);
        return new TreeNode(text) { Tag = tag, ImageKey = imageKey, SelectedImageKey = imageKey };
    }

    private void ServicesTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        ShowServiceDetails(ResolveService(e.Node?.Tag));
    }

    private void ServicesTree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        ServiceEntity? service = ResolveService(e.Node?.Tag);
        if (service is not null)
            ServiceOpenRequested?.Invoke(service.Name);
    }

    private string? GetSelectedServiceName() => ResolveService(_servicesTree.SelectedNode?.Tag)?.Name;

    private void SelectTreeNodeByService(string serviceName)
    {
        foreach (TreeNode node in _servicesTree.Nodes)
        {
            if (SelectTreeNodeRecursive(node, serviceName))
                return;
        }
    }

    private bool SelectTreeNodeRecursive(TreeNode node, string serviceName)
    {
        ServiceEntity? service = ResolveService(node.Tag);
        if (service is not null && string.Equals(service.Name, serviceName, StringComparison.OrdinalIgnoreCase))
        {
            _servicesTree.SelectedNode = node;
            return true;
        }

        foreach (TreeNode child in node.Nodes)
        {
            if (SelectTreeNodeRecursive(child, serviceName))
                return true;
        }

        return false;
    }

    private bool MatchesFilter(ServiceEntity service, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        if (service.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return _graph?.Dependencies.Any(d => string.Equals(d.FromService, service.Name, StringComparison.OrdinalIgnoreCase)
            && d.ToService.Contains(filter, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private ServiceEntity? ResolveService(object? tag)
    {
        if (_graph is null)
            return null;

        return tag switch
        {
            ServiceEntity entity => entity,
            string name => _graph.Services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private void ShowServiceDetails(ServiceEntity? service)
    {
        if (_graph is null || service is null)
        {
            _serviceNameLabel.Text = "Select a service";
            _teamLabel.Text = string.Empty;
            _environmentLabel.Text = string.Empty;
            _docsLinkLabel.Text = string.Empty;
            _docsLinkLabel.Tag = null;
            _upstreamList.Items.Clear();
            _downstreamList.Items.Clear();
            _openDocsButton.Enabled = false;
            return;
        }

        _serviceNameLabel.Text = service.Name;
        _teamLabel.Text = $"Team: {service.Team ?? "Unknown"}";
        _environmentLabel.Text = $"Environment: {service.Environment ?? "Unknown"}";
        _docsLinkLabel.Text = string.IsNullOrWhiteSpace(service.DocumentationUrl) ? "Documentation: n/a" : service.DocumentationUrl;
        _docsLinkLabel.Tag = service.DocumentationUrl;
        _openDocsButton.Enabled = !string.IsNullOrWhiteSpace(service.DocumentationUrl);

        IEnumerable<string> upstream = _graph.Dependencies
            .Where(d => string.Equals(d.ToService, service.Name, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.FromService)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> downstream = _graph.Dependencies
            .Where(d => string.Equals(d.FromService, service.Name, StringComparison.OrdinalIgnoreCase))
            .Select(d => $"{d.ToService} [{d.Type}]")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        _upstreamList.BeginUpdate();
        _downstreamList.BeginUpdate();
        try
        {
            _upstreamList.Items.Clear();
            _downstreamList.Items.Clear();
            foreach (string item in upstream)
                _upstreamList.Items.Add(item);
            foreach (string item in downstream)
                _downstreamList.Items.Add(item);
        }
        finally
        {
            _upstreamList.EndUpdate();
            _downstreamList.EndUpdate();
        }
    }

    private void BuildImageList()
    {
        _imageList.Images.Clear();
        _imageList.Images.Add(nameof(ServiceHealthState.Unknown), CreateDotBitmap(Color.Gray));
        _imageList.Images.Add(nameof(ServiceHealthState.Healthy), CreateDotBitmap(Color.ForestGreen));
        _imageList.Images.Add(nameof(ServiceHealthState.Degraded), CreateDotBitmap(Color.Goldenrod));
        _imageList.Images.Add(nameof(ServiceHealthState.Critical), CreateDotBitmap(Color.IndianRed));
    }

    private static Bitmap CreateDotBitmap(Color color)
    {
        Bitmap bitmap = new(12, 12);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using SolidBrush brush = new(color);
        graphics.FillEllipse(brush, 1, 1, 10, 10);
        return bitmap;
    }

    private string GetImageKey(string serviceName)
        => _healthStates.TryGetValue(serviceName, out ServiceHealthState state) ? state.ToString() : nameof(ServiceHealthState.Unknown);

    private static ServiceHealthState EvaluateHealth(ServiceHealth health)
    {
        if (health.ErrorRatePercent >= 5 || health.AvailabilityPercent < 99 || health.P99LatencyMs >= 1000)
            return ServiceHealthState.Critical;
        if (health.ErrorRatePercent >= 1 || health.AvailabilityPercent < 99.9 || health.P99LatencyMs >= 500)
            return ServiceHealthState.Degraded;
        return ServiceHealthState.Healthy;
    }
}
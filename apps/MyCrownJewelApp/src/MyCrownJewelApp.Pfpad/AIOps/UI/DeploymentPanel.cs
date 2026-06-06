using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Displays pipeline and deployment activity.
/// </summary>
public sealed class DeploymentPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _refreshButton;
    private readonly Button _closeButton;
    private readonly Panel _tabStrip;
    private readonly Button _pipelinesTabButton;
    private readonly Button _deploymentsTabButton;
    private readonly Panel _contentPanel;
    private readonly Panel _pipelinesPanel;
    private readonly TreeView _pipelinesTree;
    private readonly Panel _deploymentsPanel;
    private readonly Panel _deploymentsFilterBar;
    private readonly ComboBox _environmentFilter;
    private readonly ListView _deploymentsList;
    private readonly List<Pipeline> _pipelines = [];
    private readonly List<Deployment> _deployments = [];

    private Theme _theme;
    private bool _showPipelines = true;

    /// <summary>
    /// Raised when the panel should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Initializes a new deployment panel.
    /// </summary>
    public DeploymentPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(420, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🚀 DEPLOYMENTS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _refreshButton = AIOpsUiHelper.CreateHeaderButton("↺ Refresh", (s, e) => RefreshCurrentView(), 84);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Resize += (s, e) =>
        {
            _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
            _refreshButton.Location = new Point(_closeButton.Left - _refreshButton.Width - 4, 4);
        };
        _header.Controls.AddRange([_titleLabel, _refreshButton, _closeButton]);

        _tabStrip = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 5, 6, 5) };
        _pipelinesTabButton = new Button { Text = "Pipelines", Width = 90, Height = 24, FlatStyle = FlatStyle.Flat, Location = new Point(6, 5) };
        _deploymentsTabButton = new Button { Text = "Deployments", Width = 100, Height = 24, FlatStyle = FlatStyle.Flat, Location = new Point(102, 5) };
        _pipelinesTabButton.Click += (s, e) => ShowTab(true);
        _deploymentsTabButton.Click += (s, e) => ShowTab(false);
        _tabStrip.Controls.AddRange([_pipelinesTabButton, _deploymentsTabButton]);

        _contentPanel = new Panel { Dock = DockStyle.Fill };

        _pipelinesPanel = new Panel { Dock = DockStyle.Fill };
        _pipelinesTree = new TreeView
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
        _pipelinesTree.DrawNode += PipelinesTree_DrawNode;
        _pipelinesTree.NodeMouseDoubleClick += PipelinesTree_NodeMouseDoubleClick;
        _pipelinesPanel.Controls.Add(_pipelinesTree);

        _deploymentsPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        _deploymentsFilterBar = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(6, 3, 6, 3) };
        _environmentFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Location = new Point(6, 3) };
        _environmentFilter.Items.Add("All");
        _environmentFilter.SelectedIndex = 0;
        _environmentFilter.SelectedIndexChanged += (s, e) => RefreshDeploymentsList();
        _deploymentsFilterBar.Controls.Add(_environmentFilter);

        _deploymentsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };
        _deploymentsList.Columns.Add("Environment", 100);
        _deploymentsList.Columns.Add("Service", 140);
        _deploymentsList.Columns.Add("Version", 120);
        _deploymentsList.Columns.Add("Status", 90);
        _deploymentsList.Columns.Add("Deployed By", 120);
        _deploymentsList.Columns.Add("Time", 120);
        _deploymentsList.DoubleClick += DeploymentsList_DoubleClick;
        _deploymentsPanel.Controls.Add(_deploymentsList);
        _deploymentsPanel.Controls.Add(_deploymentsFilterBar);

        _contentPanel.Controls.Add(_pipelinesPanel);
        _contentPanel.Controls.Add(_deploymentsPanel);

        Controls.Add(_contentPanel);
        Controls.Add(_tabStrip);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        ShowTab(true);
    }

    /// <summary>
    /// Loads pipeline data into the panel.
    /// </summary>
    public void SetPipelines(IReadOnlyList<Pipeline> pipelines)
    {
        _pipelines.Clear();
        _pipelines.AddRange(pipelines.OrderByDescending(p => p.StartedAt));
        RefreshPipelinesTree();
    }

    /// <summary>
    /// Loads deployment data into the panel.
    /// </summary>
    public void SetDeployments(IReadOnlyList<Deployment> deployments)
    {
        _deployments.Clear();
        _deployments.AddRange(deployments.OrderByDescending(d => d.DeployedAt));

        string selected = _environmentFilter.SelectedItem as string ?? "All";
        List<string> environments = _deployments
            .Select(d => d.Environment)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _environmentFilter.BeginUpdate();
        try
        {
            _environmentFilter.Items.Clear();
            _environmentFilter.Items.Add("All");
            foreach (string environment in environments)
                _environmentFilter.Items.Add(environment);
            _environmentFilter.SelectedItem = _environmentFilter.Items.Contains(selected) ? selected : "All";
        }
        finally
        {
            _environmentFilter.EndUpdate();
        }

        RefreshDeploymentsList();
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        _tabStrip.BackColor = theme.MenuBackground;
        _contentPanel.BackColor = theme.MenuBackground;
        _pipelinesPanel.BackColor = theme.MenuBackground;
        _deploymentsPanel.BackColor = theme.MenuBackground;
        _deploymentsFilterBar.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _refreshButton.BackColor = Color.Transparent;
        _refreshButton.ForeColor = theme.Text;
        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = theme.Text;
        _environmentFilter.BackColor = theme.EditorBackground;
        _environmentFilter.ForeColor = theme.Text;
        ApplyTabButtonTheme(_pipelinesTabButton, _showPipelines);
        ApplyTabButtonTheme(_deploymentsTabButton, !_showPipelines);
        AIOpsUiHelper.ApplyTreeTheme(_pipelinesTree, theme);
        AIOpsUiHelper.ApplyListTheme(_deploymentsList, theme);
        _pipelinesTree.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private void ShowTab(bool pipelines)
    {
        _showPipelines = pipelines;
        _pipelinesPanel.Visible = pipelines;
        _deploymentsPanel.Visible = !pipelines;
        ApplyTabButtonTheme(_pipelinesTabButton, pipelines);
        ApplyTabButtonTheme(_deploymentsTabButton, !pipelines);
    }

    private void ApplyTabButtonTheme(Button button, bool selected)
    {
        button.BackColor = selected ? AIOpsUiHelper.CurrentLineBackground(_theme) : _theme.PanelBackground;
        button.ForeColor = selected ? _theme.Text : AIOpsUiHelper.SecondaryText(_theme);
        button.FlatAppearance.BorderColor = _theme.Border;
    }

    private void RefreshCurrentView()
    {
        if (_showPipelines)
            RefreshPipelinesTree();
        else
            RefreshDeploymentsList();
    }

    private void RefreshPipelinesTree()
    {
        _pipelinesTree.BeginUpdate();
        try
        {
            _pipelinesTree.Nodes.Clear();
            if (_pipelines.Count == 0)
            {
                _pipelinesTree.Nodes.Add(new TreeNode("Not configured") { ForeColor = AIOpsUiHelper.SecondaryText(_theme) });
                return;
            }

            foreach (IGrouping<string, Pipeline> branchGroup in _pipelines.GroupBy(p => p.Branch).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode branchNode = new(branchGroup.Key);
                foreach (Pipeline pipeline in branchGroup)
                {
                    TreeNode pipelineNode = new($"{AIOpsUiHelper.StatusIcon(pipeline.Status)} {pipeline.Name} [{pipeline.Status}] Started: {pipeline.StartedAt.LocalDateTime:g} Duration: {AIOpsUiHelper.FormatDuration(pipeline.Duration)}")
                    {
                        Tag = pipeline.Url,
                        ForeColor = AIOpsUiHelper.DeploymentStatusColor(pipeline.Status)
                    };

                    foreach (PipelineStage stage in pipeline.Stages)
                    {
                        TreeNode stageNode = new($"{AIOpsUiHelper.StatusIcon(stage.Status)} {stage.Name} [{stage.Status}] {AIOpsUiHelper.FormatDuration(stage.Duration)}")
                        {
                            Tag = stage.LogUrl,
                            ForeColor = AIOpsUiHelper.DeploymentStatusColor(stage.Status)
                        };
                        pipelineNode.Nodes.Add(stageNode);
                    }

                    branchNode.Nodes.Add(pipelineNode);
                }

                _pipelinesTree.Nodes.Add(branchNode);
            }

            _pipelinesTree.ExpandAll();
        }
        finally
        {
            _pipelinesTree.EndUpdate();
        }
    }

    private void RefreshDeploymentsList()
    {
        string selectedEnvironment = _environmentFilter.SelectedItem as string ?? "All";
        IEnumerable<Deployment> filtered = _deployments;
        if (!string.Equals(selectedEnvironment, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(d => string.Equals(d.Environment, selectedEnvironment, StringComparison.OrdinalIgnoreCase));

        _deploymentsList.BeginUpdate();
        try
        {
            _deploymentsList.Items.Clear();
            foreach (Deployment deployment in filtered)
            {
                var item = new ListViewItem(deployment.Environment)
                {
                    Tag = deployment,
                    ForeColor = GetEnvironmentColor(deployment.Environment)
                };
                item.SubItems.Add(deployment.Service);
                item.SubItems.Add(deployment.Version);
                item.SubItems.Add(deployment.Status.ToString());
                item.SubItems.Add(string.IsNullOrWhiteSpace(deployment.DeployedBy) ? "-" : deployment.DeployedBy);
                item.SubItems.Add(deployment.DeployedAt.LocalDateTime.ToString("g"));
                _deploymentsList.Items.Add(item);
            }
        }
        finally
        {
            _deploymentsList.EndUpdate();
        }
    }

    private Color GetEnvironmentColor(string environment)
    {
        if (environment.Contains("prod", StringComparison.OrdinalIgnoreCase))
            return AIOpsUiHelper.Accent(_theme);
        if (environment.Contains("stag", StringComparison.OrdinalIgnoreCase))
            return Color.MediumPurple;
        return AIOpsUiHelper.SecondaryText(_theme);
    }

    private void PipelinesTree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        e.DrawDefault = false;
        Color backColor = e.Node.IsSelected ? AIOpsUiHelper.CurrentLineBackground(_theme) : _pipelinesTree.BackColor;
        using SolidBrush backBrush = new(backColor);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Node.Text, _pipelinesTree.Font, e.Bounds, e.Node.ForeColor.IsEmpty ? _theme.Text : e.Node.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void PipelinesTree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e) => AIOpsUiHelper.OpenUrl(e.Node.Tag as string);

    private void DeploymentsList_DoubleClick(object? sender, EventArgs e)
    {
        if (_deploymentsList.SelectedItems.Count == 0)
            return;

        if (_deploymentsList.SelectedItems[0].Tag is Deployment deployment)
            AIOpsUiHelper.OpenUrl(deployment.PipelineUrl);
    }
}

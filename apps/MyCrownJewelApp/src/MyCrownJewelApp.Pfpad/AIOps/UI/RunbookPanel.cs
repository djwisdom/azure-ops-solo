using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class RunbookPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _refreshButton;
    private readonly Button _closeButton;
    private readonly SplitContainer _splitContainer;
    private readonly TreeView _runbooksTree;
    private readonly ImageList _imageList;
    private readonly Label _runbookNameLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _serviceLabel;
    private readonly Label _severityBadge;
    private readonly ListView _stepsList;
    private readonly TextBox _commandBox;
    private readonly Button _dryRunButton;
    private readonly LinkLabel _externalLinkLabel;
    private readonly TextBox _resultBox;

    private readonly List<Runbook> _runbooks = new();
    private Theme _theme;
    private AIOpsEngine? _engine;

    public event Action? CloseRequested;

    public RunbookPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(460, 340);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "📋 RUNBOOKS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _refreshButton = AIOpsUiHelper.CreateHeaderButton("↺ Refresh", (s, e) => RefreshView(), 84);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _header.Resize += (_, _) => LayoutHeader();
        _header.Controls.AddRange([_titleLabel, _refreshButton, _closeButton]);

        _splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 280, BorderStyle = BorderStyle.None };

        _imageList = new ImageList { ImageSize = new Size(12, 12), ColorDepth = ColorDepth.Depth32Bit };
        BuildImageList();
        _runbooksTree = new TreeView
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
        _runbooksTree.AfterSelect += RunbooksTree_AfterSelect;
        _splitContainer.Panel1.Controls.Add(_runbooksTree);

        Panel detailsPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _runbookNameLabel = new Label { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
        _descriptionLabel = new Label { Dock = DockStyle.Top, Height = 52, AutoSize = false };
        _serviceLabel = new Label { Dock = DockStyle.Top, Height = 18 };
        _severityBadge = new Label { Dock = DockStyle.Top, Height = 22, AutoSize = false, Padding = new Padding(8, 0, 8, 0), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _stepsList = new ListView { Dock = DockStyle.Top, Height = 160, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
        _stepsList.Columns.Add("Step#", 60);
        _stepsList.Columns.Add("Description", 320);
        _stepsList.Columns.Add("Requires Approval", 120);
        _stepsList.SelectedIndexChanged += StepsList_SelectedIndexChanged;
        _commandBox = new TextBox { Dock = DockStyle.Top, Height = 50, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };
        Panel actionPanel = new() { Dock = DockStyle.Top, Height = 32 };
        _dryRunButton = new Button { Text = "Dry Run", Width = 84, Height = 26, FlatStyle = FlatStyle.Flat, Location = new Point(0, 3) };
        _dryRunButton.Click += DryRunButton_Click;
        _externalLinkLabel = new LinkLabel { AutoSize = true, Location = new Point(96, 8), Text = "View External" };
        _externalLinkLabel.LinkClicked += (_, _) => AIOpsUiHelper.OpenUrl(_externalLinkLabel.Tag as string);
        actionPanel.Controls.AddRange([_dryRunButton, _externalLinkLabel]);
        _resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };

        detailsPanel.Controls.Add(_resultBox);
        detailsPanel.Controls.Add(actionPanel);
        detailsPanel.Controls.Add(_commandBox);
        detailsPanel.Controls.Add(_stepsList);
        detailsPanel.Controls.Add(_severityBadge);
        detailsPanel.Controls.Add(_serviceLabel);
        detailsPanel.Controls.Add(_descriptionLabel);
        detailsPanel.Controls.Add(_runbookNameLabel);
        _splitContainer.Panel2.Controls.Add(detailsPanel);

        Controls.Add(_splitContainer);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeader();
        ShowRunbook(null);
    }

    public void SetRunbooks(IReadOnlyList<Runbook> runbooks)
    {
        _runbooks.Clear();
        _runbooks.AddRange(runbooks.OrderBy(r => r.ServiceName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase));
        PopulateTree();
    }

    public void SetEngine(AIOpsEngine engine) => _engine = engine;

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        _header.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _refreshButton.BackColor = Color.Transparent;
        _closeButton.BackColor = Color.Transparent;
        _refreshButton.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        _commandBox.BackColor = theme.EditorBackground;
        _commandBox.ForeColor = theme.Text;
        _resultBox.BackColor = theme.EditorBackground;
        _resultBox.ForeColor = theme.Text;
        _dryRunButton.BackColor = theme.PanelBackground;
        _dryRunButton.ForeColor = theme.Text;
        _dryRunButton.FlatAppearance.BorderColor = theme.Border;
        _externalLinkLabel.LinkColor = AIOpsUiHelper.Accent(theme);
        _externalLinkLabel.ActiveLinkColor = AIOpsUiHelper.Accent(theme);
        _externalLinkLabel.VisitedLinkColor = AIOpsUiHelper.Accent(theme);
        AIOpsUiHelper.ApplyTreeTheme(_runbooksTree, theme);
        AIOpsUiHelper.ApplyListTheme(_stepsList, theme);
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
        _refreshButton.Location = new Point(_closeButton.Left - _refreshButton.Width - 4, 4);
    }

    private void RefreshView()
    {
        if (_engine?.Runbooks.Count > 0)
            SetRunbooks(_engine.Runbooks);
        else
            PopulateTree();
    }

    private void PopulateTree()
    {
        string? selectedId = GetSelectedRunbook()?.Id;
        _runbooksTree.BeginUpdate();
        try
        {
            _runbooksTree.Nodes.Clear();
            foreach (IGrouping<string, Runbook> group in _runbooks.GroupBy(r => r.ServiceName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode serviceNode = new(group.Key) { ImageKey = nameof(Severity.Info), SelectedImageKey = nameof(Severity.Info) };
                foreach (Runbook runbook in group)
                {
                    string text = runbook.IsAutomatable ? $"{runbook.Name} (auto)" : runbook.Name;
                    TreeNode runbookNode = new(text) { Tag = runbook, ImageKey = runbook.Severity.ToString(), SelectedImageKey = runbook.Severity.ToString() };
                    serviceNode.Nodes.Add(runbookNode);
                }
                _runbooksTree.Nodes.Add(serviceNode);
            }
        }
        finally
        {
            _runbooksTree.EndUpdate();
        }

        _runbooksTree.ExpandAll();
        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectRunbookNode(selectedId);
        if (_runbooksTree.SelectedNode is null)
        {
            TreeNode? firstLeaf = _runbooksTree.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).FirstOrDefault();
            if (firstLeaf is not null)
                _runbooksTree.SelectedNode = firstLeaf;
        }
        if (_runbooksTree.SelectedNode is null)
            ShowRunbook(null);
    }

    private void SelectRunbookNode(string runbookId)
    {
        foreach (TreeNode serviceNode in _runbooksTree.Nodes)
        {
            foreach (TreeNode runbookNode in serviceNode.Nodes)
            {
                if (runbookNode.Tag is Runbook runbook && string.Equals(runbook.Id, runbookId, StringComparison.OrdinalIgnoreCase))
                {
                    _runbooksTree.SelectedNode = runbookNode;
                    return;
                }
            }
        }
    }

    private void RunbooksTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        ShowRunbook(e.Node?.Tag as Runbook);
    }

    private void ShowRunbook(Runbook? runbook)
    {
        _stepsList.BeginUpdate();
        try
        {
            _stepsList.Items.Clear();
            _commandBox.Clear();
            _resultBox.Clear();

            if (runbook is null)
            {
                _runbookNameLabel.Text = "Select a runbook";
                _descriptionLabel.Text = string.Empty;
                _serviceLabel.Text = string.Empty;
                _severityBadge.Text = string.Empty;
                _severityBadge.BackColor = _theme.PanelBackground;
                _externalLinkLabel.Tag = null;
                _externalLinkLabel.Visible = false;
                return;
            }

            _runbookNameLabel.Text = runbook.Name;
            _descriptionLabel.Text = runbook.Description;
            _serviceLabel.Text = $"Service: {runbook.ServiceName}";
            _severityBadge.Text = $" {runbook.Severity} ";
            _severityBadge.BackColor = AIOpsUiHelper.SeverityColor(runbook.Severity);
            _severityBadge.ForeColor = Color.White;
            _externalLinkLabel.Tag = runbook.ExternalUrl;
            _externalLinkLabel.Visible = !string.IsNullOrWhiteSpace(runbook.ExternalUrl);

            foreach (RunbookStep step in runbook.Steps.OrderBy(s => s.Order))
            {
                ListViewItem item = new(step.Order.ToString()) { Tag = step, ForeColor = step.RequiresApproval ? Color.Goldenrod : Color.ForestGreen };
                item.SubItems.Add(step.Description);
                item.SubItems.Add(step.RequiresApproval ? "✓" : "✗");
                _stepsList.Items.Add(item);
            }
            if (_stepsList.Items.Count > 0)
                _stepsList.Items[0].Selected = true;
        }
        finally
        {
            _stepsList.EndUpdate();
        }
    }

    private void StepsList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_stepsList.SelectedItems.Count == 0)
        {
            _commandBox.Clear();
            return;
        }

        if (_stepsList.SelectedItems[0].Tag is not RunbookStep step)
            return;

        List<string> lines = new() { step.Command ?? step.Description };
        if (!string.IsNullOrWhiteSpace(step.SafetyNote))
            lines.Add($"Safety: {step.SafetyNote}");
        if (step.Timeout is not null)
            lines.Add($"Timeout: {AIOpsUiHelper.FormatDuration(step.Timeout)}");
        _commandBox.Text = string.Join(Environment.NewLine, lines);
    }

    private async void DryRunButton_Click(object? sender, EventArgs e)
    {
        Runbook? runbook = GetSelectedRunbook();
        if (runbook is null || _engine is null)
            return;

        foreach (RunbookStep step in runbook.Steps.OrderBy(s => s.Order).Where(s => s.RequiresApproval))
        {
            DialogResult result = MessageBox.Show(
                this,
                $"Approve dry run for step {step.Order}?\n\n{step.Description}\n{step.Command}\n\n{step.SafetyNote}",
                "Runbook Approval",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                _resultBox.Text = $"Dry run cancelled before step {step.Order}.";
                return;
            }
        }

        _dryRunButton.Enabled = false;
        _resultBox.Text = "Running dry run...";
        try
        {
            RunbookExecutionResult result = await _engine.RunbookEngine.ExecuteAsync(runbook, dryRun: true);
            List<string> lines = new()
            {
                result.Success ? "Dry run succeeded." : "Dry run failed.",
                $"Executed: {result.ExecutedAt.LocalDateTime:g}",
                $"Approval required: {(result.RequiredApproval ? "Yes" : "No")}",
                $"Approval granted: {(result.ApprovalGranted ? "Yes" : "No")}"
            };
            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                lines.Add(string.Empty);
                lines.Add($"Error: {result.Error}");
            }
            if (result.ExecutedSteps.Count > 0)
            {
                lines.Add(string.Empty);
                lines.AddRange(result.ExecutedSteps);
            }
            _resultBox.Text = string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            _resultBox.Text = $"Dry run failed: {ex.Message}";
        }
        finally
        {
            _dryRunButton.Enabled = true;
        }
    }

    private Runbook? GetSelectedRunbook() => _runbooksTree.SelectedNode?.Tag as Runbook;

    private void BuildImageList()
    {
        _imageList.Images.Clear();
        foreach (Severity severity in Enum.GetValues<Severity>())
            _imageList.Images.Add(severity.ToString(), CreateDotBitmap(AIOpsUiHelper.SeverityColor(severity)));
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
}
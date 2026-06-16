using System.IO;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class PullRequestRiskPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly ComboBox _prCombo;
    private readonly Button _analyzeButton;
    private readonly Button _closeButton;
    private readonly SplitContainer _splitContainer;
    private readonly Label _prTitleLabel;
    private readonly Label _authorLabel;
    private readonly Label _branchLabel;
    private readonly Label _createdLabel;
    private readonly Label _statusBadge;
    private readonly ListView _changedFilesList;
    private readonly TabControl _tabs;
    private readonly Label _riskScoreBadge;
    private readonly Label _riskLevelLabel;
    private readonly ListView _riskFactorsList;
    private readonly TextBox _recommendationsBox;
    private readonly ListView _securityList;
    private readonly ListView _commitsList;

    private readonly List<GitPullRequest> _pullRequests = new();
    private readonly List<SecurityFinding> _allSecurityFindings = new();
    private DeploymentRiskReport? _riskReport;
    private Theme _theme;
    private readonly Panel _hintBar;
    private readonly Label _hintLabel;

    public event Action<GitPullRequest>? AnalyzeRequested;
    public event Action<string, int>? SecurityFindingNavigateRequested;
    public event Action? CloseRequested;

    public PullRequestRiskPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(460, 340);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🔀 PR RISK", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _prCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _analyzeButton = AIOpsUiHelper.CreateHeaderButton("Analyze", (s, e) => RaiseAnalyzeRequested(), 72);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _header.Resize += (_, _) => LayoutHeader();
        _prCombo.SelectedIndexChanged += (_, _) => PopulateSelectedPullRequest();
        _header.Controls.AddRange([_titleLabel, _prCombo, _analyzeButton, _closeButton]);

        _splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 210, BorderStyle = BorderStyle.None };

        Panel prDetailsPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _prTitleLabel = new Label { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        _authorLabel = new Label { Dock = DockStyle.Top, Height = 18 };
        _branchLabel = new Label { Dock = DockStyle.Top, Height = 18 };
        _createdLabel = new Label { Dock = DockStyle.Top, Height = 18 };
        _statusBadge = new Label { Dock = DockStyle.Top, Height = 22, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 8, 0) };
        _changedFilesList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };
        _changedFilesList.Columns.Add("File Path", 420);
        _changedFilesList.Columns.Add("Change Type", 120);
        prDetailsPanel.Controls.Add(_changedFilesList);
        prDetailsPanel.Controls.Add(_createdLabel);
        prDetailsPanel.Controls.Add(_branchLabel);
        prDetailsPanel.Controls.Add(_authorLabel);
        prDetailsPanel.Controls.Add(_statusBadge);
        prDetailsPanel.Controls.Add(_prTitleLabel);
        _splitContainer.Panel1.Controls.Add(prDetailsPanel);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        AIOpsUiHelper.AttachAccentTabControl(_tabs, () => _theme);
        TabPage riskTab = new("Risk");
        TabPage securityTab = new("Security");
        TabPage commitsTab = new("Commits");

        Panel riskPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _riskScoreBadge = new Label { Dock = DockStyle.Top, Height = 24, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _riskLevelLabel = new Label { Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _riskFactorsList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
        _riskFactorsList.Columns.Add("Factor", 180);
        _riskFactorsList.Columns.Add("Level", 90);
        _riskFactorsList.Columns.Add("Description", 420);
        _recommendationsBox = new TextBox { Dock = DockStyle.Bottom, Height = 84, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None };
        riskPanel.Controls.Add(_riskFactorsList);
        riskPanel.Controls.Add(_recommendationsBox);
        riskPanel.Controls.Add(_riskLevelLabel);
        riskPanel.Controls.Add(_riskScoreBadge);
        riskTab.Controls.Add(riskPanel);

        _securityList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
        _securityList.Columns.Add("Severity", 90);
        _securityList.Columns.Add("Title", 220);
        _securityList.Columns.Add("File", 260);
        _securityList.Columns.Add("Line", 60);
        _securityList.DoubleClick += SecurityList_DoubleClick;
        securityTab.Controls.Add(_securityList);

        _commitsList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None };
        _commitsList.Columns.Add("SHA", 90);
        _commitsList.Columns.Add("Author", 120);
        _commitsList.Columns.Add("Message", 360);
        _commitsList.Columns.Add("Date", 120);
        commitsTab.Controls.Add(_commitsList);

        _tabs.TabPages.Add(riskTab);
        _tabs.TabPages.Add(securityTab);
        _tabs.TabPages.Add(commitsTab);
        _splitContainer.Panel2.Controls.Add(_tabs);

        Controls.Add(_splitContainer);
        (_hintBar, _hintLabel) = AIOpsUiHelper.CreateHintBar("ⓘ Scores PRs using changed files, test coverage, incident history, and SLO burn rate.");
        Controls.Add(_hintBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeader();
        PopulateRiskTab();
    }

    public void SetPullRequests(IReadOnlyList<GitPullRequest> prs)
    {
        _pullRequests.Clear();
        _pullRequests.AddRange(prs.OrderByDescending(pr => pr.CreatedAt));
        _prCombo.BeginUpdate();
        try
        {
            _prCombo.Items.Clear();
            foreach (GitPullRequest pr in _pullRequests)
                _prCombo.Items.Add(pr);
        }
        finally
        {
            _prCombo.EndUpdate();
        }

        if (_pullRequests.Count > 0)
            _prCombo.SelectedIndex = 0;
        else
            PopulateSelectedPullRequest();
    }

    public void SetRiskReport(DeploymentRiskReport report)
    {
        _riskReport = report;
        PopulateRiskTab();
    }

    public void SetSecurityFindings(IReadOnlyList<SecurityFinding> findings)
    {
        _allSecurityFindings.Clear();
        _allSecurityFindings.AddRange(findings);
        PopulateSecurityTab();
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        _header.BackColor = theme.MenuBackground;
        AIOpsUiHelper.SetHintBarTheme(_hintBar, _hintLabel, theme);
        _titleLabel.ForeColor = theme.Text;
        _analyzeButton.BackColor = Color.Transparent;
        _closeButton.BackColor = Color.Transparent;
        _analyzeButton.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        _recommendationsBox.ApplyTheme(theme);
        AIOpsUiHelper.ApplyListTheme(_changedFilesList, theme);
        AIOpsUiHelper.ApplyListTheme(_riskFactorsList, theme);
        AIOpsUiHelper.ApplyListTheme(_securityList, theme);
        AIOpsUiHelper.ApplyListTheme(_commitsList, theme);
        foreach (TabPage tab in _tabs.TabPages)
        {
            tab.BackColor = theme.MenuBackground;
            tab.ForeColor = theme.Text;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private void LayoutHeader()
    {
        _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _analyzeButton.Location = new Point(_closeButton.Left - _analyzeButton.Width - 4, 4);
        _prCombo.Location = new Point(_analyzeButton.Left - _prCombo.Width - 6, 4);
    }

    private GitPullRequest? GetSelectedPr() => _prCombo.SelectedItem as GitPullRequest;

    private void PopulateSelectedPullRequest()
    {
        GitPullRequest? pr = GetSelectedPr();
        _changedFilesList.BeginUpdate();
        _commitsList.BeginUpdate();
        try
        {
            _changedFilesList.Items.Clear();
            _commitsList.Items.Clear();

            if (pr is null)
            {
                _prTitleLabel.Text = "No pull request selected";
                _authorLabel.Text = string.Empty;
                _branchLabel.Text = string.Empty;
                _createdLabel.Text = string.Empty;
                _statusBadge.Text = string.Empty;
                PopulateSecurityTab();
                return;
            }

            _prTitleLabel.Text = pr.Title;
            _authorLabel.Text = $"Author: {pr.Author}";
            _branchLabel.Text = $"Branch: {pr.SourceBranch} → {pr.TargetBranch}";
            _createdLabel.Text = $"Created: {pr.CreatedAt.LocalDateTime:g}";
            _statusBadge.Text = $" {pr.Status} ";
            _statusBadge.BackColor = StatusColor(pr.Status);
            _statusBadge.ForeColor = FlatUiHelper.BadgeForeground(_theme);

            foreach (string file in pr.ChangedFiles)
            {
                ListViewItem item = new(file) { ForeColor = FileTypeColor(file) };
                item.SubItems.Add(InferChangeType(file));
                _changedFilesList.Items.Add(item);
            }

            foreach (GitCommit commit in pr.Commits.OrderByDescending(c => c.Timestamp))
            {
                ListViewItem item = new(commit.ShortSha);
                item.SubItems.Add(commit.Author);
                item.SubItems.Add(commit.Message);
                item.SubItems.Add(commit.Timestamp.LocalDateTime.ToString("g"));
                _commitsList.Items.Add(item);
            }
        }
        finally
        {
            _changedFilesList.EndUpdate();
            _commitsList.EndUpdate();
        }

        PopulateSecurityTab();
    }

    private void PopulateRiskTab()
    {
        _riskFactorsList.BeginUpdate();
        try
        {
            _riskFactorsList.Items.Clear();
            if (_riskReport is null)
            {
                _riskScoreBadge.Text = " Score: n/a ";
                _riskScoreBadge.BackColor = _theme.PanelBackground;
                _riskScoreBadge.ForeColor = _theme.Text;
                _riskLevelLabel.Text = "Risk report not generated.";
                _recommendationsBox.Text = string.Empty;
                return;
            }

            _riskScoreBadge.Text = $" Score: {_riskReport.Score} ";
            _riskScoreBadge.BackColor = AIOpsUiHelper.RiskColor(_riskReport.OverallRisk);
            _riskScoreBadge.ForeColor = FlatUiHelper.BadgeForeground(_theme);
            _riskLevelLabel.Text = $"Overall risk: {_riskReport.OverallRisk}";
            _riskLevelLabel.ForeColor = AIOpsUiHelper.RiskColor(_riskReport.OverallRisk);
            foreach (DeploymentRiskFactor factor in _riskReport.Factors)
            {
                ListViewItem item = new(factor.Name) { ForeColor = AIOpsUiHelper.RiskColor(factor.Level) };
                item.SubItems.Add(factor.Level.ToString());
                item.SubItems.Add(factor.Description);
                _riskFactorsList.Items.Add(item);
            }
            _recommendationsBox.Text = _riskReport.Recommendations.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, _riskReport.Recommendations.Select(text => $"• {text}"));
        }
        finally
        {
            _riskFactorsList.EndUpdate();
        }
    }

    private void PopulateSecurityTab()
    {
        GitPullRequest? pr = GetSelectedPr();
        HashSet<string> changedFiles = pr is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(pr.ChangedFiles, StringComparer.OrdinalIgnoreCase);

        _securityList.BeginUpdate();
        try
        {
            _securityList.Items.Clear();
            IEnumerable<SecurityFinding> findings = changedFiles.Count == 0
                ? Enumerable.Empty<SecurityFinding>()
                : _allSecurityFindings.Where(f => changedFiles.Contains(f.FilePath));

            foreach (SecurityFinding finding in findings.OrderByDescending(f => AIOpsUiHelper.SeverityRank(f.Severity)).ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                ListViewItem item = new(finding.Severity.ToString()) { Tag = finding, ForeColor = AIOpsUiHelper.SeverityColor(finding.Severity) };
                item.SubItems.Add(finding.Title);
                item.SubItems.Add(finding.FilePath);
                item.SubItems.Add(finding.LineNumber.ToString());
                _securityList.Items.Add(item);
            }
        }
        finally
        {
            _securityList.EndUpdate();
        }
    }

    private void SecurityList_DoubleClick(object? sender, EventArgs e)
    {
        if (_securityList.SelectedItems.Count == 0)
            return;

        if (_securityList.SelectedItems[0].Tag is SecurityFinding finding)
            SecurityFindingNavigateRequested?.Invoke(finding.FilePath, Math.Max(1, finding.LineNumber));
    }

    private void RaiseAnalyzeRequested()
    {
        GitPullRequest? pr = GetSelectedPr();
        if (pr is not null)
            AnalyzeRequested?.Invoke(pr);
    }

    private static string InferChangeType(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? "Changed" : extension.Trim('.').ToUpperInvariant();
    }

    private static Color FileTypeColor(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".cs" => Color.DeepSkyBlue,
            ".json" or ".yaml" or ".yml" => Color.MediumPurple,
            ".md" => Color.SlateGray,
            ".sql" => Color.Goldenrod,
            ".ps1" or ".sh" => Color.SeaGreen,
            _ => Color.SteelBlue
        };
    }

    private static Color StatusColor(PullRequestStatus status) => status switch
    {
        PullRequestStatus.Merged => Color.ForestGreen,
        PullRequestStatus.Closed => Color.IndianRed,
        PullRequestStatus.Draft => Color.Goldenrod,
        _ => Color.SteelBlue
    };
}
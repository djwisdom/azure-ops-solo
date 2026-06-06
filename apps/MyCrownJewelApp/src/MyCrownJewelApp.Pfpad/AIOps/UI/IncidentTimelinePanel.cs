using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class IncidentTimelinePanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly ComboBox _incidentCombo;
    private readonly Button _analyzeButton;
    private readonly Button _closeButton;
    private readonly SplitContainer _splitContainer;
    private readonly ListView _incidentList;
    private readonly TextBox _incidentDetailsBox;
    private readonly TabControl _tabs;
    private readonly ListView _timelineList;
    private readonly SplitContainer _rootCauseSplit;
    private readonly ListView _hypothesesList;
    private readonly TextBox _hypothesisDetailsBox;
    private readonly ListBox _relatedFilesList;

    private readonly List<Incident> _incidents = new();
    private readonly List<CorrelationChain> _chains = new();
    private RootCauseAnalysis? _rootCauseAnalysis;
    private Theme _theme;
    private bool _syncingSelection;

    public event Action<Incident>? RcaRequested;
    public event Action<string, int>? NavigateRequested;
    public event Action? CloseRequested;

    public IncidentTimelinePanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(460, 340);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🚨 INCIDENT TIMELINE", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _incidentCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Height = 24, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _analyzeButton = AIOpsUiHelper.CreateHeaderButton("Analyze RCA", (s, e) => RaiseRcaRequested(), 94);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _header.Resize += (_, _) => LayoutHeader();
        _incidentCombo.SelectedIndexChanged += IncidentCombo_SelectedIndexChanged;
        _header.Controls.AddRange([_titleLabel, _incidentCombo, _analyzeButton, _closeButton]);

        _splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 210, BorderStyle = BorderStyle.None };

        Panel topPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(6) };
        _incidentList = new ListView
        {
            Dock = DockStyle.Top,
            Height = 150,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };
        _incidentList.Columns.Add("Severity", 80);
        _incidentList.Columns.Add("Title", 240);
        _incidentList.Columns.Add("Status", 90);
        _incidentList.Columns.Add("Time", 90);
        _incidentList.SelectedIndexChanged += IncidentList_SelectedIndexChanged;
        _incidentDetailsBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };
        topPanel.Controls.Add(_incidentDetailsBox);
        topPanel.Controls.Add(_incidentList);
        _splitContainer.Panel1.Controls.Add(topPanel);

        _tabs = new TabControl { Dock = DockStyle.Fill };
        TabPage timelineTab = new("Timeline");
        TabPage rootCauseTab = new("Root Cause");

        _timelineList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };
        _timelineList.Columns.Add("Time", 120);
        _timelineList.Columns.Add("Type", 90);
        _timelineList.Columns.Add("Description", 520);
        timelineTab.Controls.Add(_timelineList);

        _rootCauseSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 150, BorderStyle = BorderStyle.None };
        _hypothesesList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };
        _hypothesesList.Columns.Add("Rank", 60);
        _hypothesesList.Columns.Add("Confidence %", 100);
        _hypothesesList.Columns.Add("Cause", 520);
        _hypothesesList.SelectedIndexChanged += HypothesesList_SelectedIndexChanged;
        _rootCauseSplit.Panel1.Controls.Add(_hypothesesList);

        Panel rcDetailsPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(6) };
        Label relatedFilesLabel = new() { Dock = DockStyle.Top, Height = 18, Text = "Related files", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _relatedFilesList = new ListBox { Dock = DockStyle.Right, Width = 220, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        _relatedFilesList.DoubleClick += RelatedFilesList_DoubleClick;
        _hypothesisDetailsBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle };
        rcDetailsPanel.Controls.Add(_hypothesisDetailsBox);
        rcDetailsPanel.Controls.Add(_relatedFilesList);
        rcDetailsPanel.Controls.Add(relatedFilesLabel);
        _rootCauseSplit.Panel2.Controls.Add(rcDetailsPanel);

        rootCauseTab.Controls.Add(_rootCauseSplit);
        _tabs.TabPages.Add(timelineTab);
        _tabs.TabPages.Add(rootCauseTab);
        _splitContainer.Panel2.Controls.Add(_tabs);

        Controls.Add(_splitContainer);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeader();
    }

    public void SetData(IReadOnlyList<Incident> incidents, IReadOnlyList<CorrelationChain> chains)
    {
        _incidents.Clear();
        _incidents.AddRange(incidents.OrderByDescending(i => AIOpsUiHelper.SeverityRank(i.Severity)).ThenByDescending(i => i.StartedAt));
        _chains.Clear();
        _chains.AddRange(chains.OrderByDescending(c => c.WindowEnd));
        PopulateIncidents();
    }

    public void SetRootCauseAnalysis(RootCauseAnalysis rca)
    {
        _rootCauseAnalysis = rca;
        PopulateRootCause();
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        _header.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _incidentDetailsBox.BackColor = theme.EditorBackground;
        _incidentDetailsBox.ForeColor = theme.Text;
        _hypothesisDetailsBox.BackColor = theme.EditorBackground;
        _hypothesisDetailsBox.ForeColor = theme.Text;
        _relatedFilesList.BackColor = theme.EditorBackground;
        _relatedFilesList.ForeColor = theme.Text;
        _incidentCombo.BackColor = theme.EditorBackground;
        _incidentCombo.ForeColor = theme.Text;
        _analyzeButton.BackColor = Color.Transparent;
        _closeButton.BackColor = Color.Transparent;
        _analyzeButton.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        AIOpsUiHelper.ApplyListTheme(_incidentList, theme);
        AIOpsUiHelper.ApplyListTheme(_timelineList, theme);
        AIOpsUiHelper.ApplyListTheme(_hypothesesList, theme);
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
        _incidentCombo.Location = new Point(_analyzeButton.Left - _incidentCombo.Width - 6, 4);
    }

    private void PopulateIncidents()
    {
        _syncingSelection = true;
        try
        {
            _incidentCombo.BeginUpdate();
            _incidentList.BeginUpdate();
            _incidentCombo.Items.Clear();
            _incidentList.Items.Clear();
            foreach (Incident incident in _incidents)
            {
                _incidentCombo.Items.Add(incident);
                ListViewItem item = new($"{SeverityGlyph(incident.Severity)} {incident.Severity}") { Tag = incident, ForeColor = AIOpsUiHelper.SeverityColor(incident.Severity) };
                item.SubItems.Add(incident.Title);
                item.SubItems.Add(incident.Status.ToString());
                item.SubItems.Add(AIOpsUiHelper.FormatAge(incident.StartedAt));
                _incidentList.Items.Add(item);
            }
        }
        finally
        {
            _incidentCombo.EndUpdate();
            _incidentList.EndUpdate();
            _syncingSelection = false;
        }

        if (_incidents.Count > 0)
        {
            _incidentCombo.SelectedIndex = 0;
            _incidentList.Items[0].Selected = true;
        }
        else
        {
            _incidentDetailsBox.Text = "No incidents available.";
            PopulateTimeline(null);
            PopulateRootCause();
        }
    }

    private void IncidentCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncingSelection)
            return;

        Incident? incident = _incidentCombo.SelectedItem as Incident;
        if (incident is null)
            return;

        SelectIncident(incident);
    }

    private void IncidentList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_syncingSelection || _incidentList.SelectedItems.Count == 0)
            return;

        if (_incidentList.SelectedItems[0].Tag is Incident incident)
            SelectIncident(incident);
    }

    private void SelectIncident(Incident incident)
    {
        _syncingSelection = true;
        try
        {
            int comboIndex = _incidents.FindIndex(i => i.Id == incident.Id);
            if (comboIndex >= 0 && _incidentCombo.SelectedIndex != comboIndex)
                _incidentCombo.SelectedIndex = comboIndex;

            foreach (ListViewItem item in _incidentList.Items)
                item.Selected = item.Tag is Incident selected && selected.Id == incident.Id;
        }
        finally
        {
            _syncingSelection = false;
        }

        _incidentDetailsBox.Text = BuildIncidentDetails(incident);
        PopulateTimeline(incident);
        PopulateRootCause();
    }

    private void PopulateTimeline(Incident? incident)
    {
        _timelineList.BeginUpdate();
        try
        {
            _timelineList.Items.Clear();
            IEnumerable<CorrelationEvent> events = Enumerable.Empty<CorrelationEvent>();
            if (incident is not null)
            {
                string? service = incident.AffectedService;
                events = _chains
                    .Where(c => string.IsNullOrWhiteSpace(service) || string.Equals(c.ServiceName, service, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(c => c.Events)
                    .OrderByDescending(e => e.Timestamp);
            }

            foreach (CorrelationEvent evt in events)
            {
                ListViewItem item = new(evt.Timestamp.LocalDateTime.ToString("g")) { Tag = evt, ForeColor = TimelineColor(evt) };
                item.SubItems.Add($"{TimelineGlyph(evt)} {evt.EntityType}");
                item.SubItems.Add(evt.Description);
                _timelineList.Items.Add(item);
            }

            if (_timelineList.Items.Count == 0)
                _timelineList.Items.Add(new ListViewItem(new[] { string.Empty, string.Empty, "No correlated events." }) { ForeColor = _theme.Muted });
        }
        finally
        {
            _timelineList.EndUpdate();
        }
    }

    private void PopulateRootCause()
    {
        Incident? incident = GetSelectedIncident();
        _hypothesesList.BeginUpdate();
        try
        {
            _hypothesesList.Items.Clear();
            _relatedFilesList.Items.Clear();
            _hypothesisDetailsBox.Clear();

            if (incident is null || _rootCauseAnalysis is null || !string.Equals(_rootCauseAnalysis.IncidentId, incident.Id, StringComparison.OrdinalIgnoreCase))
            {
                _hypothesisDetailsBox.Text = incident is null ? "Select an incident to view root-cause analysis." : "No root-cause analysis loaded for the selected incident.";
                return;
            }

            foreach (RootCauseHypothesis hypothesis in _rootCauseAnalysis.Hypotheses.OrderBy(h => h.Rank))
            {
                ListViewItem item = new(hypothesis.Rank.ToString()) { Tag = hypothesis };
                item.SubItems.Add($"{Math.Round(Math.Clamp(hypothesis.ConfidenceScore, 0d, 1d) * 100)}%");
                item.SubItems.Add(hypothesis.Cause);
                _hypothesesList.Items.Add(item);
            }

            if (_hypothesesList.Items.Count > 0)
                _hypothesesList.Items[0].Selected = true;
            else
                _hypothesisDetailsBox.Text = BuildRcaMetaText(_rootCauseAnalysis);
        }
        finally
        {
            _hypothesesList.EndUpdate();
        }
    }

    private void HypothesesList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_hypothesesList.SelectedItems.Count == 0)
        {
            if (_rootCauseAnalysis is not null)
                _hypothesisDetailsBox.Text = BuildRcaMetaText(_rootCauseAnalysis);
            return;
        }

        if (_hypothesesList.SelectedItems[0].Tag is not RootCauseHypothesis hypothesis)
            return;

        _relatedFilesList.BeginUpdate();
        try
        {
            _relatedFilesList.Items.Clear();
            foreach (string file in hypothesis.RelatedFiles)
                _relatedFilesList.Items.Add(file);
        }
        finally
        {
            _relatedFilesList.EndUpdate();
        }

        List<string> lines = new();
        lines.Add($"Cause: {hypothesis.Cause}");
        lines.Add($"Confidence: {Math.Round(Math.Clamp(hypothesis.ConfidenceScore, 0d, 1d) * 100)}%");
        if (!string.IsNullOrWhiteSpace(hypothesis.RelatedDeploymentId))
            lines.Add($"Deployment: {hypothesis.RelatedDeploymentId}");
        if (!string.IsNullOrWhiteSpace(hypothesis.RelatedCommitSha))
            lines.Add($"Commit: {hypothesis.RelatedCommitSha}");
        if (hypothesis.Evidence.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Evidence:");
            lines.AddRange(hypothesis.Evidence.Select(evidence => $"• {evidence.Source}: {evidence.Description}"));
        }
        if (hypothesis.ValidationSteps.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Validation steps:");
            lines.AddRange(hypothesis.ValidationSteps.Select(step => $"• {step}"));
        }
        if (hypothesis.Remediation.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Remediation:");
            lines.AddRange(hypothesis.Remediation.Select(remediation => $"• {remediation.Title}: {remediation.Description}"));
        }
        if (!string.IsNullOrWhiteSpace(_rootCauseAnalysis?.Disclaimer))
        {
            lines.Add(string.Empty);
            lines.Add($"Disclaimer: {_rootCauseAnalysis.Disclaimer}");
        }
        _hypothesisDetailsBox.Text = string.Join(Environment.NewLine, lines);
    }

    private void RelatedFilesList_DoubleClick(object? sender, EventArgs e)
    {
        if (_relatedFilesList.SelectedItem is string filePath)
            NavigateRequested?.Invoke(filePath, 1);
    }

    private Incident? GetSelectedIncident() => _incidentCombo.SelectedItem as Incident;

    private void RaiseRcaRequested()
    {
        Incident? incident = GetSelectedIncident();
        if (incident is not null)
            RcaRequested?.Invoke(incident);
    }

    private static string SeverityGlyph(Severity severity) => severity switch
    {
        Severity.Critical => "🟥",
        Severity.High => "🟧",
        Severity.Medium => "🟨",
        Severity.Low => "🟦",
        _ => "⬜"
    };

    private static string TimelineGlyph(CorrelationEvent evt)
    {
        string type = evt.EntityType.ToLowerInvariant();
        if (type.Contains("commit")) return "🔵";
        if (type.Contains("deploy") || type.Contains("build")) return evt.Severity == Severity.High || evt.Severity == Severity.Critical ? "🔴" : "🟢";
        if (type.Contains("incident")) return "🚨";
        if (type.Contains("alert")) return "🟠";
        if (type.Contains("anomaly")) return "🟡";
        return "•";
    }

    private static Color TimelineColor(CorrelationEvent evt)
    {
        string type = evt.EntityType.ToLowerInvariant();
        if (type.Contains("commit")) return Color.SteelBlue;
        if (type.Contains("deploy") || type.Contains("build")) return evt.Severity == Severity.High || evt.Severity == Severity.Critical ? Color.IndianRed : Color.ForestGreen;
        if (type.Contains("incident")) return Color.IndianRed;
        if (type.Contains("alert")) return Color.DarkOrange;
        if (type.Contains("anomaly")) return Color.Goldenrod;
        return Color.SlateGray;
    }

    private static string BuildIncidentDetails(Incident incident)
    {
        List<string> lines = new()
        {
            incident.Title,
            $"Severity: {incident.Severity}",
            $"Status: {incident.Status}",
            $"Started: {incident.StartedAt.LocalDateTime:g}"
        };
        if (incident.ResolvedAt is not null)
            lines.Add($"Resolved: {incident.ResolvedAt.Value.LocalDateTime:g}");
        if (!string.IsNullOrWhiteSpace(incident.AffectedService))
            lines.Add($"Service: {incident.AffectedService}");
        if (!string.IsNullOrWhiteSpace(incident.RootCauseHypothesis))
            lines.Add($"Hypothesis: {incident.RootCauseHypothesis}");
        if (incident.AffectedEndpoints.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Affected endpoints:");
            lines.AddRange(incident.AffectedEndpoints.Select(endpoint => $"• {endpoint}"));
        }
        if (!string.IsNullOrWhiteSpace(incident.PostMortemUrl))
        {
            lines.Add(string.Empty);
            lines.Add($"Postmortem: {incident.PostMortemUrl}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRcaMetaText(RootCauseAnalysis rca)
    {
        List<string> lines = new()
        {
            $"Analyzed: {rca.AnalyzedAt.LocalDateTime:g}",
            $"Sufficient data: {(rca.HasSufficientData ? "Yes" : "No")}",
            $"Hypotheses: {rca.Hypotheses.Count}"
        };
        if (!string.IsNullOrWhiteSpace(rca.Disclaimer))
            lines.Add($"Disclaimer: {rca.Disclaimer}");
        return string.Join(Environment.NewLine, lines);
    }
}
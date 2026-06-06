using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class RemediationWorkflowDialog : Form
{
    private readonly RemediationSuggestion _suggestion;
    private readonly Label _actionValueLabel;
    private readonly Label _riskBadge;
    private readonly TextBox _descriptionBox;
    private readonly ListView _evidenceList;
    private readonly Label _impactWarningLabel;
    private readonly TextBox _commentBox;
    private readonly Button _approveButton;
    private readonly Button _rejectButton;
    private readonly Button _moreInfoButton;

    public string? Comment { get; private set; }
    public bool IsApproved { get; private set; }

    public RemediationWorkflowDialog(RemediationSuggestion suggestion)
    {
        _suggestion = suggestion;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(560, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Text = "⚠ Production Action Approval Required";

        Panel contentPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
        FlowLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };

        layout.Controls.Add(CreateSectionLabel("Action:"));
        _actionValueLabel = new Label { AutoSize = false, Width = 510, Height = 24, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Text = suggestion.Title };
        layout.Controls.Add(_actionValueLabel);

        layout.Controls.Add(CreateSectionLabel("Risk Level:"));
        _riskBadge = new Label { AutoSize = false, Width = 180, Height = 24, Padding = new Padding(10, 0, 10, 0), TextAlign = ContentAlignment.MiddleLeft, Text = $" {suggestion.Risk} ", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        layout.Controls.Add(_riskBadge);

        layout.Controls.Add(CreateSectionLabel("Description:"));
        _descriptionBox = new TextBox { Multiline = true, ReadOnly = true, Width = 510, Height = 72, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, Text = suggestion.Description };
        layout.Controls.Add(_descriptionBox);

        layout.Controls.Add(CreateSectionLabel("Evidence:"));
        _evidenceList = new ListView { Width = 510, Height = 110, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.FixedSingle };
        _evidenceList.Columns.Add("Source", 120);
        _evidenceList.Columns.Add("Description", 360);
        foreach (Evidence evidence in suggestion.Evidence)
        {
            ListViewItem item = new(evidence.Source);
            item.SubItems.Add(evidence.Description);
            _evidenceList.Items.Add(item);
        }
        layout.Controls.Add(_evidenceList);

        _impactWarningLabel = new Label { AutoSize = false, Width = 510, Height = 36, Padding = new Padding(10, 8, 10, 8), Text = "⚠ This action affects production systems. Approval is required." };
        layout.Controls.Add(_impactWarningLabel);

        layout.Controls.Add(CreateSectionLabel("Comment:"));
        _commentBox = new TextBox { Multiline = true, Width = 510, Height = 46, BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Optional: add approval comment..." };
        _commentBox.TextChanged += (_, _) => UpdateApproveState();
        layout.Controls.Add(_commentBox);

        Panel buttonRow = new() { Width = 510, Height = 36 };
        _approveButton = new Button { Text = "✅ Approve", Width = 110, Height = 28, Location = new Point(0, 4), FlatStyle = FlatStyle.Flat };
        _rejectButton = new Button { Text = "❌ Reject", Width = 100, Height = 28, Location = new Point(118, 4), FlatStyle = FlatStyle.Flat };
        _moreInfoButton = new Button { Text = "More Info", Width = 96, Height = 28, Location = new Point(226, 4), FlatStyle = FlatStyle.Flat };
        _approveButton.Click += (_, _) => Approve();
        _rejectButton.Click += (_, _) => Reject();
        _moreInfoButton.Click += (_, _) => AIOpsUiHelper.OpenUrl(GetMoreInfoUrl());
        buttonRow.Controls.AddRange([_approveButton, _rejectButton, _moreInfoButton]);
        layout.Controls.Add(buttonRow);

        contentPanel.Controls.Add(layout);
        Controls.Add(contentPanel);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(ThemeManager.Instance.CurrentTheme);
        UpdateApproveState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private Label CreateSectionLabel(string text)
        => new() { AutoSize = false, Width = 510, Height = 18, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Text = text, Margin = new Padding(0, 2, 0, 2) };

    private void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        ForeColor = theme.Text;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        _actionValueLabel.ForeColor = theme.Text;
        _descriptionBox.BackColor = theme.EditorBackground;
        _descriptionBox.ForeColor = theme.Text;
        _commentBox.BackColor = theme.EditorBackground;
        _commentBox.ForeColor = theme.Text;
        _approveButton.BackColor = theme.PanelBackground;
        _rejectButton.BackColor = theme.PanelBackground;
        _moreInfoButton.BackColor = theme.PanelBackground;
        _approveButton.ForeColor = theme.Text;
        _rejectButton.ForeColor = theme.Text;
        _moreInfoButton.ForeColor = theme.Text;
        _approveButton.FlatAppearance.BorderColor = theme.Border;
        _rejectButton.FlatAppearance.BorderColor = theme.Border;
        _moreInfoButton.FlatAppearance.BorderColor = theme.Border;
        _riskBadge.BackColor = RiskColor(_suggestion.Risk);
        _riskBadge.ForeColor = Color.White;
        _impactWarningLabel.BackColor = Color.FromArgb(40, Color.DarkOrange);
        _impactWarningLabel.ForeColor = Color.DarkOrange;
        AIOpsUiHelper.ApplyListTheme(_evidenceList, theme);
    }

    private void UpdateApproveState()
    {
        bool requiresComment = _suggestion.Risk == RemediationRisk.ProductionImpact;
        _approveButton.Enabled = !requiresComment || !string.IsNullOrWhiteSpace(_commentBox.Text);
        _moreInfoButton.Enabled = !string.IsNullOrWhiteSpace(GetMoreInfoUrl());
    }

    private void Approve()
    {
        Comment = string.IsNullOrWhiteSpace(_commentBox.Text) ? null : _commentBox.Text.Trim();
        IsApproved = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Reject()
    {
        Comment = string.IsNullOrWhiteSpace(_commentBox.Text) ? null : _commentBox.Text.Trim();
        IsApproved = false;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private string? GetMoreInfoUrl() => _suggestion.Evidence.Select(e => e.Url).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

    private static Color RiskColor(RemediationRisk risk) => risk switch
    {
        RemediationRisk.ProductionImpact => Color.IndianRed,
        RemediationRisk.ReviewRequired => Color.Goldenrod,
        _ => Color.ForestGreen
    };
}
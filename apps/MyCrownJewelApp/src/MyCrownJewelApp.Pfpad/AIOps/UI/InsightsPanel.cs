using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Displays AI-generated insights and remediations.
/// </summary>
public sealed class InsightsPanel : UserControl
{
    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _closeButton;
    private readonly FlowLayoutPanel _cardsPanel;

    private Theme _theme;
    private IReadOnlyList<ObservabilityGap> _gaps = Array.Empty<ObservabilityGap>();
    private IReadOnlyList<RemediationSuggestion> _remediations = Array.Empty<RemediationSuggestion>();
    private DeploymentRiskReport? _riskReport;

    /// <summary>
    /// Raised when a remediation should be applied.
    /// </summary>
    public event Action<RemediationSuggestion>? RemediationApplyRequested;

    /// <summary>
    /// Raised when the panel should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Initializes a new insights panel.
    /// </summary>
    public InsightsPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(360, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "💡 INSIGHTS", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Resize += (s, e) => _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _header.Controls.AddRange([_titleLabel, _closeButton]);

        _cardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8)
        };
        _cardsPanel.Resize += (s, e) => ResizeCards();

        Controls.Add(_cardsPanel);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        RebuildCards();
    }

    /// <summary>
    /// Loads gap, remediation, and deployment risk insights.
    /// </summary>
    public void SetInsights(IReadOnlyList<ObservabilityGap> gaps, IReadOnlyList<RemediationSuggestion> remediations, DeploymentRiskReport? risk)
    {
        _gaps = gaps;
        _remediations = remediations;
        _riskReport = risk;
        RebuildCards();
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        _cardsPanel.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = theme.Text;
        RebuildCards();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private void RebuildCards()
    {
        if (IsDisposed)
            return;

        _cardsPanel.SuspendLayout();
        try
        {
            _cardsPanel.Controls.Clear();

            if (_riskReport is not null)
            {
                string description = _riskReport.Recommendations.Count == 0
                    ? "Deployment risk report available."
                    : string.Join(" • ", _riskReport.Recommendations.Take(3));
                _cardsPanel.Controls.Add(CreateInsightCard(
                    "⚠",
                    $"Deployment Risk: {_riskReport.OverallRisk}",
                    description,
                    AIOpsUiHelper.RiskColor(_riskReport.OverallRisk),
                    _riskReport.ConfidenceScore,
                    BuildEvidenceText(_riskReport),
                    null));
            }

            foreach (ObservabilityGap gap in _gaps)
            {
                _cardsPanel.Controls.Add(CreateInsightCard(
                    "📡",
                    gap.Category,
                    gap.Description,
                    GetConfidenceColor(gap.ConfidenceLevel),
                    gap.ConfidenceScore,
                    BuildEvidenceText(gap),
                    null));
            }

            foreach (RemediationSuggestion remediation in _remediations)
            {
                _cardsPanel.Controls.Add(CreateInsightCard(
                    remediation.RequiresApproval ? "🛡" : "🛠",
                    remediation.Title,
                    remediation.Description,
                    GetRemediationColor(remediation),
                    remediation.ConfidenceScore,
                    BuildEvidenceText(remediation),
                    remediation));
            }

            if (_cardsPanel.Controls.Count == 0)
            {
                Label emptyLabel = new()
                {
                    AutoSize = false,
                    Width = Math.Max(280, _cardsPanel.ClientSize.Width - 24),
                    Height = 72,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "No insights available.",
                    ForeColor = AIOpsUiHelper.SecondaryText(_theme),
                    BackColor = _theme.EditorBackground,
                    Margin = new Padding(0, 0, 0, 8)
                };
                _cardsPanel.Controls.Add(emptyLabel);
            }
        }
        finally
        {
            _cardsPanel.ResumeLayout();
            ResizeCards();
        }
    }

    private Panel CreateInsightCard(string icon, string title, string description, Color accentColor, double confidenceScore, string evidenceText, RemediationSuggestion? remediation)
    {
        Panel card = new()
        {
            Height = 132,
            Width = Math.Max(280, _cardsPanel.ClientSize.Width - 24),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = _theme.EditorBackground,
            BorderStyle = BorderStyle.FixedSingle
        };

        Panel accent = new() { Dock = DockStyle.Left, Width = 6, BackColor = accentColor };
        Label titleLabel = new()
        {
            AutoSize = false,
            Location = new Point(16, 10),
            Size = new Size(card.Width - 28, 20),
            Text = $"{icon} {title}",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = _theme.Text,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Label descriptionLabel = new()
        {
            AutoSize = false,
            Location = new Point(16, 34),
            Size = new Size(card.Width - 28, 38),
            Text = description,
            ForeColor = _theme.Text,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Panel confidenceTrack = new()
        {
            Location = new Point(16, 76),
            Size = new Size(card.Width - 28, 8),
            BackColor = _theme.PanelBackground,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        Panel confidenceFill = new()
        {
            Location = new Point(0, 0),
            Height = confidenceTrack.Height,
            Width = Math.Max(8, (int)Math.Round(confidenceTrack.Width * Math.Clamp(confidenceScore, 0d, 1d))),
            BackColor = accentColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Left
        };
        confidenceTrack.Controls.Add(confidenceFill);

        Label evidenceLabel = new()
        {
            AutoSize = false,
            Location = new Point(16, 90),
            Size = new Size(card.Width - 160, 28),
            Text = evidenceText,
            Font = new Font("Segoe UI", 8f, FontStyle.Italic),
            ForeColor = AIOpsUiHelper.SecondaryText(_theme),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        if (remediation is not null)
        {
            if (remediation.Risk == RemediationRisk.Safe)
            {
                Button applyButton = CreateActionButton("Apply Fix", remediation, 98);
                applyButton.Location = new Point(card.Width - applyButton.Width - 12, 92);
                applyButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                card.Controls.Add(applyButton);
            }
            else if (remediation.RequiresApproval)
            {
                Button reviewButton = CreateActionButton("Review & Apply", remediation, 110);
                reviewButton.Location = new Point(card.Width - reviewButton.Width - 12, 92);
                reviewButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                card.Controls.Add(reviewButton);
            }
        }

        card.Controls.AddRange([accent, titleLabel, descriptionLabel, confidenceTrack, evidenceLabel]);
        return card;
    }

    private Button CreateActionButton(string text, RemediationSuggestion remediation, int width)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text
        };
        button.FlatAppearance.BorderColor = _theme.Border;
        button.Click += (s, e) => RemediationApplyRequested?.Invoke(remediation);
        return button;
    }

    private void ResizeCards()
    {
        int width = Math.Max(280, _cardsPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 20);
        foreach (Control control in _cardsPanel.Controls)
            control.Width = width;
    }

    private string BuildEvidenceText(AIOpsInsight insight)
    {
        string source = insight.Evidence.FirstOrDefault()?.Source ?? "Unknown source";
        return $"Source: {source} • {insight.Evidence.Count} piece(s) of evidence";
    }

    private Color GetConfidenceColor(ConfidenceLevel confidenceLevel) => confidenceLevel switch
    {
        ConfidenceLevel.High => Color.ForestGreen,
        ConfidenceLevel.Medium => Color.Goldenrod,
        _ => AIOpsUiHelper.Accent(_theme)
    };

    private Color GetRemediationColor(RemediationSuggestion remediation) => remediation.Risk switch
    {
        RemediationRisk.ProductionImpact => Color.DarkRed,
        RemediationRisk.ReviewRequired => Color.Goldenrod,
        _ => Color.ForestGreen
    };
}

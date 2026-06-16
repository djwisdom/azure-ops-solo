using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Displays security findings and policy violations.
/// </summary>
public sealed class SecurityPanel : UserControl
{
    private sealed record SecurityRow(
        Severity Severity,
        string Category,
        string Rule,
        string FilePath,
        int LineNumber,
        string Title,
        string Description,
        string Remediation,
        string? LinkUrl,
        object Source);

    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _closeButton;
    private readonly Panel _filterBar;
    private readonly ComboBox _severityFilter;
    private readonly ComboBox _categoryFilter;
    private readonly Button _clearFiltersButton;
    private readonly SplitContainer _splitContainer;
    private readonly ListView _findingsList;
    private readonly Label _descriptionTitle;
    private readonly TextBox _descriptionBox;
    private readonly Label _remediationTitle;
    private readonly TextBox _remediationBox;
    private readonly LinkLabel _linkLabel;
    private readonly List<SecurityRow> _rows = [];
    private readonly Panel _hintBar;
    private readonly Label _hintLabel;

    private Theme _theme;

    /// <summary>
    /// Raised when navigation to a finding is requested.
    /// </summary>
    public event Action<string, int>? FindingNavigateRequested;

    /// <summary>
    /// Raised when the panel should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Initializes a new security panel.
    /// </summary>
    public SecurityPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(420, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🔒 SECURITY (0 findings)", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _header.Resize += (s, e) => _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _header.Controls.AddRange([_titleLabel, _closeButton]);

        _filterBar = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 2, 6, 2) };
        _severityFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Location = new Point(6, 4) };
        _severityFilter.Items.AddRange(["All", "Critical", "High", "Medium", "Low"]);
        _severityFilter.SelectedIndex = 0;
        _categoryFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Location = new Point(134, 4) };
        _categoryFilter.Items.AddRange(["All", "Secret", "Injection", "Policy", "Infrastructure"]);
        _categoryFilter.SelectedIndex = 0;
        _clearFiltersButton = new Button { Text = "Clear", Width = 64, Height = 24, Location = new Point(282, 4), FlatStyle = FlatStyle.Flat };
        _clearFiltersButton.Click += (s, e) => ClearFilters();
        _severityFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
        _categoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
        _filterBar.Controls.AddRange([_severityFilter, _categoryFilter, _clearFiltersButton]);

        _splitContainer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260, BorderStyle = BorderStyle.None };

        _findingsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            OwnerDraw = true
        };
        _findingsList.Columns.Add("Severity", 72);
        _findingsList.Columns.Add("Category", 90);
        _findingsList.Columns.Add("Rule", 90);
        _findingsList.Columns.Add("File", 180);
        _findingsList.Columns.Add("Line", 56);
        _findingsList.Columns.Add("Description", 420);
        _findingsList.SelectedIndexChanged += (s, e) => UpdateDetailPanel();
        _findingsList.DoubleClick += FindingsList_DoubleClick;
        _findingsList.DrawColumnHeader += FindingsList_DrawColumnHeader;
        _findingsList.DrawSubItem += FindingsList_DrawSubItem;
        _splitContainer.Panel1.Controls.Add(_findingsList);

        Panel detailPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
        _descriptionTitle = new Label { AutoSize = true, Text = "Description", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Dock = DockStyle.Top };
        _descriptionBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Top, Height = 44, BorderStyle = BorderStyle.None };
        _remediationTitle = new Label { AutoSize = true, Text = "Remediation", Font = new Font("Segoe UI", 9f, FontStyle.Bold), Dock = DockStyle.Top, Padding = new Padding(0, 6, 0, 0) };
        _remediationBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
        _linkLabel = new LinkLabel { Dock = DockStyle.Bottom, Height = 22, Text = "", TextAlign = ContentAlignment.MiddleLeft };
        _linkLabel.LinkClicked += (s, e) => AIOpsUiHelper.OpenUrl(_linkLabel.Tag as string);
        detailPanel.Controls.AddRange([_remediationBox, _remediationTitle, _descriptionBox, _descriptionTitle, _linkLabel]);
        _splitContainer.Panel2.Controls.Add(detailPanel);

        Controls.Add(_splitContainer);
        Controls.Add(_filterBar);
        (_hintBar, _hintLabel) = AIOpsUiHelper.CreateHintBar("ⓘ Scans run locally — no connector required. Open a file or use Scan Active File (Ctrl+Alt+S) to populate findings.");
        Controls.Add(_hintBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
    }

    /// <summary>
    /// Loads findings into the panel.
    /// </summary>
    public void SetFindings(IReadOnlyList<SecurityFinding> findings, IReadOnlyList<PolicyViolation> violations)
    {
        _rows.Clear();
        _rows.AddRange(findings.Select(f => new SecurityRow(
            f.Severity,
            f.Category.ToString(),
            f.RuleId,
            f.FilePath,
            f.LineNumber,
            f.Title,
            f.Description,
            f.Remediation,
            f.CweUrl,
            f)));
        _rows.AddRange(violations.Select(v => new SecurityRow(
            v.Severity,
            "Policy",
            v.PolicyId,
            v.FilePath,
            v.LineNumber,
            v.PolicyName,
            v.Description,
            v.Remediation,
            v.PolicyDocUrl,
            v)));
        ApplyFilters();
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        AIOpsUiHelper.SetHintBarTheme(_hintBar, _hintLabel, theme);
        _splitContainer.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _descriptionTitle.ForeColor = theme.Text;
        _remediationTitle.ForeColor = theme.Text;
        _linkLabel.LinkColor = AIOpsUiHelper.Accent(theme);
        _linkLabel.ActiveLinkColor = Color.DeepSkyBlue;
        _linkLabel.VisitedLinkColor = AIOpsUiHelper.Accent(theme);
        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = theme.Text;
        _clearFiltersButton.BackColor = theme.PanelBackground;
        _clearFiltersButton.ForeColor = theme.Text;
        _clearFiltersButton.FlatAppearance.BorderColor = theme.Border;
        _severityFilter.ApplyTheme(theme);
        _categoryFilter.ApplyTheme(theme);
        _descriptionBox.ApplyTheme(theme);
        _remediationBox.ApplyTheme(theme);
        AIOpsUiHelper.ApplyListTheme(_findingsList, theme);
        _findingsList.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private void ClearFilters()
    {
        _severityFilter.SelectedIndex = 0;
        _categoryFilter.SelectedIndex = 0;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<SecurityRow> filtered = _rows;

        if (_severityFilter.SelectedItem is string severity && !string.Equals(severity, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(r => string.Equals(r.Severity.ToString(), severity, StringComparison.OrdinalIgnoreCase));

        if (_categoryFilter.SelectedItem is string category && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<SecurityRow> materialized = filtered
            .OrderByDescending(r => AIOpsUiHelper.SeverityRank(r.Severity))
            .ThenBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.LineNumber)
            .ToList();

        _findingsList.BeginUpdate();
        try
        {
            _findingsList.Items.Clear();
            foreach (SecurityRow row in materialized)
            {
                var item = new ListViewItem(row.Severity.ToString()) { Tag = row };
                item.SubItems.Add(row.Category);
                item.SubItems.Add(row.Rule);
                item.SubItems.Add(row.FilePath);
                item.SubItems.Add(row.LineNumber <= 0 ? "-" : row.LineNumber.ToString());
                item.SubItems.Add(row.Title);
                _findingsList.Items.Add(item);
            }
        }
        finally
        {
            _findingsList.EndUpdate();
        }

        _titleLabel.Text = $"🔒 SECURITY ({materialized.Count} findings)";
        UpdateDetailPanel();
    }

    private void FindingsList_DoubleClick(object? sender, EventArgs e)
    {
        if (_findingsList.SelectedItems.Count == 0)
            return;

        if (_findingsList.SelectedItems[0].Tag is SecurityRow row)
            FindingNavigateRequested?.Invoke(row.FilePath, Math.Max(1, row.LineNumber));
    }

    private void FindingsList_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using SolidBrush backBrush = new(_theme.MenuBackground);
        using SolidBrush textBrush = new(_theme.Text);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, e.Font ?? _findingsList.Font, e.Bounds, _theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private void FindingsList_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item is null) return;
        SecurityRow? row = e.Item.Tag as SecurityRow;
        Color backColor = e.Item.Selected ? AIOpsUiHelper.CurrentLineBackground(_theme) : _theme.EditorBackground;
        Color foreColor = _theme.Text;

        if (row is not null && !e.Item.Selected)
        {
            switch (row.Severity)
            {
                case Severity.Critical:
                    backColor = Color.FromArgb(96, 70, 0, 0);
                    foreColor = Color.White;
                    break;
                case Severity.High:
                    foreColor = Color.IndianRed;
                    break;
                case Severity.Medium:
                    foreColor = Color.Goldenrod;
                    break;
                case Severity.Low:
                    foreColor = AIOpsUiHelper.SecondaryText(_theme);
                    break;
            }
        }

        using SolidBrush backBrush = new(backColor);
        e.Graphics.FillRectangle(backBrush, e.Bounds);
        Rectangle textBounds = Rectangle.Inflate(e.Bounds, -4, 0);
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, _findingsList.Font, textBounds, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void UpdateDetailPanel()
    {
        if (_findingsList.SelectedItems.Count == 0 || _findingsList.SelectedItems[0].Tag is not SecurityRow row)
        {
            _descriptionBox.Text = "Select a finding to view details.";
            _remediationBox.Text = string.Empty;
            _linkLabel.Text = string.Empty;
            _linkLabel.Tag = null;
            return;
        }

        _descriptionBox.Text = row.Description;
        _remediationBox.Text = row.Remediation;
        _linkLabel.Text = string.IsNullOrWhiteSpace(row.LinkUrl) ? string.Empty : row.LinkUrl;
        _linkLabel.Tag = row.LinkUrl;
    }
}

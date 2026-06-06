using System.ComponentModel;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Configures AIOps connectors and runtime options.
/// </summary>
public sealed class AIOpsSettingsDialog : Form
{
    private readonly AIOpsSettings _settings;
    private readonly TabControl _tabs;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _applyButton;

    private readonly CheckBox _enabledCheckBox;
    private readonly CheckBox _mockDataCheckBox;
    private readonly NumericUpDown _pollIntervalUpDown;
    private readonly CheckBox _autoScanCheckBox;
    private readonly CheckBox _requireApprovalCheckBox;

    private readonly CheckBox _azureMonitorEnabledCheckBox;
    private readonly TextBox _azureTenantIdTextBox;
    private readonly TextBox _azureClientIdTextBox;
    private readonly TextBox _azureClientSecretTextBox;
    private readonly TextBox _azureSubscriptionIdTextBox;
    private readonly TextBox _azureResourceGroupTextBox;
    private readonly TextBox _azureWorkspaceIdTextBox;
    private readonly Button _azureTestButton;
    private readonly Label _azureStatusLabel;

    private readonly CheckBox _azureDevOpsEnabledCheckBox;
    private readonly TextBox _azureDevOpsOrganizationTextBox;
    private readonly TextBox _azureDevOpsProjectTextBox;
    private readonly TextBox _azureDevOpsPatTextBox;
    private readonly Button _azureDevOpsTestButton;
    private readonly Label _azureDevOpsStatusLabel;

    private readonly CheckBox _kubernetesEnabledCheckBox;
    private readonly TextBox _kubernetesApiServerTextBox;
    private readonly TextBox _kubernetesBearerTokenTextBox;
    private readonly TextBox _kubernetesNamespaceTextBox;
    private readonly CheckBox _kubernetesSkipTlsCheckBox;
    private readonly Button _kubernetesTestButton;
    private readonly Label _kubernetesStatusLabel;

    // Prometheus
    private readonly CheckBox _prometheusEnabledCheckBox;
    private readonly TextBox _prometheusBaseUrlTextBox;
    private readonly TextBox _prometheusUsernameTextBox;
    private readonly TextBox _prometheusPasswordTextBox;
    private readonly TextBox _prometheusBearerTokenTextBox;

    // PagerDuty
    private readonly CheckBox _pagerDutyEnabledCheckBox;
    private readonly TextBox _pagerDutyApiTokenTextBox;
    private readonly TextBox _pagerDutyServiceIdTextBox;

    // GitHub Actions
    private readonly CheckBox _githubEnabledCheckBox;
    private readonly TextBox _githubOwnerTextBox;
    private readonly TextBox _githubRepositoryTextBox;
    private readonly TextBox _githubPatTextBox;

    private readonly ToolTip _secretToolTip = new();
    private Theme _theme;

    /// <summary>Gets or sets the Azure Monitor connector used by the Test Connection action.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? AzureMonitorConnector { get; set; }

    /// <summary>Gets or sets the Azure DevOps connector used by the Test Connection action.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? AzureDevOpsConnector { get; set; }

    /// <summary>Gets or sets the Kubernetes connector used by the Test Connection action.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? KubernetesConnector { get; set; }

    /// <summary>
    /// Raised when settings have been saved or applied.
    /// </summary>
    public event Action<AIOpsSettings>? SettingsSaved;

    /// <summary>
    /// Initializes a new settings dialog.
    /// </summary>
    public AIOpsSettingsDialog(AIOpsSettings settings)
    {
        _settings = settings ?? new AIOpsSettings();
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "AIOps Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimumSize = new Size(640, 520);
        ClientSize = new Size(760, 560);

        _tabs = new TabControl { Dock = DockStyle.Fill };

        (_enabledCheckBox, _mockDataCheckBox, _pollIntervalUpDown, _autoScanCheckBox, _requireApprovalCheckBox) = BuildGeneralTab();
        (_azureMonitorEnabledCheckBox, _azureTenantIdTextBox, _azureClientIdTextBox, _azureClientSecretTextBox, _azureSubscriptionIdTextBox, _azureResourceGroupTextBox, _azureWorkspaceIdTextBox, _azureTestButton, _azureStatusLabel) = BuildAzureMonitorTab();
        (_azureDevOpsEnabledCheckBox, _azureDevOpsOrganizationTextBox, _azureDevOpsProjectTextBox, _azureDevOpsPatTextBox, _azureDevOpsTestButton, _azureDevOpsStatusLabel) = BuildAzureDevOpsTab();
        (_kubernetesEnabledCheckBox, _kubernetesApiServerTextBox, _kubernetesBearerTokenTextBox, _kubernetesNamespaceTextBox, _kubernetesSkipTlsCheckBox, _kubernetesTestButton, _kubernetesStatusLabel) = BuildKubernetesTab();
        (_prometheusEnabledCheckBox, _prometheusBaseUrlTextBox, _prometheusUsernameTextBox, _prometheusPasswordTextBox, _prometheusBearerTokenTextBox) = BuildPrometheusTab();
        (_pagerDutyEnabledCheckBox, _pagerDutyApiTokenTextBox, _pagerDutyServiceIdTextBox) = BuildPagerDutyTab();
        (_githubEnabledCheckBox, _githubOwnerTextBox, _githubRepositoryTextBox, _githubPatTextBox) = BuildGitHubActionsTab();

        // DPAPI tooltips on all secret fields
        string dpapiTip = "🔒 DPAPI-protected — value is encrypted with your Windows user account and stored as EncryptedAuthKey in settings.json";
        foreach (var tb in new[] { _azureClientSecretTextBox, _azureDevOpsPatTextBox, _kubernetesBearerTokenTextBox,
                                   _prometheusPasswordTextBox, _prometheusBearerTokenTextBox,
                                   _pagerDutyApiTokenTextBox, _githubPatTextBox })
            _secretToolTip.SetToolTip(tb, dpapiTip);

        Panel buttonsPanel = new() { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8) };
        _saveButton = new Button { Text = "Save", Width = 88, Height = 26, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, FlatStyle = FlatStyle.Flat };
        _cancelButton = new Button { Text = "Cancel", Width = 88, Height = 26, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        _applyButton = new Button { Text = "Apply", Width = 88, Height = 26, Anchor = AnchorStyles.Right | AnchorStyles.Bottom, FlatStyle = FlatStyle.Flat };
        buttonsPanel.Resize += (s, e) =>
        {
            _cancelButton.Location = new Point(buttonsPanel.ClientSize.Width - _cancelButton.Width - 8, 8);
            _saveButton.Location = new Point(_cancelButton.Left - _saveButton.Width - 8, 8);
            _applyButton.Location = new Point(_saveButton.Left - _applyButton.Width - 8, 8);
        };

        _saveButton.Click += (s, e) => SaveAndClose();
        _applyButton.Click += (s, e) => ApplySettings();
        _cancelButton.Click += (s, e) => Close();
        _azureTestButton.Click += async (s, e) => await TestConnectionAsync(AzureMonitorConnector, _azureStatusLabel);
        _azureDevOpsTestButton.Click += async (s, e) => await TestConnectionAsync(AzureDevOpsConnector, _azureDevOpsStatusLabel);
        _kubernetesTestButton.Click += async (s, e) => await TestConnectionAsync(KubernetesConnector, _kubernetesStatusLabel);
        buttonsPanel.Controls.AddRange([_applyButton, _saveButton, _cancelButton]);

        Controls.Add(_tabs);
        Controls.Add(buttonsPanel);

        LoadSettings();
        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
    }

    /// <summary>
    /// Applies the active editor theme.
    /// </summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        foreach (Button button in new[] { _saveButton, _cancelButton, _applyButton, _azureTestButton, _azureDevOpsTestButton, _kubernetesTestButton })
        {
            button.BackColor = theme.PanelBackground;
            button.ForeColor = theme.Text;
            button.FlatAppearance.BorderColor = theme.Border;
        }

        foreach (Label label in new[] { _azureStatusLabel, _azureDevOpsStatusLabel, _kubernetesStatusLabel })
            label.ForeColor = AIOpsUiHelper.SecondaryText(theme);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private (CheckBox enabled, CheckBox mockData, NumericUpDown pollInterval, CheckBox autoScan, CheckBox requireApproval) BuildGeneralTab()
    {
        TabPage page = new("General");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable AIOps", AutoSize = true };
        CheckBox mockData = new() { Text = "Mock data mode", AutoSize = true };
        NumericUpDown pollInterval = new() { Minimum = 5, Maximum = 3600, Width = 120 };
        CheckBox autoScan = new() { Text = "Auto-scan on save", AutoSize = true };
        CheckBox requireApproval = new() { Text = "Require approval for production", AutoSize = true };

        AddRow(layout, 0, new Label { Text = "General", AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) }, enabled);
        AddRow(layout, 1, new Label { Text = "Mode", AutoSize = true }, mockData);
        AddRow(layout, 2, new Label { Text = "Poll interval (seconds)", AutoSize = true }, pollInterval);
        AddRow(layout, 3, new Label { Text = "Scanning", AutoSize = true }, autoScan);
        AddRow(layout, 4, new Label { Text = "Production safety", AutoSize = true }, requireApproval);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, mockData, pollInterval, autoScan, requireApproval);
    }

    private (CheckBox enabled, TextBox tenantId, TextBox clientId, TextBox clientSecret, TextBox subscriptionId, TextBox resourceGroup, TextBox workspaceId, Button testButton, Label statusLabel) BuildAzureMonitorTab()
    {
        TabPage page = new("Azure Monitor");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable Azure Monitor", AutoSize = true };
        TextBox tenantId = new();
        TextBox clientId = new();
        TextBox clientSecret = new() { UseSystemPasswordChar = true };
        TextBox subscriptionId = new();
        TextBox resourceGroup = new();
        TextBox workspaceId = new();
        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "Tenant ID", AutoSize = true }, tenantId);
        AddRow(layout, 2, new Label { Text = "Client ID", AutoSize = true }, clientId);
        AddRow(layout, 3, new Label { Text = "Client Secret", AutoSize = true }, clientSecret);
        AddRow(layout, 4, new Label { Text = "Subscription ID", AutoSize = true }, subscriptionId);
        AddRow(layout, 5, new Label { Text = "Resource Group", AutoSize = true }, resourceGroup);
        AddRow(layout, 6, new Label { Text = "Workspace ID", AutoSize = true }, workspaceId);
        AddRow(layout, 7, new Label { Text = "Actions", AutoSize = true }, actionPanel);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, tenantId, clientId, clientSecret, subscriptionId, resourceGroup, workspaceId, testButton, statusLabel);
    }

    private (CheckBox enabled, TextBox organization, TextBox project, TextBox pat, Button testButton, Label statusLabel) BuildAzureDevOpsTab()
    {
        TabPage page = new("Azure DevOps");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable Azure DevOps", AutoSize = true };
        TextBox organization = new();
        TextBox project = new();
        TextBox pat = new() { UseSystemPasswordChar = true };
        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "Organization", AutoSize = true }, organization);
        AddRow(layout, 2, new Label { Text = "Project", AutoSize = true }, project);
        AddRow(layout, 3, new Label { Text = "PAT", AutoSize = true }, pat);
        AddRow(layout, 4, new Label { Text = "Actions", AutoSize = true }, actionPanel);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, organization, project, pat, testButton, statusLabel);
    }

    private (CheckBox enabled, TextBox apiServer, TextBox bearerToken, TextBox namespaceText, CheckBox skipTls, Button testButton, Label statusLabel) BuildKubernetesTab()
    {
        TabPage page = new("Kubernetes");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable Kubernetes", AutoSize = true };
        TextBox apiServer = new();
        TextBox bearerToken = new() { UseSystemPasswordChar = true };
        TextBox namespaceText = new();
        CheckBox skipTls = new() { Text = "Skip TLS verify", AutoSize = true };
        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "API Server URL", AutoSize = true }, apiServer);
        AddRow(layout, 2, new Label { Text = "Bearer Token", AutoSize = true }, bearerToken);
        AddRow(layout, 3, new Label { Text = "Namespace", AutoSize = true }, namespaceText);
        AddRow(layout, 4, new Label { Text = "TLS", AutoSize = true }, skipTls);
        AddRow(layout, 5, new Label { Text = "Actions", AutoSize = true }, actionPanel);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, apiServer, bearerToken, namespaceText, skipTls, testButton, statusLabel);
    }

    private (CheckBox enabled, TextBox baseUrl, TextBox username, TextBox password, TextBox bearerToken) BuildPrometheusTab()
    {
        TabPage page = new("Prometheus");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable Prometheus", AutoSize = true };
        TextBox baseUrl = new() { Text = "http://localhost:9090" };
        TextBox username = new();
        TextBox password = new() { UseSystemPasswordChar = true };
        TextBox bearerToken = new() { UseSystemPasswordChar = true };

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "Base URL", AutoSize = true }, baseUrl);
        AddRow(layout, 2, new Label { Text = "Username", AutoSize = true }, username);
        AddRow(layout, 3, new Label { Text = "Password 🔒", AutoSize = true }, password);
        AddRow(layout, 4, new Label { Text = "Bearer Token 🔒", AutoSize = true }, bearerToken);
        AddRow(layout, 5, new Label { Text = "Auth note", AutoSize = true },
            new Label { Text = "Use Bearer Token OR Username/Password, not both.", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, baseUrl, username, password, bearerToken);
    }

    private (CheckBox enabled, TextBox apiToken, TextBox serviceId) BuildPagerDutyTab()
    {
        TabPage page = new("PagerDuty");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable PagerDuty", AutoSize = true };
        TextBox apiToken = new() { UseSystemPasswordChar = true };
        TextBox serviceId = new();

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "API Token 🔒", AutoSize = true }, apiToken);
        AddRow(layout, 2, new Label { Text = "Service ID (optional)", AutoSize = true }, serviceId);
        AddRow(layout, 3, new Label { Text = "Get token", AutoSize = true },
            new Label { Text = "User Icon → My Profile → User Settings → Create API Key", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, apiToken, serviceId);
    }

    private (CheckBox enabled, TextBox owner, TextBox repository, TextBox pat) BuildGitHubActionsTab()
    {
        TabPage page = new("GitHub Actions");
        TableLayoutPanel layout = CreateFormLayout();

        CheckBox enabled = new() { Text = "Enable GitHub Actions", AutoSize = true };
        TextBox owner = new();
        TextBox repository = new();
        TextBox pat = new() { UseSystemPasswordChar = true };

        AddRow(layout, 0, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 1, new Label { Text = "Owner (org or user)", AutoSize = true }, owner);
        AddRow(layout, 2, new Label { Text = "Repository", AutoSize = true }, repository);
        AddRow(layout, 3, new Label { Text = "Personal Access Token 🔒", AutoSize = true }, pat);
        AddRow(layout, 4, new Label { Text = "Required scopes", AutoSize = true },
            new Label { Text = "repo, workflow, read:org", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, owner, repository, pat);
    }

    private static TableLayoutPanel CreateFormLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 10,
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 10; row++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, int row, Control label, Control control)
    {
        label.Margin = new Padding(0, 8, 8, 8);
        control.Margin = new Padding(0, 4, 0, 4);
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (control is TextBox textBox)
            textBox.Width = 320;
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void LoadSettings()
    {
        _enabledCheckBox.Checked = _settings.Enabled;
        _mockDataCheckBox.Checked = _settings.MockDataEnabled;
        decimal pollInterval = Math.Max(_pollIntervalUpDown.Minimum, Math.Min(_pollIntervalUpDown.Maximum, (decimal)_settings.PollIntervalSeconds));
        _pollIntervalUpDown.Value = pollInterval;
        _autoScanCheckBox.Checked = _settings.AutoScanOnSave;
        _requireApprovalCheckBox.Checked = _settings.RequireApprovalForProduction;

        _azureMonitorEnabledCheckBox.Checked = _settings.AzureMonitor.Enabled;
        _azureTenantIdTextBox.Text = _settings.AzureMonitor.TenantId;
        _azureClientIdTextBox.Text = _settings.AzureMonitor.ClientId;
        _azureClientSecretTextBox.Text = _settings.AzureMonitor.ClientSecret;
        _azureSubscriptionIdTextBox.Text = _settings.AzureMonitor.SubscriptionId;
        _azureResourceGroupTextBox.Text = _settings.AzureMonitor.ResourceGroup;
        _azureWorkspaceIdTextBox.Text = _settings.AzureMonitor.WorkspaceId;

        _azureDevOpsEnabledCheckBox.Checked = _settings.AzureDevOps.Enabled;
        _azureDevOpsOrganizationTextBox.Text = _settings.AzureDevOps.Organization;
        _azureDevOpsProjectTextBox.Text = _settings.AzureDevOps.Project;
        _azureDevOpsPatTextBox.Text = _settings.AzureDevOps.PersonalAccessToken;

        _kubernetesEnabledCheckBox.Checked = _settings.Kubernetes.Enabled;
        _kubernetesApiServerTextBox.Text = _settings.Kubernetes.ApiServerUrl;
        _kubernetesBearerTokenTextBox.Text = _settings.Kubernetes.BearerToken;
        _kubernetesNamespaceTextBox.Text = _settings.Kubernetes.Namespace;
        _kubernetesSkipTlsCheckBox.Checked = _settings.Kubernetes.SkipTlsVerify;

        _prometheusEnabledCheckBox.Checked = _settings.Prometheus.Enabled;
        _prometheusBaseUrlTextBox.Text = _settings.Prometheus.BaseUrl;
        _prometheusUsernameTextBox.Text = _settings.Prometheus.Username ?? "";
        _prometheusPasswordTextBox.Text = _settings.Prometheus.Password ?? "";
        _prometheusBearerTokenTextBox.Text = _settings.Prometheus.BearerToken ?? "";

        _pagerDutyEnabledCheckBox.Checked = _settings.PagerDuty.Enabled;
        _pagerDutyApiTokenTextBox.Text = _settings.PagerDuty.ApiToken;
        _pagerDutyServiceIdTextBox.Text = _settings.PagerDuty.ServiceId ?? "";

        _githubEnabledCheckBox.Checked = _settings.GitHubActions.Enabled;
        _githubOwnerTextBox.Text = _settings.GitHubActions.Owner;
        _githubRepositoryTextBox.Text = _settings.GitHubActions.Repository;
        _githubPatTextBox.Text = _settings.GitHubActions.PersonalAccessToken;
    }

    private void ApplySettings()
    {
        _settings.Enabled = _enabledCheckBox.Checked;
        _settings.MockDataEnabled = _mockDataCheckBox.Checked;
        _settings.PollIntervalSeconds = (int)_pollIntervalUpDown.Value;
        _settings.AutoScanOnSave = _autoScanCheckBox.Checked;
        _settings.RequireApprovalForProduction = _requireApprovalCheckBox.Checked;

        _settings.AzureMonitor.Enabled = _azureMonitorEnabledCheckBox.Checked;
        _settings.AzureMonitor.TenantId = _azureTenantIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientId = _azureClientIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientSecret = _azureClientSecretTextBox.Text;
        _settings.AzureMonitor.SubscriptionId = _azureSubscriptionIdTextBox.Text.Trim();
        _settings.AzureMonitor.ResourceGroup = _azureResourceGroupTextBox.Text.Trim();
        _settings.AzureMonitor.WorkspaceId = _azureWorkspaceIdTextBox.Text.Trim();

        _settings.AzureDevOps.Enabled = _azureDevOpsEnabledCheckBox.Checked;
        _settings.AzureDevOps.Organization = _azureDevOpsOrganizationTextBox.Text.Trim();
        _settings.AzureDevOps.Project = _azureDevOpsProjectTextBox.Text.Trim();
        _settings.AzureDevOps.PersonalAccessToken = _azureDevOpsPatTextBox.Text;

        _settings.Kubernetes.Enabled = _kubernetesEnabledCheckBox.Checked;
        _settings.Kubernetes.ApiServerUrl = _kubernetesApiServerTextBox.Text.Trim();
        _settings.Kubernetes.BearerToken = _kubernetesBearerTokenTextBox.Text;
        _settings.Kubernetes.Namespace = _kubernetesNamespaceTextBox.Text.Trim();
        _settings.Kubernetes.SkipTlsVerify = _kubernetesSkipTlsCheckBox.Checked;

        _settings.Prometheus.Enabled = _prometheusEnabledCheckBox.Checked;
        _settings.Prometheus.BaseUrl = _prometheusBaseUrlTextBox.Text.Trim();
        _settings.Prometheus.Username = string.IsNullOrWhiteSpace(_prometheusUsernameTextBox.Text) ? null : _prometheusUsernameTextBox.Text.Trim();
        _settings.Prometheus.Password = string.IsNullOrWhiteSpace(_prometheusPasswordTextBox.Text) ? null : _prometheusPasswordTextBox.Text;
        _settings.Prometheus.BearerToken = string.IsNullOrWhiteSpace(_prometheusBearerTokenTextBox.Text) ? null : _prometheusBearerTokenTextBox.Text;

        _settings.PagerDuty.Enabled = _pagerDutyEnabledCheckBox.Checked;
        _settings.PagerDuty.ApiToken = _pagerDutyApiTokenTextBox.Text;
        _settings.PagerDuty.ServiceId = string.IsNullOrWhiteSpace(_pagerDutyServiceIdTextBox.Text) ? null : _pagerDutyServiceIdTextBox.Text.Trim();

        _settings.GitHubActions.Enabled = _githubEnabledCheckBox.Checked;
        _settings.GitHubActions.Owner = _githubOwnerTextBox.Text.Trim();
        _settings.GitHubActions.Repository = _githubRepositoryTextBox.Text.Trim();
        _settings.GitHubActions.PersonalAccessToken = _githubPatTextBox.Text;

        SettingsSaved?.Invoke(_settings);
    }

    private void SaveAndClose()
    {
        ApplySettings();
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task TestConnectionAsync(IAIOpsConnector? connector, Label statusLabel)
    {
        if (connector is null)
        {
            statusLabel.Text = "Status: connector not configured";
            return;
        }

        statusLabel.Text = "Status: connecting...";
        try
        {
            ConnectorState state = await connector.ConnectAsync();
            string message = state.Status == ConnectorStatus.Connected
                ? $"Status: connected ({state.LastSyncAt?.LocalDateTime:g})"
                : $"Status: {state.Status} {state.ErrorMessage}";
            statusLabel.Text = message.Trim();
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Status: failed - {ex.Message}";
        }
    }
}

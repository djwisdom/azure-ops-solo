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
    private readonly TextBox _azureAppInsightsAppIdTextBox;
    private readonly Button _azureTestButton;
    private readonly Label _azureStatusLabel;
    private readonly RadioButton _azureAuthSpRadio;
    private readonly RadioButton _azureAuthCliRadio;
    private Label _azureClientIdLabel = null!;
    private Label _azureClientSecretLabel = null!;

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
        (_azureMonitorEnabledCheckBox, _azureTenantIdTextBox, _azureClientIdTextBox, _azureClientSecretTextBox, _azureSubscriptionIdTextBox, _azureResourceGroupTextBox, _azureWorkspaceIdTextBox, _azureAppInsightsAppIdTextBox, _azureTestButton, _azureStatusLabel, _azureAuthSpRadio, _azureAuthCliRadio) = BuildAzureMonitorTab();
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

    private (CheckBox enabled, TextBox tenantId, TextBox clientId, TextBox clientSecret, TextBox subscriptionId, TextBox resourceGroup, TextBox workspaceId, TextBox appInsightsAppId, Button testButton, Label statusLabel, RadioButton authSp, RadioButton authCli) BuildAzureMonitorTab()
    {
        TabPage page = new("Azure Monitor");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable Azure Monitor", AutoSize = true };

        // Auth mode selector
        RadioButton authSp  = new() { Text = "Service Principal (Client ID + Secret)", AutoSize = true, Checked = true };
        RadioButton authCli = new() { Text = "az login (current user)", AutoSize = true };
        Button importCliButton = new() { Text = "⬇ Import from az CLI", Width = 160, Height = 24, FlatStyle = FlatStyle.Flat };
        _secretToolTip.SetToolTip(importCliButton, "Run az account show and fill Tenant ID and Subscription ID automatically");

        FlowLayoutPanel authPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        authPanel.Controls.AddRange([authSp, authCli]);

        FlowLayoutPanel importPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        importPanel.Controls.Add(importCliButton);

        TextBox tenantId = new();
        var clientIdLabel = new Label { Text = "Client ID", AutoSize = true };
        TextBox clientId = new();
        var clientSecretLabel = new Label { Text = "Client Secret 🔒", AutoSize = true };
        var (clientSecret, clientSecretWrapper) = CreateSecretField(_settings.AzureMonitor.EncryptedClientSecret);
        TextBox subscriptionId = new();
        TextBox resourceGroup = new();
        TextBox workspaceId = new();

        Button importWsButton = new() { Text = "⬇ Import Workspaces", Width = 160, Height = 24, FlatStyle = FlatStyle.Flat };
        _secretToolTip.SetToolTip(importWsButton, "List Log Analytics workspaces for the selected subscription and auto-fill Resource Group + Workspace ID");
        FlowLayoutPanel importWsPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        importWsPanel.Controls.Add(importWsButton);

        TextBox appInsightsAppId = new() { PlaceholderText = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" };
        Button importAiButton = new() { Text = "⬇ Import App Insights", Width = 170, Height = 24, FlatStyle = FlatStyle.Flat };
        _secretToolTip.SetToolTip(importAiButton, "List Application Insights components in this subscription and fill App ID");
        FlowLayoutPanel importAiPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        importAiPanel.Controls.Add(importAiButton);

        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 1,  new Label { Text = "Connector",      AutoSize = true }, enabled);
        AddRow(layout, 2,  new Label { Text = "Auth Mode",      AutoSize = true }, authPanel);
        AddRow(layout, 3,  new Label { Text = "",               AutoSize = true }, importPanel);
        AddRow(layout, 4,  new Label { Text = "Tenant ID",      AutoSize = true }, tenantId);
        AddRow(layout, 5,  clientIdLabel,                                           clientId);
        AddRow(layout, 6,  clientSecretLabel,                                       clientSecretWrapper);
        AddRow(layout, 7,  new Label { Text = "Subscription ID", AutoSize = true }, subscriptionId);
        AddRow(layout, 8,  new Label { Text = "Resource Group",  AutoSize = true }, resourceGroup);
        AddRow(layout, 9,  new Label { Text = "Workspace ID",    AutoSize = true }, workspaceId);
        AddRow(layout, 10, new Label { Text = "",                AutoSize = true }, importWsPanel);
        AddRow(layout, 11, new Label { Text = "App Insights App ID", AutoSize = true }, appInsightsAppId);
        AddRow(layout, 12, new Label { Text = "",                AutoSize = true }, importAiPanel);
        AddRow(layout, 13, new Label { Text = "Actions",         AutoSize = true }, actionPanel);

        // Store labels so Load/Save can access them
        _azureClientIdLabel     = clientIdLabel;
        _azureClientSecretLabel = clientSecretLabel;

        // Toggle SP fields when auth mode changes
        void ApplyAuthMode()
        {
            bool isSp = authSp.Checked;
            clientIdLabel.Enabled     = isSp;
            clientId.Enabled          = isSp;
            clientSecretLabel.Enabled = isSp;
            clientSecretWrapper.Enabled = isSp;
            clientSecret.Enabled      = isSp;
            importCliButton.Enabled   = authCli.Checked;
        }
        authSp.CheckedChanged  += (_, _) => ApplyAuthMode();
        authCli.CheckedChanged += (_, _) => ApplyAuthMode();
        ApplyAuthMode();

        // Import from az CLI: list subscriptions and let user pick if multiple exist
        importCliButton.Click += async (s, e) =>
        {
            statusLabel.Text = "Status: querying az CLI subscriptions…";
            try
            {
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("az")
                    { ArgumentList = { "account", "list", "--output", "json", "--all" },
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                    }
                };
                proc.Start();
                string output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(true);
                await proc.WaitForExitAsync().ConfigureAwait(true);

                if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    statusLabel.Text = "Status: az CLI import failed — run 'az login' first.";
                    return;
                }

                // Parse subscription list
                using var doc = System.Text.Json.JsonDocument.Parse(output);
                var subs = new List<(string Id, string Name, string TenantId, bool IsDefault)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string id       = el.TryGetProperty("id",           out var p1) ? p1.GetString() ?? "" : "";
                    string name     = el.TryGetProperty("name",         out var p2) ? p2.GetString() ?? "" : "";
                    string tenant   = el.TryGetProperty("tenantId",     out var p3) ? p3.GetString() ?? "" : "";
                    bool   isDefault = el.TryGetProperty("isDefault",   out var p4) && p4.GetBoolean();
                    if (!string.IsNullOrWhiteSpace(id))
                        subs.Add((id, name, tenant, isDefault));
                }

                if (subs.Count == 0)
                {
                    statusLabel.Text = "Status: no subscriptions found — run 'az login' first.";
                    return;
                }

                // If only one subscription, use it directly
                var chosen = subs.Count == 1 ? subs[0] : PickSubscription(subs);
                if (chosen == default)
                {
                    statusLabel.Text = "Status: import cancelled.";
                    return;
                }

                tenantId.Text      = chosen.TenantId;
                subscriptionId.Text = chosen.Id;
                string label = subs.Count == 1 ? "imported" : "imported (selected)";
                statusLabel.Text = $"Status: {label} — {chosen.Name} ✓";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Status: import error — {ex.Message}";
            }
        };

        // Import Workspaces: list Log Analytics workspaces for the current subscription
        importWsButton.Click += async (s, e) =>
        {
            string subId = subscriptionId.Text.Trim();
            if (string.IsNullOrWhiteSpace(subId))
            {
                statusLabel.Text = "Status: enter or import a Subscription ID first.";
                return;
            }
            statusLabel.Text = "Status: querying workspaces…";
            try
            {
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("az")
                    {
                        ArgumentList = { "monitor", "log-analytics", "workspace", "list", "--subscription", subId, "--output", "json" },
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                    }
                };
                proc.Start();
                string output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(true);
                await proc.WaitForExitAsync().ConfigureAwait(true);

                if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    statusLabel.Text = "Status: workspace query failed — check subscription ID and az login.";
                    return;
                }

                using var doc = System.Text.Json.JsonDocument.Parse(output);
                var workspaces = new List<(string WorkspaceId, string Name, string ResourceGroup, string Location)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    // customerId is the Log Analytics Workspace GUID used by the API
                    string wsId  = el.TryGetProperty("customerId",     out var p1) ? p1.GetString() ?? "" : "";
                    string name  = el.TryGetProperty("name",           out var p2) ? p2.GetString() ?? "" : "";
                    string rg    = el.TryGetProperty("resourceGroup",  out var p3) ? p3.GetString() ?? "" : "";
                    string loc   = el.TryGetProperty("location",       out var p4) ? p4.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(wsId))
                        workspaces.Add((wsId, name, rg, loc));
                }

                if (workspaces.Count == 0)
                {
                    statusLabel.Text = "Status: no Log Analytics workspaces found in this subscription.";
                    return;
                }

                var chosen = workspaces.Count == 1 ? workspaces[0] : PickWorkspace(workspaces);
                if (chosen == default)
                {
                    statusLabel.Text = "Status: workspace import cancelled.";
                    return;
                }

                resourceGroup.Text = chosen.ResourceGroup;
                workspaceId.Text   = chosen.WorkspaceId;
                string lbl2 = workspaces.Count == 1 ? "imported" : "imported (selected)";
                statusLabel.Text = $"Status: {lbl2} — {chosen.Name} ✓";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Status: workspace import error — {ex.Message}";
            }
        };

        // Import App Insights: list Application Insights components in the subscription
        importAiButton.Click += async (s, e) =>
        {
            string subId = subscriptionId.Text.Trim();
            if (string.IsNullOrWhiteSpace(subId))
            {
                statusLabel.Text = "Status: enter or import a Subscription ID first.";
                return;
            }
            statusLabel.Text = "Status: querying App Insights components…";
            try
            {
                // Use 'az rest' against the ARM API — no extension required.
                // az monitor app-insights component list requires the application-insights extension.
                string armUrl = $"https://management.azure.com/subscriptions/{subId}/providers/Microsoft.Insights/components?api-version=2020-02-02";
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo("az")
                    {
                        ArgumentList = { "rest", "--method", "get", "--url", armUrl, "--output", "json" },
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                    }
                };
                proc.Start();
                string output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(true);
                string stderr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(true);
                await proc.WaitForExitAsync().ConfigureAwait(true);

                if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    string hint = stderr.Contains("az login", StringComparison.OrdinalIgnoreCase)
                        ? "Run 'az login' first."
                        : stderr.Length > 0 ? stderr.Trim().Split('\n')[0] : "Check subscription ID and az login.";
                    statusLabel.Text = $"Status: App Insights query failed — {hint}";
                    return;
                }

                using var doc = System.Text.Json.JsonDocument.Parse(output);
                // ARM list response wraps items in a "value" array
                var root = doc.RootElement;
                var items = root.TryGetProperty("value", out var val) ? val : root;

                var components = new List<(string AppId, string Name, string ResourceGroup, string Location)>();
                foreach (var el in items.EnumerateArray())
                {
                    string name  = el.TryGetProperty("name",          out var p1) ? p1.GetString() ?? "" : "";
                    string rg    = el.TryGetProperty("resourceGroup", out var p2) ? p2.GetString() ?? "" : "";
                    string loc   = el.TryGetProperty("location",      out var p3) ? p3.GetString() ?? "" : "";
                    string appId = "";
                    if (el.TryGetProperty("properties", out var props) && props.TryGetProperty("AppId", out var aid))
                        appId = aid.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(appId))
                        components.Add((appId, name, rg, loc));
                }

                if (components.Count == 0)
                {
                    statusLabel.Text = "Status: no Application Insights components found in this subscription.";
                    return;
                }

                var chosen = components.Count == 1 ? components[0] : PickAppInsightsComponent(components);
                if (chosen == default)
                {
                    statusLabel.Text = "Status: App Insights import cancelled.";
                    return;
                }

                appInsightsAppId.Text  = chosen.AppId;
                if (string.IsNullOrWhiteSpace(resourceGroup.Text)) resourceGroup.Text = chosen.ResourceGroup;
                string lbl3 = components.Count == 1 ? "imported" : "imported (selected)";
                statusLabel.Text = $"Status: {lbl3} — {chosen.Name} ✓";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Status: App Insights import error — {ex.Message}";
            }
        };

        layout.RowCount = 14;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, tenantId, clientId, clientSecret, subscriptionId, resourceGroup, workspaceId, appInsightsAppId, testButton, statusLabel, authSp, authCli);
    }

    private (CheckBox enabled, TextBox organization, TextBox project, TextBox pat, Button testButton, Label statusLabel) BuildAzureDevOpsTab()
    {
        TabPage page = new("Azure DevOps");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable Azure DevOps", AutoSize = true };
        TextBox organization = new();
        TextBox project = new();
        var (pat, patWrapper) = CreateSecretField(_settings.AzureDevOps.EncryptedPersonalAccessToken);
        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 1, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 2, new Label { Text = "Organization", AutoSize = true }, organization);
        AddRow(layout, 3, new Label { Text = "Project", AutoSize = true }, project);
        AddRow(layout, 4, new Label { Text = "PAT 🔒", AutoSize = true }, patWrapper);
        AddRow(layout, 5, new Label { Text = "Actions", AutoSize = true }, actionPanel);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, organization, project, pat, testButton, statusLabel);
    }

    private (CheckBox enabled, TextBox apiServer, TextBox bearerToken, TextBox namespaceText, CheckBox skipTls, Button testButton, Label statusLabel) BuildKubernetesTab()
    {
        TabPage page = new("Kubernetes");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable Kubernetes", AutoSize = true };
        TextBox apiServer = new();
        var (bearerToken, bearerTokenWrapper) = CreateSecretField(_settings.Kubernetes.EncryptedBearerToken);
        TextBox namespaceText = new();
        CheckBox skipTls = new() { Text = "Skip TLS verify", AutoSize = true };
        Button testButton = new() { Text = "Test Connection", Width = 120, Height = 26, FlatStyle = FlatStyle.Flat };
        Label statusLabel = new() { Text = "Status: not tested", AutoSize = true };
        FlowLayoutPanel actionPanel = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        actionPanel.Controls.AddRange([testButton, statusLabel]);

        AddRow(layout, 1, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 2, new Label { Text = "API Server URL", AutoSize = true }, apiServer);
        AddRow(layout, 3, new Label { Text = "Bearer Token 🔒", AutoSize = true }, bearerTokenWrapper);
        AddRow(layout, 4, new Label { Text = "Namespace", AutoSize = true }, namespaceText);
        AddRow(layout, 5, new Label { Text = "TLS", AutoSize = true }, skipTls);
        AddRow(layout, 6, new Label { Text = "Actions", AutoSize = true }, actionPanel);

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, apiServer, bearerToken, namespaceText, skipTls, testButton, statusLabel);
    }

    private (CheckBox enabled, TextBox baseUrl, TextBox username, TextBox password, TextBox bearerToken) BuildPrometheusTab()
    {
        TabPage page = new("Prometheus");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable Prometheus", AutoSize = true };
        TextBox baseUrl = new() { Text = "http://localhost:9090" };
        TextBox username = new();
        var (password, passwordWrapper) = CreateSecretField(_settings.Prometheus.EncryptedPassword);
        var (bearerToken, bearerTokenWrapper) = CreateSecretField(_settings.Prometheus.EncryptedBearerToken);

        AddRow(layout, 1, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 2, new Label { Text = "Base URL", AutoSize = true }, baseUrl);
        AddRow(layout, 3, new Label { Text = "Username", AutoSize = true }, username);
        AddRow(layout, 4, new Label { Text = "Password 🔒", AutoSize = true }, passwordWrapper);
        AddRow(layout, 5, new Label { Text = "Bearer Token 🔒", AutoSize = true }, bearerTokenWrapper);
        AddRow(layout, 6, new Label { Text = "Auth note", AutoSize = true },
            new Label { Text = "Use Bearer Token OR Username/Password, not both.", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, baseUrl, username, password, bearerToken);
    }

    private (CheckBox enabled, TextBox apiToken, TextBox serviceId) BuildPagerDutyTab()
    {
        TabPage page = new("PagerDuty");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable PagerDuty", AutoSize = true };
        var (apiToken, apiTokenWrapper) = CreateSecretField(_settings.PagerDuty.EncryptedApiToken);
        TextBox serviceId = new();

        AddRow(layout, 1, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 2, new Label { Text = "API Token 🔒", AutoSize = true }, apiTokenWrapper);
        AddRow(layout, 3, new Label { Text = "Service ID (optional)", AutoSize = true }, serviceId);
        AddRow(layout, 4, new Label { Text = "Get token", AutoSize = true },
            new Label { Text = "User Icon → My Profile → User Settings → Create API Key", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, apiToken, serviceId);
    }

    private (CheckBox enabled, TextBox owner, TextBox repository, TextBox pat) BuildGitHubActionsTab()
    {
        TabPage page = new("GitHub Actions");
        TableLayoutPanel layout = CreateFormLayout();
        InsertDpapiBannerRow(layout);

        CheckBox enabled = new() { Text = "Enable GitHub Actions", AutoSize = true };
        TextBox owner = new();
        TextBox repository = new();
        var (pat, patWrapper) = CreateSecretField(_settings.GitHubActions.EncryptedPersonalAccessToken);

        AddRow(layout, 1, new Label { Text = "Connector", AutoSize = true }, enabled);
        AddRow(layout, 2, new Label { Text = "Owner (org or user)", AutoSize = true }, owner);
        AddRow(layout, 3, new Label { Text = "Repository", AutoSize = true }, repository);
        AddRow(layout, 4, new Label { Text = "Personal Access Token 🔒", AutoSize = true }, patWrapper);
        AddRow(layout, 5, new Label { Text = "Required scopes", AutoSize = true },
            new Label { Text = "repo, workflow, read:org", AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
        return (enabled, owner, repository, pat);
    }

    private static (string AppId, string Name, string ResourceGroup, string Location) PickAppInsightsComponent(
        List<(string AppId, string Name, string ResourceGroup, string Location)> components)
    {
        using var dlg = new Form
        {
            Text            = "Select Application Insights Component",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            ClientSize      = new Size(620, 300),
            MinimizeBox     = false, MaximizeBox     = false,
        };
        var lbl  = new Label { Text = $"Found {components.Count} components. Select one:", AutoSize = true, Location = new Point(12, 12) };
        var list = new ListView
        {
            Location = new Point(12, 36), Size = new Size(596, 180),
            View = View.Details, FullRowSelect = true, MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Name",           200);
        list.Columns.Add("App ID",         240);
        list.Columns.Add("Resource Group", 110);
        list.Columns.Add("Location",        70);
        foreach (var c in components)
        {
            var item = new ListViewItem(c.Name);
            item.SubItems.Add(c.AppId); item.SubItems.Add(c.ResourceGroup); item.SubItems.Add(c.Location);
            item.Tag = c; list.Items.Add(item);
        }
        if (list.Items.Count > 0) { list.Items[0].Selected = true; list.Items[0].EnsureVisible(); }
        var okBtn     = new Button { Text = "Select", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK,     Location = new Point(dlg.ClientSize.Width - 196, 236) };
        var cancelBtn = new Button { Text = "Cancel", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel, Location = new Point(dlg.ClientSize.Width - 100, 236) };
        dlg.AcceptButton = okBtn; dlg.CancelButton = cancelBtn;
        dlg.Controls.AddRange([lbl, list, okBtn, cancelBtn]);
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0) return default;
        return ((string AppId, string Name, string ResourceGroup, string Location))list.SelectedItems[0].Tag!;
    }

    /// <summary>
    /// Shows a flat workspace picker dialog and returns the chosen entry, or <c>default</c> if cancelled.
    /// </summary>
    private static (string WorkspaceId, string Name, string ResourceGroup, string Location) PickWorkspace(
        List<(string WorkspaceId, string Name, string ResourceGroup, string Location)> workspaces)
    {
        using var dlg = new Form
        {
            Text            = "Select Log Analytics Workspace",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            ClientSize      = new Size(620, 320),
            MinimizeBox     = false,
            MaximizeBox     = false,
        };

        var lbl = new Label
        {
            Text     = $"Found {workspaces.Count} workspaces. Select one:",
            AutoSize = true,
            Location = new Point(12, 12),
        };

        var list = new ListView
        {
            Location      = new Point(12, 36),
            Size          = new Size(596, 200),
            View          = View.Details,
            FullRowSelect = true,
            MultiSelect   = false,
            BorderStyle   = BorderStyle.FixedSingle,
            Font          = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Workspace Name",  180);
        list.Columns.Add("Workspace ID (customerId)", 240);
        list.Columns.Add("Resource Group",  120);
        list.Columns.Add("Location",        80);

        foreach (var w in workspaces)
        {
            var item = new ListViewItem(w.Name);
            item.SubItems.Add(w.WorkspaceId);
            item.SubItems.Add(w.ResourceGroup);
            item.SubItems.Add(w.Location);
            item.Tag = w;
            list.Items.Add(item);
        }
        if (list.Items.Count > 0) { list.Items[0].Selected = true; list.Items[0].EnsureVisible(); }

        var okBtn     = new Button { Text = "Select", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var cancelBtn = new Button { Text = "Cancel", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        okBtn.Location     = new Point(dlg.ClientSize.Width - 196, 254);
        cancelBtn.Location = new Point(dlg.ClientSize.Width - 100, 254);

        dlg.AcceptButton = okBtn;
        dlg.CancelButton = cancelBtn;
        dlg.Controls.AddRange([lbl, list, okBtn, cancelBtn]);
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };

        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0)
            return default;

        return ((string WorkspaceId, string Name, string ResourceGroup, string Location))list.SelectedItems[0].Tag!;
    }

    /// <summary>
    /// Shows a flat subscription picker dialog and returns the chosen entry,
    /// or <c>default</c> if the user cancels. The default subscription is
    /// pre-selected in the list.
    /// </summary>
    private static (string Id, string Name, string TenantId, bool IsDefault) PickSubscription(
        List<(string Id, string Name, string TenantId, bool IsDefault)> subscriptions)
    {
        using var dlg = new Form
        {
            Text            = "Select Azure Subscription",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            ClientSize      = new Size(560, 340),
            MinimizeBox     = false,
            MaximizeBox     = false,
        };

        var lbl = new Label
        {
            Text      = $"Found {subscriptions.Count} subscriptions. Select one to use:",
            AutoSize  = true,
            Location  = new Point(12, 12),
        };

        var list = new ListView
        {
            Location      = new Point(12, 36),
            Size          = new Size(536, 220),
            View          = View.Details,
            FullRowSelect = true,
            MultiSelect   = false,
            BorderStyle   = BorderStyle.FixedSingle,
            Font          = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Subscription Name", 240);
        list.Columns.Add("Subscription ID",   180);
        list.Columns.Add("Tenant ID",         110);

        int defaultIndex = 0;
        for (int i = 0; i < subscriptions.Count; i++)
        {
            var s = subscriptions[i];
            var item = new ListViewItem(s.IsDefault ? $"★ {s.Name}" : s.Name);
            item.SubItems.Add(s.Id);
            item.SubItems.Add(s.TenantId);
            item.Tag = s;
            list.Items.Add(item);
            if (s.IsDefault) defaultIndex = i;
        }
        if (list.Items.Count > 0)
        {
            list.Items[defaultIndex].Selected = true;
            list.Items[defaultIndex].EnsureVisible();
        }

        var okBtn     = new Button { Text = "Select",  Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        var cancelBtn = new Button { Text = "Cancel",  Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        okBtn.Location     = new Point(dlg.ClientSize.Width - 196, 278);
        cancelBtn.Location = new Point(dlg.ClientSize.Width - 100, 278);

        dlg.AcceptButton = okBtn;
        dlg.CancelButton = cancelBtn;
        dlg.Controls.AddRange([lbl, list, okBtn, cancelBtn]);

        // Double-click selects immediately
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };

        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0)
            return default;

        return ((string Id, string Name, string TenantId, bool IsDefault))list.SelectedItems[0].Tag!;
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

    /// <summary>
    /// Inserts a DPAPI encryption notice as the first row (row 0) of a connector
    /// tab layout. All subsequent <see cref="AddRow"/> calls in that tab should
    /// start at row 1.
    /// </summary>
    private static void InsertDpapiBannerRow(TableLayoutPanel layout)
    {
        Panel banner = new()
        {
            BackColor = Color.FromArgb(232, 244, 255),
            Dock = DockStyle.Fill,
            Height = 28,
            Padding = new Padding(6, 4, 0, 0),
            Margin = new Padding(0, 0, 0, 4),
        };
        Label bannerLabel = new()
        {
            Text = "🔒  Secret fields are protected using Windows DPAPI and stored as EncryptedAuthKey — tied to your Windows user account on this machine.",
            ForeColor = Color.FromArgb(0, 70, 140),
            AutoSize = true,
            Font = new Font("Segoe UI", 8f),
        };
        banner.Controls.Add(bannerLabel);

        layout.Controls.Add(banner, 0, 0);
        layout.SetColumnSpan(banner, 2);
        layout.RowStyles[0] = new RowStyle(SizeType.Absolute, 36);
    }

    /// <summary>
    /// Creates a secret text field wrapped with a live encryption-status badge.
    /// The returned <see cref="TextBox"/> is the actual input field; the returned
    /// <see cref="Panel"/> is the composite control to pass to <see cref="AddRow"/>.
    /// </summary>
    /// <param name="encryptedValue">
    /// The current DPAPI-encrypted value stored on disk (e.g. <c>EncryptedClientSecret</c>).
    /// An empty string means the secret has not been stored yet.
    /// </param>
    private static (TextBox textBox, Panel wrapper) CreateSecretField(string encryptedValue)
    {
        TextBox tb = new() { UseSystemPasswordChar = true, Width = 250 };

        Label badge = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8f),
            Padding = new Padding(6, 5, 0, 0),
        };

        void UpdateBadge()
        {
            if (!string.IsNullOrEmpty(tb.Text))
            {
                badge.Text = "🔒 Will encrypt on save";
                badge.ForeColor = Color.FromArgb(0, 128, 0);
            }
            else if (!string.IsNullOrEmpty(encryptedValue))
            {
                badge.Text = "🔒 Encrypted";
                badge.ForeColor = Color.FromArgb(0, 100, 0);
            }
            else
            {
                badge.Text = "🔓 Not set";
                badge.ForeColor = Color.Gray;
            }
        }

        tb.TextChanged += (_, _) => UpdateBadge();
        UpdateBadge();

        FlowLayoutPanel wrapper = new()
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        wrapper.Controls.Add(tb);
        wrapper.Controls.Add(badge);

        return (tb, wrapper);
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
        _azureAuthSpRadio.Checked  = _settings.AzureMonitor.AuthMode == AzureAuthMode.ServicePrincipal;
        _azureAuthCliRadio.Checked = _settings.AzureMonitor.AuthMode == AzureAuthMode.AzureCli;
        _azureTenantIdTextBox.Text = _settings.AzureMonitor.TenantId;
        _azureClientIdTextBox.Text = _settings.AzureMonitor.ClientId;
        _azureClientSecretTextBox.Text = _settings.AzureMonitor.ClientSecret;
        _azureSubscriptionIdTextBox.Text = _settings.AzureMonitor.SubscriptionId;
        _azureResourceGroupTextBox.Text = _settings.AzureMonitor.ResourceGroup;
        _azureWorkspaceIdTextBox.Text = _settings.AzureMonitor.WorkspaceId;
        _azureAppInsightsAppIdTextBox.Text = _settings.AzureMonitor.AppInsightsAppId;

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
        _settings.AzureMonitor.AuthMode = _azureAuthCliRadio.Checked ? AzureAuthMode.AzureCli : AzureAuthMode.ServicePrincipal;
        _settings.AzureMonitor.TenantId = _azureTenantIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientId = _azureClientIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientSecret = _azureClientSecretTextBox.Text;
        _settings.AzureMonitor.SubscriptionId = _azureSubscriptionIdTextBox.Text.Trim();
        _settings.AzureMonitor.ResourceGroup = _azureResourceGroupTextBox.Text.Trim();
        _settings.AzureMonitor.WorkspaceId = _azureWorkspaceIdTextBox.Text.Trim();
        _settings.AzureMonitor.AppInsightsAppId = _azureAppInsightsAppIdTextBox.Text.Trim();

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

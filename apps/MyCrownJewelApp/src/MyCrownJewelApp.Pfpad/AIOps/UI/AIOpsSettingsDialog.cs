using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>
/// Configures AIOps connectors and runtime options.
/// </summary>
public sealed class AIOpsSettingsDialog : Form
{
    private const int LabelX = 12;
    private const int LabelWidth = 130;
    private const int InputX = 150;
    private const int RightPadding = 12;
    private const string DpapiTooltip = "🔒 DPAPI-protected — value is encrypted with your Windows user account and stored as an encrypted setting.";

    private readonly AIOpsSettings _settings;
    private readonly ToolTip _toolTip = new();
    private readonly SplitContainer _splitContainer;
    private readonly ListBox _navListBox;
    private readonly Panel _navPanel;
    private readonly Panel _contentScrollPanel;
    private readonly Panel _infoBar;
    private readonly Label _infoBarLabel;
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly Dictionary<SectionId, Panel> _sectionPanels = [];
    private readonly List<NavItem> _navItems = [];
    private readonly List<Label> _hintLabels = [];
    private readonly List<Label> _descriptionLabels = [];
    private readonly List<Panel> _separatorLines = [];
    private readonly List<Panel> _headerCards = [];
    private readonly List<SecretFieldState> _secretFields = [];
    private readonly Dictionary<Label, StatusTone> _statusTones = [];

    private Theme _theme;
    private int _selectedNavIndex;

    private Button _saveButton = null!;
    private Button _cancelButton = null!;
    private Button _applyButton = null!;

    private CheckBox _enabledCheckBox = null!;
    private CheckBox _mockDataCheckBox = null!;
    private NumericUpDown _pollIntervalUpDown = null!;
    private CheckBox _autoScanCheckBox = null!;
    private CheckBox _requireApprovalCheckBox = null!;
    private CheckBox _inlineInsightsCheckBox = null!;

    private CheckBox _azureMonitorEnabledCheckBox = null!;
    private RadioButton _azureAuthSpRadio = null!;
    private RadioButton _azureAuthCliRadio = null!;
    private TextBox _azureTenantIdTextBox = null!;
    private TextBox _azureClientIdTextBox = null!;
    private TextBox _azureClientSecretTextBox = null!;
    private TextBox _azureSubscriptionIdTextBox = null!;
    private TextBox _azureResourceGroupTextBox = null!;
    private TextBox _azureWorkspaceIdTextBox = null!;
    private TextBox _azureAppInsightsAppIdTextBox = null!;
    private TextBox _azureAppInsightsKeyTextBox = null!;
    private Button _azureImportCliButton = null!;
    private Button _azureImportWorkspacesButton = null!;
    private Button _azureImportAppInsightsButton = null!;
    private Button _azureTestButton = null!;
    private Label _azureStatusLabel = null!;
    private Label _azureClientIdLabel = null!;
    private Label _azureClientSecretLabel = null!;
    private Label _azureTenantIdLabel = null!;
    private Panel _azureClientSecretWrapper = null!;

    private CheckBox _azureDevOpsEnabledCheckBox = null!;
    private TextBox _azureDevOpsOrganizationTextBox = null!;
    private TextBox _azureDevOpsProjectTextBox = null!;
    private TextBox _azureDevOpsRepositoryTextBox = null!;
    private TextBox _azureDevOpsPatTextBox = null!;
    private Button _azureDevOpsTestButton = null!;
    private Label _azureDevOpsStatusLabel = null!;

    private CheckBox _kubernetesEnabledCheckBox = null!;
    private TextBox _kubernetesApiServerTextBox = null!;
    private TextBox _kubernetesBearerTokenTextBox = null!;
    private TextBox _kubernetesNamespaceTextBox = null!;
    private CheckBox _kubernetesSkipTlsCheckBox = null!;
    private Button _kubernetesTestButton = null!;
    private Label _kubernetesStatusLabel = null!;

    private CheckBox _prometheusEnabledCheckBox = null!;
    private TextBox _prometheusBaseUrlTextBox = null!;
    private RadioButton _prometheusAuthNoneRadio = null!;
    private RadioButton _prometheusAuthBasicRadio = null!;
    private RadioButton _prometheusAuthBearerRadio = null!;
    private TextBox _prometheusUsernameTextBox = null!;
    private TextBox _prometheusPasswordTextBox = null!;
    private TextBox _prometheusBearerTokenTextBox = null!;
    private NumericUpDown _prometheusTimeoutUpDown = null!;
    private Label _prometheusUsernameLabel = null!;
    private Label _prometheusPasswordLabel = null!;
    private Label _prometheusBearerTokenLabel = null!;
    private Panel _prometheusPasswordWrapper = null!;
    private Panel _prometheusBearerWrapper = null!;
    private Button _prometheusTestButton = null!;
    private Label _prometheusStatusLabel = null!;

    private CheckBox _pagerDutyEnabledCheckBox = null!;
    private TextBox _pagerDutyApiTokenTextBox = null!;
    private TextBox _pagerDutyServiceIdTextBox = null!;
    private NumericUpDown _pagerDutyTimeoutUpDown = null!;
    private Button _pagerDutyTestButton = null!;
    private Label _pagerDutyStatusLabel = null!;

    private CheckBox _githubEnabledCheckBox = null!;
    private TextBox _githubOwnerTextBox = null!;
    private TextBox _githubRepositoryTextBox = null!;
    private TextBox _githubPatTextBox = null!;
    private NumericUpDown _githubTimeoutUpDown = null!;
    private Button _githubTestButton = null!;
    private Label _githubStatusLabel = null!;

    private CheckBox _gitLabEnabledCheckBox = null!;
    private TextBox _gitLabInstanceUrlTextBox = null!;
    private TextBox _gitLabNamespacePathTextBox = null!;
    private TextBox _gitLabPatTextBox = null!;
    private NumericUpDown _gitLabTimeoutUpDown = null!;
    private Button _gitLabTestButton = null!;
    private Label _gitLabStatusLabel = null!;

    private CheckBox _bitbucketEnabledCheckBox = null!;
    private TextBox _bitbucketWorkspaceTextBox = null!;
    private TextBox _bitbucketRepositoryTextBox = null!;
    private TextBox _bitbucketUsernameTextBox = null!;
    private TextBox _bitbucketAppPasswordTextBox = null!;
    private NumericUpDown _bitbucketTimeoutUpDown = null!;
    private Button _bitbucketTestButton = null!;
    private Label _bitbucketStatusLabel = null!;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? AzureMonitorConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? AzureDevOpsConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? KubernetesConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? PrometheusConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? PagerDutyConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? GitHubActionsConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? GitLabConnector { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IAIOpsConnector? BitbucketConnector { get; set; }

    public event Action<AIOpsSettings>? SettingsSaved;

    public AIOpsSettingsDialog(AIOpsSettings settings)
    {
        _settings = settings ?? new AIOpsSettings();
        _theme = ThemeManager.Instance.CurrentTheme;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(960, 720);
        ClientSize = new Size(1120, 780);
        Text = "AIOps Settings";
        MaximizeBox = false;
        MinimizeBox = false;

        _navListBox = CreateNavigationListBox();
        _navPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
        _navPanel.Controls.Add(_navListBox);

        _contentScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };
        _contentScrollPanel.Resize += (_, _) => ResizeSectionPanels();

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = true,
            SplitterDistance = 220,
            Panel1MinSize = 160,
            BorderStyle = BorderStyle.None,
        };
        _splitContainer.Panel1.Controls.Add(_navPanel);
        _splitContainer.Panel2.Controls.Add(_contentScrollPanel);

        _infoBarLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 10),
            Text = "🛡 Secrets protected by Windows DPAPI (per-user encryption).",
        };
        _infoBar = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        _infoBar.Controls.Add(_infoBarLabel);

        _cancelButton = CreateButton("Cancel", 100);
        _applyButton = CreateButton("Apply", 100);
        _saveButton = CreateButton("Save && Close", 118);
        _buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(12, 10, 12, 10),
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        _buttonPanel.Controls.AddRange([_cancelButton, _saveButton, _applyButton]);

        _cancelButton.DialogResult = DialogResult.Cancel;
        _applyButton.Click += (_, _) => ApplySettings();
        _saveButton.Click += (_, _) => SaveAndClose();
        _cancelButton.Click += (_, _) => Close();
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        BuildSections();
        BuildNavigation();
        WireEvents();

        Controls.Add(_splitContainer);
        Controls.Add(_infoBar);
        Controls.Add(_buttonPanel);

        LoadSettings();
        ThemeManager.Instance.ThemeChanged += SetTheme;
        if (_navItems.Count > 0)
        {
            _selectedNavIndex = 0;
            _navListBox.SelectedIndex = _selectedNavIndex;
            ShowSection(_navItems[_selectedNavIndex].SectionId!.Value);
        }

        SetTheme(_theme);
        ResumeLayout(true);
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);

        BackColor = theme.MenuBackground;
        ForeColor = theme.Text;
        _splitContainer.BackColor = theme.MenuBackground;
        _splitContainer.Panel1.BackColor = theme.PanelBackground;
        _splitContainer.Panel2.BackColor = theme.EditorBackground;
        _navPanel.BackColor = theme.PanelBackground;
        _navListBox.BackColor = theme.PanelBackground;
        _navListBox.ForeColor = theme.Text;
        _contentScrollPanel.BackColor = theme.EditorBackground;

        foreach (Panel panel in _sectionPanels.Values)
            panel.BackColor = theme.EditorBackground;

        foreach (Panel card in _headerCards)
        {
            card.BackColor = Blend(theme.EditorBackground, theme.Accent, 0.10f);
            card.Invalidate();
        }

        foreach (Panel line in _separatorLines)
            line.BackColor = theme.Border;

        foreach (Label hintLabel in _hintLabels)
            hintLabel.ForeColor = AIOpsUiHelper.SecondaryText(theme);

        foreach (Label descriptionLabel in _descriptionLabels)
            descriptionLabel.ForeColor = AIOpsUiHelper.SecondaryText(theme);

        foreach ((Label label, StatusTone tone) in _statusTones)
            ApplyStatusTheme(label, tone);

        foreach (SecretFieldState secretField in _secretFields)
            UpdateSecretBadge(secretField);

        _infoBar.BackColor = Blend(theme.EditorBackground, theme.Accent, 0.08f);
        _infoBarLabel.ForeColor = GetReadableTextColor(_infoBar.BackColor);

        foreach (Button button in new[]
                 {
                     _applyButton, _saveButton, _cancelButton,
                     _azureImportCliButton, _azureImportWorkspacesButton, _azureImportAppInsightsButton,
                     _azureTestButton, _azureDevOpsTestButton, _kubernetesTestButton, _prometheusTestButton,
                     _pagerDutyTestButton, _githubTestButton, _gitLabTestButton, _bitbucketTestButton
                 })
        {
            button.BackColor = theme.PanelBackground;
            button.ForeColor = theme.Text;
            button.FlatAppearance.BorderColor = theme.Border;
            button.FlatAppearance.MouseOverBackColor = Blend(theme.PanelBackground, theme.Accent, 0.15f);
            button.FlatAppearance.MouseDownBackColor = Blend(theme.PanelBackground, theme.Accent, 0.22f);
        }

        _navListBox.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= SetTheme;
            DisposeConnector(AzureMonitorConnector);
            DisposeConnector(AzureDevOpsConnector);
            DisposeConnector(KubernetesConnector);
            DisposeConnector(PrometheusConnector);
            DisposeConnector(PagerDutyConnector);
            DisposeConnector(GitHubActionsConnector);
            DisposeConnector(GitLabConnector);
            DisposeConnector(BitbucketConnector);
        }

        base.Dispose(disposing);
    }

    private void BuildSections()
    {
        AddSection(SectionId.General, BuildGeneralPanel());
        AddSection(SectionId.AzureMonitor, BuildAzureMonitorPanel());
        AddSection(SectionId.AzureDevOps, BuildAzureDevOpsPanel());
        AddSection(SectionId.Kubernetes, BuildKubernetesPanel());
        AddSection(SectionId.Prometheus, BuildPrometheusPanel());
        AddSection(SectionId.PagerDuty, BuildPagerDutyPanel());
        AddSection(SectionId.GitHubActions, BuildGitHubActionsPanel());
        AddSection(SectionId.GitLab, BuildGitLabPanel());
        AddSection(SectionId.Bitbucket, BuildBitbucketPanel());
        ResizeSectionPanels();
    }

    private void BuildNavigation()
    {
        _navItems.Clear();
        _navItems.Add(new NavItem(SectionId.General, "⚙", "General"));
        _navItems.Add(NavItem.Separator("Connectors"));
        _navItems.Add(new NavItem(SectionId.AzureMonitor, "☁", "Azure Monitor", () => _azureMonitorEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.AzureDevOps, "📋", "Azure DevOps", () => _azureDevOpsEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.Kubernetes, "⚙", "Kubernetes", () => _kubernetesEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.Prometheus, "📊", "Prometheus", () => _prometheusEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.PagerDuty, "🔔", "PagerDuty", () => _pagerDutyEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.GitHubActions, "🐙", "GitHub Actions", () => _githubEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.GitLab, "🦊", "GitLab", () => _gitLabEnabledCheckBox.Checked));
        _navItems.Add(new NavItem(SectionId.Bitbucket, "🪣", "Bitbucket", () => _bitbucketEnabledCheckBox.Checked));

        _navListBox.Items.Clear();
        foreach (NavItem item in _navItems)
            _navListBox.Items.Add(item);
    }

    private void WireEvents()
    {
        _navListBox.SelectedIndexChanged += (_, _) => HandleNavigationChanged();

        _azureMonitorEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _azureDevOpsEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _kubernetesEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _prometheusEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _pagerDutyEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _githubEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _gitLabEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();
        _bitbucketEnabledCheckBox.CheckedChanged += (_, _) => _navListBox.Invalidate();

        _azureAuthSpRadio.CheckedChanged += (_, _) => ApplyAzureAuthMode();
        _azureAuthCliRadio.CheckedChanged += (_, _) => ApplyAzureAuthMode();
        _prometheusAuthNoneRadio.CheckedChanged += (_, _) => ApplyPrometheusAuthMode();
        _prometheusAuthBasicRadio.CheckedChanged += (_, _) => ApplyPrometheusAuthMode();
        _prometheusAuthBearerRadio.CheckedChanged += (_, _) => ApplyPrometheusAuthMode();

        _azureImportCliButton.Click += async (_, _) => await ImportAzureSubscriptionAsync();
        _azureImportWorkspacesButton.Click += async (_, _) => await ImportAzureWorkspaceAsync();
        _azureImportAppInsightsButton.Click += async (_, _) => await ImportAzureAppInsightsAsync();

        _azureTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateAzureMonitor, GetAzureMonitorConnectorForTest, _azureStatusLabel, _azureTestButton);
        _azureDevOpsTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateAzureDevOps, GetAzureDevOpsConnectorForTest, _azureDevOpsStatusLabel, _azureDevOpsTestButton);
        _kubernetesTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateKubernetes, GetKubernetesConnectorForTest, _kubernetesStatusLabel, _kubernetesTestButton);
        _prometheusTestButton.Click += async (_, _) => await TestConnectionAsync(ValidatePrometheus, GetPrometheusConnectorForTest, _prometheusStatusLabel, _prometheusTestButton);
        _pagerDutyTestButton.Click += async (_, _) => await TestConnectionAsync(ValidatePagerDuty, GetPagerDutyConnectorForTest, _pagerDutyStatusLabel, _pagerDutyTestButton);
        _githubTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateGitHubActions, GetGitHubActionsConnectorForTest, _githubStatusLabel, _githubTestButton);
        _gitLabTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateGitLab, GetGitLabConnectorForTest, _gitLabStatusLabel, _gitLabTestButton);
        _bitbucketTestButton.Click += async (_, _) => await TestConnectionAsync(ValidateBitbucket, GetBitbucketConnectorForTest, _bitbucketStatusLabel, _bitbucketTestButton);
    }

    private Panel BuildGeneralPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "⚙", "General Settings", "Configure AIOps behavior, polling, and safety controls.");
        AddToggle(panel, ref y, _enabledCheckBox = CreateCheckBox("Enable AIOps", true), "Master switch. Disabling stops all connectors and engine analysis.");
        AddToggle(panel, ref y, _mockDataCheckBox = CreateCheckBox("Mock data mode"), "Use simulated data for testing without real cloud connections.");
        _pollIntervalUpDown = CreateNumericUpDown(5, 3600);
        AddField(panel, ref y, "Poll interval *", _pollIntervalUpDown, "How often to refresh data from connectors (seconds).");
        AddToggle(panel, ref y, _autoScanCheckBox = CreateCheckBox("Auto-scan on save"), "Automatically run SAST and secret scan when a file is saved.");
        AddToggle(panel, ref y, _requireApprovalCheckBox = CreateCheckBox("Require approval for production"), "Block automated remediations targeting production environments.");
        AddToggle(panel, ref y, _inlineInsightsCheckBox = CreateCheckBox("Inline insights"), "Show AIOps annotations inline in the editor gutter.");

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildAzureMonitorPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "🔷", "Azure Monitor", "Monitor Azure cloud infrastructure, Log Analytics workspaces, and Application Insights resources.");
        AddToggle(panel, ref y, _azureMonitorEnabledCheckBox = CreateCheckBox("Enable connector", true), "Turn on Azure Monitor telemetry, log, and application health collection.");
        AddSeparator(panel, ref y);

        _azureAuthSpRadio = new RadioButton { Text = "Service Principal", AutoSize = true, Checked = true };
        _azureAuthCliRadio = new RadioButton { Text = "az login", AutoSize = true };
        AddField(panel, ref y, "Auth Mode *", CreateRadioGroup(_azureAuthSpRadio, _azureAuthCliRadio), "Choose between service credentials or your current Azure CLI session.");

        _azureImportCliButton = CreateButton("⬇ Import from az CLI", 170);
        AddField(panel, ref y, string.Empty, _azureImportCliButton, "Import tenant and subscription details from the active Azure CLI login.");

        _azureTenantIdTextBox = CreateTextBox("00000000-0000-0000-0000-000000000000");
        AddField(panel, ref y, "Tenant ID *", _azureTenantIdTextBox, "Enter your Azure tenant GUID.", out _azureTenantIdLabel);

        _azureClientIdTextBox = CreateTextBox("00000000-0000-0000-0000-000000000000");
        AddField(panel, ref y, "Client ID *", _azureClientIdTextBox, "Azure AD application (service principal) client ID.", out _azureClientIdLabel);

        (_azureClientSecretTextBox, _azureClientSecretWrapper) = CreateSecretField(_settings.AzureMonitor.EncryptedClientSecret, "Paste client secret");
        AddField(panel, ref y, "Client Secret *", _azureClientSecretWrapper, "Stored encrypted with Windows DPAPI.", out _azureClientSecretLabel);

        _azureSubscriptionIdTextBox = CreateTextBox("00000000-0000-0000-0000-000000000000");
        _azureImportWorkspacesButton = CreateButton("Import Workspaces", 150);
        AddField(panel, ref y, "Subscription ID *", CreateInlineButtonHost(_azureSubscriptionIdTextBox, _azureImportWorkspacesButton), "Used to discover Log Analytics workspaces for this subscription.");

        _azureResourceGroupTextBox = CreateTextBox("rg-observability-prod");
        AddField(panel, ref y, "Resource Group", _azureResourceGroupTextBox, "Optional. Helpful when importing and scoping Azure resources.");

        _azureWorkspaceIdTextBox = CreateTextBox("Workspace customerId");
        AddField(panel, ref y, "Workspace ID", _azureWorkspaceIdTextBox, "Log Analytics workspace customerId GUID.");

        _azureAppInsightsAppIdTextBox = CreateTextBox("Application Insights App ID");
        _azureImportAppInsightsButton = CreateButton("Import App Insights", 158);
        AddField(panel, ref y, "App Insights App ID", CreateInlineButtonHost(_azureAppInsightsAppIdTextBox, _azureImportAppInsightsButton), "Application Insights App ID used for direct app telemetry queries.");

        (_azureAppInsightsKeyTextBox, Panel appInsightsKeyWrapper) = CreateSecretField(_settings.AzureMonitor.EncryptedAppInsightsKey, "Instrumentation key or API key");
        AddField(panel, ref y, "App Insights Key", appInsightsKeyWrapper, "Optional secret for Application Insights API access.");

        _azureTestButton = CreateButton("Test Connection", 136);
        _azureStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _azureTestButton, _azureStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildAzureDevOpsPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "📋", "Azure DevOps", "Inspect builds, work items, and pull request activity from your Azure DevOps project.");
        AddToggle(panel, ref y, _azureDevOpsEnabledCheckBox = CreateCheckBox("Enable connector", true), "Connect Azure DevOps to surface pipelines, incidents, and repository activity.");
        AddSeparator(panel, ref y);

        _azureDevOpsOrganizationTextBox = CreateTextBox("https://dev.azure.com/YOUR_ORG");
        AddField(panel, ref y, "Organization *", _azureDevOpsOrganizationTextBox, "https://dev.azure.com/YOUR_ORG");

        _azureDevOpsProjectTextBox = CreateTextBox("MyProject");
        AddField(panel, ref y, "Project *", _azureDevOpsProjectTextBox, "Azure DevOps project name.");

        _azureDevOpsRepositoryTextBox = CreateTextBox("my-repo");
        AddField(panel, ref y, "Repository", _azureDevOpsRepositoryTextBox, "Optional. Defaults to project name if blank.");

        (_azureDevOpsPatTextBox, Panel patWrapper) = CreateSecretField(_settings.AzureDevOps.EncryptedPersonalAccessToken, "Paste Personal Access Token");
        AddField(panel, ref y, "PAT *", patWrapper, "Personal Access Token — needs Code (Read), Build (Read), Work Items (Read).");

        _azureDevOpsTestButton = CreateButton("Test Connection", 136);
        _azureDevOpsStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _azureDevOpsTestButton, _azureDevOpsStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildKubernetesPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "⚙", "Kubernetes", "Monitor Kubernetes cluster health, workloads, and deployment activity.");
        AddToggle(panel, ref y, _kubernetesEnabledCheckBox = CreateCheckBox("Enable connector", true), "Use Kubernetes API access for cluster inventory and workload signals.");
        AddSeparator(panel, ref y);

        _kubernetesApiServerTextBox = CreateTextBox("https://your-cluster:6443");
        AddField(panel, ref y, "API Server URL *", _kubernetesApiServerTextBox, "https://your-cluster:6443");

        (_kubernetesBearerTokenTextBox, Panel bearerWrapper) = CreateSecretField(_settings.Kubernetes.EncryptedBearerToken, "Paste service account token");
        AddField(panel, ref y, "Bearer Token *", bearerWrapper, "Service account token with cluster-reader permissions.");

        _kubernetesNamespaceTextBox = CreateTextBox("default");
        AddField(panel, ref y, "Namespace", _kubernetesNamespaceTextBox, "Kubernetes namespace to monitor. Default: default.");

        _kubernetesSkipTlsCheckBox = CreateCheckBox("Skip TLS verify");
        AddField(panel, ref y, "TLS", _kubernetesSkipTlsCheckBox, "Disable only for local/dev clusters.");

        _kubernetesTestButton = CreateButton("Test Connection", 136);
        _kubernetesStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _kubernetesTestButton, _kubernetesStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildPrometheusPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "📊", "Prometheus", "Query metrics and alerting data from your Prometheus server.");
        AddToggle(panel, ref y, _prometheusEnabledCheckBox = CreateCheckBox("Enable connector", true), "Bring Prometheus metrics into AIOps analysis and health checks.");
        AddSeparator(panel, ref y);

        _prometheusBaseUrlTextBox = CreateTextBox("http://localhost:9090");
        AddField(panel, ref y, "Base URL *", _prometheusBaseUrlTextBox, "http://localhost:9090");

        _prometheusAuthNoneRadio = new RadioButton { Text = "None", AutoSize = true, Checked = true };
        _prometheusAuthBasicRadio = new RadioButton { Text = "Basic auth", AutoSize = true };
        _prometheusAuthBearerRadio = new RadioButton { Text = "Bearer token", AutoSize = true };
        AddField(panel, ref y, "Auth *", CreateRadioGroup(_prometheusAuthNoneRadio, _prometheusAuthBasicRadio, _prometheusAuthBearerRadio), "Use Bearer Token OR Basic auth, not both.");

        _prometheusUsernameTextBox = CreateTextBox("prometheus-user");
        AddField(panel, ref y, "Username", _prometheusUsernameTextBox, "Username for Basic auth.", out _prometheusUsernameLabel);

        (_prometheusPasswordTextBox, _prometheusPasswordWrapper) = CreateSecretField(_settings.Prometheus.EncryptedPassword, "Paste password");
        AddField(panel, ref y, "Password", _prometheusPasswordWrapper, "Password for Basic auth.", out _prometheusPasswordLabel);

        (_prometheusBearerTokenTextBox, _prometheusBearerWrapper) = CreateSecretField(_settings.Prometheus.EncryptedBearerToken, "Paste bearer token");
        AddField(panel, ref y, "Bearer Token", _prometheusBearerWrapper, "Bearer token for secured Prometheus instances.", out _prometheusBearerTokenLabel);

        _prometheusTimeoutUpDown = CreateNumericUpDown(1, 120);
        AddField(panel, ref y, "Timeout (seconds)", _prometheusTimeoutUpDown, "Maximum time to wait for Prometheus API responses.");

        _prometheusTestButton = CreateButton("Test Connection", 136);
        _prometheusStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _prometheusTestButton, _prometheusStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildPagerDutyPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "🔔", "PagerDuty", "Track incidents and operational alerts from PagerDuty.");
        AddToggle(panel, ref y, _pagerDutyEnabledCheckBox = CreateCheckBox("Enable connector", true), "Use PagerDuty incidents to enrich AIOps context and remediation insights.");
        AddSeparator(panel, ref y);

        (_pagerDutyApiTokenTextBox, Panel apiTokenWrapper) = CreateSecretField(_settings.PagerDuty.EncryptedApiToken, "Paste PagerDuty API token");
        AddField(panel, ref y, "API Token *", apiTokenWrapper, "User Icon → My Profile → User Settings → Create API Key.");

        _pagerDutyServiceIdTextBox = CreateTextBox("P12345");
        AddField(panel, ref y, "Service ID", _pagerDutyServiceIdTextBox, "Filter incidents to a specific service. Leave blank for all.");

        _pagerDutyTimeoutUpDown = CreateNumericUpDown(1, 120);
        AddField(panel, ref y, "Timeout (seconds)", _pagerDutyTimeoutUpDown, "Maximum time to wait for the PagerDuty API.");

        _pagerDutyTestButton = CreateButton("Test Connection", 136);
        _pagerDutyStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _pagerDutyTestButton, _pagerDutyStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildGitHubActionsPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "🐙", "GitHub Actions", "Inspect workflow runs, deployments, and pull request activity from GitHub.");
        AddToggle(panel, ref y, _githubEnabledCheckBox = CreateCheckBox("Enable connector", true), "Connect GitHub Actions to monitor automation and repository delivery signals.");
        AddSeparator(panel, ref y);

        _githubOwnerTextBox = CreateTextBox("your-org");
        AddField(panel, ref y, "Owner *", _githubOwnerTextBox, "GitHub org or username.");

        _githubRepositoryTextBox = CreateTextBox("your-repo");
        AddField(panel, ref y, "Repository *", _githubRepositoryTextBox, "Repository name without owner prefix.");

        (_githubPatTextBox, Panel githubPatWrapper) = CreateSecretField(_settings.GitHubActions.EncryptedPersonalAccessToken, "Paste GitHub PAT");
        AddField(panel, ref y, "PAT *", githubPatWrapper, "Required scopes: repo, workflow, read:org.");

        _githubTimeoutUpDown = CreateNumericUpDown(1, 120);
        AddField(panel, ref y, "Timeout (seconds)", _githubTimeoutUpDown, "Maximum time to wait for GitHub API responses.");

        _githubTestButton = CreateButton("Test Connection", 136);
        _githubStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _githubTestButton, _githubStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildGitLabPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "🦊", "GitLab", "Connect to GitLab cloud or self-hosted projects for pipelines, merge requests, and commits.");
        AddToggle(panel, ref y, _gitLabEnabledCheckBox = CreateCheckBox("Enable connector", true), "Use GitLab pipeline and repository signals in AIOps workflows.");
        AddSeparator(panel, ref y);

        _gitLabInstanceUrlTextBox = CreateTextBox("https://gitlab.com");
        AddField(panel, ref y, "Instance URL *", _gitLabInstanceUrlTextBox, "https://gitlab.com for cloud, or your self-hosted URL.");

        _gitLabNamespacePathTextBox = CreateTextBox("mygroup/myrepo");
        AddField(panel, ref y, "Namespace Path *", _gitLabNamespacePathTextBox, "e.g. mygroup/myrepo");

        (_gitLabPatTextBox, Panel gitLabPatWrapper) = CreateSecretField(_settings.GitLab.EncryptedPersonalAccessToken, "Paste GitLab PAT");
        AddField(panel, ref y, "PAT *", gitLabPatWrapper, "Personal Access Token — needs api or read_api scope.");

        _gitLabTimeoutUpDown = CreateNumericUpDown(1, 120);
        AddField(panel, ref y, "Timeout (seconds)", _gitLabTimeoutUpDown, "Maximum time to wait for GitLab API responses.");

        _gitLabTestButton = CreateButton("Test Connection", 136);
        _gitLabStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _gitLabTestButton, _gitLabStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private Panel BuildBitbucketPanel()
    {
        Panel panel = CreateSectionPanel();
        int y = 12;

        AddHeaderCard(panel, ref y, "🪣", "Bitbucket", "Bring Bitbucket Cloud repository and pipeline activity into your AIOps workflow.");
        AddToggle(panel, ref y, _bitbucketEnabledCheckBox = CreateCheckBox("Enable connector", true), "Use Bitbucket pull request and pipeline metadata for release visibility.");
        AddSeparator(panel, ref y);

        _bitbucketWorkspaceTextBox = CreateTextBox("your-workspace");
        AddField(panel, ref y, "Workspace *", _bitbucketWorkspaceTextBox, "Bitbucket workspace slug (username or team).");

        _bitbucketRepositoryTextBox = CreateTextBox("your-repo");
        AddField(panel, ref y, "Repository *", _bitbucketRepositoryTextBox, "Repository slug (part after workspace in URL).");

        _bitbucketUsernameTextBox = CreateTextBox("your-username");
        AddField(panel, ref y, "Username *", _bitbucketUsernameTextBox, "Your Bitbucket username.");

        (_bitbucketAppPasswordTextBox, Panel appPasswordWrapper) = CreateSecretField(_settings.Bitbucket.EncryptedAppPassword, "Paste app password");
        AddField(panel, ref y, "App Password *", appPasswordWrapper, "Bitbucket App Password — Repositories: Read, Pull requests: Read.");

        _bitbucketTimeoutUpDown = CreateNumericUpDown(1, 120);
        AddField(panel, ref y, "Timeout (seconds)", _bitbucketTimeoutUpDown, "Maximum time to wait for Bitbucket API responses.");

        _bitbucketTestButton = CreateButton("Test Connection", 136);
        _bitbucketStatusLabel = CreateStatusLabel();
        AddTestArea(panel, ref y, _bitbucketTestButton, _bitbucketStatusLabel);

        panel.Height = y + 20;
        return panel;
    }

    private void LoadSettings()
    {
        _enabledCheckBox.Checked = _settings.Enabled;
        _mockDataCheckBox.Checked = _settings.MockDataEnabled;
        _pollIntervalUpDown.Value = ClampNumeric(_pollIntervalUpDown, _settings.PollIntervalSeconds);
        _autoScanCheckBox.Checked = _settings.AutoScanOnSave;
        _requireApprovalCheckBox.Checked = _settings.RequireApprovalForProduction;
        _inlineInsightsCheckBox.Checked = _settings.InlineInsightsEnabled;

        _azureMonitorEnabledCheckBox.Checked = _settings.AzureMonitor.Enabled;
        _azureAuthSpRadio.Checked = _settings.AzureMonitor.AuthMode == AzureAuthMode.ServicePrincipal;
        _azureAuthCliRadio.Checked = _settings.AzureMonitor.AuthMode == AzureAuthMode.AzureCli;
        _azureTenantIdTextBox.Text = _settings.AzureMonitor.TenantId;
        _azureClientIdTextBox.Text = _settings.AzureMonitor.ClientId;
        _azureClientSecretTextBox.Text = _settings.AzureMonitor.ClientSecret;
        _azureSubscriptionIdTextBox.Text = _settings.AzureMonitor.SubscriptionId;
        _azureResourceGroupTextBox.Text = _settings.AzureMonitor.ResourceGroup;
        _azureWorkspaceIdTextBox.Text = _settings.AzureMonitor.WorkspaceId;
        _azureAppInsightsAppIdTextBox.Text = _settings.AzureMonitor.AppInsightsAppId;
        _azureAppInsightsKeyTextBox.Text = _settings.AzureMonitor.AppInsightsKey;

        _azureDevOpsEnabledCheckBox.Checked = _settings.AzureDevOps.Enabled;
        _azureDevOpsOrganizationTextBox.Text = _settings.AzureDevOps.Organization;
        _azureDevOpsProjectTextBox.Text = _settings.AzureDevOps.Project;
        _azureDevOpsRepositoryTextBox.Text = _settings.AzureDevOps.Repository;
        _azureDevOpsPatTextBox.Text = _settings.AzureDevOps.PersonalAccessToken;

        _kubernetesEnabledCheckBox.Checked = _settings.Kubernetes.Enabled;
        _kubernetesApiServerTextBox.Text = _settings.Kubernetes.ApiServerUrl;
        _kubernetesBearerTokenTextBox.Text = _settings.Kubernetes.BearerToken;
        _kubernetesNamespaceTextBox.Text = _settings.Kubernetes.Namespace;
        _kubernetesSkipTlsCheckBox.Checked = _settings.Kubernetes.SkipTlsVerify;

        _prometheusEnabledCheckBox.Checked = _settings.Prometheus.Enabled;
        _prometheusBaseUrlTextBox.Text = _settings.Prometheus.BaseUrl;
        _prometheusUsernameTextBox.Text = _settings.Prometheus.Username ?? string.Empty;
        _prometheusPasswordTextBox.Text = _settings.Prometheus.Password ?? string.Empty;
        _prometheusBearerTokenTextBox.Text = _settings.Prometheus.BearerToken ?? string.Empty;
        _prometheusTimeoutUpDown.Value = ClampNumeric(_prometheusTimeoutUpDown, _settings.Prometheus.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(_settings.Prometheus.BearerToken))
            _prometheusAuthBearerRadio.Checked = true;
        else if (!string.IsNullOrWhiteSpace(_settings.Prometheus.Username) || !string.IsNullOrWhiteSpace(_settings.Prometheus.Password))
            _prometheusAuthBasicRadio.Checked = true;
        else
            _prometheusAuthNoneRadio.Checked = true;

        _pagerDutyEnabledCheckBox.Checked = _settings.PagerDuty.Enabled;
        _pagerDutyApiTokenTextBox.Text = _settings.PagerDuty.ApiToken;
        _pagerDutyServiceIdTextBox.Text = _settings.PagerDuty.ServiceId ?? string.Empty;
        _pagerDutyTimeoutUpDown.Value = ClampNumeric(_pagerDutyTimeoutUpDown, _settings.PagerDuty.TimeoutSeconds);

        _githubEnabledCheckBox.Checked = _settings.GitHubActions.Enabled;
        _githubOwnerTextBox.Text = _settings.GitHubActions.Owner;
        _githubRepositoryTextBox.Text = _settings.GitHubActions.Repository;
        _githubPatTextBox.Text = _settings.GitHubActions.PersonalAccessToken;
        _githubTimeoutUpDown.Value = ClampNumeric(_githubTimeoutUpDown, _settings.GitHubActions.TimeoutSeconds);

        _gitLabEnabledCheckBox.Checked = _settings.GitLab.Enabled;
        _gitLabInstanceUrlTextBox.Text = _settings.GitLab.InstanceUrl;
        _gitLabNamespacePathTextBox.Text = _settings.GitLab.NamespacePath;
        _gitLabPatTextBox.Text = _settings.GitLab.PersonalAccessToken;
        _gitLabTimeoutUpDown.Value = ClampNumeric(_gitLabTimeoutUpDown, _settings.GitLab.TimeoutSeconds);

        _bitbucketEnabledCheckBox.Checked = _settings.Bitbucket.Enabled;
        _bitbucketWorkspaceTextBox.Text = _settings.Bitbucket.Workspace;
        _bitbucketRepositoryTextBox.Text = _settings.Bitbucket.Repository;
        _bitbucketUsernameTextBox.Text = _settings.Bitbucket.Username;
        _bitbucketAppPasswordTextBox.Text = _settings.Bitbucket.AppPassword;
        _bitbucketTimeoutUpDown.Value = ClampNumeric(_bitbucketTimeoutUpDown, _settings.Bitbucket.TimeoutSeconds);

        ApplyAzureAuthMode();
        ApplyPrometheusAuthMode();
        _navListBox.Invalidate();
    }

    private void ApplySettings() => ApplySettings(true);

    private void ApplySettings(bool raiseEvent)
    {
        _settings.Enabled = _enabledCheckBox.Checked;
        _settings.MockDataEnabled = _mockDataCheckBox.Checked;
        _settings.PollIntervalSeconds = (int)_pollIntervalUpDown.Value;
        _settings.AutoScanOnSave = _autoScanCheckBox.Checked;
        _settings.RequireApprovalForProduction = _requireApprovalCheckBox.Checked;
        _settings.InlineInsightsEnabled = _inlineInsightsCheckBox.Checked;

        _settings.AzureMonitor.Enabled = _azureMonitorEnabledCheckBox.Checked;
        _settings.AzureMonitor.AuthMode = _azureAuthCliRadio.Checked ? AzureAuthMode.AzureCli : AzureAuthMode.ServicePrincipal;
        _settings.AzureMonitor.TenantId = _azureTenantIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientId = _azureClientIdTextBox.Text.Trim();
        _settings.AzureMonitor.ClientSecret = _azureClientSecretTextBox.Text;
        _settings.AzureMonitor.SubscriptionId = _azureSubscriptionIdTextBox.Text.Trim();
        _settings.AzureMonitor.ResourceGroup = _azureResourceGroupTextBox.Text.Trim();
        _settings.AzureMonitor.WorkspaceId = _azureWorkspaceIdTextBox.Text.Trim();
        _settings.AzureMonitor.AppInsightsAppId = _azureAppInsightsAppIdTextBox.Text.Trim();
        _settings.AzureMonitor.AppInsightsKey = _azureAppInsightsKeyTextBox.Text;

        _settings.AzureDevOps.Enabled = _azureDevOpsEnabledCheckBox.Checked;
        _settings.AzureDevOps.Organization = _azureDevOpsOrganizationTextBox.Text.Trim();
        _settings.AzureDevOps.Project = _azureDevOpsProjectTextBox.Text.Trim();
        _settings.AzureDevOps.Repository = _azureDevOpsRepositoryTextBox.Text.Trim();
        _settings.AzureDevOps.PersonalAccessToken = _azureDevOpsPatTextBox.Text;

        _settings.Kubernetes.Enabled = _kubernetesEnabledCheckBox.Checked;
        _settings.Kubernetes.ApiServerUrl = _kubernetesApiServerTextBox.Text.Trim();
        _settings.Kubernetes.BearerToken = _kubernetesBearerTokenTextBox.Text;
        _settings.Kubernetes.Namespace = string.IsNullOrWhiteSpace(_kubernetesNamespaceTextBox.Text) ? "default" : _kubernetesNamespaceTextBox.Text.Trim();
        _settings.Kubernetes.SkipTlsVerify = _kubernetesSkipTlsCheckBox.Checked;

        _settings.Prometheus.Enabled = _prometheusEnabledCheckBox.Checked;
        _settings.Prometheus.BaseUrl = _prometheusBaseUrlTextBox.Text.Trim();
        _settings.Prometheus.TimeoutSeconds = (int)_prometheusTimeoutUpDown.Value;
        if (_prometheusAuthBasicRadio.Checked)
        {
            _settings.Prometheus.Username = EmptyToNull(_prometheusUsernameTextBox.Text.Trim());
            _settings.Prometheus.Password = EmptyToNull(_prometheusPasswordTextBox.Text);
            _settings.Prometheus.BearerToken = null;
        }
        else if (_prometheusAuthBearerRadio.Checked)
        {
            _settings.Prometheus.Username = null;
            _settings.Prometheus.Password = null;
            _settings.Prometheus.BearerToken = EmptyToNull(_prometheusBearerTokenTextBox.Text);
        }
        else
        {
            _settings.Prometheus.Username = null;
            _settings.Prometheus.Password = null;
            _settings.Prometheus.BearerToken = null;
        }

        _settings.PagerDuty.Enabled = _pagerDutyEnabledCheckBox.Checked;
        _settings.PagerDuty.ApiToken = _pagerDutyApiTokenTextBox.Text;
        _settings.PagerDuty.ServiceId = EmptyToNull(_pagerDutyServiceIdTextBox.Text.Trim());
        _settings.PagerDuty.TimeoutSeconds = (int)_pagerDutyTimeoutUpDown.Value;

        _settings.GitHubActions.Enabled = _githubEnabledCheckBox.Checked;
        _settings.GitHubActions.Owner = _githubOwnerTextBox.Text.Trim();
        _settings.GitHubActions.Repository = _githubRepositoryTextBox.Text.Trim();
        _settings.GitHubActions.PersonalAccessToken = _githubPatTextBox.Text;
        _settings.GitHubActions.TimeoutSeconds = (int)_githubTimeoutUpDown.Value;

        _settings.GitLab.Enabled = _gitLabEnabledCheckBox.Checked;
        _settings.GitLab.InstanceUrl = _gitLabInstanceUrlTextBox.Text.Trim();
        _settings.GitLab.NamespacePath = _gitLabNamespacePathTextBox.Text.Trim();
        _settings.GitLab.PersonalAccessToken = _gitLabPatTextBox.Text;
        _settings.GitLab.TimeoutSeconds = (int)_gitLabTimeoutUpDown.Value;

        _settings.Bitbucket.Enabled = _bitbucketEnabledCheckBox.Checked;
        _settings.Bitbucket.Workspace = _bitbucketWorkspaceTextBox.Text.Trim();
        _settings.Bitbucket.Repository = _bitbucketRepositoryTextBox.Text.Trim();
        _settings.Bitbucket.Username = _bitbucketUsernameTextBox.Text.Trim();
        _settings.Bitbucket.AppPassword = _bitbucketAppPasswordTextBox.Text;
        _settings.Bitbucket.TimeoutSeconds = (int)_bitbucketTimeoutUpDown.Value;

        _navListBox.Invalidate();
        if (raiseEvent)
            SettingsSaved?.Invoke(_settings);
    }

    private void SaveAndClose()
    {
        ApplySettings();
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task TestConnectionAsync(Func<string?> validator, Func<IAIOpsConnector?> connectorFactory, Label statusLabel, Button button)
    {
        string? validation = validator();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            SetStatus(statusLabel, validation, StatusTone.Error);
            return;
        }

        ApplySettings(false);
        IAIOpsConnector? connector = connectorFactory();
        if (connector is null)
        {
            SetStatus(statusLabel, "Connector is not available.", StatusTone.Error);
            return;
        }

        button.Enabled = false;
        SetStatus(statusLabel, "Testing connection…", StatusTone.Info);
        try
        {
            ConnectorState state = await connector.ConnectAsync().ConfigureAwait(true);
            if (state.Status == ConnectorStatus.Connected)
            {
                string when = state.LastSyncAt?.LocalDateTime.ToString("g") ?? DateTime.Now.ToString("g");
                SetStatus(statusLabel, $"✅ Connected {when}", StatusTone.Success);
            }
            else
            {
                string error = string.IsNullOrWhiteSpace(state.ErrorMessage) ? state.Status.ToString() : $"{state.Status}: {state.ErrorMessage}";
                SetStatus(statusLabel, error, StatusTone.Error);
            }
        }
        catch (Exception ex)
        {
            SetStatus(statusLabel, $"Test failed: {ex.Message}", StatusTone.Error);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private string? ValidateAzureMonitor()
    {
        if (!_azureMonitorEnabledCheckBox.Checked)
            return "Enable the connector before testing the connection.";
        if (_azureAuthSpRadio.Checked)
        {
            if (string.IsNullOrWhiteSpace(_azureTenantIdTextBox.Text)) return "Tenant ID is required for Service Principal auth.";
            if (string.IsNullOrWhiteSpace(_azureClientIdTextBox.Text)) return "Client ID is required for Service Principal auth.";
            if (string.IsNullOrWhiteSpace(_azureClientSecretTextBox.Text)) return "Client Secret is required for Service Principal auth.";
        }
        if (string.IsNullOrWhiteSpace(_azureSubscriptionIdTextBox.Text))
            return "Subscription ID is required.";
        return null;
    }

    private string? ValidateAzureDevOps()
    {
        if (!_azureDevOpsEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_azureDevOpsOrganizationTextBox.Text)) return "Organization is required.";
        if (string.IsNullOrWhiteSpace(_azureDevOpsProjectTextBox.Text)) return "Project is required.";
        if (string.IsNullOrWhiteSpace(_azureDevOpsPatTextBox.Text)) return "PAT is required.";
        return null;
    }

    private string? ValidateKubernetes()
    {
        if (!_kubernetesEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_kubernetesApiServerTextBox.Text)) return "API Server URL is required.";
        if (string.IsNullOrWhiteSpace(_kubernetesBearerTokenTextBox.Text)) return "Bearer Token is required.";
        return null;
    }

    private string? ValidatePrometheus()
    {
        if (!_prometheusEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_prometheusBaseUrlTextBox.Text)) return "Base URL is required.";
        if (_prometheusAuthBasicRadio.Checked)
        {
            if (string.IsNullOrWhiteSpace(_prometheusUsernameTextBox.Text)) return "Username is required for Basic auth.";
            if (string.IsNullOrWhiteSpace(_prometheusPasswordTextBox.Text)) return "Password is required for Basic auth.";
        }
        if (_prometheusAuthBearerRadio.Checked && string.IsNullOrWhiteSpace(_prometheusBearerTokenTextBox.Text))
            return "Bearer Token is required for Bearer auth.";
        return null;
    }

    private string? ValidatePagerDuty()
    {
        if (!_pagerDutyEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_pagerDutyApiTokenTextBox.Text)) return "API Token is required.";
        return null;
    }

    private string? ValidateGitHubActions()
    {
        if (!_githubEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_githubOwnerTextBox.Text)) return "Owner is required.";
        if (string.IsNullOrWhiteSpace(_githubRepositoryTextBox.Text)) return "Repository is required.";
        if (string.IsNullOrWhiteSpace(_githubPatTextBox.Text)) return "PAT is required.";
        return null;
    }

    private string? ValidateGitLab()
    {
        if (!_gitLabEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_gitLabInstanceUrlTextBox.Text)) return "Instance URL is required.";
        if (string.IsNullOrWhiteSpace(_gitLabNamespacePathTextBox.Text)) return "Namespace Path is required.";
        if (string.IsNullOrWhiteSpace(_gitLabPatTextBox.Text)) return "PAT is required.";
        return null;
    }

    private string? ValidateBitbucket()
    {
        if (!_bitbucketEnabledCheckBox.Checked) return "Enable the connector before testing the connection.";
        if (string.IsNullOrWhiteSpace(_bitbucketWorkspaceTextBox.Text)) return "Workspace is required.";
        if (string.IsNullOrWhiteSpace(_bitbucketRepositoryTextBox.Text)) return "Repository is required.";
        if (string.IsNullOrWhiteSpace(_bitbucketUsernameTextBox.Text)) return "Username is required.";
        if (string.IsNullOrWhiteSpace(_bitbucketAppPasswordTextBox.Text)) return "App Password is required.";
        return null;
    }

    private async Task ImportAzureSubscriptionAsync()
    {
        SetStatus(_azureStatusLabel, "Querying Azure CLI subscriptions…", StatusTone.Info);
        try
        {
            (int exitCode, string output, string _) = await RunCommandAsync(["az", "account", "list", "--output", "json", "--all"]).ConfigureAwait(true);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                SetStatus(_azureStatusLabel, "Azure CLI import failed — run 'az login' first.", StatusTone.Error);
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(output);
            List<(string Id, string Name, string TenantId, bool IsDefault)> subscriptions = [];
            foreach (JsonElement el in doc.RootElement.EnumerateArray())
            {
                string id = el.TryGetProperty("id", out JsonElement p1) ? p1.GetString() ?? string.Empty : string.Empty;
                string name = el.TryGetProperty("name", out JsonElement p2) ? p2.GetString() ?? string.Empty : string.Empty;
                string tenantId = el.TryGetProperty("tenantId", out JsonElement p3) ? p3.GetString() ?? string.Empty : string.Empty;
                bool isDefault = el.TryGetProperty("isDefault", out JsonElement p4) && p4.GetBoolean();
                if (!string.IsNullOrWhiteSpace(id))
                    subscriptions.Add((id, name, tenantId, isDefault));
            }

            if (subscriptions.Count == 0)
            {
                SetStatus(_azureStatusLabel, "No subscriptions found — run 'az login' first.", StatusTone.Error);
                return;
            }

            var chosen = subscriptions.Count == 1 ? subscriptions[0] : PickSubscription(subscriptions);
            if (chosen == default)
            {
                SetStatus(_azureStatusLabel, "Azure subscription import cancelled.", StatusTone.Info);
                return;
            }

            _azureTenantIdTextBox.Text = chosen.TenantId;
            _azureSubscriptionIdTextBox.Text = chosen.Id;
            string label = subscriptions.Count == 1 ? "Imported" : "Imported selection";
            SetStatus(_azureStatusLabel, $"{label}: {chosen.Name}", StatusTone.Success);
        }
        catch (Exception ex)
        {
            SetStatus(_azureStatusLabel, $"Azure CLI import failed: {ex.Message}", StatusTone.Error);
        }
    }

    private async Task ImportAzureWorkspaceAsync()
    {
        string subscriptionId = _azureSubscriptionIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            SetStatus(_azureStatusLabel, "Enter or import a Subscription ID first.", StatusTone.Error);
            return;
        }

        SetStatus(_azureStatusLabel, "Querying Log Analytics workspaces…", StatusTone.Info);
        try
        {
            (int exitCode, string output, string _) = await RunCommandAsync(["az", "monitor", "log-analytics", "workspace", "list", "--subscription", subscriptionId, "--output", "json"]).ConfigureAwait(true);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                SetStatus(_azureStatusLabel, "Workspace query failed — check subscription ID and az login.", StatusTone.Error);
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(output);
            List<(string WorkspaceId, string Name, string ResourceGroup, string Location)> workspaces = [];
            foreach (JsonElement el in doc.RootElement.EnumerateArray())
            {
                string workspaceId = el.TryGetProperty("customerId", out JsonElement p1) ? p1.GetString() ?? string.Empty : string.Empty;
                string name = el.TryGetProperty("name", out JsonElement p2) ? p2.GetString() ?? string.Empty : string.Empty;
                string resourceGroup = el.TryGetProperty("resourceGroup", out JsonElement p3) ? p3.GetString() ?? string.Empty : string.Empty;
                string location = el.TryGetProperty("location", out JsonElement p4) ? p4.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(workspaceId))
                    workspaces.Add((workspaceId, name, resourceGroup, location));
            }

            if (workspaces.Count == 0)
            {
                SetStatus(_azureStatusLabel, "No Log Analytics workspaces found in this subscription.", StatusTone.Error);
                return;
            }

            var chosen = workspaces.Count == 1 ? workspaces[0] : PickWorkspace(workspaces);
            if (chosen == default)
            {
                SetStatus(_azureStatusLabel, "Workspace import cancelled.", StatusTone.Info);
                return;
            }

            _azureResourceGroupTextBox.Text = chosen.ResourceGroup;
            _azureWorkspaceIdTextBox.Text = chosen.WorkspaceId;
            string label = workspaces.Count == 1 ? "Imported" : "Imported selection";
            SetStatus(_azureStatusLabel, $"{label}: {chosen.Name}", StatusTone.Success);
        }
        catch (Exception ex)
        {
            SetStatus(_azureStatusLabel, $"Workspace import failed: {ex.Message}", StatusTone.Error);
        }
    }

    private async Task ImportAzureAppInsightsAsync()
    {
        string subscriptionId = _azureSubscriptionIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            SetStatus(_azureStatusLabel, "Enter or import a Subscription ID first.", StatusTone.Error);
            return;
        }

        SetStatus(_azureStatusLabel, "Querying Application Insights components…", StatusTone.Info);
        try
        {
            string armUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Insights/components?api-version=2020-02-02";
            (int exitCode, string output, string stderr) = await RunCommandAsync(["az", "rest", "--method", "get", "--url", armUrl, "--output", "json"]).ConfigureAwait(true);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                string hint = stderr.Contains("az login", StringComparison.OrdinalIgnoreCase)
                    ? "Run 'az login' first."
                    : string.IsNullOrWhiteSpace(stderr) ? "Check subscription ID and az login." : stderr.Trim().Split('\n')[0];
                SetStatus(_azureStatusLabel, $"App Insights query failed — {hint}", StatusTone.Error);
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement items = doc.RootElement.TryGetProperty("value", out JsonElement value) ? value : doc.RootElement;
            List<(string AppId, string Name, string ResourceGroup, string Location)> components = [];
            foreach (JsonElement el in items.EnumerateArray())
            {
                string name = el.TryGetProperty("name", out JsonElement p1) ? p1.GetString() ?? string.Empty : string.Empty;
                string resourceGroup = el.TryGetProperty("resourceGroup", out JsonElement p2) ? p2.GetString() ?? string.Empty : string.Empty;
                string location = el.TryGetProperty("location", out JsonElement p3) ? p3.GetString() ?? string.Empty : string.Empty;
                string appId = string.Empty;
                if (el.TryGetProperty("properties", out JsonElement properties) && properties.TryGetProperty("AppId", out JsonElement appIdElement))
                    appId = appIdElement.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(appId))
                    components.Add((appId, name, resourceGroup, location));
            }

            if (components.Count == 0)
            {
                SetStatus(_azureStatusLabel, "No Application Insights components found in this subscription.", StatusTone.Error);
                return;
            }

            var chosen = components.Count == 1 ? components[0] : PickAppInsightsComponent(components);
            if (chosen == default)
            {
                SetStatus(_azureStatusLabel, "App Insights import cancelled.", StatusTone.Info);
                return;
            }

            _azureAppInsightsAppIdTextBox.Text = chosen.AppId;
            if (string.IsNullOrWhiteSpace(_azureResourceGroupTextBox.Text))
                _azureResourceGroupTextBox.Text = chosen.ResourceGroup;
            string label = components.Count == 1 ? "Imported" : "Imported selection";
            SetStatus(_azureStatusLabel, $"{label}: {chosen.Name}", StatusTone.Success);
        }
        catch (Exception ex)
        {
            SetStatus(_azureStatusLabel, $"App Insights import failed: {ex.Message}", StatusTone.Error);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCommandAsync(IReadOnlyList<string> arguments)
    {
        using Process proc = new()
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        proc.StartInfo.ArgumentList.Add("/c");
        foreach (string arg in arguments)
            proc.StartInfo.ArgumentList.Add(arg);

        proc.Start();
        string output = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(true);
        string error = await proc.StandardError.ReadToEndAsync().ConfigureAwait(true);
        await proc.WaitForExitAsync().ConfigureAwait(true);
        return (proc.ExitCode, output, error);
    }

    private void ApplyAzureAuthMode()
    {
        bool servicePrincipal = _azureAuthSpRadio.Checked;
        _azureTenantIdLabel.Enabled = servicePrincipal;
        _azureTenantIdTextBox.Enabled = servicePrincipal;
        _azureClientIdLabel.Enabled = servicePrincipal;
        _azureClientIdTextBox.Enabled = servicePrincipal;
        _azureClientSecretLabel.Enabled = servicePrincipal;
        _azureClientSecretWrapper.Enabled = servicePrincipal;
        _azureImportCliButton.Enabled = _azureAuthCliRadio.Checked;
    }

    private void ApplyPrometheusAuthMode()
    {
        bool basic = _prometheusAuthBasicRadio.Checked;
        bool bearer = _prometheusAuthBearerRadio.Checked;

        _prometheusUsernameLabel.Enabled = basic;
        _prometheusUsernameTextBox.Enabled = basic;
        _prometheusPasswordLabel.Enabled = basic;
        _prometheusPasswordWrapper.Enabled = basic;
        _prometheusBearerTokenLabel.Enabled = bearer;
        _prometheusBearerWrapper.Enabled = bearer;
    }

    private void HandleNavigationChanged()
    {
        if (_navListBox.SelectedIndex < 0 || _navListBox.SelectedIndex >= _navItems.Count)
            return;

        NavItem item = _navItems[_navListBox.SelectedIndex];
        if (item.IsSeparator || item.SectionId is null)
        {
            BeginInvoke(new Action(() => _navListBox.SelectedIndex = _selectedNavIndex));
            return;
        }

        _selectedNavIndex = _navListBox.SelectedIndex;
        ShowSection(item.SectionId.Value);
    }

    private void ShowSection(SectionId sectionId)
    {
        foreach ((SectionId key, Panel panel) in _sectionPanels)
            panel.Visible = key == sectionId;

        if (_sectionPanels.TryGetValue(sectionId, out Panel? selected))
        {
            selected.BringToFront();
            _contentScrollPanel.AutoScrollPosition = new Point(0, 0);
        }
    }

    private void AddSection(SectionId sectionId, Panel panel)
    {
        panel.Visible = false;
        _sectionPanels[sectionId] = panel;
        _contentScrollPanel.Controls.Add(panel);
    }

    private void ResizeSectionPanels()
    {
        int width = Math.Max(640, _contentScrollPanel.ClientSize.Width - 24);
        foreach (Panel panel in _sectionPanels.Values)
        {
            panel.Location = new Point(0, 0);
            panel.Width = width;
        }
    }

    private Panel CreateSectionPanel() => new()
    {
        AutoScroll = false,
        BorderStyle = BorderStyle.None,
        Width = 760,
        Height = 400,
    };

    private ListBox CreateNavigationListBox()
    {
        ListBox listBox = new()
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 34,
            IntegralHeight = false,
        };
        listBox.DrawItem += DrawNavItem;
        return listBox;
    }

    private void DrawNavItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _navItems.Count)
            return;

        NavItem item = _navItems[e.Index];
        Rectangle bounds = e.Bounds;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected && !item.IsSeparator;

        using SolidBrush backgroundBrush = new(selected ? Blend(_theme.PanelBackground, _theme.Accent, 0.24f) : _theme.PanelBackground);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (item.IsSeparator)
        {
            TextRenderer.DrawText(e.Graphics, $"── {item.Text} ──", new Font("Segoe UI", 8.5f, FontStyle.Bold), bounds,
                AIOpsUiHelper.SecondaryText(_theme), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        if (selected)
        {
            using SolidBrush accentBrush = new(_theme.Accent);
            e.Graphics.FillRectangle(accentBrush, new Rectangle(bounds.Left, bounds.Top + 4, 4, bounds.Height - 8));
        }

        Rectangle textBounds = Rectangle.Inflate(bounds, -12, 0);
        string label = $"{item.Icon}  {item.Text}";
        Color textColor = selected ? GetReadableTextColor(backgroundBrush.Color) : _theme.Text;
        TextRenderer.DrawText(e.Graphics, label, Font, new Rectangle(textBounds.Left, textBounds.Top, textBounds.Width - 30, textBounds.Height),
            textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (item.EnabledAccessor is not null)
        {
            bool enabled = item.EnabledAccessor();
            string dot = enabled ? "●" : "○";
            Color dotColor = enabled ? Color.MediumSeaGreen : AIOpsUiHelper.SecondaryText(_theme);
            TextRenderer.DrawText(e.Graphics, dot, Font, new Rectangle(bounds.Right - 24, bounds.Top, 18, bounds.Height),
                dotColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private Button CreateButton(string text, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 30,
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(8, 0, 0, 0),
    };

    private static CheckBox CreateCheckBox(string text, bool prominent = false) => new()
    {
        Text = text,
        AutoSize = true,
        Font = prominent ? new Font("Segoe UI", 10f, FontStyle.Bold) : new Font("Segoe UI", 9.25f),
    };

    private static NumericUpDown CreateNumericUpDown(int minimum, int maximum) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Width = 110,
    };

    private static TextBox CreateTextBox(string placeholder) => new()
    {
        PlaceholderText = placeholder,
    };

    private FlowLayoutPanel CreateRadioGroup(params RadioButton[] buttons)
    {
        FlowLayoutPanel panel = new()
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        panel.Controls.AddRange(buttons);
        return panel;
    }

    private (TextBox TextBox, Panel Wrapper) CreateSecretField(string encryptedValue, string placeholder)
    {
        TextBox textBox = CreateTextBox(placeholder);
        textBox.UseSystemPasswordChar = true;
        _toolTip.SetToolTip(textBox, DpapiTooltip);

        Label badge = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
        };

        Panel wrapper = new()
        {
            Height = Math.Max(textBox.Height, badge.Height) + 2,
        };
        wrapper.Controls.AddRange([textBox, badge]);
        wrapper.Resize += (_, _) =>
        {
            int badgeWidth = badge.PreferredWidth;
            badge.Location = new Point(Math.Max(0, wrapper.ClientSize.Width - badgeWidth), Math.Max(3, (wrapper.ClientSize.Height - badge.Height) / 2));
            textBox.Location = new Point(0, 0);
            textBox.Width = Math.Max(180, badge.Left - 8);
        };

        SecretFieldState state = new(textBox, badge, encryptedValue);
        _secretFields.Add(state);
        textBox.TextChanged += (_, _) => UpdateSecretBadge(state);
        UpdateSecretBadge(state);
        return (textBox, wrapper);
    }

    private Panel CreateInlineButtonHost(Control primaryControl, Button button)
    {
        Panel host = new() { Height = Math.Max(primaryControl.Height, button.Height) };
        host.Controls.AddRange([primaryControl, button]);
        host.Resize += (_, _) =>
        {
            button.Location = new Point(Math.Max(0, host.ClientSize.Width - button.Width), 0);
            primaryControl.Location = new Point(0, 0);
            primaryControl.Width = Math.Max(180, button.Left - 8);
        };
        return host;
    }

    private void AddHeaderCard(Panel container, ref int y, string icon, string title, string description)
    {
        Panel card = new()
        {
            Location = new Point(12, y),
            Size = new Size(Math.Max(620, container.Width - 24), 86),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Padding = new Padding(16, 12, 16, 12),
        };
        card.Paint += (_, e) =>
        {
            using Pen pen = new(Blend(_theme.Border, _theme.Accent, 0.35f));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        Label titleLabel = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Location = new Point(16, 12),
            Text = $"{icon} {title}",
        };
        Label descriptionLabel = new()
        {
            AutoSize = true,
            MaximumSize = new Size(card.Width - 32, 0),
            Location = new Point(16, 42),
            Text = description,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _descriptionLabels.Add(descriptionLabel);
        _headerCards.Add(card);
        card.Controls.AddRange([titleLabel, descriptionLabel]);
        container.Controls.Add(card);
        y += card.Height + 18;
    }

    private void AddSeparator(Panel container, ref int y)
    {
        Panel separator = new()
        {
            Location = new Point(12, y),
            Size = new Size(Math.Max(620, container.Width - 24), 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        _separatorLines.Add(separator);
        container.Controls.Add(separator);
        y += 16;
    }

    private void AddToggle(Panel container, ref int y, CheckBox checkBox, string hint)
    {
        checkBox.Location = new Point(InputX, y);
        checkBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        container.Controls.Add(checkBox);
        y = checkBox.Bottom + 2;

        if (!string.IsNullOrWhiteSpace(hint))
        {
            Label hintLabel = CreateHintLabel(hint);
            hintLabel.Location = new Point(InputX, y);
            hintLabel.MaximumSize = new Size(Math.Max(360, container.Width - InputX - RightPadding), 0);
            hintLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            container.Controls.Add(hintLabel);
            y = hintLabel.Bottom + 10;
        }
        else
        {
            y += 8;
        }
    }

    private int AddField(Panel container, ref int y, string label, Control input, string hint = "")
        => AddField(container, ref y, label, input, hint, out _);

    private int AddField(Panel container, ref int y, string label, Control input, string hint, out Label fieldLabel)
    {
        fieldLabel = new Label
        {
            AutoSize = false,
            Width = LabelWidth,
            Height = Math.Max(22, input.Height),
            Location = new Point(LabelX, y + 2),
            Text = label,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        container.Controls.Add(fieldLabel);

        input.Location = new Point(InputX, y);
        input.Width = Math.Max(320, container.Width - InputX - RightPadding);
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        container.Controls.Add(input);

        int nextY = y + input.Height;
        if (!string.IsNullOrWhiteSpace(hint))
        {
            Label hintLabel = CreateHintLabel(hint);
            hintLabel.Location = new Point(InputX, nextY + 2);
            hintLabel.MaximumSize = new Size(Math.Max(320, container.Width - InputX - RightPadding), 0);
            hintLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            container.Controls.Add(hintLabel);
            nextY = hintLabel.Bottom;
        }

        y = nextY + 8;
        return y;
    }

    private void AddTestArea(Panel container, ref int y, Button button, Label statusLabel)
    {
        button.Location = new Point(InputX, y);
        button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        container.Controls.Add(button);

        statusLabel.Location = new Point(InputX, button.Bottom + 8);
        statusLabel.MaximumSize = new Size(Math.Max(320, container.Width - InputX - RightPadding), 0);
        statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        container.Controls.Add(statusLabel);
        y = statusLabel.Bottom + 14;
    }

    private Label CreateHintLabel(string text)
    {
        Label label = new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            Text = text,
        };
        _hintLabels.Add(label);
        return label;
    }

    private Label CreateStatusLabel()
    {
        Label label = new()
        {
            AutoSize = true,
            Text = "Connection status will appear here.",
        };
        _statusTones[label] = StatusTone.Neutral;
        return label;
    }

    private void SetStatus(Label label, string message, StatusTone tone)
    {
        label.Text = message;
        _statusTones[label] = tone;
        ApplyStatusTheme(label, tone);
    }

    private void ApplyStatusTheme(Label label, StatusTone tone)
    {
        label.ForeColor = tone switch
        {
            StatusTone.Success => Color.MediumSeaGreen,
            StatusTone.Error => Color.IndianRed,
            StatusTone.Info => Blend(_theme.Text, _theme.Accent, 0.55f),
            _ => AIOpsUiHelper.SecondaryText(_theme),
        };
    }

    private void UpdateSecretBadge(SecretFieldState state)
    {
        if (!string.IsNullOrEmpty(state.TextBox.Text))
        {
            state.Badge.Text = "🔒 Will encrypt";
            state.Badge.ForeColor = Color.MediumSeaGreen;
        }
        else if (!string.IsNullOrEmpty(state.EncryptedValue))
        {
            state.Badge.Text = "🔒 Encrypted";
            state.Badge.ForeColor = Color.SeaGreen;
        }
        else
        {
            state.Badge.Text = "🔓 Not set";
            state.Badge.ForeColor = AIOpsUiHelper.SecondaryText(_theme);
        }
    }

    private IAIOpsConnector GetAzureMonitorConnectorForTest()
    {
        AzureMonitorConnector = CreateOrRefreshConnector(AzureMonitorConnector, typeof(AzureMonitorConnector), () => new AzureMonitorConnector(_settings.AzureMonitor));
        return AzureMonitorConnector;
    }

    private IAIOpsConnector GetAzureDevOpsConnectorForTest()
    {
        AzureDevOpsConnector = CreateOrRefreshConnector(AzureDevOpsConnector, typeof(AzureDevOpsConnector), () => new AzureDevOpsConnector(_settings.AzureDevOps));
        return AzureDevOpsConnector;
    }

    private IAIOpsConnector GetKubernetesConnectorForTest()
    {
        KubernetesConnector = CreateOrRefreshConnector(KubernetesConnector, typeof(KubernetesConnector), () => new KubernetesConnector(_settings.Kubernetes));
        return KubernetesConnector;
    }

    private IAIOpsConnector GetPrometheusConnectorForTest()
    {
        PrometheusConnector = CreateOrRefreshConnector(PrometheusConnector, typeof(PrometheusConnector), () => new PrometheusConnector(_settings.Prometheus));
        return PrometheusConnector;
    }

    private IAIOpsConnector GetPagerDutyConnectorForTest()
    {
        PagerDutyConnector = CreateOrRefreshConnector(PagerDutyConnector, typeof(PagerDutyConnector), () => new PagerDutyConnector(_settings.PagerDuty));
        return PagerDutyConnector;
    }

    private IAIOpsConnector GetGitHubActionsConnectorForTest()
    {
        GitHubActionsConnector = CreateOrRefreshConnector(GitHubActionsConnector, typeof(GitHubActionsConnector), () => new GitHubActionsConnector(_settings.GitHubActions));
        return GitHubActionsConnector;
    }

    private IAIOpsConnector GetGitLabConnectorForTest()
    {
        GitLabConnector = CreateOrRefreshConnector(GitLabConnector, typeof(GitLabConnector), () => new GitLabConnector(_settings.GitLab));
        return GitLabConnector;
    }

    private IAIOpsConnector GetBitbucketConnectorForTest()
    {
        BitbucketConnector = CreateOrRefreshConnector(BitbucketConnector, typeof(BitbucketConnector), () => new BitbucketConnector(_settings.Bitbucket));
        return BitbucketConnector;
    }

    private static IAIOpsConnector CreateOrRefreshConnector(IAIOpsConnector? connector, Type expectedType, Func<IAIOpsConnector> factory)
    {
        if (connector is null || connector.GetType() == expectedType)
        {
            connector?.Dispose();
            return factory();
        }

        return connector;
    }

    private static void DisposeConnector(IAIOpsConnector? connector)
    {
        try
        {
            connector?.Dispose();
        }
        catch
        {
        }
    }

    private static decimal ClampNumeric(NumericUpDown control, int value)
        => Math.Max(control.Minimum, Math.Min(control.Maximum, value));

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static Color Blend(Color baseColor, Color overlayColor, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        int r = (int)Math.Round(baseColor.R + ((overlayColor.R - baseColor.R) * amount));
        int g = (int)Math.Round(baseColor.G + ((overlayColor.G - baseColor.G) * amount));
        int b = (int)Math.Round(baseColor.B + ((overlayColor.B - baseColor.B) * amount));
        return Color.FromArgb(r, g, b);
    }

    private static Color GetReadableTextColor(Color color)
    {
        double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return luminance >= 150 ? Color.Black : Color.White;
    }

    private static (string AppId, string Name, string ResourceGroup, string Location) PickAppInsightsComponent(
        List<(string AppId, string Name, string ResourceGroup, string Location)> components)
    {
        using Form dlg = new()
        {
            Text = "Select Application Insights Component",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(620, 300),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        ThemeManager.Instance.ApplyTheme(dlg, applyToMenus: false);

        Label lbl = new() { Text = $"Found {components.Count} components. Select one:", AutoSize = true, Location = new Point(12, 12) };
        ListView list = new()
        {
            Location = new Point(12, 36),
            Size = new Size(596, 180),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Name", 200);
        list.Columns.Add("App ID", 240);
        list.Columns.Add("Resource Group", 110);
        list.Columns.Add("Location", 70);
        foreach (var component in components)
        {
            ListViewItem item = new(component.Name);
            item.SubItems.Add(component.AppId);
            item.SubItems.Add(component.ResourceGroup);
            item.SubItems.Add(component.Location);
            item.Tag = component;
            list.Items.Add(item);
        }
        if (list.Items.Count > 0)
        {
            list.Items[0].Selected = true;
            list.Items[0].EnsureVisible();
        }

        Button okButton = new() { Text = "Select", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK, Location = new Point(dlg.ClientSize.Width - 196, 236) };
        Button cancelButton = new() { Text = "Cancel", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel, Location = new Point(dlg.ClientSize.Width - 100, 236) };
        dlg.AcceptButton = okButton;
        dlg.CancelButton = cancelButton;
        dlg.Controls.AddRange([lbl, list, okButton, cancelButton]);
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0)
            return default;
        return ((string AppId, string Name, string ResourceGroup, string Location))list.SelectedItems[0].Tag!;
    }

    private static (string WorkspaceId, string Name, string ResourceGroup, string Location) PickWorkspace(
        List<(string WorkspaceId, string Name, string ResourceGroup, string Location)> workspaces)
    {
        using Form dlg = new()
        {
            Text = "Select Log Analytics Workspace",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(620, 320),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        ThemeManager.Instance.ApplyTheme(dlg, applyToMenus: false);

        Label lbl = new()
        {
            Text = $"Found {workspaces.Count} workspaces. Select one:",
            AutoSize = true,
            Location = new Point(12, 12),
        };

        ListView list = new()
        {
            Location = new Point(12, 36),
            Size = new Size(596, 200),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Workspace Name", 180);
        list.Columns.Add("Workspace ID (customerId)", 240);
        list.Columns.Add("Resource Group", 120);
        list.Columns.Add("Location", 80);

        foreach (var workspace in workspaces)
        {
            ListViewItem item = new(workspace.Name);
            item.SubItems.Add(workspace.WorkspaceId);
            item.SubItems.Add(workspace.ResourceGroup);
            item.SubItems.Add(workspace.Location);
            item.Tag = workspace;
            list.Items.Add(item);
        }
        if (list.Items.Count > 0)
        {
            list.Items[0].Selected = true;
            list.Items[0].EnsureVisible();
        }

        Button okButton = new() { Text = "Select", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        Button cancelButton = new() { Text = "Cancel", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        okButton.Location = new Point(dlg.ClientSize.Width - 196, 254);
        cancelButton.Location = new Point(dlg.ClientSize.Width - 100, 254);

        dlg.AcceptButton = okButton;
        dlg.CancelButton = cancelButton;
        dlg.Controls.AddRange([lbl, list, okButton, cancelButton]);
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };

        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0)
            return default;

        return ((string WorkspaceId, string Name, string ResourceGroup, string Location))list.SelectedItems[0].Tag!;
    }

    private static (string Id, string Name, string TenantId, bool IsDefault) PickSubscription(
        List<(string Id, string Name, string TenantId, bool IsDefault)> subscriptions)
    {
        using Form dlg = new()
        {
            Text = "Select Azure Subscription",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(560, 340),
            MinimizeBox = false,
            MaximizeBox = false,
        };
        ThemeManager.Instance.ApplyTheme(dlg, applyToMenus: false);

        Label lbl = new()
        {
            Text = $"Found {subscriptions.Count} subscriptions. Select one to use:",
            AutoSize = true,
            Location = new Point(12, 12),
        };

        ListView list = new()
        {
            Location = new Point(12, 36),
            Size = new Size(536, 220),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
        };
        list.Columns.Add("Subscription Name", 240);
        list.Columns.Add("Subscription ID", 180);
        list.Columns.Add("Tenant ID", 110);

        int defaultIndex = 0;
        for (int i = 0; i < subscriptions.Count; i++)
        {
            var subscription = subscriptions[i];
            ListViewItem item = new(subscription.IsDefault ? $"★ {subscription.Name}" : subscription.Name);
            item.SubItems.Add(subscription.Id);
            item.SubItems.Add(subscription.TenantId);
            item.Tag = subscription;
            list.Items.Add(item);
            if (subscription.IsDefault)
                defaultIndex = i;
        }
        if (list.Items.Count > 0)
        {
            list.Items[defaultIndex].Selected = true;
            list.Items[defaultIndex].EnsureVisible();
        }

        Button okButton = new() { Text = "Select", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
        Button cancelButton = new() { Text = "Cancel", Width = 88, Height = 26, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
        okButton.Location = new Point(dlg.ClientSize.Width - 196, 278);
        cancelButton.Location = new Point(dlg.ClientSize.Width - 100, 278);

        dlg.AcceptButton = okButton;
        dlg.CancelButton = cancelButton;
        dlg.Controls.AddRange([lbl, list, okButton, cancelButton]);
        list.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };

        if (dlg.ShowDialog() != DialogResult.OK || list.SelectedItems.Count == 0)
            return default;

        return ((string Id, string Name, string TenantId, bool IsDefault))list.SelectedItems[0].Tag!;
    }

    private enum SectionId
    {
        General,
        AzureMonitor,
        AzureDevOps,
        Kubernetes,
        Prometheus,
        PagerDuty,
        GitHubActions,
        GitLab,
        Bitbucket,
    }

    private enum StatusTone
    {
        Neutral,
        Info,
        Success,
        Error,
    }

    private sealed class NavItem(SectionId? sectionId, string icon, string text, Func<bool>? enabledAccessor = null, bool isSeparator = false)
    {
        public SectionId? SectionId { get; } = sectionId;
        public string Icon { get; } = icon;
        public string Text { get; } = text;
        public Func<bool>? EnabledAccessor { get; } = enabledAccessor;
        public bool IsSeparator { get; } = isSeparator;

        public static NavItem Separator(string text) => new(null, string.Empty, text, null, true);

        public override string ToString() => Text;
    }

    private sealed class SecretFieldState(TextBox textBox, Label badge, string encryptedValue)
    {
        public TextBox TextBox { get; } = textBox;
        public Label Badge { get; } = badge;
        public string EncryptedValue { get; } = encryptedValue;
    }
}

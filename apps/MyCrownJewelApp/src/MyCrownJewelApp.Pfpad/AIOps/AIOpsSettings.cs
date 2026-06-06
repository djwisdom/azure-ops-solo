namespace MyCrownJewelApp.Pfpad.AIOps;

public class AIOpsSettings
{
    public bool Enabled { get; set; } = true;
    public bool MockDataEnabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 60;
    public bool InlineInsightsEnabled { get; set; } = true;
    public bool AutoScanOnSave { get; set; } = true;
    public bool RequireApprovalForProduction { get; set; } = true;

    public AzureMonitorSettings AzureMonitor { get; set; } = new();
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public KubernetesSettings Kubernetes { get; set; } = new();
    public PrometheusSettings Prometheus { get; set; } = new();
    public PagerDutySettings PagerDuty { get; set; } = new();
    public GitHubActionsSettings GitHubActions { get; set; } = new();

    // ── DPAPI encryption helpers ───────────────────────────────────────────────

    /// <summary>
    /// Decrypts all <c>Encrypted*</c> fields into their plaintext counterparts
    /// in-place. Safe to call multiple times. Supports migration from older
    /// settings files that stored plaintext secrets directly: if a
    /// <c>Encrypted*</c> field is empty but the plain field has a value that
    /// looks like plaintext, the plain value is kept as-is and will be
    /// encrypted on the next <see cref="CreateEncryptedCopy"/> call.
    /// </summary>
    public void LoadSecretsFromEncrypted()
    {
        AzureMonitor.LoadSecretsFromEncrypted();
        AzureDevOps.LoadSecretsFromEncrypted();
        Kubernetes.LoadSecretsFromEncrypted();
        Prometheus.LoadSecretsFromEncrypted();
        PagerDuty.LoadSecretsFromEncrypted();
        GitHubActions.LoadSecretsFromEncrypted();
    }

    /// <summary>
    /// Returns a deep clone of this instance suitable for persistence.
    /// All sensitive plain-text fields are cleared; their DPAPI-encrypted
    /// equivalents are populated. The original object is not modified and
    /// retains its decrypted in-memory values for connector use.
    /// </summary>
    public AIOpsSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled,
        MockDataEnabled = MockDataEnabled,
        PollIntervalSeconds = PollIntervalSeconds,
        InlineInsightsEnabled = InlineInsightsEnabled,
        AutoScanOnSave = AutoScanOnSave,
        RequireApprovalForProduction = RequireApprovalForProduction,
        AzureMonitor = AzureMonitor.CreateEncryptedCopy(),
        AzureDevOps = AzureDevOps.CreateEncryptedCopy(),
        Kubernetes = Kubernetes.CreateEncryptedCopy(),
        Prometheus = Prometheus.CreateEncryptedCopy(),
        PagerDuty = PagerDuty.CreateEncryptedCopy(),
        GitHubActions = GitHubActions.CreateEncryptedCopy(),
    };
}

public class AzureMonitorSettings
{
    public bool Enabled { get; set; } = false;
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedClientSecret"/>.</summary>
    public string ClientSecret { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string ResourceGroup { get; set; } = "";
    public string WorkspaceId { get; set; } = "";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedAppInsightsKey"/>.</summary>
    public string AppInsightsKey { get; set; } = "";

    // ── DPAPI-encrypted fields (persisted to settings file) ───────────────────
    public string EncryptedClientSecret { get; set; } = "";
    public string EncryptedAppInsightsKey { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedClientSecret))
            ClientSecret = DpapiSettingsProtector.Unprotect(EncryptedClientSecret);
        if (!string.IsNullOrEmpty(EncryptedAppInsightsKey))
            AppInsightsKey = DpapiSettingsProtector.Unprotect(EncryptedAppInsightsKey);
    }

    internal AzureMonitorSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, TenantId = TenantId, ClientId = ClientId,
        SubscriptionId = SubscriptionId, ResourceGroup = ResourceGroup,
        WorkspaceId = WorkspaceId,
        ClientSecret = "", AppInsightsKey = "",
        EncryptedClientSecret  = DpapiSettingsProtector.Protect(ClientSecret),
        EncryptedAppInsightsKey = DpapiSettingsProtector.Protect(AppInsightsKey),
    };
}

public class AzureDevOpsSettings
{
    public bool Enabled { get; set; } = false;
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedPersonalAccessToken"/>.</summary>
    public string PersonalAccessToken { get; set; } = "";

    // ── DPAPI-encrypted fields ────────────────────────────────────────────────
    public string EncryptedPersonalAccessToken { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedPersonalAccessToken))
            PersonalAccessToken = DpapiSettingsProtector.Unprotect(EncryptedPersonalAccessToken);
    }

    internal AzureDevOpsSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, Organization = Organization, Project = Project,
        PersonalAccessToken = "",
        EncryptedPersonalAccessToken = DpapiSettingsProtector.Protect(PersonalAccessToken),
    };
}

public class KubernetesSettings
{
    public bool Enabled { get; set; } = false;
    public string ApiServerUrl { get; set; } = "https://localhost:6443";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedBearerToken"/>.</summary>
    public string BearerToken { get; set; } = "";
    public string Namespace { get; set; } = "default";
    public bool SkipTlsVerify { get; set; } = false;

    // ── DPAPI-encrypted fields ────────────────────────────────────────────────
    public string EncryptedBearerToken { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedBearerToken))
            BearerToken = DpapiSettingsProtector.Unprotect(EncryptedBearerToken);
    }

    internal KubernetesSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, ApiServerUrl = ApiServerUrl,
        Namespace = Namespace, SkipTlsVerify = SkipTlsVerify,
        BearerToken = "",
        EncryptedBearerToken = DpapiSettingsProtector.Protect(BearerToken),
    };
}

public class PrometheusSettings
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "http://localhost:9090";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedBearerToken"/>.</summary>
    public string? BearerToken { get; set; }
    public string? Username { get; set; }
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedPassword"/>.</summary>
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 10;

    // ── DPAPI-encrypted fields ────────────────────────────────────────────────
    public string EncryptedBearerToken { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedBearerToken))
            BearerToken = DpapiSettingsProtector.Unprotect(EncryptedBearerToken);
        if (!string.IsNullOrEmpty(EncryptedPassword))
            Password = DpapiSettingsProtector.Unprotect(EncryptedPassword);
    }

    internal PrometheusSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, BaseUrl = BaseUrl,
        Username = Username, TimeoutSeconds = TimeoutSeconds,
        BearerToken = null, Password = null,
        EncryptedBearerToken = DpapiSettingsProtector.Protect(BearerToken),
        EncryptedPassword    = DpapiSettingsProtector.Protect(Password),
    };
}

public class PagerDutySettings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedApiToken"/>.</summary>
    public string ApiToken { get; set; } = "";
    public string? ServiceId { get; set; }
    public int TimeoutSeconds { get; set; } = 10;

    // ── DPAPI-encrypted fields ────────────────────────────────────────────────
    public string EncryptedApiToken { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedApiToken))
            ApiToken = DpapiSettingsProtector.Unprotect(EncryptedApiToken);
    }

    internal PagerDutySettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, ServiceId = ServiceId, TimeoutSeconds = TimeoutSeconds,
        ApiToken = "",
        EncryptedApiToken = DpapiSettingsProtector.Protect(ApiToken),
    };
}

public class GitHubActionsSettings
{
    public bool Enabled { get; set; } = false;
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    /// <summary>Plaintext at runtime; always written as empty to disk. Use <see cref="EncryptedPersonalAccessToken"/>.</summary>
    public string PersonalAccessToken { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;

    // ── DPAPI-encrypted fields ────────────────────────────────────────────────
    public string EncryptedPersonalAccessToken { get; set; } = "";

    internal void LoadSecretsFromEncrypted()
    {
        if (!string.IsNullOrEmpty(EncryptedPersonalAccessToken))
            PersonalAccessToken = DpapiSettingsProtector.Unprotect(EncryptedPersonalAccessToken);
    }

    internal GitHubActionsSettings CreateEncryptedCopy() => new()
    {
        Enabled = Enabled, Owner = Owner, Repository = Repository,
        TimeoutSeconds = TimeoutSeconds,
        PersonalAccessToken = "",
        EncryptedPersonalAccessToken = DpapiSettingsProtector.Protect(PersonalAccessToken),
    };
}

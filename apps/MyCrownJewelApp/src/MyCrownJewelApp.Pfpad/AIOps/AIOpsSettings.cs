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
}

public class AzureMonitorSettings
{
    public bool Enabled { get; set; } = false;
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string ResourceGroup { get; set; } = "";
    public string WorkspaceId { get; set; } = "";
    public string AppInsightsKey { get; set; } = "";
}

public class AzureDevOpsSettings
{
    public bool Enabled { get; set; } = false;
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    public string PersonalAccessToken { get; set; } = "";
}

public class KubernetesSettings
{
    public bool Enabled { get; set; } = false;
    public string ApiServerUrl { get; set; } = "https://localhost:6443";
    public string BearerToken { get; set; } = "";
    public string Namespace { get; set; } = "default";
    public bool SkipTlsVerify { get; set; } = false;
}

public class PrometheusSettings
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "http://localhost:9090";
    public string? BearerToken { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

public class PagerDutySettings
{
    public bool Enabled { get; set; } = false;
    public string ApiToken { get; set; } = "";
    public string? ServiceId { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

public class GitHubActionsSettings
{
    public bool Enabled { get; set; } = false;
    public string Owner { get; set; } = "";
    public string Repository { get; set; } = "";
    public string PersonalAccessToken { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
}

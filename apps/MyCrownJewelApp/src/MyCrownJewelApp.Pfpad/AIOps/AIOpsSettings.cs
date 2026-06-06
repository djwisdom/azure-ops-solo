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

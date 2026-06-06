using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class PolicyValidator
{
    public Task<IReadOnlyList<PolicyViolation>> ValidateAsync(string filePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string fileName = Path.GetFileName(filePath);
        var violations = new List<PolicyViolation>();

        if (extension is ".tf" or ".bicep" or ".yaml" or ".yml")
        {
            if ((Regex.IsMatch(content, @"\bresource\b|resource ""|kind:", RegexOptions.IgnoreCase) || extension is ".tf" or ".bicep")
                && (!Regex.IsMatch(content, @"environment", RegexOptions.IgnoreCase) || !Regex.IsMatch(content, @"owner", RegexOptions.IgnoreCase)))
            {
                violations.Add(CreateViolation("POL-001", "Required tags", "Resources should include environment and owner tags or labels.", Severity.Medium, filePath, FindLine(content, "environment"), "Add environment and owner tags/labels to every provisioned resource.", "https://learn.microsoft.com/azure/azure-resource-manager/management/tag-resources", content));
            }
        }

        if (extension is ".tf" or ".bicep")
        {
            if (Regex.IsMatch(content, @"publicIPAllocationMethod\s*=\s*'Dynamic'|allocation_method\s*=\s*""Dynamic""", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(content, @"networkSecurityGroup|azurerm_network_security_group|nsg", RegexOptions.IgnoreCase))
            {
                violations.Add(CreateViolation("POL-002", "No public exposure", "Dynamic public IP detected without evidence of a network security group.", Severity.High, filePath, FindLine(content, "publicIPAllocationMethod"), "Attach an NSG or remove the public exposure before deployment.", "https://learn.microsoft.com/azure/virtual-network/network-security-groups-overview", content));
            }

            if ((Regex.IsMatch(content, @"storage", RegexOptions.IgnoreCase) || Regex.IsMatch(content, @"StorageAccount", RegexOptions.IgnoreCase))
                && Regex.IsMatch(content, @"encrypt|encryption", RegexOptions.IgnoreCase) == false)
            {
                violations.Add(CreateViolation("POL-003", "Encryption at rest", "Storage resource does not appear to enable encryption at rest.", Severity.High, filePath, 1, "Enable platform-managed or customer-managed encryption for storage resources.", "https://learn.microsoft.com/azure/storage/common/storage-service-encryption", content));
            }

            if (Regex.IsMatch(content, @"TLS1_0|TLS1_1|minTlsVersion\s*=\s*'1\.0'|minimum_tls_version\s*=\s*""TLS1_0""", RegexOptions.IgnoreCase))
            {
                violations.Add(CreateViolation("POL-004", "TLS minimum 1.2", "Legacy TLS version detected.", Severity.High, filePath, FindLine(content, "TLS1_0"), "Set the minimum TLS version to 1.2 or higher.", "https://learn.microsoft.com/azure/storage/common/transport-layer-security-configure-minimum-version", content));
            }
        }

        if (fileName.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(content, @"^\s*USER\s+root\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline))
        {
            violations.Add(CreateViolation("POL-005", "No root containers", "Dockerfile explicitly runs as root.", Severity.High, filePath, FindLine(content, "USER root"), "Run the container as a non-root user and set file permissions explicitly.", "https://docs.docker.com/develop/develop-images/dockerfile_best-practices/", content));
        }

        if (extension is ".yaml" or ".yml")
        {
            if (Regex.IsMatch(content, @"kind:\s*(Deployment|StatefulSet|DaemonSet|Pod)", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(content, @"limits:\s*[\s\S]*cpu:[\s\S]*memory:", RegexOptions.IgnoreCase))
            {
                violations.Add(CreateViolation("POL-006", "Resource limits", "Kubernetes workload is missing cpu/memory limits.", Severity.Medium, filePath, FindLine(content, "resources"), "Define requests and limits for cpu and memory on every container.", "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/", content));
            }

            foreach (Match match in Regex.Matches(content, @"image:\s*(?<image>[^\s]+)", RegexOptions.IgnoreCase))
            {
                string image = match.Groups["image"].Value;
                if (!IsApprovedRegistry(image))
                {
                    violations.Add(CreateViolation("POL-007", "Approved image registries", $"Image '{image}' is not from an approved registry.", Severity.Medium, filePath, FindLine(content, match.Value), "Use images from approved registries such as mcr.microsoft.com, ghcr.io, or your private ACR.", "https://kubernetes.io/docs/concepts/containers/images/", match.Value));
                }
            }

            if (Regex.IsMatch(content, @"kind:\s*ClusterRole", RegexOptions.IgnoreCase)
                && Regex.IsMatch(content, @"verbs:\s*\[[^\]]*\*[^\]]*\]|verbs:[\s\S]*-\s*\*", RegexOptions.IgnoreCase)
                && Regex.IsMatch(content, @"resources:\s*\[[^\]]*\*[^\]]*\]|resources:[\s\S]*-\s*\*", RegexOptions.IgnoreCase))
            {
                violations.Add(CreateViolation("POL-008", "No wildcard RBAC", "ClusterRole grants wildcard verbs and resources.", Severity.High, filePath, FindLine(content, "kind: ClusterRole"), "Scope RBAC rules to explicit verbs and resource kinds instead of '*'.", "https://kubernetes.io/docs/reference/access-authn-authz/rbac/", content));
            }
        }

        return Task.FromResult<IReadOnlyList<PolicyViolation>>(violations
            .GroupBy(v => $"{v.PolicyId}:{v.FilePath}:{v.LineNumber}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList());
    }

    private static bool IsApprovedRegistry(string image)
    {
        string normalized = image.Trim();
        return normalized.StartsWith("mcr.microsoft.com/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("ghcr.io/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("myregistry.azurecr.io/", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains('/');
    }

    private static PolicyViolation CreateViolation(string id, string name, string description, Severity severity, string filePath, int lineNumber, string remediation, string docsUrl, string snippet)
        => new()
        {
            PolicyId = id,
            PolicyName = name,
            Description = description,
            Severity = severity,
            FilePath = filePath,
            LineNumber = lineNumber,
            Remediation = remediation,
            PolicyDocUrl = docsUrl,
            ConfidenceScore = 0.8,
            Evidence = [new Evidence("Policy validation", DataRedactor.Redact(snippet), docsUrl, DateTimeOffset.UtcNow)]
        };

    private static int FindLine(string content, string fragment)
    {
        int index = content.IndexOf(fragment, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return 1;

        int line = 1;
        for (int i = 0; i < index; i++)
        {
            if (content[i] == '\n')
                line++;
        }

        return line;
    }
}

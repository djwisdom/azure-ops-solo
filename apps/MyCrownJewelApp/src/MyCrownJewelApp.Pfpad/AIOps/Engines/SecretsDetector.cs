using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class SecretsDetector
{
    private readonly record struct SecretRule(string Type, Regex Pattern, Severity Severity, string Remediation);

    // Regex instances compiled once; NonBacktracking = O(N) DFA, prevents ReDoS in security scanning.
    private static readonly IReadOnlyList<SecretRule> Rules =
    [
        new("AWS-ACCESS-KEY",
            new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.Critical, "Rotate the AWS access key immediately and move credentials to an IAM role, AWS Secrets Manager, or environment-based secret provider."),
        new("AWS-SECRET-KEY",
            new Regex(@"aws_secret_access_key['""\s:=]+[A-Za-z0-9/+=]{32,}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.Critical, "Rotate the AWS secret access key and load it from a secure secret store instead of source code."),
        new("AZURE-SAS",
            new Regex(@"sv=\d{4}-\d{2}-\d{2}[^\s'""` ]*sig=", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.Critical, "Revoke the SAS token and replace it with managed identity or a newly generated short-lived SAS from secure configuration."),
        new("AZURE-CONNECTION-STRING",
            new Regex(@"DefaultEndpointsProtocol=https;AccountName=[^;]+;AccountKey=[^;]+", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.High, "Move the Azure connection string to Azure Key Vault, user secrets, or environment variables and rotate the account key."),
        new("GITHUB-TOKEN",
            new Regex(@"ghp_[A-Za-z0-9]{36}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.Critical, "Revoke the GitHub token and replace it with a new token stored in a secure credential provider."),
        new("API-KEY",
            new Regex(@"api[_-]?key['""\s:=]+[A-Za-z0-9]{20,}", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.High, "Move the API key to secure configuration and rotate the exposed credential."),
        new("PRIVATE-KEY",
            new Regex(@"-----BEGIN [A-Z ]+ PRIVATE KEY-----", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.Critical, "Remove the private key from source control, rotate it, and store it in a managed secret store or certificate vault."),
        new("BASIC-AUTH-URL",
            new Regex(@"https?://[^:\s]+:[^@\s]+@", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking),
            Severity.High, "Do not embed credentials in URLs. Use secure headers, credential providers, or managed identities instead."),
    ];

    public ValueTask<IReadOnlyList<SecurityFinding>> ScanAsync(string filePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var findings = new List<SecurityFinding>();

        foreach (var rule in Rules)
        {
            foreach (Match match in rule.Pattern.Matches(content))
            {
                ct.ThrowIfCancellationRequested();
                findings.Add(new SecurityFinding
                {
                    RuleId = $"SECRET-{rule.Type}",
                    Title = $"Exposed secret detected: {rule.Type}",
                    Description = $"Potential secret material matched the {rule.Type} detector.",
                    Severity = rule.Severity,
                    Category = SecurityCategory.Secret,
                    FilePath = filePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    CodeSnippet = DataRedactor.Redact(GetSnippet(content, match.Index)),
                    Remediation = rule.Remediation,
                    ConfidenceScore = 0.9,
                    Evidence = [new Evidence("Secrets scan", $"Matched {rule.Type} secret pattern", null, DateTimeOffset.UtcNow)]
                });
            }
        }

        IReadOnlyList<SecurityFinding> result = findings
            .GroupBy(f => $"{f.RuleId}:{f.FilePath}:{f.LineNumber}:{f.CodeSnippet}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        return ValueTask.FromResult(result);
    }

    private static string GetSnippet(string content, int index)
    {
        int start = content.LastIndexOf('\n', Math.Max(0, index - 1));
        int end = content.IndexOf('\n', index);
        start = start < 0 ? 0 : start + 1;
        end = end < 0 ? content.Length : end;
        return content[start..end].Trim();
    }

    private static int GetLineNumber(string content, int index)
        => content.AsSpan(0, Math.Min(index, content.Length)).Count('\n') + 1;
}

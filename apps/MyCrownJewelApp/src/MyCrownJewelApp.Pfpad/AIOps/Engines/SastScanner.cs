using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class SastScanner
{
    private readonly record struct Rule(string RuleId, string Title, string Pattern, Severity Severity, SecurityCategory Category, IReadOnlySet<string> Languages, string Description, string Remediation);

    private static readonly IReadOnlyList<Rule> Rules =
    [
        new("CWE-89", "SQL Injection", @"(string\.Format|StringFormat|Execute|Query).*\+.*", Severity.High, SecurityCategory.Injection, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs", "sql" }, "Dynamic SQL appears to be assembled with string concatenation.", "Use parameterized queries or ORM parameters instead of concatenating SQL input."),
        new("CWE-78", "Command Injection", @"(Process\.Start|cmd\.exe|shell_exec|os\.system).*(\+|\$\{|Request\.|Console\.ReadLine)", Severity.High, SecurityCategory.Injection, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs", "py", "sh" }, "Shell execution may include untrusted input.", "Use strict allow-lists and avoid passing user input directly into shell commands."),
        new("CWE-22", "Path Traversal", @"(Path\.Combine.*Request\.|\.\./)", Severity.Medium, SecurityCategory.Injection, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs" }, "Potential path traversal pattern detected.", "Normalize and validate paths against an allow-listed base directory."),
        new("CWE-338", "Insecure Random", @"(new Random\(\)|random\.Next)", Severity.Medium, SecurityCategory.Insecure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs" }, "Non-cryptographic random source used in a potentially sensitive context.", "Use RandomNumberGenerator for secrets, tokens, and security decisions."),
        new("CWE-327", "Weak Crypto", @"(MD5\.Create|SHA1\.Create|DES\.Create|RC2\.)", Severity.High, SecurityCategory.Insecure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs" }, "Weak or obsolete cryptographic primitive detected.", "Use SHA-256 or stronger hashes and modern authenticated encryption primitives."),
        new("CWE-798", "Hardcoded Credentials", @"(password\s*=\s*""[^""]+""|apikey\s*=\s*""[^""]+"")", Severity.Critical, SecurityCategory.Secret, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "all" }, "Hardcoded secret material found in source.", "Move credentials to secure configuration such as Azure Key Vault, user secrets, or environment variables."),
        new("CWE-601", "Open Redirect", @"Response\.Redirect.*Request\.", Severity.Medium, SecurityCategory.Injection, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs" }, "Redirect target appears to come from the request.", "Validate redirect targets against trusted hosts or use fixed routes."),
        new("CWE-476", "Null Deref Risk", @"(FirstOrDefault\(\)\.[A-Za-z_]|SingleOrDefault\(\)\.[A-Za-z_]|[A-Za-z_][A-Za-z0-9_]*!\.)", Severity.Low, SecurityCategory.Insecure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs" }, "Code may dereference a value that can be null.", "Add null checks or safe navigation operators before dereferencing."),
        new("SEC-K8S-001", "Privileged Container", @"privileged:\s*true", Severity.High, SecurityCategory.Infrastructure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "yaml", "yml" }, "Kubernetes container is configured as privileged.", "Run containers as non-privileged and grant only the capabilities that are required."),
        new("SEC-K8S-002", "Latest Image Tag", @"image:\s*.+:latest", Severity.Medium, SecurityCategory.Infrastructure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "yaml", "yml" }, "Container image uses the latest tag.", "Pin images to immutable version or digest references."),
        new("SEC-TF-001", "Public Storage", @"(public_access\s*=\s*true|allow_blob_public_access)", Severity.High, SecurityCategory.Infrastructure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tf" }, "Storage account appears to allow public access.", "Disable public access and restrict access with private endpoints or firewall rules."),
        new("SEC-TF-002", "No HTTPS", @"https_only\s*=\s*false", Severity.High, SecurityCategory.Infrastructure, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tf" }, "HTTPS-only enforcement appears disabled.", "Require HTTPS/TLS for ingress and storage endpoints."),
    ];

    public Task<IReadOnlyList<SecurityFinding>> ScanAsync(string filePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string language = DetectLanguage(filePath);
        var findings = new List<SecurityFinding>();

        foreach (var rule in Rules)
        {
            if (!rule.Languages.Contains("all") && !rule.Languages.Contains(language))
                continue;

            var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
            foreach (Match match in regex.Matches(content))
            {
                ct.ThrowIfCancellationRequested();
                if (!match.Success)
                    continue;
                if (rule.RuleId == "CWE-89" && !LooksLikeSql(match.Value, content))
                    continue;
                if (rule.RuleId == "CWE-338" && !LooksSecuritySensitive(match.Index, content))
                    continue;

                findings.Add(new SecurityFinding
                {
                    RuleId = rule.RuleId,
                    Title = rule.Title,
                    Description = rule.Description,
                    Severity = rule.Severity,
                    Category = rule.Category,
                    FilePath = filePath,
                    LineNumber = GetLineNumber(content, match.Index),
                    CodeSnippet = DataRedactor.Redact(GetSnippet(content, match.Index)),
                    Remediation = rule.Remediation,
                    ConfidenceScore = 0.7,
                    Evidence = [new Evidence("Pattern scan", $"Matched {rule.RuleId} in {Path.GetFileName(filePath)}", null, DateTimeOffset.UtcNow)]
                });
            }
        }

        return Task.FromResult<IReadOnlyList<SecurityFinding>>(Deduplicate(findings));
    }

    private static IReadOnlyList<SecurityFinding> Deduplicate(IEnumerable<SecurityFinding> findings)
        => findings.GroupBy(f => $"{f.RuleId}:{f.FilePath}:{f.LineNumber}:{f.CodeSnippet}", StringComparer.Ordinal).Select(g => g.First()).ToList();

    private static string DetectLanguage(string filePath)
        => Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant() switch
        {
            "cs" => "cs",
            "sql" => "sql",
            "py" => "py",
            "sh" => "sh",
            "yaml" => "yaml",
            "yml" => "yml",
            "tf" => "tf",
            _ => "all"
        };

    private static bool LooksLikeSql(string matchValue, string content)
    {
        string window = GetSnippet(content, Math.Max(0, content.IndexOf(matchValue, StringComparison.Ordinal)));
        return Regex.IsMatch(window, @"\b(select|insert|update|delete|from|where)\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksSecuritySensitive(int index, string content)
    {
        string window = GetWindow(content, index, 120);
        return Regex.IsMatch(window, @"(token|secret|password|auth|session|nonce|key)", RegexOptions.IgnoreCase);
    }

    private static string GetSnippet(string content, int index)
    {
        int start = content.LastIndexOf('\n', Math.Max(0, index - 1));
        int end = content.IndexOf('\n', index);
        start = start < 0 ? 0 : start + 1;
        end = end < 0 ? content.Length : end;
        return content[start..end].Trim();
    }

    private static string GetWindow(string content, int index, int radius)
    {
        int start = Math.Max(0, index - radius);
        int length = Math.Min(content.Length - start, radius * 2);
        return content.Substring(start, length);
    }

    private static int GetLineNumber(string content, int index)
    {
        int line = 1;
        for (int i = 0; i < Math.Min(index, content.Length); i++)
        {
            if (content[i] == '\n')
                line++;
        }

        return line;
    }
}

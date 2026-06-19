using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed partial class SastScanner
{
    // Shared FrozenSet instances for language filtering — allocated once, lookup O(1).
    private static readonly FrozenSet<string> _langAll  = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "all"                  }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> _langCs   = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs"                   }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> _langCsSql= new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cs", "sql"            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> _langCsPySh= new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "cs", "py", "sh"       }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> _langYaml = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "yaml", "yml"          }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenSet<string> _langTf   = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tf"                   }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly record struct Rule(
        string RuleId, string Title, Regex Pattern,
        Severity Severity, SecurityCategory Category,
        FrozenSet<string> Languages, string Description, string Remediation);

    // Regex compiled once with NonBacktracking (DFA — prevents ReDoS; O(N) match time).
    private static readonly IReadOnlyList<Rule> Rules =
    [
        new("CWE-89",  "SQL Injection",         new Regex(@"(string\.Format|StringFormat|Execute|Query).*\+.*",                          RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Injection,      _langCsSql,  "Dynamic SQL appears to be assembled with string concatenation.",                           "Use parameterized queries or ORM parameters instead of concatenating SQL input."),
        new("CWE-78",  "Command Injection",      new Regex(@"(Process\.Start|cmd\.exe|shell_exec|os\.system).*(\+|\$\{|Request\.|Console\.ReadLine)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Injection,      _langCsPySh, "Shell execution may include untrusted input.",                                             "Use strict allow-lists and avoid passing user input directly into shell commands."),
        new("CWE-22",  "Path Traversal",         new Regex(@"(Path\.Combine.*Request\.|\.\./)",                                          RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Medium,   SecurityCategory.Injection,      _langCs,     "Potential path traversal pattern detected.",                                               "Normalize and validate paths against an allow-listed base directory."),
        new("CWE-338", "Insecure Random",        new Regex(@"(new Random\(\)|random\.Next)",                                             RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Medium,   SecurityCategory.Insecure,       _langCs,     "Non-cryptographic random source used in a potentially sensitive context.",                 "Use RandomNumberGenerator for secrets, tokens, and security decisions."),
        new("CWE-327", "Weak Crypto",            new Regex(@"(MD5\.Create|SHA1\.Create|DES\.Create|RC2\.)",                              RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Insecure,       _langCs,     "Weak or obsolete cryptographic primitive detected.",                                       "Use SHA-256 or stronger hashes and modern authenticated encryption primitives."),
        new("CWE-798", "Hardcoded Credentials",  new Regex(@"(password\s*=\s*""[^""]+""|apikey\s*=\s*""[^""]+"")",                      RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Critical, SecurityCategory.Secret,         _langAll,    "Hardcoded secret material found in source.",                                               "Move credentials to secure configuration such as Azure Key Vault, user secrets, or environment variables."),
        new("CWE-601", "Open Redirect",          new Regex(@"Response\.Redirect.*Request\.",                                             RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Medium,   SecurityCategory.Injection,      _langCs,     "Redirect target appears to come from the request.",                                       "Validate redirect targets against trusted hosts or use fixed routes."),
        new("CWE-476", "Null Deref Risk",        new Regex(@"(FirstOrDefault\(\)\.[A-Za-z_]|SingleOrDefault\(\)\.[A-Za-z_]|[A-Za-z_][A-Za-z0-9_]*!\.)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Low,      SecurityCategory.Insecure,       _langCs,     "Code may dereference a value that can be null.",                                           "Add null checks or safe navigation operators before dereferencing."),
        new("SEC-K8S-001", "Privileged Container",new Regex(@"privileged:\s*true",                                                       RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Infrastructure, _langYaml,   "Kubernetes container is configured as privileged.",                                        "Run containers as non-privileged and grant only the capabilities that are required."),
        new("SEC-K8S-002", "Latest Image Tag",   new Regex(@"image:\s*.+:latest",                                                        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.Medium,   SecurityCategory.Infrastructure, _langYaml,   "Container image uses the latest tag.",                                                     "Pin images to immutable version or digest references."),
        new("SEC-TF-001", "Public Storage",      new Regex(@"(public_access\s*=\s*true|allow_blob_public_access)",                       RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Infrastructure, _langTf,     "Storage account appears to allow public access.",                                         "Disable public access and restrict access with private endpoints or firewall rules."),
        new("SEC-TF-002", "No HTTPS",            new Regex(@"https_only\s*=\s*false",                                                    RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.NonBacktracking), Severity.High,     SecurityCategory.Infrastructure, _langTf,     "HTTPS-only enforcement appears disabled.",                                                 "Require HTTPS/TLS for ingress and storage endpoints."),
    ];

    [GeneratedRegex(@"\b(select|insert|update|delete|from|where)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SqlKeywordsRegex();

    [GeneratedRegex(@"(token|secret|password|auth|session|nonce|key)", RegexOptions.IgnoreCase)]
    private static partial Regex SecuritySensitiveRegex();

    public ValueTask<IReadOnlyList<SecurityFinding>> ScanAsync(string filePath, string content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string language = DetectLanguage(filePath);
        var findings = new List<SecurityFinding>();

        foreach (var rule in Rules)
        {
            if (!rule.Languages.Contains("all") && !rule.Languages.Contains(language))
                continue;

            foreach (Match match in rule.Pattern.Matches(content))
            {
                ct.ThrowIfCancellationRequested();
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

        return ValueTask.FromResult<IReadOnlyList<SecurityFinding>>(Deduplicate(findings));
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
        return SqlKeywordsRegex().IsMatch(window);
    }

    private static bool LooksSecuritySensitive(int index, string content)
    {
        string window = GetWindow(content, index, 120);
        return SecuritySensitiveRegex().IsMatch(window);
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
        => content.AsSpan(0, Math.Min(index, content.Length)).Count('\n') + 1;
}

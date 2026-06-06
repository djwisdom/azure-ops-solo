using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>Redacts secrets and PII from strings before display.</summary>
public static class DataRedactor
{
    private static readonly (Regex pattern, string replacement)[] Rules =
    [
        (new Regex(@"(?i)(password|pwd|secret|token|apikey|api_key|sas|sig)\s*[=:]\s*\S+", RegexOptions.Compiled), "$1=[REDACTED]"),
        (new Regex(@"sig=[A-Za-z0-9%/+=]{10,}", RegexOptions.Compiled), "sig=[REDACTED]"),
        (new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled), "[AWS-KEY-REDACTED]"),
        (new Regex(@"(?i)Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled), "Bearer [REDACTED]"),
        (new Regex(@"-----BEGIN [A-Z ]+PRIVATE KEY-----[\s\S]+?-----END [A-Z ]+PRIVATE KEY-----", RegexOptions.Compiled), "[PRIVATE-KEY-REDACTED]"),
        (new Regex(@"(?<=[=:'""])[A-Za-z0-9+/]{40,}={0,2}(?=['""\s,}])", RegexOptions.Compiled), "[SECRET-REDACTED]"),
    ];

    public static string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        string result = input;
        foreach (var (pattern, replacement) in Rules)
            result = pattern.Replace(result, replacement);

        return result;
    }
}

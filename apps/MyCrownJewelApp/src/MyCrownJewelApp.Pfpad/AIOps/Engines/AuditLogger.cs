using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class AuditLogger
{
    private readonly string _logFilePath;
    private readonly Lock _sync = new();

    public AuditLogger(string? logDirectory = null)
    {
        string baseDirectory = logDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pfpad", "aiops-audit");
        Directory.CreateDirectory(baseDirectory);
        _logFilePath = Path.Combine(baseDirectory, $"aiops-audit-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void LogRecommendation(string query, OpsQueryResponse response, string? userId = null)
        => WriteEntry(new
        {
            type = "recommendation",
            timestamp = DateTimeOffset.UtcNow,
            userId = Redact(userId),
            query = Redact(query),
            answer = Redact(response.Answer),
            confidence = response.ConfidenceScore,
            isGuess = response.IsGuess,
            disclaimer = Redact(response.Disclaimer),
            evidence = response.Evidence.Select(e => new { source = Redact(e.Source), description = Redact(e.Description), url = Redact(e.Url), timestamp = e.Timestamp }),
            recommendations = response.Recommendations.Select(r => new { title = Redact(r.Title), description = Redact(r.Description), risk = r.Risk.ToString(), filePath = Redact(r.FilePath) })
        });

    public void LogSecurityFinding(SecurityFinding finding)
        => WriteEntry(new
        {
            type = "security_finding",
            timestamp = DateTimeOffset.UtcNow,
            ruleId = Redact(finding.RuleId),
            title = Redact(finding.Title),
            description = Redact(finding.Description),
            severity = finding.Severity.ToString(),
            category = finding.Category.ToString(),
            filePath = Redact(finding.FilePath),
            lineNumber = finding.LineNumber,
            remediation = Redact(finding.Remediation)
        });

    public void LogProductionAction(string action, RemediationRisk risk, bool approved, string? userId = null)
        => WriteEntry(new
        {
            type = "production_action",
            timestamp = DateTimeOffset.UtcNow,
            userId = Redact(userId),
            action = Redact(action),
            risk = risk.ToString(),
            approved
        });

    public void LogRunbookExecution(RunbookExecutionResult result)
        => WriteEntry(new
        {
            type = "runbook_execution",
            timestamp = DateTimeOffset.UtcNow,
            runbookId = Redact(result.RunbookId),
            result.Success,
            error = Redact(result.Error),
            executedSteps = result.ExecutedSteps.Select(Redact),
            result.RequiredApproval,
            result.ApprovalGranted,
            result.ExecutedAt
        });

    private void WriteEntry(object entry)
    {
        string line = DataRedactor.Redact(JsonSerializer.Serialize(entry));
        lock (_sync)
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
    }

    private static string? Redact(string? value)
        => value is null ? null : DataRedactor.Redact(value);
}

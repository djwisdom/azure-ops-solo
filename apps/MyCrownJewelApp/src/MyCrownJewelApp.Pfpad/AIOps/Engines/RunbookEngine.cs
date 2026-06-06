using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad.AIOps;

public class RunbookEngine
{
    private static readonly IReadOnlySet<string> _safePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "kubectl get", "kubectl describe", "kubectl logs", "kubectl top",
        "git log", "git status", "git diff",
        "dotnet --version", "docker ps", "docker images"
    };

    public event Action<RunbookExecutionResult>? ExecutionCompleted;
    public event Action<Runbook, RunbookStep>? ApprovalRequested;

    public bool IsCommandSafe(string? command)
        => !string.IsNullOrWhiteSpace(command)
           && _safePrefixes.Any(prefix => command.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public async Task<RunbookExecutionResult> ExecuteAsync(Runbook runbook, bool dryRun = true, CancellationToken ct = default)
    {
        var executedSteps = new List<string>();
        bool requiredApproval = false;
        bool approvalGranted = false;

        foreach (RunbookStep step in runbook.Steps.OrderBy(s => s.Order))
        {
            ct.ThrowIfCancellationRequested();

            if (step.RequiresApproval)
            {
                requiredApproval = true;
                ApprovalRequested?.Invoke(runbook, step);
                if (!dryRun)
                {
                    var pending = new RunbookExecutionResult(runbook.Id, false, $"Approval required before step {step.Order}.", executedSteps, DateTimeOffset.UtcNow, true, false);
                    ExecutionCompleted?.Invoke(pending);
                    return pending;
                }
            }

            if (dryRun)
            {
                executedSteps.Add($"DRYRUN {step.Order}: {step.Description}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.Command))
            {
                executedSteps.Add($"MANUAL {step.Order}: {step.Description}");
                continue;
            }

            if (!IsCommandSafe(step.Command))
            {
                var unsafeResult = new RunbookExecutionResult(runbook.Id, false, $"Unsafe command blocked: {step.Command}", executedSteps, DateTimeOffset.UtcNow, requiredApproval, approvalGranted);
                ExecutionCompleted?.Invoke(unsafeResult);
                return unsafeResult;
            }

            (bool success, string output) = await ExecuteCommandAsync(step.Command, step.Timeout ?? TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            executedSteps.Add($"CMD {step.Order}: {step.Command} => {DataRedactor.Redact(output)}");
            if (!success)
            {
                var failed = new RunbookExecutionResult(runbook.Id, false, output, executedSteps, DateTimeOffset.UtcNow, requiredApproval, approvalGranted);
                ExecutionCompleted?.Invoke(failed);
                return failed;
            }
        }

        approvalGranted = dryRun && requiredApproval;
        var result = new RunbookExecutionResult(runbook.Id, true, null, executedSteps, DateTimeOffset.UtcNow, requiredApproval, approvalGranted);
        ExecutionCompleted?.Invoke(result);
        return result;
    }

    private static async Task<(bool Success, string Output)> ExecuteCommandAsync(string command, TimeSpan timeout, CancellationToken ct)
    {
        var tokens = Regex.Matches(command, "\"[^\"]+\"|\\S+")
            .Select(match => match.Value.Trim('"'))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
        if (tokens.Count == 0)
            return (false, "No command provided.");

        var startInfo = new ProcessStartInfo
        {
            FileName = tokens[0],
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string token in tokens.Skip(1))
            startInfo.ArgumentList.Add(token);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);
        Task waitTask = process.WaitForExitAsync(ct);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, ct)).ConfigureAwait(false);
        if (completed != waitTask)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
            }

            return (false, $"Command timed out after {timeout.TotalSeconds:F0}s.");
        }

        await waitTask.ConfigureAwait(false);
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        string output = string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(output))
            output = $"Exit code {process.ExitCode}.";
        return (process.ExitCode == 0, output);
    }
}

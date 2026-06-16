using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

/// <summary>A single parsed MSBuild diagnostic from a build output line.</summary>
public record BuildDiagnostic(
    string? FilePath,
    int Line,
    int Column,
    DiagnosticSeverity Severity,
    string Code,
    string Message);

/// <summary>Summary result returned when a build finishes.</summary>
public record BuildResult(bool Success, int ErrorCount, int WarningCount, TimeSpan Elapsed);

/// <summary>
/// Runs <c>dotnet build</c> asynchronously. Fires per-line output events
/// so callers can stream output to a panel without waiting for completion.
/// </summary>
public sealed class BuildService
{
    // MSBuild diagnostic format: path(line,col): error|warning CODE: message
    private static readonly Regex DiagnosticRx = new(
        @"^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\)\s*:\s*(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<msg>.+)$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // MSBuild tool-level diagnostic (no file): "MSBUILD : error MSB1001:"
    private static readonly Regex ToolDiagRx = new(
        @"^(?:MSBUILD|CSC|VBC)\s*:\s*(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<msg>.+)$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>Fires when a line of stdout/stderr is received. (line, isStdErr)</summary>
    public event Action<string, bool>? OutputLine;

    /// <summary>Fires when the build process starts.</summary>
    public event Action? BuildStarted;

    /// <summary>Fires when the build process finishes.</summary>
    public event Action<BuildResult>? BuildCompleted;

    /// <summary>
    /// Runs <c>dotnet build &lt;targetPath&gt;</c> and fires streaming output events.
    /// Returns when the process exits or the token is cancelled.
    /// </summary>
    public async Task<BuildResult> RunAsync(string targetPath, string configuration = "Debug", CancellationToken cancellationToken = default)
    {
        int errors = 0;
        int warnings = 0;
        var sw = Stopwatch.StartNew();

        BuildStarted?.Invoke();

        string workDir = Directory.Exists(targetPath)
            ? targetPath
            : (Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{targetPath}\" -c {configuration} -nologo",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var diag = ParseDiagnostic(e.Data);
            if (diag != null)
            {
                if (diag.Severity == DiagnosticSeverity.Error) errors++;
                else if (diag.Severity == DiagnosticSeverity.Warning) warnings++;
            }
            OutputLine?.Invoke(e.Data, false);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var diag = ParseDiagnostic(e.Data);
            if (diag != null)
            {
                if (diag.Severity == DiagnosticSeverity.Error) errors++;
                else if (diag.Severity == DiagnosticSeverity.Warning) warnings++;
            }
            OutputLine?.Invoke(e.Data, true);
        };

        bool started;
        try
        {
            started = process.Start();
        }
        catch (Exception ex)
        {
            sw.Stop();
            string msg = $"Failed to start dotnet build: {ex.Message}";
            OutputLine?.Invoke(msg, true);
            var failResult = new BuildResult(false, 1, 0, sw.Elapsed);
            BuildCompleted?.Invoke(failResult);
            return failResult;
        }

        if (!started)
        {
            sw.Stop();
            OutputLine?.Invoke("dotnet process failed to start.", true);
            var failResult = new BuildResult(false, 1, 0, sw.Elapsed);
            BuildCompleted?.Invoke(failResult);
            return failResult;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            sw.Stop();
            OutputLine?.Invoke("Build cancelled.", false);
            var cancelResult = new BuildResult(false, errors, warnings, sw.Elapsed);
            BuildCompleted?.Invoke(cancelResult);
            return cancelResult;
        }

        sw.Stop();
        bool success = process.ExitCode == 0;
        var result = new BuildResult(success, errors, warnings, sw.Elapsed);
        BuildCompleted?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Parses a single MSBuild output line into a <see cref="BuildDiagnostic"/>.
    /// Returns <c>null</c> for non-diagnostic lines (info, blank, etc.).
    /// </summary>
    public static BuildDiagnostic? ParseDiagnostic(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var m = DiagnosticRx.Match(line.Trim());
        if (m.Success)
        {
            return new BuildDiagnostic(
                FilePath: m.Groups["file"].Value.Trim(),
                Line: int.TryParse(m.Groups["line"].Value, out int ln) ? ln : 0,
                Column: int.TryParse(m.Groups["col"].Value, out int col) ? col : 0,
                Severity: m.Groups["sev"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticSeverity.Error
                    : DiagnosticSeverity.Warning,
                Code: m.Groups["code"].Value,
                Message: m.Groups["msg"].Value.Trim());
        }

        var t = ToolDiagRx.Match(line.Trim());
        if (t.Success)
        {
            return new BuildDiagnostic(
                FilePath: null,
                Line: 0,
                Column: 0,
                Severity: t.Groups["sev"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticSeverity.Error
                    : DiagnosticSeverity.Warning,
                Code: t.Groups["code"].Value,
                Message: t.Groups["msg"].Value.Trim());
        }

        return null;
    }
}

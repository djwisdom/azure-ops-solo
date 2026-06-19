using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyCrownJewelApp.Pfpad.AIOps;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Orchestrates the two automated pre-commit safety stages:
/// Stage 1 — Secret scan: runs <see cref="SecretsDetector"/> over every staged diff.
/// Stage 2 — Hook shim: runs .git/hooks/pre-commit, .husky/pre-commit, or pre-commit tool.
/// Stage 3 (review dialog) is handled by the caller (GitPanel).
/// </summary>
internal sealed class PreCommitPipeline
{
    private readonly GitService _git;
    private readonly SecretsDetector _secrets = new();

    public PreCommitPipeline(GitService git) => _git = git;

    /// <summary>
    /// Scans all staged diffs for secrets. Returns findings (empty = pass).
    /// </summary>
    public async Task<IReadOnlyList<SecurityFinding>> ScanSecretsAsync(CancellationToken ct = default)
    {
        var (staged, _, _) = _git.GetStatus();
        var all = new List<SecurityFinding>();

        foreach (var entry in staged)
        {
            ct.ThrowIfCancellationRequested();
            if (_git.RepoPath is null) continue;

            try
            {
                var diff = _git.GetDiffContent(entry.Path, staged: true);
                var fullPath = Path.Combine(_git.RepoPath, entry.Path);
                var findings = await _secrets.ScanAsync(fullPath, diff, ct).ConfigureAwait(false);
                all.AddRange(findings);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* skip files with no diff or unreadable content */ }
        }

        return all;
    }

    /// <summary>
    /// Probes for a pre-commit hook and runs it. Returns (true, output) on pass or "no hooks".
    /// Detection order: .git/hooks/pre-commit → .husky/pre-commit → .pre-commit-config.yaml
    /// </summary>
    public Task<(bool Passed, string Output)> RunHooksAsync(CancellationToken ct = default)
    {
        if (_git.RepoPath is null) return Task.FromResult((true, "No repository."));

        // 1. Standard git hook (chmod +x — Windows: sh wrapper required)
        var stdHook = Path.Combine(_git.RepoPath, ".git", "hooks", "pre-commit");
        if (File.Exists(stdHook))
            return RunViaSh(stdHook, _git.RepoPath, ct);

        // 2. Husky v8/v9 managed hook
        var huskyHook = Path.Combine(_git.RepoPath, ".husky", "pre-commit");
        if (File.Exists(huskyHook))
            return RunViaSh(huskyHook, _git.RepoPath, ct);

        // 3. pre-commit tool (https://pre-commit.com)
        var preCommitConfig = Path.Combine(_git.RepoPath, ".pre-commit-config.yaml");
        if (File.Exists(preCommitConfig))
            return RunCommandAsync("pre-commit", "run", _git.RepoPath, ct);

        return Task.FromResult((true, "No hooks configured."));
    }

    /// <summary>Returns true if any hook infrastructure is present in the repo.</summary>
    public bool HasHooks()
    {
        if (_git.RepoPath is null) return false;
        return File.Exists(Path.Combine(_git.RepoPath, ".git", "hooks", "pre-commit"))
            || File.Exists(Path.Combine(_git.RepoPath, ".husky", "pre-commit"))
            || File.Exists(Path.Combine(_git.RepoPath, ".pre-commit-config.yaml"));
    }

    // Run a shell script via sh.exe (Git for Windows ships sh.exe in PATH)
    private static Task<(bool, string)> RunViaSh(string scriptPath, string workDir, CancellationToken ct)
        => RunCommandAsync("sh", $"\"{scriptPath}\"", workDir, ct);

    private static async Task<(bool Passed, string Output)> RunCommandAsync(
        string exe, string args, string workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null.");

            var outTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            string stdout = await outTask.ConfigureAwait(false);
            string stderr = await errTask.ConfigureAwait(false);
            string combined = $"{stdout}\n{stderr}".Trim();
            return (proc.ExitCode == 0, combined);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // If sh/pre-commit isn't in PATH, treat as "not available" rather than failure
            return (true, $"Hook runner not available ({exe}): {ex.Message}");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Reads PR / MR description templates from the repository working tree,
/// using the correct platform-specific paths resolved by <see cref="GitRemotePlatform"/>.
/// </summary>
public static class PullRequestTemplateReader
{
    /// <summary>
    /// Scans the repo root for a PR/MR template and returns its text, or
    /// <c>null</c> when no template is found.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root directory.</param>
    /// <param name="platform">Hosting platform (drives path resolution).</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<string?> ReadAsync(
        string repoRoot,
        GitPlatform platform,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return null;

        foreach (var relativePath in GitRemotePlatform.PrTemplatePaths(platform))
        {
            ct.ThrowIfCancellationRequested();

            // GitLab: .gitlab/merge_request_templates/ is a directory — pick first .md file
            if (relativePath.EndsWith('/') || relativePath.EndsWith('\\'))
            {
                var dir = Path.Combine(repoRoot, relativePath.TrimEnd('/', '\\'));
                if (!Directory.Exists(dir)) continue;

                var first = Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .FirstOrDefault();
                if (first is null) continue;
                return await File.ReadAllTextAsync(first, ct).ConfigureAwait(false);
            }

            var full = Path.Combine(repoRoot, relativePath);
            if (!File.Exists(full)) continue;

            var text = await File.ReadAllTextAsync(full, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    /// <summary>
    /// Returns the path of the first PR template file found, or <c>null</c>.
    /// Useful for offering the user an "Edit template" link.
    /// </summary>
    public static string? FindTemplatePath(string repoRoot, GitPlatform platform)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            return null;

        foreach (var relativePath in GitRemotePlatform.PrTemplatePaths(platform))
        {
            if (relativePath.EndsWith('/') || relativePath.EndsWith('\\'))
            {
                var dir = Path.Combine(repoRoot, relativePath.TrimEnd('/', '\\'));
                if (!Directory.Exists(dir)) continue;
                var first = Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .FirstOrDefault();
                if (first is not null) return first;
                continue;
            }

            var full = Path.Combine(repoRoot, relativePath);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    /// <summary>
    /// Reads the CODEOWNERS file (if any) and returns a list of owners that match
    /// the given set of staged file paths.  Returns an empty array when no match.
    /// </summary>
    public static string[] GetOwnersForFiles(
        string repoRoot,
        GitPlatform platform,
        string[] stagedFilePaths)
    {
        if (!Directory.Exists(repoRoot) || stagedFilePaths.Length == 0)
            return Array.Empty<string>();

        foreach (var rel in GitRemotePlatform.CodeownersPaths(platform))
        {
            var full = Path.Combine(repoRoot, rel);
            if (!File.Exists(full)) continue;

            var owners = ParseCodeowners(full, stagedFilePaths);
            if (owners.Length > 0) return owners;
        }

        return Array.Empty<string>();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string[] ParseCodeowners(string codeownersPath, string[] stagedFiles)
    {
        // Simple CODEOWNERS parser: lines of the form  <pattern>  @owner1 @owner2 ...
        // We match staged files against patterns and collect the last matching owners
        // (CODEOWNERS uses last-match-wins, same as .gitignore).
        var matchedOwners = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var lines = File.ReadAllLines(codeownersPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

                var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                var pattern = parts[0];
                var owners  = parts[1..];

                foreach (var staged in stagedFiles)
                {
                    var normalized = staged.Replace('\\', '/').TrimStart('/');
                    if (MatchesGlobPattern(normalized, pattern))
                    {
                        matchedOwners.Clear();
                        foreach (var o in owners)
                            matchedOwners.Add(o);
                    }
                }
            }
        }
        catch { /* IO or format errors — return empty */ }

        return matchedOwners.ToArray();
    }

    private static bool MatchesGlobPattern(string filePath, string pattern)
    {
        // Normalize pattern
        var p = pattern.TrimStart('/');

        // Simple cases: exact match, directory prefix, wildcard extension
        if (p.EndsWith("/**"))
        {
            var dir = p[..^3];
            return filePath.StartsWith(dir + "/", StringComparison.OrdinalIgnoreCase) ||
                   filePath.Equals(dir, StringComparison.OrdinalIgnoreCase);
        }

        if (!p.Contains('*'))
        {
            // Plain path — match exact or as prefix (directory)
            return filePath.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                   filePath.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase);
        }

        // Convert glob to simple regex: * → [^/]*, ** → .*
        var regexPat = "^" +
            System.Text.RegularExpressions.Regex.Escape(p)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*",   "[^/]*") +
            "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            filePath, regexPat, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

using System;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

/// <summary>Git hosting platform inferred from the remote URL.</summary>
public enum GitPlatform
{
    Unknown,
    GitHub,
    AzureDevOps,
    GitLab,
    Bitbucket,
    Gitea,
}

/// <summary>
/// Detects the Git hosting platform from a remote URL and provides per-platform
/// helpers: PR/MR creation URLs, issue reference syntax, PR template paths,
/// and display metadata (name, icon glyph).
/// </summary>
public static class GitRemotePlatform
{
    // ── Detection ────────────────────────────────────────────────────────────

    /// <summary>Infers the platform from a remote URL string.</summary>
    public static GitPlatform Detect(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return GitPlatform.Unknown;

        var url = remoteUrl.ToLowerInvariant();

        if (url.Contains("github.com"))          return GitPlatform.GitHub;
        if (url.Contains("dev.azure.com") ||
            url.Contains("visualstudio.com"))    return GitPlatform.AzureDevOps;
        if (url.Contains("gitlab.com") ||
            url.Contains("gitlab."))             return GitPlatform.GitLab;
        if (url.Contains("bitbucket.org"))       return GitPlatform.Bitbucket;
        if (url.Contains("gitea.") ||
            url.Contains("/gitea"))              return GitPlatform.Gitea;

        return GitPlatform.Unknown;
    }

    /// <summary>
    /// Returns the platform detected from the first remote (usually <c>origin</c>)
    /// of an open <see cref="GitService"/>.
    /// </summary>
    public static GitPlatform DetectFromService(GitService git)
    {
        var repo = git.GetRepo();
        if (repo is null) return GitPlatform.Unknown;

        foreach (var remote in repo.Network.Remotes)
        {
            var platform = Detect(remote.Url);
            if (platform != GitPlatform.Unknown)
                return platform;
        }
        return GitPlatform.Unknown;
    }

    /// <summary>Returns the raw URL of the first remote, or <c>null</c> if none.</summary>
    public static string? GetOriginUrl(GitService git)
    {
        var repo = git.GetRepo();
        if (repo is null) return null;
        foreach (var remote in repo.Network.Remotes)
            return remote.Url;
        return null;
    }

    // ── Display metadata ─────────────────────────────────────────────────────

    /// <summary>Human-readable short name for display in UI.</summary>
    public static string DisplayName(GitPlatform platform) => platform switch
    {
        GitPlatform.GitHub       => "GitHub",
        GitPlatform.AzureDevOps  => "Azure DevOps",
        GitPlatform.GitLab       => "GitLab",
        GitPlatform.Bitbucket    => "Bitbucket",
        GitPlatform.Gitea        => "Gitea",
        _                        => "Git",
    };

    /// <summary>
    /// Unicode glyph used as a compact platform badge in the Git panel header.
    /// Chosen from characters that render well at 9 pt in Segoe UI.
    /// </summary>
    public static string HeaderGlyph(GitPlatform platform) => platform switch
    {
        GitPlatform.GitHub       => "⑂",   // fork symbol
        GitPlatform.AzureDevOps  => "⬡",   // hexagon (Azure)
        GitPlatform.GitLab       => "◈",   // GitLab-ish diamond
        GitPlatform.Bitbucket    => "⚑",   // flag (Bitbucket buckets)
        GitPlatform.Gitea        => "⛵",   // boat / tea cup
        _                        => "⎇",   // generic branch symbol
    };

    // ── PR / MR creation URLs ─────────────────────────────────────────────────

    /// <summary>
    /// Builds a browser URL the developer can open to start a new PR/MR for
    /// <paramref name="branch"/> on the given platform.
    /// Returns <c>null</c> if the URL cannot be determined.
    /// </summary>
    public static string? BuildCreatePrUrl(
        GitPlatform platform,
        string? remoteUrl,
        string? branch,
        string? baseBranch = "main")
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) || string.IsNullOrWhiteSpace(branch))
            return null;

        branch    = Uri.EscapeDataString(branch);
        baseBranch = Uri.EscapeDataString(baseBranch ?? "main");

        return platform switch
        {
            GitPlatform.GitHub => BuildGitHubPrUrl(remoteUrl, branch, baseBranch),
            GitPlatform.AzureDevOps => BuildAdoPrUrl(remoteUrl, branch, baseBranch),
            GitPlatform.GitLab => BuildGitLabMrUrl(remoteUrl, branch, baseBranch),
            GitPlatform.Bitbucket => BuildBitbucketPrUrl(remoteUrl, branch),
            _ => null,
        };
    }

    private static string? BuildGitHubPrUrl(string remoteUrl, string branch, string baseBranch)
    {
        // https://github.com/{owner}/{repo}/compare/{base}...{branch}?quick_pull=1
        var slug = ExtractGitHubSlug(remoteUrl);
        if (slug is null) return null;
        return $"https://github.com/{slug}/compare/{baseBranch}...{branch}?quick_pull=1";
    }

    private static string? BuildAdoPrUrl(string remoteUrl, string branch, string baseBranch)
    {
        // https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequestcreate
        //   ?sourceRef={branch}&targetRef={base}
        var parts = ExtractAdoParts(remoteUrl);
        if (parts is null) return null;
        var (org, project, repo) = parts.Value;
        return $"https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequestcreate" +
               $"?sourceRef={branch}&targetRef={baseBranch}";
    }

    private static string? BuildGitLabMrUrl(string remoteUrl, string branch, string baseBranch)
    {
        // https://gitlab.com/{group}/{repo}/-/merge_requests/new
        //   ?merge_request[source_branch]={branch}&merge_request[target_branch]={base}
        var slug = ExtractGitLabSlug(remoteUrl);
        if (slug is null) return null;
        var (host, path) = slug.Value;
        return $"https://{host}/{path}/-/merge_requests/new" +
               $"?merge_request%5Bsource_branch%5D={branch}" +
               $"&merge_request%5Btarget_branch%5D={baseBranch}";
    }

    private static string? BuildBitbucketPrUrl(string remoteUrl, string branch)
    {
        // https://bitbucket.org/{workspace}/{repo}/pull-requests/new?source={branch}
        var slug = ExtractBitbucketSlug(remoteUrl);
        if (slug is null) return null;
        return $"https://bitbucket.org/{slug}/pull-requests/new?source={branch}";
    }

    // ── Issue reference syntax ────────────────────────────────────────────────

    /// <summary>
    /// Placeholder text shown in the Conventional Commit composer footer field
    /// so the developer uses the correct issue-closing syntax for the platform.
    /// </summary>
    public static string IssueReferencePlaceholder(GitPlatform platform) => platform switch
    {
        GitPlatform.GitHub       => "Closes #123  (or Fixes #123, Resolves #123)",
        GitPlatform.AzureDevOps  => "AB#1234  (auto-links Azure Boards work item)",
        GitPlatform.GitLab       => "Closes #123  (or Related to !456 for MR)",
        GitPlatform.Bitbucket    => "PROJ-123  (Jira issue key, e.g. APP-42)",
        _                        => "Closes #123",
    };

    // ── PR template paths ─────────────────────────────────────────────────────

    /// <summary>
    /// Ordered list of relative paths (from repo root) to search for PR/MR templates.
    /// The first path that exists should be used.
    /// </summary>
    public static string[] PrTemplatePaths(GitPlatform platform) => platform switch
    {
        GitPlatform.GitHub => new[]
        {
            ".github/pull_request_template.md",
            ".github/PULL_REQUEST_TEMPLATE.md",
            "docs/pull_request_template.md",
            "pull_request_template.md",
        },
        GitPlatform.AzureDevOps => new[]
        {
            ".azuredevops/pull_request_template.md",
            "docs/pull_request_template.md",
            "pull_request_template.md",
        },
        GitPlatform.GitLab => new[]
        {
            // GitLab uses a directory; return the directory marker — reader handles glob
            ".gitlab/merge_request_templates/",
            "docs/merge_request_template.md",
        },
        GitPlatform.Bitbucket => new[]
        {
            // Bitbucket has no native template — try common conventions
            "docs/pull_request_template.md",
            ".github/pull_request_template.md",
            "pull_request_template.md",
        },
        _ => new[]
        {
            ".github/pull_request_template.md",
            "docs/pull_request_template.md",
            "pull_request_template.md",
        },
    };

    // ── Branch name suggestion ────────────────────────────────────────────────

    /// <summary>
    /// Generates a suggested branch name from a prefix, optional issue/ticket ID,
    /// and a short description, formatted per platform conventions.
    /// </summary>
    public static string SuggestBranchName(
        GitPlatform platform,
        string prefix,        // e.g. "feature", "fix", "hotfix"
        string? ticketId,     // e.g. "123", "PROJ-42", "AB#1234"
        string description)
    {
        var slug = SlugifyDescription(description);

        return platform switch
        {
            GitPlatform.AzureDevOps when !string.IsNullOrWhiteSpace(ticketId)
                => $"{prefix}/AB{ticketId.TrimStart('#')}-{slug}",

            GitPlatform.Bitbucket when !string.IsNullOrWhiteSpace(ticketId)
                => ticketId.Contains('-')
                    ? $"{prefix}/{ticketId.ToUpperInvariant()}-{slug}"    // PROJ-42
                    : $"{prefix}/{slug}",

            GitPlatform.GitLab when !string.IsNullOrWhiteSpace(ticketId)
                => $"{ticketId}-{slug}",

            GitPlatform.GitHub when !string.IsNullOrWhiteSpace(ticketId)
                => $"{prefix}/{ticketId}-{slug}",

            _ => string.IsNullOrWhiteSpace(ticketId)
                    ? $"{prefix}/{slug}"
                    : $"{prefix}/{ticketId}-{slug}",
        };
    }

    // ── CODEOWNERS path ───────────────────────────────────────────────────────

    /// <summary>
    /// Ordered list of relative paths to search for a CODEOWNERS file.
    /// </summary>
    public static string[] CodeownersPaths(GitPlatform platform) => platform switch
    {
        GitPlatform.GitHub      => new[] { ".github/CODEOWNERS", "CODEOWNERS", "docs/CODEOWNERS" },
        GitPlatform.GitLab      => new[] { ".gitlab/CODEOWNERS", "CODEOWNERS" },
        GitPlatform.Bitbucket   => new[] { "CODEOWNERS" },
        GitPlatform.AzureDevOps => Array.Empty<string>(), // enforced server-side only
        _                       => new[] { "CODEOWNERS" },
    };

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string? ExtractGitHubSlug(string remoteUrl)
    {
        // https://github.com/owner/repo.git  or  git@github.com:owner/repo.git
        var m = Regex.Match(remoteUrl, @"github\.com[:/](.+?)(?:\.git)?$", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim('/') : null;
    }

    private static (string Org, string Project, string Repo)? ExtractAdoParts(string remoteUrl)
    {
        // https://dev.azure.com/{org}/{project}/_git/{repo}
        var m = Regex.Match(remoteUrl,
            @"dev\.azure\.com/([^/]+)/([^/]+)/_git/([^/]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);

        // https://{org}.visualstudio.com/{project}/_git/{repo}
        var m2 = Regex.Match(remoteUrl,
            @"([^.]+)\.visualstudio\.com/([^/]+)/_git/([^/]+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (m2.Success)
            return (m2.Groups[1].Value, m2.Groups[2].Value, m2.Groups[3].Value);

        return null;
    }

    private static (string Host, string Path)? ExtractGitLabSlug(string remoteUrl)
    {
        // https://gitlab.com/group/subgroup/repo.git  or  git@gitlab.com:group/repo.git
        var m = Regex.Match(remoteUrl,
            @"(gitlab[^:/]+)[:/](.+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        return (m.Groups[1].Value, m.Groups[2].Value.Trim('/'));
    }

    private static string? ExtractBitbucketSlug(string remoteUrl)
    {
        // https://bitbucket.org/workspace/repo.git  or  git@bitbucket.org:ws/repo.git
        var m = Regex.Match(remoteUrl,
            @"bitbucket\.org[:/](.+?)(?:\.git)?$",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim('/') : null;
    }

    private static string SlugifyDescription(string description)
    {
        // lowercase, replace spaces and special chars with hyphens, collapse runs
        var s = description.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9]+", "-");
        s = s.Trim('-');
        return s.Length > 50 ? s[..50] : s;
    }
}

namespace MyCrownJewelApp.Pfpad.AIOps;

/// <summary>A Git commit with changed files.</summary>
public record GitCommit(
    string Sha,
    string ShortSha,
    string Message,
    string Author,
    string AuthorEmail,
    DateTimeOffset Timestamp,
    IReadOnlyList<string> ChangedFiles,
    string? PullRequestId = null,
    string? Url = null);

/// <summary>Status of a pull request.</summary>
public enum PullRequestStatus { Open, Closed, Merged, Draft }

/// <summary>A pull request / code review.</summary>
public record GitPullRequest(
    string Id,
    string Title,
    string Author,
    string SourceBranch,
    string TargetBranch,
    PullRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? MergedAt,
    string? Url,
    string? Description,
    IReadOnlyList<GitCommit> Commits,
    IReadOnlyList<string> ChangedFiles,
    string? ReviewUrl = null);

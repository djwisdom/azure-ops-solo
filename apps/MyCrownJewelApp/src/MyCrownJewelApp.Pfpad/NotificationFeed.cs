using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyCrownJewelApp.Pfpad;

public enum FeedSource
{
    HackerNews,
    Reddit,
    GitHubReleases,
    Medium,
    StackOverflow,
    BBCNews,
    Reuters,
    NYT,
    Custom
}

public sealed class FeedItem
{
    public string Id { get; init; } = "";
    public FeedSource Source { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public Uri? Link { get; init; }
    public DateTime Published { get; init; }
    public bool IsRead { get; set; }

    [JsonIgnore]
    public string SourceLabel => Source switch
    {
        FeedSource.HackerNews => "Hacker News",
        FeedSource.Reddit => "Reddit",
        FeedSource.GitHubReleases => "GitHub Releases",
        FeedSource.Medium => "Medium",
        FeedSource.StackOverflow => "Stack Overflow",
        FeedSource.BBCNews => "BBC News",
        FeedSource.Reuters => "Reuters",
        FeedSource.NYT => "NYT",
        FeedSource.Custom => "Custom",
        _ => Source.ToString()
    };

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - Published;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return Published.ToString("MMM d");
        }
    }
}

public sealed class FeedSourceConfig
{
    public FeedSource Source { get; set; } = FeedSource.Custom;
    public string Url { get; set; } = "";
    public string Label { get; set; } = "";
    public int MaxItems { get; set; } = 10;
    public int PollIntervalMinutes { get; set; } = 15;
    public bool Enabled { get; set; } = true;

    [JsonIgnore]
    public string DisplayLabel => !string.IsNullOrEmpty(Label) ? Label : SourceLabel;

    [JsonIgnore]
    public string SourceLabel => Source switch
    {
        FeedSource.HackerNews => "Hacker News",
        FeedSource.Reddit => "Reddit",
        FeedSource.GitHubReleases => "GitHub Releases",
        FeedSource.Medium => "Medium",
        FeedSource.StackOverflow => "Stack Overflow",
        FeedSource.BBCNews => "BBC News",
        FeedSource.Reuters => "Reuters",
        FeedSource.NYT => "NYT",
        FeedSource.Custom => "Custom",
        _ => Source.ToString()
    };
}

public static class DefaultFeedSources
{
    public static List<FeedSourceConfig> GetDefaults() => new()
    {
        new() { Source = FeedSource.HackerNews, Url = "https://hnrss.org/frontpage", Label = "Hacker News", MaxItems = 15, PollIntervalMinutes = 10, Enabled = true },
        new() { Source = FeedSource.Reddit, Url = "https://www.reddit.com/r/programming/.rss", Label = "Reddit r/programming", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
        new() { Source = FeedSource.GitHubReleases, Url = "https://github.com/dotnet/runtime/releases.atom", Label = "dotnet/runtime releases", MaxItems = 5, PollIntervalMinutes = 30, Enabled = true },
        new() { Source = FeedSource.Medium, Url = "https://medium.com/feed/tag/programming", Label = "Medium programming", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
        new() { Source = FeedSource.StackOverflow, Url = "https://stackoverflow.com/feeds/tag/c%23", Label = "Stack Overflow C#", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
        new() { Source = FeedSource.BBCNews, Url = "https://feeds.bbci.co.uk/news/rss.xml", Label = "BBC News", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
        new() { Source = FeedSource.Reuters, Url = "https://www.reutersagency.com/feed/", Label = "Reuters", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
        new() { Source = FeedSource.NYT, Url = "https://rss.nytimes.com/services/xml/rss/nyt/HomePage.xml", Label = "NYT HomePage", MaxItems = 10, PollIntervalMinutes = 15, Enabled = true },
    };
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

public sealed class NotificationFeedService : IDisposable
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<FeedSource, List<FeedItem>> _items = new();
    private readonly ConcurrentDictionary<string, bool> _seenIds = new();
    private readonly string _statePath;
    private readonly string _configPath;
    private CancellationTokenSource? _pollCts;
    private bool _disposed;

    private List<FeedSourceConfig> _sources = DefaultFeedSources.GetDefaults();

    public event Action? OnItemsUpdated;

    public IReadOnlyList<FeedItem> AllItems =>
        _items.Values.SelectMany(x => x).OrderByDescending(i => i.Published).ToList();

    public int UnreadCount => AllItems.Count(i => !i.IsRead);

    public IReadOnlyList<FeedSourceConfig> Sources => _sources.AsReadOnly();

    public NotificationFeedService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalFlipPad/1.0");
        _http.Timeout = TimeSpan.FromSeconds(15);

        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp",
            "Notifications");

        _statePath = Path.Combine(baseDir, "feed_state.json");
        _configPath = Path.Combine(baseDir, "feed_config.json");

        LoadConfig();
        LoadState();
    }

    public void UpdateSources(List<FeedSourceConfig> sources)
    {
        _sources = sources;
        SaveConfig();
        OnItemsUpdated?.Invoke();
    }

    public void StartPolling()
    {
        if (_pollCts is not null) return;
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await FetchAllAsync(ct);
            var minInterval = _sources.Where(s => s.Enabled).Select(s => s.PollIntervalMinutes).DefaultIfEmpty(15).Min();
            try { await Task.Delay(TimeSpan.FromMinutes(minInterval), ct); } catch { break; }
        }
    }

    public async Task FetchAllAsync(CancellationToken ct = default)
    {
        var enabled = _sources.Where(s => s.Enabled).ToList();
        var tasks = enabled.Select(cfg => FetchSourceAsync(cfg, ct));
        await Task.WhenAll(tasks);
        SaveState();
        OnItemsUpdated?.Invoke();
    }

    private async Task FetchSourceAsync(FeedSourceConfig cfg, CancellationToken ct)
    {
        try
        {
            var xml = await _http.GetStringAsync(cfg.Url, ct);
            var items = ParseFeed(cfg.Source, xml, cfg.MaxItems);
            _items[cfg.Source] = items;
        }
        catch { }
    }

    private List<FeedItem> ParseFeed(FeedSource source, string xml, int maxItems)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;

            if (root?.Name?.LocalName == "feed")
                return ParseAtom(root, source, maxItems);
            if (root?.Name?.LocalName == "rss")
                return ParseRss(root, source, maxItems);
        }
        catch { }

        return new List<FeedItem>();
    }

    private List<FeedItem> ParseRss(XElement rss, FeedSource source, int maxItems)
    {
        return rss.Descendants("item")
            .Take(maxItems)
            .Select(item =>
            {
                var title = item.Element("title")?.Value ?? "(no title)";
                var link = item.Element("link")?.Value;
                var desc = item.Element("description")?.Value ?? "";
                var pubDateStr = item.Element("pubDate")?.Value ?? item.Element("dc:date")?.Value;
                var pubDate = TryParseDate(pubDateStr);
                var id = item.Element("guid")?.Value ?? link ?? title;

                return new FeedItem
                {
                    Id = id,
                    Source = source,
                    Title = StripHtml(title),
                    Summary = StripHtml(desc).Truncate(200),
                    Link = !string.IsNullOrEmpty(link) && Uri.TryCreate(link, UriKind.Absolute, out var u) ? u : null,
                    Published = pubDate,
                    IsRead = _seenIds.ContainsKey(id)
                };
            })
            .Where(i => i.Link is not null)
            .ToList();
    }

    private List<FeedItem> ParseAtom(XElement feed, FeedSource source, int maxItems)
    {
        var ns = feed.GetDefaultNamespace();
        return feed.Descendants(ns + "entry")
            .Take(maxItems)
            .Select(entry =>
            {
                var title = entry.Element(ns + "title")?.Value ?? "(no title)";
                var linkEl = entry.Element(ns + "link");
                var link = linkEl?.Attribute("href")?.Value;
                var summary = entry.Element(ns + "summary")?.Value
                    ?? entry.Element(ns + "content")?.Value ?? "";
                var pubDateStr = entry.Element(ns + "published")?.Value
                    ?? entry.Element(ns + "updated")?.Value;
                var pubDate = TryParseDate(pubDateStr);
                var id = entry.Element(ns + "id")?.Value ?? link ?? title;

                return new FeedItem
                {
                    Id = id,
                    Source = source,
                    Title = StripHtml(title),
                    Summary = StripHtml(summary).Truncate(200),
                    Link = !string.IsNullOrEmpty(link) && Uri.TryCreate(link, UriKind.Absolute, out var u) ? u : null,
                    Published = pubDate,
                    IsRead = _seenIds.ContainsKey(id)
                };
            })
            .Where(i => i.Link is not null)
            .ToList();
    }

    private static DateTime TryParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return DateTime.UtcNow;
        if (DateTime.TryParse(s, out var dt)) return dt.ToUniversalTime();
        if (DateTime.TryParseExact(s, "ddd, dd MMM yyyy HH:mm:ss zzz",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
            return dt.ToUniversalTime();
        return DateTime.UtcNow;
    }

    public void MarkAsRead(FeedItem item)
    {
        item.IsRead = true;
        _seenIds.TryAdd(item.Id, true);
        OnItemsUpdated?.Invoke();
        SaveState();
    }

    public void MarkAllAsRead()
    {
        foreach (var item in AllItems)
        {
            item.IsRead = true;
            _seenIds.TryAdd(item.Id, true);
        }
        OnItemsUpdated?.Invoke();
        SaveState();
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<List<FeedSourceConfig>>(json);
                if (loaded is { Count: > 0 })
                {
                    _sources = loaded;
                    return;
                }
            }
        }
        catch { }
        _sources = DefaultFeedSources.GetDefaults();
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_sources, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                var json = File.ReadAllText(_statePath);
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids is not null)
                    foreach (var id in ids)
                        _seenIds.TryAdd(id, true);
            }
        }
        catch { }
    }

    private void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(_statePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var ids = _seenIds.Keys.ToList();
            File.WriteAllText(_statePath, JsonSerializer.Serialize(ids));
        }
        catch { }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        result = System.Net.WebUtility.HtmlDecode(result);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopPolling();
        _http.Dispose();
        SaveState();
        SaveConfig();
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";
}

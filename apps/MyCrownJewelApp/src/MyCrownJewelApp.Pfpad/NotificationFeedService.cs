using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

public sealed class NotificationFeedService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<FeedSource, List<FeedItem>> _items   = new();
    private readonly ConcurrentDictionary<string, bool>               _seenIds = new();
    private readonly ConcurrentDictionary<FeedSource, string?>  _sourceErrors   = new();
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private readonly string _statePath;
    private readonly string _configPath;
    private CancellationTokenSource? _pollCts;
    private PeriodicTimer?  _pollTimer;
    private Task?           _pollTask;
    private bool _disposed;

    private List<FeedSourceConfig> _sources = DefaultFeedSources.GetDefaults();

    /// <summary>Raised after each source resolves. Subscribers must marshal to the UI thread themselves.</summary>
    public event Action? OnItemsUpdated;

    public void AddNotification(string title, string summary)
    {
        var id   = $"app-{Guid.NewGuid():N}";
        var item = new FeedItem
        {
            Id        = id,
            Source    = FeedSource.Custom,
            Title     = title,
            Summary   = summary,
            Published = DateTime.UtcNow,
            IsRead    = false
        };
        _seenIds.TryAdd(id, false);
        var list = _items.GetOrAdd(FeedSource.Custom, _ => new List<FeedItem>());
        lock (list) { list.Add(item); }     // List<T> is not thread-safe
        FireItemsUpdated();
    }

    public IReadOnlyList<FeedItem> AllItems =>
        _items.Values.SelectMany(x => x).OrderByDescending(i => i.Published).ToList();

    public int UnreadCount => AllItems.Count(i => !i.IsRead);

    public IReadOnlyList<FeedSourceConfig> Sources => _sources.AsReadOnly();

    public string? GetSourceError(FeedSource source) =>
        _sourceErrors.TryGetValue(source, out var err) ? err : null;

    public bool HasErrors => _sourceErrors.Values.Any(e => e is not null);

    /// <summary>True when the background poll loop is alive.</summary>
    public bool IsPolling => _pollTask is { IsCompleted: false };

    public NotificationFeedService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalFlipPad/1.0");
        _http.Timeout = TimeSpan.FromSeconds(15);

        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp", "Notifications");

        _statePath  = Path.Combine(baseDir, "feed_state.json");
        _configPath = Path.Combine(baseDir, "feed_config.json");

        LoadConfig();
        LoadState();
    }

    public void UpdateSources(List<FeedSourceConfig> sources)
    {
        _sources = sources;
        SaveConfig();
        // Restart with the (potentially new) minimum interval
        if (IsPolling) { StopPolling(); StartPolling(); }
        FireItemsUpdated();
    }

    public void StartPolling()
    {
        if (IsPolling) return;          // Guard: check task liveness, not just the CTS

        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();

        int intervalMinutes = _sources
            .Where(s => s.Enabled)
            .Select(s => s.PollIntervalMinutes)
            .DefaultIfEmpty(15)
            .Min();

        _pollTimer?.Dispose();
        _pollTimer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, intervalMinutes)));
        _pollTask  = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    /// <summary>Call periodically (e.g. on window activation) to recover from an unexpected poll-loop crash.</summary>
    public void RestartPollingIfDead()
    {
        if (!IsPolling && _pollTimer is not null)
        {
            Debug.WriteLine("[NotificationFeed] Poll loop was dead — restarting.");
            StopPolling();
            StartPolling();
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        // Immediate first fetch so the UI has data right away
        try
        {
            await FetchAllAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Debug.WriteLine($"[NotificationFeed] Initial fetch error: {ex.Message}"); }

        try
        {
            // PeriodicTimer (.NET 6+): accurate ticking — no drift accumulation, respects cancellation cleanly
            while (await _pollTimer!.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await FetchAllAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    // A single bad fetch must NOT kill the loop — just log and wait for the next tick
                    Debug.WriteLine($"[NotificationFeed] Fetch cycle error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { /* expected on StopPolling() */ }
    }

    /// <summary>
    /// Fetches all enabled sources concurrently. Uses <c>Task.WhenEach</c> (.NET 9+) so
    /// <see cref="OnItemsUpdated"/> fires progressively as each source resolves rather than
    /// waiting for the slowest feed.
    /// </summary>
    public async Task FetchAllAsync(CancellationToken ct = default)
    {
        // Skip if a fetch is already running (e.g. manual refresh during a scheduled poll)
        if (!await _fetchLock.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        try
        {
            var enabled = _sources.Where(s => s.Enabled).ToList();
            if (enabled.Count == 0) return;

            var tasks = enabled.Select(cfg => FetchSourceAsync(cfg, ct)).ToList();

            await foreach (var completedTask in Task.WhenEach(tasks).ConfigureAwait(false))
            {
                try { await completedTask.ConfigureAwait(false); }
                catch { /* errors already recorded inside FetchSourceAsync */ }

                FireItemsUpdated();
            }

            SaveState();
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private void FireItemsUpdated()
    {
        try { OnItemsUpdated?.Invoke(); }
        catch (Exception ex)
        {
            // A misbehaving subscriber must not propagate exceptions into the poll loop
            Debug.WriteLine($"[NotificationFeed] OnItemsUpdated subscriber threw: {ex.Message}");
        }
    }

    private async Task FetchSourceAsync(FeedSourceConfig cfg, CancellationToken ct)
    {
        try
        {
            if (!IsValidFeedUrl(cfg.Url))
            {
                _sourceErrors[cfg.Source] = "Invalid or disallowed feed URL.";
                return;
            }

            var xml   = await _http.GetStringAsync(cfg.Url, ct).ConfigureAwait(false);
            var items = ParseFeed(cfg.Source, xml, cfg.MaxItems);
            _items[cfg.Source] = items;
            _sourceErrors[cfg.Source] = null; // clear any previous error
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception ex)
        {
            _sourceErrors[cfg.Source] = ex.Message;
        }
    }

    private static bool IsValidFeedUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Only allow HTTPS
        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Allow only reputable domains for feeds
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "github.com",
            "news.ycombinator.com",
            "reddit.com",
            "stackoverflow.com",
            "devblogs.microsoft.com",
            "docs.microsoft.com",
            "blog.jetbrains.com",
            "blog.rust-lang.org",
            "blog.golang.org",
            "aws.amazon.com",
            "azure.microsoft.com",
            "cloud.google.com",
            "kubernetes.io",
            "docker.com",
            "techcrunch.com",
            "arstechnica.com",
            "wired.com",
            "theverge.com"
        };

        return allowedHosts.Contains(uri.Host) ||
               uri.Host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith(".amazon.com", StringComparison.OrdinalIgnoreCase);
    }

    private List<FeedItem> ParseFeed(FeedSource source, string xml, int maxItems)
    {
        try
        {
            // Limit XML size to prevent DoS
            if (xml.Length > 5 * 1024 * 1024) // 5MB limit
                return new List<FeedItem>();

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
                var json   = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<List<FeedSourceConfig>>(json, JsonOpts);
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
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_sources, JsonOpts));
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
        _fetchLock.Dispose();
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

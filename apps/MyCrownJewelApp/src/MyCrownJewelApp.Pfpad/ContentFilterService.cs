using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Enterprise-grade content filter for the built-in browser.
/// Downloads, caches, and parses industry-standard ad/tracker blocklists.
/// Provides thread-safe O(1) domain lookups with subdomain walk-up matching.
/// </summary>
internal sealed class ContentFilterService : IDisposable
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static ContentFilterService Instance { get; } = new();

    // ── Blocklist catalogue ────────────────────────────────────────────────────

    public enum BlocklistFormat { EasyList, Hosts }

    /// <summary>Describes one downloadable blocklist.</summary>
    public sealed record BlocklistEntry(
        string Id,
        string Name,
        string Url,
        string FileName,
        BlocklistFormat Format,
        string Description);

    /// <summary>All downloadable blocklists. Order is preserved for display; IDs are the stable keys.</summary>
    public static readonly BlocklistEntry[] KnownLists =
    [
        new("easylist",
            "EasyList",
            "https://easylist.to/easylist/easylist.txt",
            "easylist.txt",
            BlocklistFormat.EasyList,
            "Removes most advertisements from web pages (maintained by the EasyList authors)"),

        new("easyprivacy",
            "EasyPrivacy",
            "https://easylist.to/easylist/easyprivacy.txt",
            "easyprivacy.txt",
            BlocklistFormat.EasyList,
            "Removes analytics, tracking pixels, and cross-site data collectors"),

        new("peterlow",
            "Peter Lowe's Ad List",
            "https://pgl.yoyo.org/adservers/serverlist.php?hostformat=hosts&showintro=0&mimetype=plaintext",
            "peterlow.txt",
            BlocklistFormat.Hosts,
            "Long-standing curated ad-server list in hosts format (~3 000 domains)"),

        new("fanboy-annoyance",
            "Fanboy's Annoyance List",
            "https://easylist.to/easylist/fanboy-annoyance.txt",
            "fanboy-annoyance.txt",
            BlocklistFormat.EasyList,
            "Removes cookie consent banners, newsletter pop-ups, push-notification prompts, and social-share toolbars"),

        new("idontcareaboutcookies",
            "I Don't Care About Cookies",
            "https://raw.githubusercontent.com/OhMyGuus/I-Dont-Care-About-Cookies/master/src/filter.txt",
            "idontcareaboutcookies.txt",
            BlocklistFormat.EasyList,
            "60 000+ site-specific rules that dismiss GDPR / cookie-consent dialogs automatically"),

        new("hagezi-pro",
            "Hagezi Pro",
            "https://raw.githubusercontent.com/hagezi/dns-blocklists/main/hosts/pro.txt",
            "hagezi-pro.txt",
            BlocklistFormat.Hosts,
            "Comprehensive unified list: ads, trackers, telemetry, fake-news, and malware domains (~170 000 entries)"),

        new("urlhaus",
            "URLhaus Malware Hosts",
            "https://malware-filter.gitlab.io/malware-filter/urlhaus-filter-hosts-online.txt",
            "urlhaus.txt",
            BlocklistFormat.Hosts,
            "Live feed from abuse.ch: domains actively distributing malware and ransomware"),
    ];

    // ── Configuration ──────────────────────────────────────────────────────────
    private const int RefreshDays    = 7;       // re-download after N days
    private const int MaxParseLines  = 750_000; // safety cap per file
    private const int HttpTimeoutSec = 30;

    // ── State ──────────────────────────────────────────────────────────────────
    private readonly string _cacheDir;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>Immutable domain set; replaced atomically on each recompile.</summary>
    private volatile HashSet<string>? _blocked;

    private bool _enabled = true;
    /// <summary>IDs of lists currently enabled; replaced atomically by Configure().</summary>
    private volatile HashSet<string> _enabledListIds = new(KnownLists.Select(l => l.Id), StringComparer.OrdinalIgnoreCase);
    private long _sessionBlocked;

    private CancellationTokenSource _cts = new();
    private readonly System.Threading.Timer _dailyTimer;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired (from any thread) every time a request is blocked. Arg = new session total.</summary>
    public event Action<long>? BlockedCountChanged;

    /// <summary>Fired on the thread-pool when the domain set is successfully compiled. Arg = domain count.</summary>
    public event Action<int>? ListsReady;

    /// <summary>Fired when a list download or parse step changes. Suitable for progress UI.</summary>
    public event Action<string>? StatusChanged;

    // ── Public properties ──────────────────────────────────────────────────────
    public bool IsEnabled   => _enabled;
    public bool IsReady     => _blocked != null;
    public int  DomainCount => _blocked?.Count ?? 0;
    public long SessionBlockedCount => Interlocked.Read(ref _sessionBlocked);

    // ── Ctor ───────────────────────────────────────────────────────────────────

    private ContentFilterService()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCrownJewelApp", "Pfpad", "blocklists");

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSec) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MyCrownJewelApp-Pfpad/1.0 (blocklist-updater; +https://github.com/djwisdom/azure-ops-solo)");

        // Initial load on background thread — never blocks the UI thread
        _ = Task.Run(() => LoadCycleAsync(_cts.Token));

        // Daily check; actual download only happens when cache is stale
        _dailyTimer = new System.Threading.Timer(_ =>
        {
            var token = _cts.Token;
            _ = Task.Run(() => LoadCycleAsync(token));
        }, null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Configure which lists are active. Thread-safe; triggers an async recompile if anything changed.
    /// <paramref name="listToggles"/> maps list IDs (from <see cref="KnownLists"/>) to enabled state.
    /// Lists whose IDs are absent default to enabled.
    /// </summary>
    public void Configure(bool filterEnabled, IReadOnlyDictionary<string, bool> listToggles)
    {
        var newEnabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in KnownLists)
        {
            bool on = !listToggles.TryGetValue(list.Id, out bool val) || val;
            if (on) newEnabled.Add(list.Id);
        }

        bool changed = _enabled != filterEnabled || !newEnabled.SetEquals(_enabledListIds);
        _enabled = filterEnabled;
        _enabledListIds = newEnabled;

        if (changed && IsReady)
            _ = Task.Run(() => RecompileAsync(_cts.Token));
    }

    /// <summary>
    /// Returns true if the given URL should be blocked.
    /// Always returns false when not enabled or lists are not yet loaded.
    /// </summary>
    public bool IsBlocked(string url)
    {
        if (!_enabled) return false;
        var blocked = _blocked;
        if (blocked == null || blocked.Count == 0) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        string host = uri.Host.ToLowerInvariant();
        return IsDomainBlocked(host, blocked);
    }

    /// <summary>Records one blocked request and fires <see cref="BlockedCountChanged"/>.</summary>
    public void RecordBlocked()
    {
        long n = Interlocked.Increment(ref _sessionBlocked);
        BlockedCountChanged?.Invoke(n);
    }

    /// <summary>Resets the per-session blocked counter to zero.</summary>
    public void ResetSessionCount()
    {
        Interlocked.Exchange(ref _sessionBlocked, 0);
        BlockedCountChanged?.Invoke(0);
    }

    /// <summary>Forces a fresh download of all enabled lists regardless of cache age, then recompiles.</summary>
    public Task ForceRefreshAsync() => LoadCycleAsync(_cts.Token, force: true);

    /// <summary>Returns the cached file path for the given list index.</summary>
    public string GetCachePath(int listIndex) =>
        Path.Combine(_cacheDir, KnownLists[listIndex].FileName);

    /// <summary>Returns the last-downloaded timestamp for the given list, or null if not cached.</summary>
    public DateTime? GetCacheAge(int listIndex)
    {
        string meta = GetCachePath(listIndex) + ".meta";
        if (!File.Exists(meta)) return null;
        return DateTime.TryParse(File.ReadAllText(meta), out var dt) ? dt : null;
    }

    // ── Core: load cycle ───────────────────────────────────────────────────────

    private async Task LoadCycleAsync(CancellationToken ct, bool force = false)
    {
        if (!await _loadLock.WaitAsync(0, ct)) return; // already running
        try
        {
            await DownloadStaleListsAsync(force, ct).ConfigureAwait(false);
            await RecompileAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[ContentFilter] Load cycle error: {ex.Message}");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    // ── Download ───────────────────────────────────────────────────────────────

    private async Task DownloadStaleListsAsync(bool force, CancellationToken ct)
    {
        Directory.CreateDirectory(_cacheDir);

        for (int i = 0; i < KnownLists.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var list = KnownLists[i];
            string path     = Path.Combine(_cacheDir, list.FileName);
            string metaPath = path + ".meta";

            bool stale = force || !File.Exists(path);
            if (!stale && File.Exists(metaPath))
            {
                if (DateTime.TryParse(File.ReadAllText(metaPath), out var cached))
                    stale = DateTime.UtcNow - cached > TimeSpan.FromDays(RefreshDays);
            }

            if (!stale) continue;

            StatusChanged?.Invoke($"Downloading {list.Name}…");
            try
            {
                using var resp = await _http
                    .GetAsync(list.Url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();

                string tmpPath = path + ".tmp";
                await using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write,
                                 FileShare.None, 65536, useAsync: true))
                    await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);

                File.Move(tmpPath, path, overwrite: true);
                await File.WriteAllTextAsync(metaPath, DateTime.UtcNow.ToString("O"), ct)
                    .ConfigureAwait(false);

                StatusChanged?.Invoke($"{list.Name} updated.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.TraceWarning($"[ContentFilter] Download failed for {list.Name}: {ex.Message}");
                // Fall through: stale or missing cache means this list won't contribute
            }
        }
    }

    // ── Parse & compile ────────────────────────────────────────────────────────

    private async Task RecompileAsync(CancellationToken ct)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < KnownLists.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!_enabledListIds.Contains(KnownLists[i].Id)) continue;

            string path = Path.Combine(_cacheDir, KnownLists[i].FileName);
            if (!File.Exists(path)) continue;

            var format = KnownLists[i].Format;
            string listName = KnownLists[i].Name;
            StatusChanged?.Invoke($"Parsing {listName}…");

            try
            {
                int before = domains.Count;
                await Task.Run(() => ParseIntoSet(path, format, domains), ct).ConfigureAwait(false);
                Trace.TraceInformation($"[ContentFilter] {listName}: +{domains.Count - before} domains");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.TraceWarning($"[ContentFilter] Parse error for {listName}: {ex.Message}");
            }
        }

        // Atomic swap — readers see either the old complete set or the new complete set
        _blocked = domains;
        StatusChanged?.Invoke($"Content filter ready — {domains.Count:N0} domains blocked.");
        ListsReady?.Invoke(domains.Count);
    }

    // ── Parsers ────────────────────────────────────────────────────────────────

    private static void ParseIntoSet(string path, BlocklistFormat format, HashSet<string> domains)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072);
        using var sr = new StreamReader(fs);
        int lines = 0;
        string? line;

        while ((line = sr.ReadLine()) != null && lines < MaxParseLines)
        {
            lines++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? domain = format == BlocklistFormat.Hosts
                ? ParseHostsLine(line)
                : ParseEasyListLine(line);

            if (domain != null && IsValidDomain(domain))
                domains.Add(domain);
        }
    }

    /// <summary>
    /// Parses one line of a hosts-format file.
    /// Recognises: <c>0.0.0.0 domain.tld</c> and <c>127.0.0.1 domain.tld</c>.
    /// </summary>
    private static string? ParseHostsLine(string line)
    {
        if (line[0] == '#') return null;

        ReadOnlySpan<char> span = line.AsSpan().Trim();

        // Inline comment stripping
        int hash = span.IndexOf('#');
        if (hash >= 0) span = span[..hash].TrimEnd();

        // Require "0.0.0.0 <domain>" or "127.0.0.1 <domain>"
        int sp = span.IndexOf(' ');
        if (sp < 0) sp = span.IndexOf('\t');
        if (sp < 0) return null;

        ReadOnlySpan<char> addr = span[..sp];
        if (!addr.SequenceEqual("0.0.0.0") && !addr.SequenceEqual("127.0.0.1")) return null;

        string domain = span[(sp + 1)..].Trim().ToString().ToLowerInvariant();

        // Reject placeholder / loopback entries
        if (domain is "localhost" or "localhost.localdomain" or "local"
            or "broadcasthost" or "0.0.0.0" or "127.0.0.1") return null;

        return string.IsNullOrEmpty(domain) ? null : domain;
    }

    /// <summary>
    /// Parses one line of an EasyList-format file.
    /// Only handles simple domain rules (<c>||domain^</c>) — element-hiding
    /// rules (<c>##</c>), regex, and modifier-heavy rules are intentionally skipped
    /// because they require a full browser extension engine.
    /// </summary>
    private static string? ParseEasyListLine(string line)
    {
        // Skip comments, metadata, exception rules, element hiding, extended CSS
        if (line.Length == 0 || line[0] == '!') return null;
        if (line[0] == '[') return null;          // [Adblock Plus ...]
        if (line.StartsWith("@@")) return null;   // exception/whitelist rule
        if (line.Contains("##") || line.Contains("#@#") || line.Contains("#?#")) return null;

        // Simple domain anchor: ||ads.example.com^  or  ||ads.example.com^$options
        if (!line.StartsWith("||")) return null;

        // Find the anchor terminator
        int caret = line.IndexOf('^', 2);
        if (caret < 0) return null;

        string domain = line[2..caret].ToLowerInvariant();

        // Reject rules with path, wildcard, or port — those need URL-pattern matching
        if (domain.Contains('/') || domain.Contains('*') || domain.Contains('?')
            || domain.Contains(':') || domain.Contains('@')) return null;

        return string.IsNullOrEmpty(domain) ? null : domain;
    }

    // ── Domain matching ────────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether <paramref name="host"/> or any parent domain is in the block set.
    /// E.g. "sub.ads.example.com" matches if "ads.example.com" is blocked.
    /// </summary>
    private static bool IsDomainBlocked(string host, HashSet<string> blocked)
    {
        // Strip port if present (shouldn't happen after Uri.Host but be safe)
        int colon = host.IndexOf(':');
        if (colon >= 0) host = host[..colon];

        if (blocked.Contains(host)) return true;

        // Walk up: sub1.sub2.domain.tld → sub2.domain.tld → domain.tld
        int dot = host.IndexOf('.');
        while (dot >= 0 && dot < host.Length - 1)
        {
            string parent = host[(dot + 1)..];
            if (blocked.Contains(parent)) return true;
            dot = host.IndexOf('.', dot + 1);
        }

        return false;
    }

    // ── Validation ─────────────────────────────────────────────────────────────

    private static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrEmpty(domain) || domain.Length > 253) return false;
        if (!domain.Contains('.')) return false; // must have TLD
        if (domain.StartsWith('-') || domain.EndsWith('-')) return false;

        foreach (char c in domain)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '.')
                return false;
        }
        return true;
    }

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _dailyTimer.Dispose();
        _http.Dispose();
        _loadLock.Dispose();
    }
}

using System;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// YouTube-specific ad blocking: URL-pattern matching and a content script injected
/// via CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync.
///
/// Works alongside the domain-based ContentFilterService — YouTube ads are served
/// from google-owned domains that can't be domain-blocked (they also carry regular
/// content), so this layer uses URL pattern matching and in-page JS auto-skip logic.
/// </summary>
internal static class YouTubeAdBlocker
{
    // ── URL patterns (substring match, lower-cased) ─────────────────────────
    // These endpoints serve or track ads specifically; regular video content
    // never hits them.
    private static readonly string[] _adUrlSubstrings =
    [
        "youtube.com/api/stats/ads",
        "youtube.com/api/stats/qoe?",      // QoE ping that fires only for ad views
        "youtube.com/ptracking",
        "youtube.com/pagead/",
        "youtube.com/get_midroll_info",
        "youtube.com/get_video_info?",      // legacy ad-companion requests
        "youtube.com/youtubei/v1/ad_break",
        "youtube.com/youtubei/v1/log_event?",  // broad event log — targeted by ad-specific body
        "doubleclick.net/pagead/",
        "doubleclick.net/ddm/",
        "googleads.g.doubleclick.net",
        "static.doubleclick.net",
        "googleadservices.com/pagead",
        "googlesyndication.com/pagead",
        "imasdk.googleapis.com/js/sdkloader",  // IMA SDK — serves video ads
    ];

    /// <summary>
    /// Returns true if the request URL is a known YouTube / Google ad serving or
    /// tracking endpoint that should be suppressed.  O(n) — n is small (~15).
    /// </summary>
    public static bool IsAdRequest(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        // Fast pre-check — avoid lower-casing if the URL clearly isn't relevant
        if (!url.Contains("youtube") && !url.Contains("doubleclick") &&
            !url.Contains("googlead") && !url.Contains("imasdk"))
            return false;

        string lower = url.ToLowerInvariant();
        foreach (string pattern in _adUrlSubstrings)
        {
            if (lower.Contains(pattern))
                return true;
        }
        return false;
    }

    // ── Content script ───────────────────────────────────────────────────────
    // Injected once after CoreWebView2 initialises.  Self-guards on youtube.com.
    // Runs before any page script, bypasses page CSP (WebView2 host injection).
    //
    // Strategy:
    //   1. Inject CSS that hides all static ad overlay elements.
    //   2. Poll every 300 ms: if .ad-showing is active on the player, either
    //      click the skip button or rush through at 16x playback speed.
    //      NOTE: we never seek via currentTime on non-skippable ads — YouTube
    //      reuses the same <video> element for ads and the main video, so
    //      seeking to video.duration - 0.1 lands inside the main content and
    //      causes the buffer/progress bar to hang at ~75-80%.
    //   3. A MutationObserver on the player's 'class' attribute detects when
    //      'ad-showing' is removed and restores playbackRate / volume.
    //   4. A second MutationObserver fires skipAd() when new DOM nodes arrive,
    //      catching inline/overlay ads faster than the poll.
    //   5. After ad dismissal a 600 ms stall-recovery check kicks the main
    //      video if it is paused or still buffering.

    public static string ContentScript { get; } = """
(function () {
    'use strict';
    if (!location.hostname.endsWith('youtube.com')) return;

    // ── 1. CSS element hiding ─────────────────────────────────────────────
    var style = document.createElement('style');
    style.id = '__pfpad_yt_adblock';
    style.textContent = [
        '.ad-showing .ytp-ad-module',
        '.ytp-ad-overlay-slot',
        '.ytp-ad-overlay-container',
        '.ytp-ad-text-overlay',
        '.ytp-ad-simple-ad-badge',
        '.ytp-ad-preview-container',
        '.ytp-ad-preview-text-wrapper',
        '.ytp-ad-button-icon',
        'ytd-action-companion-ad-renderer',
        'ytd-promoted-sparkles-web-renderer',
        'ytd-promoted-video-renderer',
        'ytd-player-legacy-desktop-watch-ads-renderer',
        'ytd-banner-promo-renderer',
        'ytd-statement-banner-renderer',
        '.ytd-companion-slot-renderer',
        '#player-ads',
        '#masthead-ad',
        '.ytp-ad-progress-list',
        '.ytp-ad-progress',
        '.ytp-ad-duration-remaining',
        '.ytp-ce-covering-overlay',
        'ytd-ad-slot-renderer',
        'ytd-in-feed-ad-layout-renderer',
        'ytd-display-ad-renderer'
    ].join(',\n') + ' { display:none!important; visibility:hidden!important; }';
    (document.head || document.documentElement).appendChild(style);

    // ── 2. State ──────────────────────────────────────────────────────────
    var _adWasActive = false;
    var _stallTimer  = null;

    // ── 3. Post-ad recovery ───────────────────────────────────────────────
    // Called once when 'ad-showing' is removed from the player class list.
    // Restores all properties we modified and kicks buffering if stalled.
    function onAdDismissed(video) {
        clearTimeout(_stallTimer);
        video.playbackRate = 1;
        video.muted        = false;
        video.volume       = 1;

        // Give the player 600 ms to start the main video on its own;
        // if it is still paused or under-buffered, nudge it.
        _stallTimer = setTimeout(function () {
            var player = document.querySelector('.html5-video-player');
            if (!player || player.classList.contains('ad-showing')) return;
            if (video.paused || video.readyState < 3) {
                try { video.currentTime = video.currentTime; } catch (_) {} // re-request buffer
                video.play().catch(function () {});
            }
        }, 600);
    }

    // ── 4. Player-class observer — detects ad start/end ───────────────────
    // Watching the 'class' attribute is more reliable than the 'play' event
    // because 'play' can fire before 'ad-showing' has been removed.
    function watchPlayerClass(player) {
        if (!player || player.__pfpad_classWatched) return;
        player.__pfpad_classWatched = true;

        new MutationObserver(function () {
            var isAd = player.classList.contains('ad-showing');
            if (_adWasActive && !isAd) {
                var video = document.querySelector('.html5-main-video');
                if (video) onAdDismissed(video);
            }
            _adWasActive = isAd;
        }).observe(player, { attributes: true, attributeFilter: ['class'] });
    }

    // ── 5. Skip / end ad function ─────────────────────────────────────────
    function skipAd() {
        var player = document.querySelector('.html5-video-player');
        var video  = document.querySelector('.html5-main-video');
        if (!player || !video) return;

        // Attach class watcher lazily (player may not exist at script injection time).
        watchPlayerClass(player);

        if (!player.classList.contains('ad-showing')) return;

        // Silence the ad immediately.
        video.muted  = true;
        video.volume = 0;

        // Prefer the skip button.
        var skipBtn = document.querySelector(
            '.ytp-ad-skip-button, .ytp-skip-ad-button, .ytp-ad-skip-button-modern');
        if (skipBtn) {
            skipBtn.click();
            skipBtn.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
            skipBtn.dispatchEvent(new MouseEvent('mouseup',   { bubbles: true, cancelable: true }));
            return;
        }

        // Non-skippable ad: rush through at 16x.
        // We deliberately do NOT seek to video.duration - 0.1 here because YouTube
        // shares the <video> element between ads and the main video — seeking near
        // the end of what appears to be the ad's duration actually jumps into the
        // main video content and leaves the progress bar frozen at ~75-80%.
        // Playing at 16x ends a 15-second ad in under a second, which is acceptable.
        try { video.playbackRate = 16; } catch (_) {}
        if (video.paused) { video.play().catch(function () {}); }

        // If duration is finite and very short (≤ 60 s) we can safely assume this
        // is a standalone ad segment and seek to its end.  Longer durations indicate
        // the main video duration has leaked into the element — skip the seek.
        if (isFinite(video.duration) && video.duration > 0 && video.duration <= 60) {
            video.currentTime = video.duration - 0.1;
        }
    }

    // ── 6. Poll ───────────────────────────────────────────────────────────
    setInterval(skipAd, 300);

    // ── 7. MutationObserver — catch ads injected after DOMContentLoaded ──
    var _domObserver = new MutationObserver(function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            if (mutations[i].addedNodes.length > 0) {
                skipAd();
                break;
            }
        }
    });

    function startObservers() {
        if (!document.body) return;
        _domObserver.observe(document.body, { childList: true, subtree: true });
        var player = document.querySelector('.html5-video-player');
        if (player) watchPlayerClass(player);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startObservers);
    } else {
        startObservers();
    }

    // ── 8. SPA navigation ────────────────────────────────────────────────
    window.addEventListener('yt-navigate-finish', function () {
        clearTimeout(_stallTimer);
        _adWasActive = false;
        // Re-attach class watcher — the player element may have been recreated.
        setTimeout(function () {
            var player = document.querySelector('.html5-video-player');
            if (player) {
                player.__pfpad_classWatched = false;
                watchPlayerClass(player);
            }
            skipAd();
        }, 500);
    });
})();
""";
}

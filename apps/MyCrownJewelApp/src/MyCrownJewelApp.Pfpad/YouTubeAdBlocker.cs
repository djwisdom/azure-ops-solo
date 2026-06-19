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
    //      click the skip button or force currentTime to the ad's duration.
    //   3. MutationObserver fires skipAd() immediately when new nodes arrive,
    //      catching inline/overlay ads faster than the poll.

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
        '.ytp-ce-covering-overlay',          /* card overlays during ads */
        'ytd-ad-slot-renderer',
        'ytd-in-feed-ad-layout-renderer',
        'ytd-display-ad-renderer'
    ].join(',\n') + ' { display:none!important; visibility:hidden!important; }';
    (document.head || document.documentElement).appendChild(style);

    // ── 2. Skip / end ad function ─────────────────────────────────────────
    function skipAd() {
        var player = document.querySelector('.html5-video-player');
        var video  = document.querySelector('.html5-main-video');
        if (!player || !video) return;

        if (!player.classList.contains('ad-showing')) return;

        // Silence the ad immediately so it isn't heard while we work to dismiss it.
        video.muted  = true;
        video.volume = 0;

        // Prefer the skip button — avoids any potential side-effects.
        // Dispatch synthetic mouse events in addition to .click() for stubborn buttons.
        var skipBtn = document.querySelector(
            '.ytp-ad-skip-button, .ytp-skip-ad-button, .ytp-ad-skip-button-modern');
        if (skipBtn) {
            skipBtn.click();
            skipBtn.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
            skipBtn.dispatchEvent(new MouseEvent('mouseup',   { bubbles: true, cancelable: true }));
            return;
        }

        // No skip button (non-skippable or still loading).
        // Rush through the ad at max speed so it ends as fast as possible.
        try { video.playbackRate = 16; } catch (_) {}

        // Ensure the video is actually playing (buffering/paused state recovery).
        if (video.paused) { video.play().catch(function () {}); }

        // Jump to the very end to trigger ad completion.
        if (isFinite(video.duration) && video.duration > 0) {
            video.currentTime = video.duration - 0.1;
            return;
        }

        // Duration not yet available — ad is still buffering.
        // Register one-shot listeners so we skip the instant it becomes ready.
        if (!video.__pfpad_skipPending) {
            video.__pfpad_skipPending = true;
            var onReady = function () {
                video.__pfpad_skipPending = false;
                video.removeEventListener('durationchange', onReady);
                video.removeEventListener('canplay',        onReady);
                video.removeEventListener('playing',        onReady);
                skipAd();
            };
            video.addEventListener('durationchange', onReady);
            video.addEventListener('canplay',        onReady);
            video.addEventListener('playing',        onReady);
        }
    }

    // Restore playback rate and unmute after the ad ends.
    document.addEventListener('video', function (e) {}, true); // keep listener alive
    document.addEventListener('play', function () {
        var player = document.querySelector('.html5-video-player');
        var video  = document.querySelector('.html5-main-video');
        if (!player || !video) return;
        if (!player.classList.contains('ad-showing')) {
            // Regular video resumed — restore normal playback
            try { video.playbackRate = 1; } catch (_) {}
            video.muted  = false;
        }
    }, true);

    // ── 3. Poll ───────────────────────────────────────────────────────────
    var _pollId = setInterval(skipAd, 300);

    // ── 4. MutationObserver — catch ads injected after DOMContentLoaded ──
    var _observer = new MutationObserver(function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            if (mutations[i].addedNodes.length > 0) {
                skipAd();
                break;
            }
        }
    });

    function startObserver() {
        if (document.body) {
            _observer.observe(document.body, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startObserver);
    } else {
        startObserver();
    }

    // ── 5. Clean up on SPA navigation (YouTube is a single-page app) ─────
    // YouTube fires yt-navigate-start / yt-navigate-finish on every video change.
    window.addEventListener('yt-navigate-finish', function () {
        skipAd(); // immediate check after navigation
    });
})();
""";
}

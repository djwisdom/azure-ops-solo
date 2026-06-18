using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Embedded browser panel backed by WebView2 (Chromium/Edge).
/// Lazy-initialized — WebView2 is only created on first activation to avoid
/// unnecessary RAM cost when the tab has never been opened.
/// </summary>
internal sealed class BrowserPanel : UserControl
{
    // ── Toolbar controls ──────────────────────────────────────────────────────
    private readonly Panel _toolbar;
    private readonly Button _backBtn;
    private readonly Button _forwardBtn;
    private readonly Button _refreshStopBtn;
    private readonly RoundedAddressPanel _addressBarPanel;   // rounded wrapper
    private readonly TextBox _addressBar;                    // inner TextBox (alias to _addressBarPanel.TextBox)
    private readonly Button _goBtn;
    private readonly Button _devToolsBtn;
    private readonly Button _openExternalBtn;
    private readonly ProgressBar _progressBar;

    // ── Favorites bar ─────────────────────────────────────────────────────────
    private readonly Panel _favBar;
    private readonly FlowLayoutPanel _favFlow;
    private Button? _favOverflowBtn;          // "»" shown when items don't fit
    private ContextMenuStrip? _favOverflowMenu;
    private List<FavItem> _favItems = [];      // parsed bookmark model

    private record FavItem(string Name, string? Url, List<FavItem>? Children);

    // ── Status bar ────────────────────────────────────────────────────────────
    private readonly Panel _statusBar;
    private readonly Label _statusLabel;
    private readonly Label _blockedLabel;  // shows "🛡 N blocked" when filter active

    // ── Content area ─────────────────────────────────────────────────────────
    private readonly Panel _contentArea;   // fills space below toolbar / above statusbar
    private WebView2? _webView;
    private Panel? _initPanel;      // spinner shown during WebView2 init
    private Panel? _fallbackPanel;  // shown if WebView2 runtime is missing

    // ── State ─────────────────────────────────────────────────────────────────
    private bool _initialized;
    private bool _webViewReady;
    private bool _isLoading;
    private string _pendingUrl = "";
    private string _homepage = "about:blank";
    private Theme _currentTheme;

    // ── Browser settings ──────────────────────────────────────────────────────
    private bool _ephemeral = false;
    private int _maxHistory = 200;
    private bool _allowLocalhost = true;
    private double _defaultZoom = 1.0;
    private bool _showInTitlebar = false;
    private bool _contentFilterEnabled = true;
    private string _userAgentPreset = "default";
    private string _customUserAgent = "";
    private bool _showFavBar = false;
    private string _favSource = "edge";
    private string? _ephemeralProfileDir;
    private readonly List<string> _historyList = new();

    /// <summary>Fired when the page title changes.</summary>
    public event Action<string>? PageTitleChanged;

    private static readonly Dictionary<string, string> UserAgentPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "",   // empty → WebView2 uses Edge default UA
        ["chrome"]  = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36",
        ["firefox"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:139.0) Gecko/20100101 Firefox/139.0",
        ["safari"]  = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15",
    };

    public BrowserPanel()
    {
        _currentTheme = ThemeManager.Instance.CurrentTheme;

        // ── Toolbar ──────────────────────────────────────────────────────────
        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(0)
        };

        const int navBtnW = 44;
        const int vertMargin = 9;    // top + bottom margin inside each cell → buttons 34px tall
        const int horzMargin = 2;    // horizontal gap between cells

        _backBtn = MakeNavBtn("\uE76B", "Back (Alt+Left)");
        _forwardBtn = MakeNavBtn("\uE76C", "Forward (Alt+Right)");
        _refreshStopBtn = MakeNavBtn("\uE72C", "Refresh (F5)");

        _addressBarPanel = new RoundedAddressPanel(_currentTheme)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(horzMargin, vertMargin, horzMargin, vertMargin),
        };
        _addressBar = _addressBarPanel.TextBox;
        _addressBar.KeyDown += AddressBar_KeyDown;
        _addressBar.Enter += (_, _) => BeginInvoke(() => _addressBar.SelectAll());

        _goBtn = MakeToolBtn("Go", "Navigate", 40);
        _devToolsBtn = MakeNavBtn("\uE943", "Developer Tools (F12)");
        _openExternalBtn = MakeToolBtn("⧉", "Open in system browser", 28);

        // Apply consistent margin to all nav buttons for vertical centering
        foreach (var b in new[] { _backBtn, _forwardBtn, _refreshStopBtn, _devToolsBtn })
            b.Margin = new Padding(horzMargin, vertMargin, horzMargin, vertMargin);
        _goBtn.Margin = new Padding(horzMargin, vertMargin, horzMargin, vertMargin);

        _progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Height = 2,
            Dock = DockStyle.Bottom,
            Visible = false
        };

        // ── Toolbar layout via TableLayoutPanel ───────────────────────────────
        var toolTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(0)
        };
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, navBtnW)); // back
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, navBtnW)); // forward
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, navBtnW)); // refresh
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));      // address
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));      // go
        toolTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, navBtnW)); // devtools
        toolTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _backBtn.Dock = DockStyle.Fill;
        _forwardBtn.Dock = DockStyle.Fill;
        _refreshStopBtn.Dock = DockStyle.Fill;
        _goBtn.Dock = DockStyle.Fill;
        _devToolsBtn.Dock = DockStyle.Fill;

        toolTable.Controls.Add(_backBtn, 0, 0);
        toolTable.Controls.Add(_forwardBtn, 1, 0);
        toolTable.Controls.Add(_refreshStopBtn, 2, 0);
        toolTable.Controls.Add(_addressBarPanel, 3, 0);
        toolTable.Controls.Add(_goBtn, 4, 0);
        toolTable.Controls.Add(_devToolsBtn, 5, 0);

        _toolbar.Controls.Add(toolTable);
        _toolbar.Controls.Add(_progressBar);

        // ── Status bar ────────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8f),
            Padding = new Padding(4, 0, 4, 0),
            AutoSize = false,
            Text = "Ready"
        };
        _openExternalBtn.Dock = DockStyle.Right;
        _openExternalBtn.Width = 28;

        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22
        };
        _blockedLabel = new Label
        {
            Dock = DockStyle.Right,
            Width = 0,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 7.5f),
            Padding = new Padding(4, 0, 6, 0),
            AutoSize = false,
            Visible = false,
            Text = ""
        };
        _statusBar.Controls.Add(_statusLabel);
        _statusBar.Controls.Add(_blockedLabel);
        _statusBar.Controls.Add(_openExternalBtn);

        // ── Content area (holds init placeholder, WebView2, or fallback) ────────
        _contentArea = new Panel { Dock = DockStyle.Fill };

        _initPanel = new Panel { Dock = DockStyle.Fill };
        var initLabel = new Label
        {
            Text = "Click the Browser tab to load the browser...",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9f)
        };
        _initPanel.Controls.Add(initLabel);
        _contentArea.Controls.Add(_initPanel);

        // ── Favorites bar ─────────────────────────────────────────────────────
        _favFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(2, 0, 2, 0),
            Margin = new Padding(0)
        };
        _favBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            Visible = false
        };

        _favOverflowBtn = new Button
        {
            Text = "»",
            AutoSize = false,
            Width = 26,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _favOverflowBtn.FlatAppearance.BorderSize = 0;
        _favOverflowBtn.Click += (_, _) =>
        {
            if (_favOverflowMenu != null)
                _favOverflowMenu.Show(_favOverflowBtn, new Point(0, _favOverflowBtn.Height));
        };

        _favBar.Controls.Add(_favOverflowBtn);  // docked right — must be added before _favFlow
        _favBar.Controls.Add(_favFlow);
        _favBar.SizeChanged += (_, _) => { if (_favItems.Count > 0) RefreshFavLayout(); };

        // Toolbar docks top, status bar docks bottom, content area fills the rest.
        // WebView2 lives inside _contentArea so it can never overlap the toolbar.
        // NOTE: Controls added FIRST with Dock=Top are positioned topmost.
        Controls.Add(_contentArea);
        Controls.Add(_statusBar);
        Controls.Add(_favBar);    // Top, below toolbar
        Controls.Add(_toolbar);   // Top, topmost

        // ── Button events ─────────────────────────────────────────────────────
        _backBtn.Click += (_, _) => _webView?.CoreWebView2?.GoBack();
        _forwardBtn.Click += (_, _) => _webView?.CoreWebView2?.GoForward();
        _refreshStopBtn.Click += (_, _) =>
        {
            if (_isLoading) _webView?.CoreWebView2?.Stop();
            else _webView?.CoreWebView2?.Reload();
        };
        _goBtn.Click += (_, _) => Navigate(_addressBar.Text);
        _devToolsBtn.Click += (_, _) => _webView?.CoreWebView2?.OpenDevToolsWindow();
        _openExternalBtn.Click += OpenInSystemBrowser;

        ApplyTheme(_currentTheme);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetHomepage(string url)
    {
        _homepage = string.IsNullOrWhiteSpace(url) ? "about:blank" : url;
    }

    public void SetTheme(Theme theme)
    {
        _currentTheme = theme;
        ApplyTheme(theme);
    }

    public void SetEphemeral(bool ephemeral) => _ephemeral = ephemeral;

    public void SetMaxHistory(int max) => _maxHistory = Math.Clamp(max, 10, 5000);

    public void SetAllowLocalhost(bool allow) => _allowLocalhost = allow;

    public void SetDefaultZoom(double zoom)
    {
        _defaultZoom = Math.Clamp(zoom, 0.25, 5.0);
        if (_webViewReady && _webView != null)
            _webView.ZoomFactor = _defaultZoom;
    }

    public void SetShowInTitlebar(bool show) => _showInTitlebar = show;

    public void SetContentFilterEnabled(bool enabled)
    {
        _contentFilterEnabled = enabled;
        UpdateBlockedLabel();
    }

    public void SetUserAgent(string preset, string custom)
    {
        _userAgentPreset = preset;
        _customUserAgent = custom;
        ApplyUserAgent();
    }

    public void SetShowFavoritesBar(bool show, string source)
    {
        _showFavBar = show;
        _favSource = source;
        _favBar.Visible = show;
        if (show)
            LoadFavoritesBar(source);
    }

    /// <summary>
    /// Focuses the address bar and selects all text — call when the browser tab is activated
    /// so the user can immediately type or paste a URL without clicking first.
    /// </summary>
    public void FocusAddressBar()
    {
        _addressBar.Focus();
        BeginInvoke(() => _addressBar.SelectAll());
    }

    /// <summary>
    /// Navigate the browser panel to the given URL.
    /// Safe to call before initialization — queued and applied on first load.
    /// </summary>
    public void NavigateTo(string url)
    {
        if (_webViewReady && _webView != null)
            Navigate(url);
        else
            _pendingUrl = url;
    }

    /// <summary>
    /// Called when the Browser tab is first activated. Creates WebView2.
    /// Subsequent calls are no-ops.
    /// </summary>
    public async void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            string profileDir;
            if (_ephemeral)
            {
                // Ephemeral mode: isolated temp directory, deleted on Dispose
                _ephemeralProfileDir = Path.Combine(
                    Path.GetTempPath(),
                    $"pfpad_browser_{Environment.ProcessId}_{Guid.NewGuid():N}");
                profileDir = _ephemeralProfileDir;
            }
            else
            {
                profileDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MyCrownJewelApp", "BrowserProfile");
            }

            var env = await CoreWebView2Environment.CreateAsync(null, profileDir);

            _webView = new WebView2 { Dock = DockStyle.Fill };

            // Remove placeholder before adding WebView2
            if (_initPanel != null)
            {
                _contentArea.Controls.Remove(_initPanel);
                _initPanel.Dispose();
                _initPanel = null;
            }

            // WebView2 goes inside _contentArea, never overlapping toolbar or status bar
            _contentArea.Controls.Add(_webView);

            await _webView.EnsureCoreWebView2Async(env);

            WireWebViewEvents();
            ApplyUserAgent();
            _webViewReady = true;
            UpdateNavButtons();

            // Navigate to pending URL or homepage
            string target = string.IsNullOrWhiteSpace(_pendingUrl) ? _homepage : _pendingUrl;
            Navigate(target);
        }
        catch (Exception ex) when (
            ex is COMException ||
            ex is WebView2RuntimeNotFoundException ||
            ex is DllNotFoundException ||
            ex is FileNotFoundException)
        {
            if (IsHandleCreated)
                BeginInvoke(() => ShowFallback());
            else
                HandleCreated += (_, _) => ShowFallback();
        }
        catch
        {
            // Unexpected error — show fallback
            ShowFallback();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void WireWebViewEvents()
    {
        if (_webView?.CoreWebView2 == null) return;
        var cw = _webView.CoreWebView2;

        cw.NavigationStarting += (_, args) =>
        {
            // Localhost blocking
            if (!_allowLocalhost && IsLocalhostUri(args.Uri))
            {
                args.Cancel = true;
                BeginInvoke(() => SetStatus("Localhost navigation blocked by settings."));
                return;
            }

            // Content filter — top-level navigation
            if (_contentFilterEnabled && ContentFilterService.Instance.IsBlocked(args.Uri))
            {
                args.Cancel = true;
                ContentFilterService.Instance.RecordBlocked();
                BeginInvoke(() => SetStatus($"⛔ Navigation blocked: {new Uri(args.Uri).Host}"));
                return;
            }

            _isLoading = true;
            _refreshStopBtn.Tag = "\uE711"; // Stop glyph
            _refreshStopBtn.Invalidate();
            _toolTip.SetToolTip(_refreshStopBtn, "Stop");
            _addressBar.Text = args.Uri;
            SetStatus("Loading " + args.Uri);
            BeginInvoke(() => _progressBar.Visible = true);

            // Track history (cap at _maxHistory)
            if (!string.IsNullOrEmpty(args.Uri) && !args.Uri.StartsWith("about:"))
            {
                _historyList.Remove(args.Uri);
                _historyList.Insert(0, args.Uri);
                while (_historyList.Count > _maxHistory)
                    _historyList.RemoveAt(_historyList.Count - 1);
            }
        };

        cw.NavigationCompleted += (_, args) =>
        {
            _isLoading = false;
            _refreshStopBtn.Tag = "\uE72C"; // Refresh glyph
            _refreshStopBtn.Invalidate();
            _toolTip.SetToolTip(_refreshStopBtn, "Refresh (F5)");
            BeginInvoke(() =>
            {
                _progressBar.Visible = false;
                UpdateNavButtons();
                SetStatus(args.IsSuccess ? "" : "Navigation failed");
                // Apply default zoom after each navigation
                if (_webView != null) _webView.ZoomFactor = _defaultZoom;
            });
        };

        cw.SourceChanged += (_, _) =>
        {
            BeginInvoke(() =>
            {
                _addressBar.Text = cw.Source;
                UpdateNavButtons();
            });
        };

        cw.DocumentTitleChanged += (_, _) =>
            BeginInvoke(() =>
            {
                SetStatus(cw.DocumentTitle);
                PageTitleChanged?.Invoke(cw.DocumentTitle);
            });

        // ── Sub-resource content filtering (scripts, images, XHR, etc.) ──────
        cw.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        cw.WebResourceRequested += (_, e) =>
        {
            if (!_contentFilterEnabled) return;
            if (!ContentFilterService.Instance.IsBlocked(e.Request.Uri)) return;

            // Block by returning an empty 200 response — avoids browser console net-errors
            e.Response = _webView!.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 200, "Blocked", "");
            ContentFilterService.Instance.RecordBlocked();
        };

        // Subscribe to blocked-count changes so the status bar label stays live
        ContentFilterService.Instance.BlockedCountChanged += count =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(() => UpdateBlockedLabel(count));
        };

        ContentFilterService.Instance.ListsReady += _ =>
        {
            if (!IsHandleCreated) return;
            BeginInvoke(UpdateBlockedLabel);
        };

        UpdateBlockedLabel();
    }

    /// <summary>Returns true if the URI targets localhost (127.0.0.1, [::1], or localhost).</summary>
    private static bool IsLocalhostUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;
        var host = u.Host.ToLowerInvariant();
        return host == "localhost" || host == "127.0.0.1" || host == "::1" || host == "[::1]";
    }

    private void Navigate(string rawUrl)
    {
        string url = NormalizeUrl(rawUrl);
        _addressBar.Text = url;
        _webView?.CoreWebView2?.Navigate(url);
    }

    private static string NormalizeUrl(string raw)
    {
        raw = raw.Trim();
        if (string.IsNullOrEmpty(raw)) return "about:blank";
        if (raw.StartsWith("about:") || raw.StartsWith("file://")) return raw;
        if (raw.Contains("://")) return raw;

        // Looks like a domain (e.g. "github.com")
        if (raw.Contains('.') && !raw.Contains(' '))
            return "https://" + raw;

        // Treat as a search query
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(raw);
    }

    private void UpdateNavButtons()
    {
        bool canBack    = _webView?.CoreWebView2?.CanGoBack    ?? false;
        bool canForward = _webView?.CoreWebView2?.CanGoForward ?? false;
        _backBtn.Enabled    = canBack;
        _forwardBtn.Enabled = canForward;
        _backBtn.Invalidate();
        _forwardBtn.Invalidate();
    }

    private void ApplyUserAgent()
    {
        if (_webView?.CoreWebView2 == null) return;
        string ua = _userAgentPreset == "custom"
            ? _customUserAgent
            : (UserAgentPresets.TryGetValue(_userAgentPreset, out var preset) ? preset : "");
        _webView.CoreWebView2.Settings.UserAgent = ua;
    }

    private void LoadFavoritesBar(string source)
    {
        string[] candidatePaths = source == "chrome"
            ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                  "Google", "Chrome", "User Data", "Default", "Bookmarks")]
            : [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                  "Microsoft", "Edge", "User Data", "Default", "Bookmarks"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                  "Microsoft", "Edge Beta", "User Data", "Default", "Bookmarks"),
              ];

        string? bookmarksFile = Array.Find(candidatePaths, File.Exists);

        _favItems = [];
        _favFlow.Controls.Clear();
        _favOverflowBtn!.Visible = false;

        if (bookmarksFile == null)
        {
            _favFlow.Controls.Add(new Label
            {
                Text = source == "chrome" ? "Chrome bookmarks not found." : "Edge bookmarks not found.",
                AutoSize = true,
                ForeColor = _currentTheme.Muted,
                Font = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent,
                Margin = new Padding(4, 4, 0, 0)
            });
            return;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(bookmarksFile));
            if (!doc.RootElement.TryGetProperty("roots", out var roots)) return;
            if (!roots.TryGetProperty("bookmark_bar", out var bar)) return;
            if (!bar.TryGetProperty("children", out var children)) return;

            _favItems = ParseFavItems(children);
            RefreshFavLayout();
        }
        catch
        {
            _favFlow.Controls.Add(new Label
            {
                Text = "Could not parse bookmarks.",
                AutoSize = true,
                ForeColor = _currentTheme.Muted,
                Font = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent,
                Margin = new Padding(4, 4, 0, 0)
            });
        }
    }

    /// <summary>
    /// Recursively converts a JSON bookmarks array into the in-memory FavItem model.
    /// </summary>
    private static List<FavItem> ParseFavItems(System.Text.Json.JsonElement children)
    {
        var result = new List<FavItem>();
        foreach (var item in children.EnumerateArray())
        {
            string type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            string name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "(no name)";

            if (type == "url")
            {
                string url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(url))
                    result.Add(new FavItem(name, url, null));
            }
            else if (type == "folder")
            {
                List<FavItem> sub = item.TryGetProperty("children", out var sc)
                    ? ParseFavItems(sc) : [];
                result.Add(new FavItem(name, null, sub));
            }
        }
        return result;
    }

    /// <summary>
    /// Measures which FavItems fit in the current bar width and populates _favFlow with
    /// those that fit. Items that overflow go into the "»" dropdown button's menu.
    /// Called on first load and on every _favBar.SizeChanged.
    /// </summary>
    private void RefreshFavLayout()
    {
        _favFlow.SuspendLayout();
        _favFlow.Controls.Clear();
        _favOverflowMenu?.Dispose();
        _favOverflowMenu = null;

        if (_favItems.Count == 0 || _favBar.Width <= 0)
        {
            _favOverflowBtn!.Visible = false;
            _favFlow.ResumeLayout();
            return;
        }

        const int overflowBtnWidth = 30;     // reserved width for the "»" button
        const int itemMargin      = 2;        // gap between buttons
        using var font = new Font("Segoe UI", 8.5f);
        int available = _favBar.Width - overflowBtnWidth - 4;

        var visible  = new List<FavItem>();
        var overflow = new List<FavItem>();
        int used = 0;

        foreach (var item in _favItems)
        {
            string label = item.Name.Length > 20 ? item.Name[..20] + "…" : item.Name;
            string displayLabel = item.Children != null ? "📁 " + label : label;
            int btnWidth = TextRenderer.MeasureText(displayLabel, font).Width + 14 + itemMargin;

            if (used + btnWidth <= available)
            {
                visible.Add(item);
                used += btnWidth;
            }
            else
            {
                overflow.Add(item);
            }
        }

        // Populate visible buttons
        foreach (var item in visible)
        {
            string label = item.Name.Length > 20 ? item.Name[..20] + "…" : item.Name;
            string displayLabel = item.Children != null ? "📁 " + label : label;
            var btn = MakeFavButton(displayLabel, item.Url, item.Children != null);
            WireFavButton(btn, item);
            _favFlow.Controls.Add(btn);
        }

        // Build overflow menu and show/hide "»" button
        if (overflow.Count > 0)
        {
            _favOverflowMenu = new ContextMenuStrip();
            ApplyContextMenuTheme(_favOverflowMenu, _currentTheme);
            BuildFavMenu(_favOverflowMenu.Items, overflow);
            _favOverflowBtn!.Visible = true;
        }
        else
        {
            _favOverflowBtn!.Visible = false;
        }

        _favFlow.ResumeLayout();
    }

    /// <summary>
    /// Wires click navigation or folder popup to an already-created fav bar button.
    /// </summary>
    private void WireFavButton(Button btn, FavItem item)
    {
        if (item.Children != null)
        {
            var menu = new ContextMenuStrip();
            ApplyContextMenuTheme(menu, _currentTheme);
            BuildFavMenu(menu.Items, item.Children);
            if (menu.Items.Count == 0)
                menu.Items.Add("(empty folder)").Enabled = false;
            btn.Click += (_, _) => menu.Show(btn, new Point(0, btn.Height));
        }
        else if (item.Url != null)
        {
            string url = item.Url;
            btn.Click += (_, _) =>
            {
                if (_webViewReady) Navigate(url);
                else _pendingUrl = url;
            };
        }
    }

    /// <summary>
    /// Recursively builds a ToolStripItemCollection from a list of FavItems.
    /// Used for folder submenus and the overflow "»" dropdown.
    /// </summary>
    private void BuildFavMenu(ToolStripItemCollection items, List<FavItem> favItems)
    {
        foreach (var item in favItems)
        {
            if (item.Children != null)
            {
                string label = item.Name.Length > 40 ? item.Name[..40] + "…" : item.Name;
                var sub = new ToolStripMenuItem("📁 " + label);
                BuildFavMenu(sub.DropDownItems, item.Children);
                if (sub.DropDownItems.Count == 0)
                    sub.DropDownItems.Add("(empty folder)").Enabled = false;
                items.Add(sub);
            }
            else if (item.Url != null)
            {
                string label = item.Name.Length > 60 ? item.Name[..60] + "…" : item.Name;
                string url = item.Url;
                var menuItem = new ToolStripMenuItem(label);
                menuItem.Click += (_, _) =>
                {
                    if (_webViewReady) Navigate(url);
                    else _pendingUrl = url;
                };
                items.Add(menuItem);
            }
        }
    }

    private Button MakeFavButton(string text, string? tooltip, bool isFolder)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = false,
            Height = 22,
            Width = TextRenderer.MeasureText(text, new Font("Segoe UI", 8.5f)).Width + 14,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f),
            Cursor = Cursors.Hand,
            Margin = new Padding(1, 2, 1, 2),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = _currentTheme.MenuBackground,
            ForeColor = _currentTheme.Text
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, _currentTheme.Text);
        if (tooltip != null) _toolTip.SetToolTip(btn, tooltip);
        return btn;
    }

    private static void ApplyContextMenuTheme(ContextMenuStrip menu, Theme theme)
    {
        menu.BackColor = theme.MenuBackground;
        menu.ForeColor = theme.Text;
        menu.RenderMode = ToolStripRenderMode.System;
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
        _statusLabel.Text = text;
    }

    private void UpdateBlockedLabel(long? count = null)
    {
        if (InvokeRequired) { BeginInvoke(() => UpdateBlockedLabel(count)); return; }
        long n = count ?? ContentFilterService.Instance.SessionBlockedCount;
        bool show = _contentFilterEnabled && ContentFilterService.Instance.IsReady;
        _blockedLabel.Visible = show;
        if (show)
        {
            _blockedLabel.Text  = n == 0 ? "🛡 0 blocked" : $"🛡 {n:N0} blocked";
            _blockedLabel.Width = TextRenderer.MeasureText(_blockedLabel.Text,
                _blockedLabel.Font).Width + 14;
            _blockedLabel.ForeColor = n > 0 ? _currentTheme.Accent : _currentTheme.Muted;
        }
    }

    private void ShowFallback()
    {
        if (_initPanel != null) { _contentArea.Controls.Remove(_initPanel); _initPanel.Dispose(); _initPanel = null; }
        _webView?.Dispose();
        _webView = null;

        _fallbackPanel = new Panel { Dock = DockStyle.Fill, BackColor = _currentTheme.PanelBackground };
        var lbl = new Label
        {
            Text = "The Browser panel requires the WebView2 runtime.\n\nInstall Microsoft Edge (v85+) to use this feature.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = _currentTheme.Muted,
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(20)
        };
        var installBtn = new Button
        {
            Text = "Download Microsoft Edge",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        installBtn.FlatAppearance.BorderColor = _currentTheme.Accent;
        installBtn.ForeColor = _currentTheme.Accent;
        installBtn.Click += (_, _) =>
            Process.Start(new ProcessStartInfo("https://www.microsoft.com/edge") { UseShellExecute = true });

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = false,
            WrapContents = false,
            Padding = new Padding(0, 80, 0, 0)
        };
        stack.Controls.Add(lbl);
        stack.Controls.Add(installBtn);
        _fallbackPanel.Controls.Add(stack);
        _contentArea.Controls.Add(_fallbackPanel);
    }

    private void OpenInSystemBrowser(object? sender, EventArgs e)
    {
        string url = _webView?.CoreWebView2?.Source ?? _addressBar.Text;
        if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            Navigate(_addressBar.Text);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { _webView?.CoreWebView2?.Reload(); return true; }
        if (keyData == Keys.F12) { _webView?.CoreWebView2?.OpenDevToolsWindow(); return true; }
        if (keyData == (Keys.Alt | Keys.Left))  { _webView?.CoreWebView2?.GoBack();    return true; }
        if (keyData == (Keys.Alt | Keys.Right)) { _webView?.CoreWebView2?.GoForward(); return true; }
        if (keyData == (Keys.Control | Keys.L))
        {
            _addressBar.Focus();
            _addressBar.SelectAll();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ApplyTheme(Theme theme)
    {
        BackColor = theme.PanelBackground;
        _toolbar.BackColor = theme.MenuBackground;
        _statusBar.BackColor = theme.MenuBackground;
        _contentArea.BackColor = theme.PanelBackground;
        _statusLabel.ForeColor = theme.Muted;
        _blockedLabel.BackColor = theme.MenuBackground;
        _progressBar.BackColor = theme.MenuBackground;
        _favBar.BackColor = theme.MenuBackground;
        foreach (Control c in _favFlow.Controls)
        {
            if (c is Button b)
            {
                b.BackColor = theme.MenuBackground;
                b.ForeColor = theme.Text;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, theme.Text);
            }
            else if (c is Label l) l.ForeColor = theme.Muted;
        }
        if (_favOverflowBtn != null)
        {
            _favOverflowBtn.BackColor = theme.MenuBackground;
            _favOverflowBtn.ForeColor = theme.Text;
            _favOverflowBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, theme.Text);
        }
        if (_favOverflowMenu != null)
            ApplyContextMenuTheme(_favOverflowMenu, theme);

        foreach (Control c in _toolbar.Controls)
            if (c is TableLayoutPanel tlp)
                foreach (Control btn in tlp.Controls)
                    if (btn is Button b) ApplyToolButton(b, theme);

        ApplyToolButton(_openExternalBtn, theme);

        _addressBarPanel.SetTheme(theme);
        _addressBar.BackColor = theme.EditorBackground;
        _addressBar.ForeColor = theme.Text;

        if (_fallbackPanel != null)
        {
            _fallbackPanel.BackColor = theme.PanelBackground;
            foreach (Control c in _fallbackPanel.Controls)
                ApplyControlTheme(c, theme);
        }

        if (_initPanel != null)
        {
            _initPanel.BackColor = theme.PanelBackground;
            foreach (Control c in _initPanel.Controls)
                if (c is Label l) { l.ForeColor = theme.Muted; l.BackColor = theme.PanelBackground; }
        }

        UpdateNavButtons();
        // Redraw owner-drawn nav buttons with new theme colors
        _backBtn.Invalidate();
        _forwardBtn.Invalidate();
        _refreshStopBtn.Invalidate();
        _devToolsBtn.Invalidate();
    }

    private static void ApplyToolButton(Button b, Theme t)
    {
        b.BackColor = t.MenuBackground;
        b.ForeColor = t.Text;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, t.Text);
    }

    private static void ApplyControlTheme(Control c, Theme t)
    {
        if (c is Label l) { l.ForeColor = t.Muted; l.BackColor = t.PanelBackground; }
        if (c is Button b) { b.ForeColor = t.Accent; b.BackColor = t.PanelBackground; }
        foreach (Control child in c.Controls) ApplyControlTheme(child, t);
    }

    private readonly ToolTip _toolTip = new();

    /// <summary>
    /// Creates an owner-drawn navigation button with Edge-style rounded hover highlight
    /// and a Segoe MDL2 Assets glyph. The initial glyph is stored in <see cref="Control.Tag"/>
    /// so it can be swapped at runtime (e.g. refresh ↔ stop).
    /// </summary>
    private Button MakeNavBtn(string glyph, string tip)
    {
        bool hovered = false;
        bool pressed = false;

        var btn = new Button
        {
            Text = "",
            Tag = glyph,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
        _toolTip.SetToolTip(btn, tip);

        btn.MouseEnter += (_, _) => { hovered = true;  btn.Invalidate(); };
        btn.MouseLeave += (_, _) => { hovered = false; pressed = false; btn.Invalidate(); };
        btn.MouseDown  += (_, _) => { pressed = true;  btn.Invalidate(); };
        btn.MouseUp    += (_, _) => { pressed = false; btn.Invalidate(); };

        btn.Paint += (_, e) =>
        {
            var g   = e.Graphics;
            var r   = btn.ClientRectangle;
            bool en = btn.Enabled;

            g.SmoothingMode        = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint    = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality   = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            // Fill button background to match toolbar (prevents ghost border from flat button)
            using (var bg = new SolidBrush(_currentTheme.MenuBackground))
                g.FillRectangle(bg, r);

            // Rounded-rect hover / press highlight (Edge-style pill)
            if (hovered && en)
            {
                int alpha = pressed ? 48 : 20;
                var pad   = new Rectangle(r.X + 3, r.Y + 4, r.Width - 6, r.Height - 8);
                using var hvrBrush = new SolidBrush(Color.FromArgb(alpha, _currentTheme.Text));
                using var path     = NavBtnHoverPath(pad, 5);
                g.FillPath(hvrBrush, path);
            }

            // Glyph — reads Tag so refresh/stop can be swapped without recreating the button
            string currentGlyph = btn.Tag as string ?? glyph;
            using var iconFont   = new Font("Segoe MDL2 Assets", 15f, FontStyle.Regular, GraphicsUnit.Point);
            var glyphColor       = en ? _currentTheme.Text : _currentTheme.Muted;
            using var glyphBrush = new SolidBrush(glyphColor);

            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };
            // Nudge 1px up so MDL2 glyphs appear vertically centred
            var textRect = new RectangleF(r.X, r.Y - 1, r.Width, r.Height);
            g.DrawString(currentGlyph, iconFont, glyphBrush, textRect, sf);
        };

        return btn;
    }

    private static System.Drawing.Drawing2D.GraphicsPath NavBtnHoverPath(Rectangle r, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X,              r.Y,               d, d, 180, 90);
        path.AddArc(r.Right - d,      r.Y,               d, d, 270, 90);
        path.AddArc(r.Right - d,      r.Bottom - d,      d, d,   0, 90);
        path.AddArc(r.X,              r.Bottom - d,      d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private Button MakeToolBtn(string text, string tip, int w)
    {
        var btn = new Button
        {
            Text = text,
            Width = w,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false,
            Font = new Font("Segoe UI", 9f)
        };
        _toolTip.SetToolTip(btn, tip);
        return btn;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView?.Dispose();
            // Clean up ephemeral temp profile so no browser data lingers on disk
            if (_ephemeralProfileDir != null)
            {
                try { Directory.Delete(_ephemeralProfileDir, recursive: true); } catch { }
                _ephemeralProfileDir = null;
            }
        }
        base.Dispose(disposing);
    }

    // ── Rounded address bar ──────────────────────────────────────────────────
    /// <summary>
    /// An owner-drawn Panel that wraps a borderless TextBox inside a rounded rectangle,
    /// changing border colour on focus — similar to Edge's address bar styling.
    /// </summary>
    private sealed class RoundedAddressPanel : Panel
    {
        internal readonly TextBox TextBox;
        private bool _focused;
        private Theme _theme;

        internal RoundedAddressPanel(Theme initialTheme)
        {
            _theme = initialTheme;
            TextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 9f),
                BackColor   = initialTheme.EditorBackground,
                ForeColor   = initialTheme.Text,
            };
            TextBox.GotFocus  += (_, _) => { _focused = true;  Invalidate(); };
            TextBox.LostFocus += (_, _) => { _focused = false; Invalidate(); };
            Controls.Add(TextBox);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ResizeRedraw = true;
        }

        internal void SetTheme(Theme t)
        {
            _theme = t;
            TextBox.BackColor = t.EditorBackground;
            TextBox.ForeColor = t.Text;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int th = TextBox.PreferredHeight;
            int y  = Math.Max(0, (Height - th) / 2);
            TextBox.SetBounds(10, y, Math.Max(0, Width - 20), th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Paint the parent background behind the rounded corners
            g.Clear(Parent?.BackColor ?? SystemColors.Control);

            var rc = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            const float radius = 6f;

            using var path = BuildPath(rc, radius);

            // Fill
            using (var fill = new SolidBrush(_theme.EditorBackground))
                g.FillPath(fill, path);

            // Border — accent colour on focus, subdued when idle
            Color borderColor = _focused
                ? _theme.Accent
                : Color.FromArgb(80, _theme.Text);
            float borderWidth = _focused ? 1.5f : 1f;
            using var pen = new Pen(borderColor, borderWidth);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath BuildPath(RectangleF rc, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(rc.Left,          rc.Top,            d, d, 180, 90);
            p.AddArc(rc.Right - d,     rc.Top,            d, d, 270, 90);
            p.AddArc(rc.Right - d,     rc.Bottom - d,     d, d,   0, 90);
            p.AddArc(rc.Left,          rc.Bottom - d,     d, d,  90, 90);
            p.CloseFigure();
            return p;
        }
    }
}

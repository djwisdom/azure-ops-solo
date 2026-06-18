using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Edge-style "View site information" flyout popup.
/// Shows protocol/security info, with drill-down panels for
/// Certificate, Cookies, Trackers, and Permissions.
/// </summary>
internal sealed class SiteInfoPopup : Form
{
    private const int W           = 330;
    private const int HeaderH     = 64;
    private const int RowH        = 38;
    private const int Pad         = 14;
    private const int GlyphColW   = 28;
    private const float SmallFont = 8.5f;
    private const float MedFont   = 9.5f;

    private readonly Theme _theme;
    private readonly SiteProtocolInfo _info;
    private readonly long _blockCount;
    private readonly CoreWebView2? _cw;

    // Navigation stack
    private readonly Panel _host;           // fills client area
    private Panel _currentPanel;

    internal SiteInfoPopup(string url, long blockCount, Theme theme, CoreWebView2? cw)
    {
        _theme      = theme;
        _info       = SiteProtocolInfo.From(url);
        _blockCount = blockCount;
        _cw         = cw;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        ShowInTaskbar   = false;
        AutoScaleMode   = AutoScaleMode.None;
        BackColor       = theme.Border;
        Padding         = new Padding(1);   // 1px border

        _host = new Panel { Dock = DockStyle.Fill, BackColor = theme.MenuBackground };
        Controls.Add(_host);

        _currentPanel = BuildMainPanel();
        _host.Controls.Add(_currentPanel);
        UpdateSize();

        Deactivate += (_, _) => Close();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void Navigate(Panel next)
    {
        _host.Controls.Clear();
        _currentPanel = next;
        _host.Controls.Add(next);
        UpdateSize();
    }

    private void GoBack() => Navigate(BuildMainPanel());

    private void UpdateSize()
    {
        // Let the panel compute its preferred height from its controls
        int h = _currentPanel.Controls.Cast<Control>().Sum(c => c.Height) + 2;
        ClientSize = new Size(W, Math.Max(80, h));
        _currentPanel.Bounds = new Rectangle(0, 0, W, h);
    }

    // ── Main panel ────────────────────────────────────────────────────────────

    private Panel BuildMainPanel()
    {
        var p = new Panel { AutoSize = false, BackColor = _theme.MenuBackground };
        int y = 0;

        // Header
        var header = BuildHeader();
        header.Location = new Point(0, y);
        p.Controls.Add(header);
        y += header.Height;

        // Divider
        p.Controls.Add(MakeDivider(y)); y += 1;

        // Rows
        foreach (var row in BuildMainRows())
        {
            row.Location = new Point(0, y);
            p.Controls.Add(row);
            y += row.Height;
        }

        p.Height = y;
        return p;
    }

    private Panel BuildHeader()
    {
        var h = new Panel { Height = HeaderH, BackColor = _theme.MenuBackground };

        var glyphLbl = MakeLabel(_info.Glyph, new Font("Segoe MDL2 Assets", 13f),
                                  _info.GlyphColor(_theme), new Rectangle(Pad, 10, 22, 22));

        var domainLbl = MakeLabel(_info.Domain, new Font("Segoe UI", 10f, FontStyle.Bold),
                                   _theme.Text, new Rectangle(Pad + 26, 8, W - Pad * 2 - 26, 22));
        domainLbl.TextAlign = ContentAlignment.MiddleLeft;

        var statusLbl = MakeLabel(_info.StatusText, new Font("Segoe UI", SmallFont),
                                   _info.StatusColor(_theme), new Rectangle(Pad + 26, 32, W - Pad * 2 - 26, 18));
        statusLbl.TextAlign = ContentAlignment.MiddleLeft;

        h.Controls.Add(glyphLbl);
        h.Controls.Add(domainLbl);
        h.Controls.Add(statusLbl);
        return h;
    }

    private IEnumerable<Panel> BuildMainRows()
    {
        // 1. Certificate / connection
        string certLabel = _info.IsHttps  ? "Certificate is valid"        :
                           _info.IsHttp   ? "Connection is not secure"     :
                           _info.IsFile   ? "Viewing a local file"         :
                                            _info.Protocol + ":// page";
        string certGlyph = _info.IsHttps  ? "\uE72E" :
                           _info.IsFile   ? "\uE8A5" : "\uE946";
        Color  certColor = _info.IsHttps  ? Color.FromArgb(0, 160, 80)
                         : _info.IsHttp   ? Color.FromArgb(200, 110, 0)
                         :                  Color.FromArgb(120, _theme.Text.R, _theme.Text.G, _theme.Text.B);
        yield return BuildRow(certGlyph, certLabel, null, certColor, () => Navigate(BuildCertPanel()));

        // 2. Cookies — load count async, show "…" until ready
        var cookieRow = BuildRow("\uE8C1", "Cookies and site data", "…",
                                  null, () => Navigate(BuildCookiesPanel()));
        yield return cookieRow;
        if (_cw != null)
            _ = LoadCookieCountAsync(cookieRow);

        // 3. Trackers
        string trackerDetail = _blockCount == 0 ? "None blocked" : $"{_blockCount} blocked";
        yield return BuildRow("\uE711", "Trackers blocked (session)", trackerDetail,
                               null, () => Navigate(BuildTrackersPanel()));

        // 4. Permissions
        yield return BuildRow("\uE770", "Permissions for this site", null,
                               null, () => Navigate(BuildPermissionsPanel()));
    }

    private async Task LoadCookieCountAsync(Panel cookieRow)
    {
        try
        {
            var cookies = await _cw!.CookieManager.GetCookiesAsync(_info.Url);
            if (IsDisposed) return;
            int n = cookies?.Count ?? 0;
            string text = n == 0 ? "None in use" : n == 1 ? "1 in use" : $"{n} in use";
            foreach (Control c in cookieRow.Controls)
                if (c.Tag is string t && t == "detail")
                    c.Text = text;
        }
        catch { /* swallow — cookie API unavailable */ }
    }

    // ── Detail panels ─────────────────────────────────────────────────────────

    private Panel BuildCertPanel()
    {
        var rows = new List<(string, string)>
        {
            ("Connection",  _info.IsHttps ? "TLS encrypted (HTTPS)" : _info.IsHttp ? "Not encrypted (HTTP)" : _info.Protocol),
            ("Host",        _info.Domain),
        };
        if (_info.IsHttps)
            rows.Add(("Issued to",  _info.Domain));

        return BuildDetailPanel("Certificate", rows);
    }

    private Panel BuildCookiesPanel()
    {
        var detail = new List<(string, string)>
        {
            ("Site",    _info.Domain),
            ("Status",  "Loading…"),
        };
        var p = BuildDetailPanel("Cookies and site data", detail);

        // Async-fill the cookie list
        if (_cw != null)
            _ = FillCookieDetailAsync(p);

        return p;
    }

    private async Task FillCookieDetailAsync(Panel p)
    {
        try
        {
            var cookies = await _cw!.CookieManager.GetCookiesAsync(_info.Url);
            if (cookies == null || IsDisposed) return;

            string summary = cookies.Count == 0 ? "No cookies in use"
                           : cookies.Count == 1  ? "1 cookie in use"
                           : $"{cookies.Count} cookies in use";

            var distinct = cookies
                .Select(c => c.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            ReplaceCookieDetail(p, summary, distinct);
        }
        catch { }
    }

    private void ReplaceCookieDetail(Panel p, string summary, List<string> names)
    {
        // Find and replace the status row label
        foreach (Control c in p.Controls)
        {
            if (c.Tag is string t && t == "detail-status")
            {
                c.Text = summary;
                break;
            }
        }

        // Append cookie name rows below existing controls
        int y = p.Controls.Cast<Control>().Max(c => c.Bottom) + 2;
        foreach (string name in names)
        {
            var lbl = MakeLabel("  · " + name, new Font("Segoe UI", SmallFont),
                                 Color.FromArgb(160, _theme.Text.R, _theme.Text.G, _theme.Text.B),
                                 new Rectangle(Pad + GlyphColW, y, W - Pad * 2 - GlyphColW, 20));
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            p.Controls.Add(lbl);
            y += 20;
        }
        p.Height = y + 8;
        UpdateSize();
    }

    private Panel BuildTrackersPanel()
    {
        string status = _blockCount == 0 ? "No trackers blocked this session"
                      : $"{_blockCount} tracker{(_blockCount == 1 ? "" : "s")} blocked this session";
        var detail = new List<(string, string)>
        {
            ("Tracking prevention", "Enabled (blocklists)"),
            ("Blocked this session", _blockCount.ToString()),
            ("Status", status),
        };
        return BuildDetailPanel("Tracker blocking", detail);
    }

    private Panel BuildPermissionsPanel()
    {
        var detail = new List<(string, string)>
        {
            ("Camera",          "Ask (default)"),
            ("Microphone",      "Ask (default)"),
            ("Location",        "Ask (default)"),
            ("Notifications",   "Ask (default)"),
            ("JavaScript",      "Allowed"),
            ("Cookies",         "Allowed"),
        };
        return BuildDetailPanel("Permissions for this site", detail);
    }

    private Panel BuildDetailPanel(string title, List<(string Key, string Val)> rows)
    {
        var p = new Panel { AutoSize = false, BackColor = _theme.MenuBackground };
        int y = 0;

        // Back button row
        var backBtn = new Button
        {
            Text      = "‹  " + title,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", MedFont, FontStyle.Bold),
            ForeColor = _theme.Accent,
            BackColor = _theme.MenuBackground,
            TextAlign = ContentAlignment.MiddleLeft,
            Location  = new Point(0, 0),
            Size      = new Size(W, 38),
            Cursor    = Cursors.Hand,
        };
        backBtn.FlatAppearance.BorderSize = 0;
        backBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, _theme.Text);
        backBtn.Click += (_, _) => GoBack();
        p.Controls.Add(backBtn);
        y += 38;

        p.Controls.Add(MakeDivider(y)); y += 1;

        foreach (var (key, val) in rows)
        {
            int rowY = y;
            var keyLbl = MakeLabel(key, new Font("Segoe UI", SmallFont),
                                    Color.FromArgb(160, _theme.Text.R, _theme.Text.G, _theme.Text.B),
                                    new Rectangle(Pad, rowY, 120, RowH));
            keyLbl.TextAlign = ContentAlignment.MiddleLeft;

            var valLbl = MakeLabel(val, new Font("Segoe UI", SmallFont),
                                    _theme.Text,
                                    new Rectangle(Pad + 124, rowY, W - Pad * 2 - 124, RowH));
            valLbl.TextAlign = ContentAlignment.MiddleLeft;
            if (key == "Status") valLbl.Tag = "detail-status";

            p.Controls.Add(keyLbl);
            p.Controls.Add(valLbl);
            y += RowH;
        }

        p.Height = y + 8;
        return p;
    }

    // ── Row builder ───────────────────────────────────────────────────────────

    private Panel BuildRow(string glyph, string label, string? detail, Color? glyphColor, Action onClick)
    {
        var row = new Panel
        {
            Size      = new Size(W, RowH),
            BackColor = _theme.MenuBackground,
            Cursor    = Cursors.Hand,
        };

        var glyphLbl = MakeLabel(glyph, new Font("Segoe MDL2 Assets", 10f),
                                  glyphColor ?? Color.FromArgb(140, _theme.Text.R, _theme.Text.G, _theme.Text.B),
                                  new Rectangle(Pad, 0, GlyphColW, RowH));
        glyphLbl.TextAlign = ContentAlignment.MiddleCenter;

        int labelW = detail != null ? 160 : W - Pad * 2 - GlyphColW - 18;
        var labelLbl = MakeLabel(label, new Font("Segoe UI", SmallFont), _theme.Text,
                                  new Rectangle(Pad + GlyphColW, 0, labelW, RowH));
        labelLbl.TextAlign = ContentAlignment.MiddleLeft;

        var chevron = MakeLabel("\uE76C", new Font("Segoe MDL2 Assets", 8f),
                                 Color.FromArgb(120, _theme.Text.R, _theme.Text.G, _theme.Text.B),
                                 new Rectangle(W - Pad - 14, 0, 14, RowH));
        chevron.TextAlign = ContentAlignment.MiddleRight;

        row.Controls.Add(glyphLbl);
        row.Controls.Add(labelLbl);
        row.Controls.Add(chevron);

        if (detail != null)
        {
            var detailLbl = MakeLabel(detail, new Font("Segoe UI", SmallFont),
                                       Color.FromArgb(140, _theme.Text.R, _theme.Text.G, _theme.Text.B),
                                       new Rectangle(Pad + GlyphColW + labelW, 0, W - Pad * 2 - GlyphColW - labelW - 18, RowH));
            detailLbl.TextAlign = ContentAlignment.MiddleRight;
            detailLbl.Tag = "detail";
            row.Controls.Add(detailLbl);
        }

        // Hover + click on whole row including all child labels
        Action<object?, EventArgs> hover = (_, _) => row.BackColor = Color.FromArgb(20, _theme.Text);
        Action<object?, EventArgs> leave = (_, _) => row.BackColor = _theme.MenuBackground;
        Action<object?, EventArgs> click = (_, _) => onClick();
        ApplyToAll(row, hover, leave, click);

        return row;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Label MakeLabel(string text, Font font, Color fore, Rectangle bounds)
    {
        return new Label
        {
            Text      = text,
            Font      = font,
            ForeColor = fore,
            AutoSize  = false,
            Bounds    = bounds,
        };
    }

    private Panel MakeDivider(int y)
    {
        return new Panel
        {
            Location  = new Point(0, y),
            Size      = new Size(W, 1),
            BackColor = _theme.Border,
        };
    }

    private static void ApplyToAll(Panel row,
        Action<object?, EventArgs> enter, Action<object?, EventArgs> leave,
        Action<object?, EventArgs> click)
    {
        row.MouseEnter += new EventHandler(enter);
        row.MouseLeave += new EventHandler(leave);
        row.Click      += new EventHandler(click);
        foreach (Control c in row.Controls)
        {
            c.MouseEnter += new EventHandler(enter);
            c.MouseLeave += new EventHandler(leave);
            c.Click      += new EventHandler(click);
        }
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessDialogKey(keyData);
    }
}

// ── Protocol/domain info ──────────────────────────────────────────────────────

internal sealed record SiteProtocolInfo(
    string Url, string Protocol, string Domain,
    string Glyph, string StatusText,
    bool IsHttps, bool IsHttp, bool IsFile)
{
    internal static SiteProtocolInfo From(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Unknown(url);

        return uri.Scheme.ToLowerInvariant() switch
        {
            "https" => new(url, "HTTPS", uri.Host, "\uE72E", "Connection is secure",   true,  false, false),
            "http"  => new(url, "HTTP",  uri.Host, "\uE946", "Connection is not secure", false, true,  false),
            "file"  => new(url, "file",  "Local file", "\uE8A5", "Viewing a local file", false, false, true),
            "about" => new(url, "about", url, "\uE946", "Browser internal page", false, false, false),
            var s   => new(url, s, uri.Host.Length > 0 ? uri.Host : url, "\uE946", $"{s}:// page", false, false, false),
        };
    }

    private static SiteProtocolInfo Unknown(string url) =>
        new(url, "", url.Length > 40 ? url[..40] + "…" : url, "\uE946", "", false, false, false);

    internal Color GlyphColor(Theme t) =>
        IsHttps ? Color.FromArgb(0, 160, 80) :
        IsHttp  ? Color.FromArgb(200, 120, 0) :
        IsFile  ? Color.FromArgb(80, 140, 200) :
                  Color.FromArgb(140, t.Text.R, t.Text.G, t.Text.B);

    internal Color StatusColor(Theme t) =>
        IsHttps ? Color.FromArgb(0, 160, 80) :
        IsHttp  ? Color.FromArgb(200, 100, 0) :
                  Color.FromArgb(160, t.Text.R, t.Text.G, t.Text.B);
}

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Borderless floating panel shown when the user clicks the site-info glyph
/// in the browser address bar.  Mirrors Edge's "View site information" flyout.
/// Auto-dismisses when it loses activation.
/// </summary>
internal sealed class SiteInfoPopup : Form
{
    // ── Layout constants ──────────────────────────────────────────────────────
    private const int PopupWidth   = 320;
    private const int HeaderHeight = 56;
    private const int RowHeight    = 36;
    private const int Padding      = 12;

    // ── Construction ─────────────────────────────────────────────────────────

    internal SiteInfoPopup(string url, long blockCount, Theme theme)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        ShowInTaskbar   = false;
        TopMost         = false;
        AutoScaleMode   = AutoScaleMode.None;

        var info   = SiteProtocolInfo.From(url);
        var rows   = BuildRows(info, blockCount);
        int height = HeaderHeight + rows.Count * RowHeight + Padding;

        ClientSize = new Size(PopupWidth, height);

        // ── Drop-shadow border ────────────────────────────────────────────────
        var border = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = theme.Border,
            Padding   = new Padding(1)
        };

        // ── Inner panel ───────────────────────────────────────────────────────
        var inner = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = theme.MenuBackground,
        };

        // ── Header ────────────────────────────────────────────────────────────
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = HeaderHeight,
            BackColor = theme.MenuBackground,
            Padding   = new Padding(Padding, 10, Padding, 0),
        };

        var glyphLbl = new Label
        {
            Text      = info.Glyph,
            Font      = new Font("Segoe MDL2 Assets", 14f),
            ForeColor = info.GlyphColor(theme),
            AutoSize  = true,
            Location  = new Point(Padding, 10),
        };

        string domain = info.Domain;
        var domainLbl = new Label
        {
            Text      = domain,
            Font      = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            ForeColor = theme.Text,
            AutoSize  = false,
            Width     = PopupWidth - Padding * 3 - 22,
            Height    = 20,
            Location  = new Point(Padding + 26, 8),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var statusLbl = new Label
        {
            Text      = info.StatusText,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = info.StatusColor(theme),
            AutoSize  = false,
            Width     = PopupWidth - Padding * 2,
            Height    = 16,
            Location  = new Point(Padding + 26, 28),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        header.Controls.Add(glyphLbl);
        header.Controls.Add(domainLbl);
        header.Controls.Add(statusLbl);

        // ── Divider ───────────────────────────────────────────────────────────
        var divider = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 1,
            BackColor = theme.Border,
        };

        // ── Rows ──────────────────────────────────────────────────────────────
        var rowsPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = theme.MenuBackground,
        };

        int y = 0;
        foreach (var (glyph, glyphFont, label, detail) in rows)
        {
            var row = BuildRow(glyph, glyphFont, label, detail, y, theme);
            rowsPanel.Controls.Add(row);
            y += RowHeight;
        }

        inner.Controls.Add(rowsPanel);
        inner.Controls.Add(divider);
        inner.Controls.Add(header);
        border.Controls.Add(inner);
        Controls.Add(border);

        // Dismiss on lost activation
        Deactivate += (_, _) => Close();
    }

    // ── Row builder ───────────────────────────────────────────────────────────

    private static Panel BuildRow(string glyph, Font glyphFont, string label, string? detail,
                                   int y, Theme theme)
    {
        var row = new Panel
        {
            Location  = new Point(0, y),
            Size      = new Size(PopupWidth, RowHeight),
            BackColor = theme.MenuBackground,
        };

        var glyphLbl = new Label
        {
            Text      = glyph,
            Font      = glyphFont,
            ForeColor = Color.FromArgb(160, theme.Text),
            AutoSize  = false,
            Size      = new Size(24, RowHeight),
            Location  = new Point(Padding, 0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var labelLbl = new Label
        {
            Text      = label,
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = theme.Text,
            AutoSize  = false,
            Location  = new Point(Padding + 26, 0),
            Size      = new Size(detail != null ? 155 : 230, RowHeight),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        row.Controls.Add(glyphLbl);
        row.Controls.Add(labelLbl);

        if (detail != null)
        {
            var detailLbl = new Label
            {
                Text      = detail,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, theme.Text),
                AutoSize  = false,
                Location  = new Point(Padding + 26 + 160, 0),
                Size      = new Size(PopupWidth - Padding * 2 - 186, RowHeight),
                TextAlign = ContentAlignment.MiddleRight,
            };
            row.Controls.Add(detailLbl);
        }

        // Subtle hover highlight
        row.MouseEnter += (_, _) => row.BackColor = Color.FromArgb(20, theme.Text);
        row.MouseLeave += (_, _) => row.BackColor = theme.MenuBackground;
        foreach (Control c in row.Controls)
        {
            c.MouseEnter += (_, _) => row.BackColor = Color.FromArgb(20, theme.Text);
            c.MouseLeave += (_, _) => row.BackColor = theme.MenuBackground;
        }

        return row;
    }

    // ── Row content ───────────────────────────────────────────────────────────

    private static System.Collections.Generic.List<(string, Font, string, string?)>
        BuildRows(SiteProtocolInfo info, long blockCount)
    {
        var mdl2  = new Font("Segoe MDL2 Assets", 10f);
        var emoji = new Font("Segoe UI Symbol", 10f);

        var rows = new System.Collections.Generic.List<(string, Font, string, string?)>();

        // Security detail row
        if (info.IsHttps)
        {
            rows.Add(("\uE72E", mdl2, "Certificate is valid", null));
        }
        else if (info.IsHttp)
        {
            rows.Add(("\uE946", mdl2, "Your connection is not secure", null));
        }
        else if (info.IsFile)
        {
            rows.Add(("\uE8A5", mdl2, "Viewing a local file", null));
        }
        else
        {
            rows.Add(("\uE946", mdl2, info.Protocol, null));
        }

        // Trackers row
        string blockedText = blockCount == 0 ? "None blocked" : $"{blockCount} blocked";
        rows.Add(("\uE711", mdl2, "Trackers blocked this session", blockedText));

        // Cookies row (static info)
        rows.Add(("\uE8C1", mdl2, "Cookies and site data", null));

        // Permissions row
        rows.Add(("\uE770", mdl2, "Permissions for this site", null));

        return rows;
    }

    // ── Keyboard dismiss ─────────────────────────────────────────────────────
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessDialogKey(keyData);
    }
}

// ── Protocol info helper ──────────────────────────────────────────────────────

internal sealed record SiteProtocolInfo(
    string Url,
    string Protocol,
    string Domain,
    string Glyph,       // Segoe MDL2 Assets char
    string StatusText,
    bool IsHttps,
    bool IsHttp,
    bool IsFile)
{
    internal static SiteProtocolInfo From(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Unknown(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Unknown(url);

        string scheme = uri.Scheme.ToLowerInvariant();

        return scheme switch
        {
            "https" => new SiteProtocolInfo(url, "HTTPS", uri.Host,
                           "\uE72E", "Connection is secure",
                           true, false, false),

            "http"  => new SiteProtocolInfo(url, "HTTP", uri.Host,
                           "\uE946", "Connection is not secure",
                           false, true, false),

            "file"  => new SiteProtocolInfo(url, "file", "Local file",
                           "\uE8A5", "You are viewing a local file",
                           false, false, true),

            "about" => new SiteProtocolInfo(url, "about", url,
                           "\uE946", "Browser internal page",
                           false, false, false),

            _       => new SiteProtocolInfo(url, scheme, uri.Host.Length > 0 ? uri.Host : url,
                           "\uE946", $"{scheme}:// page",
                           false, false, false),
        };
    }

    private static SiteProtocolInfo Unknown(string url) =>
        new(url, "", url.Length > 40 ? url[..40] + "…" : url,
            "\uE946", "", false, false, false);

    internal Color GlyphColor(Theme theme) =>
        IsHttps  ? Color.FromArgb(0, 160, 80)   :
        IsHttp   ? Color.FromArgb(200, 120, 0)  :
        IsFile   ? Color.FromArgb(80, 140, 200) :
                   Color.FromArgb(140, theme.Text.R, theme.Text.G, theme.Text.B);

    internal Color StatusColor(Theme theme) =>
        IsHttps  ? Color.FromArgb(0, 160, 80)  :
        IsHttp   ? Color.FromArgb(200, 100, 0) :
                   Color.FromArgb(160, theme.Text.R, theme.Text.G, theme.Text.B);
}

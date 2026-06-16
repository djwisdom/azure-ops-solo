using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Shared Markdown-to-themed-HTML renderer used by both MarkdownPreviewPanel and UserManualDialog.
/// Does NOT support raw HTML passthrough to prevent injection from untrusted content.
/// </summary>
internal static class MarkdownRenderer
{
    /// <summary>Converts Markdown text to a complete themed HTML document.</summary>
    public static string ConvertToHtml(string md, Theme theme)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        html.Append("<style>");
        html.Append(BuildThemeCss(theme));
        html.Append("</style></head><body>");

        var lines = md.Replace("\r\n", "\n").Split('\n');
        bool inCodeBlock = false;
        bool inTable = false;
        bool inUl = false;
        bool inOl = false;
        var tableRows = new List<string>();
        var codeBlockLines = new StringBuilder();

        foreach (var rawLine in lines)
        {
            string line = rawLine;

            // Code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                FlushList(html, ref inUl, ref inOl);
                FlushTable(html, tableRows, ref inTable);
                if (inCodeBlock)
                {
                    html.Append("<pre><code>");
                    html.Append(System.Web.HttpUtility.HtmlEncode(codeBlockLines.ToString().TrimEnd('\n', '\r')));
                    html.Append("</code></pre>");
                    codeBlockLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.AppendLine(line);
                continue;
            }

            string trimmed = line.TrimStart();

            // Table rows
            if (trimmed.StartsWith("|"))
            {
                FlushList(html, ref inUl, ref inOl);
                if (!inTable) { inTable = true; tableRows.Clear(); }
                tableRows.Add(trimmed);
                continue;
            }
            FlushTable(html, tableRows, ref inTable);

            // Empty line
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushList(html, ref inUl, ref inOl);
                continue;
            }

            // Headers
            if (line.StartsWith("#### ")) { FlushList(html, ref inUl, ref inOl); html.Append($"<h4>{FormatInline(line[5..])}</h4>"); continue; }
            if (line.StartsWith("### "))  { FlushList(html, ref inUl, ref inOl); html.Append($"<h3>{FormatInline(line[4..])}</h3>"); continue; }
            if (line.StartsWith("## "))   { FlushList(html, ref inUl, ref inOl); html.Append($"<h2>{FormatInline(line[3..])}</h2>"); continue; }
            if (line.StartsWith("# "))    { FlushList(html, ref inUl, ref inOl); html.Append($"<h1>{FormatInline(line[2..])}</h1>"); continue; }

            // Horizontal rule
            if (line is "---" or "***" or "___") { FlushList(html, ref inUl, ref inOl); html.Append("<hr>"); continue; }

            // Blockquote
            if (line.StartsWith("> ")) { FlushList(html, ref inUl, ref inOl); html.Append($"<blockquote><p>{FormatInline(line[2..])}</p></blockquote>"); continue; }

            // Unordered list
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
            {
                FlushOl(html, ref inOl);
                if (!inUl) { html.Append("<ul>"); inUl = true; }
                int skip = (trimmed[0] == '•') ? 2 : 2;
                html.Append($"<li>{FormatInline(trimmed[skip..])}</li>");
                continue;
            }

            // Ordered list
            var orderedMatch = Regex.Match(line, @"^\d+\.\s(.+)$");
            if (orderedMatch.Success)
            {
                FlushUl(html, ref inUl);
                if (!inOl) { html.Append("<ol>"); inOl = true; }
                html.Append($"<li>{FormatInline(orderedMatch.Groups[1].Value)}</li>");
                continue;
            }

            FlushList(html, ref inUl, ref inOl);
            html.Append($"<p>{FormatInline(line)}</p>");
        }

        // Flush any open blocks
        if (inCodeBlock && codeBlockLines.Length > 0)
        {
            html.Append("<pre><code>");
            html.Append(System.Web.HttpUtility.HtmlEncode(codeBlockLines.ToString().TrimEnd('\n', '\r')));
            html.Append("</code></pre>");
        }
        FlushList(html, ref inUl, ref inOl);
        FlushTable(html, tableRows, ref inTable);

        html.Append("</body></html>");
        return html.ToString();
    }

    private static void FlushUl(StringBuilder html, ref bool inUl)  { if (inUl)  { html.Append("</ul>"); inUl  = false; } }
    private static void FlushOl(StringBuilder html, ref bool inOl)  { if (inOl)  { html.Append("</ol>"); inOl  = false; } }
    private static void FlushList(StringBuilder html, ref bool inUl, ref bool inOl) { FlushUl(html, ref inUl); FlushOl(html, ref inOl); }

    private static void FlushTable(StringBuilder html, List<string> rows, ref bool inTable)
    {
        if (!inTable || rows.Count == 0) return;
        inTable = false;
        if (rows.Count < 2) return;

        var alignments = new List<string>();
        foreach (var part in rows[1].Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            string c = part.Trim();
            alignments.Add(c.StartsWith(":") && c.EndsWith(":") ? "center" : c.EndsWith(":") ? "right" : "left");
        }

        html.Append("<table>");
        html.Append("<thead><tr>");
        foreach (var (cell, i) in rows[0].Split('|', StringSplitOptions.RemoveEmptyEntries).Select((c, i) => (c, i)))
            html.Append($"<th style='text-align:{Align(alignments, i)}'>{FormatInline(cell.Trim())}</th>");
        html.Append("</tr></thead>");

        if (rows.Count > 2)
        {
            html.Append("<tbody>");
            for (int r = 2; r < rows.Count; r++)
            {
                html.Append("<tr>");
                foreach (var (cell, i) in rows[r].Split('|', StringSplitOptions.RemoveEmptyEntries).Select((c, i) => (c, i)))
                    html.Append($"<td style='text-align:{Align(alignments, i)}'>{FormatInline(cell.Trim())}</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody>");
        }
        html.Append("</table>");
    }

    private static string Align(List<string> alignments, int i) => i < alignments.Count ? alignments[i] : "left";

    internal static string FormatInline(string text)
    {
        // Escape HTML first, then apply inline formatting
        string s = System.Web.HttpUtility.HtmlEncode(text);
        s = Regex.Replace(s, @"`([^`]+)`", "<code>$1</code>");
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"__(.+?)__", "<strong>$1</strong>");
        s = Regex.Replace(s, @"\*(.+?)\*", "<em>$1</em>");
        s = Regex.Replace(s, @"_(.+?)_", "<em>$1</em>");
        // Links: open externally (href preserved, target handled by browser Navigating event)
        s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "<a href='$2'>$1</a>");
        return s;
    }

    internal static string BuildThemeCss(Theme theme)
    {
        string bg       = Hex(theme.EditorBackground);
        string text     = Hex(theme.Text);
        string panel    = Hex(theme.PanelBackground);
        string border   = Hex(theme.Border);
        string accent   = Hex(theme.Accent);
        string muted    = Hex(theme.Muted);
        string codeText = Hex(theme.CommentColor);
        string preBg    = Hex(Dim(theme.EditorBackground, theme.IsLight ? 0.93f : 0.75f));
        string hBorder  = Hex(Dim(theme.Border, 0.5f));

        float sf = theme.IsLight ? 0.55f : 1.4f;
        string sThumb = Hex(Dim(theme.Border, sf));
        string sTrack = Hex(theme.IsLight ? Dim(theme.EditorBackground, 0.9f) : Dim(theme.EditorBackground, 1.15f));
        string sArrow = Hex(theme.Muted);

        return
            $"body{{background:{bg};color:{text};font-family:'Segoe UI',Arial,sans-serif;" +
            $"font-size:14px;padding:24px 32px;line-height:1.7;max-width:860px;margin:0 auto;" +
            $"scrollbar-face-color:{sThumb};scrollbar-track-color:{sTrack};scrollbar-arrow-color:{sArrow};" +
            $"scrollbar-shadow-color:{sThumb};scrollbar-highlight-color:{sTrack};" +
            $"scrollbar-3dlight-color:{sTrack};scrollbar-darkshadow-color:{sThumb}}}" +
            $"h1{{font-size:1.9em;color:{accent};border-bottom:2px solid {hBorder};padding-bottom:6px;margin-top:32px}}" +
            $"h2{{font-size:1.45em;color:{accent};border-bottom:1px solid {hBorder};padding-bottom:4px;margin-top:28px}}" +
            $"h3{{font-size:1.15em;color:{accent};margin-top:20px}}" +
            $"h4{{font-size:1em;color:{muted};margin-top:16px}}" +
            $"code{{background:{panel};color:{codeText};padding:2px 5px;border-radius:3px;font-family:Consolas,monospace;font-size:0.9em}}" +
            $"pre{{background:{preBg};padding:14px;border-radius:5px;overflow-x:auto;border:1px solid {border}}}" +
            $"pre code{{background:transparent;padding:0;color:{text};font-size:0.88em}}" +
            $"a{{color:{accent};text-decoration:none}}a:hover{{text-decoration:underline}}" +
            $"blockquote{{border-left:3px solid {accent};margin:12px 0;padding:6px 14px;color:{muted};background:{panel};border-radius:0 4px 4px 0}}" +
            $"table{{border-collapse:collapse;width:100%;margin:12px 0}}" +
            $"th,td{{border:1px solid {border};padding:7px 12px;text-align:left}}" +
            $"th{{background:{panel};font-weight:600}}" +
            $"tr:nth-child(even){{background:{Hex(Dim(theme.EditorBackground, theme.IsLight ? 0.97f : 1.04f))}}}" +
            $"hr{{border:none;border-top:1px solid {border};margin:20px 0}}" +
            $"ul,ol{{padding-left:26px;margin:8px 0}}" +
            $"li{{margin-bottom:4px}}" +
            $"kbd{{background:{panel};border:1px solid {border};border-radius:3px;padding:1px 5px;" +
            $"font-family:Consolas,monospace;font-size:0.85em;color:{text}}}" +
            $".note{{background:{panel};border-left:3px solid {accent};padding:8px 12px;border-radius:0 4px 4px 0;margin:12px 0}}";
    }

    private static string Hex(Color c) =>
        $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color Dim(Color c, float f) =>
        Color.FromArgb(
            (int)Math.Min(255, Math.Max(0, c.R * f)),
            (int)Math.Min(255, Math.Max(0, c.G * f)),
            (int)Math.Min(255, Math.Max(0, c.B * f)));
}

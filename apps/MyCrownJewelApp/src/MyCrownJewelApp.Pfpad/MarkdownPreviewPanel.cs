using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class MarkdownPreviewPanel : Panel
{
    private readonly WebBrowser _browser;
    private readonly Button _closeButton;
    private readonly Panel _headerPanel;
    private readonly Label _headerText;
    private string _currentSource = "";
    private Theme _currentTheme;

    public event Action? CloseRequested;

    public MarkdownPreviewPanel()
    {
        BackColor = Color.FromArgb(30, 30, 30);
        MinimumSize = new Size(100, 60);

        _closeButton = new Button
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 180, 180),
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(22, 22),
            Location = new Point(Width - 26, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _closeButton.Click += (_, _) => CloseRequested?.Invoke();

        _browser = new WebBrowser
        {
            Dock = DockStyle.Fill,
            AllowNavigation = false,
            AllowWebBrowserDrop = false,
            IsWebBrowserContextMenuEnabled = false,
            WebBrowserShortcutsEnabled = false,
            ScrollBarsEnabled = true,
        };

        Controls.Add(_browser);

        // Header bar
        _headerText = new Label
        {
            Text = "  Markdown Preview",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 180, 180),
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 150,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _headerPanel = new Panel
        {
            Height = 26,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(45, 45, 45),
        };
        _headerPanel.Controls.Add(_headerText);
        _headerPanel.Controls.Add(_closeButton);
        Controls.Add(_headerPanel);

        Dock = DockStyle.Fill;
    }

    public void RenderMarkdown(string markdown, string? fileName)
    {
        string html;
        if (string.IsNullOrEmpty(markdown))
        {
            string mutedHex = ColorToHex(_currentTheme.Muted);
            html = $"<html><body style='color:{mutedHex};font-family:sans-serif;padding:20px;background:{ColorToHex(_currentTheme.EditorBackground)}'><p>No content</p></body></html>";
        }
        else
        {
            _currentSource = markdown;
            html = ConvertToHtml(markdown, _currentTheme);
        }

        try
        {
            if (_browser.Document != null && _browser.Document.Body != null)
            {
                _browser.Document.OpenNew(true);
                _browser.Document.Write(html);
            }
            else
            {
                _browser.DocumentText = html;
            }
        }
        catch
        {
            _browser.DocumentText = html;
        }
    }

    public void SetTheme(Theme theme)
    {
        _currentTheme = theme;
        BackColor = theme.PanelBackground;
        _closeButton.ForeColor = theme.Text;
        _headerPanel.BackColor = theme.MenuBackground;
        _headerText.ForeColor = theme.Text;

        if (!string.IsNullOrEmpty(_currentSource))
            RenderMarkdown(_currentSource, null);
    }

    private string ConvertToHtml(string md, Theme theme)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        html.Append("<style>");
        html.Append(BuildThemeCss(theme));
        html.Append("</style></head><body>");

        var lines = md.Replace("\r\n", "\n").Split('\n');
        bool inCodeBlock = false;
        bool inTable = false;
        var tableRows = new List<string>();
        var codeBlockLines = new StringBuilder();

        foreach (var rawLine in lines)
        {
            string line = rawLine;

            // Code blocks
            if (line.TrimStart().StartsWith("```"))
            {
                FlushTable(html, tableRows, ref inTable);
                if (inCodeBlock)
                {
                    html.Append("<pre><code>");
                    html.Append(System.Web.HttpUtility.HtmlEncode(codeBlockLines.ToString()));
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

            // Table rows — lines starting with |
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("|"))
            {
                if (!inTable)
                {
                    inTable = true;
                    tableRows.Clear();
                }
                tableRows.Add(trimmed);
                continue;
            }

            // Flush table when a non-table line is encountered
            FlushTable(html, tableRows, ref inTable);

            // Empty line = paragraph break
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Headers
            if (line.StartsWith("### "))
            {
                html.Append($"<h3>{FormatInline(line[4..])}</h3>");
                continue;
            }
            if (line.StartsWith("## "))
            {
                html.Append($"<h2>{FormatInline(line[3..])}</h2>");
                continue;
            }
            if (line.StartsWith("# "))
            {
                html.Append($"<h1>{FormatInline(line[2..])}</h1>");
                continue;
            }

            // Horizontal rule
            if (line is "---" or "***" or "___")
            {
                html.Append("<hr>");
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                html.Append($"<blockquote>{FormatInline(line[2..])}</blockquote>");
                continue;
            }

            // Unordered list
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                html.Append($"<li>{FormatInline(line[2..])}</li>");
                continue;
            }

            // Ordered list
            var orderedMatch = Regex.Match(line, @"^\d+\.\s(.+)$");
            if (orderedMatch.Success)
            {
                html.Append($"<li>{FormatInline(orderedMatch.Groups[1].Value)}</li>");
                continue;
            }

            // Regular paragraph with inline formatting
            html.Append($"<p>{FormatInline(line)}</p>");
        }

        // Flush remaining code block or table
        if (inCodeBlock && codeBlockLines.Length > 0)
        {
            html.Append("<pre><code>");
            html.Append(System.Web.HttpUtility.HtmlEncode(codeBlockLines.ToString()));
            html.Append("</code></pre>");
        }
        FlushTable(html, tableRows, ref inTable);

        html.Append("</body></html>");
        return html.ToString();
    }

    private static void FlushTable(StringBuilder html, List<string> rows, ref bool inTable)
    {
        if (!inTable || rows.Count == 0) return;
        inTable = false;

        if (rows.Count < 2) return;

        // Parse alignment from separator row (second row)
        string sep = rows[1].Trim();
        var alignments = new List<string>();
        foreach (var part in sep.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            string c = part.Trim();
            if (c.StartsWith(":") && c.EndsWith(":"))
                alignments.Add("center");
            else if (c.EndsWith(":"))
                alignments.Add("right");
            else
                alignments.Add("left");
        }

        html.Append("<table>");

        // Header row
        html.Append("<thead><tr>");
        var headerCells = rows[0].Split('|', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < headerCells.Length; i++)
        {
            string a = i < alignments.Count ? alignments[i] : "left";
            html.Append($"<th style='text-align:{a}'>{FormatInline(headerCells[i].Trim())}</th>");
        }
        html.Append("</tr></thead>");

        // Body rows
        if (rows.Count > 2)
        {
            html.Append("<tbody>");
            for (int r = 2; r < rows.Count; r++)
            {
                html.Append("<tr>");
                var bodyCells = rows[r].Split('|', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < bodyCells.Length; i++)
                {
                    string a = i < alignments.Count ? alignments[i] : "left";
                    html.Append($"<td style='text-align:{a}'>{FormatInline(bodyCells[i].Trim())}</td>");
                }
                html.Append("</tr>");
            }
            html.Append("</tbody>");
        }

        html.Append("</table>");
    }

    private static string FormatInline(string text)
    {
        string processed = Escape(text);
        processed = Regex.Replace(processed, @"`([^`]+)`", "<code>$1</code>");
        processed = Regex.Replace(processed, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        processed = Regex.Replace(processed, @"__(.+?)__", "<strong>$1</strong>");
        processed = Regex.Replace(processed, @"\*(.+?)\*", "<em>$1</em>");
        processed = Regex.Replace(processed, @"_(.+?)_", "<em>$1</em>");
        processed = Regex.Replace(processed, @"\[([^\]]+)\]\(([^)]+)\)", "<a href='$2'>$1</a>");
        return processed;
    }

    private static string Escape(string text) =>
        System.Web.HttpUtility.HtmlEncode(text);

    private static string BuildThemeCss(Theme theme)
    {
        string bg = ColorToHex(theme.EditorBackground);
        string text = ColorToHex(theme.Text);
        string panel = ColorToHex(theme.PanelBackground);
        string border = ColorToHex(theme.Border);
        string accent = ColorToHex(theme.Accent);
        string muted = ColorToHex(theme.Muted);
        string codeText = ColorToHex(theme.CommentColor);
        string preBg = ColorToHex(DimColor(theme.EditorBackground, 0.85f));
        string headingBorder = ColorToHex(DimColor(theme.Border, 0.5f));

        return $"body{{background:{bg};color:{text};font-family:'Segoe UI',sans-serif;padding:20px;line-height:1.6}}" +
               $"h1,h2,h3,h4{{color:{accent};border-bottom:1px solid {headingBorder};padding-bottom:4px}}" +
               $"h1{{font-size:1.8em}}h2{{font-size:1.4em}}h3{{font-size:1.2em}}" +
               $"code{{background:{panel};color:{codeText};padding:1px 4px;border-radius:3px}}" +
               $"pre{{background:{preBg};padding:12px;border-radius:4px;overflow-x:auto}}" +
               $"pre code{{background:transparent;padding:0;color:{text}}}" +
               $"a{{color:{accent}}}" +
               $"blockquote{{border-left:3px solid {accent};margin:10px 0;padding:4px 12px;color:{muted}}}" +
               $"table{{border-collapse:collapse;width:100%;margin:8px 0}}" +
               $"th,td{{border:1px solid {border};padding:6px 10px;text-align:left}}" +
               $"th{{background:{panel}}}" +
               $"hr{{border:none;border-top:1px solid {border}}}" +
               $"ul,ol{{padding-left:24px}}";
    }

    private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Color DimColor(Color c, float factor) =>
        Color.FromArgb((int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));
}

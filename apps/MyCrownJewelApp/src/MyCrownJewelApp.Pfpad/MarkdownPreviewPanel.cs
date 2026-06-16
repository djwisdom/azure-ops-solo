using System;
using System.Drawing;
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
            string mutedHex = $"#{_currentTheme.Muted.R:X2}{_currentTheme.Muted.G:X2}{_currentTheme.Muted.B:X2}";
            string bgHex    = $"#{_currentTheme.EditorBackground.R:X2}{_currentTheme.EditorBackground.G:X2}{_currentTheme.EditorBackground.B:X2}";
            html = $"<html><body style='color:{mutedHex};font-family:sans-serif;padding:20px;background:{bgHex}'><p>No content</p></body></html>";
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

    // Delegate to the shared MarkdownRenderer — keeps a single rendering pipeline
    private static string ConvertToHtml(string md, Theme theme) =>
        MarkdownRenderer.ConvertToHtml(md, theme);
}

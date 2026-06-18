using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyCrownJewelApp.Pfpad;

public sealed class MarkdownPreviewPanel : Panel
{
    private WebView2? _webView;
    private Panel? _fallbackPanel;
    private bool _webViewReady;
    private string _pendingHtml = "";

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

        // Async WebView2 init — fires after constructor returns
        InitWebViewAsync();
    }

    private async void InitWebViewAsync()
    {
        try
        {
            string profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyCrownJewelApp", "MarkdownPreviewProfile");

            var env = await CoreWebView2Environment.CreateAsync(null, profileDir);

            _webView = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_webView);
            _webView.SendToBack(); // header stays on top

            await _webView.EnsureCoreWebView2Async(env);

            // Markdown preview: no script, no nav, no context menu
            _webView.CoreWebView2.Settings.IsScriptEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                // Allow only initial navigate-to-string; block all external links
                if (args.Uri != null && !args.Uri.StartsWith("data:") && args.Uri != "about:blank")
                    args.Cancel = true;
            };

            _webViewReady = true;

            if (!string.IsNullOrEmpty(_pendingHtml))
                _webView.NavigateToString(_pendingHtml);
        }
        catch (Exception ex) when (
            ex is COMException ||
            ex is WebView2RuntimeNotFoundException ||
            ex is DllNotFoundException ||
            ex is FileNotFoundException)
        {
            if (IsHandleCreated)
                BeginInvoke(() => ShowFallback("Markdown preview requires the WebView2 runtime.\nInstall Microsoft Edge 85+ to enable this panel."));
            else
                HandleCreated += (_, _) => ShowFallback("Markdown preview requires the WebView2 runtime.\nInstall Microsoft Edge 85+ to enable this panel.");
        }
        catch
        {
            // ignore unexpected errors — preview simply stays blank
        }
    }

    private void ShowFallback(string message)
    {
        _webView?.Dispose();
        _webView = null;

        _fallbackPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        var label = new Label
        {
            Text = message,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(140, 140, 140),
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(20)
        };
        _fallbackPanel.Controls.Add(label);
        Controls.Add(_fallbackPanel);
        _fallbackPanel.SendToBack();
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

        if (_webViewReady && _webView != null)
            _webView.NavigateToString(html);
        else
            _pendingHtml = html;
    }

    public void SetTheme(Theme theme)
    {
        _currentTheme = theme;
        BackColor = theme.PanelBackground;
        _closeButton.ForeColor = theme.Text;
        _headerPanel.BackColor = theme.MenuBackground;
        _headerText.ForeColor = theme.Text;

        if (_fallbackPanel != null)
        {
            _fallbackPanel.BackColor = theme.PanelBackground;
            foreach (Control c in _fallbackPanel.Controls)
                if (c is Label l) l.ForeColor = theme.Muted;
        }

        if (!string.IsNullOrEmpty(_currentSource))
            RenderMarkdown(_currentSource, null);
    }

    private static string ConvertToHtml(string md, Theme theme) =>
        MarkdownRenderer.ConvertToHtml(md, theme);
}

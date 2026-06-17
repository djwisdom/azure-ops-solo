using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Displays MANUAL.md or ARCHITECTURE.md rendered as themed HTML inside a WebBrowser control.
///
/// Manual loading priority (highest first):
///   1. Embedded resource "MyCrownJewelApp.Pfpad.Resources.MANUAL.md" — always
///      available inside the compiled binary regardless of install location.
///      This is the authoritative source for release/installer builds.
///   2. File next to the executable — "MANUAL.md" in AppContext.BaseDirectory.
///      Only used in debug builds to allow easy iteration without recompiling.
///
/// External links in the manual open in the system default browser;
/// all in-app navigation is cancelled for safety.
/// </summary>
public sealed partial class UserManualDialog : Form
{
    // ── Win32 constants for drag-move and edge-resize ────────────────────────
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCHITTEST     = 0x0084;
    private const int HTCAPTION        = 2;
    private const int HTLEFT           = 10;
    private const int HTRIGHT          = 11;
    private const int HTTOP            = 12;
    private const int HTTOPLEFT        = 13;
    private const int HTTOPRIGHT       = 14;
    private const int HTBOTTOM         = 15;
    private const int HTBOTTOMLEFT     = 16;
    private const int HTBOTTOMRIGHT    = 17;
    private const int ResizeBorder     = 6;   // px hit-area at each edge

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")] private static partial int  SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    private readonly WebBrowser _browser;
    private readonly Button     _manualTab;
    private readonly Button     _archTab;
    private readonly Button     _terminalTab;
    private readonly Button     _closeBtn;
    private readonly Panel      _titleBar;
    private enum ActiveTab { Manual, Architecture, Terminal }
    private ActiveTab _activeTab = ActiveTab.Manual;
    private Theme _theme;

    public UserManualDialog()
    {
        _theme = ThemeManager.Instance.CurrentTheme;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.CenterParent;
        ShowInTaskbar   = false;
        ShowIcon        = false;
        Size            = new Size(940, 720);
        MinimumSize     = new Size(660, 500);
        BackColor       = _theme.Background;
        ForeColor       = _theme.Text;
        DoubleBuffered  = true;

        // ── Title bar ──────────────────────────────────────────────────────────
        _titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            BackColor = _theme.PanelBackground,
        };
        // Dragging: only on background of title bar, not on child controls
        _titleBar.MouseDown += TitleBar_MouseDown;

        var titleLabel = new Label
        {
            Text      = "📄  Personal Flip Pad — Documentation",
            Font      = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            AutoSize  = true,
            Location  = new Point(12, 14),
        };
        _titleBar.Controls.Add(titleLabel);

        // Close button — anchored to the right; re-positioned on resize
        _closeBtn = new Button
        {
            Text      = "✕",
            Font      = new Font("Segoe UI", 11, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = _theme.Muted,
            Size      = new Size(40, 32),
            Cursor    = Cursors.Hand,
            TabStop   = false,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(196, 43, 28) }
        };
        _closeBtn.MouseEnter += (_, _) => _closeBtn.ForeColor = Color.White;
        _closeBtn.MouseLeave += (_, _) => _closeBtn.ForeColor = _theme.Muted;
        _closeBtn.Click      += (_, _) => Close();
        _titleBar.Controls.Add(_closeBtn);

        // Reposition close button whenever the title bar is resized
        _titleBar.SizeChanged += (_, _) =>
            _closeBtn.Location = new Point(_titleBar.Width - _closeBtn.Width - 4, 7);

        // ── Tab strip ──────────────────────────────────────────────────────────
        var tabStrip = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 32,
            BackColor = _theme.MenuBackground,
        };

        _manualTab   = CreateTabButton("📖  User Manual",           active: true);
        _archTab     = CreateTabButton("🏗  Architecture & Design",  active: false);
        _terminalTab = CreateTabButton("🖥  Terminal Guide",          active: false);
        _manualTab.Location   = new Point(0, 0);
        _archTab.Location     = new Point(_manualTab.Width, 0);
        _terminalTab.Location = new Point(_manualTab.Width + _archTab.Width, 0);
        _manualTab.Height = _archTab.Height = _terminalTab.Height = 32;

        _manualTab.Click   += (_, _) => SwitchTab(ActiveTab.Manual);
        _archTab.Click     += (_, _) => SwitchTab(ActiveTab.Architecture);
        _terminalTab.Click += (_, _) => SwitchTab(ActiveTab.Terminal);

        tabStrip.Controls.Add(_manualTab);
        tabStrip.Controls.Add(_archTab);
        tabStrip.Controls.Add(_terminalTab);

        // Draw active-tab underline
        tabStrip.Paint += (_, e) =>
        {
            var activeBtn = _activeTab switch
            {
                ActiveTab.Architecture => _archTab,
                ActiveTab.Terminal     => _terminalTab,
                _                     => _manualTab,
            };
            using var pen = new Pen(_theme.Accent, 2);
            int y = tabStrip.Height - 2;
            e.Graphics.DrawLine(pen, activeBtn.Left, y, activeBtn.Right, y);
        };
        _manualTab.Click   += (_, _) => tabStrip.Invalidate();
        _archTab.Click     += (_, _) => tabStrip.Invalidate();
        _terminalTab.Click += (_, _) => tabStrip.Invalidate();

        // ── Resize grip (bottom-right corner) ─────────────────────────────────
        var grip = new Panel
        {
            Size      = new Size(16, 16),
            Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.Transparent,
            Cursor    = Cursors.SizeNWSE,
        };
        grip.Paint += (_, e) =>
        {
            // Draw three diagonal dots like a standard size grip
            using var pen = new Pen(_theme.Muted, 1);
            for (int i = 1; i <= 3; i++)
            {
                int offset = i * 4;
                e.Graphics.DrawLine(pen,
                    grip.Width - offset,     grip.Height - 1,
                    grip.Width - 1,          grip.Height - offset);
            }
        };
        grip.MouseDown += (_, _) =>
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTBOTTOMRIGHT, 0);
        };

        // ── Browser ────────────────────────────────────────────────────────────
        _browser = new WebBrowser
        {
            Dock                           = DockStyle.Fill,
            AllowNavigation                = true,   // programmatic DocumentText re-loads must be allowed; external links are blocked via the Navigating event instead
            AllowWebBrowserDrop            = false,
            IsWebBrowserContextMenuEnabled = false,
            WebBrowserShortcutsEnabled     = false,
            ScrollBarsEnabled              = true,
        };
        _browser.Navigating += Browser_Navigating;

        // ── Assemble ───────────────────────────────────────────────────────────
        Controls.Add(_browser);
        Controls.Add(grip);
        Controls.Add(tabStrip);
        Controls.Add(_titleBar);

        // ── Border ─────────────────────────────────────────────────────────────
        Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        Load         += (_, _) => RenderContent();
        HandleCreated += (_, _) =>
        {
            if (!_theme.IsLight) NativeThemed.ApplyDarkModeToWindow(Handle);
        };

        // Position grip after layout
        SizeChanged += (_, _) =>
            grip.Location = new Point(Width - grip.Width - 2, Height - grip.Height - 2);
    }

    // ── Resize from all edges via WM_NCHITTEST ────────────────────────────────
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            var pos    = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF));
            bool left  = pos.X <= ResizeBorder;
            bool right = pos.X >= Width  - ResizeBorder;
            bool top   = pos.Y <= ResizeBorder;
            bool bot   = pos.Y >= Height - ResizeBorder;

            m.Result = (IntPtr)(left && top  ? HTTOPLEFT     :
                                right && top  ? HTTOPRIGHT    :
                                left && bot   ? HTBOTTOMLEFT  :
                                right && bot  ? HTBOTTOMRIGHT :
                                left          ? HTLEFT        :
                                right         ? HTRIGHT       :
                                top           ? HTTOP         :
                                bot           ? HTBOTTOM      :
                                (int)m.Result);
            return;
        }
        base.WndProc(ref m);
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        // Only drag on left-button and only when not clicking a child control
        if (e.Button == MouseButtons.Left && _titleBar.GetChildAtPoint(e.Location) == null)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }

    private Button CreateTabButton(string text, bool active) => new()
    {
        Text      = text,
        Font      = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.Transparent,
        ForeColor = active ? _theme.Text : _theme.Muted,
        Width     = 200,
        Cursor    = Cursors.Hand,
        TabStop   = false,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.Transparent }
    };

    private void SwitchTab(ActiveTab tab)
    {
        if (_activeTab == tab) return;
        _activeTab = tab;
        UpdateTabState();
        RenderContent();
    }

    private void UpdateTabState()
    {
        SetTabActive(_manualTab,   _activeTab == ActiveTab.Manual);
        SetTabActive(_archTab,     _activeTab == ActiveTab.Architecture);
        SetTabActive(_terminalTab, _activeTab == ActiveTab.Terminal);
    }

    private void SetTabActive(Button btn, bool active)
    {
        btn.ForeColor = active ? _theme.Text : _theme.Muted;
        btn.Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular);
    }

    private void RenderContent()
    {
        string markdown = _activeTab switch
        {
            ActiveTab.Architecture => LoadArchitectureMarkdown(),
            ActiveTab.Terminal     => LoadTerminalMarkdown(),
            _                     => LoadManualMarkdown(),
        };
        string html = MarkdownRenderer.ConvertToHtml(markdown, _theme);
        _browser.DocumentText = html;
    }

    private static string LoadManualMarkdown()
    {
#if DEBUG
        string localPath = Path.Combine(AppContext.BaseDirectory, "MANUAL.md");
        if (File.Exists(localPath))
            try { return File.ReadAllText(localPath); } catch { }
#endif
        return LoadEmbeddedResource("MyCrownJewelApp.Pfpad.Resources.MANUAL.md");
    }

    private static string LoadArchitectureMarkdown()
    {
#if DEBUG
        string localPath = Path.Combine(AppContext.BaseDirectory, "ARCHITECTURE.md");
        if (File.Exists(localPath))
            try { return File.ReadAllText(localPath); } catch { }
#endif
        return LoadEmbeddedResource("MyCrownJewelApp.Pfpad.Resources.ARCHITECTURE.md");
    }

    private static string LoadTerminalMarkdown()
    {
#if DEBUG
        string localPath = Path.Combine(AppContext.BaseDirectory, "TERMINAL.md");
        if (File.Exists(localPath))
            try { return File.ReadAllText(localPath); } catch { }
#endif
        return LoadEmbeddedResource("MyCrownJewelApp.Pfpad.Resources.TERMINAL.md");
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            return $"# Document Not Found\n\nEmbedded resource `{resourceName}` could not be located.\n\nThis is a build configuration issue — please report it.";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Browser_Navigating(object? sender, WebBrowserNavigatingEventArgs e)
    {
        string url = e.Url?.ToString() ?? "";
        if (url == "about:blank" || string.IsNullOrEmpty(url))
            return;

        e.Cancel = true;
        if (e.Url?.Scheme is "http" or "https")
        {
            try
            {
                if (SecurityEnforcementService.IsUrlSchemeAllowed(url))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

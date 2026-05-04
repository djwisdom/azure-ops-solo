using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class NotificationToastForm : Form
{
    private readonly FeedItem _item;
    private readonly System.Windows.Forms.Timer _autoCloseTimer;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly Theme _theme;
    private bool _closing;
    private float _opacity = 0f;
    private const int DisplaySeconds = 12;
    private const int FadeStepMs = 30;
    private const float FadeDelta = 0.06f;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public NotificationToastForm(FeedItem item)
    {
        _item = item;
        _theme = ThemeManager.Instance.CurrentTheme;

        ShowInTaskbar = false;
        ShowIcon = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(400, 130);
        TopMost = true;

        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        _autoCloseTimer = new System.Windows.Forms.Timer { Interval = DisplaySeconds * 1000 };
        _autoCloseTimer.Tick += (s, e) => BeginClose();

        _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeStepMs };
        _fadeTimer.Tick += FadeTick;

        BuildUI();
        PositionNearNotifyIcon();
    }

    private void BuildUI()
    {
        var sourceColor = GetSourceColor(_item.Source);

        // Border panel
        var borderPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.Border,
            Padding = new Padding(1)
        };

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.Background,
            Padding = new Padding(12, 8, 12, 8)
        };

        // Source color stripe (top)
        var stripe = new Panel
        {
            Dock = DockStyle.Top,
            Height = 3,
            BackColor = sourceColor
        };

        // Source label
        var sourceLbl = new Label
        {
            Text = _item.SourceLabel,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = sourceColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(12, 14)
        };

        // Close button
        var closeBtn = new Button
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 12, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = _theme.ButtonHoverBackground },
            BackColor = Color.Transparent,
            ForeColor = _theme.Muted,
            Size = new Size(26, 26),
            Location = new Point(Width - 34, 8),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        closeBtn.Click += (s, e) => BeginClose();
        body.Controls.Add(closeBtn);

        // Title
        var titleLbl = new Label
        {
            Text = _item.Title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(Width - 60, 40),
            Location = new Point(12, 34),
            TextAlign = ContentAlignment.TopLeft
        };
        body.Controls.Add(titleLbl);

        // Summary
        var summary = _item.Summary;
        if (summary.Length > 120) summary = summary[..117] + "...";
        var summaryLbl = new Label
        {
            Text = summary,
            Font = new Font("Segoe UI", 8, FontStyle.Regular),
            ForeColor = _theme.Muted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(Width - 60, 36),
            Location = new Point(12, 76),
            TextAlign = ContentAlignment.TopLeft
        };
        body.Controls.Add(summaryLbl);

        // Click to open
        body.MouseClick += (s, e) => OpenLink();
        titleLbl.MouseClick += (s, e) => OpenLink();
        summaryLbl.MouseClick += (s, e) => OpenLink();

        // Pause auto-close on hover
        body.MouseEnter += (s, e) => { _autoCloseTimer.Stop(); };
        body.MouseLeave += (s, e) => { if (!_closing) _autoCloseTimer.Start(); };
        titleLbl.MouseEnter += (s, e) => { _autoCloseTimer.Stop(); };
        titleLbl.MouseLeave += (s, e) => { if (!_closing) _autoCloseTimer.Start(); };

        borderPanel.Controls.Add(body);
        borderPanel.Controls.Add(stripe);

        Controls.Add(borderPanel);

        Paint += (s, e) =>
        {
            using var p = new Pen(sourceColor, 2);
            var r = ClientRectangle;
            e.Graphics.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        };
    }

    private void PositionNearNotifyIcon()
    {
        var screen = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(screen.Right - Width - 16, screen.Bottom - Height - 16);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Opacity = 0;
        _fadeTimer.Start();
        _autoCloseTimer.Start();
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        _opacity += FadeDelta;
        if (_opacity >= 1.0f)
        {
            Opacity = 1.0;
            _fadeTimer.Stop();
        }
        else
        {
            Opacity = _opacity;
        }
    }

    private void BeginClose()
    {
        if (_closing) return;
        _closing = true;
        _autoCloseTimer.Stop();
        _fadeTimer.Stop();

        var fadeOut = new System.Windows.Forms.Timer { Interval = FadeStepMs };
        fadeOut.Tick += (s, e) =>
        {
            _opacity -= FadeDelta * 1.5f;
            if (_opacity <= 0f || IsDisposed)
            {
                Opacity = 0;
                fadeOut.Stop();
                fadeOut.Dispose();
                Close();
            }
            else
            {
                Opacity = _opacity;
            }
        };
        fadeOut.Start();
    }

    private void OpenLink()
    {
        if (_item.Link is not null)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(_item.Link.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
        }
        BeginClose();
    }

    private static Color GetSourceColor(FeedSource source) => source switch
    {
        FeedSource.HackerNews => Color.FromArgb(255, 102, 0),
        FeedSource.Reddit => Color.FromArgb(255, 69, 0),
        FeedSource.GitHubReleases => Color.FromArgb(36, 137, 219),
        FeedSource.Medium => Color.FromArgb(0, 158, 111),
        FeedSource.StackOverflow => Color.FromArgb(244, 128, 36),
        FeedSource.BBCNews => Color.FromArgb(187, 22, 22),
        FeedSource.Reuters => Color.FromArgb(247, 148, 30),
        FeedSource.NYT => Color.FromArgb(51, 51, 51),
        _ => Color.Gray
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoCloseTimer?.Dispose();
            _fadeTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

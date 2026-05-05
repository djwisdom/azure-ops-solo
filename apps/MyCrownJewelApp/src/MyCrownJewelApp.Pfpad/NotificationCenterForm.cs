using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class NotificationCenterForm : Form
{
    private readonly NotificationFeedService _feed;
    private readonly ListView _listView;
    private readonly Button _markAllReadBtn;
    private readonly Button _refreshBtn;
    private readonly Label _titleLabel;
    private Theme _theme;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    public NotificationCenterForm(NotificationFeedService feed)
    {
        _feed = feed;
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "Notification Center";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        Size = new Size(480, 540);
        MinimumSize = new Size(360, 300);

        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        // Title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = _theme.PanelBackground,
            Cursor = Cursors.SizeAll
        };
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        };

        _titleLabel = new Label
        {
            Text = "Notification Center",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 8)
        };
        titleBar.Controls.Add(_titleLabel);

        var closeBtn = new Button
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 13, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.Transparent,
            ForeColor = _theme.Muted,
            Size = new Size(30, 30),
            Location = new Point(Width - 36, 3),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.FlatAppearance.MouseOverBackColor = _theme.Accent;
        titleBar.Controls.Add(closeBtn);

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = _theme.Background,
            Padding = new Padding(10, 6, 10, 4)
        };

        _markAllReadBtn = new Button
        {
            Text = "Mark All Read",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9),
            Size = new Size(110, 28),
            Location = new Point(0, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _markAllReadBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_theme.Accent);
        _markAllReadBtn.Click += (s, e) => { _feed.MarkAllAsRead(); RefreshList(); };
        toolbar.Controls.Add(_markAllReadBtn);

        _refreshBtn = new Button
        {
            Text = "\u21BB Refresh",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.Transparent,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            Size = new Size(90, 28),
            Location = new Point(130, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _refreshBtn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        _refreshBtn.Click += async (s, e) =>
        {
            _refreshBtn.Enabled = false;
            _refreshBtn.Text = "Loading...";
            await _feed.FetchAllAsync();
            RefreshList();
            _refreshBtn.Text = "\u21BB Refresh";
            _refreshBtn.Enabled = true;
        };
        toolbar.Controls.Add(_refreshBtn);

        var settingsBtn = new Button
        {
            Text = "\u2699",
            Font = new Font("Segoe UI", 11),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.Transparent,
            ForeColor = _theme.Text,
            Size = new Size(28, 28),
            Location = new Point(toolbar.Width - 34, 0),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        settingsBtn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        settingsBtn.Click += (s, e) =>
        {
            using var dlg = new NotificationSettingsForm(_feed);
            dlg.ShowDialog(this);
            RefreshList();
        };
        toolbar.Controls.Add(settingsBtn);

        // List view
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.List,
            ShowItemToolTips = true,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.Background,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            MultiSelect = false,
            FullRowSelect = true
        };
        _listView.MouseClick += ListView_MouseClick;
        _listView.MouseDoubleClick += ListView_MouseDoubleClick;

        // Loading indicator
        var loadingLabel = new Label
        {
            Text = "Loading feeds...",
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            ForeColor = _theme.Muted,
            BackColor = _theme.Background,
            AutoSize = true,
            Location = new Point(180, 240)
        };

        Controls.Add(_listView);
        Controls.Add(toolbar);
        Controls.Add(titleBar);

        _feed.OnItemsUpdated += OnFeedUpdated;

        // Populate once if already loaded
        if (_feed.AllItems.Count > 0)
            RefreshList();
    }

    private void OnFeedUpdated()
    {
        if (IsHandleCreated && !IsDisposed)
            BeginInvoke(RefreshList);
    }

    private void RefreshList()
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        var items = _feed.AllItems;
        if (items.Count == 0)
        {
            _listView.Items.Add("No notifications yet.");
        }
        else
        {
            foreach (var item in items)
            {
                string prefix = item.IsRead ? "  " : "\u25CF ";
                string display = $"{prefix}[{item.SourceLabel}] {item.Title}";
                if (display.Length > 120)
                    display = display[..117] + "...";
                var lvi = new ListViewItem
                {
                    Text = display,
                    Tag = item,
                    ToolTipText = item.Summary
                };
                _listView.Items.Add(lvi);
            }
        }

        _titleLabel.Text = $"Notification Center ({(items.Count(i => !i.IsRead))} new)";
        _listView.EndUpdate();
    }

    private void ListView_MouseClick(object? sender, MouseEventArgs e)
    {
        var hit = _listView.HitTest(e.Location);
        if (hit.Item?.Tag is FeedItem item)
        {
            _feed.MarkAsRead(item);
            RefreshList();
        }
    }

    private void ListView_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        var hit = _listView.HitTest(e.Location);
        if (hit.Item?.Tag is FeedItem item && item.Link is not null)
        {
            try { Process.Start(new ProcessStartInfo(item.Link.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
        }
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

    public void UpdateTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        _listView.BackColor = theme.Background;
        _listView.ForeColor = theme.Text;
        foreach (Control c in Controls)
        {
            if (c is Panel p)
            {
                p.BackColor = theme.PanelBackground;
                foreach (Control cc in p.Controls)
                {
                    if (cc is Label l)
                    {
                        l.ForeColor = theme.Text;
                        l.BackColor = Color.Transparent;
                    }
                    else if (cc is Button b)
                    {
                        b.ForeColor = theme.Text;
                        if (b != _markAllReadBtn)
                            b.BackColor = Color.Transparent;
                    }
                }
            }
        }
        Invalidate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _feed.OnItemsUpdated -= OnFeedUpdated;
        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var p = new Pen(_theme.Border, 1);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_feed.AllItems.Count == 0)
            _ = _feed.FetchAllAsync();
    }
}

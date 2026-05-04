using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GitPanel : UserControl
{
    private readonly GitService _git;
    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _branchLabel;
    private readonly ToolStripDropDownButton _branchDropdown;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _closeButton;

    private readonly Panel _bodyPanel;
    private readonly ListBox _statusList;
    private readonly ListBox _commitList;
    private readonly TextBox _commitMessage;
    private readonly Button _commitBtn;
    private readonly Button _stageAllBtn;
    private readonly Button _fetchBtn;
    private readonly Button _pullBtn;
    private readonly Button _pushBtn;
    private readonly Label _statusHeader;
    private readonly Label _commitHeader;
    private readonly Label _noRepoLabel;

    private List<StatusEntry> _staged = new();
    private List<StatusEntry> _unstaged = new();
    private List<StatusEntry> _untracked = new();

    public event Action<string>? FileOpenRequested;
    public event Action? CloseRequested;

    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public GitPanel(GitService git)
    {
        _git = git;
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(200, 200);

        var theme = ThemeManager.Instance.CurrentTheme;

        _branchLabel = new ToolStripLabel("Git")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Margin = new Padding(4, 0, 0, 0)
        };

        _branchDropdown = new ToolStripDropDownButton
        {
            Text = "(no repo)",
            Font = new Font("Segoe UI", 8),
            AutoSize = false,
            Width = 130,
            Height = 22,
            DisplayStyle = ToolStripItemDisplayStyle.Text
        };
        _branchDropdown.DropDownOpening += BranchDropdown_DropDownOpening;
        _branchDropdown.DropDownItemClicked += BranchDropdown_ItemClicked;

        _refreshButton = new ToolStripButton
        {
            Text = "\u21BB",
            Font = new Font("Segoe UI", 10),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Refresh"
        };
        _refreshButton.Click += (s, e) => RefreshStatus();

        _closeButton = new ToolStripButton
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right,
            AutoSize = false,
            Width = 22,
            Height = 22
        };
        _closeButton.Click += (s, e) => CloseRequested?.Invoke();

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(2, 0, 0, 0),
            AutoSize = false,
            Height = 24
        };
        _headerStrip.Items.Add(_branchLabel);
        _headerStrip.Items.Add(_branchDropdown);
        _headerStrip.Items.Add(_refreshButton);
        _headerStrip.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
        _headerStrip.Items.Add(_closeButton);

        // No repo label (shown when not in a git repo)
        _noRepoLabel = new Label
        {
            Text = "Not in a Git repository.\nOpen a file inside a repo\nto see git status.",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            Visible = false
        };

        // Body panel (scrollable)
        _bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(6, 4, 6, 4)
        };

        int y = 0;

        _statusHeader = new Label
        {
            Text = "Changes",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(4, y)
        };

        _stageAllBtn = new Button
        {
            Text = "Stage All",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 22),
            Location = new Point(120, y - 1),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _stageAllBtn.Click += (s, e) => { _git.StageAll(); RefreshStatus(); };

        y += 24;
        _statusList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ItemHeight = 22,
            Cursor = Cursors.Hand
        };
        _statusList.DrawItem += StatusList_DrawItem;
        _statusList.MouseClick += StatusList_MouseClick;
        _statusList.MouseDoubleClick += StatusList_DoubleClick;
        _statusList.MeasureItem += (s, e) => e.ItemHeight = 24;

        y += 4;
        _commitBtn = new Button
        {
            Text = "Commit",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(80, 26),
            Location = new Point(4, y),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _commitBtn.Click += Commit_Click;

        _commitMessage = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(90, y),
            Size = new Size(180, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = ""
        };
        _commitMessage.TextChanged += (s, e) => _commitBtn.Enabled = _commitMessage.Text.Trim().Length > 0;

        y += 32;
        _commitHeader = new Label
        {
            Text = "Recent Commits",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(4, y)
        };

        y += 20;
        _commitList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ItemHeight = 40
        };
        _commitList.DrawItem += CommitList_DrawItem;
        _commitList.MeasureItem += (s, e) => e.ItemHeight = 42;

        y += 4;
        _fetchBtn = new Button
        {
            Text = "Fetch",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(60, 24),
            Location = new Point(4, y),
            Cursor = Cursors.Hand
        };
        _fetchBtn.Click += async (s, e) => { SetSyncEnabled(false); _git.Fetch(); RefreshStatus(); SetSyncEnabled(true); };

        _pullBtn = new Button
        {
            Text = "Pull",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(60, 24),
            Location = new Point(70, y),
            Cursor = Cursors.Hand
        };
        _pullBtn.Click += async (s, e) =>
        {
            SetSyncEnabled(false);
            var (ok, msg) = _git.Pull();
            if (!ok) ThemedMessageBox.Show(msg, "Pull Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
            SetSyncEnabled(true);
        };

        _pushBtn = new Button
        {
            Text = "Push",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(60, 24),
            Location = new Point(136, y),
            Cursor = Cursors.Hand
        };
        _pushBtn.Click += async (s, e) =>
        {
            SetSyncEnabled(false);
            var (ok, msg) = _git.Push();
            if (!ok) ThemedMessageBox.Show(msg, "Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
            SetSyncEnabled(true);
        };

        _bodyPanel.Controls.AddRange(new Control[] {
            _statusHeader, _stageAllBtn, _statusList, _commitMessage, _commitBtn,
            _commitHeader, _commitList, _fetchBtn, _pullBtn, _pushBtn
        });

        Controls.Add(_noRepoLabel);
        Controls.Add(_bodyPanel);
        Controls.Add(_headerStrip);

        _git.OnRepoChanged += OnRepoChanged;
        _git.OnError += (msg) => BeginInvoke(() => ThemedMessageBox.Show(msg, "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning));

        SetTheme(theme);
        RefreshStatus();
    }

    private void SetSyncEnabled(bool enabled)
    {
        BeginInvoke(() => { _fetchBtn.Enabled = enabled; _pullBtn.Enabled = enabled; _pushBtn.Enabled = enabled; });
    }

    private void OnRepoChanged()
    {
        if (IsDisposed) return;
        BeginInvoke(RefreshStatus);
    }

    public void RefreshStatus()
    {
        if (_git.IsActive)
        {
            _noRepoLabel.Visible = false;
            _bodyPanel.Visible = true;
            var (staged, unstaged, untracked) = _git.GetStatus();
            _staged = staged;
            _unstaged = unstaged;
            _untracked = untracked;

            var items = new List<string>();
            if (staged.Count > 0)
            {
                items.Add($"\u2501 Staged ({staged.Count}) \u2501");
                foreach (var s in staged) items.Add($"  [{s.StateLabel}] {s.Path}");
            }
            if (unstaged.Count > 0)
            {
                items.Add($"\u2501 Unstaged ({unstaged.Count}) \u2501");
                foreach (var s in unstaged) items.Add($"  [{s.StateLabel}] {s.Path}");
            }
            if (untracked.Count > 0)
            {
                items.Add($"\u2501 Untracked ({untracked.Count}) \u2501");
                foreach (var s in untracked) items.Add($"  [?] {s.Path}");
            }
            if (items.Count == 0) items.Add("(no changes)");

            _statusList.Items.Clear();
            foreach (var item in items) _statusList.Items.Add(item);
            _statusHeader.Text = $"Changes ({staged.Count + unstaged.Count + untracked.Count})";

            _branchDropdown.Text = _git.CurrentBranch ?? "(detached)";
            _commitBtn.Enabled = _commitMessage.Text.Trim().Length > 0 && staged.Count > 0;

            // Commit log
            var log = _git.GetLog(30);
            _commitList.Items.Clear();
            foreach (var c in log) _commitList.Items.Add(c);
        }
        else
        {
            _noRepoLabel.Visible = true;
            _bodyPanel.Visible = false;
            _branchDropdown.Text = "(no repo)";
            _statusList.Items.Clear();
            _commitList.Items.Clear();
        }

        LayoutControls();
    }

    private void LayoutControls()
    {
        if (_statusList is null || _commitList is null || _commitMessage is null) return;
        int w = _bodyPanel.ClientSize.Width - 12;
        if (w < 100) w = 100;

        _statusList.Size = new Size(w, Math.Min(_statusList.Items.Count * 24 + 4, 300));
        _commitMessage.Size = new Size(w - 94, 24);
        _commitList.Location = new Point(4, _commitList.Location.Y);
        _commitList.Size = new Size(w, Math.Max(60, _bodyPanel.ClientSize.Height - _commitList.Top - 4));
    }

    private void CommitList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _commitList.Items.Count) return;
        if (_commitList.Items[e.Index] is not CommitEntry entry) return;

        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        using var shaFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
        using var msgFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        using var metaFont = new Font("Segoe UI", 7, FontStyle.Regular);

        TextRenderer.DrawText(e.Graphics, entry.Sha, shaFont,
            new Point(rect.X + 4, rect.Y + 2), theme.Accent, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, entry.Message, msgFont,
            new Point(rect.X + 60, rect.Y + 2), theme.Text,
            TextFormatFlags.NoPadding | TextFormatFlags.WordEllipsis);
        TextRenderer.DrawText(e.Graphics, $"{entry.Author}  {entry.Date}", metaFont,
            new Point(rect.X + 60, rect.Y + 22), theme.Muted, TextFormatFlags.NoPadding);

        using var sepPen = new Pen(theme.Border);
        e.Graphics.DrawLine(sepPen, rect.X, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
    }

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _branchLabel.ForeColor = theme.Muted;
        _branchDropdown.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        _refreshButton.ForeColor = theme.Text;

        _bodyPanel.BackColor = theme.MenuBackground;
        _statusHeader.ForeColor = theme.Text;
        _statusList.BackColor = theme.MenuBackground;
        _statusList.ForeColor = theme.Text;
        _commitHeader.ForeColor = theme.Text;
        _commitList.BackColor = theme.MenuBackground;
        _commitList.ForeColor = theme.Text;
        _commitMessage.BackColor = theme.EditorBackground;
        _commitMessage.ForeColor = theme.Text;
        _commitBtn.BackColor = theme.Accent;
        _commitBtn.ForeColor = Color.White;
        _commitBtn.FlatAppearance.BorderSize = 0;
        _stageAllBtn.BackColor = theme.Background;
        _stageAllBtn.ForeColor = theme.Text;
        _stageAllBtn.FlatAppearance.BorderColor = theme.Muted;
        _fetchBtn.BackColor = theme.Background;
        _fetchBtn.ForeColor = theme.Text;
        _fetchBtn.FlatAppearance.BorderColor = theme.Muted;
        _pullBtn.BackColor = theme.Background;
        _pullBtn.ForeColor = theme.Text;
        _pullBtn.FlatAppearance.BorderColor = theme.Muted;
        _pushBtn.BackColor = theme.Accent;
        _pushBtn.ForeColor = Color.White;
        _pushBtn.FlatAppearance.BorderSize = 0;
        _noRepoLabel.ForeColor = theme.Muted;
        _noRepoLabel.BackColor = theme.MenuBackground;

        foreach (var c in new[] { _stageAllBtn, _fetchBtn, _pullBtn })
            c.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _commitBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent);
        _pushBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent);

        ApplyScrollbarTheme(_statusList.Handle);
        ApplyScrollbarTheme(_commitList.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : null, null);
    }

    private void StatusList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _statusList.Items.Count) return;
        var text = _statusList.Items[e.Index].ToString() ?? "";
        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;

        bool isHeader = text.StartsWith('\u2501');
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        Color textColor;
        Font font;

        if (isHeader)
        {
            textColor = theme.Muted;
            font = new Font("Segoe UI", 8, FontStyle.Bold);
        }
        else
        {
            textColor = isSelected ? theme.Text : theme.Text;
            font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            // Color the status indicator
            if (text.Contains("[M]")) textColor = Color.FromArgb(200, 180, 50);
            else if (text.Contains("[A]")) textColor = Color.FromArgb(60, 180, 75);
            else if (text.Contains("[D]")) textColor = Color.FromArgb(200, 60, 60);
            else if (text.Contains("[?]")) textColor = theme.Muted;
        }

        TextRenderer.DrawText(e.Graphics, text, font, rect with { X = rect.X + 4 },
            textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
        font.Dispose();
    }

    private void StatusList_MouseClick(object? sender, MouseEventArgs e)
    {
        int idx = _statusList.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _statusList.Items.Count) return;
        var text = _statusList.Items[idx].ToString() ?? "";

        // Toggle stage/unstage on click
        var entry = FindStatusEntry(text);
        if (entry is null) return;

        if (entry.IsStaged)
        {
            _git.Unstage(entry.Path);
            ThemedMessageBox.Show($"Unstaged: {entry.Path}", "Git", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _git.Stage(entry.Path);
        }
        RefreshStatus();
    }

    private void StatusList_DoubleClick(object? sender, MouseEventArgs e)
    {
        int idx = _statusList.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _statusList.Items.Count) return;
        var text = _statusList.Items[idx].ToString() ?? "";
        var entry = FindStatusEntry(text);
        if (entry is not null && _git.RepoPath is not null)
        {
            var fullPath = Path.Combine(_git.RepoPath, entry.Path);
            if (File.Exists(fullPath))
                FileOpenRequested?.Invoke(fullPath);
        }
    }

    private StatusEntry? FindStatusEntry(string listText)
    {
        var trimmed = listText.TrimStart();
        if (trimmed.StartsWith('['))
        {
            var close = trimmed.IndexOf(']');
            if (close > 0)
            {
                var path = trimmed[(close + 1)..].Trim();
                return _staged.FirstOrDefault(s => s.Path == path)
                    ?? _unstaged.FirstOrDefault(s => s.Path == path)
                    ?? _untracked.FirstOrDefault(s => s.Path == path);
            }
        }
        return null;
    }

    private void Commit_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_commitMessage.Text))
        { ThemedMessageBox.Show("Enter a commit message.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_staged.Count == 0)
        { ThemedMessageBox.Show("No staged changes to commit.\nClick a file to stage it, or use 'Stage All'.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (_git.Commit(_commitMessage.Text.Trim()))
        {
            _commitMessage.Text = "";
            RefreshStatus();
        }
    }

    private void BranchDropdown_DropDownOpening(object? sender, EventArgs e)
    {
        _branchDropdown.DropDownItems.Clear();
        if (!_git.IsActive) return;

        var branches = _git.GetBranches();
        foreach (var b in branches)
        {
            var item = new ToolStripMenuItem(b.Name)
            {
                Checked = b.IsCurrent,
                Tag = b.Name
            };
            _branchDropdown.DropDownItems.Add(item);
        }
    }

    private void BranchDropdown_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem?.Tag is string name && name != _git.CurrentBranch)
        {
            if (_git.SwitchBranch(name))
                RefreshStatus();
        }
        _branchDropdown.HideDropDown();
    }

    public void RefreshRepo(string? filePath)
    {
        _git.TryOpenRepo(filePath);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutControls();
    }
}

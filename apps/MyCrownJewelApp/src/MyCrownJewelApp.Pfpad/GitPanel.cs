using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GitPanel : UserControl
{
    private readonly GitService _git;
    private readonly Panel _modernHeader;
    private readonly Label _repoNameLabel;
    private readonly Label _branchLabel;
    private readonly Button _syncButton;
    private readonly Button _moreActionsButton;

    private readonly SplitContainer _bodySplit;
    private readonly Panel _topPanel;
    private readonly DiffPanel _diffPanel;
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

    private int _selectedDiffIndex = -1;

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

        // Modern VS Code-style header
        _modernHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(8, 4, 8, 4)
        };

        _repoNameLabel = new Label
        {
            Text = "(no repo)",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 6),
            Cursor = Cursors.Hand
        };
        _repoNameLabel.Click += (s, e) => RefreshStatus(); // Refresh on click

        _branchLabel = new Label
        {
            Text = "main",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Location = new Point(0, 6),
            Cursor = Cursors.Hand
        };
        _branchLabel.Click += BranchLabel_Click; // Branch dropdown

        _syncButton = new Button
        {
            Text = "⬇️ 0 ↑ 0",
            Font = new Font("Segoe UI", 8),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(60, 24),
            Location = new Point(0, 4),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _syncButton.Click += SyncButton_Click;

        _moreActionsButton = new Button
        {
            Text = "⋯",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 24),
            Location = new Point(0, 4),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _moreActionsButton.Click += MoreActionsButton_Click;

        _modernHeader.Controls.AddRange(new Control[] { _repoNameLabel, _branchLabel, _syncButton, _moreActionsButton });

        _noRepoLabel = new Label
        {
            Text = "Not in a Git repository.\nOpen a file inside a repo\nto see git status.",
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            Visible = false
        };

        // Top panel with git controls (scrollable)
        _topPanel = new Panel
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
        _statusList.SelectedIndexChanged += StatusList_SelectedIndexChanged;
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

        _topPanel.Controls.AddRange(new Control[] {
            _statusHeader, _stageAllBtn, _statusList, _commitMessage, _commitBtn,
            _commitHeader, _commitList, _fetchBtn, _pullBtn, _pushBtn
        });

        // Diff panel
        _diffPanel = new DiffPanel();
        _diffPanel.StageRequested += (path) => { _git.Stage(path); RefreshStatus(); };
        _diffPanel.UnstageRequested += (path) => { _git.Unstage(path); RefreshStatus(); };
        _diffPanel.DiscardRequested += (path) => { _git.DiscardFile(path); RefreshStatus(); };

        // Body split: top = git controls, bottom = diff view
        _diffPanel.Dock = DockStyle.Fill;
        _bodySplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false,
            SplitterWidth = 4,
            Panel1 = { Controls = { _topPanel } },
            Panel2 = { Controls = { _diffPanel } },
            Panel2Collapsed = true
        };

        Controls.Add(_noRepoLabel);
        Controls.Add(_bodySplit);
        Controls.Add(_modernHeader);

        _git.OnRepoChanged += OnRepoChanged;
        _git.OnError += (msg) => BeginInvoke(() => ThemedMessageBox.Show(msg, "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning));

        SetTheme(theme);
        RefreshStatus();
    }

    private void BranchLabel_Click(object? sender, EventArgs e)
    {
        // Show branch dropdown context menu
        var menu = new ContextMenuStrip();
        if (_git.IsActive)
        {
            var branches = _git.GetBranches();
            foreach (var b in branches)
            {
                var item = new ToolStripMenuItem(b.Name)
                {
                    Checked = b.IsCurrent,
                    Tag = b.Name
                };
                item.Click += (s, e) =>
                {
                    if (s is ToolStripMenuItem tsmi && tsmi.Tag is string name && name != _git.CurrentBranch)
                    {
                        if (_git.SwitchBranch(name))
                            RefreshStatus();
                    }
                };
                menu.Items.Add(item);
            }
        }
        menu.Show(_branchLabel, new Point(0, _branchLabel.Height));
    }

    private void SyncButton_Click(object? sender, EventArgs e)
    {
        // Show sync options context menu
        var menu = new ContextMenuStrip();
        var fetchItem = new ToolStripMenuItem("Fetch", null, (s, e) => { _git.Fetch(); RefreshStatus(); });
        var pullItem = new ToolStripMenuItem("Pull", null, async (s, e) =>
        {
            var (ok, msg) = _git.Pull();
            if (!ok) ThemedMessageBox.Show(msg, "Pull Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
        });
        var pushItem = new ToolStripMenuItem("Push", null, async (s, e) =>
        {
            var (ok, msg) = _git.Push();
            if (!ok) ThemedMessageBox.Show(msg, "Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
        });

        menu.Items.AddRange(new ToolStripItem[] { fetchItem, pullItem, pushItem });
        menu.Show(_syncButton, new Point(0, _syncButton.Height));
    }

    private void MoreActionsButton_Click(object? sender, EventArgs e)
    {
        // Show more actions context menu
        var menu = new ContextMenuStrip();
        var refreshItem = new ToolStripMenuItem("Refresh", null, (s, e) => RefreshStatus());
        var closeItem = new ToolStripMenuItem("Close Panel", null, (s, e) => CloseRequested?.Invoke());

        menu.Items.AddRange(new ToolStripItem[] { refreshItem, new ToolStripSeparator(), closeItem });
        menu.Show(_moreActionsButton, new Point(0, _moreActionsButton.Height));
    }

    private string GetStatusIcon(FileStatus state)
    {
        if (state.HasFlag(FileStatus.NewInIndex)) return "🆕";      // Added
        if (state.HasFlag(FileStatus.ModifiedInIndex)) return "📝"; // Modified staged
        if (state.HasFlag(FileStatus.DeletedFromIndex)) return "❌"; // Deleted staged
        if (state.HasFlag(FileStatus.RenamedInIndex)) return "📋";   // Renamed staged
        if (state.HasFlag(FileStatus.ModifiedInWorkdir)) return "🔧"; // Modified
        if (state.HasFlag(FileStatus.DeletedFromWorkdir)) return "🗑️"; // Deleted
        if (state.HasFlag(FileStatus.RenamedInWorkdir)) return "📋";  // Renamed
        if (state.HasFlag(FileStatus.NewInWorkdir)) return "❓";      // Untracked
        return "📄"; // Default
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

    private StatusEntry? FindStatusEntry(string listText)
    {
        var trimmed = listText.TrimStart();
        // Skip headers and empty lines
        if (trimmed.StartsWith("📝") || trimmed == "(no changes)" || string.IsNullOrWhiteSpace(trimmed))
            return null;

        // Extract path after the icon by finding the first space after emoji
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex > 0 && spaceIndex < trimmed.Length - 1)
        {
            var path = trimmed[(spaceIndex + 1)..].Trim();
            return _staged.FirstOrDefault(s => s.Path == path)
                ?? _unstaged.FirstOrDefault(s => s.Path == path)
                ?? _untracked.FirstOrDefault(s => s.Path == path);
        }
        return null;
    }

    private string? GetStatusPathAtIndex(int idx)
    {
        if (idx < 0 || idx >= _statusList.Items.Count) return null;
        var text = _statusList.Items[idx].ToString() ?? "";
        if (text.StartsWith("📝") || text == "(no changes)") return null;
        var entry = FindStatusEntry(text);
        return entry?.Path;
    }

    public void RefreshStatus()
    {
        if (_git.IsActive)
        {
            _noRepoLabel.Visible = false;
            _bodySplit.Visible = true;
            var (staged, unstaged, untracked) = _git.GetStatus();
            _staged = staged;
            _unstaged = unstaged;
            _untracked = untracked;

            var items = new List<string>();
            if (staged.Count > 0)
            {
                items.Add($"📝 STAGED CHANGES ({staged.Count})");
                foreach (var s in staged) items.Add($"    {GetStatusIcon(s.State)} {s.Path}");
            }
            if (unstaged.Count > 0)
            {
                items.Add($"📝 CHANGES ({unstaged.Count})");
                foreach (var s in unstaged) items.Add($"    {GetStatusIcon(s.State)} {s.Path}");
            }
            if (untracked.Count > 0)
            {
                items.Add($"📝 UNTRACKED ({untracked.Count})");
                foreach (var s in untracked) items.Add($"    ❓ {s.Path}");
            }
            if (items.Count == 0) items.Add("(no changes)");

            _statusList.Items.Clear();
            foreach (var item in items) _statusList.Items.Add(item);
            _statusHeader.Text = $"Changes ({staged.Count + unstaged.Count + untracked.Count})";

            // Update modern header
            _repoNameLabel.Text = Path.GetFileName(_git.RepoPath ?? "(no repo)");
            _branchLabel.Text = _git.CurrentBranch ?? "(detached)";

            // Update sync button with behind/ahead counts
            try
            {
                var (behind, ahead) = _git.GetRemoteStatus();
                _syncButton.Text = $"⬇️ {behind} ↑ {ahead}";
            }
            catch
            {
                _syncButton.Text = "⬇️ ? ↑ ?";
            }

            _commitBtn.Enabled = _commitMessage.Text.Trim().Length > 0 && staged.Count > 0;

            var log = _git.GetLog(30);
            _commitList.Items.Clear();
            foreach (var c in log) _commitList.Items.Add(c);

            // Re-select previously selected file if it still exists
            if (_selectedDiffIndex >= 0)
            {
                var prevPath = GetStatusPathAtIndex(_selectedDiffIndex);
                var reSelectIdx = FindStatusIndexByPath(prevPath);
                if (reSelectIdx >= 0)
                {
                    _selectedDiffIndex = reSelectIdx;
                    ShowDiffForIndex(_selectedDiffIndex);
                }
                else
                {
                    _selectedDiffIndex = -1;
                    _diffPanel.ClearDiff();
                    _bodySplit.Panel2Collapsed = true;
                }
            }
        }
        else
        {
            _noRepoLabel.Visible = true;
            _bodySplit.Visible = false;
            _repoNameLabel.Text = "(no repo)";
            _branchLabel.Text = "(no repo)";
            _syncButton.Text = "⬇️ 0 ↑ 0";
            _statusList.Items.Clear();
            _commitList.Items.Clear();
            _diffPanel.ClearDiff();
            _bodySplit.Panel2Collapsed = true;
        }

        LayoutControls();
    }

    private int FindStatusIndexByPath(string? path)
    {
        if (path is null) return -1;
        for (int i = 0; i < _statusList.Items.Count; i++)
        {
            var text = _statusList.Items[i].ToString() ?? "";
            var entry = FindStatusEntry(text);
            if (entry?.Path == path) return i;
        }
        return -1;
    }

    private void ShowDiffForIndex(int idx)
    {
        var path = GetStatusPathAtIndex(idx);
        if (path is null) { _diffPanel.ClearDiff(); _bodySplit.Panel2Collapsed = true; return; }

        var entry = FindStatusEntry(_statusList.Items[idx].ToString()!);
        if (entry is null) { _diffPanel.ClearDiff(); _bodySplit.Panel2Collapsed = true; return; }

        bool isStaged = entry.IsStaged && !entry.IsUnstaged;
        var diffContent = _git.GetDiffContent(path, isStaged);

        _diffPanel.ShowDiff(path, isStaged, diffContent);

        if (_bodySplit.Panel2Collapsed)
        {
            _bodySplit.Panel2Collapsed = false;
            _bodySplit.SplitterDistance = Math.Max(100, (int)(Height * 0.55));
        }
    }

    private void LayoutControls()
    {
        if (_topPanel is null || _statusList is null || _commitList is null || _commitMessage is null) return;
        int w = _topPanel.ClientSize.Width - 12;
        if (w < 100) w = 100;

        _statusList.Size = new Size(w, Math.Min(_statusList.Items.Count * 24 + 4, 300));
        _commitMessage.Size = new Size(w - 94, 24);
        _commitList.Location = new Point(4, _commitList.Location.Y);
        _commitList.Size = new Size(w, Math.Max(60, _topPanel.ClientSize.Height - _commitList.Top - 4));

        // Layout modern header controls
        int headerWidth = _modernHeader.ClientSize.Width - 16; // Account for padding
        _repoNameLabel.Location = new Point(0, 6);
        _branchLabel.Location = new Point(_repoNameLabel.Right + 8, 6);
        _syncButton.Location = new Point(headerWidth - _moreActionsButton.Width - _syncButton.Width - 8, 4);
        _moreActionsButton.Location = new Point(headerWidth - _moreActionsButton.Width, 4);
    }

    private void StatusList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_statusList.SelectedIndex < 0) return;
        var text = _statusList.Items[_statusList.SelectedIndex].ToString() ?? "";
        if (text.StartsWith("📝") || text == "(no changes)")
        {
            _diffPanel.ClearDiff();
            _bodySplit.Panel2Collapsed = true;
            _selectedDiffIndex = -1;
            return;
        }
        _selectedDiffIndex = _statusList.SelectedIndex;
        ShowDiffForIndex(_selectedDiffIndex);
    }

    private void StatusList_MouseClick(object? sender, MouseEventArgs e)
    {
        int idx = _statusList.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _statusList.Items.Count) return;
        var text = _statusList.Items[idx].ToString() ?? "";
        if (text.StartsWith("📝") || text == "(no changes)") return;

        var entry = FindStatusEntry(text);
        if (entry is null) return;

        // Single click toggles stage/unstage ONLY for header sections
        // (selected index change already triggers diff view)
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

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _modernHeader.BackColor = theme.TerminalHeaderBackground;
        _repoNameLabel.ForeColor = theme.Text;
        _branchLabel.ForeColor = theme.Accent;
        _syncButton.BackColor = theme.Background;
        _syncButton.ForeColor = theme.Text;
        _syncButton.FlatAppearance.BorderColor = theme.Border;
        _syncButton.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _moreActionsButton.BackColor = theme.Background;
        _moreActionsButton.ForeColor = theme.Text;
        _moreActionsButton.FlatAppearance.BorderColor = theme.Border;
        _moreActionsButton.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _bodySplit.BackColor = theme.Border;
        _topPanel.BackColor = theme.MenuBackground;
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

        _diffPanel.SetTheme(theme);

        ApplyScrollbarTheme(_statusList.Handle);
        ApplyScrollbarTheme(_commitList.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : "", null);
    }

    private void StatusList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _statusList.Items.Count) return;
        var text = _statusList.Items[e.Index].ToString() ?? "";
        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;

        bool isHeader = text.StartsWith("📝");
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        Color textColor;
        Font font;

        if (isHeader)
        {
            textColor = theme.Text;
            font = new Font("Segoe UI", 8, FontStyle.Bold);
            // Draw header background
            using var headerBrush = new SolidBrush(theme.Border);
            e.Graphics.FillRectangle(headerBrush, rect.X, rect.Y, rect.Width, rect.Height);
        }
        else if (text == "(no changes)")
        {
            textColor = theme.Muted;
            font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
        }
        else
        {
            textColor = theme.Text;
            font = new Font("Segoe UI", 8.5f, FontStyle.Regular);

            // Color code based on status icons
            if (text.Contains("🆕")) textColor = Color.FromArgb(60, 180, 75);    // Green for added
            else if (text.Contains("🔧")) textColor = Color.FromArgb(200, 180, 50); // Orange for modified
            else if (text.Contains("❌") || text.Contains("🗑️")) textColor = Color.FromArgb(200, 60, 60); // Red for deleted
            else if (text.Contains("📋")) textColor = Color.FromArgb(100, 150, 200); // Blue for renamed
            else if (text.Contains("❓")) textColor = theme.Muted; // Gray for untracked
        }

        TextRenderer.DrawText(e.Graphics, text, font, rect with { X = rect.X + 4 },
            textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
        font.Dispose();
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

    private void Commit_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_commitMessage.Text))
        { ThemedMessageBox.Show("Enter a commit message.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_staged.Count == 0)
        { ThemedMessageBox.Show("No staged changes to commit.\nClick a file to stage it, or use 'Stage All'.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (_git.Commit(_commitMessage.Text.Trim()))
        {
            _commitMessage.Text = "";
            _diffPanel.ClearDiff();
            _bodySplit.Panel2Collapsed = true;
            _selectedDiffIndex = -1;
            RefreshStatus();
        }
    }



    public void RefreshRepo(string? filePath)
    {
        _git.TryOpenRepo(filePath);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Guard: _bodySplit is null during construction (MinimumSize triggers resize before field assignment)
        if (_bodySplit is null) return;
        LayoutControls();
    }
}

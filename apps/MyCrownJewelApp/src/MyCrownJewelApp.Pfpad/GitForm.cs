using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GitForm : Form
{
    private readonly GitService _git;

    private readonly ToolStrip _branchHost;
    private readonly ToolStripDropDownButton _branchDropdown;
    private readonly ListBox _statusList;
    private readonly TextBox _commitMessage;
    private readonly Button _commitBtn;
    private readonly Button _stageAllBtn;
    private readonly Button _unstageAllBtn;
    private readonly Button _initBtn;
    private readonly Button _fetchBtn;
    private readonly Button _pullBtn;
    private readonly Button _pushBtn;
    private readonly ListBox _commitList;
    private readonly TextBox _diffBox;
    private readonly ContextMenuStrip _statusContextMenu;
    private readonly ToolStripMenuItem _discardMenuItem;
    private readonly Label _statusHeader;
    private readonly FlowLayoutPanel _syncPanel;
    private readonly FlowLayoutPanel _btnFlow;
    private readonly Label _syncLabel;
    private readonly Label _diffLabel;
    private readonly TableLayoutPanel _rightLayout;

    private List<StatusEntry> _staged = new();
    private List<StatusEntry> _unstaged = new();
    private List<StatusEntry> _untracked = new();

    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_EX_CLIENTEDGE = 0x00000200;

    public event Action<string>? FileOpenRequested;

    public GitForm(GitService git)
    {
        _git = git;
        Text = "Source Control";
        Size = new Size(900, 600);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9);
        DoubleBuffered = true;

        var theme = ThemeManager.Instance.CurrentTheme;

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 380,
            Padding = new Padding(0),
            SplitterWidth = 6,
            SplitterIncrement = 1
        };

        var leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 4, 4, 4),
            ColumnCount = 1,
            RowCount = 6
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var branchRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var branchLabel = new Label
        {
            Text = "Branch:",
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 3, 4, 0)
        };

        _branchDropdown = new ToolStripDropDownButton
        {
            Text = "(no repo)",
            Font = new Font("Segoe UI", 8),
            AutoSize = false,
            Width = 160,
            Height = 22
        };
        _branchDropdown.DropDownOpening += BranchDropdown_DropDownOpening;
        _branchDropdown.DropDownItemClicked += BranchDropdown_ItemClicked;

        _branchHost = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Size = new Size(170, 24),
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _branchHost.Items.Add(_branchDropdown);

        _initBtn = new Button
        {
            Text = "Init Repo",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(75, 24),
            Margin = new Padding(4, 0, 0, 0),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _initBtn.Click += InitRepo_Click;

        branchRow.Controls.AddRange(new Control[] { branchLabel, _branchHost, _initBtn });

        var changesHeaderRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0),
            Height = 24
        };

        _statusHeader = new Label
        {
            Text = "Changes",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 3, 8, 0)
        };

        _stageAllBtn = new Button
        {
            Text = "Stage All",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 22),
            Margin = new Padding(0, 1, 4, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _stageAllBtn.Click += (s, e) => { _git.StageAll(); RefreshStatus(); };

        _unstageAllBtn = new Button
        {
            Text = "Unstage All",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(75, 22),
            Margin = new Padding(0, 1, 0, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _unstageAllBtn.Click += (s, e) => { _git.UnstageAll(); RefreshStatus(); };

        changesHeaderRow.Controls.AddRange(new Control[] { _statusHeader, _stageAllBtn, _unstageAllBtn });

        _statusList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Dock = DockStyle.Fill,
            ItemHeight = 22,
            Cursor = Cursors.Hand
        };
        _statusList.DrawItem += StatusList_DrawItem;
        _statusList.MouseClick += StatusList_MouseClick;
        _statusList.MouseDoubleClick += StatusList_DoubleClick;
        _statusList.MeasureItem += (s, e) => e.ItemHeight = 24;
        _statusList.HandleCreated += (s, e) => ApplyScrollbarTheme(_statusList.Handle);

        _statusContextMenu = new ContextMenuStrip();
        _discardMenuItem = new ToolStripMenuItem("Discard Changes");
        _discardMenuItem.Click += Discard_Click;
        _statusContextMenu.Items.Add(_discardMenuItem);

        var commitRow = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _commitBtn = new Button
        {
            Text = "Commit",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(75, 60),
            Location = new Point(0, 0),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _commitBtn.Click += Commit_Click;

        _commitMessage = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(79, 0),
            Size = new Size(100, 60),
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = false,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = 10000,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _commitMessage.KeyDown += (s, ke) =>
        {
            if (ke.Control && ke.KeyCode == Keys.Enter)
            {
                ke.SuppressKeyPress = true;
                if (_commitBtn.Enabled)
                    Commit_Click(s, EventArgs.Empty);
            }
        };

        _commitMessage.TextChanged += (s, e) =>
        {
            bool hasText = _commitMessage.Text.Trim().Length > 0;
            bool hasStaged = _staged.Count > 0;
            _commitBtn.Enabled = hasText && hasStaged;
        };

        commitRow.Controls.Add(_commitBtn);
        commitRow.Controls.Add(_commitMessage);

        var commitsHeader = new Label
        {
            Text = "Recent Commits",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
            Dock = DockStyle.Top
        };

        _commitList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Dock = DockStyle.Fill,
            ItemHeight = 40
        };
        _commitList.DrawItem += CommitList_DrawItem;
        _commitList.MeasureItem += (s, e) => e.ItemHeight = 42;
        _commitList.HandleCreated += (s, e) => ApplyScrollbarTheme(_commitList.Handle);

        leftLayout.Controls.Add(branchRow, 0, 0);
        leftLayout.Controls.Add(changesHeaderRow, 0, 1);
        leftLayout.Controls.Add(_statusList, 0, 2);
        leftLayout.Controls.Add(commitRow, 0, 3);
        leftLayout.Controls.Add(commitsHeader, 0, 4);
        leftLayout.Controls.Add(_commitList, 0, 5);

        _rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 4, 4, 4),
            ColumnCount = 1,
            RowCount = 3
        };
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _syncPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        _syncLabel = new Label
        {
            Text = "Remote Sync",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        _btnFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0),
            WrapContents = false
        };

        _fetchBtn = new Button
        {
            Text = "Fetch",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Margin = new Padding(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _fetchBtn.Click += (s, e) => { _git.Fetch(); RefreshStatus(); };

        _pullBtn = new Button
        {
            Text = "Pull",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Margin = new Padding(0, 0, 4, 0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _pullBtn.Click += Pull_Click;

        _pushBtn = new Button
        {
            Text = "Push",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Margin = new Padding(0),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _pushBtn.Click += Push_Click;

        _btnFlow.Controls.AddRange(new Control[] { _fetchBtn, _pullBtn, _pushBtn });
        _syncPanel.Controls.Add(_syncLabel);
        _syncPanel.Controls.Add(_btnFlow);

        _diffLabel = new Label
        {
            Text = "Diff Preview",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 2)
        };

        _diffBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9),
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill
        };
        _diffBox.HandleCreated += (s, e) => ApplyScrollbarTheme(_diffBox.Handle);

        _rightLayout.Controls.Add(_syncPanel, 0, 0);
        _rightLayout.Controls.Add(_diffLabel, 0, 1);
        _rightLayout.Controls.Add(_diffBox, 0, 2);

        mainSplit.Panel1.Controls.Add(leftLayout);
        mainSplit.Panel2.Controls.Add(_rightLayout);

        Controls.Add(mainSplit);

        _git.OnRepoChanged += OnRepoChanged;
        _git.OnError += (msg) => BeginInvoke(() => ThemedMessageBox.Show(msg, "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning));

        ApplyTheme(theme);
        ThemeManager.Instance.ThemeChanged += t =>
        {
            if (IsHandleCreated)
                BeginInvoke(() => ApplyTheme(t));
            else
                ApplyTheme(t);
        };

        Shown += (s, e) => RefreshStatus();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeThemed.ApplyDarkModeToWindow(Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, theme.IsLight ? null : DARK_MODE_SCROLLBAR, null);
    }

    private void ThemeControl(Control c, Theme theme)
    {
        if (c is ToolStrip) return;

        c.BackColor = theme.MenuBackground;
        c.ForeColor = theme.Text;

        switch (c)
        {
            case Button btn:
                StyleButton(btn, theme, btn == _commitBtn || btn == _pushBtn);
                break;
            case TextBox tb:
                tb.BackColor = theme.EditorBackground;
                tb.ForeColor = theme.Text;
                break;
            case ListBox:
                break;
            case Label:
                c.BackColor = Color.Transparent;
                break;
            case TableLayoutPanel tlp:
                foreach (Control child in tlp.Controls)
                    ThemeControl(child, theme);
                break;
            case FlowLayoutPanel:
                c.BackColor = Color.Transparent;
                foreach (Control child in c.Controls)
                    ThemeControl(child, theme);
                break;
            case Panel p:
                foreach (Control child in p.Controls)
                    ThemeControl(child, theme);
                break;
        }
    }

    private static void StyleButton(Button btn, Theme theme, bool isAccent)
    {
        btn.FlatAppearance.BorderColor = theme.Muted;
        btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        if (isAccent)
        {
            btn.BackColor = theme.Accent;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderSize = 0;
        }
        else
        {
            btn.BackColor = theme.Background;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.BorderSize = 1;
        }
    }

    private void ApplyTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        ForeColor = theme.Text;

        _branchHost.BackColor = theme.MenuBackground;
        _branchHost.ForeColor = theme.Text;
        _branchHost.Renderer = new ThemeAwareMenuRenderer(theme);
        _branchDropdown.BackColor = theme.MenuBackground;
        _branchDropdown.ForeColor = theme.Text;

        _statusContextMenu.BackColor = theme.MenuBackground;
        _statusContextMenu.ForeColor = theme.Text;
        _statusContextMenu.Renderer = new ThemeAwareMenuRenderer(theme);
        _discardMenuItem.BackColor = theme.MenuBackground;
        _discardMenuItem.ForeColor = theme.Text;

        _initBtn.BackColor = theme.Background;
        _initBtn.ForeColor = theme.Text;
        _initBtn.FlatAppearance.BorderColor = theme.Muted;
        _initBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _initBtn.FlatAppearance.BorderSize = 1;

        StyleButton(_stageAllBtn, theme, false);
        StyleButton(_unstageAllBtn, theme, false);
        StyleButton(_fetchBtn, theme, false);
        StyleButton(_pullBtn, theme, false);
        StyleButton(_commitBtn, theme, true);
        StyleButton(_pushBtn, theme, true);

        ApplyScrollbarTheme(_statusList.Handle);
        ApplyScrollbarTheme(_commitList.Handle);
        ApplyScrollbarTheme(_diffBox.Handle);

        foreach (Control c in Controls)
        {
            if (c is SplitContainer sc)
            {
                sc.BackColor = theme.Border;
                foreach (Control child in sc.Panel1.Controls)
                    ThemeControl(child, theme);
                foreach (Control child in sc.Panel2.Controls)
                    ThemeControl(child, theme);
            }
        }
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
            Text = $"Source Control — {_git.RepoPath}";
            var (staged, unstaged, untracked) = _git.GetStatus();
            _staged = staged;
            _unstaged = unstaged;
            _untracked = untracked;

            var conflicts = _git.GetConflicts();
            bool hasConflicts = conflicts.Count > 0;

            var items = new List<string>();

            if (hasConflicts)
            {
                items.Add($"\u2501 Conflicts ({conflicts.Count}) \u2501");
                foreach (var c in conflicts) items.Add($"  [!] {c.Path}");
            }

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

            _statusHeader.Text = hasConflicts
                ? $"Changes — MERGE CONFLICTS ({conflicts.Count})"
                : $"Changes ({staged.Count + unstaged.Count + untracked.Count})";

            _branchDropdown.Text = _git.CurrentBranch ?? "(detached)";
            _commitBtn.Enabled = !hasConflicts && _commitMessage.Text.Trim().Length > 0 && staged.Count > 0;

            var log = _git.GetLog(30);
            _commitList.Items.Clear();
            foreach (var c in log) _commitList.Items.Add(c);

            _initBtn.Visible = false;
        }
        else
        {
            Text = "Source Control — (no repository)";
            _branchDropdown.Text = "(no repo)";
            _statusList.Items.Clear();
            _commitList.Items.Clear();
            _commitBtn.Enabled = false;
            _initBtn.Visible = true;
            _statusHeader.Text = "Changes";
        }
    }

    private void Pull_Click(object? sender, EventArgs e)
    {
        if (_git.HasUncommittedChanges())
        {
            var result = ThemedMessageBox.Show(
                "You have uncommitted changes. Pull may overwrite them.\n\nStash them before pulling?",
                "Uncommitted Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                var (stashOk, stashMsg) = _git.Stash("before-pull");
                if (!stashOk)
                {
                    ThemedMessageBox.Show(stashMsg, "Stash Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var (pullOk, pullMsg) = _git.Pull();
                var (popOk, popMsg) = _git.StashPop();
                if (!popOk) ThemedMessageBox.Show($"Pull completed, but stash pop failed: {popMsg}", "Stash Pop", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else if (!pullOk) ThemedMessageBox.Show(pullMsg, "Pull Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshStatus();
                return;
            }
        }

        var (ok, msg) = _git.Pull();
        if (!ok)
        {
            ThemedMessageBox.Show(msg, "Pull Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        RefreshStatus();

        var conflicts = _git.GetConflicts();
        if (conflicts.Count > 0)
        {
            var paths = string.Join("\n", conflicts.Select(c => $"  • {c.Path}"));
            ThemedMessageBox.Show(
                $"Merge conflicts detected in {conflicts.Count} file(s):\n\n{paths}\n\nClick a conflicted file (shown in red) and choose Ours (local) or Theirs (incoming) to resolve, then commit.",
                "Merge Conflicts",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void Push_Click(object? sender, EventArgs e)
    {
        _git.Fetch();
        int behind = _git.GetBehindCount();
        if (behind > 0)
        {
            var result = ThemedMessageBox.Show(
                $"Remote has {behind} commit(s) ahead of your branch.\n\nPull changes first to avoid rejection?",
                "Branch is Behind Remote",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                Pull_Click(sender, e);
                var (ok, msg) = _git.Push();
                if (!ok) ThemedMessageBox.Show(msg, "Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshStatus();
                return;
            }
        }

        var (pushOk, pushMsg) = _git.Push();
        if (!pushOk && pushMsg.Contains("rejected"))
        {
            var retry = ThemedMessageBox.Show(
                "Push was rejected because the remote contains commits you don't have locally.\n\nForce push instead? This overwrites the remote branch.",
                "Push Rejected",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (retry == DialogResult.Yes)
            {
                var confirm = ThemedMessageBox.Show(
                    "Force push permanently overwrites commits on the remote.\n\nThis cannot be undone.",
                    "Confirm Force Push",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation);

                if (confirm == DialogResult.Yes)
                {
                    var (fpOk, fpMsg) = _git.Push(force: true);
                    if (!fpOk) ThemedMessageBox.Show(fpMsg, "Force Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        else if (!pushOk)
        {
            ThemedMessageBox.Show(pushMsg, "Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        RefreshStatus();
    }

    private void Discard_Click(object? sender, EventArgs e)
    {
        if (_statusList.SelectedItem is not string text) return;
        var entry = FindStatusEntry(text);
        if (entry is null) return;

        var result = ThemedMessageBox.Show(
            $"Discard all changes to '{entry.Path}'?\n\nThis reverts the file to its last committed state and cannot be undone.",
            "Discard Changes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Exclamation);

        if (result == DialogResult.Yes)
        {
            var (ok, msg) = _git.DiscardFile(entry.Path);
            if (!ok) ThemedMessageBox.Show(msg, "Discard Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshStatus();
        }
    }

    private void SwitchBranchWithGuard(string name)
    {
        if (_git.HasUncommittedChanges())
        {
            var result = ThemedMessageBox.Show(
                "You have uncommitted changes that would be lost when switching branches.\n\nStash them before switching?",
                "Uncommitted Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                var (ok, msg) = _git.Stash($"before-switch-to-{name}");
                if (!ok)
                {
                    ThemedMessageBox.Show(msg, "Stash Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        if (_git.SwitchBranch(name))
            RefreshStatus();
    }

    private void ShowDiff(StatusEntry entry)
    {
        if (_git.RepoPath is null) return;
        var fullPath = Path.Combine(_git.RepoPath, entry.Path);
        if (!File.Exists(fullPath))
        {
            _diffBox.Text = $"(deleted) {entry.Path}";
            return;
        }

        try
        {
            var content = File.ReadAllText(fullPath);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"--- a/{entry.Path}");
            sb.AppendLine($"+++ b/{entry.Path}");

            foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                sb.AppendLine($" {line}");

            _diffBox.Text = sb.ToString();
            _diffBox.Select(0, 0);
        }
        catch
        {
            _diffBox.Text = $"(binary or unreadable) {entry.Path}";
        }
    }

    private StatusEntry? FindStatusEntry(string listText)
    {
        var trimmed = listText.TrimStart();
        if (trimmed.StartsWith('[') || trimmed.StartsWith('!'))
        {
            var start = trimmed.IndexOf('[') >= 0 ? trimmed.IndexOf('[') : trimmed.IndexOf('!');
            var close = trimmed.IndexOf(']', start);
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

    private static bool IsConflictEntry(string listText)
    {
        var trimmed = listText.TrimStart();
        return trimmed.StartsWith("[!]");
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

    private void StatusList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _statusList.Items.Count) return;
        var text = _statusList.Items[e.Index].ToString() ?? "";
        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;

        bool isHeader = text.StartsWith('\u2501');
        bool isSelected = (e.State & DrawItemState.Selected) != 0;
        bool isConflict = text.TrimStart().StartsWith("[!]");

        using var bgBrush = new SolidBrush(
            isConflict ? Color.FromArgb(80, 180, 40, 40) :
            isSelected ? theme.ButtonHoverBackground :
            theme.MenuBackground);
        e.Graphics.FillRectangle(bgBrush, rect);

        Color textColor;
        Font font;

        if (isHeader)
        {
            textColor = text.Contains("CONFLICT") ? Color.FromArgb(255, 100, 100) : theme.Muted;
            font = new Font("Segoe UI", 8, FontStyle.Bold);
        }
        else if (isConflict)
        {
            textColor = Color.FromArgb(255, 120, 120);
            font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        }
        else
        {
            textColor = theme.Text;
            font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
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
        if (e.Button == MouseButtons.Right)
        {
            int idx = _statusList.IndexFromPoint(e.Location);
            if (idx < 0) return;
            _statusList.SelectedIndex = idx;
            var text = _statusList.Items[idx].ToString() ?? "";
            _discardMenuItem.Visible = FindStatusEntry(text) is not null;
            _statusContextMenu.Show(_statusList, e.Location);
            return;
        }

        if (e.Button != MouseButtons.Left) return;

        int index = _statusList.IndexFromPoint(e.Location);
        if (index < 0 || index >= _statusList.Items.Count) return;
        var listText = _statusList.Items[index].ToString() ?? "";

        if (IsConflictEntry(listText))
        {
            var trimmed = listText.TrimStart();
            var start = trimmed.IndexOf("]");
            if (start > 0)
            {
                var path = trimmed[(start + 1)..].Trim();
                var useOurs = ThemedMessageBox.Show(
                    $"Resolve conflict in '{path}':\n\nChoose Yes to accept the local (ours) version.\nChoose No to accept the incoming (theirs) version.",
                    "Resolve Conflict",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                bool resolved = useOurs == DialogResult.Yes
                    ? _git.ResolveConflictOurs(path)
                    : _git.ResolveConflictTheirs(path);

                if (resolved) RefreshStatus();
            }
            return;
        }

        var entry = FindStatusEntry(listText);
        if (entry is null) return;

        if (entry.IsStaged)
            _git.Unstage(entry.Path);
        else
            _git.Stage(entry.Path);

        RefreshStatus();
        ShowDiff(entry);
    }

    private void StatusList_DoubleClick(object? sender, MouseEventArgs e)
    {
        int idx = _statusList.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _statusList.Items.Count) return;
        var text = _statusList.Items[idx].ToString() ?? "";
        if (IsConflictEntry(text)) return;

        var entry = FindStatusEntry(text);
        if (entry is not null && _git.RepoPath is not null)
        {
            var fullPath = Path.Combine(_git.RepoPath, entry.Path);
            if (File.Exists(fullPath))
                FileOpenRequested?.Invoke(fullPath);
        }
    }

    private void Commit_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_commitMessage.Text))
        {
            ThemedMessageBox.Show("Enter a commit message.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_staged.Count == 0)
        {
            ThemedMessageBox.Show("No staged changes to commit.\nClick a file to stage it, or use 'Stage All'.", "Commit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_git.GetConflicts().Count > 0)
        {
            ThemedMessageBox.Show("Resolve all merge conflicts before committing.", "Commit Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_git.Commit(_commitMessage.Text.Trim()))
        {
            _commitMessage.Text = "";
            _diffBox.Clear();
            RefreshStatus();
        }
    }

    private void InitRepo_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        dlg.Description = "Choose a folder to initialize as a Git repository";
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            if (_git.InitRepo(dlg.SelectedPath))
                RefreshStatus();
        }
    }

    private void BranchDropdown_DropDownOpening(object? sender, EventArgs e)
    {
        _branchDropdown.DropDownItems.Clear();
        if (!_git.IsActive) return;

        var theme = ThemeManager.Instance.CurrentTheme;
        var branches = _git.GetBranches();
        foreach (var b in branches)
        {
            var item = new ToolStripMenuItem(b.Name)
            {
                Checked = b.IsCurrent,
                Tag = b.Name,
                BackColor = theme.MenuBackground,
                ForeColor = theme.Text
            };
            _branchDropdown.DropDownItems.Add(item);
        }
    }

    private void BranchDropdown_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
    {
        if (e.ClickedItem?.Tag is string name && name != _git.CurrentBranch)
        {
            SwitchBranchWithGuard(name);
        }
        _branchDropdown.HideDropDown();
    }
}

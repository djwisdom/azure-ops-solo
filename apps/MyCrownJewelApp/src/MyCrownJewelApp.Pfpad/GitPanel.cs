using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GitOperationsPanel : Panel
{
    private readonly GitService _git;
    private readonly Label _branchLabel;
    private readonly Label _remoteLabel;
    private readonly Button _fetchBtn;
    private readonly Button _pullBtn;
    private readonly Button _pushBtn;
    private readonly Button _newBranchBtn;
    private readonly Button _stashBtn;
    private readonly Button _tagBtn;

    public GitOperationsPanel(GitService git)
    {
        _git = git;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8, 4, 8, 4);

        _branchLabel = new Label
        {
            Text = "🌿 main",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 4)
        };

        _remoteLabel = new Label
        {
            Text = "🔄 origin/main (up to date)",
            Font = new Font("Segoe UI", 8),
            AutoSize = true,
            Location = new Point(0, 24)
        };

        _fetchBtn = new Button
        {
            Text = "🔄 Fetch",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Location = new Point(0, 44),
            Cursor = Cursors.Hand
        };
        _fetchBtn.Click += (s, e) =>
        {
            _git.Fetch();
            UpdateStatus();
        };

        _pullBtn = new Button
        {
            Text = "⬇️ Pull",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Location = new Point(76, 44),
            Cursor = Cursors.Hand
        };
        _pullBtn.Click += async (s, e) =>
        {
            var (ok, msg) = _git.Pull();
            if (!ok) ThemedMessageBox.Show(msg, "Pull Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateStatus();
        };

        _pushBtn = new Button
        {
            Text = "⬆️ Push",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Location = new Point(152, 44),
            Cursor = Cursors.Hand
        };
        _pushBtn.Click += async (s, e) =>
        {
            var (ok, msg) = _git.Push();
            if (!ok) ThemedMessageBox.Show(msg, "Push Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateStatus();
        };

        _newBranchBtn = new Button
        {
            Text = "🌿 New Branch",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(90, 26),
            Location = new Point(0, 76),
            Cursor = Cursors.Hand
        };
        _newBranchBtn.Click += NewBranch_Click;

        _stashBtn = new Button
        {
            Text = "📦 Stash",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Location = new Point(96, 76),
            Cursor = Cursors.Hand
        };
        _stashBtn.Click += Stash_Click;

        _tagBtn = new Button
        {
            Text = "🏷️ Tag",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 26),
            Location = new Point(172, 76),
            Cursor = Cursors.Hand
        };
        _tagBtn.Click += Tag_Click;

        Controls.AddRange(new Control[] {
            _branchLabel, _remoteLabel,
            _fetchBtn, _pullBtn, _pushBtn,
            _newBranchBtn, _stashBtn, _tagBtn
        });

        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (_git.IsActive)
        {
            _branchLabel.Text = $"🌿 {_git.CurrentBranch ?? "detached"}";

            try
            {
                var (behind, ahead) = _git.GetRemoteStatus();
                string remoteStatus = "origin/main";
                if (behind > 0 && ahead > 0)
                    remoteStatus += $" ({behind}↓ {ahead}↑)";
                else if (behind > 0)
                    remoteStatus += $" ({behind}↓)";
                else if (ahead > 0)
                    remoteStatus += $" ({ahead}↑)";
                else
                    remoteStatus += " (up to date)";

                _remoteLabel.Text = $"🔄 {remoteStatus}";
            }
            catch
            {
                _remoteLabel.Text = "🔄 origin/main";
            }
        }
        else
        {
            _branchLabel.Text = "🌿 (no repo)";
            _remoteLabel.Text = "🔄 (no remote)";
        }
    }

    private void NewBranch_Click(object? sender, EventArgs e)
    {
        using var dialog = new Form
        {
            Text = "Create New Branch",
            Size = new Size(300, 150),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Text = "Branch name:", Location = new Point(12, 20), AutoSize = true };
        var textBox = new TextBox { Location = new Point(12, 40), Size = new Size(260, 23) };
        var okBtn = new Button { Text = "Create", Location = new Point(120, 70), Size = new Size(75, 23), DialogResult = DialogResult.OK };
        var cancelBtn = new Button { Text = "Cancel", Location = new Point(201, 70), Size = new Size(75, 23), DialogResult = DialogResult.Cancel };

        dialog.Controls.AddRange(new Control[] { label, textBox, okBtn, cancelBtn });
        dialog.AcceptButton = okBtn;
        dialog.CancelButton = cancelBtn;

        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            if (_git.CreateBranch(textBox.Text.Trim()))
            {
                _git.SwitchBranch(textBox.Text.Trim());
                UpdateStatus();
            }
        }
    }

    private void Stash_Click(object? sender, EventArgs e)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Create Stash", null, (s, args) =>
        {
            using var dialog = new Form
            {
                Text = "Create Stash",
                Size = new Size(300, 150),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            var label = new Label { Text = "Stash message:", Location = new Point(12, 20), AutoSize = true };
            var textBox = new TextBox { Location = new Point(12, 40), Size = new Size(260, 23), Text = "WIP" };
            var okBtn = new Button { Text = "Stash", Location = new Point(120, 70), Size = new Size(75, 23), DialogResult = DialogResult.OK };
            var cancelBtn = new Button { Text = "Cancel", Location = new Point(201, 70), Size = new Size(75, 23), DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange(new Control[] { label, textBox, okBtn, cancelBtn });
            dialog.AcceptButton = okBtn;
            dialog.CancelButton = cancelBtn;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var (success, message) = _git.Stash(textBox.Text.Trim());
                if (!success)
                    ThemedMessageBox.Show(message, "Stash Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateStatus();
            }
        });

        menu.Items.Add("Apply Stash", null, (s, args) =>
        {
            var (success, message) = _git.StashPop();
            if (!success)
                ThemedMessageBox.Show(message, "Apply Stash Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            UpdateStatus();
        });

        menu.Items.Add("Drop Stash", null, (s, args) =>
        {
            if (MessageBox.Show("Drop the most recent stash?", "Drop Stash", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                // Note: No DropStash method in existing API, would need to implement
                ThemedMessageBox.Show("Drop stash not implemented yet.", "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });

        menu.Show(_stashBtn, new Point(0, _stashBtn.Height));
    }

    private void Tag_Click(object? sender, EventArgs e)
    {
        using var dialog = new Form
        {
            Text = "Create Tag",
            Size = new Size(300, 180),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var nameLabel = new Label { Text = "Tag name:", Location = new Point(12, 20), AutoSize = true };
        var nameTextBox = new TextBox { Location = new Point(12, 40), Size = new Size(260, 23) };

        var messageLabel = new Label { Text = "Message:", Location = new Point(12, 70), AutoSize = true };
        var messageTextBox = new TextBox { Location = new Point(12, 90), Size = new Size(260, 23) };

        var okBtn = new Button { Text = "Create", Location = new Point(120, 125), Size = new Size(75, 23), DialogResult = DialogResult.OK };
        var cancelBtn = new Button { Text = "Cancel", Location = new Point(201, 125), Size = new Size(75, 23), DialogResult = DialogResult.Cancel };

        dialog.Controls.AddRange(new Control[] { nameLabel, nameTextBox, messageLabel, messageTextBox, okBtn, cancelBtn });
        dialog.AcceptButton = okBtn;
        dialog.CancelButton = cancelBtn;

        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(nameTextBox.Text))
        {
            if (_git.CreateTag(nameTextBox.Text.Trim(), messageTextBox.Text.Trim()))
            {
                MessageBox.Show("Tag created successfully!", "Tag Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;

        _branchLabel.ForeColor = theme.Text;
        _remoteLabel.ForeColor = theme.Muted;

        foreach (var btn in new[] { _fetchBtn, _pullBtn, _pushBtn, _newBranchBtn, _stashBtn, _tagBtn })
        {
            btn.BackColor = theme.Background;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.BorderColor = theme.Border;
            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        }
    }
    }

    internal sealed class GitConflictResolverPanel : Panel
    {
        private readonly GitService _git;
        private readonly Label _headerLabel;
        private readonly FlowLayoutPanel _conflictsPanel;
        private readonly Button _resolveAllBtn;

        public GitConflictResolverPanel(GitService git)
        {
            _git = git;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(8, 4, 8, 4);
            BorderStyle = BorderStyle.FixedSingle;

            _headerLabel = new Label
            {
                Text = "⚠️ Merge Conflicts",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 4),
                ForeColor = Color.FromArgb(220, 80, 80)
            };

            _conflictsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(0, 24),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _resolveAllBtn = new Button
            {
                Text = "Resolve All Conflicts",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Size = new Size(140, 28),
                Location = new Point(0, 0),
                Cursor = Cursors.Hand
            };
            _resolveAllBtn.Click += ResolveAll_Click;

            Controls.AddRange(new Control[] { _headerLabel, _conflictsPanel, _resolveAllBtn });

            UpdateConflicts();
        }

        public void UpdateConflicts()
        {
            _conflictsPanel.Controls.Clear();

            if (!_git.IsActive)
            {
                _headerLabel.Text = "⚠️ Merge Conflicts (no repo)";
                _resolveAllBtn.Enabled = false;
                return;
            }

            var conflicts = _git.GetConflicts();
            if (conflicts.Count == 0)
            {
                _headerLabel.Text = "✅ No merge conflicts";
                _resolveAllBtn.Enabled = false;
                return;
            }

            _headerLabel.Text = $"⚠️ Merge Conflicts ({conflicts.Count})";
            _resolveAllBtn.Enabled = true;

            foreach (var conflict in conflicts)
            {
                var conflictPanel = CreateConflictPanel(conflict.Path);
                _conflictsPanel.Controls.Add(conflictPanel);
            }
        }

        private Panel CreateConflictPanel(string filePath)
        {
            var panel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(8, 6, 8, 6),
                BorderStyle = BorderStyle.FixedSingle
            };

            var fileNameLabel = new Label
            {
                Text = $"📄 {Path.GetFileName(filePath)}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 0)
            };

            var buttonsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Location = new Point(0, 20)
            };

            var acceptCurrentBtn = new Button
            {
                Text = "Accept Current",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Size = new Size(100, 24),
                Cursor = Cursors.Hand
            };
            acceptCurrentBtn.Click += (s, e) =>
            {
                if (_git.ResolveConflictOurs(filePath))
                {
                    UpdateConflicts();
                }
            };

            var acceptIncomingBtn = new Button
            {
                Text = "Accept Incoming",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Size = new Size(100, 24),
                Cursor = Cursors.Hand
            };
            acceptIncomingBtn.Click += (s, e) =>
            {
                if (_git.ResolveConflictTheirs(filePath))
                {
                    UpdateConflicts();
                }
            };

            var compareBtn = new Button
            {
                Text = "Compare",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8),
                Size = new Size(70, 24),
                Cursor = Cursors.Hand
            };
            compareBtn.Click += (s, e) =>
            {
                // Open both versions in split view - simplified for now
                using var dialog = new Form
                {
                    Text = $"Compare: {Path.GetFileName(filePath)}",
                    Size = new Size(800, 600),
                    StartPosition = FormStartPosition.CenterParent
                };

                var splitContainer = new SplitContainer
                {
                    Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical
                };

                var currentBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 9),
                    ScrollBars = ScrollBars.Vertical
                };

                var incomingBox = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", 9),
                    ScrollBars = ScrollBars.Vertical
                };

                try
                {
                    // Get current version
                    string repoPath = _git.RepoPath ?? "";
                    var currentContent = File.ReadAllText(Path.Combine(repoPath, filePath));
                    currentBox.Text = currentContent;

                    // Get incoming version (from conflicts)
                    var incomingContent = "";
                    var repo = _git.GetRepo();
                    if (repo != null)
                    {
                        var conflict = repo.Index.Conflicts
                            .FirstOrDefault(c =>
                                (c.Ours?.Path == filePath ||
                                 c.Theirs?.Path == filePath ||
                                 c.Ancestor?.Path == filePath));
                        if (conflict != null && conflict.Theirs != null)
                        {
                            incomingContent = repo.Lookup<Blob>(conflict.Theirs.Id).GetContentText();
                        }
                    }
                    incomingBox.Text = incomingContent;
                }
            catch
            {
                // Ignore errors when updating analytics
            }

                splitContainer.Panel1.Controls.Add(currentBox);
                splitContainer.Panel2.Controls.Add(incomingBox);

                dialog.Controls.Add(splitContainer);
                dialog.ShowDialog();
            };

            buttonsPanel.Controls.AddRange(new Control[] { acceptCurrentBtn, acceptIncomingBtn, compareBtn });

            panel.Controls.AddRange(new Control[] { fileNameLabel, buttonsPanel });
            return panel;
        }

        private void ResolveAll_Click(object? sender, EventArgs e)
        {
            // For simplicity, resolve all as "accept current"
            // In a real implementation, you might want to ask the user
            var conflicts = _git.GetConflicts();
            foreach (var conflict in conflicts)
            {
                _git.ResolveConflictOurs(conflict.Path);
            }
            UpdateConflicts();
        }

        public void SetTheme(Theme theme)
        {
            BackColor = theme.MenuBackground;

            _headerLabel.ForeColor = theme.Accent;
            _resolveAllBtn.BackColor = theme.Background;
            _resolveAllBtn.ForeColor = theme.Text;
            _resolveAllBtn.FlatAppearance.BorderColor = theme.Border;
            _resolveAllBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

            // Update individual conflict panels
            foreach (Control control in _conflictsPanel.Controls)
            {
                if (control is Panel panel)
                {
                    panel.BackColor = theme.Background;
                    foreach (Control child in panel.Controls)
                    {
                        if (child is Button btn)
                        {
                            btn.BackColor = theme.Background;
                            btn.ForeColor = theme.Text;
                            btn.FlatAppearance.BorderColor = theme.Border;
                            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
                        }
                        else if (child is Label lbl)
                        {
                            lbl.ForeColor = theme.Text;
                        }
                    }
                }
            }
        }
    }

    internal sealed class GitHistoryAnalyticsPanel : Panel
    {
        private readonly GitService _git;
        private readonly Label _titleLabel;
        private readonly Label _commitFrequencyLabel;
        private readonly Label _topContributorsLabel;
        private readonly Label _mostChangedFilesLabel;
        private readonly Label _recentTagsLabel;

        public GitHistoryAnalyticsPanel(GitService git)
        {
            _git = git;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(8, 4, 8, 4);
            BorderStyle = BorderStyle.FixedSingle;

            _titleLabel = new Label
            {
                Text = "📊 Git History & Analytics",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(0, 4)
            };

            _commitFrequencyLabel = new Label
            {
                Text = "📈 Commit Frequency: Calculating...",
                Font = new Font("Segoe UI", 8),
                AutoSize = true,
                Location = new Point(0, 24)
            };

            _topContributorsLabel = new Label
            {
                Text = "👥 Top Contributors: Calculating...",
                Font = new Font("Segoe UI", 8),
                AutoSize = true,
                Location = new Point(0, 44)
            };

            _mostChangedFilesLabel = new Label
            {
                Text = "📁 Most Changed Files: Calculating...",
                Font = new Font("Segoe UI", 8),
                AutoSize = true,
                Location = new Point(0, 64)
            };

            _recentTagsLabel = new Label
            {
                Text = "🏷️ Recent Tags: Calculating...",
                Font = new Font("Segoe UI", 8),
                AutoSize = true,
                Location = new Point(0, 84)
            };

            Controls.AddRange(new Control[] {
                _titleLabel,
                _commitFrequencyLabel,
                _topContributorsLabel,
                _mostChangedFilesLabel,
                _recentTagsLabel
            });

            UpdateAnalytics();
        }

        public void UpdateAnalytics()
        {
            if (!_git.IsActive)
            {
                _titleLabel.Text = "📊 Git History & Analytics (no repo)";
                _commitFrequencyLabel.Text = "📈 Commit Frequency: N/A";
                _topContributorsLabel.Text = "👥 Top Contributors: N/A";
                _mostChangedFilesLabel.Text = "📁 Most Changed Files: N/A";
                _recentTagsLabel.Text = "🏷️ Recent Tags: N/A";
                return;
            }

            try
            {
                // Update commit frequency (last 7 days)
                var commits = _git.GetLog(100); // Get last 100 commits for analysis
                var commitFrequency = CalculateCommitFrequency(commits);
                _commitFrequencyLabel.Text = $"📈 Commit Frequency: {commitFrequency}/day";

                // Update top contributors
                var topContributors = GetTopContributors(commits);
                _topContributorsLabel.Text = $"👥 Top Contributors: {topContributors}";

                // Update most changed files
                var mostChangedFiles = GetMostChangedFiles(commits);
                _mostChangedFilesLabel.Text = $"📁 Most Changed Files: {mostChangedFiles}";

                // Update recent tags
                var recentTags = _git.GetTags().Take(5).ToList();
                var tagsText = recentTags.Any() 
                    ? string.Join(", ", recentTags.Select(t => 
                    {
                        try { return t.FriendlyName; } 
                        catch { return t.ToString(); }
                    })) 
                    : "None";
                _recentTagsLabel.Text = $"🏷️ Recent Tags: {tagsText}";
            }
            catch
            {
                _commitFrequencyLabel.Text = "📈 Commit Frequency: Error";
                _topContributorsLabel.Text = "👥 Top Contributors: Error";
                _mostChangedFilesLabel.Text = "📁 Most Changed Files: Error";
                _recentTagsLabel.Text = "🏷️ Recent Tags: Error";
            }
        }

        private string CalculateCommitFrequency(List<CommitEntry> commits)
        {
            if (commits.Count < 2) return "0";

            // Group commits by date and calculate average per day
            var commitDates = commits
                .Select(c => DateTime.Parse(c.Date))
                .OrderBy(d => d)
                .ToList();

            if (commitDates.Count < 2) return "0";

            var firstDate = commitDates.First();
            var lastDate = commitDates.Last();
            var totalDays = Math.Max(1, (lastDate - firstDate).TotalDays);
            var frequency = commits.Count / totalDays;

            return frequency.ToString("F1");
        }

        private string GetTopContributors(List<CommitEntry> commits)
        {
            var contributorCounts = commits
                .GroupBy(c => c.Author)
                .Select(g => new { Author = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(3)
                .ToList();

            if (!contributorCounts.Any()) return "None";

            return string.Join(", ", contributorCounts
                .Select(c => $"{c.Author} ({c.Count})"));
        }

        private string GetMostChangedFiles(List<CommitEntry> commits)
        {
            // This would require more detailed analysis from git diff
            // For now, we'll show a placeholder
            return "Analysis pending...";
        }

        public void SetTheme(Theme theme)
        {
            BackColor = theme.MenuBackground;

            _titleLabel.ForeColor = theme.Text;
            _commitFrequencyLabel.ForeColor = theme.Text;
            _topContributorsLabel.ForeColor = theme.Text;
            _mostChangedFilesLabel.ForeColor = theme.Text;
            _recentTagsLabel.ForeColor = theme.Text;
        }
    }

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
    private readonly Button _amendBtn;
    private readonly Button _commitPushBtn;
    private readonly Button _templatesBtn;
    private readonly Button _stageAllBtn;
    private readonly Button _unstageAllBtn;
    private readonly GitOperationsPanel _operationsPanel;
    private readonly GitConflictResolverPanel _conflictResolverPanel;
    private readonly GitHistoryAnalyticsPanel _historyAnalyticsPanel;
    private readonly Button _fetchBtn;
    private readonly Button _pullBtn;
    private readonly Button _pushBtn;
    private readonly Label _statusHeader;
    private readonly Label _commitHeader;
    private readonly Label _noRepoLabel;
    private readonly ContextMenuStrip _statusContextMenu;

    private List<StatusEntry> _staged = new();
    private List<StatusEntry> _unstaged = new();
    private List<StatusEntry> _untracked = new();

    private int _selectedDiffIndex = -1;

    public event Action<string>? FileOpenRequested;
    public event Action? CloseRequested;
    public event Action? StatusChanged;

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

        // Git Operations Panel
        _operationsPanel = new GitOperationsPanel(_git);
        _operationsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Conflict Resolver Panel
        _conflictResolverPanel = new GitConflictResolverPanel(_git);
        _conflictResolverPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // History and Analytics Panel
        _historyAnalyticsPanel = new GitHistoryAnalyticsPanel(_git);
        _historyAnalyticsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // Context menu for status list
        _statusContextMenu = new ContextMenuStrip();
        var stageItem = new ToolStripMenuItem("Stage", null, (s, e) => ToggleStageForSelected());
        var unstageItem = new ToolStripMenuItem("Unstage", null, (s, e) => ToggleStageForSelected());
        var discardItem = new ToolStripMenuItem("Discard Changes", null, (s, e) =>
        {
            if (_statusList?.SelectedIndices?.Count > 0)
            {
                var result = ThemedMessageBox.Show(
                    $"Discard changes for {_statusList.SelectedIndices.Count} selected file(s)?\nThis cannot be undone.",
                    "Discard Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    foreach (int index in _statusList.SelectedIndices)
                    {
                        var path = GetStatusPathAtIndex(index);
                        if (path != null)
                        {
                            _git.DiscardFile(path);
                        }
                    }
                    RefreshStatus();
                }
            }
        });
        var diffItem = new ToolStripMenuItem("View Diff", null, (s, e) =>
        {
            if (_statusList?.SelectedIndices?.Count > 0)
            {
                int lastIndex = _statusList.SelectedIndices[_statusList.SelectedIndices.Count - 1];
                _selectedDiffIndex = lastIndex;
                ShowDiffForIndex(_selectedDiffIndex);
            }
        });

        _statusContextMenu.Items.AddRange(new ToolStripItem[] {
            stageItem, unstageItem, new ToolStripSeparator(), diffItem, discardItem
        });

        // Initially hide all items, will be shown based on selection
        stageItem.Visible = false;
        unstageItem.Visible = false;
        diffItem.Visible = false;
        discardItem.Visible = false;

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

        _unstageAllBtn = new Button
        {
            Text = "Unstage All",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(80, 22),
            Location = new Point(200, y - 1),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _unstageAllBtn.Click += (s, e) => { _git.UnstageAll(); RefreshStatus(); };

        y += 24;
        _statusList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ItemHeight = 22,
            Cursor = Cursors.Hand,
            SelectionMode = SelectionMode.MultiExtended // Enable multi-selection
        };
        _statusList.DrawItem += StatusList_DrawItem;
        _statusList.MouseClick += StatusList_MouseClick;
        _statusList.MouseDoubleClick += StatusList_DoubleClick;
        _statusList.SelectedIndexChanged += StatusList_SelectedIndexChanged;
        _statusList.MeasureItem += (s, e) => e.ItemHeight = 24;
        _statusList.KeyDown += StatusList_KeyDown;

        y += 4;
        _commitBtn = new Button
        {
            Text = "Commit",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(70, 26),
            Location = new Point(4, y),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _commitBtn.Click += Commit_Click;

        _amendBtn = new Button
        {
            Text = "Amend",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(60, 26),
            Location = new Point(80, y),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _amendBtn.Click += Amend_Click;

        _commitPushBtn = new Button
        {
            Text = "Commit & Push",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(100, 26),
            Location = new Point(146, y),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _commitPushBtn.Click += CommitPush_Click;

        _commitMessage = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(90, y),
            Size = new Size(160, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = ""
        };

        _templatesBtn = new Button
        {
            Text = "📝",
            Font = new Font("Segoe UI", 8),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 24),
            Location = new Point(256, y + 1),
            Cursor = Cursors.Hand
        };
        _templatesBtn.Click += TemplatesButton_Click;
        _commitMessage.TextChanged += (s, e) =>
        {
            var hasMessage = _commitMessage.Text.Trim().Length > 0;
            var hasStaged = _staged.Count > 0;
            var hasLastCommit = _git.GetLog(1).Any();

            _commitBtn.Enabled = hasMessage && hasStaged;
            _amendBtn.Enabled = hasMessage && hasLastCommit;
            _commitPushBtn.Enabled = hasMessage && hasStaged;
        };

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
            _statusHeader, _stageAllBtn, _unstageAllBtn, _statusList, _commitMessage, _templatesBtn, _commitBtn, _amendBtn, _commitPushBtn,
            _commitHeader, _commitList, _fetchBtn, _pullBtn, _pushBtn
        });

        // Set locations for sub-panels
        _operationsPanel.Location = new Point(4, _topPanel.Bottom + 8);
        _conflictResolverPanel.Location = new Point(4, _operationsPanel.Bottom + 8);
        _historyAnalyticsPanel.Location = new Point(4, _conflictResolverPanel.Bottom + 8);

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
        Controls.Add(_operationsPanel);
        Controls.Add(_conflictResolverPanel);
        Controls.Add(_historyAnalyticsPanel);

        _git.OnRepoChanged += OnRepoChanged;
        _git.OnError += (msg) => BeginInvoke(() => ThemedMessageBox.Show(msg, "Git", MessageBoxButtons.OK, MessageBoxIcon.Warning));

        SetTheme(theme);
        RefreshStatus();
    }

    private void ToggleStageForSelected()
    {
        var selectedPaths = new List<string>();
        foreach (int index in _statusList.SelectedIndices)
        {
            var path = GetStatusPathAtIndex(index);
            if (path != null)
            {
                selectedPaths.Add(path);
            }
        }

        if (selectedPaths.Count == 0) return;

        // Check if we're in staged or unstaged section to determine action
        bool hasStaged = false;
        bool hasUnstaged = false;
        foreach (var path in selectedPaths)
        {
            if (_staged.Any(s => s.Path == path))
                hasStaged = true;
            if (_unstaged.Any(s => s.Path == path) || _untracked.Any(s => s.Path == path))
                hasUnstaged = true;
        }

        if (hasStaged && !hasUnstaged)
        {
            // All selected are staged, so unstage them
            foreach (var path in selectedPaths)
            {
                _git.Unstage(path);
            }
        }
        else
        {
            // Stage all selected
            foreach (var path in selectedPaths)
            {
                _git.Stage(path);
            }
        }

        RefreshStatus();
    }

    private void SelectAllFiles()
    {
        _statusList.ClearSelected();
        for (int i = 0; i < _statusList.Items.Count; i++)
        {
            var text = _statusList.Items[i].ToString() ?? "";
            if (!text.StartsWith("📝") && text != "(no changes)")
            {
                _statusList.SetSelected(i, true);
            }
        }
    }

    private void UpdateContextMenuForSelection()
    {
        var menu = _statusContextMenu;
        if (menu.Items.Count < 5) return; // Safety check

        var stageItem = (ToolStripMenuItem)menu.Items[0];
        var unstageItem = (ToolStripMenuItem)menu.Items[1];
        var diffItem = (ToolStripMenuItem)menu.Items[3];
        var discardItem = (ToolStripMenuItem)menu.Items[4];

        bool hasStaged = false;
        bool hasUnstaged = false;
        bool hasFiles = false;

        foreach (int index in _statusList.SelectedIndices)
        {
            var path = GetStatusPathAtIndex(index);
            if (path != null)
            {
                hasFiles = true;
                if (_staged.Any(s => s.Path == path))
                    hasStaged = true;
                if (_unstaged.Any(s => s.Path == path) || _untracked.Any(s => s.Path == path))
                    hasUnstaged = true;
            }
        }

        stageItem.Visible = hasFiles && hasUnstaged;
        unstageItem.Visible = hasFiles && hasStaged;
        diffItem.Visible = hasFiles;
        discardItem.Visible = hasFiles && hasUnstaged;
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

    private void TemplatesButton_Click(object? sender, EventArgs e)
    {
        // Show commit message templates
        var menu = new ContextMenuStrip();

        var templates = new[]
        {
            ("✨ feat: ", "New feature"),
            ("🐛 fix: ", "Bug fix"),
            ("📚 docs: ", "Documentation"),
            ("♻️ refactor: ", "Code refactoring"),
            ("🎨 style: ", "Code style changes"),
            ("✅ test: ", "Testing"),
            ("🔧 chore: ", "Maintenance"),
            ("⚡ perf: ", "Performance improvement"),
            ("🔒 security: ", "Security fix"),
            ("🚀 ci: ", "CI/CD changes")
        };

        foreach (var (prefix, description) in templates)
        {
            var item = new ToolStripMenuItem($"{prefix} {description.ToLower()}", null, (s, args) =>
            {
                _commitMessage.Text = prefix;
                _commitMessage.Focus();
                _commitMessage.SelectionStart = _commitMessage.Text.Length;
            });
            menu.Items.Add(item);
        }

        menu.Show(_templatesBtn, new Point(0, _templatesBtn.Height));
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

            var hasMessage = _commitMessage.Text.Trim().Length > 0;
            var hasStaged = staged.Count > 0;
            var hasLastCommit = _git.GetLog(1).Any();

            _commitBtn.Enabled = hasMessage && hasStaged;
            _amendBtn.Enabled = hasMessage && hasLastCommit;
            _commitPushBtn.Enabled = hasMessage && hasStaged;

            var log = _git.GetLog(30);
            _commitList.Items.Clear();
            foreach (var c in log) _commitList.Items.Add(c);

            // Update panels
            _operationsPanel.UpdateStatus();
            _conflictResolverPanel.UpdateConflicts();
            _historyAnalyticsPanel.UpdateAnalytics();

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

        StatusChanged?.Invoke();
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
        _commitMessage.Size = new Size(w - 200, 24); // Adjust for templates button and extra buttons
        _commitList.Location = new Point(4, _commitList.Location.Y);
        _commitList.Size = new Size(w, Math.Max(60, _topPanel.ClientSize.Height - _commitList.Top - 4));

        // Layout modern header controls
        int headerWidth = _modernHeader.ClientSize.Width - 16; // Account for padding
        _repoNameLabel.Location = new Point(0, 6);
        _branchLabel.Location = new Point(_repoNameLabel.Right + 8, 6);
        _syncButton.Location = new Point(headerWidth - _moreActionsButton.Width - _syncButton.Width - 8, 4);
        _moreActionsButton.Location = new Point(headerWidth - _moreActionsButton.Width, 4);

        // Layout operations panel
        if (_operationsPanel != null)
        {
            _operationsPanel.Location = new Point(4, _topPanel.Bottom + 8);
        }
        
        // Layout conflict resolver panel
        if (_conflictResolverPanel != null)
        {
            int topPosition = (_operationsPanel != null) ? _operationsPanel.Bottom : _topPanel.Bottom;
            _conflictResolverPanel.Location = new Point(4, topPosition + 8);
        }
        
        // Layout history and analytics panel
        if (_historyAnalyticsPanel != null)
        {
            int topPosition = (_conflictResolverPanel != null) ? _conflictResolverPanel.Bottom : 
                            ((_operationsPanel != null) ? _operationsPanel.Bottom : _topPanel.Bottom);
            _historyAnalyticsPanel.Location = new Point(4, topPosition + 8);
        }
    }

    private void StatusList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_statusList.SelectedIndices.Count > 0)
        {
            // For multi-selection, show diff of the last selected item
            int lastIndex = _statusList.SelectedIndices[_statusList.SelectedIndices.Count - 1];
            var text = _statusList.Items[lastIndex].ToString() ?? "";
            if (text.StartsWith("📝") || text == "(no changes)")
            {
                _diffPanel.ClearDiff();
                _bodySplit.Panel2Collapsed = true;
                _selectedDiffIndex = -1;
                return;
            }
            _selectedDiffIndex = lastIndex;
            ShowDiffForIndex(_selectedDiffIndex);
        }
    }

    private void StatusList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_statusList.SelectedIndices.Count == 0) return;

        if (e.KeyCode == Keys.Space)
        {
            // Toggle stage/unstage for selected files
            ToggleStageForSelected();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            // Select all files (not headers)
            SelectAllFiles();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Enter)
        {
            // Commit
            if (_commitBtn.Enabled)
            {
                Commit_Click(null, EventArgs.Empty);
            }
            e.Handled = true;
        }
    }

    private void StatusList_MouseClick(object? sender, MouseEventArgs e)
    {
        int idx = _statusList.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _statusList.Items.Count) return;
        var text = _statusList.Items[idx].ToString() ?? "";

        if (e.Button == MouseButtons.Right)
        {
            // Show context menu
            if (!text.StartsWith("📝") && text != "(no changes)")
            {
                // If right-clicking on non-selected item, select it
                if (!_statusList.GetSelected(idx))
                {
                    if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                    {
                        _statusList.SetSelected(idx, true);
                    }
                    else
                    {
                        _statusList.ClearSelected();
                        _statusList.SetSelected(idx, true);
                    }
                }

                // Update menu items based on selection
                UpdateContextMenuForSelection();
                _statusContextMenu.Show(_statusList, e.Location);
            }
        }
        else if (e.Button == MouseButtons.Left)
        {
            if (text.StartsWith("📝") || text == "(no changes)") return;

            var entry = FindStatusEntry(text);
            if (entry is null) return;

            // Single click toggles stage/unstage ONLY for header sections
            // (selected index change already triggers diff view)
        }
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
        _amendBtn.BackColor = theme.Background;
        _amendBtn.ForeColor = theme.Text;
        _amendBtn.FlatAppearance.BorderColor = theme.Border;
        _commitPushBtn.BackColor = theme.Background;
        _commitPushBtn.ForeColor = theme.Text;
        _commitPushBtn.FlatAppearance.BorderColor = theme.Border;
        _templatesBtn.BackColor = theme.Background;
        _templatesBtn.ForeColor = theme.Text;
        _templatesBtn.FlatAppearance.BorderColor = theme.Border;
        _stageAllBtn.BackColor = theme.Background;
        _stageAllBtn.ForeColor = theme.Text;
        _stageAllBtn.FlatAppearance.BorderColor = theme.Muted;
        _unstageAllBtn.BackColor = theme.Background;
        _unstageAllBtn.ForeColor = theme.Text;
        _unstageAllBtn.FlatAppearance.BorderColor = theme.Muted;
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

        foreach (var c in new[] { _stageAllBtn, _unstageAllBtn, _fetchBtn, _pullBtn })
            c.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _commitBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent);
        _pushBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(theme.Accent);
        _amendBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _commitPushBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        _templatesBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _diffPanel.SetTheme(theme);
        _operationsPanel.SetTheme(theme);
        _conflictResolverPanel.SetTheme(theme);
        _historyAnalyticsPanel.SetTheme(theme);

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

    private void Amend_Click(object? sender, EventArgs e)
    {
        var lastCommit = _git.GetLog(1).FirstOrDefault();
        if (lastCommit == null)
        {
            ThemedMessageBox.Show("No commits to amend.", "Amend", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Pre-fill with last commit message
        _commitMessage.Text = lastCommit.Message;

        if (string.IsNullOrWhiteSpace(_commitMessage.Text))
        { ThemedMessageBox.Show("Enter a commit message.", "Amend", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (_git.AmendCommit(_commitMessage.Text.Trim()))
        {
            _commitMessage.Text = "";
            _diffPanel.ClearDiff();
            _bodySplit.Panel2Collapsed = true;
            _selectedDiffIndex = -1;
            RefreshStatus();
        }
    }

    private void CommitPush_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_commitMessage.Text))
        { ThemedMessageBox.Show("Enter a commit message.", "Commit & Push", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_staged.Count == 0)
        { ThemedMessageBox.Show("No staged changes to commit.\nClick a file to stage it, or use 'Stage All'.", "Commit & Push", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (_git.Commit(_commitMessage.Text.Trim()))
        {
            _commitMessage.Text = "";
            _diffPanel.ClearDiff();
            _bodySplit.Panel2Collapsed = true;
            _selectedDiffIndex = -1;

            // Try to push after commit
            var (ok, msg) = _git.Push();
            if (!ok)
            {
                ThemedMessageBox.Show($"Commit successful, but push failed: {msg}", "Commit & Push", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

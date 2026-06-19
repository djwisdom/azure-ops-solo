using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MyCrownJewelApp.Pfpad.AIOps;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Pre-commit review dialog — shows all staged files and their diffs before committing.
/// Surfaces any secret findings or hook failures, lets the user adjust staged set,
/// then confirms or cancels the commit.
/// </summary>
internal sealed class PreCommitReviewDialog : Form
{
    private readonly GitService _git;
    private readonly Theme _theme;

    private readonly Label _titleLabel;
    private readonly ListBox _fileList;
    private readonly RichTextBox _diffBox;
    private readonly Button _stageBtn;
    private readonly Button _unstageBtn;
    private readonly Button _commitBtn;
    private readonly Button _cancelBtn;
    private readonly SplitContainer _mainSplit;

    private List<StatusEntry> _staged = [];
    private List<StatusEntry> _unstaged = [];
    private List<StatusEntry> _untracked = [];

    public PreCommitReviewDialog(
        GitService git,
        IReadOnlyList<SecurityFinding> secretFindings,
        bool hooksFound,
        bool hooksPassed,
        string hooksOutput)
    {
        _git = git;
        _theme = ThemeManager.Instance.CurrentTheme;
        RefreshStagedLists();

        Text = "Pre-Commit Review";
        Size = new Size(880, 620);
        MinimumSize = new Size(600, 440);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        // ── Title bar ─────────────────────────────────────────────────────────
        _titleLabel = new Label
        {
            Text = $"Review {_staged.Count} staged file(s) before committing",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        // ── Warning / check-result banner ─────────────────────────────────────
        bool hasWarnings = secretFindings.Count > 0 || (hooksFound && !hooksPassed);
        var warnPanel = BuildWarnPanel(secretFindings, hooksFound, hooksPassed, hooksOutput, hasWarnings);

        // ── File list (left pane) ─────────────────────────────────────────────
        _fileList = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            SelectionMode = SelectionMode.MultiExtended,
            Font = new Font("Segoe UI", 8.5f)
        };
        _fileList.DrawItem += FileList_DrawItem;
        _fileList.SelectedIndexChanged += FileList_SelectionChanged;
        PopulateFileList();

        _stageBtn = MakeSmallButton("↑ Stage");
        _unstageBtn = MakeSmallButton("↓ Unstage");
        _stageBtn.Click += (s, e) => ToggleStage(stage: true);
        _unstageBtn.Click += (s, e) => ToggleStage(stage: false);

        var fileActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2)
        };
        fileActions.Controls.AddRange(new Control[] { _stageBtn, _unstageBtn });

        var leftPanel = new Panel { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(_fileList);
        leftPanel.Controls.Add(fileActions);

        // ── Diff viewer (right pane) ──────────────────────────────────────────
        _diffBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Consolas", 8.5f),
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            DetectUrls = false
        };

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 280,
            SplitterWidth = 4
        };
        _mainSplit.Panel1.Controls.Add(leftPanel);
        _mainSplit.Panel2.Controls.Add(_diffBox);

        // ── Bottom buttons ────────────────────────────────────────────────────
        _commitBtn = new Button
        {
            Text = "✅ Commit",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 30),
            Cursor = Cursors.Hand
        };
        _commitBtn.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

        _cancelBtn = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 30),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel
        };

        var bottomFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6)
        };
        bottomFlow.Controls.AddRange(new Control[] { _cancelBtn, _commitBtn });

        // ── Assemble ──────────────────────────────────────────────────────────
        Controls.Add(_mainSplit);
        Controls.Add(warnPanel);
        Controls.Add(_titleLabel);
        Controls.Add(bottomFlow);

        AcceptButton = _commitBtn;
        CancelButton = _cancelBtn;

        ApplyTheme();
        UpdateCommitButton();
        if (_fileList.Items.Count > 0) _fileList.SelectedIndex = 0;
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private void RefreshStagedLists()
    {
        var (staged, unstaged, untracked) = _git.GetStatus();
        _staged = staged;
        _unstaged = unstaged;
        _untracked = untracked;
    }

    private void PopulateFileList()
    {
        _fileList.Items.Clear();
        foreach (var e in _staged)   _fileList.Items.Add(new FileItem(e, true));
        foreach (var e in _unstaged)  _fileList.Items.Add(new FileItem(e, false));
        foreach (var e in _untracked) _fileList.Items.Add(new FileItem(e, false));
    }

    private void ToggleStage(bool stage)
    {
        foreach (int idx in _fileList.SelectedIndices)
        {
            if (_fileList.Items[idx] is FileItem item)
            {
                if (stage) _git.Stage(item.Entry.Path);
                else _git.Unstage(item.Entry.Path);
            }
        }
        RefreshStagedLists();
        PopulateFileList();
        UpdateCommitButton();
    }

    private void UpdateCommitButton()
    {
        _commitBtn.Text = $"✅ Commit {_staged.Count} file(s)";
        _commitBtn.Enabled = _staged.Count > 0;
    }

    // ── Diff view ─────────────────────────────────────────────────────────────

    private void FileList_SelectionChanged(object? sender, EventArgs e)
    {
        if (_fileList.SelectedItem is FileItem item) ShowDiff(item);
    }

    private void ShowDiff(FileItem item)
    {
        _diffBox.Clear();
        try
        {
            var diff = _git.GetDiffContent(item.Entry.Path, item.IsStaged);
            foreach (var rawLine in diff.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                Color c = line.StartsWith('+') && !line.StartsWith("+++")
                    ? Color.FromArgb(80, 200, 80)
                    : line.StartsWith('-') && !line.StartsWith("---")
                        ? Color.FromArgb(220, 70, 70)
                        : line.StartsWith("@@")
                            ? Color.FromArgb(90, 160, 230)
                            : _theme.Text;

                int start = _diffBox.TextLength;
                _diffBox.AppendText(line + "\n");
                _diffBox.Select(start, line.Length + 1);
                _diffBox.SelectionColor = c;
            }
        }
        catch { _diffBox.Text = "(diff unavailable)"; }
        _diffBox.SelectionStart = 0;
        _diffBox.ScrollToCaret();
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void FileList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _fileList.Items.Count) return;
        if (_fileList.Items[e.Index] is not FileItem item) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? _theme.ButtonHoverBackground : _theme.MenuBackground);
        e.Graphics.FillRectangle(bg, e.Bounds);

        // Status dot
        string dot = item.IsStaged ? "●" : "○";
        Color dotColor = item.IsStaged ? Color.FromArgb(60, 200, 80) : _theme.Muted;
        TextRenderer.DrawText(e.Graphics, dot,
            new Font("Segoe UI", 8, FontStyle.Regular),
            e.Bounds with { Width = 18 },
            dotColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

        // File name + path
        string label = $"{Path.GetFileName(item.Entry.Path)}  {Path.GetDirectoryName(item.Entry.Path)}".TrimEnd();
        TextRenderer.DrawText(e.Graphics, label,
            new Font("Segoe UI", 8.5f, FontStyle.Regular),
            e.Bounds with { X = e.Bounds.X + 20 },
            _theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    private void ApplyTheme()
    {
        BackColor = _theme.MenuBackground;
        _titleLabel.BackColor = _theme.TerminalHeaderBackground;
        _titleLabel.ForeColor = _theme.Text;
        _fileList.BackColor = _theme.MenuBackground;
        _fileList.ForeColor = _theme.Text;
        _diffBox.BackColor = _theme.EditorBackground;
        _diffBox.ForeColor = _theme.Text;
        _mainSplit.BackColor = _theme.Border;

        foreach (var btn in new[] { _stageBtn, _unstageBtn, _cancelBtn })
        {
            btn.BackColor = _theme.Background;
            btn.ForeColor = _theme.Text;
            btn.FlatAppearance.BorderColor = _theme.Border;
            btn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        }
        _commitBtn.BackColor = _theme.Accent;
        _commitBtn.ForeColor = FlatUiHelper.BadgeForeground(_theme);
        _commitBtn.FlatAppearance.BorderSize = 0;
        _commitBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_theme.Accent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Panel BuildWarnPanel(
        IReadOnlyList<SecurityFinding> findings,
        bool hooksFound, bool hooksPassed, string hooksOutput, bool hasWarnings)
    {
        var header = new Label
        {
            Text = hasWarnings
                ? $"⚠️  {findings.Count} secret finding(s){(hooksFound && !hooksPassed ? " + hook failure" : "")} — review carefully:"
                : "✅  Pre-commit checks passed",
            Font = new Font("Segoe UI", 8.5f, hasWarnings ? FontStyle.Bold : FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 22,
            Padding = new Padding(6, 2, 0, 0),
            ForeColor = hasWarnings ? Color.FromArgb(220, 120, 0) : Color.FromArgb(60, 200, 80)
        };

        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = hasWarnings ? 80 : 26,
            Padding = new Padding(4, 0, 4, 2),
            BorderStyle = hasWarnings ? BorderStyle.FixedSingle : BorderStyle.None
        };

        if (hasWarnings)
        {
            var warnBox = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("Consolas", 8),
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 180, 60, 0),
                ForeColor = _theme.Text
            };
            foreach (var f in findings)
                warnBox.AppendText($"🔐 {f.RuleId}  {Path.GetFileName(f.FilePath ?? "")}:{f.LineNumber}  {f.Title}\r\n");
            if (hooksFound && !hooksPassed)
                warnBox.AppendText($"⛔ Pre-commit hook failed:\r\n{hooksOutput}\r\n");
            panel.Controls.Add(warnBox);
        }

        panel.Controls.Add(header);
        return panel;
    }

    private static Button MakeSmallButton(string text) => new()
    {
        Text = text,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 8),
        Size = new Size(80, 24),
        Cursor = Cursors.Hand
    };

    // ── Inner record ──────────────────────────────────────────────────────────

    private sealed record FileItem(StatusEntry Entry, bool IsStaged)
    {
        public override string ToString() => $"{(IsStaged ? "● " : "○ ")}{Entry.Path}";
    }
}

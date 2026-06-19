using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Platform-aware "Create new branch" dialog.
/// Replaces the bare single-TextBox prompt with structured fields:
/// prefix, optional ticket/issue ID, and description.
/// Generates the branch name via <see cref="GitRemotePlatform.SuggestBranchName"/>.
/// </summary>
internal sealed class NewBranchDialog : Form
{
    private readonly GitPlatform _platform;
    private readonly ComboBox _prefixBox;
    private readonly TextBox _ticketBox;
    private readonly TextBox _descBox;
    private readonly Label _previewLabel;
    private readonly Button _createBtn;
    private readonly Button _cancelBtn;
    private readonly Theme _theme;

    // ── Outputs ───────────────────────────────────────────────────────────────
    public string BranchName { get; private set; } = "";

    // ── Prefix options ────────────────────────────────────────────────────────
    private static readonly string[] Prefixes =
        { "feature", "fix", "hotfix", "chore", "release", "refactor", "docs", "experiment" };

    public NewBranchDialog(GitPlatform platform = GitPlatform.Unknown)
    {
        _platform = platform;
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "Create New Branch";
        Size = new Size(480, 310);
        MinimumSize = new Size(400, 280);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        // ── Prefix ────────────────────────────────────────────────────────────
        var prefixLabel = MakeLabel("Prefix");
        _prefixBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            Location = new Point(16, 44),
            Size = new Size(130, 26),
            FlatStyle = FlatStyle.Flat
        };
        _prefixBox.Items.AddRange(Prefixes);
        _prefixBox.SelectedIndex = 0;
        _prefixBox.SelectedIndexChanged += (s, e) => Refresh_();

        // ── Ticket / Issue ID ─────────────────────────────────────────────────
        var ticketHint = TicketHint(platform);
        var ticketLabel = MakeLabel(ticketHint.Label);
        _ticketBox = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            Location = new Point(162, 44),
            Size = new Size(130, 26),
            PlaceholderText = ticketHint.Placeholder
        };
        _ticketBox.TextChanged += (s, e) => Refresh_();

        // ── Description ───────────────────────────────────────────────────────
        var descLabel = MakeLabel("Short description *");
        _descBox = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            Location = new Point(308, 44),
            Size = new Size(144, 26),
            MaxLength = 60
        };
        _descBox.TextChanged += (s, e) => Refresh_();

        // ── Preview ───────────────────────────────────────────────────────────
        var previewCaption = new Label
        {
            Text = "Branch name preview",
            Font = new Font("Segoe UI", 8),
            Location = new Point(16, 90),
            AutoSize = true
        };
        _previewLabel = new Label
        {
            Text = "",
            Font = new Font("Consolas", 9, FontStyle.Bold),
            Location = new Point(16, 110),
            Size = new Size(436, 50),
            AutoSize = false,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(6, 6, 6, 6),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // ── Platform hint ─────────────────────────────────────────────────────
        var platformHint = new Label
        {
            Text = platform != GitPlatform.Unknown
                ? $"{GitRemotePlatform.HeaderGlyph(platform)}  {GitRemotePlatform.DisplayName(platform)} convention"
                : "Generic Git branch naming",
            Font = new Font("Segoe UI", 8),
            Location = new Point(16, 168),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };

        // ── Buttons ───────────────────────────────────────────────────────────
        _createBtn = new Button
        {
            Text = "Create Branch",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(260, 230),
            Size = new Size(110, 30),
            DialogResult = DialogResult.OK,
            Enabled = false
        };
        _createBtn.Click += CreateBtn_Click;

        _cancelBtn = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(378, 230),
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel
        };

        // ── Anchor labels to field tops ───────────────────────────────────────
        prefixLabel.Location = new Point(16, 22);
        ticketLabel.Location = new Point(162, 22);
        descLabel.Location = new Point(308, 22);

        Controls.AddRange(new Control[]
        {
            prefixLabel, _prefixBox,
            ticketLabel, _ticketBox,
            descLabel, _descBox,
            previewCaption, _previewLabel,
            platformHint,
            _createBtn, _cancelBtn
        });

        AcceptButton = _createBtn;
        CancelButton = _cancelBtn;

        ThemeManager.Instance.ApplyTheme(this);
        ApplyExtraTheme();
        Refresh_();
    }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void Refresh_()
    {
        var prefix = _prefixBox.SelectedItem?.ToString() ?? "feature";
        var ticket = _ticketBox.Text.Trim();
        var desc   = _descBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(desc))
        {
            _previewLabel.Text = "(enter a description to see preview)";
            _previewLabel.ForeColor = SystemColors.GrayText;
            _createBtn.Enabled = false;
            return;
        }

        var name = GitRemotePlatform.SuggestBranchName(_platform, prefix, ticket, desc);
        _previewLabel.Text = name;
        _previewLabel.ForeColor = _theme.Text;
        _createBtn.Enabled = true;
        BranchName = name;
    }

    private void CreateBtn_Click(object? sender, EventArgs e)
    {
        BranchName = _previewLabel.Text.Trim();
        if (string.IsNullOrWhiteSpace(BranchName) || BranchName.StartsWith('('))
        {
            MessageBox.Show("Please enter a description.", "Branch Name Required",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private void ApplyExtraTheme()
    {
        BackColor = _theme.Background;
        _previewLabel.BackColor = _theme.MenuBackground;
        _previewLabel.ForeColor = _theme.Text;
        foreach (Control c in Controls)
        {
            c.ForeColor = _theme.Text;
            if (c is TextBox tb) { tb.BackColor = _theme.EditorBackground; tb.ForeColor = _theme.Text; }
            if (c is ComboBox cb) { cb.BackColor = _theme.EditorBackground; cb.ForeColor = _theme.Text; }
            if (c is Button btn)
            {
                btn.BackColor = btn == _createBtn ? _theme.Accent : _theme.MenuBackground;
                btn.ForeColor = btn == _createBtn ? FlatUiHelper.BadgeForeground(_theme) : _theme.Text;
                btn.FlatAppearance.BorderColor = _theme.Border;
            }
        }
    }

    // ── Per-platform ticket ID field metadata ─────────────────────────────────

    private static (string Label, string Placeholder) TicketHint(GitPlatform platform) =>
        platform switch
        {
            GitPlatform.GitHub       => ("Issue #",          "123"),
            GitPlatform.AzureDevOps  => ("Work Item AB#",    "1234"),
            GitPlatform.GitLab       => ("Issue #",          "456"),
            GitPlatform.Bitbucket    => ("Jira ticket",      "PROJ-42"),
            _                        => ("Ticket (optional)", ""),
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8),
        AutoSize = true
    };
}

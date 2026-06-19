using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Structured Conventional Commit message composer.
/// Outputs a complete message following Conventional Commits v1.0.0.
/// Opens in place of the 📝 templates button in GitPanel.
/// </summary>
internal sealed class ConventionalCommitComposerDialog : Form
{
    private readonly Theme _theme;

    private readonly ComboBox _typeBox;
    private readonly TextBox _scopeBox;
    private readonly CheckBox _breakingBox;
    private readonly TextBox _subjectBox;
    private readonly Label _subjectCounter;
    private readonly TextBox _bodyBox;
    private readonly TextBox _footerBox;
    private readonly CheckBox _signOffBox;
    private readonly RichTextBox _previewBox;
    private readonly Button _okBtn;
    private readonly Button _cancelBtn;

    /// <summary>Full composed commit message after OK is clicked.</summary>
    public string ComposedMessage { get; private set; } = "";

    private static readonly string[] _types =
    [
        "feat", "fix", "docs", "refactor", "style",
        "test", "chore", "perf", "ci", "build", "revert"
    ];

    private static readonly string[] _typeDescriptions =
    [
        "feat     — new feature for the user",
        "fix      — bug fix for the user",
        "docs     — documentation only",
        "refactor — code change, no feature/fix",
        "style    — formatting, whitespace",
        "test     — adding / fixing tests",
        "chore    — build process, tooling",
        "perf     — performance improvement",
        "ci       — CI/CD configuration",
        "build    — build system / deps",
        "revert   — revert a prior commit"
    ];

    public ConventionalCommitComposerDialog(string initialMessage, string authorName, string authorEmail)
    {
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "Compose Commit  —  Conventional Commits v1.0";
        Size = new Size(660, 620);
        MinimumSize = new Size(520, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;

        ParseInitial(initialMessage,
            out string initType, out string initScope, out bool initBreaking,
            out string initSubject, out string initBody, out string initFooter);

        // ── Type ──────────────────────────────────────────────────────────────
        _typeBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Consolas", 9),
            Width = 260,
            Margin = new Padding(0, 0, 12, 0)
        };
        _typeBox.Items.AddRange(_typeDescriptions);
        int typeIdx = Array.IndexOf(_types, initType);
        _typeBox.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;
        _typeBox.SelectedIndexChanged += (s, e) => Refresh_();

        // ── Scope ─────────────────────────────────────────────────────────────
        _scopeBox = MakeInput("optional — e.g. parser, auth", 140, initScope);
        _scopeBox.TextChanged += (s, e) => Refresh_();

        // ── Breaking change ───────────────────────────────────────────────────
        _breakingBox = new CheckBox
        {
            Text = "BREAKING CHANGE  (!)",
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            Checked = initBreaking,
            Margin = new Padding(0, 4, 0, 0)
        };
        _breakingBox.CheckedChanged += (s, e) => Refresh_();

        // ── Subject ───────────────────────────────────────────────────────────
        _subjectBox = MakeInput("short imperative description", 0, initSubject);
        _subjectBox.Dock = DockStyle.Fill;
        _subjectBox.TextChanged += (s, e) => { Refresh_(); RefreshCounter(); };

        _subjectCounter = new Label
        {
            Font = new Font("Segoe UI", 7.5f),
            AutoSize = true,
            ForeColor = _theme.Muted,
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(4, 0, 0, 0)
        };

        // ── Body ──────────────────────────────────────────────────────────────
        _bodyBox = MakeMultiline("optional longer description, wrap at 72 chars…", 70, initBody);
        _bodyBox.TextChanged += (s, e) => Refresh_();

        // ── Footer ────────────────────────────────────────────────────────────
        _footerBox = MakeMultiline("BREAKING CHANGE: description\nFixes #42", 44, initFooter);
        _footerBox.TextChanged += (s, e) => Refresh_();

        // ── Signed-off-by ─────────────────────────────────────────────────────
        _signOffBox = new CheckBox
        {
            Text = $"Signed-off-by: {authorName} <{authorEmail}>",
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            Checked = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        _signOffBox.CheckedChanged += (s, e) => Refresh_();

        // ── Preview ───────────────────────────────────────────────────────────
        _previewBox = new RichTextBox
        {
            ReadOnly = true,
            Font = new Font("Consolas", 8.5f),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = false,
            DetectUrls = false,
            Height = 100,
            Dock = DockStyle.Fill
        };

        // ── Buttons ───────────────────────────────────────────────────────────
        _okBtn = new Button
        {
            Text = "✅ Use this message",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(170, 30),
            Cursor = Cursors.Hand
        };
        _okBtn.Click += OkBtn_Click;

        _cancelBtn = new Button
        {
            Text = "Cancel",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(80, 30),
            DialogResult = DialogResult.Cancel,
            Cursor = Cursors.Hand
        };

        // ── Build layout ──────────────────────────────────────────────────────
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            AutoSize = false,
            Padding = new Padding(12, 10, 12, 6)
        };
        void AddRow(Control c, int height = 0)
        {
            layout.Controls.Add(c);
            if (height > 0) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            else layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }

        // Row: type + scope + breaking
        var row1 = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row1.Controls.Add(SectionLabel("Type *"));
        row1.Controls.Add(_typeBox);
        row1.Controls.Add(SectionLabel("Scope"));
        row1.Controls.Add(_scopeBox);
        row1.Controls.Add(_breakingBox);
        AddRow(row1, 36);

        // Subject
        AddRow(SectionLabel("Subject *  (imperative, present tense, ≤72 chars)"), 20);
        var subjectRow = new Panel { Dock = DockStyle.Fill, Height = 28 };
        subjectRow.Controls.Add(_subjectCounter);
        subjectRow.Controls.Add(_subjectBox);
        AddRow(subjectRow, 28);

        // Body
        AddRow(SectionLabel("Body  (optional — explain what and why, not how)"), 18);
        var bodyPanel = new Panel { Height = 70, Dock = DockStyle.Fill };
        bodyPanel.Controls.Add(_bodyBox);
        AddRow(bodyPanel, 72);

        // Footer
        AddRow(SectionLabel("Footer  (breaking changes, issue refs)"), 18);
        var footerPanel = new Panel { Height = 44, Dock = DockStyle.Fill };
        footerPanel.Controls.Add(_footerBox);
        AddRow(footerPanel, 46);

        // Sign-off
        AddRow(_signOffBox, 26);

        // Preview
        AddRow(SectionLabel("Preview"), 18);
        var previewPanel = new Panel { Height = 100, Dock = DockStyle.Fill };
        previewPanel.Controls.Add(_previewBox);
        AddRow(previewPanel, 102);

        // Buttons
        var btnRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Height = 38,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0)
        };
        btnRow.Controls.AddRange(new Control[] { _cancelBtn, _okBtn });
        AddRow(btnRow, 38);

        Controls.Add(layout);
        AcceptButton = _okBtn;
        CancelButton = _cancelBtn;

        ApplyTheme(layout);
        Refresh_();
        RefreshCounter();
    }

    // ── Message building ──────────────────────────────────────────────────────

    private string BuildMessage()
    {
        var sb = new StringBuilder();

        // Header
        string type = _types[Math.Max(0, _typeBox.SelectedIndex)];
        string scope = _scopeBox.Text.Trim();
        bool breaking = _breakingBox.Checked;

        sb.Append(type);
        if (!string.IsNullOrEmpty(scope)) sb.Append($"({scope})");
        if (breaking) sb.Append('!');
        sb.Append(": ");
        sb.Append(_subjectBox.Text.Trim());

        // Body
        string body = _bodyBox.Text.Trim();
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(body);
        }

        // Footer — auto-add BREAKING CHANGE if checkbox but not already in footer
        string footer = _footerBox.Text.Trim();
        if (breaking && !footer.Contains("BREAKING CHANGE", StringComparison.OrdinalIgnoreCase))
        {
            footer = string.IsNullOrEmpty(footer)
                ? "BREAKING CHANGE: describe the breaking change"
                : $"BREAKING CHANGE: describe the breaking change\n{footer}";
        }
        if (!string.IsNullOrEmpty(footer))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(footer);
        }

        // Signed-off-by
        if (_signOffBox.Checked)
        {
            string signText = _signOffBox.Text
                .Replace("Signed-off-by: ", "", StringComparison.Ordinal).Trim();
            sb.AppendLine();
            sb.AppendLine();
            sb.Append($"Signed-off-by: {signText}");
        }

        return sb.ToString();
    }

    private void Refresh_()
    {
        string msg = BuildMessage();
        _previewBox.Clear();

        int firstNewline = msg.IndexOf('\n');
        int headerEnd = firstNewline < 0 ? msg.Length : firstNewline;

        // Accent-colour header line
        _previewBox.AppendText(msg[..headerEnd]);
        _previewBox.Select(0, headerEnd);
        _previewBox.SelectionColor = _theme.Accent;

        if (headerEnd < msg.Length)
        {
            int bodyStart = _previewBox.TextLength;
            _previewBox.AppendText(msg[headerEnd..]);
            _previewBox.Select(bodyStart, _previewBox.TextLength - bodyStart);
            _previewBox.SelectionColor = _theme.Text;
        }

        _previewBox.SelectionStart = 0;
    }

    private void RefreshCounter()
    {
        int len = _subjectBox.Text.Length;
        _subjectCounter.Text = $"{len}/72";
        _subjectCounter.ForeColor = len > 72 ? Color.OrangeRed
            : len > 50 ? Color.DarkOrange
            : _theme.Muted;
    }

    private void OkBtn_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_subjectBox.Text))
        {
            ThemedMessageBox.Show("Subject is required.", "Commit Composer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _subjectBox.Focus();
            return;
        }
        ComposedMessage = BuildMessage();
        DialogResult = DialogResult.OK;
        Close();
    }

    // ── Parsing ───────────────────────────────────────────────────────────────

    private static void ParseInitial(string msg,
        out string type, out string scope, out bool breaking,
        out string subject, out string body, out string footer)
    {
        type = "feat"; scope = ""; breaking = false;
        subject = ""; body = ""; footer = "";
        if (string.IsNullOrWhiteSpace(msg)) return;

        var parts = msg.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string header = parts[0].Trim();

        int colonIdx = header.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx > 0)
        {
            string prefix = header[..colonIdx];
            subject = header[(colonIdx + 2)..].Trim();
            breaking = prefix.EndsWith('!');
            if (breaking) prefix = prefix[..^1];

            int pOpen = prefix.IndexOf('(');
            int pClose = prefix.IndexOf(')');
            if (pOpen > 0 && pClose > pOpen)
            {
                type = prefix[..pOpen].ToLowerInvariant().Trim();
                scope = prefix[(pOpen + 1)..pClose].Trim();
            }
            else type = prefix.ToLowerInvariant().Trim();

            if (parts.Length > 2)
                body = string.Join('\n', parts[2..]).Trim();
        }
        else
        {
            subject = header;
        }
    }

    // ── Theme / layout helpers ────────────────────────────────────────────────

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 8, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 2, 8, 0),
        ForeColor = Color.Gray
    };

    private static TextBox MakeInput(string placeholder, int width, string initial) => new()
    {
        Font = new Font("Segoe UI", 9),
        Width = width > 0 ? width : 200,
        PlaceholderText = placeholder,
        Text = initial,
        Margin = new Padding(0, 0, 10, 0)
    };

    private static TextBox MakeMultiline(string placeholder, int height, string initial) => new()
    {
        Font = new Font("Consolas", 8.5f),
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = height,
        Dock = DockStyle.Fill,
        PlaceholderText = placeholder,
        Text = initial,
        AcceptsReturn = true
    };

    private void ApplyTheme(Control root)
    {
        BackColor = _theme.MenuBackground;
        ApplyRecursive(root);
    }

    private void ApplyRecursive(Control c)
    {
        switch (c)
        {
            case TextBox tb:
                tb.BackColor = _theme.EditorBackground;
                tb.ForeColor = _theme.Text;
                break;
            case ComboBox cb:
                cb.BackColor = _theme.EditorBackground;
                cb.ForeColor = _theme.Text;
                break;
            case RichTextBox rtb:
                rtb.BackColor = _theme.EditorBackground;
                rtb.ForeColor = _theme.Text;
                break;
            case Label lbl:
                lbl.BackColor = Color.Transparent;
                lbl.ForeColor = _theme.Text;
                break;
            case CheckBox chk:
                chk.BackColor = Color.Transparent;
                chk.ForeColor = _theme.Text;
                break;
            case Button btn when btn == _okBtn:
                btn.BackColor = _theme.Accent;
                btn.ForeColor = FlatUiHelper.BadgeForeground(_theme);
                btn.FlatAppearance.BorderSize = 0;
                break;
            case Button btn:
                btn.BackColor = _theme.Background;
                btn.ForeColor = _theme.Text;
                btn.FlatAppearance.BorderColor = _theme.Border;
                btn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
                break;
            case Panel or FlowLayoutPanel or TableLayoutPanel:
                c.BackColor = _theme.MenuBackground;
                break;
        }

        foreach (Control child in c.Controls)
            ApplyRecursive(child);
    }
}

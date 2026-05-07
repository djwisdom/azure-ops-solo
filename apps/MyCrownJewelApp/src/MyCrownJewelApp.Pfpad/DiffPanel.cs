using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class DiffPanel : UserControl
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _headerLabel;
    private readonly RichTextBox _diffBox;
    private readonly Button _stageBtn;
    private readonly Button _unstageBtn;
    private readonly Button _discardBtn;

    private string _currentPath = string.Empty;
    private bool _currentIsStaged;

    private const int MaxDiffLineCount = 5000;
    private const int MaxDiffCharCount = 300_000;

    public event Action<string>? StageRequested;
    public event Action<string>? UnstageRequested;
    public event Action<string>? DiscardRequested;

    public DiffPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _headerLabel = new ToolStripLabel("Diff")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Margin = new Padding(4, 0, 0, 0)
        };

        _stageBtn = new Button
        {
            Text = "Stage",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(56, 22),
            Margin = new Padding(0, 1, 2, 1),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _stageBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentPath)) StageRequested?.Invoke(_currentPath); };

        _unstageBtn = new Button
        {
            Text = "Unstage",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(62, 22),
            Margin = new Padding(0, 1, 2, 1),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _unstageBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentPath)) UnstageRequested?.Invoke(_currentPath); };

        _discardBtn = new Button
        {
            Text = "Discard",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(62, 22),
            Margin = new Padding(0, 1, 0, 1),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _discardBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_currentPath))
            {
                var result = ThemedMessageBox.Show(
                    $"Discard changes in '{_currentPath}'?\nThis cannot be undone.",
                    "Discard", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                    DiscardRequested?.Invoke(_currentPath);
            }
        };

        var headerRightPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            Width = 190,
            Padding = new Padding(0, 0, 2, 0),
            WrapContents = false
        };
        headerRightPanel.Controls.Add(_stageBtn);
        headerRightPanel.Controls.Add(_unstageBtn);
        headerRightPanel.Controls.Add(_discardBtn);

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(2, 1, 0, 1),
            AutoSize = false,
            Height = 28
        };
        _headerStrip.Items.Add(_headerLabel);

        var host = new ToolStripControlHost(headerRightPanel)
        {
            Alignment = ToolStripItemAlignment.Right
        };
        _headerStrip.Items.Add(host);

        _diffBox = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            WordWrap = false,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            HideSelection = false
        };
        _diffBox.VScroll += (s, e) => ApplyScrollbarTheme(_diffBox.Handle);

        Controls.Add(_diffBox);
        Controls.Add(_headerStrip);

        SetTheme(ThemeManager.Instance.CurrentTheme);
    }

    public void ShowDiff(string path, bool isStaged, string diffContent)
    {
        _currentPath = path;
        _currentIsStaged = isStaged;

        _headerLabel.Text = $"Diff — {path}";

        _stageBtn.Visible = !isStaged;
        _unstageBtn.Visible = isStaged;
        _discardBtn.Visible = !isStaged;

        if (string.IsNullOrEmpty(diffContent))
        {
            _diffBox.Text = "(no changes)";
            return;
        }

        if (diffContent.Length > MaxDiffCharCount)
        {
            _diffBox.Text = diffContent[..MaxDiffCharCount]
                + $"\n\n(diff truncated at {MaxDiffCharCount / 1000}K characters)";
        }
        else
        {
            _diffBox.Text = diffContent;
        }

        ApplyDiffColoring();
    }

    public void ClearDiff()
    {
        _currentPath = string.Empty;
        _headerLabel.Text = "Diff";
        _diffBox.Text = string.Empty;
        _stageBtn.Visible = false;
        _unstageBtn.Visible = false;
        _discardBtn.Visible = false;
    }

    private void ApplyDiffColoring()
    {
        int lineCount = 0;
        int lineStart = 0;
        for (int i = 0; i < _diffBox.TextLength && lineCount < MaxDiffLineCount; i++)
        {
            if (_diffBox.Text[i] == '\n')
            {
                ColorLine(lineStart, i);
                lineStart = i + 1;
                lineCount++;
            }
        }
        // Last line if no trailing newline
        if (lineStart < _diffBox.TextLength)
            ColorLine(lineStart, _diffBox.TextLength);
    }

    private void ColorLine(int start, int end)
    {
        if (start >= _diffBox.TextLength || end > _diffBox.TextLength) return;
        if (end - start < 1) return;

        char firstChar = _diffBox.Text[start];

        switch (firstChar)
        {
            case '+':
                _diffBox.Select(start, end - start);
                _diffBox.SelectionBackColor = Color.FromArgb(30, 80, 40);
                break;
            case '-':
                _diffBox.Select(start, end - start);
                _diffBox.SelectionBackColor = Color.FromArgb(80, 30, 30);
                break;
            case '@':
                _diffBox.Select(start, end - start);
                _diffBox.SelectionBackColor = Color.FromArgb(30, 40, 60);
                _diffBox.SelectionColor = Color.FromArgb(130, 180, 230);
                break;
        }

        // Hunk header lines (--- a/..., +++ b/...)
        if (end - start > 4 && (firstChar == '-' || firstChar == '+'))
        {
            var line = _diffBox.Text.AsSpan(start, end - start);
            if (line.Length > 5 && line[1] == '-' && line[2] == '-' && line[3] == ' ')
            {
                _diffBox.Select(start, end - start);
                _diffBox.SelectionColor = Color.FromArgb(130, 180, 230);
            }
            else if (line.Length > 5 && line[1] == '+' && line[2] == '+' && line[3] == ' ')
            {
                _diffBox.Select(start, end - start);
                _diffBox.SelectionColor = Color.FromArgb(130, 180, 230);
            }
        }

        _diffBox.Select(0, 0);
    }

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _headerLabel.ForeColor = theme.Text;

        _diffBox.BackColor = theme.EditorBackground;
        _diffBox.ForeColor = theme.Text;

        _stageBtn.BackColor = theme.Background;
        _stageBtn.ForeColor = theme.Text;
        _stageBtn.FlatAppearance.BorderColor = theme.Accent;
        _stageBtn.FlatAppearance.BorderSize = 1;
        _stageBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _unstageBtn.BackColor = theme.Background;
        _unstageBtn.ForeColor = theme.Text;
        _unstageBtn.FlatAppearance.BorderColor = theme.Muted;
        _unstageBtn.FlatAppearance.BorderSize = 1;
        _unstageBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _discardBtn.BackColor = theme.Background;
        _discardBtn.ForeColor = Color.FromArgb(220, 80, 80);
        _discardBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 60, 60);
        _discardBtn.FlatAppearance.BorderSize = 1;
        _discardBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 30, 30);

        ApplyScrollbarTheme(_diffBox.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : null, null);
    }
}

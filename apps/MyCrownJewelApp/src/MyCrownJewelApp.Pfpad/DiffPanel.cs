using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class DiffPanel : UserControl
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly Panel _modernHeader;
    private readonly Label _headerLabel;
    private readonly Button _inlineStageBtn;
    private readonly Button _inlineUnstageBtn;
    private readonly Button _inlineDiscardBtn;
    private readonly RichTextBox _diffBox;

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

        // Modern VS Code-style header
        _modernHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(8, 4, 8, 4)
        };

        _headerLabel = new Label
        {
            Text = "Diff",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 6)
        };

        _inlineStageBtn = new Button
        {
            Text = "➕ Stage",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(60, 24),
            Location = new Point(0, 4),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _inlineStageBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentPath)) StageRequested?.Invoke(_currentPath); };

        _inlineUnstageBtn = new Button
        {
            Text = "➖ Unstage",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 24),
            Location = new Point(0, 4),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _inlineUnstageBtn.Click += (s, e) => { if (!string.IsNullOrEmpty(_currentPath)) UnstageRequested?.Invoke(_currentPath); };

        _inlineDiscardBtn = new Button
        {
            Text = "🗑️ Discard",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Size = new Size(70, 24),
            Location = new Point(0, 4),
            Cursor = Cursors.Hand,
            TabStop = false,
            Visible = false
        };
        _inlineDiscardBtn.Click += (s, e) =>
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

        _modernHeader.Controls.AddRange(new Control[] { _headerLabel, _inlineStageBtn, _inlineUnstageBtn, _inlineDiscardBtn });

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
        Controls.Add(_modernHeader);

        SetTheme(ThemeManager.Instance.CurrentTheme);
    }

    public void ShowDiff(string path, bool isStaged, string diffContent)
    {
        _currentPath = path;
        _currentIsStaged = isStaged;

        _headerLabel.Text = $"Diff — {Path.GetFileName(path)}";

        _inlineStageBtn.Visible = !isStaged;
        _inlineUnstageBtn.Visible = isStaged;
        _inlineDiscardBtn.Visible = !isStaged;

        // Layout buttons in header
        LayoutHeaderButtons();

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
        _inlineStageBtn.Visible = false;
        _inlineUnstageBtn.Visible = false;
        _inlineDiscardBtn.Visible = false;
    }

    private void LayoutHeaderButtons()
    {
        int headerWidth = _modernHeader.ClientSize.Width - 16;
        int currentX = headerWidth;
        const int buttonSpacing = 4;

        // Right-align buttons from right to left
        if (_inlineDiscardBtn.Visible)
        {
            currentX -= _inlineDiscardBtn.Width;
            _inlineDiscardBtn.Location = new Point(currentX, 4);
            currentX -= buttonSpacing;
        }

        if (_inlineUnstageBtn.Visible)
        {
            currentX -= _inlineUnstageBtn.Width;
            _inlineUnstageBtn.Location = new Point(currentX, 4);
            currentX -= buttonSpacing;
        }

        if (_inlineStageBtn.Visible)
        {
            currentX -= _inlineStageBtn.Width;
            _inlineStageBtn.Location = new Point(currentX, 4);
        }
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
        _modernHeader.BackColor = theme.TerminalHeaderBackground;
        _headerLabel.ForeColor = theme.Text;

        _diffBox.BackColor = theme.EditorBackground;
        _diffBox.ForeColor = theme.Text;

        _inlineStageBtn.BackColor = theme.Background;
        _inlineStageBtn.ForeColor = theme.Accent;
        _inlineStageBtn.FlatAppearance.BorderColor = theme.Border;
        _inlineStageBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _inlineUnstageBtn.BackColor = theme.Background;
        _inlineUnstageBtn.ForeColor = theme.Text;
        _inlineUnstageBtn.FlatAppearance.BorderColor = theme.Border;
        _inlineUnstageBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;

        _inlineDiscardBtn.BackColor = theme.Background;
        _inlineDiscardBtn.ForeColor = Color.FromArgb(220, 80, 80);
        _inlineDiscardBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 60, 60);
        _inlineDiscardBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 30, 30);

        ApplyScrollbarTheme(_diffBox.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : "", null);
    }
}

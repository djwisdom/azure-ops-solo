using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Bottom-panel UserControl that streams and displays <c>dotnet build</c> output.
/// Colour-codes error lines (red), warning lines (amber), and normal lines (theme text).
/// Clicking an error or warning line fires <see cref="DiagnosticNavigated"/> so the
/// editor can jump to the relevant file and line.
/// </summary>
internal sealed class BuildOutputPanel : UserControl
{
    // ── Controls ─────────────────────────────────────────────────────────────

    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Button _buildBtn;
    private readonly Button _cancelBtn;
    private readonly Button _clearBtn;
    private readonly RichTextBox _output;

    // ── State ─────────────────────────────────────────────────────────────────

    private Theme _theme;
    private bool _isBusy;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fires when the user clicks on a diagnostic line. (filePath, lineNumber)</summary>
    public event Action<string, int>? DiagnosticNavigated;

    /// <summary>Fires when the user clicks the Build button.</summary>
    public event Action? BuildRequested;

    /// <summary>Fires when the user clicks the Cancel button.</summary>
    public event Action? CancelRequested;

    // ── Construction ─────────────────────────────────────────────────────────

    public BuildOutputPanel()
    {
        _theme = ThemeManager.Instance.CurrentTheme;
        Dock = DockStyle.Fill;
        Padding = Padding.Empty;
        Margin = Padding.Empty;
        BackColor = _theme.MenuBackground;

        // Header bar
        _header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = _theme.TerminalHeaderBackground,
            Padding = new Padding(4, 0, 4, 0)
        };

        _titleLabel = new Label
        {
            Text = "Build Output",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = _theme.Text,
            Dock = DockStyle.Left,
            Width = 100
        };

        _statusLabel = new Label
        {
            Text = "",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = _theme.Muted,
            Dock = DockStyle.Fill
        };

        _clearBtn = MakeHeaderButton("⌫", "Clear output");
        _cancelBtn = MakeHeaderButton("■ Cancel", "Cancel build");
        _buildBtn = MakeHeaderButton("▶ Build", "Build solution (Ctrl+Shift+B)");

        _clearBtn.Click += (_, __) => Clear();
        _buildBtn.Click += (_, __) => BuildRequested?.Invoke();
        _cancelBtn.Click += (_, __) => CancelRequested?.Invoke();
        _cancelBtn.Visible = false;

        _header.Controls.Add(_statusLabel);
        _header.Controls.Add(_cancelBtn);
        _header.Controls.Add(_buildBtn);
        _header.Controls.Add(_clearBtn);
        _header.Controls.Add(_titleLabel);

        // Output box
        _output = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            Font = new Font("Consolas", 9f),
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false,
            Multiline = true,
            BorderStyle = BorderStyle.None,
            Margin = Padding.Empty
        };
        _output.MouseClick += Output_MouseClick;

        Controls.Add(_output);
        Controls.Add(_header);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Appends a line of build output, colour-coded by content.</summary>
    public void AppendLine(string line, bool isStdErr)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => AppendLine(line, isStdErr)); return; }

        var diag = BuildService.ParseDiagnostic(line);
        Color color = diag?.Severity switch
        {
            DiagnosticSeverity.Error => Color.FromArgb(255, 100, 100),
            DiagnosticSeverity.Warning => Color.FromArgb(255, 185, 50),
            _ => isStdErr ? Color.FromArgb(220, 140, 100) : _theme.Text
        };

        _output.SuspendLayout();
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = color;
        _output.AppendText(line + "\n");
        _output.SelectionColor = _theme.Text;
        _output.ScrollToCaret();
        _output.ResumeLayout();
    }

    /// <summary>Clears all output text and resets the status label.</summary>
    public void Clear()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(Clear); return; }
        _output.Clear();
        _statusLabel.Text = "";
    }

    /// <summary>Marks the panel as busy (build running) or idle.</summary>
    public void SetBusy(bool busy)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => SetBusy(busy)); return; }
        _isBusy = busy;
        _buildBtn.Visible = !busy;
        _cancelBtn.Visible = busy;
        _statusLabel.ForeColor = _theme.Muted;
        _statusLabel.Text = busy ? "Building…" : "";
    }

    /// <summary>Shows a build-complete summary in the status label.</summary>
    public void ShowResult(BuildResult result)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => ShowResult(result)); return; }
        SetBusy(false);
        bool ok = result.Success;
        _statusLabel.ForeColor = ok ? Color.FromArgb(100, 200, 100) : Color.FromArgb(255, 100, 100);
        string elapsed = result.Elapsed.TotalSeconds < 60
            ? $"{result.Elapsed.TotalSeconds:F1}s"
            : $"{(int)result.Elapsed.TotalMinutes}m {result.Elapsed.Seconds}s";
        _statusLabel.Text = ok
            ? $"Build succeeded  {result.WarningCount} warning(s)  {elapsed}"
            : $"Build FAILED  {result.ErrorCount} error(s)  {result.WarningCount} warning(s)  {elapsed}";
    }

    /// <summary>Applies a new theme to the panel.</summary>
    public void SetTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.TerminalHeaderBackground;
        _titleLabel.ForeColor = theme.Text;
        _statusLabel.ForeColor = theme.Muted;
        _output.BackColor = theme.EditorBackground;
        _output.ForeColor = theme.Text;
        foreach (Button b in new[] { _buildBtn, _cancelBtn, _clearBtn })
        {
            b.BackColor = theme.TerminalHeaderBackground;
            b.ForeColor = theme.Text;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Button MakeHeaderButton(string text, string tooltip)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = _theme.TerminalHeaderBackground,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 8f),
            Padding = new Padding(4, 1, 4, 1),
            Margin = new Padding(2, 2, 2, 2),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        new ToolTip().SetToolTip(btn, tooltip);
        return btn;
    }

    private void Output_MouseClick(object? sender, MouseEventArgs e)
    {
        int charIndex = _output.GetCharIndexFromPosition(e.Location);
        int lineIndex = _output.GetLineFromCharIndex(charIndex);
        if (lineIndex < 0 || lineIndex >= _output.Lines.Length) return;

        string line = _output.Lines[lineIndex];
        var diag = BuildService.ParseDiagnostic(line);
        if (diag?.FilePath != null && File.Exists(diag.FilePath))
            DiagnosticNavigated?.Invoke(diag.FilePath, diag.Line);
    }
}

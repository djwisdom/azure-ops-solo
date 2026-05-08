using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class CloneRepositoryDialog : Form
{
    private readonly Form1 _mainForm;
    private TextBox _urlTextBox = null!;
    private TextBox _pathTextBox = null!;
    private Button _browseButton = null!;
    private Button _cloneButton = null!;
    private Button _cancelButton = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;
    private Process? _cloneProcess;
    private readonly CancellationTokenSource _cts = new();
    private bool _cloning;

    public CloneRepositoryDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        Text = "Clone Repository";
        Size = new Size(540, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        InitializeForm();
        ApplyTheme();
    }

    private void InitializeForm()
    {
        var urlLabel = new Label
        {
            Text = "Repository &URL:",
            Location = new Point(12, 15),
            AutoSize = true
        };

        _urlTextBox = new TextBox
        {
            Location = new Point(12, 32),
            Width = 500
        };

        var pathLabel = new Label
        {
            Text = "Local &path:",
            Location = new Point(12, 62),
            AutoSize = true
        };

        _pathTextBox = new TextBox
        {
            Location = new Point(12, 79),
            Width = 410
        };

        _browseButton = new Button
        {
            Text = "&Browse...",
            Location = new Point(428, 77),
            Width = 84,
            Height = 26
        };
        _browseButton.Click += BrowseButton_Click;

        _cloneButton = new Button
        {
            Text = "&Clone",
            Location = new Point(340, 175),
            Width = 80,
            Height = 30,
            Enabled = false
        };
        _cloneButton.Click += CloneButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(426, 175),
            Width = 80,
            Height = 30
        };
        _cancelButton.Click += (s, e) => CancelClone();

        _statusLabel = new Label
        {
            Location = new Point(12, 118),
            Width = 500,
            Height = 40,
            AutoEllipsis = true
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(12, 155),
            Width = 310,
            Height = 22,
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        _urlTextBox.TextChanged += (s, e) => UpdateCloneButton();
        _pathTextBox.TextChanged += (s, e) => UpdateCloneButton();

        AcceptButton = _cloneButton;
        CancelButton = _cancelButton;

        Controls.AddRange(new Control[]
        {
            urlLabel, _urlTextBox,
            pathLabel, _pathTextBox, _browseButton,
            _statusLabel, _progressBar,
            _cloneButton, _cancelButton
        });
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        foreach (Control c in Controls)
        {
            if (c is Button btn && btn != _cloneButton && btn != _cancelButton)
            {
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = theme.PanelBackground;
                btn.ForeColor = theme.Text;
                btn.FlatAppearance.BorderColor = theme.Border;
                btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
            }
            else if (c is TextBox tb)
            {
                tb.BackColor = theme.EditorBackground;
                tb.ForeColor = theme.Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is Label lbl)
            {
                lbl.BackColor = Color.Transparent;
                lbl.ForeColor = theme.Text;
            }
        }

        _cloneButton.FlatStyle = FlatStyle.Flat;
        _cloneButton.BackColor = theme.Accent;
        _cloneButton.ForeColor = theme.Background;
        _cloneButton.FlatAppearance.BorderColor = theme.Border;

        _cancelButton.FlatStyle = FlatStyle.Flat;
        _cancelButton.BackColor = theme.PanelBackground;
        _cancelButton.ForeColor = theme.Text;
        _cancelButton.FlatAppearance.BorderColor = theme.Border;
        _cancelButton.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select a directory to clone into",
            UseDescriptionForTitle = true
        };

        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "source",
            "repos");

        if (Directory.Exists(defaultPath))
            dlg.SelectedPath = defaultPath;
        else if (!string.IsNullOrEmpty(_mainForm.WorkspaceRoot))
            dlg.SelectedPath = _mainForm.WorkspaceRoot;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            _pathTextBox.Text = dlg.SelectedPath;
    }

    private void UpdateCloneButton()
    {
        bool valid = Uri.IsWellFormedUriString(_urlTextBox.Text, UriKind.Absolute) &&
                     !string.IsNullOrWhiteSpace(_pathTextBox.Text);
        _cloneButton.Enabled = valid && !_cloning;
    }

    private void CloneButton_Click(object? sender, EventArgs e)
    {
        string url = _urlTextBox.Text.Trim();
        string destDir = _pathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(destDir))
            return;

        string repoName = GetRepoName(url);
        string targetPath = Path.Combine(destDir, repoName);

        if (Directory.Exists(targetPath))
        {
            var result = ThemedMessageBox.Show(
                $"Directory already exists:\n{targetPath}\n\nClone into it anyway?",
                "Directory Exists",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
                return;
        }

        _cloning = true;
        _cloneButton.Enabled = false;
        _urlTextBox.ReadOnly = true;
        _pathTextBox.ReadOnly = true;
        _browseButton.Enabled = false;
        _progressBar.Visible = true;
        _statusLabel.Text = $"Cloning {repoName}...";

        try
        {
            Directory.CreateDirectory(destDir);

            _cloneProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone \"{url}\" \"{targetPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _cloneProcess.OutputDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    BeginInvoke(() => _statusLabel.Text = TruncateStatus(args.Data));
                }
            };

            _cloneProcess.ErrorDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    BeginInvoke(() => _statusLabel.Text = TruncateStatus(args.Data));
                }
            };

            _cloneProcess.Exited += (s, args) =>
            {
                BeginInvoke(() => OnCloneCompleted(_cloneProcess!, targetPath));
            };

            _cloneProcess.Start();
            _cloneProcess.BeginOutputReadLine();
            _cloneProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _cloning = false;
            _cloneButton.Enabled = true;
            _urlTextBox.ReadOnly = false;
            _pathTextBox.ReadOnly = false;
            _browseButton.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private void OnCloneCompleted(Process process, string targetPath)
    {
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            _statusLabel.Text = "Clone completed successfully.";
            _progressBar.Visible = false;

            var result = ThemedMessageBox.Show(
                $"Repository cloned successfully.\n\nOpen {Path.GetFileName(targetPath)} as workspace?",
                "Clone Complete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                _mainForm.OpenWorkspaceFolder(targetPath);
            }

            Close();
        }
        else
        {
            _cloning = false;
            _cloneButton.Enabled = true;
            _urlTextBox.ReadOnly = false;
            _pathTextBox.ReadOnly = false;
            _browseButton.Enabled = true;
            _progressBar.Visible = false;
            _statusLabel.Text = "Clone failed. Check the URL and try again.";
        }
    }

    private void CancelClone()
    {
        if (_cloning && _cloneProcess is { HasExited: false })
        {
            try
            {
                _cloneProcess.Kill(entireProcessTree: true);
            }
            catch { }
            _cloneProcess = null;
            _cloning = false;
        }
        Close();
    }

    private static string GetRepoName(string url)
    {
        url = url.TrimEnd('/');
        if (url.EndsWith(".git"))
            url = url[..^4];

        int lastSlash = url.LastIndexOf('/');
        return lastSlash >= 0 ? url[(lastSlash + 1)..] : "repo";
    }

    private static string TruncateStatus(string message)
    {
        if (message.Length > 80)
            return message[..77] + "...";
        return message;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cloning && _cloneProcess is { HasExited: false })
        {
            var result = ThemedMessageBox.Show(
                "Cloning is in progress. Cancel?",
                "Clone In Progress",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try { _cloneProcess.Kill(entireProcessTree: true); }
                catch { }
            }
            else
            {
                e.Cancel = true;
            }
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}

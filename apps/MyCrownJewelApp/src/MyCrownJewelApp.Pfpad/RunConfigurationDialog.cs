using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class RunConfigurationDialog : Form
{
    private readonly ListBox _profileList;
    private readonly Label _statusLabel;
    private readonly Button _runButton;
    private readonly Button _stopButton;
    private readonly string _workspaceRoot;
    private List<LaunchProfileParser.LaunchProfile> _profiles = new();
    private Process? _runningProcess;

    public RunConfigurationDialog(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        Text = "Run Configurations";
        Size = new Size(500, 400);
        MinimumSize = new Size(350, 250);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _statusLabel = new Label
        {
            Text = "Loading profiles...",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Muted
        };

        _profileList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            IntegralHeight = false,
            ItemHeight = 22
        };
        _profileList.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            string text = _profileList.Items[e.Index].ToString() ?? "";
            bool isHeader = text.StartsWith('\u2501');
            bool isSelected = (e.State & DrawItemState.Selected) != 0;
            using var bg = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
            e.Graphics.FillRectangle(bg, e.Bounds);
            using var f = new Font("Segoe UI", isHeader ? 8 : 9, isHeader ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(e.Graphics, text, f, e.Bounds with { X = e.Bounds.X + 4 },
                isHeader ? theme.Muted : theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
        };
        _profileList.SelectedIndexChanged += (s, e) => UpdateButtonState();
        _profileList.MouseDoubleClick += (s, e) => RunSelected();

        _runButton = new Button
        {
            Text = "\u25B6 Run",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(100, 28),
            Cursor = Cursors.Hand,
            Enabled = false
        };
        _runButton.Click += (s, e) => RunSelected();

        _stopButton = new Button
        {
            Text = "\u25A0 Stop",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(200, 60, 60),
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Size = new Size(100, 28),
            Cursor = Cursors.Hand,
            Visible = false
        };
        _stopButton.Click += (s, e) => StopProcess();

        var closeBtn = new Button
        {
            Text = "Close",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Muted },
            Size = new Size(80, 28),
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => { StopProcess(); Close(); };

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            Height = 40,
            BackColor = theme.Background
        };
        btnPanel.Controls.Add(closeBtn);
        btnPanel.Controls.Add(_stopButton);
        btnPanel.Controls.Add(_runButton);

        Controls.Add(_profileList);
        Controls.Add(_statusLabel);
        Controls.Add(btnPanel);

        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        string root = _workspaceRoot;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
            {
                if (dir.EnumerateFiles("*.sln").Any() || dir.EnumerateFiles("*.csproj").Any())
                { root = dir.FullName; break; }
            }
        }

        _profiles = LaunchProfileParser.ScanWorkspace(root);
        var grouped = _profiles.GroupBy(p => Path.GetFileNameWithoutExtension(p.ProjectPath));

        _profileList.BeginUpdate();
        _profileList.Items.Clear();
        foreach (var group in grouped)
        {
            _profileList.Items.Add($"\u2501 {group.Key} \u2501");
            foreach (var p in group)
                _profileList.Items.Add($"    {p.Name}");
        }
        if (_profiles.Count == 0)
            _profileList.Items.Add("(no launch profiles or .env files found)");
        _profileList.EndUpdate();

        _statusLabel.Text = $"{_profiles.Count} profile(s) found";
        UpdateButtonState();
    }

    private void RunSelected()
    {
        if (_profileList.SelectedItem?.ToString() is not string s || s.StartsWith('\u2501')) return;
        string name = s.Trim();
        var profile = _profiles.FirstOrDefault(p => p.Name == name);
        if (profile == null) return;

        _runningProcess = new Process();
        _runningProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{profile.ProjectPath}\"",
            WorkingDirectory = profile.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var kvp in profile.EnvironmentVariables)
            _runningProcess.StartInfo.Environment[kvp.Key] = kvp.Value;

        try
        {
            _runningProcess.Start();
            _runningProcess.BeginOutputReadLine();
            _runningProcess.BeginErrorReadLine();
            _runningProcess.OutputDataReceived += (s, args) =>
            {
                if (args.Data != null)
                    BeginInvoke(() => _statusLabel.Text = args.Data.Length > 80 ? args.Data[..80] + "..." : args.Data);
            };
            _runningProcess.ErrorDataReceived += (s, args) =>
            {
                if (args.Data != null)
                    BeginInvoke(() => _statusLabel.Text = $"Error: {args.Data}");
            };
            _runningProcess.Exited += (s, args) =>
            {
                BeginInvoke(() => { _runningProcess = null; _statusLabel.Text = "Process exited"; UpdateButtonState(); });
            };
            _runningProcess.EnableRaisingEvents = true;

            _statusLabel.Text = $"Running {profile.Name}...";
            _runButton.Visible = false;
            _stopButton.Visible = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Failed: {ex.Message}";
        }
    }

    private void StopProcess()
    {
        if (_runningProcess != null && !_runningProcess.HasExited)
        {
            try { _runningProcess.Kill(); } catch { }
            _runningProcess = null;
        }
        _runButton.Visible = true;
        _stopButton.Visible = false;
        _statusLabel.Text = "Stopped";
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        bool hasSel = _profileList.SelectedItem?.ToString() is string sel && !sel.StartsWith('\u2501');
        _runButton.Enabled = hasSel && _runningProcess == null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) StopProcess();
        base.Dispose(disposing);
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class NewProjectDialog : Form
{
    private readonly Form1 _mainForm;
    private ListView _templateList = null!;
    private TextBox _nameTextBox = null!;
    private TextBox _locationTextBox = null!;
    private Button _browseButton = null!;
    private CheckBox _solutionCheckBox = null!;
    private ComboBox _frameworkCombo = null!;
    private Button _createButton = null!;
    private Button _cancelButton = null!;
    private Label _statusLabel = null!;
    private bool _creating;

    public NewProjectDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        Text = "New Project";
        Size = new Size(640, 520);
        MinimumSize = new Size(640, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        InitializeForm();
        ApplyTheme();
        LoadTemplates();
        LoadFrameworks();
    }

    private void InitializeForm()
    {
        var templateLabel = new Label
        {
            Text = "&Template:",
            Location = new Point(12, 12),
            AutoSize = true
        };

        _templateList = new ListView
        {
            Location = new Point(12, 28),
            Size = new Size(600, 200),
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Clickable
        };
        _templateList.Columns.Add("Name", 180);
        _templateList.Columns.Add("Description", 300);
        _templateList.Columns.Add("Tags", 100);

        var nameLabel = new Label
        {
            Text = "Project &name:",
            Location = new Point(12, 240),
            AutoSize = true
        };

        _nameTextBox = new TextBox
        {
            Text = "MyApp",
            Location = new Point(12, 256),
            Width = 300
        };

        var locationLabel = new Label
        {
            Text = "&Location:",
            Location = new Point(12, 286),
            AutoSize = true
        };

        _locationTextBox = new TextBox
        {
            Location = new Point(12, 302),
            Width = 500
        };
        // Default to Documents or last workspace parent
        string? wsRoot = _mainForm.WorkspaceRoot;
        if (!string.IsNullOrEmpty(wsRoot))
        {
            var parent = Directory.GetParent(wsRoot);
            if (parent != null)
                _locationTextBox.Text = parent.FullName;
        }
        if (string.IsNullOrEmpty(_locationTextBox.Text))
            _locationTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        _browseButton = new Button
        {
            Text = "&Browse...",
            Location = new Point(518, 300),
            Width = 94,
            Height = 26
        };
        _browseButton.Click += BrowseButton_Click;

        _solutionCheckBox = new CheckBox
        {
            Text = "Create &solution file (.sln)",
            Location = new Point(12, 334),
            AutoSize = true,
            Checked = true
        };

        var frameworkLabel = new Label
        {
            Text = "&Framework:",
            Location = new Point(12, 362),
            AutoSize = true
        };

        _frameworkCombo = new ComboBox
        {
            Location = new Point(12, 378),
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        _statusLabel = new Label
        {
            Text = "",
            Location = new Point(12, 410),
            Size = new Size(600, 30),
            ForeColor = Color.Gray
        };

        _createButton = new Button
        {
            Text = "&Create",
            Location = new Point(440, 445),
            Width = 80,
            Height = 28
        };
        _createButton.Click += CreateButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(530, 445),
            Width = 80,
            Height = 28,
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[] {
            templateLabel, _templateList,
            nameLabel, _nameTextBox,
            locationLabel, _locationTextBox, _browseButton,
            _solutionCheckBox,
            frameworkLabel, _frameworkCombo,
            _statusLabel,
            _createButton, _cancelButton
        });
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _templateList.BackColor = theme.EditorBackground;
        _templateList.ForeColor = theme.Text;

        _nameTextBox.BackColor = theme.EditorBackground;
        _nameTextBox.ForeColor = theme.Text;

        _locationTextBox.BackColor = theme.EditorBackground;
        _locationTextBox.ForeColor = theme.Text;

        _frameworkCombo.BackColor = theme.EditorBackground;
        _frameworkCombo.ForeColor = theme.Text;

        _createButton.BackColor = theme.PanelBackground;
        _createButton.ForeColor = theme.Text;

        _cancelButton.BackColor = theme.PanelBackground;
        _cancelButton.ForeColor = theme.Text;

        _solutionCheckBox.ForeColor = theme.Text;
    }

    private void LoadTemplates()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "new list")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;

            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(20000);

            if (proc.ExitCode != 0) return;

            ParseTemplateList(stdout);
        }
        catch
        {
            // fallback: let user type template name manually
        }

        if (_templateList.Items.Count == 0)
        {
            // Fallback: show a textbox hint and allow manual entry
            _statusLabel.Text = "Could not load templates from SDK. Type template name manually.";
        }
    }

    private void ParseTemplateList(string output)
    {
        // dotnet new list output format (pipe-delimited columns):
        // Template Name                       Short Name     Language    Tags
        // ----------------------------------- -------------- ----------- --------------------------
        // ASP.NET Core Empty                  webapi          [C#]        Web/API
        // Console App                         console         [C#]        Common/Console
        // Class Library                       classlib        [C#]        Common/Library
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool inData = false;
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("---"))
            {
                inData = true;
                continue;
            }
            if (!inData || string.IsNullOrEmpty(line)) continue;

            // Split on 2+ spaces to handle pipe-delimited columns
            var parts = Regex.Split(line, @"\s{2,}");
            if (parts.Length < 2) continue;

            string tplName = parts[0].Trim();
            string shortName = parts.Length > 1 ? parts[1].Trim() : "";
            string language = parts.Length > 2 ? parts[2].Trim().Trim('[', ']') : "";
            string tags = parts.Length > 3 ? parts[3].Trim() : "";

            // Filter to C# templates by default
            if (!string.IsNullOrEmpty(language) && !language.Equals("C#", StringComparison.OrdinalIgnoreCase))
                continue;

            var item = new ListViewItem(shortName);
            item.SubItems.Add(tplName);
            item.SubItems.Add(tags);
            item.Tag = shortName;
            _templateList.Items.Add(item);
        }

        // Auto-select first item
        if (_templateList.Items.Count > 0)
            _templateList.Items[0].Selected = true;
    }

    private void LoadFrameworks()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;

            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            // Parse SDK versions like "8.0.403" or "9.0.102" → extract major.minor
            var versions = new HashSet<string>();
            foreach (Match m in Regex.Matches(stdout, @"(\d+\.\d+)\.\d+"))
            {
                versions.Add(m.Groups[1].Value);
            }

            foreach (var v in versions.OrderByDescending(x => x))
                _frameworkCombo.Items.Add(v);

            if (_frameworkCombo.Items.Count > 0)
                _frameworkCombo.SelectedIndex = 0;
        }
        catch { }

        if (_frameworkCombo.Items.Count == 0)
        {
            _frameworkCombo.Items.Add("net8.0");
            _frameworkCombo.Items.Add("net9.0");
            _frameworkCombo.SelectedIndex = 0;
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        dlg.SelectedPath = _locationTextBox.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _locationTextBox.Text = dlg.SelectedPath;
    }

    private async void CreateButton_Click(object? sender, EventArgs e)
    {
        if (_creating) return;

        string template = _templateList.SelectedItems.Count > 0
            ? (_templateList.SelectedItems[0].Tag as string) ?? ""
            : "";
        string projectName = _nameTextBox.Text.Trim();
        string location = _locationTextBox.Text.Trim();
        bool createSolution = _solutionCheckBox.Checked;
        string? framework = _frameworkCombo.SelectedItem as string;

        if (string.IsNullOrEmpty(template))
        {
            _statusLabel.Text = "Please select a template.";
            return;
        }
        if (string.IsNullOrEmpty(projectName))
        {
            _statusLabel.Text = "Please enter a project name.";
            return;
        }
        if (string.IsNullOrEmpty(location) || !Directory.Exists(location))
        {
            _statusLabel.Text = "Please select a valid location.";
            return;
        }

        _creating = true;
        _createButton.Enabled = false;
        _cancelButton.Enabled = false;
        _statusLabel.ForeColor = Color.DarkBlue;

        string projectDir = Path.Combine(location, projectName);

        try
        {
            // Step 1: dotnet new
            _statusLabel.Text = $"Creating project '{projectName}'...";
            string newArgs = $"new \"{template}\" -n \"{projectName}\" -o \"{projectDir}\"";
            if (!string.IsNullOrEmpty(framework))
                newArgs += $" --framework {framework}";

            var newPsi = new ProcessStartInfo("dotnet", newArgs)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var newProc = Process.Start(newPsi);
            if (newProc == null) throw new Exception("Failed to start dotnet new process.");

            string newOut = await newProc.StandardOutput.ReadToEndAsync();
            string newErr = await newProc.StandardError.ReadToEndAsync();
            newProc.WaitForExit(120000);

            if (newProc.ExitCode != 0)
            {
                _statusLabel.ForeColor = Color.DarkRed;
                _statusLabel.Text = $"Error: {newErr.Trim()}";
                _creating = false;
                _createButton.Enabled = true;
                _cancelButton.Enabled = true;
                return;
            }

            // Step 2: create .sln and add project if checked
            if (createSolution)
            {
                _statusLabel.Text = "Creating solution file...";
                var slnPsi = new ProcessStartInfo("dotnet", $"new sln -o \"{location}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var slnProc = Process.Start(slnPsi);
                if (slnProc != null)
                {
                    await slnProc.StandardOutput.ReadToEndAsync();
                    slnProc.WaitForExit(30000);
                }

                // Find the .csproj in the project directory
                string? csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

                _statusLabel.Text = "Adding project to solution...";
                var addPsi = new ProcessStartInfo("dotnet", $"sln \"{location}\" add \"{csproj}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var addProc = Process.Start(addPsi);
                if (addProc != null)
                {
                    await addProc.StandardOutput.ReadToEndAsync();
                    addProc.WaitForExit(30000);
                }
            }

            _statusLabel.Text = "Project created successfully!";
            _statusLabel.ForeColor = Color.DarkGreen;

            // Auto-open the project folder as workspace
            _mainForm.BeginInvoke(() =>
            {
                _mainForm.OpenWorkspaceFolder(projectDir);
            });

            // Close dialog after brief delay
            await Task.Delay(500);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = $"Error: {ex.Message}";
            _creating = false;
            _createButton.Enabled = true;
            _cancelButton.Enabled = true;
        }
    }
}
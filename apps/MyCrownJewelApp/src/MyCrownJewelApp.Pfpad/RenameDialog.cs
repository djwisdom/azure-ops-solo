using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class RenameDialog : Form
{
    private readonly TextBox _newNameBox;
    private readonly ListView _previewList;
    private readonly Label _statusLabel;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly string _oldName;
    private readonly string _workspaceRoot;
    private CancellationTokenSource? _scanCts;
    private List<(string file, int line, string context)> _matches = new();

    public string NewName => _newNameBox.Text.Trim();
    public bool Applied { get; private set; }

    public RenameDialog(string oldName, string workspaceRoot)
    {
        _oldName = oldName;
        _workspaceRoot = workspaceRoot;

        Text = $"Rename: {oldName}";
        Size = new Size(700, 450);
        MinimumSize = new Size(500, 300);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10),
            BackColor = theme.Background
        };

        // Row 0: label
        var renameLabel = new Label
        {
            Text = $"Rename '{oldName}' to:",
            Font = new Font("Segoe UI", 10),
            AutoSize = true,
            ForeColor = theme.Text
        };
        mainPanel.Controls.Add(renameLabel, 0, 0);
        mainPanel.SetColumnSpan(renameLabel, 2);

        // Row 1: new name textbox
        _newNameBox = new TextBox
        {
            Text = oldName,
            Font = new Font("Consolas", 11),
            Dock = DockStyle.Fill,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        _newNameBox.SelectAll();
        _newNameBox.TextChanged += (s, args) => _okButton.Enabled = !string.IsNullOrWhiteSpace(_newNameBox.Text);
        mainPanel.Controls.Add(_newNameBox, 0, 1);
        mainPanel.SetColumnSpan(_newNameBox, 2);

        // Row 2: preview header
        _statusLabel = new Label
        {
            Text = "Scanning workspace...",
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            ForeColor = theme.Muted
        };
        mainPanel.Controls.Add(_statusLabel, 0, 2);
        mainPanel.SetColumnSpan(_statusLabel, 2);

        // Row 3: preview list
        _previewList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            MultiSelect = false
        };
        _previewList.Columns.Add("File", 200);
        _previewList.Columns.Add("Line", 50);
        _previewList.Columns.Add("Context", 400);
        mainPanel.Controls.Add(_previewList, 0, 3);
        mainPanel.SetColumnSpan(_previewList, 2);
        mainPanel.RowStyles.Clear();
        for (int i = 0; i < 5; i++)
            mainPanel.RowStyles.Add(new RowStyle(i == 3 ? SizeType.Percent : SizeType.AutoSize));

        // Row 4: buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 6, 0, 0),
            BackColor = theme.Background
        };

        _okButton = new Button
        {
            Text = "Rename",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(100, 28),
            BackColor = theme.Accent,
            ForeColor = Color.White,
            FlatAppearance = { BorderSize = 0 },
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _okButton.Click += Ok_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Size = new Size(80, 28),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Muted },
            Cursor = Cursors.Hand
        };
        _cancelButton.Click += (s, e) => DialogResult = DialogResult.Cancel;

        btnPanel.Controls.Add(_okButton);
        btnPanel.Controls.Add(_cancelButton);
        mainPanel.Controls.Add(btnPanel, 0, 4);
        mainPanel.SetColumnSpan(btnPanel, 2);

        Controls.Add(mainPanel);

        // Start background scan
        BeginInvoke(ScanWorkspace);
    }

    private async void ScanWorkspace()
    {
        if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
        {
            _statusLabel.Text = "No workspace folder open. Rename limited to current file.";
            return;
        }

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        try
        {
            var matches = await Task.Run(() => FindOccurrences(_workspaceRoot, _oldName, token), token);
            _matches = matches;

            _previewList.BeginUpdate();
            _previewList.Items.Clear();
            foreach (var (file, line, context) in matches)
            {
                var item = new ListViewItem(new[]
                {
                    Path.GetFileName(file),
                    line.ToString(),
                    context.Trim()
                })
                {
                    Tag = file
                };
                _previewList.Items.Add(item);
            }
            _previewList.EndUpdate();

            _statusLabel.Text = $"{matches.Count} occurrence(s) in {matches.Select(m => m.file).Distinct().Count()} file(s)";
            _okButton.Enabled = !string.IsNullOrWhiteSpace(_newNameBox.Text) && matches.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private static List<(string file, int line, string context)> FindOccurrences(string root, string name, CancellationToken token)
    {
        var results = new List<(string, int, string)>();
        var pattern = $@"\b{Regex.Escape(name)}\b";

        try
        {
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (token.IsCancellationRequested) break;
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string[] textExts = { ".cs", ".vb", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".html", ".htm", ".css", ".scss", ".md", ".txt", ".yaml", ".yml", ".toml", ".ini", ".csproj", ".sln", ".ps1", ".py", ".rb", ".java", ".kt", ".go", ".rs", ".c", ".cpp", ".h", ".hpp", ".sql", ".lua", ".php", ".tf", ".bicep", ".editorconfig", ".gitignore", ".props", ".targets", ".resx" };
                if (!textExts.Contains(ext)) continue;
                if (file.Contains("\\node_modules\\") || file.Contains("\\.git\\") || file.Contains("\\bin\\") || file.Contains("\\obj\\"))
                    continue;

                try
                {
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (Regex.IsMatch(lines[i], pattern))
                        {
                            results.Add((file, i + 1, lines[i].Trim()));
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        return results;
    }

    private void Ok_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_newNameBox.Text) || _matches.Count == 0) return;

        try
        {
            string pattern = $@"\b{Regex.Escape(_oldName)}\b";
            string replacement = _newNameBox.Text.Trim();

            // Group by file and replace
            var fileGroups = _matches.GroupBy(m => m.file);
            foreach (var group in fileGroups)
            {
                string content = File.ReadAllText(group.Key);
                content = Regex.Replace(content, pattern, replacement);
                File.WriteAllText(group.Key, content);
            }

            Applied = true;
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Rename failed: {ex.Message}", "Rename Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}

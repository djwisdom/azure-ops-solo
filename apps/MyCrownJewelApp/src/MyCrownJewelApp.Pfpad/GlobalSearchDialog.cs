using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

internal sealed class GlobalSearchDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly TextBox _searchBox;
    private readonly TextBox _replaceBox;
    private readonly CheckBox _caseCheck;
    private readonly CheckBox _regexCheck;
    private readonly TextBox _filterBox;
    private readonly TextBox _excludeBox;
    private readonly Button _searchButton;
    private readonly Button _stopButton;
    private readonly Button _replaceAllButton;
    private readonly Button _expandAllButton;
    private readonly Button _collapseAllButton;
    private readonly TreeView _resultsTree;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly StatusStrip _statusStrip;

    private CancellationTokenSource? _cts;
    private readonly HashSet<string> _defaultFilters;
    private string _workspaceRoot = "";
    private int _totalMatches;

    private static readonly HashSet<string> DefaultIgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".svn", ".hg", "bin", "obj", ".vs", "packages", ".terraform"
    };

    public GlobalSearchDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _defaultFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".vb", ".fs", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs",
            ".json", ".jsonc", ".xml", ".html", ".htm", ".css", ".scss", ".sass", ".less",
            ".md", ".txt", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".config",
            ".csproj", ".sln", ".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".sh", ".bash", ".zsh",
            ".py", ".rb", ".java", ".kt", ".swift", ".go", ".rs",
            ".c", ".cpp", ".cxx", ".cc", ".h", ".hpp", ".hxx", ".hh",
            ".sql", ".lua", ".php", ".tf", ".tfvars", ".bicep",
            ".editorconfig", ".gitignore", ".props", ".targets", ".resx", ".razor", ".cshtml"
        };

        Text = "Global Search";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        var mainTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(8)
        };

        var topPanel = new Panel { Height = 100, Dock = DockStyle.Top };

        int y = 4;
        int inputW = 500;
        int leftCol = 8;

        _searchBox = new TextBox
        {
            Location = new Point(leftCol, y),
            Width = inputW,
            Font = new Font("Segoe UI", 10)
        };
        _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BeginSearch(); } };

        _searchButton = new Button
        {
            Text = "Search",
            Location = new Point(leftCol + inputW + 6, y - 1),
            Width = 75,
            Height = 24
        };
        _searchButton.Click += (s, e) => BeginSearch();

        _stopButton = new Button
        {
            Text = "Stop",
            Location = new Point(leftCol + inputW + 87, y - 1),
            Width = 60,
            Height = 24,
            Enabled = false
        };
        _stopButton.Click += (s, e) => _cts?.Cancel();

        topPanel.Controls.AddRange(new Control[] { _searchBox, _searchButton, _stopButton });
        y += 30;

        _replaceBox = new TextBox
        {
            Location = new Point(leftCol, y),
            Width = inputW,
            Font = new Font("Segoe UI", 10)
        };
        _replaceBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BeginSearch(); } };
        var replaceLabel = new Label
        {
            Text = "Replace:",
            Location = new Point(leftCol - 52, y + 2),
            Width = 48,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8.25f)
        };
        topPanel.Controls.Add(replaceLabel);
        topPanel.Controls.Add(_replaceBox);
        y += 28;

        _caseCheck = new CheckBox
        {
            Text = "Case",
            Location = new Point(leftCol, y),
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = theme.Text
        };
        _regexCheck = new CheckBox
        {
            Text = "Regex",
            Location = new Point(leftCol + 70, y),
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = theme.Text
        };

        var filterLabel = new Label
        {
            Text = "Files:",
            Location = new Point(leftCol + 140, y + 2),
            AutoSize = true,
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8.25f)
        };
        _filterBox = new TextBox
        {
            Text = "*.cs, *.ts, *.js, *.json, *.md, *.xml, *.yaml, *.html, *.css, *.py, *.go, *.rs, *.tf, *.ps1, *.sh",
            Location = new Point(leftCol + 180, y - 1),
            Width = 280,
            Font = new Font("Segoe UI", 8.25f)
        };

        var excludeLabel = new Label
        {
            Text = "Exclude:",
            Location = new Point(leftCol + 466, y + 2),
            AutoSize = true,
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8.25f)
        };
        _excludeBox = new TextBox
        {
            Text = "node_modules, .git, bin, obj, .vs, packages, .terraform",
            Location = new Point(leftCol + 524, y - 1),
            Width = 340,
            Font = new Font("Segoe UI", 8.25f)
        };
        topPanel.Controls.AddRange(new Control[]
        {
            _caseCheck, _regexCheck, filterLabel, _filterBox, excludeLabel, _excludeBox
        });
        y += 28;

        _replaceAllButton = new Button
        {
            Text = "Replace All",
            Location = new Point(leftCol, y),
            Width = 100,
            Height = 24,
            Enabled = false
        };
        _replaceAllButton.Click += (s, e) => PerformReplaceAll();

        _expandAllButton = new Button
        {
            Text = "Expand All",
            Location = new Point(leftCol + 106, y),
            Width = 80,
            Height = 24
        };
        _expandAllButton.Click += (s, e) =>
        {
            if (_resultsTree is not null) _resultsTree.ExpandAll();
        };

        _collapseAllButton = new Button
        {
            Text = "Collapse All",
            Location = new Point(leftCol + 192, y),
            Width = 90,
            Height = 24
        };
        _collapseAllButton.Click += (s, e) =>
        {
            if (_resultsTree is not null) _resultsTree.CollapseAll();
        };

        topPanel.Controls.AddRange(new Control[] { _replaceAllButton, _expandAllButton, _collapseAllButton });
        topPanel.Height = y + 32;

        _resultsTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowPlusMinus = true,
            Font = new Font("Consolas", 9.25f)
        };
        _resultsTree.NodeMouseDoubleClick += ResultsTree_NodeMouseDoubleClick;
        _resultsTree.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && _resultsTree.SelectedNode != null)
                OpenResultNode(_resultsTree.SelectedNode);
        };

        _statusStrip = new StatusStrip
        {
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Text
        };
        _statusLabel = new ToolStripStatusLabel("Ready");
        _statusStrip.Items.Add(_statusLabel);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = topPanel.Height + 4,
            IsSplitterFixed = true,
            Panel1 = { Controls = { topPanel } },
            Panel2 = { Controls = { _resultsTree } }
        };
        splitContainer.Panel1MinSize = topPanel.Height;

        Controls.Add(splitContainer);
        Controls.Add(_statusStrip);

        if (_mainForm.WorkspaceRoot is { Length: > 0 } root && Directory.Exists(root))
            _workspaceRoot = root;
    }

    public string WorkspaceRoot
    {
        get => _workspaceRoot;
        set => _workspaceRoot = value;
    }

    private void BeginSearch()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _resultsTree.Nodes.Clear();
        _totalMatches = 0;
        _searchButton.Enabled = false;
        _stopButton.Enabled = true;
        _replaceAllButton.Enabled = false;
        _statusLabel.Text = "Searching...";

        if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
        {
            _statusLabel.Text = "No workspace folder open. Use Panel > Open Folder.";
            _searchButton.Enabled = true;
            _stopButton.Enabled = false;
            return;
        }

        string pattern = _searchBox.Text;
        if (string.IsNullOrEmpty(pattern))
        {
            _statusLabel.Text = "Enter a search pattern.";
            _searchButton.Enabled = true;
            _stopButton.Enabled = false;
            return;
        }

        bool useRegex = _regexCheck.Checked;
        bool caseSensitive = _caseCheck.Checked;
        Regex? regex = null;
        StringComparison comparison = caseSensitive
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;

        if (useRegex)
        {
            try
            {
                var opts = RegexOptions.Multiline;
                if (!caseSensitive) opts |= RegexOptions.IgnoreCase;
                regex = new Regex(pattern, opts, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Invalid regex: {ex.Message}";
                _searchButton.Enabled = true;
                _stopButton.Enabled = false;
                return;
            }
        }

        var includeExtensions = ParseFilterPatterns(_filterBox.Text);
        var excludeDirs = ParseFilterPatterns(_excludeBox.Text);
        excludeDirs.UnionWith(DefaultIgnoredDirs);

        _searchButton.Enabled = false;
        _stopButton.Enabled = true;

        var sw = Stopwatch.StartNew();
        var fileResults = new ConcurrentDictionary<string, ConcurrentBag<(int Line, string Text)>>();

        Task.Run(() =>
        {
            try
            {
                SearchDirectoryAsync(_workspaceRoot, _workspaceRoot, includeExtensions, excludeDirs,
                    pattern, regex, comparison, fileResults, token);
            }
            catch (OperationCanceledException) { }
        }, token).ContinueWith(_ =>
        {
            BeginInvoke(() => PopulateResults(fileResults, sw, token));
        }, token);
    }

    private void SearchDirectoryAsync(string rootDir, string dir,
        HashSet<string> extensions, HashSet<string> excludeDirs,
        string findText, Regex? regex, StringComparison comparison,
        ConcurrentDictionary<string, ConcurrentBag<(int Line, string Text)>> results,
        CancellationToken token)
    {
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            token.ThrowIfCancellationRequested();
            string name = Path.GetFileName(d);
            if (!excludeDirs.Contains(name))
                SearchDirectoryAsync(rootDir, d, extensions, excludeDirs, findText, regex, comparison, results, token);
        }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            token.ThrowIfCancellationRequested();
            string ext = Path.GetExtension(file);
            if (!extensions.Contains(ext) && !extensions.Contains("*.*") && !extensions.Contains("*"))
                continue;

            try
            {
                string relativePath = Path.GetRelativePath(rootDir, file);
                using var reader = new StreamReader(file);
                string? line;
                int lineNum = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    bool match = regex != null
                        ? regex.IsMatch(line)
                        : line.Contains(findText, comparison);
                    if (match)
                    {
                        results.GetOrAdd(relativePath, _ => new ConcurrentBag<(int, string)>())
                            .Add((lineNum, line.Trim()));
                    }
                }
            }
            catch { }
        }
    }

    private void PopulateResults(
        ConcurrentDictionary<string, ConcurrentBag<(int Line, string Text)>> fileResults,
        Stopwatch sw, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            _statusLabel.Text = "Search cancelled.";
            _searchButton.Enabled = true;
            _stopButton.Enabled = false;
            return;
        }

        _resultsTree.BeginUpdate();
        _resultsTree.Nodes.Clear();
        _totalMatches = 0;

        var theme = ThemeManager.Instance.CurrentTheme;
        var nodeFont = new Font("Segoe UI", 9.25f, FontStyle.Regular);
        var matchFont = new Font("Consolas", 9.25f);

        foreach (var kvp in fileResults.OrderBy(x => x.Key))
        {
            var fileNode = new TreeNode
            {
                Text = $"{kvp.Key}  ({kvp.Value.Count} matches)",
                Tag = kvp.Key,
                NodeFont = nodeFont,
                ForeColor = theme.Text,
                BackColor = theme.EditorBackground
            };

            foreach (var (lineNum, text) in kvp.Value.OrderBy(x => x.Line))
            {
                string display = $"  {lineNum,6}: {text}";
                var matchNode = new TreeNode
                {
                    Text = display,
                    Tag = (kvp.Key, lineNum),
                    NodeFont = matchFont,
                    ForeColor = theme.Text,
                    BackColor = theme.EditorBackground
                };
                fileNode.Nodes.Add(matchNode);
                _totalMatches++;
            }

            _resultsTree.Nodes.Add(fileNode);
        }

        _resultsTree.EndUpdate();

        int fileCount = fileResults.Count;
        long ms = sw.ElapsedMilliseconds;
        _statusLabel.Text = $"{_totalMatches} matches in {fileCount} files — {ms}ms";
        _searchButton.Enabled = true;
        _stopButton.Enabled = false;
        _replaceAllButton.Enabled = _totalMatches > 0 && !_regexCheck.Checked;

        if (_totalMatches > 0)
            _resultsTree.ExpandAll();
    }

    private void ResultsTree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        OpenResultNode(e.Node);
    }

    private void OpenResultNode(TreeNode node)
    {
        if (node.Tag is ValueTuple<string, int> tag)
        {
            string fullPath = Path.Combine(_workspaceRoot, tag.Item1);
            if (File.Exists(fullPath))
            {
                Form1? form = _mainForm ?? Application.OpenForms["Form1"] as Form1;
                form?.NavigateToFileLine(fullPath, tag.Item2);
            }
        }
    }

    private void PerformReplaceAll()
    {
        if (_regexCheck.Checked)
        {
            _statusLabel.Text = "Replace All is not supported with regex. Disable regex to use Replace All.";
            return;
        }

        string findText = _searchBox.Text;
        string replaceText = _replaceBox.Text;
        bool caseSensitive = _caseCheck.Checked;
        var comparison = caseSensitive
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;

        if (string.IsNullOrEmpty(findText)) return;

        int totalReplacements = 0;
        int fileCount = 0;

        foreach (TreeNode fileNode in _resultsTree.Nodes)
        {
            if (fileNode.Tag is not string filePath) continue;
            string fullPath = Path.Combine(_workspaceRoot, filePath);
            if (!File.Exists(fullPath)) continue;

            try
            {
                string content = File.ReadAllText(fullPath);
                int count = 0;
                string newContent = ReplaceIgnoreCase(content, findText, replaceText, comparison, ref count);
                if (count > 0)
                {
                    File.WriteAllText(fullPath, newContent);
                    totalReplacements += count;
                    fileCount++;
                    UpdateFileNodeResults(fileNode, findText, comparison);
                }
            }
            catch { }
        }

        _statusLabel.Text = $"Replaced {totalReplacements} occurrences across {fileCount} files.";
        _mainForm?.RefreshWorkspace();
        BeginSearch();
    }

    private static string ReplaceIgnoreCase(string text, string find, string replace, StringComparison comparison, ref int count)
    {
        int pos = 0;
        var sb = new System.Text.StringBuilder();
        while (pos < text.Length)
        {
            int idx = text.IndexOf(find, pos, comparison);
            if (idx < 0) break;
            sb.Append(text, pos, idx - pos);
            sb.Append(replace);
            pos = idx + find.Length;
            count++;
        }
        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }

    private void UpdateFileNodeResults(TreeNode fileNode, string findText, StringComparison comparison)
    {
        string filePath = (string)fileNode.Tag;
        string fullPath = Path.Combine(_workspaceRoot, filePath);
        if (!File.Exists(fullPath)) return;

        try
        {
            string[] lines = File.ReadAllLines(fullPath);
            int matchCount = 0;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains(findText, comparison))
                    matchCount++;

            fileNode.Text = $"{filePath}  ({matchCount} matches)";
            fileNode.Nodes.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(findText, comparison))
                {
                    fileNode.Nodes.Add(new TreeNode($"  {i + 1,6}: {lines[i].Trim()}")
                    {
                        Tag = (filePath, i + 1)
                    });
                }
            }
        }
        catch { }
    }

    private static HashSet<string> ParseFilterPatterns(string input)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return set;

        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string p = part.Trim();
            if (p.StartsWith("*."))
                set.Add(p[1..]);
            else if (p.StartsWith('.'))
                set.Add(p);
            else if (p == "*" || p == "*.*")
                set.Add("*.*");
            else
                set.Add(p);
        }
        return set;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            Close();
        else if (e.Control && e.KeyCode == Keys.F)
            _searchBox.Focus();
        else if (e.Control && e.KeyCode == Keys.H)
            _replaceBox.Focus();
        base.OnKeyDown(e);
    }
}

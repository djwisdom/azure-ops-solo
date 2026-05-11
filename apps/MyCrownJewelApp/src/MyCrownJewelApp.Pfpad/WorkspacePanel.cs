using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyCrownJewelApp.Pfpad;

internal sealed class WorkspacePanel : UserControl
{
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private const int TVS_LINESATROOT = 0x1000;
    private const int GWL_STYLE = -16;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private static void RemoveRootLinesStyle(IntPtr hWnd)
    {
        int style = GetWindowLong(hWnd, GWL_STYLE);
        if ((style & TVS_LINESATROOT) != 0)
            SetWindowLong(hWnd, GWL_STYLE, style & ~TVS_LINESATROOT);
    }

    private readonly TreeView _tree;
    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _rootLabel;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _collapseButton;
    private readonly ToolStripButton _closeButton;
    private readonly ContextMenuStrip _fileContextMenu;
    private string _rootPath = "";
    private readonly HashSet<string> _textExtensions;
#pragma warning disable CS0649
    private bool _showAllFiles;
#pragma warning restore CS0649
    private bool _projectFilterEnabled;
    private HashSet<string> _projectFiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _detectedProjectFile;
    private readonly ToolStripButton _projectFilterButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CancellationTokenSource? _scanCts;
    private FileSystemWatcher? _fileWatcher;

    public event Action<string>? FileOpenRequested;
    public event Action? CloseRequested;
    public event Action? ScanStarted;
    public event Action? ScanCompleted;
    public event Action<string>? ScanProgressChanged;

    public string RootPath => _rootPath;
    public bool IsScanning => _scanCts is not null;

    public void CancelScan()
    {
        if (_scanCts is not null)
        {
            _scanCts.Cancel();
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelScan();
            _refreshTimer?.Dispose();
            _fileWatcher?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static readonly HashSet<string> DefaultTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".vb", ".fs", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".html", ".htm",
        ".css", ".scss", ".less", ".md", ".txt", ".yaml", ".yml", ".toml", ".ini", ".cfg",
        ".config", ".csproj", ".sln", ".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".sh",
        ".bash", ".zsh", ".py", ".rb", ".java", ".kt", ".swift", ".go", ".rs", ".c", ".cpp",
        ".h", ".hpp", ".sql", ".r", ".m", ".mm", ".pl", ".pm", ".lua", ".php", ".asp",
        ".aspx", ".cshtml", ".razor", ".vue", ".svelte", ".tf", ".bicep", ".dockerfile",
        ".editorconfig", ".gitignore", ".gitattributes", ".env", ".nuspec", ".props",
        ".targets", ".resx", ".svg", ".tsv"
    };

    public WorkspacePanel()
    {
        _textExtensions = new HashSet<string>(DefaultTextExtensions, StringComparer.OrdinalIgnoreCase);

        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = false,
            HotTracking = false,
            FullRowSelect = true,
            HideSelection = false,
            LabelEdit = false,
            Indent = 20,
            ItemHeight = 24,
            DrawMode = TreeViewDrawMode.Normal,
            ImageList = FileIconProvider.ImageList
        };
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
        _tree.MouseDown += Tree_MouseDown;
        _tree.KeyDown += Tree_KeyDown;
        _tree.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        _tree.DragDrop += Tree_DragDrop;
        _tree.AllowDrop = true;
        _tree.HandleCreated += (s, e) =>
        {
            ApplyScrollbarTheme(_tree.Handle);
            RemoveRootLinesStyle(_tree.Handle);
        };

        _rootLabel = new ToolStripLabel("Workspace")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Margin = new Padding(6, 2, 0, 2)
        };

        _refreshButton = new ToolStripButton
        {
            Text = "\u21BB",
            Font = new Font("Segoe UI", 10),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Refresh"
        };
        _refreshButton.Click += (s, e) => RefreshTree();

        _collapseButton = new ToolStripButton
        {
            Text = "\u25BC",
            Font = new Font("Segoe UI", 8),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Collapse All"
        };
        _collapseButton.Click += (s, e) =>
        {
            if (_tree.Nodes.Count > 0 && _tree.Nodes[0].IsExpanded)
            {
                _tree.CollapseAll();
                _collapseButton.Text = "\u25B6";
                _collapseButton.ToolTipText = "Expand All";
            }
            else
            {
                var root = _tree.Nodes.Count > 0 ? _tree.Nodes[0] : null;
                if (root != null)
                {
                    EnsureNodePopulated(root);
                    root.Expand();
                }
                _collapseButton.Text = "\u25BC";
                _collapseButton.ToolTipText = "Collapse All";
            }
        };

        _projectFilterButton = new ToolStripButton
        {
            Text = "\uD83D\uDCC4",
            Font = new Font("Segoe UI", 9),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Show Project Files Only — no .sln/.csproj detected",
            Enabled = false
        };
        _projectFilterButton.Click += (s, e) =>
        {
            _projectFilterEnabled = !_projectFilterEnabled;
            UpdateProjectFilterVisual();
            if (!string.IsNullOrEmpty(_rootPath))
                RefreshTree();
        };

        _closeButton = new ToolStripButton
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right,
            AutoSize = false,
            Width = 22,
            Height = 22
        };
        _closeButton.Click += (s, e) => CloseRequested?.Invoke();

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(4, 0, 0, 0),
            AutoSize = false,
            Height = 24,
            Renderer = new FlatToolStripRenderer()
        };
        _headerStrip.Items.Add(_rootLabel);
        _headerStrip.Items.Add(_refreshButton);
        _headerStrip.Items.Add(_collapseButton);
        _headerStrip.Items.Add(_projectFilterButton);
        _headerStrip.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
        _headerStrip.Items.Add(_closeButton);

        _fileContextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open", null, (s, e) => OpenSelectedNode());
        var openFolderItem = new ToolStripMenuItem("Open Containing Folder", null, (s, e) => OpenContainingFolder());
        var copyPathItem = new ToolStripMenuItem("Copy Path", null, (s, e) => CopyPath());
        _fileContextMenu.Items.AddRange(new ToolStripItem[] {
            openItem, openFolderItem, new ToolStripSeparator(),
            new ToolStripMenuItem("New File", null, (s, e) => CreateNewFile()),
            new ToolStripMenuItem("New Folder", null, (s, e) => CreateNewFolder()),
            new ToolStripSeparator(),
            copyPathItem
        });

        Controls.Add(_headerStrip);
        Controls.Add(_tree);

        SetTheme(ThemeManager.Instance.CurrentTheme);

        // Debounced refresh timer (used by both polling fallback and FileSystemWatcher)
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _refreshTimer.Tick += (s, e) =>
        {
            _refreshTimer.Stop();
            if (!string.IsNullOrEmpty(_rootPath) && _tree.Nodes.Count > 0)
                RefreshTree();
        };
    }

    public void SetTheme(Theme theme)
    {
        bool isDark = !theme.IsLight;
        BackColor = theme.MenuBackground;

        _tree.BackColor = theme.MenuBackground;
        _tree.ForeColor = theme.Text;
        _tree.LineColor = theme.Border;

        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _rootLabel.ForeColor = theme.Muted;

        _closeButton.ForeColor = theme.Text;
        _refreshButton.ForeColor = theme.Text;
        _collapseButton.ForeColor = theme.Text;
        _projectFilterButton.ForeColor = theme.Text;

        _fileContextMenu.BackColor = theme.MenuBackground;
        _fileContextMenu.ForeColor = theme.Text;
        _fileContextMenu.Renderer = new ThemeAwareMenuRenderer(theme);

        ApplyScrollbarTheme(_tree.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? DARK_MODE_SCROLLBAR : "", null);
    }

    public void SetRoot(string path)
    {
        CancelScan();
        _fileWatcher?.Dispose();
        _fileWatcher = null;
        _rootPath = path;
        _rootLabel.Text = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "Workspace";
        _rootLabel.ToolTipText = path;
        _tree.Nodes.Clear();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            var rootNode = CreateDirectoryNode(path);
            _tree.Nodes.Add(rootNode);
            _tree.SelectedNode = rootNode;
            // Detect project files for filtering
            DetectProjectFiles();
            if (IsHandleCreated)
                BeginInvoke(() => PopulateDirectoryAsync(rootNode, path));
            else
            {
                // Handle not yet created (e.g. startup) — do sync fallback
                rootNode.Expand();
            }

            // Start FileSystemWatcher for instant refresh on file/dir changes
            try
            {
                _fileWatcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                _fileWatcher.Created += OnWatcherChange;
                _fileWatcher.Deleted += OnWatcherChange;
                _fileWatcher.Renamed += OnWatcherChange;
                _fileWatcher.EnableRaisingEvents = true;
            }
            catch { }
        }
        _refreshTimer.Stop();
    }

    private void OnWatcherChange(object sender, FileSystemEventArgs e)
    {
        if (!IsHandleCreated) return;
        // The debounce timer already rate-limits; no need for _pendingRefresh gating.
        try { BeginInvoke(() => RefreshTree()); }
        catch { }
    }

    public void RefreshTree()
    {
        CancelScan();
        if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath)) return;
        if (_tree.Nodes.Count == 0)
        {
            _tree.Nodes.Add(CreateDirectoryNode(_rootPath));
            return;
        }

        // Refresh visible nodes without collapsing
        SaveExpandedState();
        RefreshNodeRecursive(_tree.Nodes[0], _rootPath);
        RestoreExpandedState();
    }

    private readonly HashSet<string> _expandedPaths = new(StringComparer.OrdinalIgnoreCase);

    private void SaveExpandedState()
    {
        _expandedPaths.Clear();
        CollectExpanded(_tree.Nodes);
    }

    private void CollectExpanded(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string path && node.IsExpanded)
            {
                _expandedPaths.Add(path);
                CollectExpanded(node.Nodes);
            }
        }
    }

    private void RestoreExpandedState()
    {
        RestoreExpanded(_tree.Nodes);
    }

    private void RestoreExpanded(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string path && _expandedPaths.Contains(path))
            {
                node.Expand();
                RestoreExpanded(node.Nodes);
            }
        }
    }

    private void RefreshNodeRecursive(TreeNode node, string dirPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists) return;

            var currentDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TreeNode child in node.Nodes)
            {
                if (child.Tag is string childPath)
                {
                    if (Directory.Exists(childPath))
                        currentDirs.Add(childPath);
                    else if (File.Exists(childPath))
                        currentFiles.Add(childPath);
                }
            }

            var actualDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in Directory.EnumerateDirectories(dirPath))
            {
                string name = Path.GetFileName(d);
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name))
                    actualDirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f))
                    actualFiles.Add(f);
            }

            // Remove stale dirs
            foreach (var d in currentDirs)
            {
                if (!actualDirs.Contains(d))
                {
                    var toRemove = node.Nodes.Cast<TreeNode>().FirstOrDefault(n => string.Equals(n.Tag as string, d, StringComparison.OrdinalIgnoreCase));
                    toRemove?.Remove();
                }
            }

            // Remove stale files
            foreach (var f in currentFiles)
            {
                if (!actualFiles.Contains(f))
                {
                    var toRemove = node.Nodes.Cast<TreeNode>().FirstOrDefault(n => string.Equals(n.Tag as string, f, StringComparison.OrdinalIgnoreCase));
                    toRemove?.Remove();
                }
            }

            // Add new files
            foreach (var f in actualFiles)
            {
                if (!currentFiles.Contains(f))
                {
                    var fn = CreateFileNode(f);
                    int insertIdx = FindSortedInsertIndex(node.Nodes, Path.GetFileName(f));
                    node.Nodes.Insert(insertIdx, fn);
                }
            }

            // Add new directories and recurse
            foreach (var d in actualDirs)
            {
                TreeNode? existing = node.Nodes.Cast<TreeNode>()
                    .FirstOrDefault(n => string.Equals(n.Tag as string, d, StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                {
                    var dn = CreateDirectoryNode(d);
                    int insertIdx = FindSortedInsertIndex(node.Nodes, Path.GetFileName(d));
                    node.Nodes.Insert(insertIdx, dn);

                    if (node.IsExpanded)
                        PopulateDirectoryNode(dn, d);
                }
                else
                {
                    bool isUnpopulated = existing.Nodes.Count == 0 ||
                                         (existing.Nodes.Count == 1 && existing.Nodes[0].Text == "Loading...");

                    if (isUnpopulated)
                    {
                        existing.Nodes.Clear();
                        PopulateDirectoryNode(existing, d);
                    }

                    if (existing.IsExpanded)
                        RefreshNodeRecursive(existing, d);
                }
            }

            // Add new files
            foreach (var f in actualFiles)
            {
                if (!currentFiles.Contains(f))
                {
                    var fn = CreateFileNode(f);
                    int insertIdx = FindSortedInsertIndex(node.Nodes, Path.GetFileName(f));
                    node.Nodes.Insert(insertIdx, fn);
                }
            }
        }
        catch { }
    }

    private static int FindSortedInsertIndex(TreeNodeCollection nodes, string name)
    {
        int i = 0;
        foreach (TreeNode n in nodes)
        {
            string? nName = n.Text;
            if (string.Compare(nName, name, StringComparison.OrdinalIgnoreCase) > 0)
                return i;
            i++;
        }
        return i;
    }

private TreeNode CreateDirectoryNode(string dirPath)
    {
        var node = new TreeNode(Path.GetFileName(dirPath))
        {
            Tag = dirPath,
            ImageIndex = FileIconProvider.FolderIconIndex,
            SelectedImageIndex = FileIconProvider.FolderIconIndex
        };
        node.Nodes.Add(new TreeNode("Loading..."));
        return node;
    }

    private void EnsureNodePopulated(TreeNode node)
    {
        if (node.Tag is not string path || !Directory.Exists(path)) return;
        if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
        {
            node.Nodes.Clear();
            PopulateDirectoryNode(node, path);
        }
    }

    private static TreeNode CreateFileNode(string filePath)
    {
        int iconIdx = FileIconProvider.GetIconIndex(filePath);
        return new TreeNode(Path.GetFileName(filePath))
        {
            Tag = filePath,
            ImageIndex = iconIdx,
            SelectedImageIndex = iconIdx
        };
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node?.Tag is not string path || !Directory.Exists(path)) return;

        if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
        {
            node.Nodes.Clear();
            PopulateDirectoryNode(node, path);
        }
    }

    private void PopulateDirectoryNode(TreeNode node, string dirPath)
    {
        try
        {
            var dirs = new List<string>();
            var files = new List<string>();

            foreach (var d in Directory.EnumerateDirectories(dirPath))
            {
                string name = Path.GetFileName(d);
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name))
                    dirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f))
                    files.Add(f);
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var d in dirs)
            {
                var dn = CreateDirectoryNode(d);
                node.Nodes.Add(dn);
            }

            foreach (var f in files)
            {
                node.Nodes.Add(CreateFileNode(f));
            }
        }
        catch { }
    }

    private async void PopulateDirectoryAsync(TreeNode rootNode, string rootPath)
    {
        CancelScan();
        var cts = new CancellationTokenSource();
        _scanCts = cts;
        var token = cts.Token;

        try
        {
            ScanStarted?.Invoke();

            // Collect all entries on background thread to avoid blocking the UI
            var result = await Task.Run(() => CollectTreeStructure(rootPath, token), token);

            if (token.IsCancellationRequested || result is null) return;

            // Apply all nodes to the tree on the UI thread
            if (!IsHandleCreated) return;
            rootNode.Nodes.Clear();
            ApplyNodes(rootNode, result);

            rootNode.Expand();
            _tree.SelectedNode = rootNode;
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception)
        {
            // Background scan failed silently — tree remains with root-only placeholder
        }
        finally
        {
            if (_scanCts == cts)
            {
                _scanCts = null;
                cts.Dispose();
            }
            ScanCompleted?.Invoke();
        }
    }

    private sealed class DirEntry
    {
        public string Name { get; init; } = "";
        public string FullPath { get; init; } = "";
        public List<DirEntry> SubDirs { get; } = new();
        public List<string> Files { get; } = new();
    }

    private DirEntry? CollectTreeStructure(string rootPath, CancellationToken token)
    {
        try
        {
            int progressCount = 0;
            var root = new DirEntry
            {
                Name = Path.GetFileName(rootPath),
                FullPath = rootPath
            };
            CollectDir(root, rootPath, token, ref progressCount);
            return root;
        }
        catch { return null; }
    }

    private void CollectDir(DirEntry entry, string dirPath, CancellationToken token, ref int progressCount)
    {
        if (token.IsCancellationRequested) return;

        try
        {
            var dirs = new List<string>();
            var files = new List<string>();

            foreach (var d in Directory.EnumerateDirectories(dirPath))
            {
                if (token.IsCancellationRequested) return;
                string name = Path.GetFileName(d);
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name))
                    dirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                if (token.IsCancellationRequested) return;
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f))
                    files.Add(f);
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var d in dirs)
            {
                if (token.IsCancellationRequested) return;
                var child = new DirEntry { Name = Path.GetFileName(d), FullPath = d };
                entry.SubDirs.Add(child);
                CollectDir(child, d, token, ref progressCount);
            }

            entry.Files.AddRange(files);

            progressCount++;
            if (progressCount % 25 == 0 && IsHandleCreated)
            {
                int pct = Math.Min(progressCount, 100);
                BeginInvoke(new Action(() =>
                    ScanProgressChanged?.Invoke($"Scanning ({pct}%)...")));
            }
        }
        catch { }
    }

    private void ApplyNodes(TreeNode parentNode, DirEntry entry)
    {
        foreach (var sub in entry.SubDirs)
        {
            var node = CreateDirectoryNode(sub.FullPath);
            // Recursive population fills children immediately — remove the "Loading..."
            // placeholder so ApplyNodes' children aren't appended alongside it.
            node.Nodes.Clear();
            parentNode.Nodes.Add(node);
            ApplyNodes(node, sub);
        }
        foreach (var f in entry.Files)
        {
            parentNode.Nodes.Add(CreateFileNode(f));
        }
    }

    private static bool IsIgnoredDirectory(string name)
    {
        return name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".svn", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".hg", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || name.Equals("packages", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateProjectFilterVisual()
    {
        _projectFilterButton.Checked = _projectFilterEnabled;
        _projectFilterButton.ToolTipText = _projectFilterEnabled
            ? "Show Project Files Only — ON"
            : "Show Project Files Only — OFF";
    }

    public void DetectProjectFiles()
    {
        _projectFiles.Clear();
        _detectedProjectFile = null;

        if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath))
        {
            _projectFilterButton.Enabled = false;
            _projectFilterButton.ToolTipText = "Show Project Files Only — no .sln/.csproj detected";
            _projectFilterEnabled = false;
            return;
        }

        try
        {
            string? sln = Directory.EnumerateFiles(_rootPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (sln != null)
            {
                _detectedProjectFile = sln;
                // Parse .sln for project files
                foreach (string line in File.ReadLines(sln))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(line,
                        @"^Project\(.*\)\s*=\s*""[^""]*""\s*,\s*""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        string projPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sln)!, m.Groups[1].Value));
                        if (File.Exists(projPath))
                            ParseProjectForCompileItems(projPath);
                    }
                }
                // If no projects parsed from .sln, fallback: scan all .csproj
                if (_projectFiles.Count == 0)
                {
                    foreach (var proj in Directory.EnumerateFiles(_rootPath, "*.csproj", SearchOption.AllDirectories))
                        ParseProjectForCompileItems(proj);
                }
            }
            else
            {
                string? csproj = Directory.EnumerateFiles(_rootPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
                if (csproj != null)
                {
                    _detectedProjectFile = csproj;
                    ParseProjectForCompileItems(csproj);
                }
            }

            if (_projectFiles.Count > 0)
            {
                _projectFilterButton.Enabled = true;
                _projectFilterButton.ToolTipText = "Show Project Files Only";
                UpdateProjectFilterVisual();
            }
            else
            {
                _projectFilterButton.Enabled = false;
                _projectFilterButton.ToolTipText = "Show Project Files Only — no .sln/.csproj detected";
                _projectFilterEnabled = false;
            }
        }
        catch
        {
            _projectFilterButton.Enabled = false;
            _projectFilterButton.ToolTipText = "Show Project Files Only — error scanning";
            _projectFilterEnabled = false;
        }
    }

    private void ParseProjectForCompileItems(string csprojPath)
    {
        try
        {
            string content = File.ReadAllText(csprojPath);
            string projDir = Path.GetDirectoryName(csprojPath)!;

            // Check if this is an SDK-style project (no explicit Compile items)
            bool isSdkStyle = content.Contains("Sdk=\"Microsoft.NET.Sdk")
                || content.Contains("<Project Sdk=");

            if (isSdkStyle)
            {
                // SDK-style: gather all .cs files relative to project directory
                foreach (var f in Directory.EnumerateFiles(projDir, "*.cs", SearchOption.AllDirectories))
                {
                    // Skip bin/obj
                    string rel = Path.GetRelativePath(projDir, f);
                    if (rel.StartsWith("bin") || rel.StartsWith("obj") || rel.StartsWith(".vs"))
                        continue;
                    _projectFiles.Add(f);
                }
            }
            else
            {
                // Non-SDK / Framework-style: parse explicit Compile items
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(content);
                var ns = new System.Xml.XmlNamespaceManager(doc.NameTable);
                ns.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003");

                var compileNodes = doc.SelectNodes("//ns:Compile", ns);
                if (compileNodes != null)
                {
                    foreach (System.Xml.XmlNode compile in compileNodes)
                    {
                        string? include = compile?.Attributes?["Include"]?.Value;
                        if (string.IsNullOrEmpty(include)) continue;

                        // Skip wildcards
                        if (include.Contains('*') || include.Contains('?')) continue;

                        string fullPath = Path.GetFullPath(Path.Combine(projDir, include));
                        if (File.Exists(fullPath))
                            _projectFiles.Add(fullPath);
                    }
                }
            }
        }
        catch
        {
            // Ignore individual project parse errors
        }
    }

    private bool ShouldIncludeFile(string filePath)
    {
        if (!_projectFilterEnabled || _projectFiles.Count == 0)
            return true;
        return _projectFiles.Contains(filePath);
    }

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        OpenNode(e.Node);
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && _tree.SelectedNode != null)
        {
            OpenNode(_tree.SelectedNode);
            e.Handled = true;
        }
    }

    private void Tree_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var hit = _tree.HitTest(e.Location);
            _tree.SelectedNode = hit.Node;
            _fileContextMenu.Show(_tree, e.Location);
        }
    }

    private void Tree_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true && e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var f in files)
            {
                if (File.Exists(f))
                    FileOpenRequested?.Invoke(f);
                else if (Directory.Exists(f))
                {
                    CancelScan();
                    SetRoot(f);
                }
            }
        }
    }

    private void OpenSelectedNode()
    {
        if (_tree.SelectedNode != null)
            OpenNode(_tree.SelectedNode);
    }

    private void OpenNode(TreeNode node)
    {
        if (node.Tag is string path)
        {
            if (File.Exists(path))
                FileOpenRequested?.Invoke(path);
            else if (Directory.Exists(path))
            {
                if (node.IsExpanded)
                    node.Collapse();
                else
                    node.Expand();
            }
        }
    }

    private void OpenContainingFolder()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? "";
            if (!string.IsNullOrEmpty(dir))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
                catch { }
            }
        }
    }

    private void CopyPath()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            try { Clipboard.SetText(path); }
            catch { }
        }
    }

    private void CreateNewFile()
    {
        string? dir = GetSelectedDirectory();
        if (dir is null) return;

        string? name = SimpleInputDialog.Show(ParentForm!, "File name:", "New File", "newfile.txt");
        if (string.IsNullOrWhiteSpace(name)) return;

        string path = System.IO.Path.Combine(dir, name);
        try
        {
            if (System.IO.File.Exists(path))
            { ThemedMessageBox.Show("File already exists.", "New File", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            System.IO.File.WriteAllText(path, "");
            RefreshTree();
            FileOpenRequested?.Invoke(path);
        }
        catch (Exception ex) { ThemedMessageBox.Show($"Could not create file: {ex.Message}", "New File", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void CreateNewFolder()
    {
        string? dir = GetSelectedDirectory();
        if (dir is null) return;

        string? name = SimpleInputDialog.Show(ParentForm!, "Folder name:", "New Folder", "newfolder");
        if (string.IsNullOrWhiteSpace(name)) return;

        string path = System.IO.Path.Combine(dir, name);
        try
        {
            if (System.IO.Directory.Exists(path))
            { ThemedMessageBox.Show("Folder already exists.", "New Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            System.IO.Directory.CreateDirectory(path);
            RefreshTree();
        }
        catch (Exception ex) { ThemedMessageBox.Show($"Could not create folder: {ex.Message}", "New Folder", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private string? GetSelectedDirectory()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            if (System.IO.Directory.Exists(path)) return path;
            if (System.IO.File.Exists(path)) return System.IO.Path.GetDirectoryName(path);
        }
        return !string.IsNullOrEmpty(_rootPath) ? _rootPath : null;
    }
}

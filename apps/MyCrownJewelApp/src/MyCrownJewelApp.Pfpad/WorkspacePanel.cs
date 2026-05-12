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
    private readonly Panel _modernHeader;
    private readonly Label _workspaceTitle;
    private readonly Button _refreshButton;
    private readonly Button _collapseButton;
    private readonly Button _closeButton;
    private readonly TextBox _searchBox;
    private readonly Button _searchButton;
    private readonly Button _clearSearchButton;

    private readonly ContextMenuStrip _fileContextMenu;
    private string _rootPath = "";
    private readonly HashSet<string> _textExtensions;
#pragma warning disable CS0649
    private bool _showAllFiles;
#pragma warning restore CS0649
    private bool _projectFilterEnabled;
    private HashSet<string> _projectFiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _detectedProjectFile;
    private readonly Button _projectFilterButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CancellationTokenSource? _scanCts;
    private FileSystemWatcher? _fileWatcher;
    private string _searchFilter = "";

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
            ShowLines = false, // VS Code style - no lines
            ShowPlusMinus = true,
            ShowRootLines = false,
            HotTracking = true, // Highlight on hover
            FullRowSelect = true,
            HideSelection = false,
            LabelEdit = false,
            Indent = 24, // Slightly more indentation for better hierarchy
            ItemHeight = 26, // Taller items for better touch targets
            DrawMode = TreeViewDrawMode.OwnerDrawAll, // Custom drawing for modern look
            ImageList = FileIconProvider.ImageList
        };
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
        _tree.MouseDown += Tree_MouseDown;
        _tree.KeyDown += Tree_KeyDown;
        _tree.DrawNode += Tree_DrawNode; // Custom drawing for modern appearance
        _tree.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        _tree.DragDrop += Tree_DragDrop;
        _tree.AllowDrop = true;
        _tree.HandleCreated += (s, e) =>
        {
            ApplyScrollbarTheme(_tree.Handle);
            RemoveRootLinesStyle(_tree.Handle);
        };

        // Modern header with search
        _modernHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = ThemeManager.Instance.CurrentTheme.MenuBackground,
            Padding = new Padding(6, 4, 6, 4)
        };

        // Title and controls row
        _workspaceTitle = new Label
        {
            Text = "Explorer",
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = ThemeManager.Instance.CurrentTheme.Text,
            AutoSize = true,
            Location = new Point(0, 4)
        };

        _refreshButton = new Button
        {
            Text = "🔄",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 24),
            Location = new Point(180, 2),
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        _refreshButton.Click += (s, e) => RefreshTree();

        _collapseButton = new Button
        {
            Text = "📂",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 24),
            Location = new Point(212, 2),
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        _collapseButton.Click += (s, e) =>
        {
            if (_tree.Nodes.Count > 0 && _tree.Nodes[0].IsExpanded)
            {
                _tree.CollapseAll();
                _collapseButton.Text = "📁";
            }
            else
            {
                var root = _tree.Nodes.Count > 0 ? _tree.Nodes[0] : null;
                if (root != null)
                {
                    EnsureNodePopulated(root);
                    root.Expand();
                }
                _collapseButton.Text = "📂";
            }
        };

        _closeButton = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 24),
            Location = new Point(_modernHeader.Width - 30, 2),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        _closeButton.Click += (s, e) => CloseRequested?.Invoke();

        // Search row
        _searchBox = new TextBox
        {
            Font = new Font("Segoe UI", 9),
            Location = new Point(0, 30),
            Size = new Size(160, 24),
            BackColor = ThemeManager.Instance.CurrentTheme.EditorBackground,
            ForeColor = ThemeManager.Instance.CurrentTheme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Search files..."
        };
        _searchBox.TextChanged += SearchBox_TextChanged;
        _searchBox.KeyDown += SearchBox_KeyDown;

        _searchButton = new Button
        {
            Text = "🔍",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 24),
            Location = new Point(164, 30),
            BackColor = ThemeManager.Instance.CurrentTheme.PanelBackground,
            FlatAppearance = { BorderSize = 1 },
            Cursor = Cursors.Hand
        };
        _searchButton.Click += (s, e) => PerformSearch();

        _clearSearchButton = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 24),
            Location = new Point(196, 30),
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,
            Visible = false
        };
        _closeButton.Click += (s, e) => CloseRequested?.Invoke();

        _modernHeader.Controls.AddRange(new Control[] {
            _workspaceTitle, _refreshButton, _collapseButton, _closeButton,
            _searchBox, _searchButton, _clearSearchButton
        });

        // Set up theme
        SetTheme(ThemeManager.Instance.CurrentTheme);

        // Project filter button (hidden for now, will be enhanced later)
        _projectFilterButton = new Button
        {
            Text = "📋",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 24),
            Location = new Point(144, 2),
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand,

            Enabled = false,
            Visible = false // Hide for Phase 1, will enhance later
        };
        _projectFilterButton.Click += (s, e) =>
        {
            _projectFilterEnabled = !_projectFilterEnabled;
            UpdateProjectFilterVisual();
            if (!string.IsNullOrEmpty(_rootPath))
                RefreshTree();
        };
        _modernHeader.Controls.Add(_projectFilterButton);

        _fileContextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open", null, (s, e) => OpenSelectedNode());
        var openToSideItem = new ToolStripMenuItem("Open to the Side", null, (s, e) => OpenToSide());
        var openFolderItem = new ToolStripMenuItem("Open Containing Folder", null, (s, e) => OpenContainingFolder());
        var revealInExplorerItem = new ToolStripMenuItem("Reveal in File Explorer", null, (s, e) => RevealInExplorer());
        var copyPathItem = new ToolStripMenuItem("Copy Path", null, (s, e) => CopyPath());
        var copyRelativePathItem = new ToolStripMenuItem("Copy Relative Path", null, (s, e) => CopyRelativePath());

        _fileContextMenu.Items.AddRange(new ToolStripItem[] {
            openItem, openToSideItem, new ToolStripSeparator(),
            openFolderItem, revealInExplorerItem, new ToolStripSeparator(),
            new ToolStripMenuItem("New File...", null, (s, e) => CreateNewFile()),
            new ToolStripMenuItem("New Folder...", null, (s, e) => CreateNewFolder()),
            new ToolStripSeparator(),
            copyPathItem, copyRelativePathItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Rename...", null, (s, e) => RenameSelected()),
            new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelected())
        });

        Controls.Add(_modernHeader);
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

        // Modern header theming
        _modernHeader.BackColor = theme.MenuBackground;
        _workspaceTitle.ForeColor = theme.Text;

        _searchBox.BackColor = theme.EditorBackground;
        _searchBox.ForeColor = theme.Text;

        _refreshButton.BackColor = Color.Transparent;
        _refreshButton.ForeColor = theme.Text;

        _collapseButton.BackColor = Color.Transparent;
        _collapseButton.ForeColor = theme.Text;

        _closeButton.BackColor = Color.Transparent;
        _closeButton.ForeColor = theme.Text;

        _searchButton.BackColor = theme.PanelBackground;
        _searchButton.ForeColor = theme.Text;

        _clearSearchButton.BackColor = Color.Transparent;
        _clearSearchButton.ForeColor = theme.Text;

        _projectFilterButton.BackColor = Color.Transparent;
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
        _workspaceTitle.Text = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) ?? "Workspace" : "Explorer";
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
        // Visual feedback through text/icon changes instead of Checked property
        _projectFilterButton.Text = _projectFilterEnabled ? "📋✓" : "📋";
    }

    public void DetectProjectFiles()
    {
        _projectFiles.Clear();
        _detectedProjectFile = null;

        if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath))
        {
            _projectFilterButton.Enabled = false;
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
                UpdateProjectFilterVisual();
            }
            else
            {
                _projectFilterButton.Enabled = false;
                _projectFilterEnabled = false;
            }
        }
        catch
        {
            _projectFilterButton.Enabled = false;
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

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        var node = e.Node;
        var bounds = e.Bounds;

        // Background
        Color backColor;
        if ((e.State & TreeNodeStates.Selected) != 0)
        {
            backColor = theme.ButtonHoverBackground;
        }
        else if (node.BackColor != Color.Transparent) // Search highlight
        {
            backColor = node.BackColor;
        }
        else
        {
            backColor = theme.MenuBackground;
        }

        using (var brush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(brush, bounds);
        }

        // Icon
        if (node.ImageIndex >= 0 && FileIconProvider.ImageList != null)
        {
            var iconBounds = new Rectangle(bounds.X + (node.Level * _tree.Indent) + 2, bounds.Y + 1, 20, 20);
            e.Graphics.DrawImage(FileIconProvider.ImageList.Images[node.ImageIndex], iconBounds);
        }

        // Text
        Color textColor;
        if (node.BackColor != Color.Transparent) // Search highlight
        {
            textColor = node.ForeColor;
        }
        else if ((e.State & TreeNodeStates.Selected) != 0)
        {
            textColor = theme.Text;
        }
        else
        {
            textColor = theme.Text;
        }

        var textBounds = new Rectangle(bounds.X + (node.Level * _tree.Indent) + 26, bounds.Y + 2,
                                       bounds.Width - (node.Level * _tree.Indent) - 26, bounds.Height - 4);

        TextRenderer.DrawText(e.Graphics, node.Text, _tree.Font, textBounds, textColor,
                             TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
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

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        _searchFilter = _searchBox.Text.Trim();
        _clearSearchButton.Visible = !string.IsNullOrEmpty(_searchFilter);
        PerformSearch();
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            ClearSearch();
            e.Handled = true;
        }
    }

    private void PerformSearch()
    {
        if (string.IsNullOrEmpty(_searchFilter))
        {
            ClearSearch();
            return;
        }

        // Expand all nodes and highlight matches
        ExpandAllNodes();
        HighlightSearchMatches(_tree.Nodes, _searchFilter.ToLowerInvariant());
    }

    private void ClearSearch()
    {
        _searchFilter = "";
        _searchBox.Text = "";
        _clearSearchButton.Visible = false;

        // Clear all highlighting and collapse nodes back to default state
        ClearSearchHighlights(_tree.Nodes);
        RefreshTree(); // This will restore the default expansion state
    }

    private void ExpandAllNodes()
    {
        foreach (TreeNode node in _tree.Nodes)
        {
            EnsureNodePopulated(node);
            node.Expand();
            ExpandAllChildNodes(node);
        }
    }

    private void ExpandAllChildNodes(TreeNode parentNode)
    {
        foreach (TreeNode child in parentNode.Nodes)
        {
            EnsureNodePopulated(child);
            child.Expand();
            ExpandAllChildNodes(child);
        }
    }

    private void HighlightSearchMatches(TreeNodeCollection nodes, string searchTerm)
    {
        foreach (TreeNode node in nodes)
        {
            string nodeText = node.Text.ToLowerInvariant();
            bool matches = nodeText.Contains(searchTerm);

            // Highlight matching nodes
            if (matches)
            {
                node.BackColor = Color.Yellow;
                node.ForeColor = Color.Black;
            }
            else
            {
                node.BackColor = Color.Transparent;
                node.ForeColor = ThemeManager.Instance.CurrentTheme.Text;
            }

            // Recursively check children
            HighlightSearchMatches(node.Nodes, searchTerm);

            // Expand parent if any child matches
            if (!matches && HasMatchingChild(node, searchTerm))
            {
                node.Expand();
            }
        }
    }

    private bool HasMatchingChild(TreeNode node, string searchTerm)
    {
        foreach (TreeNode child in node.Nodes)
        {
            if (child.Text.ToLowerInvariant().Contains(searchTerm) ||
                HasMatchingChild(child, searchTerm))
            {
                return true;
            }
        }
        return false;
    }

    private void ClearSearchHighlights(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            node.BackColor = Color.Transparent;
            node.ForeColor = ThemeManager.Instance.CurrentTheme.Text;
            ClearSearchHighlights(node.Nodes);
        }
    }

    private void OpenToSide()
    {
        if (_tree.SelectedNode?.Tag is string path && File.Exists(path))
        {
            FileOpenRequested?.Invoke(path); // For now, just open normally - tab system handles "to side"
        }
    }

    private void RevealInExplorer()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? "";
            if (!string.IsNullOrEmpty(dir))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                catch { }
            }
        }
    }

    private void CopyRelativePath()
    {
        if (_tree.SelectedNode?.Tag is string path && !string.IsNullOrEmpty(_rootPath))
        {
            try
            {
                string relativePath = Path.GetRelativePath(_rootPath, path);
                Clipboard.SetText(relativePath);
            }
            catch { }
        }
    }

    private void RenameSelected()
    {
        if (_tree.SelectedNode != null)
        {
            _tree.SelectedNode.BeginEdit();
        }
    }

    private void DeleteSelected()
    {
        if (_tree.SelectedNode?.Tag is string path)
        {
            string itemType = Directory.Exists(path) ? "folder" : "file";
            string itemName = Path.GetFileName(path);

            var result = MessageBox.Show(
                $"Are you sure you want to delete the {itemType} '{itemName}'?",
                "Delete Item",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                    else if (File.Exists(path))
                        File.Delete(path);

                    RefreshTree();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete item: {ex.Message}", "Delete Failed",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

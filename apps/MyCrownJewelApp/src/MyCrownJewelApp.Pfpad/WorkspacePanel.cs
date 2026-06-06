using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

internal sealed class WorkspacePanel : UserControl
{
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";
    private const string FavoritesFile = "workspace_favorites.json";

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private readonly GitService _git;

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
    private RepositoryAnalyticsEngine? _analyticsEngine;
#pragma warning disable CS0649
    private bool _showAllFiles;
#pragma warning restore CS0649
    private bool _projectFilterEnabled;
    private HashSet<string> _projectFiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _detectedProjectFile;
    private readonly Button _projectFilterButton;
    private readonly Button _showAllFilesButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private CancellationTokenSource? _scanCts;
    private FileSystemWatcher? _fileWatcher;
    private string _searchFilter = "";
    private TreeNode? _hoveredNode;
    private string? _activeFilePath;
    private HashSet<string>? _searchResultFiles;
    private Dictionary<string, GitStatusType>? _gitStatusCache;
    private readonly HashSet<TreeNode> _multiSelectedNodes = new();
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    // .gitignore patterns loaded for the current root — raw pattern strings.
    private List<string> _gitignorePatterns = [];
    private TreeNode? _favoritesRoot;

    public event Action<string>? FileOpenRequested;
    public event Action? CloseRequested;
    public event Action? ScanStarted;
    public event Action? ScanCompleted;
#pragma warning disable CS0067 // Event is never used - kept for future extensibility
    public event Action<string>? ScanProgressChanged;
#pragma warning restore CS0067
    public event Action<string?>? ActiveFileChanged;
    public event Action<string>? RootChanged;

    public void HighlightSearchResults(IEnumerable<string> filePaths)
    {
        _searchResultFiles = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
        RefreshTreeHighlight();
    }

    public void ClearSearchHighlights()
    {
        _searchResultFiles = null;
        RefreshTreeHighlight();
    }

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
            ThemeManager.Instance.ThemeChanged -= SetTheme;
            if (_git != null) _git.OnRepoChanged -= RefreshGitStatusIndicators;
            SaveFavorites();
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

    public WorkspacePanel(GitService git)
    {
        _git = git;
        _textExtensions = new HashSet<string>(DefaultTextExtensions, StringComparer.OrdinalIgnoreCase);

        LoadFavorites();

        // Listen for git repo changes to update status indicators
        _git.OnRepoChanged += RefreshGitStatusIndicators;

        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowLines = false, // VS Code style - no lines
            ShowPlusMinus = true,
            ShowRootLines = false,
            HotTracking = false, // Custom hover handling
            FullRowSelect = false, // Custom full-row selection drawing
            HideSelection = true, // Handle selection drawing ourselves
            LabelEdit = false,
            Indent = 24, // Slightly more indentation for better hierarchy
            ItemHeight = 26, // Taller items for better touch targets
            DrawMode = TreeViewDrawMode.OwnerDrawAll, // Custom drawing for full-width hover
            ImageList = FileIconProvider.ImageList
        };
        _tree.BeforeExpand += Tree_BeforeExpand;
        _tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
        _tree.MouseDown += Tree_MouseDown;
        _tree.MouseMove += Tree_MouseMove;
        _tree.MouseLeave += Tree_MouseLeave;
        _tree.DrawNode += Tree_DrawNode;
        _tree.KeyDown += Tree_KeyDown;
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

        // Theme will be set by Form1 after initialization
        ThemeManager.Instance.ThemeChanged += SetTheme;

        // Project filter button
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
            Visible = false // Hide for now
        };
        _projectFilterButton.Click += (s, e) =>
        {
            _projectFilterEnabled = !_projectFilterEnabled;
            UpdateProjectFilterVisual();
            if (!string.IsNullOrEmpty(_rootPath))
                RefreshTree();
        };

        // Show all files button
        _showAllFilesButton = new Button
        {
            Text = "📄",
            Font = new Font("Segoe UI", 9),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 24),
            Location = new Point(176, 2),
            BackColor = Color.Transparent,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
        _showAllFilesButton.Click += (s, e) =>
        {
            _showAllFiles = !_showAllFiles;
            UpdateShowAllFilesVisual();
            if (!string.IsNullOrEmpty(_rootPath))
                RefreshTree();
        };

        _modernHeader.Controls.AddRange(new Control[] { _projectFilterButton, _showAllFilesButton });

        _fileContextMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open", null, (s, e) => OpenSelectedNode());
        var openToSideItem = new ToolStripMenuItem("Open to the Side", null, (s, e) => OpenToSide());
        var openFolderItem = new ToolStripMenuItem("Open Containing Folder", null, (s, e) => OpenContainingFolder());
        var revealInExplorerItem = new ToolStripMenuItem("Reveal in File Explorer", null, (s, e) => RevealInExplorer());
        var copyPathItem = new ToolStripMenuItem("Copy Path", null, (s, e) => CopyPath());
        var copyRelativePathItem = new ToolStripMenuItem("Copy Relative Path", null, (s, e) => CopyRelativePath());

        // Git operations submenu
        var gitMenu = new ToolStripMenuItem("Git");
        gitMenu.DropDownItems.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("Stage", null, (s, e) => GitStageSelected()),
            new ToolStripMenuItem("Unstage", null, (s, e) => GitUnstageSelected()),
            new ToolStripMenuItem("Discard Changes", null, (s, e) => GitDiscardSelected()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Diff", null, (s, e) => GitDiffSelected())
        });

        _fileContextMenu.Items.AddRange(new ToolStripItem[] {
            openItem, openToSideItem, new ToolStripSeparator(),
            openFolderItem, revealInExplorerItem, new ToolStripSeparator(),
            new ToolStripMenuItem("New File...", null, (s, e) => CreateNewFile()),
            new ToolStripMenuItem("New Folder...", null, (s, e) => CreateNewFolder()),
            new ToolStripSeparator(),
            gitMenu, new ToolStripSeparator(),
            copyPathItem, copyRelativePathItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Add to Favorites", null, (s, e) => AddToFavorites()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Rename...", null, (s, e) => RenameSelected()),
            new ToolStripMenuItem("Delete", null, (s, e) => DeleteSelected())
        });

        Controls.Add(_modernHeader);
        Controls.Add(_tree);

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

        _showAllFilesButton.BackColor = Color.Transparent;
        _showAllFilesButton.ForeColor = theme.Text;

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
        _fileWatcher    = null;
        _gitStatusCache = null;   // invalidate stale git status for previous root
        _gitignorePatterns = LoadGitignorePatterns(path);
        _rootPath = path;
        RootChanged?.Invoke(path);
        _workspaceTitle.Text = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) ?? "Workspace" : "Explorer";
        _tree.Nodes.Clear();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            var rootNode = CreateDirectoryNode(path);
            _tree.Nodes.Add(rootNode);
            RefreshFavorites();
            _tree.SelectedNode = rootNode;
            // Detect project files for filtering
            DetectProjectFiles();
            UpdateShowAllFilesVisual();
            RefreshGitStatusIndicators();
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

    private async void RunRepositoryAnalyticsAsync(string path)
    {
        try
        {
            _analyticsEngine = new RepositoryAnalyticsEngine(path);
            var analysis = await _analyticsEngine.AnalyzeAsync();

            // Show analysis results to user
            BeginInvoke(() =>
            {
                ShowRepositoryAnalysis(analysis);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Repository analysis failed: {ex.Message}");
        }
    }

    private void ShowRepositoryAnalysis(RepositoryAnalysis analysis)
    {
        using var dialog = new RepositoryAnalysisDialog(analysis);
        dialog.ShowDialog(ParentForm);
    }

    private void OnWatcherChange(object sender, FileSystemEventArgs e)
    {
        if (!IsHandleCreated) return;
        // Invalidate the git status cache so the next draw picks up the correct status.
        _gitStatusCache = null;
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
            var path = GetNodePath(node);
            if (path != null && node.IsExpanded)
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
            var path = GetNodePath(node);
            if (path != null && _expandedPaths.Contains(path))
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
                var childPath = GetNodePath(child);
                if (childPath != null)
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
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name) && !ShouldIgnoreByGitignore(d))
                    actualDirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f) && !ShouldIgnoreByGitignore(f))
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
        var gitStatus = GetDirectoryGitStatusType(dirPath);
        var node = new TreeNode(Path.GetFileName(dirPath))
        {
            Tag = new FileNodeTag(dirPath, gitStatus),
            ImageIndex = FileIconProvider.FolderIconIndex,
            SelectedImageIndex = FileIconProvider.FolderIconIndex
        };
        node.Nodes.Add(new TreeNode("Loading..."));
        return node;
    }

    private void EnsureNodePopulated(TreeNode node)
    {
        var path = GetNodePath(node);
        if (path == null || !Directory.Exists(path)) return;
        if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
        {
            node.Nodes.Clear();
            PopulateDirectoryNode(node, path);
        }
    }

    private TreeNode CreateFileNode(string filePath)
    {
        int iconIdx = FileIconProvider.GetIconIndex(filePath);
        string fileName = Path.GetFileName(filePath);
        return new TreeNode(fileName)
        {
            Tag = new FileNodeTag(filePath, GitStatusType.None), // Initial status, will be updated later
            ImageIndex = iconIdx,
            SelectedImageIndex = iconIdx
        };
    }

    private void Tree_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node == null) return;
        var path = GetNodePath(node);
        if (path == null || !Directory.Exists(path)) return;

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
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name) && !ShouldIgnoreByGitignore(d))
                    dirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f) && !ShouldIgnoreByGitignore(f))
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

            // Collect directories and files
            foreach (var d in Directory.EnumerateDirectories(dirPath))
            {
                if (token.IsCancellationRequested) return;
                string name = Path.GetFileName(d);
                if (!name.StartsWith('.') && !IsIgnoredDirectory(name) && !ShouldIgnoreByGitignore(d))
                    dirs.Add(d);
            }

            foreach (var f in Directory.EnumerateFiles(dirPath))
            {
                if (token.IsCancellationRequested) return;
                string ext = Path.GetExtension(f);
                if ((_showAllFiles || _textExtensions.Contains(ext)) && ShouldIncludeFile(f) && !ShouldIgnoreByGitignore(f))
                    files.Add(f);
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            // Sequential processing (parallel would be too complex with the existing structure)
            foreach (var d in dirs)
            {
                if (token.IsCancellationRequested) return;
                var child = new DirEntry { Name = Path.GetFileName(d), FullPath = d };
                entry.SubDirs.Add(child);
                CollectDir(child, d, token, ref progressCount);
            }

            foreach (var f in files)
            {
                entry.Files.Add(f);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
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

    /// <summary>Reads the top-level .gitignore and returns the pattern lines.</summary>
    private static List<string> LoadGitignorePatterns(string rootPath)
    {
        var patterns = new List<string>();
        if (string.IsNullOrEmpty(rootPath)) return patterns;
        var gitignorePath = Path.Combine(rootPath, ".gitignore");
        if (!File.Exists(gitignorePath)) return patterns;
        try
        {
            foreach (var raw in File.ReadAllLines(gitignorePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                patterns.Add(line);
            }
        }
        catch { }
        return patterns;
    }

    /// <summary>
    /// Returns true when a file or directory path (relative to the workspace root)
    /// matches one of the loaded .gitignore patterns. Supports * wildcards.
    /// </summary>
    private bool ShouldIgnoreByGitignore(string fullPath)
    {
        if (_gitignorePatterns.Count == 0 || string.IsNullOrEmpty(_rootPath)) return false;

        // Build a forward-slash-normalised relative path for matching.
        string rel;
        try
        {
            rel = Path.GetRelativePath(_rootPath, fullPath).Replace('\\', '/');
        }
        catch { return false; }

        var name = Path.GetFileName(fullPath);

        foreach (var pattern in _gitignorePatterns)
        {
            // Negation patterns (!) are not supported — we skip them.
            if (pattern.StartsWith('!')) continue;

            var p = pattern.TrimStart('/');
            bool dirOnly = p.EndsWith('/');
            if (dirOnly) p = p.TrimEnd('/');

            // Match against the bare name first, then the full relative path.
            if (GitignoreGlobMatch(p, name) || GitignoreGlobMatch(p, rel))
                return true;
        }
        return false;
    }

    /// <summary>Minimal glob matching: * matches within a path segment; ** matches across segments.</summary>
    private static bool GitignoreGlobMatch(string pattern, string input)
    {
        return GlobMatch(pattern, input, 0, 0);
    }

    private static bool GlobMatch(string pattern, string input, int pi, int ii)
    {
        while (pi < pattern.Length && ii < input.Length)
        {
            char pc = pattern[pi];
            if (pc == '*' && pi + 1 < pattern.Length && pattern[pi + 1] == '*')
            {
                // ** — match any number of characters including '/'
                if (GlobMatch(pattern, input, pi + 2, ii)) return true;
                ii++;
                continue;
            }
            if (pc == '*')
            {
                // Single * — match any characters except '/'
                while (ii <= input.Length)
                {
                    if (GlobMatch(pattern, input, pi + 1, ii)) return true;
                    if (ii < input.Length && input[ii] == '/') break;
                    ii++;
                }
                return false;
            }
            if (pc == '?' && input[ii] != '/') { pi++; ii++; continue; }
            if (!pc.Equals(char.ToLowerInvariant(input[ii])) &&
                !pc.Equals(input[ii])) return false;
            pi++; ii++;
        }
        // Consume trailing stars
        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        return pi == pattern.Length && ii == input.Length;
    }

    private void UpdateProjectFilterVisual()
    {
        // Visual feedback through text/icon changes instead of Checked property
        _projectFilterButton.Text = _projectFilterEnabled ? "📋✓" : "📋";
    }

    private void UpdateShowAllFilesVisual()
    {
        _showAllFilesButton.Text = _showAllFiles ? "📄✓" : "📄";
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
        if (e.Node != null) OpenNode(e.Node);
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_multiSelectedNodes.Count == 0) return;

        if (e.KeyCode == Keys.Enter)
        {
            foreach (var node in _multiSelectedNodes.ToList())
            {
                OpenNode(node);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2 && _multiSelectedNodes.Count == 1)
        {
            RenameSelected();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedItems();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.A)
        {
            SelectAll();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.N)
        {
            CreateNewFile();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && e.KeyCode == Keys.N)
        {
            CreateNewFolder();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.C && _multiSelectedNodes.Count == 1)
        {
            CopyPath();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && e.KeyCode == Keys.C && _multiSelectedNodes.Count == 1)
        {
            CopyRelativePath();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.Enter && _multiSelectedNodes.Count == 1)
        {
            OpenToSide();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.S)
        {
            GitStageSelected();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.U)
        {
            GitUnstageSelected();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && (Control.ModifierKeys & Keys.Shift) == Keys.Shift && e.KeyCode == Keys.D)
        {
            GitDiscardSelected();
            e.Handled = true;
        }
        else if ((Control.ModifierKeys & Keys.Control) == Keys.Control && e.KeyCode == Keys.D)
        {
            GitDiffSelected();
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
#pragma warning disable CS8602 // node is not null in DrawNode
        if (node != null && _multiSelectedNodes.Contains(node))
        {
            backColor = theme.ButtonHoverBackground;
        }
        else if (node.BackColor != Color.Transparent) // Search highlight
        {
            backColor = node.BackColor;
        }
        else if (node == _hoveredNode) // Full-width hover highlight
        {
            backColor = Color.FromArgb(120, theme.ButtonHoverBackground);
        }
        else
        {
            backColor = theme.MenuBackground;
        }
#pragma warning restore CS8602

        // For selected or hovered, draw full width
        Rectangle drawBounds = bounds;
        if (_multiSelectedNodes.Contains(node) || node == _hoveredNode)
        {
            drawBounds = new Rectangle(0, bounds.Y, _tree.ClientSize.Width, bounds.Height);
        }

        using (var brush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(brush, drawBounds);
        }

        // Icon
        if (node.ImageIndex >= 0 && FileIconProvider.ImageList != null)
        {
            var iconBounds = new Rectangle(bounds.X + (node.Level * _tree.Indent) + 2, bounds.Y + 1, 16, 16);
            e.Graphics.DrawImage(FileIconProvider.ImageList.Images[node.ImageIndex], iconBounds);
        }

        // Git status indicator
        GitStatusType gitStatus = GitStatusType.None;
        if (node.Tag is FileNodeTag fileTag)
        {
            gitStatus = fileTag.GitStatus;
        }

        // Calculate text bounds accounting for status indicator
        int statusIndicatorSize = gitStatus != GitStatusType.None ? 8 : 0;
        int statusIndicatorMargin = gitStatus != GitStatusType.None ? 6 : 0;

        var textBounds = new Rectangle(bounds.X + (node.Level * _tree.Indent) + 22 + statusIndicatorSize + statusIndicatorMargin, bounds.Y + 2,
                                       bounds.Width - (node.Level * _tree.Indent) - 22 - statusIndicatorSize - statusIndicatorMargin, bounds.Height - 4);

        // Text color
        Color textColor;
        if (node.BackColor != Color.Transparent) // Search highlight
        {
            textColor = node.ForeColor;
        }
        else
        {
            textColor = theme.Text;
        }

        TextRenderer.DrawText(e.Graphics, node.Text, _tree.Font, textBounds, textColor,
                             TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);

        // Draw Git status indicator
        if (gitStatus != GitStatusType.None)
        {
            Color statusColor = GetGitStatusColor(gitStatus, theme);
            int indicatorX = bounds.X + (node.Level * _tree.Indent) + 22;
            int indicatorY = bounds.Y + (bounds.Height / 2) - (statusIndicatorSize / 2);

            using (var brush = new SolidBrush(statusColor))
            {
                e.Graphics.FillEllipse(brush, indicatorX, indicatorY, statusIndicatorSize, statusIndicatorSize);
            }

            // Add border for better visibility
            using (var pen = new Pen(Color.FromArgb(128, statusColor), 1))
            {
                e.Graphics.DrawEllipse(pen, indicatorX, indicatorY, statusIndicatorSize, statusIndicatorSize);
            }
        }
    }

    private void Tree_MouseDown(object? sender, MouseEventArgs e)
    {
        var hit = _tree.HitTest(e.Location);
        if (hit.Node == null) return;

        if (e.Button == MouseButtons.Left)
        {
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                // Toggle multi-selection
                if (_multiSelectedNodes.Contains(hit.Node))
                {
                    _multiSelectedNodes.Remove(hit.Node);
                }
                else
                {
                    _multiSelectedNodes.Add(hit.Node);
                }
                _tree.SelectedNode = hit.Node; // Keep for keyboard navigation
            }
            else
            {
                // Single selection
                _multiSelectedNodes.Clear();
                _multiSelectedNodes.Add(hit.Node);
                _tree.SelectedNode = hit.Node;
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            // If right-click on non-selected, make it the selection
            if (!_multiSelectedNodes.Contains(hit.Node))
            {
                _multiSelectedNodes.Clear();
                _multiSelectedNodes.Add(hit.Node);
                _tree.SelectedNode = hit.Node;
            }
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
        string? path = null;
        if (node.Tag is FileNodeTag fileTag)
            path = fileTag.FilePath;
        else if (node.Tag is string strPath)
            path = strPath;

        if (path != null)
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null && File.Exists(path))
        {
            FileOpenRequested?.Invoke(path); // For now, just open normally - tab system handles "to side"
        }
    }

    private void RevealInExplorer()
    {
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null && !string.IsNullOrEmpty(_rootPath))
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
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
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

    private void DeleteSelectedItems()
    {
        var items = GetSelectedFilePaths();
        if (items.Count == 0) return;

        string itemType = items.Count == 1 ? (Directory.Exists(items[0]) ? "folder" : "file") : "items";
        string message = items.Count == 1
            ? $"Are you sure you want to delete the {itemType} '{Path.GetFileName(items[0])}'?"
            : $"Are you sure you want to delete {items.Count} selected items?";

        var result = MessageBox.Show(message, "Delete Items", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            try
            {
                foreach (var path in items)
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                    else if (File.Exists(path))
                        File.Delete(path);
                }
                RefreshTree();
                _multiSelectedNodes.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete items: {ex.Message}", "Delete Failed",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SelectAll()
    {
        _multiSelectedNodes.Clear();
        CollectAllNodes(_tree.Nodes);
    }

    private void CollectAllNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            _multiSelectedNodes.Add(node);
            CollectAllNodes(node.Nodes);
        }
    }

    private void AddToFavorites()
    {
        var path = GetNodePath(_tree.SelectedNode);
        if (path != null)
        {
            if (_favorites.Add(path))
            {
                RefreshFavorites();
                SaveFavorites();
            }
        }
    }

    private void RefreshFavorites()
    {
        if (_favorites.Count == 0)
        {
            if (_favoritesRoot != null)
            {
                _favoritesRoot.Remove();
                _favoritesRoot = null;
            }
            return;
        }

        if (_favoritesRoot == null)
        {
            _favoritesRoot = new TreeNode("⭐ Favorites")
            {
                ImageIndex = FileIconProvider.FolderIconIndex,
                SelectedImageIndex = FileIconProvider.FolderIconIndex
            };
            _tree.Nodes.Insert(0, _favoritesRoot);
        }

        _favoritesRoot.Nodes.Clear();
        foreach (var fav in _favorites.OrderBy(f => Path.GetFileName(f)))
        {
            var node = CreateFavoriteNode(fav);
            _favoritesRoot.Nodes.Add(node);
        }
        _favoritesRoot.Expand();
    }

    private TreeNode CreateFavoriteNode(string path)
    {
        string name = Path.GetFileName(path);
        int iconIdx = FileIconProvider.GetIconIndex(path);
        GitStatusType gitStatus = Directory.Exists(path) ? GetDirectoryGitStatusType(path) : GetGitStatusType(path);
        var node = new TreeNode(name)
        {
            Tag = new FileNodeTag(path, gitStatus),
            ImageIndex = iconIdx,
            SelectedImageIndex = iconIdx
        };
        return node;
    }

    private TreeNode? GetNodeAtY(int y)
    {
        foreach (TreeNode node in _tree.Nodes)
        {
            var found = GetNodeAtYRecursive(node, y);
            if (found != null) return found;
        }
        return null;
    }

    private TreeNode? GetNodeAtYRecursive(TreeNode node, int y)
    {
        var bounds = node.Bounds;
        if (y >= bounds.Y && y < bounds.Y + bounds.Height)
        {
            return node;
        }
        foreach (TreeNode child in node.Nodes)
        {
            var found = GetNodeAtYRecursive(child, y);
            if (found != null) return found;
        }
        return null;
    }

    private void Tree_MouseMove(object? sender, MouseEventArgs e)
    {
        var hit = _tree.HitTest(e.Location);
        if (hit.Node != _hoveredNode)
        {
            var oldHovered = _hoveredNode;
            _hoveredNode = hit.Node;
            // Invalidate the affected areas
            if (oldHovered != null)
            {
                var bounds = oldHovered.Bounds;
                _tree.Invalidate(new Rectangle(0, bounds.Y, _tree.ClientSize.Width, bounds.Height));
            }
            if (_hoveredNode != null)
            {
                var bounds = _hoveredNode.Bounds;
                _tree.Invalidate(new Rectangle(0, bounds.Y, _tree.ClientSize.Width, bounds.Height));
            }
        }
    }

    private void Tree_MouseLeave(object? sender, EventArgs e)
    {
        if (_hoveredNode != null)
        {
            var bounds = _hoveredNode.Bounds;
            _tree.Invalidate(new Rectangle(0, bounds.Y, _tree.ClientSize.Width, bounds.Height));
            _hoveredNode = null;
        }
    }

    private GitStatusType GetGitStatusType(string filePath)
    {
        // Use cached status if available
        if (_gitStatusCache != null && _gitStatusCache.TryGetValue(filePath, out var cachedStatus))
        {
            return cachedStatus;
        }

        // Fallback to direct calculation if cache is not available
        if (_git?.IsActive != true) return GitStatusType.None;

        try
        {
            var (staged, unstaged, untracked) = _git.GetStatus();

            // Check staged files first
            var stagedEntry = staged.FirstOrDefault(s => s.Path == filePath);
            if (stagedEntry != null)
            {
                if (stagedEntry.State.HasFlag(FileStatus.NewInIndex)) return GitStatusType.Added;
                if (stagedEntry.State.HasFlag(FileStatus.ModifiedInIndex)) return GitStatusType.Modified;
                if (stagedEntry.State.HasFlag(FileStatus.DeletedFromIndex)) return GitStatusType.Deleted;
                if (stagedEntry.State.HasFlag(FileStatus.RenamedInIndex)) return GitStatusType.Renamed;
            }

            // Check unstaged files
            var unstagedEntry = unstaged.FirstOrDefault(s => s.Path == filePath);
            if (unstagedEntry != null)
            {
                if (unstagedEntry.State.HasFlag(FileStatus.NewInWorkdir)) return GitStatusType.Added;
                if (unstagedEntry.State.HasFlag(FileStatus.ModifiedInWorkdir)) return GitStatusType.Modified;
                if (unstagedEntry.State.HasFlag(FileStatus.DeletedFromWorkdir)) return GitStatusType.Deleted;
                if (unstagedEntry.State.HasFlag(FileStatus.RenamedInWorkdir)) return GitStatusType.Renamed;
            }

            // Check untracked files
            var untrackedEntry = untracked.FirstOrDefault(s => s.Path == filePath);
            if (untrackedEntry != null)
            {
                return GitStatusType.Untracked;
            }
        }
        catch
        {
            // Ignore git errors
        }

        return GitStatusType.None;
    }

    private GitStatusType GetDirectoryGitStatusType(string dirPath)
    {
        if (_git?.IsActive != true) return GitStatusType.None;

        try
        {
            // Use cached status if available
            if (_gitStatusCache != null)
            {
                foreach (var kvp in _gitStatusCache)
                {
                    if (kvp.Value != GitStatusType.None &&
                        (kvp.Key.StartsWith(dirPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                         kvp.Key.StartsWith(dirPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    {
                        return GitStatusType.Modified;
                    }
                }
                return GitStatusType.None;
            }

            // Fallback to direct calculation
            var (staged, unstaged, untracked) = _git.GetStatus();

            // Check if any file under this directory has changes
            var allChangedFiles = staged.Concat(unstaged).Concat(untracked);

            foreach (var entry in allChangedFiles)
            {
                if (entry.Path.StartsWith(dirPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    entry.Path.StartsWith(dirPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // Directory has modified content - return Modified as the status
                    return GitStatusType.Modified;
                }
            }
        }
        catch
        {
            // Ignore git errors
        }

        return GitStatusType.None;
    }

    public void RefreshGitStatusIndicators()
    {
        if (!IsHandleCreated) return;

        BeginInvoke(() =>
        {
            try
            {
                // Build Git status cache for all files at once
                BuildGitStatusCache();
                UpdateGitStatusInNodes(_tree.Nodes);
            }
            catch
            {
                // Ignore errors when updating git status
            }
        });
    }

    private void BuildGitStatusCache()
    {
        _gitStatusCache = new Dictionary<string, GitStatusType>(StringComparer.OrdinalIgnoreCase);

        if (_git?.IsActive != true) return;

        try
        {
            var (staged, unstaged, untracked) = _git.GetStatus();

            // Build a lookup dictionary for faster access
            var stagedLookup = staged.ToDictionary(s => s.Path, StringComparer.OrdinalIgnoreCase);
            var unstagedLookup = unstaged.ToDictionary(s => s.Path, StringComparer.OrdinalIgnoreCase);
            var untrackedLookup = untracked.ToDictionary(s => s.Path, StringComparer.OrdinalIgnoreCase);

            // Get all files that are currently displayed in the tree
            var allFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAllFilePaths(_tree.Nodes, allFiles);

            // Only compute status for files that are actually in the tree
            foreach (var filePath in allFiles)
            {
                GitStatusType status = GitStatusType.None;

                // Check staged files first
                if (stagedLookup.TryGetValue(filePath, out var stagedEntry))
                {
                    if (stagedEntry.State.HasFlag(FileStatus.NewInIndex)) status = GitStatusType.Added;
                    else if (stagedEntry.State.HasFlag(FileStatus.ModifiedInIndex)) status = GitStatusType.Modified;
                    else if (stagedEntry.State.HasFlag(FileStatus.DeletedFromIndex)) status = GitStatusType.Deleted;
                    else if (stagedEntry.State.HasFlag(FileStatus.RenamedInIndex)) status = GitStatusType.Renamed;
                }
                // Check unstaged files
                else if (unstagedLookup.TryGetValue(filePath, out var unstagedEntry))
                {
                    if (unstagedEntry.State.HasFlag(FileStatus.NewInWorkdir)) status = GitStatusType.Added;
                    else if (unstagedEntry.State.HasFlag(FileStatus.ModifiedInWorkdir)) status = GitStatusType.Modified;
                    else if (unstagedEntry.State.HasFlag(FileStatus.DeletedFromWorkdir)) status = GitStatusType.Deleted;
                    else if (unstagedEntry.State.HasFlag(FileStatus.RenamedInWorkdir)) status = GitStatusType.Renamed;
                }
                // Check untracked files
                else if (untrackedLookup.TryGetValue(filePath, out var untrackedEntry))
                {
                    status = GitStatusType.Untracked;
                }

                _gitStatusCache[filePath] = status;
            }
        }
        catch
        {
            // Ignore git errors
            _gitStatusCache?.Clear();
        }
    }

    private void CollectAllFilePaths(TreeNodeCollection nodes, HashSet<string> files)
    {
        foreach (TreeNode node in nodes)
        {
            var path = GetNodePath(node);
            if (path != null && File.Exists(path))
            {
                files.Add(path);
            }
            CollectAllFilePaths(node.Nodes, files);
        }
    }

    public void SetActiveFile(string? filePath)
    {
        if (_activeFilePath == filePath) return;
        _activeFilePath = filePath;
        ActiveFileChanged?.Invoke(filePath);
        RefreshTreeHighlight();
    }

    private void RefreshTreeHighlight()
    {
        if (!IsHandleCreated) return;

        BeginInvoke(() =>
        {
            try
            {
                UpdateActiveFileHighlight(_tree.Nodes);
            }
            catch
            {
                // Ignore errors
            }
        });
    }

    private void UpdateActiveFileHighlight(TreeNodeCollection nodes)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is FileNodeTag fileTag)
            {
                if (fileTag.FilePath == _activeFilePath)
                {
                    // Active file - highest priority
                    node.BackColor = theme.Accent;
                    node.ForeColor = Color.White;
                }
                else if (_searchResultFiles?.Contains(fileTag.FilePath) == true)
                {
                    // Search result file
                    node.BackColor = Color.FromArgb(60, theme.Accent);
                    node.ForeColor = theme.Text;
                }
                else
                {
                    // Normal file
                    node.BackColor = Color.Transparent;
                    node.ForeColor = theme.Text;
                }
            }

            // Recursively update child nodes
            UpdateActiveFileHighlight(node.Nodes);
        }
    }

    private void UpdateGitStatusInNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is FileNodeTag existingTag && File.Exists(existingTag.FilePath))
            {
                GitStatusType gitStatus = GetGitStatusType(existingTag.FilePath);
                node.Tag = existingTag with { GitStatus = gitStatus };
            }
            else if (node.Tag is string filePath && File.Exists(filePath))
            {
                // Legacy support for nodes created before FileNodeTag
                string fileName = Path.GetFileName(filePath);
                GitStatusType gitStatus = GetGitStatusType(filePath);
                node.Text = fileName; // Clean filename without status text
                node.Tag = new FileNodeTag(filePath, gitStatus);
            }
            else if (node.Tag is string dirPath && Directory.Exists(dirPath))
            {
                // Directory node
                GitStatusType gitStatus = GetDirectoryGitStatusType(dirPath);
                node.Tag = new FileNodeTag(dirPath, gitStatus);
            }

            // Recursively update child nodes
            UpdateGitStatusInNodes(node.Nodes);
        }
    }

    private enum GitStatusType
    {
        None,
        Modified,
        Added,
        Deleted,
        Renamed,
        Untracked,
        Conflicted
    }

    private record FileNodeTag(string FilePath, GitStatusType GitStatus);

    /// <summary>
    /// Extracts the file path from a node tag, handling both string and FileNodeTag types.
    /// </summary>
    private string? GetNodePath(TreeNode? node)
    {
        if (node?.Tag is FileNodeTag fileTag)
            return fileTag.FilePath;
        if (node?.Tag is string path)
            return path;
        return null;
    }

    /// <summary>
    /// Gets all selected file paths from multi-selected nodes.
    /// </summary>
    private List<string> GetSelectedFilePaths()
    {
        return _multiSelectedNodes
            .Select(n => GetNodePath(n))
            .Where(p => p != null && File.Exists(p))
            .Cast<string>()
            .ToList();
    }

    private Color GetGitStatusColor(GitStatusType status, Theme theme)
    {
        return status switch
        {
            GitStatusType.Modified => Color.FromArgb(255, 200, 100), // Orange - modified
            GitStatusType.Added => Color.FromArgb(100, 200, 100),    // Green - added
            GitStatusType.Deleted => Color.FromArgb(200, 100, 100),  // Red - deleted
            GitStatusType.Renamed => Color.FromArgb(150, 150, 255),  // Blue - renamed
            GitStatusType.Untracked => Color.FromArgb(150, 150, 150), // Gray - untracked
            GitStatusType.Conflicted => Color.FromArgb(255, 100, 255), // Magenta - conflicted
            _ => theme.Text // None - use default text color
        };
    }

    private void LoadFavorites()
    {
        try
        {
            string filePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath)!, FavoritesFile);
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var favorites = JsonSerializer.Deserialize<HashSet<string>>(json);
                if (favorites != null)
                {
                    _favorites.UnionWith(favorites);
                }
            }
        }
        catch { }
    }

    private void SaveFavorites()
    {
        try
        {
            string filePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath)!, FavoritesFile);
            string json = JsonSerializer.Serialize(_favorites);
            File.WriteAllText(filePath, json);
        }
        catch { }
    }

    // Git bulk operations for selected files
    private void GitStageSelected()
    {
        var filePaths = GetSelectedFilePaths();
        if (filePaths.Count == 0) return;

        try
        {
            foreach (var path in filePaths)
            {
                _git?.Stage(path);
            }
            RefreshGitStatusIndicators();
            _git?.Refresh();
            ThemedMessageBox.Show($"Staged {filePaths.Count} file(s).", "Git Stage", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Git stage failed: {ex.Message}", "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GitUnstageSelected()
    {
        var filePaths = GetSelectedFilePaths();
        if (filePaths.Count == 0) return;

        try
        {
            foreach (var path in filePaths)
            {
                _git?.Unstage(path);
            }
            RefreshGitStatusIndicators();
            _git?.Refresh();
            ThemedMessageBox.Show($"Unstaged {filePaths.Count} file(s).", "Git Unstage", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Git unstage failed: {ex.Message}", "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GitDiscardSelected()
    {
        var filePaths = GetSelectedFilePaths();
        if (filePaths.Count == 0) return;

        string message = filePaths.Count == 1
            ? $"Discard changes to '{Path.GetFileName(filePaths[0])}'?"
            : $"Discard changes to {filePaths.Count} selected files?";

        var result = ThemedMessageBox.Show(message, "Discard Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;

        try
        {
            foreach (var path in filePaths)
            {
                _git?.DiscardFile(path);
            }
            RefreshGitStatusIndicators();
            _git?.Refresh();
            ThemedMessageBox.Show($"Discarded changes to {filePaths.Count} file(s).", "Git Discard", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Git discard failed: {ex.Message}", "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GitDiffSelected()
    {
        var filePaths = GetSelectedFilePaths();
        if (filePaths.Count == 0) return;

        // For now, show diff for the first selected file
        // Future enhancement: show multi-file diff
        var path = filePaths[0];
        try
        {
            var diff = _git?.GetDiffContent(path, false);
            if (!string.IsNullOrEmpty(diff))
            {
                // Open diff in a new tab or dedicated diff view
                // For now, show in message box (limited by size)
                if (diff.Length > 10000)
                {
                    ThemedMessageBox.Show($"Diff is too large to display ({diff.Length} chars). Use Git Panel for full diff.", "Large Diff", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    using var diffForm = new Form
                    {
                        Text = $"Diff: {Path.GetFileName(path)}",
                        Size = new Size(800, 600),
                        StartPosition = FormStartPosition.CenterParent
                    };

                    var textBox = new TextBox
                    {
                        Multiline = true,
                        ReadOnly = true,
                        ScrollBars = ScrollBars.Both,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 9),
                        Text = diff
                    };

                    diffForm.Controls.Add(textBox);
                    diffForm.ShowDialog(this);
                }
            }
            else
            {
                ThemedMessageBox.Show("No changes to diff.", "Git Diff", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Git diff failed: {ex.Message}", "Git Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }


}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

internal sealed class SolutionExplorerPanel : UserControl
{
    private const string DarkModeScrollbar = "DarkMode_Explorer";

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private readonly TreeView _tree;
    private readonly Panel _header;
    private readonly Label _title;
    private readonly Button _openBtn;
    private readonly Button _refreshBtn;
    private readonly Button _closeBtn;
    private readonly ContextMenuStrip _solutionMenu;
    private readonly ContextMenuStrip _projectMenu;
    private readonly ContextMenuStrip _fileMenu;
    private readonly ContextMenuStrip _packageMenu;

    private readonly Dictionary<TreeNode, ProjectNodeInfo> _projectNodes = new();
    private readonly Dictionary<TreeNode, FileNodeInfo> _fileNodes = new();
    private readonly Dictionary<TreeNode, PackageRef> _packageNodes = new();

    private CancellationTokenSource? _loadCts;
    private string? _startupProjectPath;

    public event Action<string>? FileOpenRequested;
    public event Action? CloseRequested;
    public event Action<string>? SolutionLoaded;

    public string? CurrentSolutionPath { get; private set; }

    public SolutionExplorerPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(120, 60);
        DoubleBuffered = true;

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowLines = false,
            ShowPlusMinus = true,
            ShowRootLines = false,
            FullRowSelect = true,
            HideSelection = false,
            LabelEdit = true,
            HotTracking = false,
            Indent = 22,
            ItemHeight = 24,
            DrawMode = TreeViewDrawMode.OwnerDrawText
        };
        _tree.DrawNode += Tree_DrawNode;
        _tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
        _tree.NodeMouseClick += Tree_NodeMouseClick;
        _tree.KeyDown += Tree_KeyDown;
        _tree.BeforeLabelEdit += Tree_BeforeLabelEdit;
        _tree.AfterLabelEdit += Tree_AfterLabelEdit;
        _tree.HandleCreated += (s, e) => ApplyTreeTheme();

        _header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(6, 4, 6, 4)
        };
        _header.Resize += (s, e) => LayoutHeader();

        _title = new Label
        {
            AutoSize = true,
            Text = "SOLUTION EXPLORER",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Location = new Point(6, 8)
        };

        _openBtn = CreateHeaderButton("📂 Open", OpenButton_Click, 74);
        _refreshBtn = CreateHeaderButton("↺ Refresh", (s, e) => Refresh(), 82);
        _closeBtn = CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);

        _header.Controls.AddRange([_title, _openBtn, _refreshBtn, _closeBtn]);

        _solutionMenu = new ContextMenuStrip();
        _solutionMenu.Items.Add("Build Solution", null, async (s, e) => await BuildCurrentSolutionAsync());
        _solutionMenu.Items.Add(new ToolStripSeparator());
        _solutionMenu.Items.Add("Open in File Explorer", null, (s, e) => OpenCurrentSolutionFolder());
        _solutionMenu.Items.Add("Reload Solution", null, (s, e) => Refresh());

        _projectMenu = new ContextMenuStrip();
        _projectMenu.Items.Add("Build Project", null, async (s, e) => await BuildSelectedProjectAsync());
        _projectMenu.Items.Add("Set as Startup Project", null, (s, e) => SetSelectedStartupProject());
        _projectMenu.Items.Add(new ToolStripSeparator());
        _projectMenu.Items.Add("Add New File...", null, (s, e) => AddNewFileToSelectedProject());
        _projectMenu.Items.Add("Add Existing File...", null, (s, e) => AddExistingFileToSelectedProject());
        _projectMenu.Items.Add(new ToolStripSeparator());
        _projectMenu.Items.Add("Open in File Explorer", null, (s, e) => OpenSelectedProjectFolder());
        _projectMenu.Items.Add("Remove Project", null, (s, e) => RemoveSelectedProject());

        _fileMenu = new ContextMenuStrip();
        _fileMenu.Items.Add("Open", null, (s, e) => OpenSelectedFile());
        _fileMenu.Items.Add("Rename", null, (s, e) => BeginRenameSelectedFile());
        _fileMenu.Items.Add("Delete", null, (s, e) => DeleteSelectedFile());
        _fileMenu.Items.Add(new ToolStripSeparator());
        _fileMenu.Items.Add("Open Containing Folder", null, (s, e) => OpenSelectedFileFolder());

        _packageMenu = new ContextMenuStrip();
        _packageMenu.Opening += PackageMenu_Opening;

        Controls.Add(_tree);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(ThemeManager.Instance.CurrentTheme);
        LayoutHeader();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            ThemeManager.Instance.ThemeChanged -= SetTheme;
        }

        base.Dispose(disposing);
    }

    public void LoadSolution(string slnPath)
    {
        if (string.IsNullOrWhiteSpace(slnPath))
            return;

        CurrentSolutionPath = slnPath;
        // Cancel but do NOT dispose immediately — the running task still holds the token.
        // The old CTS will be GC'd; only Dispose() disposes the current one on shutdown.
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadSolutionAsync(slnPath, _loadCts.Token);
    }

    private async Task LoadSolutionAsync(string slnPath, CancellationToken ct)
    {
        try
        {
            (SolutionFile solution, List<(SolutionProject proj, ProjectData data)> projects) result = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                string extension = Path.GetExtension(slnPath);
                SolutionFile solution;
                if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
                {
                    solution = SolutionParser.ParseSolution(slnPath);
                }
                else
                {
                    string projectName = Path.GetFileNameWithoutExtension(slnPath);
                    solution = new SolutionFile
                    {
                        Path = slnPath,
                        Projects =
                        [
                            new SolutionProject(
                                projectName,
                                Path.GetFileName(slnPath),
                                string.Empty,
                                Guid.NewGuid().ToString("D").ToUpperInvariant(),
                                null)
                        ]
                    };
                }

                var projects = new List<(SolutionProject proj, ProjectData data)>();
                string solutionDir = Path.GetDirectoryName(solution.Path) ?? string.Empty;
                foreach (SolutionProject project in solution.Projects)
                {
                    ct.ThrowIfCancellationRequested();
                    string fullProjectPath = Path.IsPathRooted(project.RelativePath)
                        ? project.RelativePath
                        : Path.GetFullPath(Path.Combine(solutionDir, project.RelativePath));
                    ProjectData data = SolutionParser.ParseProject(fullProjectPath);
                    projects.Add((project, data));
                }

                return (solution, projects);
            }, ct);

            if (ct.IsCancellationRequested || IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (ct.IsCancellationRequested || IsDisposed)
                    return;

                BuildTree(result.solution, result.projects);
                SolutionLoaded?.Invoke(slnPath);
            }));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                    ThemedMessageBox.Show(this, $"Failed to load solution: {ex.Message}", "Solution Explorer", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }
    }

    private void BuildTree(SolutionFile sln, List<(SolutionProject proj, ProjectData data)> projects)
    {
        _tree.BeginUpdate();
        try
        {
            _tree.Nodes.Clear();
            _projectNodes.Clear();
            _fileNodes.Clear();
            _packageNodes.Clear();

            string solutionDir = Path.GetDirectoryName(sln.Path) ?? string.Empty;
            TreeNode root = new($"📋 {sln.Name}") { Tag = sln.Path };
            _tree.Nodes.Add(root);

            Dictionary<string, TreeNode> folderNodes = new(StringComparer.OrdinalIgnoreCase);
            foreach (SolutionFolder folder in sln.Folders.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode folderNode = new($"📁 {folder.Name}");
                folderNodes[folder.Name] = folderNode;
                root.Nodes.Add(folderNode);
            }

            foreach ((SolutionProject proj, ProjectData data) in projects.OrderBy(p => p.proj.SolutionFolderName ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.proj.Name, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode parent = root;
                if (!string.IsNullOrWhiteSpace(proj.SolutionFolderName))
                {
                    if (!folderNodes.TryGetValue(proj.SolutionFolderName, out TreeNode? folderNode))
                    {
                        folderNode = new TreeNode($"📁 {proj.SolutionFolderName}");
                        folderNodes[proj.SolutionFolderName] = folderNode;
                        root.Nodes.Add(folderNode);
                    }

                    parent = folderNode;
                }

                string fullProjectPath = Path.IsPathRooted(proj.RelativePath)
                    ? proj.RelativePath
                    : Path.GetFullPath(Path.Combine(solutionDir, proj.RelativePath));
                TreeNode projectNode = CreateProjectNode(proj, data, fullProjectPath);
                parent.Nodes.Add(projectNode);
            }

            SortTree(root);
            root.Expand();
            foreach (TreeNode child in root.Nodes)
            {
                if (child.Text.StartsWith("📁 ", StringComparison.Ordinal))
                    child.Expand();
            }

            _tree.SelectedNode = root;
        }
        finally
        {
            _tree.EndUpdate();
        }
    }

    public new void Refresh()
    {
        if (CurrentSolutionPath != null)
            LoadSolution(CurrentSolutionPath);
    }

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        _title.ForeColor = theme.Text;

        ApplyButtonTheme(_openBtn, theme);
        ApplyButtonTheme(_refreshBtn, theme);
        ApplyButtonTheme(_closeBtn, theme);

        _tree.BackColor = theme.MenuBackground;
        _tree.ForeColor = theme.Text;
        _tree.LineColor = theme.Border;

        ApplyMenuTheme(_solutionMenu, theme);
        ApplyMenuTheme(_projectMenu, theme);
        ApplyMenuTheme(_fileMenu, theme);
        ApplyMenuTheme(_packageMenu, theme);

        ApplyTreeTheme();
        _tree.Invalidate();
        Invalidate();
    }

    private TreeNode CreateProjectNode(SolutionProject project, ProjectData data, string fullProjectPath)
    {
        TreeNode projectNode = new(GetProjectNodeText(project, data)) { Tag = fullProjectPath };
        _projectNodes[projectNode] = new ProjectNodeInfo(project, data, fullProjectPath);

        TreeNode dependenciesNode = new($"🔗 Dependencies") { Tag = $"deps:{project.Name}" };
        TreeNode packagesNode = new("📦 Packages") { Tag = $"packages:{project.Name}" };
        foreach (PackageRef package in data.Packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            string label = string.IsNullOrWhiteSpace(package.Version)
                ? $"📦 {package.Name}"
                : $"📦 {package.Name} {package.Version}";
            TreeNode packageNode = new(label) { Tag = $"package:{package.Name}" };
            _packageNodes[packageNode] = package;
            packagesNode.Nodes.Add(packageNode);
        }

        TreeNode projectRefsNode = new("📁 Projects") { Tag = $"projectrefs:{project.Name}" };
        string projectDir = Path.GetDirectoryName(fullProjectPath) ?? string.Empty;
        foreach (ProjectRef projectRef in data.ProjectRefs.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            string refPath = Path.GetFullPath(Path.Combine(projectDir, projectRef.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            projectRefsNode.Nodes.Add(new TreeNode($"↗ {projectRef.Name}") { Tag = refPath });
        }

        dependenciesNode.Nodes.Add(packagesNode);
        dependenciesNode.Nodes.Add(projectRefsNode);
        projectNode.Nodes.Add(dependenciesNode);

        Dictionary<string, TreeNode> folderMap = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectFileItem fileItem in data.Files
                     .Where(f => !string.IsNullOrWhiteSpace(f.RelativePath))
                     .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First())
                     .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            string relativePath = fileItem.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullFilePath = Path.GetFullPath(Path.Combine(projectDir, relativePath));
            string[] parts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            TreeNode parent = projectNode;
            string runningPath = string.Empty;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                runningPath = string.IsNullOrEmpty(runningPath) ? parts[i] : Path.Combine(runningPath, parts[i]);
                if (!folderMap.TryGetValue(runningPath, out TreeNode? folderNode))
                {
                    folderNode = new TreeNode($"📁 {parts[i]}") { Tag = $"folder:{runningPath.Replace('\\', '/')}" };
                    folderMap[runningPath] = folderNode;
                    parent.Nodes.Add(folderNode);
                }

                parent = folderNode;
            }

            string fileName = parts[^1];
            TreeNode fileNode = new($"{FileIconProvider.GetIconChar(fileName)} {fileName}") { Tag = fullFilePath };
            _fileNodes[fileNode] = new FileNodeInfo(fullFilePath, fullProjectPath, fileItem.RelativePath.Replace('\\', '/'));
            parent.Nodes.Add(fileNode);
        }

        dependenciesNode.Expand();
        return projectNode;
    }

    private void SortTree(TreeNode node)
    {
        var children = node.Nodes.Cast<TreeNode>()
            .OrderBy(GetSortBucket)
            .ThenBy(n => StripNodePrefix(n.Text), StringComparer.OrdinalIgnoreCase)
            .ToList();

        node.Nodes.Clear();
        foreach (TreeNode child in children)
        {
            node.Nodes.Add(child);
            SortTree(child);
        }
    }

    private static int GetSortBucket(TreeNode node)
    {
        string text = node.Text;
        if (text.StartsWith("🔗 ", StringComparison.Ordinal)) return 0;
        if (text.StartsWith("📦 ", StringComparison.Ordinal) && text.Contains('[', StringComparison.Ordinal)) return 1;
        if (text.StartsWith("📁 ", StringComparison.Ordinal)) return 2;
        return 3;
    }

    private void Tree_DrawNode(object? sender, DrawTreeNodeEventArgs e)
    {
        if (e.Node == null)
            return;

        Theme theme = ThemeManager.Instance.CurrentTheme;
        bool selected = (e.State & TreeNodeStates.Selected) != 0;
        Color backColor = selected ? theme.Highlight : theme.MenuBackground;
        Color foreColor = theme.Text;

        using SolidBrush bg = new(backColor);
        e.Graphics.FillRectangle(bg, new Rectangle(0, e.Bounds.Top, _tree.Width, e.Bounds.Height));

        Rectangle textBounds = new(e.Bounds.X, e.Bounds.Y + 2, _tree.Width - e.Bounds.X, e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            e.Node.Text,
            e.Node.NodeFont ?? _tree.Font,
            textBounds,
            foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node != null && _fileNodes.TryGetValue(e.Node, out FileNodeInfo? fileInfo) && File.Exists(fileInfo.FullPath))
            FileOpenRequested?.Invoke(fileInfo.FullPath);
    }

    private void Tree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node == null)
            return;

        _tree.SelectedNode = e.Node;
        ContextMenuStrip? menu = GetMenuForNode(e.Node);
        if (menu != null)
            menu.Show(_tree, e.Location);
    }

    private void Tree_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            Refresh();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2)
        {
            BeginRenameSelectedFile();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedFile();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            OpenSelectedFile();
            e.Handled = true;
        }
    }

    private void Tree_BeforeLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        e.CancelEdit = e.Node == null || !_fileNodes.ContainsKey(e.Node);
    }

    private void Tree_AfterLabelEdit(object? sender, NodeLabelEditEventArgs e)
    {
        if (e.Node == null || e.Label == null || !_fileNodes.TryGetValue(e.Node, out FileNodeInfo? fileInfo))
            return;

        string prefix = $"{FileIconProvider.GetIconChar(fileInfo.FullPath)} ";
        string newName = e.Label.Trim();
        if (newName.StartsWith(prefix, StringComparison.Ordinal))
            newName = newName[prefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            e.CancelEdit = true;
            return;
        }

        string directory = Path.GetDirectoryName(fileInfo.FullPath) ?? string.Empty;
        string newFullPath = Path.Combine(directory, newName);
        if (string.Equals(fileInfo.FullPath, newFullPath, StringComparison.OrdinalIgnoreCase))
        {
            e.CancelEdit = true;
            return;
        }

        if (File.Exists(newFullPath))
        {
            ThemedMessageBox.Show(this, "A file with that name already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            e.CancelEdit = true;
            return;
        }

        try
        {
            File.Move(fileInfo.FullPath, newFullPath);
            string projectDir = Path.GetDirectoryName(fileInfo.ProjectPath) ?? string.Empty;
            string newRelativePath = Path.GetRelativePath(projectDir, newFullPath).Replace('\\', '/');
            RenameProjectItemInclude(fileInfo.ProjectPath, fileInfo.RelativePath, newRelativePath);
            Refresh();
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Rename failed: {ex.Message}", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Error);
            e.CancelEdit = true;
        }
    }

    private void OpenSelectedFile()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node != null && _fileNodes.TryGetValue(node, out FileNodeInfo? fileInfo) && File.Exists(fileInfo.FullPath))
            FileOpenRequested?.Invoke(fileInfo.FullPath);
    }

    private void BeginRenameSelectedFile()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node != null && _fileNodes.ContainsKey(node))
            node.BeginEdit();
    }

    private void DeleteSelectedFile()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_fileNodes.TryGetValue(node, out FileNodeInfo? fileInfo))
            return;

        string name = Path.GetFileName(fileInfo.FullPath);
        DialogResult result = ThemedMessageBox.Show(
            this,
            $"Delete '{name}'?",
            "Delete File",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
            return;

        try
        {
            if (File.Exists(fileInfo.FullPath))
                File.Delete(fileInfo.FullPath);

            RemoveProjectItemInclude(fileInfo.ProjectPath, fileInfo.RelativePath);
            Refresh();
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Delete failed: {ex.Message}", "Delete File", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task BuildCurrentSolutionAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentSolutionPath))
            return;

        await RunBuildAsync(CurrentSolutionPath, "Solution Build");
    }

    private async Task BuildSelectedProjectAsync()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        await RunBuildAsync(projectInfo.ProjectPath, $"Build {projectInfo.Project.Name}");
    }

    private async Task RunBuildAsync(string targetPath, string title)
    {
        try
        {
            UseWaitCursor = true;
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{targetPath}\" -nologo",
                WorkingDirectory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            string combined = string.Join(Environment.NewLine, new[] { output, error }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            if (process.ExitCode == 0)
            {
                ThemedMessageBox.Show(this, string.IsNullOrWhiteSpace(combined) ? "Build succeeded." : $"Build succeeded.{Environment.NewLine}{Environment.NewLine}{Truncate(combined)}", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ThemedMessageBox.Show(this, string.IsNullOrWhiteSpace(combined) ? "Build failed." : Truncate(combined), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Build failed: {ex.Message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OpenCurrentSolutionFolder()
    {
        if (string.IsNullOrWhiteSpace(CurrentSolutionPath))
            return;

        string directory = Directory.Exists(CurrentSolutionPath)
            ? CurrentSolutionPath
            : Path.GetDirectoryName(CurrentSolutionPath) ?? CurrentSolutionPath;
        OpenPathInExplorer(directory, false);
    }

    private void OpenSelectedProjectFolder()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        string? directory = Path.GetDirectoryName(projectInfo.ProjectPath);
        if (!string.IsNullOrWhiteSpace(directory))
            OpenPathInExplorer(directory, false);
    }

    private void OpenSelectedFileFolder()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_fileNodes.TryGetValue(node, out FileNodeInfo? fileInfo))
            return;

        OpenPathInExplorer(fileInfo.FullPath, true);
    }

    private void SetSelectedStartupProject()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        _startupProjectPath = projectInfo.ProjectPath;
        UpdateProjectNodeLabels();
        ThemedMessageBox.Show(this, $"Startup project set to {projectInfo.Project.Name}.", "Solution Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateProjectNodeLabels()
    {
        foreach ((TreeNode node, ProjectNodeInfo info) in _projectNodes)
            node.Text = GetProjectNodeText(info.Project, info.Data);

        _tree.Invalidate();
    }

    private void AddNewFileToSelectedProject()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        string projectDir = Path.GetDirectoryName(projectInfo.ProjectPath) ?? string.Empty;
        using SaveFileDialog sfd = new()
        {
            Title = "Add New File",
            InitialDirectory = projectDir,
            FileName = "NewFile.cs",
            Filter = "C# Files|*.cs|Text Files|*.txt|All Files|*.*",
            OverwritePrompt = true
        };

        if (sfd.ShowThemed() != DialogResult.OK)
            return;

        try
        {
            string newPath = sfd.FileName;
            string? newDir = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrWhiteSpace(newDir))
                Directory.CreateDirectory(newDir);
            if (!File.Exists(newPath))
                File.WriteAllText(newPath, string.Empty);
            EnsureProjectItemIncluded(projectInfo.ProjectPath, newPath);
            Refresh();
            FileOpenRequested?.Invoke(newPath);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Could not add file: {ex.Message}", "Add File", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddExistingFileToSelectedProject()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        using OpenFileDialog ofd = new()
        {
            Title = "Add Existing File",
            Filter = "All Files|*.*",
            Multiselect = false
        };

        if (ofd.ShowThemed() != DialogResult.OK)
            return;

        string projectDir = Path.GetDirectoryName(projectInfo.ProjectPath) ?? string.Empty;
        string targetPath = Path.Combine(projectDir, Path.GetFileName(ofd.FileName));

        try
        {
            if (!string.Equals(ofd.FileName, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(targetPath))
            {
                DialogResult overwrite = ThemedMessageBox.Show(this, $"'{Path.GetFileName(targetPath)}' already exists. Overwrite it?", "Add Existing File", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (overwrite != DialogResult.Yes)
                    return;
            }

            if (!string.Equals(ofd.FileName, targetPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(ofd.FileName, targetPath, true);

            EnsureProjectItemIncluded(projectInfo.ProjectPath, targetPath);
            Refresh();
            FileOpenRequested?.Invoke(targetPath);
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Could not add file: {ex.Message}", "Add Existing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveSelectedProject()
    {
        TreeNode? node = _tree.SelectedNode;
        if (node == null || !_projectNodes.TryGetValue(node, out ProjectNodeInfo? projectInfo))
            return;

        if (string.IsNullOrWhiteSpace(CurrentSolutionPath) || !string.Equals(Path.GetExtension(CurrentSolutionPath), ".sln", StringComparison.OrdinalIgnoreCase))
        {
            ThemedMessageBox.Show(this, "Project removal is only supported when a .sln file is open.", "Solution Explorer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult result = ThemedMessageBox.Show(this, $"Remove project '{projectInfo.Project.Name}' from the solution?", "Remove Project", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes)
            return;

        try
        {
            RemoveProjectFromSolution(CurrentSolutionPath, projectInfo.Project);
            Refresh();
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show(this, $"Could not remove project: {ex.Message}", "Remove Project", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void RemoveProjectFromSolution(string solutionPath, SolutionProject project)
    {
        List<string> lines = File.ReadAllLines(solutionPath).ToList();
        string guidToken = "{" + project.ProjectGuid.ToUpperInvariant() + "}";
        string projectPathToken = project.RelativePath.Replace('\\', '/');
        bool skippingProject = false;
        List<string> output = new(lines.Count);

        foreach (string line in lines)
        {
            string normalizedLine = line.Replace('\\', '/');
            if (!skippingProject && line.StartsWith("Project(", StringComparison.OrdinalIgnoreCase) && normalizedLine.Contains(projectPathToken, StringComparison.OrdinalIgnoreCase) && line.Contains(guidToken, StringComparison.OrdinalIgnoreCase))
            {
                skippingProject = true;
                continue;
            }

            if (skippingProject)
            {
                if (line.Trim().Equals("EndProject", StringComparison.OrdinalIgnoreCase))
                    skippingProject = false;
                continue;
            }

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith(guidToken, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(project.ProjectGuid + ".", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("{" + project.ProjectGuid + "}.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            output.Add(line);
        }

        File.WriteAllLines(solutionPath, output);
    }

    private void EnsureProjectItemIncluded(string projectPath, string filePath)
    {
        if (!File.Exists(projectPath))
            return;

        XDocument doc = XDocument.Load(projectPath);
        if (IsSdkStyleProject(doc))
            return;

        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        string projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        string relativePath = Path.GetRelativePath(projectDir, filePath).Replace('\\', '/');
        string itemType = DetermineItemType(filePath);

        bool exists = doc.Descendants()
            .Any(e => string.Equals(e.Name.LocalName, itemType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizePath(e.Attribute("Include")?.Value), relativePath, StringComparison.OrdinalIgnoreCase));
        if (exists)
            return;

        XElement itemGroup = doc.Root?.Elements(ns + "ItemGroup").FirstOrDefault() ?? new XElement(ns + "ItemGroup");
        if (itemGroup.Parent == null)
            doc.Root?.Add(itemGroup);
        itemGroup.Add(new XElement(ns + itemType, new XAttribute("Include", relativePath)));
        doc.Save(projectPath);
    }

    private void RenameProjectItemInclude(string projectPath, string oldRelativePath, string newRelativePath)
    {
        if (!File.Exists(projectPath))
            return;

        XDocument doc = XDocument.Load(projectPath);
        bool changed = false;
        foreach (XElement element in doc.Descendants().Where(e => e.Attribute("Include") != null))
        {
            string include = NormalizePath(element.Attribute("Include")?.Value);
            if (string.Equals(include, NormalizePath(oldRelativePath), StringComparison.OrdinalIgnoreCase))
            {
                element.SetAttributeValue("Include", NormalizePath(newRelativePath));
                changed = true;
            }
        }

        if (changed)
            doc.Save(projectPath);
    }

    private void RemoveProjectItemInclude(string projectPath, string relativePath)
    {
        if (!File.Exists(projectPath))
            return;

        XDocument doc = XDocument.Load(projectPath);
        List<XElement> matches = doc.Descendants()
            .Where(e => e.Attribute("Include") != null
                && string.Equals(NormalizePath(e.Attribute("Include")?.Value), NormalizePath(relativePath), StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0)
            return;

        foreach (XElement element in matches)
            element.Remove();
        doc.Save(projectPath);
    }

    private static bool IsSdkStyleProject(XDocument doc)
    {
        return doc.Root?.Attribute("Sdk") != null
            || doc.Descendants().Any(e => e.Name.LocalName == "Import" && e.Attribute("Sdk") != null);
    }

    private static string DetermineItemType(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        return ext.ToLowerInvariant() switch
        {
            ".cs" or ".vb" or ".fs" => "Compile",
            ".resx" => "EmbeddedResource",
            ".json" or ".xml" or ".config" or ".txt" or ".md" => "None",
            _ => "Content"
        };
    }

    private static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/');

    private static string Truncate(string text, int maxLength = 3000)
    {
        if (text.Length <= maxLength)
            return text;
        return text[..maxLength] + Environment.NewLine + "...";
    }

    private string GetProjectNodeText(SolutionProject project, ProjectData data)
    {
        string tf = string.IsNullOrWhiteSpace(data.TargetFramework) ? string.Empty : $" [{data.TargetFramework}]";
        string startup = string.Equals(_startupProjectPath, data.Path, StringComparison.OrdinalIgnoreCase) ? " (Startup)" : string.Empty;
        return $"📦 {project.Name}{tf}{startup}";
    }

    private static string StripNodePrefix(string text)
    {
        int index = text.IndexOf(' ');
        return index >= 0 && index + 1 < text.Length ? text[(index + 1)..] : text;
    }

    private ContextMenuStrip? GetMenuForNode(TreeNode node)
    {
        if (_fileNodes.ContainsKey(node))
            return _fileMenu;
        if (_packageNodes.ContainsKey(node))
            return _packageMenu;
        if (_projectNodes.ContainsKey(node))
            return _projectMenu;
        if (node.Tag is string tag && string.Equals(tag, CurrentSolutionPath, StringComparison.OrdinalIgnoreCase))
            return _solutionMenu;
        return null;
    }

    private void PackageMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _packageMenu.Items.Clear();
        if (_tree.SelectedNode != null && _packageNodes.TryGetValue(_tree.SelectedNode, out PackageRef? package))
        {
            _packageMenu.Items.Add(new ToolStripMenuItem($"Package: {package.Name} {package.Version}" ) { Enabled = false });
        }
        else
        {
            e.Cancel = true;
        }
    }

    private void OpenButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new()
        {
            Title = "Open Solution or Project",
            Filter = "Solution Files|*.sln|Project Files|*.csproj;*.vbproj;*.fsproj|All Files|*.*",
            Multiselect = false
        };
        if (ofd.ShowThemed() == DialogResult.OK)
            LoadSolution(ofd.FileName);
    }

    private static void OpenPathInExplorer(string path, bool selectItem)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = selectItem ? $"/select,\"{path}\"" : $"\"{path}\"",
            UseShellExecute = true
        });
    }

    private Button CreateHeaderButton(string text, EventHandler onClick, int width)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        button.FlatAppearance.BorderSize = 1;
        button.Click += onClick;
        return button;
    }

    private void LayoutHeader()
    {
        const int top = 4;
        const int spacing = 6;
        _closeBtn.Location = new Point(_header.Width - _closeBtn.Width - 6, top);
        _refreshBtn.Location = new Point(_closeBtn.Left - _refreshBtn.Width - spacing, top);
        _openBtn.Location = new Point(_refreshBtn.Left - _openBtn.Width - spacing, top);
    }

    private void ApplyButtonTheme(Button button, Theme theme)
    {
        button.BackColor = theme.PanelBackground;
        button.ForeColor = theme.Text;
        button.FlatAppearance.BorderColor = theme.Border;
        button.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        button.FlatAppearance.MouseDownBackColor = theme.Highlight;
    }

    private static void ApplyMenuTheme(ContextMenuStrip menu, Theme theme)
    {
        menu.Renderer = new ThemeAwareMenuRenderer(theme);
        menu.BackColor = theme.MenuBackground;
        menu.ForeColor = theme.Text;
    }

    private void ApplyTreeTheme()
    {
        if (!_tree.IsHandleCreated)
            return;

        Theme theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(_tree.Handle, theme.IsLight ? null : DarkModeScrollbar, null);
    }

    private sealed record ProjectNodeInfo(SolutionProject Project, ProjectData Data, string ProjectPath);
    private sealed record FileNodeInfo(string FullPath, string ProjectPath, string RelativePath);
}

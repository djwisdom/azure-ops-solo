using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class FavoritesManagerPanel : UserControl
{
    private readonly Action<string> _navigateCallback;
    private readonly FlowLayoutPanel _toolbarButtons;
    private readonly Button _addFavoriteButton;
    private readonly Button _addFolderButton;
    private readonly Button _importEdgeButton;
    private readonly Button _importChromeButton;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly Button _closeButton;
    private readonly Panel _toolbar;
    private readonly SplitContainer _splitContainer;
    private readonly TreeView _folderTree;
    private readonly ListView _itemsList;
    private readonly Panel _statusBar;
    private readonly Label _statusLabel;
    private readonly ContextMenuStrip _listMenu;
    private readonly ContextMenuStrip _treeMenu;
    private Theme _theme;

    public FavoritesManagerPanel(Action<string> navigateCallback, Theme theme)
    {
        _navigateCallback = navigateCallback;
        _theme = theme;
        Dock = DockStyle.Fill;

        _toolbarButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 6, 0, 6),
            Margin = new Padding(0)
        };

        _addFavoriteButton = CreateToolbarButton("+ Add Favorite");
        _addFolderButton = CreateToolbarButton("+ Add Folder");
        _importEdgeButton = CreateToolbarButton("Import from Edge");
        _importChromeButton = CreateToolbarButton("Import from Chrome");
        _moveUpButton   = CreateToolbarButton("↑ Move Up");
        _moveDownButton = CreateToolbarButton("↓ Move Down");
        _closeButton = CreateToolbarButton("✕ Close");
        _closeButton.Dock = DockStyle.Right;

        _toolbarButtons.Controls.AddRange([_addFavoriteButton, _addFolderButton, _importEdgeButton, _importChromeButton, _moveUpButton, _moveDownButton]);

        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38
        };
        _toolbar.Controls.Add(_closeButton);
        _toolbar.Controls.Add(_toolbarButtons);

        _folderTree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            BorderStyle = BorderStyle.None
        };

        _itemsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            GridLines = false,
            BorderStyle = BorderStyle.None,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _itemsList.Columns.Add("Name", 220);
        _itemsList.Columns.Add("URL", 300);

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.None,
            IsSplitterFixed = false,
            SplitterWidth = 5,
            Panel1MinSize = 120,
            Panel2MinSize = 200,
        };
        // SplitterDistance cannot be set before the control has real width;
        // set it once on the first layout pass instead.
        _splitContainer.Layout += OnSplitContainerFirstLayout;
        _splitContainer.Panel1.Controls.Add(_folderTree);
        _splitContainer.Panel2.Controls.Add(_itemsList);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        _statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22
        };
        _statusBar.Controls.Add(_statusLabel);

        _listMenu = new ContextMenuStrip();
        _treeMenu = new ContextMenuStrip();

        Controls.Add(_splitContainer);
        Controls.Add(_statusBar);
        Controls.Add(_toolbar);

        WireEvents();
        ApplyTheme(theme);
        RefreshFavoritesView(null);
    }

    public void ApplyTheme(Theme theme)
    {
        _theme = theme;

        BackColor = theme.PanelBackground;
        ForeColor = theme.Text;

        foreach (var button in new[] { _addFavoriteButton, _addFolderButton, _importEdgeButton, _importChromeButton, _moveUpButton, _moveDownButton, _closeButton })
            ApplyButtonTheme(button);

        _toolbar.BackColor = theme.MenuBackground;

        _splitContainer.BackColor = theme.Border;
        _splitContainer.Panel1.BackColor = theme.PanelBackground;
        _splitContainer.Panel2.BackColor = theme.PanelBackground;
        _folderTree.BackColor = theme.EditorBackground;
        _folderTree.ForeColor = theme.Text;
        _itemsList.BackColor = theme.EditorBackground;
        _itemsList.ForeColor = theme.Text;
        _statusBar.BackColor = theme.MenuBackground;
        _statusLabel.BackColor = theme.MenuBackground;
        _statusLabel.ForeColor = theme.Muted;

        ApplyMenuTheme(_listMenu);
        ApplyMenuTheme(_treeMenu);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            FavoritesService.Instance.Changed -= FavoritesChanged;
            ThemeManager.Instance.ThemeChanged -= ThemeChanged;
        }

        base.Dispose(disposing);
    }

    private void OnSplitContainerFirstLayout(object? sender, LayoutEventArgs e)
    {
        if (_splitContainer.Width <= _splitContainer.Panel1MinSize + _splitContainer.Panel2MinSize + _splitContainer.SplitterWidth)
            return;

        _splitContainer.Layout -= OnSplitContainerFirstLayout;  // one-shot
        _splitContainer.SplitterDistance = Math.Max(_splitContainer.Panel1MinSize, 220);
    }

    private void WireEvents()
    {
        _addFavoriteButton.Click += (_, _) => AddFavorite();
        _addFolderButton.Click += (_, _) => AddFolder(GetSelectedFolderId());
        _importEdgeButton.Click += (_, _) => ImportFavorites("edge");
        _importChromeButton.Click += (_, _) => ImportFavorites("chrome");
        _moveUpButton.Click   += (_, _) => MoveSelectedItem(-1);
        _moveDownButton.Click += (_, _) => MoveSelectedItem(+1);
        _closeButton.Click += (_, _) => _navigateCallback("close");

        _folderTree.AfterSelect += (_, _) => RefreshList();
        _folderTree.NodeMouseClick += (_, e) =>
        {
            _folderTree.SelectedNode = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                ConfigureTreeMenu();
                _treeMenu.Show(_folderTree, e.Location);
            }
        };

        _itemsList.MouseDoubleClick += (_, _) => OpenSelectedListItem();
        _itemsList.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;

            var hit = _itemsList.HitTest(e.Location);
            if (hit.Item == null)
                return;

            hit.Item.Selected = true;
            ConfigureListMenu();
            _listMenu.Show(_itemsList, e.Location);
        };
        _itemsList.Resize += (_, _) => ResizeListColumns();

        FavoritesService.Instance.Changed += FavoritesChanged;
        ThemeManager.Instance.ThemeChanged += ThemeChanged;
    }

    private void ThemeChanged(Theme theme)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyTheme(theme));
            return;
        }

        ApplyTheme(theme);
    }

    private void FavoritesChanged()
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(() => RefreshFavoritesView(GetSelectedFolderId()));
            return;
        }

        RefreshFavoritesView(GetSelectedFolderId());
    }

    private void RefreshFavoritesView(string? preferredFolderId)
    {
        BuildFolderTree(preferredFolderId);
        RefreshList();
    }

    private void BuildFolderTree(string? preferredFolderId)
    {
        _folderTree.BeginUpdate();
        try
        {
            _folderTree.Nodes.Clear();

            var root = new TreeNode("Favorites Bar");
            AddFolderNodes(root, null);
            root.Expand();
            _folderTree.Nodes.Add(root);

            TreeNode selectedNode = FindNodeByFolderId(root, preferredFolderId) ?? root;
            _folderTree.SelectedNode = selectedNode;
        }
        finally
        {
            _folderTree.EndUpdate();
        }
    }

    private void AddFolderNodes(TreeNode parentNode, string? parentId)
    {
        foreach (var folder in FavoritesService.Instance.GetChildren(parentId).Where(item => item.IsFolder))
        {
            var node = new TreeNode(folder.Name) { Tag = folder.Id };
            parentNode.Nodes.Add(node);
            AddFolderNodes(node, folder.Id);
        }
    }

    private void RefreshList()
    {
        string? folderId = GetSelectedFolderId();
        var children = FavoritesService.Instance.GetChildren(folderId);

        _itemsList.BeginUpdate();
        try
        {
            _itemsList.Items.Clear();

            foreach (var item in children)
            {
                string displayName = item.IsFolder ? $"📁 {item.Name}" : item.Name;
                var row = new ListViewItem(displayName) { Tag = item };
                row.SubItems.Add(item.Url ?? "");
                _itemsList.Items.Add(row);
            }
        }
        finally
        {
            _itemsList.EndUpdate();
        }

        ResizeListColumns();
        _statusLabel.Text = children.Count == 1 ? "1 item" : $"{children.Count} items";
    }

    private void AddFavorite()
    {
        string? name = SimpleInputDialog.Show(this, "Enter a display name for the favorite.", "Add Favorite");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? url = SimpleInputDialog.Show(this, "Enter the URL to save.", "Add Favorite", "https://");
        if (string.IsNullOrWhiteSpace(url))
            return;

        FavoritesService.Instance.AddUrl(name, url, GetSelectedFolderId());
    }

    private void AddFolder(string? parentId)
    {
        string? name = SimpleInputDialog.Show(this, "Enter a folder name.", "Add Folder");
        if (string.IsNullOrWhiteSpace(name))
            return;

        FavoritesService.Instance.AddFolder(name, parentId);
    }

    private void ImportFavorites(string source)
    {
        int beforeCount = FavoritesService.Instance.Items.Count;
        bool found = FavoritesService.Instance.ImportFromBrowser(source);
        if (!found)
        {
            MessageBox.Show(this,
                source == "chrome" ? "Chrome bookmarks file was not found." : "Edge bookmarks file was not found.",
                "Import Favorites",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        int importedCount = FavoritesService.Instance.Items.Count - beforeCount;
        string browserName = source == "chrome" ? "Chrome" : "Edge";
        MessageBox.Show(this,
            importedCount > 0
                ? $"Imported {importedCount} item{(importedCount == 1 ? "" : "s")} from {browserName}."
                : $"No new items were imported from {browserName}.",
            "Import Favorites",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ConfigureListMenu()
    {
        _listMenu.Items.Clear();

        var selected = GetSelectedListItem();
        if (selected == null)
            return;

        var renameItem = new ToolStripMenuItem("Rename");
        renameItem.Click += (_, _) => RenameItem(selected);

        var deleteItem = new ToolStripMenuItem("Delete");
        deleteItem.Click += (_, _) => DeleteItem(selected);

        var openItem = new ToolStripMenuItem("Open in browser")
        {
            Enabled = !selected.IsFolder && !string.IsNullOrWhiteSpace(selected.Url)
        };
        openItem.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(selected.Url))
                _navigateCallback(selected.Url);
        };

        var moveUpItem   = new ToolStripMenuItem("↑ Move Up");
        moveUpItem.Click += (_, _) => MoveSelectedItem(-1);
        var moveDownItem = new ToolStripMenuItem("↓ Move Down");
        moveDownItem.Click += (_, _) => MoveSelectedItem(+1);

        _listMenu.Items.Add(renameItem);
        _listMenu.Items.Add(deleteItem);
        _listMenu.Items.Add(moveUpItem);
        _listMenu.Items.Add(moveDownItem);
        _listMenu.Items.Add(openItem);
    }

    private void ConfigureTreeMenu()
    {
        _treeMenu.Items.Clear();
        string? folderId = GetSelectedFolderId();
        bool isRoot = folderId == null;
        FavItem? folder = folderId == null ? null : FavoritesService.Instance.Items.FirstOrDefault(item => item.Id == folderId);

        var renameItem = new ToolStripMenuItem("Rename folder") { Enabled = !isRoot && folder != null };
        renameItem.Click += (_, _) =>
        {
            if (folder != null)
                RenameItem(folder);
        };

        var deleteItem = new ToolStripMenuItem("Delete folder") { Enabled = !isRoot && folder != null };
        deleteItem.Click += (_, _) =>
        {
            if (folder != null)
                DeleteItem(folder);
        };

        var addItem = new ToolStripMenuItem("Add subfolder");
        addItem.Click += (_, _) => AddFolder(folderId);

        _treeMenu.Items.Add(renameItem);
        _treeMenu.Items.Add(deleteItem);
        _treeMenu.Items.Add(addItem);
    }

    private void RenameItem(FavItem item)
    {
        string? newName = SimpleInputDialog.Show(this, "Enter a new name.", "Rename Favorite", item.Name);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        FavoritesService.Instance.Rename(item.Id, newName);
    }

    private void DeleteItem(FavItem item)
    {
        string caption = item.IsFolder ? "Delete Folder" : "Delete Favorite";
        string message = item.IsFolder
            ? $"Delete '{item.Name}' and all of its contents?"
            : $"Delete '{item.Name}'?";

        if (MessageBox.Show(this, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        FavoritesService.Instance.Delete(item.Id);
    }

    private void MoveSelectedItem(int direction)
    {
        var selected = GetSelectedListItem();
        if (selected == null) return;

        string? parentId = selected.ParentId;
        var siblings = FavoritesService.Instance.GetChildren(parentId);
        int idx = siblings.FindIndex(x => x.Id == selected.Id);
        int newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= siblings.Count) return;

        // Swap orders with the neighbour
        var neighbour = siblings[newIdx];
        FavoritesService.Instance.Move(selected.Id, parentId, neighbour.Order);
        FavoritesService.Instance.Move(neighbour.Id, parentId, selected.Order);
    }

    private void OpenSelectedListItem()
    {
        var selected = GetSelectedListItem();
        if (selected == null)
            return;

        if (selected.IsFolder)
        {
            TreeNode? node = FindNodeByFolderId(_folderTree.Nodes.Count > 0 ? _folderTree.Nodes[0] : null, selected.Id);
            if (node != null)
            {
                _folderTree.SelectedNode = node;
                node.EnsureVisible();
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(selected.Url))
            _navigateCallback(selected.Url);
    }

    private FavItem? GetSelectedListItem()
    {
        return _itemsList.SelectedItems.Count == 0 ? null : _itemsList.SelectedItems[0].Tag as FavItem;
    }

    private string? GetSelectedFolderId()
    {
        return _folderTree.SelectedNode?.Tag as string;
    }

    private void ResizeListColumns()
    {
        if (_itemsList.Columns.Count < 2)
            return;

        const int urlWidth = 300;
        int nameWidth = Math.Max(180, _itemsList.ClientSize.Width - urlWidth - 6);
        _itemsList.Columns[0].Width = nameWidth;
        _itemsList.Columns[1].Width = urlWidth;
    }

    private Button CreateToolbarButton(string text)
    {
        return new Button
        {
            AutoSize = true,
            Height = 26,
            Text = text,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 6, 0),
            Padding = new Padding(8, 2, 8, 2),
            Cursor = Cursors.Hand
        };
    }

    private void ApplyButtonTheme(Button button)
    {
        button.BackColor = _theme.MenuBackground;
        button.ForeColor = _theme.Text;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = _theme.Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, _theme.Text);
    }

    private void ApplyMenuTheme(ContextMenuStrip menu)
    {
        menu.BackColor = _theme.MenuBackground;
        menu.ForeColor = _theme.Text;
        menu.RenderMode = ToolStripRenderMode.System;
    }

    private static TreeNode? FindNodeByFolderId(TreeNode? startNode, string? folderId)
    {
        if (startNode == null)
            return null;

        if (folderId == null && startNode.Tag == null)
            return startNode;

        if (string.Equals(startNode.Tag as string, folderId, StringComparison.OrdinalIgnoreCase))
            return startNode;

        foreach (TreeNode child in startNode.Nodes)
        {
            var match = FindNodeByFolderId(child, folderId);
            if (match != null)
                return match;
        }

        return null;
    }
}

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MyCrownJewelApp.Pfpad.Features.RoslynControl;

public sealed class RoslynVisualizerForm : Form
{
    private readonly TabControl _tabControl;
    private readonly TreeView _syntaxTreeView;
    private readonly ListView _semanticInfoView;
    private readonly DataGridView _diagnosticsGrid;
    private readonly ListView _generatedSourcesView;
    private readonly RichTextBox _generatedSourcePreview;
    private readonly SplitContainer _generatedSplit;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _copyButton;
    private RoslynVisualizerSnapshot? _currentSnapshot;

    private const int FormWidth = 700;
    private const int FormHeight = 500;

    public RoslynVisualizerForm()
    {
        Text = "Roslyn Visualizer";
        Size = new Size(FormWidth, FormHeight);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var toolbar = new ToolStrip();
        _refreshButton = new ToolStripButton("Refresh");
        _refreshButton.Click += (s, e) => _ = RefreshAsync();
        _copyButton = new ToolStripButton("Copy to Clipboard");
        _copyButton.Click += (s, e) => CopyCurrentTab();
        toolbar.Items.Add(_refreshButton);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(_copyButton);
        toolbar.Items.Add(new ToolStripSeparator());
        _statusLabel = new ToolStripStatusLabel("Ready");
        toolbar.Items.Add(new ToolStripLabel("  "));
        toolbar.Items.Add(_statusLabel);

        mainPanel.Controls.Add(toolbar, 0, 0);

        _tabControl = new TabControl { Dock = DockStyle.Fill };

        _syntaxTreeView = new TreeView
        {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            Font = new Font("Consolas", 9),
        };
        _tabControl.TabPages.Add(new TabPage("Syntax Tree") { Controls = { _syntaxTreeView } });

        _semanticInfoView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 9),
        };
        _semanticInfoView.Columns.Add("Property", 160);
        _semanticInfoView.Columns.Add("Value", 400);
        _tabControl.TabPages.Add(new TabPage("Semantic Info") { Controls = { _semanticInfoView } });

        _diagnosticsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Font = new Font("Consolas", 9),
        };
        _diagnosticsGrid.Columns.Add("Severity", "Severity");
        _diagnosticsGrid.Columns.Add("Id", "Id");
        _diagnosticsGrid.Columns.Add("Message", "Message");
        _diagnosticsGrid.Columns.Add("Location", "Location");
        _tabControl.TabPages.Add(new TabPage("Diagnostics") { Controls = { _diagnosticsGrid } });

        _generatedSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };
        _generatedSourcesView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            Font = new Font("Consolas", 9),
        };
        _generatedSourcesView.Columns.Add("Generated File", 300);
        _generatedSourcesView.SelectedIndexChanged += GeneratedSourceSelected;
        _generatedSourcePreview = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            WordWrap = false,
        };
        _generatedSplit.Panel1.Controls.Add(_generatedSourcesView);
        _generatedSplit.Panel2.Controls.Add(_generatedSourcePreview);
        _tabControl.TabPages.Add(new TabPage("Generated Sources") { Controls = { _generatedSplit } });

        mainPanel.Controls.Add(_tabControl, 0, 1);
        Controls.Add(mainPanel);

        ThemeManager.Instance.ThemeChanged += theme => { if (!IsDisposed) BeginInvoke(ApplyTheme); };
        ApplyTheme();
    }

    public Task RefreshAsync()
    {
        _statusLabel.Text = "Refreshing...";
        _refreshButton.Enabled = false;
        try
        {
            OnRefreshRequested?.Invoke();
        }
        finally
        {
            _refreshButton.Enabled = true;
        }

        return Task.CompletedTask;
    }

    public void UpdateSnapshot(RoslynVisualizerSnapshot? snapshot)
    {
        _currentSnapshot = snapshot;

        if (snapshot is null)
        {
            _statusLabel.Text = "No document open or snapshot unavailable.";
            ClearAllTabs();
            return;
        }

        _statusLabel.Text = $"Snapshot updated — {snapshot.Document.Name}";
        PopulateSyntaxTree(snapshot);
        PopulateSemanticInfo(snapshot);
        PopulateDiagnostics(snapshot);
        PopulateGeneratedSources(snapshot);
    }

    private void ClearAllTabs()
    {
        _syntaxTreeView.Nodes.Clear();
        _semanticInfoView.Items.Clear();
        _diagnosticsGrid.Rows.Clear();
        _generatedSourcesView.Items.Clear();
        _generatedSourcePreview.Clear();
    }

    private void PopulateSyntaxTree(RoslynVisualizerSnapshot snapshot)
    {
        _syntaxTreeView.Nodes.Clear();
        if (snapshot.SyntaxRoot is null) return;

        var rootNode = BuildTreeNode(snapshot.SyntaxRoot);
        _syntaxTreeView.Nodes.Add(rootNode);
        rootNode.Expand();
    }

    private static TreeNode BuildTreeNode(SyntaxNode node)
    {
        var span = node.Span;
        var tn = new TreeNode($"{node.Kind()} [{span.Start}..{span.End}]");
        foreach (var child in node.ChildNodes())
            tn.Nodes.Add(BuildTreeNode(child));
        foreach (var token in node.ChildTokens())
        {
            var text = token.Text;
            if (text.Length > 60) text = text[..60] + "...";
            tn.Nodes.Add($"{token.Kind()}: \"{text}\"");
        }
        return tn;
    }

    private void PopulateSemanticInfo(RoslynVisualizerSnapshot snapshot)
    {
        _semanticInfoView.Items.Clear();
        if (snapshot.SemanticModel is null)
        {
            _semanticInfoView.Items.Add(new ListViewItem(["Model", "null"]));
            return;
        }

        var sm = snapshot.SemanticModel;
        AddSemanticItem("Compilation", sm.Compilation?.AssemblyName ?? "unknown");
        AddSemanticItem("Language", sm.Language);
        AddSemanticItem("Syntax Tree Count", sm.Compilation?.SyntaxTrees.Count().ToString() ?? "0");
        AddSemanticItem("Source Module", sm.Compilation?.SourceModule?.Name ?? "unknown");
    }

    private void AddSemanticItem(string property, string value)
    {
        _semanticInfoView.Items.Add(new ListViewItem([property, value]));
    }

    private void PopulateDiagnostics(RoslynVisualizerSnapshot snapshot)
    {
        _diagnosticsGrid.Rows.Clear();
        foreach (var diag in snapshot.Diagnostics)
        {
            string location = diag.Location.IsInSource
                ? $"{diag.Location.GetLineSpan().StartLinePosition.Line + 1},{diag.Location.GetLineSpan().StartLinePosition.Character + 1}"
                : "(metadata)";
            var msg = diag.GetMessage();
            if (msg.Length > 120) msg = msg[..120] + "...";
            _diagnosticsGrid.Rows.Add(
                diag.Severity.ToString(),
                diag.Id,
                msg,
                location);
        }
    }

    private void PopulateGeneratedSources(RoslynVisualizerSnapshot snapshot)
    {
        _generatedSourcesView.Items.Clear();
        _generatedSourcePreview.Clear();

        foreach (var tree in snapshot.GeneratedTrees)
        {
            var path = tree.FilePath;
            if (string.IsNullOrEmpty(path))
                path = $"[generated] {tree.GetText().Lines.Count} lines";
            var item = new ListViewItem(path);
            item.Tag = tree;
            _generatedSourcesView.Items.Add(item);
        }

        if (_generatedSourcesView.Items.Count == 0)
        {
            _generatedSourcesView.Items.Add("(no generated sources)");
        }
    }

    private void GeneratedSourceSelected(object? sender, EventArgs e)
    {
        if (_generatedSourcesView.SelectedItems.Count == 0) return;
        var item = _generatedSourcesView.SelectedItems[0];
        if (item.Tag is SyntaxTree tree)
        {
            _generatedSourcePreview.Text = tree.GetText().ToString();
        }
    }

    private void CopyCurrentTab()
    {
        string text = _tabControl.SelectedIndex switch
        {
            0 => CopySyntaxTree(),
            1 => CopySemanticInfo(),
            2 => CopyDiagnostics(),
            3 => CopyGeneratedSources(),
            _ => "",
        };

        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            _statusLabel.Text = "Copied to clipboard.";
        }
    }

    private string CopySyntaxTree()
    {
        var sb = new StringBuilder();
        foreach (TreeNode node in _syntaxTreeView.Nodes)
            AppendTreeNode(sb, node, 0);
        return sb.ToString();
    }

    private static void AppendTreeNode(StringBuilder sb, TreeNode node, int depth)
    {
        sb.AppendLine(new string(' ', depth * 2) + node.Text);
        foreach (TreeNode child in node.Nodes)
            AppendTreeNode(sb, child, depth + 1);
    }

    private string CopySemanticInfo()
    {
        var sb = new StringBuilder();
        foreach (ListViewItem item in _semanticInfoView.Items)
            sb.AppendLine($"{item.Text}: {item.SubItems[1].Text}");
        return sb.ToString();
    }

    private string CopyDiagnostics()
    {
        var sb = new StringBuilder();
        foreach (DataGridViewRow row in _diagnosticsGrid.Rows)
        {
            if (row.Cells[0].Value is not null)
                sb.AppendLine($"[{row.Cells[0].Value}] {row.Cells[1].Value}: {row.Cells[2].Value}");
        }
        return sb.ToString();
    }

    private string CopyGeneratedSources()
    {
        var sb = new StringBuilder();
        foreach (ListViewItem item in _generatedSourcesView.Items)
        {
            sb.AppendLine($"=== {item.Text} ===");
            if (item.Tag is SyntaxTree tree)
                sb.AppendLine(tree.GetText().ToString());
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        _syntaxTreeView.BackColor = theme.EditorBackground;
        _syntaxTreeView.ForeColor = theme.Text;
        _semanticInfoView.BackColor = theme.Background;
        _semanticInfoView.ForeColor = theme.Text;
        _diagnosticsGrid.BackgroundColor = theme.Background;
        _diagnosticsGrid.DefaultCellStyle.BackColor = theme.Background;
        _diagnosticsGrid.DefaultCellStyle.ForeColor = theme.Text;
        _diagnosticsGrid.ColumnHeadersDefaultCellStyle.BackColor = theme.PanelBackground;
        _diagnosticsGrid.ColumnHeadersDefaultCellStyle.ForeColor = theme.Text;
        _diagnosticsGrid.GridColor = theme.Border;
        _generatedSourcePreview.BackColor = theme.EditorBackground;
        _generatedSourcePreview.ForeColor = theme.Text;

        foreach (TabPage page in _tabControl.TabPages)
        {
            page.BackColor = theme.PanelBackground;
            page.ForeColor = theme.Text;
        }

        Invalidate(true);
    }

    public event Action? OnRefreshRequested;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

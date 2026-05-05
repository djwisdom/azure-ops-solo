namespace MyCrownJewelApp.Pfpad.Debugger;

internal sealed class DebugVariablesPanel : Form
{
    private readonly TreeView _tree;
    private readonly DebugSession _session;
    private readonly TextBox _watchInput;
    private readonly List<string> _watchExpressions = new();
    private int _currentFrameId;

    public DebugVariablesPanel(DebugSession session)
    {
        _session = session;
        Text = "Variables";
        Size = new Size(350, 300);
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            HideSelection = false,
            Font = new Font("Consolas", 9.25f)
        };
        _tree.NodeMouseDoubleClick += async (s, e) =>
        {
            if (e.Node.Tag is int varRef && varRef > 0)
                await ExpandVariableNode(e.Node, varRef);
        };

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 26 };

        var addWatchBtn = new Button
        {
            Text = "+",
            Location = new Point(4, 2),
            Width = 24,
            Height = 22,
            FlatStyle = FlatStyle.Flat
        };

        _watchInput = new TextBox
        {
            Location = new Point(32, 2),
            Width = topPanel.Width - 40,
            Height = 22,
            Font = new Font("Consolas", 9)
        };
        _watchInput.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(_watchInput.Text))
            {
                _watchExpressions.Add(_watchInput.Text.Trim());
                _watchInput.Clear();
                RefreshWatchesAsync();
            }
        };

        topPanel.Controls.Add(addWatchBtn);
        topPanel.Controls.Add(_watchInput);
        Controls.Add(_tree);
        Controls.Add(topPanel);

        var header = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 24,
            BackColor = theme.TerminalHeaderBackground,
            Renderer = new FlatToolStripRenderer()
        };
        header.Items.Add(new ToolStripLabel("Locals") { Font = new Font("Segoe UI", 8.25f) });
        header.Items.Add(new ToolStripButton("Clear Watch", null!, (s, e) =>
        {
            _watchExpressions.Clear();
            RefreshWatchesAsync();
        }) { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right });

        Controls.Add(header);
        header.BringToFront();
    }

    public async Task RefreshAsync(int frameId)
    {
        _currentFrameId = frameId;
        RefreshWatchesAsync();

        var scopes = await _session.GetScopesAsync(frameId);
        if (scopes == null) return;

        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        foreach (var scope in scopes)
        {
            var scopeNode = new TreeNode(scope.Name) { NodeFont = new Font("Segoe UI", 9, FontStyle.Bold) };
            if (scope.VariablesReference > 0)
            {
                var vars = await _session.GetVariablesAsync(scope.VariablesReference);
                if (vars != null)
                {
                    foreach (var v in vars)
                    {
                        string display = $"{v.Name} = {v.Value}";
                        if (v.Type != null) display += $"  ({v.Type})";
                        var varNode = new TreeNode(display) { Tag = v.VariablesReference };
                        if (v.VariablesReference > 0)
                            varNode.Nodes.Add(new TreeNode("..."));
                        scopeNode.Nodes.Add(varNode);
                    }
                }
            }
            _tree.Nodes.Add(scopeNode);
            scopeNode.Expand();
        }

        _tree.EndUpdate();
    }

    private async Task RefreshWatchesAsync()
    {
        _tree.BeginUpdate();
        var watchRoot = _tree.Nodes.Count > 0 && _tree.Nodes[0].Text == "Watch"
            ? _tree.Nodes[0]
            : null;

        if (watchRoot == null)
        {
            watchRoot = new TreeNode("Watch") { NodeFont = new Font("Segoe UI", 9, FontStyle.Bold) };
            _tree.Nodes.Insert(0, watchRoot);
        }
        else
        {
            watchRoot.Nodes.Clear();
        }

        foreach (var expr in _watchExpressions)
        {
            string? val = await _session.EvaluateAsync(expr, _currentFrameId, "watch");
            string display = $"{expr} = {val ?? "<error>"}";
            watchRoot.Nodes.Add(new TreeNode(display));
        }

        watchRoot.Expand();
        _tree.EndUpdate();
    }

    private async Task ExpandVariableNode(TreeNode node, int varRef)
    {
        node.Nodes.Clear();
        var children = await _session.GetVariablesAsync(varRef);
        if (children == null) return;

        foreach (var child in children)
        {
            string display = $"{child.Name} = {child.Value}";
            if (child.Type != null) display += $"  ({child.Type})";
            var childNode = new TreeNode(display) { Tag = child.VariablesReference };
            if (child.VariablesReference > 0)
                childNode.Nodes.Add(new TreeNode("..."));
            node.Nodes.Add(childNode);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

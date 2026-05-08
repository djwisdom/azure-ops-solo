using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class OutlinePanel : UserControl
{
    private readonly TreeView _tree;
    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _headerLabel;
    private readonly ToolStripButton _refreshButton;
    private readonly ToolStripButton _closeButton;
    private string _currentText = "";
    private int _lastParseLineCount;

    public event Action<int>? SymbolClicked;

    public OutlinePanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        MinimumSize = new Size(100, 60);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            HotTracking = false,
            FullRowSelect = true,
            HideSelection = false,
            LabelEdit = false,
            Indent = 16,
            ItemHeight = 20,
            Font = new Font("Consolas", 9),
        };
        _tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
        _tree.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && _tree.SelectedNode?.Tag is int line)
            {
                SymbolClicked?.Invoke(line);
                e.Handled = true;
            }
        };

        _headerLabel = new ToolStripLabel("Outline")
        {
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            Margin = new Padding(4, 0, 0, 0)
        };

        _refreshButton = new ToolStripButton
        {
            Text = "\u21BB",
            Font = new Font("Segoe UI", 10),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Refresh Outline"
        };
        _refreshButton.Click += (_, _) => RefreshFromText(_currentText);

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
        _closeButton.Click += (_, _) => CloseRequested?.Invoke();

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(2, 0, 0, 0),
            AutoSize = false,
            Height = 24,
            Renderer = new FlatToolStripRenderer()
        };
        _headerStrip.Items.Add(_headerLabel);
        _headerStrip.Items.Add(_refreshButton);
        _headerStrip.Items.Add(_closeButton);

        Controls.Add(_headerStrip);
        Controls.Add(_tree);

        SetTheme(ThemeManager.Instance.CurrentTheme);
    }

        public event Action? CloseRequested;

    public void SetTheme(Theme theme)
    {
        BackColor = theme.MenuBackground;
        _tree.BackColor = theme.MenuBackground;
        _tree.ForeColor = theme.Text;
        _tree.LineColor = theme.Border;
        _headerStrip.BackColor = theme.TerminalHeaderBackground;
        _headerStrip.ForeColor = theme.Text;
        _headerLabel.ForeColor = theme.Muted;
        _closeButton.ForeColor = theme.Text;
        _refreshButton.ForeColor = theme.Text;
    }

    public void RefreshFromText(string text)
    {
        _currentText = text ?? "";
        _tree.BeginUpdate();
        _tree.Nodes.Clear();

        if (string.IsNullOrEmpty(_currentText))
        {
            _tree.EndUpdate();
            return;
        }

        var lines = _currentText.Split('\n');
        _lastParseLineCount = lines.Length;

        // Track hierarchy stack for nesting
        var stack = new Stack<TreeNode>();
        TreeNode? lastAdded = null;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            string trimmed = line.TrimStart();
            int indent = line.Length - trimmed.Length;

            // Skip empty, comment-only, and brace-only lines
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;
            if (trimmed is "{" or "}" or "};" or ");") continue;

            // Detect regions
            Match regionMatch = Regex.Match(trimmed, @"^#(region|endregion)\s*(.*)");
            if (regionMatch.Success)
            {
                bool isStart = regionMatch.Groups[1].Value == "region";
                string label = isStart ? $"\u25B6 {regionMatch.Groups[2].Value.Trim()}" : $"\u25C0 endregion";
                var node = new TreeNode(label) { Tag = i };
                InsertByIndent(node, indent, stack, ref lastAdded);
                continue;
            }

            // Detect C# members: namespace, class, struct, interface, enum, method, property, field, event
            Match m = Regex.Match(trimmed,
                @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|readonly|async|unsafe|extern|new)\s+)*" +
                @"(?:(?:class|struct|interface|enum|record|namespace)\s+(\w+)" +
                @"|(\w+)\s*\([^)]*\)\s*[{]" +
                @"|(\w+)\s*\{[^}]*\}" +
                @"|(?:void|int|string|bool|char|byte|long|float|double|decimal|var|\w+(?:<[^>]*>)?)\s+(\w+)\s*(?:[{=;]|$)" +
                @"|(?:event)\s+\w+\s+(\w+)\s*[{;])\s*");

            if (m.Success)
            {
                string name = m.Groups[1].Success ? m.Groups[1].Value
                    : m.Groups[2].Success ? $"{m.Groups[2].Value}()"
                    : m.Groups[3].Success ? $"{m.Groups[3].Value} {{ }}"
                    : m.Groups[4].Success ? m.Groups[4].Value
                    : m.Groups[5].Success ? $"event {m.Groups[5].Value}"
                    : trimmed;

                // Determine symbol type icon
                string icon = "";
                if (m.Groups[1].Success)
                {
                    string kw = m.Groups[1].Value;
                    icon = kw switch
                    {
                        "namespace" => "\uD83D\uDCC1", "class" => "\uD83D\uDCBB",
                        "struct" => "\uD83D\uDD17", "interface" => "\uD83D\uDD0C",
                        "enum" => "\uD83D\uDD22", "record" => "\uD83D\uDCC4",
                        _ => "\u25A0"
                    };
                }
                else if (trimmed.Contains('(')) icon = "\u2699";
                else if (trimmed.Contains('{')) icon = "\uD83D\uDCCC";
                else icon = "\u25CF";

                var node = new TreeNode($"{icon} {name}") { Tag = i };
                InsertByIndent(node, indent, stack, ref lastAdded);
            }
        }

        if (_tree.Nodes.Count > 0)
            _tree.Nodes[0].Expand();

        _tree.EndUpdate();
    }

    private static void InsertByIndent(TreeNode node, int indent, Stack<TreeNode> stack, ref TreeNode? lastAdded)
    {
        // Pop stack until we find a parent with less indent
        while (stack.Count > 0 && indent <= (stack.Peek().Tag is int ? 0 : 999))
        {
            // Use indent heuristic: pop if current indent is <= parent's approximate indent
            stack.Pop();
        }

        if (stack.Count > 0)
            stack.Peek().Nodes.Add(node);
        else if (lastAdded != null)
        {
            if (lastAdded.Parent != null)
                lastAdded.Parent.Nodes.Add(node);
            else
                lastAdded.Nodes.Add(node);
        }

        // Approximate indent tracking: use node level as indent proxy
        stack.Push(node);
        lastAdded = node;
    }

    private void Tree_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is int line)
        {
            SymbolClicked?.Invoke(line);
        }
    }
}

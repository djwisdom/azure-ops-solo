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

public sealed class CallHierarchyDialog : Form
{
    private readonly TreeView _tree;
    private readonly Label _statusLabel;
    private readonly string _symbol;
    private readonly string _workspaceRoot;
    private readonly string _currentFile;
    private CancellationTokenSource? _scanCts;

    public CallHierarchyDialog(string symbol, string workspaceRoot, string currentFile)
    {
        _symbol = symbol;
        _workspaceRoot = workspaceRoot;
        _currentFile = currentFile;

        Text = $"Call Hierarchy: {symbol}";
        Size = new Size(700, 450);
        MinimumSize = new Size(400, 250);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _statusLabel = new Label
        {
            Text = "Scanning...",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Muted
        };

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            LineColor = theme.Border,
            FullRowSelect = true,
            HideSelection = false,
            ShowLines = true,
            Font = new Font("Segoe UI", 9)
        };
        _tree.NodeMouseDoubleClick += (s, e) =>
        {
            if (e.Node?.Tag is string tag && tag.StartsWith("file://"))
            {
                var parts = tag[7..].Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int line))
                    OpenFileRequested?.Invoke(parts[0], line);
            }
        };

        var closeBtn = new Button
        {
            Text = "Close",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Bottom,
            Size = new Size(80, 28),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Muted },
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => Close();

        Controls.Add(_tree);
        Controls.Add(_statusLabel);
        Controls.Add(closeBtn);

        BeginInvoke(ScanAsync);
    }

    public event Action<string, int>? OpenFileRequested;

    private async void ScanAsync()
    {
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        try
        {
            var (outgoing, incoming) = await Task.Run(() => ScanCallHierarchy(token), token);

            _tree.BeginUpdate();
            _tree.Nodes.Clear();

            var theme = ThemeManager.Instance.CurrentTheme;
            var outgoingLabel = new Font("Segoe UI", 9, FontStyle.Bold);

            // Outgoing — what this calls
            var outNode = new TreeNode($"What '{_symbol}' Calls ({outgoing.Count})")
            {
                ForeColor = theme.Accent,
                NodeFont = outgoingLabel
            };
            foreach (var (name, file, line, context) in outgoing.Take(200))
            {
                var child = new TreeNode($"{name}  —  Ln {line}  {context}")
                {
                    Tag = $"file://{file}:{line}",
                    ForeColor = theme.Text
                };
                outNode.Nodes.Add(child);
            }
            if (outgoing.Count == 0)
                outNode.Nodes.Add(new TreeNode("(no outgoing calls found)") { ForeColor = theme.Muted });
            _tree.Nodes.Add(outNode);
            outNode.Expand();

            // Incoming — who calls this
            var inNode = new TreeNode($"Who Calls '{_symbol}' ({incoming.Count})")
            {
                ForeColor = theme.Accent,
                NodeFont = outgoingLabel
            };
            foreach (var (callerName, file, line, context) in incoming.Take(500))
            {
                var child = new TreeNode($"{callerName}  —  {Path.GetFileName(file)}:{line}  {context}")
                {
                    Tag = $"file://{file}:{line}",
                    ForeColor = theme.Text
                };
                inNode.Nodes.Add(child);
            }
            if (incoming.Count == 0)
                inNode.Nodes.Add(new TreeNode("(no callers found)") { ForeColor = theme.Muted });
            _tree.Nodes.Add(inNode);
            inNode.Expand();

            _tree.EndUpdate();
            _statusLabel.Text = $"Calls: {outgoing.Count} outgoing, {incoming.Count} incoming";

            if (outgoing.Count + incoming.Count == 0 && _tree.Nodes.Count > 0)
            {
                foreach (TreeNode n in _tree.Nodes)
                    n.Collapse();
            }
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private (List<(string name, string file, int line, string context)> outgoing,
             List<(string callerName, string file, int line, string context)> incoming)
        ScanCallHierarchy(CancellationToken token)
    {
        var outgoing = new List<(string name, string file, int line, string context)>();
        var incoming = new List<(string callerName, string file, int line, string context)>();

        // Find method body bounds in current file
        var methodBodyLines = FindMethodBody(_currentFile, _symbol);

        // Outgoing: scan method body for calls
        if (methodBodyLines != null)
        {
            var callPattern = new Regex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
            foreach (int lineIdx in methodBodyLines)
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    string line = File.ReadLines(_currentFile).Skip(lineIdx).FirstOrDefault() ?? "";
                    var matches = callPattern.Matches(line);
                    foreach (Match m in matches)
                    {
                        string callName = m.Groups[1].Value;
                        if (IsKeyword(callName)) continue;
                        if (callName == _symbol) continue;
                        if (!outgoing.Any(o => o.name == callName && o.line == lineIdx + 1))
                            outgoing.Add((callName, _currentFile, lineIdx + 1, line.Trim()));
                    }
                }
                catch { }
            }
        }

        if (token.IsCancellationRequested) return (outgoing, incoming);

        // Incoming: scan workspace for calls to this symbol
        if (!string.IsNullOrEmpty(_workspaceRoot) && Directory.Exists(_workspaceRoot))
        {
            var pattern = $@"\b{Regex.Escape(_symbol)}\s*\(";
            var regex = new Regex(pattern, RegexOptions.Compiled);

            try
            {
                var files = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string[] textExts = { ".cs", ".vb", ".ts", ".js", ".jsx", ".tsx", ".py", ".java", ".go", ".rs", ".cpp", ".c", ".h", ".hpp" };
                    if (!textExts.Contains(ext)) continue;
                    if (file.Contains("\\node_modules\\") || file.Contains("\\.git\\") || file.Contains("\\bin\\") || file.Contains("\\obj\\"))
                        continue;

                    try
                    {
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var match = regex.Match(lines[i]);
                            if (!match.Success) continue;
                            if (lines[i].TrimStart().StartsWith("//") || lines[i].TrimStart().StartsWith('*')) continue;

                            // Skip self
                            if (file == _currentFile && i + 1 >= (methodBodyLines?.Min() ?? 0) && i + 1 <= (methodBodyLines?.Max() ?? 0))
                                continue;

                            // Try to find the enclosing method name
                            string callerName = FindEnclosingMethod(lines, i);
                            incoming.Add((callerName, file, i + 1, lines[i].Trim()));
                            if (incoming.Count >= 500) break;
                        }
                    }
                    catch { }

                    if (incoming.Count >= 500) break;
                }
            }
            catch { }
        }

        return (outgoing, incoming);
    }

    private static List<int>? FindMethodBody(string file, string methodName)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var lines = File.ReadAllLines(file);
            var declPattern = new Regex($@"\b(void|int|string|bool|double|float|long|char|byte|short|uint|ulong|ushort|sbyte|decimal|var|Task|ValueTask|IEnumerable|IEnumerator|async|static|private|public|internal|protected|override|virtual|abstract|sealed|partial|readonly)\s+\b{Regex.Escape(methodName)}\s*\(");
            int? bodyStart = null;
            int? bodyEnd = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (declPattern.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//"))
                {
                    // Find opening brace
                    for (int j = i; j < lines.Length; j++)
                    {
                        int braceIdx = lines[j].IndexOf('{');
                        if (braceIdx >= 0 && !lines[j].TrimStart().StartsWith("//"))
                        {
                            bodyStart = j;
                            break;
                        }
                    }
                    if (bodyStart.HasValue) break;
                }
            }

            if (bodyStart == null) return null;

            // Match braces to find end
            int depth = 0;
            for (int i = bodyStart.Value; i < lines.Length; i++)
            {
                string line = lines[i];
                foreach (char c in line)
                {
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                }
                if (depth == 0)
                {
                    bodyEnd = i;
                    break;
                }
            }

            if (bodyEnd == null) return null;
            return Enumerable.Range(bodyStart.Value, bodyEnd.Value - bodyStart.Value + 1).ToList();
        }
        catch { return null; }
    }

    private static string FindEnclosingMethod(string[] lines, int lineIndex)
    {
        var methodPattern = new Regex(@"\b(void|int|string|bool|double|float|long|char|byte|short|var|Task|async)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(");
        int depth = 0;
        string? lastMethod = null;

        for (int i = lineIndex; i >= 0; i--)
        {
            string line = lines[i];

            // Adjust brace depth
            foreach (char c in line) { if (c == '}') depth++; }

            var match = methodPattern.Match(line);
            if (match.Success && depth >= 0 && !line.TrimStart().StartsWith("//"))
                lastMethod = match.Groups[2].Value;

            foreach (char c in line) { if (c == '{') depth--; }
        }
        return lastMethod ?? "(global scope)";
    }

    private static bool IsKeyword(string word)
    {
        return word is "if" or "for" or "while" or "foreach" or "switch" or "catch" or "using"
            or "return" or "throw" or "new" or "sizeof" or "typeof" or "nameof" or "base" or "this"
            or "await" or "yield" or "lock" or "fixed" or "stackalloc" or "delegate" or "default"
            or "when" or "where" or "select" or "from" or "in" or "join" or "let" or "orderby"
            or "group" or "into" or "as" or "is" or "not" or "and" or "or" or "var" or "out"
            or "ref" or "in" or "params" or "checked" or "unchecked" or "unsafe" or "sizeof"
            or "GetHashCode" or "Equals" or "ToString" or "GetType";
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

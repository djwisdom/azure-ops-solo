using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class SnippetEngine
{
    private readonly Dictionary<string, Snippet> _snippets = new(StringComparer.OrdinalIgnoreCase);
    private readonly RichTextBox _editor;

    public sealed record Snippet(string Prefix, string Description, string Body);

    public SnippetEngine(RichTextBox editor)
    {
        _editor = editor;
        RegisterBuiltIn();
    }

    private void RegisterBuiltIn()
    {
        // C# snippets
        Add("for", "for loop", "for (int i = 0; i < $0; i++)\r\n{\r\n}");
        Add("foreach", "foreach loop", "foreach (var item in $0)\r\n{\r\n}");
        Add("while", "while loop", "while ($0)\r\n{\r\n}");
        Add("do", "do-while loop", "do\r\n{\r\n$0\r\n} while ($1);");
        Add("try", "try-catch", "try\r\n{\r\n$0\r\n}\r\ncatch (Exception ex)\r\n{\r\n}");
        Add("tryf", "try-finally", "try\r\n{\r\n$0\r\n}\r\nfinally\r\n{\r\n}");
        Add("if", "if statement", "if ($0)\r\n{\r\n}");
        Add("ife", "if-else", "if ($0)\r\n{\r\n}\r\nelse\r\n{\r\n}");
        Add("else", "else statement", "else\r\n{\r\n$0\r\n}");
        Add("switch", "switch statement", "switch ($0)\r\n{\r\ncase $1:\r\nbreak;\r\ndefault:\r\nbreak;\r\n}");
        Add("class", "class declaration", "public class $0\r\n{\r\n}");
        Add("struct", "struct declaration", "public struct $0\r\n{\r\n}");
        Add("interface", "interface declaration", "public interface I$0\r\n{\r\n}");
        Add("enum", "enum declaration", "public enum $0\r\n{\r\n}");
        Add("prop", "auto-property", "public $0 { get; set; }");
        Add("propg", "auto-property (private set)", "public $0 { get; private set; }");
        Add("propfull", "full property with backing field", "private $1 _$0;\r\npublic $1 $0\r\n{\r\n    get => _$0;\r\n    set => _$0 = value;\r\n}");
        Add("ctor", "constructor", "public $0()\r\n{\r\n}");
        Add("main", "Main method", "static void Main(string[] args)\r\n{\r\n$0\r\n}");
        Add("console", "Console.WriteLine", "Console.WriteLine($0);");
        Add("cw", "Console.WriteLine (short)", "Console.WriteLine($0);");

        // General snippets
        Add("todo", "TODO comment", "// TODO: $0");
        Add("hack", "HACK comment", "// HACK: $0");
        Add("note", "NOTE comment", "// NOTE: $0");
    }

    public void Add(string prefix, string description, string body)
    {
        _snippets[prefix] = new Snippet(prefix, description, body);
    }

    public bool TryExpand()
    {
        int pos = _editor.SelectionStart;
        if (pos <= 0) return false;

        string text = _editor.Text;

        // Walk backward from cursor to find start of word
        int start = pos - 1;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
            start--;
        start++;

        if (start >= pos) return false;

        string prefix = text[start..pos];
        if (string.IsNullOrEmpty(prefix)) return false;

        if (!_snippets.TryGetValue(prefix, out var snippet)) return false;

        // Replace prefix with snippet body
        _editor.Select(start, pos - start);
        _editor.SelectedText = snippet.Body;

        // Find and position at first tab-stop ($0)
        int bodyIndex = snippet.Body.IndexOf("$0", StringComparison.Ordinal);
        if (bodyIndex >= 0)
        {
            _editor.SelectionStart = start + bodyIndex;
            _editor.SelectionLength = 0;
        }

        // Track tab-stops for subsequent Tab presses
        _tabStops.Clear();
        int stopIndex = 0;
        while (true)
        {
            int idx = snippet.Body.IndexOf($"${stopIndex}", StringComparison.Ordinal);
            if (idx < 0) break;
            _tabStops.Add((start + idx, stopIndex == 0));
            stopIndex++;
        }
        _currentTabStop = 0;

        return true;
    }

    private readonly List<(int Position, bool IsFinal)> _tabStops = new();
    private int _currentTabStop;

    public bool TryNavigateNextTabStop()
    {
        if (_tabStops.Count == 0) return false;

        // Advance to next non-final tab stop
        for (int i = _currentTabStop + 1; i < _tabStops.Count; i++)
        {
            if (!_tabStops[i].IsFinal)
            {
                _currentTabStop = i;
                _editor.SelectionStart = _tabStops[i].Position;
                _editor.SelectionLength = 0;
                _editor.ScrollToCaret();
                return true;
            }
        }

        // No more tab-stops — place cursor at end of inserted text
        _editor.SelectionStart = _editor.SelectionStart;
        return false;
    }

    public void ClearTabStops()
    {
        _tabStops.Clear();
        _currentTabStop = 0;
    }
}

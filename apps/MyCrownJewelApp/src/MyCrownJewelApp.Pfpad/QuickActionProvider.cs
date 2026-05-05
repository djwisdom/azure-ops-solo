using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public sealed class QuickActionProvider
{
    private readonly SymbolIndexService _symbolIndex;

    public QuickActionProvider(SymbolIndexService symbolIndex)
    {
        _symbolIndex = symbolIndex;
    }

    public List<QuickAction> GetActions(string text, IReadOnlyList<Diagnostic> diagnostics)
    {
        var actions = new List<QuickAction>();
        var lines = text.Split('\n');

        foreach (var d in diagnostics)
        {
            if (d.Line < 1 || d.Line > lines.Length) continue;

            switch (d.RuleId)
            {
                case "PFP001": // Trailing whitespace
                    actions.Add(new QuickAction
                    {
                        Title = "Remove trailing whitespace",
                        DiagnosticRuleId = d.RuleId,
                        Line = d.Line,
                        Apply = (txt) =>
                        {
                            var ls = txt.Split('\n');
                            int idx = d.Line - 1;
                            if (idx < ls.Length)
                                ls[idx] = ls[idx].TrimEnd('\r').TrimEnd() + (ls[idx].EndsWith('\r') ? "\r" : "");
                            return string.Join("\n", ls);
                        }
                    });
                    break;

                case "PFP004": // Missing semicolon
                    actions.Add(new QuickAction
                    {
                        Title = "Insert semicolon",
                        DiagnosticRuleId = d.RuleId,
                        Line = d.Line,
                        Apply = (txt) =>
                        {
                            var ls = txt.Split('\n');
                            int idx = d.Line - 1;
                            if (idx < ls.Length)
                            {
                                string l = ls[idx].TrimEnd('\r', '\n');
                                if (!l.EndsWith(';'))
                                    ls[idx] = l + ";" + (ls[idx].EndsWith('\r') ? "\r" : "");
                            }
                            return string.Join("\n", ls);
                        }
                    });
                    break;
            }
        }

        // Add "Add missing using" suggestions based on unresolved type names
        AddMissingUsingActions(text, lines, actions);

        return actions;
    }

    private void AddMissingUsingActions(string text, string[] lines, List<QuickAction> actions)
    {
        if (!_symbolIndex.HasIndex) return;
        if (!text.Contains("using ")) return;

        var existingUsings = new HashSet<string>(
            lines
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("using "))
                .Select(l =>
                {
                    var m = Regex.Match(l, @"using\s+([^;]+);");
                    return m.Success ? m.Groups[1].Value.Trim() : "";
                })
                .Where(n => n.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var usedTypes = new HashSet<string>();
        foreach (var line in lines)
        {
            var matches = Regex.Matches(line, @"\b([A-Z][a-zA-Z0-9]+)\b");
            foreach (Match m in matches)
            {
                string name = m.Value;
                if (name.Length < 2) continue;
                if (name == "I" || name == "ID") continue;
                if (Regex.IsMatch(name, @"^(If|For|While|Switch|Case|Return|Using|New|Var|Null|True|False|This|Base|String|Int|Bool|Double|Float|Long|Char|Byte|Short|Object|Void|Decimal|Uint|Ulong|Ushort|Sbyte|Class|Struct|Interface|Enum|Record|Namespace|Public|Private|Internal|Protected|Static|Readonly|Const|Virtual|Override|Abstract|Sealed|Async|Await|Partial|Get|Set|Value|Var)$")) continue;
                usedTypes.Add(name);
            }
        }

        foreach (var typeName in usedTypes)
        {
            if (existingUsings.Any(u => u.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase)
                || u.Equals(typeName, StringComparison.OrdinalIgnoreCase)))
                continue;

            var symbols = _symbolIndex.Lookup(typeName);
            if (symbols.Count == 0) continue;

            string? ns = InferNamespace(symbols[0].File);
            if (ns is null || existingUsings.Contains(ns)) continue;

            // Find a line where this type is used
            int lineNum = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(typeName))
                {
                    lineNum = i + 1;
                    break;
                }
            }

            actions.Add(new QuickAction
            {
                Title = $"using {ns}",
                DiagnosticRuleId = "using",
                Line = lineNum,
                Apply = (txt) =>
                {
                    var ls = txt.Split('\n');
                    // Insert after last existing using, before first non-using line
                    int insertIdx = -1;
                    for (int i = 0; i < ls.Length; i++)
                    {
                        if (ls[i].TrimStart().StartsWith("using "))
                            insertIdx = i + 1;
                        else if (insertIdx >= 0)
                            break;
                    }
                    if (insertIdx < 0) insertIdx = 0;
                    var newLines = ls.ToList();
                    newLines.Insert(insertIdx, $"using {ns};");
                    return string.Join("\n", newLines);
                }
            });
        }
    }

    private static string? InferNamespace(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath)) return null;
            var lines = System.IO.File.ReadAllLines(filePath);
            foreach (var l in lines)
            {
                var m = Regex.Match(l.Trim(), @"^namespace\s+([^;{]+)");
                if (m.Success) return m.Groups[1].Value.Trim();
            }
        }
        catch { }
        return null;
    }
}

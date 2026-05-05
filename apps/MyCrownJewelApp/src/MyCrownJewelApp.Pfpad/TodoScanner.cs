using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public static class TodoScanner
{
    private static readonly Regex TodoPattern = new(
        @"//\s*(TODO|FIXME|HACK|XXX|BUG|OPTIMIZE|REVIEW|NOTE|WORKAROUND|TEMP|UNDONE)\b[:\s]*(.*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BlockTodoPattern = new(
        @"\*\s*(TODO|FIXME|HACK|XXX|BUG|OPTIMIZE|REVIEW|NOTE|WORKAROUND|TEMP|UNDONE)\b[:\s]*(.*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<Diagnostic> ScanFile(string filePath)
    {
        var results = new List<Diagnostic>();
        if (!File.Exists(filePath)) return results;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        string[] textExts = { ".cs", ".vb", ".ts", ".tsx", ".js", ".jsx", ".py", ".rb", ".java", ".kt", ".go", ".rs", ".cpp", ".c", ".h", ".hpp", ".swift", ".php", ".lua", ".r", ".m", ".mm", ".pl", ".pm" };
        if (!textExts.Contains(ext)) return results;

        try
        {
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                var m = TodoPattern.Match(line);
                if (!m.Success)
                    m = BlockTodoPattern.Match(line);

                if (m.Success)
                {
                    string tag = m.Groups[1].Value.ToUpperInvariant();
                    string message = m.Groups[2].Value.Trim();

                    DiagnosticSeverity severity = tag switch
                    {
                        "FIXME" or "BUG" or "XXX" => DiagnosticSeverity.Warning,
                        "TODO" => DiagnosticSeverity.Suggestion,
                        _ => DiagnosticSeverity.Hint
                    };

                    int col = line.IndexOf("//") >= 0 ? line.IndexOf("//") + 1 : m.Index + 1;
                    if (col < 1) col = 1;

                    results.Add(new Diagnostic
                    {
                        File = filePath,
                        Line = i + 1,
                        Column = col,
                        Length = Math.Max(1, line.Length - col + 1),
                        Message = $"[{tag}] {message}",
                        Severity = severity,
                        RuleId = "TODO"
                    });
                }
            }
        }
        catch { }

        return results;
    }

    public static List<Diagnostic> ScanWorkspace(string workspaceRoot)
    {
        var results = new List<Diagnostic>();
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return results;

        try
        {
            var files = Directory.EnumerateFiles(workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\node_modules\\") && !f.Contains("\\.git\\")
                    && !f.Contains("\\bin\\") && !f.Contains("\\obj\\")
                    && !f.Contains("\\packages\\"));

            foreach (var file in files)
            {
                results.AddRange(ScanFile(file));
            }
        }
        catch { }

        return results;
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public static class StackTraceParser
{
    // .NET: at Namespace.Class.Method() in C:\path\to\file.cs:line 42
    private static readonly Regex DotNetFormat = new(
        @"\s+at\s+.+?\sin\s+([^\s:]+):line\s+(\d+)",
        RegexOptions.Compiled);

    // JS/TS: at functionName (file.ts:42:10)  or  at file.ts:42:10
    private static readonly Regex JsFormat = new(
        @"\s+at\s+(?:.+?\s+\()?([^\s(:]+):(\d+)(?::\d+)?\)?",
        RegexOptions.Compiled);

    // Python: File "path/to/file.py", line 42, in functionName
    private static readonly Regex PythonFormat = new(
        @"File\s+""([^""]+)"",\s+line\s+(\d+)",
        RegexOptions.Compiled);

    // Generic: path.ext:line
    private static readonly Regex GenericFormat = new(
        @"\b([A-Za-z]:\\[^\s:]+\.\w+):(\d+)\b",
        RegexOptions.Compiled);

    public static List<(string file, int line, string method)> Parse(string text)
    {
        var results = new List<(string file, int line, string method)>();

        foreach (Match m in DotNetFormat.Matches(text))
        {
            string file = m.Groups[1].Value;
            int line = int.TryParse(m.Groups[2].Value, out int l) ? l : 0;
            string method = ExtractMethodNameDotNet(m.Value);
            if (!results.Exists(r => r.file == file && r.line == line))
                results.Add((file, line, method));
        }

        foreach (Match m in JsFormat.Matches(text))
        {
            string file = m.Groups[1].Value;
            if (!System.IO.File.Exists(file) && !System.IO.Path.IsPathRooted(file))
            {
                // Might be a relative path or URL — skip unless it ends with .cs/.ts/etc
                string ext = System.IO.Path.GetExtension(file)?.ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(ext)) continue;
            }
            int line = int.TryParse(m.Groups[2].Value, out int l) ? l : 0;
            if (!results.Exists(r => r.file == file && r.line == line))
                results.Add((file, line, ""));
        }

        foreach (Match m in PythonFormat.Matches(text))
        {
            string file = m.Groups[1].Value;
            int line = int.TryParse(m.Groups[2].Value, out int l) ? l : 0;
            if (!results.Exists(r => r.file == file && r.line == line))
                results.Add((file, line, ""));
        }

        foreach (Match m in GenericFormat.Matches(text))
        {
            string file = m.Groups[1].Value;
            int line = int.TryParse(m.Groups[2].Value, out int l) ? l : 0;
            if (!results.Exists(r => r.file == file && r.line == line))
                results.Add((file, line, ""));
        }

        return results;
    }

    private static string ExtractMethodNameDotNet(string line)
    {
        // "at Namespace.Class.Method() in ..." → extract Method
        var m = Regex.Match(line, @"at\s+(?:.*\.)?(\w+)\s*\(");
        return m.Success ? m.Groups[1].Value : "";
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public static class XmlDocParser
{
    private static readonly Regex DocLine = new(@"^\s*///\s?(.*)", RegexOptions.Compiled);
    private static readonly Regex SummaryTag = new(@"<summary>\s*(.*?)\s*</summary>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ParamTag = new(@"<param\s+name=""([^""]+)"">\s*(.*?)\s*</param>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ReturnsTag = new(@"<returns>\s*(.*?)\s*</returns>", RegexOptions.Singleline | RegexOptions.Compiled);

    public sealed record DocComment
    {
        public string Summary { get; init; } = "";
        public List<(string name, string text)> Params { get; init; } = new();
        public string Returns { get; init; } = "";
    }

    public static DocComment? ExtractFromFile(string filePath, int declarationLine)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var lines = File.ReadAllLines(filePath);
            if (declarationLine < 1 || declarationLine > lines.Length) return null;

            var docLines = new List<string>();
            for (int i = declarationLine - 2; i >= 0; i--)
            {
                string trimmed = lines[i].Trim();
                var m = DocLine.Match(trimmed);
                if (m.Success)
                    docLines.Insert(0, m.Groups[1].Value);
                else if (string.IsNullOrWhiteSpace(trimmed))
                    continue; // skip blank lines between doc and declaration
                else
                    break;
            }

            if (docLines.Count == 0) return null;

            string joined = string.Join("\n", docLines);

            var summaryMatch = SummaryTag.Match(joined);
            var returnsMatch = ReturnsTag.Match(joined);
            var paramMatches = ParamTag.Matches(joined);

            return new DocComment
            {
                Summary = summaryMatch.Success ? summaryMatch.Groups[1].Value.Trim() : joined,
                Returns = returnsMatch.Success ? returnsMatch.Groups[1].Value.Trim() : "",
                Params = paramMatches.Select(m => (m.Groups[1].Value, m.Groups[2].Value.Trim())).ToList()
            };
        }
        catch { return null; }
    }

    public static (string signature, List<string> paramNames)? ParseSignature(string filePath, string methodName)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var lines = File.ReadAllLines(filePath);
            var declPattern = new Regex($@"\b{Regex.Escape(methodName)}\s*\(");
            int? braceLine = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("//")) continue;
                if (declPattern.IsMatch(lines[i]))
                {
                    // Collect full signature (may span multiple lines)
                    var sig = new System.Text.StringBuilder();
                    int depth = 0;
                    bool inSig = false;
                    for (int j = i; j < lines.Length; j++)
                    {
                        string line = lines[j];
                        foreach (char c in line)
                        {
                            sig.Append(c);
                            if (c == '(') { depth++; inSig = true; }
                            else if (c == ')') { depth--; }
                        }
                        if (inSig && depth == 0)
                        {
                            braceLine = j;
                            break;
                        }
                        sig.Append(' ');
                    }
                    if (braceLine.HasValue)
                    {
                        string fullSig = sig.ToString().Trim();
                        return ParseParameterNames(fullSig);
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static (string signature, List<string> paramNames) ParseParameterNames(string sig)
    {
        var paramNames = new List<string>();
        // Extract parameter section between ( and )
        int parenOpen = sig.IndexOf('(');
        int parenClose = sig.LastIndexOf(')');
        if (parenOpen < 0 || parenClose < 0 || parenClose <= parenOpen)
            return (sig, paramNames);

        string paramsSection = sig.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
        if (string.IsNullOrEmpty(paramsSection))
            return (sig, paramNames);

        // Split by comma, extract name (last word of each param)
        var paramParts = paramsSection.Split(',');
        foreach (var part in paramParts)
        {
            string trimmed = part.Trim();
            var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Last word that isn't a keyword/default value is the parameter name
            for (int i = words.Length - 1; i >= 0; i--)
            {
                string w = words[i].TrimEnd('=', ' ');
                if (!IsKeyword(w) && w.Length > 0)
                {
                    paramNames.Add(w);
                    break;
                }
                // Fallback: use the last word regardless
                if (i == 0) paramNames.Add(words[^1].TrimEnd('=', ' '));
            }
        }

        return (sig, paramNames);
    }

    private static bool IsKeyword(string w) => w is "int" or "string" or "bool" or "double" or "float" or "long"
        or "char" or "byte" or "short" or "void" or "var" or "object" or "decimal" or "uint" or "ulong"
        or "ushort" or "sbyte" or "Task" or "ValueTask" or "IEnumerable" or "IEnumerator" or "List"
        or "Dictionary" or "ref" or "out" or "in" or "params" or "this" or "async" or "await"
        or "readonly" or "static" or "public" or "private" or "internal" or "protected" or "override"
        or "virtual" or "abstract" or "sealed" or "partial" or "class" or "struct" or "interface"
        or "enum" or "record" or "new" or "return" or "throw";
}

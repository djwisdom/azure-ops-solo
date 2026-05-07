using System.Collections.Generic;

namespace MyCrownJewelApp.Pfpad;

public sealed class StickyScope
{
    public int OpenLine { get; init; }
    public int CloseLine { get; init; }
    public string HeaderText { get; init; } = "";
    public string ScopeKind { get; init; } = "Block";
}

public sealed class StickyScrollService
{
    private readonly List<StickyScope> _scopes = new();

    public IReadOnlyList<StickyScope> Scopes => _scopes;

    public void Rebuild(string text, IEnumerable<FoldingManager.FoldRegion> regions)
    {
        _scopes.Clear();
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Split('\n');
        var regionList = new List<FoldingManager.FoldRegion>(regions);

        foreach (var r in regionList)
        {
            if (r.OpenLine >= lines.Length) continue;
            if (r.CloseLine - r.OpenLine < 1) continue;

            string openLineText = lines[r.OpenLine].TrimEnd('\r');
            string headerText;
            string kind;

            if (IsMeaningfulScope(openLineText, out headerText, out kind))
            {
                _scopes.Add(new StickyScope
                {
                    OpenLine = r.OpenLine,
                    CloseLine = r.CloseLine,
                    HeaderText = headerText,
                    ScopeKind = kind
                });
            }
        }

        _scopes.Sort((a, b) => a.OpenLine.CompareTo(b.OpenLine));
    }

    public List<StickyScope> GetEnclosingScopes(int lineNumber)
    {
        var result = new List<StickyScope>();
        foreach (var s in _scopes)
        {
            if (lineNumber >= s.OpenLine && lineNumber <= s.CloseLine)
                result.Add(s);
        }
        return result;
    }

    private static bool IsMeaningfulScope(string openLineText, out string headerText, out string kind)
    {
        headerText = "";
        kind = "Block";
        string trimmed = openLineText.Trim();

        if (trimmed.StartsWith("#region"))
        {
            headerText = trimmed;
            kind = "Region";
            return true;
        }

        int braceIdx = trimmed.IndexOf('{');
        if (braceIdx < 0) return false;

        string declPart = braceIdx > 0 ? trimmed[..braceIdx].Trim() : "";

        if (declPart.Length == 0) return false;

        if (ContainsKeyword(declPart, "class "))
        {
            headerText = ExtractHeader(declPart, "class");
            kind = "Class";
            return true;
        }
        if (ContainsKeyword(declPart, "struct "))
        {
            headerText = ExtractHeader(declPart, "struct");
            kind = "Struct";
            return true;
        }
        if (ContainsKeyword(declPart, "interface "))
        {
            headerText = ExtractHeader(declPart, "interface");
            kind = "Interface";
            return true;
        }
        if (ContainsKeyword(declPart, "enum "))
        {
            headerText = ExtractHeader(declPart, "enum");
            kind = "Enum";
            return true;
        }
        if (ContainsKeyword(declPart, "record "))
        {
            headerText = ExtractHeader(declPart, "record");
            kind = "Record";
            return true;
        }
        if (ContainsKeyword(declPart, "namespace "))
        {
            headerText = ExtractHeader(declPart, "namespace");
            kind = "Namespace";
            return true;
        }

        if (declPart.Contains('(') && declPart.Contains(')'))
        {
            headerText = declPart;
            if (declPart.Contains('=') && declPart.Contains('>'))
                kind = "Lambda";
            else
                kind = "Method";
            return true;
        }

        return false;
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        int idx = text.IndexOf(keyword, System.StringComparison.Ordinal);
        if (idx < 0) return false;
        if (idx > 0 && !char.IsWhiteSpace(text[idx - 1])) return false;
        int end = idx + keyword.Length;
        if (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '{' && text[end] != '<') return false;
        return true;
    }

    private static string ExtractHeader(string text, string keyword)
    {
        int idx = text.IndexOf(keyword, System.StringComparison.Ordinal);
        if (idx < 0) return text;
        return text[idx..].Trim();
    }
}

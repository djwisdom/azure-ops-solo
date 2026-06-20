using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public sealed class StickyScope
{
    public int OpenLine { get; init; }
    public int CloseLine { get; init; }
    public string HeaderText { get; init; } = "";
    /// <summary>Clean identifier name for breadcrumb display (e.g. "ByteSpan" not "public void ByteSpan(string data)").</summary>
    public string SymbolName { get; init; } = "";
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

            if (IsMeaningfulScope(openLineText, out string headerText, out string symbolName, out string kind))
            {
                _scopes.Add(new StickyScope
                {
                    OpenLine   = r.OpenLine,
                    CloseLine  = r.CloseLine,
                    HeaderText = headerText,
                    SymbolName = symbolName,
                    ScopeKind  = kind
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

    private static bool IsMeaningfulScope(string openLineText, out string headerText, out string symbolName, out string kind)
    {
        headerText  = "";
        symbolName  = "";
        kind        = "Block";
        string trimmed = openLineText.Trim();

        // ── #region ──────────────────────────────────────────────────────────
        if (trimmed.StartsWith("#region"))
        {
            string label = trimmed.Length > 7 ? trimmed[7..].Trim() : "#region";
            headerText = trimmed;
            symbolName = string.IsNullOrEmpty(label) ? "#region" : label;
            kind = "Region";
            return true;
        }

        int braceIdx = trimmed.IndexOf('{');
        if (braceIdx < 0) return false;

        string declPart = braceIdx > 0 ? trimmed[..braceIdx].Trim() : "";
        if (declPart.Length == 0) return false;

        // ── Named type keywords (C# + JS/TS) ─────────────────────────────────
        string[][] typeKeywords = [
            ["class",     "Class"],
            ["struct",    "Struct"],
            ["interface", "Interface"],
            ["enum",      "Enum"],
            ["record",    "Record"],
            ["namespace", "Namespace"],
        ];
        foreach (var kw in typeKeywords)
        {
            if (ContainsKeyword(declPart, kw[0] + " "))
            {
                headerText = ExtractHeader(declPart, kw[0]);
                symbolName = ExtractIdentifier(declPart, kw[0]);
                kind = kw[1];
                return true;
            }
        }

        // ── JS: named function declaration — function foo( ────────────────────
        var namedFuncM = Regex.Match(declPart, @"\bfunction\s+(\w+)\s*\(");
        if (namedFuncM.Success)
        {
            symbolName  = namedFuncM.Groups[1].Value;
            headerText  = symbolName + "()";
            kind        = "Method";
            return true;
        }

        // ── JS: assignment to function — const/let/var foo = function( ────────
        var anonAssignM = Regex.Match(declPart, @"\b(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?function\s*\(");
        if (anonAssignM.Success)
        {
            symbolName  = anonAssignM.Groups[1].Value;
            headerText  = symbolName + "()";
            kind        = "Method";
            return true;
        }

        // ── constructor ───────────────────────────────────────────────────────
        if (Regex.IsMatch(declPart, @"(?:^|[\s;])constructor\s*\("))
        {
            symbolName  = "constructor";
            headerText  = "constructor";
            kind        = "Constructor";
            return true;
        }

        // ── Arrow function: const foo = (...) => ─────────────────────────────
        if (declPart.Contains('(') && declPart.Contains(')') &&
            declPart.Contains('=') && declPart.Contains('>'))
        {
            var lambdaM = Regex.Match(declPart, @"\b(?:const|let|var)?\s*(\w+)\s*=");
            symbolName  = lambdaM.Success ? lambdaM.Groups[1].Value : "⟨function⟩";
            headerText  = declPart;
            kind        = "Lambda";
            return true;
        }

        // ── Generic method/function with ( ) ─────────────────────────────────
        if (declPart.Contains('(') && declPart.Contains(')'))
        {
            // Extract last \w+ before the first (
            int parenIdx = declPart.IndexOf('(');
            string beforeParen = declPart[..parenIdx];
            var methodM = Regex.Match(beforeParen, @"(\w+)\s*$");
            if (methodM.Success)
            {
                symbolName = methodM.Groups[1].Value + "()";
                headerText = declPart;
                kind       = "Method";
                return true;
            }
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

    /// <summary>Returns just the identifier immediately following the keyword (stops at space, &lt;, (, {, :, =).</summary>
    private static string ExtractIdentifier(string text, string keyword)
    {
        int idx = text.IndexOf(keyword, System.StringComparison.Ordinal);
        if (idx < 0) return text;
        string after = text[(idx + keyword.Length)..].TrimStart();
        var m = Regex.Match(after, @"^(\w+)");
        return m.Success ? m.Groups[1].Value : after;
    }
}

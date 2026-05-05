using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public abstract class LintRule
{
    public abstract string Id { get; }
    public abstract string Description { get; }
    public abstract DiagnosticSeverity DefaultSeverity { get; }

    public abstract void Analyze(string text, string filePath, List<Diagnostic> diagnostics);
}

public sealed class TrailingWhitespaceRule : LintRule
{
    public override string Id => "PFP001";
    public override string Description => "Trailing whitespace";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Hint;

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            int line = i;
            string l = lines[i].TrimEnd('\r');
            int wsStart = -1;
            for (int j = l.Length - 1; j >= 0; j--)
            {
                if (l[j] == ' ' || l[j] == '\t')
                    wsStart = j;
                else
                    break;
            }
            if (wsStart >= 0)
            {
                diagnostics.Add(new Diagnostic
                {
                    File = filePath,
                    Line = line + 1,
                    Column = wsStart + 1,
                    Length = l.Length - wsStart,
                    Message = "Trailing whitespace",
                    Severity = DefaultSeverity,
                    RuleId = Id
                });
            }
        }
    }
}

public sealed class LineTooLongRule : LintRule
{
    private readonly int _maxLength;
    public override string Id => "PFP002";
    public override string Description => $"Line exceeds {_maxLength} characters";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Suggestion;

    public LineTooLongRule(int maxLength = 120)
    {
        _maxLength = maxLength;
    }

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r');
            if (l.Length > _maxLength)
            {
                diagnostics.Add(new Diagnostic
                {
                    File = filePath,
                    Line = i + 1,
                    Column = _maxLength + 1,
                    Length = l.Length - _maxLength,
                    Message = $"Line too long ({l.Length} chars, max {_maxLength})",
                    Severity = DefaultSeverity,
                    RuleId = Id
                });
            }
        }
    }
}

public sealed class MagicNumberRule : LintRule
{
    private static readonly HashSet<string> _allowed = new(StringComparer.Ordinal)
    {
        "0", "1", "-1", "0.0", "0.0f", "0.0m", "0.0d",
        "1.0", "1.0f", "1.0m", "1.0d",
        "100", "1000", "10000", "100000", "1000000"
    };

    private static readonly Regex _magicPattern = new(
        @"(?<![.\w])(\d+)(?![.\w])|(?<![.\w])(\d+\.\d+)[fFmMdD]?(?![.\w])",
        RegexOptions.Compiled);

    public override string Id => "PFP003";
    public override string Description => "Magic number literal";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Suggestion;

    // Lines that commonly contain allowed numeric literals
    private static readonly Regex _skipPattern = new(
        @"^\s*(#|//|using|namespace|public|private|internal|protected|static|void|int|string|var|const|readonly)",
        RegexOptions.Compiled);

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(l)) continue;
            if (_skipPattern.IsMatch(l.TrimStart())) continue;

            var matches = _magicPattern.Matches(l);
            foreach (Match m in matches)
            {
                string val = m.Value;
                if (_allowed.Contains(val)) continue;
                int col = m.Index + 1;
                diagnostics.Add(new Diagnostic
                {
                    File = filePath,
                    Line = i + 1,
                    Column = col,
                    Length = m.Length,
                    Message = $"Magic number '{val}' — consider a named constant",
                    Severity = DefaultSeverity,
                    RuleId = Id
                });
            }
        }
    }
}

public sealed class MissingSemicolonRule : LintRule
{
    private static readonly Regex _statementEnd = new(
        @"^\s*(return|break|continue|throw|yield\s+return|yield\s+break)",
        RegexOptions.Compiled);

    private static readonly Regex _assignmentOrCall = new(
        @"[a-zA-Z0-9_)\]]+\s*(=|\.|\+\+|--|\[|\()",
        RegexOptions.Compiled);

    public override string Id => "PFP004";
    public override string Description => "Missing semicolon";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Error;

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string l = raw.TrimEnd('\r', '\n');
            string trimmed = l.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            // Skip lines that end with block delimiters, preprocessor, comments, attributes, labels
            if (trimmed.EndsWith('{') || trimmed.EndsWith('}')) continue;
            if (trimmed.EndsWith(':') && !trimmed.StartsWith("case ") && !trimmed.StartsWith("default:")) continue;
            if (trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith('*')) continue;
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) continue;
            if (trimmed.EndsWith(';')) continue;

            // Must look like a statement: starts with keyword, identifier, or is an assignment/expression
            bool looksLikeStatement = char.IsLetter(trimmed[0])
                || trimmed[0] == '_'
                || trimmed[0] == '@'
                || _statementEnd.IsMatch(trimmed)
                || _assignmentOrCall.IsMatch(l);

            if (looksLikeStatement)
            {
                diagnostics.Add(new Diagnostic
                {
                    File = filePath,
                    Line = i + 1,
                    Column = l.Length,
                    Length = 1,
                    Message = "Missing semicolon",
                    Severity = DefaultSeverity,
                    RuleId = Id
                });
            }
        }
    }
}

public sealed class NamingConventionRule : LintRule
{
    private static readonly Regex _pascalWord = new(@"\b[A-Z][a-zA-Z0-9]*\b", RegexOptions.Compiled);
    private static readonly Regex _camelWord = new(@"\b[a-z][a-zA-Z0-9]*\b", RegexOptions.Compiled);

    // Keywords that declare named types/members
    private static readonly Regex _declarationStart = new(
        @"\b(class|struct|interface|enum|record)\s+",
        RegexOptions.Compiled);

    public override string Id => "PFP005";
    public override string Description => "Naming convention violation";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Suggestion;

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r');

            // Check type declarations must be PascalCase
            var declMatch = _declarationStart.Match(l);
            if (declMatch.Success)
            {
                string rest = l[declMatch.Index..];
                var nameMatch = Regex.Match(rest, @"\b([A-Za-z_][A-Za-z0-9_]*)\b");
                if (nameMatch.Success)
                {
                    string name = nameMatch.Groups[1].Value;
                    if (name.Length > 0 && char.IsLower(name[0]))
                    {
                        diagnostics.Add(new Diagnostic
                        {
                            File = filePath,
                            Line = i + 1,
                            Column = declMatch.Index + nameMatch.Index + 1,
                            Length = name.Length,
                            Message = $"Type name '{name}' should be PascalCase",
                            Severity = DefaultSeverity,
                            RuleId = Id
                        });
                    }
                }
            }

            // Check local variable declarations (var x = ...) should be camelCase
            var localMatch = Regex.Match(l, @"\b(var|int|string|bool|double|float|long|char|byte|short|uint|ulong|ushort|sbyte|decimal)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*(=|;)");
            if (localMatch.Success)
            {
                string name = localMatch.Groups[2].Value;
                if (name.Length > 0 && char.IsUpper(name[0]))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        File = filePath,
                        Line = i + 1,
                        Column = localMatch.Groups[2].Index + 1,
                        Length = name.Length,
                        Message = $"Local variable '{name}' should be camelCase",
                        Severity = DefaultSeverity,
                        RuleId = Id
                    });
                }
            }
        }
    }
}

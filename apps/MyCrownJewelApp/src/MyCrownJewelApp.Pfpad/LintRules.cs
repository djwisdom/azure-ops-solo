using System.Collections.Frozen;
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

public sealed partial class MagicNumberRule : LintRule
{
    private static readonly FrozenSet<string> _allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "0", "1", "-1", "0.0", "0.0f", "0.0m", "0.0d",
        "1.0", "1.0f", "1.0m", "1.0d",
        "100", "1000", "10000", "100000", "1000000"
    }.ToFrozenSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<![.\w])(\d+)(?![.\w])|(?<![.\w])(\d+\.\d+)[fFmMdD]?(?![.\w])", RegexOptions.Compiled)]
    private static partial Regex MagicPattern();

    public override string Id => "PFP003";
    public override string Description => "Magic number literal";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Suggestion;

    [GeneratedRegex(@"^\s*(#|//|using|namespace|public|private|internal|protected|static|void|int|string|var|const|readonly)", RegexOptions.Compiled)]
    private static partial Regex SkipPattern();

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(l)) continue;
            if (SkipPattern().IsMatch(l.TrimStart())) continue;

            var matches = MagicPattern().Matches(l);
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

public sealed partial class MissingSemicolonRule : LintRule
{
    [GeneratedRegex(@"^\s*(return|break|continue|throw|yield\s+return|yield\s+break)\b", RegexOptions.Compiled)]
    private static partial Regex StatementEnd();

    [GeneratedRegex(@"[a-zA-Z0-9_)\]]+\s*(=|\.|\+\+|--|\[|\()", RegexOptions.Compiled)]
    private static partial Regex AssignmentOrCall();

    // Declaration headers and control-flow lines that never end with ';'
    [GeneratedRegex(
        @"^\s*(if|else|for|foreach|while|do|switch|try|catch|finally|lock|using\s*\()" +
        @"|^\s*(public|private|protected|internal|static|abstract|virtual|override|sealed|async|partial|readonly|extern|unsafe|new)\b" +
        @"|^\s*(class|struct|interface|enum|record|delegate|namespace)\b" +
        @"|^\s*(get|set|init)\s*$",
        RegexOptions.Compiled)]
    private static partial Regex DeclarationOrControlFlow();

    public override string Id => "PFP004";
    public override string Description => "Missing semicolon";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Error;

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        // C# only — semicolons are not meaningful syntax in HTML, CSS, JS, etc.
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return;

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r', '\n');
            string trimmed = l.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.EndsWith(';')) continue;
            if (trimmed.EndsWith('{') || trimmed.EndsWith('}')) continue;
            if (trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith('*')) continue;
            // Attributes on their own line [Foo] or [Foo(Bar)]
            if (trimmed.StartsWith('[')) continue;
            // case/default labels
            if (trimmed.EndsWith(':')) continue;

            // Multi-line continuation endings: operator, comma, open paren/bracket, arrow
            char last = trimmed[^1];
            if (last is ',' or '(' or '[' or '.' or '+' or '-' or '*' or '/'
                      or '|' or '&' or '^' or '?' or '=' or '<' or '>' or '\\') continue;
            if (trimmed.EndsWith("=>") || trimmed.EndsWith("&&") || trimmed.EndsWith("||")
                || trimmed.EndsWith("??") || trimmed.EndsWith("..")) continue;

            // Declaration headers / control-flow (never need a semicolon on that line)
            if (DeclarationOrControlFlow().IsMatch(trimmed)) continue;

            // Lines ending with ')' may be declaration headers (e.g. `public void Foo(int x)`)
            // when the next non-empty line opens a block.
            if (last == ')')
            {
                bool nextIsBlock = false;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    string next = lines[j].Trim();
                    if (string.IsNullOrWhiteSpace(next)) continue;
                    nextIsBlock = next.StartsWith('{') || next == "=>";
                    break;
                }
                if (nextIsBlock) continue;
            }

            // Must look like an expression statement
            bool looksLikeStatement = char.IsLetter(trimmed[0])
                || trimmed[0] == '_'
                || trimmed[0] == '@'
                || StatementEnd().IsMatch(trimmed)
                || AssignmentOrCall().IsMatch(l);

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

public sealed partial class NamingConventionRule : LintRule
{
    [GeneratedRegex(@"\b(class|struct|interface|enum|record)\s+", RegexOptions.Compiled)]
    private static partial Regex DeclarationStart();

    [GeneratedRegex(@"\b([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled)]
    private static partial Regex TypeNameCapture();

    [GeneratedRegex(@"\b(var|int|string|bool|double|float|long|char|byte|short|uint|ulong|ushort|sbyte|decimal)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*(=|;)", RegexOptions.Compiled)]
    private static partial Regex LocalVarDecl();

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
            var declMatch = DeclarationStart().Match(l);
            if (declMatch.Success)
            {
                string rest = l[declMatch.Index..];
                var nameMatch = TypeNameCapture().Match(rest);
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
            var localMatch = LocalVarDecl().Match(l);
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

/// <summary>
/// Detects hardcoded credentials — passwords, API keys, tokens, secrets, and
/// connection strings assigned inline in source code. Rule ID: PFP006.
/// </summary>
public sealed partial class HardcodedSecretsRule : LintRule
{
    public override string Id => "PFP006";
    public override string Description => "Hardcoded secret or credential";
    public override DiagnosticSeverity DefaultSeverity => DiagnosticSeverity.Error;

    [GeneratedRegex(
        @"(?i)\b(password|passwd|pwd|secret|api[_\-]?key|apikey|token|access[_\-]?key|auth[_\-]?key|private[_\-]?key|client[_\-]?secret|connection[_\-]?string|connectionstring)\s*[=:]\s*[""'][^""']{4,}[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SecretPattern();

    private static readonly FrozenSet<string> _safePlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "your_password", "your_secret", "your_api_key", "changeme", "change_me",
        "placeholder", "todo", "fixme", "xxx", "***", "password123", "secret123",
        "${", "$(", "env:", "@env", "process.env", "Environment.GetEnvironmentVariable",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public override void Analyze(string text, string filePath, List<Diagnostic> diagnostics)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(l)) continue;

            string trimmed = l.TrimStart();
            if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith('*') || trimmed.StartsWith('#'))
                continue;

            var m = SecretPattern().Match(l);
            if (!m.Success) continue;

            string value = m.Value;
            if (_safePlaceholders.Any(p => value.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            diagnostics.Add(new Diagnostic
            {
                File = filePath,
                Line = i + 1,
                Column = m.Index + 1,
                Length = m.Length,
                Message = "Potential hardcoded secret detected — move to environment variable or secret store",
                Severity = DefaultSeverity,
                RuleId = Id
            });
        }
    }
}

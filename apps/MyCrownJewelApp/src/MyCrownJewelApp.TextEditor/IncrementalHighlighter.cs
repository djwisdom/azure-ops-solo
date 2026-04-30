using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MyCrownJewelApp.TextEditor;

/// <summary>
/// Immutable state carried across line boundaries for multi-line constructs.
/// </summary>
public sealed record TokenizerState
{
    public bool InString { get; init; }
    public bool InComment { get; init; }  // block comment /* ... */
    public bool InCharLiteral { get; init; }
    public int OpenDelimiterIndex { get; init; }
    public static readonly TokenizerState Initial = new();
}

/// <summary>
/// Cached token result for a single line.
/// </summary>
internal sealed class LineTokens
{
    public IReadOnlyList<TokenInfo> Tokens { get; }
    public TokenizerState StateAfter { get; }

    public LineTokens(IReadOnlyList<TokenInfo> tokens, TokenizerState stateAfter)
    {
        Tokens = tokens;
        StateAfter = stateAfter;
    }
}

/// <summary>
/// Patch sent from worker thread to UI thread containing tokens for a line.
/// </summary>
public sealed record HighlightPatch(int LineNumber, IReadOnlyList<TokenInfo> Tokens);

/// <summary>
/// Non-blocking incremental syntax highlighter with worker-based tokenization.
/// Tokenizes only dirty regions, streams patches to UI, preserves state across lines.
/// </summary>
public sealed class IncrementalHighlighter : IDisposable
{
    private readonly RichTextBox _textEditor;
    private readonly SyntaxDefinition _syntax;
    private readonly Dictionary<string, (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor)> _regexCache;
    private readonly ConcurrentDictionary<int, LineTokens> _tokenCache;
    private readonly Channel<int> _dirtyLines;
    private readonly CancellationTokenSource _cts;
    private readonly Task _workerTask;
    private readonly SynchronizationContext? _uiContext;
    private readonly Color _baseColor;
    private readonly Color _keywordColor;
    private readonly Color _stringColor;
    private readonly Color _commentColor;
    private readonly Color _numberColor;
    private readonly Color _preprocessorColor;
    private bool _disposed;

    public event EventHandler<HighlightPatch>? PatchReady;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, StringBuilder lParam);
    private const int EM_GETLINE = 0x00C4;

    private string GetLineText(int lineIndex)
    {
        if (_textEditor.IsDisposed) return string.Empty;

        if (_textEditor.IsHandleCreated)
        {
            var sb = new StringBuilder(4096);
            int len = SendMessage(_textEditor.Handle, EM_GETLINE, lineIndex, sb);
            if (len > 0) return sb.ToString();
            return string.Empty;
        }
        else
        {
            // Fallback for scenarios without a window handle (e.g., unit tests)
            var lines = _textEditor.Lines;
            if (lineIndex >= 0 && lineIndex < lines.Length) return lines[lineIndex];
            return string.Empty;
        }
    }

    public IncrementalHighlighter(
        RichTextBox textEditor,
        SyntaxDefinition syntax,
        Color baseColor,
        Color keywordColor,
        Color stringColor,
        Color commentColor,
        Color numberColor,
        Color preprocessorColor)
    {
        _textEditor = textEditor;
        _syntax = syntax;
        _baseColor = baseColor;
        _keywordColor = keywordColor;
        _stringColor = stringColor;
        _commentColor = commentColor;
        _numberColor = numberColor;
        _preprocessorColor = preprocessorColor;

        _regexCache = new Dictionary<string, (Regex?, Regex?, Regex?, Regex?, Regex?, Regex?)>();
        _tokenCache = new ConcurrentDictionary<int, LineTokens>();
        _dirtyLines = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _uiContext = SynchronizationContext.Current;

        // Compile regexes upfront
        GetOrCreateCompiledRegexes(syntax);

        // Start background worker
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Marks a line as requiring re-tokenization (called after text edit).
    /// Evicts from cache and enqueues dirty line for background processing.
    /// </summary>
    public void MarkDirty(int lineNumber)
    {
        if (lineNumber < 0) return;

        // Evict from cache — content changed
        _tokenCache.TryRemove(lineNumber, out _);
        _dirtyLines.Writer.TryWrite(lineNumber);
    }

    /// <summary>
    /// Requests highlighting for a range of lines (used on viewport change).
    /// Only enqueues lines that are not already cached.
    /// </summary>
    public void RequestRange(int startLine, int endLine)
    {
        for (int line = startLine; line <= endLine; line++)
        {
            // Only mark dirty if not already cached
            if (!_tokenCache.ContainsKey(line))
            {
                _dirtyLines.Writer.TryWrite(line);
            }
        }
    }

    /// <summary>
    /// Gets cached tokens for a line, or null if not yet tokenized.
    /// </summary>
    public IReadOnlyList<TokenInfo>? GetTokens(int lineIndex)
    {
        if (_tokenCache.TryGetValue(lineIndex, out var entry))
            return entry.Tokens;
        return null;
    }

    private (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor) GetOrCreateCompiledRegexes(SyntaxDefinition syntax)
    {
        string key = syntax.Name;
        lock (_regexCache)
        {
            if (_regexCache.TryGetValue(key, out var existing))
                return existing;
        }

        static Regex? Build(string? pattern, RegexOptions options)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            return new Regex(pattern, options | RegexOptions.Compiled);
        }

        var keywords = Build(@"\b(" + string.Join("|", syntax.Keywords.Select(Regex.Escape)) + @")\b", RegexOptions.None);
        var types = Build(@"\b(" + string.Join("|", syntax.Types.Select(Regex.Escape)) + @")\b", RegexOptions.None);
        var stringRegex = Build(syntax.StringPattern, RegexOptions.Singleline);
        var comment = Build(syntax.CommentPattern, RegexOptions.Multiline);
        var number = Build(syntax.NumberPattern, RegexOptions.None);
        var preprocessor = syntax.Preprocessor?.Length > 0
            ? Build(@"^\s*(" + string.Join("|", syntax.Preprocessor.Select(Regex.Escape)) + @")\b", RegexOptions.Multiline)
            : null;

        var tuple = (keywords, types, stringRegex, comment, number, preprocessor);
        lock (_regexCache) { _regexCache[key] = tuple; }
        return tuple;
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        var dirtyBatch = new List<int>();
        var dirtySet = new HashSet<int>();

        await foreach (var line in _dirtyLines.Reader.ReadAllAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Batch consecutive dirty lines
            dirtyBatch.Clear();
            dirtySet.Clear();
            dirtyBatch.Add(line);
            dirtySet.Add(line);

            // Drain channel into batch
            while (_dirtyLines.Reader.TryRead(out var next))
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!dirtySet.Contains(next))
                {
                    dirtyBatch.Add(next);
                    dirtySet.Add(next);
                }
            }

            // Sort and coalesce into ranges
            dirtyBatch.Sort();
            var ranges = CoalesceIntoRanges(dirtyBatch);

            foreach (var (start, end) in ranges)
            {
                if (cancellationToken.IsCancellationRequested) break;
                TokenizeRange(start, end);
            }
        }
    }

    private static List<(int start, int end)> CoalesceIntoRanges(List<int> lines)
    {
        var ranges = new List<(int, int)>();
        if (lines.Count == 0) return ranges;

        int rangeStart = lines[0];
        int prev = lines[0];

        for (int i = 1; i < lines.Count; i++)
        {
            int curr = lines[i];
            if (curr <= prev + 3) // small gap, coalesce (allow slight discontinuity)
            {
                prev = curr;
                continue;
            }
            ranges.Add((rangeStart, prev));
            rangeStart = curr;
            prev = curr;
        }
        ranges.Add((rangeStart, prev));
        return ranges;
    }

    private void TokenizeRange(int startLine, int endLine)
    {
        // Get initial state from cache at startLine - 1
        TokenizerState state = startLine > 0 && _tokenCache.TryGetValue(startLine - 1, out var prevEntry)
            ? prevEntry.StateAfter
            : TokenizerState.Initial;

        for (int lineNum = startLine; lineNum <= endLine; lineNum++)
        {
            string line = GetLineText(lineNum);
            var (tokens, nextState) = TokenizeLine(line, state, lineNum);
            state = nextState;

            var lineTokens = new LineTokens(tokens, state);
            _tokenCache[lineNum] = lineTokens;

            var patch = new HighlightPatch(lineNum, tokens);
            _uiContext?.Post(_ =>
            {
                try { PatchReady?.Invoke(this, patch); }
                catch { }
            }, null);
        }
    }

    private (List<TokenInfo> Tokens, TokenizerState StateAfter) TokenizeLine(string line, TokenizerState initialState, int lineIndex)
    {
        var tokens = new List<TokenInfo>();
        if (string.IsNullOrEmpty(line))
            return (tokens, initialState);

        var (keywords, types, stringRegex, commentRegex, numberRegex, preprocessorRegex) = GetOrCreateCompiledRegexes(_syntax);
        var colored = new bool[line.Length];
        int pos = 0;

        // ---- Handle multi-line continuation from previous line ----
        if (initialState.InComment)
        {
            // Find end of block comment
            int endIdx = line.IndexOf("*/", pos);
            if (endIdx >= 0)
            {
                // Entire segment [pos..endIdx+2) is comment
                AddToken(tokens, colored, SyntaxTokenType.Comment, 0, endIdx + 2);
                pos = endIdx + 2;
            }
            else
            {
                // Whole line is comment; state remains InComment
                AddToken(tokens, colored, SyntaxTokenType.Comment, 0, line.Length);
                return (tokens, initialState);
            }
        }

        if (initialState.InString)
        {
            // We're inside a string that started on previous line.
            // Find closing delimiter (respecting escapes)
            int closePos = FindStringEnd(line, pos, initialState.OpenDelimiterIndex);
            if (closePos >= 0)
            {
                // String from pos to closePos+1
                AddToken(tokens, colored, SyntaxTokenType.String, pos, closePos + 1 - pos);
                pos = closePos + 1;
            }
            else
            {
                // Whole line is string continuation
                AddToken(tokens, colored, SyntaxTokenType.String, 0, line.Length);
                return (tokens, initialState); // Still in string
            }
        }

        // ---- Scan remaining line with priority rules ----
        // Preprocessor: only at line start (pos should be 0 still)
        if (pos == 0 && preprocessorRegex != null)
        {
            var m = preprocessorRegex.Match(line);
            if (m.Success && m.Index == 0)
            {
                AddToken(tokens, colored, SyntaxTokenType.Preprocessor, m.Index, m.Length);
                pos = m.Index + m.Length;
            }
        }

        while (pos < line.Length)
        {
            // Skip whitespace not part of tokens (colored span consumption checked by AddToken)
            // We'll let regexes match freely; AddToken respects colored[]
            bool matched = false;

            // Block comment start
            if (commentRegex != null && pos == 0) // only check at pos for /* (avoid mid-line false positives)
            {
                var m = commentRegex.Match(line, pos);
                if (m.Success && m.Index == pos)
                {
                    AddToken(tokens, colored, SyntaxTokenType.Comment, m.Index, m.Length);
                    pos = m.Index + m.Length;
                    matched = true;
                }
            }

            // Line comment (//)
            if (!matched && commentRegex != null && pos < line.Length - 1 && line[pos] == '/' && line[pos + 1] == '/')
            {
                // remainder is comment
                int len = line.Length - pos;
                AddToken(tokens, colored, SyntaxTokenType.Comment, pos, len);
                pos = line.Length;
                matched = true;
            }

            // String (regular or verbatim)
            if (!matched && stringRegex != null)
            {
                if (line[pos] == '"')
                {
                    var end = FindStringEnd(line, pos, pos);
                    if (end >= 0)
                    {
                        int len = end - pos + 1;
                        AddToken(tokens, colored, SyntaxTokenType.String, pos, len);
                        pos = end + 1;
                        matched = true;
                    }
                    else
                    {
                        // Unterminated: rest is string
                        int len = line.Length - pos;
                        AddToken(tokens, colored, SyntaxTokenType.String, pos, len);
                        return (tokens, initialState with { InString = true, OpenDelimiterIndex = pos });
                    }
                }
            }

            // Number
            if (!matched && numberRegex != null)
            {
                var m = numberRegex.Match(line, pos);
                if (m.Success && m.Index == pos)
                {
                    AddToken(tokens, colored, SyntaxTokenType.Number, m.Index, m.Length);
                    pos = m.Index + m.Length;
                    matched = true;
                }
            }

            // Keywords
            if (!matched && keywords != null)
            {
                var m = keywords.Match(line, pos);
                if (m.Success && m.Index == pos)
                {
                    AddToken(tokens, colored, SyntaxTokenType.Keyword, m.Index, m.Length);
                    pos = m.Index + m.Length;
                    matched = true;
                }
            }

            // Types (as keywords)
            if (!matched && types != null)
            {
                var m = types.Match(line, pos);
                if (m.Success && m.Index == pos)
                {
                    AddToken(tokens, colored, SyntaxTokenType.Keyword, m.Index, m.Length);
                    pos = m.Index + m.Length;
                    matched = true;
                }
            }

            // If nothing matched, advance one char
            if (!matched)
            {
                // Check if this position already colored by earlier match
                if (pos < colored.Length && colored[pos])
                {
                    pos++;
                }
                else
                {
                    // Not colored, skip ahead to next possible token start or end of line
                    pos++;
                }
            }
        }

        return (tokens, initialState);
    }

    private static void AddToken(List<TokenInfo> tokens, bool[] colored, SyntaxTokenType type, int start, int length)
    {
        if (start < 0 || start >= colored.Length) return;
        int end = Math.Min(start + length, colored.Length);
        // Check if any position in [start, end) already colored
        bool free = true;
        for (int i = start; i < end; i++)
        {
            if (colored[i])
            {
                free = false;
                break;
            }
        }
        if (free)
        {
            for (int i = start; i < end; i++) colored[i] = true;
            tokens.Add(new TokenInfo { Type = type, StartIndex = start, Length = end - start, Text = "" });
        }
    }

    private static int FindStringEnd(string line, int startPos, int openDelimiterPos)
    {
        // openDelimiterPos is position of opening " within the line on previous line (if any)
        // For regular strings: scan for unescaped " (not preceded by \)
        // For verbatim strings (@"..."): scan for "" (double quote) which escapes
        // Simplify: we'll scan for " not preceded by \ (for regular). For verbatim, detect from syntax.
        for (int i = startPos; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                // Check escape: if previous char is \, count preceding backslashes
                if (i > 0 && line[i - 1] == '\\')
                {
                    // escaped, skip
                    continue;
                }
                return i;
            }
        }
        return -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cts.Cancel();
        _dirtyLines.Writer.Complete();
        try { _workerTask.Wait(1000); } catch { }
        _cts.Dispose();
        _disposed = true;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed record TokenizerState
{
    public bool InComment { get; init; }
    public static readonly TokenizerState Initial = new();
}

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

public sealed record HighlightPatch(int LineNumber, IReadOnlyList<TokenInfo> Tokens);

public sealed class IncrementalHighlighter : IDisposable
{
    private readonly RichTextBox _textEditor;
    private readonly ConcurrentDictionary<int, LineTokens> _tokenCache;
    private readonly Channel<(int Line, string Text)> _dirtyLines;
    private readonly CancellationTokenSource _cts;
    private readonly Task? _workerTask;
    private readonly SynchronizationContext? _uiContext;

    private readonly HashSet<string> _keywordSet;
    private readonly HashSet<string> _preprocessorSet;

    private const int MaxLinesPerBatch = 50;
    private bool _disposed;

    public event EventHandler<List<HighlightPatch>>? BatchReady;

    public IncrementalHighlighter(RichTextBox textEditor, SyntaxDefinition syntax)
        : this(textEditor, syntax, SynchronizationContext.Current)
    {
    }

    public IncrementalHighlighter(RichTextBox textEditor, SyntaxDefinition syntax, SynchronizationContext? uiContext)
    {
        _textEditor = textEditor;
        _keywordSet = new HashSet<string>(syntax.Keywords, StringComparer.Ordinal);
        foreach (var t in syntax.Types) _keywordSet.Add(t);
        _preprocessorSet = new HashSet<string>(syntax.Preprocessor, StringComparer.Ordinal);

        _tokenCache = new ConcurrentDictionary<int, LineTokens>();
        _dirtyLines = Channel.CreateBounded<(int, string)>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _uiContext = uiContext;
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token), _cts.Token);
    }

    public void RequestRange(int startLine, int endLine)
    {
        if (startLine < 0) return;
        for (int line = startLine; line <= endLine; line++)
        {
            if (!_tokenCache.ContainsKey(line))
            {
                string? text = GetLineText(line);
                if (text != null)
                    _dirtyLines.Writer.TryWrite((line, text));
            }
        }
    }

    private string? GetLineText(int lineIndex)
    {
        try
        {
            if (_textEditor.IsDisposed) return null;
            if (!_textEditor.IsHandleCreated)
            {
                var unused = _textEditor.Handle;
            }
            string text = _textEditor.Text;
            int lineStart = _textEditor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return null;
            int lineEnd = _textEditor.GetFirstCharIndexFromLine(lineIndex + 1);
            if (lineEnd < 0) lineEnd = text.Length;
            return text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r', '\n');
        }
        catch { return null; }
    }

    public IReadOnlyList<TokenInfo>? GetTokens(int lineIndex)
    {
        return _tokenCache.TryGetValue(lineIndex, out var entry) ? entry.Tokens : null;
    }

    public void MarkDirty(int lineNumber)
    {
        if (lineNumber < 0) return;
        _tokenCache.TryRemove(lineNumber, out _);
        string? text = GetLineText(lineNumber);
        if (text != null)
            _dirtyLines.Writer.TryWrite((lineNumber, text));
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        var batch = new List<(int Line, string Text)>();
        await foreach (var entry in _dirtyLines.Reader.ReadAllAsync(ct))
        {
            if (ct.IsCancellationRequested) break;

            batch.Clear();
            batch.Add(entry);
            while (_dirtyLines.Reader.TryRead(out var next) && batch.Count < MaxLinesPerBatch)
            {
                if (ct.IsCancellationRequested) break;
                batch.Add(next);
            }

            batch.Sort((a, b) => a.Line.CompareTo(b.Line));

            TokenizerState state = TokenizerState.Initial;
            int startLine = batch[0].Line;
            if (startLine > 0 && _tokenCache.TryGetValue(startLine - 1, out var prev))
                state = prev.StateAfter;

            var patches = new List<HighlightPatch>(batch.Count);
            for (int i = 0; i < batch.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                int lineNum = batch[i].Line;
                string text = batch[i].Text;
                var (tokens, nextState) = TokenizeLine(text, state);
                state = nextState;
                _tokenCache[lineNum] = new LineTokens(tokens, state);
                patches.Add(new HighlightPatch(lineNum, tokens));
            }

            // Send entire batch as one UI update
            _uiContext?.Post(_ =>
            {
                try { BatchReady?.Invoke(this, patches); }
                catch { }
            }, null);

            await Task.Delay(1, ct);
        }
    }

    internal (List<TokenInfo> Tokens, TokenizerState StateAfter) TokenizeLine(string line, TokenizerState state)
    {
        var tokens = new List<TokenInfo>();
        if (string.IsNullOrEmpty(line)) return (tokens, state with { InComment = false });

        int pos = 0;

        if (state.InComment)
        {
            int end = line.IndexOf("*/", pos, StringComparison.Ordinal);
            if (end >= 0) { AddToken(tokens, pos, end + 2 - pos, SyntaxTokenType.Comment); pos = end + 2; state = state with { InComment = false }; }
            else { AddToken(tokens, 0, line.Length, SyntaxTokenType.Comment); return (tokens, state); }
        }

        if (pos == 0 && _preprocessorSet.Count > 0)
        {
            int ws = pos; while (ws < line.Length && char.IsWhiteSpace(line[ws])) ws++;
            if (ws < line.Length && line[ws] == '#') { AddToken(tokens, 0, line.Length, SyntaxTokenType.Preprocessor); return (tokens, state); }
        }

        while (pos < line.Length)
        {
            char c = line[pos];

            if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
            {
                int end = line.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                if (end >= 0) { AddToken(tokens, pos, end - pos + 2, SyntaxTokenType.Comment); pos = end + 2; }
                else { AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment); return (tokens, state with { InComment = true }); }
                continue;
            }

            if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
            {
                AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                break;
            }

            if (c == '"' || c == '\'')
            {
                int s = pos; char q = c; pos++;
                while (pos < line.Length) { if (line[pos] == '\\') { pos += 2; continue; } if (line[pos] == q) { pos++; break; } pos++; }
                AddToken(tokens, s, pos - s, SyntaxTokenType.String);
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
            {
                int s = pos; if (c == '-') pos++;
                while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '.')) pos++;
                AddToken(tokens, s, pos - s, SyntaxTokenType.Number);
                continue;
            }

            if (char.IsLetter(c) || c == '_' || c == '@')
            {
                int s = pos;
                while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_')) pos++;
                string word = line.AsSpan(s, pos - s).ToString();
                if (_keywordSet.Contains(word))
                    AddToken(tokens, s, pos - s, SyntaxTokenType.Keyword);
                continue;
            }

            pos++;
        }

        return (tokens, state);
    }

    private static void AddToken(List<TokenInfo> tokens, int start, int length, SyntaxTokenType type)
    {
        if (length <= 0) return;
        tokens.Add(new TokenInfo { Type = type, StartIndex = start, Length = length, Text = "" });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cts.Cancel();
        _dirtyLines.Writer.Complete();
        try { _workerTask?.Wait(500); } catch { }
        _cts.Dispose();
        _disposed = true;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

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
    private readonly SyntaxDefinition _syntax;
    private readonly ConcurrentDictionary<int, LineTokens> _tokenCache;
    private readonly Channel<int> _dirtyLines;
    private readonly CancellationTokenSource _cts;
    private readonly Task? _workerTask;
    private readonly SynchronizationContext? _uiContext;

    private readonly HashSet<string> _keywordSet;
    private readonly HashSet<string> _preprocessorSet;

    private const int MaxLinesPerBatch = 50;
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
            return len > 0 ? sb.ToString() : string.Empty;
        }
        var lines = _textEditor.Lines;
        return lineIndex >= 0 && lineIndex < lines.Length ? lines[lineIndex] : string.Empty;
    }

    public IncrementalHighlighter(RichTextBox textEditor, SyntaxDefinition syntax)
    {
        _textEditor = textEditor;
        _syntax = syntax;

        _keywordSet = new HashSet<string>(syntax.Keywords, StringComparer.Ordinal);
        foreach (var t in syntax.Types)
            _keywordSet.Add(t);
        _preprocessorSet = new HashSet<string>(syntax.Preprocessor, StringComparer.Ordinal);

        _tokenCache = new ConcurrentDictionary<int, LineTokens>();
        _dirtyLines = Channel.CreateBounded<int>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _cts = new CancellationTokenSource();
        _uiContext = SynchronizationContext.Current;

        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token), _cts.Token);
    }

    public void MarkDirty(int lineNumber)
    {
        if (lineNumber < 0) return;
        _tokenCache.TryRemove(lineNumber, out _);
        _dirtyLines.Writer.TryWrite(lineNumber);
    }

    public void MarkDirtyRange(int startLine, int endLine)
    {
        if (startLine < 0) return;
        for (int line = startLine; line <= endLine; line++)
            MarkDirty(line);
    }

    public void RequestRange(int startLine, int endLine)
    {
        if (startLine < 0) return;
        for (int line = startLine; line <= endLine; line++)
        {
            if (!_tokenCache.ContainsKey(line))
                _dirtyLines.Writer.TryWrite(line);
        }
    }

    public IReadOnlyList<TokenInfo>? GetTokens(int lineIndex)
    {
        return _tokenCache.TryGetValue(lineIndex, out var entry) ? entry.Tokens : null;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        var batch = new List<int>();
        await foreach (var line in _dirtyLines.Reader.ReadAllAsync(ct))
        {
            if (ct.IsCancellationRequested) break;

            batch.Clear();
            batch.Add(line);

            while (_dirtyLines.Reader.TryRead(out var next) && batch.Count < MaxLinesPerBatch)
            {
                if (ct.IsCancellationRequested) break;
                if (!batch.Contains(next))
                    batch.Add(next);
            }

            batch.Sort();

            TokenizerState state = TokenizerState.Initial;
            int startLine = batch[0];
            if (startLine > 0 && _tokenCache.TryGetValue(startLine - 1, out var prev))
                state = prev.StateAfter;

            for (int i = 0; i < batch.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                int lineNum = batch[i];
                string text = GetLineText(lineNum);
                var (tokens, nextState) = TokenizeLine(text, state);
                state = nextState;
                _tokenCache[lineNum] = new LineTokens(tokens, state);
                var patch = new HighlightPatch(lineNum, tokens);
                _uiContext?.Post(_ =>
                {
                    try { PatchReady?.Invoke(this, patch); }
                    catch { }
                }, null);
            }

            await Task.Delay(1, ct);
        }
    }

    internal (List<TokenInfo> Tokens, TokenizerState StateAfter) TokenizeLine(string line, TokenizerState state)
    {
        var tokens = new List<TokenInfo>();
        if (string.IsNullOrEmpty(line))
            return (tokens, state with { InComment = false });

        int pos = 0;

        // Handle multi-line block comment continuation
        if (state.InComment)
        {
            int end = line.IndexOf("*/", pos, StringComparison.Ordinal);
            if (end >= 0)
            {
                AddToken(tokens, pos, end + 2 - pos, SyntaxTokenType.Comment);
                pos = end + 2;
                state = state with { InComment = false };
            }
            else
            {
                AddToken(tokens, 0, line.Length, SyntaxTokenType.Comment);
                return (tokens, state);
            }
        }

        // Preprocessor directive (must be at start of line)
        if (pos == 0 && _preprocessorSet.Count > 0)
        {
            ReadPreprocessor(line, ref pos, tokens);
        }

        while (pos < line.Length)
        {
            char c = line[pos];

            // Block comment start
            if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '*')
            {
                int end = line.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    AddToken(tokens, pos, end - pos + 2, SyntaxTokenType.Comment);
                    pos = end + 2;
                }
                else
                {
                    AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                    return (tokens, state with { InComment = true });
                }
                continue;
            }

            // Line comment
            if (c == '/' && pos + 1 < line.Length && line[pos + 1] == '/')
            {
                AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                pos = line.Length;
                break;
            }

            // String
            if (c == '"' || c == '\'')
            {
                int start = pos;
                char quote = c;
                pos++;
                while (pos < line.Length)
                {
                    if (line[pos] == '\\') { pos += 2; continue; }
                    if (line[pos] == quote) { pos++; break; }
                    pos++;
                }
                AddToken(tokens, start, pos - start, SyntaxTokenType.String);
                continue;
            }

            // Number
            if (char.IsDigit(c) || (c == '-' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
            {
                int start = pos;
                if (c == '-') pos++;
                while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '.' || line[pos] == 'f' || line[pos] == 'F' || line[pos] == 'd' || line[pos] == 'D' || line[pos] == 'L' || line[pos] == 'l' || line[pos] == 'u' || line[pos] == 'U'))
                    pos++;
                AddToken(tokens, start, pos - start, SyntaxTokenType.Number);
                continue;
            }

            // Word (keyword / type / identifier)
            if (char.IsLetter(c) || c == '_' || c == '@' || c == '#')
            {
                int start = pos;
                while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_'))
                    pos++;
                string word = line.AsSpan(start, pos - start).ToString();

                if (_keywordSet.Contains(word))
                    AddToken(tokens, start, pos - start, SyntaxTokenType.Keyword);
                else if (char.IsUpper(word[0]) && word.Length > 1 && !_keywordSet.Contains(word))
                {
                    // Could be a type - highlight as keyword
                    // For simplicity, just skip
                }
                continue;
            }

            pos++;
        }

        return (tokens, state);
    }

    private void ReadPreprocessor(string line, ref int pos, List<TokenInfo> tokens)
    {
        int start = pos;
        // Skip leading whitespace
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            pos++;
        // Check for '#' at column 0 (after optional whitespace)
        if (pos < line.Length && line[pos] == '#')
        {
            int end = pos;
            while (end < line.Length && line[end] != '"' && line[end] != '\'')
            {
                if (end + 1 < line.Length && line[end] == '/' && line[end + 1] == '/')
                    break;
                end++;
            }
            AddToken(tokens, start, end - start, SyntaxTokenType.Preprocessor);
            pos = end;
        }
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

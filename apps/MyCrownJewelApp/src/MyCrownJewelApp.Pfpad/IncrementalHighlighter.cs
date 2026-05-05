using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

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
    private readonly RichTextBox _editor;
    private readonly ConcurrentDictionary<int, LineTokens> _cache = new();
    private readonly Channel<(int Line, string Text)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly HashSet<string> _keywords;
    private readonly HashSet<string> _types;
    private readonly HashSet<string> _preprocs;
    private bool _disposed;
    private int _totalTimeouts;
    private int _consecutiveTimeouts;
    private bool _fallbackActive;
    private int _stateCycleCount;

    private const int MaxBatch = 500;
    private const int MaxParseTimeMs = 16;
    private const int MaxIterationsPerLine = 10000;
    private const int MaxWorkerBatchTimeMs = 50;
    private const int MaxConsecutiveTimeouts = 5;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
    private const int EM_GETLINE = 0xC4;
    private const int EM_GETLINECOUNT = 0xBA;

    public event Action<List<HighlightPatch>>? PatchReady;

    public IncrementalHighlighter(RichTextBox editor, SyntaxDefinition syntax)
    {
        _editor = editor;
        _keywords = new HashSet<string>(syntax.Keywords, StringComparer.Ordinal);
        _types = new HashSet<string>(syntax.Types, StringComparer.Ordinal);
        _preprocs = new HashSet<string>(syntax.Preprocessor, StringComparer.Ordinal);

        _channel = Channel.CreateBounded<(int, string)>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _worker = Task.Run(WorkerLoop);
    }

    public void RequestRange(int startLine, int endLine)
    {
        if (startLine < 0 || _editor.IsDisposed) return;
        int lineCount = SendMessage(_editor.Handle, EM_GETLINECOUNT, 0, IntPtr.Zero);
        for (int line = startLine; line <= endLine && line < lineCount; line++)
            if (!_cache.ContainsKey(line))
                EnqueueLine(line);
    }

    public IReadOnlyList<TokenInfo>? GetTokens(int lineIndex)
        => _cache.TryGetValue(lineIndex, out var entry) ? entry.Tokens : null;

    public void MarkDirty(int lineNumber)
    {
        if (lineNumber < 0) return;
        _cache.TryRemove(lineNumber, out _);
        EnqueueLine(lineNumber);
    }

    private void EnqueueLine(int line)
    {
        string? text = GetLineText(line);
        if (text != null)
            _channel.Writer.TryWrite((line, text));
    }

    private string? GetLineText(int line)
    {
        try
        {
            int count = SendMessage(_editor.Handle, EM_GETLINECOUNT, 0, IntPtr.Zero);
            if (line < 0 || line >= count) return null;
            IntPtr ptr = Marshal.AllocCoTaskMem(4096 * 2);
            try
            {
                Marshal.WriteInt16(ptr, (short)4096);
                int len = SendMessage(_editor.Handle, EM_GETLINE, line, ptr);
                return len > 0 ? Marshal.PtrToStringUni(ptr, len).TrimEnd('\r', '\n') : null;
            }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }
        catch { return null; }
    }

    private async Task WorkerLoop()
    {
        var batch = new List<(int Line, string Text)>();
        var sw = new Stopwatch();
        await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            batch.Clear();
            batch.Add(entry);
            while (_channel.Reader.TryRead(out var next) && batch.Count < MaxBatch)
                batch.Add(next);

            batch.Sort((a, b) => a.Line.CompareTo(b.Line));

            var patches = new List<HighlightPatch>(batch.Count);
            TokenizerState state = TokenizerState.Initial;

            int firstLine = batch[0].Line;
            if (firstLine > 0 && _cache.TryGetValue(firstLine - 1, out var prev))
                state = prev.StateAfter;

            if (_fallbackActive)
            {
                foreach (var item in batch)
                    if (!_cache.ContainsKey(item.Line))
                        patches.Add(new HighlightPatch(item.Line, Array.Empty<TokenInfo>()));
            }
            else
            {
                int consecutiveTimeoutsThisBatch = 0;
                sw.Restart();

                for (int i = 0; i < batch.Count; i++)
                {
                    if (_cts.IsCancellationRequested) break;

                    if (sw.ElapsedMilliseconds > MaxWorkerBatchTimeMs)
                    {
                        await Task.Yield();
                        sw.Restart();
                    }

                    int lineNum = batch[i].Line;
                    var (tokens, nextState) = TokenizeLine(batch[i].Text.AsSpan(), state);
                    if (tokens.Count == 1 && tokens[0].Type == SyntaxTokenType.Comment && tokens[0].Length == batch[i].Text.Length && tokens[0].StartIndex == 0)
                    {
                        bool wasTimeout = state.InComment && tokens[0].Length < batch[i].Text.Length;
                        if (wasTimeout)
                        {
                            consecutiveTimeoutsThisBatch++;
                            Interlocked.Increment(ref _totalTimeouts);
                        }
                    }
                    if (state.InComment == nextState.InComment && state.InComment)
                        Interlocked.Increment(ref _stateCycleCount);
                    else
                        Interlocked.Exchange(ref _stateCycleCount, 0);

                    state = nextState;
                    _cache[lineNum] = new LineTokens(tokens, state);
                    patches.Add(new HighlightPatch(lineNum, tokens));
                }

                if (consecutiveTimeoutsThisBatch > 0)
                {
                    Interlocked.Add(ref _consecutiveTimeouts, consecutiveTimeoutsThisBatch);
                    if (Volatile.Read(ref _consecutiveTimeouts) >= MaxConsecutiveTimeouts)
                        Volatile.Write(ref _fallbackActive, true);
                }
                else
                {
                    Interlocked.Exchange(ref _consecutiveTimeouts, 0);
                }

                if (Volatile.Read(ref _stateCycleCount) > 100)
                {
                    Volatile.Write(ref _fallbackActive, true);
                }
            }

            if (patches.Count > 0)
            {
                try { _editor.BeginInvoke(() => PatchReady?.Invoke(patches)); }
                catch { }
            }

        }
    }

    internal (List<TokenInfo> Tokens, TokenizerState StateAfter) TokenizeLine(
        ReadOnlySpan<char> line, TokenizerState state)
    {
        var tokens = new List<TokenInfo>();
        if (line.Length == 0)
            return (tokens, state with { InComment = false });

        int pos = 0;
        int iterations = 0;
        var sw = new Stopwatch();
        sw.Start();

        if (state.InComment)
        {
            int end = line.Slice(pos).IndexOf("*/".AsSpan());
            if (end >= 0)
            {
                AddToken(tokens, pos, end + 2, SyntaxTokenType.Comment);
                pos += end + 2;
                state = state with { InComment = false };
            }
            else
            {
                AddToken(tokens, 0, line.Length, SyntaxTokenType.Comment);
                return (tokens, state);
            }
        }

        if (pos == 0 && _preprocs.Count > 0)
        {
            int ws = pos;
            while (ws < line.Length && char.IsWhiteSpace(line[ws])) ws++;
            if (ws < line.Length && line[ws] == '#')
            {
                AddToken(tokens, 0, line.Length, SyntaxTokenType.Preprocessor);
                return (tokens, state);
            }
        }

        while (pos < line.Length)
        {
            iterations++;
            if (iterations > MaxIterationsPerLine)
            {
                AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                return (tokens, state with { InComment = true });
            }
            if (sw.ElapsedMilliseconds > MaxParseTimeMs)
            {
                if (state.InComment)
                    AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                else
                    AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                return (tokens, state with { InComment = true });
            }

            char c = line[pos];

            if (c == '/' && pos + 1 < line.Length)
            {
                if (line[pos + 1] == '*')
                {
                    int end = line.Slice(pos + 2).IndexOf("*/".AsSpan());
                    if (end >= 0)
                    {
                        AddToken(tokens, pos, end + 4, SyntaxTokenType.Comment);
                        pos += end + 4;
                    }
                    else
                    {
                        AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                        return (tokens, state with { InComment = true });
                    }
                    continue;
                }
                if (line[pos + 1] == '/')
                {
                    AddToken(tokens, pos, line.Length - pos, SyntaxTokenType.Comment);
                    break;
                }
            }

            if (c is '"' or '\'')
            {
                int start = pos;
                char q = c;
                pos++;
                while (pos < line.Length)
                {
                    if (line[pos] == '\\') { pos += 2; continue; }
                    if (line[pos] == q) { pos++; break; }
                    pos++;
                }
                AddToken(tokens, start, pos - start, SyntaxTokenType.String);
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && pos + 1 < line.Length && char.IsDigit(line[pos + 1])))
            {
                int start = pos;
                if (c == '-') pos++;
                while (pos < line.Length && (char.IsDigit(line[pos]) || line[pos] == '.'))
                    pos++;
                AddToken(tokens, start, pos - start, SyntaxTokenType.Number);
                continue;
            }

            if (char.IsLetter(c) || c == '_' || c == '@')
            {
                int start = pos;
                while (pos < line.Length && (char.IsLetterOrDigit(line[pos]) || line[pos] == '_'))
                    pos++;
                string word = line.Slice(start, pos - start).ToString();
                if (_keywords.Contains(word))
                    AddToken(tokens, start, pos - start, SyntaxTokenType.Keyword);
                else if (_types.Contains(word))
                    AddToken(tokens, start, pos - start, SyntaxTokenType.Type);
                continue;
            }

            pos++;
        }

        return (tokens, state);
    }

    private static void AddToken(List<TokenInfo> tokens, int start, int length, SyntaxTokenType type)
    {
        if (length <= 0) return;
        tokens.Add(new TokenInfo { Type = type, StartIndex = start, Length = length });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _worker.Wait(500); } catch { }
        _cts.Dispose();
        _disposed = true;
    }
}

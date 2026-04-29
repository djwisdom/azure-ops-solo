# Incremental Syntax Highlighter Design

## Overview
Replace the current synchronous per-visible-range full re-tokenization with a non-blocking, worker-based incremental highlighter that:
- Tokenizes only changed/dirty regions
- Streams patch updates to the main thread
- Preserves tokenizer state across line boundaries (multi-line strings/comments)
- Supports fallback synchronous mode
- Achieves <50ms incremental update latency for ~1k lines
- Provides progressive rendering for large files

## Current Implementation (Baseline)

**Flow** (`HighlightSyntaxAsync`):
1. Debounced timer (150ms) fires → `HighlightSyntaxAsync`
2. Capture visible line range (`GetVisibleLineRange`)
3. `Task.Run` on background thread: for each visible line:
   - Get compiled regexes (cached per-syntax)
   - Create `bool[line.Length] colored` array
   - Apply regex matches in priority order via `ApplyTokenMatches`
   - Collect `(start, length, color)` tuples per line
4. Back on UI thread: `ApplyLineRanges` sets `SelectionColor` for each span
5. Minimap calls `GetTokensForLine` (synchronous per-call) per visible line

**Problems**:
- Re-tokenizes entire visible range on every update, even if only 1 line changed
- No per-line token caching → repeated work on scroll/re-highlight
- Multi-line constructs (block comments, strings) lose state at line boundaries
- Minimap tokenization blocks UI when requesting many lines
- No true incremental streaming; batches whole visible range each time

## New Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│ Form1 (UI thread)                                          │
│  - TextEditor RichTextBox                                  │
│  - HighlightTimer (debounce)                               │
│  - IncrementalHighlighter instance                         │
│  - Patch application: ApplyHighlightPatch                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ enqueue dirty lines
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ IncrementalHighlighter                                      │
│  - TokenCache: Dictionary<int, LineTokens>                 │
│  - DirtyLines: ConcurrentQueue<int> or HashSet              │
│  - Worker Task (background)                                │
│  - Channel<HighlightPatch> → UI                            │
│  - SyntaxDefinition + CompiledRegexes                      │
│  - CancellationTokenSource                                  │
└────────────────────────┬────────────────────────────────────┘
                         │
                    Tokenize dirty lines
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ Tokenizer (stateful, line-by-line)                         │
│  Input: line text, TokenizerState? (from prev line)        │
│  Output: List<TokenInfo>, TokenizerState (for next line)   │
└─────────────────────────────────────────────────────────────┘
```

### Data Structures

```csharp
// Immutable state carried across line boundaries
public sealed class TokenizerState
{
    public bool InString { get; init; }
    public bool InComment { get; init; }
    public bool InCharLiteral { get; init; }
    public int StringDelimiterPos { get; init; }  // position of opening " or ' (for escape handling)
    // Could extend: partial regex match data for performance (future)
    public static readonly TokenizerState Initial = new();
}

// Cached tokens for a single line
internal sealed class LineTokens
{
    public IReadOnlyList<TokenInfo> Tokens { get; }
    public TokenizerState StateAfter { get; }  // state after this line (for next line)
    public LineTokens(IReadOnlyList<TokenInfo> tokens, TokenizerState stateAfter)
    { Tokens = tokens; StateAfter = stateAfter; }
}

// Patch sent from worker → UI
public sealed record HighlightPatch(int LineNumber, IReadOnlyList<TokenInfo> Tokens);

// Public token info for rendering & minimap
public sealed class TokenInfo
{
    public SyntaxTokenType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int Length { get; set; }
}
```

### IncrementalHighlighter Class

**Responsibilities**:
- Maintain cache: `Dictionary<int, LineTokens> _cache` (line → tokens)
- Track dirty lines: `ConcurrentQueue<int> _dirtyLines` or `HashSet<int>`
- Cancelable background worker
- Patch delivery to UI thread via `SynchronizationContext.Post` or `Control.BeginInvoke`

**Public API**:
```csharp
public sealed class IncrementalHighlighter
{
    // Called by Form1 when syntax changes or file loads
    public void Reset(SyntaxDefinition syntax);

    // Called by Form1 when a range of lines changed (TextChanged)
    public void MarkDirty(int startLine, int endLine);

    // Called by Form1 to get tokens for minimap (returns cached or null)
    public IReadOnlyList<TokenInfo>? GetTokens(int lineIndex);

    // Called by Form1 when shutting down
    public void Dispose();

    // Internal: worker loop
    private async Task WorkerLoopAsync(CancellationToken token);
    private void TokenizeRange(int startLine, int endLine);
}
```

**Worker Loop**:
```csharp
private async Task WorkerLoopAsync()
{
    while (!cancellation.IsCancellationRequested)
    {
        // Wait for dirty work (AutoResetEvent or Channel)
        var batch = await _dirtyLines.Reader.ReadAsync(cancellation);
        if (batch == null) break;

        // Convert to contiguous sorted ranges
        var ranges = BatchIntoRanges(batch);
        foreach (var (start, end) in ranges)
        {
            TokenizeRange(start, end);
        }
    }
}
```

**TokenizeRange**:
```csharp
private void TokenizeRange(int startLine, int endLine)
{
    // Get starting state from cache at startLine-1, or Initial
    TokenizerState state = startLine > 0 && _cache.TryGetValue(startLine - 1, out var prev)
        ? prev.StateAfter
        : TokenizerState.Initial;

    string[] lines = _textSource.Lines; // captured snapshot; must be careful with textEditor.Lines

    for (int line = startLine; line <= endLine; line++)
    {
        if (line >= lines.Length) break;

        string text = lines[line];
        var tokens = TokenizeLine(text, state, line);
        state = tokens.StateAfter; // capture for next line

        // Update cache
        var entry = new LineTokens(tokens.Tokens, state);
        lock (_cacheLock) { _cache[line] = entry; }

        // Send patch to UI
        var patch = new HighlightPatch(line, tokens.Tokens);
        _uiContext?.Post(_ => OnPatchReady?.Invoke(this, patch), null);
    }
}
```

**TokenizerLine**:
```csharp
private (List<TokenInfo> Tokens, TokenizerState StateAfter) TokenizeLine(
    string line, TokenizerState initialState, int lineIndex)
{
    var tokens = new List<TokenInfo>();
    var colored = new bool[line.Length];

    // Apply state: if in the middle of a construct, it occupies from start
    // For InComment: whole line is comment (and maybe find end)
    // For InString: partial string at start, then tokenize rest, check if closes
    // Simpler approach: treat state as prepending an "open" region that must be matched first

    // We'll scan the line and apply regexes in priority order but respecting that
    // some regions are already occupied by continuing multi-line constructs.

    // Implementation details below.
}
```

#### Multi-line State Handling

**State machine**:
- `InComment`: means we're inside a block comment (`/* ... */`) that started on a previous line. The entire current line is comment until we find `*/`. If found, remaining text processed normally.
- `InString`: inside a verbatim `@"..."` or regular `"..."` that wasn't closed on previous line. The string continues until an unescaped `"` is found (for verbatim, `""` is escaped).
- `InCharLiteral`: similar for `'...'`.
- `InStringDelimiterPos`: tracks start index of the open delimiter for proper boundary detection.

**Line tokenization**:
```csharp
// Pseudocode
int pos = 0;
if (prev.InComment)
{
    // Find end of block comment: "*/"
    int end = line.IndexOf("*/", pos);
    if (end >= 0)
    {
        tokens.Add(Comment, 0, end+2);
        pos = end + 2;
        // now not in comment anymore; continue
    }
    else
    {
        tokens.Add(Comment, 0, line.Length);
        return (tokens, new State { InComment = true });
    }
}
// ... handle InString similarly
```

After handling continuing constructs, apply regex matching to the remaining text from `pos`:
- Preprocessor (`^#...`) - only if at line start (pos == 0) (or after state cleared)
- Comment (`//` or `/*` start) - matches from pos; if block comment starts, set InComment true and break (line consumed)
- String (`"` or `@"`) - match; set InString true, track delimiter
- Number pattern
- Keywords / Types

Note: For performance, we'll keep regexes compiled and reuse. Priority handling:
1. Preprocessor (if at start of line)
2. Block comment start (`/*`) — if found, rest of line (and potential multi-line)
3. Line comment (`//`) → remainder comment
4. String/char literals (both regular and verbatim)
5. Numbers
6. Keywords
7. Types
8. Identifiers (fallback, no color)

**Regex notes**:
- Compile regexes already exist: `GetOrCreateCompiledRegexes`
- For stateful tokenization, we might need raw scanning for strings/comments to properly handle escapes and block boundaries.

### Patch Application

**UI Thread**:
```csharp
private void ApplyHighlightPatch(object? sender, HighlightPatch patch)
{
    if (textEditor.IsDisposed) return;
    int line = patch.LineNumber;
    if (line < 0 || line >= textEditor.Lines.Length) return;

    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
    int lineLen = textEditor.Lines[line].Length;
    if (lineLen == 0) return;

    BeginUpdate(textEditor);
    textEditor.SuspendLayout();
    try
    {
        // Reset line to base color
        textEditor.SelectionStart = lineStart;
        textEditor.SelectionLength = lineLen;
        textEditor.SelectionColor = baseColor;

        // Apply colored spans
        foreach (var token in patch.Tokens)
        {
            int idx = lineStart + token.StartIndex;
            int len = token.Length;
            if (idx >= lineStart && idx + len <= lineStart + lineLen)
            {
                textEditor.SelectionStart = idx;
                textEditor.SelectionLength = len;
                textEditor.SelectionColor = GetColorForToken(token.Type);
            }
        }
    }
    finally
    {
        textEditor.ResumeLayout();
        EndUpdate(textEditor);
    }
}
```

### Fallback Mode

If `Environment.ProcessorCount == 1` or config disables workers:
- `MarkDirty` → `Task.Run` still OK, but tokenization runs on UI thread via synchronous call.
- Or simply call `HighlightVisibleRangeSynchronously` as before.

### Progressively Rendering Large Files

The request mentions progressive rendering for large files:
- Worker tokenizes visible range (+ buffer) and streams patches line-by-line as they complete, not in one batch.
- UI receives patches individually → line-by-line coloring appears progressively.
- For large files (>10k lines), we only tokenize dirty lines; scrolling triggers tokenization of newly visible lines on-demand.

## Integration Points

Replace:
- `HighlightSyntaxAsync()` (line 1843) with new flow
- `GetTokensForLine` (line 2031) → reads from cache or calls synchronous tokenizer fallback

Add to `Form1`:
- `private IncrementalHighlighter? _incrementalHighlighter;`
- `DetectSyntaxFromFile` sets up highlighter with appropriate regexes

Flow changes:
1. `TextEditor_TextChanged`:
   - `SetDirty()` (existing)
   - Determine affected line range (maybe changed line ± 1 for context)
   - `_incrementalHighlighter?.MarkDirty(changedLine, changedLine + 1);`

2. `highlightTimer.Tick`:
   - Cancel any pending work? Not needed; worker continuously processes DirtyLines
   - Just ensure `DetectSyntaxFromFile` called; then request highlight for visible range `GetVisibleLineRange()` if not already queued
   - `_incrementalHighlighter?.RequestHighlight(firstLine, lastLine);`

3. Scroll:
   - On `VScroll` or viewport change, request highlight for newly visible lines (if cache misses, they'll be tokenized)

4. `GetTokensForLine` for Minimap:
   - `if (_incrementalHighlighter?.GetTokens(line) is var tokens && tokens != null) return tokens;`
   - Else fall back to synchronous tokenize of that line (single line).

## Testing & Benchmarks

**Unit tests**:
- `TokenizerState_Transitions`: verify state evolves correctly for multi-line constructs (open/close)
- `TokenizeLine_SingleLine`: regex patterns produce correct tokens
- `TokenizeLine_MultiLine_String`: state carries across calls
- `IncrementalHighlighter_MarkDirty_ProcessesOnlyDirty`: ensure only dirty lines tokenized
- `IncrementalHighlighter_PatchesDelivered`: patch contains expected tokens
- `PatchApplication_AppliesColors`: verify RichTextBox selection colors set correctly

**Benchmarks** (using BenchmarkDotNet or custom timer):
- `Benchmark_1kLines_Incremental`: load file ~1000 lines; edit single line → measure highlight latency (< 50 ms target)
- `Benchmark_10kLines_Scroll`: scroll through large file → tokenization latency per viewport
- `Benchmark_FullRefresh_vs_Incremental`: compare old vs new

## Implementation Phases

1. **Data structures** (`IncrementalHighlighter.cs`): TokenizerState, LineTokens, HighlightPatch
2. **Stateful tokenizer**: `TokenizeLine` method, state transitions
3. **Worker infrastructure**: Dirty queue, background loop, patch channel
4. **Cache management**: Insert/replace, eviction (optional)
5. **Form1 integration**: Replace `HighlightSyntaxAsync` methods
6. **Minimap integration**: Use cache in `GetTokensForLine`
7. **Fallback mode**: toggle `_useIncremental` flag
8. **Tests** + **Benchmarks**

## Performance Targets

| Metric | Target |
|--------|--------|
| Incremental update latency (1k lines) | < 50 ms |
| Visible range highlight (100 lines) | < 10 ms |
| Token cache hit rate (scrolling) | > 95% |
| Patch delivery → apply latency | < 5 ms |
| Large file (10k lines) memory overhead | < 20 MB |

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Regex per-line overhead remains high | Pre-compile all regexes; reuse MatchCache using `Regex.Match` with start-at |
| State propagation errors for multi-line constructs | Comprehensive unit tests for state transitions |
| Memory blow-up with large files | LRU eviction (max 5000 cached lines) or limit cache to visible + margin |
| Cross-thread marshaling overhead | Batch patches per contiguous range; use `SynchronizationContext` |
| UI thread blocked applying many patches | Limit patch rate; coalesce overlapping ranges; apply 10 patches per frame via `BeginInvoke` |

---

## Design Complete — Implementation Next

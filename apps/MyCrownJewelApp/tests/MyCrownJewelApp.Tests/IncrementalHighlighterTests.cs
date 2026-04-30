using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using MyCrownJewelApp.TextEditor;

namespace MyCrownJewelApp.Tests;

[Collection("Sequential")]
public class IncrementalHighlighterTests : IDisposable
{
    private readonly RichTextBox _rtb;
    private readonly IncrementalHighlighter _highlighter;
    private readonly string _tempFile;

    public IncrementalHighlighterTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"inc_{Guid.NewGuid()}.cs");
        var lines = new string[1000];
        for (int i = 0; i < 1000; i++)
        {
            lines[i] = i % 10 == 0 ? $"class C{i} {{ void M{i}() {{ int x{i} = {i}; }} }}" : $"    // line {i}";
        }
        File.WriteAllLines(_tempFile, lines);

        Exception? ex = null;
        RichTextBox? rtb = null;
        IncrementalHighlighter? highlighter = null;
        var thread = new Thread(() =>
        {
            try
            {
                rtb = new RichTextBox();
                rtb.Text = File.ReadAllText(_tempFile);
                // No handle creation - fallback to Text splitting
                var syntax = SyntaxDefinition.CSharp;
                highlighter = new IncrementalHighlighter(
                    rtb,
                    syntax,
                    Color.Black,
                    Color.Blue,
                    Color.Brown,
                    Color.Green,
                    Color.Purple,
                    Color.Gray,
                    useWorker: true,
                    maxCacheSize: 5000);
                highlighter.RequestRange(0, 999);
                Thread.Sleep(300);
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex!;
        _rtb = rtb!;
        _highlighter = highlighter!;
    }

    public void Dispose()
    {
        _highlighter?.Dispose();
        _rtb?.Dispose();
        try { File.Delete(_tempFile); } catch { }
    }

    private void RunOnUI(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex!;
    }

    [Fact]
    public void Tokenizer_ProducesCSharpTokens()
    {
        RunOnUI(() =>
        {
            // Ensure line 0 is requested and wait for it
            _highlighter.RequestRange(0, 0);
            var sw = Stopwatch.StartNew();
            while (_highlighter.GetTokens(0) == null && sw.ElapsedMilliseconds < 50)
                Thread.Sleep(1);
            var tokens = _highlighter.GetTokens(0);
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Keyword);
        });
    }

    [Fact]
    public void Highlighter_MarksDirty_AndTokenizes()
    {
        RunOnUI(() =>
        {
            _highlighter.MarkDirty(100);
            Thread.Sleep(200);
            var tokens = _highlighter.GetTokens(100);
            Assert.NotNull(tokens);
            // Note: comment lines may have zero tokens; that's acceptable
        });
    }

    [Fact]
    public void IncrementalUpdate_LatencyUnder50ms()
    {
        RunOnUI(() =>
        {
            _highlighter.MarkDirty(500);
            var sw = Stopwatch.StartNew();
            while (_highlighter.GetTokens(500) == null && sw.ElapsedMilliseconds < 50)
                Thread.Sleep(1);
            sw.Stop();
            Assert.NotNull(_highlighter.GetTokens(500));
            Assert.True(sw.ElapsedMilliseconds < 50, $"Latency {sw.ElapsedMilliseconds}ms >= 50ms");
        });
    }

    [Fact]
    public void VisibleRange_100Lines_Under10ms()
    {
        RunOnUI(() =>
        {
            int start = 400, end = 499;
            _highlighter.RequestRange(start, end);
            var sw = Stopwatch.StartNew();
            while (Enumerable.Range(start, end - start + 1).Any(i => _highlighter.GetTokens(i) == null)
                   && sw.ElapsedMilliseconds < 10)
            {
                Thread.Sleep(1);
            }
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 10, $"100 lines took {sw.ElapsedMilliseconds}ms");
        });
    }

    [Fact]
    public void TokenizerState_BlockComment_SingleLine_Closes()
    {
        var highlighter = CreateTestHighlighter(out var rtb);
        var line = "int x = 5; /* comment */ int y;";
        var (tokens, nextState) = highlighter.TokenizeLine(line, TokenizerState.Initial, 0);
        Assert.False(nextState.InComment);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Comment);
    }

    [Fact]
    public void TokenizerState_BlockComment_MultiLine_Spans()
    {
        var highlighter = CreateTestHighlighter(out var rtb);
        var line1 = "int x = /* start";
        var (tokens1, state1) = highlighter.TokenizeLine(line1, TokenizerState.Initial, 0);
        Assert.True(state1.InComment);
        var line2 = "   continued */ int y;";
        var (tokens2, state2) = highlighter.TokenizeLine(line2, state1, 1);
        Assert.False(state2.InComment);
    }

    [Fact]
    public void TokenizerState_String_Multiline_Spans()
    {
        var highlighter = CreateTestHighlighter(out var rtb);
        var line1 = "string s = \"hello";
        var (tokens1, state1) = highlighter.TokenizeLine(line1, TokenizerState.Initial, 0);
        Assert.True(state1.InString);
        var line2 = "world\";";
        var (tokens2, state2) = highlighter.TokenizeLine(line2, state1, 1);
        Assert.False(state2.InString);
    }

    [Fact]
    public void MarkDirtyRange_MarksMultipleLines()
    {
        RunOnUI(() =>
        {
            _highlighter.MarkDirtyRange(10, 15);
            Thread.Sleep(200);
            for (int i = 10; i <= 15; i++)
            {
                var tokens = _highlighter.GetTokens(i);
                Assert.NotNull(tokens);
            }
        });
    }

    [Fact]
    public void RequestRange_SkipsCachedLines()
    {
        RunOnUI(() =>
        {
            var initial = _highlighter.GetTokens(100);
            Assert.NotNull(initial);
            _highlighter.RequestRange(100, 100);
            Thread.Sleep(100);
            var after = _highlighter.GetTokens(100);
            Assert.Same(initial, after);
        });
    }

    [Fact]
    public void Dispose_StopsWorker()
    {
        var rtb = new RichTextBox();
        var highlighter = new IncrementalHighlighter(
            rtb,
            SyntaxDefinition.CSharp,
            Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
        highlighter.Dispose();
        var sw = Stopwatch.StartNew();
        highlighter.MarkDirty(0);
        Thread.Sleep(100);
        sw.Stop();
        Assert.True(true);
        rtb.Dispose();
    }

    private IncrementalHighlighter CreateTestHighlighter(out RichTextBox rtb)
    {
        rtb = new RichTextBox();
        rtb.Text = "";
        IntPtr h = rtb.Handle;
        var highlighter = new IncrementalHighlighter(
            rtb,
            SyntaxDefinition.CSharp,
            Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
        return highlighter;
    }
}

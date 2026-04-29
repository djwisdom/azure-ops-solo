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
                var syntax = SyntaxDefinition.CSharp;
                highlighter = new IncrementalHighlighter(
                    rtb,
                    syntax,
                    Color.Black,
                    Color.Blue,
                    Color.Brown,
                    Color.Green,
                    Color.Purple,
                    Color.Gray);
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
            Thread.Sleep(150);
            var tokens = _highlighter.GetTokens(100);
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
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
}

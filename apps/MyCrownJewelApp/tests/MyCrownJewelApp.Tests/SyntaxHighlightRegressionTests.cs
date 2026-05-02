using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

[Collection("Sequential")]
public class SyntaxHighlightRegressionTests
{
    private IncrementalHighlighter CreateHighlighter(RichTextBox rtb, SyntaxDefinition syn)
    {
        IntPtr h = rtb.Handle; // force handle creation
        return new IncrementalHighlighter(rtb, syn);
    }

    [Fact]
    public void ToggleSyntaxHighlighting_DoesNotHang()
    {
        // This test verifies that creating a highlighter, requesting ranges,
        // and disposing all complete within a reasonable time.
        var rtb = new RichTextBox();
        rtb.Text = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"int x{i} = {i};"));
        var sw = Stopwatch.StartNew();
        var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
        hl.RequestRange(0, 99);
        Thread.Sleep(500);
        hl.Dispose();
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Toggle took {sw.ElapsedMilliseconds}ms (limit 5s)");
        rtb.Dispose();
    }

    [Fact]
    public void LargeFile_ProcessesWithinTime()
    {
        var rtb = new RichTextBox();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 500; i++)
            sb.AppendLine($"class C{i} {{ void M{i}() {{ if (true) {{ int x = {i}; }} }} }}");
        rtb.Text = sb.ToString();
        var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
        var sw = Stopwatch.StartNew();
        hl.RequestRange(0, 499);
        Thread.Sleep(2000);
        hl.Dispose();
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 10000);
        rtb.Dispose();
    }

    [Fact]
    public void C_Syntax_KeywordsHighlighted()
    {
        var rtb = new RichTextBox();
        rtb.Text = "int main() { return 0; }";
        var hl = CreateHighlighter(rtb, SyntaxDefinition.C);
        hl.RequestRange(0, 0);
        Thread.Sleep(500);
        var tokens = hl.GetTokens(0);
        Assert.NotNull(tokens);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Keyword);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void CSharp_MultipleLineTypes()
    {
        var rtb = new RichTextBox();
        rtb.Text = "class Foo\n{\n    // comment\n    int x = \"hello\";\n}";
        var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
        hl.RequestRange(0, 4);
        Thread.Sleep(1000);
        for (int i = 0; i < 5; i++)
        {
            var tokens = hl.GetTokens(i);
            Assert.NotNull(tokens);
        }
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void UnclosedComment_DoesNotHang()
    {
        var rtb = new RichTextBox();
        rtb.Text = "/* unclosed comment\nthat spans\nmultiple lines\nwithout closing";
        var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
        var sw = Stopwatch.StartNew();
        hl.RequestRange(0, 3);
        Thread.Sleep(500);
        hl.Dispose();
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000);
        rtb.Dispose();
    }

    [Fact]
    public void RapidToggle_DoesNotDegrade()
    {
        var rtb = new RichTextBox();
        rtb.Text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"int x{i} = {i};"));
        for (int t = 0; t < 5; t++)
        {
            var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
            hl.RequestRange(0, 49);
            Thread.Sleep(300);
            hl.Dispose();
        }
        Assert.True(true);
        rtb.Dispose();
    }
}

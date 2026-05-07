using System;
using System.Drawing;
using System.Linq;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public sealed class RainbowBracketTests
{
    [Fact]
    public void Parse_EmptyText_ReturnsEmptyResult()
    {
        var result = RainbowBracketEngine.Parse("", 1);
        Assert.Empty(result.Brackets);
        Assert.Empty(result.Pairs);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public void Parse_NoBraces_ReturnsEmptyResult()
    {
        var result = RainbowBracketEngine.Parse("hello world; int x = 42;", 1);
        Assert.Empty(result.Brackets);
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void Parse_SimplePair_MatchesCorrectly()
    {
        var result = RainbowBracketEngine.Parse("(hello)", 1);
        Assert.Equal(2, result.Brackets.Count);
        Assert.Single(result.Pairs);

        var open = result.Brackets[0];
        var close = result.Brackets[1];

        Assert.Equal('(', open.Char);
        Assert.True(open.IsOpen);
        Assert.Equal(1, open.Depth);
        Assert.Equal(BracketPairState.Matched, open.PairState);
        Assert.Equal(1, open.PairIndex);

        Assert.Equal(')', close.Char);
        Assert.False(close.IsOpen);
        Assert.Equal(1, close.Depth);
        Assert.Equal(BracketPairState.Matched, close.PairState);
        Assert.Equal(0, close.PairIndex);
    }

    [Fact]
    public void Parse_NestedBraces_CorrectDepth()
    {
        var result = RainbowBracketEngine.Parse("({[]})", 1);

        Assert.Equal(6, result.Brackets.Count);
        Assert.Equal(3, result.Pairs.Count);

        Assert.Equal(1, result.Brackets[0].Depth); // (
        Assert.Equal(2, result.Brackets[1].Depth); // {
        Assert.Equal(3, result.Brackets[2].Depth); // [
        Assert.Equal(3, result.Brackets[3].Depth); // ]
        Assert.Equal(2, result.Brackets[4].Depth); // }
        Assert.Equal(1, result.Brackets[5].Depth); // )
    }

    [Fact]
    public void Parse_MismatchedClose_DetectsMismatch()
    {
        var result = RainbowBracketEngine.Parse("(]", 1);
        Assert.Equal(2, result.Brackets.Count);

        Assert.Equal(BracketPairState.Unmatched, result.Brackets[0].PairState);
        Assert.Equal(BracketPairState.Mismatched, result.Brackets[1].PairState);
        Assert.Null(result.Brackets[1].PairIndex);
    }

    [Fact]
    public void Parse_UnmatchedOpen_LeavesUnmatched()
    {
        var result = RainbowBracketEngine.Parse("((", 1);
        Assert.Equal(2, result.Brackets.Count);
        Assert.All(result.Brackets, b => Assert.Equal(BracketPairState.Unmatched, b.PairState));
        Assert.All(result.Brackets, b => Assert.Null(b.PairIndex));
    }

    [Fact]
    public void Parse_MultipleBraceTypes_IndependentStack()
    {
        var result = RainbowBracketEngine.Parse("{[()]}", 1);
        Assert.Equal(6, result.Brackets.Count);
        Assert.Equal(3, result.Pairs.Count);

        // { depth 1, [ depth 2, ( depth 3, ) depth 3, ] depth 2, } depth 1
        Assert.Equal(1, result.Brackets[0].Depth);
        Assert.Equal(2, result.Brackets[1].Depth);
        Assert.Equal(3, result.Brackets[2].Depth);
    }

    [Fact]
    public void Parse_SkipsStringContents()
    {
        var result = RainbowBracketEngine.Parse("\"hello (world)\"", 1);
        Assert.Empty(result.Brackets);
    }

    [Fact]
    public void Parse_SkipsStringWithEscapedQuote()
    {
        var result = RainbowBracketEngine.Parse("\"hello \\\"(world)\\\"\"", 1);
        Assert.Empty(result.Brackets);
    }

    [Fact]
    public void Parse_SkipsCharLiteral()
    {
        var result = RainbowBracketEngine.Parse("ch = '(', y = ')'", 1);
        Assert.Empty(result.Brackets);
    }

    [Fact]
    public void Parse_SkipsLineComment()
    {
        var result = RainbowBracketEngine.Parse("// (hello {world}", 1);
        Assert.Empty(result.Brackets);
    }

    [Fact]
    public void Parse_SkipsBlockComment()
    {
        var result = RainbowBracketEngine.Parse("/* (hello {world} */", 1);
        Assert.Empty(result.Brackets);
    }

    [Fact]
    public void Parse_BracesOutsideComment_StillMatched()
    {
        var result = RainbowBracketEngine.Parse("x /* comment */ (y) /* another */ z", 1);
        Assert.Equal(2, result.Brackets.Count);
        Assert.Equal(BracketPairState.Matched, result.Brackets[0].PairState);
        Assert.Equal(BracketPairState.Matched, result.Brackets[1].PairState);
    }

    [Fact]
    public void Parse_CodeWithBracesAndComments()
    {
        var text = @"void M() {
    // {
    int[] arr = { 1, 2 };
}";
        var result = RainbowBracketEngine.Parse(text, 1);

        // Brackets: ( ) { (from body) [ ] { } }  (ending body brace)
        // Since brackets and [] are tracked, we get 4 pairs
        Assert.Equal(8, result.Brackets.Count);
        Assert.Equal(4, result.Pairs.Count);

        var openParen = result.Brackets[0];
        Assert.Equal('(', openParen.Char);

        var closeParen = result.Brackets[1];
        Assert.Equal(')', closeParen.Char);

        var methodBrace = result.Brackets[2];
        Assert.Equal('{', methodBrace.Char);

        var arrayBracketOpen = result.Brackets[3];
        Assert.Equal('[', arrayBracketOpen.Char);
    }

    [Fact]
    public void Parse_MismatchedTypes_DetectedCorrectly()
    {
        var result = RainbowBracketEngine.Parse("(}", 1);
        Assert.Equal(BracketPairState.Unmatched, result.Brackets[0].PairState);
        Assert.Equal(BracketPairState.Mismatched, result.Brackets[1].PairState);
        Assert.Equal('}', result.Brackets[1].Char);
    }

    [Fact]
    public void FindBraceAt_ReturnsCorrectIndex()
    {
        var result = RainbowBracketEngine.Parse("a(b)c", 1);
        int? idx = result.FindBraceAt(1);
        Assert.Equal(0, idx);

        idx = result.FindBraceAt(3);
        Assert.Equal(1, idx);

        idx = result.FindBraceAt(0);
        Assert.Null(idx);
    }

    [Fact]
    public void GetPairFor_ReturnsCorrectBracket()
    {
        var result = RainbowBracketEngine.Parse("(hello)", 1);
        var pair = result.GetPairFor(0);
        Assert.NotNull(pair);
        Assert.Equal(')', pair!.Char);
        Assert.Equal(6, pair.Position);
    }

    [Fact]
    public void DefaultPalette_HasEightColors()
    {
        Assert.Equal(8, RainbowBracketEngine.DefaultPalette.Length);
        Assert.All(RainbowBracketEngine.DefaultPalette, c => Assert.NotEqual(Color.Empty, c));
    }

    [Fact]
    public void HighContrastPalette_HasEightColors()
    {
        Assert.Equal(8, RainbowBracketEngine.HighContrastPalette.Length);
        Assert.All(RainbowBracketEngine.HighContrastPalette, c => Assert.NotEqual(Color.Empty, c));
    }

    [Fact]
    public void Parse_LargeInput_PerformsWithinBounds()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 2000; i++)
        {
            sb.AppendLine($"class Class{i} {{ void M() {{ int[] arr = {{ {i} }}; }} }}");
        }
        var text = sb.ToString();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = RainbowBracketEngine.Parse(text, 1);
        sw.Stop();

        Assert.NotEmpty(result.Brackets);
        Assert.True(result.Pairs.Count > 0);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Parsing 2000 lines took {sw.ElapsedMilliseconds}ms (expected <500ms)");
    }

    [Fact]
    public void VersionToken_ReflectedInResult()
    {
        var r1 = RainbowBracketEngine.Parse("()", 42);
        Assert.Equal(42, r1.Version);

        var r2 = RainbowBracketEngine.Parse("()", 99);
        Assert.Equal(99, r2.Version);
    }

    [Fact]
    public void Parse_DeepNesting_CorrectDepth()
    {
        string text = new string(Enumerable.Range(0, 20).SelectMany(i => new[] { '{', '[' }).ToArray())
                    + new string(Enumerable.Range(0, 20).Reverse().SelectMany(i => new[] { ']', '}' }).ToArray());

        var result = RainbowBracketEngine.Parse(text, 1);
        Assert.Equal(80, result.Brackets.Count);
        Assert.Equal(40, result.Pairs.Count);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(i * 2 + 1, result.Brackets[i * 2].Depth);     // {
            Assert.Equal(i * 2 + 2, result.Brackets[i * 2 + 1].Depth); // [
        }
    }
}

using System;
using System.Threading;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

public class ElasticTabServiceTests
{
    // Simple fixed-width measurer: 1 pixel per character
    private static int FlatMeasure(string s) => s.Length;

    // ── ComputeTabStops ─────────────────────────────────────────────────────

    [Fact] public void ComputeTabStops_EmptyLines_ReturnsEmpty()
    {
        int[] stops = ElasticTabService.ComputeTabStops(
            Array.Empty<string>(), 0, -1, FlatMeasure, CancellationToken.None);
        Assert.Empty(stops);
    }

    [Fact] public void ComputeTabStops_LinesWithNoTabs_ReturnsEmpty()
    {
        string[] lines = { "hello world", "no tabs here" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 0, 1, FlatMeasure, CancellationToken.None);
        Assert.Empty(stops);
    }

    [Fact] public void ComputeTabStops_SingleTabColumn_ReturnsSingleStop()
    {
        // "abc\tdef" — col 0 max width = 3 (abc), stop = 3 + 2 = 5
        string[] lines = { "abc\tdef" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 0, 0, FlatMeasure, CancellationToken.None);
        Assert.Single(stops);
        Assert.Equal(5, stops[0]); // 3 chars + 2px padding
    }

    [Fact] public void ComputeTabStops_TwoTabColumns_ReturnsTwoStops()
    {
        // "a\tb\tc" — col0 max=1, col1 max=1
        // stop0 = 1+2=3, stop1 = 3 + 1+2 = 6
        string[] lines = { "a\tb\tc" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 0, 0, FlatMeasure, CancellationToken.None);
        Assert.Equal(2, stops.Length);
        Assert.Equal(3, stops[0]);
        Assert.Equal(6, stops[1]);
    }

    [Fact] public void ComputeTabStops_MultipleLines_UsesMaxWidthPerColumn()
    {
        // Line 0: "ab\tx"   — col0 width 2
        // Line 1: "abcd\ty" — col0 width 4
        // Expected: stop0 = 4+2=6
        string[] lines = { "ab\tx", "abcd\ty" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 0, 1, FlatMeasure, CancellationToken.None);
        Assert.Single(stops);
        Assert.Equal(6, stops[0]);
    }

    [Fact] public void ComputeTabStops_CancelledToken_ReturnsEmpty()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        string[] lines = { "a\tb" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 0, 0, FlatMeasure, cts.Token);
        Assert.Empty(stops);
    }

    [Fact] public void ComputeTabStops_OnlyConsidersVisibleRange()
    {
        // Line 0 (not visible): "longcell\tx" — col0 width 8
        // Line 1 (visible): "a\tx"            — col0 width 1
        // With firstLine=1, lastLine=1, only line 1 counted → stop0 = 1+2=3
        string[] lines = { "longcell\tx", "a\tx" };
        int[] stops = ElasticTabService.ComputeTabStops(lines, 1, 1, FlatMeasure, CancellationToken.None);
        Assert.Single(stops);
        Assert.Equal(3, stops[0]);
    }

    // ── BuildFixedTabStops ──────────────────────────────────────────────────

    [Fact] public void BuildFixedTabStops_BasicCase_ReturnsUniformStops()
    {
        int[] stops = ElasticTabService.BuildFixedTabStops(charWidth: 8, tabSize: 4, maxStopCount: 3);
        // charWidth=8, tabSize=4 → tabPixelWidth=32
        Assert.Equal(new[] { 32, 64, 96 }, stops);
    }

    [Fact] public void BuildFixedTabStops_ZeroCharWidth_FallsBackTo8()
    {
        // charWidth 0 → defaults to 8; tabSize 2 → tabPixelWidth=16
        int[] stops = ElasticTabService.BuildFixedTabStops(charWidth: 0, tabSize: 2, maxStopCount: 2);
        Assert.Equal(new[] { 16, 32 }, stops);
    }

    [Fact] public void BuildFixedTabStops_DefaultMaxCount_Returns500Entries()
    {
        int[] stops = ElasticTabService.BuildFixedTabStops(charWidth: 8, tabSize: 4);
        Assert.Equal(500, stops.Length);
    }

    [Fact] public void BuildFixedTabStops_StopsAreMonotonicallyIncreasing()
    {
        int[] stops = ElasticTabService.BuildFixedTabStops(charWidth: 7, tabSize: 4, maxStopCount: 10);
        for (int i = 1; i < stops.Length; i++)
            Assert.True(stops[i] > stops[i - 1]);
    }
}

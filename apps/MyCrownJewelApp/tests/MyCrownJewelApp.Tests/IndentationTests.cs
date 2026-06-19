using FluentAssertions;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

/// <summary>
/// Unit tests for indentation logic.
/// </summary>
public class IndentationTests
{
    [Theory]
    [InlineData(8, 4, true, "        ")]
    [InlineData(6, 4, false, "\t  ")]
    [InlineData(0, 4, true, "")]
    [InlineData(-1, 4, true, "")]
    public void ComputeMixedIndent_ReturnsExpectedIndent(int targetColumns, int tabSize, bool insertSpaces, string expected)
    {
        IndentationHelper.ComputeMixedIndent(targetColumns, tabSize, insertSpaces).Should().Be(expected);
    }

    [Theory]
    [InlineData("\t\t  Hello", 4, false, "\t\t  ")]
    [InlineData("\t\tWorld", 4, true, "        ")]
    [InlineData("", 4, true, "")]
    [InlineData("    ", 4, true, "    ")]
    public void ComputeIndent_ReturnsExpectedIndent(string prevLine, int tabSize, bool insertSpaces, string expected)
    {
        IndentationHelper.ComputeIndent(prevLine, tabSize, insertSpaces).Should().Be(expected);
    }
}

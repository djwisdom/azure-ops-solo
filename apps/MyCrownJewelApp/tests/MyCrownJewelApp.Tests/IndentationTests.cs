using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

/// <summary>
/// Unit tests for indentation logic.
/// </summary>
public class IndentationTests
{
    [Fact]
    public void ComputeMixedIndent_WithInsertSpaces_ReturnsOnlySpaces()
    {
        // Arrange
        int targetColumns = 8;
        int tabSize = 4;
        bool insertSpaces = true;

        // Act
        string result = IndentationHelper.ComputeMixedIndent(targetColumns, tabSize, insertSpaces);

        // Assert
        Assert.Equal("        ", result); // 8 spaces
    }

    [Fact]
    public void ComputeMixedIndent_WithTabs_ReturnsTabAndSpaces()
    {
        // Arrange
        int targetColumns = 6;
        int tabSize = 4;
        bool insertSpaces = false;

        // Act
        string result = IndentationHelper.ComputeMixedIndent(targetColumns, tabSize, insertSpaces);

        // Assert
        Assert.Equal("\t  ", result); // 1 tab (4 cols) + 2 spaces
    }

    [Fact]
    public void ComputeMixedIndent_ZeroOrNegative_ReturnsEmpty()
    {
        Assert.Equal("", IndentationHelper.ComputeMixedIndent(0, 4, true));
        Assert.Equal("", IndentationHelper.ComputeMixedIndent(-1, 4, true));
    }

    [Fact]
    public void ComputeIndent_CopiesExactWhitespaceFromPrevLine()
    {
        // Arrange
        string prevLine = "\t\t  Hello"; // two tabs + two spaces = 10 visual columns (if tabSize=4)
        int tabSize = 4;
        bool insertSpaces = false; // use tabs

        // Act
        string result = IndentationHelper.ComputeIndent(prevLine, tabSize, insertSpaces);

        // When using tabs, the result should be two tabs followed by two spaces to match visual column
        Assert.Equal("\t\t  ", result);
    }

    [Fact]
    public void ComputeIndent_ConvertsTabsToSpacesWhenInsertSpacesTrue()
    {
        // Arrange
        string prevLine = "\t\tWorld"; // 2 tabs => 8 columns
        int tabSize = 4;
        bool insertSpaces = true;

        // Act
        string result = IndentationHelper.ComputeIndent(prevLine, tabSize, insertSpaces);

        // Should produce 8 spaces
        Assert.Equal("        ", result);
    }

    [Fact]
    public void ComputeIndent_EmptyPrevLine_ReturnsEmpty()
    {
        string prevLine = "";
        int tabSize = 4;
        bool insertSpaces = true;
        string result = IndentationHelper.ComputeIndent(prevLine, tabSize, insertSpaces);
        Assert.Equal("", result);
    }

    [Fact]
    public void ComputeIndent_PrevLineOnlySpaces_ReturnsSameCount()
    {
        string prevLine = "    "; // 4 spaces
        int tabSize = 4;
        bool insertSpaces = true;
        string result = IndentationHelper.ComputeIndent(prevLine, tabSize, insertSpaces);
        Assert.Equal("    ", result);
    }
}

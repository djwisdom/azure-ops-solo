using System.Collections.Generic;
using FluentAssertions;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

public class LintRulesTests
{
    [Theory]
    [InlineData(".html")]
    [InlineData(".js")]
    [InlineData(".css")]
    public void MissingSemicolonRule_IgnoresNonCSharpFiles(string extension)
    {
        var diagnostics = new List<Diagnostic>();

        new MissingSemicolonRule().Analyze("int x = 5", $"sample{extension}", diagnostics);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MissingSemicolonRule_DoesNotFlagMethodHeaderBeforeBlock()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("DoThing()\n{", "sample.cs", diagnostics);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MissingSemicolonRule_DoesNotFlagIfStatement()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("if (x)\n{", "sample.cs", diagnostics);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MissingSemicolonRule_DoesNotFlagForStatement()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("for (int i = 0; i < 10; i++)\n{", "sample.cs", diagnostics);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MissingSemicolonRule_DoesNotFlagClassInheritanceHeader()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("class Foo : Bar\n{", "sample.cs", diagnostics);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MissingSemicolonRule_FlagsActualMissingSemicolon()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("int x = 5", "sample.cs", diagnostics);

        diagnostics.Should().ContainSingle();
        diagnostics[0].RuleId.Should().Be("PFP004");
        diagnostics[0].Message.Should().Contain("Missing semicolon");
    }

    [Fact]
    public void MissingSemicolonRule_DoesNotFlagMultilineLinqContinuation()
    {
        List<Diagnostic> diagnostics = [];

        new MissingSemicolonRule().Analyze("items.Select(x => x)\n.Where(x => x > 0);", "sample.cs", diagnostics);

        diagnostics.Should().BeEmpty();
    }
}

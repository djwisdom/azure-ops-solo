using System;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

/// <summary>Tests for <see cref="BuildService.ParseDiagnostic"/>. No WinForms, no STA required.</summary>
public sealed class BuildServiceTests
{
    // ── Error lines ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseDiagnostic_ParsesCSharpErrorLine()
    {
        const string line = @"C:\src\App.cs(12,5): error CS0103: The name 'foo' does not exist in the current context";
        var d = BuildService.ParseDiagnostic(line);

        Assert.NotNull(d);
        Assert.Equal(DiagnosticSeverity.Error, d!.Severity);
        Assert.Equal("CS0103", d.Code);
        Assert.Equal(12, d.Line);
        Assert.Equal(5, d.Column);
        Assert.Contains("foo", d.Message);
        Assert.Contains(@"C:\src\App.cs", d.FilePath);
    }

    [Fact]
    public void ParseDiagnostic_ParsesCSharpWarningLine()
    {
        const string line = @"C:\src\Util.cs(7,3): warning CS8602: Dereference of a possibly null reference.";
        var d = BuildService.ParseDiagnostic(line);

        Assert.NotNull(d);
        Assert.Equal(DiagnosticSeverity.Warning, d!.Severity);
        Assert.Equal("CS8602", d.Code);
        Assert.Equal(7, d.Line);
        Assert.Equal(3, d.Column);
    }

    [Fact]
    public void ParseDiagnostic_ParsesMSBuildErrorLine_NoFile()
    {
        const string line = "MSBUILD : error MSB1001: Unknown switch.";
        var d = BuildService.ParseDiagnostic(line);

        Assert.NotNull(d);
        Assert.Equal(DiagnosticSeverity.Error, d!.Severity);
        Assert.Equal("MSB1001", d.Code);
        Assert.Null(d.FilePath);
        Assert.Equal(0, d.Line);
    }

    [Fact]
    public void ParseDiagnostic_ParsesMSBuildWarningLine_NoFile()
    {
        const string line = "MSBUILD : warning MSB3270: There was a mismatch between the processor architecture.";
        var d = BuildService.ParseDiagnostic(line);

        Assert.NotNull(d);
        Assert.Equal(DiagnosticSeverity.Warning, d!.Severity);
        Assert.Equal("MSB3270", d.Code);
        Assert.Null(d.FilePath);
    }

    // ── Non-diagnostic lines ─────────────────────────────────────────────────

    [Fact]
    public void ParseDiagnostic_ReturnsNull_ForBlankLine()
    {
        Assert.Null(BuildService.ParseDiagnostic(""));
        Assert.Null(BuildService.ParseDiagnostic("   "));
    }

    [Fact]
    public void ParseDiagnostic_ReturnsNull_ForBuildSucceededLine()
    {
        Assert.Null(BuildService.ParseDiagnostic("Build succeeded."));
    }

    [Fact]
    public void ParseDiagnostic_ReturnsNull_ForInfoLine()
    {
        Assert.Null(BuildService.ParseDiagnostic("  Determining projects to restore..."));
    }

    [Fact]
    public void ParseDiagnostic_ReturnsNull_ForRestoreLine()
    {
        Assert.Null(BuildService.ParseDiagnostic("  Restored C:\\src\\MyApp\\packages.lock.json (1.2s)."));
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseDiagnostic_HandlesPathWithSpaces()
    {
        const string line = @"C:\my project\src\App.cs(1,1): error CS0001: Something went wrong";
        var d = BuildService.ParseDiagnostic(line);

        Assert.NotNull(d);
        Assert.Equal("CS0001", d!.Code);
        Assert.Contains("my project", d.FilePath);
    }

    [Fact]
    public void ParseDiagnostic_IsCaseInsensitiveForSeverity()
    {
        const string errorLine = @"C:\f.cs(1,1): Error CS0001: msg";
        const string warnLine = @"C:\f.cs(1,1): Warning CS0001: msg";

        // Regex is Singleline but severity check is OrdinalIgnoreCase
        var d1 = BuildService.ParseDiagnostic(errorLine);
        var d2 = BuildService.ParseDiagnostic(warnLine);

        Assert.NotNull(d1);
        Assert.NotNull(d2);
        Assert.Equal(DiagnosticSeverity.Error, d1!.Severity);
        Assert.Equal(DiagnosticSeverity.Warning, d2!.Severity);
    }

    // ── BuildResult record ───────────────────────────────────────────────────

    [Fact]
    public void BuildResult_Success_HasCorrectProperties()
    {
        var r = new BuildResult(true, 0, 2, TimeSpan.FromSeconds(1.5));
        Assert.True(r.Success);
        Assert.Equal(0, r.ErrorCount);
        Assert.Equal(2, r.WarningCount);
        Assert.Equal(1.5, r.Elapsed.TotalSeconds, precision: 1);
    }

    [Fact]
    public void BuildResult_Failure_HasCorrectProperties()
    {
        var r = new BuildResult(false, 3, 1, TimeSpan.FromSeconds(5));
        Assert.False(r.Success);
        Assert.Equal(3, r.ErrorCount);
        Assert.Equal(1, r.WarningCount);
    }
}

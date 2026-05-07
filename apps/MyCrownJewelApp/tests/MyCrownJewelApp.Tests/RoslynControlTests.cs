using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MyCrownJewelApp.Pfpad.Features.RoslynControl;
using Xunit;

namespace MyCrownJewelApp.Tests;

public class AnalyzerControllerTests
{
    [Fact]
    public void BeginRun_ReturnsIncrementingVersion()
    {
        using var controller = new AnalyzerController();
        var (v1, _) = controller.BeginRun(out _);
        var (v2, _) = controller.BeginRun(out _);
        Assert.True(v2 > v1);
    }

    [Fact]
    public async Task RunAnalyzersAsync_EmptyCompilation_ReturnsEmptyResult()
    {
        using var controller = new AnalyzerController();
        var result = await controller.RunAnalyzersAsync(null, [], default);
        Assert.NotNull(result);
        Assert.True(result.Diagnostics.IsEmpty);
    }

    [Fact]
    public async Task RunAnalyzersAsync_NoAnalyzers_ReturnsCompilationDiagnostics()
    {
        using var controller = new AnalyzerController();
        var code = "class C { int x = }";
        var compilation = CreateCompilation(code);
        var result = await controller.RunAnalyzersAsync(compilation, [], default);
        Assert.NotNull(result);
        Assert.False(result.IsCancelled);
        Assert.False(result.IsFailed);
    }

    [Fact]
    public void CancelCurrentRun_PreventsStaleResults()
    {
        using var controller = new AnalyzerController();
        var (v1, token1) = controller.BeginRun(out _);
        controller.CancelCurrentRun();
        var (v2, token2) = controller.BeginRun(out _);
        Assert.True(token1.IsCancellationRequested);
        Assert.False(token2.IsCancellationRequested);
        Assert.True(v2 > v1);
    }

    [Fact]
    public async Task RunGeneratorsAsync_NoGenerators_ReturnsEmpty()
    {
        using var controller = new AnalyzerController();
        var compilation = CreateCompilation("class C { }");
        var result = await controller.RunGeneratorsAsync(compilation, [], default);
        Assert.NotNull(result);
        Assert.True(result.GeneratedTrees.IsEmpty);
    }

    [Fact]
    public async Task RunFullAnalysisAsync_CompletesSuccessfully()
    {
        using var controller = new AnalyzerController();
        var compilation = CreateCompilation("class C { }");
        var (analyzerResult, generatorResult) = await controller.RunFullAnalysisAsync(
            compilation, [], [], default);
        Assert.NotNull(analyzerResult);
        Assert.NotNull(generatorResult);
        Assert.False(analyzerResult.IsCancelled);
        Assert.False(generatorResult.IsCancelled);
    }

    [Fact]
    public void GetAnalyzersFromProject_NullProject_ReturnsEmpty()
    {
        using var controller = new AnalyzerController();
        var result = controller.GetAnalyzersFromProject(null);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void GetGeneratorsFromProject_NullProject_ReturnsEmpty()
    {
        using var controller = new AnalyzerController();
        var result = controller.GetGeneratorsFromProject(null);
        Assert.True(result.IsEmpty);
    }

    private static CSharpCompilation CreateCompilation(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return CSharpCompilation.Create("TestAssembly",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
    }
}

public class RoslynServiceTests
{
    [Fact]
    public void AreAnalyzersEnabled_DefaultsToTrue()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        using var service = new RoslynService(workspace);
        Assert.True(service.AreAnalyzersEnabled);
    }

    [Fact]
    public async Task SetAnalyzersEnabledAsync_ToggleOff_DisablesAnalyzers()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        using var service = new RoslynService(workspace);
        await service.SetAnalyzersEnabledAsync(false);
        Assert.False(service.AreAnalyzersEnabled);
    }

    [Fact]
    public async Task SetAnalyzersEnabledAsync_ToggleOnThenOff_StateChanges()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        using var service = new RoslynService(workspace);

        Assert.True(service.AreAnalyzersEnabled);
        await service.SetAnalyzersEnabledAsync(false);
        Assert.False(service.AreAnalyzersEnabled);
        await service.SetAnalyzersEnabledAsync(true);
        Assert.True(service.AreAnalyzersEnabled);
    }

    [Fact]
    public async Task GetVisualizerSnapshotAsync_NoDocument_ReturnsNull()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        using var service = new RoslynService(workspace);
        var snapshot = await service.GetVisualizerSnapshotAsync();
        Assert.Null(snapshot);
    }

    [Fact]
    public void CancelCurrentAnalysis_DoesNotThrow()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        using var service = new RoslynService(workspace);
        service.CancelCurrentAnalysis();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var workspace = new MyCrownJewelApp.Pfpad.Roslyn.RoslynWorkspaceService();
        var service = new RoslynService(workspace);
        service.Dispose();
        service.Dispose();
    }
}

public class AnalyzerRunResultTests
{
    [Fact]
    public void Empty_ReturnsDefault()
    {
        var empty = AnalyzerRunResult.Empty;
        Assert.True(empty.Diagnostics.IsEmpty);
        Assert.True(empty.CompilationDiagnostics.IsEmpty);
        Assert.Equal(0, empty.Version);
    }

    [Fact]
    public void Cancelled_SetsIsCancelled()
    {
        var result = AnalyzerRunResult.Cancelled(5);
        Assert.True(result.IsCancelled);
        Assert.Equal(5, result.Version);
    }

    [Fact]
    public void Failed_SetsIsFailed()
    {
        var result = AnalyzerRunResult.Failed(new System.Exception("test error"), 3);
        Assert.True(result.IsFailed);
        Assert.Equal("test error", result.ErrorMessage);
        Assert.Equal(3, result.Version);
    }
}

public class GeneratorOutputTests
{
    [Fact]
    public void Empty_ReturnsDefault()
    {
        var empty = GeneratorOutput.Empty;
        Assert.True(empty.GeneratedTrees.IsEmpty);
        Assert.Null(empty.UpdatedCompilation);
    }

    [Fact]
    public void Cancelled_SetsIsCancelled()
    {
        var result = GeneratorOutput.Cancelled(5);
        Assert.True(result.IsCancelled);
    }

    [Fact]
    public void Failed_SetsIsFailed()
    {
        var result = GeneratorOutput.Failed(new System.Exception("gen error"), 2);
        Assert.True(result.IsFailed);
        Assert.Equal("gen error", result.ErrorMessage);
    }
}

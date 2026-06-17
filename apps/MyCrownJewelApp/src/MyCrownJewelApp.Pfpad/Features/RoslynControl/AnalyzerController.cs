using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MyCrownJewelApp.Pfpad.Features.RoslynControl;

public sealed class AnalyzerController : IDisposable
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _currentCts;
    private int _version;
    private bool _disposed;

    private static readonly TimeSpan GeneratorTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AnalyzerTimeout = TimeSpan.FromSeconds(30);

    public int CurrentVersion => Volatile.Read(ref _version);

    public (int Version, CancellationToken Token) BeginRun(out CancellationToken externalToken)
    {
        externalToken = CancellationToken.None;
        lock (_lock)
        {
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = new CancellationTokenSource();
            var token = _currentCts.Token;
            int v = Interlocked.Increment(ref _version);
            return (v, token);
        }
    }

    public void CancelCurrentRun()
    {
        lock (_lock)
        {
            _currentCts?.Cancel();
        }
    }

    public async Task<AnalyzerRunResult> RunAnalyzersAsync(
        Compilation? compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        CancellationToken externalToken)
    {
        if (_disposed || compilation is null)
            return AnalyzerRunResult.Empty;

        var (version, ctsToken) = BeginRun(out _);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ctsToken, externalToken);
        var token = linkedCts.Token;

        try
        {
            return await Task.Run(async () =>
            {
                if (analyzers.IsDefaultOrEmpty)
                {
                    var diags = compilation.GetDiagnostics(token);
                    return new AnalyzerRunResult([.. diags], [], version);
                }

                var compilationWithAnalyzers = compilation.WithAnalyzers(
                    analyzers,
                    new CompilationWithAnalyzersOptions(
                        null!,
                        (ex, analyzer, diag) =>
                            Debug.WriteLine($"[AnalyzerController] Analyzer '{analyzer}' threw: {ex.Message}"),
                        concurrentAnalysis: true,
                        logAnalyzerExecutionTime: false));

                using var timeoutCts = new CancellationTokenSource(AnalyzerTimeout);
                using var linkedTimeout = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync(linkedTimeout.Token);

                if (token.IsCancellationRequested)
                    return AnalyzerRunResult.Cancelled(version);

                return new AnalyzerRunResult([.. diagnostics], [], version);
            }, token);
        }
        catch (OperationCanceledException)
        {
            return AnalyzerRunResult.Cancelled(version);
        }
        catch (Exception ex)
        {
            return AnalyzerRunResult.Failed(ex, version);
        }
    }

    public async Task<GeneratorOutput> RunGeneratorsAsync(
        Compilation? compilation,
        ImmutableArray<ISourceGenerator> generators,
        CancellationToken externalToken)
    {
        if (_disposed || compilation is null)
            return GeneratorOutput.Empty;

        var (version, ctsToken) = BeginRun(out _);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ctsToken, externalToken);
        var token = linkedCts.Token;

        try
        {
            return await Task.Run(() =>
            {
                if (generators.IsDefaultOrEmpty || generators.Length == 0)
                    return new GeneratorOutput([], compilation, version);

                var driver = CSharpGeneratorDriver.Create(
                    generators,
                    parseOptions: compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions);

                using var timeoutCts = new CancellationTokenSource(GeneratorTimeout);
                using var linkedTimeout = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                var resultDriver = driver.RunGenerators(compilation, linkedTimeout.Token);
                var runResult = resultDriver.GetRunResult();

                if (token.IsCancellationRequested)
                    return GeneratorOutput.Cancelled(version);

                var generatedTrees = runResult.GeneratedTrees;
                var updatedCompilation = generatedTrees.Length > 0
                    ? compilation.AddSyntaxTrees(generatedTrees)
                    : compilation;

                return new GeneratorOutput([.. generatedTrees], updatedCompilation, version);
            }, token);
        }
        catch (OperationCanceledException)
        {
            return GeneratorOutput.Cancelled(version);
        }
        catch (Exception ex)
        {
            return GeneratorOutput.Failed(ex, version);
        }
    }

    public async Task<(AnalyzerRunResult AnalyzerResult, GeneratorOutput GeneratorResult)> RunFullAnalysisAsync(
        Compilation? compilation,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        ImmutableArray<ISourceGenerator> generators,
        CancellationToken externalToken)
    {
        var analyzerResult = await RunAnalyzersAsync(compilation, analyzers, externalToken);
        var generatorResult = await RunGeneratorsAsync(compilation, generators, externalToken);
        return (analyzerResult, generatorResult);
    }

    public ImmutableArray<ISourceGenerator> GetGeneratorsFromProject(Project? project)
    {
        if (project is null) return [];
        try
        {
            return [.. project.AnalyzerReferences
                .SelectMany(ar =>
                {
                    try { return ar.GetGeneratorsForAllLanguages().OfType<ISourceGenerator>(); }
                    catch { return []; }
                })
                .OfType<ISourceGenerator>()];
        }
        catch
        {
            return [];
        }
    }

    public ImmutableArray<DiagnosticAnalyzer> GetAnalyzersFromProject(Project? project)
    {
        if (project is null) return [];
        try
        {
            return [.. project.AnalyzerReferences
                .SelectMany(ar =>
                {
                    try { return ar.GetAnalyzers("C#"); }
                    catch { return []; }
                })
                .OfType<DiagnosticAnalyzer>()];
        }
        catch
        {
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelCurrentRun();
        lock (_lock)
        {
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }
}

public sealed record AnalyzerRunResult(
    ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
    ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> CompilationDiagnostics,
    int Version,
    bool IsCancelled = false,
    bool IsFailed = false,
    string? ErrorMessage = null)
{
    public static readonly AnalyzerRunResult Empty = new([], [], 0);

    public static AnalyzerRunResult Cancelled(int version) =>
        new([], [], version, IsCancelled: true);

    public static AnalyzerRunResult Failed(Exception ex, int version) =>
        new([], [], version, IsFailed: true, ErrorMessage: ex.Message);
}

public sealed record GeneratorOutput(
    ImmutableArray<SyntaxTree> GeneratedTrees,
    Compilation? UpdatedCompilation,
    int Version,
    bool IsCancelled = false,
    bool IsFailed = false,
    string? ErrorMessage = null)
{
    public static readonly GeneratorOutput Empty = new([], null, 0);

    public static GeneratorOutput Cancelled(int version) =>
        new([], null, version, IsCancelled: true);

    public static GeneratorOutput Failed(Exception ex, int version) =>
        new([], null, version, IsFailed: true, ErrorMessage: ex.Message);
}

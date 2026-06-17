using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad.Features.RoslynControl;

public sealed class RoslynService : IDisposable
{
    private readonly RoslynWorkspaceService _workspace;
    private readonly AnalyzerController _controller;
    private readonly Lock _lock = new();
    private bool _disposed;

    private bool _analyzersEnabled = true;
    private CancellationTokenSource? _restartCts;
    private int _restartThrottleVersion;
    private DateTime _lastRestartTime = DateTime.MinValue;

    private static readonly TimeSpan RestartThrottleInterval = TimeSpan.FromMilliseconds(300);

    public event Action<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>>? DiagnosticsUpdated;
    public event Action<string>? ProgressReported;
    public event Action<string>? ErrorReported;

    public bool AreAnalyzersEnabled
    {
        get => _analyzersEnabled;
        private set => _analyzersEnabled = value;
    }

    public RoslynService(RoslynWorkspaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _controller = new AnalyzerController();
    }

    public async Task RestartAnalyzersAsync(CancellationToken externalToken = default)
    {
        if (_disposed) return;

        try
        {
            if (_workspace is null)
            {
                ReportProgress("Roslyn workspace not initialized.");
                return;
            }

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if (now - _lastRestartTime < RestartThrottleInterval)
                {
                    Debug.WriteLine("[RoslynService] Restart throttled (rapid repeat)");
                    return;
                }
                _lastRestartTime = now;
            }

            _restartCts?.Cancel();
            _restartCts?.Dispose();
            _restartCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _restartCts.Token, externalToken);
            var token = linkedCts.Token;

            int throttleVersion = Interlocked.Increment(ref _restartThrottleVersion);

            ReportProgress("Restarting analyzers and generators...");

            if (!_analyzersEnabled)
            {
                ReportProgress("Analyzers are disabled. Toggle them on first.");
                return;
            }

            Document? doc;
            try
            {
                doc = await _workspace.GetCurrentDocumentAsync();
            }
            catch (Exception wsEx)
            {
                string wsMsg = $"Workspace error in GetCurrentDocumentAsync: {wsEx.GetType().Name}: {wsEx.Message}";
                System.Diagnostics.Debug.WriteLine($"[RoslynService] {wsMsg}\n{wsEx.StackTrace}");
                ReportProgress(wsMsg);
                ErrorReported?.Invoke(wsMsg);
                return;
            }

            if (doc is null)
            {
                ReportProgress("No document open. Nothing to analyze.");
                return;
            }

            var project = doc.Project;
            var compilation = await project.GetCompilationAsync(token);

            if (compilation is null)
            {
                ReportProgress("Could not obtain compilation. Ensure the project builds.");
                return;
            }

            var analyzers = _controller.GetAnalyzersFromProject(project);
            var generators = _controller.GetGeneratorsFromProject(project);

            ReportProgress($"Running {analyzers.Length} analyzers and {generators.Length} generators...");

            var (analyzerResult, generatorResult) = await _controller.RunFullAnalysisAsync(
                compilation, analyzers, generators, token);

            if (token.IsCancellationRequested || throttleVersion != _restartThrottleVersion)
            {
                ReportProgress("Analysis cancelled (superseded by newer restart).");
                return;
            }

            if (analyzerResult.IsCancelled)
            {
                ReportProgress("Analysis cancelled.");
                return;
            }

            if (analyzerResult.IsFailed)
            {
                string msg = $"Analyzer error: {analyzerResult.ErrorMessage}";
                ReportProgress(msg);
                ErrorReported?.Invoke(msg);
                return;
            }

            if (analyzerResult.Diagnostics.Length > 0)
            {
                DiagnosticsUpdated?.Invoke(analyzerResult.Diagnostics);
            }

            if (generatorResult.IsFailed)
            {
                string msg = $"Generator error: {generatorResult.ErrorMessage}";
                ReportProgress(msg);
                ErrorReported?.Invoke(msg);
            }

            string diagCount = analyzerResult.Diagnostics.Length > 0
                ? $" Found {analyzerResult.Diagnostics.Length} diagnostics."
                : "";
            string genCount = generatorResult.GeneratedTrees.Length > 0
                ? $" Generated {generatorResult.GeneratedTrees.Length} files."
                : "";

            ReportProgress($"Analysis complete.{diagCount}{genCount}");
        }
        catch (OperationCanceledException)
        {
            ReportProgress("Analysis cancelled.");
        }
        catch (Exception ex)
        {
            string msg = $"Analysis failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[RoslynService] RestartAnalyzersAsync error: {ex}");
            ReportProgress(msg);
            ErrorReported?.Invoke(msg);
        }
    }

    public async Task<bool> SetAnalyzersEnabledAsync(bool enabled, CancellationToken externalToken = default)
    {
        if (_disposed) return false;

        _analyzersEnabled = enabled;

        if (!enabled)
        {
            CancelCurrentAnalysis();
            DiagnosticsUpdated?.Invoke([]);
            ReportProgress("Analyzers disabled.");
        }
        else
        {
            ReportProgress("Analyzers enabled. Restarting...");
            await RestartAnalyzersAsync(externalToken);
        }

        return true;
    }

    public void CancelCurrentAnalysis()
    {
        _controller.CancelCurrentRun();
        _restartCts?.Cancel();
    }

    public async Task<RoslynVisualizerSnapshot?> GetVisualizerSnapshotAsync(CancellationToken externalToken = default)
    {
        if (_disposed) return null;

        try
        {
            var doc = await _workspace.GetCurrentDocumentAsync();
            if (doc is null) return null;

            var syntaxRoot = await doc.GetSyntaxRootAsync(externalToken);
            var semanticModel = await doc.GetSemanticModelAsync(externalToken);
            var project = doc.Project;
            var compilation = await project.GetCompilationAsync(externalToken);

            ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics = [];
            if (compilation is not null)
            {
                var analyzers = _controller.GetAnalyzersFromProject(project);
                if (_analyzersEnabled && analyzers.Length > 0)
                {
                    var result = await _controller.RunAnalyzersAsync(compilation, analyzers, externalToken);
                    diagnostics = result.Diagnostics;
                }
                else
                {
                    diagnostics = compilation.GetDiagnostics(externalToken);
                }
            }

            ImmutableArray<SyntaxTree> generatedTrees = [];
            Compilation? updatedCompilation = compilation;
            if (_analyzersEnabled)
            {
                var generators = _controller.GetGeneratorsFromProject(project);
                if (generators.Length > 0)
                {
                    var genResult = await _controller.RunGeneratorsAsync(compilation, generators, externalToken);
                    generatedTrees = genResult.GeneratedTrees;
                    updatedCompilation = genResult.UpdatedCompilation ?? compilation;
                }
            }

            string sourceText = (await doc.GetTextAsync(externalToken))?.ToString() ?? "";

            return new RoslynVisualizerSnapshot(
                Document: doc,
                SyntaxRoot: syntaxRoot,
                SemanticModel: semanticModel,
                Compilation: updatedCompilation,
                Diagnostics: diagnostics,
                GeneratedTrees: generatedTrees,
                SourceText: sourceText.ToString());
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            ErrorReported?.Invoke($"Visualizer snapshot failed: {ex.Message}");
            return null;
        }
    }

    public int GetCaretPosition()
    {
        return 0;
    }

    private void ReportProgress(string message)
    {
        ProgressReported?.Invoke(message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _controller.Dispose();
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        _restartCts = null;
    }
}

public sealed record RoslynVisualizerSnapshot(
    Document Document,
    SyntaxNode? SyntaxRoot,
    SemanticModel? SemanticModel,
    Compilation? Compilation,
    ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
    ImmutableArray<SyntaxTree> GeneratedTrees,
    string SourceText);

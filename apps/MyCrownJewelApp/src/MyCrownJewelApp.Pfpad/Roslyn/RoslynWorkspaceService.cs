using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace MyCrownJewelApp.Pfpad.Roslyn;

public sealed class RoslynWorkspaceService : IRoslynWorkspace
{
    private readonly AdhocWorkspace _adhocWorkspace;
    private MSBuildWorkspace? _msbuildWorkspace;
    private readonly CancellationTokenSource _cts = new();

    // Adhoc tracking
    private ProjectId _adhocProjectId;
    private DocumentId _adhocDocumentId;

    // MSBuild tracking (only used when Kind == MSBuild)
    private ProjectId _msbuildProjectId = null!;
    private DocumentId _msbuildDocumentId = null!;

    private string _filePath = "";
    private string _currentText = "";
    private bool _disposed;

    private static readonly Lazy<(AdhocWorkspace Workspace, ProjectId ProjectId, DocumentId DocumentId)> _scratch =
        new(() =>
        {
            var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            var ws = new AdhocWorkspace(host);
            var pid = ProjectId.CreateNewId();
            ws.AddProject(ProjectInfo.Create(pid, VersionStamp.Default, "Scratch", "Scratch", "C#"));
            var did = ws.AddDocument(pid, "scratch.cs", SourceText.From("")).Id;
            return (ws, pid, did);
        });

    public WorkspaceKind Kind { get; private set; } = WorkspaceKind.Adhoc;
    public bool IsReady { get; private set; }
    public event Action? WorkspaceUpgraded;

    private Solution CurrentSolution => Kind == WorkspaceKind.MSBuild
        ? _msbuildWorkspace!.CurrentSolution
        : _adhocWorkspace.CurrentSolution;

    private ProjectId CurrentProjectId => Kind == WorkspaceKind.MSBuild
        ? _msbuildProjectId
        : _adhocProjectId;

    private DocumentId CurrentDocumentId => Kind == WorkspaceKind.MSBuild
        ? _msbuildDocumentId
        : _adhocDocumentId;

    public RoslynWorkspaceService()
    {
        _adhocWorkspace = _scratch.Value.Workspace;
        _adhocProjectId = _scratch.Value.ProjectId;
        _adhocDocumentId = _scratch.Value.DocumentId;
    }

    public void OpenDocument(string filePath)
    {
        _filePath = filePath;
        _currentText = File.Exists(filePath) ? File.ReadAllText(filePath) : "";

        // Create a fresh scratch document
        _adhocProjectId = ProjectId.CreateNewId();
        _adhocWorkspace.AddProject(ProjectInfo.Create(
            _adhocProjectId, VersionStamp.Default, "Scratch", "Scratch", "C#"));
        _adhocDocumentId = _adhocWorkspace.AddDocument(
            _adhocProjectId, "scratch.cs", SourceText.From("")).Id;

        UpdateAdhocText(_currentText);
        AddMetadataReferences();
        Kind = WorkspaceKind.Adhoc;
        IsReady = true;
        _ = TryUpgradeToMSBuild(filePath);
    }

    public void UpdateDocumentText(string newText)
    {
        _currentText = newText;
        try
        {
            if (Kind == WorkspaceKind.MSBuild && _msbuildWorkspace is not null)
                UpdateMsBuildText(newText);
            else
                UpdateAdhocText(newText);
        }
        catch { }
    }

    public void CloseDocument()
    {
        _filePath = "";
        _currentText = "";
        IsReady = false;
        Kind = WorkspaceKind.Adhoc;
    }

    public async Task<ImmutableArray<ClassifiedSpan>> GetClassificationsAsync(int start, int length)
    {
        if (_disposed || !IsReady) return [];
        try
        {
            var solution = CurrentSolution;
            var doc = solution.GetDocument(CurrentDocumentId);
            if (doc is null) return [];
            var text = await doc.GetTextAsync(_cts.Token);
            var span = new TextSpan(start, Math.Min(length, text.Length - start));
            var result = await Classifier.GetClassifiedSpansAsync(doc, span, _cts.Token);
            return [.. result];
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<ISymbol>> FindSymbolsAsync(string name)
    {
        if (_disposed || !IsReady) return [];
        try
        {
            var project = CurrentSolution.GetProject(CurrentProjectId);
            if (project is null) return [];
            var results = await SymbolFinder.FindSourceDeclarationsAsync(project, name, false, _cts.Token);
            return [.. results];
        }
        catch { return []; }
    }

    public async Task<ISymbol?> FindSymbolAtPositionAsync(int position)
    {
        if (_disposed || !IsReady) return null;
        try
        {
            var doc = CurrentSolution.GetDocument(CurrentDocumentId);
            return await ResolveSymbolAtPosition(doc, position);
        }
        catch { return null; }
    }

    public async Task<ISymbol?> FindSourceDefinitionAsync(ISymbol symbol)
    {
        if (_disposed || !IsReady) return null;
        try
        {
            return await SymbolFinder.FindSourceDefinitionAsync(
                symbol, CurrentSolution, _cts.Token);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> GetRoslynDiagnosticsAsync()
    {
        if (_disposed || !IsReady) return [];
        try
        {
            var project = CurrentSolution.GetProject(CurrentProjectId);
            if (project is null) return [];
            var compilation = await project.GetCompilationAsync(_cts.Token);
            return compilation?.GetDiagnostics(_cts.Token) ?? [];
        }
        catch { return []; }
    }

    public async Task<Solution> PrepareRenameAsync(ISymbol symbol, string newName)
    {
        if (_disposed) return CurrentSolution;
        try
        {
#pragma warning disable CS0618
            return await Renamer.RenameSymbolAsync(
                CurrentSolution, symbol, newName, null, _cts.Token);
#pragma warning restore CS0618
        }
        catch { return CurrentSolution; }
    }

    public async Task<SignatureHelpData?> GetSignatureHelpAsync(int position)
    {
        if (_disposed || !IsReady) return null;
        try
        {
            var doc = CurrentSolution.GetDocument(CurrentDocumentId);
            if (doc is null) return null;
            var model = await doc.GetSemanticModelAsync(_cts.Token);
            if (model is null) return null;
            var tree = await doc.GetSyntaxTreeAsync(_cts.Token);
            if (tree is null) return null;
            var root = await tree.GetRootAsync(_cts.Token);
            var token = root.FindToken(position);
            if (token == default) return null;
            var symbolInfo = model.GetSymbolInfo(token.Parent!, _cts.Token);
            var method = symbolInfo.Symbol as IMethodSymbol
                ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (method is null) return null;

            var paramNames = method.Parameters.Select(p => p.Name).ToList();
            var paramDoc = method.Parameters.Select(p =>
            {
                var xml = p.GetDocumentationCommentXml();
                if (string.IsNullOrEmpty(xml)) return "";
                try { return System.Xml.Linq.XElement.Parse(xml).Value?.Trim() ?? ""; }
                catch { return ""; }
            }).ToList();

            int currentParam = 0;
            string text = _currentText;
            int depth = 0;
            for (int i = Math.Min(position - 1, text.Length - 1); i >= 0; i--)
            {
                char c = text[i];
                if (c == ')') depth++;
                else if (c == '(') { if (depth == 0) break; depth--; }
                else if (c == ',' && depth == 0) currentParam++;
            }

            var fmt = new SymbolDisplayFormat(
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                parameterOptions: SymbolDisplayParameterOptions.IncludeName
                    | SymbolDisplayParameterOptions.IncludeType
                    | SymbolDisplayParameterOptions.IncludeParamsRefOut,
                memberOptions: SymbolDisplayMemberOptions.IncludeParameters
                    | SymbolDisplayMemberOptions.IncludeType,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);
            string sig = method.ToDisplayString(fmt);

            return new SignatureHelpData(
                sig,
                paramNames,
                Math.Min(currentParam, Math.Max(0, method.Parameters.Length - 1)),
                paramDoc);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<ReferencedSymbol>> FindReferencesAsync(ISymbol symbol)
    {
        if (_disposed || !IsReady) return [];
        try
        {
            var refs = await SymbolFinder.FindReferencesAsync(
                symbol, CurrentSolution, _cts.Token);
            return [.. refs];
        }
        catch { return []; }
    }

    public async Task<CompletionData?> GetCompletionAsync(int position, char triggerChar = '\0')
    {
        if (_disposed || !IsReady) return null;
        try
        {
            var doc = CurrentSolution.GetDocument(CurrentDocumentId);
            if (doc is null) return null;

            var completionService = CompletionService.GetService(doc);
            if (completionService is null) return null;

            var trigger = CompletionTrigger.Invoke;
            var completions = await completionService.GetCompletionsAsync(
                doc, position, trigger, cancellationToken: _cts.Token);

            if (completions is null || completions.ItemsList.Count == 0) return null;

            var items = completions.ItemsList.Select(item => new CompletionItem(
                item.DisplayText,
                item.Properties.TryGetValue("InsertionText", out var insertText) ? insertText : item.DisplayText,
                item.InlineDescription,
                item.Tags.FirstOrDefault() ?? "unknown"
            )).ToList();

            return new CompletionData(items, completions.Span.Start, completions.Span.Length);
        }
        catch { return null; }
    }

    public string? GetXmlDocumentation(ISymbol symbol)
    {
        if (_disposed) return null;
        try { return symbol.GetDocumentationCommentXml(); }
        catch { return null; }
    }

    public string GetSymbolDisplayString(ISymbol symbol)
    {
        if (_disposed) return "";
        try { return symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat); }
        catch { return symbol?.Name ?? ""; }
    }

    public ISymbol? GetSymbolAtLineColumn(string filePath, int line, int column)
    {
        if (_disposed || !IsReady) return null;
        try
        {
            var doc = CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (doc is null) return null;

            var tree = doc.GetSyntaxTreeAsync(_cts.Token)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            var root = tree?.GetRootAsync(_cts.Token)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            if (root is null) return null;
            var text = tree!.GetTextAsync(_cts.Token)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            var pos = text.Lines.GetPosition(new LinePosition(line - 1, column - 1));
            var token = root.FindToken(pos);
            if (token == default) return null;
            var model = doc.GetSemanticModelAsync(_cts.Token)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return model?.GetDeclaredSymbol(token.Parent!)
                ?? model?.GetSymbolInfo(token.Parent!, _cts.Token).Symbol;
        }
        catch { return null; }
    }

    // --- Private helpers ---

    private void UpdateAdhocText(string text)
    {
        var solution = _adhocWorkspace.CurrentSolution
            .WithDocumentText(_adhocDocumentId, SourceText.From(text));
        _adhocWorkspace.TryApplyChanges(solution);
        _adhocDocumentId = _adhocWorkspace.CurrentSolution
            .GetDocument(_adhocDocumentId)?.Id ?? _adhocDocumentId;
    }

    private void UpdateMsBuildText(string text)
    {
        var solution = _msbuildWorkspace!.CurrentSolution
            .WithDocumentText(_msbuildDocumentId, SourceText.From(text));
        _msbuildWorkspace!.TryApplyChanges(solution);
        _msbuildDocumentId = _msbuildWorkspace.CurrentSolution
            .GetDocument(_msbuildDocumentId)?.Id ?? _msbuildDocumentId;
    }

    private void AddMetadataReferences()
    {
        try
        {
            var refs = ReferenceAssemblyProvider.Load();
            var project = _adhocWorkspace.CurrentSolution.GetProject(_adhocProjectId);
            if (project is null) return;
            var existingPaths = project.MetadataReferences
                .OfType<PortableExecutableReference>()
                .Select(r => r.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var toAdd = refs.Where(r => !existingPaths.Contains(r.FilePath)).ToList();
            if (toAdd.Count == 0) return;
            var newSolution = project.AddMetadataReferences(toAdd).Solution;
            _adhocWorkspace.TryApplyChanges(newSolution);
        }
        catch { }
    }

    private async Task TryUpgradeToMSBuild(string filePath)
    {
        try
        {
            var csproj = FindProjectOrSolution(Path.GetDirectoryName(filePath) ?? "");
            if (csproj is null) return;

            var msbuild = MSBuildWorkspace.Create();
            msbuild.LoadMetadataForReferencedProjects = true;

            Solution solution;
            if (csproj.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                solution = await msbuild.OpenSolutionAsync(csproj, cancellationToken: _cts.Token);
            else
                solution = (await msbuild.OpenProjectAsync(csproj, cancellationToken: _cts.Token)).Solution;

            if (msbuild.Diagnostics.Any(d => d.Kind == WorkspaceDiagnosticKind.Failure))
            {
                msbuild.Dispose();
                return;
            }

            var docInMsbuild = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d =>
                    string.Equals(d.FilePath, _filePath, StringComparison.OrdinalIgnoreCase));

            if (docInMsbuild is null)
            {
                msbuild.Dispose();
                return;
            }

            _msbuildWorkspace = msbuild;
            _msbuildProjectId = docInMsbuild.Project.Id;
            _msbuildDocumentId = docInMsbuild.Id;
            Kind = WorkspaceKind.MSBuild;
            WorkspaceUpgraded?.Invoke();
        }
        catch
        {
            // Graceful degradation — stay on Adhoc
        }
    }

    private static async Task<ISymbol?> ResolveSymbolAtPosition(Document? doc, int position)
    {
        if (doc is null) return null;
        var tree = await doc.GetSyntaxTreeAsync();
        if (tree is null) return null;
        var root = await tree.GetRootAsync();
        var token = root.FindToken(position);
        if (token == default) return null;
        var model = await doc.GetSemanticModelAsync();
        return model?.GetDeclaredSymbol(token.Parent!)
            ?? model?.GetSymbolInfo(token.Parent!).Symbol;
    }

    private static string? FindProjectOrSolution(string dir)
    {
        var di = new DirectoryInfo(dir);
        while (di is not null)
        {
            var csproj = Directory.EnumerateFiles(di.FullName, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj is not null) return csproj;
            var sln = Directory.EnumerateFiles(di.FullName, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (sln is not null) return sln;
            di = di.Parent;
        }
        return null;
    }

    public Task<Document?> GetCurrentDocumentAsync()
    {
        if (_disposed || !IsReady) return Task.FromResult<Document?>(null);
        try
        {
            var solution = CurrentSolution;
            var doc = solution.GetDocument(CurrentDocumentId);
            if (doc is null) return Task.FromResult<Document?>(null);
            return Task.FromResult<Document?>(doc.WithText(SourceText.From(_currentText)));
        }
        catch { return Task.FromResult<Document?>(null); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsReady = false;
        _cts.Cancel();
        _cts.Dispose();
    }
}

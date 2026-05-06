using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.FindSymbols;

namespace MyCrownJewelApp.Pfpad.Roslyn;

public interface IRoslynWorkspace : IDisposable
{
    WorkspaceKind Kind { get; }
    bool IsReady { get; }
    event Action? WorkspaceUpgraded;

    void OpenDocument(string filePath);
    void UpdateDocumentText(string newText);
    void CloseDocument();

    Task<ImmutableArray<ClassifiedSpan>> GetClassificationsAsync(int start, int length);
    Task<IReadOnlyList<ISymbol>> FindSymbolsAsync(string name);
    Task<ISymbol?> FindSymbolAtPositionAsync(int position);
    Task<ISymbol?> FindSourceDefinitionAsync(ISymbol symbol);
    Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> GetRoslynDiagnosticsAsync();
    Task<Solution> PrepareRenameAsync(ISymbol symbol, string newName);
    Task<SignatureHelpData?> GetSignatureHelpAsync(int position);
    Task<IReadOnlyList<ReferencedSymbol>> FindReferencesAsync(ISymbol symbol);
    string? GetXmlDocumentation(ISymbol symbol);
    string GetSymbolDisplayString(ISymbol symbol);
    ISymbol? GetSymbolAtLineColumn(string filePath, int line, int column);
}

public sealed record SignatureHelpData(
    string Signature,
    IReadOnlyList<string> ParameterNames,
    int CurrentParameter,
    IReadOnlyList<string> ParameterDocumentation
);

public enum WorkspaceKind { None, Adhoc, MSBuild }

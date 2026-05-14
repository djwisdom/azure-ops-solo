using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.FindSymbols;

using Microsoft.CodeAnalysis.Text;

namespace MyCrownJewelApp.Pfpad.Roslyn;

public interface IRoslynWorkspace : IDisposable
{
    WorkspaceKind Kind { get; }
    bool IsReady { get; }
    event Action? WorkspaceUpgraded;

    void OpenDocument(string filePath);
    void UpdateDocumentText(string newText);
    void CloseDocument();

    Task<Document?> GetCurrentDocumentAsync();

    Task<ImmutableArray<ClassifiedSpan>> GetClassificationsAsync(int start, int length);
    Task<IReadOnlyList<ISymbol>> FindSymbolsAsync(string name);
    Task<ISymbol?> FindSymbolAtPositionAsync(int position);
    Task<ISymbol?> FindSourceDefinitionAsync(ISymbol symbol);
    Task<IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic>> GetRoslynDiagnosticsAsync();
    Task<Solution> PrepareRenameAsync(ISymbol symbol, string newName);
    Task<SignatureHelpData?> GetSignatureHelpAsync(int position);
    Task<IReadOnlyList<ReferencedSymbol>> FindReferencesAsync(ISymbol symbol);
    Task<CompletionData?> GetCompletionAsync(int position, char triggerChar = '\0');
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

public sealed record CompletionItem(
    string DisplayText,
    string InsertText,
    string? Description,
    string Kind
);

public sealed record CompletionData(
    IReadOnlyList<CompletionItem> Items,
    int StartPosition,
    int Length
);

public enum WorkspaceKind { None, Adhoc, MSBuild }

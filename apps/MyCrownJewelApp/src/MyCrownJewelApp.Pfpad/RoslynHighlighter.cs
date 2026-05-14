using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace MyCrownJewelApp.Pfpad;

public sealed class RoslynHighlighter
{
    private readonly Roslyn.RoslynWorkspaceService _workspace;

    public RoslynHighlighter(Roslyn.RoslynWorkspaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task<IReadOnlyList<TokenInfo>> GetTokensForLineAsync(int lineIndex, string lineText, int lineStartPosition)
    {
        var tokens = new List<TokenInfo>();

        if (!_workspace.IsReady)
        {
            return tokens;
        }

        try
        {
            // Get classifications for the line
            var classifications = await _workspace.GetClassificationsAsync(lineStartPosition, lineText.Length);

            foreach (var classifiedSpan in classifications)
            {
                if (classifiedSpan.TextSpan.Start < lineStartPosition || classifiedSpan.TextSpan.End > lineStartPosition + lineText.Length)
                    continue; // Skip spans outside this line

                var startInLine = classifiedSpan.TextSpan.Start - lineStartPosition;
                var length = classifiedSpan.TextSpan.Length;

                if (startInLine < 0 || startInLine + length > lineText.Length)
                    continue; // Invalid span

                var tokenType = MapClassificationTypeToTokenType(classifiedSpan.ClassificationType);
                if (tokenType != SyntaxTokenType.Identifier) // Unknown maps to Identifier
                {
                    tokens.Add(new TokenInfo
                    {
                        Type = tokenType,
                        Text = lineText.Substring(startInLine, length),
                        StartIndex = startInLine,
                        Length = length
                    });
                }
            }

            // Sort tokens by position
            tokens.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));

            // Merge overlapping tokens (keep the first one)
            for (int i = tokens.Count - 1; i > 0; i--)
            {
                var current = tokens[i];
                var previous = tokens[i - 1];

                if (current.StartIndex < previous.StartIndex + previous.Length)
                {
                    // Overlap - remove the current one
                    tokens.RemoveAt(i);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Roslyn classification error for line {lineIndex}: {ex.Message}");
        }

        return tokens;
    }

    private SyntaxTokenType MapClassificationTypeToTokenType(string classificationType) => classificationType switch
    {
        ClassificationTypeNames.Keyword => SyntaxTokenType.Keyword,
        ClassificationTypeNames.ClassName => SyntaxTokenType.Type,
        ClassificationTypeNames.StructName => SyntaxTokenType.Type,
        ClassificationTypeNames.InterfaceName => SyntaxTokenType.Type,
        ClassificationTypeNames.EnumName => SyntaxTokenType.Type,
        ClassificationTypeNames.DelegateName => SyntaxTokenType.Type,
        ClassificationTypeNames.TypeParameterName => SyntaxTokenType.Type,
        ClassificationTypeNames.ModuleName => SyntaxTokenType.Type,

        ClassificationTypeNames.MethodName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.PropertyName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.FieldName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.EventName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.ParameterName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.LocalName => SyntaxTokenType.Identifier,
        ClassificationTypeNames.NamespaceName => SyntaxTokenType.Identifier,

        ClassificationTypeNames.StringLiteral => SyntaxTokenType.String,
        ClassificationTypeNames.NumericLiteral => SyntaxTokenType.Number,

        ClassificationTypeNames.Comment => SyntaxTokenType.Comment,
        ClassificationTypeNames.XmlDocCommentText => SyntaxTokenType.Comment,
        ClassificationTypeNames.XmlDocCommentDelimiter => SyntaxTokenType.Comment,
        ClassificationTypeNames.XmlDocCommentName => SyntaxTokenType.Comment,
        ClassificationTypeNames.XmlDocCommentAttributeName => SyntaxTokenType.Comment,

        ClassificationTypeNames.PreprocessorKeyword => SyntaxTokenType.Preprocessor,
        ClassificationTypeNames.PreprocessorText => SyntaxTokenType.Preprocessor,

        _ => SyntaxTokenType.Identifier
    };
}
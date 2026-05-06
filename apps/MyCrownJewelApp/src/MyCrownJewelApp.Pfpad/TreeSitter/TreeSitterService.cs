using TreeSitter;

namespace MyCrownJewelApp.Pfpad.TreeSitter;

public sealed class TreeSitterService : IDisposable
{
    private readonly Dictionary<string, Language> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _extLangMap;
    private bool _disposed;

    public TreeSitterService()
    {
        _extLangMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".c"] = "C",
            [".h"] = "C",
            [".cpp"] = "C++",
            [".cxx"] = "C++",
            [".cc"] = "C++",
            [".c++"] = "C++",
            [".hpp"] = "C++",
            [".hxx"] = "C++",
            [".hh"] = "C++",
            [".js"] = "JavaScript",
            [".jsx"] = "JavaScript",
            [".mjs"] = "JavaScript",
            [".cjs"] = "JavaScript",
            [".ts"] = "TypeScript",
            [".tsx"] = "TSX",
            [".py"] = "Python",
            [".rb"] = "Ruby",
            [".go"] = "Go",
            [".rs"] = "Rust",
            [".java"] = "Java",
            [".php"] = "PHP",
            [".sh"] = "Bash",
            [".bash"] = "Bash",
            [".zsh"] = "Bash",
            [".html"] = "HTML",
            [".htm"] = "HTML",
            [".css"] = "CSS",
            [".scss"] = "CSS",
            [".json"] = "JSON",
            [".swift"] = "Swift",
            [".kt"] = "Kotlin",
        };
    }

    public bool CanHandle(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        return ext is not null && _extLangMap.ContainsKey(ext);
    }

    public List<TokenInfo> GetTokens(string text, string filePath)
    {
        var tokens = new List<TokenInfo>();
        var lang = GetOrCreateLanguage(filePath);
        if (lang is null) return tokens;

        try
        {
            using var parser = new Parser(lang);
            using var tree = parser.Parse(text);
            if (tree is null) return tokens;
            CollectTokens(tree.RootNode, tokens, text);
            tokens.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            return tokens;
        }
        catch { return tokens; }
    }

    public List<SymbolLocation> GetSymbols(string text, string filePath)
    {
        var symbols = new List<SymbolLocation>();
        var lang = GetOrCreateLanguage(filePath);
        if (lang is null) return symbols;

        try
        {
            using var parser = new Parser(lang);
            using var tree = parser.Parse(text);
            if (tree is null) return symbols;
            CollectDeclarations(tree.RootNode, symbols, text, filePath);
            return symbols;
        }
        catch { return symbols; }
    }

    private Language? GetOrCreateLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (!_extLangMap.TryGetValue(ext, out var langName)) return null;

        if (_languages.TryGetValue(langName, out var existing))
            return existing;

        try
        {
            var lang = new Language(langName);
            _languages[langName] = lang;
            return lang;
        }
        catch
        {
            _languages[langName] = null!;
            return null;
        }
    }

    private static void CollectTokens(Node node, List<TokenInfo> tokens, string text)
    {
        if (node.Children.Count == 0)
        {
            var start = (int)node.StartIndex;
            var len = (int)(node.EndIndex - node.StartIndex);
            if (len <= 0) return;
            var type = MapNodeKind(node.Type);
            tokens.Add(new TokenInfo { StartIndex = start, Length = len, Type = type, Text = node.Text ?? "" });
        }
        else
        {
            // Check if this node is a meaningful container (comment, string)
            // that should be treated as a single token rather than decomposed
            var nodeType = MapNodeKind(node.Type);
            if (nodeType == SyntaxTokenType.Comment || nodeType == SyntaxTokenType.String || nodeType == SyntaxTokenType.Preprocessor)
            {
                var start = (int)node.StartIndex;
                var len = (int)(node.EndIndex - node.StartIndex);
                if (len > 0)
                    tokens.Add(new TokenInfo { StartIndex = start, Length = len, Type = nodeType, Text = node.Text ?? "" });
            }
            else
            {
                foreach (var child in node.Children)
                    CollectTokens(child, tokens, text);
            }
        }
    }

    private static void CollectDeclarations(Node node, List<SymbolLocation> symbols, string text, string filePath)
    {
        var kind = node.Type;
        var declKind = ParseDeclarationKind(kind);
        if (declKind != SymbolKind.Unknown && node.NamedChildren.Count >= 1)
        {
            var nameNode = FindNameNode(node);
            if (nameNode is not null)
            {
                var pos = nameNode.StartPosition;
                int lineIdx = (int)pos.Row;
                string lineText = "";
                var lines = text.Split('\n');
                if (lineIdx >= 0 && lineIdx < lines.Length)
                    lineText = lines[lineIdx].TrimEnd('\r').Trim();

                symbols.Add(new SymbolLocation
                {
                    File = filePath,
                    Line = lineIdx + 1,
                    Column = (int)pos.Column + 1,
                    Name = nameNode.Text ?? "",
                    Kind = declKind,
                    Context = lineText
                });
            }
        }

        foreach (var child in node.NamedChildren)
            CollectDeclarations(child, symbols, text, filePath);
    }

    private static Node? FindNameNode(Node node)
    {
        // Try named children first
        foreach (var child in node.NamedChildren)
        {
            var k = child.Type;
            if (k == "identifier" || k == "property_identifier" || k == "name")
                return child;
        }
        // Try unnamed children (for C++ class/struct name patterns)
        foreach (var child in node.Children)
        {
            var k = child.Type;
            if (k == "identifier" || k == "property_identifier" || k == "name")
                return child;
        }
        return null;
    }

    private static SyntaxTokenType MapNodeKind(string kind)
    {
        return kind switch
        {
            "comment" or "block_comment" or "line_comment" => SyntaxTokenType.Comment,
            "string_literal" or "string" or "char_literal" or "raw_string_literal"
                or "interpreted_string_literal" or "template_string" or "heredoc"
                or "string_content" or "string_fragment" => SyntaxTokenType.String,
            "integer_literal" or "float_literal" or "number_literal"
                or "real_literal" or "imaginary_literal" or "hex_literal"
                or "octal_literal" or "binary_literal" => SyntaxTokenType.Number,
            "escape_sequence" or "interpolation" => SyntaxTokenType.Identifier,
            _ when kind.StartsWith("keyword") => SyntaxTokenType.Keyword,
            _ when kind.StartsWith("preproc") || kind.StartsWith("pre_") => SyntaxTokenType.Preprocessor,
            "type_identifier" or "primitive_type" => SyntaxTokenType.Type,
            "field_identifier" or "property_identifier"
                or "shorthand_property_identifier" or "shorthand_field_identifier" => SyntaxTokenType.Identifier,
            _ => SyntaxTokenType.Identifier
        };
    }

    private static SymbolKind ParseDeclarationKind(string kind)
    {
        return kind switch
        {
            "function_definition" or "function_declaration" or "function_signature"
                or "method_definition" or "method_signature" or "arrow_function"
                or "lambda_expression" or "anonymous_function" => SymbolKind.Method,
            "class_specifier" or "class_definition" or "class_declaration"
                or "class" or "record_declaration" => SymbolKind.Class,
            "struct_specifier" or "struct_definition" or "struct" => SymbolKind.Struct,
            "interface_specifier" or "interface_definition" or "interface_declaration"
                or "interface" => SymbolKind.Interface,
            "enum_specifier" or "enum_definition" or "enum" => SymbolKind.Enum,
            "field_declaration" or "field_definition" => SymbolKind.Field,
            "property_declaration" or "property_definition" => SymbolKind.Property,
            "variable_declaration" or "lexical_declaration"
                or "variable_definition" => SymbolKind.Variable,
            "type_alias" or "typedef" or "using_declaration" => SymbolKind.Type,
            _ => SymbolKind.Unknown
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var lang in _languages.Values)
            lang?.Dispose();
        _languages.Clear();
    }
}

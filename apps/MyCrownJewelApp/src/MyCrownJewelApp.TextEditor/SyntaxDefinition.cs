using System.Text.RegularExpressions;

namespace MyCrownJewelApp.TextEditor;

/// <summary>
/// Immutable definition for a language's syntax: keyword lists, regex patterns for tokens.
/// </summary>
public sealed record SyntaxDefinition
{
    public string Name { get; init; } = string.Empty;
    public string[] Extensions { get; init; } = Array.Empty<string>();
    public string[] Keywords { get; init; } = Array.Empty<string>();
    public string[] Types { get; init; } = Array.Empty<string>();
    public string[] Preprocessor { get; init; } = Array.Empty<string>();
    public string StringPattern { get; init; } = string.Empty;
    public string CommentPattern { get; init; } = string.Empty;
    public string NumberPattern { get; init; } = string.Empty;
    public string[] MultiLineCommentPatterns { get; init; } = Array.Empty<string>();

    // Built-in definitions
    public static SyntaxDefinition CSharp => new()
    {
        Name = "C#",
        Extensions = new[] { ".cs", ".csx" },
        Keywords = new[]
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while", "async", "await", "record", "init"
        },
        Types = new[] { "string", "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "char", "void", "object", "dynamic" },
        Preprocessor = new[] { "#define", "#undef", "#if", "#elif", "#else", "#endif", "#line", "#error", "#warning", "#region", "#endregion", "#pragma" },
        StringPattern = @"""([^""\\]|\\.)*""|@""([^""]|"""")*""",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b"
    };

    public static SyntaxDefinition C => new()
    {
        Name = "C",
        Extensions = new[] { ".c", ".h" },
        Keywords = new[]
        {
            "auto", "break", "case", "char", "const", "continue", "default", "do", "double", "else",
            "enum", "extern", "float", "for", "goto", "if", "inline", "int", "long", "register",
            "restrict", "return", "short", "signed", "sizeof", "static", "struct", "switch", "typedef",
            "union", "unsigned", "void", "volatile", "while", "_Bool", "_Complex", "_Imaginary"
        },
        Types = new[] { "int", "char", "float", "double", "void", "short", "long", "signed", "unsigned", "size_t", "ptrdiff_t", "wchar_t" },
        Preprocessor = new[] { "#define", "#undef", "#include", "#if", "#ifdef", "#ifndef", "#elif", "#else", "#endif", "#line", "#error", "#pragma" },
        StringPattern = @"""([^""\\]|\\.)*""",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b"
    };

    public static SyntaxDefinition Cpp => new()
    {
        Name = "C++",
        Extensions = new[] { ".cpp", ".cxx", ".cc", ".c++", ".hpp", ".hxx", ".hh", ".h++", ".h" },
        Keywords = new[]
        {
            "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool", "break",
            "case", "catch", "char", "char8_t", "char16_t", "char32_t", "class", "compl", "concept",
            "const", "consteval", "constexpr", "constinit", "const_cast", "continue", "co_await",
            "co_return", "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast",
            "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto",
            "if", "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "not", "not_eq",
            "nullptr", "operator", "or", "or_eq", "private", "protected", "public", "register",
            "reinterpret_cast", "requires", "return", "short", "signed", "sizeof", "static",
            "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local",
            "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned", "using",
            "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq"
        },
        Types = new[] { "int", "char", "float", "double", "void", "bool", "short", "long", "signed", "unsigned", "size_t", "ptrdiff_t", "wchar_t", "char8_t", "char16_t", "char32_t" },
        Preprocessor = new[] { "#define", "#undef", "#include", "#if", "#ifdef", "#ifndef", "#elif", "#else", "#endif", "#line", "#error", "#pragma" },
        StringPattern = @"""([^""\\]|\\.)*""",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b"
    };

    public static SyntaxDefinition Bicep => new()
    {
        Name = "Bicep",
        Extensions = new[] { ".bicep" },
        Keywords = new[]
        {
            "param", "var", "resource", "module", "output", "import", "targetScope", "func", "type",
            "if", "for", "else", "in", "existing", "resource", "module", "output", "parameter",
            "description", "default", "allowed", "minLength", "maxLength", "minValue", "maxValue",
            "secure", "unique", "metadata", "tags", "name", "location", "sku", "kind", "plan",
            "properties", "identity", "apiVersion", "scope"
        },
        Types = new[] { "string", "bool", "int", "float", "array", "object", "null" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition Terraform => new()
    {
        Name = "Terraform",
        Extensions = new[] { ".tf", ".tfvars", ".tfstate" },
        Keywords = new[]
        {
            "resource", "data", "variable", "output", "module", "provider", "terraform",
            "locals", "count", "for_each", "depends_on", "lifecycle", "provisioner",
            "connection", "backend", "required_providers", "required_version", "configuration",
            "dynamic", "in", "null", "true", "false", "element", "file", "jsonencode", "lookup",
            "merge", "concat", "distinct", "flatten", "length", "upper", "lower", "title",
            "startswith", "endswith", "contains", "replace", "regex", "split", "trim", "trimspace"
        },
        Types = new[] { "string", "number", "bool", "list", "map", "set", "object", "any" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""",
        CommentPattern = @"#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition Yaml => new()
    {
        Name = "YAML",
        Extensions = new[] { ".yaml", ".yml", ".yaml.example", ".yml.example" },
        Keywords = new[]
        {
            "true", "false", "null", "yes", "no", "on", "off",
            "!!str", "!!int", "!!float", "!!bool", "!!null", "!!seq", "!!map", "!!set", "!!omap", "!!pairs"
        },
        Types = new[] { "str", "int", "float", "bool", "null", "seq", "map", "set", "omap", "pairs" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'",
        CommentPattern = @"#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition Html => new()
    {
        Name = "HTML",
        Extensions = new[] { ".html", ".htm", ".xhtml" },
        Keywords = new[] { "html", "head", "body", "div", "span", "p", "a", "img", "script", "style", "table", "tr", "td", "th", "ul", "ol", "li", "h1", "h2", "h3", "h4", "h5", "h6", "form", "input", "button", "link", "meta", "title" },
        Types = Array.Empty<string>(),
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'",
        CommentPattern = @"<!--.*?-->",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition Css => new()
    {
        Name = "CSS",
        Extensions = new[] { ".css", ".scss", ".sass", ".less" },
        Keywords = new[] { "color", "background", "font", "margin", "padding", "border", "width", "height", "display", "position", "flex", "grid", "box-shadow", "transition", "transform", "animation", "media", "import", "url" },
        Types = new[] { "px", "em", "rem", "vh", "vw", "%", "auto", "inherit", "initial", "unset" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'",
        CommentPattern = @"/\*.*?\*/",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?(px|em|rem|vh|vw|%|pt|pc|in|cm|mm|ex|ch|s|ms|deg|rad|grad|turn)?\b"
    };

    public static SyntaxDefinition JavaScript => new()
    {
        Name = "JavaScript",
        Extensions = new[] { ".js", ".jsx", ".mjs", ".cjs" },
        Keywords = new[]
        {
            "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else", "export", "extends", "finally", "for", "function", "if", "import", "in", "instanceof", "new", "return", "super", "switch", "this", "throw", "try", "typeof", "var", "void", "while", "with", "let", "static", "yield", "async", "await", "of"
        },
        Types = new[] { "string", "number", "boolean", "undefined", "null", "symbol", "bigint", "object", "function", "Array", "Promise", "Map", "Set", "WeakMap", "WeakSet", "Date", "RegExp", "Error", "JSON" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'|`([^`\\]|\\.)*`",
        CommentPattern = @"//.*$|/\*.*?\*/",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+\.?\d*(?:[eE][+-]?\d+)?\b"
    };

    public static SyntaxDefinition Json => new()
    {
        Name = "JSON",
        Extensions = new[] { ".json", ".jsonc" },
        Keywords = new[] { "true", "false", "null" },
        Types = Array.Empty<string>(),
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""",
        CommentPattern = @"//.*$", // .jsonc supports line comments
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" }, // .jsonc supports block comments
        NumberPattern = @"-?\b\d+(\.\d+)?(?:[eE][+-]?\d+)?\b"
    };

    public static SyntaxDefinition PowerShell => new()
    {
        Name = "PowerShell",
        Extensions = new[] { ".ps1", ".psm1", ".psd1", ".psrc", ".pssc" },
        Keywords = new[]
        {
            "begin", "break", "catch", "class", "continue", "data", "define", "do", "dynamicparam",
            "else", "elseif", "end", "enum", "exit", "filter", "finally", "for", "foreach", "from",
            "function", "hidden", "if", "in", "inline", "namespace", "param", "process", "return",
            "switch", "throw", "trap", "try", "using", "var", "while"
        },
        Types = new[] { "string", "int", "long", "double", "bool", "decimal", "array", "hashtable", "object", "void", "datetime", "char", "byte", "single", "single[]", "psobject" },
        Preprocessor = new[] { "#requires", "#comment", "#commentbasedhelp", "#region", "#endregion", "#if", "#else", "#endif" },
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'",
        CommentPattern = "#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition Bash => new()
    {
        Name = "Bash",
        Extensions = new[] { ".sh", ".bash", ".zsh", ".ksh", ".csh", ".tcsh", ".fish" },
        Keywords = new[]
        {
            "if", "then", "else", "elif", "fi", "case", "esac", "for", "select", "while", "until",
            "do", "done", "in", "function", "time", "coproc", "select", "continue", "break",
            "return", "exit", "export", "readonly", "set", "unset", "shift", "source", "alias",
            "bg", "fg", "jobs", "disown", "wait", "kill", "trap", "command", "builtin", "enable",
            "type", "hash", "help", "history", "pushd", "popd", "dirs", "cd", "pwd", "echo",
            "printf", "read", "test", "expr", "getopts", "true", "false"
        },
        Types = new[] { "string", "int", "bool", "array", "assoc", "null" },
        Preprocessor = new[] { "#!", "#if", "#else", "#elif", "#endif", "#define", "#undef", "#include" },
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'",
        CommentPattern = "#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+(\.\d+)?\b"
    };

    public static SyntaxDefinition? GetDefinitionForFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return CSharp;

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" or ".csx" => CSharp,
            ".c" or ".h" => C,
            ".cpp" or ".cxx" or ".cc" or ".c++" or ".hpp" or ".hxx" or ".hh" or ".h++" => Cpp,
            ".bicep" => Bicep,
            ".tf" or ".tfvars" or ".tfstate" => Terraform,
            ".yaml" or ".yml" => Yaml,
            ".html" or ".htm" or ".xhtml" => Html,
            ".css" or ".scss" or ".sass" or ".less" => Css,
            ".js" or ".jsx" or ".mjs" or ".cjs" => JavaScript,
            ".json" or ".jsonc" => Json,
            ".ps1" or ".psm1" or ".psd1" or ".psrc" or ".pssc" => PowerShell,
            ".sh" or ".bash" or ".zsh" or ".ksh" or ".csh" or ".tcsh" or ".fish" => Bash,
            _ => null
        };
    }
}

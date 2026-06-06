using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

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
    public string[] DefinitionPatterns { get; init; } = Array.Empty<string>();

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
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b",
        DefinitionPatterns = new[]
        {
            @"(public|private|protected|internal|static|virtual|abstract|override|partial|readonly|async|unsafe)?\s*(class|struct|interface|enum|record)\s+(?<name>\w+)",
            @"(public|private|protected|internal|static|virtual|abstract|override|partial|async|unsafe)?\s*\w+\s+(?<name>\w+)\s*\([^)]*\)\s*[{<]",
            @"(public|private|protected|internal|static|readonly)\s+\w+\s+(?<name>\w+)\s*[{=;]",
            @"(delegate|event)\s+\w+\s+(?<name>\w+)",
            @"namespace\s+(?<name>[\w.]+)",
            @"using\s+(?<name>[\w.]+)\s*=",
        }
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
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b",
        DefinitionPatterns = new[]
        {
            @"(static|extern|inline|const|typedef|struct|union|enum)?\s*\w+\s+(?<name>\w+)\s*\([^)]*\)\s*\{",
            @"(typedef|struct|union|enum)\s+(?<name>\w+)\s*\{",
            @"#define\s+(?<name>\w+)",
        }
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
        NumberPattern = @"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b",
        DefinitionPatterns = new[]
        {
            @"(public|private|protected)?\s*(class|struct|interface|enum|union)\s+(?<name>\w+)\s*(:\s*\w+)?\s*\{",
            @"(\w+|<[\w\s,>]+>)*\s+(?<name>\w+)\s*\([^)]*\)\s*(const|override|final|\{)?",
            @"(typedef|using)\s+\w+\s+(?<name>\w+)\s*[=;]",
            @"#define\s+(?<name>\w+)",
            @"template\s*<[^>]+>\s*(class|struct|typename)?\s*(?<name>\w+)",
        }
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
        NumberPattern = @"\b\d+\.?\d*(?:[eE][+-]?\d+)?\b",
        DefinitionPatterns = new[]
        {
            @"(export\s+)?(async\s+)?function\s+(?<name>\w+)",
            @"(export\s+)?(async\s+)?\(?\w+\)?\s*=>\s*\{",
            @"(export\s+)?class\s+(?<name>\w+)",
            @"(export\s+)?(const|let|var)\s+(?<name>\w+)\s*[=:]",
            @"(export\s+)?interface\s+(?<name>\w+)",
            @"(export\s+)?(abstract\s+)?class\s+(?<name>\w+)",
            @"(get|set)\s+(?<name>\w+)\s*\(\)",
        }
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
        NumberPattern = @"\b\d+(\.\d+)?\b",
        DefinitionPatterns = new[]
        {
            @"function\s+(?<name>\w+)",
            @"(filter|workflow)\s+(?<name>\w+)",
            @"class\s+(?<name>\w+)",
            @"(public|private|protected|internal)?\s*(function|filter)\s+(?<name>\w+)",
        }
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
        NumberPattern = @"\b\d+(\.\d+)?\b",
        DefinitionPatterns = new[]
        {
            @"^function\s+(?<name>\w+)",
            @"^(?<name>\w+)\s*\(\s*\)\s*\{",
            @"^(?<name>\w+)\s*=\s*\(\)\s*\{",
        }
    };

    public static SyntaxDefinition Python => new()
    {
        Name = "Python",
        Extensions = new[] { ".py", ".pyw", ".pyi" },
        Keywords = new[]
        {
            "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
            "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in",
            "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while",
            "with", "yield", "True", "False", "None"
        },
        Types = new[] { "int", "float", "complex", "str", "bytes", "bytearray", "memoryview", "bool", "list", "tuple", "set", "frozenset", "dict", "type", "object", "Exception", "BaseException" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"(""""""[\s\S]*?""""""|'''[\s\S]*?'''|""([^""\\]|\\.)*""|'[^']*')",
        CommentPattern = @"#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+\.?\d*([eEjJ])?\b",
        DefinitionPatterns = new[]
        {
            @"(async\s+)?def\s+(?<name>\w+)",
            @"class\s+(?<name>\w+)",
            @"^(?<name>\w+)\s*=",
        }
    };

    public static SyntaxDefinition TypeScript => new()
    {
        Name = "TypeScript",
        Extensions = new[] { ".ts", ".tsx", ".mts", ".cts" },
        Keywords = new[]
        {
            "abstract", "as", "async", "await", "break", "case", "catch", "class", "const",
            "continue", "debugger", "declare", "default", "delete", "do", "else", "enum",
            "export", "extends", "finally", "for", "from", "function", "if", "implements",
            "import", "in", "instanceof", "interface", "let", "namespace", "new", "null",
            "of", "override", "package", "private", "protected", "public", "readonly",
            "return", "satisfies", "static", "super", "switch", "this", "throw", "try",
            "type", "typeof", "undefined", "var", "void", "while", "with", "yield"
        },
        Types = new[] { "string", "number", "boolean", "any", "unknown", "never", "void", "null", "undefined", "symbol", "bigint", "object", "Array", "Promise", "Map", "Set", "Record", "Partial", "Required", "Readonly", "Pick", "Omit" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^']*'|`([^`\\]|\\.)*`",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*(?:[eE][+-]?\d+)?\b",
        DefinitionPatterns = new[]
        {
            @"(export\s+)?(async\s+)?function\s+(?<name>\w+)",
            @"(export\s+)?class\s+(?<name>\w+)",
            @"(export\s+)?interface\s+(?<name>\w+)",
            @"(export\s+)?type\s+(?<name>\w+)\s*=",
            @"(export\s+)?enum\s+(?<name>\w+)",
            @"(export\s+)?(const|let|var)\s+(?<name>\w+)\s*[=:]",
        }
    };

    public static SyntaxDefinition Go => new()
    {
        Name = "Go",
        Extensions = new[] { ".go" },
        Keywords = new[]
        {
            "break", "case", "chan", "const", "continue", "default", "defer", "else",
            "fallthrough", "for", "func", "go", "goto", "if", "import", "interface",
            "map", "package", "range", "return", "select", "struct", "switch", "type", "var"
        },
        Types = new[] { "bool", "byte", "complex64", "complex128", "error", "float32", "float64", "int", "int8", "int16", "int32", "int64", "rune", "string", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr", "any" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|`[^`]*`",
        CommentPattern = @"//.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*([eE][+-]?\d+)?\b",
        DefinitionPatterns = new[]
        {
            @"func\s+(?:\(\w+\s+[*]?\w+\)\s+)?(?<name>\w+)\s*\(",
            @"type\s+(?<name>\w+)\s+(struct|interface)",
            @"(const|var)\s+(?<name>\w+)",
        }
    };

    public static SyntaxDefinition Ruby => new()
    {
        Name = "Ruby",
        Extensions = new[] { ".rb", ".rake", ".gemspec", ".rbw" },
        Keywords = new[]
        {
            "alias", "and", "begin", "break", "case", "class", "def", "defined", "do",
            "else", "elsif", "end", "ensure", "false", "for", "if", "in", "module",
            "next", "nil", "not", "or", "redo", "rescue", "retry", "return", "self",
            "super", "then", "true", "undef", "unless", "until", "when", "while", "yield",
            "attr_accessor", "attr_reader", "attr_writer", "require", "require_relative",
            "include", "extend", "prepend", "raise", "puts", "print", "p"
        },
        Types = new[] { "Integer", "Float", "String", "Symbol", "Array", "Hash", "Range", "Regexp", "Proc", "Method", "NilClass", "TrueClass", "FalseClass", "Object", "BasicObject", "Module", "Class", "Exception", "IO", "File" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"""([^""\\]|\\.)*""|'[^'\\]*'",
        CommentPattern = @"#.*$",
        MultiLineCommentPatterns = Array.Empty<string>(),
        NumberPattern = @"\b\d+\.?\d*\b",
        DefinitionPatterns = new[]
        {
            @"def\s+(?<name>\w+[\?!]?)",
            @"(class|module)\s+(?<name>\w+)",
            @"^(?<name>\w+)\s*=",
        }
    };

    public static SyntaxDefinition Sql => new()
    {
        Name = "SQL",
        Extensions = new[] { ".sql" },
        Keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "CROSS", "ON",
            "GROUP", "BY", "ORDER", "HAVING", "INSERT", "INTO", "UPDATE", "SET", "DELETE",
            "CREATE", "DROP", "ALTER", "TABLE", "VIEW", "INDEX", "AS", "NULL", "NOT", "AND",
            "OR", "IN", "LIKE", "BETWEEN", "EXISTS", "DISTINCT", "COUNT", "SUM", "AVG",
            "MIN", "MAX", "CASE", "WHEN", "THEN", "ELSE", "END", "WITH", "UNION", "ALL",
            "EXCEPT", "INTERSECT", "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "UNIQUE",
            "DEFAULT", "CONSTRAINT", "IF", "BEGIN", "COMMIT", "ROLLBACK", "TRANSACTION",
            "EXEC", "EXECUTE", "PROCEDURE", "FUNCTION", "TRIGGER", "CURSOR", "DECLARE",
            "GRANT", "REVOKE", "TRUNCATE", "MERGE", "OVER", "PARTITION", "ROW_NUMBER",
            "RANK", "DENSE_RANK", "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE",
            // lowercase variants
            "select", "from", "where", "join", "left", "right", "inner", "outer", "on",
            "group", "by", "order", "having", "insert", "into", "update", "set", "delete",
            "create", "drop", "alter", "table", "view", "index", "as", "null", "not", "and",
            "or", "in", "like", "between", "exists", "distinct", "count", "sum", "avg",
            "min", "max", "case", "when", "then", "else", "end", "with", "union", "all"
        },
        Types = new[] { "INT", "INTEGER", "BIGINT", "SMALLINT", "TINYINT", "DECIMAL", "NUMERIC", "FLOAT", "REAL", "DOUBLE", "CHAR", "VARCHAR", "TEXT", "NCHAR", "NVARCHAR", "NTEXT", "DATE", "TIME", "DATETIME", "DATETIME2", "TIMESTAMP", "BOOLEAN", "BOOL", "BIT", "BLOB", "CLOB", "UUID", "JSON", "JSONB" },
        Preprocessor = Array.Empty<string>(),
        StringPattern = @"'([^'\\]|\\.)*'",
        CommentPattern = @"--.*$",
        MultiLineCommentPatterns = new[] { @"/\*.*?\*/" },
        NumberPattern = @"\b\d+\.?\d*\b",
        DefinitionPatterns = new[]
        {
            @"CREATE\s+(OR\s+REPLACE\s+)?(TABLE|VIEW|INDEX|PROCEDURE|FUNCTION|TRIGGER)\s+(?<name>\w+)",
            @"create\s+(or\s+replace\s+)?(table|view|index|procedure|function|trigger)\s+(?<name>\w+)",
        }
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
            ".ts" or ".tsx" or ".mts" or ".cts" => TypeScript,
            ".json" or ".jsonc" => Json,
            ".ps1" or ".psm1" or ".psd1" or ".psrc" or ".pssc" => PowerShell,
            ".sh" or ".bash" or ".zsh" or ".ksh" or ".csh" or ".tcsh" or ".fish" => Bash,
            ".py" or ".pyw" or ".pyi" => Python,
            ".go" => Go,
            ".rb" or ".rake" or ".gemspec" or ".rbw" => Ruby,
            ".sql" => Sql,
            _ => null
        };
    }
}

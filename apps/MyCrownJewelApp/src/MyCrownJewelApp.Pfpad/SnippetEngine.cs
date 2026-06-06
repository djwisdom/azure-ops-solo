using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class SnippetEngine
{
    private readonly Dictionary<string, Snippet> _snippets = new(StringComparer.OrdinalIgnoreCase);
    private readonly RichTextBox _editor;
    private string _currentLang = "any";

    /// <param name="Language">Language tag: "any", "cs", "c", or "cpp".</param>
    public sealed record Snippet(string Prefix, string Description, string Body, string Language = "any");

    public SnippetEngine(RichTextBox editor)
    {
        _editor = editor;
        RegisterBuiltIn();
    }

    /// <summary>Updates the active language from the currently open file extension.</summary>
    public void SetFileExtension(string? ext)
    {
        _currentLang = ext?.ToLowerInvariant() switch
        {
            ".cs" or ".csx"                    => "cs",
            ".c"                               => "c",
            ".cpp" or ".cc" or ".cxx" or ".c++" => "cpp",
            ".h" or ".hpp" or ".hxx"            => "cpp",
            ".bicep"                            => "bicep",
            ".tf" or ".tfvars" or ".hcl"        => "tf",
            _                                  => "any"
        };
    }

    private void RegisterBuiltIn()
    {
        // ── C# snippets ───────────────────────────────────────────────────────────
        Add("for",      "for loop",                        "for (int i = 0; i < $0; i++)\r\n{\r\n}",                                                        "cs");
        Add("foreach",  "foreach loop",                    "foreach (var item in $0)\r\n{\r\n}",                                                             "cs");
        Add("while",    "while loop",                      "while ($0)\r\n{\r\n}",                                                                           "cs");
        Add("do",       "do-while loop",                   "do\r\n{\r\n$0\r\n} while ($1);",                                                                 "cs");
        Add("try",      "try-catch",                       "try\r\n{\r\n$0\r\n}\r\ncatch (Exception ex)\r\n{\r\n}",                                          "cs");
        Add("tryf",     "try-finally",                     "try\r\n{\r\n$0\r\n}\r\nfinally\r\n{\r\n}",                                                       "cs");
        Add("if",       "if statement",                    "if ($0)\r\n{\r\n}",                                                                              "cs");
        Add("ife",      "if-else",                         "if ($0)\r\n{\r\n}\r\nelse\r\n{\r\n}",                                                            "cs");
        Add("else",     "else statement",                  "else\r\n{\r\n$0\r\n}",                                                                           "cs");
        Add("switch",   "switch statement",                "switch ($0)\r\n{\r\n    case $1:\r\n        break;\r\n    default:\r\n        break;\r\n}",        "cs");
        Add("class",    "class declaration",               "public class $0\r\n{\r\n}",                                                                      "cs");
        Add("struct",   "struct declaration",              "public struct $0\r\n{\r\n}",                                                                     "cs");
        Add("interface","interface declaration",           "public interface I$0\r\n{\r\n}",                                                                 "cs");
        Add("enum",     "enum declaration",                "public enum $0\r\n{\r\n}",                                                                       "cs");
        Add("prop",     "auto-property",                   "public $0 { get; set; }",                                                                       "cs");
        Add("propg",    "auto-property (private set)",     "public $0 { get; private set; }",                                                               "cs");
        Add("propfull", "full property with backing field","private $1 _$0;\r\npublic $1 $0\r\n{\r\n    get => _$0;\r\n    set => _$0 = value;\r\n}",       "cs");
        Add("ctor",     "constructor",                     "public $0()\r\n{\r\n}",                                                                          "cs");
        Add("main",     "Main method",                     "static void Main(string[] args)\r\n{\r\n    $0\r\n}",                                            "cs");
        Add("console",  "Console.WriteLine",               "Console.WriteLine($0);",                                                                        "cs");
        Add("cw",       "Console.WriteLine (short)",       "Console.WriteLine($0);",                                                                        "cs");

        // ── C snippets ────────────────────────────────────────────────────────────
        Add("main",     "main function",          "#include <stdio.h>\r\n\r\nint main(int argc, char *argv[])\r\n{\r\n    $0\r\n    return 0;\r\n}",         "c");
        Add("for",      "for loop",               "for (int i = 0; i < $0; i++) {\r\n    \r\n}",                                                            "c");
        Add("fori",     "for loop with index",    "for (size_t i = 0; i < $0; i++) {\r\n    \r\n}",                                                         "c");
        Add("while",    "while loop",             "while ($0) {\r\n    \r\n}",                                                                              "c");
        Add("do",       "do-while loop",          "do {\r\n    $0\r\n} while ($1);",                                                                        "c");
        Add("switch",   "switch statement",       "switch ($0) {\r\n    case $1:\r\n        break;\r\n    default:\r\n        break;\r\n}",                  "c");
        Add("struct",   "struct definition",      "typedef struct $0 {\r\n    $1\r\n} $0;",                                                                 "c");
        Add("typedef",  "typedef struct",         "typedef struct {\r\n    $0\r\n} $1;",                                                                    "c");
        Add("printf",   "printf statement",       "printf(\"$0\\n\");",                                                                                     "c");
        Add("printf_err","fprintf to stderr",     "fprintf(stderr, \"$0\\n\");",                                                                            "c");
        Add("malloc_free","malloc + free pair",   "void *$0 = malloc($1);\r\nif ($0 == NULL) { /* handle error */ }\r\n// ...\r\nfree($0);",               "c");
        Add("guard",    "header include guard",   "#ifndef $0_H\r\n#define $0_H\r\n\r\n$1\r\n\r\n#endif /* $0_H */",                                       "c");
        Add("if",       "if statement",           "if ($0) {\r\n    \r\n}",                                                                                 "c");
        Add("ife",      "if-else",                "if ($0) {\r\n    \r\n} else {\r\n    \r\n}",                                                             "c");

        // ── C++ snippets ──────────────────────────────────────────────────────────
        Add("main",     "main function",          "#include <iostream>\r\n\r\nint main(int argc, char* argv[])\r\n{\r\n    $0\r\n    return 0;\r\n}",        "cpp");
        Add("class",    "class with ctor/dtor",   "class $0 {\r\npublic:\r\n    $0();\r\n    ~$0();\r\n\r\nprivate:\r\n    $1\r\n};",                       "cpp");
        Add("struct",   "struct definition",      "struct $0 {\r\n    $1\r\n};",                                                                            "cpp");
        Add("template", "function template",      "template<typename $0>\r\n$1 $2($0 $3)\r\n{\r\n    $4\r\n}",                                             "cpp");
        Add("vec",      "std::vector",            "std::vector<$0> $1;",                                                                                    "cpp");
        Add("map",      "std::unordered_map",     "std::unordered_map<$0, $1> $2;",                                                                         "cpp");
        Add("uptr",     "unique_ptr",             "std::unique_ptr<$0> $1 = std::make_unique<$0>($2);",                                                     "cpp");
        Add("sptr",     "shared_ptr",             "std::shared_ptr<$0> $1 = std::make_shared<$0>($2);",                                                     "cpp");
        Add("lambda",   "lambda expression",      "[&]($0) {\r\n    $1\r\n}",                                                                               "cpp");
        Add("fore",     "range-based for",        "for (const auto& $0 : $1) {\r\n    $2\r\n}",                                                             "cpp");
        Add("ctor",     "constructor definition", "$0::$0($1)\r\n{\r\n    $2\r\n}",                                                                         "cpp");
        Add("guard",    "header include guard",   "#pragma once\r\n\r\n$0",                                                                                 "cpp");
        Add("ns",       "namespace block",        "namespace $0 {\r\n\r\n$1\r\n\r\n} // namespace $0",                                                     "cpp");
        Add("cout",     "std::cout",              "std::cout << $0 << '\\n';",                                                                              "cpp");
        Add("cerr",     "std::cerr",              "std::cerr << $0 << '\\n';",                                                                              "cpp");
        Add("try",      "try-catch",              "try {\r\n    $0\r\n} catch (const std::exception& ex) {\r\n    std::cerr << ex.what() << '\\n';\r\n}",   "cpp");
        Add("assert",   "static_assert",          "static_assert($0, \"$1\");",                                                                             "cpp");
        Add("nodiscard","[[nodiscard]] function", "[[nodiscard]] $0 $1($2)\r\n{\r\n    $3\r\n}",                                                            "cpp");
        Add("for",      "for loop",               "for (int i = 0; i < $0; ++i) {\r\n    \r\n}",                                                            "cpp");
        Add("if",       "if statement",           "if ($0) {\r\n    \r\n}",                                                                                 "cpp");

        // ── General snippets (language-agnostic) ──────────────────────────────────
        Add("todo",  "TODO comment",  "// TODO: $0");
        Add("hack",  "HACK comment",  "// HACK: $0");
        Add("note",  "NOTE comment",  "// NOTE: $0");

        // ── Bicep snippets ────────────────────────────────────────────────────────
        Add("param",       "parameter declaration",       "param $0 $1",                                                                                                         "bicep");
        Add("parms",       "parameter with decorators",   "@description('$1')\r\n@minLength(1)\r\nparam $0 string",                                                               "bicep");
        Add("var",         "variable declaration",        "var $0 = $1",                                                                                                         "bicep");
        Add("out",         "output declaration",          "output $0 string = $1",                                                                                               "bicep");
        Add("res",         "resource block",              "resource $0 '$1@$2' = {\r\n  name: '$3'\r\n  location: resourceGroup().location\r\n  properties: {\r\n    $4\r\n  }\r\n}","bicep");
        Add("mod",         "module reference",            "module $0 './$1.bicep' = {\r\n  name: '$2'\r\n  params: {\r\n    $3\r\n  }\r\n}",                                      "bicep");
        Add("existing",    "existing resource reference", "resource $0 '$1@$2' existing = {\r\n  name: '$3'\r\n}",                                                                "bicep");
        Add("loop",        "resource loop (for)",         "resource $0 '$1@$2' = [for item in $3: {\r\n  name: '${item.name}'\r\n  location: resourceGroup().location\r\n  properties: {\r\n    $4\r\n  }\r\n}]","bicep");
        Add("conditional", "conditional resource",        "resource $0 '$1@$2' = if ($3) {\r\n  name: '$4'\r\n  location: resourceGroup().location\r\n  properties: {\r\n    $5\r\n  }\r\n}","bicep");
        Add("scope",       "targetScope declaration",     "targetScope = '$0'",                                                                                                   "bicep");
        Add("decorators",  "common param decorators",     "@description('$1')\r\n@allowed([$2])\r\n@minLength(1)\r\n@maxLength(64)\r\nparam $0 string",                            "bicep");
        Add("rg",          "resourceGroup() location",    "resourceGroup().location",                                                                                             "bicep");
        Add("concat",      "string interpolation",        "'${$0}'",                                                                                                              "bicep");

        // ── Terraform / HCL snippets ──────────────────────────────────────────────
        Add("tfblock",   "terraform {} block",         "terraform {\r\n  required_version = \">= $0\"\r\n  required_providers {\r\n    $1 = {\r\n      source  = \"$2\"\r\n      version = \"~> $3\"\r\n    }\r\n  }\r\n}",                "tf");
        Add("prov",      "provider block",             "provider \"$0\" {\r\n  $1\r\n}",                                                                                          "tf");
        Add("res",       "resource block",             "resource \"$0\" \"$1\" {\r\n  $2\r\n}",                                                                                   "tf");
        Add("data",      "data source block",          "data \"$0\" \"$1\" {\r\n  $2\r\n}",                                                                                       "tf");
        Add("var",       "variable block",             "variable \"$0\" {\r\n  type        = $1\r\n  description = \"$2\"\r\n  default     = $3\r\n}",                             "tf");
        Add("out",       "output block",               "output \"$0\" {\r\n  description = \"$1\"\r\n  value       = $2\r\n}",                                                    "tf");
        Add("mod",       "module block",               "module \"$0\" {\r\n  source  = \"$1\"\r\n  version = \"$2\"\r\n\r\n  $3\r\n}",                                            "tf");
        Add("locals",    "locals block",               "locals {\r\n  $0 = $1\r\n}",                                                                                             "tf");
        Add("backend",   "backend block",              "backend \"$0\" {\r\n  $1\r\n}",                                                                                           "tf");
        Add("lifecycle", "lifecycle block",            "lifecycle {\r\n  prevent_destroy       = $0\r\n  ignore_changes        = [$1]\r\n  replace_triggered_by  = [$2]\r\n}",   "tf");
        Add("dynblock",  "dynamic block",              "dynamic \"$0\" {\r\n  for_each = $1\r\n  content {\r\n    $2\r\n  }\r\n}",                                                "tf");
        Add("foreach",   "for_each meta-argument",     "for_each = { for $0 in $1 : $0.key => $0 }",                                                                             "tf");
        Add("tags",      "common tags map",            "tags = {\r\n  environment = var.environment\r\n  project     = var.project_name\r\n  managed_by  = \"terraform\"\r\n}", "tf");
        Add("tftag",     "single tag attribute",       "$0 = var.$1",                                                                                                            "tf");
        Add("depends",   "depends_on block",           "depends_on = [$0]",                                                                                                      "tf");
    }

    public void Add(string prefix, string description, string body, string language = "any")
    {
        // For per-language snippets, use a composite key so they don't overwrite each other.
        string key = language == "any" ? prefix : $"{language}:{prefix}";
        _snippets[key] = new Snippet(prefix, description, body, language);
    }

    public bool TryExpand()
    {
        int pos = _editor.SelectionStart;
        if (pos <= 0) return false;

        string text = _editor.Text;

        // Walk backward from cursor to find start of word
        int start = pos - 1;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
            start--;
        start++;

        if (start >= pos) return false;

        string prefix = text[start..pos];
        if (string.IsNullOrEmpty(prefix)) return false;

        // Look up the most specific snippet: language-specific first, then "any".
        Snippet? snippet = null;
        if (_currentLang != "any" && _snippets.TryGetValue($"{_currentLang}:{prefix}", out var langSnippet))
            snippet = langSnippet;
        else if (_snippets.TryGetValue(prefix, out var anySnippet) && anySnippet.Language == "any")
            snippet = anySnippet;

        if (snippet is null) return false;

        // Replace prefix with snippet body
        _editor.Select(start, pos - start);
        _editor.SelectedText = snippet.Body;

        // Find and position at first tab-stop ($0)
        int bodyIndex = snippet.Body.IndexOf("$0", StringComparison.Ordinal);
        if (bodyIndex >= 0)
        {
            _editor.SelectionStart = start + bodyIndex;
            _editor.SelectionLength = 0;
        }

        // Track tab-stops for subsequent Tab presses
        _tabStops.Clear();
        int stopIndex = 0;
        while (true)
        {
            int idx = snippet.Body.IndexOf($"${stopIndex}", StringComparison.Ordinal);
            if (idx < 0) break;
            _tabStops.Add((start + idx, stopIndex == 0));
            stopIndex++;
        }
        _currentTabStop = 0;

        return true;
    }

    private readonly List<(int Position, bool IsFinal)> _tabStops = new();
    private int _currentTabStop;

    public bool TryNavigateNextTabStop()
    {
        if (_tabStops.Count == 0) return false;

        // Advance to next non-final tab stop
        for (int i = _currentTabStop + 1; i < _tabStops.Count; i++)
        {
            if (!_tabStops[i].IsFinal)
            {
                _currentTabStop = i;
                _editor.SelectionStart = _tabStops[i].Position;
                _editor.SelectionLength = 0;
                _editor.ScrollToCaret();
                return true;
            }
        }

        // No more tab-stops — place cursor at end of inserted text
        _editor.SelectionStart = _editor.SelectionStart;
        return false;
    }

    public void ClearTabStops()
    {
        _tabStops.Clear();
        _currentTabStop = 0;
    }
}

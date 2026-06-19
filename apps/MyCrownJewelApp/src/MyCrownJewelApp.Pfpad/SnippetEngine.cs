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
            ".cs" or ".csx"                      => "cs",
            ".c"                                 => "c",
            ".cpp" or ".cc" or ".cxx" or ".c++"  => "cpp",
            ".h" or ".hpp" or ".hxx"             => "cpp",
            ".bicep"                             => "bicep",
            ".tf" or ".tfvars" or ".hcl"         => "tf",
            ".py" or ".pyw"                      => "py",
            ".js" or ".jsx" or ".mjs" or ".cjs"  => "js",
            ".ts" or ".tsx"                      => "ts",
            ".go"                                => "go",
            ".rb"                                => "rb",
            ".sh" or ".bash" or ".zsh"           => "sh",
            ".yaml" or ".yml"                    => "yaml",
            ".ps1" or ".psm1" or ".psd1"         => "ps",
            ".sql"                               => "sql",
            _                                    => "any"
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

        // ── Python snippets ───────────────────────────────────────────────────────
        Add("def",       "function definition",        "def $0($1):\r\n    $2",                                                                                                   "py");
        Add("class",     "class definition",           "class $0:\r\n    def __init__(self$1):\r\n        $2",                                                                    "py");
        Add("if",        "if statement",               "if $0:\r\n    $1",                                                                                                        "py");
        Add("ife",       "if-else",                    "if $0:\r\n    $1\r\nelse:\r\n    $2",                                                                                     "py");
        Add("for",       "for loop",                   "for $0 in $1:\r\n    $2",                                                                                                 "py");
        Add("while",     "while loop",                 "while $0:\r\n    $1",                                                                                                     "py");
        Add("try",       "try-except",                 "try:\r\n    $0\r\nexcept Exception as e:\r\n    $1",                                                                      "py");
        Add("with",      "with statement",             "with $0 as $1:\r\n    $2",                                                                                                "py");
        Add("main",      "main guard",                 "if __name__ == '__main__':\r\n    $0",                                                                                    "py");
        Add("dataclass", "dataclass",                  "@dataclass\r\nclass $0:\r\n    $1: $2",                                                                                   "py");
        Add("prop",      "property decorator",         "@property\r\ndef $0(self):\r\n    return self._$0\r\n\r\n@$0.setter\r\ndef $0(self, value):\r\n    self._$0 = value",     "py");
        Add("lc",        "list comprehension",         "[$0 for $1 in $2]",                                                                                                       "py");
        Add("dc",        "dict comprehension",         "{$0: $1 for $2 in $3}",                                                                                                   "py");
        Add("lambda",    "lambda expression",          "lambda $0: $1",                                                                                                           "py");

        // ── JavaScript snippets ───────────────────────────────────────────────────
        Add("fn",        "function declaration",       "function $0($1) {\r\n    $2\r\n}",                                                                                        "js");
        Add("arrow",     "arrow function",             "const $0 = ($1) => {\r\n    $2\r\n};",                                                                                    "js");
        Add("class",     "class declaration",          "class $0 {\r\n    constructor($1) {\r\n        $2\r\n    }\r\n}",                                                         "js");
        Add("for",       "for loop",                   "for (let i = 0; i < $0; i++) {\r\n    $1\r\n}",                                                                           "js");
        Add("foreach",   "forEach",                    "$0.forEach(($1) => {\r\n    $2\r\n});",                                                                                   "js");
        Add("prom",      "Promise",                    "new Promise((resolve, reject) => {\r\n    $0\r\n});",                                                                     "js");
        Add("async",     "async function",             "async function $0($1) {\r\n    try {\r\n        $2\r\n    } catch (err) {\r\n        console.error(err);\r\n    }\r\n}", "js");
        Add("fetch",     "fetch call",                 "const res = await fetch('$0');\r\nconst data = await res.json();",                                                        "js");
        Add("mod",       "ES module export",           "export $0",                                                                                                               "js");
        Add("iife",      "immediately invoked fn",     "(() => {\r\n    $0\r\n})();",                                                                                             "js");
        Add("event",     "addEventListener",           "$0.addEventListener('$1', ($2) => {\r\n    $3\r\n});",                                                                    "js");
        Add("try",       "try-catch",                  "try {\r\n    $0\r\n} catch (err) {\r\n    console.error(err);\r\n}",                                                      "js");
        Add("switch",    "switch statement",           "switch ($0) {\r\n    case $1:\r\n        $2\r\n        break;\r\n    default:\r\n        break;\r\n}",                    "js");

        // ── TypeScript snippets ───────────────────────────────────────────────────
        Add("fn",        "function declaration",       "function $0($1: $2): $3 {\r\n    $4\r\n}",                                                                                "ts");
        Add("arrow",     "typed arrow function",       "const $0 = ($1: $2): $3 => {\r\n    $4\r\n};",                                                                            "ts");
        Add("iface",     "interface",                  "interface $0 {\r\n    $1: $2;\r\n}",                                                                                      "ts");
        Add("type",      "type alias",                 "type $0 = $1;",                                                                                                           "ts");
        Add("enum",      "enum",                       "enum $0 {\r\n    $1 = '$2',\r\n}",                                                                                        "ts");
        Add("class",     "class with constructor",     "class $0 {\r\n    constructor(private $1: $2) {}\r\n\r\n    $3($4): $5 {\r\n        $6\r\n    }\r\n}",                   "ts");
        Add("generic",   "generic function",           "function $0<T>($1: T): T {\r\n    $2\r\n    return $1;\r\n}",                                                             "ts");
        Add("guard",     "type guard",                 "function is$0(value: unknown): value is $0 {\r\n    return typeof value === '$1';\r\n}",                                  "ts");
        Add("async",     "async function",             "async function $0($1: $2): Promise<$3> {\r\n    $4\r\n}",                                                                  "ts");
        Add("prom",      "Promise type",               "Promise<$0>",                                                                                                             "ts");
        Add("try",       "try-catch typed",            "try {\r\n    $0\r\n} catch (err: unknown) {\r\n    if (err instanceof Error) console.error(err.message);\r\n}",           "ts");
        Add("mod",       "export module",              "export { $0 } from './$1';",                                                                                              "ts");

        // ── Go snippets ───────────────────────────────────────────────────────────
        Add("func",      "function",                   "func $0($1 $2) $3 {\r\n\t$4\r\n}",                                                                                       "go");
        Add("main",      "main package",               "package main\r\n\r\nimport \"fmt\"\r\n\r\nfunc main() {\r\n\t$0\r\n}",                                                    "go");
        Add("struct",    "struct type",                "type $0 struct {\r\n\t$1 $2\r\n}",                                                                                        "go");
        Add("iface",     "interface type",             "type $0 interface {\r\n\t$1($2) $3\r\n}",                                                                                 "go");
        Add("iferr",     "if err != nil",              "if err != nil {\r\n\treturn $0err\r\n}",                                                                                   "go");
        Add("goroutine", "goroutine",                  "go func() {\r\n\t$0\r\n}()",                                                                                              "go");
        Add("chan",      "channel",                    "$0 := make(chan $1, $2)",                                                                                                  "go");
        Add("for",       "for loop",                   "for $0 := 0; $0 < $1; $0++ {\r\n\t$2\r\n}",                                                                              "go");
        Add("forr",      "range for loop",             "for $0, $1 := range $2 {\r\n\t$3\r\n}",                                                                                   "go");
        Add("switch",    "switch statement",           "switch $0 {\r\ncase $1:\r\n\t$2\r\ndefault:\r\n\t$3\r\n}",                                                               "go");
        Add("defer",     "defer statement",            "defer $0()",                                                                                                               "go");
        Add("test",      "test function",              "func Test$0(t *testing.T) {\r\n\t$1\r\n}",                                                                                "go");

        // ── Ruby snippets ─────────────────────────────────────────────────────────
        Add("def",       "method definition",          "def $0($1)\r\n  $2\r\nend",                                                                                               "rb");
        Add("class",     "class definition",           "class $0\r\n  def initialize($1)\r\n    $2\r\n  end\r\nend",                                                              "rb");
        Add("module",    "module definition",          "module $0\r\n  $1\r\nend",                                                                                                "rb");
        Add("if",        "if statement",               "if $0\r\n  $1\r\nend",                                                                                                    "rb");
        Add("ife",       "if-else",                    "if $0\r\n  $1\r\nelse\r\n  $2\r\nend",                                                                                    "rb");
        Add("block",     "block (do..end)",            "do |$0|\r\n  $1\r\nend",                                                                                                  "rb");
        Add("lambda",    "lambda",                     "-> ($0) { $1 }",                                                                                                          "rb");
        Add("attr",      "attr_accessor",              "attr_accessor :$0",                                                                                                       "rb");
        Add("begin",     "begin-rescue",               "begin\r\n  $0\r\nrescue => e\r\n  $1\r\nend",                                                                            "rb");
        Add("each",      "each iterator",              "$0.each do |$1|\r\n  $2\r\nend",                                                                                          "rb");
        Add("map",       "map iterator",               "$0.map { |$1| $2 }",                                                                                                      "rb");
        Add("test",      "RSpec it block",             "it '$0' do\r\n  $1\r\nend",                                                                                               "rb");

        // ── Bash/Shell snippets ───────────────────────────────────────────────────
        Add("strict",    "strict mode header",         "#!/usr/bin/env bash\r\nset -euo pipefail\r\nIFS=$'\\n\\t'\r\n\r\n$0",                                                    "sh");
        Add("fn",        "function definition",        "$0() {\r\n  $1\r\n}",                                                                                                     "sh");
        Add("if",        "if statement",               "if [[ $0 ]]; then\r\n  $1\r\nfi",                                                                                         "sh");
        Add("ife",       "if-else",                    "if [[ $0 ]]; then\r\n  $1\r\nelse\r\n  $2\r\nfi",                                                                        "sh");
        Add("for",       "for loop",                   "for $0 in $1; do\r\n  $2\r\ndone",                                                                                        "sh");
        Add("while",     "while loop",                 "while [[ $0 ]]; do\r\n  $1\r\ndone",                                                                                      "sh");
        Add("case",      "case statement",             "case $0 in\r\n  $1)\r\n    $2\r\n    ;;\r\n  *)\r\n    $3\r\n    ;;\r\nesac",                                             "sh");
        Add("args",      "positional args check",      "if [[ $# -lt $0 ]]; then\r\n  echo \"Usage: $0 <arg>\" >&2\r\n  exit 1\r\nfi",                                          "sh");
        Add("trap",      "trap signal",                "trap '$0' EXIT INT TERM",                                                                                                  "sh");
        Add("log",       "logging functions",          "log()  { echo \"[INFO]  $*\"; }\r\nwarn() { echo \"[WARN]  $*\" >&2; }\r\nerr()  { echo \"[ERROR] $*\" >&2; exit 1; }", "sh");
        Add("chkdep",    "check dependency",           "command -v $0 &>/dev/null || { echo \"$0 not found\" >&2; exit 1; }",                                                    "sh");
        Add("readonly",  "readonly variable",          "readonly $0=$1",                                                                                                           "sh");

        // ── YAML snippets ─────────────────────────────────────────────────────────
        Add("doc",       "YAML document",              "---\r\n$0",                                                                                                                "yaml");
        Add("list",      "YAML list",                  "- $0\r\n- $1",                                                                                                            "yaml");
        Add("map",       "YAML map",                   "$0:\r\n  $1: $2",                                                                                                          "yaml");
        Add("anchor",    "anchor & alias",             "&$0\r\n  $1: $2\r\n\r\n<<: *$0",                                                                                          "yaml");
        Add("ghaction",  "GitHub Actions workflow",    "name: $0\r\non:\r\n  push:\r\n    branches: [main]\r\n  pull_request:\r\n    branches: [main]\r\njobs:\r\n  build:\r\n    runs-on: ubuntu-latest\r\n    steps:\r\n      - uses: actions/checkout@v4\r\n      - name: $1\r\n        run: $2", "yaml");
        Add("step",      "GH Actions step",            "- name: $0\r\n  run: $1",                                                                                                 "yaml");
        Add("k8sdeploy", "Kubernetes Deployment",      "apiVersion: apps/v1\r\nkind: Deployment\r\nmetadata:\r\n  name: $0\r\nspec:\r\n  replicas: $1\r\n  selector:\r\n    matchLabels:\r\n      app: $0\r\n  template:\r\n    metadata:\r\n      labels:\r\n        app: $0\r\n    spec:\r\n      containers:\r\n        - name: $0\r\n          image: $2\r\n          ports:\r\n            - containerPort: $3", "yaml");
        Add("k8ssvc",    "Kubernetes Service",         "apiVersion: v1\r\nkind: Service\r\nmetadata:\r\n  name: $0\r\nspec:\r\n  selector:\r\n    app: $0\r\n  ports:\r\n    - port: $1\r\n      targetPort: $2\r\n  type: ClusterIP", "yaml");
        Add("dcompose",  "docker-compose service",     "services:\r\n  $0:\r\n    image: $1\r\n    ports:\r\n      - \"$2:$3\"\r\n    environment:\r\n      $4: $5\r\n    volumes:\r\n      - $6:$7", "yaml");

        // ── PowerShell snippets ───────────────────────────────────────────────────
        Add("fn",        "function",                   "function $0 {\r\n    param(\r\n        [Parameter(Mandatory)]\r\n        [string]$$1\r\n    )\r\n    $2\r\n}",            "ps");
        Add("param",     "param block",                "param(\r\n    [Parameter(Mandatory)]\r\n    [string]$$0\r\n)",                                                            "ps");
        Add("if",        "if statement",               "if ($0) {\r\n    $1\r\n}",                                                                                                "ps");
        Add("foreach",   "foreach loop",               "foreach ($$0 in $1) {\r\n    $2\r\n}",                                                                                   "ps");
        Add("switch",    "switch statement",           "switch ($0) {\r\n    '$1' { $2 }\r\n    default { $3 }\r\n}",                                                            "ps");
        Add("try",       "try-catch",                  "try {\r\n    $0\r\n} catch {\r\n    Write-Error \"Error: $_\"\r\n}",                                                     "ps");
        Add("pipeline",  "pipeline filter",            "process {\r\n    if ($_ $0) {\r\n        $1\r\n    }\r\n}",                                                               "ps");
        Add("cmdlet",    "advanced function",          "[CmdletBinding()]\r\nparam(\r\n    [Parameter(Mandatory, ValueFromPipeline)]\r\n    [string]$$0\r\n)\r\nbegin {}\r\nprocess { $1 }\r\nend {}", "ps");
        Add("test",      "Pester test",                "Describe '$0' {\r\n    It '$1' {\r\n        $2 | Should -Be $3\r\n    }\r\n}",                                           "ps");
        Add("module",    "module manifest snippet",    "@{\r\n    ModuleVersion = '$0'\r\n    RootModule    = '$1.psm1'\r\n    FunctionsToExport = @('$2')\r\n}",                "ps");

        // ── SQL snippets ──────────────────────────────────────────────────────────
        Add("sel",       "SELECT statement",           "SELECT $0\r\nFROM $1\r\nWHERE $2;",                                                                                       "sql");
        Add("selall",    "SELECT *",                   "SELECT *\r\nFROM $0\r\nWHERE $1;",                                                                                        "sql");
        Add("join",      "INNER JOIN",                 "INNER JOIN $0 ON $1.$2 = $3.$4",                                                                                          "sql");
        Add("ljoin",     "LEFT JOIN",                  "LEFT JOIN $0 ON $1.$2 = $3.$4",                                                                                           "sql");
        Add("cte",       "Common Table Expression",    "WITH $0 AS (\r\n    SELECT $1\r\n    FROM $2\r\n    WHERE $3\r\n)\r\nSELECT *\r\nFROM $0;",                              "sql");
        Add("insert",    "INSERT INTO",                "INSERT INTO $0 ($1)\r\nVALUES ($2);",                                                                                     "sql");
        Add("update",    "UPDATE statement",           "UPDATE $0\r\nSET $1 = $2\r\nWHERE $3;",                                                                                  "sql");
        Add("delete",    "DELETE statement",           "DELETE FROM $0\r\nWHERE $1;",                                                                                             "sql");
        Add("create",    "CREATE TABLE",               "CREATE TABLE $0 (\r\n    id   INT          NOT NULL PRIMARY KEY,\r\n    $1   VARCHAR(255) NOT NULL,\r\n    created_at DATETIME DEFAULT CURRENT_TIMESTAMP\r\n);", "sql");
        Add("index",     "CREATE INDEX",               "CREATE INDEX idx_$0_$1 ON $0 ($1);",                                                                                     "sql");
        Add("tx",        "transaction block",          "BEGIN TRANSACTION;\r\n\r\n$0\r\n\r\nCOMMIT;",                                                                            "sql");
        Add("view",      "CREATE VIEW",                "CREATE VIEW $0 AS\r\nSELECT $1\r\nFROM $2\r\nWHERE $3;",                                                                "sql");
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

        // Strip $N tab-stop markers from the body, recording each marker's position in
        // the clean text.  Markers are processed in text order so that removing earlier
        // markers automatically shifts later ones.
        (string cleanBody, List<(int TabStop, int Position)> stops) = StripTabStops(snippet.Body);

        _editor.Select(start, pos - start);
        _editor.SelectedText = cleanBody;

        // Navigate to the first user-fillable stop ($1), falling back to $0 if absent.
        _tabStops.Clear();
        _currentTabStop = 0;

        // Build tab-stop list sorted by stop number (1, 2, …, then 0 at the very end).
        var ordered = stops
            .Where(s => s.TabStop != 0)
            .OrderBy(s => s.TabStop)
            .Concat(stops.Where(s => s.TabStop == 0))
            .ToList();

        foreach (var (num, bodyPos) in ordered)
            _tabStops.Add((start + bodyPos, num == 0));

        // Place cursor at $1 (first user stop) or $0 (exit point) if no numbered stops.
        if (_tabStops.Count > 0)
        {
            _editor.SelectionStart = _tabStops[0].Position;
            _editor.SelectionLength = 0;
            _editor.ScrollToCaret();
        }

        return true;
    }

    /// <summary>
    /// Returns a list of snippets whose prefix starts with <paramref name="partialPrefix"/>,
    /// searching the active language first then "any" language snippets.
    /// </summary>
    public IReadOnlyList<Snippet> GetSuggestions(string partialPrefix)
    {
        if (string.IsNullOrEmpty(partialPrefix))
            return [];

        var results = new List<Snippet>();

        foreach (var (key, snippet) in _snippets)
        {
            if (snippet.Language != "any" && snippet.Language != _currentLang)
                continue;
            if (snippet.Prefix.StartsWith(partialPrefix, StringComparison.OrdinalIgnoreCase))
                results.Add(snippet);
        }

        results.Sort(static (a, b) => string.Compare(a.Prefix, b.Prefix, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    /// <summary>
    /// Removes all <c>$N</c> tab-stop markers from the snippet body and returns the
    /// clean text together with each marker's 0-based position in that clean text.
    /// </summary>
    private static (string CleanBody, List<(int TabStop, int Position)> Stops) StripTabStops(string body)
    {
        var stops = new List<(int, int)>();
        var sb = new System.Text.StringBuilder(body.Length);
        int i = 0;
        int removed = 0; // chars removed so far (adjusts recorded positions)

        while (i < body.Length)
        {
            if (body[i] == '$' && i + 1 < body.Length && char.IsDigit(body[i + 1]))
            {
                // Consume all consecutive digits after '$'
                int j = i + 1;
                while (j < body.Length && char.IsDigit(body[j]))
                    j++;

                if (int.TryParse(body.AsSpan(i + 1, j - (i + 1)), out int stopNum))
                {
                    int posInClean = sb.Length; // position in the clean string
                    stops.Add((stopNum, posInClean));
                    removed += j - i;          // skip the marker chars
                    i = j;
                    continue;
                }
            }

            sb.Append(body[i]);
            i++;
        }

        return (sb.ToString(), stops);
    }

    private readonly List<(int Position, bool IsFinal)> _tabStops = new();
    private int _currentTabStop;

    public bool TryNavigateNextTabStop()
    {
        if (_tabStops.Count == 0) return false;

        // Advance to the next tab stop after the current one.
        int next = _currentTabStop + 1;
        if (next >= _tabStops.Count) return false;

        _currentTabStop = next;
        _editor.SelectionStart = _tabStops[next].Position;
        _editor.SelectionLength = 0;
        _editor.ScrollToCaret();
        return true;
    }

    public void ClearTabStops()
    {
        _tabStops.Clear();
        _currentTabStop = 0;
    }
}

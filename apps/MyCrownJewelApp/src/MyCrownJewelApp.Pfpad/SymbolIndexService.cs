using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad;

public sealed record SymbolLocation
{
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public string Name { get; init; } = "";
    public SymbolKind Kind { get; init; }
    public string Context { get; init; } = "";
}

public enum SymbolKind { Class, Method, Property, Field, Interface, Enum, Struct, Function, Variable, Type, Unknown }

public sealed class SymbolIndexService : IDisposable
{
    private Dictionary<string, List<SymbolLocation>> _index = new(StringComparer.OrdinalIgnoreCase);
    private string? _rootDir;
    private bool _disposed;
    private IRoslynWorkspace? _roslynWorkspace;
    private TreeSitter.TreeSitterService? _treeSitterService;

    public event Action? OnIndexUpdated;

    public void SetRoslynWorkspace(IRoslynWorkspace workspace)
    {
        _roslynWorkspace = workspace;
    }

    public void SetTreeSitterService(TreeSitter.TreeSitterService service)
    {
        _treeSitterService = service;
    }

    public IReadOnlyList<SymbolLocation> Lookup(string name)
    {
        // Try Roslyn first for semantic symbol resolution
        if (_roslynWorkspace is not null)
        {
            var symbols = _roslynWorkspace.FindSymbolsAsync(name).Result;
            if (symbols.Count > 0)
            {
                return symbols.Select(s => new SymbolLocation
                {
                    Name = s.Name,
                    Kind = MapSymbolKind(s.Kind),
                    File = s.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "",
                    Line = s.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Line ?? 0,
                    Column = s.Locations.FirstOrDefault()?.GetLineSpan().StartLinePosition.Character ?? 0,
                    Context = s.ContainingType?.Name ?? s.ContainingNamespace?.Name ?? ""
                }).ToList();
            }
        }

        return _index.TryGetValue(name, out var list) ? list : Array.Empty<SymbolLocation>();
    }

    public IEnumerable<SymbolLocation> GetAllSymbols()
    {
        foreach (var list in _index.Values)
            foreach (var loc in list)
                yield return loc;
    }

    public bool HasIndex => _rootDir is not null;

    public async Task RebuildIndexAsync(string rootDir)
    {
        _rootDir = rootDir;
        if (_disposed) return;

        await Task.Run(() =>
        {
            var newIndex = new Dictionary<string, List<SymbolLocation>>(StringComparer.OrdinalIgnoreCase);

            // Try ctags first
            if (TryCtags(rootDir, newIndex))
            {
                _index = newIndex;
                OnIndexUpdated?.Invoke();
                return;
            }

            // Try TreeSitter for supported languages (C, C++, JS, etc.)
            if (_treeSitterService is not null)
            {
                ScanWithTreeSitter(rootDir, newIndex);
                if (newIndex.Count > 0)
                {
                    _index = newIndex;
                    OnIndexUpdated?.Invoke();
                    return;
                }
            }

            // Fall back to regex scanner
            ScanWithRegex(rootDir, newIndex);
            _index = newIndex;
            OnIndexUpdated?.Invoke();
        });
    }

    private bool TryCtags(string rootDir, Dictionary<string, List<SymbolLocation>> index)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ctags",
                Arguments = $"-f - --fields=+nK --exclude=.git -R \"{rootDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.UTF8Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            string line;
            int count = 0;
            while ((line = proc.StandardOutput.ReadLine()!) is not null && count < 50000)
            {
                count++;
                // ctags output: {name}\t{file}\t{line};\"\t{kind}\t{signature}
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                string name = parts[0];
                string file = parts[1];
                string linePart = parts[2].TrimEnd(';', '"');
                if (!int.TryParse(linePart, out int lineNum)) continue;

                string kindStr = parts.Length > 3 ? parts[3] : "";
                string context = parts.Length > 4 ? parts[4].Trim('"') : "";

                if (string.IsNullOrEmpty(name)) continue;

                SymbolKind kind = ParseCtagsKind(kindStr);
                string fullPath = Path.IsPathRooted(file) ? file : Path.Combine(rootDir, file);

                if (!index.ContainsKey(name))
                    index[name] = new List<SymbolLocation>();

                index[name].Add(new SymbolLocation
                {
                    File = fullPath,
                    Line = lineNum,
                    Name = name,
                    Kind = kind,
                    Context = context
                });
            }

            proc.WaitForExit(15000);
            return proc.ExitCode == 0 && count > 0;
        }
        catch { return false; }
    }

    private static SymbolKind ParseCtagsKind(string kind) => kind switch
    {
        "class" => SymbolKind.Class,
        "method" or "member" or "function" => SymbolKind.Method,
        "property" => SymbolKind.Property,
        "field" or "member" => SymbolKind.Field,
        "interface" => SymbolKind.Interface,
        "enum" => SymbolKind.Enum,
        "struct" => SymbolKind.Struct,
        "function" or "func" or "def" => SymbolKind.Function,
        "variable" or "var" => SymbolKind.Variable,
        "typedef" or "type" => SymbolKind.Type,
        _ => SymbolKind.Unknown
    };

    private void ScanWithTreeSitter(string rootDir, Dictionary<string, List<SymbolLocation>> index)
    {
        if (_treeSitterService is null) return;
        try
        {
            var files = Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (IsIgnored(file)) continue;
                if (!_treeSitterService.CanHandle(file)) continue;

                try
                {
                    var text = File.ReadAllText(file);
                    var symbols = _treeSitterService.GetSymbols(text, file);
                    foreach (var sym in symbols)
                    {
                        if (!index.ContainsKey(sym.Name))
                            index[sym.Name] = new List<SymbolLocation>();
                        index[sym.Name].Add(sym);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private void ScanWithRegex(string rootDir, Dictionary<string, List<SymbolLocation>> index)
    {
        try
        {
            var files = Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (IsIgnored(file)) continue;
                var def = SyntaxDefinition.GetDefinitionForFile(file);
                if (def?.DefinitionPatterns is null || def.DefinitionPatterns.Length == 0) continue;

                try
                {
                    var lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        foreach (var pat in def.DefinitionPatterns)
                        {
                            var m = Regex.Match(line, pat, RegexOptions.None, TimeSpan.FromMilliseconds(200));
                            if (!m.Success) continue;

                            string name = m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups[1].Value;
                            if (string.IsNullOrEmpty(name)) continue;

                            string kindStr = m.Groups["kind"].Success ? m.Groups["kind"].Value : "";
                            string context = "";

                            // Try to get class/namespace context from surrounding lines
                            context = GetContext(lines, i);

                            if (!index.ContainsKey(name))
                                index[name] = new List<SymbolLocation>();

                            index[name].Add(new SymbolLocation
                            {
                                File = file,
                                Line = i + 1,
                                Column = m.Index + 1,
                                Name = name,
                                Kind = ParseKind(kindStr),
                                Context = context
                            });
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static string GetContext(string[] lines, int currentLine)
    {
        for (int i = currentLine - 1; i >= Math.Max(0, currentLine - 15); i--)
        {
            var m = Regex.Match(lines[i].Trim(), @"(class|struct|interface|namespace|module)\s+(\w+)");
            if (m.Success) return m.Groups[2].Value;
        }
        return "";
    }

    private static SymbolKind ParseKind(string kind) => kind.ToLowerInvariant() switch
    {
        "class" => SymbolKind.Class,
        "method" or "function" => SymbolKind.Method,
        "property" => SymbolKind.Property,
        "field" or "var" => SymbolKind.Field,
        "interface" => SymbolKind.Interface,
        "enum" => SymbolKind.Enum,
        "struct" => SymbolKind.Struct,
        "type" => SymbolKind.Type,
        _ => SymbolKind.Unknown
    };

    private static bool IsIgnored(string path)
    {
        string name = Path.GetFileName(path);
        if (name.StartsWith('.')) return true;
        string dir = Path.GetDirectoryName(path) ?? "";
        string[] ignoreDirs = { "node_modules", ".git", ".svn", ".hg", "bin", "obj", ".vs", "packages" };
        foreach (var d in ignoreDirs)
            if (dir.Contains(d)) return true;
        return false;
    }

    private static SymbolKind MapSymbolKind(Microsoft.CodeAnalysis.SymbolKind kind) => kind switch
    {
        Microsoft.CodeAnalysis.SymbolKind.NamedType => SymbolKind.Class,
        Microsoft.CodeAnalysis.SymbolKind.Method => SymbolKind.Method,
        Microsoft.CodeAnalysis.SymbolKind.Property => SymbolKind.Property,
        Microsoft.CodeAnalysis.SymbolKind.Field => SymbolKind.Field,
        Microsoft.CodeAnalysis.SymbolKind.ArrayType => SymbolKind.Type,
        Microsoft.CodeAnalysis.SymbolKind.Parameter or Microsoft.CodeAnalysis.SymbolKind.Local => SymbolKind.Variable,
        _ => SymbolKind.Unknown
    };

    public void Dispose()
    {
        _disposed = true;
    }
}

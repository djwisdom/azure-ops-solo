using System.Reflection;
using Microsoft.CodeAnalysis;

namespace MyCrownJewelApp.Pfpad.Roslyn;

public static class ReferenceAssemblyProvider
{
    private static List<PortableExecutableReference>? _cache;
    private static readonly object _lock = new();

    private static readonly string[] _requiredAssemblies =
    [
        "System.Runtime",
        "System.Collections",
        "System.Collections.Concurrent",
        "System.Linq",
        "System.Linq.Expressions",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Text.RegularExpressions",
        "System.Text.Encoding",
        "System.ComponentModel",
        "System.ComponentModel.Primitives",
        "System.IO",
        "System.IO.FileSystem",
        "System.Net.Primitives",
        "System.ObjectModel",
        "System.Private.CoreLib",
        "System.Runtime.InteropServices",
        "System.Reflection",
        "System.Reflection.Extensions",
        "System.Reflection.Metadata",
        "System.Diagnostics.Process",
        "System.Diagnostics.StackTrace",
        "System.Runtime.Numerics",
        "System.Net.Http",
        "System.Xml.ReaderWriter",
        "System.Xml.XDocument"
    ];

    public static IReadOnlyList<PortableExecutableReference> Load()
    {
        if (_cache is not null)
            return _cache;

        lock (_lock)
        {
            if (_cache is not null)
                return _cache;

            var refs = new List<PortableExecutableReference>();
            var dirs = GetReferenceAssemblyDirectories();

            foreach (var asmName in _requiredAssemblies)
            {
                var found = FindAssembly(asmName, dirs);
                if (found is not null)
                {
                    try { refs.Add(MetadataReference.CreateFromFile(found)); }
                    catch { }
                }
            }

            _cache = refs;
            return refs;
        }
    }

    private static string[] GetReferenceAssemblyDirectories()
    {
        var candidates = new List<string>();

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
            AddRefDirs(candidates, dotnetRoot);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        AddRefDirs(candidates, Path.Combine(programFiles, "dotnet"));

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (programFilesX86 != programFiles)
            AddRefDirs(candidates, Path.Combine(programFilesX86, "dotnet"));

        return candidates.ToArray();
    }

    private static void AddRefDirs(List<string> candidates, string dotnetRoot)
    {
        var packsDir = Path.Combine(dotnetRoot, "packs");
        if (!Directory.Exists(packsDir)) return;

        AddRefDir(candidates, packsDir, "Microsoft.NETCore.App.Ref");
        AddRefDir(candidates, packsDir, "Microsoft.WindowsDesktop.App.Ref");
    }

    private static void AddRefDir(List<string> candidates, string packsDir, string packName)
    {
        try
        {
            var packPath = Path.Combine(packsDir, packName);
            if (!Directory.Exists(packPath)) return;

            var bestVersionDir = Directory.EnumerateDirectories(packPath)
                .Select(d => (Path: d, Name: Path.GetFileName(d)))
                .Where(x => Version.TryParse(x.Name, out _))
                .OrderByDescending(x => Version.Parse(x.Name))
                .FirstOrDefault();

            if (bestVersionDir.Path is null) return;

            var refDir = Path.Combine(bestVersionDir.Path, "ref");
            if (!Directory.Exists(refDir)) return;

            // Add all available TFM directories (net8.0, net9.0, net10.0, etc.)
            foreach (var tfmDir in Directory.EnumerateDirectories(refDir))
            {
                if (!candidates.Contains(tfmDir))
                    candidates.Add(tfmDir);
            }
        }
        catch { }
    }

    private static string? FindAssembly(string name, string[] dirs)
    {
        foreach (var dir in dirs)
        {
            string path = Path.Combine(dir, $"{name}.dll");
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    public static void ClearCache()
    {
        lock (_lock) { _cache = null; }
    }
}

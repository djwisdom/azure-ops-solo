using System.IO;
using System.Linq;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Static helpers for locating .NET project files and build output assemblies.
/// Contains no WinForms dependencies and is fully unit-testable.
/// </summary>
internal static class ProjectLocator
{
    /// <summary>
    /// Walks up the directory tree from <paramref name="filePath"/> to find the nearest
    /// directory containing a .csproj file. Returns null if none found.
    /// </summary>
    public static string? FindProjectDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir != null)
        {
            if (Directory.EnumerateFiles(dir, "*.csproj").Any())
                return dir;
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    /// <summary>
    /// Looks for the first existing Debug output DLL for the .csproj found in
    /// <paramref name="projectDir"/>. Checks net8.0, net9.0, and net10.0 TFM folders.
    /// Returns null if no matching assembly exists.
    /// </summary>
    public static string? FindOutputAssembly(string projectDir)
    {
        var csproj = Directory.EnumerateFiles(projectDir, "*.csproj").FirstOrDefault();
        if (csproj == null) return null;

        string name = Path.GetFileNameWithoutExtension(csproj);
        string[] candidates =
        {
            Path.Combine(projectDir, "bin", "Debug", "net8.0",  $"{name}.dll"),
            Path.Combine(projectDir, "bin", "Debug", "net9.0",  $"{name}.dll"),
            Path.Combine(projectDir, "bin", "Debug", "net10.0", $"{name}.dll"),
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        return null;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Static helpers for workspace-level file operations: language detection and text search.
/// Contains no WinForms dependencies and is fully unit-testable.
/// </summary>
internal static class WorkspaceHelper
{
    /// <summary>
    /// Heuristically detects the primary programming language used in a workspace
    /// by scanning top-level project/config files. Returns null when undetermined.
    /// </summary>
    public static string? DetectLanguage(string root)
    {
        try
        {
            foreach (string f in Directory.EnumerateFiles(root, "*.*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(f).ToLowerInvariant();

                if (name.EndsWith(".csproj") || name.EndsWith(".sln"))
                    return "C#";

                if (name.EndsWith(".py") || name == "requirements.txt" || name == "setup.py")
                    return "Python";

                if (name == "cmakelists.txt" || name.EndsWith(".cpp") || name.EndsWith(".c") || name.EndsWith(".h"))
                    return "C++";

                if (name == "cargo.toml")
                    return "Rust";

                if (name == "package.json")
                    return "JavaScript";

                if (name.EndsWith(".go"))
                    return "Go";

                if (name.EndsWith(".rs"))
                    return "Rust";

                if (name.EndsWith(".java") || name == "pom.xml" || name == "build.gradle")
                    return "Java";
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Recursively searches text files under <paramref name="dir"/> for lines matching
    /// <paramref name="findText"/> (literal) or <paramref name="regex"/> (when non-null).
    /// Results are appended to <paramref name="results"/> as (relativePath, 1-based lineNumber, trimmedText).
    /// </summary>
    public static void SearchDirectory(
        string rootDir,
        string dir,
        List<(string File, int Line, string Text)> results,
        HashSet<string> textExtensions,
        HashSet<string> ignoredDirs,
        string findText,
        Regex? regex,
        StringComparison comparison)
    {
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            string name = Path.GetFileName(d);
            if (!ignoredDirs.Contains(name))
                SearchDirectory(rootDir, d, results, textExtensions, ignoredDirs, findText, regex, comparison);
        }

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            string ext = Path.GetExtension(file);
            if (!textExtensions.Contains(ext)) continue;

            try
            {
                string[] lines = File.ReadAllLines(file);
                string relativePath = Path.GetRelativePath(rootDir, file);
                for (int i = 0; i < lines.Length; i++)
                {
                    bool match = regex != null
                        ? regex.IsMatch(lines[i])
                        : lines[i].Contains(findText, comparison);

                    if (match)
                        results.Add((relativePath, i + 1, lines[i].Trim()));
                }
            }
            catch { }
        }
    }
}

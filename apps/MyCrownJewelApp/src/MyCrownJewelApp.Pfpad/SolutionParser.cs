using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

/// <summary>Parses Visual Studio .sln and SDK-style .csproj files.</summary>
public static class SolutionParser
{
    private const string SolutionFolderGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    private static readonly Regex ProjectLineRx = new(
        @"^Project\(""{(?<type>[^}]+)}""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<path>[^""]+)""\s*,\s*""{(?<guid>[^}]+)}""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NestedRx = new(
        @"^\s*{(?<child>[A-Fa-f0-9\-]+)}\s*=\s*{(?<parent>[A-Fa-f0-9\-]+)}",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public static SolutionFile ParseSolution(string slnPath)
    {
        string text = File.ReadAllText(slnPath);

        var folders = new Dictionary<string, SolutionFolder>(StringComparer.OrdinalIgnoreCase);
        var projects = new List<SolutionProject>();

        foreach (Match m in ProjectLineRx.Matches(text))
        {
            string typeStr = m.Groups["type"].Value;
            if (string.Equals(typeStr, SolutionFolderGuid, StringComparison.OrdinalIgnoreCase))
            {
                string guid = m.Groups["guid"].Value.ToUpperInvariant();
                folders[guid] = new SolutionFolder(m.Groups["name"].Value, m.Groups["guid"].Value);
            }
        }

        var nested = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int nestedIdx = text.IndexOf("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase);
        if (nestedIdx >= 0)
        {
            int end = text.IndexOf("EndGlobalSection", nestedIdx, StringComparison.OrdinalIgnoreCase);
            string section = end > nestedIdx ? text[nestedIdx..end] : text[nestedIdx..];
            foreach (Match m in NestedRx.Matches(section))
            {
                nested[m.Groups["child"].Value.ToUpperInvariant()] = m.Groups["parent"].Value.ToUpperInvariant();
            }
        }

        foreach (Match m in ProjectLineRx.Matches(text))
        {
            string typeStr = m.Groups["type"].Value;
            if (string.Equals(typeStr, SolutionFolderGuid, StringComparison.OrdinalIgnoreCase))
                continue;

            string projGuid = m.Groups["guid"].Value.ToUpperInvariant();
            string? parentFolderName = null;
            if (nested.TryGetValue(projGuid, out string? parentGuid) && folders.TryGetValue(parentGuid, out var folder))
                parentFolderName = folder.Name;

            string relPath = m.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
            projects.Add(new SolutionProject(
                m.Groups["name"].Value,
                relPath,
                typeStr,
                projGuid,
                parentFolderName));
        }

        return new SolutionFile
        {
            Path = slnPath,
            Projects = projects,
            Folders = folders.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    public static ProjectData ParseProject(string csprojPath)
    {
        if (!File.Exists(csprojPath))
        {
            return new ProjectData
            {
                Name = Path.GetFileNameWithoutExtension(csprojPath),
                Path = csprojPath,
                IsLoaded = false
            };
        }

        try
        {
            XDocument doc = XDocument.Load(csprojPath);
            XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            string projDir = Path.GetDirectoryName(csprojPath) ?? string.Empty;

            string? tf = doc.Descendants(ns + "TargetFramework").FirstOrDefault()?.Value
                ?? doc.Descendants(ns + "TargetFrameworks").FirstOrDefault()?.Value;

            var packages = doc.Descendants(ns + "PackageReference")
                .Select(e => new PackageRef(
                    e.Attribute("Include")?.Value ?? string.Empty,
                    e.Attribute("Version")?.Value ?? e.Element(ns + "Version")?.Value ?? string.Empty))
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var projectRefs = doc.Descendants(ns + "ProjectReference")
                .Select(e =>
                {
                    string rel = e.Attribute("Include")?.Value ?? string.Empty;
                    string name = Path.GetFileNameWithoutExtension(rel);
                    return new ProjectRef(name, rel.Replace('\\', '/'));
                })
                .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string[] itemTypes = ["Compile", "Content", "None", "EmbeddedResource", "AdditionalFiles"];
            var explicitFiles = doc.Descendants()
                .Where(e => itemTypes.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase) && e.Attribute("Include") != null)
                .Select(e => new ProjectFileItem(
                    e.Attribute("Include")!.Value.Replace('\\', '/'),
                    e.Name.LocalName))
                .ToList();

            bool isSdkStyle = doc.Root?.Attribute("Sdk") != null
                || doc.Root?.Elements().Any(e =>
                    e.Name.LocalName == "Import"
                    && (e.Attribute("Sdk") != null
                        || (e.Attribute("Project")?.Value?.Contains("Microsoft.Common", StringComparison.OrdinalIgnoreCase) ?? false))) == true;

            if (isSdkStyle && !explicitFiles.Any(f => string.Equals(f.ItemType, "Compile", StringComparison.OrdinalIgnoreCase)))
            {
                string[] sdkExtensions = [".cs", ".vb", ".fs", ".razor", ".cshtml", ".xaml", ".json", ".xml", ".config", ".resx", ".txt", ".md"];
                HashSet<string> excludeDirs = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs", "node_modules" };

                try
                {
                    var diskFiles = EnumerateProjectFiles(projDir, excludeDirs, sdkExtensions)
                        .Select(path => Path.GetRelativePath(projDir, path).Replace('\\', '/'))
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(path => new ProjectFileItem(path, "Compile"));

                    explicitFiles.AddRange(diskFiles);
                }
                catch
                {
                }
            }

            var files = explicitFiles
                .Where(f => !string.IsNullOrWhiteSpace(f.RelativePath))
                .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ProjectData
            {
                Name = Path.GetFileNameWithoutExtension(csprojPath),
                Path = csprojPath,
                TargetFramework = tf,
                Packages = packages,
                ProjectRefs = projectRefs,
                Files = files
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SolutionParser] Failed to parse {csprojPath}: {ex.Message}");
            return new ProjectData
            {
                Name = Path.GetFileNameWithoutExtension(csprojPath),
                Path = csprojPath,
                IsLoaded = false
            };
        }
    }

    private static IEnumerable<string> EnumerateProjectFiles(string dir, HashSet<string> excludeDirs, string[] extensions)
    {
        foreach (string file in Directory.EnumerateFiles(dir))
        {
            string ext = Path.GetExtension(file);
            if (extensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                yield return file;
        }

        foreach (string sub in Directory.EnumerateDirectories(dir))
        {
            string dirName = Path.GetFileName(sub);
            if (excludeDirs.Contains(dirName))
                continue;

            foreach (string file in EnumerateProjectFiles(sub, excludeDirs, extensions))
                yield return file;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MyCrownJewelApp.Pfpad;

public static class ProjectDependencyAnalyzer
{
    public sealed record ProjectInfo
    {
        public string Path { get; set; } = "";
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        public List<ProjectReference> ProjectRefs { get; set; } = new();
        public List<PackageReference> PackageRefs { get; set; } = new();
        public HashSet<string> ExportedNamespaces { get; set; } = new();
        public List<string> Files { get; set; } = new();
    }

    public sealed record ProjectReference
    {
        public string ProjectPath { get; init; } = "";
        public string ProjectName => System.IO.Path.GetFileNameWithoutExtension(ProjectPath);
    }

    public sealed record PackageReference
    {
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
    }

    public static List<ProjectInfo> Analyze(string workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return new();

        var result = new List<ProjectInfo>();
        // Map: normalized path -> project
        var projectMap = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var csprojFiles = Directory.EnumerateFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();

            foreach (var csproj in csprojFiles)
            {
                var info = new ProjectInfo { Path = csproj };
                string content = File.ReadAllText(csproj);

                // Parse project references
                var projRefs = Regex.Matches(content, @"<ProjectReference\s+[^>]*Include\s*=\s*""([^""]+)""");
                foreach (Match m in projRefs)
                {
                    string relPath = m.Groups[1].Value;
                    string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csproj)!, relPath));
                    info.ProjectRefs.Add(new ProjectReference { ProjectPath = fullPath });
                }

                // Parse package references
                var pkgRefs = Regex.Matches(content, @"<PackageReference\s+[^>]*Include\s*=\s*""([^""]+)""\s+Version\s*=\s*""([^""]+)""");
                foreach (Match m in pkgRefs)
                    info.PackageRefs.Add(new PackageReference { Name = m.Groups[1].Value, Version = m.Groups[2].Value });

                // Infer exported namespace from RootNamespace or project name
                var nsMatch = Regex.Match(content, @"<RootNamespace>([^<]+)</RootNamespace>");
                string ns = nsMatch.Success ? nsMatch.Groups[1].Value : info.Name;
                info.ExportedNamespaces.Add(ns);

                result.Add(info);
                projectMap[csproj] = info;
            }

            // Scan .cs files for using statements to build file-level dep map
            foreach (var proj in result)
            {
                string dir = Path.GetDirectoryName(proj.Path)!;
                try
                {
                    var csFiles = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                        .ToList();
                    proj.Files = csFiles;
                }
                catch { }
            }

            // Resolve project refs to existing projects
            foreach (var proj in result)
            {
                var resolved = new List<ProjectReference>();
                foreach (var dep in proj.ProjectRefs)
                {
                    if (projectMap.TryGetValue(dep.ProjectPath, out var target))
                        resolved.Add(new ProjectReference { ProjectPath = target.Path });
                }
                proj.ProjectRefs = resolved;
            }
        }
        catch { }

        return result;
    }

    public static List<string> FindAffectedFiles(string changedFile, List<ProjectInfo> projects)
    {
        var affected = new List<string>();

        // Find which project this file belongs to
        var ownerProject = projects.FirstOrDefault(p =>
            p.Files.Any(f => string.Equals(f, changedFile, StringComparison.OrdinalIgnoreCase)));

        if (ownerProject == null) return affected;

        // Find all projects that depend on this project
        var dependents = projects.Where(p =>
            p.ProjectRefs.Any(r => string.Equals(r.ProjectPath, ownerProject.Path, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Collect all namespaces that this project exports
        var exportedNs = ownerProject.ExportedNamespaces;

        // Find files in dependent projects that use these namespaces
        foreach (var dep in dependents)
        {
            foreach (var file in dep.Files)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    foreach (var ns in exportedNs)
                    {
                        if (content.Contains($"using {ns}") || content.Contains($"using {ns}."))
                        {
                            if (!affected.Contains(file))
                                affected.Add(file);
                            break;
                        }
                    }
                }
                catch { }
            }
        }

        return affected;
    }

    public static List<string> FindAllReferences(string symbolName, List<ProjectInfo> projects)
    {
        var results = new List<string>();
        var pattern = $@"\b{Regex.Escape(symbolName)}\b";

        foreach (var proj in projects)
        {
            foreach (var file in proj.Files)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    if (Regex.IsMatch(content, pattern))
                        results.Add(file);
                }
                catch { }
            }
        }

        return results;
    }
}

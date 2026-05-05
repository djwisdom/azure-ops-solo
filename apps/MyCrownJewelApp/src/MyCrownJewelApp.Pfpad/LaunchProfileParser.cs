using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad;

public static class LaunchProfileParser
{
    public sealed record LaunchProfile
    {
        public string Name { get; init; } = "";
        public string ProjectPath { get; init; } = "";
        public string CommandName { get; init; } = "Project";
        public string ApplicationUrl { get; init; } = "";
        public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
        public string WorkingDirectory { get; init; } = "";
    }

    public static List<LaunchProfile> ScanWorkspace(string workspaceRoot)
    {
        var results = new List<LaunchProfile>();

        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return results;

        try
        {
            // Find all launchSettings.json files
            var launchFiles = Directory.EnumerateFiles(workspaceRoot, "launchSettings.json", SearchOption.AllDirectories)
                .Where(f => f.Contains("\\Properties\\"))
                .ToList();

            foreach (var launchFile in launchFiles)
            {
                string projectDir = Path.GetDirectoryName(Path.GetDirectoryName(launchFile)!)!;
                string? csproj = Directory.EnumerateFiles(projectDir, "*.csproj").FirstOrDefault();
                if (csproj == null) continue;

                try
                {
                    string json = File.ReadAllText(launchFile);
                    using var doc = JsonDocument.Parse(json);
                    var profiles = doc.RootElement.GetProperty("profiles");

                    foreach (var profile in profiles.EnumerateObject())
                    {
                        var envVars = new Dictionary<string, string>();
                        if (profile.Value.TryGetProperty("environmentVariables", out var envEl))
                        {
                            foreach (var env in envEl.EnumerateObject())
                                envVars[env.Name] = env.Value.GetString() ?? "";
                        }

                        string appUrl = profile.Value.TryGetProperty("applicationUrl", out var urlEl)
                            ? urlEl.GetString() ?? ""
                            : "";

                        string cmdName = profile.Value.TryGetProperty("commandName", out var cmdEl)
                            ? cmdEl.GetString() ?? "Project"
                            : "Project";

                        results.Add(new LaunchProfile
                        {
                            Name = profile.Name,
                            ProjectPath = csproj,
                            CommandName = cmdName,
                            ApplicationUrl = appUrl,
                            EnvironmentVariables = envVars,
                            WorkingDirectory = projectDir
                        });
                    }
                }
                catch { }
            }

            // Also scan for .env files
            var envFiles = Directory.EnumerateFiles(workspaceRoot, ".env*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();

            foreach (var envFile in envFiles)
            {
                string projectDir = Path.GetDirectoryName(envFile)!;
                string? csproj = Directory.EnumerateFiles(projectDir, "*.csproj").FirstOrDefault();
                if (csproj == null) continue;

                var envVars = ParseEnvFile(envFile);
                string profileName = Path.GetFileName(envFile) == ".env" ? ".env" : Path.GetFileName(envFile);

                results.Add(new LaunchProfile
                {
                    Name = $"{profileName} ({Path.GetFileNameWithoutExtension(csproj)})",
                    ProjectPath = csproj,
                    CommandName = "Project",
                    EnvironmentVariables = envVars,
                    WorkingDirectory = projectDir
                });
            }
        }
        catch { }

        return results;
    }

    public static Dictionary<string, string> ParseEnvFile(string path)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var lines = File.ReadAllLines(path);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line[..eq].Trim();
                string value = line[(eq + 1)..].Trim();
                if (key.Length > 0)
                {
                    // Remove surrounding quotes if present
                    if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                        value = value[1..^1];
                    vars[key] = value;
                }
            }
        }
        catch { }
        return vars;
    }
}

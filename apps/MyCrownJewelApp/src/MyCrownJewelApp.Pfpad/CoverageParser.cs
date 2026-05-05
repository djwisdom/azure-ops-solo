using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

public static class CoverageParser
{
    public sealed record CoverageResult
    {
        public double LineRate { get; init; }
        public double BranchRate { get; init; }
        public int LinesCovered { get; init; }
        public int LinesValid { get; init; }
        public Dictionary<string, Dictionary<int, int>> FileLineHits { get; init; } = new();
    }

    public static CoverageResult? ParseFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return null;

            double lineRate = double.TryParse(root.Attribute("line-rate")?.Value, out var lr) ? lr : 0;
            double branchRate = double.TryParse(root.Attribute("branch-rate")?.Value, out var br) ? br : 0;

            var fileLineHits = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
            int totalCovered = 0, totalValid = 0;

            foreach (var cls in root.Descendants("class"))
            {
                string filename = cls.Attribute("filename")?.Value ?? "";
                if (string.IsNullOrEmpty(filename)) continue;

                var lineHits = new Dictionary<int, int>();
                foreach (var line in cls.Descendants("line"))
                {
                    int number = int.TryParse(line.Attribute("number")?.Value, out var n) ? n : 0;
                    int hits = int.TryParse(line.Attribute("hits")?.Value, out var h) ? h : 0;
                    if (number > 0)
                    {
                        lineHits[number] = hits;
                        totalValid++;
                        if (hits > 0) totalCovered++;
                    }
                }

                if (lineHits.Count > 0)
                    fileLineHits[filename] = lineHits;
            }

            return new CoverageResult
            {
                LineRate = lineRate,
                BranchRate = branchRate,
                LinesCovered = totalCovered,
                LinesValid = totalValid,
                FileLineHits = fileLineHits
            };
        }
        catch { return null; }
    }

    public static CoverageResult? RunTestsWithCoverage(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath)) return null;

        string tempDir = Path.Combine(Path.GetTempPath(), "pfpad_coverage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --collect \"Code Coverage\" --results-directory \"{tempDir}\" --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return null;
            proc.WaitForExit(300000); // 5 min timeout

            // Find the Cobertura XML file
            var coberturaFiles = Directory.EnumerateFiles(tempDir, "*.cobertura.xml", SearchOption.AllDirectories).ToList();
            if (coberturaFiles.Count == 0) return null;

            return ParseFile(coberturaFiles[0]);
        }
        catch { return null; }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

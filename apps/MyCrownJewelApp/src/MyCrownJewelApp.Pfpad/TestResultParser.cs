using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyCrownJewelApp.Pfpad;

public static class TestResultParser
{
    private static readonly Regex SummaryLine = new(
        @"(?:Passed|Failed|Skipped)\s*(?:\w+\s*)?(\d+).*?(?:Passed|Failed|Skipped)\s*(?:\w+\s*)?(\d+).*?(?:Passed|Failed|Skipped)\s*(?:\w+\s*)?(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TotalLine = new(
        @"Total\s*:\s*(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TestRunResult RunTests(string projectPath)
    {
        var result = new TestRunResult();

        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
            return result;

        string tempDir = Path.Combine(Path.GetTempPath(), "pfpad_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string trxFile = Path.Combine(tempDir, "results.trx");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --logger \"trx;LogFileName={trxFile}\" --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return result;

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(300000);

            result.RawOutput = stdout;
            result.RawError = stderr;

            // Parse summary from stdout
            ParseSummaryFromOutput(stdout, result);

            // Parse TRX file if it exists
            if (File.Exists(trxFile))
                ParseTrxFile(trxFile, result);
        }
        catch { }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }

        return result;
    }

    public static TestRunResult ParseTrxFile(string trxPath)
    {
        var result = new TestRunResult();
        ParseTrxFile(trxPath, result);
        return result;
    }

    private static void ParseTrxFile(string path, TestRunResult result)
    {
        try
        {
            var doc = XDocument.Load(path);
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

            var results = doc.Descendants(ns + "UnitTestResult").ToList();
            var definitions = doc.Descendants(ns + "UnitTest")
                .ToDictionary(
                    d => (string?)d.Attribute("id") ?? "",
                    d => (string?)d.Attribute("name") ?? ""
                );

            foreach (var r in results)
            {
                string testId = (string?)r.Attribute("testId") ?? "";
                string testName = (string?)r.Attribute("testName") ?? "";
                string outcome = (string?)r.Attribute("outcome") ?? "Passed";
                string duration = (string?)r.Attribute("duration") ?? "00:00:00";

                // Parse duration
                long ms = 0;
                if (TimeSpan.TryParse(duration, out var ts))
                    ms = (long)ts.TotalMilliseconds;

                TestOutcome o = outcome switch
                {
                    "Failed" => TestOutcome.Failed,
                    "NotExecuted" => TestOutcome.Skipped,
                    _ => TestOutcome.Passed
                };

                string errorMsg = "";
                string stackTrace = "";
                var errorInfo = r.Descendants(ns + "ErrorInfo").FirstOrDefault();
                if (errorInfo != null)
                {
                    errorMsg = (string?)errorInfo.Descendants(ns + "Message").FirstOrDefault() ?? "";
                    stackTrace = (string?)errorInfo.Descendants(ns + "StackTrace").FirstOrDefault() ?? "";
                }

                var frames = StackTraceParser.Parse(stackTrace);
                var stackFrames = frames.Select(f => (f.file, f.line)).ToList();

                result.Tests.Add(new TestResult
                {
                    TestName = testName,
                    Outcome = o,
                    DurationMs = ms,
                    ErrorMessage = errorMsg,
                    StackTrace = stackTrace,
                    StackFrames = stackFrames
                });
            }

            // Update counts
            result.Total = result.Tests.Count;
            result.Passed = result.Tests.Count(t => t.Outcome == TestOutcome.Passed);
            result.Failed = result.Tests.Count(t => t.Outcome == TestOutcome.Failed);
            result.Skipped = result.Tests.Count(t => t.Outcome == TestOutcome.Skipped);
        }
        catch { }
    }

    public static void ParseSummaryFromOutput(string output, TestRunResult result)
    {
        // Look for lines like: "Passed! - Failed: 2, Passed: 10, Skipped: 1, Total: 13"
        // Or: "  Passed 10, Failed 2, Skipped 1"
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            // Try total line first
            var m = TotalLine.Match(line);
            if (m.Success)
            {
                result.Total = int.TryParse(m.Groups[1].Value, out int t) ? t : 0;
            }

            // Try summary line with counts
            m = Regex.Match(line, @"(?:Passed|Failed|Skipped)[\s:]+\d+", RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            // Extract individual counts
            var passed = Regex.Match(line, @"Passed[:\s]+(\d+)", RegexOptions.IgnoreCase);
            var failed = Regex.Match(line, @"Failed[:\s]+(\d+)", RegexOptions.IgnoreCase);
            var skipped = Regex.Match(line, @"Skipped[:\s]+(\d+)", RegexOptions.IgnoreCase);

            if (passed.Success) result.Passed = int.Parse(passed.Groups[1].Value);
            if (failed.Success) result.Failed = int.Parse(failed.Groups[1].Value);
            if (skipped.Success) result.Skipped = int.Parse(skipped.Groups[1].Value);
        }

        // Fallback to TRX counts if TRX didn't have definitions
        if (result.Total == 0)
            result.Total = result.Passed + result.Failed + result.Skipped;
    }
}

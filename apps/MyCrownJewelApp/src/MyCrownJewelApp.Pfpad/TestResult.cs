using System;
using System.Collections.Generic;

namespace MyCrownJewelApp.Pfpad;

public enum TestOutcome
{
    Passed,
    Failed,
    Skipped
}

public sealed class TestResult
{
    public string TestName { get; init; } = "";
    public TestOutcome Outcome { get; init; }
    public long DurationMs { get; init; }
    public string ErrorMessage { get; init; } = "";
    public string StackTrace { get; init; } = "";
    public List<(string file, int line)> StackFrames { get; init; } = new();
}

public sealed class TestRunResult
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<TestResult> Tests { get; init; } = new();
    public string RawOutput { get; set; } = "";
    public string RawError { get; set; } = "";
}

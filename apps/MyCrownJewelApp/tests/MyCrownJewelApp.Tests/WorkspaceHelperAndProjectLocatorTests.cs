using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class WorkspaceHelperTests : IDisposable
{
    private readonly string _tmp;

    public WorkspaceHelperTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmp);
    }

    public void Dispose() => Directory.Delete(_tmp, true);

    private string Touch(string fileName)
    {
        string path = Path.Combine(_tmp, fileName);
        File.WriteAllText(path, "");
        return path;
    }

    // ── DetectLanguage ─────────────────────────────────────────────────────

    [Fact] public void DetectLanguage_CSharp_FromCsproj()
    {
        Touch("MyApp.csproj");
        Assert.Equal("C#", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_CSharp_FromSln()
    {
        Touch("MySolution.sln");
        Assert.Equal("C#", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Python_FromPy()
    {
        Touch("main.py");
        Assert.Equal("Python", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Python_FromRequirements()
    {
        Touch("requirements.txt");
        Assert.Equal("Python", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Rust_FromCargoToml()
    {
        Touch("Cargo.toml");
        Assert.Equal("Rust", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_JavaScript_FromPackageJson()
    {
        Touch("package.json");
        Assert.Equal("JavaScript", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Go_FromGoFile()
    {
        Touch("main.go");
        Assert.Equal("Go", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Java_FromPomXml()
    {
        Touch("pom.xml");
        Assert.Equal("Java", WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_Unknown_ReturnsNull()
    {
        Touch("README.md");
        Assert.Null(WorkspaceHelper.DetectLanguage(_tmp));
    }

    [Fact] public void DetectLanguage_EmptyDirectory_ReturnsNull()
    {
        Assert.Null(WorkspaceHelper.DetectLanguage(_tmp));
    }

    // ── SearchDirectory ────────────────────────────────────────────────────

    [Fact] public void SearchDirectory_FindsMatchingLine()
    {
        File.WriteAllText(Path.Combine(_tmp, "a.cs"), "hello world\nsecond line");
        var results = new List<(string File, int Line, string Text)>();
        WorkspaceHelper.SearchDirectory(_tmp, _tmp, results,
            new HashSet<string> { ".cs" }, new HashSet<string>(),
            "hello", null, StringComparison.OrdinalIgnoreCase);
        Assert.Single(results);
        Assert.Equal(1, results[0].Line);
        Assert.Contains("hello", results[0].Text);
    }

    [Fact] public void SearchDirectory_SkipsUnknownExtensions()
    {
        File.WriteAllText(Path.Combine(_tmp, "a.bin"), "hello");
        var results = new List<(string File, int Line, string Text)>();
        WorkspaceHelper.SearchDirectory(_tmp, _tmp, results,
            new HashSet<string> { ".cs" }, new HashSet<string>(),
            "hello", null, StringComparison.Ordinal);
        Assert.Empty(results);
    }

    [Fact] public void SearchDirectory_UsesRegex()
    {
        File.WriteAllText(Path.Combine(_tmp, "a.cs"), "foo123\nbarXYZ");
        var results = new List<(string File, int Line, string Text)>();
        WorkspaceHelper.SearchDirectory(_tmp, _tmp, results,
            new HashSet<string> { ".cs" }, new HashSet<string>(),
            "", new Regex(@"\d+"), StringComparison.Ordinal);
        Assert.Single(results);
        Assert.Equal(1, results[0].Line);
    }

    [Fact] public void SearchDirectory_SkipsIgnoredSubdirectory()
    {
        string sub = Path.Combine(_tmp, "node_modules");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "a.js"), "hello");
        var results = new List<(string File, int Line, string Text)>();
        WorkspaceHelper.SearchDirectory(_tmp, _tmp, results,
            new HashSet<string> { ".js" }, new HashSet<string> { "node_modules" },
            "hello", null, StringComparison.Ordinal);
        Assert.Empty(results);
    }
}

public class ProjectLocatorTests : IDisposable
{
    private readonly string _tmp;

    public ProjectLocatorTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmp);
    }

    public void Dispose() => Directory.Delete(_tmp, true);

    [Fact] public void FindProjectDirectory_FindsCsproj_InSameDirectory()
    {
        File.WriteAllText(Path.Combine(_tmp, "MyApp.csproj"), "<Project/>");
        string filePath = Path.Combine(_tmp, "Program.cs");
        File.WriteAllText(filePath, "");
        Assert.Equal(_tmp, ProjectLocator.FindProjectDirectory(filePath));
    }

    [Fact] public void FindProjectDirectory_FindsCsproj_InParent()
    {
        File.WriteAllText(Path.Combine(_tmp, "MyApp.csproj"), "<Project/>");
        string sub = Path.Combine(_tmp, "src");
        Directory.CreateDirectory(sub);
        string filePath = Path.Combine(sub, "Class1.cs");
        File.WriteAllText(filePath, "");
        Assert.Equal(_tmp, ProjectLocator.FindProjectDirectory(filePath));
    }

    [Fact] public void FindProjectDirectory_ReturnsNull_WhenNoCsproj()
    {
        string filePath = Path.Combine(_tmp, "readme.md");
        File.WriteAllText(filePath, "");
        Assert.Null(ProjectLocator.FindProjectDirectory(filePath));
    }

    [Fact] public void FindOutputAssembly_ReturnsNull_WhenNoCsproj()
    {
        Assert.Null(ProjectLocator.FindOutputAssembly(_tmp));
    }

    [Fact] public void FindOutputAssembly_ReturnsNull_WhenDllMissing()
    {
        File.WriteAllText(Path.Combine(_tmp, "MyApp.csproj"), "<Project/>");
        Assert.Null(ProjectLocator.FindOutputAssembly(_tmp));
    }

    [Fact] public void FindOutputAssembly_ReturnsPath_WhenNet9DllExists()
    {
        File.WriteAllText(Path.Combine(_tmp, "MyApp.csproj"), "<Project/>");
        string dllDir = Path.Combine(_tmp, "bin", "Debug", "net9.0");
        Directory.CreateDirectory(dllDir);
        string dll = Path.Combine(dllDir, "MyApp.dll");
        File.WriteAllText(dll, "");
        Assert.Equal(dll, ProjectLocator.FindOutputAssembly(_tmp));
    }
}

public class ContentHashTests
{
    [Fact] public void ComputeContentHash_EmptyString_ReturnsStableUpperHexDigest()
    {
        string hash = WorkspaceHelper.ComputeContentHash(string.Empty);

        hash.Should().MatchRegex("^[0-9A-F]{16}$");
        hash.Should().Be(WorkspaceHelper.ComputeContentHash(string.Empty));
    }

    [Fact] public void ComputeContentHash_SameInputs_ReturnsSameHash()
    {
        string h1 = WorkspaceHelper.ComputeContentHash("hello world");
        string h2 = WorkspaceHelper.ComputeContentHash("hello world");
        Assert.Equal(h1, h2);
    }

    [Fact] public void ComputeContentHash_DifferentInputs_ReturnsDifferentHashes()
    {
        string h1 = WorkspaceHelper.ComputeContentHash("hello world");
        string h2 = WorkspaceHelper.ComputeContentHash("Hello World");
        Assert.NotEqual(h1, h2);
    }

    [Fact] public void ComputeContentHash_WhitespaceMatters()
    {
        string h1 = WorkspaceHelper.ComputeContentHash("a b");
        string h2 = WorkspaceHelper.ComputeContentHash("ab");
        Assert.NotEqual(h1, h2);
    }

    [Fact] public void ComputeContentHash_ReturnsUpperHex()
    {
        string hash = WorkspaceHelper.ComputeContentHash("test");
        hash.Should().MatchRegex("^[0-9A-F]{16}$");
    }

    [Fact] public void ComputeContentHash_MultiLineContent_IsStable()
    {
        string content = "line1\nline2\nline3";
        string h1 = WorkspaceHelper.ComputeContentHash(content);
        string h2 = WorkspaceHelper.ComputeContentHash(content);
        Assert.Equal(h1, h2);
    }
}

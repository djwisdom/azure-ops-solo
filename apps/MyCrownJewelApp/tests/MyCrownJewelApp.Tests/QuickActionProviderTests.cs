using System.IO;
using FluentAssertions;
using MyCrownJewelApp.Pfpad;
using MyCrownJewelApp.Pfpad.Roslyn;
using Xunit;

namespace MyCrownJewelApp.Tests;

public class QuickActionProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _documentPath;

    public QuickActionProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"quickactions_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _documentPath = Path.Combine(_tempDir, "Sample.cs");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public void GetActions_WithoutUsingKeyword_DoesNotAddUsingSuggestions()
    {
        string text = "<div>\nUnknownType\nUnknownType\n</div>";

        using var workspace = CreateWorkspace(text);

        CreateProvider(workspace).GetActions(text, []).Should().BeEmpty();
    }

    [Fact]
    public void GetActions_WithUnknownTypeAndUsingKeyword_AddsUsingSuggestion()
    {
        string text = """
using System;

namespace Demo.Tools;
public class UnknownType { }

public class Consumer
{
    UnknownType value = new();
}
""";

        using var workspace = CreateWorkspace(text);

        var actions = CreateProvider(workspace).GetActions(text, []);

        actions.Should().Contain(a => a.DiagnosticRuleId == "using" && a.Title == "using Demo.Tools");
    }

    [Fact]
    public void GetActions_WithExistingMatchingUsing_DoesNotDuplicateSuggestion()
    {
        string text = """
using System;
using Demo.Tools;

namespace Demo.Tools;
public class UnknownType { }

public class Consumer
{
    UnknownType value = new();
}
""";

        using var workspace = CreateWorkspace(text);

        var actions = CreateProvider(workspace).GetActions(text, []);

        actions.Should().NotContain(a => a.DiagnosticRuleId == "using" && a.Title == "using Demo.Tools");
    }

    private RoslynWorkspaceService CreateWorkspace(string text)
    {
        File.WriteAllText(_documentPath, text);
        var workspace = new RoslynWorkspaceService();
        workspace.OpenDocument(_documentPath);
        workspace.UpdateDocumentText(text);
        return workspace;
    }

    private static QuickActionProvider CreateProvider(IRoslynWorkspace workspace)
        => new(new SymbolIndexService(), workspace);
}

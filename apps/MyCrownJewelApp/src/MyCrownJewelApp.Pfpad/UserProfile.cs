namespace MyCrownJewelApp.Pfpad;

public record UserProfile(
    string Name,
    string? WorkspaceRoot,
    string BuildCommand = "dotnet build",
    string RunCommand = "dotnet run",
    string TestCommand = "dotnet test",
    int? OverrideTabSize = null,
    bool? OverrideInsertSpaces = null,
    float? OverrideFontSize = null,
    string? OverrideThemeName = null,
    bool? OverrideWordWrap = null
);
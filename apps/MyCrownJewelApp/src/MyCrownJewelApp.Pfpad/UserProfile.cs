namespace MyCrownJewelApp.Pfpad;

public record UserProfile(
    string Name,
    string? WorkspaceRoot,
    string BuildCommand = "dotnet build",
    string RunCommand = "dotnet run",
    string TestCommand = "dotnet test"
);
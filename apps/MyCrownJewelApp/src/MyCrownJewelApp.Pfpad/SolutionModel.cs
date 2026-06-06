namespace MyCrownJewelApp.Pfpad;

public record SolutionProject(
    string Name,
    string RelativePath,
    string TypeGuid,
    string ProjectGuid,
    string? SolutionFolderName);

public record SolutionFolder(string Name, string Guid);

public record PackageRef(string Name, string Version);
public record ProjectRef(string Name, string RelativePath);
public record ProjectFileItem(string RelativePath, string ItemType);

public class ProjectData
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? TargetFramework { get; init; }
    public List<PackageRef> Packages { get; init; } = new();
    public List<ProjectRef> ProjectRefs { get; init; } = new();
    public List<ProjectFileItem> Files { get; init; } = new();
    public bool IsLoaded { get; set; } = true;
}

public class SolutionFile
{
    public string Path { get; init; } = string.Empty;
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public List<SolutionProject> Projects { get; init; } = new();
    public List<SolutionFolder> Folders { get; init; } = new();
}

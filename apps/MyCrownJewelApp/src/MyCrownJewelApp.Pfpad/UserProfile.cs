namespace MyCrownJewelApp.Pfpad;

using System.Text.Json.Serialization;

/// <summary>
/// Represents workspace metadata and configuration.
/// Supports rich workspace information similar to VS Code workspaces.
/// </summary>
public record WorkspaceInfo
{
    /// <summary>
    /// Display name for the workspace (auto-detected from folder name if not set).
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// User-friendly description of this workspace.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Absolute path to the workspace root folder.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// When this workspace was last opened.
    /// </summary>
    public DateTime LastOpened { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Path to workspace icon (optional, falls back to project type icon).
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Whether this workspace is trusted (affects security restrictions).
    /// </summary>
    public bool IsTrusted { get; init; } = true;

    /// <summary>
    /// Auto-detected project type (dotnet, node, python, etc.).
    /// </summary>
    public string? ProjectType { get; init; }

    /// <summary>
    /// Returns the display name, falling back to folder name.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => !string.IsNullOrEmpty(Name) ? Name :
        !string.IsNullOrEmpty(Path) ? System.IO.Path.GetFileName(Path) ?? "Unknown" : "Unnamed Workspace";

    /// <summary>
    /// Returns a friendly "last opened" string.
    /// </summary>
    [JsonIgnore]
    public string LastOpenedDisplay => LastOpened.ToLocalTime().ToString("g");

    /// <summary>
    /// Returns project type display name with icon.
    /// </summary>
    [JsonIgnore]
    public string ProjectTypeDisplay
    {
        get
        {
            try
            {
                if (string.IsNullOrEmpty(ProjectType)) return "";
                return ProjectType switch
                {
                    "dotnet" => "🔷 .NET",
                    "node" => "🟢 Node.js",
                    "python" => "🐍 Python",
                    "java" => "☕ Java",
                    "go" => "🐹 Go",
                    "rust" => "🦀 Rust",
                    "cpp" => "⚙️ C++",
                    "web" => "🌐 Web",
                    _ => $"📁 {ProjectType}"
                };
            }
            catch
            {
                return "";
            }
        }
    }
}

/// <summary>
/// Represents a user profile that encapsulates workspace, commands, and settings overrides.
/// Profiles allow users to quickly switch between different development environments.
/// </summary>
public record UserProfile
{
    /// <summary>
    /// Unique profile name (required, non-empty, not "Default").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// User-friendly description of this profile's purpose (optional, max 200 chars).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Primary workspace associated with this profile (optional).
    /// </summary>
    public WorkspaceInfo? PrimaryWorkspace { get; init; }

    /// <summary>
    /// List of recently used workspaces for this profile (max 10).
    /// </summary>
    public List<WorkspaceInfo> RecentWorkspaces { get; init; } = new();

    /// <summary>
    /// Build command template (e.g., "dotnet build").
    /// </summary>
    public string BuildCommand { get; init; } = "dotnet build";

    /// <summary>
    /// Run command template (e.g., "dotnet run").
    /// </summary>
    public string RunCommand { get; init; } = "dotnet run";

    /// <summary>
    /// Test command template (e.g., "dotnet test").
    /// </summary>
    public string TestCommand { get; init; } = "dotnet test";

    /// <summary>
    /// Icon identifier for the profile (0 = no icon, 1-30 = icon gallery).
    /// </summary>
    public int IconId { get; init; } = 0;

    /// <summary>
    /// Accent color hex string for profile (e.g., "#0078D4").
    /// </summary>
    public string? ColorHex { get; init; }

    /// <summary>
    /// When this profile was first created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this profile was activated (UTC).
    /// </summary>
    public DateTime LastUsed { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this profile has been activated.
    /// </summary>
    public int UsageCount { get; init; }

    /// <summary>
    /// If set, overrides the global tab size setting.
    /// </summary>
    public int? OverrideTabSize { get; init; }

    /// <summary>
    /// If set, overrides the global "insert spaces" setting.
    /// </summary>
    public bool? OverrideInsertSpaces { get; init; }

    /// <summary>
    /// If set, overrides the global font size setting.
    /// </summary>
    public float? OverrideFontSize { get; init; }

    /// <summary>
    /// If set, overrides the global font family setting.
    /// </summary>
    public string? OverrideFontName { get; init; }

    /// <summary>
    /// If set, overrides the global theme setting.
    /// </summary>
    public string? OverrideThemeName { get; init; }

    /// <summary>
    /// If set, overrides the global word wrap setting.
    /// </summary>
    public bool? OverrideWordWrap { get; init; }

    /// <summary>
    /// If set, overrides global gutter visibility.
    /// </summary>
    public bool? OverrideGutterVisible { get; init; }

    /// <summary>
    /// If set, overrides global status bar visibility.
    /// </summary>
    public bool? OverrideStatusBarVisible { get; init; }

    /// <summary>
    /// If set, overrides global column guide setting.
    /// </summary>
    public bool? OverrideShowGuide { get; init; }

    /// <summary>
    /// If set, overrides global guide column position.
    /// </summary>
    public int? OverrideGuideColumn { get; init; }

    /// <summary>
    /// If set, overrides global minimap visibility.
    /// </summary>
    public bool? OverrideMinimapVisible { get; init; }

    /// <summary>
    /// If set, overrides global rainbow brackets setting.
    /// </summary>
    public bool? OverrideRainbowBrackets { get; init; }

    /// <summary>
    /// If set, overrides global breadcrumbs setting.
    /// </summary>
    public bool? OverrideBreadcrumbs { get; init; }

    /// <summary>
    /// If set, overrides global hover line highlight setting.
    /// </summary>
    public bool? OverrideHoverLineHighlight { get; init; }

    /// <summary>
    /// If set, overrides global auto-save setting.
    /// </summary>
    public bool? OverrideAutoSave { get; init; }

    /// <summary>
    /// If set, overrides global Roslyn analyzers setting.
    /// </summary>
    public bool? OverrideAnalyzersEnabled { get; init; }

    /// <summary>
    /// If set, overrides the terminal shell path.
    /// </summary>
    public string? OverrideTerminalShell { get; init; }

    /// <summary>
    /// If set, overrides the terminal height (pixels).
    /// </summary>
    public int? OverrideTerminalHeight { get; init; }

    /// <summary>
    /// If set, overrides global Vim mode setting.
    /// </summary>
    public bool? OverrideVimMode { get; init; }

    /// <summary>
    /// If set, overrides global sticky scroll setting.
    /// </summary>
    public bool? OverrideStickyScroll { get; init; }

    /// <summary>
    /// If set, overrides global syntax highlighting setting.
    /// </summary>
    public bool? OverrideSyntaxHighlighting { get; init; }

    /// <summary>
    /// Computed display color from ColorHex, falls back to theme blue.
    /// </summary>
    [JsonIgnore]
    public Color DisplayColor => TryParseColorHex(ColorHex ?? "#0078D4", out var c) ? c : Color.FromArgb(0, 120, 215);

    /// <summary>
    /// Returns a friendly "last used" string.
    /// </summary>
    [JsonIgnore]
    public string LastUsedDisplay => LastUsed == default ? "Never" : LastUsed.ToLocalTime().ToString("g");

    private static bool TryParseColorHex(string hex, out Color color)
    {
        color = Color.FromArgb(0, 120, 215);
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return false;
        try
        {
            int r = int.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
            color = Color.FromArgb(r, g, b);
            return true;
        }
        catch { return false; }
    }
}

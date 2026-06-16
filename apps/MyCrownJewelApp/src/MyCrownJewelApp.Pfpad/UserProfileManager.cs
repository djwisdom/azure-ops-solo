using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Legacy UserProfile format for migration from old data model.
/// </summary>
internal record LegacyUserProfile
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? WorkspaceRoot { get; init; }
    public List<string> Workspaces { get; init; } = new();
    public string BuildCommand { get; init; } = "dotnet build";
    public string RunCommand { get; init; } = "dotnet run";
    public string TestCommand { get; init; } = "dotnet test";
    public int IconId { get; init; } = 0;
    public string? ColorHex { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastUsed { get; init; } = DateTime.UtcNow;
    public int UsageCount { get; init; }
    public int? OverrideTabSize { get; init; }
    public bool? OverrideInsertSpaces { get; init; }
    public float? OverrideFontSize { get; init; }
    public string? OverrideFontName { get; init; }
    public string? OverrideThemeName { get; init; }
    public bool? OverrideWordWrap { get; init; }
    public bool? OverrideGutterVisible { get; init; }
    public bool? OverrideStatusBarVisible { get; init; }
    public bool? OverrideShowGuide { get; init; }
    public int? OverrideGuideColumn { get; init; }
    public bool? OverrideMinimapVisible { get; init; }
    public bool? OverrideRainbowBrackets { get; init; }
    public bool? OverrideBreadcrumbs { get; init; }
    public bool? OverrideHoverLineHighlight { get; init; }
    public bool? OverrideAutoSave { get; init; }
    public bool? OverrideAnalyzersEnabled { get; init; }
    public string? OverrideTerminalShell { get; init; }
    public int? OverrideTerminalHeight { get; init; }
    public bool? OverrideVimMode { get; init; }
    public bool? OverrideStickyScroll { get; init; }
    public bool? OverrideSyntaxHighlighting { get; init; }
}

/// <summary>
/// Manages user profiles including persistence, loading, saving, and activation tracking.
/// </summary>
public sealed class UserProfileManager : IDisposable
{
    private readonly string _profilesDir;
    private readonly string _activeProfilePath;
    private readonly Dictionary<string, UserProfile> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Fired when a profile is saved, deleted, or the active profile changes.
    /// </summary>
    public event Action<UserProfile>? ProfileChanged;
    public event Action<string>? ProfileDeleted;
    public event Action? ProfilesReloaded;

    public UserProfileManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _profilesDir = Path.Combine(appData, "MyCrownJewelApp", "Pfpad", "Profiles");
        _activeProfilePath = Path.Combine(_profilesDir, "active.txt");
        Directory.CreateDirectory(_profilesDir);
        EnsureDefaultProfile();
    }

    #region Active Profile

    public string? ActiveProfileName
    {
        get
        {
            try
            {
                if (File.Exists(_activeProfilePath))
                {
                    string name = File.ReadAllText(_activeProfilePath).Trim();
                    return string.IsNullOrEmpty(name) ? null : name;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error reading active profile: {ex.Message}");
            }
            return null;
        }
        set
        {
            try
            {
                string? safeValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                File.WriteAllText(_activeProfilePath, safeValue ?? string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error writing active profile: {ex.Message}");
                ThemedMessageBox.Show($"Failed to save active profile setting: {ex.Message}", "Profile Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    public UserProfile? ActiveProfile
    {
        get
        {
            string? name = ActiveProfileName;
            if (string.IsNullOrEmpty(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                return DefaultProfile;
            return LoadProfile(name);
        }
    }

    #endregion

    #region Default Profile

    public static UserProfile DefaultProfile { get; } = new()
    {
        Name = "Default",
        Workspaces = new List<WorkspaceInfo>(),
        BuildCommand = "dotnet build",
        RunCommand = "dotnet run",
        TestCommand = "dotnet test",
        IconId = 0,
        ColorHex = "#0078D4",
        CreatedAt = DateTime.UtcNow,
        LastUsed = DateTime.UtcNow,
        UsageCount = 0
    };

    #endregion

    #region Profile Enumeration

    /// <summary>
    /// Gets all available profile names (including "Default").
    /// </summary>
    public string[] ProfileNames
    {
        get
        {
            try
            {
                var names = new SortedSet<string> { "Default" };
                foreach (string file in Directory.GetFiles(_profilesDir, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name) && !name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                        names.Add(name);
                }
                return names.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error enumerating profiles: {ex.Message}");
                return new[] { "Default" };
            }
        }
    }

    /// <summary>
    /// Gets all profiles with metadata (does not load file content).
    /// </summary>
    public IEnumerable<UserProfile> GetAllProfiles()
    {
        foreach (string name in ProfileNames)
        {
            if (name == "Default")
                yield return DefaultProfile;
            else
            {
                // Try cache first, then disk
                lock (_lock)
                {
                    if (_cache.TryGetValue(name, out var cached))
                    {
                        yield return cached;
                        continue;
                    }
                }
                var profile = LoadProfile(name);
                if (profile != null) yield return profile;
            }
        }
    }

    #endregion

    #region Migration

    /// <summary>
    /// Migrates a legacy profile format to the new WorkspaceInfo-based structure.
    /// </summary>
    private static UserProfile? MigrateLegacyProfile(LegacyUserProfile legacy)
    {
        try
        {
            // Create primary workspace from WorkspaceRoot
            WorkspaceInfo? primaryWorkspace = null;
            if (!string.IsNullOrEmpty(legacy.WorkspaceRoot))
            {
                primaryWorkspace = new WorkspaceInfo
                {
                    Name = System.IO.Path.GetFileName(legacy.WorkspaceRoot) ?? "Workspace",
                    Path = legacy.WorkspaceRoot,
                    LastOpened = legacy.LastUsed,
                    IsTrusted = true,
                    ProjectType = DetectProjectType(legacy.WorkspaceRoot)
                };
            }

            // Convert Workspaces list to WorkspaceInfo list
            var recentWorkspaces = new List<WorkspaceInfo>();
            foreach (string workspacePath in legacy.Workspaces ?? new List<string>())
            {
                if (!string.IsNullOrEmpty(workspacePath) && Directory.Exists(workspacePath))
                {
                    recentWorkspaces.Add(new WorkspaceInfo
                    {
                        Name = System.IO.Path.GetFileName(workspacePath) ?? "Workspace",
                        Path = workspacePath,
                        LastOpened = DateTime.UtcNow,
                        IsTrusted = true,
                        ProjectType = DetectProjectType(workspacePath)
                    });
                }
            }

            // Create migrated profile
            return new UserProfile
            {
                Name = legacy.Name,
                Description = legacy.Description,
                Workspaces = primaryWorkspace != null ? new List<WorkspaceInfo> { primaryWorkspace } : new List<WorkspaceInfo>(),
                RecentWorkspaces = recentWorkspaces,
                BuildCommand = legacy.BuildCommand ?? "dotnet build",
                RunCommand = legacy.RunCommand ?? "dotnet run",
                TestCommand = legacy.TestCommand ?? "dotnet test",
                IconId = legacy.IconId,
                ColorHex = legacy.ColorHex,
                CreatedAt = legacy.CreatedAt,
                LastUsed = legacy.LastUsed,
                UsageCount = legacy.UsageCount,
                OverrideTabSize = legacy.OverrideTabSize,
                OverrideInsertSpaces = legacy.OverrideInsertSpaces,
                OverrideFontSize = legacy.OverrideFontSize,
                OverrideFontName = legacy.OverrideFontName,
                OverrideThemeName = legacy.OverrideThemeName,
                OverrideWordWrap = legacy.OverrideWordWrap,
                OverrideGutterVisible = legacy.OverrideGutterVisible,
                OverrideStatusBarVisible = legacy.OverrideStatusBarVisible,
                OverrideShowGuide = legacy.OverrideShowGuide,
                OverrideGuideColumn = legacy.OverrideGuideColumn,
                OverrideMinimapVisible = legacy.OverrideMinimapVisible,
                OverrideRainbowBrackets = legacy.OverrideRainbowBrackets,
                OverrideBreadcrumbs = legacy.OverrideBreadcrumbs,
                OverrideHoverLineHighlight = legacy.OverrideHoverLineHighlight,
                OverrideAutoSave = legacy.OverrideAutoSave,
                OverrideAnalyzersEnabled = legacy.OverrideAnalyzersEnabled,
                OverrideTerminalShell = legacy.OverrideTerminalShell,
                OverrideTerminalHeight = legacy.OverrideTerminalHeight,
                OverrideVimMode = legacy.OverrideVimMode,
                OverrideStickyScroll = legacy.OverrideStickyScroll,
                OverrideSyntaxHighlighting = legacy.OverrideSyntaxHighlighting
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error migrating legacy profile '{legacy.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Auto-detects project type from workspace contents.
    /// </summary>
    private static string? DetectProjectType(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return null;

            // Check for specific project files
            string[] files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
            var fileNames = files.Select(f => Path.GetFileName(f).ToLowerInvariant()).ToArray();

            if (fileNames.Contains("package.json")) return "node";
            if (fileNames.Contains("requirements.txt") || fileNames.Contains("setup.py") || fileNames.Contains("pyproject.toml")) return "python";
            if (fileNames.Contains("pom.xml") || fileNames.Contains("build.gradle")) return "java";
            if (fileNames.Contains("go.mod")) return "go";
            if (fileNames.Contains("cargo.toml")) return "rust";
            if (fileNames.Contains("CMakeLists.txt") || fileNames.Any(f => f.EndsWith(".cpp") || f.EndsWith(".h"))) return "cpp";
            if (fileNames.Contains(".csproj") || fileNames.Contains(".sln") || fileNames.Contains("project.json")) return "dotnet";
            if (fileNames.Contains("index.html") || fileNames.Contains("index.htm")) return "web";

            // Check for common directories
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            var dirNames = dirs.Select(d => Path.GetFileName(d).ToLowerInvariant()).ToArray();

            if (dirNames.Contains("node_modules")) return "node";
            if (dirNames.Contains("venv") || dirNames.Contains("__pycache__")) return "python";

            return null; // Unknown
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Load / Save / Delete

    /// <summary>
    /// Loads a profile by name from cache or disk.
    /// Returns null if not found. Returns DefaultProfile for "Default" or null/empty.
    /// </summary>
    public UserProfile? LoadProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return DefaultProfile;

        // Check cache first
        lock (_lock)
        {
            if (_cache.TryGetValue(name, out var cached))
                return cached;
        }

        try
        {
            string path = Path.Combine(_profilesDir, $"{name}.json");
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);

            // Try to deserialize as new format first
            UserProfile? profile = null;
            try
            {
                profile = JsonSerializer.Deserialize<UserProfile>(json);
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] Failed to deserialize as new format, trying legacy: {ex.Message}");
                // Try legacy format
                try
                {
                    var legacy = JsonSerializer.Deserialize<LegacyUserProfile>(json);
                    if (legacy != null)
                    {
                        // Migrate to new format
                        profile = MigrateLegacyProfile(legacy);
                        if (profile != null)
                        {
                            // Save migrated profile back to disk
                            SaveProfile(profile);
                            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Migrated legacy profile '{name}' to new format");
                        }
                    }
                }
                catch (Exception legacyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileManager] Failed to deserialize legacy format: {legacyEx.Message}");
                }
            }

            if (profile != null)
            {
                // Migrate single workspace to multi-workspace format if needed
                if (profile.Workspaces.Count == 0 && profile.PrimaryWorkspace != null)
                {
                    profile = profile with { Workspaces = new List<WorkspaceInfo> { profile.PrimaryWorkspace } };
                    // Save the migrated profile
                    SaveProfile(profile);
                    System.Diagnostics.Debug.WriteLine($"[ProfileManager] Migrated profile '{name}' to multi-workspace format");
                }

                // Cache it
                lock (_lock) { _cache[name] = profile; }
                return profile;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error loading profile '{name}': {ex.Message}");
            ThemedMessageBox.Show($"Failed to load profile \"{name}\": {ex.Message}", "Profile Load Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return null;
    }

    /// <summary>
    /// Saves a profile to disk (skips Default).
    /// Updates the cache and raises ProfileChanged event.
    /// </summary>
    public void SaveProfile(UserProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return; // Skip default

        try
        {
            string path = Path.Combine(_profilesDir, $"{profile.Name}.json");
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);

            // Update cache
            lock (_lock) { _cache[profile.Name] = profile; }

            ProfileChanged?.Invoke(profile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error saving profile '{profile.Name}': {ex.Message}");
            ThemedMessageBox.Show($"Failed to save profile \"{profile.Name}\": {ex.Message}", "Profile Save Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            throw; // Re-throw so caller knows
        }
    }

    /// <summary>
    /// Deletes a profile from disk and cache (skips Default).
    /// Raises ProfileDeleted event.
    /// </summary>
    public void DeleteProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            string path = Path.Combine(_profilesDir, $"{name}.json");
            if (File.Exists(path))
                File.Delete(path);

            // Remove from cache
            lock (_lock) { _cache.Remove(name); }

            // If this was the active profile, clear active marker
            if (ActiveProfileName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                ActiveProfileName = null;

            ProfileDeleted?.Invoke(name);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Error deleting profile '{name}': {ex.Message}");
            ThemedMessageBox.Show($"Failed to delete profile \"{name}\": {ex.Message}", "Profile Delete Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            throw;
        }
    }

    /// <summary>
    /// Increments usage count and updates LastUsed timestamp. Called when profile is activated.
    /// </summary>
    public void IncrementUsage(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return;

        var profile = LoadProfile(name);
        if (profile is null) return;

        // Create updated copy with new stats
        var updated = profile with
        {
            LastUsed = DateTime.UtcNow,
            UsageCount = profile.UsageCount + 1
        };

        SaveProfile(updated);
    }

    /// <summary>
    /// Duplicates an existing profile with a new name.
    /// </summary>
    public UserProfile? DuplicateProfile(string sourceName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return null;

        var source = LoadProfile(sourceName);
        if (source == null) return null;

        var duplicate = source with
        {
            Name = newName,
            CreatedAt = DateTime.UtcNow,
            LastUsed = DateTime.MinValue,
            UsageCount = 0
        };

        SaveProfile(duplicate);
        return duplicate;
    }

    /// <summary>
    /// Renames an existing profile.
    /// </summary>
    public bool RenameProfile(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals("Default", StringComparison.OrdinalIgnoreCase))
            return false;

        var profile = LoadProfile(oldName);
        if (profile == null) return false;

        // Delete old
        DeleteProfile(oldName);

        // Save under new name
        var renamed = profile with { Name = newName };
        SaveProfile(renamed);
        return true;
    }

    #endregion

    #region Caching

    /// <summary>
    /// Clears the in-memory profile cache. Call after external file modifications.
    /// </summary>
    public void ClearCache()
    {
        lock (_lock) { _cache.Clear(); }
        ProfilesReloaded?.Invoke();
    }

    /// <summary>
    /// Replaces the active profile with a new one (used by Merge).
    /// </summary>
    public void UpdateActiveProfile(UserProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        SaveProfile(profile);
        ActiveProfileName = profile.Name;
    }

    #endregion

    #region Default Profile Setup

    private void EnsureDefaultProfile()
    {
        string path = Path.Combine(_profilesDir, "Default.json");
        if (!File.Exists(path))
        {
            try
            {
                SaveProfile(DefaultProfile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileManager] Could not create default profile: {ex.Message}");
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _cache.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

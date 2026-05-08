using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad;

public sealed class UserProfileManager
{
    private readonly string _profilesDir;
    private readonly string _activeProfilePath;

    public UserProfileManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _profilesDir = Path.Combine(appData, "MyCrownJewelApp", "Pfpad", "Profiles");
        _activeProfilePath = Path.Combine(_profilesDir, "active.txt");
        Directory.CreateDirectory(_profilesDir);
        EnsureDefaultProfile();
    }

    public string? ActiveProfileName
    {
        get
        {
            try
            {
                if (File.Exists(_activeProfilePath))
                    return File.ReadAllText(_activeProfilePath).Trim();
            }
            catch { }
            return null;
        }
        set
        {
            try
            {
                File.WriteAllText(_activeProfilePath, value ?? "");
            }
            catch { }
        }
    }

    public UserProfile? ActiveProfile
    {
        get
        {
            string? name = ActiveProfileName;
            if (string.IsNullOrEmpty(name) || name == "Default")
                return DefaultProfile;
            return LoadProfile(name);
        }
    }

    public static UserProfile DefaultProfile => new(
        Name: "Default",
        WorkspaceRoot: null,
        BuildCommand: "dotnet build",
        RunCommand: "dotnet run",
        TestCommand: "dotnet test"
    );

    public string[] ProfileNames
    {
        get
        {
            try
            {
                var names = new List<string> { "Default" };
                foreach (var f in Directory.GetFiles(_profilesDir, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (name != "Default" && !string.IsNullOrEmpty(name))
                        names.Add(name);
                }
                return names.ToArray();
            }
            catch { return new[] { "Default" }; }
        }
    }

    public UserProfile? LoadProfile(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "Default")
            return DefaultProfile;
        try
        {
            string path = Path.Combine(_profilesDir, $"{name}.json");
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserProfile>(json);
        }
        catch { return null; }
    }

    public void SaveProfile(UserProfile profile)
    {
        if (string.IsNullOrEmpty(profile.Name) || profile.Name == "Default") return;
        try
        {
            string path = Path.Combine(_profilesDir, $"{profile.Name}.json");
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public void DeleteProfile(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "Default") return;
        try
        {
            string path = Path.Combine(_profilesDir, $"{name}.json");
            if (File.Exists(path)) File.Delete(path);
            if (ActiveProfileName == name)
                ActiveProfileName = null;
        }
        catch { }
    }

    private void EnsureDefaultProfile()
    {
        string path = Path.Combine(_profilesDir, "Default.json");
        if (!File.Exists(path))
            SaveProfile(DefaultProfile);
    }
}
using System;
using System.IO;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Handles loading and saving <see cref="AppSettings"/> from/to disk.
/// Stateless: no cached settings state — Form1 owns the live settings fields.
/// </summary>
public sealed class SettingsService
{
    private readonly string _settingsPath;

    /// <summary>Production constructor — uses the standard AppData location.</summary>
    public SettingsService() : this(DefaultSettingsPath()) { }

    /// <summary>Testable constructor — caller supplies the file path.</summary>
    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public string SettingsFilePath => _settingsPath;

    public static string DefaultSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp",
            "TextEditor",
            "settings.json");

    /// <summary>
    /// Loads settings from disk. Returns <c>null</c> if the file does not exist
    /// or cannot be parsed — callers should keep their existing runtime defaults.
    /// A corrupt file is renamed to <c>.corrupt</c> before returning null.
    /// </summary>
    public AppSettings? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return null;

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch
        {
            TryBackupCorrupt();
            return null;
        }
    }

    /// <summary>
    /// Atomically saves settings to disk. Uses a unique temp file on the same
    /// volume, then <see cref="File.Replace"/> (if target exists) or
    /// <see cref="File.Move"/> (first write) to avoid partial-write windows.
    /// </summary>
    public void Save(AppSettings settings)
    {
        string? tmpPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            // Write to a unique temp file in the same directory (same volume) first.
            tmpPath = Path.Combine(
                Path.GetDirectoryName(_settingsPath)!,
                Path.GetFileNameWithoutExtension(_settingsPath)
                    + "." + Path.GetRandomFileName() + ".tmp");

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmpPath, json);

            if (File.Exists(_settingsPath))
                File.Replace(tmpPath, _settingsPath, null);
            else
                File.Move(tmpPath, _settingsPath);

            tmpPath = null; // successfully moved — don't delete
        }
        catch
        {
            // ignore save errors — editor still works without persistence
        }
        finally
        {
            if (tmpPath != null)
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
    }

    private void TryBackupCorrupt()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            string corrupt = _settingsPath + ".corrupt";
            if (File.Exists(corrupt)) File.Delete(corrupt);
            File.Move(_settingsPath, corrupt);
        }
        catch { }
    }
}

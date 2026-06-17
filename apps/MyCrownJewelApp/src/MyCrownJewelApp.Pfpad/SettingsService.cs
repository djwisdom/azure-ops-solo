using System;
using System.IO;
using System.Security.Cryptography;
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

    /// <summary>
    /// Saves with optional DPAPI encryption. When <paramref name="encrypt"/> is true the JSON bytes
    /// are protected with <see cref="DataProtectionScope.CurrentUser"/> before writing.
    /// </summary>
    public void Save(AppSettings settings, bool encrypt)
    {
        if (!encrypt) { Save(settings); return; }

        string? tmpPath = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            tmpPath = Path.Combine(
                Path.GetDirectoryName(_settingsPath)!,
                Path.GetFileNameWithoutExtension(_settingsPath)
                    + "." + Path.GetRandomFileName() + ".tmp");

            byte[] json = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            byte[] encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(tmpPath, encrypted);

            if (File.Exists(_settingsPath))
                File.Replace(tmpPath, _settingsPath, null);
            else
                File.Move(tmpPath, _settingsPath);
            tmpPath = null;
        }
        catch { }
        finally { if (tmpPath != null) try { File.Delete(tmpPath); } catch { } }
    }

    /// <summary>
    /// Loads settings, trying plain JSON first, then DPAPI-decryption as fallback.
    /// </summary>
    public AppSettings? LoadWithDecrypt()
    {
        if (!File.Exists(_settingsPath)) return null;
        try
        {
            string json = File.ReadAllText(_settingsPath);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
        }
        catch
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(_settingsPath);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string json = System.Text.Encoding.UTF8.GetString(decrypted);
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
            }
            catch
            {
                TryBackupCorrupt();
                return null;
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

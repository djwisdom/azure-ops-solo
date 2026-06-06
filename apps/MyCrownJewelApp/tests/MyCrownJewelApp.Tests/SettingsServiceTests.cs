using System;
using System.IO;
using System.Text.Json;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

/// <summary>
/// Tests for SettingsService file I/O. Each test uses a temp directory so it
/// never touches the real user profile. No WinForms, no STA thread needed.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly SettingsService _svc;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pfpad_settings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _svc = new SettingsService(_settingsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = _svc.Load();
        Assert.Null(result);
    }

    [Fact]
    public void Load_ReturnsNull_WhenJsonIsInvalid()
    {
        File.WriteAllText(_settingsPath, "{ this is not valid json }}}");
        var result = _svc.Load();
        Assert.Null(result);
    }

    [Fact]
    public void Load_BacksUpCorruptFile_WhenJsonIsInvalid()
    {
        File.WriteAllText(_settingsPath, "not json at all");
        _svc.Load();
        Assert.False(File.Exists(_settingsPath), "Original corrupt file should be removed");
        Assert.True(File.Exists(_settingsPath + ".corrupt"), "Corrupt backup should exist");
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileIsEmpty()
    {
        File.WriteAllText(_settingsPath, "");
        var result = _svc.Load();
        Assert.Null(result);
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesFile_WhenDirectoryExists()
    {
        var settings = MakeSettings();
        _svc.Save(settings);
        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_CreatesDirectory_WhenMissing()
    {
        string nestedPath = Path.Combine(_tempDir, "sub", "dir", "settings.json");
        var svc = new SettingsService(nestedPath);
        svc.Save(MakeSettings());
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Save_WritesValidJson()
    {
        _svc.Save(MakeSettings(tabSize: 4));
        string json = File.ReadAllText(_settingsPath);
        // Should parse without exception
        var doc = JsonDocument.Parse(json);
        Assert.Equal(4, doc.RootElement.GetProperty("TabSize").GetInt32());
    }

    [Fact]
    public void Save_DoesNotLeaveTemporaryFiles()
    {
        _svc.Save(MakeSettings());
        var files = Directory.GetFiles(_tempDir);
        Assert.Single(files); // only settings.json, no .tmp leftovers
    }

    // ── Roundtrip ────────────────────────────────────────────────────────────

    [Fact]
    public void Save_Then_Load_Roundtrip_PreservesAllFields()
    {
        var original = MakeSettings(
            tabSize: 2,
            fontName: "Consolas",
            fontSize: 14f,
            themeName: "Light",
            wordWrap: true,
            gutterVisible: false,
            autoSave: true,
            maxFileSizeMB: 250,
            thresholdBytes: 512 * 1024);

        _svc.Save(original);
        var loaded = _svc.Load();

        Assert.NotNull(loaded);
        Assert.Equal(original.TabSize, loaded!.TabSize);
        Assert.Equal(original.FontName, loaded.FontName);
        Assert.Equal(original.FontSize, loaded.FontSize);
        Assert.Equal(original.ThemeName, loaded.ThemeName);
        Assert.Equal(original.WordWrapEnabled, loaded.WordWrapEnabled);
        Assert.Equal(original.GutterVisible, loaded.GutterVisible);
        Assert.Equal(original.AutoSaveEnabled, loaded.AutoSaveEnabled);
        Assert.Equal(original.MaxFileSizeMB, loaded.MaxFileSizeMB);
        Assert.Equal(original.SyntaxHighlightingThresholdBytes, loaded.SyntaxHighlightingThresholdBytes);
    }

    [Fact]
    public void Save_OverwritesPreviousFile()
    {
        _svc.Save(MakeSettings(tabSize: 4));
        _svc.Save(MakeSettings(tabSize: 8));
        var loaded = _svc.Load();
        Assert.Equal(8, loaded!.TabSize);
    }

    [Fact]
    public void Load_AfterTwoSaves_ReturnsLatestValues()
    {
        _svc.Save(MakeSettings(themeName: "Dark"));
        _svc.Save(MakeSettings(themeName: "Light"));
        var loaded = _svc.Load();
        Assert.Equal("Light", loaded!.ThemeName);
    }

    // ── SettingsFilePath ─────────────────────────────────────────────────────

    [Fact]
    public void SettingsFilePath_ReturnsInjectedPath()
    {
        Assert.Equal(_settingsPath, _svc.SettingsFilePath);
    }

    [Fact]
    public void DefaultSettingsPath_ContainsMyCrownJewelApp()
    {
        string path = SettingsService.DefaultSettingsPath();
        Assert.Contains("MyCrownJewelApp", path);
        Assert.EndsWith("settings.json", path);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AppSettings MakeSettings(
        int tabSize = 4,
        string fontName = "Courier New",
        float fontSize = 12f,
        string themeName = "Dark",
        bool wordWrap = false,
        bool gutterVisible = true,
        bool autoSave = false,
        int maxFileSizeMB = 500,
        long thresholdBytes = 200 * 1024) =>
        new AppSettings
        {
            WordWrapEnabled = wordWrap,
            GutterVisible = gutterVisible,
            StatusBarVisible = true,
            ShowGuide = false,
            GuideColumn = 80,
            TabSize = tabSize,
            FontName = fontName,
            FontSize = fontSize,
            InsertSpaces = true,
            AutoIndentEnabled = true,
            SmartTabsEnabled = false,
            ElasticTabsEnabled = false,
            CurrentLineHighlightMode = MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.Off,
            SyntaxHighlightingEnabled = true,
            MinimapVisible = false,
            ThemeName = themeName,
            AutoSaveEnabled = autoSave,
            MaxFileSizeMB = maxFileSizeMB,
            SyntaxHighlightingThresholdBytes = thresholdBytes,
        };
}

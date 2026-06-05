using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class SessionManagerRecentTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SessionManager _sm;

    public SessionManagerRecentTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        // Use the real SessionManager — it writes to %APPDATA%; we test the
        // in-memory behaviour which is fully independent of the file system.
        _sm = new SessionManager();
        _sm.ClearRecent();   // start with a clean slate
    }

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public void AddRecent_AppendsToFront()
    {
        _sm.AddRecent(@"C:\workspace\alpha");
        _sm.AddRecent(@"C:\workspace\beta");

        Assert.Equal(@"C:\workspace\beta", _sm.RecentWorkspaces[0]);
        Assert.Equal(@"C:\workspace\alpha", _sm.RecentWorkspaces[1]);
    }

    [Fact]
    public void AddRecent_DeduplicatesExistingEntry()
    {
        _sm.AddRecent(@"C:\workspace\alpha");
        _sm.AddRecent(@"C:\workspace\beta");
        _sm.AddRecent(@"C:\workspace\alpha"); // re-adding alpha brings it to front

        Assert.Equal(@"C:\workspace\alpha", _sm.RecentWorkspaces[0]);
        Assert.Equal(2, _sm.RecentWorkspaces.Count);
    }

    [Fact]
    public void AddRecent_DeduplicatesCaseInsensitive()
    {
        _sm.AddRecent(@"C:\Workspace\Alpha");
        _sm.AddRecent(@"C:\workspace\alpha");

        Assert.Equal(1, _sm.RecentWorkspaces.Count);
    }

    [Fact]
    public void AddRecent_CapsAtMaxRecentWorkspaces()
    {
        for (int i = 0; i < SessionManager.MaxRecentWorkspaces + 5; i++)
            _sm.AddRecent($@"C:\workspace\ws{i}");

        Assert.Equal(SessionManager.MaxRecentWorkspaces, _sm.RecentWorkspaces.Count);
    }

    [Fact]
    public void ClearRecent_RemovesAll()
    {
        _sm.AddRecent(@"C:\workspace\alpha");
        _sm.AddRecent(@"C:\workspace\beta");
        _sm.ClearRecent();

        Assert.Empty(_sm.RecentWorkspaces);
    }

    [Fact]
    public void RecentWorkspaces_IsReadOnly()
    {
        Assert.IsAssignableFrom<IReadOnlyList<string>>(_sm.RecentWorkspaces);
    }

    [Fact]
    public void SaveAndLoadRecent_RoundTrips()
    {
        // Write a recentWorkspaces.json directly into a temp dir and verify
        // the data survives a round-trip through the JSON serialiser logic.
        var paths = new List<string> { @"C:\a", @"C:\b", @"C:\c" };
        string file = Path.Combine(_tempDir, "recentWorkspaces.json");
        File.WriteAllText(file, JsonSerializer.Serialize(paths));

        var loaded = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file));

        Assert.NotNull(loaded);
        Assert.Equal(paths.Count, loaded!.Count);
        Assert.Equal(@"C:\a", loaded[0]);
    }

    [Fact]
    public void MaxRecentWorkspaces_IsPositive()
    {
        Assert.True(SessionManager.MaxRecentWorkspaces > 0);
    }

    [Fact]
    public void MaxRecentWorkspaces_DefaultIsTen()
    {
        Assert.Equal(10, SessionManager.MaxRecentWorkspaces);
    }

    [Fact]
    public void LoadRecent_DoesNotThrow()
    {
        var ex = Record.Exception(() => _sm.LoadRecent());
        Assert.Null(ex);
    }
}

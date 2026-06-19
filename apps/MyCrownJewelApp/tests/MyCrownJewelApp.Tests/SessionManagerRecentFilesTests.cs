using System;
using System.IO;
using System.Text.Json;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class SessionManagerRecentFilesTests : IDisposable
{
    private readonly SessionManager _sm;

    public SessionManagerRecentFilesTests()
    {
        _sm = new SessionManager();
        _sm.ClearRecentFiles();
    }

    public void Dispose() => _sm.ClearRecentFiles();

    [Fact]
    public void AddRecentFile_AppendsToFront()
    {
        _sm.AddRecentFile(@"C:\code\alpha.cs");
        _sm.AddRecentFile(@"C:\code\beta.cs");

        Assert.Equal(@"C:\code\beta.cs", _sm.RecentFiles[0]);
        Assert.Equal(@"C:\code\alpha.cs", _sm.RecentFiles[1]);
    }

    [Fact]
    public void AddRecentFile_DeduplicatesExistingEntry()
    {
        _sm.AddRecentFile(@"C:\code\alpha.cs");
        _sm.AddRecentFile(@"C:\code\beta.cs");
        _sm.AddRecentFile(@"C:\code\alpha.cs");

        Assert.Equal(@"C:\code\alpha.cs", _sm.RecentFiles[0]);
        Assert.Equal(2, _sm.RecentFiles.Count);
    }

    [Fact]
    public void AddRecentFile_DeduplicatesCaseInsensitive()
    {
        _sm.AddRecentFile(@"C:\Code\Alpha.CS");
        _sm.AddRecentFile(@"C:\code\alpha.cs");

        Assert.Single(_sm.RecentFiles);
    }

    [Fact]
    public void AddRecentFile_CapsAtMaxRecentFiles()
    {
        for (int i = 0; i < _sm.MaxRecentFiles + 5; i++)
            _sm.AddRecentFile($@"C:\code\file{i}.cs");

        Assert.Equal(_sm.MaxRecentFiles, _sm.RecentFiles.Count);
    }

    [Fact]
    public void ClearRecentFiles_RemovesAll()
    {
        _sm.AddRecentFile(@"C:\code\alpha.cs");
        _sm.AddRecentFile(@"C:\code\beta.cs");
        _sm.ClearRecentFiles();

        Assert.Empty(_sm.RecentFiles);
    }

    [Fact]
    public void RecentFiles_IsReadOnly()
    {
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<string>>(_sm.RecentFiles);
    }

    [Fact]
    public void MaxRecentFiles_IsPositive()
    {
        Assert.True(_sm.MaxRecentFiles > 0);
    }

    [Fact]
    public void MaxRecentFiles_DefaultIsTen()
    {
        Assert.Equal(10, _sm.MaxRecentFiles);
    }

    [Fact]
    public void LoadRecentFiles_DoesNotThrow()
    {
        var ex = Record.Exception(() => _sm.LoadRecentFiles());
        Assert.Null(ex);
    }

    [Fact]
    public void SaveRecentFiles_DoesNotThrowOnEmptyList()
    {
        _sm.ClearRecentFiles();
        var ex = Record.Exception(() => _sm.SaveRecentFiles());
        Assert.Null(ex);
    }
}

using System;
using System.IO;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class DirtyFlagTests : IDisposable
{
    private readonly string _tempFilePath;

    public DirtyFlagTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_tempFilePath, "initial content");
    }

    public void Dispose()
    {
        try { File.Delete(_tempFilePath); } catch { }
    }

    [Fact]
    public void Edit_SetsDirtyFlag()
    {
        StaHelper.Run(form =>
        {
            Assert.False(form.IsModified());
            form.textEditor.Text = "new text";
            Assert.True(form.IsModified());
        });
    }

    [Fact]
    public void Save_ClearsDirtyFlag()
    {
        StaHelper.Run(form =>
        {
            form.textEditor.Text = "save test";
            Assert.True(form.IsModified());
            form.ClearDirtyAfterSave();
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void CheckIfClean_ClearsDirty_WhenContentMatchesSnapshot()
    {
        StaHelper.Run(form =>
        {
            form.textEditor.Text = "base";
            form.ClearDirtyAfterSave();
            form.textEditor.Text = "modified";
            Assert.True(form.IsModified());
            form.textEditor.Text = "base";
            form.CheckIfClean();
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void Undo_AfterEdit_ClearsDirtyIfBackToSaved()
    {
        StaHelper.Run(form =>
        {
            form.textEditor.Text = "line1";
            form.ClearDirtyAfterSave();
            form.textEditor.Text = "line1\nline2";
            Assert.True(form.IsModified());
            form.textEditor.Text = "line1";
            form.CheckIfClean();
            Assert.False(form.IsModified());
        });
    }
}

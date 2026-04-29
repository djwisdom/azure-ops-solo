using System;
using System.IO;
using System.Threading;
using Xunit;
using MyCrownJewelApp.TextEditor;

namespace MyCrownJewelApp.Tests;

public class DirtyFlagTests : IDisposable
{
    private readonly string _tempFilePath;

    public DirtyFlagTests()
    {
        // Create a temporary file for load/save tests
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_tempFilePath, "initial content");
    }

    public void Dispose()
    {
        try { File.Delete(_tempFilePath); } catch { }
    }

    private void RunInSta(Action<Form1> action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                using (var form = new Form1())
                {
                    action(form);
                }
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex;
    }

    [Fact]
    public void Edit_SetsDirtyFlag()
    {
        RunInSta(form =>
        {
            // GIVEN a fresh form (no file, clean)
            Assert.False(form.IsModified());

            // WHEN text is changed
            form.textEditor.Text = "new text";

            // THEN dirty flag is set
            Assert.True(form.IsModified());
        });
    }

    [Fact]
    public void Save_ClearsDirtyFlag()
    {
        RunInSta(form =>
        {
            // GIVEN a form with unsaved changes
            form.textEditor.Text = "save test";
            Assert.True(form.IsModified());

            // WHEN we simulate a successful save (internal method)
            form.ClearDirtyAfterSave();

            // THEN dirty flag cleared
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void CheckIfClean_ClearsDirty_WhenContentMatchesSnapshot()
    {
        RunInSta(form =>
        {
            // GIVEN a saved snapshot of some content
            form.textEditor.Text = "base";
            form.ClearDirtyAfterSave(); // sets savedContentHash

            // WHEN content changes and becomes dirty
            form.textEditor.Text = "modified";
            Assert.True(form.IsModified());

            // AND THEN content reverted to original (simulating undo)
            form.textEditor.Text = "base";

            // WHEN CheckIfClean is invoked (called after real undo)
            form.CheckIfClean();

            // THEN dirty flag cleared automatically
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void ExternalChange_Detected()
    {
        RunInSta(form =>
        {
            // GIVEN a file loaded into the editor
            form.LoadFile(_tempFilePath);
            Assert.False(form.IsModified());
            Assert.False(form.CheckExternalChange());

            // WHEN the file is modified externally
            File.WriteAllText(_tempFilePath, "external modification");

            // THEN external change is detected
            Assert.True(form.CheckExternalChange());
        });
    }

    [Fact]
    public void Undo_AfterEdit_ClearsDirtyIfBackToSaved()
    {
        RunInSta(form =>
        {
            // GIVEN saved state
            form.textEditor.Text = "line1";
            form.ClearDirtyAfterSave();

            // AND an edit is made (dirty)
            form.textEditor.Text = "line1\nline2";
            Assert.True(form.IsModified());

            // WHEN Undo is performed (simulate revert)
            form.textEditor.Text = "line1";

            // AND CheckIfClean called (undo handler would call this)
            form.CheckIfClean();

            // THEN buffer is clean
            Assert.False(form.IsModified());
        });
    }
}

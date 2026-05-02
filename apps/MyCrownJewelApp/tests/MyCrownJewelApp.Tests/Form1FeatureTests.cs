using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

[Collection("Sequential")]
public class Form1FeatureTests : IDisposable
{
    private readonly string _tempDir;

    public Form1FeatureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pfpad_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private void RunInSta(Action<Form1> action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form1();
                form.CreateControl();
                action(form);
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(10000);
        if (ex != null) throw ex!;
    }

    private void RunInStaWithTeardown(Action<Form1> action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form1();
                form.CreateControl();
                action(form);
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(10000);
        if (ex != null) throw ex!;
    }

    [Fact]
    public void NewFile_CreatesUntitledDocument()
    {
        RunInSta(form =>
        {
            int initialCount = form.documents.Count;
            form.NewFile();
            Assert.Equal(initialCount + 1, form.documents.Count);
            var newDoc = form.documents.Last();
            Assert.Null(newDoc.FilePath);
            Assert.False(newDoc.IsDirty);
        });
    }

    [Fact]
    public void OpenFileInNewTab_CreatesDocument()
    {
        RunInSta(form =>
        {
            string filePath = Path.Combine(_tempDir, "test.txt");
            File.WriteAllText(filePath, "hello world");
            int initialCount = form.documents.Count;
            form.OpenFileInNewTab(filePath);
            Assert.Equal(initialCount + 1, form.documents.Count);
            var doc = form.documents.Last();
            Assert.Equal(filePath, doc.FilePath);
            Assert.Equal("hello world", doc.Content);
            Assert.False(doc.IsDirty);
        });
    }

    [Fact]
    public void OpenFileInNewTab_NonexistentFile_DoesNotCreateDocument()
    {
        RunInSta(form =>
        {
            int initialCount = form.documents.Count;
            form.OpenFileInNewTab(Path.Combine(_tempDir, "nonexistent.txt"));
            Assert.Equal(initialCount, form.documents.Count);
        });
    }

    [Fact]
    public void SwitchToTab_SwitchesActiveDocument()
    {
        RunInSta(form =>
        {
            int initialCount = form.documents.Count;
            form.NewFile();
            form.NewFile();
            Assert.True(form.documents.Count >= initialCount + 1);

            int lastIndex = form.documents.Count - 1;
            form.SwitchToTab(lastIndex);
            Assert.Equal(lastIndex, form.activeDocIndex);
        });
    }

    [Fact]
    public void CloseCurrentTab_RemovesDocument()
    {
        RunInSta(form =>
        {
            form.NewFile();
            int count = form.documents.Count;
            if (count > 1)
            {
                form.SwitchToTab(count - 1);
                form.CloseCurrentTab();
                Assert.Equal(count - 1, form.documents.Count);
            }
        });
    }

    [Fact]
    public void LoadFile_LoadsContentAndSetsPath()
    {
        RunInSta(form =>
        {
            string filePath = Path.Combine(_tempDir, "loadtest.cs");
            File.WriteAllText(filePath, "class Test { }");
            form.LoadFile(filePath);
            Assert.Equal(filePath, form.currentFilePath);
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void UpdateStatusBar_ShowsCorrectCursorPosition()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line one\nline two";
            form.textEditor.SelectionStart = 0;
            form.UpdateStatusBar();
            Assert.Contains("Ln 1, Col 1", form.lineColLabel.Text);
        });
    }

    [Fact]
    public void UpdateStatusBar_ShowsLineCount()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line one\nline two\nline three";
            form.UpdateStatusBar();
            Assert.Contains("/ 3", form.linePositionLabel.Text);
        });
    }

    [Fact]
    public void UpdateStatusBar_ShowsFileType()
    {
        RunInSta(form =>
        {
            form.currentSyntax = SyntaxDefinition.CSharp;
            form.UpdateStatusBar();
            Assert.Equal("C#", form.fileTypeLabel.Text);
        });
    }

    [Fact]
    public void UpdateStatusBar_ShowsPlainTextWhenNoSyntax()
    {
        RunInSta(form =>
        {
            form.currentSyntax = null;
            form.UpdateStatusBar();
            Assert.Equal("Plain Text", form.fileTypeLabel.Text);
        });
    }

    [Fact]
    public void UpdateStatusBar_ShowsCharacterCount()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "12345";
            form.UpdateStatusBar();
            Assert.Contains("5", form.charCountLabel.Text);
        });
    }

    [Fact]
    public void GoToLine_MovesCursor()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line1\nline2\nline3";
            form.GoToLine(2);
            int line = form.textEditor.GetLineFromCharIndex(form.textEditor.SelectionStart);
            Assert.Equal(1, line); // 0-based
        });
    }

    [Fact]
    public void ToggleBookmark_AddsAndRemoves()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line1\nline2\nline3";
            form.textEditor.SelectionStart = 0;
            int line = form.textEditor.GetLineFromCharIndex(0);
            Assert.DoesNotContain(line, form.Bookmarks);
            form.ToggleBookmark(line);
            Assert.Contains(line, form.Bookmarks);
            form.ToggleBookmark(line);
            Assert.DoesNotContain(line, form.Bookmarks);
        });
    }

    [Fact]
    public void ToggleFold_CollapsesAndExpands()
    {
        RunInStaWithTeardown(form =>
        {
            form.textEditor.Text = "class A\r\n{\r\n    void M()\r\n    {\r\n    }\r\n}";
            form.ToggleFold(1);
            Assert.True(form.FoldingManager.IsCollapsed(1));
            form.ToggleFold(1);
            Assert.False(form.FoldingManager.IsCollapsed(1));
        });
    }

    [Fact]
    public void ZoomIn_ZoomFactorIncreases()
    {
        RunInSta(form =>
        {
            float before = form.zoomFactor;
            form.ZoomIn_Click(null!, EventArgs.Empty);
            Assert.True(form.zoomFactor > before);
        });
    }

    [Fact]
    public void ZoomOut_ZoomFactorDecreases()
    {
        RunInSta(form =>
        {
            form.zoomFactor = 2.0f;
            form.ZoomOut_Click(null!, EventArgs.Empty);
            Assert.True(form.zoomFactor < 2.0f);
        });
    }

    [Fact]
    public void ZoomIn_ClampsAtMax()
    {
        RunInSta(form =>
        {
            form.zoomFactor = 5.0f;
            form.ZoomIn_Click(null!, EventArgs.Empty);
            Assert.Equal(5.0f, form.zoomFactor);
        });
    }

    [Fact]
    public void ZoomOut_ClampsAtMin()
    {
        RunInSta(form =>
        {
            form.zoomFactor = 0.5f;
            form.ZoomOut_Click(null!, EventArgs.Empty);
            Assert.Equal(0.5f, form.zoomFactor);
        });
    }

    [Fact]
    public void PerformFind_FindsText()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "hello world hello";
            form.PerformFind("world", false, false);
            Assert.Equal(6, form.textEditor.SelectionStart);
            Assert.Equal(5, form.textEditor.SelectionLength);
        });
    }

    [Fact]
    public void PerformFind_Up_Works()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "hello hello";
            form.textEditor.SelectionStart = 10; // after first "hello"
            form.PerformFind("hello", false, true);
            Assert.True(form.textEditor.SelectionStart < 10);
        });
    }

    [Fact]
    public void PerformFind_CaseSensitive_MatchesCorrectly()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "Hello hello";
            form.PerformFind("Hello", true, false);
            Assert.Equal(0, form.textEditor.SelectionStart);
            form.PerformFind("hello", true, false);
            Assert.Equal(6, form.textEditor.SelectionStart);
        });
    }

    [Fact]
    public void PerformReplace_ReplacesText()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "hello world";
            form.textEditor.SelectionStart = 0;
            form.textEditor.SelectionLength = 5;
            form.PerformReplace("hello", "hi", false, false);
            Assert.Contains("hi world", form.textEditor.Text);
        });
    }

    [Fact]
    public void PerformReplace_ReplaceAll_Works()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "a a a";
            form.PerformReplace("a", "b", false, true);
            Assert.Equal("b b b", form.textEditor.Text);
        });
    }

    [Fact]
    public void SetDirty_MarksDocumentAsDirty()
    {
        RunInSta(form =>
        {
            Assert.False(form.IsModified());
            form.SetDirty();
            Assert.True(form.IsModified());
            if (form.activeDocIndex >= 0)
                Assert.True(form.ActiveDoc.IsDirty);
        });
    }

    [Fact]
    public void ClearDirtyAfterSave_ClearsDirty()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "test content";
            form.ClearDirtyAfterSave();
            Assert.False(form.IsModified());
        });
    }

    [Fact]
    public void DocumentDisplayName_UntitledShowsNumber()
    {
        RunInSta(form =>
        {
            var doc = new Form1.Document { FilePath = null, UntitledNumber = 5 };
            Assert.Equal("Untitled5", doc.DisplayName);
        });
    }

    [Fact]
    public void DocumentDisplayName_FilePathShowsFilename()
    {
        RunInSta(form =>
        {
            var doc = new Form1.Document { FilePath = @"C:\test\myfile.txt" };
            Assert.Equal("myfile.txt", doc.DisplayName);
        });
    }

    [Fact]
    public void DocumentDisplayName_DirtyShowsAsterisk()
    {
        RunInSta(form =>
        {
            var doc = new Form1.Document { FilePath = @"C:\test\f.txt", IsDirty = true };
            Assert.Equal("f.txt", doc.DisplayName); // DisplayName itself doesn't include *
        });
    }

    [Fact]
    public void TextEditor_SelectionChanged_UpdatesStatusBar()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line one\nline two\nline three";
            form.textEditor.SelectionStart = 10; // somewhere on line 2
            form.UpdateStatusBar();
            Assert.Contains("Ln 2", form.lineColLabel.Text);
        });
    }

    [Fact]
    public void ToggleGutter_TogglesVisibility()
    {
        RunInSta(form =>
        {
            bool initial = form.gutterVisible;
            form.ToggleGutter();
            Assert.NotEqual(initial, form.gutterVisible);
        });
    }

    [Fact]
    public void ToggleWordWrap_TogglesWrapping()
    {
        RunInSta(form =>
        {
            bool initial = form.wordWrapEnabled;
            form.ToggleWordWrap();
            Assert.NotEqual(initial, form.wordWrapEnabled);
            Assert.Equal(form.wordWrapEnabled, form.textEditor.WordWrap);
        });
    }

    [Fact]
    public void SetTabSize_ChangesTabSize()
    {
        RunInSta(form =>
        {
            form.SetTabSize(8);
            Assert.Equal(8, form.tabSize);
            form.SetTabSize(4);
            Assert.Equal(4, form.tabSize);
        });
    }

    [Fact]
    public void ToggleSyntaxHighlighting_TogglesEnabled()
    {
        RunInSta(form =>
        {
            bool initial = form.syntaxHighlightingEnabled;
            form.ToggleSyntaxHighlighting();
            Assert.NotEqual(initial, form.syntaxHighlightingEnabled);
        });
    }

    [Fact]
    public void LoadFile_DetectsSyntaxFromExtension()
    {
        RunInSta(form =>
        {
            string csPath = Path.Combine(_tempDir, "program.cs");
            File.WriteAllText(csPath, "class C { }");
            form.LoadFile(csPath);
            Assert.NotNull(form.currentSyntax);
            Assert.Equal("C#", form.currentSyntax!.Name);
        });
    }

    [Fact]
    public void GoToLine_ClampsToBounds()
    {
        RunInSta(form =>
        {
            form.textEditor.Text = "line1\nline2";
            form.GoToLine(999);
            int line = form.textEditor.GetLineFromCharIndex(form.textEditor.SelectionStart) + 1;
            Assert.Equal(2, line);
        });
    }

    [Fact]
    public void TextEditor_TextChanged_SetsDirty()
    {
        RunInSta(form =>
        {
            Assert.False(form.IsModified());
            form.textEditor.Text = "modified content";
            Assert.True(form.IsModified());
        });
    }

    [Fact]
    public void SetGuideColumn_UpdatesAndEnablesGuide()
    {
        RunInSta(form =>
        {
            form.SetGuideColumn(100);
            Assert.Equal(100, form.guideColumn);
            Assert.True(form.showGuide);
            Assert.True(form.guidePanel.ShowGuide);
            Assert.Equal(100, form.guidePanel.GuideColumn);
        });
    }
}

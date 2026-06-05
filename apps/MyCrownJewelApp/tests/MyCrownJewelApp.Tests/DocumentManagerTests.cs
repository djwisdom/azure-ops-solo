using System;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

public class DocumentManagerTests
{
    // ── CreateNew ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateNew_AssignsSequentialUntitledNumbers()
    {
        var mgr = new DocumentManager();
        var d1 = mgr.CreateNew();
        var d2 = mgr.CreateNew();
        var d3 = mgr.CreateNew();

        Assert.Equal(1, d1.UntitledNumber);
        Assert.Equal(2, d2.UntitledNumber);
        Assert.Equal(3, d3.UntitledNumber);
    }

    [Fact]
    public void CreateNew_DoesNotAddToCollection()
    {
        var mgr = new DocumentManager();
        _ = mgr.CreateNew();
        Assert.Equal(0, mgr.Count);
    }

    [Fact]
    public void CreateNew_IncrementsPastCustomStartingNumber()
    {
        var mgr = new DocumentManager();
        mgr.NextUntitledNumber = 10;
        var d = mgr.CreateNew();
        Assert.Equal(10, d.UntitledNumber);
        Assert.Equal(11, mgr.NextUntitledNumber);
    }

    // ── Add / Count ──────────────────────────────────────────────────────────

    [Fact]
    public void Add_IncreasesCount()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        mgr.Add(new EditorDocument());
        Assert.Equal(2, mgr.Count);
    }

    [Fact]
    public void Documents_ReturnsLiveList_MutationsReflected()
    {
        var mgr = new DocumentManager();
        var list = mgr.Documents;
        mgr.Add(new EditorDocument { FilePath = "a.cs" });
        // the list reference returned earlier reflects the mutation
        Assert.Single(list);
        Assert.Equal("a.cs", list[0].FilePath);
    }

    // ── RemoveAt ─────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveAt_DecreasesCount()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        mgr.Add(new EditorDocument());
        mgr.RemoveAt(0);
        Assert.Equal(1, mgr.Count);
    }

    [Fact]
    public void RemoveAt_RemovesCorrectDocument()
    {
        var mgr = new DocumentManager();
        var a = new EditorDocument { FilePath = "a.cs" };
        var b = new EditorDocument { FilePath = "b.cs" };
        mgr.Add(a);
        mgr.Add(b);
        mgr.RemoveAt(0);
        Assert.Equal("b.cs", mgr.Documents[0].FilePath);
    }

    // ── GetAt ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAt_ReturnsDocument_WhenIndexValid()
    {
        var mgr = new DocumentManager();
        var doc = new EditorDocument { FilePath = "x.cs" };
        mgr.Add(doc);
        Assert.Same(doc, mgr.GetAt(0));
    }

    [Fact]
    public void GetAt_ReturnsNull_WhenIndexOutOfRange()
    {
        var mgr = new DocumentManager();
        Assert.Null(mgr.GetAt(0));
        Assert.Null(mgr.GetAt(-1));
        Assert.Null(mgr.GetAt(99));
    }

    // ── ActiveIndex / GetActive ──────────────────────────────────────────────

    [Fact]
    public void GetActive_ReturnsNull_WhenNoDocuments()
    {
        var mgr = new DocumentManager();
        Assert.Null(mgr.GetActive());
    }

    [Fact]
    public void GetActive_ReturnsNull_WhenActiveIndexIsMinusOne()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        mgr.ActiveIndex = -1;
        Assert.Null(mgr.GetActive());
    }

    [Fact]
    public void SetActive_ReturnsCorrectDocument()
    {
        var mgr = new DocumentManager();
        var doc = new EditorDocument { FilePath = "active.cs" };
        mgr.Add(doc);
        var returned = mgr.SetActive(0);
        Assert.Same(doc, returned);
        Assert.Equal(0, mgr.ActiveIndex);
    }

    [Fact]
    public void SetActive_AllowsMinusOneSentinel()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        mgr.SetActive(0);
        mgr.SetActive(-1);
        Assert.Equal(-1, mgr.ActiveIndex);
        Assert.Null(mgr.GetActive());
    }

    // ── IsValidIndex ─────────────────────────────────────────────────────────

    [Fact]
    public void IsValidIndex_ReturnsTrueForExistingIndex()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        Assert.True(mgr.IsValidIndex(0));
    }

    [Fact]
    public void IsValidIndex_ReturnsFalseForNegativeOrBeyondEnd()
    {
        var mgr = new DocumentManager();
        Assert.False(mgr.IsValidIndex(-1));
        Assert.False(mgr.IsValidIndex(0));  // empty
        mgr.Add(new EditorDocument());
        Assert.False(mgr.IsValidIndex(1));  // count=1, max valid=0
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsAllState()
    {
        var mgr = new DocumentManager();
        mgr.Add(new EditorDocument());
        mgr.Add(new EditorDocument());
        mgr.SetActive(1);
        mgr.NextUntitledNumber = 5;

        mgr.Clear();

        Assert.Equal(0, mgr.Count);
        Assert.Equal(-1, mgr.ActiveIndex);
        Assert.Equal(1, mgr.NextUntitledNumber);
    }

    // ── EditorDocument.DisplayName (now top-level class) ───────────────────────────

    [Fact]
    public void Document_DisplayName_UntitledWithNumber()
    {
        var doc = new EditorDocument { UntitledNumber = 3 };
        Assert.Equal("Untitled3", doc.DisplayName);
    }

    [Fact]
    public void Document_DisplayName_UntitledNoNumber()
    {
        var doc = new EditorDocument();
        Assert.Equal("Untitled", doc.DisplayName);
    }

    [Fact]
    public void Document_DisplayName_UsesFileName()
    {
        var doc = new EditorDocument { FilePath = @"C:\projects\editor.cs" };
        Assert.Equal("editor.cs", doc.DisplayName);
    }

    [Fact]
    public void Document_DisplayName_FileTakesPriorityOverUntitledNumber()
    {
        var doc = new EditorDocument { FilePath = @"C:\projects\real.cs", UntitledNumber = 2 };
        Assert.Equal("real.cs", doc.DisplayName);
    }
}

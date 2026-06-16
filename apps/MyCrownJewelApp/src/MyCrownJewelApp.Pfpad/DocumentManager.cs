using System;
using System.Collections.Generic;
using System.Linq;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Manages the collection of open documents (editor tabs) and the active-tab index.
/// This class is a pure data manager — no WinForms dependencies.
///
/// Mutation invariants are enforced centrally: Add(), RemoveAt(), SetActive(), and Clear()
/// are the only write paths. All three raise the corresponding events after mutating state,
/// allowing subscribers (Form1 UI, tests) to react without polling.
/// </summary>
public sealed class DocumentManager
{
    private readonly List<EditorDocument> _documents = new();
    private int _activeIndex = -1;
    private int _nextUntitledNumber = 1;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Raised after a document is added to the collection.</summary>
    public event Action<EditorDocument>? DocumentAdded;

    /// <summary>Raised after a document is removed. <paramref name="oldIndex"/> is the index it occupied.</summary>
    public event Action<EditorDocument, int>? DocumentRemoved;

    /// <summary>Raised after the active document changes. Argument is the new active document (null when none).</summary>
    public event Action<EditorDocument?>? ActiveDocumentChanged;

    // ── Collection access ────────────────────────────────────────────────────

    /// <summary>
    /// Live mutable reference to the documents list. Prefer Add() and RemoveAt()
    /// so that events fire and invariants are enforced.
    /// </summary>
    public List<EditorDocument> Documents => _documents;

    /// <summary>Index of the currently active EditorDocument, or -1 when no document is active.</summary>
    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            if (_activeIndex == value) return;
            _activeIndex = value;
            ActiveDocumentChanged?.Invoke(GetActive());
        }
    }

    /// <summary>Counter for naming new untitled documents: Untitled1, Untitled2, etc.</summary>
    public int NextUntitledNumber
    {
        get => _nextUntitledNumber;
        set => _nextUntitledNumber = value;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>Number of open documents.</summary>
    public int Count => _documents.Count;

    /// <summary>Returns the active EditorDocument, or null if none is active.</summary>
    public EditorDocument? GetActive() =>
        _activeIndex >= 0 && _activeIndex < _documents.Count
            ? _documents[_activeIndex]
            : null;

    /// <summary>Returns the EditorDocument at the given index, or null if out of range.</summary>
    public EditorDocument? GetAt(int index) =>
        index >= 0 && index < _documents.Count ? _documents[index] : null;

    /// <summary>
    /// Returns the first open document whose <see cref="EditorDocument.FilePath"/> matches
    /// <paramref name="path"/> (case-insensitive, OS path rules), or null if not found.
    /// </summary>
    public EditorDocument? FindByPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return _documents.FirstOrDefault(d =>
            string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the zero-based index of <paramref name="doc"/> in the collection,
    /// or -1 if it is not present.
    /// </summary>
    public int IndexOf(EditorDocument doc) => _documents.IndexOf(doc);

    /// <summary>Returns true if the given index is a valid document index.</summary>
    public bool IsValidIndex(int index) =>
        index >= 0 && index < _documents.Count;

    // ── Mutations ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new untitled EditorDocument and increments the untitled counter.
    /// The caller is responsible for adding it to the collection via Add().
    /// </summary>
    public EditorDocument CreateNew() =>
        new EditorDocument { UntitledNumber = _nextUntitledNumber++ };

    /// <summary>Adds a document to the end of the collection and raises <see cref="DocumentAdded"/>.</summary>
    public void Add(EditorDocument doc)
    {
        _documents.Add(doc);
        DocumentAdded?.Invoke(doc);
    }

    /// <summary>
    /// Removes the document at <paramref name="index"/>. The caller must update
    /// <see cref="ActiveIndex"/> before or after to keep it in range.
    /// Raises <see cref="DocumentRemoved"/> with the removed document and its old index.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _documents.Count) return;
        var doc = _documents[index];
        _documents.RemoveAt(index);
        DocumentRemoved?.Invoke(doc, index);
    }

    /// <summary>
    /// Sets the active document index and raises <see cref="ActiveDocumentChanged"/>.
    /// Returns the newly active document, or null when index is -1 or out of range.
    /// </summary>
    public EditorDocument? SetActive(int index)
    {
        ActiveIndex = index;
        return GetActive();
    }

    /// <summary>
    /// Clears all documents and resets state to empty. Raises <see cref="DocumentRemoved"/>
    /// for each removed document, then <see cref="ActiveDocumentChanged"/> with null.
    /// </summary>
    public void Clear()
    {
        for (int i = _documents.Count - 1; i >= 0; i--)
        {
            var doc = _documents[i];
            _documents.RemoveAt(i);
            DocumentRemoved?.Invoke(doc, i);
        }
        _activeIndex = -1;
        _nextUntitledNumber = 1;
        ActiveDocumentChanged?.Invoke(null);
    }
}

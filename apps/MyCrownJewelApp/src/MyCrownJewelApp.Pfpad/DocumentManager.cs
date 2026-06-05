using System.Collections.Generic;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Manages the collection of open documents (editor tabs) and the active-tab index.
/// This class is a pure data manager — no WinForms dependencies.
///
/// Phase 3 note: Form1 still mutates the Documents list directly in many places
/// for backward compatibility. The mutable-list exposure is a transitional design;
/// the goal is to route all mutations through this class in a future phase so that
/// invariants (active index bounds, untitled numbering) can be enforced centrally.
///
/// Events are intentionally omitted from v1 because Form1 still bypasses this class
/// for mutations in several paths. Events will be added once all mutations are routed
/// through DocumentManager methods.
/// </summary>
public sealed class DocumentManager
{
    private readonly List<EditorDocument> _documents = new();
    private int _activeIndex = -1;
    private int _nextUntitledNumber = 1;

    /// <summary>
    /// Live mutable reference to the documents list. Form1 may mutate this directly
    /// during the transition period. Prefer Add() and RemoveAt() where possible.
    /// </summary>
    public List<EditorDocument> Documents => _documents;

    /// <summary>Index of the currently active EditorDocument, or -1 when no EditorDocument is active.</summary>
    public int ActiveIndex
    {
        get => _activeIndex;
        set => _activeIndex = value;  // intentionally no bounds-check (Form1 uses -1 as sentinel)
    }

    /// <summary>Counter for naming new untitled documents: Untitled1, Untitled2, etc.</summary>
    public int NextUntitledNumber
    {
        get => _nextUntitledNumber;
        set => _nextUntitledNumber = value;
    }

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
    /// Creates a new untitled EditorDocument and increments the untitled counter.
    /// The caller is responsible for adding it to the collection via Add().
    /// </summary>
    public EditorDocument CreateNew() =>
        new EditorDocument { UntitledNumber = _nextUntitledNumber++ };

    /// <summary>Adds a EditorDocument to the end of the collection.</summary>
    public void Add(EditorDocument doc) => _documents.Add(doc);

    /// <summary>
    /// Removes the EditorDocument at the given index. The caller must update
    /// ActiveIndex before or after calling this to keep it in range.
    /// </summary>
    public void RemoveAt(int index) => _documents.RemoveAt(index);

    /// <summary>
    /// Sets the active EditorDocument index and returns the newly active EditorDocument,
    /// or null if the index is out of range (including -1).
    /// </summary>
    public EditorDocument? SetActive(int index)
    {
        _activeIndex = index;
        return GetActive();
    }

    /// <summary>
    /// Clears all documents and resets state to empty. Used on session restore.
    /// </summary>
    public void Clear()
    {
        _documents.Clear();
        _activeIndex = -1;
        _nextUntitledNumber = 1;
    }

    /// <summary>
    /// Returns true if the given index is a valid EditorDocument index.
    /// </summary>
    public bool IsValidIndex(int index) =>
        index >= 0 && index < _documents.Count;
}

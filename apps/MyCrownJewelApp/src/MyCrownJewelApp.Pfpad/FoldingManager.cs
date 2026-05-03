using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class FoldingManager
{
    private readonly RichTextBox _editor;
    private readonly List<FoldRegion> _regions = new();
    private readonly Stack<UndoFold> _undoStack = new();

    public struct FoldRegion
    {
        public int OpenLine;      // original line number of opening marker
        public int CloseLine;     // original line number of closing marker
        public int NestLevel;
        public string OpenText;   // e.g. "{", "#region Name"
        public bool IsCollapsed;
    }

    private struct UndoFold
    {
        public int StartIndex;
        public int Length;
        public string ReplacedText;
        public int OpenLine;
        public int CloseLine;
    }

    public FoldingManager(RichTextBox editor)
    {
        _editor = editor;
    }

    /// <summary>
    /// Re-scans the editor text for foldable regions.
    /// </summary>
    public void ScanRegions()
    {
        _regions.Clear();
        string text = _editor.Text;
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Split('\n');
        var braceStack = new Stack<(int line, int type)>();
        var openBraceStack = new Stack<int>();
        int regionDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("#endregion"))
            {
                regionDepth--;
                if (braceStack.Count > 0 && braceStack.Peek().type == 1)
                {
                    var open = braceStack.Pop();
                    _regions.Add(new FoldRegion
                    {
                        OpenLine = open.line,
                        CloseLine = i,
                        NestLevel = regionDepth,
                        OpenText = lines[open.line].Trim(),
                        IsCollapsed = false
                    });
                }
            }
            else if (trimmed.StartsWith("#region"))
            {
                braceStack.Push((i, 1));
                regionDepth++;
            }

            // Brace matching using a stack for full nesting support
            for (int c = 0; c < lines[i].Length; c++)
            {
                if (lines[i][c] == '{')
                {
                    openBraceStack.Push(i);
                }
                else if (lines[i][c] == '}')
                {
                    if (openBraceStack.Count > 0)
                    {
                        int openLine = openBraceStack.Pop();
                        if (openLine < i)
                        {
                            _regions.Add(new FoldRegion
                            {
                                OpenLine = openLine,
                                CloseLine = i,
                                NestLevel = openBraceStack.Count,
                                OpenText = lines[openLine].Trim(),
                                IsCollapsed = false
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the fold region at the given line, or null if none.
    /// Only returns regions where the open line matches (not close lines).
    /// </summary>
    public FoldRegion? GetRegionAtLine(int lineIndex)
    {
        foreach (var r in _regions)
        {
            if (r.OpenLine == lineIndex)
                return r;
        }
        return null;
    }

    /// <summary>
    /// Returns all regions (for fold marker drawing).
    /// </summary>
    public IEnumerable<FoldRegion> GetAllRegions() => _regions;

    /// <summary>
    /// Returns true if the given line index (pre-collapse) is an opening marker for a foldable region.
    /// </summary>
    public bool IsFoldStart(int lineIndex) => _regions.Any(r => r.OpenLine == lineIndex);

    /// <summary>
    /// Returns true if the given line is currently collapsed.
    /// </summary>
    public bool IsCollapsed(int lineIndex) => _regions.Any(r => r.OpenLine == lineIndex && r.IsCollapsed);

    /// <summary>
    /// Toggles the fold state of the region at the given line.
    /// </summary>
    public bool ToggleFold(int lineIndex)
    {
        for (int i = 0; i < _regions.Count; i++)
        {
            var r = _regions[i];
            if (r.OpenLine != lineIndex) continue;

            if (r.IsCollapsed)
            {
                ExpandFold(i);
            }
            else
            {
                CollapseFold(i);
            }
            return true;
        }
        return false;
    }

    private void CollapseFold(int index)
    {
        var r = _regions[index];
        if (r.IsCollapsed) return;

        try
        {
            int startChar = _editor.GetFirstCharIndexFromLine(r.OpenLine);
            int endCharExclusive;
            if (r.CloseLine + 1 < _editor.Lines.Length)
                endCharExclusive = _editor.GetFirstCharIndexFromLine(r.CloseLine + 1);
            else
                endCharExclusive = _editor.TextLength;

            if (startChar < 0 || endCharExclusive <= startChar) return;

            int length = endCharExclusive - startChar;
            string originalText = _editor.Text.Substring(startChar, length);

            string openLineText = _editor.Lines[r.OpenLine];
            string replacement = openLineText + " // ...";

            _undoStack.Push(new UndoFold
            {
                StartIndex = startChar,
                Length = replacement.Length,
                ReplacedText = originalText,
                OpenLine = r.OpenLine,
                CloseLine = r.CloseLine
            });

            _editor.Select(startChar, length);
            _editor.SelectedText = replacement;

            r.IsCollapsed = true;
            _regions[index] = r;
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.WriteLine($"[FoldingManager] CollapseFold: index {index} out of range");
        }
        catch (NullReferenceException)
        {
            Debug.WriteLine("[FoldingManager] CollapseFold: editor disposed");
        }
    }

    private void ExpandFold(int index)
    {
        var r = _regions[index];
        if (!r.IsCollapsed) return;

        try
        {
            UndoFold? undo = null;
            foreach (var u in _undoStack)
            {
                if (u.OpenLine == r.OpenLine && u.CloseLine == r.CloseLine)
                {
                    undo = u;
                    break;
                }
            }
            if (undo == null) return;

            var u2 = undo.Value;
            if (u2.StartIndex < 0 || u2.StartIndex > _editor.TextLength) return;

            _editor.Select(u2.StartIndex, u2.ReplacedText.Length);
            _editor.SelectedText = u2.ReplacedText;

            r.IsCollapsed = false;
            _regions[index] = r;
        }
        catch (ArgumentOutOfRangeException)
        {
            Debug.WriteLine($"[FoldingManager] ExpandFold: index {index} out of range");
        }
        catch (NullReferenceException)
        {
            Debug.WriteLine("[FoldingManager] ExpandFold: editor disposed");
        }
    }
}

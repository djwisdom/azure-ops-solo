using System;
using System.Collections.Generic;
using System.IO;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Represents a single open EditorDocument (editor tab).
/// Pure data object — no WinForms dependencies.
/// Previously nested as Form1.EditorDocument; extracted to top-level for testability.
/// </summary>
public class EditorDocument
{
    public string? FilePath { get; set; }
    public string Content { get; set; } = "";
    public bool IsDirty { get; set; }
    public HashSet<int> ModifiedLines { get; set; } = new();
    public HashSet<int> Bookmarks { get; set; } = new();
    public HashSet<int> CollapsedRegions { get; set; } = new();
    public string? SavedHash { get; set; }
    public DateTime? LastWriteTime { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public int FirstVisibleLine { get; set; }
    public SyntaxDefinition? Syntax { get; set; }
    public int? UntitledNumber { get; set; }

    public System.Text.Encoding? FileEncoding { get; set; }
    public bool ContainsRtlText { get; set; } = false;

    // Large-file feature degradation flags (set by Form1.ApplyLargeFileDegradation)
    public bool DisableSyntaxHighlighting { get; set; } = false;
    public bool DisableMinimap { get; set; } = false;
    public bool DisableWordWrap { get; set; } = false;

    public string DisplayName =>
        string.IsNullOrEmpty(FilePath) && UntitledNumber.HasValue ? $"Untitled{UntitledNumber}" :
        string.IsNullOrEmpty(FilePath) ? "Untitled" :
        Path.GetFileName(FilePath);
}

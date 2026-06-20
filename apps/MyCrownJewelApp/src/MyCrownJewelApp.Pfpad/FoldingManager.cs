using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyCrownJewelApp.Pfpad;

public sealed partial class FoldingManager
{
    private readonly RichTextBox _editor;
    private readonly List<FoldRegion> _regions = new();
    private readonly Dictionary<int, FoldSnapshot> _snapshots = new();

    private const string CollapseSuffix = " // ...";

    public struct FoldRegion
    {
        public int OpenLine;
        public int CloseLine;
        public int NestLevel;
        public string OpenText;
        public bool IsCollapsed;
    }

    private readonly struct FoldSnapshot
    {
        public readonly int StartIndex;
        public readonly int Length;
        public readonly string OriginalText;
        public readonly string ReplacementText;
        public readonly int OpenLine;
        public readonly int CloseLine;

        public FoldSnapshot(int startIndex, int length, string originalText, string replacementText, int openLine, int closeLine)
        {
            StartIndex = startIndex;
            Length = length;
            OriginalText = originalText;
            ReplacementText = replacementText;
            OpenLine = openLine;
            CloseLine = closeLine;
        }
    }

    public event Action<int, int>? FoldStateChanged;

    private readonly Roslyn.RoslynWorkspaceService? _roslynWorkspace;

    public FoldingManager(RichTextBox editor, Roslyn.RoslynWorkspaceService? roslynWorkspace = null)
    {
        _editor = editor;
        _roslynWorkspace = roslynWorkspace;
    }

    public void ScanRegions(string? fileExtension = null)
    {
        // Don't wipe active collapsed folds — debounce timers call this after a fold
        // collapses text, which would reset IsCollapsed=false and make the + disappear.
        if (_snapshots.Count > 0) return;

        _regions.Clear();
        string text = _editor.Text;
        if (string.IsNullOrEmpty(text)) return;

        // Use Roslyn-backed folding only for confirmed C# files.
        if (_roslynWorkspace?.IsReady == true && IsCSharpFile(fileExtension))
        {
            ScanRegionsWithRoslyn(text);
        }
        else
        {
            // Text-based brace-stack folding — works for C, C++, JS, and all brace languages.
            ScanRegionsWithText(text);
        }
    }

    /// <summary>Returns true only for .cs / .csx files — never misidentifies C or C++.</summary>
    private static bool IsCSharpFile(string? ext) =>
        ext is ".cs" or ".csx";

    private void ScanRegionsWithRoslyn(string text)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetRoot();

            // Find folding regions using Roslyn syntax analysis
            var regions = new List<FoldRegion>();

            // Block-level folding (methods, classes, etc.)
            foreach (var node in root.DescendantNodes())
            {
                if (node is BlockSyntax block)
                {
                    var openBrace = block.OpenBraceToken;
                    var closeBrace = block.CloseBraceToken;

                    if (openBrace.IsMissing || closeBrace.IsMissing) continue;

                    var openLine = GetLineFromPosition(text, openBrace.Span.Start);
                    var closeLine = GetLineFromPosition(text, closeBrace.Span.End);

                    if (closeLine - openLine > 1) // Only fold if there's content
                    {
                        regions.Add(new FoldRegion
                        {
                            OpenLine = openLine,
                            CloseLine = closeLine,
                            NestLevel = GetNestingLevel(node),
                            OpenText = GetFoldText(text, openLine),
                            IsCollapsed = false
                        });
                    }
                }
                else if (node is NamespaceDeclarationSyntax ns)
                {
                    var openLine = GetLineFromPosition(text, ns.Span.Start);
                    var closeLine = GetLineFromPosition(text, ns.Span.End);
                    regions.Add(new FoldRegion
                    {
                        OpenLine = openLine,
                        CloseLine = closeLine,
                        NestLevel = 0,
                        OpenText = GetFoldText(text, openLine),
                        IsCollapsed = false
                    });
                }
                else if (node is ClassDeclarationSyntax cls)
                {
                    var openLine = GetLineFromPosition(text, cls.Span.Start);
                    var closeLine = GetLineFromPosition(text, cls.Span.End);
                    regions.Add(new FoldRegion
                    {
                        OpenLine = openLine,
                        CloseLine = closeLine,
                        NestLevel = GetNestingLevel(cls),
                        OpenText = GetFoldText(text, openLine),
                        IsCollapsed = false
                    });
                }
            }

            // Sort by position and nesting
            regions.Sort((a, b) =>
            {
                int cmp = a.OpenLine.CompareTo(b.OpenLine);
                return cmp != 0 ? cmp : a.NestLevel.CompareTo(b.NestLevel);
            });

            _regions.AddRange(regions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Roslyn folding failed: {ex.Message}");
            // Fall back to text-based folding
            ScanRegionsWithText(text);
        }
    }

    private int GetLineFromPosition(string text, int position)
    {
        int line = 0;
        for (int i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\n') line++;
        }
        return line;
    }

    private int GetNestingLevel(SyntaxNode node)
    {
        int level = 0;
        var current = node.Parent;
        while (current != null)
        {
            if (current is BlockSyntax || current is ClassDeclarationSyntax ||
                current is MethodDeclarationSyntax || current is PropertyDeclarationSyntax)
            {
                level++;
            }
            current = current.Parent;
        }
        return level;
    }

    private string GetFoldText(string text, int lineIndex)
    {
        var lines = text.Split('\n');
        if (lineIndex < lines.Length)
        {
            return lines[lineIndex].Trim();
        }
        return "";
    }

    private void ScanPreprocessorRegions(string text)
    {
        var lines = text.Split('\n');
        var regionStack = new Stack<int>();
        int regionDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            if (trimmed.StartsWith("#endregion"))
            {
                regionDepth--;
                if (regionStack.Count > 0)
                {
                    var openLine = regionStack.Pop();
                    _regions.Add(new FoldRegion
                    {
                        OpenLine = openLine,
                        CloseLine = i,
                        NestLevel = regionDepth,
                        OpenText = lines[openLine].Trim(),
                        IsCollapsed = false
                    });
                }
            }
            else if (trimmed.StartsWith("#region"))
            {
                regionStack.Push(i);
                regionDepth++;
            }
        }
    }

    private void ScanRegionsWithText(string text)
    {
        var lines = text.Split('\n');
        var regionStack = new Stack<(int line, int type)>();
        var braceStack = new Stack<int>();
        int regionDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();

            // Preprocessor regions
            if (trimmed.StartsWith("#endregion"))
            {
                regionDepth--;
                if (regionStack.Count > 0 && regionStack.Peek().type == 1)
                {
                    var open = regionStack.Pop();
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
                regionStack.Push((i, 1));
                regionDepth++;
            }

            // Brace folding
            for (int c = 0; c < lines[i].Length; c++)
            {
                char ch = lines[i][c];
                if (ch == '{')
                {
                    braceStack.Push(i);
                }
                else if (ch == '}')
                {
                    if (braceStack.Count > 0)
                    {
                        int openLine = braceStack.Pop();
                        if (i - openLine > 1) // Only fold if there's content
                        {
                            _regions.Add(new FoldRegion
                            {
                                OpenLine = openLine,
                                CloseLine = i,
                                NestLevel = braceStack.Count,
                                OpenText = lines[openLine].Trim(),
                                IsCollapsed = false
                            });
                        }
                    }
                }
            }
        }
    }

    public FoldRegion? GetRegionAtLine(int lineIndex)
    {
        foreach (var r in _regions)
            if (r.OpenLine == lineIndex)
                return r;
        return null;
    }

    public IEnumerable<FoldRegion> GetAllRegions() => _regions;

    public bool IsFoldStart(int lineIndex) => _regions.Any(r => r.OpenLine == lineIndex)
                                           || _snapshots.ContainsKey(lineIndex);

    public bool IsCollapsed(int lineIndex) => _regions.Any(r => r.OpenLine == lineIndex && r.IsCollapsed)
                                           || _snapshots.ContainsKey(lineIndex);

    public bool ToggleFold(int lineIndex)
    {
        for (int i = 0; i < _regions.Count; i++)
        {
            var r = _regions[i];
            if (r.OpenLine != lineIndex) continue;

            if (r.IsCollapsed)
                ExpandFold(i);
            else
                CollapseFold(i);
            return true;
        }

        // Snapshot exists but region metadata was lost (e.g. a rescan ran before the guard
        // was in place). Expand using the snapshot directly.
        if (_snapshots.ContainsKey(lineIndex))
        {
            var syntheticRegion = new FoldRegion
            {
                OpenLine = lineIndex,
                CloseLine = lineIndex,
                IsCollapsed = true,
                NestLevel = 0,
                OpenText = string.Empty
            };
            _regions.Add(syntheticRegion);
            ExpandFold(_regions.Count - 1);
            return true;
        }

        return false;
    }

    private void CollapseFold(int index)
    {
        var r = _regions[index];
        if (r.IsCollapsed) return;

        int startChar;
        int endCharExclusive;

        try
        {
            startChar = _editor.GetFirstCharIndexFromLine(r.OpenLine);
            if (r.CloseLine + 1 < _editor.Lines.Length)
                endCharExclusive = _editor.GetFirstCharIndexFromLine(r.CloseLine + 1);
            else
                endCharExclusive = _editor.TextLength;
        }
        catch
        {
            ScanRegions();
            return;
        }

        if (startChar < 0 || endCharExclusive <= startChar) return;

        int length = endCharExclusive - startChar;
        if (length <= 0) return;

        string originalText;
        string openLineText;
        try
        {
            originalText = _editor.Text.Substring(startChar, length);
            openLineText = _editor.Lines[r.OpenLine];
        }
        catch
        {
            ScanRegions();
            return;
        }

        string replacement = openLineText + CollapseSuffix;

        if (string.IsNullOrEmpty(originalText) || originalText.Length <= replacement.Length)
            return;

        _snapshots[r.OpenLine] = new FoldSnapshot(
            startChar, length, originalText, replacement, r.OpenLine, r.CloseLine);

        SuspendDrawing();
        try
        {
            _editor.Select(startChar, length);
            _editor.SelectedText = replacement;
        }
        finally
        {
            ResumeDrawing();
        }

        r.IsCollapsed = true;
        _regions[index] = r;
        FoldStateChanged?.Invoke(r.OpenLine, r.CloseLine);
    }

    private void ExpandFold(int index)
    {
        var r = _regions[index];
        if (!r.IsCollapsed) return;

        if (!_snapshots.TryGetValue(r.OpenLine, out var snap))
        {
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }

        if (snap.StartIndex < 0 || snap.StartIndex > _editor.TextLength)
        {
            _snapshots.Remove(r.OpenLine);
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }

        int available = Math.Min(snap.ReplacementText.Length, _editor.TextLength - snap.StartIndex);
        if (available <= 0)
        {
            _snapshots.Remove(r.OpenLine);
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }

        string currentCollapsed;
        try
        {
            currentCollapsed = _editor.Text.Substring(snap.StartIndex, available);
        }
        catch
        {
            _snapshots.Remove(r.OpenLine);
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }

        // If the collapsed text was modified by the user since collapse,
        // re-scan regions instead of restoring stale original text.
        if (!currentCollapsed.Equals(snap.ReplacementText, StringComparison.Ordinal))
        {
            Debug.WriteLine("[FoldingManager] Collapsed text diverged — re-scanning regions");
            _snapshots.Remove(r.OpenLine);
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }

        SuspendDrawing();
        try
        {
            _editor.Select(snap.StartIndex, snap.ReplacementText.Length);
            _editor.SelectedText = snap.OriginalText;
        }
        catch
        {
            ResumeDrawing();
            _snapshots.Remove(r.OpenLine);
            r.IsCollapsed = false;
            _regions[index] = r;
            ScanRegions();
            return;
        }
        finally
        {
            ResumeDrawing();
        }

        _snapshots.Remove(r.OpenLine);
        r.IsCollapsed = false;
        _regions[index] = r;
        FoldStateChanged?.Invoke(r.OpenLine, r.CloseLine);
    }

    private void SuspendDrawing()
    {
        if (_editor.IsHandleCreated && !_editor.IsDisposed)
            SendMessage(_editor.Handle, WM_SETREDRAW, 0, 0);
    }

    private void ResumeDrawing()
    {
        if (_editor.IsHandleCreated && !_editor.IsDisposed)
        {
            SendMessage(_editor.Handle, WM_SETREDRAW, 1, 0);
            _editor.Invalidate();
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WM_SETREDRAW = 0x000B;
}

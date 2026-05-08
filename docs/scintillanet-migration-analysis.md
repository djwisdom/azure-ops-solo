# ScintillaNET Migration Analysis

Generated: 2026-05-08
Target: `Scintilla5.NET` v6.1.2 (NuGet) by Brandon Desjarlais

## Current RichTextBox Dependency Map

| Component | Lines | RTB Coupling |
|---|---|---|
| `HighlightRichTextBox` | 839 | Core editor — subclass with WM_PAINT interop, EM_SETCHARFORMAT coloring, custom caret, squiggles, fold bracket lines, column guide, rainbow brackets |
| `IncrementalHighlighter` | 365 | Uses EM_GETLINE/EM_GETLINECOUNT via SendMessage; dispatches patches via PatchReady |
| `FoldingManager` | 242 | Text-substitution hack: replaces folded regions with `// ...`, stores undo |
| `VimEngine` | 1209 | Reads/writes `_tb.Text`, `SelectionStart/Length`, `SelectedText`, `GetLineFromCharIndex()`, `Lines[]` |
| `GutterPanel` | 508 | Uses `ZoomFactor`, `Font`, `GetPositionFromCharIndex()`, `Lines[]`, EM_* Win32 messages |
| `MinimapControl` | 453 | EM_GETLINE, EM_LINESCROLL, EM_GETFIRSTVISIBLELINE, EM_GETLINECOUNT, cast to RichTextBox |
| `CurrentLineHighlightPanel` | 161 | LinkedEditor: RichTextBox, subscribes to SelectionChanged/VScroll/HScroll |
| `ColumnGuidePanel` | 94 | LinkedEditor: RichTextBox |
| `WhitespaceOverlayForm` | 217 | LinkedEditor: RichTextBox, EM_GETFIRSTVISIBLELINE, GetPositionFromCharIndex() |
| `ThemeManager` | — | Explicit `if (c is RichTextBox rtb)` branch |

**Secondary RTB instances** (6 files): DiffPanel, TerminalPanel, HoverTooltipForm, TestResultsDialog, Form1 (split editor), MinimapDemoForm

**Total**: ~15+ source files, ~3500+ lines of RichTextBox-dependent code.

## Scintilla5.NET v6.1.2

| Factor | Value |
|---|---|
| **NuGet** | `Scintilla5.NET` |
| **Maintainer** | Brandon Desjarlais (desjarlais on GitHub) |
| **Latest** | 6.1.2 (March 2026) |
| **TFM** | `net8.0-windows7.0` |
| **Scintilla** | 5.6.1 native + Lexilla 5.4.8 |
| **License** | MIT |
| **Contributors** | 30+ |

## What ScintillaNET Gives For Free

- Lexilla lexers: 80+ languages auto-highlighted
- Native folding: margin-based, per-folder indicators
- Margins API: built-in line numbers, markers for breakpoints/bookmarks, fold margin
- Multiple/rectangular selections
- Autocompletion (AutoC) and calltips (CallTip)
- Zoom independent of font
- Piece-table model: handles 100K+ line files efficiently
- Indicators: squiggles, underlines for diagnostics
- Configurable EOL modes

## Feature Porting Table

| Feature | Current | Scintilla Equivalent | Effort |
|---|---|---|---|
| Syntax highlighting | IncrementalHighlighter + EM_SETCHARFORMAT | Lexilla lexer + Style indexer | 1-2d |
| Folding | FoldingManager text-substitution | SetFoldLevel() + fold margin | 1-2d |
| Line numbers | GutterPanel custom paint | `Margins[0].Width = 60` | 0 |
| Breakpoints | GutterPanel.DrawBreakpoint() | `Markers[0].SetBackColor()` | 0 |
| Bookmarks | GutterPanel.DrawBookmark() | `Markers[1]` | 0 |
| Current line | CurrentLineHighlightPanel overlay | `CaretLineBackColor` | 0 |
| Column guide | ColumnGuidePanel | `EdgeColumn` + `EdgeMode.Line` | 0 |
| Rainbow brackets | RainbowBracketEngine → EM_SETCHARFORMAT | Custom indicator per depth level | 2-3d |
| Squiggles | HighlightRichTextBox DrawSquiggles | `Indicators[0].Style = Squiggle` | 1d |
| Vim engine | VimEngine (1209 lines, RTB-specific) | Rewire to Scintilla coordinate API | 3-5d |
| Minimap | MinimapControl custom paint | Adapt existing (same concept, new coordinates) | 1-2d |
| Whitespace overlay | WhitespaceOverlayForm | Scintilla has ViewWhitespace but limited glyph control | 1d |
| Split editor | Secondary RichTextBox | Secondary Scintilla instance | 1d |
| Diff viewer | DiffPanel._diffBox: RichTextBox | Scintilla in read-only with indicator coloring | 1-2d |
| Zoom | ZoomFactor | `editor.Zoom = n` | trivial |
| Search | VimEngine: `Text.IndexOf()` | `TargetStart/End` + `SearchInTarget` | 1d |
| Undo/Redo | Undo(), EM_REDO | `UndoRedo.Undo()/Redo()` | trivial |
| Scroll sync | EM_GETFIRSTVISIBLELINE, EM_LINESCROLL | `FirstVisibleLine`, `LineScroll()` | trivial |
| Tests | new RichTextBox() in fixtures | new Scintilla() — fundamental API change | 2-3d |

## Implementation Plan

### Phase 0 — Proof of Concept (1 week)
- Add Scintilla5.NET NuGet package
- Create Scintilla control alongside existing RTB in a side form
- Verify: line numbers margin, basic Lexilla highlighting, fold margin, zoom
- Run existing tests to confirm no regression

### Phase 1 — Core Editor Replacement (2-3 weeks)
- Replace HighlightRichTextBox with Scintilla worker
- Map 20+ theme colors to Scintilla Style definitions
- Remove IncrementalHighlighter → Lexilla lexers
- Rewrite FoldingManager → native SetFoldLevel + fold margin
- Wire TextChanged/caret events to existing consumers

### Phase 2 — Feature Port (3-4 weeks)
- Port VimEngine to Scintilla coordinate model
- Port MinimapControl
- Simplify GutterPanel (most features now native)
- Port WhitespaceOverlayForm
- Port squiggles to indicators
- Port rainbow brackets to indicators

### Phase 3 — Secondary Controls (1-2 weeks)
- Replace split editor RTB → Scintilla
- Replace DiffPanel._diffBox → Scintilla
- Update ThemeManager.ApplyToControls
- Update all event handlers

### Phase 4 — Testing & Polish (1-2 weeks)
- Rewrite test fixtures
- Edge case hunting (Unicode, long lines, encoding)
- Large file benchmark
- Final bug bash

**Total: 8-12 weeks (solo developer)**

## Recommendation: DON'T Migrate (Now)

The current editor already has a working feature set. Key reasons against:

1. **Risk-reward unfavorable**: User-facing gains are incremental (better large-file perf, native margins/fold feel) vs. 8-12 weeks of rewiring 15+ tightly-coupled files
2. **Solo developer bandwidth**: 80% of the changes are invisible to the user
3. **Existing implementation is already sophisticated**: WM_PAINT interop, rainbow brackets, squiggles, block cursor are already credible Scintilla-level features — just done the hard way
4. **Biggest pain point is FoldingManager's text-substitution hack**: Consider a lighter fix — implement collapsed-region rendering (skip drawing folded lines) instead of text replacement. This eliminates the undo-destruction bug with far less effort
5. **If you hit a hard wall** (200K-line file crashes, multi-caret requirement), Scintilla5.NET is the right next step — this analysis will be the blueprint

## Key Files to Modify (if proceeding)

```
Pfpad.csproj                          + Scintilla5.NET PackageReference
HighlightRichTextBox.cs               → Scintilla subclass (discard RTB base)
IncrementalHighlighter.cs             → Remove, replaced by Lexilla
FoldingManager.cs                     → Rewrite to SetFoldLevel + fold margin
VimEngine.cs                          → Rewrite coordinate/selection APIs
GutterPanel.cs                        → Simplify (most features now native)
MinimapControl.cs                     → Adapt coordinate APIs
CurrentLineHighlightPanel.cs          → Remove (Scintilla has built-in)
ColumnGuidePanel.cs                   → Remove (EdgeColumn built-in)
WhitespaceOverlayForm.cs              → Adapt coordinate APIs
ThemeManager.cs                       → Add Scintilla branch, remove RTB branch
Form1.cs                              → Replace HighlightRichTextBox with Scintilla
Form1.Designer.cs                     → Change field type, remove RTB-specific props
DiffPanel.cs                          → Replace RichTextBox with Scintilla
TerminalPanel.cs                      → Keep as RTB
Form1.Roslyn.cs                       → No API change (uses RoslynWorkspaceService)
RoslynWorkspaceService.cs             → No API change (accepts string)
IncrementalHighlighterTests.cs        → Rewrite fixtures
SyntaxHighlightRegressionTests.cs     → Rewrite fixtures
```

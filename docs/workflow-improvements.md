# Pfpad Workflow Improvements & Feature Recommendations

**Generated:** 2026-05-08 (Updated 2026-05-09)  
**Target:** Personal Flip Pad (Pfpad) — WinForms C# code editor

**Implementation status:** All 13 recommendations have been implemented since this document was written:
- **P0 #1 Format Document** — ✅ implemented (4fae677)
- **P0 #2 Auto-Save** — ✅ implemented (4fae677)  
- **P0 #3 Git inline diff gutter** — ✅ implemented (aa51315)
- **P1 #5 Session Restore** — ✅ implemented (0d8bdd8) via `SessionManager.cs`
- **P1 #4 Command Palette** — ✅ implemented (4fae677) via `CommandPaletteForm.cs`
- **P1 #6 Snippet Expansion** — ✅ implemented (4fae677) via `SnippetEngine.cs`
- **P1 #7 File Watcher** — ✅ implemented (4fae677)
- **P2 #8 Auto-Import / Add Missing Using** — ✅ implemented (4fae661) via Roslyn
- **P2 #9 Error Lens** — ✅ implemented (4fae677)
- **P2 #10 Multi-Caret Editing** — ✅ implemented (4fae677) via `MultiCaretManager.cs`
- **P3 #11 Markdown Preview** — ✅ implemented (4fae677) via `MarkdownPreviewPanel.cs`
- **P3 #12 Document Outline** — ✅ implemented (4fae677) via `OutlinePanel.cs`
- **P3 #13 Zen Mode** — ✅ implemented (4fae677) via `ToggleFullScreen()`

**All 13 recommendations are now implemented.** See `DEVELOPER_WORKFLOWS.md` and `WORKFLOW_ACTION_PLAN.md` for the remaining backlog (New Project Dialog, Welcome Page, Lint Rule Expansion, Build Config Selector, Solution Explorer).

The feature descriptions below are preserved for reference.

---

## Priority Matrix (Updated 2026-05-09 — All Implemented)

| Priority | Effort | Feature | Impact | Status |
|---|---|---|---|---|
| P0 | Low | Format Document (Roslyn) | High | ✅ |
| P0 | Low | Auto-Save | High | ✅ |
| P0 | Low | Git inline diff gutter | High | ✅ |
| P1 | Medium | Command Palette | High | ✅ |
| P1 | Medium | Session Restore | High | ✅ |
| P1 | Medium | Snippet Expansion | High | ✅ |
| P1 | Low | File Watcher (auto-refresh) | Medium | ✅ |
| P2 | Medium | Auto-Import / Add Missing Using | High | ✅ |
| P2 | Medium | Error Lens (inline diagnostics) | High | ✅ |
| P2 | High | Multi-Caret Editing | Medium | ✅ |
| P3 | Medium | Markdown Preview | Medium | ✅ |
| P3 | Medium | Document Outline | Medium | ✅ |
| P3 | Low | Zen Mode / Full Screen | Low | ✅ |

---

## P0 — Low Effort, High Impact

### 1. Format Document (Roslyn)

**What:** Single command (e.g. Ctrl+K, Ctrl+D) to auto-format the entire document using Roslyn's `CSharpFormatter.FormatAsync`. Also format on paste, format on save as opt-in.

**Current gap:** No formatting command exists. User manually indents. Roslyn `Microsoft.CodeAnalysis.CSharp.Formatting` is already a transitive dependency via `Microsoft.CodeAnalysis.Features`.

**Implementation:**

```csharp
// In Form1.Roslyn.cs
private async Task FormatDocumentAsync()
{
    var doc = await _roslynWorkspace.GetCurrentDocumentAsync();
    if (doc is null) return;
    var formatted = await CSharpFormatter.FormatAsync(doc);
    var text = await formatted.GetTextAsync();
    textEditor.Text = text.ToString();
    _roslynWorkspace.UpdateDocumentText(text.ToString());
}
```

Bind to `Keys.Control | Keys.K` followed by `Keys.Control | Keys.D` via the existing `ProcessCmdKey` override.

**Files to modify:** `Form1.Roslyn.cs`, `Form1.cs` (keybinding), menu edit menu

**Reason:** Developers spend significant time on formatting. Roslyn already provides perfect formatting — it's the #1 missing ergonomic feature.

---

### 2. Auto-Save

**What:** Periodically save all dirty files (configurable interval, default 30s). Visual indicator when auto-save fires. Respects the existing save-on-run setting.

**Current gap:** Only manual save via Ctrl+S. No safety net for crashes.

**Implementation:**

```csharp
// In Form1.cs — add a Timer
_autoSaveTimer = new Timer { Interval = 30000 };
_autoSaveTimer.Tick += (_, _) =>
{
    if (!_autoSaveEnabled) return;
    foreach (var doc in documents)
        if (doc.IsDirty && !string.IsNullOrEmpty(doc.Path))
            SaveFile(doc);
    ShowNotification("Auto-Save", $"Saved {documents.Count(d => d.IsDirty)} files");
};
```

Add `_autoSaveEnabled` to `AppSettings`. Toggle from View menu.

**Files to modify:** `Form1.cs`, `Form1.Designer.cs` (menu + status bar indicator)

**Reason:** Prevents data loss on crashes. Low effort (one Timer). Every modern editor has it.

---

### 3. Git Inline Diff Gutter

**What:** Color-coded vertical strips in the gutter margin showing added (green), modified (orange), deleted (red) lines compared to HEAD. Uses existing `GitService` which already provides status/diff via LibGit2Sharp.

**Current gap:** `GutterPanel.DrawChangeIndicator()` draws a single orange bar for lines with uncommitted changes. No distinction between added/modified/deleted, no per-line granularity.

**Implementation:**

```csharp
// In GitService.cs — add method
public Dictionary<int, DiffLineType> GetLineDiffs(string filePath)
{
    var repo = new Repository(_repoPath);
    var patch = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, filePath);
    var result = new Dictionary<int, DiffLineType>();
    foreach (var change in patch)
        foreach (var line in change.Lines)
            result[line.NewLineNumber] = line.Type; // Insert/Delete/Modified
    return result;
}

// In GutterPanel.cs — replace DrawChangeIndicator
private void DrawChangeIndicator(Graphics g, int lineIndex, int x, int y)
{
    if (_lineDiffs.TryGetValue(lineIndex + 1, out var type))
    {
        Color c = type switch
        {
            DiffLineType.Insert => Color.FromArgb(60, 180, 60),
            DiffLineType.Delete => Color.FromArgb(220, 60, 60),
            _ => Color.FromArgb(200, 160, 40)
        };
        using var brush = new SolidBrush(c);
        g.FillRectangle(brush, x + 2, y + 2, 4, lineH - 4);
    }
}
```

**Files to modify:** `GitService.cs`, `GutterPanel.cs`, `Form1.cs` (periodic diff refresh)

**Reason:** This is the most visible git integration missing. VS Code, IntelliJ, and every modern editor shows this. The data (`GitService`) already exists — just need to render it.

---

## P1 — Medium Effort, High Impact

### 4. Command Palette

**What:** Ctrl+Shift+P overlay that lists all available commands (menu items, vim ex commands, external tools) with fuzzy search. Select or type to execute. Modeled after VS Code's Command Palette.

**Current gap:** All commands are buried in menus. No quick way to discover or execute commands without memorizing keyboard shortcuts.

**Implementation — new file `CommandPaletteForm.cs`:**

```csharp
public sealed class CommandPaletteForm : Form
{
    private TextBox _searchBox;
    private ListBox _resultsList;
    private List<CommandEntry> _commands;

    public record CommandEntry(string Name, string Shortcut, Action Execute);

    public CommandPaletteForm(IEnumerable<CommandEntry> commands)
    {
        // Form: small overlay, centered, dark-themed
        // SearchBox: top, filters results by fuzzy match as user types
        // ResultsList: shows filtered commands, Enter to execute
        // Escape to close
    }
}
```

Wire up in `Form1.cs`:

```csharp
// Collect all commands from menus
private void ShowCommandPalette()
{
    var commands = new List<CommandEntry>();
    CollectMenuCommands(menuStrip.Items, commands);
    // Add vim :set options, external tools, etc.
    var palette = new CommandPaletteForm(commands);
    palette.ShowDialog(this);
}
```

**Files to modify:** New `CommandPaletteForm.cs`, `Form1.cs` (keybind Ctrl+Shift+P, command collection)

**Reason:** Transforms discoverability. Every modern editor has it. Reduces the learning curve for the app's massive feature set.

---

### 5. Session Restore

**What:** On app start, re-open all files that were open when the app last closed, restore cursor positions, scroll positions, and active tab. Persist to a JSON file in `%APPDATA%/Pfpad/session.json`.

**Current gap:** App always opens to a blank editor. No memory of previous session.

**Implementation:**

```csharp
public sealed record SessionData
{
    public List<DocumentSnapshot> Documents { get; init; } = new();
    public int ActiveIndex { get; init; }
    public DateTime LastSession { get; init; }
}

public sealed record DocumentSnapshot
{
    public string Path { get; init; } = "";
    public int CursorLine { get; init; }
    public int CursorColumn { get; init; }
    public int FirstVisibleLine { get; init; }
}

// Save: Form1.FormClosing event
private void SaveSession()
{
    var session = new SessionData
    {
        Documents = documents.Select(d => new DocumentSnapshot
        {
            Path = d.Path ?? "",
            CursorLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart),
            CursorColumn = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)),
            FirstVisibleLine = GetFirstVisibleLine()
        }).ToList(),
        ActiveIndex = tabControl.SelectedIndex,
        LastSession = DateTime.UtcNow
    };
    File.WriteAllText(_sessionPath, JsonSerializer.Serialize(session));
}

// Load: Form1.Shown event
private void RestoreSession()
{
    if (!File.Exists(_sessionPath)) return;
    var session = JsonSerializer.Deserialize<SessionData>(File.ReadAllText(_sessionPath));
    if (session is null) return;
    foreach (var snap in session.Documents)
        if (File.Exists(snap.Path))
            OpenFile(snap.Path, snap.CursorLine, snap.CursorColumn);
}
```

**Files to modify:** `Form1.cs`, `Form1.Designer.cs` (wire FormClosing/Shown)

**Reason:** Huge quality-of-life win. Lose zero context on restart. Every editor does this.

---

### 6. Snippet Expansion

**What:** Type a snippet shortcut + Tab to expand to a code template (e.g. `for` → `for (int i = 0; i < length; i++) { }`, `ctor` → constructor, `prop` → auto-property, `try` → try-catch). Support both built-in and user-defined snippets.

**Current gap:** No code generation beyond typing.

**Implementation — new file `SnippetEngine.cs`:**

```csharp
public sealed class SnippetEngine
{
    private readonly Dictionary<string, Snippet> _snippets;

    public record Snippet(string Prefix, string Description, string Body);

    public void TryExpand(RichTextBox editor, string prefix)
    {
        if (!_snippets.TryGetValue(prefix, out var snippet)) return;
        
        int pos = editor.SelectionStart;
        int start = pos - prefix.Length;
        
        // Replace prefix with snippet body
        editor.Select(start, prefix.Length);
        editor.SelectedText = snippet.Body;
        
        // Place cursor at first tab-stop if any ($0, $1)
        int cursorOffset = snippet.Body.IndexOf("$0");
        if (cursorOffset >= 0)
        {
            editor.SelectionStart = start + cursorOffset;
            editor.SelectionLength = 0;
        }
    }
}
```

```csharp
// In Form1.cs, intercept Tab in insert mode
// When Tab is pressed:
//   1. Get word before cursor
//   2. Try snippet expansion
//   3. If no match, insert indent
```

**Files to modify:** New `SnippetEngine.cs`, `Form1.cs` (Tab key handler), `Form1.Designer.cs` (menu for snippet management)

**Built-in snippets to include:**
- C#: `for`, `foreach`, `while`, `try`, `catch`, `finally`, `if`, `else`, `switch`, `using`, `class`, `struct`, `interface`, `enum`, `prop`, `propfull`, `ctor`, `destructor`, `main`, `console`
- General: `todo`, `hack`, `note`

**Reason:** One of the highest-productivity features in any editor. Low implementation complexity (string replacement + tab-stop tracking).

---

### 7. File Watcher (Auto-Refresh Workspace)

**What:** Use `FileSystemWatcher` to detect file/directory changes on disk and auto-refresh the workspace tree (and optionally reload changed files in the editor with a prompt).

**Current gap:** User must click Refresh or close/reopen the workspace. The auto-refresh timer at line 191-196 polls every 5s but uses `RefreshTree()` which only works for expanded directories.

**Implementation:**

```csharp
// In WorkspacePanel.cs
private FileSystemWatcher _fileWatcher;

private void StartFileWatcher(string rootPath)
{
    _fileWatcher?.Dispose();
    _fileWatcher = new FileSystemWatcher(rootPath)
    {
        IncludeSubdirectories = true,
        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
    };
    _fileWatcher.Created += (_, e) => DebounceRefresh();
    _fileWatcher.Deleted += (_, e) => DebounceRefresh();
    _fileWatcher.Renamed += (_, e) => DebounceRefresh();
    _fileWatcher.EnableRaisingEvents = true;
}

private void DebounceRefresh()
{
    _refreshTimer.Stop();
    _refreshTimer.Interval = 500;
    _refreshTimer.Tick += OnDebouncedRefresh;
    _refreshTimer.Start();
}

private void OnDebouncedRefresh(object? s, EventArgs e)
{
    _refreshTimer.Tick -= OnDebouncedRefresh;
    BeginInvoke(() => RefreshTree());
}
```

**Files to modify:** `WorkspacePanel.cs`

**Reason:** The current auto-refresh timer polls every 5s and only refreshes expanded nodes. A `FileSystemWatcher` is instant, low-overhead, and doesn't waste CPU on polling. This is the fix for the issue the user reported (Refresh not showing new files).

---

## P2 — Medium-High Effort

### 8. Auto-Import / Add Missing Using

**What:** When typing an unresolvable type name, show a lightbulb quick-action to add the missing `using` directive. Also offer auto-import on paste. Roslyn's `Microsoft.CodeAnalysis.AddImport` namespace provides this.

**Current gap:** Roslyn analyses and reports diagnostics but never suggests or applies fixes. The existing `QuickActionProvider` only handles lint rules.

**Implementation:**

```csharp
// In Form1.Roslyn.cs or new AddImportService.cs
private async Task CheckForMissingUsingsAsync()
{
    var doc = await _roslynWorkspace.GetCurrentDocumentAsync();
    if (doc is null) return;
    var model = await doc.GetSemanticModelAsync();
    var root = await doc.GetSyntaxRootAsync();
    
    // Find unresolved identifiers
    var unresolved = root.DescendantNodes()
        .OfType<IdentifierNameSyntax>()
        .Where(id => model.GetSymbolInfo(id).Symbol is null);
    
    foreach (var id in unresolved)
    {
        // Use Roslyn's AddImport analysis
        var addImport = await AddImportAnalyzer.GetFixesAsync(doc, id, CancellationToken.None);
        if (addImport.Count > 0)
        {
            // Show lightbulb in gutter
            gutterPanel.SetQuickAction(id.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                $"using {addImport[0]}",
                text => ApplyAddImport(text, addImport[0]));
        }
    }
}
```

**Files to modify:** New `AddImportService.cs` (or extend Form1.Roslyn.cs), `GutterPanel.cs` (lightbulb integration)

**Reason:** This is one of the most-used Roslyn IDE features. The Roslyn infrastructure is already wired up — just need the UI to suggest/apply fixes.

---

### 9. Error Lens (Inline Diagnostics)

**What:** Show diagnostic messages inline at the end of the affected line, in a muted color, instead of only in the Problems panel. Click the inline text to open Problems panel focused on that diagnostic.

**Current gap:** `HighlightRichTextBox` already draws squiggles for diagnostics, but the message text is only visible in the `ProblemsPanel` sidebar. User must look away from the editor to see the error message.

**Implementation — in `HighlightRichTextBox.cs`:**

```csharp
// Add to WndProc WM_PAINT
private void DrawDiagnosticInline(Graphics g)
{
    if (_squiggles.Count == 0) return;
    
    int lineH = (int)Math.Ceiling(Font.GetHeight() * ZoomFactor);
    using var font = new Font("Consolas", 8);
    using var brush = new SolidBrush(Color.FromArgb(160, Color.FromArgb(220, 180, 80)));
    
    foreach (var (start, length, color) in _squiggles)
    {
        int line = GetLineFromCharIndex(start);
        if (line < 0) continue;
        
        Point pt = GetPositionFromCharIndex(Math.Min(start + length, TextLength - 1));
        if (pt.IsEmpty) continue;
        
        int lineEndX = GetPositionFromCharIndex(Math.Min(GetFirstCharIndexFromLine(line + 1) - 1, TextLength - 1)).X;
        string message = GetDiagnosticMessageAt(start); // from ProblemsPanel data
        if (string.IsNullOrEmpty(message)) continue;
        
        int textX = Math.Max(pt.X, lineEndX + 20);
        if (textX + 400 > ClientSize.Width) continue; // off-screen
        
        g.DrawString($"// {message}", font, brush, textX, pt.Y);
    }
}
```

**Files to modify:** `HighlightRichTextBox.cs`, `Form1.cs` (pass diagnostic data to editor)

**Reason:** Dramatically reduces context-switching. See errors where they occur — no sidebar lookup needed.

---

### 10. Multi-Caret Editing

**What:** Alt+Click to add additional carets. Ctrl+Alt+Up/Down to add carets above/below. Type at multiple positions simultaneously. Each caret has its own selection.

**Current gap:** Single cursor. Editing is linear. The `RichTextBox` doesn't natively support multiple selections.

**Implementation approach:**

This is the hardest recommendation because WinForms `RichTextBox` doesn't support multiple carets natively. Two approaches:

**A) Simulated multi-caret (easier):**
- Track a list of virtual caret positions
- On typing, iterate through all positions and apply the same edit via `Select`/`SelectedText` in sequence
- On arrow keys, move all carets

```csharp
public class MultiCaretManager
{
    private readonly RichTextBox _editor;
    private List<int> _carets = new();
    private List<int> _selections = new(); // selection length per caret
    private bool _active;

    public void AddCaretAtClick(Point location)
    {
        int pos = _editor.GetCharIndexFromPosition(location);
        _carets.Add(pos);
        _selections.Add(0);
    }

    public void InsertText(string text)
    {
        // Save scroll position
        // For each caret (descending by position to avoid offset issues):
        //   Select(start, length), SelectedText = text
        // Restore scroll position
    }
}
```

**B) RichTextBox hack (fragile):**
- Use `EM_SETCHARFORMAT` with `CFE_SELECTION` coloring
- Simulate multiple selections by cycling through positions and using `EM_SETSEL`
- This is what the incremental highlighter already does for rainbow brackets

**Files to modify:** New `MultiCaretManager.cs`, `Form1.cs` (mouse/keyboard interception)

**Reason:** Multi-caret is a superpower for repetitive edits. Every modern editor supports it. But this is high effort for a fragile result on RTB.

---

## P3 — Nice to Have

### 11. Markdown Preview

**What:** Side-by-side rendered HTML preview for `.md` files. Uses MarkdownSharp or Markdig NuGet package to render to HTML, displayed in a `WebView2` or `WebBrowser` control.

**Implementation:**

```csharp
// New MarkdownPreviewForm.cs or embedded panel
public sealed class MarkdownPreviewPanel : Panel
{
    private WebView2 _webView; // or WebBrowser for older compat
    private string _sourceFile;

    public void Render(string markdown)
    {
        string html = Markdig.Markdown.ToHtml(markdown);
        _webView.NavigateToString(WrapInStyledHtml(html));
    }
}

// In Form1.cs, detect .md file and offer split view
```

**Reason:** Useful for reading README, documentation files without leaving the editor. Markdig is a well-maintained NuGet package.

---

### 12. Document Outline

**What:** Tree view of the current file's structure — classes, methods, properties, fields, regions. Click to navigate. Updates live as you type. Similar to VS Code's Outline view.

**Current gap:** `SymbolPanel.cs` shows a flat searchable list of symbols. An outline should show the **hierarchical** structure of the **current file only**, updating in real-time.

**Implementation:**

```csharp
// New OutlinePanel.cs (or extend SymbolPanel)
public sealed class OutlinePanel : UserControl
{
    private TreeView _tree;
    
    public void UpdateFromTokens(string text, SyntaxDefinition syntax)
    {
        _tree.Nodes.Clear();
        // Parse for class/method/region headers using regex
        // Build tree: namespace > class > method
        // Subscribe to editor TextChanged for live updates
    }
}
```

**Files to modify:** New `OutlinePanel.cs`, `Form1.Designer.cs` (add to sidebar), `Form1.cs` (wire to editor TextChanged)

**Reason:** Quick navigation within large files. Complements the symbol panel (which is cross-file search) with per-file hierarchy.

---

### 13. Zen Mode / Full Screen

**What:** Toggle (F11 or Ctrl+K Z) that hides all toolbars, sidebars, panels, status bar, and terminal — leaving only the editor. Escape or F11 to restore.

**Current gap:** No way to hide the chrome and focus purely on the code.

**Implementation:**

```csharp
private bool _zenMode;

private void ToggleZenMode()
{
    _zenMode = !_zenMode;
    menuStrip.Visible = !_zenMode;
    sidebarPanel.Visible = !_zenMode;
    terminalSplitContainer.Visible = !_zenMode;
    statusStrip.Visible = !_zenMode;
    tabControl.Visible = !_zenMode;
    // Move editor to fill entire form
    textEditor.Dock = _zenMode ? DockStyle.Fill : DockStyle.None;
    // Restore layout
}
```

**Files to modify:** `Form1.cs`, `Form1.Designer.cs` (keybind F11, View menu item)

**Reason:** Low effort. Useful for presentations, deep focus, or working on small screens.

---

## Quick Wins Summary

| # | Feature | Files to Create | Files to Modify | Est. Days | Status |
|---|---|---|---|---|---|---|
| 1 | Format Document | — | `Form1.Roslyn.cs`, `Form1.cs` | 0.5 | ✅ |
| 2 | Auto-Save | — | `Form1.cs` | 0.5 | ✅ |
| 3 | Git inline diff gutter | — | `GitService.cs`, `GutterPanel.cs` | 1 | ✅ |
| 4 | Command Palette | `CommandPaletteForm.cs` | `Form1.cs` | 2 | ✅ |
| 5 | Session Restore | `SessionManager.cs` | `Form1.cs` | 1 | ✅ |
| 6 | Snippet Expansion | `SnippetEngine.cs` | `Form1.cs` | 2 | ✅ |
| 7 | File Watcher (auto-refresh) | — | `WorkspacePanel.cs` | 1 | ✅ |
| 8 | Auto-Import / Add Using | — | `Form1.Roslyn.cs`, `GutterPanel.cs` | 2 | ✅ |
| 9 | Error Lens (inline diagnostics) | — | `HighlightRichTextBox.cs`, `Form1.cs` | 1 | ✅ |
| 10 | Multi-Caret Editing | `MultiCaretManager.cs` | `Form1.cs` | 3 | ✅ |
| 11 | Markdown Preview | `MarkdownPreviewPanel.cs` | `Form1.cs` | 2 | ✅ |
| 12 | Document Outline | `OutlinePanel.cs` | `Form1.Designer.cs`, `Form1.cs` | 2 | ✅ |
| 13 | Zen Mode / Full Screen | — | `Form1.cs` | 0.5 | ✅ |

**All 13 features implemented. Estimated total: 18.5 days of work — delivered across commits 4fae677, aa51315, 0d8bdd8, 4fae661.**

If I had to pick 3: **Format Document** (0.5d), **Auto-Save** (0.5d), **Session Restore** (1d) = 2 days for the highest bang-for-buck.

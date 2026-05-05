# Personal Flip Pad: The Complete User Manual

*Written in the style of a Stack Overflow answer that somehow got out of hand.*

---

## Question

I've just started using Personal Flip Pad (Pfpad), a WinForms code editor written in C# targeting .NET 8. What features does it have, and how do I use them without accidentally causing a stack overflow?

---

## Answer

The short answer is "rather a lot for a solo project." The long answer follows below. I'll assume you're familiar with the general concept of a text editor — if not, I admire your commitment to jumping in at the deep end.

---

## 1. Getting Started

### 1.1 Installation

The installer (`PersonalFlipPad-Setup-1.0.0.0.exe`) supports both per-user and per-machine installation courtesy of Inno Setup:

```
PersonalFlipPad-Setup-1.0.0.0.exe /CURRENTUSER   # No admin required
PersonalFlipPad-Setup-1.0.0.0.exe /ALLUSERS       # Admin required
PersonalFlipPad-Setup-1.0.0.0.exe /VERYSILENT /CURRENTUSER  # Quiet mode
```

The editor is published as a self-contained single-file executable — no .NET runtime required on the target machine. It is, however, approximately 48 MB, so you might want to ensure your storage budget can accommodate it.

### 1.2 Starting the Editor

Launch `MyCrownJewelApp.Pfpad.exe`. You can pass file paths as command-line arguments:

```bash
Pfpad.exe file1.cs file2.cs README.md
```

Each file opens in its own tab. If no files are specified, you get a single untitled document. Crash logs go to `%LOCALAPPDATA%\MyCrownJewelApp\Pfpad\crash.log` — hopefully you won't need them, but they're there if you do.

---

## 2. The Menu System

The menu strip at the top contains six menus: File, Edit, View, Panel, Run, and Tools. (Plus a Help menu with exactly one item, which is about as helpful as you'd expect from a software project.) The separation is deliberate — View controls how the editor looks, Panel controls which sidebars appear, and Run controls what happens when you press the "go" lever.

### 2.1 File Menu

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| New Tab | `Ctrl+T` | Creates an untitled document. Numbering starts at 1 and increments. No, you can't reset it without restarting. |
| New Window | `Ctrl+Shift+N` | Opens a completely new instance of the editor. Useful for comparing two files side by side on different monitors, or for that "I've gone too far and need a clean slate" feeling. |
| Open | `Ctrl+O` | Standard open file dialog. The dialog is theme-aware, which means it won't blind you at 2 AM. |
| Recent Files | | Submenu listing the last 10 files you opened. Persisted across sessions. Includes a "Clear Recent" option for when your browsing history embarrasses you. |
| Save | `Ctrl+S` | Saves the current file. If the file has no path yet, this is equivalent to Save As. |
| Save As | `Ctrl+Shift+S` | Saves with a new name and path. |
| Save All | `Ctrl+Alt+S` | Saves all open files. Currently, this saves the active file, which is "all" in the sense that you can only edit one file at a time. Future versions may expand this definition. |
| Close Tab | `Ctrl+W` | Closes the current tab. Will prompt to save if modified. The editor never closes its last tab — you'd have to close the window instead, which is a deliberate design choice to prevent accidentally losing your workspace. |
| Close Window | `Ctrl+Shift+W` | Closes the entire window. |
| Close All | `Ctrl+Alt+W` | Prompts to save all modified files, then exits. |
| Exit | | Also closes the application. It's the same as Close Window but with less ceremony. |

### 2.2 Edit Menu

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| Undo | `Ctrl+Z` | Reverses the last edit. The editor also tracks dirty state using a SHA-256 hash of the content, so it knows when you've returned to the saved version. |
| Cut | `Ctrl+X` | Removes selected text and places it on the clipboard. Classic. |
| Copy | `Ctrl+C` | Duplicates selected text to the clipboard without removing it. |
| Paste | `Ctrl+V` | Inserts clipboard contents at the cursor position. |
| Delete | `Delete` | Removes selected text without copying. |
| Find | `Ctrl+F` | Opens the Find dialog. Supports case-sensitive, regex, direction (up/down), and wrap-around searching. |
| Find in Files | `Ctrl+Shift+F` | Opens the Global Search dialog for async workspace-wide search with file type filters, exclude dirs, regex, and Replace All. |
| Find Next | `F3` | Repeats the last search forward. |
| Find Next | `F3` | Repeats the last search forward. |
| Find Previous | `Shift+F3` | Repeats the last search backward. |
| Replace | `Ctrl+H` | Opens the Replace variant of the Find dialog, with Replace and Replace All buttons. |
| Go To | `Ctrl+G` | Jumps to a specific line number. If you type a number larger than the total lines, it gracefully clamps to the last line rather than throwing an exception. |
| Select All | `Ctrl+A` | Selects the entire document. |
| Time/Date | `F5` | Inserts the current time and date in `HH:mm:ss yyyy-MM-dd` format at the cursor. |
| Font | `Ctrl+Shift+F` | Opens the standard Font dialog to change the editor font. |
| Toggle Bookmark | `Ctrl+F2` | Places or removes an orange bookmark dot in the gutter at the current line. |
| Next Bookmark | `F2` | Moves the cursor to the next bookmark. |
| Previous Bookmark | `Shift+F2` | Moves the cursor to the previous bookmark. |
| Clear Bookmarks | | Removes all bookmarks at once. |
| Toggle Fold | `Ctrl+Shift+[` | Collapses or expands the code fold at the current line. |
| Toggle All Folds | `Ctrl+Alt+[` | Collapses or expands all foldable regions. |
| Go to Definition | `F12` | Navigates to the definition of the identifier at the cursor. Uses ctags or regex-based fallback. |
| Rename | `F2` | Opens a project-wide rename dialog for the identifier at the cursor. Regex-based, not semantic — it renames every occurrence in every workspace file. Preview before applying. |
| Call Hierarchy | `Ctrl+Shift+H` | Shows incoming callers and outgoing calls for the method at the cursor. Tree view with clickable navigation. |
| Parse Stack Trace | `Ctrl+Shift+T` | Parses a selected .NET, JavaScript, Python, or generic stack trace into clickable file:line frames. |

### 2.3 View Menu

This is where most of the editor's personality lives.

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| Zoom In | `Ctrl+Plus` | Increases the zoom factor by 0.1, up to a maximum of 5.0 (500%). |
| Zoom Out | `Ctrl+Minus` | Decreases the zoom factor by 0.1, down to a minimum of 0.5 (50%). |
| Restore Default Zoom | `Ctrl+0` | Resets zoom to 1.0 (100%). |
| Status Bar | | Toggles the status bar at the bottom. The status bar shows cursor position, character count, git branch, zoom level, line endings, encoding, theme, syntax type, and notification count. It's quite informative for a single line of pixels. |
| Word Wrap | | Toggles whether long lines wrap at the window edge. |
| Syntax Highlighting | | Enables or disables the incremental syntax highlighter. When disabled, all text is rendered in the base color — useful if you find colors distracting, or if you've already memorized the syntax of all 12 supported languages. |
| Current Line Highlight | | Three sub-options: **Off** (nothing), **Line Number Only** (gutter line number in bold), **Whole Line** (full-width background highlight). |
| Insert Spaces | | When enabled, pressing Tab inserts spaces instead of a tab character. The number of spaces is determined by the Tab Size setting. |
| Tab Size | | Submenu with options: 2, 4, 6, 8, 10, 12. If you use anything other than 4, you should be prepared to defend your choice. |
| Auto Indent | | When enabled, pressing Enter on a line copies the leading whitespace from the previous line. |
| Smart Tabs | | When enabled, pressing Tab at the start of a line indents to the next tab stop; pressing Tab in the middle of text inserts an actual tab character. |
| Elastic Tabs | | When enabled, tab stops are computed dynamically based on the maximum column width of visible lines — similar to the "elastic tabstops" concept. This runs on a background thread with a 250ms debounce. |
| Column Guide | | Submenu with presets (72, 80, 100, 120, 150) and a "Custom" option. Shows a vertical dotted line at the specified column. |
| Gutter | | Toggles the line number gutter on the left side of the editor. |
| Show Whitespace | | Renders dots for spaces, arrows for tabs, and return symbols for newlines. On by default — you can finally see where all those trailing spaces are. |
| Minimap | | Toggles the code minimap overlay on the right side of the editor. Shows a syntax-colored overview of the entire document with a viewport indicator. Click to jump, drag to scroll. |
| Vim Mode | `Ctrl+Alt+V` | Enables or disables Vim emulation. See the Vim section below for the full list of supported commands. |
| Split Vertical | `Ctrl+Shift+V` | Creates a vertical split pane showing a different document. |
| Split Horizontal | `Ctrl+Alt+H` | Creates a horizontal split pane. |
| Theme | | Submenu with 15 built-in themes: Dark (default), Light, four Catppuccin variants (Latte, Frappe, Macchiato, Mocha), Dracula, One Dark Pro, Tokyo Night, Night Owl, Shades of Purple, Atom One Light, GitHub Light, Light Owl, Ayu Light, and Bluloco Light. |

### 2.4 Panel Menu

The Panel menu is where all sidebar panels live — think of it as the "more tools" drawer. Each item toggles a panel's visibility, and they can be combined freely.

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| Open Folder | `Ctrl+Alt+O` | Sets the workspace root directory. Required before most panel features work. |
| Workspace | `Ctrl+Shift+W` | Toggles the file tree sidebar. See section 4.1. |
| Source Control | `Ctrl+Alt+G` | Toggles the Git panel in the sidebar. See section 6.3. |
| Source Control Window | `Ctrl+Shift+G` | Opens a standalone git form with a larger commit area and full log. |
| Symbols | `Ctrl+Alt+S` | Toggles the symbol index sidebar listing all classes, methods, properties, and variables found in the workspace. Color-coded kind badges and a search filter make finding things bearable. Double-click to navigate. |
| Problems | `Ctrl+Alt+P` | Toggles the diagnostics panel showing lint warnings, errors, and TODO items. Color-coded by severity (red=error, yellow=warning, blue=info). Double-click to jump to the offending line. |
| Terminal | `` Ctrl+` `` | Toggles the integrated terminal panel. |
| Notification Center | `Ctrl+Shift+N` | Opens the RSS feed notification viewer. |
| Notification Settings | | Opens the feed configuration dialog. |
| Dependencies | `Ctrl+Alt+D` | Opens the project dependency graph dialog showing project references and NuGet package references. |
| Impact Analysis | `Ctrl+Alt+I` | Shows which files would be affected by changes to the current file, based on cross-file namespace usage. |
| Run Configurations | `Ctrl+F5` | Opens the launch profiles dialog. Scans for `launchSettings.json` (from ASP.NET projects) and `.env` files, grouped by project. Run and Stop buttons launch `dotnet run` with environment variables. |
| Task List | `Ctrl+Alt+T` | Scans the workspace for TODO, FIXME, HACK, NOTE, XXX, BUG, OPTIMIZE, and REVIEW comments. Results are merged into the Problems panel with severity mapped by tag type. |

### 2.5 Run Menu

Everything related to running tests and measuring code quality lives here. If you're not a "run tests first, ask questions later" person, this section may cause discomfort.

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| Run Tests | `Ctrl+Alt+F5` | Finds the first `*Tests.csproj` or `*Test.csproj` in the workspace and runs `dotnet test` with TRX logging. Parses the results into a structured dialog showing pass/fail/skip counts, test names with colored badges, error messages, and stack traces. Double-click a stack trace frame to open the file at the failing line. |
| Rerun Failed Tests | `Ctrl+Alt+F6` | Re-runs only the tests that failed in the last run (using a fully-qualified-name filter). Shows results in the same structured dialog. |
| Run Tests with Coverage | `Ctrl+Alt+R` | Runs `dotnet test --collect "Code Coverage"` with coverlet. Parses the resulting Cobertura XML and shows a coverage summary dialog. Code coverage bars appear in the gutter — green for covered, red for missed. |
| Load Coverage File | | Opens a file picker for loading a `.cobertura.xml` or `.xml` file directly, without re-running tests. |

### 2.6 Tools Menu

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| External Tools | `Ctrl+Alt+T` | Opens the External Tools configuration dialog. You can define up to 9 custom commands with variable substitution (`$(FilePath)`, `$(SelText)`, `$(CurLine)`, etc.) and assign them keyboard shortcuts `Ctrl+Alt+Shift+1` through `9`. |

### 2.7 Help Menu

| Command | What It Does |
|---------|-------------|
| About | Shows a dialog with the version number, git commit hash, build date, and OS information. The commit hash is embedded at compile time via a build target that runs `git rev-parse HEAD`. If git isn't available, it displays "unknown." |

---

## 3. The Status Bar

The status bar at the bottom contains, from left to right:

1. **Vim Mode Indicator** — Shows `-- NORMAL --`, `-- INSERT --`, `-- VISUAL --`, or `-- VISUAL LINE --` when Vim mode is active. Hidden otherwise.
2. **Cursor Position** — `Ln 42, Col 7` format.
3. **Character Count** — Total number of characters in the document.
4. **Tab Size** — Dropdown showing "Tab: 4" (or whatever you've set it to). Click to change without visiting the menu.
5. **Git Branch** — Current branch name when inside a git repository. Uses LibGit2Sharp rather than shelling out to git.exe.
6. **Git Dirty Indicator** — A dot (●) when there are uncommitted changes.
7. **Line Position** — `{currentLine} / {totalLines}`.
8. **Zoom Level** — `100%` (or whatever).
9. **Line Endings** — Always shows "Windows (CRLF)."
10. **Encoding** — Always shows "UTF-8."
11. **Theme Dropdown** — Click to switch themes without visiting the menu.
12. **File Type** — Shows the detected syntax name ("C#", "Plain Text", etc.).
13. **Notification Badge** — Shows "N" with count of unread RSS feed items. Click to open the Notification Center.

---

## 4. The Sidebar Panels

### 4.1 Workspace Panel

The file tree sidebar. Features:

- **Lazy-loading** — Directories expand on demand, not eagerly.
- **Filtered view** — Shows only known text file extensions by default (about 65 of them).
- **Ignored directories** — `.git`, `node_modules`, `bin`, `obj`, `.vs`, `packages`, and any directory starting with `.` are hidden.
- **Auto-refresh** — Refreshes every 5 seconds when the tree is expanded.
- **Right-click context menu** — Open, Open Containing Folder, New File, New Folder, Copy Path.
- **New File** — Prompts for a filename, creates the file, opens it in the editor, and refreshes the tree.
- **New Folder** — Prompts for a folder name, creates it, and refreshes the tree.
- **Drag-drop** — Dropping a file opens it; dropping a directory sets a new workspace root.
- **Collapse All / Expand All** toggle button in the header.

### 4.2 Source Control (Git) Panel

Appears below the Workspace panel when toggled on. Powered by LibGit2Sharp (no git.exe required):

- **Branch switcher** — Dropdown showing all local branches.
- **Changes list** — Three sections: Staged, Unstaged, Untracked. Status letters: `[M]` modified, `[A]` added, `[D]` deleted, `[?]` untracked.
- **Stage/Unstage** — Single-click toggles between staged and unstaged.
- **Double-click** on a file opens it in the editor.
- **Stage All** — Stages all modified/new/deleted files at once.
- **Commit** — Type a message and click Commit. Only enabled when there are staged changes AND a non-empty message.
- **Recent commits** — Shows the last 30 commits.
- **Fetch / Pull / Push** — Buttons with error handling. Pull and Push show result dialogs.
- **Not in a repo?** — Shows a friendly message instead of crashing.

### 4.3 Terminal Panel

An integrated terminal that auto-detects the available shell (PowerShell 7, PowerShell 5.1, cmd.exe, zsh, bash). Features:

- **ANSI escape sequence support** — 16 colors, bold, erase-line, carriage-return handling.
- **Multiple terminal tabs** — Click the "+" button to add more.
- **Command history** — Up/Down arrow keys.
- **Ctrl+C** via stop button.
- **URL detection** — Clickable URLs.
- **Clear output** button.
- **Theme-aware** — Colors match the current editor theme.

### 4.4 Symbols Panel (Ctrl+Alt+S)

A dockable sidebar listing every indexed symbol in the workspace. The symbol index is rebuilt on a background thread after each workspace scan, scanning every `.cs` file with regex patterns for:

- `class`, `struct`, `interface`, `enum` declarations
- `method`, `function`, `property`, `field`, `variable` declarations

Each symbol is displayed with:
- **Kind badge**: A colored square with a single-letter label — C (class, teal), I (interface, purple), M (method, blue), P (property, gold), F (field/function, gold), V (variable, gold).
- **Name and file**: The symbol name and its source file (short name).
- **Search filter**: Type to filter by name, context, or kind.

Double-click any symbol to navigate to its file and line. The panel auto-refreshes when the index is updated.

### 4.5 Problems Panel (Ctrl+Alt+P)

Displays all diagnostics in the workspace in a unified list — lint warnings, errors, and scanned TODO items all appear here. Each entry shows:

- **Severity badge**: Red (error), yellow (warning), blue (info).
- **Message**: The diagnostic text, truncated with ellipsis if too long.
- **Location**: Line number on the right side.
- **Rule code**: The diagnostic code (e.g., `PFP002`, `TODO`).

Double-click any item to jump to the source location. The panel header shows a count summary: "Problems (3 errors, 5 warnings, 2 other)." Items are sorted by line number for natural reading order.

## 5. Code Editing Features

### 5.1 Syntax Highlighting

The editor employs an **incremental** syntax highlighting engine (`IncrementalHighlighter.cs`). Rather than tokenizing the entire document on every keystroke (which would be wasteful), it:

1. Tokenizes only the **visible lines** using a background worker thread
2. Communicates via a **`Channel<(int, string)>`** bounded queue with a drop-oldest policy when the queue exceeds 2000 items
3. Tracks **multi-line comment state** across lines
4. Marks lines as **dirty** when text changes and re-tokenizes them

Supported languages (12):

| Language | Extensions |
|----------|-----------|
| C# | `.cs`, `.csx` |
| C | `.c`, `.h` |
| C++ | `.cpp`, `.cxx`, `.cc`, `.hpp`, `.hxx`, `.hh` |
| Bicep | `.bicep` |
| Terraform | `.tf`, `.tfvars` |
| YAML | `.yaml`, `.yml` |
| HTML | `.html`, `.htm`, `.xhtml` |
| CSS / SCSS / Less | `.css`, `.scss`, `.sass`, `.less` |
| JavaScript / JSX | `.js`, `.jsx`, `.mjs`, `.cjs` |
| JSON / JSONC | `.json`, `.jsonc` |
| PowerShell | `.ps1`, `.psm1`, `.psd1` |
| Bash | `.sh`, `.bash`, `.zsh`, `.ksh` |

Token types that are highlighted: **keywords**, **strings**, **numbers**, **comments** (line and block), **preprocessor directives**, and **types**. Comments, types, and preprocessor tokens are rendered in italic. Because we can.

### 5.2 Code Folding

The folding manager (`FoldingManager.cs`) scans for brace pairs `{ }` with nesting awareness, and also recognizes `#region`/`#endregion` directives. Fold markers appear in the gutter as chevron arrows. When a region is collapsed, the text is replaced with `{ // ... }` — a reasonable compromise between "show nothing" and "show everything."

Fold bracket lines are drawn as dotted vertical lines connecting open and close braces — useful for visually tracking scope depth without counting indent levels.

### 5.3 Matching Braces

When the cursor is positioned on `{`, `}`, `[`, `]`, `(`, or `)`, the editor draws a 2px-thick white rectangle around both the cursor brace and its matching counterpart. This works for nested brackets as well, assuming the nesting is correct. If the brackets are mismatched, no rectangle is drawn — an honest admission of uncertainty.

### 5.4 Column Guide

A dotted vertical line at the specified column position. Defaults to column 80, because some traditions are worth preserving even if the original reasons (punch cards) no longer apply.

### 5.5 Current Line Highlight

Three modes:
- **Off**: Nothing happens. The current line looks like any other line.
- **Line Number Only**: The gutter's line number for the current line is rendered in bold yellow.
- **Whole Line**: A semi-transparent background highlight is applied across the entire width of the current line, composited via BitBlt to avoid overwriting text.

### 5.6 Whitespace Visualization

An overlay panel draws glyphs for invisible characters:
- **Spaces** → `·` (middle dot)
- **Tabs** → `→` (right arrow)
- **Newlines** → `↵` (carriage return arrow)

The overlay uses the `WS_EX_TRANSPARENT` window style to pass mouse events through, and repaints are debounced at 16ms intervals to avoid burning CPU cycles on every keystroke.

### 5.7 Minimap

A miniature, syntax-colored overview of the entire document, positioned on the right side of the editor. Features:

- Each line is rendered as a single-pixel row with syntax coloring
- A semi-transparent blue viewport rectangle shows the visible area
- **Click** to jump to a position
- **Drag** the viewport rectangle for smooth scrolling
- The minimap is rebuilt on a background thread with dirty tracking
- Configurable width (default 200px)

### 5.8 Zoom

Zoom ranges from 50% to 500% in 10% increments. Ctrl+MouseWheel is the quickest way to adjust. The gutter width, caret width, and status bar zoom label are all updated to reflect the current zoom level.

### 5.9 Elastic Tab Stops

When enabled, elastic tab stops compute the optimal alignment for tab-delimited columns based on the visible lines. The computation runs on a background thread with a 250ms debounce timer to avoid thrashing during rapid typing. This is particularly useful for editing tabular data or code where alignment matters.

### 5.10 Indentation

The editor supports four indentation-related settings:

1. **Tab Size** (2–12): Width of a tab stop.
2. **Insert Spaces**: Tab key inserts spaces instead of tab characters.
3. **Auto Indent**: New lines inherit the leading whitespace of the previous line.
4. **Smart Tabs**: Tab at the start of a line indents to the next tab stop; tab in the middle of text inserts a literal tab.

The `IndentationHelper` class computes the optimal mix of tabs and spaces for a given indentation target — useful for projects with mixed indentation conventions.

### 5.11 Split View

The editor supports split panes in both vertical and horizontal orientations. You can also **drag a tab** toward the edge of the window to create a split pane in that direction (left, right, top, bottom). Each split pane shows a different document, and each has its own gutter, minimap, and scroll position.

### 5.12 Go to Definition (F12)

Press F12 (or right-click → "Go to Definition") with the cursor on an identifier to navigate to its definition. The feature works via a two-tier approach:

1. **Tier 1**: Attempts to use `ctags.exe` if available (provides ~95% accuracy for 40+ languages).
2. **Tier 2**: Falls back to per-language regex patterns that match class, method, property, function, and variable declarations.

If there's a single match, you're taken directly to the file and line. Multiple matches show a picker dialog. No matches show a helpful message suggesting you open a workspace folder first — because the index needs something to index.

### 5.13 Live Diagnostics & Squiggly Underlines

The lint engine runs on a background thread with a 400ms debounce. It doesn't need Roslyn — all rules are regex/text-based. Five rules are currently implemented:

| Code | Rule | What It Flags |
|------|------|---------------|
| PFP001 | Trailing whitespace | Any line ending with spaces or tabs |
| PFP002 | Line length | Lines exceeding 120 characters |
| PFP003 | Magic numbers | Numeric literals that aren't 0, 1, or -1 (ignores array indices, enum values, and a few other well-known exceptions) |
| PFP004 | Missing semicolon | Lines that look like they need a `;` but don't have one |
| PFP005 | Naming convention | `camelCase` for local variables, `PascalCase` for methods and types, `_camelCase` for fields |

Diagnostics appear in the Problems panel (Panel > Problems or `Ctrl+Alt+P`) and as squiggly underlines in the editor — red for errors, yellow for warnings, blue for info. The squiggles are drawn as zigzag lines in the `WM_PAINT` handler, which is the kind of sentence that makes WinForms developers nod knowingly and everyone else look concerned.

### 5.14 Quick Actions (Gutter Lightbulbs)

When the lint engine detects a fixable issue, a lightbulb icon appears in the gutter. Click the lightbulb to see available quick actions:

- **Remove trailing whitespace**: Trims the offending line.
- **Insert semicolon**: Adds `;` at the end of a statement that forgot one.
- **Add missing using**: Cross-references the symbol index to find the correct namespace and inserts a `using` directive.

Lightbulbs are yellow when hovered, amber otherwise — a deliberate design choice to remind you that the fix is there but requires conscious effort.

### 5.15 Rename (F2)

Press F2 with the cursor on an identifier to trigger a project-wide rename. The feature:

1. Finds the word under the cursor.
2. Searches all workspace files for occurrences using regex (word-boundary matched).
3. Shows a preview dialog listing every match grouped by file.
4. On confirmation, applies replacements file by file and saves each file.

This is regex-based, not semantic. It will rename every occurrence of that identifier across the entire workspace, including false positives if another scope happens to use the same name. You have been warned. A red "Fix" button next to alarming stats is there for a reason.

### 5.16 Call Hierarchy (Ctrl+Shift+H)

With the cursor on a method name, press `Ctrl+Shift+H` to see who calls whom. The dialog shows:

- **Outgoing calls**: Methods called by the current method, extracted by brace-matching the method body and scanning for `Identifier(` patterns.
- **Incoming callers**: Every workspace file that references the method name.

The result is a tree view with navigation. Double-click any node to jump to the file and line. The analysis is lexical — if two methods share the same name, both will appear as callers. This is a limitation of the regex approach, but it works well enough for most codebases.

### 5.17 Stack Trace Parsing (Ctrl+Shift+T)

Paste or type a stack trace into the editor, select it (or just ensure the cursor is on it), press `Ctrl+Shift+T`, and the editor parses it into clickable frames. The parser handles four formats:

| Format | Example Frame |
|--------|---------------|
| .NET | `at Foo.Bar() in C:\project\file.cs:line 42` |
| JavaScript | `at functionName (file.ts:42:10)` |
| Python | `File "path/to/file.py", line 42, in functionName` |
| Generic | `C:\project\file.cs:42` |

Each frame shows whether the file exists (checkmark) or doesn't (cross). Double-click to navigate. The dialog has a "Copy All" button for sharing — because sometimes you need to forward the evidence.

### 5.18 Hover Documentation

Hovering over an identifier in the editor for 400ms triggers a tooltip overlay showing:

1. **Symbol kind** (class, method, property, etc.)
2. **XML doc summary** — parsed from `///` comments in the source file
3. **Declaration context** — the surrounding type and namespace

The tooltip is a borderless form that auto-positions above the cursor. It dismisses when the cursor moves more than 10 pixels or after 400ms of inactivity — which is just enough time to read, but not enough time to recline.

### 5.19 Signature Help

When you type an opening parenthesis `(`, a signature help overlay appears showing:

1. **Method name and parameter list**
2. **Current parameter** highlighted in bold with its XML `<param>` documentation
3. **Overload list** (if multiple overloads exist)

Navigate parameters by typing commas — the highlight shifts to the next parameter. Dismiss by typing `)`, `;`, or pressing Escape. The form repositions to track your cursor location.

### 5.20 Code Coverage

The editor supports code coverage analysis via the Cobertura XML format (the default output from coverlet). Two entry points:

- **Run > Run Tests with Coverage** (`Ctrl+Alt+R`): Runs `dotnet test --collect "Code Coverage"` and parses the generated `.cobertura.xml` file.
- **Run > Load Coverage File**: Opens an existing `.cobertura.xml` file without re-running tests.

Coverage indicators appear in the gutter: a green bar for covered lines, red for missed. The Coverage Summary dialog shows per-file stats (file name, coverage percentage, covered/total lines, and a checkmark/cross status). The color coding is aggressive: green for >= 80%, yellow for >= 50%, red for anything below. The dialog uses a doubled format to make you feel bad about that 23% file.

### 5.21 Project Dependencies & Impact Analysis

**Dependencies** (`Ctrl+Alt+D` or Panel > Dependencies): Scans workspace `.csproj` files for `ProjectReference` and `PackageReference` entries. Shows a tree dialog with projects, their NuGet packages, and cross-project references. Useful for understanding why your build takes 47 seconds and which packages you're not actually using.

**Impact Analysis** (`Ctrl+Alt+I` or Panel > Impact Analysis): Given the current file, finds all other files in the workspace that reference the same namespace. The result is a "who breaks if I change this" list — perfect for that refactoring you're about to do at 4:55 PM on a Friday. Results include file names, line numbers, and the matching context line.

### 5.22 Run Configurations (Ctrl+F5)

Scans the workspace for `Properties\launchSettings.json` files (from ASP.NET projects) and `.env*` files. Shows a dialog grouping profiles by project, each with:

- **Profile name** and associated environment variables
- **Run button**: Launches `dotnet run --project` with the profile's working directory and environment variables.
- **Stop button**: Kills the running process.
- **Stdout/stderr streaming**: Output is shown in the status label (truncated to 80 characters, because you don't need the full NuGet restore log in a dialog).

If no `launchSettings.json` exists, it falls back to scanning parent directories for solution or project files, so it still shows something useful.

### 5.23 Task List (Ctrl+Alt+T)

Scans all workspace files for comment tags:

| Tag | Severity |
|-----|----------|
| `TODO` | Info |
| `FIXME` | Warning |
| `HACK` | Warning |
| `BUG` | Error |
| `XXX` | Info |
| `NOTE` | Info |
| `OPTIMIZE` | Info |
| `REVIEW` | Info |

Results are merged into the Problems panel alongside lint diagnostics. The scanner runs on a background thread to avoid blocking the UI while it reads every file in your workspace. Tags are matched with word boundaries to avoid false positives from variable names like `BUG_REPORT_URL`.

### 5.24 Test Runner

**Run > Run Tests** (`Ctrl+Alt+F5`): Finds `*Tests.csproj` or `*Test.csproj` in the workspace and runs `dotnet test` with TRX logging on a background thread. The results open in the **Test Results** dialog featuring:

- **Summary header**: Color-coded (green for all passing, red for any failures) with counts.
- **Test list**: Owner-drawn with colored badges — green check for pass, red X for fail, yellow circle for skip — plus duration.
- **Detail panel**: Selecting a test shows its full name, outcome, error message, and stack trace in a monospace detail pane.
- **Stack trace navigation**: Double-click a file:line pattern in the detail pane to open the file at that line.

**Rerun Failed Tests** (`Ctrl+Alt+F6`): Filters to only run tests that failed in the last run. Uses `--filter` with the fully-qualified names of failed tests.

**Run > Run Tests with Coverage** (`Ctrl+Alt+R`): Runs the same test project with `--collect "Code Coverage"` for combined test results and coverage data (see section 5.20).

The test runner uses the TRX (Visual Studio Test Results) XML format for structured parsing rather than trying to parse console output — a deliberate choice for reliability over cleverness. The parser handles the standard `http://microsoft.com/schemas/VisualStudio/TeamTest/2010` namespace and extracts error info and stack traces from the `<ErrorInfo>` elements.

### 6.1 How It Works

The editor uses **LibGit2Sharp** (version 0.31.0) — a .NET wrapper around libgit2, the same library that GitHub, GitLab, and Visual Studio use under the hood. No `git.exe` required; the native binaries are bundled with the application.

### 6.2 Status Bar

When you open a file that's inside a git repository, the status bar shows:
- **Branch name** (e.g., "main", "feature/foo")
- **Dirty indicator** (●) when there are uncommitted changes

### 6.3 Source Control Panel (Ctrl+Alt+G)

Opens a panel in the sidebar with:

- **Branch switcher**: Dropdown listing all local branches.
- **Changes list**: Files organized into Staged, Unstaged, and Untracked sections. Status abbreviations: `[M]` modified, `[A]` added, `[D]` deleted, `[?]` untracked. Single-click to stage/unstage; double-click to open.
- **Commit**: Textbox + button combination. Only enabled when there's both a commit message AND staged changes. The editor won't let you create empty commits.
- **Recent commits**: Last 30 commits with SHA, author, date, and message.
- **Sync buttons**: Fetch, Pull, Push. Pull and Push show result dialogs on success or failure.

All git operations are wrapped in try/catch blocks with user-friendly error messages. If a push fails because you're not authenticated, the editor tells you — it doesn't silently swallow the error or display an incomprehensible stack trace.

---

## 7. Notification System (RSS Feeds)

### 7.1 Feed Sources

The editor polls 8 RSS/Atom feeds by default:

| Source | URL | Interval |
|--------|-----|----------|
| Hacker News | `hnrss.org/frontpage` | 10 min |
| Reddit r/programming | `reddit.com/r/programming/.rss` | 15 min |
| dotnet/runtime releases | `github.com/dotnet/runtime/releases.atom` | 30 min |
| Medium programming | `medium.com/feed/tag/programming` | 15 min |
| Stack Overflow C# | `stackoverflow.com/feeds/tag/c%23` | 15 min |
| BBC News | `feeds.bbci.co.uk/news/rss.xml` | 15 min |
| Reuters | `reutersagency.com/feed/` | 15 min |
| NYT HomePage | `rss.nytimes.com/services/xml/rss/nyt/HomePage.xml` | 15 min |

All feeds are free and require no API keys. If that changes, you can disable or replace them in the settings dialog.

### 7.2 Notification Toast

When new items arrive, a balloon toast appears in the bottom-right corner of the screen. It:
- Fades in smoothly
- Shows the source, title, and summary
- Auto-closes after 12 seconds (pauses on mouse hover)
- Click to open the article in your default browser
- Can be dismissed manually via the `×` button

### 7.3 Notification Center (Ctrl+Shift+N)

The full notification viewer shows all feed items in a customized list view with:
- Color-coded source stripes (Hacker News = orange, BBC = red, etc.)
- Bold titles for unread items
- Single-click to mark as read
- Double-click to open the link
- "Mark All Read" and "Refresh" buttons
- Auto-closes when it loses focus

### 7.4 Feed Settings

You can configure which feeds are enabled, their URLs, labels, and polling intervals. Custom feeds with the `FeedSource.Custom` type can be added for any RSS or Atom URL that doesn't fit one of the predefined sources. Changes are persisted to `feed_config.json` in `%APPDATA%\MyCrownJewelApp\Notifications\`.

---

## 8. Vim Mode

### 8.1 Enabling Vim Mode

Press `Ctrl+Alt+V` or navigate to View > Vim Mode. The status bar will display `-- NORMAL --`, confirming the mode is active. Note that this is *modal editing* — keys behave differently depending on which mode you're in.

### 8.2 Modes

| Mode | Trigger | Description |
|------|---------|-------------|
| **Normal** | Default | Navigation, operators, and commands |
| **Insert** | `i`, `a`, `I`, `A`, `o`, `O`, `s`, `S`, `c`, `C` | Text insertion. Escape returns to Normal. |
| **Visual** | `v` | Character-wise selection |
| **Visual Line** | `V` | Line-wise selection |
| **Command** | `:` | Ex-commands |
| **Search Forward** | `/` | Interactive search |
| **Search Backward** | `?` | Interactive search (upward) |

### 8.3 Normal Mode Motions

| Key | Action |
|-----|--------|
| `h` / Left | Left (with repeat count) |
| `j` / Down | Down (with repeat count) |
| `k` / Up | Up (with repeat count) |
| `l` / Right | Right (with repeat count) |
| `w` | Word forward |
| `b` | Word backward |
| `e` | End of word |
| `0` | Start of line |
| `$` | End of line |
| `^` | First non-blank character |
| `gg` | First line |
| `G` | Last line |
| `%` | Matching bracket |
| `Space` | Right (one character) |

### 8.4 Normal Mode Editing

| Key | Action |
|-----|--------|
| `x` / `X` | Delete / backspace character |
| `dd` | Delete line |
| `D` / `d$` | Delete to end of line |
| `dw` | Delete word |
| `diw` | Delete inner word |
| `d0` / `d^` | Delete to start of line |
| `yy` / `Y` | Yank (copy) line |
| `yw` | Yank word |
| `p` / `P` | Paste after / before cursor |
| `cc` | Change (delete + enter Insert) |
| `C` | Change to end of line |
| `cw` / `ciw` | Change word / inner word |
| `u` | Undo |
| `Ctrl+R` | Redo |
| `>>` / `<<` | Indent / outdent |
| `~` | Toggle case |
| `J` | Join lines |
| `.` | Repeat last action |
| `d{motion}` / `y{motion}` / `c{motion}` | Delete / yank / change with motion |

### 8.5 Visual Mode

Select text using motions, then:
- `d` or `x` — Cut
- `y` — Copy (yank)
- `c` — Change (delete + Insert)
- `.` — Indent selection
- `,` — Outdent selection

### 8.6 Search

- `/` — Search forward
- `?` — Search backward
- `n` — Next match (wraps around)
- `N` — Previous match (wraps around)
- Search is case-insensitive plain text.

### 8.7 Command Mode (:)

| Command | Action |
|---------|--------|
| `:w` | Save |
| `:q` / `:q!` | Close |
| `:wq` / `:x` | Save and close |
| `:w {filename}` | Save as |
| `:e!` | Reload (stub) |
| `:sp` / `:vsp` | Split horizontal / vertical |
| `:{number}` | Go to line |
| `:set expandtab` / `:set noexpandtab` | Toggle spaces mode |
| `:set tabstop=N` | Set tab size |
| `:set smartindent` / `:set nosmartindent` | Toggle auto indent |
| `:set smarttab` / `:set nosmarttab` | Toggle smart tabs |

The Vim engine intercepts keypresses via `ProcessCmdKey` only when Vim mode is active. Regular shortcuts (`Ctrl+S`, `Ctrl+F`, etc.) continue to work as normal.

---

## 9. Find and Replace

### 9.1 Find in Document (Ctrl+F)

The Find dialog supports:
- **Case-sensitive** searching
- **Regex** search (uses `Regex` under the hood — be careful with backtracking)
- **Direction**: Up or Down from the cursor
- **Wrap**: Continue searching from the opposite end when reaching the boundary

### 9.2 Replace (Ctrl+H)

Same features as Find, plus:
- **Replace**: Replaces the current match
- **Replace All**: Replaces all matches in the document

### 9.3 Find in Files / Global Search (`Ctrl+Shift+F`)

Press `Ctrl+Shift+F` or go to Edit > Find in Files to open the **Global Search** dialog — a full-featured workspace search tool:

- **Search text** with Enter to execute
- **Replace text** with "Replace All" for bulk replacements across all matched files
- **Case-sensitive** and **Regex** toggle
- **File type filter** — comma-separated glob patterns (default: `*.cs, *.ts, *.js, *.json, *.md, *.xml, *.yaml, *.html, *.css, *.py, *.go, *.rs, *.tf, *.ps1, *.sh`)
- **Exclude directories** — comma-separated directory names (default: `node_modules, .git, bin, obj, .vs, packages, .terraform`)
- **Async search** — runs on a background thread; results stream in progressively; Stop button cancels
- **Results tree** — grouped by file with match count, expandable to individual lines with line numbers
- **Double-click** or **Enter** on a match to navigate to the file at that line
- **Status bar** — shows `N matches in M files — Xms` duration

The existing "Find in Files" button in the Find dialog (`Ctrl+F`) still works for the basic synchronous search.

---

## 10. External Tools

The External Tools system (Tools > External Tools...) lets you define custom commands that operate on the current file or selection. Each tool has:

- **Title**: Display name in the Tools menu
- **Command**: Path to the executable
- **Arguments**: With variable substitution
- **Initial Directory**: Working directory for the tool
- **Prompt for Arguments**: Shows an input dialog before running
- **Use Shell Execute**: Whether to use shell execute (opens the file with its associated program) or direct execution

### Variable Substitution

| Variable | Replaced With |
|----------|---------------|
| `$(FilePath)` | Full path of the current file |
| `$(FileDir)` | Directory of the current file |
| `$(FileName)` | Filename with extension |
| `$(FileNameNoExt)` | Filename without extension |
| `$(FileExt)` | Extension (including the dot) |
| `$(SelText)` | Currently selected text |
| `$(CurLine)` | Current line number |
| `$(CurCol)` | Current column number |

Up to 9 tools can have keyboard shortcuts (`Ctrl+Alt+Shift+1` through `9`). The menu shows them in order, and you can reorder them using the Move Up/Move Down buttons.

---

## 11. Themes

### 11.1 Available Themes

The editor ships with 15 themes, divided roughly into "dark" and "light" categories:

**Dark themes (9):**
- Dark (default), Catppuccin Frappe, Catppuccin Macchiato, Catppuccin Mocha, Dracula, One Dark Pro, Tokyo Night, Night Owl, Shades of Purple

**Light themes (6):**
- Light, Catppuccin Latte, Atom One Light, GitHub Light, Light Owl, Ayu Light, Bluloco Light

### 11.2 What Gets Themed

Each theme defines 21 color slots covering:

- **UI**: Background, text, menu background, panel background, borders, accents, highlights, disabled elements, hover states, muted text
- **Syntax**: Keywords (one color), strings, comments, numbers, preprocessor directives, types
- **Terminal**: Background, foreground, input area, header

### 11.3 Theme Switching

Themes can be switched from:
- View > Theme menu
- Status bar theme dropdown (right side)
- Cycling: The editor's `ToggleTheme()` method cycles through all themes in order

### 11.4 Theme Persistence

The active theme is saved to `settings.json` in `%APPDATA%\MyCrownJewelApp\TextEditor\` and restored on the next launch.

---

## 12. Settings Persistence

All user-configurable settings are saved to `settings.json` in `%APPDATA%\MyCrownJewelApp\TextEditor\`. The following are persisted:

- Theme, word wrap, gutter visibility, status bar visibility, column guide
- Tab size, insert spaces, auto indent, smart tabs, elastic tabs
- Font name and size
- Syntax highlighting enabled
- Current line highlight mode
- Minimap, terminal, workspace visibility
- Terminal shell path
- External tools list
- Recent files list (stored separately in `recent.txt`)

Settings are saved automatically whenever you toggle a feature, and loaded on startup.

---

## 13. Keyboard Shortcut Reference

### File Operations

| Shortcut | Action |
|----------|--------|
| `Ctrl+T` | New Tab |
| `Ctrl+Shift+N` | New Window |
| `Ctrl+O` | Open |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+Alt+S` | Save All |
| `Ctrl+W` | Close Tab |
| `Ctrl+Shift+W` | Close Window |
| `Ctrl+Alt+W` | Close All |

### Editing

| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo |
| `Ctrl+X` | Cut |
| `Ctrl+C` | Copy |
| `Ctrl+V` | Paste |
| `Delete` | Delete |
| `Ctrl+A` | Select All |
| `F5` | Insert Time/Date |
| `Ctrl+Shift+F` | Font |

### Navigation & Code Analysis

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Find |
| `Ctrl+Shift+F` | Find in Files (Global Search) |
| `F3` | Find Next |
| `Shift+F3` | Find Previous |
| `Ctrl+H` | Replace |
| `Ctrl+G` | Go To Line |
| `F12` | Go to Definition |
| `F2` | Rename (project-wide) — also "Next Bookmark" when bookmarks are active; the button you press last in the menu wins |
| `Ctrl+Shift+H` | Call Hierarchy |
| `Ctrl+Shift+T` | Parse Stack Trace |

### Bookmarks & Folding

| Shortcut | Action |
|----------|--------|
| `Ctrl+F2` | Toggle Bookmark |
| `F2` | Next Bookmark (also Rename; see above) |
| `Shift+F2` | Previous Bookmark |
| `Ctrl+Shift+[` | Toggle Fold |
| `Ctrl+Alt+[` | Toggle All Folds |

### Panels

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+W` | Workspace Panel |
| `Ctrl+Alt+O` | Open Folder |
| `Ctrl+Alt+G` | Source Control Panel |
| `Ctrl+Shift+G` | Source Control Window |
| `Ctrl+Alt+S` | Symbols Panel |
| `Ctrl+Alt+P` | Problems Panel |
| `` Ctrl+` `` | Terminal |
| `Ctrl+Shift+N` | Notification Center |
| `Ctrl+Alt+D` | Dependencies |
| `Ctrl+Alt+I` | Impact Analysis |
| `Ctrl+F5` | Run Configurations |
| `Ctrl+Alt+T` | Task List |

### View

| Shortcut | Action |
|----------|--------|
| `Ctrl+Plus` | Zoom In |
| `Ctrl+Minus` | Zoom Out |
| `Ctrl+0` | Reset Zoom |
| `Ctrl+Alt+V` | Vim Mode |
| `Ctrl+Shift+V` | Split Vertical |
| `Ctrl+Alt+H` | Split Horizontal |

### Testing

| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+F5` | Run Tests |
| `Ctrl+Alt+F6` | Rerun Failed Tests |
| `Ctrl+Alt+R` | Run Tests with Coverage |

### External Tools (user-defined)

| Shortcut | Action |
|----------|--------|
| `Ctrl+Alt+Shift+1` | External Tool #1 |
| `Ctrl+Alt+Shift+2` | External Tool #2 |
| ... | ... |
| `Ctrl+Alt+Shift+9` | External Tool #9 |

### Window

| Shortcut | Action |
|----------|--------|
| `F11` | Fullscreen |
| `F10` | Toggle Menu |
| `Ctrl+Backtick` | Toggle Terminal |

---

## 14. Architecture Notes (for the curious)

- The editor uses **incremental syntax highlighting** via a background worker thread that communicates through a `System.Threading.Channels.Channel<(int, string)>`. Only visible lines are tokenized. The highlighter has a maximum batch size of 500 lines per iteration, which prevents it from blocking the UI thread on large files.
- **Elastic tab stops** are computed on a background thread with caching (`TabMeasurementCache`) and debounced at 250ms.
- **Git operations** use LibGit2Sharp (native binaries bundled). The editor doesn't shell out to `git.exe` — a deliberate choice to avoid dependency on external tools.
- **RSS feed polling** runs on a background `HttpClient` loop within the `NotificationFeedService`. Feeds are fetched in parallel, with the minimum polling interval across enabled sources determining the loop delay.
- **All code analysis is regex/text-based**, not Roslyn-based. This was a deliberate zero-dependency decision — the editor weighs ~48 MB self-contained. Adding `Microsoft.CodeAnalysis.CSharp` would add another ~50 MB and require the .NET 8 SDK on the target machine. The trade-off is acceptable: the analysis is ~80% as accurate for ~0% of the dependency cost.
- **The lint engine** (`LintEngine.cs`) is decoupled from the editor via a debounced event pipeline: text change → 400ms debounce → background `Task.Run` → `IReadOnlyList<Diagnostic>` → UI thread → squiggly underlines + problems panel. Five rules ship by default; adding more requires implementing a single `Func<string, string, List<Diagnostic>>` signature.
- **Squiggly underlines** are drawn in the RichTextBox's `WM_PAINT` handler using a dedicated `DrawSquiggles` method. The squiggle positions are pre-computed on the UI thread from the diagnostic data — no line-by-line scanning during paint.
- **The symbol index** (`SymbolIndexService.cs`) is rebuilt asynchronously after each workspace scan. It feeds the Symbols panel, Go to Definition fallback, Rename, and the "Add missing using" quick action. All consumers read from the same cached symbol list, which is published via an `Action OnIndexUpdated` event.
- **The sidebar panel system** uses a nested `SplitContainer` layout: outer split (workspace left / everything else right), middle split (git + symbol split), inner split (symbols top / problems bottom). Each panel implements `SetTheme(Theme)` for color consistency and fires `CloseRequested` / `*Selected` events back to Form1.
- **Test runner** (`TestResultParser.cs`) runs `dotnet test --logger trx` as a child process with a 5-minute timeout. Results are parsed from the TRX XML format (VSTest namespace) rather than scraping console output — structured data is always preferable to regex parsing of log text. The parser handles `<ErrorInfo>` elements for failure messages and stack traces.
- **Crash resilience**: All non-trivial operations are wrapped in try/catch blocks. The `Program.Main` entry point catches unhandled exceptions and logs them to `crash.log` before displaying a message box.

---

*This manual was prepared with the same level of care that went into the editor itself: thorough, occasionally pedantic, and with a healthy respect for edge cases. If you find an feature that isn't documented here, it's either because it was added after this manual was written, or because I forgot. Both are equally likely.*

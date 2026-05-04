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

The menu strip at the top contains four menus: File, Edit, View, and Tools. (Plus a Help menu with exactly one item, which is about as helpful as you'd expect from a software project.)

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
| Workspace | `Ctrl+Shift+W` | Toggles the file tree sidebar. |
| Open Folder | `Ctrl+Alt+O` | Sets the workspace root directory. |
| Source Control | `Ctrl+Alt+G` | Toggles the Git panel in the sidebar. |
| Terminal | `` Ctrl+` `` | Toggles the integrated terminal panel. |
| Notification Center | `Ctrl+Shift+N` | Opens the RSS feed notification viewer. |
| Notification Settings | | Opens the feed configuration dialog. |
| Theme | | Submenu with 15 built-in themes: Dark (default), Light, four Catppuccin variants (Latte, Frappe, Macchiato, Mocha), Dracula, One Dark Pro, Tokyo Night, Night Owl, Shades of Purple, Atom One Light, GitHub Light, Light Owl, Ayu Light, and Bluloco Light. |

### 2.4 Tools Menu

| Command | Shortcut | What It Does |
|---------|----------|-------------|
| External Tools | `Ctrl+Alt+T` | Opens the External Tools configuration dialog. You can define up to 9 custom commands with variable substitution (`$(FilePath)`, `$(SelText)`, `$(CurLine)`, etc.) and assign them keyboard shortcuts `Ctrl+Alt+Shift+1` through `9`. |

### 2.5 Help Menu

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

---

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

---

## 6. Git Integration

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

### 9.3 Find in Files

Click the "Find in Files" button in the Find dialog to search all files in the workspace. Results appear in a separate dialog with columns for File, Line, and Content. Double-click or press Enter to open the file at the matched line.

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

### Navigation

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Find |
| `F3` | Find Next |
| `Shift+F3` | Find Previous |
| `Ctrl+H` | Replace |
| `Ctrl+G` | Go To Line |
| `F12` | Go to Definition |

### Bookmarks & Folding

| Shortcut | Action |
|----------|--------|
| `Ctrl+F2` | Toggle Bookmark |
| `F2` | Next Bookmark |
| `Shift+F2` | Previous Bookmark |
| `Ctrl+Shift+[` | Toggle Fold |
| `Ctrl+Alt+[` | Toggle All Folds |

### View

| Shortcut | Action |
|----------|--------|
| `Ctrl+Plus` | Zoom In |
| `Ctrl+Minus` | Zoom Out |
| `Ctrl+0` | Reset Zoom |
| `Ctrl+Alt+V` | Vim Mode |
| `Ctrl+Shift+V` | Split Vertical |
| `Ctrl+Alt+H` | Split Horizontal |
| `` Ctrl+` `` | Terminal |
| `Ctrl+Shift+W` | Workspace |
| `Ctrl+Alt+O` | Open Folder |
| `Ctrl+Alt+G` | Source Control |
| `Ctrl+Shift+N` | Notification Center |
| `Ctrl+Alt+T` | External Tools Config |

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
- **Crash resilience**: All non-trivial operations are wrapped in try/catch blocks. The `Program.Main` entry point catches unhandled exceptions and logs them to `crash.log` before displaying a message box.

---

*This manual was prepared with the same level of care that went into the editor itself: thorough, occasionally pedantic, and with a healthy respect for edge cases. If you find an feature that isn't documented here, it's either because it was added after this manual was written, or because I forgot. Both are equally likely.*

# Personal Flip Pad — Cheatsheet

## File Operations

| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `Ctrl+T` | New Tab | `Ctrl+W` | Close Tab |
| `Ctrl+Shift+N` | New Window | `Ctrl+Shift+W` | Close Window |
| `Ctrl+O` | Open | `Ctrl+Alt+W` | Close All |
| `Ctrl+S` | Save | `F11` | Fullscreen |
| `Ctrl+Shift+S` | Save As | `F10` | Toggle Menu |
| `Ctrl+Alt+S` | Save All | | |

## Editing

| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `Ctrl+Z` | Undo | `Delete` | Delete |
| `Ctrl+X` | Cut | `Ctrl+A` | Select All |
| `Ctrl+C` | Copy | `F5` | Insert Time/Date |
| `Ctrl+V` | Paste | `Ctrl+Shift+F` | Font |

## Navigation / Search

| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `Ctrl+F` | Find | `Ctrl+H` | Replace |
| `F3` | Find Next | `Ctrl+G` | Go To Line |
| `Shift+F3` | Find Previous | `F12` | Go to Definition |

## Bookmarks & Folding

| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `Ctrl+F2` | Toggle Bookmark | `Ctrl+Shift+[` | Toggle Fold |
| `F2` | Next Bookmark | `Ctrl+Alt+[` | Toggle All Folds |
| `Shift+F2` | Previous Bookmark | | |

## View Toggles

| Keys | Action |
|------|--------|
| `Ctrl+Plus` / `Ctrl+Minus` | Zoom In / Out |
| `Ctrl+0` | Reset Zoom (100%) |
| `Ctrl+Alt+V` | Vim Mode |
| `Ctrl+Shift+V` | Split Vertical |
| `Ctrl+Alt+H` | Split Horizontal |
| `` Ctrl+` `` | Terminal |
| `Ctrl+Shift+W` | Workspace Panel |
| `Ctrl+Alt+O` | Open Folder |
| `Ctrl+Alt+G` | Source Control (Git) |
| `Ctrl+Shift+N` | Notification Center |
| `Ctrl+Alt+T` | External Tools Config |
| `Ctrl+Alt+Shift+1..9` | External Tool #1..9 |

## View Menu Features

| Feature | Description |
|---------|-------------|
| Zoom | 0.5x–5.0x (0.1 steps); `Ctrl+MouseWheel` |
| Status Bar | Toggle bottom bar |
| Word Wrap | Toggle line wrapping |
| Syntax Highlighting | 12 languages, incremental |
| Current Line Highlight | Off / Number Only / Whole Line |
| Insert Spaces | Tab → spaces |
| Tab Size | 2, 4, 6, 8, 10, 12 |
| Auto Indent | Auto-indent on Enter |
| Smart Tabs | Context-aware tab behavior |
| Elastic Tabs | Dynamic column-aligned tab stops |
| Column Guide | Vertical dotted line at N columns |
| Gutter | Line numbers, bookmarks, fold markers |
| Show Whitespace | Space · Tab → Newline ↵ |
| Minimap | Syntax-colored document overview |
| Themes | 15 built-in (Dark / Light / Catppuccin / etc.) |

## Status Bar (left to right)

| Item | What it shows |
|------|---------------|
| Vim Mode | `-- NORMAL --`, `-- INSERT --`, etc. |
| Cursor | `Ln 42, Col 7` |
| Chars | Total character count |
| Tab Size | Dropdown: current tab width |
| Git Branch | Branch name |
| Dirty | `●` when uncommitted changes |
| Line Pos | `42 / 100` |
| Zoom | `100%` |
| Endings | `Windows (CRLF)` |
| Encoding | `UTF-8` |
| Theme | Dropdown: current theme |
| File Type | `C#`, `Plain Text`, etc. |
| Notifications | `N 3` (unread count; click to open) |

## Vim Mode Quick Reference

### Modes
| Key | Mode |
|-----|------|
| `Esc` / `Ctrl+[` | Normal |
| `i` / `a` / `I` / `A` / `o` / `O` / `s` / `S` | Insert |
| `v` | Visual |
| `V` | Visual Line |
| `:` | Command |
| `/` | Search forward |
| `?` | Search backward |

### Motions
| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `h/j/k/l` | Left/Down/Up/Right | `gg` | First line |
| `w` / `b` | Word forward/backward | `G` | Last line |
| `e` | Word end | `%` | Matching bracket |
| `0` / `$` | Line start/end | `Space` | Right one char |
| `^` | First non-blank | | |

### Editing
| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `x` / `X` | Delete char | `dd` | Delete line |
| `D` / `d$` | Delete to EOL | `yy` / `Y` | Yank line |
| `dw` / `diw` | Delete word | `p` / `P` | Paste after/before |
| `cc` / `C` | Change line/to EOL | `u` / `Ctrl+R` | Undo / Redo |
| `cw` / `ciw` | Change word | `>>` / `<<` | Indent / outdent |
| `~` | Toggle case | `J` | Join lines |
| `.` | Repeat last | `d/y/c{motion}` | Operator + motion |

### Search
| Keys | Action | Keys | Action |
|------|--------|------|--------|
| `/` | Search forward | `n` | Next match |
| `?` | Search backward | `N` | Previous match |

### Command Mode
| Command | Action | Command | Action |
|---------|--------|---------|--------|
| `:w` | Save | `:wq` / `:x` | Save & close |
| `:q` / `:q!` | Close | `:{n}` | Go to line N |
| `:sp` / `:vsp` | Split H/V | `:e!` | Reload |
| `:set expandtab` | Spaces on | `:set smartindent` | Auto-indent on |
| `:set tabstop=N` | Set tab size | `:set smarttab` | Smart tabs on |

## Git Panel (Ctrl+Alt+G)

| Action | How |
|--------|-----|
| Stage file | Single-click in changes list |
| Unstage file | Single-click staged file |
| Stage All | Click "Stage All" button |
| Commit | Type message + click Commit |
| Switch branch | Dropdown at top |
| Fetch / Pull / Push | Bottom buttons |
| Open changed file | Double-click in changes list |

## Notification System

| Action | How |
|--------|-----|
| Open notification center | `Ctrl+Shift+N` or click "N" in status bar |
| Mark all read | Click "Mark All Read" |
| Open notification link | Double-click |
| Dismiss toast | Click `×` |
| Configure feeds | Gear icon in notification center, or View > Notification Settings |

## Themes

| Theme | Type | Theme | Type |
|-------|------|-------|------|
| Dark | Dark | Catppuccin Latte | Light |
| Light | Light | Catppuccin Frappe | Dark |
| One Dark Pro | Dark | Catppuccin Macchiato | Dark |
| Dracula | Dark | Catppuccin Mocha | Dark |
| Tokyo Night | Dark | Atom One Light | Light |
| Night Owl | Dark | GitHub Light | Light |
| Shades of Purple | Dark | Light Owl | Light |
| | | Ayu / Bluloco Light | Light |

## Sidebar Panels

| Panel | Toggle | Location |
|-------|--------|----------|
| Workspace (file tree) | `Ctrl+Shift+W` | Left, top |
| Source Control | `Ctrl+Alt+G` | Left, below Workspace |
| Terminal | `` Ctrl+` `` | Bottom |

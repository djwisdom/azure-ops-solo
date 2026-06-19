# Personal Flip Pad — End-to-End Workflow Guide

**Current state:** v1.0.16 — Functional but rudimentary. Many integrations exist as islands; the bridges between them are still under construction.

*This document walks through the complete lifecycle of working with a project in Pfpad as it exists today. Gaps and rough edges are called out explicitly.*

---

## Table of Contents

1. [Project and Workspace Setup](#1-project-and-workspace-setup)
   - [1.1 Creating a Project (No Solution Support)](#11-creating-a-project-no-solution-support)
   - [1.2 Opening a Local Folder](#12-opening-a-local-folder)
2. [Editing with Assistance](#2-editing-with-assistance)
3. [Building](#3-building)
4. [Running](#4-running)
5. [Debugging](#5-debugging)
6. [Revisioning (Git)](#6-revisioning-git)
   - [6.1 Cloning Remote Repositories](#61-cloning-remote-repositories)
7. [Saving and Persistence](#7-saving-and-persistence)
8. [Full Workflow Walkthrough](#8-full-workflow-walkthrough)
9. [Known Gaps](#9-known-gaps)

---

## 1. Project and Workspace Setup

Pfpad has **no concept of a "solution" (.sln)** and **no "New Project" wizard**. Projects are treated as files in a folder tree. The editor is folder-centric, not solution-centric. All `.sln`/`.csproj` awareness is internal-only (Roslyn `MSBuildWorkspace` silently upgrades to full C# analysis when it finds one nearby).

### 1.1 Creating a Project (No Solution Support)

**There is no UI to create a project or solution.** No menu item, no dialog, no keyboard shortcut. The `dotnet new` CLI is the only path, accessed through the terminal panel.

#### What Exists

| Step | Method | Notes |
|------|--------|-------|
| **Create project scaffold** | Terminal panel (`Ctrl+\``) → `dotnet new <template>` | No UI wrapper. Full list of templates via `dotnet new list` |
| **Create solution file (.sln)** | Terminal → `dotnet new sln` / `dotnet sln add` | Pfpad has zero .sln creation or management |
| **Open created project** | Panel → Open Folder (`Ctrl+Alt+O`) | Select the folder containing the .csproj |

#### Terminal Templates Available

All standard .NET templates work. Common ones:

```bash
dotnet new console -n MyApp           # Console application
dotnet new web -n MyWeb               # ASP.NET Core (empty)
dotnet new webapi -n MyApi             # ASP.NET Core Web API
dotnet new mvc -n MyMvc                # ASP.NET Core MVC
dotnet new classlib -n MyLib           # Class library
dotnet new xunit -n MyTests            # xUnit test project
dotnet new sln                         # Solution file (if needed)
dotnet new editorconfig -n .editorconfig  # Editor config
dotnet new gitignore                   # .gitignore file
```

#### How Pfpad "Discovers" the Project

When you edit a `.cs` file, the Roslyn `RoslynWorkspaceService` silently walks up directories looking for `.csproj` or `.sln`:

```
[file.cs]
   ↓ walk up directories
[*.csproj found?] ─yes→ MSBuildWorkspace (full C# analysis)
   ↓ no
[*.sln found?]    ─yes→ MSBuildWorkspace (via solution)
   ↓ no
[AdhocWorkspace]       (metadata-only, no NuGet resolution)
```

The user never sees this. There's no UI indicator of which project or solution is active, no "startup project" selector, and no way to manually trigger the upgrade.

#### What's Missing

| Feature | Status |
|---------|--------|
| "New Project" wizard / dialog | ❌ Does not exist |
| `dotnet new` menu integration | ❌ Does not exist |
| Solution (.sln) creation UI | ❌ Does not exist |
| Add project to solution UI | ❌ Does not exist |
| Solution explorer / solution tree | ❌ Does not exist |
| Build configuration per project | ❌ Does not exist |
| Startup project selection | ❌ Does not exist |
| Project property pages | ❌ Does not exist |

#### Current Workaround

```
1. Ctrl+`             Open Terminal panel
2. dotnet new console -n FlipCalc
3. Ctrl+Alt+O         Open Folder → select FlipCalc/
4. Write code
5. (Optional) Terminal: dotnet new sln && dotnet sln add FlipCalc.csproj
```

---

### 1.2 Opening a Local Folder

This is the **primary entry point** for all Pfpad workflows. Every feature (symbol index, lint, git, test runner, debugger) activates once a folder is opened.

#### How to Open a Folder

| Method | UX | Result |
|--------|----|--------|
| **Panel → Open Folder** (`Ctrl+Alt+O`) | Standard WinForms `FolderBrowserDialog` with title "Select a folder to open as workspace" | Sets workspace root, populates tree, triggers symbol index rebuild |
| **Drag folder** onto Workspace panel | Drag from Explorer, drop on tree | Replaces current workspace root |
| **Drag folder** onto editor title bar | Drag from Explorer, drop on title bar | Opens folder as workspace (same as above) |
| **Launch with command-line args** | `Pfpad.exe "C:\path\to\folder"` | ✅ — directory args set workspace root; file args open as tabs |

#### What Happens When You Open a Folder

```
Ctrl+Alt+O
    │
    ▼
FolderBrowserDialog ("Select a folder to open as workspace")
    │
    ├─ Dialog opens at last-used location (persisted in settings.json)
    │
    ▼ User selects folder and clicks OK
    │
    ├─ 1. Cancel any in-flight workspace scan (WorkspacePanel.CancelScan)
    ├─ 2. Cancel any in-flight symbol index rebuild (SymbolIndexService)
    ├─ 3. Store path in _workspaceRoot
    ├─ 4. Call WorkspacePanel.SetRoot(path)
    │      │
    │      ├─ Update root label text → folder name (e.g., "FlipCalc")
    │      ├─ Update tooltip → full path
    │      ├─ Clear existing TreeView
    │      ├─ Create root TreeNode with Tag = path
    │      ├─ Start async PopulateDirectoryAsync:
    │      │     ├─ Recursively walk ALL subdirectories
    │      │     ├─ Skip: .git, node_modules, bin, obj, .vs, packages, .svn, .hg
    │      │     ├─ Skip: any directory starting with "."
    │      │     ├─ Filter files: only ~65 known text extensions
    │      │     ├─ Report progress every 25 directories
    │      │     └─ Build tree on UI thread via ApplyNodes
    │      └─ Start 5-second auto-refresh timer
    │
    ├─ 5. Start background SymbolIndex.RebuildIndex(path)
    │      (for Go to Definition, Symbols panel, Rename)
    │
    ├─ 6. If workspace panel hidden → auto-show it (ToggleWorkspace)
    │
    └─ 7. Save workspace path to settings.json
```

#### What the Workspace Panel Shows

The workspace panel (`Ctrl+Shift+W`) is a `TreeView` with:

- **Header**: Root folder name (bold, clickable label), refresh button (↻), collapse/expand button (▼), close (×)
- **Tree**: Alphabetically sorted, directories first then files
- **Lazy-loading**: Subdirectories populate on expand (placeholder "Loading..." shown until async scan completes)
- **File filter**: Only ~65 text extensions shown (`.cs`, `.fs`, `.vb`, `.js`, `.ts`, `.py`, `.go`, `.rs`, `.csproj`, `.sln`, `.json`, `.xml`, `.yaml`, `.md`, `.ps1`, `.sh`, etc.)
- **Auto-refresh**: Every 5 seconds when root node is expanded
- **Hidden items**: `.git`, `node_modules`, `bin`, `obj`, `.vs`, `packages`, `.svn`, `.hg`, all dot-prefixed dirs

#### Right-Click Context Menu

| Action | Behavior |
|--------|----------|
| **Open** | Opens file in editor tab; expands/collapses directory |
| **Open Containing Folder** | Opens Windows Explorer to the file's directory |
| **New File** | `SimpleInputDialog` → prompts for filename → creates empty file → opens it → refreshes tree |
| **New Folder** | `SimpleInputDialog` → prompts for folder name → creates directory → refreshes tree |
| **Copy Path** | Copies full path to clipboard |

#### Double-Click / Enter

- **File**: Opens in editor tab (fires `FileOpenRequested` event → Form1 creates a new `Document` and opens a tab)
- **Directory**: Toggles expand/collapse

#### Drag-Drop

- **File(s)**: Opens each in a new editor tab
- **Folder**: Replaces the current workspace root entirely

#### What Auto-Activates After Opening a Folder

| Service | What It Does | When |
|---------|-------------|------|
| **Symbol Index** | Background scan for class/method/property declarations | Immediately after folder open |
| **Git Service** | Auto-detects `.git` in parent hierarchy when first file is loaded | On first file open |
| **Roslyn Workspace** | Scans for `.csproj`/`.sln` to upgrade from Adhoc to MSBuild | On first `.cs` file open |
| **Lint Engine** | Monitors text changes, runs 5 rules on 400ms debounce | On text edit |
| **Minimap** | Rebuilds minimap on background thread | On document load |

#### When Workspace Panel Opens Without a Folder

If no folder has been opened (fresh launch with no command-line args), the workspace panel shows an empty header with just the controls. Opening a file via `Ctrl+O` opens the file in a tab but does **not** set the workspace root — the workspace root must be set explicitly via `Ctrl+Alt+O`. This means features like symbol index, test runner, and Go to Definition will not work fully until a folder is opened.

#### What's Missing

| Feature | Impact |
|---------|--------|
| ~~**No folder-from-command-line** — `Pfpad.exe .` does nothing folder-wise~~ | ✅ Fixed in 1.0.18 — directory args set workspace root |
| **No "Open Folder" from File menu** — it's under the Panel menu | Non-obvious location for new users |
| **No recent folders list** — only recent files | Must navigate to project folder each time |
| **No solution-filtered view** — tree shows ALL text files, not just project files | Cluttered for large repos with mixed file types |
| **No multi-root workspaces** — only one workspace root at a time | Must switch roots to work on multiple projects |
| **No "Add folder to workspace"** — drag-drop replaces, not adds | Can't composite multiple folders |

---

## 2. Editing with Assistance

Once files are open, the editor provides substantial assistance:

### What's Available

| Feature | How | Shortcut |
|---------|-----|----------|
| **Syntax highlighting** | Automatic based on file extension — 12 languages supported | — |
| **Code folding** | Brace-pair and `#region` folding, dotted scope lines | `Ctrl+Shift+[` / `Ctrl+Alt+[` |
| **Brace matching** | Auto-highlights matching `{}[]()` | — |
| **Go to Definition** | Two-tier: ctags (if available) → regex fallback | `F12` |
| **Rename** | Project-wide regex-based rename with preview | `F2` |
| **Call Hierarchy** | Incoming + outgoing callers via lexical analysis | `Ctrl+Shift+H` |
| **Hover documentation** | Tooltip with XML doc summary (400ms delay) | — |
| **Signature Help** | Parameter tooltip on `(` with overloads | — |
| **Lint diagnostics** | 5 rules (trailing whitespace, line length, magic numbers, missing semicolons, naming) | — |
| **Squiggly underlines** | Red/yellow/blue zigzag for errors/warnings/info | — |
| **Quick actions** | Gutter lightbulbs: remove trailing whitespace, insert semicolon, add missing using | — |
| **Symbol index** | Background-indexed classes/methods/properties, displayed in Symbols panel | `Ctrl+Alt+S` |
| **Problems panel** | Unified view of lint warnings + TODO/FIXME/BUG tags | `Ctrl+Alt+P` |
| **Task list** | Scans for 8 comment tags (TODO, FIXME, HACK, etc.) | `Ctrl+Alt+T` |
| **Vim mode** | Full modal editing with motions, operators, ex-commands | `Ctrl+Alt+V` |
| **Find in Files** | Async workspace-wide search with filters and Replace All | `Ctrl+Shift+F` |
| **Impact Analysis** | Finds files referencing the same namespace | `Ctrl+Alt+I` |
| **Dependencies** | Reads `.csproj` for project + NuGet references (read-only) | `Ctrl+Alt+D` |

### What's Limited

- **Go to Definition** is regex-based (not semantic). Works ~80% of the time. For C#, a Roslyn-powered fallback exists but is only used for hover, signature help, and a separate "Roslyn" toggle.
- **Rename** replaces every matching identifier in the workspace — no scope awareness. Preview before applying is essential.
- **Lint rules** are text-only — no type checking, no symbol resolution, no cross-file analysis.
- **Quick actions** are limited to three fix types.

---

## 3. Building

### What Exists

| Scenario | How | Result |
|----------|-----|--------|
| **Ad-hoc build** | Terminal: `dotnet build` | Full output, errors visible |
| **Pre-debug build** | Press `F5` when no output DLL exists | Synchronous `Process.Start` with 30s timeout — **no output shown** |

### What Exists But Is Rudimentary

The `RunDotnetBuild()` method called during debug startup is:
- **Synchronous** — blocks the UI thread
- **Silent** — output is not captured or displayed
- **Blind** — success/failure is determined only by exit code
- **Unconfigurable** — always Debug, always current TFM

### What's Missing

- **No Build menu** — no `Ctrl+Shift+B`, no "Build Solution"
- **No build output panel** — can't see compiler warnings or errors in the Problems panel
- **No build configuration selector** — Debug/Release doesn't exist as a UI concept
- **No error list** — compilation failures from `dotnet build` are not parsed or displayed
- **No auto-build on save**
- **No project build order** — single-project implicit only

### Current Workaround

```
Terminal panel:
  > dotnet build
  > dotnet build -c Release
```

The Terminal panel is your build output window. It works, but it's manual.

---

## 4. Running

### What Exists

| Method | Shortcut | Behavior | Status |
|--------|----------|----------|--------|
| **Run Without Debug** | `Ctrl+F5` | Launches `dotnet "<dll>"` in a **separate OS window** | ✅ Works |
| **Run Configurations** | `Ctrl+F5` (opens dialog) | Parses `launchSettings.json` + `.env`, runs `dotnet run` | ✅ Works |
| **Terminal run** | Manual `dotnet run` in terminal | Full control, output visible in terminal | ✅ Works |

### What's Missing

- **No "Run in Terminal"** — run output goes to a separate window or a small status label, not into the integrated terminal panel
- **No Run/Stop toolbar buttons**
- **No restart button** (for Run Without Debug)
- **No multi-project run** — no way to set startup project
- **Run Configurations dialog** truncates output to 80 characters in a status label — useful for verification, not for reading actual output

### Current Best Practice

For serious work, use the Terminal panel (`Ctrl+\``) and run `dotnet run` or `dotnet watch` directly. The Run Configurations dialog is useful for quick smoke-testing with environment variables from `launchSettings.json`.

---

## 5. Debugging

This is the most complete integrated workflow. The editor ships with a bundled `netcoredbg.exe` and full DAP implementation.

### What Exists

| Phase | Action | How | Shortcut |
|-------|--------|-----|----------|
| **Setup** | Set breakpoints | Click gutter (left of line numbers) | `F9` |
| **Launch** | Start Debugging | Auto-detects project, builds if needed, launches netcoredbg | `F5` |
| **Control** | Continue / Step Over / Step Into / Step Out | Standard debug controls | `F5` / `F10` / `F11` / `Shift+F11` |
| **Inspect** | Call Stack panel | Auto-opens on breakpoint hit, double-click navigates | — |
| **Inspect** | Variables + Watch panel | Tree view with locals/autos/expressions | — |
| **Inspect** | Watch expressions | Type in watch input box, press Enter | — |
| **Modify** | Enable/disable/remove breakpoints | Menu items under Run → Debug | — |
| **Stop** | Stop Debugging | Kills netcoredbg | `Shift+F5` |
| **Restart** | Restart | Respawns the debug session | `Ctrl+Shift+F5` |

### Breakpoint Features

- Conditional breakpoints (expression-based)
- Hit-count breakpoints
- Logpoints (tracepoints that log to output without breaking)
- Persistence across sessions (JSON in `%APPDATA%`)
- Gutter rendering: red dot (set), gold dot (executing)

### What's Missing

- **No debug toolbar** — all debug actions are keyboard-driven or in menus
- **No attach to process**
- **No threads panel** — single-thread view only
- **No hover-value tooltips during debugging** — must use Variables panel
- **No Edit and Continue** — deferred, advanced feature
- **No debug output panel** — netcoredbg stderr is not captured in the UI
- **No disassembly view**
- **No exception configuration** — no way to break on first-chance exceptions

---

## 6. Revisioning (Git)

### What Exists

| Operation | Where | How |
|-----------|-------|-----|
| **View status** | Source Control Panel (`Ctrl+Alt+G`) or Source Control Window (`Ctrl+Shift+G`) | Staged, Unstaged, Untracked sections |
| **Stage files** | Single-click file in panel, or "Stage All" button | — |
| **Unstage files** | Single-click staged file | — |
| **Commit** | Type message → Commit button | LibGit2Sharp, no `git.exe` needed |
| **Push** | Push button | Shows result dialog |
| **Pull** | Pull button | Shows result dialog |
| **Fetch** | Fetch button | — |
| **Switch branch** | Branch dropdown | Lists local branches |
| **View recent commits** | Scrollable list in panel/window | Last 30 commits with SHA, author, date |
| **Status bar indicator** | Shows branch name + `●` if dirty | — |
| **Open file from status** | Double-click file in changes list | Opens in editor tab |

### What's Missing

- **No diff view** — can't see what changed before staging
- **No blame/annotate**
- **No stash management** — `stash@{0}` exists in the repo but no UI for it
- **No merge conflict resolution UI**
- **No rebase or cherry-pick**
- **No remote branch management** — fetch fetches, but no "new branch from remote" flow
- **No commit graph visualization**
- **No amend or interactive rebase**

### Current Best Practice

For simple workflows (stage → commit → push), the Source Control panel is sufficient. For anything beyond that (diffs, conflict resolution, branching strategy), drop to the terminal.

---

### 6.1 Cloning Remote Repositories

**Pfpad has a Clone Repository dialog** (`Ctrl+Shift+C`) for GitHub, Azure DevOps, GitLab, Bitbucket, OneDev, Codeberg, Radicle, Gitea, Google Cloud Source Repositories, AWS CodeCommit, Launchpad, and any other git hosting platform.

The `CloneRepositoryDialog.cs` class provides a simple UI: enter the remote URL, pick a local path, and click Clone. It runs `git clone` in a background process with output streaming. On success, it auto-opens the cloned folder as the workspace root.

For platforms where system git credential helpers are configured (GCM, SSH agent), authentication is handled transparently. For platforms that require manual PAT or SSH setup, credentials must be configured externally before using the dialog.

The `GitService.cs` class (backed by LibGit2Sharp) has full support for local git operations via its `Clone()` method:

#### Current Workflow

All twelve platforms follow the exact same manual process:

```
┌──────────────────────────────────────────────────────────────────┐
│  EXTERNAL (outside Pfpad)                                        │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Option A: CLI (any platform)                                    │
│    git clone https://github.com/user/repo.git                    │
│    git clone git@ssh.dev.azure.com:v3/org/project/repo           │
│    git clone https://gitlab.com/user/repo.git                    │
│    git clone https://bitbucket.org/user/repo.git                 │
│    git clone https://codeberg.org/user/repo.git                  │
│    git clone https://gitea.com/user/repo.git                     │
│    git clone https://source.developers.google.com/p/project/r/repo │
│    git clone https://git-codecommit.us-east-1.amazonaws.com/v1/  │
│    git clone https://git.launchpad.net/repo                       │
│    (etc.)                                                        │
│                                                                   │
│  Option B: GitHub Desktop / Git GUI / IDE                        │
│    File → Clone Repository → enter URL → pick folder             │
│                                                                   │
│  Option C: Browser                                                │
│    Navigate to repo → Code → Download ZIP → extract manually     │
│    (loses git history, but works for read-only)                  │
│                                                                   │
├──────────────────────────────────────────────────────────────────┤
│  INSIDE Pfpad                                                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. Ctrl+Alt+O      Open Folder → select the cloned directory    │
│  2. GitService      Auto-detects .git in parent hierarchy        │
│  3. Status bar      Shows branch name + dirty indicator          │
│  4. Ctrl+Alt+G      Source Control panel with full Git ops       │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
```

#### Authentication Per Platform

Since Pfpad passes **no credentials** to LibGit2Sharp for any remote operation (fetch/pull/push), authentication depends entirely on the system's existing git credential configuration:

| Platform | Typical Auth Method | Works with Pfpad? | Notes |
|----------|-------------------|-------------------|-------|
| **GitHub** | PAT, SSH key, GCM (Git Credential Manager) | ✅ If cached in GCM | Newer tokenless auth may fail |
| **Azure DevOps** | PAT, SSH key, Microsoft Entra (OAuth via GCM) | ✅ If cached in GCM | DevOps "clone" button gives you a URL |
| **GitLab** | PAT, SSH key | ✅ If SSH agent or cached | Self-hosted instances work same way |
| **Bitbucket** | PAT, SSH key, App password | ✅ If SSH agent or cached | — |
| **Gitea** | PAT, SSH key, HTTP password | ✅ If cached or SSH | Self-hosted, URL varies |
| **Codeberg** | SSH key, HTTP password | ✅ If cached or SSH | Based on Forgejo/Gitea |
| **OneDev** | SSH key, HTTP password | ✅ If cached or SSH | Self-hosted CI/CD platform |
| **Radicle** | SSH key (p2p, no central server) | ⚠️ Requires `rad clone` CLI | Not a standard git remote; Radicle uses its own CLI |
| **Google Cloud Source Repos** | `gcloud` ADC, SSH key | ✅ If `gcloud` credential helper configured | Requires `git-credential-gcloud` |
| **AWS CodeCommit** | `git-remote-codecommit` helper, SSH key | ✅ If credential helper configured | Helper translates `GRC://` URLs |
| **Launchpad (Canonical)** | SSH key (Launchpad.net account) | ✅ If SSH agent configured | Ubuntu's hosting platform; mostly Bazaar historically, git supported |

#### What Pfpad Does With the Cloned Repo

Once a repo is cloned externally and opened in Pfpad:

| Operation | Supported | Auth Dependency |
|-----------|-----------|-----------------|
| View status (staged/unstaged/untracked) | ✅ Yes | None (local only) |
| Stage / Unstage files | ✅ Yes | None |
| Commit | ✅ Yes | None |
| Switch branches | ✅ Yes | None |
| View commit log (last 30) | ✅ Yes | None |
| Stash / Pop | ✅ Yes | None |
| Resolve merge conflicts (ours/theirs) | ✅ Yes | None |
| Fetch from remote | ✅ Yes | System credential helpers |
| Pull from remote | ✅ Yes | System credential helpers |
| Push to remote | ✅ Yes | System credential helpers |

#### What's Missing

| Gap | Impact |
|-----|--------|
| ~~**No Clone dialog** — no way to enter a remote URL, pick a local path, and clone from within Pfpad~~ | ✅ Fixed in 1.0.18 — `Ctrl+Shift+C` opens CloneRepositoryDialog, auto-opens folder |
| **No credential management** — no username/password prompt, no PAT input, no SSH key config, no token storage | Remote ops silently fail if system credentials aren't cached |
| **No remote management UI** — no way to add/change/remove remotes | Must use `git remote` CLI |
| **No hosted-provider integration** — no OAuth flow for GitHub/Azure DevOps/GitLab/Bitbucket | No "Sign in with GitHub" flow |
| **No SSH key generation or management** | Must configure SSH keys externally |
| **No `git-credential` helper configuration** | Relies on existing system setup |

## 7. Saving and Persistence

### File Saving

| Action | Shortcut | Behavior |
|--------|----------|----------|
| Save | `Ctrl+S` | Writes to disk if path exists; falls to Save As if untitled |
| Save As | `Ctrl+Shift+S` | Opens themed SaveFileDialog |
| Save All | `Ctrl+Alt+S` | Saves active file only (limitation — doesn't iterate all tabs) |
| Close Tab | `Ctrl+W` | Prompts if modified (`*` in tab title) |
| Close All | `Ctrl+Alt+W` | Prompts for each modified file |
| Exit | Menu → Exit | Prompts for all unsaved changes |

**Dirty tracking:** SHA-256 hash of content stored at last save. On modification, hash is recomputed and compared. The `*` prefix appears on the tab title when dirty.

### Settings Persistence

| File | Location | Contents |
|------|----------|----------|
| `settings.json` | `%APPDATA%\MyCrownJewelApp\TextEditor\` | Theme, word wrap, gutter, tabs, font, minimization, terminal, workspace, panel visibility, external tools, all toggles |
| `recent.txt` | Same directory | Last 10 files (persisted, clearable) |
| Breakpoints JSON | `%APPDATA%\MyCrownJewelApp\TextEditor\` | Breakpoint positions, conditions, hit counts |
| `feed_config.json` | `%APPDATA%\MyCrownJewelApp\Notifications\` | RSS feed URLs, intervals, enabled state |
| `crash.log` | `%LOCALAPPDATA%\Personal Flip Pad\` | Startup/fatal crash stack traces |

Settings are saved **automatically on toggle** — no manual "save settings" step required.

---

## 8. Full Workflow Walkthrough

Here is the complete end-to-end workflow as it exists today, from blank screen to committed, debugged application. Two entry paths exist depending on whether you're starting fresh or joining an existing project:

### Path B: Clone Existing Repository

```
┌─────────────────────────────────────────────────────────┐
│  PHASE 0: CLONE (VIA Pfpad)                             │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Ctrl+Shift+C    Open Clone Repository dialog        │
│  2. Enter remote URL (GitHub, Azure DevOps, etc.)       │
│  3. Browse or type local path                           │
│  4. Click Clone → background git clone with progress    │
│  5. On success: auto-opens folder as workspace          │
│  6. GitService      Auto-detects .git, shows branch     │
│  7. Ctrl+Alt+G      Source Control panel ready          │
│                                                         │
│  (Optional: still works to clone externally via         │
│   terminal and Ctrl+Alt+O to open)                     │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  CONTINUE TO PHASE 2: EDIT (same as Path A)            │
└─────────────────────────────────────────────────────────┘
```

### Path A: New Project From Scratch

```
┌─────────────────────────────────────────────────────────┐
│  PHASE 1: CREATE                                       │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Ctrl+`          Open Terminal                       │
│  2. dotnet new console -n FlipCalc                      │
│  3. Ctrl+Alt+O      Open Folder → select FlipCalc/      │
│  4. Ctrl+Shift+W    Workspace panel → see files         │
│  5. Right-click WS → New File → Calculator.cs           │
│  6. Write code (numeric up to ~250KB per file)          │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 2: EDIT                                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  • Syntax highlighting is automatic (.cs → C#)          │
│  • F12 to navigate to definitions                       │
│  • Hover for XML doc tooltips                           │
│  • Type ( → Signature Help shows parameters             │
│  • Ctrl+Shift+F to find across files                    │
│  • Ctrl+Alt+P to see lint warnings                      │
│  • Ctrl+Alt+S to browse all symbols in workspace        │
│  • Gutter lightbulbs for quick fixes                    │
│  • Ctrl+S to save frequently                           │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 3: BUILD                                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Ctrl+` → dotnet build                                  │
│  (read output in terminal for errors/warnings)          │
│                                                         │
│  NOTE: No Ctrl+Shift+B, no error list panel.            │
│  Build output lives entirely in the terminal.           │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 4: RUN                                           │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Option A: Ctrl+F5        Run Without Debug             │
│            (opens separate OS window)                   │
│                                                         │
│  Option B: Ctrl+F5 →      Run Configurations dialog     │
│            (reads launchSettings.json)                  │
│                                                         │
│  Option C: Ctrl+` →       dotnet run (in terminal)      │
│            (best for real work)                         │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 5: DEBUG                                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Click gutter or F9   Set breakpoints                │
│  2. F5                   Start Debugging                │
│     (auto-builds if DLL missing)                        │
│  3. F10 / F11            Step Over / Step Into          │
│  4. Auto-opens:          Call Stack + Variables panels  │
│  5. Shift+F5             Stop Debugging                 │
│  6. Ctrl+Shift+F5        Restart Debugging              │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 6: TEST                                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Ctrl+Alt+F5          Run Tests                      │
│     (auto-finds *Tests.csproj in workspace)             │
│  2. Test Results dialog auto-opens with pass/fail/skip  │
│  3. Ctrl+Alt+F6          Rerun failed tests only        │
│  4. Ctrl+Alt+R           Run Tests with Coverage        │
│                                                         │
├─────────────────────────────────────────────────────────┤
│  PHASE 7: REVISION (Git)                                │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Ctrl+Alt+G    Open Source Control panel             │
│  2. Review changes (staged/unstaged/untracked)          │
│  3. Stage files (single-click or "Stage All")           │
│  4. Write commit message → Commit                       │
│  5. Push button    (or Pull/Fetch)                      │
│                                                         │
│  NOTE: No diff view. No blame. No stash UI.             │
│  For anything beyond basic commit/push, use terminal.   │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 9. Known Gaps

These are the most impactful workflow gaps, extracted from `SOLUTION_IDE_ASSESSMENT.md` and confirmed by code analysis:

| Gap | Impact | Workaround |
|-----|--------|------------|
| **No "New Project" wizard** | Must use terminal to scaffold projects | `dotnet new` in terminal panel |
| **No solution (.sln) creation/management** | No multi-project orchestration, no build order, no solution explorer | `dotnet new sln && dotnet sln add` in terminal |
| ~~**No Clone Repository**~~ | ✅ Fixed 1.0.18 — `Ctrl+Shift+C` opens clone dialog, auto-opens folder | `CloneRepositoryDialog.cs` |
| **No credential management** | Remote fetch/pull/push silently fail if system git credentials aren't cached | Configure `git-credential-manager` or SSH agent externally |
| ~~**No workspace root from CLI args**~~ | ✅ Fixed 1.0.18 — `Pfpad.exe .` sets workspace root | Directory detection in `Form1.cs` constructor |
| **No recent folders list** | Must navigate to project folder each time | Bookmark in Windows Explorer |
| **No multi-root workspace** | Only one folder open at a time | Switch roots manually |
| **Open Folder is under Panel menu (not File)** | Non-obvious location for new users | Memorize `Ctrl+Alt+O` |
| **No Build menu / Ctrl+Shift+B** | No keyboard-driven build; no build output panel | Terminal: `dotnet build` |
| **No error list** | Build errors not parsed or displayed in Problems panel | Read terminal output manually |
| **No build configuration** | Always Debug; can't switch to Release from UI | Terminal: `dotnet build -c Release` |
| **No Run in Terminal** | Run output goes to separate window or tiny status label | Terminal: `dotnet run` |
| **No debug toolbar** | All debug actions are keyboard or menu-only | Memorize F5/F10/F11/Shift+F5 |
| **No attach to process** | Can only debug projects launched by netcoredbg | N/A |
| **No diff view** | Can't see line-by-line changes in Git panel | `git diff` in terminal |
| **No stash UI** | Can't manage stashes from Git panel | `git stash` in terminal |
| **No solution explorer** | Workspace panel is a flat file tree; no solution awareness | Manual project management |
| **No NuGet package manager** | No GUI for adding/removing/updating packages | `dotnet add package` in terminal |
| **Go to Definition** | Regex-based, not semantic (~80% accuracy for non-C#) | Roslyn toggle for C# (limited) |
| **Rename** | Workspace-wide regex replace (no scope awareness) | Review preview carefully |

---

*This document describes the workflow as of v1.0.46. Remaining gaps are documented in `SOLUTION_IDE_ASSESSMENT.md` and `FEATURE_TRACKER.md` and are being iterated over incrementally.*

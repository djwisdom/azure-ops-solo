# Developer Workflow Analysis — Personal Flip Pad (Pfpad)

**Target:** v1.0.18 — WinForms C# code editor, .NET 8.0-windows  
**Role:** Solo developer using Pfpad as primary editing environment  
**Date:** 2026-05-08

---

## Overview

Pfpad is a **folder-centric** editor. Unlike Visual Studio or JetBrains Rider (solution-centric) or VS Code (workspace-centric with solution awareness), Pfpad treats the filesystem as its primary organizational unit. Every workflow passes through the workspace panel (`Ctrl+Alt+O` → select folder).

This document analyzes 5 fundamental developer entry points:

| # | Workflow | Pfpad Maturity |
|---|----------|---------------|
| A | Clone a repository | ❌ External only (terminal/GUI required) |
| B | Open a project or solution | 🟡 Partial (no solution explorer, Roslyn auto-discovers) |
| C | Open a local folder | ✅ Primary path (works well) |
| D | Create a new project | 🟡 Terminal-only, no UI wizard |
| E | Continue without code | ❌ No session restore, no welcome page |

---

## Table of Contents

1. [Workflow A: Clone a Repository](#a-clone-a-repository)
2. [Workflow B: Open a Project or Solution](#b-open-a-project-or-solution)
3. [Workflow C: Open a Local Folder](#c-open-a-local-folder)
4. [Workflow D: Create a New Project](#d-create-a-new-project)
5. [Workflow E: Continue Without Code](#e-continue-without-code)
6. [Implementation Priority Matrix](#6-implementation-priority-matrix)

---

## A) Clone a Repository

### Current Details

Pfpad has **no clone dialog**. The user must clone externally:

```
EXTERNAL (required today):
  ┌─ Terminal:    git clone <url> <path>
  ├─ GitHub Desktop: File → Clone Repository
  ├─ VS Code:     Ctrl+Shift+P → "Git: Clone"
  └─ Browser:     Download ZIP (loses history)

THEN INSIDE Pfpad:
  ┌─ Ctrl+Alt+O      Open Folder → select cloned directory
  ├─ GitService       Auto-detects .git, shows branch in status bar
  └─ Ctrl+Alt+G       Source Control panel with stage/commit/push/pull
```

Once the folder is opened:
- **LibGit2Sharp** (v0.31.0) handles local git operations: status, stage, commit, log, branch switching, stash, merge-conflict resolution
- **Remote operations** (fetch/pull/push) rely on system credential helpers (GCM, SSH agent, PAT caching)
- **No credential management UI** — no username/password prompt, no PAT input, no SSH config
- **No remote management** — add/change/remove remotes requires terminal `git remote`
- **No hosted-provider OAuth** — no "Sign in with GitHub/Azure DevOps/GitLab"

### Authentication Matrix

| Platform | Auth Method | Works with Pfpad Remotes? |
|----------|-------------|---------------------------|
| GitHub | PAT, SSH, GCM | ✅ If cached |
| Azure DevOps | PAT, SSH, Entra OAuth via GCM | ✅ If cached |
| GitLab | PAT, SSH | ✅ If SSH agent or cached |
| Bitbucket | PAT, SSH, App password | ✅ If SSH agent or cached |
| Gitea/Codeberg | SSH, HTTP password | ✅ If cached |

### Underlying Code

| File | Role |
|------|------|
| `GitService.cs` | Git operations via LibGit2Sharp (status, diff, branch, log) |
| `GitPanel.cs` | Source Control panel UI (file list, stage buttons, commit box, push/pull) |
| `GitForm.cs` | Legacy/dialog-based git interactions |
| `Form1.cs:6037-6064` | Status bar display (branch, dirty indicator via `RefreshGitRepo()` / `UpdateGitStatusBar()`) |

### Gaps

1. **No clone dialog** — must leave the editor to clone. The #1 missing Git operation.
2. **No progress indication** — clone can take minutes; no progress bar, no cancellation.
3. **No credential capture** — if system credentials aren't cached, remote ops fail silently.
4. **No SSH key management** — must configure SSH externally.
5. **No hosted-provider integration** — no OAuth flow for any platform.

### Justification

A clone dialog is **medium priority** for a solo developer because:
- The solo dev typically clones repos infrequently (once per project, not per session)
- Terminal `git clone` is reliable and well-understood
- The external clone → `Ctrl+Alt+O` flow adds ~15 seconds of friction, once per repo
- Credential management and hosted-provider OAuth are significant implementation efforts

### Recommendation

**Short-term (1 day):** Add a simple "Clone Repository" dialog:
- URL textbox + local path picker + "Clone" button
- Launch `git clone <url> <path>` as a background process (reuse `TestResultParser` async process pattern)
- Stream output to a progress display
- On success, auto-open the cloned folder as workspace
- Keep existing credential model (rely on system helpers)

This avoids LibGit2Sharp's unreliable remote transport and reuses the battle-tested `git.exe` for cloning only, while LibGit2Sharp handles local operations.

**Medium-term (deferred):** Remote management UI, credential caching within Pfpad, hosted-provider OAuth. These are complex and low-frequency needs for a solo dev.

### Implementation Plan

```
Phase 1 — Clone Dialog (1 day)
├── New file: CloneRepositoryDialog.cs
│   ├── URL textbox (with paste detection / URL validation)
│   ├── Local path picker (FolderBrowserDialog, default: Documents/)
│   ├── Clone button → spawns git clone process
│   ├── RichTextBox for stdout/stderr streaming
│   ├── Cancel button → kill process
│   ├── On success → close dialog → auto-open folder
│   └── Error handling: invalid URL, auth failure, disk full, network timeout
│
├── Modify: Form1.cs
│   └── File menu → "Clone Repository..." item
│   └── Keyboard shortcut (Ctrl+Shift+C or similar)
│
└── Modify: Form1.Designer.cs
    └── Menu wire-up

Phase 2 — Remote management (deferred, 2 days)
├── New file: RemoteManagementDialog.cs
│   ├── List remotes (name, URL, fetch/push URL)
│   ├── Add / Edit / Remove buttons
│   └── Credential helper config display
│
└── Modify: GitPanel.cs — "Remotes" button in toolbar
```

---

## B) Open a Project or Solution

### Current Details

Pfpad has **no solution explorer** and **no solution-aware UI**. The workspace panel (`Ctrl+Shift+W`) shows a flat filesystem tree filtered to ~65 text extensions. However, the **Roslyn integration** silently handles .sln/.csproj discovery in the background:

```
[file.cs opened in editor]
    ↓ walk up directories
[*.csproj found?] ─yes→ MSBuildWorkspace (full C# analysis with NuGet)
    ↓ no
[*.sln found?]    ─yes→ MSBuildWorkspace (via solution)
    ↓ no
[AdhocWorkspace]       (metadata-only, BCL-only, no NuGet)
```

The user **never sees this transition**. There is no UI indicator of:
- Which project or solution is active
- How many projects are in the solution
- Which project is the "startup project"
- Build configuration (Debug/Release)
- Project dependencies

### What Works

| Capability | Via | Notes |
|------------|-----|-------|
| Roslyn C# analysis | `RoslynWorkspaceService.cs` | Hybrid Adhoc→MSBuild, auto-upgrade |
| Go to Definition (C#) | `Form1.Roslyn.cs` | Roslyn semantic, ~98% accuracy |
| Hover tooltips | `HoverTooltipForm.cs` | XML doc from Roslyn |
| Signature help | `SignatureHelpForm.cs` | Parameter info on `(` |
| Symbol index | `SymbolIndexService.cs` | Background scan for all languages |
| Dependency viewer | `ProjectDependencyAnalyzer.cs` | Reads .csproj refs (read-only) |
| Launch profiles | `LaunchProfileParser.cs` | Reads launchSettings.json |
| Test discovery | `TestResultParser.cs` | Finds *Tests.csproj in workspace |

### What's Missing

| Feature | Impact | Where Documented |
|---------|--------|-----------------|
| Solution explorer tree | Can't see solution structure | `SOLUTION_IDE_ASSESSMENT.md` (3-4 days) |
| Solution file parser/writer | Can't create/edit .sln from UI | `SOLUTION_IDE_ASSESSMENT.md` (1-2 days) |
| New Solution wizard | No template-based creation | `SOLUTION_IDE_ASSESSMENT.md` (1-2 days) |
| Add/Remove projects | No project management | `SOLUTION_IDE_ASSESSMENT.md` (1 day) |
| Startup project selection | Run always targets current file's project | `SOLUTION_IDE_ASSESSMENT.md` (0.5 day) |
| Build configuration | Always Debug, no Release toggle | `SOLUTION_IDE_ASSESSMENT.md` (0.5 day) |
| NuGet package manager | No GUI for packages | `SOLUTION_IDE_ASSESSMENT.md` (3-4 days) |
| Solution-level state | No recent solutions, no persistence | `SOLUTION_IDE_ASSESSMENT.md` (0.5 day) |
| Build output panel | No structured error list | `SOLUTION_IDE_ASSESSMENT.md` (1-2 days) |

### Underlying Code

| File | Role |
|------|------|
| `Roslyn/RoslynWorkspaceService.cs` | Hybrid workspace (Adhoc→MSBuild), project/solution discovery |
| `Roslyn/IRoslynWorkspace.cs` | Workspace abstraction interface |
| `Roslyn/ReferenceAssemblyProvider.cs` | Loads reference assemblies |
| `WorkspacePanel.cs` | Filesystem tree (NOT solution-aware) |
| `ProjectDependencyAnalyzer.cs` | Regex-based .csproj reference reader |
| `LaunchProfileParser.cs` | launchSettings.json reader |
| `Form1.Roslyn.cs` | Roslyn integration points (highlighting, go-to-def, rename, diagnostics) |

### Justification

A full solution explorer is a **major architectural investment** (~10-14 days per SOLUTION_IDE_ASSESSMENT.md) with diminishing returns for a solo developer who typically works on single-project repos. The current auto-discovery model works for C# analysis.

However, three high-value improvements exist:

1. **Solution startup project indicator** — show which .csproj Roslyn found, make it clickable
2. **Solution-aware file tree** — filter workspace panel to show only files belonging to the active project (hide node_modules, unrelated files)
3. **Build configuration selector** — Debug/Release dropdown in toolbar

### Recommendation

**Do not build a full solution explorer.** The effort (10-14 days) does not justify the benefit for a solo developer. Instead:

1. **Add a workspace status indicator** (0.5 day) — show current project/solution name in status bar, with tooltip showing full path. Click to open in Explorer.
2. **Add project-filtered tree** (1 day) — optional toggle in workspace panel to show only files belonging to the detected .csproj/.sln (parse .csproj for Compile items, .sln for project files). Fall back to full tree when no solution detected.
3. **Add build config dropdown** (0.5 day) — Debug/Release combo box in toolbar, pass `--configuration` to build commands.

### Implementation Plan

```
Phase 1 — Workspace Status Indicator (0.5 day)
├── Modify: Form1.cs (status bar section)
│   ├── Add _workspaceProjectLabel to status strip
│   ├── On workspace root change:
│   │   ├── Scan for .sln (prefer) or .csproj
│   │   ├── Display: "Project: FlipCalc" or "Solution: MyApp (3 projects)"
│   │   └── Tooltip: full path to .sln/.csproj
│   └── On click: open containing folder in Explorer
│
└── Modify: RoslynWorkspaceService.cs
    └── Expose CurrentProjectName, CurrentSolutionName, IsSolutionLoaded

Phase 2 — Project-Filtered Tree (1 day)
├── Modify: WorkspacePanel.cs
│   ├── Add toolbar toggle: "Show Project Files Only" (📄 icon)
│   ├── When enabled:
│   │   ├── Parse .csproj for <Compile Include="..."> items
│   │   ├── Alternatively: parse .sln for project file list
│   │   └── Filter TreeView to show only those files + directories
│   └── When disabled: show full filesystem tree (current behavior)
│
└── Edge cases:
    ├── No .csproj/.sln found → toggle grayed out
    ├── .csproj uses wildcards → treat as "show all .cs files"
    └── Multi-project solution → union of all project file sets

Phase 3 — Build Configuration Selector (0.5 day)
├── Modify: Form1.Designer.cs (toolbar)
│   └── Add ToolStripComboBox: Debug | Release | (custom)
│
├── Modify: Form1.cs build methods
│   ├── Read selected config
│   └── Pass --configuration <config> to dotnet build/run
│
└── Persist: settings.json — activeConfiguration
```

---

## C) Open a Local Folder

### Current Details

This is the **primary entry point** for ALL Pfpad workflows. It's the most mature and well-tested path in the editor.

#### How to Open

| Method | UX | Notes |
|--------|----|-------|
| **Panel → Open Folder** (`Ctrl+Alt+O`) | Standard FolderBrowserDialog | Sets workspace root, triggers index rebuild |
| **Drag folder onto workspace panel** | Drag from Explorer | Replaces current workspace root |
| **Drag folder onto title bar** | Drag from Explorer | Same as above |
| **Command-line arg** | `Pfpad.exe folder/` | ❌ NOT supported — CLI args treated as files |

#### What Happens

```
Ctrl+Alt+O
  │
  ├─ 1. Cancel any in-flight workspace scan (WorkspacePanel.CancelScan)
  ├─ 2. Cancel any in-flight symbol index rebuild (SymbolIndexService)
  ├─ 3. Store path in _workspaceRoot
  ├─ 4. Call WorkspacePanel.SetRoot(path)
  │      ├─ Update root label → folder name
  │      ├─ Update tooltip → full path
  │      ├─ Clear existing TreeView
  │      ├─ Create root TreeNode with Tag = path
  │      ├─ Start async PopulateDirectoryAsync:
  │      │     ├─ Recursively walk ALL subdirectories
  │      │     ├─ Skip: .git, node_modules, bin, obj, .vs, packages, .svn, .hg
  │      │     ├─ Skip: any directory starting with "."
  │      │     ├─ Filter: only ~65 known text extensions
  │      │     ├─ Report progress every 25 directories
  │      │     └─ Build tree on UI thread via ApplyNodes
  │      └─ Start 5-second auto-refresh timer
  │
  ├─ 5. Start background SymbolIndex.RebuildIndex(path)
  ├─ 6. If workspace panel hidden → auto-show it
  └─ 7. Save workspace path to settings.json
```

#### What Auto-Activates

| Service | When | What It Does |
|---------|------|-------------|
| Symbol Index | Immediately | Background class/method/property scan |
| Git Service | On first file open | Auto-detects .git in parent hierarchy |
| Roslyn Workspace | On first .cs open | Scans for .csproj/.sln, upgrades Adhoc→MSBuild |
| Lint Engine | On text edit | 5 rules, 400ms debounce |
| Minimap | On document load | Background thread rebuild |

#### Workspace Panel Features

| Feature | Details |
|---------|---------|
| Header | Root folder name (bold, clickable), refresh (↻), collapse/expand (▼), close (×) |
| Tree | Alphabetical, directories first, lazy-load subdirs on expand |
| File filter | ~65 text extensions (`.cs`, `.js`, `.py`, `.md`, `.json`, `.yaml`, etc.) |
| Auto-refresh | Every 5s when root node expanded |
| Hidden | `.git`, `node_modules`, `bin`, `obj`, `.vs`, `packages`, `.svn`, `.hg`, dot-prefixed |
| Right-click | Open, Open Containing Folder, New File, New Folder, Copy Path |
| Double-click | Opens file in tab / toggles directory expand |
| Drag-drop | Files → open, Folder → replace workspace |

### Gaps

1. **No CLI folder arg** — `Pfpad.exe .` should set workspace root
2. **No recent folders** — must navigate manually each time
3. **Open Folder hidden under Panel menu** — non-obvious for new users
4. **No multi-root** — only one folder at a time
5. **No "Add folder to workspace"** — drag-drop replaces, doesn't add
6. **Auto-refresh is polling** — 5s timer, not event-driven. FileSystemWatcher would be instant.
7. **No solution-filtered view** — shows ALL text files, not just project files

### Underlying Code

| File | Role |
|------|------|
| `WorkspacePanel.cs` | TreeView + async directory scan + auto-refresh |
| `SymbolIndexService.cs` | Background symbol indexer |
| `Form1.cs` | `_workspaceRoot` field, `ToggleWorkspace()`, event wiring |

### Justification

This workflow is **already the strongest** in Pfpad. The gaps are paper cuts, not broken paths. However, since this is the primary entry point for everything else, fixing these paper cuts has an outsized impact on overall developer experience.

### Recommendation

Fix the gaps in priority order:

1. **CLI folder arg** (0.5 day) — `Pfpad.exe "C:\path\to\folder"` should set workspace root. Currently treats all args as files. Detect if arg is a directory → set workspace root.
2. **Recent folders** (0.5 day) — persist last 10 workspace paths in settings.json. Show in File menu or Panel menu as "Recent Workspaces" submenu.
3. **FileSystemWatcher** (0.5 day) — replace 5s polling timer with event-driven FileSystemWatcher. Instant refresh on file create/delete/rename.
4. **Move "Open Folder" to File menu** (0.25 day) — also keep it in Panel menu for existing users, but add File → Open Folder for discoverability.

### Implementation Plan

```
Phase 1 — CLI Folder Arg (0.5 day)
├── Modify: Program.cs (Main method)
│   ├── Iterate command-line args
│   ├── For each arg:
│   │   ├── If Directory.Exists(arg) → set as workspace root
│   │   └── If File.Exists(arg) → open as file (existing behavior)
│   └── Pass workspace root to Form1 constructor or via Startup event
│
└── Modify: Form1.cs
    └── Handle initial workspace root from command line

Phase 2 — Recent Folders (0.5 day)
├── Modify: settings.json schema
│   └── Add "RecentWorkspaces": string[]
│
├── Modify: Form1.cs
│   ├── On workspace open: add path to RecentWorkspaces (dedup, trim to 10)
│   ├── File menu → "Recent Workspaces" submenu
│   │   └── Each item: workspace path, click to open
│   └── "Clear Recent Workspaces" menu item
│
└── Modify: WorkspacePanel.cs
    └── Show "Recent Workspaces" in empty panel state (when no folder open)

Phase 3 — FileSystemWatcher (0.5 day)
├── Modify: WorkspacePanel.cs
│   ├── StartFileWatcher(rootPath):
│   │   ├── new FileSystemWatcher with IncludeSubdirectories=true
│   │   ├── NotifyFilter: FileName | DirectoryName | LastWrite
│   │   ├── Events: Created, Deleted, Renamed → DebounceRefresh
│   │   └── Debounce: 500ms timer (cancel pending on new event)
│   └── Dispose watcher on workspace close / cancel
│
└── Remove: 5-second polling timer (if FileSystemWatcher covers all cases)

Phase 4 — File Menu entry (0.25 day)
├── Modify: Form1.Designer.cs
│   └── File menu → add "Open Folder..." with Ctrl+Alt+O shortcut
│
└── Modify: Form1.cs
    └── Wire to same handler as Panel → Open Folder
```

---

## D) Create a New Project

### Current Details

Pfpad has **no "New Project" wizard**. This is the most glaring gap in the editor's project-management story. The only path is through the terminal panel:

```
CURRENT WORKFLOW:
  1. Ctrl+`            Open Terminal panel
  2. dotnet new sln
  3. dotnet new console -n MyApp
  4. dotnet sln add MyApp/MyApp.csproj
  5. Ctrl+Alt+O        Open Folder → select containing folder
  6. Start coding
```

All standard .NET templates are available through `dotnet new`:

| Template | Command |
|----------|---------|
| Console | `dotnet new console -n MyApp` |
| ASP.NET Core (empty) | `dotnet new web -n MyWeb` |
| Web API | `dotnet new webapi -n MyApi` |
| MVC | `dotnet new mvc -n MyMvc` |
| Class library | `dotnet new classlib -n MyLib` |
| xUnit test | `dotnet new xunit -n MyTests` |
| Solution file | `dotnet new sln` |
| Editorconfig | `dotnet new editorconfig` |
| Gitignore | `dotnet new gitignore` |

### What Exists vs. Missing

| Feature | Status | Notes |
|---------|--------|-------|
| .sln/.csproj discovery | ✅ | Roslyn auto-discovers |
| Project reference parsing | ✅ | ProjectDependencyAnalyzer (read-only) |
| Launch profile reading | ✅ | LaunchProfileParser |
| **"New Project" wizard** | ❌ | No UI at all |
| **Solution file creation** | ❌ | Must use terminal `dotnet new sln` |
| **Add project to solution** | ❌ | Must use terminal `dotnet sln add` |
| **Project template selection** | ❌ | Must know template names |
| **NuGet package management** | ❌ | Must use terminal `dotnet add package` |

### Underlying Code

| File | Role |
|------|------|
| `Roslyn/RoslynWorkspaceService.cs` | Project/solution discovery (auto, no UI) |
| `ProjectDependencyAnalyzer.cs` | Reads .csproj for references (read-only) |
| `LaunchProfileParser.cs` | Reads launchSettings.json |
| `TerminalPanel.cs` | De facto "new project" interface |

### Justification

For a solo developer, `dotnet new` in the terminal is **adequate but inefficient**. The friction points are:
- Must know exact template names (minor, `dotnet new list` helps)
- Must manually `dotnet sln add` (minor, but easy to forget)
- Must then `Ctrl+Alt+O` to open the folder (minor)
- No template preview (no description of what each template creates)

A "New Project" wizard is **medium-high effort** (1-2 days for basic) with moderate daily impact since project creation is infrequent. However, the presence of the terminal panel makes the workaround tolerable.

### Recommendation

Build a **simple New Project dialog** (not a full wizard with multiple pages):

1. **Template selector** — list from `dotnet new list`, parse output for name, description, language
2. **Project name** — textbox, populates folder name
3. **Location** — folder picker, default: last workspace parent or Documents
4. **Solution checkbox** — "Create solution file" (optional)
5. **Create button** — runs `dotnet new`, then `dotnet sln add` if checked, then auto-opens the folder

This wraps the terminal commands in a UI without reimplementing project scaffolding.

### Implementation Plan

```
Phase 1 — New Project Dialog (1.5 days)
├── New file: NewProjectDialog.cs
│   ├── Form with:
│   │   ├── Template list (ListView with Name, Description, Tags columns)
│   │   │   ├── Populated by running `dotnet new list` at dialog open
│   │   │   └── Parse output: columns are pipe-delimited
│   │   ├── Project name textbox (default: "MyApp")
│   │   ├── Location picker (FolderBrowserDialog, default: last workspace parent)
│   │   ├── "Create solution file (.sln)" checkbox
│   │   ├── Framework dropdown (net8.0, net9.0 — from dotnet --list-sdks)
│   │   └── Create + Cancel buttons
│   │
│   ├── On Create:
│   │   ├── Disable form, show progress label
│   │   ├── Background process: dotnet new <template> -n <name> -o <location>\<name>
│   │   │   └── Stream stdout/stderr to progress label
│   │   ├── If "Create solution" checked:
│   │   │   └── dotnet new sln -o <location> && dotnet sln <location>\<name>.sln add <location>\<name>\<name>.csproj
│   │   ├── On success: close dialog → auto-open <location>\<name> as workspace
│   │   └── On failure: show error, remain open
│   │
│   └── Error handling: template not found, SDK not installed, disk full, name conflict
│
├── Modify: Form1.cs
│   └── File menu → "New Project..." item (Ctrl+Shift+N for consistency with New Window)
│   └── (Reuse Ctrl+Shift+N or pick new shortcut)
│
├── Modify: Form1.Designer.cs
│   └── Menu wire-up
│
└── Edge cases:
    ├── No .NET SDK installed → show helpful error, link to download
    ├── Template list empty / parse failure → show textbox for manual template name
    └── Cancel during creation → kill process, clean up partial files

Phase 2 — NuGet Package Dialog (deferred, 3-4 days)
├── See: SOLUTION_IDE_ASSESSMENT.md NuGet section
└── Dependency: NuGet.Protocol + NuGet.PackageManagement NuGet packages
```

---

## E) Continue Without Code

### Current Details

When Pfpad starts with no command-line arguments, the user sees:

```
┌──────────────────────────────────────────────────┐
│  File  Edit  View  Panel  Run  Tools  Help       │
├──────────────────────────────────────────────────┤
│  [×] Untitled-1                                  │
├──────────────────────────────────────────────────┤
│                                                  │
│                                                  │
│              (blank editor area)                 │
│                                                  │
│                                                  │
├──────────────────────────────────────────────────┤
│ Ln 1  Col 1  Ch 0  master  ...                   │
└──────────────────────────────────────────────────┘
```

A single untitled tab. A workspace panel with no root. No recent files list visible. No welcome dialog. No quick-start guide. Nothing telling the user what to do next.

### What Exists

| Feature | Detail | Source |
|---------|--------|--------|
| Recent files | Last 10 files in File → Recent Files submenu | `Form1.cs` |
| No recent folders | ❌ Not persisted | — |
| No session restore | ❌ Last session state not saved | — |
| No welcome page | ❌ No landing page | — |
| No tip of the day | ❌ No onboarding | — |
| Command-line args | Files open as tabs; folders NOT supported | `Program.cs` |

### Underlying Code

| File | Role |
|------|------|
| `Form1.cs` | `Form1_Load`, `Form1_Shown`, `Form1_FormClosing` events |
| `Form1.Designer.cs` | Menu strip, status strip, panel layout |
| `Program.cs` | `Main()` — processes args, creates Form1 |

### Justification

"Continue without code" is the **starting state** every single session. For a solo developer who typically works on one project at a time, the ideal flow is:
1. Launch Pfpad
2. Automatically reopen last workspace + last files with cursor positions
3. Start typing where you left off

Currently, every session requires:
1. Remember what you were working on
2. `Ctrl+Alt+O` → navigate to project folder
3. Navigate workspace tree → open files
4. Scroll to where you were

This wastes **30-60 seconds per session** and breaks flow state.

### Recommendation

Build a **session restore system** and a **minimal welcome page**:

1. **Session restore** (1 day) — save open files, cursor positions, scroll positions, active tab on close. Restore on launch. This is the highest-ROI improvement in this category.

2. **Welcome page** (1 day) — shown when no session to restore. Quick links: Open Folder, New Project, Clone Repository, Recent Workspaces, Recent Files.

3. **Auto-restore last workspace** (0.5 day) — on launch, if a workspace was open on last close, auto-open it (in background, without blocking UI).

### Implementation Plan

```
Phase 1 — Session Restore (1 day)
├── New file: SessionManager.cs
│   ├── record SessionData:
│   │   ├── List<DocumentSnapshot> Documents { Path, CursorLine, CursorColumn, TopLine }
│   │   ├── int ActiveTabIndex
│   │   ├── string? WorkspacePath
│   │   └── DateTime LastSession
│   │
│   ├── Save path: %APPDATA%\MyCrownJewelApp\TextEditor\session.json
│   │
│   ├── SaveSession():
│   │   ├── Iterate open tab documents
│   │   ├── Capture cursor position (GetLineFromCharIndex, SelectionStart)
│   │   ├── Capture first visible line
│   │   ├── Capture active tab index
│   │   ├── Serialize to JSON
│   │   └── Write atomically (write to .tmp, then rename)
│   │
│   ├── RestoreSession():
│   │   ├── Read session.json
│   │   ├── Deserialize, validate (file exists checks)
│   │   ├── Open each file in order
│   │   ├── Restore cursor + scroll per file
│   │   ├── Activate last active tab
│   │   └── If WorkspacePath exists → auto-set workspace root
│   │
│   └── Error handling: corrupt JSON, deleted files, missing directories
│
├── Modify: Form1.cs
│   ├── Form1_FormClosing → call SessionManager.SaveSession()
│   ├── Form1_Shown → call SessionManager.RestoreSession()
│   └── Dispose SessionManager on form close
│
├── Modify: Form1.Designer.cs
│   └── Wire FormClosing and Shown events
│
└── Edge cases:
    ├── Pfpad launched with command-line args → skip session restore (args take priority)
    ├── File deleted between sessions → skip silently, note in status bar
    ├── Session file corrupt → back up to .corrupt, start fresh
    └── Large session (50+ files) → restore first 10, background-load rest

Phase 2 — Welcome Page (1 day)
├── New file: WelcomePage.cs (UserControl)
│   ├── Designed to replace the blank editor area when no tabs are open
│   │
│   ├── Layout:
│   │   ├── [Header] Personal Flip Pad — centered, large font
│   │   ├── [Quick Actions] Button row:
│   │   │   ├── 📂 Open Folder (Ctrl+Alt+O)
│   │   │   ├── ➕ New Project (Ctrl+Shift+N)
│   │   │   └── 📋 Clone Repository
│   │   ├── [Recent Workspaces] ListView (if any) — click to open
│   │   ├── [Recent Files] ListView (if any) — click to open
│   │   └── [Keyboard Shortcuts] Collapsible section with common shortcuts
│   │
│   ├── Integration:
│   │   ├── Show when TabControl.TabPages.Count == 0
│   │   ├── Hide when first tab opens (file or new tab)
│   │   └── Click actions call same handlers as menu items
│   │
│   └── Theming: respects active theme (Dark/Light)
│
├── Modify: Form1.cs
│   ├── Add WelcomePage to form (behind tab control or as overlay)
│   ├── TabControl.TabPages.CollectionChanged → show/hide welcome
│   └── Wire click handlers to existing menu command methods
│
└── Modify: Form1.Designer.cs
    └── Add WelcomePage control to form layout

Phase 3 — Auto-Restore Last Workspace (0.5 day)
├── Modify: SessionManager.cs
│   └── On restore, if WorkspacePath is set and directory exists:
│       ├── Call WorkspacePanel.SetRoot(WorkspacePath) (fires async scan)
│       └── Do NOT block UI — restore in background
│
└── Modify: WorkspacePanel.cs
    └── Ensure SetRoot is safe to call during Form1_Shown (before handle created)
```

---

## 6. Implementation Priority Matrix

All estimates assume a solo developer familiar with the codebase.

| Priority | Workflow | Phase | Effort | ROI | Dependencies |
|----------|----------|-------|--------|-----|-------------|
| **P0** | E — Session Restore | Phase 1 | 1 day | High — saves 30-60s per session | None |
| **P0** | C — CLI Folder Arg | Phase 1 | 0.5 day | High — fixes broken entry point | None |
| **P1** | C — Recent Folders | Phase 2 | 0.5 day | Medium — reduces navigation friction | None |
| **P1** | A — Clone Dialog | Phase 1 | 1 day | Medium — once per repo, high visibility | None |
| **P1** | C — FileSystemWatcher | Phase 3 | 0.5 day | Medium — instant > polling | None |
| **P1** | B — Workspace Status Indicator | Phase 1 | 0.5 day | Medium — shows hidden state | RoslynWorkspaceService |
| **P2** | D — New Project Dialog | Phase 1 | 1.5 days | Medium — infrequent but high visibility | None |
| **P2** | E — Welcome Page | Phase 2 | 1 day | Low-medium — quality of life | Session Manager |
| **P2** | B — Project-Filtered Tree | Phase 2 | 1 day | Medium — reduces clutter | WorkspacePanel |
| **P3** | B — Build Config Selector | Phase 3 | 0.5 day | Low — terminal workaround exists | None |
| **P3** | C — Move Open Folder to File Menu | Phase 4 | 0.25 day | Low — cosmetics | None |
| **P3** | E — Auto-Restore Last Workspace | Phase 3 | 0.5 day | Low — incremental to session restore | Session Restore |
| **P3** | D — NuGet Package Dialog | Phase 2 | 3-4 days | Low — terminal workaround exists | NuGet.Protocol |
| **Defer** | B — Full Solution Explorer | — | 10-14 days | Low for solo dev | SOLUTION_IDE_ASSESSMENT.md |
| **Defer** | A — Remote Management UI | Phase 2 | 2 days | Low — infrequent need | None |

### Recommended 2-Week Sprint

```
Week 1
├── Day 1:  E.1 — Session Restore (1d)
├── Day 2:  C.1 — CLI Folder Arg (0.5d) + C.2 — Recent Folders (0.5d)
├── Day 3:  A.1 — Clone Dialog (1d)
├── Day 4:  C.3 — FileSystemWatcher (0.5d) + B.1 — Workspace Status (0.5d)
└── Day 5:  C.4 — File Menu (0.25d) + Buffer

Week 2
├── Day 1:  D.1 — New Project Dialog (1d)
├── Day 2:  D.1 — New Project Dialog (cont'd, 0.5d) + E.2 — Welcome Page start
├── Day 3:  E.2 — Welcome Page (1d)
├── Day 4:  B.2 — Project-Filtered Tree (1d)
└── Day 5:  Buffer / bug fixes / polish
```

### Quick Wins (2 Days or Less, Sorted by Impact)

1. **Session Restore** (1d) — biggest daily quality-of-life improvement
2. **CLI Folder Arg** (0.5d) — fixes a broken entry point, trivial to implement
3. **Recent Folders** (0.5d) — saves navigation on every session
4. **Clone Dialog** (1d) — visible missing feature, moderate implementation
5. **FileSystemWatcher** (0.5d) — instant workspace refresh vs. 5s polling
6. **Workspace Status Indicator** (0.5d) — shows hidden solution/project state
7. **Build Config Selector** (0.5d) — toolbar dropdown, low effort
8. **Welcome Page** (1d) — nice-to-have, depends on session restore

---

## Appendix: Code Locations Reference

| File | Absolute Path |
|------|---------------|
| Form1.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/Form1.cs` |
| Form1.Designer.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/Form1.Designer.cs` |
| Form1.Roslyn.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/Form1.Roslyn.cs` |
| Program.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/Program.cs` |
| WorkspacePanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/WorkspacePanel.cs` |
| GitService.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/GitService.cs` |
| GitPanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/GitPanel.cs` |
| TerminalPanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/TerminalPanel.cs` |
| Roslyn/RoslynWorkspaceService.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/Roslyn/RoslynWorkspaceService.cs` |
| ProjectDependencyAnalyzer.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/ProjectDependencyAnalyzer.cs` |
| LaunchProfileParser.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/LaunchProfileParser.cs` |
| SymbolIndexService.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/SymbolIndexService.cs` |
| TestResultParser.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/TestResultParser.cs` |
| DiffPanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/DiffPanel.cs` (existing stub) |
| CommandPaletteForm.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/CommandPaletteForm.cs` (existing stub) |
| SnippetEngine.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/SnippetEngine.cs` (existing stub) |
| MarkdownPreviewPanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/MarkdownPreviewPanel.cs` (existing stub) |
| OutlinePanel.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/OutlinePanel.cs` (existing stub) |
| MultiCaretManager.cs | `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/MultiCaretManager.cs` (existing stub) |
| settings.json | `%APPDATA%\MyCrownJewelApp\TextEditor\settings.json` |

---

*This document cross-references existing analyses: `SOLUTION_IDE_ASSESSMENT.md`, `WORKFLOW_GUIDE.md`, `WORKFLOW_ACTION_PLAN.md`, `GIT_DEEP_DIVE.md`, `docs/workflow-improvements.md`. All estimates are for a single developer familiar with the codebase, working full-time.*

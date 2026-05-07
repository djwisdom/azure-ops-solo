# Solution-Level IDE Assessment — Build, Run, Debug

Assessment date: 2026-05-07
Applies to: MyCrownJewelApp (Personal Flip Pad) v1.0.14

---

## Current State

The editor currently operates as a **folder-based, single-file** editor with a surprisingly capable single-project debugger. There is no solution concept: `.sln` and `.csproj` files are only consumed internally by Roslyn's `MSBuildWorkspace` or regex-scanned for basic info. Build is a single synchronous `dotnet build` shell-out with no output capture. Run launches in a separate window or pipes to a truncated status label. Debug is functional but single-project only.

---

## 1. Create / Load / Save a Solution

### What Exists
| Component | Status |
|---|---|
| `.sln`/`.csproj` discovery | RoslynWorkspaceService.FindProjectOrSolution() walks up from current file |
| Project reference parsing | ProjectDependencyAnalyzer.Analyze() regex-scans .csproj refs (read-only) |
| Folder tree | WorkspacePanel shows filesystem tree, not solution structure |
| Launch profile reading | LaunchProfileParser finds launchSettings.json near .csproj |

### What Is Missing

| Feature | Gap | Approach |
|---|---|---|
| **Solution file parser/writer** | No .sln format handling at all; Roslyn MSBuildWorkspace handles it internally but is opaque | Write a `SolutionFile` class that parses/writes `.sln` format (the format is stable — nested project GUIDs, config platforms, solution folders). Or reference `Microsoft.Build` assemblies directly. |
| **Solution explorer tree** | WorkspacePanel is a filesystem tree with no solution awareness | New `SolutionExplorerPanel` control: `TreeView` with solution root → project nodes → folders/files → dependencies. Context menus: Build, Debug, Set as Startup Project, Add → New/Existing Item, Remove, Rename. Drag support. |
| **New Solution wizard** | No template-based creation | `dotnet new sln` + `dotnet new <template>` (winforms, console, classlib, etc.) invoked via process, then parse output. Or generate manually (`.sln` header + project GUIDs). Dialog: solution name, location, project template selection. |
| **Add/Remove projects** | No project management UI | Solution explorer context menu: Add → New Project (wizard), Add → Existing Project (file picker), Remove (confirm + unload or delete). Update `.sln` file. |
| **Solution-level state** | No recent solutions, no startup project persistence | `settings.json` extension: `RecentSolutions[]`, `ActiveSolutionPath`, `StartupProjectGuid`. Solution-scoped build configuration. |
| **NuGet package management** | No package UI | New dependency: `NuGet.Protocol` + `NuGet.PackageManagement`. Dialog: Browse (search nuget.org), Installed (current packages), Updates (available upgrades). Install/update/uninstall per project. |

### Effort
| Part | Estimate |
|---|---|
| SolutionFile parser/writer | 1-2 days |
| SolutionExplorerPanel (tree + context menus) | 3-4 days |
| New Solution wizard | 1-2 days |
| Add/Remove projects | 1 day |
| Solution-level state persistence | 0.5 days |
| NuGet package manager dialog | 3-4 days |
| **Total** | **~10-14 days** |

---

## 2. Build

### What Exists
| Component | Status |
|---|---|
| `RunDotnetBuild()` | Synchronous `Process.Start("dotnet", "build")` — no output capture, 30s timeout |
| Build invocation | Called only from `StartDebug_Click` and `RunWithoutDebug_Click` when DLL is missing |
| Build menu | None — no menu item, no toolbar button, no keyboard shortcut |

### What Is Missing

| Feature | Gap | Approach |
|---|---|---|
| **Async build service** | No background build with streaming output | Follow `TestResultParser.RunTests()` pattern: `Task<BuildResult> BuildProjectAsync(string projectPath, string configuration, CancellationToken ct)`. Stream stdout/stderr via `DataReceivedEventHandler`. |
| **Build output parser** | MSBuild errors/warnings not captured or parsed | Regex: `^(\S+?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+)\s*:\s*(.+)$`. Produce `BuildDiagnostic { File, Line, Column, Severity, Code, Message }`. |
| **Build output panel** | No output display | Dockable panel (`BuildOutputPanel`) with two views: raw log (read-only RichTextBox) and error list (DataGridView with double-click navigation). Follow `TestResultsDialog` pattern. |
| **Problems panel integration** | ProblemsPanel exists but only shows lint diagnostics | Route `BuildDiagnostic` items into `ProblemsPanel` alongside lint items. Tag by source ("Build" vs "Lint"). |
| **Build menu/UI** | No build command surface | Menu: Build → Build Solution (Ctrl+Shift+B), Rebuild Solution, Clean Solution, Build Selection. Toolbar button. Configuration selector: Debug/Release, AnyCPU/x86/x64. |
| **Build configuration** | No Debug/Release switching | Read from solution config (`.sln` has `Debug|AnyCPU = Debug|AnyCPU` lines). Persist active config in `settings.json`. Pass `--configuration <config>` to `dotnet build`. |
| **Build cancellation** | No way to stop a running build | `CancellationTokenSource` per build, tied to a Stop button in the output panel toolbar. Kill the `dotnet` process. |
| **Auto-build** | No build-on-save | Optional setting: on file save in a project context, debounce 500ms then build. |

### Effort
| Part | Estimate |
|---|---|
| Async build service | 0.5 days |
| Build output parser | 0.5 days |
| Build output panel | 1-2 days |
| Problems panel integration | 0.5 days |
| Build menu/UI + toolbar | 0.5 days |
| Build configuration management | 0.5 days |
| Build cancellation | 0.25 days |
| Auto-build (optional) | 0.5 days |
| **Total** | **~4-6 days** |

---

## 3. Run

### What Exists
| Component | Status |
|---|---|
| Run Without Debugging (Ctrl+F5) | Launches `dotnet "<outputDll>"` in separate window (`UseShellExecute=true`) |
| RunConfigurationDialog | Reads `launchSettings.json` profiles; runs `dotnet run`; output truncated to 80-char status label |
| TerminalPanel | Full WinForms terminal with ANSI color, multi-tab, shell process — NOT connected to run/debug |
| ExternalTools | Tools menu with user-configured process launches |

### What Is Missing

| Feature | Gap | Approach |
|---|---|---|
| **Run in terminal** | App output goes to separate window or truncated label, not the terminal panel | Option A (simpler): send `dotnet run --project "<path>"` to `TerminalPanel.SendInput()`. User gets full terminal interaction. Option B (more controlled): spawn `Process` with redirected IO, stream to a "Run Output" panel (read-only). |
| **Run button** | No toolbar play button | Toolbar button (▶), keyboard shortcut (Ctrl+F5), linked to startup project. Same handler path. |
| **Run configuration persistence** | launchSettings.json is read-only in the dialog | Write-back support for launch profiles. Or a simpler app-level run config in `settings.json`: `RunConfig { ProjectPath, Arguments, WorkingDirectory, EnvVars }`. |
| **Startup project selection** | Run always acts on current file's project | Solution explorer: bold the startup project node. Menu item: "Set as Startup Project". Fallback: first project in solution, or last-used. |
| **Stop/Restart** | Partial — RunConfigurationDialog has Stop | Global stop button (■) in toolbar/menu kills the running process. Restart (⟳) = Stop + Run. Track the active process in a `RunningProcess` field on Form1. |
| **Multi-project startup** | No way to run multiple projects simultaneously | Advanced: launch multiple processes from a startup config. Low priority. |

### Effort
| Part | Estimate |
|---|---|
| Run in terminal (Option A — terminal send) | 0.5 days |
| Run button + toolbar integration | 0.25 days |
| Run configuration persistence | 0.5 days |
| Startup project selection | 0.5 days |
| Stop/Restart global controls | 0.25 days |
| **Total** | **~2-3 days** |

---

## 4. Debug

### What Exists
| Component | Status |
|---|---|
| DAP transport | `DebugAdapterClient.cs` — launches netcoredbg, JSON messaging over stdin/stdout |
| Debug session manager | `DebugSession.cs` — initialize, launch, breakpoints, step/continue, stack, scopes, variables, evaluate, threads |
| Breakpoint management | `BreakpointManager.cs` — toggle, enable/disable, clear, conditional, hit-count, logpoints, JSON persistence |
| Call stack panel | `DebugCallStackPanel.cs` — ListView with double-click navigation |
| Variables panel | `DebugVariablesPanel.cs` — TreeView with expandable locals/watches |
| State machine | Idle → Initializing → Running → Paused/Stepping → Terminating → Terminated |
| Keyboard shortcuts | F5 (continue), Shift+F5 (stop), F10 (step over), F11 (step into) |
| Gutter breakpoints | GutterPanel.Invalidate() on BreakpointsChanged |

### What Is Missing

| Feature | Gap | Approach |
|---|---|---|
| **Debug toolbar** | Only menu items, no dockable toolbar | Toolbar: ▶ Continue, ■ Stop, ⟳ Restart, ↷ Step Over, ↓ Step Into, ↑ Step Out, ⏸ Pause. Bind to existing `DebugSession` methods. |
| **Startup project selection** | Deb fights "current file's project" | Same as Run: startup project is a solution-level setting. When debugging, launch that project's output via netcoredbg. |
| **Launch profile integration** | launchSettings.json not connected to debugger | Read `LaunchProfileParser` profiles, populate a dropdown, apply env vars/args to the debug launch config. |
| **Attach to process** | No process picker | Dialog listing processes (via `Process.GetProcesses()`), user picks one, then `netcoredbg --attach <pid>` instead of `--launch`. DAP supports attach config. |
| **Debug output panel** | DAP `DebugOutputEvent` goes to notification toast, not a panel | New dockable panel (`DebugOutputPanel`): read-only text with category coloring (console, warnings, errors). Append events from `DebugSession.OutputReceived`. |
| **Threads panel** | `GetThreadsAsync()` exists but no UI | New tool window: list of threads with ID, name, location, active state. Click to switch thread (calls `SetThreadAsync()` + refreshes stack/variables). |
| **Modules panel** | No loaded-module display | Optional panel showing loaded assemblies during debug. DAP `ModuleEvent` exists. Low priority. |
| **Hover value tooltip while debugging** | No variable inspection on hover when paused | When debugger is paused and user hovers over an identifier, call `EvaluateAsync(identifier)` and show result in a `HoverTooltipForm`. Integrate with existing hover system. |
| **Edit and Continue** | No hot-reload support | Requires `dotnet watch` or Roslyn's EnC API + metadata update. Very complex — requires the running process to support hot-reload modules. Not recommended for initial implementation. |

### Effort
| Part | Estimate |
|---|---|
| Debug toolbar | 0.5 days |
| Startup project selection (shared with Run) | 0.5 days (shared) |
| Launch profile integration | 0.5 days |
| Attach to process dialog | 1 day |
| Debug output panel | 0.5 days |
| Threads panel | 1 day |
| Hover value tooltip | 1 day |
| Modules panel (optional) | 0.5 days |
| Edit and Continue | 3-5 days (advanced) |
| **Total (core)** | **~4-6 days** |
| **Total (+EnC)** | **~7-11 days** |

---

## Existing Patterns to Reuse

The test runner subsystem provides the best template for build and run:

| Pattern | File | Use for |
|---|---|---|
| Async process with output capture | `TestResultParser.RunTests()` | Async build service |
| Structured results + file:line navigation | `TestResultsDialog` | Build output panel, error list |
| Background task with status notification | `Form1.RunTests()` | Build/Run status feedback |
| xUnit test invocation | `dotnet test --logger trx` | Replace with `dotnet build` / `dotnet run` |

The debugger is already the most complete subsystem and needs the least structural change — mostly UI wiring and multi-project awareness.

---

## Implementation Order

1. **Solution file management** (SolutionFile parser, .sln load/save)
2. **Solution explorer tree** (replace/upgrade WorkspacePanel)
3. **Async build service + output parser** (follow TestResultParser)
4. **Build output panel + Problems integration**
5. **Run in terminal + Run button** (wire existing pieces)
6. **Debug toolbar + startup project integration**
7. **Attach to process + Threads panel**
8. **Launch profile integration for debug**
9. **NuGet package manager**
10. **Edit and Continue** (advanced, deferrable)

Steps 1-2 are prerequisites for everything else. Steps 3-6 can overlap. Step 7-8 are polish. Step 9-10 are independent enhancements.

---

## Total Effort Summary

| Capability | Effort | Dependencies |
|---|---|---|
| Solution load/save | 10-14 days | None (foundational) |
| Build | 4-6 days | Solution explorer (for project selection) |
| Run | 2-3 days | Solution explorer (for startup project) |
| Debug | 4-6 days (+3-5 for EnC) | Solution explorer, build (for fresh DLL) |
| **Grand total** | **~20-29 days** | **~4-6 weeks** |

All estimates assume a single developer familiar with the codebase, working full-time.

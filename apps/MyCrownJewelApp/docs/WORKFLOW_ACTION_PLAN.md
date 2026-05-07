# Top 5 Workflows — Recommendation & Action Plan

**Date:** 2026-05-07
**Applies to:** Personal Flip Pad (Pfpad) v1.0.16
**Audience:** Solo developer — limited time, maximum ROI per change

---

## Guiding Principles

1. **Fix what's already built before adding new things.** The debugger and Git panel are functional but have painful paper cuts. Smoothing those delivers immediate value.
2. **Close the terminal gap.** Too many workflows require dropping to the terminal panel. The goal is to keep the developer in the editor for as many cycles as possible.
3. **Accuracy over features.** A regex-based Go to Definition that works 80% of the time is worse than a Roslyn-based one that works 98%. Invest in foundations.
4. **Each item should ship independently.** No mega-releases. Each improvement should be shippable in a single session.

---

## Priority Ranking

| Priority | Workflow | Rationale |
|----------|----------|-----------|
| **P0** | Git diff view | Biggest daily-impact paper cut. Every commit cycle requires context-switch. |
| **P1** | Debug hover values | Directly accelerates the most valuable existing workflow. |
| **P1** | Roslyn Go to Definition for C# | Fixes the #1 accuracy complaint. Regex fallback remains for non-C#. |
| **P2** | Run in Terminal | Closes the run-feedback loop. Eliminates the separate-window problem. |
| **P3** | Lint rule expansion | Low-effort, high-visibility quality improvement. |

---

## P0 — Git Diff View

### Current State
The Git panel (section 6) can stage, commit, push, pull, fetch, and switch branches — but **there is no diff view**. The developer must drop to `git diff` in the terminal to see what changed before staging. This breaks flow on every single commit.

### Recommendation
Add an inline diff panel that opens when a file is selected in the Source Control panel's changes list.

### Implementation Sketch

```
New file: DiffPanel.cs (or extend GitPanel.cs)

Architecture:
  ┌─ SourceControlPanel ─────────────────────┐
  │  ┌─ Changes List ──────────────────────┐ │
  │  │  [M] Program.cs          ← click    │ │
  │  │  [M] Services/Auth.cs               │ │
  │  └─────────────────────────────────────┘ │
  │  ┌─ Diff View (opens below) ───────────┐ │
  │  │  - public void OldMethod()          │ │
  │  │  + public void NewMethod()          │ │
  │  │  (syntax colored, line numbers)     │ │
  │  └─────────────────────────────────────┘ │
  └──────────────────────────────────────────┘
```

**Key decisions:**
- Use LibGit2Sharp `Diff.Compare<Tree, Tree>()` to get patch content — no process spawning
- Render in a read-only `RichTextBox` with basic diff coloring (green/red line backgrounds)
- Show the diff below the files list in a `SplitContainer` (vertical resize handle)
- Stage/unstage buttons directly in the diff panel header
- Max file size cutoff (e.g., 500KB diffs show "File too large, view in terminal")

**Estimated effort:** 1–2 days
- Diff content fetching via LibGit2Sharp: 0.5 day
- Diff rendering control (RichTextBox with syntax-colored +/- lines): 0.5 day
- Panel integration (split, buttons, selection sync): 0.5 day
- Edge cases (binary files, new files, deleted files, large files): 0.5 day

**ROI:** Every commit cycle (multiple times per hour) saves 5–10 seconds of `git diff` + mental context-switch. Over a day: dozens of saved switches. Highest frequency workflow improvement available.

---

## P1 — Debug Hover Value Tooltips

### Current State
The debugger (section 5) can inspect locals via the Variables panel (tree view, expand objects). But to see a variable's value, the developer must:

1. Pause at breakpoint
2. Look down at the Variables panel
3. Find the variable in the tree
4. Expand if needed

This breaks visual focus. VS/VS Code show values in a tooltip when you hover over a variable during break — zero eye movement.

### Recommendation
When a debug session is paused, intercept hover events over identifiers and query netcoredbg's `evaluate` request. Show the value in the existing `HoverTooltipForm`.

### Implementation Sketch

```
Existing HoverTooltipForm (400ms hover, XML doc):
  ↓ Add check: is debugger paused?
  ↓ Yes → call DebugAdapterClient.Evaluate(identifierName)
  ↓      → show "value = 42" in tooltip alongside or replacing doc

Form1.cs mouse hover handler:
  if (_debugSession?.State == DebugSession.DebugState.Paused)
  {
      var word = GetWordUnderCursor(e.Location);
      var value = await _debugSession.Evaluate(word);
      ShowTooltip(word, value); // reuse HoverTooltipForm
  }
```

**DAP message:** `EvaluateArguments` with `context: "hover"` — netcoredbg supports this natively.

**Estimated effort:** 0.5–1 day
- DAP evaluate integration in hover handler: 0.25 day
- Tooltip rendering (value types, long strings truncation, null display): 0.25 day
- Edge cases (expressions, properties with side effects, error handling): 0.25 day

**ROI:** Directly amplifies the #1 most valuable workflow. The difference between "glance at variable" and "search Variables panel" is ~1–2 seconds per inspection but is felt as a major flow interruption.

---

## P1 — Roslyn Go to Definition for C#

### Current State
Go to Definition (F12) uses a two-tier approach: ctags (if available) → per-language regex fallback. For C#, this is ~80% accurate. It fails on generics, partial classes, inherited members, and anything involving NuGet package types. A Roslyn-powered path exists but only fires for hover and signature help, not for Go to Definition itself.

### Recommendation
Wire the existing `RoslynWorkspaceService` into the F12 handler. When Roslyn's MSBuild workspace is active (`.csproj` or `.sln` found), prefer the Roslyn `SymbolFinder.FindSourceDefinitionAsync` / `FindReferencesAsync` result. Fall back to the regex path when Roslyn is unavailable or returns no results.

### Implementation Sketch

```
Form1.cs GoToDefinition_Click (or Form1.Roslyn.cs):
  if (activeDoc?.Syntax == "C#" && _roslynService?.IsReady == true)
  {
      var result = await _roslynService.FindDefinitionAsync(filePath, line, col);
      if (result != null) { NavigateTo(result); return; }
  }
  // Fall through to existing ctags → regex path
```

**Note:** `RoslynService.cs` already has `GetGoToDefinitionLocation()` (line ~45 in `Form1.Roslyn.cs`) — it needs to be checked for completeness and wired to the F12 keybinding path rather than only to the Roslyn-specific menu.

**Estimated effort:** 0.5 day
- Audit existing `Form1.Roslyn.cs` GoToDefinition (line 45): 0.25 day
- Wire into main F12 handler with graceful fallback: 0.25 day

**ROI:** Fixes the single most visible accuracy complaint. C# is the primary language for a .NET solo dev. 80% → 98% accuracy on F12 is a qualitative leap, not incremental.

---

## P2 — Run in Terminal

### Current State
Run Without Debug (`Ctrl+F5`) launches the app in a **separate OS window**. The Run Configurations dialog truncates output to 80 characters in a status label. The terminal panel exists but there's no integration — the developer must manually `dotnet run`.

### Recommendation
When `Ctrl+F5` is pressed, instead of launching a new window, spawn the process in the existing Terminal panel. Pipe stdout/stderr to the active terminal tab. Add a stop button to kill the process.

### Implementation Sketch

```
Form1.cs RunWithoutDebug_Click:
  // Old: Process.Start("dotnet", $"\"{outputDll}\"")  -- separate window
  // New: 
  var process = new Process
  {
      StartInfo = { FileName = "dotnet", Arguments = $"run --project \"{projectPath}\"" },
      // redirect stdout/stderr
  };
  _terminalPanel.StartProcess(process);  // shows output in terminal tab
  // Show stop button in terminal header
```

**Reuse:** The terminal panel already has ANSI escape handling, multi-tab support, and a stop button (for terminal Ctrl+C). Add a "process mode" where a managed process pipes into a terminal tab.

**Estimated effort:** 1–2 days
- Process launch + pipe into terminal control: 0.5 day
- Process lifecycle management (start/stop/restart indicators): 0.5 day
- Environment variable support (from launchSettings.json): 0.5 day
- Edge cases (process exit code display, multi-project, quick successive restarts): 0.5 day

**ROI:** Eliminates the jarring "popup window" workflow. Output visible in-editor. P2 because the terminal workaround exists and is adequate.

---

## P3 — Lint Rule Expansion

### Current State
The lint engine fires 5 rules on a 400ms debounce. Rules are simple regex checks in `LintEngine.cs`. Three quick-action fixes exist (trailing whitespace, insert semicolon, add missing using).

### Recommendation
Add 3–5 new lint rules that catch common solo-developer mistakes with minimal false positive risk. Each rule is a 10-line function.

### Proposed Rules

| Code | Rule | Regex/Check | Effort |
|------|------|-------------|--------|
| PFP006 | **Missing `readonly`** | Field declarations like `private int _x;` where `_x` is never assigned outside constructor | 2 hours |
| PFP007 | **Empty catch block** | `catch { }` or `catch (Exception) { }` with nothing between braces | 1 hour |
| PFP008 | **Hardcoded connection string** | Contains `Server=;` or `Data Source=` pattern | 0.5 hour |
| PFP009 | **Switch without default** | `switch` block with no `default:` case | 1 hour |
| PFP010 | **Unused using directive** | Line starts with `using ` and the namespace doesn't appear elsewhere in file | 2 hours |

**Estimated effort:** 1–2 days total (all 5 rules + tests)
- Each rule: 0.5–2 hours (regex design, edge cases, squiggle positioning)
- Quick-action fixes for high-value rules (PFP006/PFP008): 1 hour each
- Tests for each rule: 0.5 hour each

**ROI:** Low effort, highly visible. Each new rule is a standalone win. P3 because existing 5 rules already provide basic coverage — this is polish, not necessity.

---

## Summary Roadmap

```
Week 1 ─── P0: Git Diff View ───────────────── 1–2 days
           │
           └── Ships: developer no longer drops to terminal for every commit

Week 2 ─── P1: Debug Hover Values ──────────── 0.5–1 day
           │
           ├── P1: Roslyn Go to Definition ──── 0.5 day
           │
           └── Ships: debugger now feels "complete"

Week 3 ─── P2: Run in Terminal ─────────────── 1–2 days
           │
           └── Ships: no more popup windows, output visible in-editor

Week 4 ─── P3: Lint Rule Expansion ─────────── 1–2 days
           │
           └── Ships: 5 new rules, "add readonly" quick action

Total:     ~5–8 days of focused work
```

---

## What NOT To Do (For Now)

These are deliberately excluded from the P0–P3 plan:

| Workflow | Why Not Now |
|----------|-------------|
| **Clone dialog** | Too large (credential management, SSH, OAuth). Terminal workaround is fine. |
| **Solution explorer** | Major architectural work (see SOLUTION_IDE_ASSESSMENT.md estimate: 10–14 days). Not worth the investment for a solo dev using single-project repos. |
| **Build output panel** | Terminal workaround exists. Requires parsing MSBuild output format. Low ROI vs. effort. |
| **Edit and Continue** | Deep DAP/CLR integration. Would take weeks. Deferred indefinitely. |
| **Attach to process** | Low frequency need for solo dev. Terminal `dotnet run` + F5 is sufficient. |

---

*This plan is optimized for a solo developer maximizing value per unit of implementation time. Each item is self-contained, shippable independently, and addresses a concrete daily pain point. Estimated implementation times assume familiarity with the existing codebase.*

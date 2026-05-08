# Top 5 Workflows — Recommendation & Action Plan

**Date:** 2026-05-07 (Updated 2026-05-09)
**Applies to:** Personal Flip Pad (Pfpad) v1.0.20
**Audience:** Solo developer — limited time, maximum ROI per change

---

## Guiding Principles

1. **Fix what's already built before adding new things.** The debugger and Git panel are functional but have painful paper cuts. Smoothing those delivers immediate value.
2. **Close the terminal gap.** Too many workflows require dropping to the terminal panel. The goal is to keep the developer in the editor for as many cycles as possible.
3. **Accuracy over features.** A regex-based Go to Definition that works 80% of the time is worse than a Roslyn-based one that works 98%. Invest in foundations.
4. **Each item should ship independently.** No mega-releases. Each improvement should be shippable in a single session.

---

## Priority Ranking (Updated 2026-05-09)

| Priority | Workflow | Rationale | Status |
|----------|----------|-----------|--------|
| **P0** | Git diff view | Biggest daily-impact paper cut. Every commit cycle requires context-switch. | ✅ **Done** (aa51315) |
| **P1** | Debug hover values | Directly accelerates the most valuable existing workflow. | ✅ **Done** (0d7ad87) |
| **P1** | Roslyn Go to Definition for C# | Fixes the #1 accuracy complaint. Regex fallback remains for non-C#. | ✅ **Done** (4fae661) |
| **P2** | Run in Terminal | Closes the run-feedback loop. Eliminates the separate-window problem. | ✅ **Done** (94e508e) |
| **P3** | Lint rule expansion | Low-effort, high-visibility quality improvement. | ❌ Pending |

---

## P0 — Git Diff View ✅ COMPLETED (aa51315)

### Current State (Was)
The Git panel (section 6) can stage, commit, push, pull, fetch, and switch branches — but **there is no diff view**. The developer must drop to `git diff` in the terminal to see what changed before staging. This breaks flow on every single commit.

### Implementation
- `DiffPanel.cs` — full implementation with LCS-based hunk computation
- Staged/unstaged diffs via LibGit2Sharp `Diff.Compare<Tree, Tree>()` — no process spawning
- Read-only `RichTextBox` with inline diff coloring (green/red line backgrounds, blue hunk headers)
- Split panel: files list above, diff view below with resize handle
- Stage/Unstage/Discard buttons in header strip
- Max file size cutoff for large diffs
- Fixes: splitter reset, panel dock filling, button sizing, launch NRE (commits 325aaa7, 9e93dcc, 53df29d, 79c445c)

### What Changed from Original Sketch
- Architecture uses `DiffPanel.cs` as a standalone control embedded in `GitPanel.cs`
- Diff coloring merged into a single `ApplyDiffColoring` method instead of raw RTB
- Button styling matches professional color conventions

**Effort spent:** ~3 days (including fixes), versus 1-2 day estimate

---

## P1 — Debug Hover Value Tooltips ✅ COMPLETED (0d7ad87)

### Current State (Was)
The debugger (section 5) can inspect locals via the Variables panel (tree view, expand objects). But to see a variable's value, the developer must:

### Implementation
- `Form1.cs:6492` — `ShowDebugHoverTooltipAsync` wires DAP `evaluate` into the existing hover timer (`HoverTimer_Tick`)
- When debugger is paused (`DebugState.Paused`), hover over an identifier calls `DebugAdapterClient.Evaluate(word, frameId)`
- Reuses `HoverTooltipForm` to display value alongside or replacing doc tooltip
- Frame ID is cached on breakpoint hit
- DAP message uses `EvaluateArguments` with `context: "hover"`

**Effort spent:** ~0.5 day

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

**Effort spent:** ~0.5 day

---

## P1 — Roslyn Go to Definition for C# ✅ COMPLETED (4fae661)

### Current State (Was)
Go to Definition (F12) uses a two-tier approach: ctags (if available) → per-language regex fallback. For C#, this is ~80% accurate.

### Implementation
- `Form1.Roslyn.cs:46` — `GoToDefinitionRoslyn()` uses `SymbolFinder.FindSourceDefinitionAsync` via existing `RoslynWorkspaceService`
- Wired into F12 keybinding path, with graceful fallback to ctags/regex when Roslyn unavailable or returns no results
- Works for generics, partial classes, inherited members, NuGet package types

**Effort spent:** ~0.5 day

```
Form1.cs GoToDefinition_Click (or Form1.Roslyn.cs):
  if (activeDoc?.Syntax == "C#" && _roslynService?.IsReady == true)
  {
      var result = await _roslynService.FindDefinitionAsync(filePath, line, col);
      if (result != null) { NavigateTo(result); return; }
  }
  // Fall through to existing ctags → regex path
```
---

## P2 — Run in Terminal ✅ COMPLETED (94e508e)

### Current State (Was)
Run Without Debug (`Ctrl+F5`) launches the app in a **separate OS window**.

### Implementation
- `Form1.cs:6676` — `RunWithoutDebug_Click` spawns `dotnet run --project "{projectPath}"` with redirected stdout/stderr piped into the embedded terminal panel
- Stop button kills the process
- Environment variables from `launchSettings.json` are supported via `LaunchProfileParser`
- Output visible in-editor with ANSI escape handling, multi-tab support

**Effort spent:** ~1 day

---

## P3 — Lint Rule Expansion (❌ Still Pending)

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

## Summary Roadmap (Updated 2026-05-09)

```
Week 1 ─── P0: Git Diff View ───────────────── 1–2 days  ✅ COMPLETED
           │
           └── Ships: developer no longer drops to terminal for every commit

Week 2 ─── P1: Debug Hover Values ──────────── 0.5–1 day ✅ COMPLETED
           │
           ├── P1: Roslyn Go to Definition ──── 0.5 day   ✅ COMPLETED
           │
           └── Ships: debugger now feels "complete"

Week 3 ─── P2: Run in Terminal ─────────────── 1–2 days  ✅ COMPLETED
           │
           └── Ships: no more popup windows, output visible in-editor

Week 4 ─── P3: Lint Rule Expansion ─────────── 1–2 days  ❌ REMAINING
           │
           └── Ships: 5 new rules, "add readonly" quick action
```

---

## What NOT To Do (For Now)

These are deliberately excluded from the P0–P3 plan:

| Workflow | Why Not Now |
|----------|-------------|
| **Clone dialog** | ✅ **Implemented** — `CloneRepositoryDialog.cs` ships with `Ctrl+Shift+C`. |
| **Solution explorer** | Major architectural work (see SOLUTION_IDE_ASSESSMENT.md estimate: 10–14 days). Not worth the investment for a solo dev using single-project repos. |
| **Build output panel** | Terminal workaround exists. Requires parsing MSBuild output format. Low ROI vs. effort. |
| **Edit and Continue** | Deep DAP/CLR integration. Would take weeks. Deferred indefinitely. |
| **Attach to process** | Low frequency need for solo dev. Terminal `dotnet run` + F5 is sufficient. |

---

*This plan is optimized for a solo developer maximizing value per unit of implementation time. Each item is self-contained, shippable independently, and addresses a concrete daily pain point. Estimated implementation times assume familiarity with the existing codebase.*

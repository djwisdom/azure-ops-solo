# Built-in Terminal — Personal Flip Pad (Pfpad)

**App version:** 1.0.46.0 · **Target:** net10.0-windows  
**Last updated:** 2026-06-16

---

## Overview

Pfpad includes a multi-tab built-in terminal panel. It supports full ANSI colour output, command history, multi-tab sessions, and custom shell selection. The terminal automatically opens to the current workspace folder.

**Toggle:** `Ctrl+`` ` (backtick) or **View → Terminal**

---

## Terminal Modes

The terminal has two operating modes. It selects the best available mode automatically at startup:

### 1. PTY Mode (ConPTY) — preferred

Uses the **Windows Pseudo Console (ConPTY)** API introduced in Windows 10 1809 (build 17763). In this mode:

- Child processes see a real TTY (`isatty()` returns `true`)
- Interactive CLI tools — `gh copilot`, `fzf`, `pnpm`, ncurses-based menus — render correctly inside the terminal panel
- Arrow keys route to the running process for menu navigation when the input box is empty
- Terminal resize signals are sent to the child via `ResizePseudoConsole`

### 2. Compatibility Mode (pipe mode) — fallback

Uses standard `Process` I/O redirection (`stdin`/`stdout`/`stderr` pipes). In this mode:

- Commands run and output correctly for non-interactive programs
- Interactive TUI programs (e.g. `gh copilot suggest`) may open their menu in a **separate console window** rather than inside the terminal panel, because they detect a non-TTY stdin and call `AllocConsole()` themselves
- All other terminal functionality (history, ANSI colour, multi-tab, themes) works normally

---

## PTY Mode Blocked by Enterprise Security Software

Some enterprise security agents (such as **Avacee SIPAgent**, common in corporate environments) inject monitoring DLLs into every new process. When a process is launched with the ConPTY attribute (`EXTENDED_STARTUPINFO_PRESENT`), the DLL injection fails during `DLL_PROCESS_ATTACH`, causing the child process to exit immediately with:

```
STATUS_DLL_INIT_FAILED (0xC0000142)
```

Pfpad detects this automatically:

1. **First launch after ConPTY is blocked** — the terminal shows:
   ```
   [Terminal] ConPTY blocked by security software on this system.
   [Terminal] Switching to compatibility mode (some interactive CLIs may open in a separate window).
   ```
   Then starts normally in pipe mode.

2. **All subsequent launches** — the block is persisted to a flag file:
   ```
   %APPDATA%\MyCrownJewelApp\TextEditor\.conpty-blocked
   ```
   Pfpad reads this at startup and goes directly to pipe mode with no ConPTY attempt.

> **Note:** The ConPTY code itself is correct and will work as designed on machines without the interfering security agent.

---

## Re-enabling PTY Mode

If the security agent is removed, updated, or Pfpad is added to its process exclusion list, you can re-enable ConPTY by deleting the flag file:

```powershell
Remove-Item "$env:APPDATA\MyCrownJewelApp\TextEditor\.conpty-blocked"
```

Then restart Pfpad. If ConPTY still fails, the flag file will be recreated automatically.

---

## Shell Selection

Pfpad auto-discovers the shell in this order:

| Priority | Path |
|----------|------|
| 1 | User-configured path (Settings → Terminal → Shell Path) |
| 2 | `%ProgramFiles%\PowerShell\7\pwsh.exe` |
| 3 | `%ProgramFiles(x86)%\PowerShell\7\pwsh.exe` |
| 4 | `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` |
| 5 | `cmd.exe` |

---

## Input Handling

| Key | Behaviour |
|-----|-----------|
| **Enter** | Send current line to the shell |
| **↑ / ↓** (input has text) | Navigate in-app command history |
| **↑ / ↓** (input empty, PTY mode) | Send `ESC[A` / `ESC[B` to process (menu navigation) |
| **← / →** (input empty, PTY mode) | Send `ESC[D` / `ESC[C` to process |
| **Ctrl+C** | Send `^C` (interrupt) |
| **Ctrl+D** | Send `^D` (EOF) |
| **Tab** | Send `\t` (tab completion in PTY mode) |
| **Escape** | Send `ESC` |

---

## Multiple Tabs

Click **+** in the tab bar to open an additional shell tab. Each tab runs an independent shell instance. Tabs survive editor file switches.

---

## Limitations in Compatibility (Pipe) Mode

- `gh copilot suggest` and `gh copilot explain` interactive menus open in a separate console window
- Programs that call `AllocConsole()` or check `isatty()` to decide output format will use plain text mode or a new window
- Resize signals are not forwarded to child processes
- Arrow-key process routing is disabled

These limitations are inherent to pipe-based I/O and are resolved by enabling PTY mode (see [Re-enabling PTY Mode](#re-enabling-pty-mode) above).

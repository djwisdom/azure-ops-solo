# MyCrownJewelApp.Terminal

Embedded terminal for Avalonia-based editors using ConPTY on Windows and Unix PTY elsewhere.

## Components

- **TerminalManager.cs** – Backend PTY manager. On Windows uses `Conpty.Net`; on Unix uses `System.Diagnostics.Process` with PTY.
- **TerminalPane.axaml** + **TerminalPane.axaml.cs** – Avalonia UserControl embedding `Avalonia.TerminalControl.TerminalControl`.
- **TerminalTests.cs** – xUnit integration test that runs `echo test` and validates output.

## Dependencies (NuGet)

| Package | Version | Notes |
|---|---|---|
| Avalonia.Base | 11.0.0 | Core Avalonia |
| Avalonia.Desktop | 11.0.0 | Desktop runtime |
| Avalonia.TerminalControl | 2.0.0 | Terminal UI widget |
| Avalonia.Win32 | 11.0.0 | Windows interop |
| Conpty.Net | 0.5.0 | Windows ConPTY wrapper |
| xunit | 2.9.2 | Test framework |
| Microsoft.NET.Test.Sdk | 17.11.1 | Test SDK |

**Assumptions:**
- Using .NET 8.0
- Avalonia 11.0.0 is compatible with TerminalControl 2.0.0
- Conpty.Net 0.5.0 supports the Windows 10/11 build you're on
- On Unix, `/bin/bash` or `/bin/zsh` exists; `TERM=xterm-256color` is set

If versions mismatch, adjust accordingly in the `.csproj` files.

## Build Commands

```bash
# Build Terminal library
dotnet build apps/MyCrownJewelApp/src/MyCrownJewelApp.Terminal/MyCrownJewelApp.Terminal.csproj

# Run tests (requires a display on Linux/macOS; on Windows should work out of box)
dotnet test apps/MyCrownJewelApp/tests/MyCrownJewelApp.Terminal.Tests/MyCrownJewelApp.Terminal.Tests.csproj
```

## Notes

- TerminalManager starts a shell process and pipes its stdio. It exposes `SendInput(string)` to send commands and `Output` to read accumulated stdout.
- TerminalPane connects TerminalManager's output events to the Avalonia TerminalControl via `Write()`.
- For headless test runs on Linux/macOS, you may need `xvfb-run` or similar to provide a virtual display for Avalonia.
- The test uses `Thread.Sleep(500)` to wait for output. In production code, use a proper synchronization primitive (e.g., `TaskCompletionSource` waiting for `OutputReceived` event with expected content).

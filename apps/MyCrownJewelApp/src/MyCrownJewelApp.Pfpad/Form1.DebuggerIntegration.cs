using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using MyCrownJewelApp.Pfpad.Debugger;
using MyCrownJewelApp.Pfpad.Features.RoslynControl;
using Microsoft.Extensions.DependencyInjection;
namespace MyCrownJewelApp.Pfpad
{
    public partial class Form1
    {
        #region Debugger Integration

        private async void ShowDebugHoverTooltipAsync(string word, Point mouseLoc)
        {
            try
            {
                string? value = await _debugSession.EvaluateAsync(word, _debugActiveFrameId, "hover");
                if (value is null) return;
                var theme = ThemeManager.Instance.CurrentTheme;
                Point screenLoc = textEditor.PointToScreen(mouseLoc);
                _hoverTooltip.Dismiss();
                _hoverTooltip.ShowAt(screenLoc, $"{word}", value, "Debugger");
            }
            catch
            {
                // Silently ignore — debugger may have moved on
            }
        }

        private void OnDebugStateChanged(DebugState state)
        {
            bool isRunning = state == DebugState.Running;
            bool isPaused = state == DebugState.Paused;
            bool isActive = isRunning || isPaused || state == DebugState.Stepping;
            bool isTerminated = state == DebugState.Terminated || state == DebugState.Terminating;

            BeginInvoke(() =>
            {
                startDebugMenuItem.Enabled = !isActive && !isTerminated;
                runWithoutDebugMenuItem.Enabled = !isActive && !isTerminated;
                stopDebugMenuItem.Enabled = isActive;
                restartDebugMenuItem.Enabled = isActive;
                continueDebugMenuItem.Enabled = isPaused;
                stepOverMenuItem.Enabled = isPaused;
                stepIntoMenuItem.Enabled = isPaused;
                stepOutMenuItem.Enabled = isPaused;

                if (isTerminated)
                {
                    _debugActiveLine = -1;
                    _debugActiveFile = null;
                    _debugActiveFrameId = 0;
                    _debugVariablesPanel?.Close();
                    _debugVariablesPanel = null;
                    _debugCallStackPanel?.Close();
                    _debugCallStackPanel = null;
                    gutterPanel?.Invalidate();
                }
            });
        }

        private async void OnDebugThreadStopped(int threadId, string reason)
        {
            _debugActiveLine = -1;
            _debugActiveFile = null;

            var frames = await _debugSession.GetStackTraceAsync(threadId);
            if (frames is { Length: > 0 })
            {
                var top = frames[0];
                _debugActiveLine = top.Line;
                _debugActiveFile = top.Source?.Path;
                _debugActiveFrameId = top.Id;

                if (_debugActiveFile != null && File.Exists(_debugActiveFile))
                    NavigateToFileLine(_debugActiveFile, _debugActiveLine);

                gutterPanel?.Invalidate();
            }

            BeginInvoke(async () =>
            {
                if (_debugVariablesPanel == null || _debugVariablesPanel.IsDisposed)
                {
                    _debugVariablesPanel = new DebugVariablesPanel(_debugSession);
                    _debugVariablesPanel.Show(this);
                    _debugVariablesPanel.Location = new Point(Right - 360, Top + 200);
                }
                else
                {
                    _debugVariablesPanel.BringToFront();
                }

                if (_debugCallStackPanel == null || _debugCallStackPanel.IsDisposed)
                {
                    _debugCallStackPanel = new DebugCallStackPanel(_debugSession, this);
                    _debugCallStackPanel.Show(this);
                    _debugCallStackPanel.Location = new Point(Right - 760, Top + 60);
                }
                else
                {
                    _debugCallStackPanel.BringToFront();
                }

                if (frames is { Length: > 0 })
                {
                    await _debugVariablesPanel.RefreshAsync(frames[0].Id);
                    await _debugCallStackPanel.RefreshAsync(threadId);
                }
            });
        }

        private async void StartDebug_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Open a file first to start debugging.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? projectDir = ProjectLocator.FindProjectDirectory(currentFilePath);
            if (projectDir == null)
            {
                ThemedMessageBox.Show("Could not find a .csproj file. Open a file that is part of a .NET project.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? outputDll = ProjectLocator.FindOutputAssembly(projectDir);
            if (outputDll == null)
            {
                var result = ThemedMessageBox.Show("No build output found. Build the project first?",
                    "Debugger", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    RunDotnetBuild(projectDir);
                    outputDll = ProjectLocator.FindOutputAssembly(projectDir);
                }
                if (outputDll == null)
                {
                    ThemedMessageBox.Show("Build output not found. Build the project and try again.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            string? error = await _debugSession.StartAsync(outputDll!, projectDir, settings: CurrentDebugSettings());
            if (error != null)
            {
                ThemedMessageBox.Show(error, "Debugger Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string? fileForBp = currentFilePath;
            if (fileForBp != null)
            {
                var bps = _breakpointManager.GetDapBreakpoints(fileForBp);
                await _debugSession.SetBreakpointsAsync(fileForBp, bps);
            }

            await _debugSession.ConfigurationDoneAsync();
            ShowNotification("Debug", $"Debug session started: {Path.GetFileName(outputDll)}");
        }

        private void StopDebug_Click(object? sender, EventArgs e)
        {
            _debugSession.Dispose();
            OnDebugStateChanged(DebugState.Terminated);
            ShowNotification("Debug", "Debug session stopped.");
        }

        private async void DebugContinue_Click(object? sender, EventArgs e)
        {
            await _debugSession.ContinueAsync();
        }

        private async void StepOver_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepOverAsync();
        }

        private async void StepInto_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepInAsync();
        }

        private async void StepOut_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepOutAsync();
        }

        private void ToggleBreakpointMenu_Click(object? sender, EventArgs e)
        {
            if (currentFilePath == null) return;
            int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            _breakpointManager.ToggleBreakpoint(currentFilePath, line);
        }

        private void RunWithoutDebug_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Open a file first.", "Run", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? projectDir = ProjectLocator.FindProjectDirectory(currentFilePath);
            if (projectDir == null)
            {
                ThemedMessageBox.Show("Could not find a .csproj file.", "Run", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? outputDll = ProjectLocator.FindOutputAssembly(projectDir);
            if (outputDll == null)
            {
                var result = ThemedMessageBox.Show("No build output found. Build the project first?",
                    "Run", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    RunDotnetBuild(projectDir);
                    outputDll = ProjectLocator.FindOutputAssembly(projectDir);
                }
                if (outputDll == null) return;
            }

            try
            {
                // Run in terminal panel instead of separate window
                var terminal = ActiveTerminal ?? AddTerminalTab(null);
                terminal.ClearOutput();
                var projectName = Path.GetFileName(projectDir);
                if (_terminalTabControl?.SelectedTab is TabPage tab)
                    tab.Text = projectName;
                ShowTerminal();
                string runCmd = ResolveRunCommand();
                terminal.SendInput($"{runCmd} --project \"{projectDir}\"\r\n");
                ShowNotification("Run", $"Started: {projectName}");
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to start: {ex.Message}", "Run Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void RestartDebug_Click(object? sender, EventArgs e)
        {
            _debugSession.Dispose();
            OnDebugStateChanged(DebugState.Terminated);
            await Task.Delay(200);
            StartDebug_Click(sender, e);
        }

        private void NewBreakpoint_Click(object? sender, EventArgs e)
        {
            if (currentFilePath == null) return;
            string defaultLine = (textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1).ToString();
            string? input = SimpleInputDialog.Show(this, "Enter line number:", "New Breakpoint", defaultLine);
            if (input != null && int.TryParse(input, out int line) && line > 0)
            {
                _breakpointManager.ToggleBreakpoint(currentFilePath, line);
                gutterPanel?.Invalidate();
            }
        }

        private void EnableAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.EnableAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints enabled.");
        }

        private void DisableAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.DisableAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints disabled.");
        }

        private void RemoveAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.ClearAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints removed.");
        }

        private void RunDotnetBuild(string projectDir)
        {
            try
            {
                string cmd = ResolveBuildCommand();
                string exe = cmd.Split(' ')[0];
                string args = cmd.Length > exe.Length ? cmd[(exe.Length + 1)..] : "";
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(30000);
            }
            catch { }
        }

        #endregion
    }
}

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
        #region Edit Menu Handlers

        private void Undo_Click(object? sender, EventArgs e)
        {
            if (textEditor.CanUndo) 
            {
                textEditor.Undo();
                CheckIfClean(); // May clear dirty if undo restored to saved state
            }
        }

        private void Cut_Click(object? sender, EventArgs e)
        {
            if (textEditor.SelectionLength > 0) textEditor.Cut();
        }

        private void Copy_Click(object? sender, EventArgs e)
        {
            if (textEditor.SelectionLength > 0) textEditor.Copy();
        }

        private void Paste_Click(object? sender, EventArgs e)
        {
            if (Clipboard.ContainsText()) textEditor.Paste();
        }

        private void Delete_Click(object? sender, EventArgs e)
        {
            if (textEditor.SelectionLength > 0)
            {
                textEditor.SelectedText = "";
            }
            else if (textEditor.SelectionStart < textEditor.TextLength)
            {
                // Delete the character after the caret (forward delete)
                int start = textEditor.SelectionStart;
                textEditor.SelectionStart = start;
                textEditor.SelectionLength = 1;
                textEditor.SelectedText = "";
                // Restore caret position at the original start (after deletion)
                textEditor.SelectionStart = start;
            }
        }

        private void SelectAll_Click(object? sender, EventArgs e)
        {
            textEditor.SelectAll();
        }

        private void TimeDate_Click(object? sender, EventArgs e)
        {
            string time = DateTime.Now.ToString("HH:mm:ss yyyy-MM-dd");
            int pos = textEditor.SelectionStart;
            textEditor.Text = textEditor.Text.Insert(pos, time);
            textEditor.SelectionStart = pos + time.Length;
        }

        private void Font_Click(object? sender, EventArgs e)
        {
            using var fd = new FontDialog();
            fd.Font = textEditor.Font;
            if (fd.ShowDialog() == DialogResult.OK)
            {
                _fontService.SetFont(fd.Font);
                _fontService.ApplyFont(textEditor);
                // Trigger gutter refresh to recalc line numbers
                if (gutterPanel != null) gutterPanel.RefreshGutter();
                SaveSettings();
                
                // Recompute tab stops with new font metrics
                UpdateTabStops();
            }
        }

        private void About_Click(object? sender, EventArgs e)
        {
            using var dlg = new AboutDialog(openFileCount: documents.Count, workspaceRoot: _workspaceRoot);
            dlg.ShowDialog(this);
        }

        private void UserManual_Click(object? sender, EventArgs e)
        {
            using var dlg = new UserManualDialog();
            dlg.ShowDialog(this);
        }

        private void ShowPerformanceProfiler()
        {
            try
            {
                if (_profilerDialog == null || _profilerDialog.IsDisposed)
                {
                    _profilerDialog = new PerformanceProfilerDialog(this);
                }
                _profilerDialog.Show();
                _profilerDialog.BringToFront();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error opening Performance Profiler: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

#if PROFILING && DEBUG
        /// <summary>
        /// Signal UI activity for performance profiling.
        /// </summary>
        private void SignalUiActivity()
        {
            _debugOverlay?.OnFrame();
            _profilerDialog?._samplingEngine?.SignalUiActivity();
        }
#endif

        private void GcMonitorTimer_Tick(object? sender, EventArgs e)
        {
            var currentGC = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            if (currentGC > _lastGCCollections)
            {
                _profilerDialog?.Log($"GC Activity: {currentGC - _lastGCCollections} collections occurred");
                _lastGCCollections = currentGC;
            }
        }

        // External tools
        private List<ExternalTool> _externalTools = new();
        private ToolStripItem? _externalToolsSeparator;
        private readonly List<ToolStripMenuItem> _externalToolMenuItems = new();

        // Find/Replace state
        private string _lastFindText = "";
        private bool _lastFindCaseSensitive;
        private bool _lastFindUp;
        private bool _lastUseRegex;
        private string _lastReplaceText = "";

        // Find settings
        private bool _findCaseSensitive = false;
        private bool _findUseRegex = false;
        private bool _findWrapAround = true;
        private bool _findSeedFromSelection = true;
        private string _findNotFoundNotification = "messagebox";
        private string _findInFilesFilter = "*.cs, *.ts, *.js, *.json, *.md, *.xml, *.yaml, *.html, *.css, *.py, *.go, *.rs, *.tf, *.ps1, *.sh";
        private string _findInFilesExclude = "node_modules, .git, bin, obj, .vs, packages, .terraform";
        private int _findInFilesMaxResults = 5000;

        private void ConfigureTools_Click(object? sender, EventArgs e)
        {
            using var dlg = new ExternalToolsConfigDialog(_externalTools);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RebuildExternalToolsMenu();
                SaveSettings();
            }
        }

        private void Settings_Click(object? sender, EventArgs e)
        {
            using var dlg = new SettingsDialog(this);
            dlg.ShowDialog(this);
        }

        private void OpenSettingsSecurity(object? sender, EventArgs e)
        {
            using var dlg = new SettingsDialog(this, "application.security");
            dlg.ShowDialog(this);
        }

        private void RebuildExternalToolsMenu()
        {
            foreach (var item in _externalToolMenuItems)
                toolsMenu.DropDownItems.Remove(item);
            _externalToolMenuItems.Clear();

            if (_externalToolsSeparator != null)
            {
                toolsMenu.DropDownItems.Remove(_externalToolsSeparator);
                _externalToolsSeparator = null;
            }

            if (_externalTools.Count == 0)
                return;

            _externalToolsSeparator = new ToolStripSeparator();
            toolsMenu.DropDownItems.Add(_externalToolsSeparator);

            for (int i = 0; i < _externalTools.Count; i++)
            {
                var tool = _externalTools[i];
                int index = i;
                var item = new ToolStripMenuItem(tool.Name, null, (s, e) => RunExternalTool(index));
                if (index < 9)
                    item.ShortcutKeys = Keys.Control | Keys.Alt | Keys.Shift | (Keys.D1 + index);
                _externalToolMenuItems.Add(item);
                toolsMenu.DropDownItems.Add(item);
            }
        }

        private void RunExternalTool(int index)
        {
            if (index < 0 || index >= _externalTools.Count) return;
            var tool = _externalTools[index];

            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                ThemedMessageBox.Show($"Tool \"{tool.Name}\" has no command configured.", "External Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_secConfirmExternalTools)
            {
                if (ThemedMessageBox.Show(
                    $"Run external tool \"{tool.Name}\"?\n\nCommand: {tool.Command}",
                    "External Tool", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            string args = tool.Arguments;
            if (tool.PromptForArguments)
            {
                string? input = SimpleInputDialog.Show(this, $"Arguments for \"{tool.Name}\":", "External Tool", args);
                if (input == null) return;
                args = input;
            }

            try
            {
                string resolvedCmd = ResolveVariables(tool.Command);
                string resolvedArgs = ResolveVariables(args);
                string resolvedDir = ResolveVariables(tool.InitialDirectory);

                if (string.IsNullOrEmpty(resolvedDir))
                {
                    string? filePath = ActiveDoc?.FilePath;
                    if (!string.IsNullOrEmpty(filePath))
                        resolvedDir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
                    else
                        resolvedDir = Environment.CurrentDirectory;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = resolvedCmd,
                    Arguments = resolvedArgs,
                    WorkingDirectory = resolvedDir,
                    UseShellExecute = tool.UseShellExecute
                };

                if (!tool.UseShellExecute)
                {
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                }

                using var proc = new Process();
                proc.StartInfo = psi;

                if (tool.UseShellExecute)
                {
                    proc.Start();
                }
                else
                {
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(output) || !string.IsNullOrEmpty(error))
                    {
                        string caption = $"Output: {tool.Name}";
                        string msg = "";
                        if (!string.IsNullOrEmpty(output)) msg += output;
                        if (!string.IsNullOrEmpty(error)) msg += $"\n--- stderr ---\n{error}";
                        ThemedMessageBox.Show(msg.Trim(), caption);
                    }
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error running \"{tool.Name}\": {ex.Message}", "External Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ResolveVariables(string input)
        {
            string result = input;
            string? filePath = ActiveDoc?.FilePath;
            string? fileName = filePath != null ? Path.GetFileName(filePath) : "";
            string? fileDir = filePath != null ? Path.GetDirectoryName(filePath) : "";
            string? fileExt = filePath != null ? Path.GetExtension(filePath) : "";
            string? fileNameNoExt = filePath != null ? Path.GetFileNameWithoutExtension(filePath) : "";
            string selText = textEditor?.SelectedText ?? "";
            int curLine = textEditor != null ? textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1 : 1;
            int curCol = textEditor != null ? textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)) + 1 : 1;

            result = result.Replace("$(FilePath)", filePath ?? "");
            result = result.Replace("$(FileDir)", fileDir ?? "");
            result = result.Replace("$(FileName)", fileName ?? "");
            result = result.Replace("$(FileNameNoExt)", fileNameNoExt ?? "");
            result = result.Replace("$(FileExt)", fileExt ?? "");
            result = result.Replace("$(SelText)", selText);
            result = result.Replace("$(CurLine)", curLine.ToString());
            result = result.Replace("$(CurCol)", curCol.ToString());

            return result;
        }

        private void ToggleWorkspace()
        {
            _workspaceVisible = !_workspaceVisible;
            workspaceMenuItem.Checked = _workspaceVisible;
            if (_workspaceVisible)
            {
                if (_workspaceSplitContainer != null && !string.IsNullOrEmpty(_workspaceRoot))
                {
                    Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
                }
            }
            else
            {
                if (_workspaceSplitContainer != null)
                    _workspaceWidth = Math.Max(80, Math.Min(600, _workspaceSplitContainer.SplitterDistance));

                // Only return focus to editor if no AIOps panels are keeping the sidebar open.
                if (!AnyAIOpsPanelVisible)
                    textEditor?.Focus();
            }

            // Let UpdateSidebarLayout decide what to collapse — AIOps panels may still need the sidebar.
            UpdateSidebarLayout();
            SaveSettings();
        }

        private void ToggleSolutionExplorer()
        {
            _workspaceVisible = true;
            workspaceMenuItem.Checked = true;
            if (_workspaceSplitContainer != null)
            {
                _workspaceSplitContainer.Panel1Collapsed = false;
                _workspaceSplitContainer.SplitterDistance = _workspaceWidth;
                _workspaceSplitContainer.PerformLayout();
            }
            // If already showing Solution tab, toggle back to Explorer; otherwise show Solution
            SetSideTab(_sideTabIndex == 1 ? 0 : 1);
            SaveSettings();
        }

        private void SideTabHeader_Paint(object? sender, PaintEventArgs e)
        {
            if (_sideTabHeader == null) return;
            var g = e.Graphics;
            var t = _themeManager.CurrentTheme;
            g.Clear(t.MenuBackground);

            string[] labels = ["Explorer", "Solution"];
            float tabW = _sideTabHeader.Width / (float)labels.Length;
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var accentBrush = new SolidBrush(Color.FromArgb(0, 120, 212));

            for (int i = 0; i < labels.Length; i++)
            {
                bool active = i == _sideTabIndex;
                var rect = new RectangleF(i * tabW, 0, tabW, _sideTabHeader.Height - 1f);
                using var fg = new SolidBrush(active ? t.Text : t.Muted);
                g.DrawString(labels[i], _sideTabHeader.Font, fg, rect, sf);
                if (active)
                    g.FillRectangle(accentBrush, i * tabW, _sideTabHeader.Height - 2f, tabW, 2f);
            }

            using var sepPen = new Pen(Color.FromArgb(60, t.Muted), 1f);
            g.DrawLine(sepPen, 0, _sideTabHeader.Height - 1, _sideTabHeader.Width, _sideTabHeader.Height - 1);
        }

        private void SideTabHeader_MouseClick(object? sender, MouseEventArgs e)
        {
            if (_sideTabHeader == null) return;
            int newIndex = e.X < _sideTabHeader.Width / 2 ? 0 : 1;
            if (newIndex != _sideTabIndex)
                SetSideTab(newIndex);
        }

        private void SetSideTab(int index)
        {
            _sideTabIndex = index;
            if (_sideContentArea == null) return;

            // Hide whichever panel is being removed before clearing parent
            foreach (Control c in _sideContentArea.Controls)
                c.Visible = false;

            _sideContentArea.Controls.Clear();

            Control? panel = index == 0 ? (Control?)_workspacePanel : _solutionExplorerPanel;
            if (panel != null)
            {
                panel.Dock = DockStyle.Fill;
                panel.Visible = true;
                _sideContentArea.Controls.Add(panel);
            }
            _sideTabHeader?.Invalidate();
        }

        private void ToggleWorkspace_Click(object? sender, EventArgs e)
        {
            ToggleWorkspace();
        }

        private void OpenFolder_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to open as workspace",
                UseDescriptionForTitle = true,
                SelectedPath = _workspaceRoot
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                 // Cancel any in-flight scan and symbol rebuild
                 _workspacePanel?.CancelScan();
                 try
                 {
                     _symbolIndexCts?.Cancel();
                 }
                 catch (ObjectDisposedException)
                 {
                     // Ignore if already disposed
                 }
                  _symbolIndexCts = new CancellationTokenSource();
                var token = _symbolIndexCts.Token;
                _workspaceRoot = dlg.SelectedPath;
                Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
                AddToRecentWorkspaces(_workspaceRoot);
                _ = _symbolIndex.RebuildIndexAsync(_workspaceRoot);
                if (!_workspaceVisible)
                    ToggleWorkspace();
                else
                    SaveSettings();
            }
        }

        internal void OpenWorkspaceFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            _workspacePanel?.CancelScan();
            try
            {
                _symbolIndexCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            _symbolIndexCts = new CancellationTokenSource();
            var token = _symbolIndexCts.Token;
            _workspaceRoot = path;
            _workspacePanel?.SetRoot(_workspaceRoot);
            AddToRecentWorkspaces(_workspaceRoot);
            UpdateWorkspaceProjectLabel();
            Task.Run(() =>
            {
                if (!token.IsCancellationRequested)
                    _ = _symbolIndex.RebuildIndexAsync(_workspaceRoot);
            }, token);
            if (!_workspaceVisible)
                ToggleWorkspace();
            else
                SaveSettings();

            // Navigate all running terminal tabs to the new folder so the terminal
            // is immediately in context with the opened project.
            foreach (var terminal in _terminalTabs)
                terminal.ChangeDirectory(path);

            if (_terminalTabs.Any(t => t.IsRunning))
                SetStatus($"Terminal navigated to: {path}");
        }

        private void OnWorkspaceScanStarted()
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(OnWorkspaceScanStarted); return; }
            scanProgressBar.Visible = true;
            scanProgressBar.Value = 0;
            scanProgressBar.Maximum = 100;
            _originalStatusBarColor = statusStrip.BackColor;
            statusStrip.BackColor = _themeManager.CurrentTheme.Accent.IsEmpty
                ? Color.DodgerBlue
                : Color.FromArgb(_themeManager.CurrentTheme.Accent.R, _themeManager.CurrentTheme.Accent.G, _themeManager.CurrentTheme.Accent.B);
        }

        private void OnWorkspaceScanCompleted()
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(OnWorkspaceScanCompleted); return; }
            scanProgressBar.Visible = false;
            scanProgressBar.Value = 0;
            statusStrip.BackColor = _originalStatusBarColor;
            DetectAndSuggestProfile();
            ShowNotification("Workspace", "Folder scan complete");
        }

        private void DetectAndSuggestProfile()
        {
            try
            {
                // Only auto-suggest if the user is still on the Default profile
                // (not if they explicitly selected a custom profile)
                if (_currentProfile.Name != "Default") return;
                if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot)) return;

                string? detected = WorkspaceHelper.DetectLanguage(_workspaceRoot);
                if (detected == null) return;

                // Try to find an existing profile whose name contains the detected language
                string matchName = detected.ToLowerInvariant();
                foreach (string pname in _profileManager.ProfileNames)
                {
                    if (pname == "Default") continue;
                    if (pname.ToLowerInvariant().Contains(matchName))
                    {
                        var profile = _profileManager.LoadProfile(pname);
                        if (profile != null)
                        {
                            ApplyProfile(profile);
                            ShowNotification("Profile", $"Auto-switched to profile: {pname}");
                            return;
                        }
                    }
                }

                // No matching profile found — suggest creating one
                ShowNotification("Profile", $"Detected {detected} project — create a profile from File > Preferences > Profile");
            }
            catch { }
        }

        private void UpdateWorkspaceProjectLabel()
        {
            _detectedProjectPath = null;
            _detectedSolutionPath = null;

            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                _workspaceProjectLabel.Text = "";
                _workspaceProjectLabel.ToolTipText = "";
                _workspaceProjectLabel.Visible = false;
                return;
            }

            try
            {
                var sln = Directory.EnumerateFiles(_workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (sln != null)
                {
                    _detectedSolutionPath = sln;
                    _workspaceProjectLabel.Text = Path.GetFileNameWithoutExtension(sln);
                    _workspaceProjectLabel.ToolTipText = sln;
                    _workspaceProjectLabel.Visible = true;
                    return;
                }

                var csproj = Directory.EnumerateFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
                if (csproj != null)
                {
                    _detectedProjectPath = csproj;
                    _workspaceProjectLabel.Text = Path.GetFileNameWithoutExtension(csproj);
                    _workspaceProjectLabel.ToolTipText = csproj;
                    _workspaceProjectLabel.Visible = true;
                    return;
                }

                _workspaceProjectLabel.Text = Path.GetFileName(_workspaceRoot);
                _workspaceProjectLabel.ToolTipText = _workspaceRoot;
                _workspaceProjectLabel.Visible = true;
            }
            catch
            {
                _workspaceProjectLabel.Text = Path.GetFileName(_workspaceRoot);
                _workspaceProjectLabel.ToolTipText = _workspaceRoot;
                _workspaceProjectLabel.Visible = true;
            }
        }

        private void WorkspaceProjectLabel_Click(object? sender, EventArgs e)
        {
            string? target = _detectedSolutionPath ?? _detectedProjectPath;
            if (target != null)
            {
                string dir = Path.GetDirectoryName(target)!;
                try { Process.Start("explorer.exe", dir); } catch { }
            }
            else if (!string.IsNullOrEmpty(_workspaceRoot))
            {
                try { Process.Start("explorer.exe", _workspaceRoot); } catch { }
            }
        }

        private void OnWorkspaceScanProgressChanged(string message)
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(() => OnWorkspaceScanProgressChanged(message)); return; }
            // Extract percentage from message like "Scanning (45%)..."
            if (message is not null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)%");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int pct))
                {
                    scanProgressBar.Value = Math.Min(pct, 100);
                }
            }
        }

        #endregion
    }
}

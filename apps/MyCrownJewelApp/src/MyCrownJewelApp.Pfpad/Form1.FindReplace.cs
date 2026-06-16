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
        #region Find & Replace

        private void Find_Click(object? sender, EventArgs e)
        {
            string initialFind = _findSeedFromSelection
                && textEditor?.SelectionLength > 0
                && textEditor.SelectionLength <= 200
                ? textEditor.SelectedText
                : _lastFindText;
            using var dlg = new FindReplaceDialog(this, false, initialFind,
                _lastFindCaseSensitive, _lastUseRegex, _findWrapAround);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lastFindText = dlg.FindText;
                _lastFindCaseSensitive = dlg.CaseSensitive;
                _lastFindUp = dlg.SearchUp;
                _lastUseRegex = dlg.UseRegex;
            }
        }

        private void FindNext_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFindText))
                Find_Click(sender, e);
            else
                PerformFind(_lastFindText, _lastFindCaseSensitive, false, _lastUseRegex);
        }

        private void FindPrevious_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFindText))
                Find_Click(sender, e);
            else
                PerformFind(_lastFindText, _lastFindCaseSensitive, true, _lastUseRegex);
        }

        private void Replace_Click(object? sender, EventArgs e)
        {
            string initialFind = _findSeedFromSelection
                && textEditor?.SelectionLength > 0
                && textEditor.SelectionLength <= 200
                ? textEditor.SelectedText
                : _lastFindText;
            using var dlg = new FindReplaceDialog(this, true, initialFind,
                _lastFindCaseSensitive, _lastUseRegex, _findWrapAround);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lastFindText = dlg.FindText;
                _lastFindCaseSensitive = dlg.CaseSensitive;
                _lastFindUp = dlg.SearchUp;
                _lastUseRegex = dlg.UseRegex;
                _lastReplaceText = dlg.ReplaceText;
            }
        }

        private void Goto_Click(object? sender, EventArgs e)
        {
            using var dlg = new GoToDialog(this);
            dlg.ShowDialog(this);
        }

        private void FormatDocument_Click(object? sender, EventArgs e) { FormatDocumentAsync(); }
        private void ShowQuickOpen() { using var dlg = new QuickOpenDialog(this); dlg.ShowDialog(this); }

        internal List<CommandPaletteForm.CommandEntry> GetCommandPaletteCommands()
        {
            return new List<CommandPaletteForm.CommandEntry>
            {
                // File operations
                new("New File", "Ctrl+N", () => NewFile()),
                new("Open File", "Ctrl+O", () => OpenFile()),
                new("Save", "Ctrl+S", () => SaveCurrentDocument()),
                new("Save All", "Ctrl+Shift+S", () => SaveAllFiles()),
                new("Close Tab", "Ctrl+W", () => CloseCurrentTab()),
                new("Close Split", "Ctrl+Shift+W", () => CloseSplit()),
                new("Reopen Closed Tab", "Ctrl+Shift+T", () => ReopenLastClosedTab()),
                new("Close All Tabs", "", () => CloseAllTabs()),
                new("Toggle Header/Source", "Alt+O", () => ToggleHeaderSource()),
                new("Exit", "Alt+F4", () => Close()),

                // Edit operations
                new("Find", "Ctrl+F", () => { using var dlg = new FindReplaceDialog(this, false, _lastFindText, _lastFindCaseSensitive, _lastUseRegex); dlg.ShowDialog(this); }),
                new("Replace", "Ctrl+H", () => { using var dlg = new FindReplaceDialog(this, true, _lastFindText, _lastFindCaseSensitive, _lastUseRegex); dlg.ShowDialog(this); }),
                new("Find in Files", "Ctrl+Shift+F", () => { using var dlg = new GlobalSearchDialog(this); dlg.ShowDialog(this); }),
                new("Go to Line", "Ctrl+G", () => { using var dlg = new GoToDialog(this); dlg.ShowDialog(this); }),
                new("Go to Definition", "F12", () => GoToDefinition()),
                new("Rename Symbol", "F2", () => RenameSymbol()),
                new("Format EditorDocument", "Ctrl+Shift+I", () => FormatDocumentAsync()),

                // View operations
                new("Toggle Terminal", "Ctrl+`", () => ToggleTerminal()),
                new("Toggle Workspace", "", () => ToggleWorkspace()),
                new("Toggle Git Panel", "", () => ToggleGitPanel()),
                new("Toggle Symbols", "", () => ToggleSymbols()),
                new("Toggle Problems", "", () => ToggleProblems()),
                new("Toggle Minimap", "", () => ToggleMinimap()),
                new("Toggle Word Wrap", "", () => ToggleWordWrap()),
                new("Toggle Status Bar", "", () => ToggleStatusBar()),
                new("Toggle Syntax Highlighting", "", () => ToggleSyntaxHighlighting()),

                // Run/Debug operations
                new("Start Debugging", "F5", () => StartDebug_Click(null, EventArgs.Empty)),
                new("Run Without Debugging", "Ctrl+F5", () => RunWithoutDebug_Click(null, EventArgs.Empty)),
                new("Stop Debugging", "Shift+F5", () => StopDebug_Click(null, EventArgs.Empty)),
                new("Restart Debugging", "Ctrl+Shift+F5", () => RestartDebug_Click(null, EventArgs.Empty)),
                new("Run Tests", "Ctrl+R, T", () => RunTests()),
                new("Run Tests with Coverage", "", () => RunTestsWithCoverage()),
                new("Build Project", "", () => { string? projDir = GetProjectDirectory(); if (projDir != null) RunDotnetBuild(projDir); }),

                // Tools operations
                new("Settings", "Ctrl+,", () => { using var dlg = new SettingsDialog(this); dlg.ShowDialog(this); }),
                new("Configure External Tools", "Ctrl+Alt+T", () => ConfigureTools()),
                new("Performance Profiler", "", () => ShowPerformanceProfiler()),
                new("Restart Roslyn Analyzers", "Ctrl+Shift+R", () => RestartRoslynAnalyzers()),
                new("Toggle Roslyn Analyzers", "Ctrl+Alt+A", () => ToggleRoslynAnalyzers()),
                new("Open Roslyn Visualizer", "Ctrl+Alt+V", () => OpenRoslynVisualizer()),

                // Help operations
                new("About", "", () => { using var dlg = new AboutDialog(); dlg.ShowDialog(this); }),
            };
        }

        public record OpenTabInfo(string Title, string FilePath, Action Switch);

        internal List<OpenTabInfo> GetOpenTabs()
        {
            var tabs = new List<OpenTabInfo>();
            if (tabControl == null) return tabs;
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var page = tabControl.TabPages[i];
                if (page.Tag is EditorDocument doc)
                {
                    tabs.Add(new OpenTabInfo(page.Text, doc.FilePath ?? "", () => tabControl.SelectedIndex = i));
                }
            }
            return tabs;
        }

        internal EditorDocument? GetCurrentDocument()
        {
            return textEditor?.Tag as EditorDocument;
        }

        private string? GetProjectDirectory()
        {
            var projectDir = Path.GetDirectoryName(ActiveDoc?.FilePath);
            if (projectDir == null) return null;

            // Find .csproj file
            while (!string.IsNullOrEmpty(projectDir))
            {
                if (Directory.GetFiles(projectDir, "*.csproj").Length > 0)
                    return projectDir;
                projectDir = Path.GetDirectoryName(projectDir);
            }
            return null;
        }

        private void RenameSymbol()
        {
            if (textEditor is null) return;
            string? word = GetWordAtCursor();
            if (string.IsNullOrEmpty(word)) return;

            using var dlg = new RenameDialog(word, _workspaceRoot);
            if (_roslynWorkspace.IsReady)
                dlg.SetRoslynWorkspace(_roslynWorkspace, textEditor.SelectionStart);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Applied)
            {
                if (textEditor.Text.Contains(dlg.NewName))
                    ShowNotification("Rename", $"Renamed '{word}' to '{dlg.NewName}'");
            }
        }

        private void Rename_Click(object? sender, EventArgs e)
        {
            RenameSymbol();
        }

        private void CallHierarchy_Click(object? sender, EventArgs e)
        {
            ShowCallHierarchy();
        }

        private void RunCoverage_Click(object? sender, EventArgs e)
        {
            RunTestsWithCoverage();
        }

        private void ScanTODOs_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Task List", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Run TODO scan on background thread
            ShowNotification("Task List", "Scanning for TODOs...");
            string root = _workspaceRoot;
            Task.Run(() =>
            {
                try
                {
                    var todos = TodoScanner.ScanWorkspace(root);
                    BeginInvoke(() =>
                    {
                        try
                        {
                            // Merge with existing diagnostics
                            var existing = _problemsPanel?.GetDiagnostics() ?? new List<Diagnostic>();
                            var merged = new List<Diagnostic>();
                            merged.AddRange(existing);
                            merged.AddRange(todos);
                            merged.Sort((a, b) => { int c = a.Line.CompareTo(b.Line); return c != 0 ? c : a.Column.CompareTo(b.Column); });

                            var allActions = _quickActionProvider.GetActions("", merged);
                            var gutterActions = allActions.Select(a => (a.Line, a.Title, a.Apply)).ToList();
                            _problemsPanel?.SetDiagnostics(merged);
                            gutterPanel?.SetQuickActions(gutterActions);

                            double pct = merged.Count > 0 ? (double)todos.Count / merged.Count * 100 : 0;
                            ShowNotification("Task List", $"Found {todos.Count} TODO(s) — {merged.Count} total issues");
                        }
                        catch (Exception ex)
                        {
                            ShowNotification("Task List", $"Error: {ex.Message}");
                        }
                    });
                }
                catch
                {
                    BeginInvoke(() => ShowNotification("Task List", "Scan failed"));
                }
            });
        }

        private void LoadCoverage_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Open Coverage File",
                Filter = "Cobertura XML (*.cobertura.xml;*.xml)|*.cobertura.xml;*.xml|All files (*.*)|*.*",
                DefaultExt = ".xml"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                LoadCoverageFile(dlg.FileName);
        }

        private void Dependencies_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Dependencies", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Dependencies", "Scanning project dependencies...");
            try
            {
                var projects = ProjectDependencyAnalyzer.Analyze(_workspaceRoot);
                using var dlg = new DependencyGraphDialog(projects);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(
                    $"Dependency analysis failed:\n{ex.Message}",
                    "Dependencies Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ImpactAnalysis_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Save the current file first, then run impact analysis.",
                    "Impact Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Impact Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Impact", "Analyzing dependencies...");
            try
            {
                var projects = ProjectDependencyAnalyzer.Analyze(_workspaceRoot);
                var affected = ProjectDependencyAnalyzer.FindAffectedFiles(currentFilePath, projects);
                using var dlg = new ImpactAnalysisDialog(currentFilePath, affected);
                dlg.FileSelected += (file) => BeginInvoke(() =>
                {
                    if (File.Exists(file)) OpenFileInNewTab(file);
                });
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(
                    $"Impact analysis failed:\n{ex.Message}",
                    "Impact Analysis Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowDependents()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Save the current file first.", "Find Dependents",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ImpactAnalysis_Click(null, EventArgs.Empty);
        }

        private void RunTestsWithCoverage()
        {
            string? testProj = FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found in the workspace folder.\n\nEnsure a .csproj with a test SDK reference exists.",
                    "Run Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Coverage", "Running tests with coverage...");
            var result = CoverageParser.RunTestsWithCoverage(testProj);
            if (result == null || result.FileLineHits.Count == 0)
            {
                ShowNotification("Coverage", "No coverage data generated");
                return;
            }

            ApplyCoverageResult(result);
        }

        private void LoadCoverageFile(string path)
        {
            var result = CoverageParser.ParseFile(path);
            if (result == null || result.FileLineHits.Count == 0)
            {
                ThemedMessageBox.Show("Could not parse coverage file or no data found.", "Load Coverage",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ApplyCoverageResult(result);
        }

        private void ApplyCoverageResult(CoverageParser.CoverageResult result)
        {
            double pct = result.LinesValid > 0 ? (double)result.LinesCovered / result.LinesValid * 100 : 0;

            // Apply to gutter for current file
            if (currentFilePath != null && result.FileLineHits.TryGetValue(currentFilePath, out var lineHits))
                gutterPanel?.SetCoverage(lineHits);

            // Show summary
            using var dlg = new CoverageSummaryForm(result);
            dlg.FileSelected += (file) => BeginInvoke(() =>
            {
                if (File.Exists(file))
                    OpenFileInNewTab(file);
            });
            dlg.ShowDialog(this);

            ShowNotification("Coverage", $"{pct:F1}% — {result.LinesCovered}/{result.LinesValid} lines");
        }

        private string? FindTestProject()
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                return null;

            try
            {
                var csprojFiles = Directory.EnumerateFiles(_workspaceRoot, "*Tests.csproj", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(_workspaceRoot, "*Test.csproj", SearchOption.AllDirectories))
                    .ToList();
                return csprojFiles.FirstOrDefault();
            }
            catch { return null; }
        }

        private void RunTests_Click(object? sender, EventArgs e)
        {
            RunTests();
        }

        private void RerunFailedTests_Click203(object? sender, EventArgs e)
        {
            RerunFailedTests();
        }

        private void RunTests()
        {
            if (_testsRunning)
            {
                ShowNotification("Tests", "Tests are already running");
                return;
            }

            string? testProj = FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found in the workspace folder.",
                    "Run Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lastTestProject = testProj;
            _testsRunning = true;
            ShowNotification("Tests", "Running tests...");

            string proj = testProj;
            Task.Run(() =>
            {
                var result = TestResultParser.RunTests(proj);
                BeginInvoke(() =>
                {
                    _testsRunning = false;
                    _lastTestResult = result;
                    
                    if (result.Total == 0 && string.IsNullOrEmpty(result.RawOutput))
                    {
                        ShowNotification("Tests", "Test run failed or produced no results");
                        return;
                    }

                    using var dlg = new TestResultsDialog(result);
                    dlg.FrameSelected += (file, line) => BeginInvoke(() =>
                    {
                        if (File.Exists(file))
                        {
                            OpenFileInNewTab(file);
                            GoToLine(line);
                        }
                    });
                    dlg.ShowDialog(this);

                    string status = result.Failed > 0
                        ? $"{result.Failed} failed, {result.Passed} passed"
                        : $"{result.Passed} passed";
                    if (result.Skipped > 0) status += $", {result.Skipped} skipped";
                    ShowNotification("Tests", status);
                });
            });
        }

        private void RerunFailedTests()
        {
            if (_testsRunning)
            {
                ShowNotification("Tests", "Tests are already running");
                return;
            }

            string? testProj = _lastTestProject ?? FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found.",
                    "Rerun Failed Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _testsRunning = true;
            ShowNotification("Tests", "Rerunning failed tests...");

            string proj = testProj;
            string? filter = _lastTestResult != null
                ? string.Join("|", _lastTestResult.Tests
                    .Where(t => t.Outcome == TestOutcome.Failed)
                    .Select(t => t.TestName))
                : null;

            Task.Run(() =>
            {
                string args = $"test \"{proj}\" --no-build --filter \"{filter}\"";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) { BeginInvoke(() => { _testsRunning = false; }); return; }

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300000);

                BeginInvoke(() =>
                {
                    _testsRunning = false;

                    // Parse from stdout
                    var result = new TestRunResult();
                    TestResultParser.ParseSummaryFromOutput(stdout, result);

                    // If we have a TRX, parse that too
                    string tempDir = Path.Combine(Path.GetTempPath(), "pfpad_rerun_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    string trxFile = Path.Combine(tempDir, "rerun.trx");
                    try
                    {
                        // Re-run with logger to get structured results
                        var psi2 = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"test \"{proj}\" --no-build --filter \"{filter}\" --logger \"trx;LogFileName={trxFile}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc2 = System.Diagnostics.Process.Start(psi2);
                        if (proc2 != null)
                        {
                            proc2.WaitForExit(300000);
                            if (File.Exists(trxFile))
                                result = TestResultParser.ParseTrxFile(trxFile);
                        }
                    }
                    catch { }
                    finally { try { Directory.Delete(tempDir, true); } catch { } }

                    _lastTestResult = result;

                    using var dlg = new TestResultsDialog(result);
                    dlg.FrameSelected += (file, line) => BeginInvoke(() =>
                    {
                        if (File.Exists(file))
                        {
                            OpenFileInNewTab(file);
                            GoToLine(line);
                        }
                    });
                    dlg.ShowDialog(this);

                    string status = result.Failed > 0
                        ? $"{result.Failed} failed, {result.Passed} passed"
                        : $"{result.Passed} passed";
                    ShowNotification("Tests", "Rerun: " + status);
                });
            });
        }

        private void ShowCallHierarchy()
        {
            if (textEditor is null) return;
            string? word = GetWordAtCursor();
            if (string.IsNullOrEmpty(word)) return;

            string currentFile = currentFilePath ?? "";
            using var dlg = new CallHierarchyDialog(word, _workspaceRoot, currentFile);
            dlg.OpenFileRequested += (file, line) =>
            {
                BeginInvoke(() =>
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        OpenFileInNewTab(file);
                        GoToLine(line);
                    }
                });
            };
            dlg.ShowDialog(this);
        }

        private void ParseStackTrace_Click(object? sender, EventArgs e)
        {
            ParseStackTrace();
        }

        private void ParseStackTrace()
        {
            string source = textEditor?.SelectedText ?? "";
            if (string.IsNullOrWhiteSpace(source))
                source = textEditor?.Text ?? "";

            var frames = StackTraceParser.Parse(source);
            if (frames.Count == 0)
            {
                ThemedMessageBox.Show("No stack trace frames detected in the current selection or EditorDocument.\n\nSupported formats:\n.NET: at Method() in File.cs:line 42\nJS:   at func (file.js:42:10)\nPython: File \"file.py\", line 42",
                    "Parse Stack Trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new StackTraceDialog(source);
            dlg.FrameSelected += (file, line) =>
            {
                BeginInvoke(() =>
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        OpenFileInNewTab(file);
                        GoToLine(line);
                    }
                    else
                    {
                        ThemedMessageBox.Show($"File not found:\n{file}",
                            "Stack Trace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });
            };
            dlg.ShowDialog(this);
        }

        private void GoToDefinition_Click(object? sender, EventArgs e)
        {
            GoToDefinition();
        }

        private void GoBack_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Navigation history — Back not yet implemented");
        }

        private void GoForward_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Navigation history — Forward not yet implemented");
        }

        private void GoLastEditLocation_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Last edit location tracking not yet implemented");
        }

        private void GoToFile_Click(object? sender, EventArgs e)
        {
            ShowQuickOpen();
        }

        private void GoToSymbolInWorkspace_Click(object? sender, EventArgs e)
        {
            ToggleSymbolPanel();
        }

        private void GoToSymbolInEditor_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Symbol in editor — use EditorDocument Outline panel");
        }

        private void GoToDeclaration_Click(object? sender, EventArgs e)
        {
            GoToDefinition(); // Roslyn declaration is typically same as definition
        }

        private void GoToTypeDefinition_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Go to Type Definition not yet implemented");
        }

        private void GoToImplementations_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Go to Implementations not yet implemented");
        }

        private void GoToReferences_Click(object? sender, EventArgs e)
        {
            if (activeDocIndex < 0 || string.IsNullOrEmpty(currentFilePath))
            {
                ShowNotification("Go", "Open a file first");
                return;
            }
            var word = GetWordAtCursor();
            if (!string.IsNullOrEmpty(word))
                GoToDefinitionRoslyn();
            else
                ShowNotification("Go", "No symbol at cursor");
        }

        private void GoToBracket_Click(object? sender, EventArgs e)
        {
            int pos = textEditor.SelectionStart;
            int? match = HighlightRichTextBox.FindMatchingBrace(textEditor.Text, pos);
            if (match.HasValue)
            {
                GoToLine(textEditor.GetLineFromCharIndex(match.Value) + 1);
                textEditor.SelectionStart = match.Value;
                textEditor.SelectionLength = 0;
            }
            else
            {
                ShowNotification("Go", "No matching bracket found");
            }
        }

        #endregion
    }
}

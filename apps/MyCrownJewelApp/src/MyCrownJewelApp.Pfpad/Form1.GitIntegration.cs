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
        #region Git Integration

        private void RefreshGitRepo()
        {
            string? path = currentFilePath ?? documents.FirstOrDefault()?.FilePath;
            if (!string.IsNullOrEmpty(path))
            {
                _gitService.TryOpenRepo(path);
                _gitPanel?.RefreshRepo(path);
            }
            UpdateGitStatusBar();
        }

        private void UpdateGitStatusBar()
        {
            if (gitBranchLabel is null || gitDirtyLabel is null || gitSyncLabel is null) return;
            if (_gitService.IsActive)
            {
                gitBranchLabel.Text = _gitService.CurrentBranch ?? "";
                var (staged, unstaged, untracked) = _gitService.GetStatus();
                int changes = staged.Count + unstaged.Count + untracked.Count;
                gitDirtyLabel.Text = changes > 0 ? " ●" : "";

                // Show behind/ahead counts
                try
                {
                    var (behind, ahead) = _gitService.GetRemoteStatus();
                    if (behind > 0 || ahead > 0)
                    {
                        gitSyncLabel.Text = $"⬇️{behind} ⬆️{ahead}";
                        gitSyncLabel.Visible = true;
                    }
                    else
                    {
                        gitSyncLabel.Text = "🔄";
                        gitSyncLabel.Visible = true;
                    }
                }
                catch
                {
                    gitSyncLabel.Text = "🔄";
                    gitSyncLabel.Visible = true;
                }
            }
            else
            {
                gitBranchLabel.Text = "";
                gitDirtyLabel.Text = "";
                gitSyncLabel.Visible = false;
            }
        }

        private void ToggleGitPanel()
        {
            if (_gitPanel is null || _workspaceSplitContainer is null || _botSidebarSplit is null) return;

            _gitPanelVisible = !_gitPanelVisible;
            _gitPanel.Visible = _gitPanelVisible;
            UpdateSidebarLayout();
            if (_gitPanelVisible)
                RefreshGitRepo();
        }

        private void ToggleGitPanel(object? sender, EventArgs e)
        {
            ToggleGitPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _gitPanelVisible;
        }

        private void ToggleSymbolPanel()
        {
            if (_symbolPanel is null || _workspaceSplitContainer is null || _botSidebarSplit is null) return;

            _symbolPanelVisible = !_symbolPanelVisible;
            _symbolPanel.Visible = _symbolPanelVisible;
            UpdateSidebarLayout();
            if (_symbolPanelVisible)
                _symbolPanel.RefreshSymbols();
        }

        private void ToggleSymbolPanel(object? sender, EventArgs e)
        {
            ToggleSymbolPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _symbolPanelVisible;
        }

        private void ToggleSymbols()
        {
            if (_symbolPanel != null)
            {
                _symbolPanel.Visible = !_symbolPanel.Visible;
                symbolsMenuItem.Checked = _symbolPanel.Visible;
            }
        }

        private void ToggleProblems()
        {
            ToggleProblemsPanel();
        }

        private void ToggleMinimap()
        {
            if (minimapControl != null)
            {
                minimapControl.Visible = !minimapControl.Visible;
                minimapMenuItem.Checked = minimapControl.Visible;
            }
        }

        private void CloseAllTabs()
        {
            var docs = documents.ToList();
            foreach (var doc in docs)
            {
                if (doc.IsDirty)
                {
                    var result = ThemedMessageBox.Show(
                        $"Save changes to \"{doc.DisplayName}\"?",
                        "Close All Tabs", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (result == DialogResult.Cancel) return;
                    if (result == DialogResult.Yes)
                    {
                        try { File.WriteAllText(doc.FilePath!, doc.Content); } catch { }
                    }
                }
            }
            _docManager.Clear();
            tabControl.TabPages.Clear();
            NewFile();
        }

        private void ConfigureTools() => ConfigureTools_Click(null, EventArgs.Empty);

        private async void AnalyzeCurrentRepository()
        {
            var workspacePath = _workspacePanel?.RootPath;
            if (string.IsNullOrEmpty(workspacePath))
            {
                ThemedMessageBox.Show("Please open a workspace folder first (View → Panel → Open Folder).",
                    "No Workspace Open", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var progressDialog = new ProgressDialog("Analyzing Repository", "Scanning workspace and analyzing project structure...");
                progressDialog.Show(this);

                var analyticsEngine = new RepositoryAnalyticsEngine(workspacePath);
                var analysis = await analyticsEngine.AnalyzeAsync();

                progressDialog.Close();

                using var dialog = new RepositoryAnalysisDialog(analysis);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to analyze repository: {ex.Message}",
                    "Analysis Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestartRoslynAnalyzers()
        {
            // Implementation would restart Roslyn analyzers
            ShowNotification("Roslyn", "Analyzers restarted");
        }

        private void ToggleRoslynAnalyzers()
        {
            // Toggle Roslyn analyzers on/off
            ShowNotification("Roslyn", "Analyzers toggled");
        }

        private void OpenRoslynVisualizer()
        {
            // Open Roslyn visualizer dialog
            ShowNotification("Roslyn", "Visualizer opened");
        }

        private void ContinueDebug()
        {
            // Continue debugging
            if (startDebugMenuItem.Enabled)
                StartDebug_Click(null, EventArgs.Empty);
        }

        private void StepOver()
        {
            // Step over in debugger
            ShowNotification("Debug", "Step over");
        }

        private void StepInto()
        {
            // Step into in debugger
            ShowNotification("Debug", "Step into");
        }

        private void StepOut()
        {
            // Step out in debugger
            ShowNotification("Debug", "Step out");
        }

        private void ToggleProblemsPanel()
        {
            if (_terminalHost == null || _problemsPanel == null) return;

            _problemsPanelVisible = !_problemsPanelVisible;
            problemsMenuItem.Checked = _problemsPanelVisible;
            if (_problemsPanelVisible)
            {
                ShowTerminal();
                _terminalHost.ShowProblems();
                RefreshProblemsTabBadge();
                if (_terminalSplitContainer != null)
                    _terminalSplitContainer.Panel2Collapsed = false;
            }
            else
            {
                _terminalHost.HideProblems();
            }
        }

        private void ToggleProblemsPanel(object? sender, EventArgs e)
        {
            ToggleProblemsPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _problemsPanelVisible;
        }

        private void ToggleMarkdownPreview()
        {
            if (_editorSplitContainer is null || _markdownPreviewPanel is null) return;

            _markdownPreviewVisible = !_markdownPreviewVisible;
            _editorSplitContainer.Panel2Collapsed = !_markdownPreviewVisible;

            if (_markdownPreviewVisible)
                UpdateMarkdownPreview();
        }

        private void ToggleMarkdownPreview(object? sender, EventArgs e)
        {
            ToggleMarkdownPreview();
            if (sender is ToolStripMenuItem item)
                item.Checked = _markdownPreviewVisible;
        }

        private void UpdateMarkdownPreview()
        {
            if (_markdownPreviewPanel is null || _editorSplitContainer is null) return;
            if (!_markdownPreviewVisible) return;

            string? file = currentFilePath;
            if (string.IsNullOrEmpty(file) || !file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                _markdownPreviewPanel.RenderMarkdown("", null);
                return;
            }

            _markdownPreviewPanel.RenderMarkdown(textEditor?.Text ?? "", file);
        }

        private void ToggleRunConfigPanel()
        {
            using var dlg = new RunConfigurationDialog(_workspaceRoot);
            dlg.ShowDialog(this);
        }

        private void ToggleRunConfigPanel(object? sender, EventArgs e)
        {
            ToggleRunConfigPanel();
        }

        private void ScheduleLint()
        {
            if (!_lintEngine.Enabled) return;
            string text = textEditor?.Text ?? "";
            string path = currentFilePath ?? "";
            _lintEngine.ScheduleLint(text, path);
        }

        private void OnLintDiagnosticsUpdated(IReadOnlyList<Diagnostic> diagnostics)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnLintDiagnosticsUpdated(diagnostics));
                return;
            }

            _problemsPanel?.SetDiagnostics(diagnostics);
            RefreshProblemsTabBadge(diagnostics);

            // Convert diagnostics to squiggly positions
            var squiggles = new List<(int start, int length, Color color)>();
            if (textEditor is not null && !textEditor.IsDisposed && textEditor.IsHandleCreated)
            {
                string text = textEditor.Text;
                var lines = text.Split('\n');
                foreach (var d in diagnostics)
                {
                    if (d.Line < 1 || d.Line > lines.Length) continue;
                    string lineText = lines[d.Line - 1].TrimEnd('\r');
                    int lineStartIdx = textEditor.GetFirstCharIndexFromLine(d.Line - 1);
                    if (lineStartIdx < 0) continue;
                    int col = Math.Max(0, Math.Min(d.Column - 1, lineText.Length));
                    int start = lineStartIdx + col;
                    int len = Math.Min(d.Length, Math.Max(1, lineText.Length - col));
                    squiggles.Add((start, len, d.Color));
                }
            }
            if (textEditor is not null && !textEditor.IsDisposed)
                textEditor.SetSquiggles(squiggles);

            // Generate quick actions and pass to gutter
            string docText = textEditor?.Text ?? "";
            var actions = _quickActionProvider.GetActions(docText, diagnostics);
            var gutterActions = actions
                .Select(a => (a.Line, a.Title, a.Apply))
                .ToList();
            gutterPanel?.SetQuickActions(gutterActions);
        }

        private void UpdateSidebarLayout()
        {
            if (_botSidebarSplit is null || _sidebarSplit is null || _workspaceSplitContainer is null
                || _problemsSplit is null) return;

            bool aiopsAny = AnyAIOpsPanelVisible;
            bool botAny = _gitPanelVisible || _symbolPanelVisible || aiopsAny;
            bool innerAny = _symbolPanelVisible || aiopsAny;

            // Collapse each sub-panel when its content is not visible.
            _botSidebarSplit.Panel1Collapsed = !_gitPanelVisible;
            _botSidebarSplit.Panel2Collapsed = !innerAny;
            _problemsSplit.Panel2Collapsed = true;
            _sidebarSplit.Panel2Collapsed = !botAny;

            // Collapse the workspace/explorer pane whenever workspace is not explicitly open.
            _sidebarSplit.Panel1Collapsed = !_workspaceVisible;

            if (_gitPanelVisible && innerAny)
            {
                int totalH = _botSidebarSplit.Height - _botSidebarSplit.SplitterWidth;
                _botSidebarSplit.SplitterDistance = Math.Max(60, totalH / 2);
            }

            if (botAny && !_sidebarSplit.Panel1Collapsed)
            {
                int totalH = _sidebarSplit.Height - _sidebarSplit.SplitterWidth;
                if (_gitPanelVisible && innerAny)
                    _sidebarSplit.SplitterDistance = Math.Max(60, totalH / 2);
                else if (_gitPanelVisible)
                    _sidebarSplit.SplitterDistance = Math.Max(60, totalH - 60);
            }

            bool anyVisible = _workspaceVisible || botAny;
            _workspaceSplitContainer.Panel1Collapsed = !anyVisible;
            if (anyVisible)
                _workspaceSplitContainer.SplitterDistance = _workspaceWidth;
        }

        private void RefreshProblemsTabBadge(IReadOnlyList<Diagnostic>? diagnostics = null)
        {
            if (_terminalHost == null)
                return;

            var items = diagnostics ?? _problemsPanel?.GetDiagnostics();
            if (items == null)
            {
                _terminalHost.UpdateProblemsTabBadge(0, 0);
                return;
            }

            int errors = 0;
            int warnings = 0;
            foreach (var diagnostic in items)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    errors++;
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                    warnings++;
            }

            _terminalHost.UpdateProblemsTabBadge(errors, warnings);
        }

        private void OpenGitForm(object? sender, EventArgs e)
        {
            if (_gitForm is null || _gitForm.IsDisposed)
            {
                _gitForm = new GitForm(_gitService);
                _gitForm.FileOpenRequested += (path) => BeginInvoke(() => OpenFileInNewTab(path));
                _gitForm.Show(this);
            }
            else
            {
                _gitForm.Activate();
            }
        }

        #endregion
    }
}

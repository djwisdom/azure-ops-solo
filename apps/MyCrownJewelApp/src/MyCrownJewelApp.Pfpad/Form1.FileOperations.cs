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
        #region File Operations

        internal void NewFile()
        {
            NewFile(false);
        }

        private void OpenFile()
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "All Files|*.*";
            ofd.Multiselect = false;
            if (ofd.ShowThemed() == DialogResult.OK)
            {
                OpenFileInNewTabAsync(ofd.FileName);
            }
        }

         internal void LoadFile(string path)
         {
              try
              {
                   // Suspend background workers during file load
                   elasticTabTimer?.Stop();

                 string content = File.ReadAllText(path);
                 
                 // Update fields without triggering dirty
                  textEditor.TextChanged -= TextEditor_TextChanged;
                  textEditor.Text = content;
                  textEditor.TextChanged += TextEditor_TextChanged;

                  _previousTextLength = textEditor.TextLength;

                  currentFilePath = path;
                  currentSyntax = SyntaxDefinition.GetDefinitionForFile(path);
                  OpenRoslynDocument(path);
                 if (File.Exists(path))
                     lastFileWriteTime = File.GetLastWriteTimeUtc(path);
                 
                 ClearDirtyAfterSave();
                 AddToRecentFiles(path);
                 
                 // Ensure editor starts at top: set caret at 0
                 textEditor.SelectionStart = 0;
                 
                 // Defer scrolling and highlighting until control is fully laid out
                 if (textEditor.IsHandleCreated)
                 {
                     try
                     {
                         SendMessage(textEditor.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                     }
                     catch { }
                 }
                 
                 UpdateStatusBar();
                 
                 // Recreate syntax highlighter for new file type
                 CreateIncrementalHighlighter();
                 
                 // Defer syntax highlight request until UI is idle
                 if (textEditor.IsHandleCreated)
                 {
                     try
                     {
                         this.BeginInvoke(new Action(() =>
                         {
                             try { RequestVisibleHighlight(); } catch { }
                         }));
                     }
                     catch { }
                 }

                 // Resume column guide updates after load completes
                    elasticTabTimer?.Start();
                    ForceCleanState();
            }
             catch (Exception ex)
              {
                  ThemedMessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }

        internal void SaveFile()
        {
            if (currentFilePath == null)
            {
                SaveAsFile();
            }
            else
            {
                try
                {
                    UnicodeFileHelper.WriteAllText(currentFilePath, textEditor.Text);
                    _profilerDialog?.Log($"Saved file: {currentFilePath} ({textEditor.Text.Length} chars)");
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    _docManager.GetActive()?.ModifiedLines.Clear();

                    if (_aiopsEngine?.CurrentSettings.AutoScanOnSave == true)
                        _ = ScanActiveFileForAIOpsAsync();
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal void SaveAsFile()
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Text Files|*.txt|All Files|*.*";
            sfd.FileName = currentFilePath ?? "untitled.txt";
            if (sfd.ShowThemed() == DialogResult.OK)
            {
                try
                {
                    UnicodeFileHelper.WriteAllText(sfd.FileName, textEditor.Text);
                    _profilerDialog?.Log($"Saved as file: {sfd.FileName} ({textEditor.Text.Length} chars)");
                    currentFilePath = sfd.FileName;
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    AddToRecentFiles(currentFilePath);
                    _docManager.GetActive()?.ModifiedLines.Clear();
                    UpdateStatusBar();

                    if (_aiopsEngine?.CurrentSettings.AutoScanOnSave == true)
                        _ = ScanActiveFileForAIOpsAsync();
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAllFiles()
        {
            // Single EditorDocument - same as SaveFile
            SaveFile();
        }

        internal bool IsModified()
        {
            // For now, compare with stored state; could also compare with disk
            return isModified;
        }

        private string ComputeContentHash()
            => ComputeContentHash(textEditor.Text);

        internal void SetDirty()
        {
            if (isModified) return;
            isModified = true;
            if (activeDocIndex >= 0)
            {
                ActiveDoc.IsDirty = true;
            }
            UpdateWindowTitle();
            UpdateActiveTabTitle();
        }

        internal void ClearDirtyAfterSave()
        {
            isModified = false;
            savedContentHash = ComputeContentHash();
            _docManager.GetActive()?.ModifiedLines.Clear();
            if (activeDocIndex >= 0)
            {
                ActiveDoc.IsDirty = false;
                ActiveDoc.SavedHash = savedContentHash;
                ActiveDoc.LastWriteTime = lastFileWriteTime;
                ActiveDoc.FilePath = currentFilePath;
                ActiveDoc.Content = textEditor.Text;
                ActiveDoc.Syntax = currentSyntax;
            }
            UpdateWindowTitle();
            UpdateActiveTabTitle();
        }

        private void ForceCleanState()
        {
            isModified = false;
            if (activeDocIndex >= 0 && activeDocIndex < documents.Count)
            {
                documents[activeDocIndex].IsDirty = false;
            }
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = currentFilePath != null 
                ? $"Personal Flip Pad - {Path.GetFileName(currentFilePath)}"
                : "Personal Flip Pad - Untitled";
            this.Text = isModified ? "*" + baseTitle : baseTitle;
        }

        private DialogResult PromptSaveChanges()
        {
            // Only prompt if there are actual unsaved changes
            if (!isModified && (textEditor == null || string.IsNullOrEmpty(textEditor.Text) || savedContentHash == null))
                return DialogResult.No;

            var result = ThemedMessageBox.Show(
                "This file has unsaved changes. Save before proceeding?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                SaveFile();
                if (IsModified()) // Save failed
                    return DialogResult.Cancel;
            }

            return result;
        }

        internal void CheckIfClean()
        {
            if (!isModified || savedContentHash == null) return;

            string currentHash = ComputeContentHash();
            if (currentHash == savedContentHash)
            {
                isModified = false;
                _docManager.GetActive()?.ModifiedLines.Clear();
                UpdateWindowTitle();
            }
        }

        #endregion

        #region Recent Files

        private void AddToRecentFiles(string path)
        {
            _sessionManager.AddRecentFile(path);
            UpdateRecentMenu();
            _sessionManager.SaveRecentFiles();
        }

        private void UpdateRecentMenu()
        {
            recentMenuItem.DropDownItems.Clear();
            if (_sessionManager.RecentFiles.Count == 0)
            {
                recentMenuItem.Enabled = false;
                recentMenuItem.DropDownItems.Add("(No recent files)").Enabled = false;
            }
            else
            {
                recentMenuItem.Enabled = true;
                for (int i = 0; i < _sessionManager.RecentFiles.Count; i++)
                {
                    string filePath = _sessionManager.RecentFiles[i];
                    string display = $"{(i + 1)} {Path.GetFileName(filePath)}";
                    var item = new ToolStripMenuItem(display, null, (s, e) => OpenFileInNewTab(filePath));
                    recentMenuItem.DropDownItems.Add(item);
                }
                recentMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var clearItem = new ToolStripMenuItem("Clear Recent", null, (s, e) => { _sessionManager.ClearRecentFiles(); UpdateRecentMenu(); _sessionManager.SaveRecentFiles(); });
                recentMenuItem.DropDownItems.Add(clearItem);
            }
        }

        // Recent workspaces
        private void AddToRecentWorkspaces(string path)
        {
            _sessionManager.AddRecent(path);
            UpdateRecentWorkspacesMenu();
            _sessionManager.SaveRecent();
        }

        private void UpdateRecentWorkspacesMenu()
        {
            recentWorkspacesMenuItem.DropDownItems.Clear();
            if (_sessionManager.RecentWorkspaces.Count == 0)
            {
                recentWorkspacesMenuItem.Enabled = false;
                recentWorkspacesMenuItem.DropDownItems.Add("(No recent workspaces)").Enabled = false;
            }
            else
            {
                recentWorkspacesMenuItem.Enabled = true;
                for (int i = 0; i < _sessionManager.RecentWorkspaces.Count; i++)
                {
                    string wsPath = _sessionManager.RecentWorkspaces[i];
                    string display = $"{(i + 1)} {Path.GetFileName(wsPath)}";
                    var item = new ToolStripMenuItem(display, null, (s, e) => OpenWorkspaceFolder(wsPath));
                    item.ToolTipText = wsPath;
                    recentWorkspacesMenuItem.DropDownItems.Add(item);
                }
                recentWorkspacesMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var clearItem = new ToolStripMenuItem("Clear Recent Workspaces", null, (s, e) => { _sessionManager.ClearRecent(); UpdateRecentWorkspacesMenu(); _sessionManager.SaveRecent(); });
                recentWorkspacesMenuItem.DropDownItems.Add(clearItem);
            }
        }

        #endregion

        #region File Menu Handlers

        private void NewTab_Click(object? sender, EventArgs e) => NewFile();
private void NewWindow_Click(object? sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ExecutablePath, "--new-window");
        }
        private void NewProject_Click(object? sender, EventArgs e)
        {
            using var dlg = new NewProjectDialog(this);
            dlg.ShowDialog(this);
        }
        private void Open_Click(object? sender, EventArgs e) => OpenFile();

        private void OpenSolution_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Title = "Open Solution or Project";
            ofd.Filter = "Solution Files|*.sln|Project Files|*.csproj;*.vbproj;*.fsproj|All Files|*.*";
            if (ofd.ShowThemed() == DialogResult.OK)
            {
                string path = ofd.FileName;
                if (_solutionExplorerPanel == null) return;
                if (_sideTopTabs != null) _sideTopTabs.SelectedIndex = 1;
                if (_workspaceSplitContainer?.Panel1Collapsed == true)
                    ToggleWorkspace();
                _solutionExplorerPanel.LoadSolution(path);
            }
        }

        private void CloneRepository_Click(object? sender, EventArgs e)
        {
            using var dlg = new CloneRepositoryDialog(this, _secConfirmGitClone, _secTrustedGitHosts);
            dlg.ShowDialog(this);
        }
        private void Save_Click(object? sender, EventArgs e) => SaveFile();
        private void SaveAs_Click(object? sender, EventArgs e) => SaveAsFile();
        private void SaveAll_Click(object? sender, EventArgs e) => SaveAllFiles();
        private void ToggleAutoSave_Click(object? sender, EventArgs e) { _autoSaveEnabled = autoSaveMenuItem.Checked; if (_autoSaveEnabled) _autoSaveTimer.Start(); else _autoSaveTimer.Stop(); SaveSettings(); }
        private void CloseTab_Click(object? sender, EventArgs e) => CloseCurrentTab();
        private void CloseWindow_Click(object? sender, EventArgs e) => this.Close();
        private void CloseAll_Click(object? sender, EventArgs e)
        {
            if (IsModified())
            {
                var result = ThemedMessageBox.Show("Save changes before closing all?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    SaveFile();
                    if (IsModified()) return; // save failed or cancelled
                }
            }
            Application.Exit();
        }
        private void Exit_Click(object? sender, EventArgs e) => this.Close();

        #endregion
    }
}

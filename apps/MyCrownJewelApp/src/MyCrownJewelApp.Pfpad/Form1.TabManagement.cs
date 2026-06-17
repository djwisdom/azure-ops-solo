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
        #region EditorDocument Management (Tabs)

        // Get first visible line in editor
        private int GetFirstVisibleLine()
        {
            if (!textEditor.IsHandleCreated) return 0;
            return (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        }

        // Compute hash of string content
        private string ComputeContentHash(string content)
            => WorkspaceHelper.ComputeContentHash(content);

        // Save current editor state into the active EditorDocument
        private void SaveCurrentDocument()
        {
            if (activeDocIndex < 0) return;
            var doc = documents[activeDocIndex];
            doc.Content = textEditor.Text;
            doc.IsDirty = isModified;
            // bookmarks, modifiedLines, collapsedRegions are owned by doc directly
            doc.FilePath = currentFilePath;
            doc.SavedHash = savedContentHash;
            doc.LastWriteTime = lastFileWriteTime;
            doc.Syntax = currentSyntax;
            doc.SelectionStart = textEditor.SelectionStart;
            doc.SelectionLength = textEditor.SelectionLength;
            doc.FirstVisibleLine = GetFirstVisibleLine();
        }

        // Create a new empty EditorDocument
        private EditorDocument CreateNewDocument() => _docManager.CreateNew();

        // New file (untitled) - creates a new tab
        private void NewFile(bool isInitial = false)
        {
            if (!isInitial)
            {
                var result = PromptSaveChanges();
                if (result == DialogResult.Cancel) return;
            }

            // Save state of current EditorDocument before switching
            if (activeDocIndex >= 0)
            {
                SaveCurrentDocument();
            }

            var newDoc = CreateNewDocument();
            EnforceMaxOpenTabs();
            _docManager.Add(newDoc);
            int newIndex = documents.Count - 1;

            // Create tab page and add to end
            var tabPage = new TabPage(newDoc.DisplayName) { Tag = newDoc };
            tabControl.TabPages.Add(tabPage);

            // Switch to new tab and ensure visible
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            EnsureSelectedTabVisible();
        }

        // Open an existing file in a new tab
        internal void OpenFileInNewTab(string path)
        {
            if (!File.Exists(path)) return;
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > (long)MaxFileSizeMB * 1024 * 1024) // Configurable absolute limit
            {
                ThemedMessageBox.Show($"File is too large to open ({fileInfo.Length / (1024 * 1024)} MB). Maximum size is {MaxFileSizeMB} MB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (fileInfo.Length > (long)LargeFileWarningMB * 1024 * 1024) // Configurable warning threshold
            {
                var result = ThemedMessageBox.Show($"File is large ({fileInfo.Length / (1024 * 1024)} MB). Opening may be slow. Continue?", "Large File Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }
            try
            {
                var (content, encoding) = UnicodeFileHelper.ReadAllTextWithEncoding(path);
                var syntax = SyntaxDefinition.GetDefinitionForFile(path);
                bool containsRtl = UnicodeFileHelper.ContainsRtlText(content);
                _profilerDialog?.Log($"Opened file: {path} ({content.Length} chars, {UnicodeFileHelper.GetEncodingDisplayName(encoding)}{(containsRtl ? ", RTL" : "")})");
                var doc = new EditorDocument
                {
                    FilePath = path,
                    Content = content,
                    IsDirty = false,
                    ModifiedLines = new HashSet<int>(),
                    Bookmarks = new HashSet<int>(),
                    CollapsedRegions = new HashSet<int>(),
                    SavedHash = ComputeContentHash(content),
                    LastWriteTime = File.GetLastWriteTimeUtc(path),
                    SelectionStart = 0,
                    SelectionLength = 0,
                    FirstVisibleLine = 0,
                    Syntax = syntax,
                    FileEncoding = encoding,
                    ContainsRtlText = containsRtl
                };

                // Apply feature degradation for large files
                ApplyLargeFileDegradation(doc, fileInfo.Length);

                // Detect per-file indentation style if the feature is enabled
                if (_detectIndentationFromFile)
                {
                    var (detectedSpaces, detectedTabSz, detected) = IndentationHelper.DetectIndentation(content);
                    if (detected)
                    {
                        doc.DetectedInsertSpaces = detectedSpaces;
                        doc.DetectedTabSize = detectedTabSz;
                    }
                }

                EnforceMaxOpenTabs();
                _docManager.Add(doc);
                int newIndex = documents.Count - 1;
                var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                tabControl.TabPages.Add(tabPage);
                tabControl.SelectedIndex = newIndex; // triggers SwitchToTab when handle exists
                activeDocIndex = newIndex;
                EnsureSelectedTabVisible();
                ForceCleanState();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Open file asynchronously — shows tab immediately, loads content in background
        internal void OpenFileInNewTabAsync(string path)
        {
            Debug.WriteLine($"OpenFileInNewTabAsync called with path: {path}");
            if (!File.Exists(path))
            {
                Debug.WriteLine($"File does not exist: {path}");
                return;
            }
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > (long)MaxFileSizeMB * 1024 * 1024) // Configurable limit for async loading
            {
                BeginInvoke(() => ThemedMessageBox.Show($"File is too large to open ({fileInfo.Length / (1024 * 1024)} MB). Maximum size is {MaxFileSizeMB} MB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }
            if (fileInfo.Length > (long)AsyncFileWarningMB * 1024 * 1024) // Configurable warning for async
            {
                var result = ThemedMessageBox.Show($"File is large ({fileInfo.Length / (1024 * 1024)} MB). Opening may be slow and use significant memory. Continue?", "Large File Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }
            var syntax = SyntaxDefinition.GetDefinitionForFile(path);
            var doc = new EditorDocument
            {
                FilePath = path,
                Content = "",
                IsDirty = false,
                ModifiedLines = new HashSet<int>(),
                Bookmarks = new HashSet<int>(),
                CollapsedRegions = new HashSet<int>(),
                SavedHash = "",
                LastWriteTime = File.GetLastWriteTimeUtc(path),
                SelectionStart = 0,
                SelectionLength = 0,
                FirstVisibleLine = 0,
                Syntax = syntax,
                FileEncoding = null // Will be set when content is loaded
            };

            // Apply feature degradation for large files
            ApplyLargeFileDegradation(doc, fileInfo.Length);

            EnforceMaxOpenTabs();
            _docManager.Add(doc);
            int newIndex = documents.Count - 1;
            var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
            tabControl.TabPages.Add(tabPage);
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            EnsureSelectedTabVisible();

            _profilerDialog?.Log($"Started async load: {path}");

            // Read file content on background thread, then populate editor
            Task.Run(() =>
            {
                try
                {
                    var (content, encoding) = UnicodeFileHelper.ReadAllTextWithEncoding(path);
                    return (Content: content, Encoding: encoding);
                }
                catch { return (Content: (string?)null, Encoding: (System.Text.Encoding?)null); }
            }).ContinueWith(t =>
            {
                var result = t.Result;
                if (result.Content == null) return;
                string content = result.Content;
                var encoding = result.Encoding;
                // Capture doc by reference so we find the right tab regardless of
                // activeDocIndex changing while multiple files load concurrently.
                var capturedDoc = doc;
                BeginInvoke(() =>
                {
                    try
                    {
                        if (IsDisposed) return;
                        int docIndex = documents.IndexOf(capturedDoc);
                        if (docIndex < 0) return; // tab was closed before load finished
                        capturedDoc.Content = content;
                        capturedDoc.FileEncoding = encoding;
                        capturedDoc.ContainsRtlText = UnicodeFileHelper.ContainsRtlText(content);
                        capturedDoc.SavedHash = ComputeContentHash(content);

                        // Detect per-file indentation style (async path)
                        if (_detectIndentationFromFile)
                        {
                            var (detectedSpaces, detectedTabSz, detected) = IndentationHelper.DetectIndentation(content);
                            if (detected)
                            {
                                capturedDoc.DetectedInsertSpaces = detectedSpaces;
                                capturedDoc.DetectedTabSize = detectedTabSz;
                            }
                        }

                        // Update the editor only if this tab is currently visible
                        if (docIndex == tabControl.SelectedIndex && tabControl.SelectedIndex >= 0)
                        {
                            if (textEditor != null && !textEditor.IsDisposed && textEditor.IsHandleCreated)
                            {
                                textEditor.TextChanged -= TextEditor_TextChanged;
                                textEditor.Text = content;
                                textEditor.TextChanged += TextEditor_TextChanged;
                                savedContentHash = capturedDoc.SavedHash;
                                isModified = false;

                                // Re-highlight and update UI (skip for large files)
                                if (content.Length < 200000) // 200KB threshold
                                {
                                    CreateIncrementalHighlighter();
                                }
                                UpdateStatusBar();
                                UpdateTabTitle(docIndex);
                                UpdateThemeColors(_themeManager.CurrentTheme);
                                UpdateBreadcrumbs();
                            }
                        }
                        ForceCleanState();
                        _profilerDialog?.Log($"Completed async load: {path} ({content.Length} chars)");
                    }
                    catch (Exception ex)
                    {
                        BeginInvoke(() => ThemedMessageBox.Show($"Error loading file content: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    }
                });
            });
        }

        // Close current tab
        // If a max tab limit is set, close the oldest clean tab to stay within the limit.
        private void EnforceMaxOpenTabs()
        {
            if (_tabMaxOpen <= 0 || documents.Count < _tabMaxOpen) return;
            for (int i = 0; i < documents.Count; i++)
            {
                if (i == activeDocIndex) continue;
                if (!documents[i].IsDirty)
                {
                    CloseTabAt(i);
                    return;
                }
            }
        }

        internal void CloseCurrentTab()
        {
            if (documents.Count <= 1) return; // always keep at least one tab
            if (activeDocIndex < 0 || activeDocIndex >= documents.Count) return;

            if (!_tabConfirmCloseUnsaved && isModified)
            {
                // Discard unsaved changes without prompting
            }
            else
            {
                var result = PromptSaveChanges();
                if (result == DialogResult.Cancel) return;
            }

            // Push to recently closed before removal
            if (_tabRememberRecentlyClosed)
            {
                var closingDoc = documents[activeDocIndex];
                _recentlyClosed.Insert(0, (closingDoc.FilePath, closingDoc.Content ?? "", closingDoc.DisplayName));
                while (_recentlyClosed.Count > _tabMaxRecentlyClosed)
                    _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
            }

            // Remove current EditorDocument and its tab
            int closeIndex = activeDocIndex;
            if (closeIndex >= documents.Count || closeIndex >= tabControl.TabPages.Count) return;

            // Suspend layout to prevent flicker during tab removal
            tabControl.SuspendLayout();

            try
            {
                // Force activeDocIndex to -1 so that SelectedIndexChanged → SwitchToTab does not bail
                activeDocIndex = -1;

                _docManager.RemoveAt(closeIndex);
                tabControl.TabPages.RemoveAt(closeIndex);

                // Select another tab
                int newIndex = closeIndex < documents.Count ? closeIndex : documents.Count - 1;
                if (newIndex >= 0)
                    SwitchToTab(newIndex);
            }
            finally
            {
                tabControl.ResumeLayout();
            }
        }

        // Close a specific tab by index without switching to it first.
        // This avoids the "content disappears then reappears" flicker that
        // happens when selecting + closing a non-active tab.
        private void CloseTabAt(int index)
        {
            if (documents.Count <= 1) return;
            if (index < 0 || index >= documents.Count) return;

            // If it's the active tab, delegate to CloseCurrentTab
            if (index == activeDocIndex) { CloseCurrentTab(); return; }

            var doc = documents[index];
            if (doc.IsDirty && _tabConfirmCloseUnsaved)
            {
                var result = ThemedMessageBox.Show(
                    $"Save changes to \"{doc.DisplayName}\"?",
                    "Close Tab", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    try { File.WriteAllText(doc.FilePath!, doc.Content); } catch { }
                }
            }

            // Push to recently closed before removal
            if (_tabRememberRecentlyClosed)
            {
                _recentlyClosed.Insert(0, (doc.FilePath, doc.Content ?? "", doc.DisplayName));
                while (_recentlyClosed.Count > _tabMaxRecentlyClosed)
                    _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
            }

            // Suspend layout to prevent flicker during tab removal
            tabControl.SuspendLayout();

            try
            {
                // Remove EditorDocument and tab
                _docManager.RemoveAt(index);

                // Adjust activeDocIndex BEFORE removing the tab page, because
                // TabPages.RemoveAt can fire SelectedIndexChanged → SwitchToTab → SaveCurrentDocument,
                // which would use a stale out-of-range activeDocIndex.
                if (index < activeDocIndex)
                    activeDocIndex--;
                else if (index == activeDocIndex)
                    activeDocIndex = -1;

                tabControl.TabPages.RemoveAt(index);
            }
            finally
            {
                tabControl.ResumeLayout();
            }
        }

        // Switch to EditorDocument at given index (0-based)
        internal void SwitchToTab(int index)
        {
            if (index < 0 || index >= documents.Count) return;
            if (activeDocIndex == index) return;

            if (activeDocIndex >= 0 && isModified)
            {
                SaveCurrentDocument();
            }

            activeDocIndex = index;
            var doc = documents[activeDocIndex];
            LoadDocument(doc);
            UpdateWindowTitle();
            UpdateMarkdownPreview();
            UpdateEncodingLabel();
            _workspacePanel?.SetActiveFile(doc.FilePath);
        }

        // Load EditorDocument state into editor and UI
        private void LoadDocument(EditorDocument doc)
        {
            // Update core fields from EditorDocument
            currentFilePath = doc.FilePath;
            isModified = doc.IsDirty;
            savedContentHash = doc.SavedHash;
            lastFileWriteTime = doc.LastWriteTime ?? DateTime.MinValue;

            // Configure RTL support
            if (textEditor != null)
            {
                textEditor.RightToLeft = doc.ContainsRtlText ? RightToLeft.Yes : RightToLeft.No;
            }
            currentSyntax = doc.Syntax;

            // Apply per-file detected indentation overrides (set when the file was opened)
            if (doc.DetectedTabSize.HasValue || doc.DetectedInsertSpaces.HasValue)
            {
                if (doc.DetectedTabSize.HasValue && doc.DetectedTabSize.Value != tabSize)
                {
                    tabSize = doc.DetectedTabSize.Value;
                    if (tabSizeDropDown != null) tabSizeDropDown.Text = $"Tab: {tabSize}";
                }
                if (doc.DetectedInsertSpaces.HasValue)
                    insertSpaces = doc.DetectedInsertSpaces.Value;
                UpdateTabStops();
            }

            // Load text without triggering dirty flag
#pragma warning disable CS8602 // Method reference is never null
            textEditor.TextChanged -= TextEditor_TextChanged;
#pragma warning restore CS8602
            textEditor.Text = doc.Content ?? "";
            textEditor.SelectionStart = doc.SelectionStart;
            textEditor.SelectionLength = doc.SelectionLength;

            // Recompute hash from post-normalization textEditor.Text
            // RichTextBox normalizes line endings (\n → \r\n on Windows);
            // the original doc.SavedHash was computed from the raw file string.
            savedContentHash = ComputeContentHash();
            doc.SavedHash = savedContentHash;

            // Force scrollbar recalculation — native RichEdit can miss extents after .Text set
            textEditor.PerformLayout();

            textEditor.TextChanged += TextEditor_TextChanged;

            // Recreate syntax highlighter based on current syntax (skip for large files)
            if ((doc.Content?.Length ?? 0) < 200000) // 200KB threshold for highlighting
            {
                CreateIncrementalHighlighter();
            }

            // Update UI (must be after CreateIncrementalHighlighter so file type is current)
            UpdateStatusBar();
            UpdateTabTitle(activeDocIndex);
            UpdateBreadcrumbs();
            ApplyEditorColors();
            // Notify snippet engine so language-specific snippets activate correctly.
            _snippetEngine?.SetFileExtension(CurrentFileExtension);
        }

        // Update tab title for EditorDocument at index
        private void UpdateTabTitle(int docIndex)
        {
            if (docIndex < 0 || docIndex >= documents.Count) return;
            var doc = documents[docIndex];
            if (docIndex < tabControl.TabPages.Count)
            {
                var tabPage = tabControl.TabPages[docIndex];
                if (doc.IsDirty)
                {
                    tabPage.Text = _tabDirtyIndicator switch
                    {
                        "dot" => doc.DisplayName + " ●",
                        "both" => "* " + doc.DisplayName + " ●",
                        "none" => doc.DisplayName,
                        _ => "*" + doc.DisplayName   // "asterisk" (default)
                    };
                }
                else
                {
                    tabPage.Text = doc.DisplayName;
                }
            }
        }

        // Convenience for active tab
        private void UpdateActiveTabTitle()
        {
            UpdateTabTitle(activeDocIndex);
        }

        #endregion

        #region Tab Control Event Handlers

        private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < documents.Count)
            {
                SwitchToTab(tabControl.SelectedIndex);
            }
            RefreshGitRepo();
            UpdateMarkdownPreview();
        }

        private void TabControl_DoubleClick(object? sender, EventArgs e)
        {
            // Only create new tab if not over an existing tab and no drag in progress
            if (_draggedTabIndex != null) return;
            var mousePos = tabControl.PointToClient(Control.MousePosition);
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                if (tabControl.GetTabRect(i).Contains(mousePos))
                    return; // clicked on a tab, not empty area
            }
            // Create new tab on double-click anywhere else on tab bar
            NewFile();
        }

        private void TabControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle && _tabMiddleClickClose)
            {
                CloseTabAtLocation(e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    var rect = tabControl.GetTabRect(i);
                    if (rect.Contains(e.Location))
                    {
                        // Check close button (×) — always visible, same position as draw
                        int btnSize = 16;
                        int btnX = rect.Right - 22;
                        int btnY = rect.Top + 3;
                        var btnRect = new Rectangle(btnX, btnY, btnSize, btnSize);
                        // Respect close button visibility setting
                        bool closeVisible = _tabCloseButtonVisibility switch
                        {
                            "active" => i == tabControl.SelectedIndex,
                            "hover" => i == (hoveredTabIndex ?? -1) || i == tabControl.SelectedIndex,
                            "never" => false,
                            _ => true
                        };
                        if (closeVisible && btnRect.Contains(e.Location))
                        {
                            CloseTabAt(i);
                            return;
                        }
                        // Otherwise start potential drag
                        _draggedTabIndex = i;
                        _dragStartPoint = tabControl.PointToScreen(e.Location);
                        _isDragging = false;
                        _pendingDragZone = DragZone.None;
                        break;
                    }
                }
            }
        }

        private void TabControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Execute pending split-on-zone if drag ended in an edge zone
                if (_isDragging && _draggedTabIndex.HasValue && _pendingDragZone != DragZone.None && _pendingDragZone != DragZone.Outside)
                {
                    SplitTabToPane(_draggedTabIndex.Value, _pendingDragZone);
                }
                // End drag; if not detached, cancel
                _draggedTabIndex = null;
                _isDragging = false;
                _pendingDragZone = DragZone.None;
            }
        }

        private void TabControl_MouseLeave(object? sender, EventArgs e)
        {
            if (hoveredTabIndex.HasValue || _closeButtonHovered)
            {
                hoveredTabIndex = null;
                closeButtonBounds = null;
                _closeButtonHovered = false;
                tabControl.Invalidate();
            }
        }

        private void TabControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_tabMouseWheelScroll) return;
            if (tabControl.TabCount <= 1) return;
            int dir = e.Delta > 0 ? -1 : 1;
            int next = tabControl.SelectedIndex + dir;
            if (next >= 0 && next < tabControl.TabCount)
            {
                _tabScrollDebounce?.Stop();
                _pendingTabScrollIndex = next;
                _tabScrollDebounce?.Start();
            }
        }

        private void PositionTabDropdownButton()
        {
            if (_tabDropdownButton == null || tabControl == null || tabControl.IsDisposed) return;
            Point tabScreen = tabControl.PointToScreen(Point.Empty);
            Point formClient = this.PointToClient(tabScreen);

            if (formClient.IsEmpty) return;

            // Offset upward by 4px so the button doesn't cover the tab strip's bottom edge line
            int y = formClient.Y + (tabControl.Height - _tabDropdownButton.Height) / 2 - 4;

            int btnW = _tabDropdownButton.Width;
            int tabRightEdge = formClient.X + tabControl.Width;

            int lastTabIndex = tabControl.TabCount - 1;
            if (lastTabIndex >= 0 && lastTabIndex < tabControl.TabPages.Count)
            {
                Rectangle lastTabRect = tabControl.GetTabRect(lastTabIndex);

                // Detect whether horizontal scroll buttons are showing.
                // When tabs overflow, GetTabRect(0).Left < 0 (tabs scrolled left)
                // or lastTabRect.Right > tabControl.ClientSize.Width (tabs extend past right).
                bool hScroll = tabControl.GetTabRect(0).Left < 0
                               || lastTabRect.Right > tabControl.ClientSize.Width;

                if (hScroll)
                {
                    // Scroll arrows are visible on the LEFT side of the tab strip.
                    // The right side of the tab header is free — anchor the dropdown there.
                    // Use the system scroll-bar width as margin from the right edge.
                    int scrollMargin = SystemInformation.VerticalScrollBarWidth;
                    int x = tabRightEdge - scrollMargin - btnW - 2;
                    x = Math.Max(formClient.X + 2, Math.Min(x, tabRightEdge - btnW - 2));
                    _tabDropdownButton.Location = new Point(x, y);
                }
                else
                {
                    // No scroll: position after the last tab if there is a meaningful gap,
                    // otherwise right-align within the tab control.
                    int desiredX = formClient.X + lastTabRect.Right + 2;
                    int maxX = tabRightEdge - btnW - 2;
                    int x;
                    if (maxX - desiredX > btnW * 2)
                        x = maxX; // snap to right edge (large empty gap)
                    else
                        x = Math.Min(desiredX, maxX); // follow last tab

                    _tabDropdownButton.Location = new Point(
                        Math.Max(formClient.X + 2, x),
                        Math.Max(formClient.Y, y));
                }
            }
            else
            {
                // No tabs — hide button
                _tabDropdownButton.Location = new Point(
                    Math.Max(formClient.X + 2, tabRightEdge - btnW - 2),
                    Math.Max(formClient.Y, y));
            }

            _tabDropdownButton.Visible = tabControl.TabCount > 0;
        }

        private void TabDropdownButton_Click(object? sender, EventArgs e)
        {
            if (tabControl == null || tabControl.TabCount == 0 || _tabDropdownButton == null) return;
            var theme = _themeManager.CurrentTheme;
            var cm = new ContextMenuStrip();
            cm.Font = new Font("Segoe UI", 10);
            cm.BackColor = theme.MenuBackground;
            cm.ForeColor = theme.Text;
            cm.Renderer = new ThemeAwareMenuRenderer(theme);
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                int idx = i;
                var page = tabControl.TabPages[i];
                var item = cm.Items.Add(page.Text, null, (s, args) =>
                {
                    if (tabControl != null && idx < tabControl.TabCount && !tabControl.IsDisposed)
                    {
                        tabControl.SelectedIndex = idx;
                        EnsureSelectedTabVisible();
                    }
                });
                if (i == tabControl.SelectedIndex)
                    item.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            }
            cm.Show(_tabDropdownButton, new Point(_tabDropdownButton.Width, _tabDropdownButton.Height), ToolStripDropDownDirection.BelowLeft);
        }

        private void EnsureSelectedTabVisible()
        {
            if (tabControl == null || tabControl.IsDisposed) return;
            int idx = tabControl.SelectedIndex;
            var tabRect = tabControl.GetTabRect(idx);
            if (tabRect.Right <= tabControl.Width && tabRect.Left >= 0) return;
            tabControl.SuspendLayout();
            tabControl.SelectedIndex = idx;
            tabControl.ResumeLayout();
            tabControl.Invalidate();
        }
        private void CloseTabAtLocation(Point location)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var rect = tabControl.GetTabRect(i);
                if (rect.Contains(location))
                {
                    CloseTabAt(i);
                    break;
                }
            }
        }

        private void DetachTabToNewWindow(int tabIndex, Point dragScreenPos)
        {
            if (tabIndex < 0 || tabIndex >= documents.Count) return;

            try
            {
                var doc = documents[tabIndex];

                // Check if cursor is over another open Pfpad window — merge tab there instead
                var targetForm = FindForm1AtPoint(dragScreenPos);
                if (targetForm != null)
                {
                    // Transfer EditorDocument to the target window
                    targetForm.ReceiveDroppedTab(doc);

                    // Remove the EditorDocument from the original window
                    if (tabIndex < documents.Count && tabIndex < tabControl.TabPages.Count)
                    {
                        _docManager.RemoveAt(tabIndex);
                        tabControl.TabPages.RemoveAt(tabIndex);
                    }

                    // Adjust active index in original window
                    if (activeDocIndex >= documents.Count)
                        activeDocIndex = documents.Count - 1;
                    if (activeDocIndex >= 0)
                        SwitchToTab(activeDocIndex);
                    else if (documents.Count == 0)
                        NewFile(isInitial: true);

                    // Bring target window to front
                    targetForm.Activate();
                }
                else
                {
                    // Create a new Form1 instance for the detached EditorDocument (skip default untitled doc)
                    var detachedForm = new Form1(skipInitialDocument: true);
                    detachedForm.tabControl.TabPages.Clear();
                    detachedForm.activeDocIndex = -1;

                    // Add the dragged EditorDocument to the new form
                    detachedForm._docManager.Add(doc);
                    var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                    detachedForm.tabControl.TabPages.Add(tabPage);
                    detachedForm.tabControl.SelectedIndex = 0;
                    detachedForm.activeDocIndex = 0;
                    detachedForm.LoadDocument(doc);
                    detachedForm.UpdateWindowTitle();
                    detachedForm.UpdateTabTitle(0);
                    detachedForm.UpdateTabSizeDropdown();

                    // Position near the drag release point
                    detachedForm.StartPosition = FormStartPosition.Manual;
                    detachedForm.Location = new Point(dragScreenPos.X - 100, dragScreenPos.Y - 50);
                    // Prevent RestoreWindowBounds (called from Shown) from overriding manual position
                    detachedForm._savedWindowBounds = "";
                    detachedForm._savedWindowState = "";

                    // Remove the EditorDocument from the original window
                    if (tabIndex < documents.Count && tabIndex < tabControl.TabPages.Count)
                    {
                        _docManager.RemoveAt(tabIndex);
                        tabControl.TabPages.RemoveAt(tabIndex);
                    }

                    // Adjust active index in original window
                    if (activeDocIndex >= documents.Count)
                        activeDocIndex = documents.Count - 1;
                    if (activeDocIndex >= 0)
                        SwitchToTab(activeDocIndex);
                    else if (documents.Count == 0)
                        NewFile(isInitial: true);

                    // Show the detached window
                    detachedForm.Show();
                }

                // Reset drag state
                _draggedTabIndex = null;
                _isDragging = false;
                _dragStartPoint = null;
                _pendingDragZone = DragZone.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetachTabToNewWindow error: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds an open (non-minimized) Form1 at the given screen position, excluding this instance.
        /// </summary>
        private Form1? FindForm1AtPoint(Point screenPos)
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is Form1 target && target != this && target.Visible && target.WindowState != FormWindowState.Minimized)
                {
                    if (target.Bounds.Contains(screenPos))
                        return target;
                }
            }
            return null;
        }

        /// <summary>
        /// Receives a dropped tab EditorDocument from another Pfpad window into this instance.
        /// </summary>
        private void ReceiveDroppedTab(EditorDocument doc)
        {
            _docManager.Add(doc);
            var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
            tabControl.TabPages.Add(tabPage);

            // Make the dropped tab active
            int newIndex = documents.Count - 1;
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            LoadDocument(doc);
            UpdateWindowTitle();
            UpdateTabTitle(newIndex);
            UpdateTabSizeDropdown();
        }

        private enum DragZone { None, Left, Right, Top, Bottom, Outside }

        private DragZone GetDragZone(Point screenPos)
        {
            var bounds = this.Bounds;
            const double edgeRatio = 0.25;
            int edgeW = (int)(bounds.Width * edgeRatio);
            int edgeH = (int)(bounds.Height * edgeRatio);

            if (!bounds.Contains(screenPos))
                return DragZone.Outside;

            int localX = screenPos.X - bounds.X;
            int localY = screenPos.Y - bounds.Y;

            if (localX < edgeW) return DragZone.Left;
            if (localX > bounds.Width - edgeW) return DragZone.Right;
            if (localY < edgeH) return DragZone.Top;
            if (localY > bounds.Height - edgeH) return DragZone.Bottom;

            return DragZone.None;
        }

        private void SplitTabToPane(int tabIndex, DragZone zone)
        {
            if (tabIndex < 0 || tabIndex >= documents.Count) return;
            if (zone is DragZone.None or DragZone.Outside) return;

            try
            {
                var doc = documents[tabIndex];

                // If a split is already active, close it first
                CloseSplit();

                // Store EditorDocument reference for close/restore
                _splitDocument = doc;
                _splitDocumentTitle = doc.DisplayName;

                var theme = _themeManager.CurrentTheme;
                _splitEditor = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = RichTextBoxScrollBars.Both,
                    AcceptsTab = true,
                    Font = textEditor.Font,
                    BorderStyle = BorderStyle.None,
                    BackColor = theme.EditorBackground,
                    ForeColor = theme.Text,
                    Text = doc.Content ?? ""
                };
                _splitEditor.HandleCreated += (s, e) => ApplyScrollbarTheme();
                _splitEditor.Enter += (s, e) =>
                {
                    if (vimModeEnabled && vimEngine != null)
                        vimEngine.SetEditor(_splitEditor);
                };

                if (zone is DragZone.Left or DragZone.Right)
                {
                    // Vertical split: create a new SplitContainer inside _terminalSplitContainer.Panel1
                    splitContainer = new SplitContainer();
                    splitContainer.Dock = DockStyle.Fill;
                    splitContainer.SplitterWidth = 4;
                    splitContainer.Panel1MinSize = 50;
                    splitContainer.Panel2MinSize = 100;
                    splitContainer.TabStop = false;
                    splitContainer.Orientation = Orientation.Vertical;

                    var editorPanel = _terminalSplitContainer!.Panel1;
                    editorPanel.Controls.Remove(mainTable);
                    splitContainer.Panel1.Controls.Add(mainTable);
                    splitContainer.Panel2.Controls.Add(_splitEditor);

                    editorPanel.Controls.Add(splitContainer);

                    BeginInvoke(new Action(() =>
                    {
                        if (splitContainer == null || splitContainer.IsDisposed) return;
                        if (splitContainer.Width > 100)
                            splitContainer.SplitterDistance = splitContainer.Width / 2;
                    }));

                    _splitEditor.Focus();
                }
                else
                {
                    // Horizontal split: use _terminalSplitContainer.Panel2 (terminal panel)
                    _splitIsHorizontal = true;

                    // Hide any terminal content and put the split editor in Panel2
                    if (_terminalTabControl?.Parent == _terminalSplitContainer!.Panel2)
                        _terminalTabControl.Visible = false;
                    if (_terminalNewTabButton?.Parent == _terminalSplitContainer.Panel2)
                        _terminalNewTabButton.Visible = false;

                    _terminalSplitContainer.Panel2.Controls.Add(_splitEditor);
                    _terminalSplitContainer.Panel2Collapsed = false;

                    // Split evenly by height
                    int available = _terminalSplitContainer.Height - _terminalSplitContainer.SplitterWidth;
                    if (available > 200)
                        _terminalSplitContainer.SplitterDistance = available / 2;
                    else if (available > 100)
                        _terminalSplitContainer.SplitterDistance = available - 100;

                    _splitEditor.Focus();
                    _terminalSplitContainer.PerformLayout();
                }

                // Ensure Vim engine targets the split editor
                if (vimModeEnabled && vimEngine != null)
                {
                    BeginInvoke(() =>
                    {
                        vimEngine.SetEditor(_splitEditor);
                        if (vimEngine.CurrentMode == VimMode.Insert)
                            vimEngine.EnterMode(VimMode.Normal);
                        _splitEditor.Focus();
                    });
                }

                // Remove the tab page from the tab control (EditorDocument stays in memory)
                if (tabIndex >= 0 && tabIndex < tabControl.TabPages.Count)
                    tabControl.TabPages.RemoveAt(tabIndex);
                if (tabIndex >= 0 && tabIndex < documents.Count)
                    _docManager.RemoveAt(tabIndex);

                // Adjust active index
                if (activeDocIndex >= documents.Count)
                    activeDocIndex = documents.Count - 1;
                if (activeDocIndex >= 0)
                    SwitchToTab(activeDocIndex);
                else if (documents.Count == 0)
                    NewFile(isInitial: true);

                // Reset drag state
                _draggedTabIndex = null;
                _isDragging = false;
                _dragStartPoint = null;
                _pendingDragZone = DragZone.None;
                closeSplitMenuItem.Enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SplitTabToPane error: {ex.Message}");
            }
        }

        private void CloseSplit()
        {
            if (_splitEditor == null) return;

            // Save content back to EditorDocument
            if (_splitDocument != null)
            {
                _splitDocument.Content = _splitEditor.Text;
                _splitDocument.ModifiedLines = new HashSet<int>();
                _splitDocument.IsDirty = false;
            }

            if (_splitIsHorizontal && _terminalSplitContainer?.IsDisposed == false)
            {
                // Horizontal split: remove _splitEditor from _terminalSplitContainer.Panel2
                _terminalSplitContainer.Panel2.Controls.Remove(_splitEditor);
                // Restore terminal visibility
                if (_terminalTabControl != null) _terminalTabControl.Visible = true;
                if (_terminalNewTabButton != null) _terminalNewTabButton.Visible = true;
                // Collapse Panel2 if terminal was not visible before the split
                if (!_terminalVisible)
                    _terminalSplitContainer.Panel2Collapsed = true;
                _terminalSplitContainer.PerformLayout();
            }
            else if (splitContainer != null)
            {
                // Vertical split: move mainTable back to _terminalSplitContainer.Panel1
                if (splitContainer.Panel1.Controls.Contains(mainTable))
                    splitContainer.Panel1.Controls.Remove(mainTable);

                if (_terminalSplitContainer?.IsDisposed == false)
                {
                    _terminalSplitContainer.Panel1.Controls.Add(mainTable);
                    mainTable.Dock = DockStyle.Fill;
                }

                if (_splitEditor != null)
                {
                    splitContainer.Panel2.Controls.Remove(_splitEditor);
                }

                splitContainer.Dispose();
                splitContainer = null;
            }

            // Clean up split editor
            if (_splitEditor != null)
            {
                _splitEditor.Dispose();
                _splitEditor = null;
            }

            _splitIsHorizontal = false;

            // Re-add the EditorDocument and tab page to the main window
            if (_splitDocument != null)
            {
                _docManager.Add(_splitDocument);
                int newIndex = documents.Count - 1;
                var tabPage = new TabPage(_splitDocumentTitle ?? _splitDocument.DisplayName) { Tag = _splitDocument };
                tabControl.TabPages.Add(tabPage);
                tabControl.SelectedIndex = newIndex;
            }

            _splitDocument = null;
            _splitDocumentTitle = null;
            closeSplitMenuItem.Enabled = false;

            if (textEditor.CanFocus)
                textEditor.Focus();
        }

        private void CloseSplit_Click(object? sender, EventArgs e) => CloseSplit();

        /// <summary>
        /// Switches between a C/C++ source file and its corresponding header (or vice versa).
        /// Checks the same directory first, then the sibling include/ or src/ subdirectory.
        /// Bound to Alt+O.
        /// </summary>
        private void ToggleHeaderSource()
        {
            if (string.IsNullOrEmpty(currentFilePath)) return;

            string ext = Path.GetExtension(currentFilePath).ToLowerInvariant();
            string dir = Path.GetDirectoryName(currentFilePath) ?? "";
            string baseName = Path.GetFileNameWithoutExtension(currentFilePath);

            string[] candidateExts;
            string[] siblingDirs;

            if (ext is ".cpp" or ".cc" or ".cxx" or ".c++" or ".c")
            {
                candidateExts = [".hpp", ".h", ".hxx", ".h++"];
                // source → header: also check ../include
                siblingDirs = [dir, Path.Combine(dir, "..", "include"), Path.Combine(dir, "include")];
            }
            else if (ext is ".hpp" or ".h" or ".hxx" or ".h++")
            {
                candidateExts = [".cpp", ".cxx", ".cc", ".c++", ".c"];
                // header → source: also check ../src
                siblingDirs = [dir, Path.Combine(dir, "..", "src"), Path.Combine(dir, "src")];
            }
            else
            {
                ShowNotification("Toggle", "No header/source counterpart for this file type.");
                return;
            }

            foreach (string searchDir in siblingDirs)
            {
                string resolvedDir = Path.GetFullPath(searchDir);
                if (!Directory.Exists(resolvedDir)) continue;
                foreach (string candidateExt in candidateExts)
                {
                    string candidate = Path.Combine(resolvedDir, baseName + candidateExt);
                    if (File.Exists(candidate))
                    {
                        OpenFileInNewTabAsync(candidate);
                        return;
                    }
                }
            }

            ShowNotification("Toggle", $"No counterpart found for '{Path.GetFileName(currentFilePath)}'.");
        }

        private void TabControl_MouseMove(object? sender, MouseEventArgs e)
        {
            int newHoverIndex = -1;
            Rectangle? newCloseBounds = null;
            bool overAnyTab = false;

            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var rect = tabControl.GetTabRect(i);
                if (rect.Contains(e.Location))
                {
                    overAnyTab = true;
                    newHoverIndex = i;
                    int buttonSize = 16;
                    int btnX = rect.Right - 22;
                    int btnY = rect.Top + 3;
                    newCloseBounds = new Rectangle(btnX, btnY, buttonSize, buttonSize);
                    break;
            }
            }

            int oldHover = hoveredTabIndex ?? -1;
            bool oldCloseHovered = closeButtonBounds.HasValue && closeButtonBounds.Value.Contains(e.Location);
            bool newCloseHovered = newCloseBounds.HasValue && newCloseBounds.Value.Contains(e.Location);

            bool needsRedraw = (newHoverIndex != oldHover) || (newCloseHovered != oldCloseHovered);

            hoveredTabIndex = overAnyTab ? newHoverIndex : null;
            closeButtonBounds = newCloseBounds;
            _closeButtonHovered = newCloseHovered;

            // Always show hand cursor on the tab strip — whole strip is interactive
            tabControl.Cursor = Cursors.Hand;

            if (needsRedraw)
            {
                tabControl.Invalidate();
            }

            // Tab tear-away: if dragging and mouse moved beyond threshold, start drag
            if (_draggedTabIndex.HasValue && !_isDragging)
            {
                var screenPos = tabControl.PointToScreen(e.Location);
                if (_dragStartPoint.HasValue && (Math.Abs(screenPos.X - _dragStartPoint.Value.X) > 20 || Math.Abs(screenPos.Y - _dragStartPoint.Value.Y) > 5))
                {
                    _isDragging = true;
                }
            }

            // If dragging, check if outside window to detach, or near edge to split
            if (_isDragging && _draggedTabIndex.HasValue)
            {
                var screenPos = tabControl.PointToScreen(e.Location);
                var zone = GetDragZone(screenPos);
                if (zone == DragZone.Outside)
                {
                    _pendingDragZone = DragZone.None;
                    DetachTabToNewWindow(_draggedTabIndex.Value, screenPos);
                }
                else
                {
                    // Defer edge-zone split to MouseUp so user can drag past the edge to detach
                    _pendingDragZone = zone;
                }
            }
        }

        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (tabControl == null || tabControl.TabPages.Count == 0) return;
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;
            if (e.Index < 0 || e.Index >= documents.Count) return;

            if (tabControl == null || tabControl.TabPages.Count == 0) return;
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;
            if (e.Index < 0 || e.Index >= documents.Count) return;

            var theme = _themeManager.CurrentTheme;
            var tabRect = e.Bounds;

            bool isSelected = (e.Index == tabControl.SelectedIndex);
            bool isHovered = (e.Index == hoveredTabIndex);

            Color backColor;
            if (isSelected)
                backColor = theme.EditorBackground;
            else if (isHovered)
                backColor = theme.ButtonHoverBackground;
            else
                backColor = theme.MenuBackground;

            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, tabRect);
            }

            // Tab icon
            var doc = e.Index < documents.Count ? documents[e.Index] : null;
            if (_tabShowFileIcons && doc?.FilePath != null)
            {
                int iconIdx = FileIconProvider.GetIconIndex(doc.FilePath);
                if (iconIdx >= 0 && iconIdx < FileIconProvider.ImageList.Images.Count)
                {
                    var iconRect = new Rectangle(tabRect.X + 8, tabRect.Y + 3, 16, 16);
                    e.Graphics.DrawImage(FileIconProvider.ImageList.Images[iconIdx], iconRect);
                }
            }

            // Tab text
            string text = tabControl.TabPages[e.Index].Text;
            bool showClose = _tabCloseButtonVisibility switch
            {
                "active" => isSelected,
                "hover" => isSelected || isHovered,
                "never" => false,
                _ => true   // "always"
            };
            int textOffset = (_tabShowFileIcons && doc?.FilePath != null) ? 28 : 8;
            int textRightMargin = showClose ? 22 : 6;
            var textRect = new Rectangle(
                tabRect.X + textOffset, tabRect.Y + 3,
                tabRect.Right - textRightMargin - tabRect.X - textOffset, tabRect.Height - 4);

            TextRenderer.DrawText(e.Graphics, text, tabControl.Font, textRect,
                isSelected ? theme.Text : theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            // Draw borders
            if (!isSelected)
            {
                // Hide borders for inactive tabs by drawing over with background color
                using (var pen = new Pen(backColor, 1))
                {
                    e.Graphics.DrawRectangle(pen, tabRect.X, tabRect.Y, tabRect.Width - 1, tabRect.Height - 1);
                }
            }
            else
            {
                // Draw top border for active tab
                using (var pen = new Pen(theme.Text, 1))
                {
                    e.Graphics.DrawLine(pen, tabRect.Left, tabRect.Top, tabRect.Right - 1, tabRect.Top);
                }
                // Accent underline
                using var accentPen = new Pen(theme.Accent, 2);
                e.Graphics.DrawLine(accentPen, tabRect.Left, tabRect.Bottom - 2, tabRect.Right, tabRect.Bottom - 2);
            }

            // Close button (×) — visibility controlled by setting
            if (showClose)
            {
                var closeRect = new Rectangle(tabRect.Right - 22, tabRect.Y + 3, 16, 16);
                using var xFont = new Font("Segoe UI", 11, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, "\u00D7", xFont, closeRect, theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        #endregion
    }
}

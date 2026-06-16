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
        #region Bookmark & Fold Handlers

        private void ToggleBookmark_Click(object? sender, EventArgs e)
        {
            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            ToggleBookmark(currentLine);
        }

        private void NextBookmark_Click(object? sender, EventArgs e)
        {
            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            int? next = null;
            foreach (int bm in _docManager.GetActive()?.Bookmarks ?? [])
            {
                if (bm > currentLine)
                {
                    next = bm;
                    break;
                }
            }
            if (next.HasValue) GoToLine(next.Value + 1);
        }

        private void PrevBookmark_Click(object? sender, EventArgs e)
        {
            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            int? prev = null;
            foreach (int bm in _docManager.GetActive()?.Bookmarks ?? [])
            {
                if (bm < currentLine) prev = bm;
                else break;
            }
            if (prev.HasValue) GoToLine(prev.Value + 1);
        }

        private void ClearAllBookmarks_Click(object? sender, EventArgs e)
        {
            _docManager.GetActive()?.Bookmarks.Clear();
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        private void ToggleFold_Click(object? sender, EventArgs e)
        {
            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            ToggleFold(currentLine);
        }

        internal void ToggleFold(int line)
        {
            if (_foldingManager != null)
            {
                _suppressFoldRescan = true;
                try
                {
                    if (_foldingManager.ToggleFold(line))
                    {
                        if (gutterPanel != null) gutterPanel.RefreshGutter();
                    }
                }
                finally
                {
                    _suppressFoldRescan = false;
                }
            }
        }

        private void ToggleAllFolds_Click(object? sender, EventArgs e)
        {
            if (_foldingManager == null) return;
            bool allCollapsed = _foldingManager.GetAllRegions().All(r => r.IsCollapsed);
            _suppressFoldRescan = true;
            try
            {
                foreach (var r in _foldingManager.GetAllRegions().ToList())
                    _foldingManager.ToggleFold(r.OpenLine);
            }
            finally
            {
                _suppressFoldRescan = false;
                _foldingManager.ScanRegions(CurrentFileExtension);
            }
            gutterPanel?.RefreshGutter();
        }

        internal void ToggleBookmark(int line)
        {
            var bms = _docManager.GetActive()?.Bookmarks;
            if (bms == null) return;
            if (bms.Contains(line))
                bms.Remove(line);
            else
                bms.Add(line);
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        #endregion

        #region View Menu Handlers

        internal void ZoomIn_Click(object? sender, EventArgs e)
        {
            if (zoomFactor < 5.0f)
            {
                zoomFactor += 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                textEditor.SyncCaretWidth();
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                gutterPanel?.UpdateLineNumberWidth();
                SyncGutterColumnWidth();
                textEditor.Invalidate();
            }
        }

        internal void ZoomOut_Click(object? sender, EventArgs e)
        {
            if (zoomFactor > 0.5f)
            {
                zoomFactor -= 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                textEditor.SyncCaretWidth();
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                gutterPanel?.UpdateLineNumberWidth();
                SyncGutterColumnWidth();
                textEditor.Invalidate();
            }
        }

        private void RestoreDefaultZoom_Click(object? sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            textEditor.ZoomFactor = zoomFactor;
            textEditor.SyncCaretWidth();
            zoomLabel.Text = "100%";
            gutterPanel?.UpdateLineNumberWidth();
            SyncGutterColumnWidth();
            textEditor.Invalidate();
        }

        private void SyncGutterColumnWidth()
        {
            if (gutterPanel == null || mainTable == null) return;
            gutterPanel.UpdateLineNumberWidth();
            // If the column is Absolute (set by Vim engine for hide/show), keep Width in sync.
            if (mainTable.ColumnStyles.Count > 0 &&
                mainTable.ColumnStyles[0].SizeType == SizeType.Absolute)
            {
                mainTable.ColumnStyles[0].Width = gutterPanel.Width;
                mainTable.PerformLayout();
            }
        }

        /// <summary>
        /// Directly applies VimEngine's ShowLineNumbers / RelativeNumbers / GutterVisible state to
        /// the gutter panel. Must be called after setting VimEngine properties from UI, because the
        /// VimEngine property setters are plain auto-properties that do not fire the On*Changed events.
        /// </summary>
        private void ApplyGutterLineNumberState()
        {
            if (gutterPanel == null) return;

            if (!(vimEngine?.GutterVisible ?? true))
            {
                gutterPanel.Visible = false;
                gutterPanel.ShowLineNumbers = false;
                gutterPanel.RelativeNumbers = false;
                gutterPanel.Width = 0;
                if (mainTable != null)
                {
                    mainTable.ColumnStyles[0].SizeType = SizeType.Absolute;
                    mainTable.ColumnStyles[0].Width = 0;
                    mainTable.PerformLayout();
                }
            }
            else
            {
                gutterPanel.ShowLineNumbers = vimEngine?.ShowLineNumbers ?? false;
                gutterPanel.RelativeNumbers = vimEngine?.RelativeNumbers ?? false;
                gutterPanel.UpdateLineNumberWidth();
                gutterPanel.Visible = true;
                if (mainTable != null)
                {
                    mainTable.ColumnStyles[0].SizeType = SizeType.Absolute;
                    mainTable.ColumnStyles[0].Width = gutterPanel.Width;
                    mainTable.PerformLayout();
                }
            }

            gutterPanel.MarkDataDirty();
            if (gutterMenuItem != null) gutterMenuItem.Checked = vimEngine?.GutterVisible ?? false;
        }

        private void StatusBar_Click(object? sender, EventArgs e) => ToggleStatusBar();

        private void ColumnGuide_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                string text = item.Text?.Replace("&", "") ?? "";
                if (text == "Custom...")
                {
                    OpenCustomColumnDialog();
                    return;
                }
                if (int.TryParse(text, out int col))
                {
                    SetGuideColumn(col);
                }
            }
        }

        private void SplitVertical_Click(object? sender, EventArgs e)
        {
            if (documents.Count > 0)
                SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Right);
        }

        private void SplitHorizontal_Click(object? sender, EventArgs e)
        {
            if (documents.Count > 0)
                SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Bottom);
        }

        private void SyntaxHighlighting_Click(object? sender, EventArgs e) => ToggleSyntaxHighlighting();

        private void WordWrap_Click(object? sender, EventArgs e) => ToggleWordWrap();
        private void GutterMenuItem_Click(object? sender, EventArgs e) => ToggleGutter();

        private void ShowGutterBreakpoints_Click(object? sender, EventArgs e)
        {
            bool show = showGutterBreakpointsMenuItem?.Checked ?? true;
            if (gutterPanel != null)
            {
                gutterPanel.ShowBreakpoints = show;
                SyncGutterColumnWidth();
                gutterPanel.MarkDataDirty();
            }
            SaveSettings();
        }

        private void ShowGutterBookmarks_Click(object? sender, EventArgs e)
        {
            bool show = showGutterBookmarksMenuItem?.Checked ?? true;
            if (gutterPanel != null)
            {
                gutterPanel.ShowBookmarks = show;
                SyncGutterColumnWidth();
                gutterPanel.MarkDataDirty();
            }
            SaveSettings();
        }

        private void ToggleWhitespace_Click(object? sender, EventArgs e)
        {
            textEditor.ShowWhitespace = whitespaceMenuItem.Checked;
        }

        private void OnFeedUpdated()
        {
            if (_notificationStatusLabel is null || _notificationStatusLabel.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(OnFeedUpdated);
                return;
            }

            UpdateNotificationBadge();
            // ShowToastForNewItems(); // Disabled for less visual intensity
        }

        public void ShowNotification(string title, string summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowNotification(title, summary));
                return;
            }

            // Delay notifications during the first 15 seconds after launch
            if ((DateTime.UtcNow - _startTime).TotalSeconds < NotificationDelaySeconds)
            {
                _delayedNotifications.Add((title, summary));
                return;
            }

            _notificationFeed.AddNotification(title, summary);

            var id = $"app-{Guid.NewGuid():N}";
            var item = new FeedItem
            {
                Id = id,
                Source = FeedSource.Custom,
                Title = title,
                Summary = summary,
                Published = DateTime.UtcNow,
                IsRead = false
            };
            // ShowToast(item); // Disabled for less visual intensity
        }

        private void ShowToast(FeedItem item, int stackIndex = -1)
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int toastHeight = 130;
            int gap = 8;
            int yOffset = stackIndex >= 0
                ? stackIndex * (toastHeight + gap)
                : _activeToasts.Count * (toastHeight + gap);
            int bottomY = screen.Bottom - toastHeight - 16 - yOffset;
            bottomY = Math.Max(screen.Top + 16, bottomY);

            var toast = new NotificationToastForm(item, bottomY);
            toast.FormClosed += (s, e) =>
            {
                _activeToasts.Remove(toast);
                toast.Dispose();
                RepositionToasts();
            };
            _activeToasts.Add(toast);
            toast.Show(this);
            if (textEditor.CanFocus)
                textEditor.Focus();
        }

        private void RepositionToasts()
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int toastHeight = 130;
            int gap = 8;
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var t = _activeToasts[i];
                if (t.IsDisposed) continue;
                int y = screen.Bottom - toastHeight - 16 - i * (toastHeight + gap);
                y = Math.Max(screen.Top + 16, y);
                t.Location = new Point(t.Location.X, y);
            }
        }

        private void FlushDelayedNotifications()
        {
            foreach (var (title, summary) in _delayedNotifications)
            {
                _notificationFeed.AddNotification(title, summary);
            }

            var unread = _notificationFeed.AllItems.Where(i => !i.IsRead).ToList();
            foreach (var item in unread)
            {
                if (_toastedIds.Add(item.Id))
                {
                    // ShowToast(item); // Disabled for less visual intensity
                }
            }
            _delayedNotifications.Clear();
        }

        private void UpdateNotificationBadge()
        {
            if (_notificationStatusLabel is null || _notificationStatusLabel.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(UpdateNotificationBadge);
                return;
            }
            int unread = _notificationFeed.UnreadCount;
            if (unread > 0)
            {
                _notificationStatusLabel.Text = $"N {unread}";
                _notificationStatusLabel.ForeColor = _themeManager.CurrentTheme.Accent;
            }
            else
            {
                _notificationStatusLabel.Text = "N";
                _notificationStatusLabel.ForeColor = _themeManager.CurrentTheme.Muted;
            }
        }

        private void ShowToastForNewItems()
        {
            // Suppress feed-generated toasts during the first 15 seconds
            if ((DateTime.UtcNow - _startTime).TotalSeconds < NotificationDelaySeconds)
                return;

            var unread = _notificationFeed.AllItems.Where(i => !i.IsRead).ToList();
            foreach (var item in unread)
            {
                if (_toastedIds.Add(item.Id))
                {
                    // ShowToast(item); // Disabled for less visual intensity
                }
            }
        }

        private void ToggleNotificationCenter(object? sender, EventArgs e)
        {
            if (_notificationCenter is not null && !_notificationCenter.IsDisposed)
            {
                var wa1 = Screen.GetWorkingArea(_notificationCenter);
                if (wa1.Contains(_notificationCenter.Bounds))
                {
                    _notificationCenter.BringToFront();
                    _notificationCenter.Focus();
                    return;
                }
                // Off-screen — close and recreate
                _notificationCenter.Close();
                _notificationCenter = null;
            }

            _notificationCenter = new NotificationCenterForm(_notificationFeed);
            _notificationCenter.UpdateTheme(_themeManager.CurrentTheme);
            _notificationCenter.FormClosed += (s, args) =>
            {
                _notificationCenter = null;
                UpdateNotificationBadge();
            };

            // Position above the status bar notification label, clamped to working area
            var wa2 = Screen.GetWorkingArea(this);
            var screenPoint = statusStrip.PointToScreen(
                new Point(_notificationStatusLabel.Bounds.Right, statusStrip.Bounds.Top - _notificationCenter.Height));
            screenPoint.X = Math.Clamp(screenPoint.X - _notificationCenter.Width + 60, wa2.Left, wa2.Right - _notificationCenter.Width);
            screenPoint.Y = Math.Clamp(screenPoint.Y, wa2.Top, wa2.Bottom - _notificationCenter.Height);
            _notificationCenter.Location = screenPoint;
            _notificationCenter.Show(this);
        }

        private void ConfigureNotifications_Click(object? sender, EventArgs e)
        {
            using var dlg = new NotificationSettingsForm(_notificationFeed);
            dlg.ShowDialog(this);
            // Refresh notification state after settings change
            OnFeedUpdated();
            _ = _notificationFeed.FetchAllAsync();
        }

        private void ApplyThemeByName(string name)
        {
            if (!ThemeManager.Themes.ContainsKey(name)) return;
            _themeManager.SetTheme(name);
            UpdateThemeColors(_themeManager.CurrentTheme);
            UpdateTerminalTheme();
            UpdateThemeDropDown();
            SaveSettings();
        }

        private void DarkTheme_Click(object? sender, EventArgs e) => ApplyThemeByName("Dark");
        private void LightTheme_Click(object? sender, EventArgs e) => ApplyThemeByName("Light");
        private void StatusBarDarkTheme_Click(object? sender, EventArgs e) => ApplyThemeByName("Dark");
        private void StatusBarLightTheme_Click(object? sender, EventArgs e) => ApplyThemeByName("Light");
        private void CatppuccinTheme_Click(object? sender, EventArgs e)
        {
            var item = sender as ToolStripMenuItem;
            if (item?.Tag is string name) ApplyThemeByName(name);
        }

        private void TabSize2_Click(object? sender, EventArgs e) => SetTabSize(2);
        private void TabSize4_Click(object? sender, EventArgs e) => SetTabSize(4);
        private void TabSize6_Click(object? sender, EventArgs e) => SetTabSize(6);
        private void TabSize8_Click(object? sender, EventArgs e) => SetTabSize(8);
        private void TabSize10_Click(object? sender, EventArgs e) => SetTabSize(10);
        private void TabSize12_Click(object? sender, EventArgs e) => SetTabSize(12);

        private void BuildConfigCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_buildConfigCombo.SelectedItem is string config && !string.IsNullOrEmpty(config))
            {
                _activeConfiguration = config;
                SaveSettings();
            }
        }

        private void UpdateThemeDropDown()
        {
            if (themeDropDown != null)
            {
                themeDropDown.Text = _themeManager.CurrentTheme.Name;
                // Rebuild theme items from the registry
                themeDropDown.DropDownItems.Clear();
                foreach (var name in ThemeManager.ThemeNames)
                {
                    var item = new ToolStripMenuItem(name);
                    item.Click += (s, e) => ApplyThemeByName(name);
                    if (name == _themeManager.CurrentTheme.Name)
                        item.Text = "\u25CF " + name;
                    themeDropDown.DropDownItems.Add(item);
                }
            }
        }

        #endregion
    }
}

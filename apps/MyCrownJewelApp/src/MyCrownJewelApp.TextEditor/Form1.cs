using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MyCrownJewelApp.TextEditor
{
    public partial class Form1 : Form
    {
        // State
        private HashSet<int> bookmarks = new();
        private HashSet<int> modifiedLines = new();
        private HashSet<int> collapsedRegions = new();
        private bool gutterVisible = true;
        private bool statusBarVisible = true;
        private bool wordWrapEnabled = true;
        private bool isDarkTheme = true;
        private float zoomFactor = 1.0f;
        private bool isHighlighting = false;
        private int lastHighlightedLine = -1;

        // Current file state
        private string? currentFilePath;
        private bool isModified = false;

        // Recent files
        private const int MaxRecentFiles = 10;
        private List<string> recentFiles = new List<string>();

        // Designer fields (accessible via Controls collection)
        public HashSet<int> Bookmarks => bookmarks;
        public HashSet<int> ModifiedLines => modifiedLines;

        // Colors
        private Color darkBackColor = Color.FromArgb(30, 30, 30);
        private Color darkForeColor = Color.FromArgb(220, 220, 220);
        private Color darkMenuBackColor = Color.FromArgb(45, 45, 45);
        private Color darkMenuForeColor = Color.FromArgb(220, 220, 220);
        private Color darkEditorBackColor = Color.FromArgb(30, 30, 30);
        private Color darkEditorForeColor = Color.FromArgb(220, 220, 220);

        private Color lightBackColor = Color.White;
        private Color lightForeColor = Color.Black;
        private Color lightMenuBackColor = SystemColors.MenuBar;
        private Color lightMenuForeColor = SystemColors.MenuText;
        private Color lightEditorBackColor = Color.White;
        private Color lightEditorForeColor = Color.Black;

        private Color keywordColor = Color.Blue;
        private Color stringColor = Color.Maroon;
        private Color commentColor = Color.Green;
        private Color numberColor = Color.DarkRed;
        private Color preprocessorColor = Color.Gray;

        private SyntaxDefinition? currentSyntax;
        private CancellationTokenSource? highlightCancelToken;
        private System.Windows.Forms.Timer? highlightTimer;

        // Incremental highlight cache
        private Dictionary<string, (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor)> compiledRegexCache = new();

        public Form1()
        {
            InitializeComponent();
            isDarkTheme = true;
            zoomFactor = 1.0f;
            // Set flat border for editor
            textEditor.BorderStyle = BorderStyle.None;
            LoadRecentFiles();
            UpdateRecentMenu();
            UpdateThemeColors(isDarkTheme);
            ApplyWordWrap();
            UpdateStatusBar();

            // Initialize syntax highlighting debounce timer
            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 150; // 150ms debounce for responsiveness
            highlightTimer.Tick += (s, e) =>
            {
                highlightTimer.Stop();
                HighlightSyntaxAsync();
            };
        }

        #region Theme & Toggles

        private void UpdateThemeColors(bool isDark)
        {
            var backColor = isDark ? darkBackColor : lightBackColor;
            var foreColor = isDark ? darkForeColor : lightForeColor;

            this.BackColor = backColor;
            this.ForeColor = foreColor;
            if (menuStrip != null)
            {
                menuStrip.BackColor = isDark ? darkMenuBackColor : lightMenuBackColor;
                menuStrip.ForeColor = isDark ? darkMenuForeColor : lightMenuForeColor;
            }
            if (textEditor != null)
            {
                textEditor.BackColor = isDark ? darkEditorBackColor : lightEditorBackColor;
                textEditor.ForeColor = isDark ? darkEditorForeColor : lightEditorForeColor;
            }
            if (gutterPanel != null)
            {
                gutterPanel.BackColor = isDark ? darkEditorBackColor : lightEditorBackColor;
            }
    // Update checkmarks
    darkThemeMenuItem.Checked = isDark;
    lightThemeMenuItem.Checked = !isDark;

    // Reapply line highlight with new color
    lastHighlightedLine = -1;
    HighlightCurrentLine();
}

        private void ToggleTheme()
        {
            isDarkTheme = !isDarkTheme;
            UpdateThemeColors(isDarkTheme);
            // Update theme menu checkmarks
            darkThemeMenuItem.Checked = isDarkTheme;
            lightThemeMenuItem.Checked = !isDarkTheme;
        }

        private void ToggleGutter()
        {
            gutterVisible = !gutterVisible;
            gutterMenuItem.Checked = gutterVisible;
            gutterPanel.Visible = gutterVisible;
            // Adjust column width: 60 when visible, 0 when hidden
            if (mainTable.ColumnCount > 0)
            {
                mainTable.ColumnStyles[0].Width = gutterVisible ? 60 : 0;
            }
            // Force redraw after layout change
            gutterPanel.RefreshGutter();
        }

        private void ToggleStatusBar()
        {
            statusBarVisible = !statusBarVisible;
            statusBarMenuItem.Checked = statusBarVisible;
            statusStrip.Visible = statusBarVisible;
            // Form will auto-layout: statusStrip dock=Bottom, mainTable dock=Fill
        }

        private void ToggleWordWrap()
        {
            wordWrapEnabled = !wordWrapEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            ApplyWordWrap();
        }

        private void ApplyWordWrap()
        {
            if (textEditor != null)
            {
                textEditor.WordWrap = wordWrapEnabled;
                // RichTextBox: ScrollBars should be Both only if WordWrap is false for horizontal
                // If WordWrap is true, horizontal scroll not needed
                textEditor.ScrollBars = wordWrapEnabled ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            }
        }

        #endregion

        #region File Operations

        private void NewFile()
        {
            if (IsModified())
            {
                var result = MessageBox.Show("Save changes to current file?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) SaveFile();
            }

            textEditor.Clear();
            currentFilePath = null;
            isModified = false;
            this.Text = "MyCrownJewelApp TextEditor - Untitled";
            UpdateStatusBar();
        }

        private void OpenFile()
        {
            if (IsModified())
            {
                var result = MessageBox.Show("Save changes to current file?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) SaveFile();
            }

            using var ofd = new OpenFileDialog();
            ofd.Filter = "Text Files|*.txt|All Files|*.*";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                textEditor.Text = File.ReadAllText(path);
                currentFilePath = path;
                isModified = false;
                this.Text = $"MyCrownJewelApp TextEditor - {Path.GetFileName(path)}";
                AddToRecentFiles(path);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFile()
        {
            if (currentFilePath == null)
            {
                SaveAsFile();
            }
            else
            {
                try
                {
                    File.WriteAllText(currentFilePath, textEditor.Text);
                    isModified = false;
                    UpdateStatusBar();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAsFile()
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Text Files|*.txt|All Files|*.*";
            sfd.FileName = currentFilePath ?? "untitled.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, textEditor.Text);
                    currentFilePath = sfd.FileName;
                    isModified = false;
                    this.Text = $"MyCrownJewelApp TextEditor - {Path.GetFileName(currentFilePath)}";
                    AddToRecentFiles(currentFilePath);
                    UpdateStatusBar();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAllFiles()
        {
            // Single document - same as SaveFile
            SaveFile();
        }

        private bool IsModified()
        {
            // For now, compare with stored state; could also compare with disk
            return isModified;
        }

        #endregion

        #region Recent Files

        private void LoadRecentFiles()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appData, "MyCrownJewelApp", "TextEditor");
                string recentFile = Path.Combine(appFolder, "recent.txt");
                if (File.Exists(recentFile))
                {
                    var lines = File.ReadAllLines(recentFile);
                    recentFiles = new List<string>(lines);
                }
            }
            catch { }
        }

        private void SaveRecentFiles()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appData, "MyCrownJewelApp", "TextEditor");
                Directory.CreateDirectory(appFolder);
                string recentFile = Path.Combine(appFolder, "recent.txt");
                File.WriteAllLines(recentFile, recentFiles.Take(MaxRecentFiles));
            }
            catch { }
        }

        private void AddToRecentFiles(string path)
        {
            // Remove if already exists
            recentFiles.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            recentFiles.Insert(0, path);
            if (recentFiles.Count > MaxRecentFiles)
                recentFiles.RemoveRange(MaxRecentFiles, recentFiles.Count - MaxRecentFiles);
            UpdateRecentMenu();
            SaveRecentFiles();
        }

        private void UpdateRecentMenu()
        {
            // Clear old items (except the "Recent >" placeholder, but we'll rebuild)
            recentMenuItem.DropDownItems.Clear();
            if (recentFiles.Count == 0)
            {
                recentMenuItem.Enabled = false;
                recentMenuItem.DropDownItems.Add("(No recent files)").Enabled = false;
            }
            else
            {
                recentMenuItem.Enabled = true;
                for (int i = 0; i < recentFiles.Count; i++)
                {
                    string filePath = recentFiles[i];
                    string display = $"{(i + 1)} {Path.GetFileName(filePath)}";
                    var item = new ToolStripMenuItem(display, null, (s, e) => LoadFile(filePath));
                    recentMenuItem.DropDownItems.Add(item);
                }
                recentMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var clearItem = new ToolStripMenuItem("Clear Recent", null, (s, e) => { recentFiles.Clear(); UpdateRecentMenu(); SaveRecentFiles(); });
                recentMenuItem.DropDownItems.Add(clearItem);
            }
        }

        #endregion

        #region File Menu Handlers

        private void NewTab_Click(object? sender, EventArgs e) => NewFile();
        private void NewWindow_Click(object? sender, EventArgs e)
        {
            // Launch new instance
            System.Diagnostics.Process.Start(Application.ExecutablePath);
        }
        private void Open_Click(object? sender, EventArgs e) => OpenFile();
        private void Save_Click(object? sender, EventArgs e) => SaveFile();
        private void SaveAs_Click(object? sender, EventArgs e) => SaveAsFile();
        private void SaveAll_Click(object? sender, EventArgs e) => SaveAllFiles();
        private void CloseTab_Click(object? sender, EventArgs e) => NewFile();  // For now, clear
        private void CloseWindow_Click(object? sender, EventArgs e) => this.Close();
        private void CloseAll_Click(object? sender, EventArgs e)
        {
            if (IsModified())
            {
                var result = MessageBox.Show("Save changes before closing all?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes) SaveFile();
            }
            Application.Exit();
        }
        private void Exit_Click(object? sender, EventArgs e) => this.Close();

        #endregion

        #region Edit Menu Handlers

        private void Undo_Click(object? sender, EventArgs e)
        {
            if (textEditor.CanUndo) textEditor.Undo();
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
            if (textEditor.SelectionLength > 0) textEditor.SelectedText = "";
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
                textEditor.Font = fd.Font;
                // Trigger gutter refresh to recalc line numbers
                if (gutterPanel != null) gutterPanel.RefreshGutter();
            }
        }

        #endregion

        #region Find & Replace

        private void Find_Click(object? sender, EventArgs e)
        {
            using var dlg = new FindReplaceDialog(this, false);
            dlg.ShowDialog(this);
        }

        private void FindNext_Click(object? sender, EventArgs e)
        {
            // Would need to store last search parameters - stub for now
            MessageBox.Show("Find Next requires storing last search parameters.", "Info");
        }

        private void FindPrevious_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("Find Previous requires storing last search parameters.", "Info");
        }

        private void Replace_Click(object? sender, EventArgs e)
        {
            using var dlg = new FindReplaceDialog(this, true);
            dlg.ShowDialog(this);
        }

        private void Goto_Click(object? sender, EventArgs e)
        {
            using var dlg = new GoToDialog(this);
            dlg.ShowDialog(this);
        }

        #endregion

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
            foreach (int bm in bookmarks)
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
            foreach (int bm in bookmarks)
            {
                if (bm < currentLine) prev = bm;
                else break;
            }
            if (prev.HasValue) GoToLine(prev.Value + 1);
        }

        private void ClearAllBookmarks_Click(object? sender, EventArgs e)
        {
            bookmarks.Clear();
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        private void ToggleFold_Click(object? sender, EventArgs e)
        {
            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            ToggleFold(currentLine);
        }

        private void ToggleAllFolds_Click(object? sender, EventArgs e)
        {
            // Toggle collapse on all #region/#endregion
            for (int i = 0; i < textEditor.Lines.Length; i++)
            {
                string line = textEditor.Lines[i];
                if (line.TrimStart().StartsWith("#region") || line.TrimStart().StartsWith("#endregion"))
                {
                    if (collapsedRegions.Contains(i))
                        collapsedRegions.Remove(i);
                    else
                        collapsedRegions.Add(i);
                }
            }
            // In a real editor would collapse/expand text
        }

        internal void ToggleBookmark(int line)
        {
            if (bookmarks.Contains(line))
                bookmarks.Remove(line);
            else
                bookmarks.Add(line);
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        internal void ToggleFold(int line)
        {
            if (collapsedRegions.Contains(line))
                collapsedRegions.Remove(line);
            else
                collapsedRegions.Add(line);
            // In real editor, would collapse code here
        }

        #endregion

        #region View Menu Handlers

        private void ZoomIn_Click(object? sender, EventArgs e)
        {
            if (zoomFactor < 5.0f)
            {
                zoomFactor += 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                if (gutterPanel != null) gutterPanel.RefreshGutter();
            }
        }

        private void ZoomOut_Click(object? sender, EventArgs e)
        {
            if (zoomFactor > 0.5f)
            {
                zoomFactor -= 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                if (gutterPanel != null) gutterPanel.RefreshGutter();
            }
        }

        private void RestoreDefaultZoom_Click(object? sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            textEditor.ZoomFactor = zoomFactor;
            zoomLabel.Text = "100%";
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        private void StatusBar_Click(object? sender, EventArgs e) => ToggleStatusBar();
        private void WordWrap_Click(object? sender, EventArgs e) => ToggleWordWrap();
        private void GutterMenuItem_Click(object? sender, EventArgs e) => ToggleGutter();

        private void DarkTheme_Click(object? sender, EventArgs e) => UpdateThemeColors(true);
        private void LightTheme_Click(object? sender, EventArgs e) => UpdateThemeColors(false);

        #endregion

        #region Public API (called by dialogs)

        public void GoToLine(int lineNumber)
        {
            if (lineNumber < 1 || textEditor.Lines.Length == 0) return;
            int targetIndex = lineNumber - 1;
            if (targetIndex >= textEditor.Lines.Length) targetIndex = textEditor.Lines.Length - 1;
            int charIndex = textEditor.GetFirstCharIndexFromLine(targetIndex);
            if (charIndex >= 0)
            {
                textEditor.SelectionStart = charIndex;
                textEditor.ScrollToCaret();
                UpdateStatusBar();
            }
        }

        public void PerformFind(string text, bool caseSensitive, bool up)
        {
            if (string.IsNullOrEmpty(text) || textEditor.Text.Length == 0) return;

            int start = textEditor.SelectionStart;
            int length = textEditor.Text.Length;

            StringComparison comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            int found = -1;

            if (up)
            {
                for (int i = start - text.Length; i >= 0; i--)
                {
                    if (i + text.Length <= length && textEditor.Text.Substring(i, text.Length).Equals(text, comparison))
                    {
                        found = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = start + 1; i <= length - text.Length; i++)
                {
                    if (textEditor.Text.Substring(i, text.Length).Equals(text, comparison))
                    {
                        found = i;
                        break;
                    }
                }
            }

            if (found >= 0)
            {
                textEditor.SelectionStart = found;
                textEditor.SelectionLength = text.Length;
                textEditor.ScrollToCaret();
                UpdateStatusBar();
            }
            else
            {
                MessageBox.Show("Cannot find \"" + text + "\".", "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void PerformReplace(string findText, string replaceText, bool caseSensitive, bool replaceAll)
        {
            if (string.IsNullOrEmpty(findText)) return;

            StringComparison comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

            if (replaceAll)
            {
                int startIndex = 0;
                while (startIndex < textEditor.Text.Length)
                {
                    int found = textEditor.Text.IndexOf(findText, startIndex, comparison);
                    if (found < 0) break;
                    textEditor.Text = textEditor.Text.Remove(found, findText.Length).Insert(found, replaceText);
                    startIndex = found + replaceText.Length;
                }
                isModified = true;
            }
            else
            {
                if (textEditor.SelectionLength > 0 && textEditor.SelectedText.Equals(findText, comparison))
                {
                    textEditor.SelectedText = replaceText;
                    isModified = true;
                }
                else
                {
                    PerformFind(findText, caseSensitive, false);
                    if (textEditor.SelectionLength > 0 && textEditor.SelectedText.Equals(findText, comparison))
                    {
                        textEditor.SelectedText = replaceText;
                        isModified = true;
                    }
                }
            }
            UpdateStatusBar();
        }

        #endregion

        #region Editor Event Handlers

        private Color GetCurrentLineHighlightColor()
        {
            // VS Code style: subtle lighter/darker than background
            return isDarkTheme ? Color.FromArgb(60, 60, 60) : Color.FromArgb(230, 230, 230);
        }

        private void HighlightCurrentLine()
        {
            if (isHighlighting) return;
            isHighlighting = true;
            try
            {
                int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
                if (currentLine == lastHighlightedLine) return;

                // Save current selection
                int savedStart = textEditor.SelectionStart;
                int savedLength = textEditor.SelectionLength;

                // Clear previous highlight
                if (lastHighlightedLine >= 0 && lastHighlightedLine < textEditor.Lines.Length)
                {
                    int prevStart = textEditor.GetFirstCharIndexFromLine(lastHighlightedLine);
                    int prevLen = textEditor.Lines[lastHighlightedLine].Length;
                    if (prevStart >= 0 && prevLen > 0)
                    {
                        textEditor.Select(prevStart, prevLen);
                        textEditor.SelectionBackColor = textEditor.BackColor;
                    }
                }

                // Apply new highlight
                if (currentLine >= 0 && currentLine < textEditor.Lines.Length)
                {
                    int start = textEditor.GetFirstCharIndexFromLine(currentLine);
                    int len = textEditor.Lines[currentLine].Length;
                    if (start >= 0)
                    {
                        textEditor.Select(start, Math.Max(len, 1)); // ensure at least 1 char for empty lines
                        textEditor.SelectionBackColor = GetCurrentLineHighlightColor();
                    }
                }
                lastHighlightedLine = currentLine;

                // Restore original selection
                textEditor.SelectionStart = savedStart;
                textEditor.SelectionLength = savedLength;
            }
            finally
            {
                isHighlighting = false;
            }
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            isModified = true;
            UpdateStatusBar();

            // Restart debounce timer for syntax highlighting
            if (highlightTimer != null)
            {
                highlightTimer.Stop();
                highlightTimer.Start();
            }
        }

        private void TextEditor_SelectionChanged(object? sender, EventArgs e)
        {
            HighlightCurrentLine();
            UpdateStatusBar();
        }

        private void TextEditor_VScroll(object? sender, EventArgs e)
        {
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        private void TextEditor_Resize(object? sender, EventArgs e)
        {
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        private void UpdateStatusBar()
        {
            if (statusStrip == null || textEditor == null) return;

            // Line and column (1-based)
            int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int col = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)) + 1;
            lineColLabel.Text = $"Ln {line}, Col {col}";

            // Character count
            charCountLabel.Text = $"{textEditor.Text.Length:N0} characters";

            // Zoom
            zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";

            // Line endings (Windows)
            lineEndingsLabel.Text = "Windows (CRLF)";

            // Encoding
            encodingLabel.Text = "UTF-8";
        }

        #endregion

        #region Syntax Highlighting (async, incremental, visible-range)

        private Color GetKeywordColor() => keywordColor;
        private Color GetStringColor() => stringColor;
        private Color GetCommentColor() => commentColor;
        private Color GetNumberColor() => numberColor;
        private Color GetPreprocessorColor() => preprocessorColor;

        // Detect syntax from file extension
        private void DetectSyntaxFromFile()
        {
            if (currentFilePath != null)
            {
                currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
            }
            else
            {
                currentSyntax = SyntaxDefinition.CSharp; // default
            }
        }

        // Get visible line range in the editor (+ buffer)
        private (int firstLine, int lastLine) GetVisibleLineRange()
        {
            try
            {
                int visibleStart = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, 0)));
                int visibleEnd = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, textEditor.ClientSize.Height)));
                // Add buffer lines for smooth scrolling
                int buffer = 100;
                int totalLines = textEditor.Lines.Length;
                int first = Math.Max(0, visibleStart - buffer);
                int last = Math.Min(totalLines - 1, visibleEnd + buffer);
                return (first, last);
            }
            catch
            {
                // Fallback: whole document if visible range fails
                return (0, textEditor.Lines.Length - 1);
            }
        }

        // Async non-blocking incremental syntax highlighting
        private async void HighlightSyntaxAsync()
        {
            // Cancel any pending/running highlight
            highlightCancelToken?.Cancel();

            if (currentSyntax == null)
                DetectSyntaxFromFile();
            if (currentSyntax == null || textEditor.IsDisposed) return;

            var tokenSource = new CancellationTokenSource();
            highlightCancelToken = tokenSource;
            var token = tokenSource.Token;

            // Get visible line range only
            var (firstLine, lastLine) = GetVisibleLineRange();
            if (firstLine > lastLine || token.IsCancellationRequested) return;

            Color baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;

            // Run highlighting on background thread and get line token map
            Dictionary<int, List<(int start, int length, Color color)>>? lineRanges = null;
            try
            {
                lineRanges = await Task.Run(() =>
                {
                    if (token.IsCancellationRequested) return null;

                    var regexes = GetOrCreateCompiledRegexes(currentSyntax, token);
                    var ranges = new Dictionary<int, List<(int, int, Color)>>();
                    string[] lines = textEditor.Lines;

                    for (int lineNum = firstLine; lineNum <= lastLine && !token.IsCancellationRequested; lineNum++)
                    {
                        if (lineNum >= lines.Length) break;
                        string line = lines[lineNum];
                        if (string.IsNullOrEmpty(line)) continue;

                        var colored = new bool[line.Length];

                        // Preprocessor first
                        if (regexes.preprocessor != null)
                            ApplyTokenMatches(regexes.preprocessor.Matches(line), colored, ranges, lineNum, GetPreprocessorColor());

                        if (regexes.comment != null)
                            ApplyTokenMatches(regexes.comment.Matches(line), colored, ranges, lineNum, GetCommentColor());

                        if (regexes.stringRegex != null)
                            ApplyTokenMatches(regexes.stringRegex.Matches(line), colored, ranges, lineNum, GetStringColor());

                        if (regexes.number != null)
                            ApplyTokenMatches(regexes.number.Matches(line), colored, ranges, lineNum, GetNumberColor());

                        if (regexes.keywords != null)
                            ApplyTokenMatches(regexes.keywords.Matches(line), colored, ranges, lineNum, GetKeywordColor());

                        if (regexes.types != null)
                            ApplyTokenMatches(regexes.types.Matches(line), colored, ranges, lineNum, GetKeywordColor());
                    }

                    return ranges;
                }, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }

            if (lineRanges == null || token.IsCancellationRequested || textEditor.IsDisposed) return;

            // Apply on UI thread
            if (textEditor.InvokeRequired)
            {
                try { textEditor.Invoke(new Action(() => ApplyLineRanges(lineRanges, baseColor))); }
                catch { }
            }
            else
            {
                ApplyLineRanges(lineRanges, baseColor);
            }
        }

        // Apply line-by-line highlighting (incremental)
        private void ApplyLineRanges(Dictionary<int, List<(int start, int length, Color color)>> lineRanges, Color baseColor)
        {
            if (textEditor.IsDisposed) return;

            // Save selection
            int selStart = textEditor.SelectionStart;
            int selLength = textEditor.SelectionLength;

            BeginUpdate(textEditor);
            textEditor.SuspendLayout();

            try
            {
                // For each line, reset to base then apply colors for that line
                foreach (var kvp in lineRanges)
                {
                    int lineNum = kvp.Key;
                    if (lineNum < 0 || lineNum >= textEditor.Lines.Length) continue;

                    int lineStart = textEditor.GetFirstCharIndexFromLine(lineNum);
                    if (lineStart < 0) continue;
                    int lineLen = textEditor.Lines[lineNum].Length;
                    if (lineLen == 0) continue;

                    // Reset entire line to base color
                    textEditor.Select(lineStart, lineLen);
                    textEditor.SelectionColor = baseColor;

                    // Apply colored spans
                    foreach (var (start, length, color) in kvp.Value)
                    {
                        int idx = lineStart + start;
                        if (idx >= lineStart && idx + length <= lineStart + lineLen)
                        {
                            textEditor.Select(idx, length);
                            textEditor.SelectionColor = color;
                        }
                    }
                }

                // Restore selection
                textEditor.SelectionStart = selStart;
                textEditor.SelectionLength = selLength;
                textEditor.SelectionColor = baseColor;
            }
            finally
            {
                textEditor.ResumeLayout();
                EndUpdate(textEditor);
            }
        }

        // Helper: mark a range as colored
        private static void MarkRange(bool[] colored, int start, int length)
        {
            for (int i = start; i < start + length && i < colored.Length; i++)
                colored[i] = true;
        }

        // Helper: check if range is free
        private static bool IsRangeFree(bool[] colored, int start, int length)
        {
            for (int i = start; i < start + length && i < colored.Length; i++)
                if (colored[i]) return false;
            return true;
        }

        // WinForms RichTextBox BeginUpdate/EndUpdate via native API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x0B;

        private void BeginUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            rtb.Invalidate();
        }

        // Compile and cache regexes for a syntax definition
        private static Regex? BuildRegex(string? pattern, RegexOptions options)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            return new Regex(pattern, options | RegexOptions.Compiled);
        }

        private (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor) GetOrCreateCompiledRegexes(SyntaxDefinition syntax, CancellationToken token)
        {
            string key = syntax.Name; // simple unique key
            lock (compiledRegexCache)
            {
                if (compiledRegexCache.TryGetValue(key, out var existing))
                {
                    return existing;
                }
            }

            var keywords = BuildRegex(@"\b(" + string.Join("|", syntax.Keywords.Select(Regex.Escape)) + @")\b", RegexOptions.None);
            var types = BuildRegex(@"\b(" + string.Join("|", syntax.Types.Select(Regex.Escape)) + @")\b", RegexOptions.None);
            var stringRegex = BuildRegex(syntax.StringPattern, RegexOptions.Singleline);
            var comment = BuildRegex(syntax.CommentPattern, RegexOptions.Multiline);
            var number = BuildRegex(syntax.NumberPattern, RegexOptions.None);
            var preprocessor = syntax.Preprocessor?.Length > 0
                ? BuildRegex(@"^\s*(" + string.Join("|", syntax.Preprocessor.Select(Regex.Escape)) + @")\b", RegexOptions.Multiline)
                : null;

            var tuple = (keywords, types, stringRegex, comment, number, preprocessor);
            lock (compiledRegexCache) { compiledRegexCache[key] = tuple; }
            return tuple;
        }

        // Apply token matches to line ranges, respecting priority (colored[] tracks occupied spans)
        private static void ApplyTokenMatches(MatchCollection matches, bool[] colored, Dictionary<int, List<(int start, int length, Color color)>> ranges, int lineNum, Color color)
        {
            foreach (Match m in matches)
            {
                if (m.Success && m.Index >= 0)
                {
                    int len = Math.Min(m.Length, colored.Length - m.Index);
                    if (len <= 0) continue;
                    bool free = true;
                    for (int i = m.Index; i < m.Index + len && i < colored.Length; i++)
                    {
                        if (colored[i]) { free = false; break; }
                    }
                    if (free)
                    {
                        for (int i = m.Index; i < m.Index + len && i < colored.Length; i++)
                            colored[i] = true;
                        if (!ranges.ContainsKey(lineNum)) ranges[lineNum] = new List<(int, int, Color)>();
                        ranges[lineNum].Add((m.Index, len, color));
                    }
                }
            }
        }

        #endregion
    }
}

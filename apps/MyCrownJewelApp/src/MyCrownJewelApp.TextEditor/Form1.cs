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
        private bool wordWrapEnabled = false;
        private bool syntaxHighlightingEnabled = false;
        private CurrentLineHighlightMode currentLineHighlightMode = CurrentLineHighlightMode.Off;
        private int tabSize = 4;
        private bool insertSpaces = true;
        private bool isDarkTheme = true;
        private float zoomFactor = 1.0f;
        private bool isHighlighting = false;
        private int lastHighlightedLine = -1;
        private bool isFullScreen = false;
        private Rectangle normalBounds;
        private FormWindowState normalWindowState;
        private FormBorderStyle normalBorderStyle;

        // Column guide state
        private int guideColumn = 80;
        private bool showGuide = true;
        private readonly Color guideColor = Color.FromArgb(60, 60, 60);

        // Current file state
        private string? currentFilePath;
        private bool isModified = false;

        // Recent files
        private const int MaxRecentFiles = 10;
        private List<string> recentFiles = new List<string>();

        // Designer fields (accessible via Controls collection)
        public HashSet<int> Bookmarks => bookmarks;
        public HashSet<int> ModifiedLines => modifiedLines;
        public HashSet<int> CollapsedRegions => collapsedRegions;

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
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            isDarkTheme = true;
            zoomFactor = 1.0f;
            // Set flat border for editor
            textEditor.BorderStyle = BorderStyle.None;
            
            // Default feature states (all off)
            wordWrapEnabled = false;
            syntaxHighlightingEnabled = false;
            gutterVisible = false;
            showGuide = false;
            
            LoadRecentFiles();
            UpdateRecentMenu();
            UpdateThemeColors(isDarkTheme);
            ApplyWordWrap();
            UpdateStatusBar();
            UpdateColumnGuideMenuChecked();
            
            // Initialize toggles to match defaults
            gutterMenuItem.Checked = gutterVisible;
            columnGuideMenuItem.Checked = showGuide;
            minimapMenuItem.Checked = false; // minimap off by default
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            
            // Apply visibility states
            gutterPanel.Visible = gutterVisible;
            guidePanel.Visible = showGuide;
            minimapControl.Visible = false;
            
            // Set initial column widths for visible state
            if (mainTable.ColumnCount >= 1)
                mainTable.ColumnStyles[0].Width = gutterVisible ? 60 : 0;
            
            // Attach minimap to editor
            if (minimapControl != null)
            {
                minimapControl.AttachEditor(textEditor);
                minimapControl.ViewportChanged += MinimapControl_ViewportChanged;
                minimapControl.SetTokenProvider(GetTokensForLine);
                PositionMinimap(); // initial placement
            }
            
            // Initialize syntax highlighting debounce timer
            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 150; // 150ms debounce for responsiveness
            highlightTimer.Tick += (s, e) =>
            {
                highlightTimer.Stop();
                if (syntaxHighlightingEnabled)
                {
                    HighlightSyntaxAsync();
                }
            };
        }

        private void MinimapControl_ViewportChanged(object? sender, ViewportChangedEventArgs e)
        {
            // Optional: update status bar or perform other actions when viewport changes
            // Could sync with editor if needed; minimap already scrolls editor directly
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
            if (minimapControl != null)
            {
                minimapControl.BackColor = isDark ? darkEditorBackColor : lightEditorBackColor;
                minimapControl.ViewportColor = isDark ? Color.FromArgb(100, Color.DodgerBlue) : Color.FromArgb(80, Color.LightBlue);
                minimapControl.ViewportBorderColor = Color.DodgerBlue;
                minimapControl.RefreshNow(); // Regenerate buffer with new colors
            }
            if (guidePanel != null)
            {
                // Subtle guide line that contrasts with background
                guidePanel.GuideColor = isDark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(120, 120, 120);
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

        private void ToggleMinimap()
        {
            bool visible = minimapMenuItem.Checked;
            if (minimapControl != null)
            {
                minimapControl.Visible = visible;
                if (visible) PositionMinimap();
            }
        }

        private void PositionMinimap()
        {
            if (minimapControl == null || !minimapControl.Visible) return;
            
            int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
            int x = textEditor.ClientSize.Width - scrollbarWidth - minimapControl.Width;
            if (x < 0) x = 0;
            minimapControl.Location = new Point(x, 0);
            minimapControl.Height = textEditor.ClientSize.Height;
        }

        private void MinimapMenuItem_Click(object? sender, EventArgs e)
        {
            ToggleMinimap();
        }

        private void ToggleWordWrap()
        {
            wordWrapEnabled = !wordWrapEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            ApplyWordWrap();
        }

        private void ToggleSyntaxHighlighting()
        {
            syntaxHighlightingEnabled = !syntaxHighlightingEnabled;
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            if (syntaxHighlightingEnabled)
            {
                highlightTimer?.Stop();
                highlightTimer?.Start();
            }
            else
            {
                highlightCancelToken?.Cancel();
                var baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
                int selStart = textEditor.SelectionStart, selLength = textEditor.SelectionLength;
                BeginUpdate(textEditor);
                textEditor.SuspendLayout();
                try
                {
                    textEditor.SelectAll();
                    textEditor.SelectionColor = baseColor;
                    textEditor.SelectionStart = selStart;
                    textEditor.SelectionLength = selLength;
                }
                finally
                {
                    textEditor.ResumeLayout();
                    EndUpdate(textEditor);
                }
            }
        }

        // Tab handling
        private void InsertSpaces_Click(object? sender, EventArgs e) => ToggleInsertSpaces();
        private void SetTabSize(int size)
        {
            tabSize = size;
            UpdateTabSizeMenu();
            UpdateStatusBar();
        }
        private void UpdateTabSizeMenu()
        {
            if (tab2MenuItem != null && tab4MenuItem != null && tab6MenuItem != null &&
                tab8MenuItem != null && tab10MenuItem != null && tab12MenuItem != null)
            {
                tab2MenuItem.Checked = (tabSize == 2);
                tab4MenuItem.Checked = (tabSize == 4);
                tab6MenuItem.Checked = (tabSize == 6);
                tab8MenuItem.Checked = (tabSize == 8);
                tab10MenuItem.Checked = (tabSize == 10);
                tab12MenuItem.Checked = (tabSize == 12);
            }
        }
        private void ToggleInsertSpaces()
        {
            insertSpaces = !insertSpaces;
            insertSpacesMenuItem.Checked = insertSpaces;
        }

        // Current line highlight mode cycling
        private void CurrentLineHighlightMode_Click(object? sender, EventArgs e)
        {
            // Cycle: Off -> NumberOnly -> WholeLine -> Off
            currentLineHighlightMode = currentLineHighlightMode switch
            {
                CurrentLineHighlightMode.Off => CurrentLineHighlightMode.NumberOnly,
                CurrentLineHighlightMode.NumberOnly => CurrentLineHighlightMode.WholeLine,
                CurrentLineHighlightMode.WholeLine => CurrentLineHighlightMode.Off,
                _ => CurrentLineHighlightMode.Off
            };
            
            UpdateCurrentLineHighlightMenu();
            
            if (currentLineHighlightMode == CurrentLineHighlightMode.Off)
            {
                ClearCurrentLineHighlight();
            }
            else
            {
                lastHighlightedLine = -1;
                HighlightCurrentLine();
                gutterPanel?.RefreshGutter();
            }
        }

        private void UpdateCurrentLineHighlightMenu()
        {
            if (currentLineOffMenuItem != null && currentLineNumberOnlyMenuItem != null && currentLineWholeLineMenuItem != null)
            {
                currentLineOffMenuItem.Checked = (currentLineHighlightMode == CurrentLineHighlightMode.Off);
                currentLineNumberOnlyMenuItem.Checked = (currentLineHighlightMode == CurrentLineHighlightMode.NumberOnly);
                currentLineWholeLineMenuItem.Checked = (currentLineHighlightMode == CurrentLineHighlightMode.WholeLine);
            }
        }

        private void ClearCurrentLineHighlight()
        {
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
            lastHighlightedLine = -1;
        }

        // Tab key handling
        private void TextEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                HandleTab(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HandleTab(KeyEventArgs e)
        {
            if (textEditor == null) return;
            
            string tabString = insertSpaces ? new string(' ', tabSize) : "\t";
            
            if (e.Shift)
            {
                UnindentSelection();
            }
            else
            {
                int selStart = textEditor.SelectionStart;
                int selLength = textEditor.SelectionLength;
                
                if (selLength > 0)
                {
                    IndentSelection();
                }
                else
                {
                    BeginUpdate(textEditor);
                    textEditor.SuspendLayout();
                    try
                    {
                        textEditor.Text = textEditor.Text.Insert(selStart, tabString);
                        textEditor.SelectionStart = selStart + tabString.Length;
                    }
                    finally
                    {
                        EndUpdate(textEditor);
                        textEditor.ResumeLayout();
                    }
                }
            }
            
            UpdateStatusBar();
        }

        private void IndentSelection()
        {
            if (textEditor == null) return;
            int start = textEditor.SelectionStart;
            int end = textEditor.SelectionStart + textEditor.SelectionLength;
            int startLine = textEditor.GetLineFromCharIndex(start);
            int endLine = textEditor.GetLineFromCharIndex(end);
            string tabString = insertSpaces ? new string(' ', tabSize) : "\t";
            
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                for (int line = endLine; line >= startLine; line--)
                {
                    if (line < 0 || line >= textEditor.Lines.Length) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
                    if (lineStart < 0) continue;
                    textEditor.Text = textEditor.Text.Insert(lineStart, tabString);
                }
                
                textEditor.SelectionStart = start + tabString.Length;
                int selLen = textEditor.SelectionLength + (endLine - startLine + 1) * tabString.Length;
                textEditor.SelectionLength = selLen;
            }
            finally
            {
                EndUpdate(textEditor);
                textEditor.ResumeLayout();
            }
        }

        private void UnindentSelection()
        {
            if (textEditor == null) return;
            int start = textEditor.SelectionStart;
            int end = textEditor.SelectionStart + textEditor.SelectionLength;
            int startLine = textEditor.GetLineFromCharIndex(start);
            int endLine = textEditor.GetLineFromCharIndex(end);
            
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                for (int line = endLine; line >= startLine; line--)
                {
                    if (line < 0 || line >= textEditor.Lines.Length) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
                    if (lineStart < 0) continue;
                    
                    string lineText = textEditor.Lines[line];
                    int removeCount = 0;
                    foreach (char c in lineText)
                    {
                        if (char.IsWhiteSpace(c))
                            removeCount++;
                        else
                            break;
                    }
                    removeCount = Math.Min(removeCount, tabSize);
                    if (removeCount > 0)
                    {
                        textEditor.Text = textEditor.Text.Remove(lineStart, removeCount);
                    }
                }
                
                int newStart = Math.Max(0, start - tabSize);
                textEditor.SelectionStart = newStart;
                int selLen = textEditor.SelectionLength - (endLine - startLine + 1) * tabSize;
                textEditor.SelectionLength = Math.Max(0, selLen);
            }
            finally
            {
                EndUpdate(textEditor);
                textEditor.ResumeLayout();
            }
        }

        private void OpenCustomColumnDialog()
        {
            using var dlg = new Form()
            {
                Text = "Column Guide Position",
                Size = new Size(300, 120),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label label = new Label() { Text = "Column:", Location = new Point(10, 15), AutoSize = true };
            TextBox textBox = new TextBox() { Location = new Point(60, 12), Width = 80, Text = guideColumn.ToString() };
            Button ok = new Button() { Text = "OK", Location = new Point(60, 50), DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Location = new Point(150, 50), DialogResult = DialogResult.Cancel };
            dlg.Controls.AddRange(new Control[] { label, textBox, ok, cancel });
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (int.TryParse(textBox.Text, out int col) && col > 0)
                {
                    SetGuideColumn(col);
                }
            }
        }

        private void SetGuideColumn(int column)
        {
            guideColumn = column;
            showGuide = true;
            columnGuideMenuItem.Checked = true;
            UpdateColumnGuideMenuChecked();
            guidePanel?.Invalidate();
        }

        private void UpdateColumnGuideMenuChecked()
        {
            if (col72MenuItem == null || col80MenuItem == null || col100MenuItem == null ||
                col120MenuItem == null || col150MenuItem == null || colCustomMenuItem == null)
                return;

            col72MenuItem.Checked = (guideColumn == 72);
            col80MenuItem.Checked = (guideColumn == 80);
            col100MenuItem.Checked = (guideColumn == 100);
            col120MenuItem.Checked = (guideColumn == 120);
            col150MenuItem.Checked = (guideColumn == 150);
            colCustomMenuItem.Checked = !(guideColumn == 72 || guideColumn == 80 || guideColumn == 100 || guideColumn == 120 || guideColumn == 150);
        }

        private void ApplyWordWrap()
        {
            if (textEditor == null) return;
            textEditor.WordWrap = wordWrapEnabled;
            textEditor.ScrollBars = wordWrapEnabled ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            if (gutterPanel != null) gutterPanel.RefreshGutter();
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
                 
                 // Ensure editor starts at top: set caret at 0 and scroll via Win32
                 textEditor.SelectionStart = 0;
                 if (textEditor.IsHandleCreated)
                 {
                     SendMessage(textEditor.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                 }
                 
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
                guidePanel?.Invalidate();
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
                guidePanel?.Invalidate();
            }
        }

        private void RestoreDefaultZoom_Click(object? sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            textEditor.ZoomFactor = zoomFactor;
            zoomLabel.Text = "100%";
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            guidePanel?.Invalidate();
        }

        private void StatusBar_Click(object? sender, EventArgs e) => ToggleStatusBar();

        private void ColumnGuide_Click(object? sender, EventArgs e)
        {
            // Menu item's Checked state is the new desired state
            bool visible = columnGuideMenuItem.Checked;
            showGuide = visible;
            if (guidePanel != null) guidePanel.ShowGuide = visible;
        }

        private void SyntaxHighlighting_Click(object? sender, EventArgs e) => ToggleSyntaxHighlighting();

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
            if (currentLineHighlightMode == CurrentLineHighlightMode.Off) return;
            
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

                // Apply new highlight based on mode
                if (currentLineHighlightMode == CurrentLineHighlightMode.WholeLine)
                {
                    if (currentLine >= 0 && currentLine < textEditor.Lines.Length)
                    {
                        int start = textEditor.GetFirstCharIndexFromLine(currentLine);
                        int len = textEditor.Lines[currentLine].Length;
                        if (start >= 0)
                        {
                            textEditor.Select(start, Math.Max(len, 1));
                            textEditor.SelectionBackColor = GetCurrentLineHighlightColor();
                        }
                    }
                }
                // NumberOnly mode: no text background highlight; gutter will draw bold number

                lastHighlightedLine = currentLine;

                // Restore original selection (no forced ScrollToCaret to avoid jumping)
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

            // Restart debounce timer for syntax highlighting (only if enabled)
            if (syntaxHighlightingEnabled && highlightTimer != null)
            {
                highlightTimer.Stop();
                highlightTimer.Start();
            }
        }

        private void TextEditor_SelectionChanged(object? sender, EventArgs e)
        {
            HighlightCurrentLine();
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            UpdateStatusBar();
        }


        private void TextEditor_VScroll(object? sender, EventArgs e)
        {
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            if (syntaxHighlightingEnabled)
            {
                highlightTimer?.Stop();
                highlightTimer?.Start();
            }
            guidePanel?.Invalidate();
        }

         private void TextEditor_Resize(object? sender, EventArgs e)
         {
             if (gutterPanel != null) gutterPanel.RefreshGutter();
             if (syntaxHighlightingEnabled)
             {
                 highlightTimer?.Stop();
                 highlightTimer?.Start();
             }
             guidePanel?.Invalidate();
             PositionMinimap();
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

            // Tab size
            tabSizeLabel.Text = $"Tab: {tabSize}";

            // Current line / total lines
            int currentLineNum = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int totalLines = textEditor.Lines.Length;
            linePositionLabel.Text = $"{currentLineNum} / {totalLines}";

            // Scroll percentage (current line / total lines)
            int total = textEditor.Lines.Length;
            if (total > 0)
            {
                int current = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
                int percent = (int)((double)current / total * 100);
                zoomLabel.Text = $"{percent}%";
            }
            else
            {
                zoomLabel.Text = "100%";
            }

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

        // Get visible line range in the editor (no buffer — only truly visible lines)
        private (int firstLine, int lastLine) GetVisibleLineRange()
        {
            try
            {
                int visibleStart = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, 0)));
                int visibleEnd = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, textEditor.ClientSize.Height)));
                int totalLines = textEditor.Lines.Length;
                // No buffer: only highlight lines that are actually visible
                int first = Math.Max(0, visibleStart);
                int last = Math.Min(totalLines - 1, visibleEnd);
                // If viewport is invalid (e.g., control not yet laid out), return empty range
                if (first > last) return (0, -1);
                return (first, last);
            }
            catch
            {
                // Return empty range on any error
                return (0, -1);
            }
        }

        // Async non-blocking incremental syntax highlighting
        private async void HighlightSyntaxAsync()
        {
            // Cancel any pending/running highlight
            highlightCancelToken?.Cancel();

            // Small debounce delay to let cancellations propagate and coalesce rapid events
            await Task.Delay(80);

            if (textEditor.IsDisposed) return;

            // Re-detect syntax based on current file path
            DetectSyntaxFromFile();

            Color baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;

            // If no syntax definition (e.g. .txt), reset visible area to base color and exit
            if (currentSyntax == null)
            {
                if (textEditor.InvokeRequired)
                {
                    try { textEditor.Invoke(new Action(() => ResetVisibleRangeToBase(baseColor))); }
                    catch { }
                }
                else
                {
                    ResetVisibleRangeToBase(baseColor);
                }
                return;
            }

            var tokenSource = new CancellationTokenSource();
            highlightCancelToken = tokenSource;
            var token = tokenSource.Token;

            // Capture visible range at this moment
            var (firstLine, lastLine) = GetVisibleLineRange();
            if (firstLine > lastLine || token.IsCancellationRequested) return;

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

            // Strict check: viewport must be unchanged (exact match)
            var currentVisible = GetVisibleLineRange();
            if (currentVisible.firstLine != firstLine || currentVisible.lastLine != lastLine)
            {
                return;
            }

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
            int selStart = textEditor.SelectionStart, selLength = textEditor.SelectionLength;
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                foreach (var kvp in lineRanges)
                {
                    int lineNum = kvp.Key;
                    if (lineNum < 0 || lineNum >= textEditor.Lines.Length) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(lineNum);
                    if (lineStart < 0) continue;
                    int lineLen = textEditor.Lines[lineNum].Length;
                    if (lineLen == 0) continue;
                    textEditor.Select(lineStart, lineLen);
                    textEditor.SelectionColor = baseColor;
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

         // Reset all visible lines to base color (used when no syntax highlighting)
         private void ResetVisibleRangeToBase(Color baseColor)
         {
             if (textEditor.IsDisposed) return;
             int selStart = textEditor.SelectionStart, selLength = textEditor.SelectionLength;
             BeginUpdate(textEditor);
             textEditor.SuspendLayout();
             try
             {
                 var (firstLine, lastLine) = GetVisibleLineRange();
                 if (firstLine <= lastLine)
                 {
                     for (int lineNum = firstLine; lineNum <= lastLine; lineNum++)
                     {
                         if (lineNum < 0 || lineNum >= textEditor.Lines.Length) continue;
                         int lineStart = textEditor.GetFirstCharIndexFromLine(lineNum);
                         if (lineStart < 0) continue;
                         int lineLen = textEditor.Lines[lineNum].Length;
                         if (lineLen == 0) continue;
                         textEditor.Select(lineStart, lineLen);
                         textEditor.SelectionColor = baseColor;
                     }
                 }
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

         /// <summary>
         /// Returns tokens for a given line index, used by MinimapControl for syntax coloring.
         /// </summary>
         private IReadOnlyList<MyCrownJewelApp.TextEditor.TokenInfo> GetTokensForLine(int lineIndex)
         {
             if (currentSyntax == null) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();
             if (lineIndex < 0 || lineIndex >= textEditor.Lines.Length) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();
             
             string line = textEditor.Lines[lineIndex];
             if (string.IsNullOrEmpty(line)) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();
             
             var tokens = new List<MyCrownJewelApp.TextEditor.TokenInfo>();
             var colored = new bool[line.Length];
             
             var regexes = GetOrCreateCompiledRegexes(currentSyntax, CancellationToken.None);
             
             // Local helper to add token if region is free
             void AddMatches(System.Text.RegularExpressions.Regex? regex, MyCrownJewelApp.TextEditor.SyntaxTokenType type)
             {
                 if (regex == null) return;
                 var matches = regex.Matches(line);
                 foreach (System.Text.RegularExpressions.Match m in matches)
                 {
                     if (!m.Success) continue;
                     int start = m.Index;
                     int len = m.Length;
                     if (start < 0 || start >= colored.Length) continue;
                     if (start + len > colored.Length) len = colored.Length - start;
                     if (len <= 0) continue;
                     
                     // Check if region is completely free
                     bool free = true;
                     for (int i = start; i < start + len; i++)
                     {
                         if (colored[i])
                         {
                             free = false;
                             break;
                         }
                     }
                     if (free)
                     {
                         for (int i = start; i < start + len; i++)
                             colored[i] = true;
                         tokens.Add(new MyCrownJewelApp.TextEditor.TokenInfo
                         {
                             Type = type,
                             Text = line.Substring(start, len),
                             StartIndex = start,
                             Length = len
                         });
                     }
                 }
             }
             
             // Apply in priority order: preprocessor, comment, string, number, keywords, types
             AddMatches(regexes.preprocessor, MyCrownJewelApp.TextEditor.SyntaxTokenType.Preprocessor);
             AddMatches(regexes.comment, MyCrownJewelApp.TextEditor.SyntaxTokenType.Comment);
             AddMatches(regexes.stringRegex, MyCrownJewelApp.TextEditor.SyntaxTokenType.String);
             AddMatches(regexes.number, MyCrownJewelApp.TextEditor.SyntaxTokenType.Number);
             AddMatches(regexes.keywords, MyCrownJewelApp.TextEditor.SyntaxTokenType.Keyword);
             AddMatches(regexes.types, MyCrownJewelApp.TextEditor.SyntaxTokenType.Keyword);
             
             return tokens;
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

        // WinForms RichTextBox BeginUpdate/EndUpdate via native API (no forced invalidation)
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x0B;
        private const int EM_LINESCROLL = 0xB6;
        private const int WM_VSCROLL = 0x115;
        private const int SB_TOP = 6;

        private void BeginUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
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

        #region Fullscreen Toggle
        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ToggleFullScreen()
        {
            if (isFullScreen)
            {
                // Restore previous state
                this.FormBorderStyle = normalBorderStyle;
                this.WindowState = FormWindowState.Normal;
                if (normalWindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    this.Bounds = normalBounds;
                }
                this.TopMost = false;
                isFullScreen = false;
            }
            else
            {
                // Save current state
                normalBounds = this.Bounds;
                normalWindowState = this.WindowState;
                normalBorderStyle = this.FormBorderStyle;
                // Enter fullscreen: borderless, covers working area
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;
                this.Bounds = Screen.GetWorkingArea(this);
                this.TopMost = true;
                isFullScreen = true;
            }
        }

        // Current line highlight modes
        public enum CurrentLineHighlightMode
        {
            Off,
            NumberOnly,
            WholeLine
        }

        internal CurrentLineHighlightMode LineHighlightMode => currentLineHighlightMode;

        #endregion

        #endregion
    }
}

using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace MyCrownJewelApp.TextEditor;

public partial class Form1 : Form
{
    // Win32 API for flicker-free updates
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    private const int WM_SETREDRAW = 0x0B;

    private void BeginUpdate(RichTextBox control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, 0, 0);
    }

    private void EndUpdate(RichTextBox control)
    {
        SendMessage(control.Handle, WM_SETREDRAW, 1, 0);
        control.Invalidate();
    }

    // Debounced syntax highlighting timer
    private System.Windows.Forms.Timer syntaxHighlightTimer;

    // Suppress SelectionChanged during internal selection updates
    private bool suppressSelectionChanged = false;

    // State fields
    private string currentFile = null;
    private bool fileModified = false;
    private string originalText = null;
    private int currentZoom = 100;
    private bool wordWrapEnabled = true;
    private string findText = "";
    private bool isCaseSensitive = false;

    // Recent files
    private List<string> recentFiles = new();
    private const string RecentFilesConfig = "recentfiles.json";

    // Theme tracking
    private bool isDarkTheme = true;
    private Color darkBackColor = Color.FromArgb(30, 30, 30);
    private Color darkForeColor = Color.FromArgb(220, 220, 220);
    private Color darkMenuBackColor = Color.FromArgb(45, 45, 45);
    private Color darkMenuForeColor = Color.FromArgb(220, 220, 220);
    private Color darkStatusBackColor = Color.FromArgb(45, 45, 45);
    private Color darkStatusForeColor = Color.FromArgb(220, 220, 220);
    private Color darkEditorBackColor = Color.FromArgb(30, 30, 30);
    private Color darkEditorForeColor = Color.FromArgb(220, 220, 220);

    private Color lightBackColor = Color.White;
    private Color lightForeColor = Color.Black;
    private Color lightMenuBackColor = SystemColors.MenuBar;
    private Color lightMenuForeColor = SystemColors.MenuText;
    private Color lightStatusBackColor = SystemColors.Control;
    private Color lightStatusForeColor = SystemColors.ControlText;
    private Color lightEditorBackColor = Color.White;
    private Color lightEditorForeColor = Color.Black;

    // Syntax highlighting colors
    private Color keywordColor;
    private Color stringColor;
    private Color commentColor;
    private Color numberColor;
    private Color preprocessorColor;

    // Bookmark tracking
    private HashSet<int> bookmarks = new();

    // Change history tracking (modified lines)
    private HashSet<int> modifiedLines = new();

    // Code folding state (collapsed regions)
    private HashSet<int> collapsedRegions = new();

    // Current line highlighting
    private bool isHighlighting = false;
    private int lastHighlightedLine = -1;
    private int currentCaretLine = -1;
    private bool gutterVisible = true;
    private bool gutterUserOverride = false;
    private SyntaxDefinition currentSyntax;
    
    // Fast lookup for keywords/types (built on file open)
    private HashSet<string> keywordSet;
    private HashSet<string> typeSet;

    public Form1()
    {
        InitializeComponent();
        textEditor.WordWrap = wordWrapEnabled;
        textEditor.ZoomFactor = currentZoom / 100f;

        ApplyTheme(true);
        UpdateSyntaxHighlightingColors();
        UpdateStatusBar();
        UpdateTitle();
        UpdateGutterVisibility();
        LoadRecentFiles();
        PopulateRecentMenu();

        // Initialize debounced syntax highlighting timer
        syntaxHighlightTimer = new System.Windows.Forms.Timer();
        syntaxHighlightTimer.Interval = 300; // ms
        syntaxHighlightTimer.Tick += (s, e) =>
        {
            syntaxHighlightTimer.Stop();
            ApplySyntaxHighlightingIfCSharp();
        };
    }

    #region Properties

    public HashSet<int> Bookmarks => bookmarks;
    public HashSet<int> ModifiedLines => modifiedLines;

    #endregion

    #region Menu Event Handlers

    private void NewTab_Click(object sender, EventArgs e)
    {
        if (CheckUnsavedChanges())
        {
            textEditor.Clear();
            currentFile = null;
            fileModified = false;
            originalText = null;
            currentSyntax = null;
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
            currentCaretLine = -1;
            UpdateGutterVisibility();
            gutterPanel.RefreshGutter();
            UpdateStatusBar();
            UpdateTitle();
        }
    }

    private void NewWindow_Click(object sender, EventArgs e)
    {
        var newWindow = new Form1();
        newWindow.Show();
    }

    private void Open_Click(object sender, EventArgs e)
    {
        if (CheckUnsavedChanges())
        {
            using var dialog = new OpenFileDialog();
            dialog.Filter = "C# Files|*.cs|Text Files|*.txt|All Files|*.*";
            dialog.DefaultExt = "txt";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                OpenFile(dialog.FileName);
            }
        }
    }

    private void OpenFile(string filePath)
    {
        try
        {
            var encoding = Encoding.UTF8;
            string fileContent = File.ReadAllText(filePath, encoding);
            
            BeginUpdate(textEditor);
            textEditor.Text = fileContent;
            EndUpdate(textEditor);
            
            currentFile = filePath;
            fileModified = false;
            originalText = fileContent;
            currentSyntax = null;
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
            currentCaretLine = -1;
            AddToRecentFiles(filePath);
            UpdateModifiedLinesFromText();
            ApplySyntaxHighlightingIfCSharp();
            UpdateGutterVisibility();
            gutterPanel.RefreshGutter();
            UpdateStatusBar();
            UpdateTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save_Click(object sender, EventArgs e)
    {
        if (currentFile == null)
        {
            SaveAs_Click(sender, e);
        }
        else
        {
            SaveFile(currentFile);
        }
    }

    private void SaveAs_Click(object sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog();
        dialog.Filter = "C# Files|*.cs|Text Files|*.txt|All Files|*.*";
        dialog.DefaultExt = "txt";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            SaveFile(dialog.FileName);
            currentFile = dialog.FileName;
            fileModified = false;
            modifiedLines.Clear();
            gutterPanel.RefreshGutter();
            UpdateTitle();
        }
    }

    private void SaveFile(string filePath)
    {
        try
        {
            File.WriteAllText(filePath, textEditor.Text, Encoding.UTF8);
            fileModified = false;
            modifiedLines.Clear();
            gutterPanel.RefreshGutter();
            UpdateStatusBar();
            UpdateTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveAll_Click(object sender, EventArgs e)
    {
        if (currentFile != null)
        {
            SaveFile(currentFile);
        }
    }

    private void CloseTab_Click(object sender, EventArgs e)
    {
        if (CheckUnsavedChanges())
        {
            textEditor.Clear();
            currentFile = null;
            fileModified = false;
            originalText = null;
            currentSyntax = null;
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
            currentCaretLine = -1;
            UpdateGutterVisibility();
            gutterPanel.RefreshGutter();
            UpdateStatusBar();
            UpdateTitle();
        }
    }

    private void CloseWindow_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void CloseAll_Click(object sender, EventArgs e)
    {
        if (CheckUnsavedChanges())
        {
            textEditor.Clear();
            currentFile = null;
            fileModified = false;
            originalText = null;
            currentSyntax = null;
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
            currentCaretLine = -1;
            UpdateGutterVisibility();
            gutterPanel.RefreshGutter();
            UpdateStatusBar();
            UpdateTitle();
        }
    }

    private void Exit_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void Undo_Click(object sender, EventArgs e)
    {
        if (textEditor.CanUndo)
        {
            // Note: tracking changes through undo is complex; simplified tracking
            textEditor.Undo();
            UpdateModifiedLinesFromText();
            gutterPanel.RefreshGutter();
        }
    }

    private void Cut_Click(object sender, EventArgs e)
    {
        if (textEditor.SelectionLength > 0)
        {
            textEditor.Cut();
            UpdateModifiedLinesFromText();
            gutterPanel.RefreshGutter();
        }
    }

    private void Copy_Click(object sender, EventArgs e)
    {
        if (textEditor.SelectionLength > 0)
        {
            textEditor.Copy();
        }
    }

    private void Paste_Click(object sender, EventArgs e)
    {
        if (Clipboard.ContainsText())
        {
            textEditor.Paste();
            UpdateModifiedLinesFromText();
            gutterPanel.RefreshGutter();
        }
    }

    private void Delete_Click(object sender, EventArgs e)
    {
        if (textEditor.SelectionLength > 0)
        {
            textEditor.SelectedText = "";
            UpdateModifiedLinesFromText();
            gutterPanel.RefreshGutter();
        }
    }

    private void Find_Click(object sender, EventArgs e)
    {
        using var dialog = new FindReplaceDialog(this, false);
        dialog.ShowDialog();
    }

    private void FindNext_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(findText))
        {
            FindText(findText, false);
        }
    }

    private void FindPrevious_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(findText))
        {
            FindText(findText, true);
        }
    }

    private void Replace_Click(object sender, EventArgs e)
    {
        using var dialog = new FindReplaceDialog(this, true);
        dialog.ShowDialog();
    }

    private void Goto_Click(object sender, EventArgs e)
    {
        using var dialog = new GoToDialog(this);
        dialog.ShowDialog();
    }

    private void SelectAll_Click(object sender, EventArgs e)
    {
        textEditor.SelectAll();
    }

    private void TimeDate_Click(object sender, EventArgs e)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int selectionStart = textEditor.SelectionStart;
        textEditor.Text = textEditor.Text.Insert(selectionStart, now);
        UpdateModifiedLinesFromText();
        gutterPanel.RefreshGutter();
    }

    private void Font_Click(object sender, EventArgs e)
    {
        using var dialog = new FontDialog();
        dialog.Font = textEditor.Font;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textEditor.Font = dialog.Font;
            gutterPanel.RefreshGutter();
        }
    }

    private void ZoomIn_Click(object sender, EventArgs e)
    {
        SetZoom(currentZoom + 10);
    }

    private void ZoomOut_Click(object sender, EventArgs e)
    {
        SetZoom(currentZoom - 10);
    }

    private void RestoreDefaultZoom_Click(object sender, EventArgs e)
    {
        SetZoom(100);
    }

    private void StatusBar_Click(object sender, EventArgs e)
    {
        statusBarMenuItem.Checked = !statusBarMenuItem.Checked;
        statusStrip.Visible = statusBarMenuItem.Checked;
    }

    private void WordWrap_Click(object sender, EventArgs e)
    {
        wordWrapEnabled = !wordWrapEnabled;
        wordWrapMenuItem.Checked = wordWrapEnabled;
        textEditor.WordWrap = wordWrapEnabled;
    }

    private void GutterMenuItem_Click(object sender, EventArgs e)
    {
        gutterUserOverride = !gutterUserOverride;
        gutterMenuItem.Checked = gutterUserOverride;
        UpdateGutterVisibility();
    }

    private void UpdateGutterVisibility()
    {
        bool shouldShow = gutterUserOverride;
        if (!shouldShow && currentFile != null)
        {
            string ext = Path.GetExtension(currentFile).ToLowerInvariant();
            shouldShow = ext is ".cs" or ".cpp" or ".c" or ".bicep" or ".tf" or ".yml" or ".yaml" or ".ps1" or ".sh";
        }
        if (gutterVisible != shouldShow)
        {
            gutterVisible = shouldShow;
            gutterPanel.Visible = gutterVisible;
            mainTable.ColumnStyles[0].Width = gutterVisible ? 60 : 0;
        }
    }

    private void DarkTheme_Click(object sender, EventArgs e)
    {
        isDarkTheme = true;
        ApplyTheme(isDarkTheme);
        UpdateSyntaxHighlightingColors();
        if (currentFile != null)
        {
            ApplySyntaxHighlighting();
        }
        HighlightCurrentLine();
        gutterPanel.RefreshGutter();
        darkThemeMenuItem.Checked = isDarkTheme;
        lightThemeMenuItem.Checked = !isDarkTheme;
    }

    private void LightTheme_Click(object sender, EventArgs e)
    {
        isDarkTheme = false;
        ApplyTheme(isDarkTheme);
        UpdateSyntaxHighlightingColors();
        if (currentFile != null)
        {
            ApplySyntaxHighlighting();
        }
        HighlightCurrentLine();
        gutterPanel.RefreshGutter();
        darkThemeMenuItem.Checked = isDarkTheme;
        lightThemeMenuItem.Checked = !isDarkTheme;
    }

    #endregion

    #region Public Methods for Gutter

    public void ToggleBookmark(int line)
    {
        if (bookmarks.Contains(line))
        {
            bookmarks.Remove(line);
        }
        else
        {
            bookmarks.Add(line);
        }
        gutterPanel.RefreshGutter();
    }

    public void ToggleFold(int regionStartLine)
    {
        if (collapsedRegions.Contains(regionStartLine))
        {
            collapsedRegions.Remove(regionStartLine);
        }
        else
        {
            collapsedRegions.Add(regionStartLine);
        }
        // Note: Full folding implementation requires hiding lines in RichTextBox
        // For now, we just track state visually
        gutterPanel.RefreshGutter();
    }

    #endregion

    #region Theme Methods

    private void ApplyTheme(bool dark)
    {
        Color editorBackColor = dark ? darkEditorBackColor : lightEditorBackColor;
        Color editorForeColor = dark ? darkEditorForeColor : lightEditorForeColor;

        // Unified: all UI elements use editor's colors
        BackColor = editorBackColor;
        ForeColor = editorForeColor;

        menuStrip.BackColor = editorBackColor;
        menuStrip.ForeColor = editorForeColor;

        textEditor.BackColor = editorBackColor;
        textEditor.ForeColor = editorForeColor;

        statusStrip.BackColor = editorBackColor;
        statusStrip.ForeColor = editorForeColor;

        // Gutter matches editor background
        if (gutterPanel != null)
        {
            gutterPanel.BackColor = editorBackColor;
        }
    }

    private void UpdateSyntaxHighlightingColors()
    {
        if (isDarkTheme)
        {
            keywordColor = Color.Cyan;
            stringColor = Color.Orange;
            commentColor = Color.Green;
            numberColor = Color.Magenta;
            preprocessorColor = Color.Yellow;
        }
        else
        {
            keywordColor = Color.Blue;
            stringColor = Color.Brown;
            commentColor = Color.Green;
            numberColor = Color.Red;
            preprocessorColor = Color.Gray;
        }
    }

    #endregion

    #region Bookmark and Folding Handlers

    private void ToggleBookmark_Click(object sender, EventArgs e)
    {
        int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
        ToggleBookmark(currentLine);
    }

    private void NextBookmark_Click(object sender, EventArgs e)
    {
        int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
        int? next = bookmarks.Where(b => b > currentLine).OrderBy(b => b).FirstOrDefault();
        if (next.HasValue)
        {
            GoToLine(next.Value + 1);
        }
    }

    private void PrevBookmark_Click(object sender, EventArgs e)
    {
        int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
        int? prev = bookmarks.Where(b => b < currentLine).OrderByDescending(b => b).FirstOrDefault();
        if (prev.HasValue)
        {
            GoToLine(prev.Value + 1);
        }
    }

    private void ClearAllBookmarks_Click(object sender, EventArgs e)
    {
        bookmarks.Clear();
        gutterPanel.RefreshGutter();
    }

    private void ToggleFold_Click(object sender, EventArgs e)
    {
        int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
        ToggleFold(currentLine);
    }

    private void ToggleAllFolds_Click(object sender, EventArgs e)
    {
        bool anyExpanded = collapsedRegions.Count == 0;
        if (anyExpanded)
        {
            for (int i = 0; i < textEditor.Lines.Length; i++)
            {
                if (textEditor.Lines[i].TrimStart().StartsWith("#region"))
                {
                    collapsedRegions.Add(i);
                }
            }
        }
        else
        {
            collapsedRegions.Clear();
        }
        gutterPanel.RefreshGutter();
    }

    #endregion

    #region C# Syntax Highlighting

    private void ApplySyntaxHighlightingIfCSharp()
    {
        if (currentFile != null)
        {
            currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFile);
            BuildLookup();
            if (currentSyntax != null)
            {
                ApplySyntaxHighlighting();
            }
            else
            {
                ClearSyntaxHighlighting();
            }
        }
    }

    private void BuildLookup()
    {
        keywordSet = new HashSet<string>(StringComparer.Ordinal);
        typeSet = new HashSet<string>(StringComparer.Ordinal);
        
        if (currentSyntax?.Keywords != null)
        {
            foreach (var kw in currentSyntax.Keywords) keywordSet.Add(kw);
        }
        if (currentSyntax?.Types != null)
        {
            foreach (var t in currentSyntax.Types) typeSet.Add(t);
        }
    }

    private void ClearSyntaxHighlighting()
    {
        suppressSelectionChanged = true;
        try
        {
            BeginUpdate(textEditor);
            int selStart = textEditor.SelectionStart;
            int selLength = textEditor.SelectionLength;
            textEditor.SelectAll();
            textEditor.SelectionColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
            textEditor.SelectionStart = selStart;
            textEditor.SelectionLength = selLength;
            EndUpdate(textEditor);
        }
        finally
        {
            suppressSelectionChanged = false;
        }
    }

    private void UpdateModifiedLinesFromText()
    {
        // Simplified: mark all non-empty lines as modified (for demo)
        modifiedLines.Clear();
        for (int i = 0; i < textEditor.Lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(textEditor.Lines[i]))
            {
                modifiedLines.Add(i);
            }
        }
    }

    private void ApplySyntaxHighlighting()
    {
        if (currentSyntax == null) return;

        int selectionStart = textEditor.SelectionStart;
        int selectionLength = textEditor.SelectionLength;
        string text = textEditor.Text;

        suppressSelectionChanged = true;
        try
        {
            BeginUpdate(textEditor);

            // Reset all to default
            textEditor.SelectAll();
            textEditor.SelectionColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;

            var spans = new List<(int start, int length, Color color)>();
            int i = 0;
            int len = text.Length;
            bool inMultiLineComment = false;

            while (i < len)
            {
                char c = text[i];

                // Multi-line comment exit: */
                if (inMultiLineComment)
                {
                    if (i + 1 < len && text[i] == '*' && text[i + 1] == '/')
                    {
                        spans.Add((i, 2, commentColor));
                        i += 2;
                        inMultiLineComment = false;
                    }
                    else i++;
                    continue;
                }

                // Single-line comment: //...\n
                if (c == '/' && i + 1 < len && text[i + 1] == '/')
                {
                    int start = i;
                    while (i < len && text[i] != '\n') i++;
                    spans.Add((start, i - start, commentColor));
                    continue;
                }

                // Multi-line comment start: /*
                if (c == '/' && i + 1 < len && text[i + 1] == '*')
                {
                    i += 2;
                    inMultiLineComment = true;
                    continue;
                }

                // String: "..."
                if (c == '"')
                {
                    int start = i++;
                    while (i < len)
                    {
                        char ch = text[i];
                        if (ch == '\\' && i + 1 < len) { i += 2; continue; }
                        if (ch == '"') { i++; break; }
                        i++;
                    }
                    spans.Add((start, i - start, stringColor));
                    continue;
                }

                // Verbatim string: @"..."
                if (c == '@' && i + 1 < len && text[i + 1] == '"')
                {
                    int start = i;
                    i += 2;
                    while (i < len)
                    {
                        if (text[i] == '"')
                        {
                            if (i + 1 < len && text[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    spans.Add((start, i - start, stringColor));
                    continue;
                }

                // Preprocessor at line start (# or !) — track line start inline
                if ((c == '#' || c == '!') && (i == 0 || text[i - 1] == '\n'))
                {
                    int start = i;
                    while (i < len && !char.IsWhiteSpace(text[i]) && text[i] != '\n') i++;
                    spans.Add((start, i - start, preprocessorColor));
                    continue;
                }

                // Keyword / Identifier
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    string word = text.Substring(start, i - start);
                    if (keywordSet.Contains(word) || typeSet.Contains(word))
                        spans.Add((start, i - start, keywordColor));
                    continue;
                }

                // Number literal
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < len && char.IsDigit(text[i])) i++;

                    if (i < len && text[i] == '.')
                    {
                        i++;
                        while (i < len && char.IsDigit(text[i])) i++;
                    }

                    if (i < len && (text[i] == 'e' || text[i] == 'E'))
                    {
                        char after = (i + 1 < len) ? text[i + 1] : '\0';
                        if (after == '+' || after == '-' || char.IsDigit(after))
                        {
                            i++;
                            if (after == '+' || after == '-')
                            {
                                if (i < len && char.IsDigit(text[i]))
                                    while (i < len && char.IsDigit(text[i])) i++;
                            }
                            else while (i < len && char.IsDigit(text[i])) i++;
                        }
                    }

                    while (i < len)
                    {
                        char ch = text[i];
                        if (ch == 'f' || ch == 'F' || ch == 'd' || ch == 'D' || ch == 'm' || ch == 'M' ||
                            ch == 'u' || ch == 'U' || ch == 'l' || ch == 'L')
                        {
                            i++;
                            if (i < len)
                            {
                                char ch2 = text[i];
                                if ((ch == 'u' || ch == 'U') && (ch2 == 'l' || ch2 == 'L') ||
                                    (ch == 'l' || ch == 'L') && (ch2 == 'u' || ch2 == 'U'))
                                    i++;
                            }
                        }
                        else break;
                    }

                    spans.Add((start, i - start, numberColor));
                    continue;
                }

                i++;
            }

            // Apply spans in reverse order to preserve positions
            foreach (var span in spans.OrderByDescending(s => s.start))
            {
                textEditor.Select(span.start, span.length);
                textEditor.SelectionColor = span.color;
            }

            textEditor.SelectionStart = selectionStart;
            textEditor.SelectionLength = selectionLength;
            textEditor.SelectionColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;

            EndUpdate(textEditor);
        }
        finally
        {
            suppressSelectionChanged = false;
        }
    }

    #endregion

    #region Text Editor Events

    private void TextEditor_SelectionChanged(object sender, EventArgs e)
    {
        if (suppressSelectionChanged) return;
        UpdateStatusBar();
        HighlightCurrentLine();
    }

    private void TextEditor_TextChanged(object sender, EventArgs e)
    {
        fileModified = true;
        UpdateStatusBar();
        UpdateTitle();
        UpdateModifiedLinesFromText();

        // Restart debounced syntax highlighting timer
        syntaxHighlightTimer?.Stop();
        syntaxHighlightTimer?.Start();

        gutterPanel.RefreshGutter();
    }

    private void TextEditor_VScroll(object sender, EventArgs e)
    {
        gutterPanel.RefreshGutter();
    }

    private void TextEditor_Resize(object sender, EventArgs e)
    {
        gutterPanel.RefreshGutter();
    }

    #endregion

    #region Current Line Highlight

    private void HighlightCurrentLine()
    {
        if (isHighlighting) return;
        isHighlighting = true;
        try
        {
            int selStart = textEditor.SelectionStart;
            int selLength = textEditor.SelectionLength;
            int lineIndex = textEditor.GetLineFromCharIndex(selStart);
            
            if (lineIndex < 0 || lineIndex >= textEditor.Lines.Length) return;

            BeginUpdate(textEditor);

            // Clear previous line highlight if different
            if (lastHighlightedLine >= 0 && lastHighlightedLine != lineIndex)
            {
                ClearLineHighlight(lastHighlightedLine);
            }

            // Apply highlight to current line
            int lineStart = textEditor.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return;

            string lineText = textEditor.Lines[lineIndex];
            int lineLength = lineText.Length;

            textEditor.Select(lineStart, lineLength);
            textEditor.SelectionBackColor = isDarkTheme ? Color.FromArgb(60, 60, 60) : Color.FromArgb(230, 230, 230);
            
            // Restore selection
            textEditor.SelectionStart = selStart;
            textEditor.SelectionLength = selLength;

            EndUpdate(textEditor);

            lastHighlightedLine = lineIndex;
        }
        finally
        {
            isHighlighting = false;
        }
    }

    private void ClearLineHighlight(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= textEditor.Lines.Length) return;
        
        int lineStart = textEditor.GetFirstCharIndexFromLine(lineIndex);
        if (lineStart < 0) return;
        
        int lineLength = textEditor.Lines[lineIndex].Length;
        
        textEditor.Select(lineStart, lineLength);
        textEditor.SelectionBackColor = textEditor.BackColor;
    }

    #endregion

    #region Helper Methods

    private void UpdateStatusBar()
    {
        int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
        int col = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(line - 1) + 1;
        lineColLabel.Text = $"Ln {line}, Col {col}";
        charCountLabel.Text = $"{textEditor.TextLength} characters";
        zoomLabel.Text = $"{currentZoom}%";
        lineEndingsLabel.Text = "Windows (CRLF)";

        string fileType = "UTF-8";
        if (currentFile != null)
        {
            string ext = Path.GetExtension(currentFile);
            if (!string.IsNullOrEmpty(ext))
            {
                fileType = ext.ToUpperInvariant() + " " + fileType;
            }
        }
        encodingLabel.Text = fileType;
    }

    private void UpdateTitle()
    {
        string title = "Text Editor";
        if (currentFile != null)
        {
            string fileName = Path.GetFileName(currentFile);
            title = $"{fileName}{(fileModified ? "*" : "")} - {title}";
        }
        else if (fileModified)
        {
            title = $"*Untitled - {title}";
        }
        Text = title;
    }

    private void UpdateMenuItems()
    {
        undoMenuItem.Enabled = textEditor.CanUndo;
        cutMenuItem.Enabled = textEditor.SelectionLength > 0;
        copyMenuItem.Enabled = textEditor.SelectionLength > 0;
        deleteMenuItem.Enabled = textEditor.SelectionLength > 0;
        selectAllMenuItem.Enabled = textEditor.TextLength > 0;
    }

    private void SetZoom(int zoom)
    {
        currentZoom = Math.Max(10, Math.Min(500, zoom));
        textEditor.ZoomFactor = currentZoom / 100f;
        UpdateStatusBar();
        gutterPanel.RefreshGutter();
    }

    private bool CheckUnsavedChanges()
    {
        if (fileModified)
        {
            var result = MessageBox.Show(
                "Do you want to save changes to the current file?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Save_Click(null, null);
                return !fileModified;
            }
            else if (result == DialogResult.Cancel)
            {
                return false;
            }
        }
        return true;
    }

    #region Recent Files

    private void LoadRecentFiles()
    {
        try
        {
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyCrownJewelApp",
                RecentFilesConfig);
            
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                recentFiles = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
        }
        catch { recentFiles = new List<string>(); }
    }

    private void SaveRecentFiles()
    {
        try
        {
            string configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyCrownJewelApp");
            Directory.CreateDirectory(configDir);
            string configPath = Path.Combine(configDir, RecentFilesConfig);
            
            string json = JsonSerializer.Serialize(recentFiles.Take(10).ToList());
            File.WriteAllText(configPath, json);
        }
        catch { }
    }

    private void AddToRecentFiles(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        
        recentFiles.Remove(filePath);
        recentFiles.Insert(0, filePath);
        if (recentFiles.Count > 10) recentFiles.RemoveRange(10, recentFiles.Count - 10);
        
        SaveRecentFiles();
        PopulateRecentMenu();
    }

    private void PopulateRecentMenu()
    {
        recentMenuItem.DropDownItems.Clear();
        
        if (recentFiles.Count == 0)
        {
            recentMenuItem.Enabled = false;
            recentMenuItem.DropDownItems.Add("(No recent files)", null, null);
        }
        else
        {
            recentMenuItem.Enabled = true;
            foreach (string file in recentFiles)
            {
                string displayPath = file;
                if (file.Length > 50)
                {
                    displayPath = "..." + file.Substring(file.Length - 47);
                }
                var item = new ToolStripMenuItem(displayPath, null, (s, e) => OpenRecentFile(file));
                recentMenuItem.DropDownItems.Add(item);
            }
        }
    }

    private void OpenRecentFile(string filePath)
    {
        if (File.Exists(filePath) && CheckUnsavedChanges())
        {
            OpenFile(filePath);
        }
        else
        {
            recentFiles.Remove(filePath);
            PopulateRecentMenu();
        }
    }

    #endregion

    private void FindText(string text, bool backward)
    {
        int start = textEditor.SelectionStart + (backward ? -1 : textEditor.SelectionLength);
        int findLength = textEditor.Find(text, start, RichTextBoxFinds.None);

        if (findLength > 0)
        {
            if (backward)
            {
                textEditor.SelectionStart = findLength;
            }
            textEditor.SelectionLength = findLength;
            textEditor.ScrollToCaret();
        }
        else
        {
            MessageBox.Show($"Cannot find '{text}'", "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public void PerformFind(string text, bool caseSensitive, bool backward)
    {
        var options = RichTextBoxFinds.None;
        if (caseSensitive) options |= RichTextBoxFinds.MatchCase;

        if (textEditor.Find(text, 0, options) >= 0)
        {
            isCaseSensitive = caseSensitive;
            findText = text;
            FindText(text, backward);
        }
        else
        {
            MessageBox.Show($"Cannot find '{text}'", "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public void PerformReplace(string find, string replace, bool caseSensitive, bool replaceAll)
    {
        var options = RichTextBoxFinds.None;
        if (caseSensitive) options |= RichTextBoxFinds.MatchCase;

        if (replaceAll)
        {
            int count = 0;
            int start = 0;
            while ((start = textEditor.Find(find, start, options)) >= 0)
            {
                textEditor.SelectionStart = start;
                textEditor.SelectionLength = find.Length;
                textEditor.SelectedText = replace;
                count++;
            }
            MessageBox.Show($"Replaced {count} occurrence(s)", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            if (textEditor.Find(find, 0, options) >= 0)
            {
                textEditor.SelectedText = replace;
            }
            else
            {
                MessageBox.Show($"Cannot find '{find}'", "Replace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public void GoToLine(int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > textEditor.Lines.Length)
        {
            MessageBox.Show($"Invalid line number. Must be between 1 and {textEditor.Lines.Length}.",
                "Go To", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int index = textEditor.GetFirstCharIndexFromLine(lineNumber - 1);
        if (index >= 0)
        {
            textEditor.SelectionStart = index;
            textEditor.SelectionLength = 0;
            textEditor.ScrollToCaret();
            UpdateStatusBar();
            HighlightCurrentLine();
            gutterPanel.RefreshGutter();
        }
    }

    #endregion
}

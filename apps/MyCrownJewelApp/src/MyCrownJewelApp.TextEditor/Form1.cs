using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms;

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
    private int currentZoom = 100;
    private bool wordWrapEnabled = true;
    private string findText = "";
    private bool isCaseSensitive = false;

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

    public Form1()
    {
        InitializeComponent();
        textEditor.WordWrap = wordWrapEnabled;
        textEditor.ZoomFactor = currentZoom / 100f;

        ApplyTheme(true);
        UpdateSyntaxHighlightingColors();
        UpdateStatusBar();
        UpdateTitle();

        // Initialize debounced syntax highlighting timer
        syntaxHighlightTimer = new System.Windows.Forms.Timer();
        syntaxHighlightTimer.Interval = 300; // ms
        syntaxHighlightTimer.Tick += (s, e) =>
        {
            syntaxHighlightTimer.Stop();
            if (currentFile != null && Path.GetExtension(currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                ApplySyntaxHighlighting();
            }
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
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
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
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
            ApplySyntaxHighlightingIfCSharp();
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
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
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
            bookmarks.Clear();
            modifiedLines.Clear();
            collapsedRegions.Clear();
            lastHighlightedLine = -1;
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
        if (currentFile != null && Path.GetExtension(currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        {
            ApplySyntaxHighlighting();
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
        int selectionStart = textEditor.SelectionStart;
        int selectionLength = textEditor.SelectionLength;
        string text = textEditor.Text;

        suppressSelectionChanged = true;
        try
        {
            BeginUpdate(textEditor);

            textEditor.SelectAll();
            textEditor.SelectionColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;

            HighlightKeywords(text);
            HighlightStrings(text);
            HighlightComments(text);
            HighlightNumbers(text);
            HighlightPreprocessor(text);

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

    private void HighlightKeywords(string text)
    {
        string[] keywords = {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while", "async", "await", "record", "init"
        };

        foreach (var keyword in keywords)
        {
            HighlightPattern(@"\b" + keyword + @"\b", keywordColor);
        }
    }

    private void HighlightStrings(string text)
    {
        HighlightPattern(@"""([^""\\]|\\.)*""", stringColor);
        HighlightPattern(@"@""([^""]|"""")*""", stringColor);
    }

    private void HighlightComments(string text)
    {
        HighlightPattern(@"//.*$", commentColor);
        HighlightPattern(@"/\*.*?\*/", commentColor, RegexOptions.Singleline);
    }

    private void HighlightNumbers(string text)
    {
        HighlightPattern(@"\b\d+\.?\d*([fFlLdD]|uL?|UL?)?\b", numberColor);
    }

    private void HighlightPreprocessor(string text)
    {
        HighlightPattern(@"^\s*#\w+", preprocessorColor, RegexOptions.Multiline);
    }

    private void HighlightPattern(string pattern, Color color, RegexOptions options = RegexOptions.None)
    {
        try
        {
            var regex = new Regex(pattern, options);
            var matches = regex.Matches(textEditor.Text);

            foreach (Match match in matches)
            {
                textEditor.Select(match.Index, match.Length);
                textEditor.SelectionColor = color;
            }
        }
        catch { }
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

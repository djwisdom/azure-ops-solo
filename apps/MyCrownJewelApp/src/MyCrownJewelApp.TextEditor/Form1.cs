using System.Text;

namespace MyCrownJewelApp.TextEditor;

public partial class Form1 : Form
{
    // State fields
    private string currentFile = null;
    private bool fileModified = false;
    private int currentZoom = 100;
    private bool wordWrapEnabled = true;
    private string findText = "";
    private bool isCaseSensitive = false;

    public Form1()
    {
        InitializeComponent();
        textEditor.WordWrap = wordWrapEnabled;
        textEditor.ZoomFactor = currentZoom / 100f;
        UpdateStatusBar();
        UpdateTitle();
    }

    #region Menu Event Handlers

    private void NewTab_Click(object sender, EventArgs e)
    {
        if (CheckUnsavedChanges())
        {
            textEditor.Clear();
            currentFile = null;
            fileModified = false;
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
            dialog.Filter = "Text Files|*.txt|All Files|*.*";
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
            textEditor.Text = File.ReadAllText(filePath, Encoding.UTF8);
            currentFile = filePath;
            fileModified = false;
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
        dialog.Filter = "Text Files|*.txt|All Files|*.*";
        dialog.DefaultExt = "txt";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            SaveFile(dialog.FileName);
            currentFile = dialog.FileName;
            fileModified = false;
            UpdateTitle();
        }
    }

    private void SaveFile(string filePath)
    {
        try
        {
            File.WriteAllText(filePath, textEditor.Text, Encoding.UTF8);
            fileModified = false;
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
            textEditor.Undo();
        }
    }

    private void Cut_Click(object sender, EventArgs e)
    {
        if (textEditor.SelectionLength > 0)
        {
            textEditor.Cut();
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
        }
    }

    private void Delete_Click(object sender, EventArgs e)
    {
        if (textEditor.SelectionLength > 0)
        {
            textEditor.SelectedText = "";
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
    }

    private void Font_Click(object sender, EventArgs e)
    {
        using var dialog = new FontDialog();
        dialog.Font = textEditor.Font;
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textEditor.Font = dialog.Font;
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

    #endregion

    #region Text Editor Events

    private void TextEditor_TextChanged(object sender, EventArgs e)
    {
        fileModified = true;
        UpdateStatusBar();
        UpdateTitle();
    }

    private void TextEditor_SelectionChanged(object sender, EventArgs e)
    {
        UpdateStatusBar();
    }

    #endregion

    #region Helper Methods

    private void UpdateStatusBar()
    {
        // Line and Column
        int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
        int col = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(line - 1) + 1;
        lineColLabel.Text = $"Ln {line}, Col {col}";

        // Character count
        charCountLabel.Text = $"{textEditor.TextLength} characters";

        // Zoom
        zoomLabel.Text = $"{currentZoom}%";

        // Line endings
        lineEndingsLabel.Text = "Windows (CRLF)";

        // Encoding
        encodingLabel.Text = "UTF-8";
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
        }
    }

    #endregion
}

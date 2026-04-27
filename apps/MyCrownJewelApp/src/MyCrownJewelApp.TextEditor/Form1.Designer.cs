namespace MyCrownJewelApp.TextEditor;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    private MenuStrip menuStrip;
    private ToolStripMenuItem fileMenu;
    private ToolStripMenuItem newTabMenuItem;
    private ToolStripMenuItem newWindowMenuItem;
    private ToolStripMenuItem openMenuItem;
    private ToolStripMenuItem recentMenuItem;
    private ToolStripMenuItem saveMenuItem;
    private ToolStripMenuItem saveAsMenuItem;
    private ToolStripMenuItem saveAllMenuItem;
    private ToolStripMenuItem closeTabMenuItem;
    private ToolStripMenuItem closeWindowMenuItem;
    private ToolStripMenuItem closeAllMenuItem;
    private ToolStripMenuItem exitMenuItem;

    private ToolStripMenuItem editMenu;
    private ToolStripMenuItem undoMenuItem;
    private ToolStripMenuItem cutMenuItem;
    private ToolStripMenuItem copyMenuItem;
    private ToolStripMenuItem pasteMenuItem;
    private ToolStripMenuItem deleteMenuItem;
    private ToolStripMenuItem findMenuItem;
    private ToolStripMenuItem findNextMenuItem;
    private ToolStripMenuItem findPreviousMenuItem;
    private ToolStripMenuItem replaceMenuItem;
    private ToolStripMenuItem gotoMenuItem;
    private ToolStripMenuItem selectAllMenuItem;
    private ToolStripMenuItem timeDateMenuItem;
    private ToolStripMenuItem fontMenuItem;
    private ToolStripMenuItem bookmarkSeparator;
    private ToolStripMenuItem toggleBookmarkMenuItem;
    private ToolStripMenuItem nextBookmarkMenuItem;
    private ToolStripMenuItem prevBookmarkMenuItem;
    private ToolStripMenuItem clearBookmarksMenuItem;
    private ToolStripMenuItem foldingSeparator;
    private ToolStripMenuItem toggleFoldMenuItem;
    private ToolStripMenuItem toggleAllFoldsMenuItem;

    private ToolStripMenuItem viewMenu;
    private ToolStripMenuItem zoomMenu;
    private ToolStripMenuItem zoomInMenuItem;
    private ToolStripMenuItem zoomOutMenuItem;
    private ToolStripMenuItem restoreDefaultZoomMenuItem;
    private ToolStripMenuItem statusBarMenuItem;
    private ToolStripMenuItem wordWrapMenuItem;
    private ToolStripMenuItem themeMenu;
    private ToolStripMenuItem darkThemeMenuItem;
    private ToolStripMenuItem lightThemeMenuItem;
    private ToolStripMenuItem gutterMenuItem;

    private GutterPanel gutterPanel;
    private TableLayoutPanel mainTable;
    internal RichTextBox textEditor;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lineColLabel;
    private ToolStripStatusLabel charCountLabel;
    private ToolStripStatusLabel zoomLabel;
    private ToolStripStatusLabel lineEndingsLabel;
    private ToolStripStatusLabel encodingLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1000, 600);
        Text = "Text Editor";
        StartPosition = FormStartPosition.CenterScreen;

        // Menu Strip
        menuStrip = new MenuStrip();
        fileMenu = new ToolStripMenuItem("&File");
        newTabMenuItem = new ToolStripMenuItem("New Tab", null, NewTab_Click);
        newWindowMenuItem = new ToolStripMenuItem("New Window", null, NewWindow_Click);
        openMenuItem = new ToolStripMenuItem("&Open...", null, Open_Click);
        recentMenuItem = new ToolStripMenuItem("Recent >");
        saveMenuItem = new ToolStripMenuItem("&Save", null, Save_Click);
        saveAsMenuItem = new ToolStripMenuItem("Save &As...", null, SaveAs_Click);
        saveAllMenuItem = new ToolStripMenuItem("Save A&ll", null, SaveAll_Click);
        closeTabMenuItem = new ToolStripMenuItem("Close Tab", null, CloseTab_Click);
        closeWindowMenuItem = new ToolStripMenuItem("Close Window", null, CloseWindow_Click);
        closeAllMenuItem = new ToolStripMenuItem("Close All", null, CloseAll_Click);
        exitMenuItem = new ToolStripMenuItem("E&xit", null, Exit_Click);

        fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
            newTabMenuItem, newWindowMenuItem, openMenuItem, recentMenuItem,
            new ToolStripSeparator(), saveMenuItem, saveAsMenuItem, saveAllMenuItem,
            new ToolStripSeparator(), closeTabMenuItem, closeWindowMenuItem, closeAllMenuItem,
            new ToolStripSeparator(), exitMenuItem
        });

        editMenu = new ToolStripMenuItem("&Edit");
        undoMenuItem = new ToolStripMenuItem("&Undo", null, Undo_Click);
        cutMenuItem = new ToolStripMenuItem("Cu&t", null, Cut_Click);
        copyMenuItem = new ToolStripMenuItem("&Copy", null, Copy_Click);
        pasteMenuItem = new ToolStripMenuItem("&Paste", null, Paste_Click);
        deleteMenuItem = new ToolStripMenuItem("&Delete", null, Delete_Click);
        findMenuItem = new ToolStripMenuItem("&Find...", null, Find_Click);
        findNextMenuItem = new ToolStripMenuItem("Find &Next", null, FindNext_Click);
        findPreviousMenuItem = new ToolStripMenuItem("Find &Previous", null, FindPrevious_Click);
        replaceMenuItem = new ToolStripMenuItem("&Replace...", null, Replace_Click);
        gotoMenuItem = new ToolStripMenuItem("&Go To...", null, Goto_Click);
        selectAllMenuItem = new ToolStripMenuItem("Select &All", null, SelectAll_Click);
        timeDateMenuItem = new ToolStripMenuItem("&Time/Date", null, TimeDate_Click);
        fontMenuItem = new ToolStripMenuItem("&Font...", null, Font_Click);

        // Bookmarks submenu items
        toggleBookmarkMenuItem = new ToolStripMenuItem("&Toggle Bookmark", null, ToggleBookmark_Click);
        nextBookmarkMenuItem = new ToolStripMenuItem("Next Bookmark", null, NextBookmark_Click);
        prevBookmarkMenuItem = new ToolStripMenuItem("Previous Bookmark", null, PrevBookmark_Click);
        clearBookmarksMenuItem = new ToolStripMenuItem("Clear All Bookmarks", null, ClearAllBookmarks_Click);

        // Folding submenu items
        toggleFoldMenuItem = new ToolStripMenuItem("&Toggle Fold", null, ToggleFold_Click);
        toggleAllFoldsMenuItem = new ToolStripMenuItem("&Toggle All Folds", null, ToggleAllFolds_Click);

        editMenu.DropDownItems.AddRange(new ToolStripItem[] {
            undoMenuItem, new ToolStripSeparator(), cutMenuItem, copyMenuItem,
            pasteMenuItem, deleteMenuItem, new ToolStripSeparator(), findMenuItem,
            findNextMenuItem, findPreviousMenuItem, replaceMenuItem, gotoMenuItem,
            new ToolStripSeparator(), selectAllMenuItem, timeDateMenuItem,
            new ToolStripSeparator(), fontMenuItem, new ToolStripSeparator(),
            toggleBookmarkMenuItem, nextBookmarkMenuItem, prevBookmarkMenuItem,
            clearBookmarksMenuItem, new ToolStripSeparator(),
            toggleFoldMenuItem, toggleAllFoldsMenuItem
        });

        viewMenu = new ToolStripMenuItem("&View");
        zoomMenu = new ToolStripMenuItem("&Zoom");
        zoomInMenuItem = new ToolStripMenuItem("Zoom &In", null, ZoomIn_Click);
        zoomOutMenuItem = new ToolStripMenuItem("Zoom &Out", null, ZoomOut_Click);
        restoreDefaultZoomMenuItem = new ToolStripMenuItem("&Restore Default Zoom", null, RestoreDefaultZoom_Click);

        zoomMenu.DropDownItems.AddRange(new ToolStripItem[] {
            zoomInMenuItem, zoomOutMenuItem, restoreDefaultZoomMenuItem
        });

        statusBarMenuItem = new ToolStripMenuItem("&Status Bar", null, StatusBar_Click);
        statusBarMenuItem.Checked = true;
        wordWrapMenuItem = new ToolStripMenuItem("&Word Wrap", null, WordWrap_Click);
        wordWrapMenuItem.Checked = true;

        themeMenu = new ToolStripMenuItem("&Theme");
        darkThemeMenuItem = new ToolStripMenuItem("&Dark", null, DarkTheme_Click);
        lightThemeMenuItem = new ToolStripMenuItem("&Light", null, LightTheme_Click);
        themeMenu.DropDownItems.AddRange(new ToolStripItem[] { darkThemeMenuItem, lightThemeMenuItem });

        viewMenu.DropDownItems.AddRange(new ToolStripItem[] {
            zoomMenu, new ToolStripSeparator(), statusBarMenuItem, wordWrapMenuItem,
            new ToolStripSeparator(), themeMenu
        });

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, viewMenu });

        // Main Table Layout (2 columns: gutter, editor)
        mainTable = new TableLayoutPanel();
        mainTable.ColumnCount = 2;
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainTable.RowCount = 1;
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainTable.Dock = DockStyle.Fill;

        // Gutter Panel
        gutterPanel = new GutterPanel(this);
        gutterPanel.Dock = DockStyle.Fill; // Fill its cell in table

        // Text Editor (RichTextBox)
        textEditor = new RichTextBox();
        textEditor.Dock = DockStyle.Fill;
        textEditor.Multiline = true;
        textEditor.ScrollBars = RichTextBoxScrollBars.Both;
        textEditor.Font = new Font("Consolas", 12);
        textEditor.TextChanged += TextEditor_TextChanged;
        textEditor.SelectionChanged += TextEditor_SelectionChanged;
        textEditor.VScroll += TextEditor_VScroll;
        textEditor.Resize += TextEditor_Resize;
        textEditor.Resize += TextEditor_Resize;

        // Assemble table
        mainTable.Controls.Add(gutterPanel, 0, 0);
        mainTable.Controls.Add(textEditor, 1, 0);

        // Status Strip
        statusStrip = new StatusStrip();
        lineColLabel = new ToolStripStatusLabel("Ln 1, Col 1");
        charCountLabel = new ToolStripStatusLabel("0 characters");
        zoomLabel = new ToolStripStatusLabel("100%");
        lineEndingsLabel = new ToolStripStatusLabel("Windows (CRLF)");
        encodingLabel = new ToolStripStatusLabel("UTF-8");

        statusStrip.Items.AddRange(new ToolStripItem[] {
            lineColLabel, charCountLabel, new ToolStripStatusLabel { Spring = true },
            zoomLabel, lineEndingsLabel, encodingLabel
        });

        // Add controls in proper z-order
        Controls.Add(menuStrip);
        Controls.Add(mainTable);
        Controls.Add(statusStrip);

        // Set main menu strip
        MainMenuStrip = menuStrip;
    }
}

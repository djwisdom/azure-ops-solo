namespace MyCrownJewelApp.Pfpad;

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
    #pragma warning disable CS0169
    private ToolStripMenuItem bookmarkSeparator;
    private ToolStripMenuItem foldingSeparator;
    #pragma warning restore CS0169
    private ToolStripMenuItem toggleBookmarkMenuItem;
    private ToolStripMenuItem nextBookmarkMenuItem;
    private ToolStripMenuItem prevBookmarkMenuItem;
    private ToolStripMenuItem clearBookmarksMenuItem;
    #pragma warning disable CS0169
    private ToolStripMenuItem toggleFoldMenuItem;
    private ToolStripMenuItem toggleAllFoldsMenuItem;
    #pragma warning restore CS0169

    private ToolStripMenuItem viewMenu;
    private ToolStripMenuItem zoomMenu;
    private ToolStripMenuItem zoomInMenuItem;
    private ToolStripMenuItem zoomOutMenuItem;
    private ToolStripMenuItem restoreDefaultZoomMenuItem;
    private ToolStripMenuItem statusBarMenuItem;
    private ToolStripMenuItem wordWrapMenuItem;
    private ToolStripMenuItem syntaxHighlightingMenuItem;
    private ToolStripMenuItem currentLineHighlightMenu;
    private ToolStripMenuItem currentLineOffMenuItem;
    private ToolStripMenuItem currentLineNumberOnlyMenuItem;
    private ToolStripMenuItem currentLineWholeLineMenuItem;
    private ToolStripMenuItem insertSpacesMenuItem;
    private ToolStripMenuItem tabSizeMenu;
    private ToolStripMenuItem tab2MenuItem;
    private ToolStripMenuItem tab4MenuItem;
    private ToolStripMenuItem tab6MenuItem;
    private ToolStripMenuItem tab8MenuItem;
    private ToolStripMenuItem tab10MenuItem;
    private ToolStripMenuItem tab12MenuItem;
    private ToolStripMenuItem autoIndentMenuItem;
    private ToolStripMenuItem smartTabsMenuItem;
    private ToolStripMenuItem elasticTabsMenuItem;
    private ToolStripMenuItem columnGuideMenuItem;
    private ToolStripMenuItem col72MenuItem;
    private ToolStripMenuItem col80MenuItem;
    private ToolStripMenuItem col100MenuItem;
    private ToolStripMenuItem col120MenuItem;
    private ToolStripMenuItem col150MenuItem;
    private ToolStripMenuItem colCustomMenuItem;
    private ToolStripMenuItem gutterMenuItem;
    private ToolStripMenuItem vimModeMenuItem;
    private ToolStripMenuItem themeMenu;
    private ToolStripMenuItem darkThemeMenuItem;
    private ToolStripMenuItem lightThemeMenuItem;

    private TabControl tabControl;
    private TableLayoutPanel mainLayout;
    private SplitContainer splitContainer;
    private TableLayoutPanel mainTable;
    private Panel editorPanel;
    private GutterPanel gutterPanel;
    internal HighlightRichTextBox textEditor;
    private ColumnGuidePanel guidePanel;
    private MinimapControl minimapControl;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lineColLabel;
    private ToolStripStatusLabel charCountLabel;
    private ToolStripDropDownButton tabSizeDropDown;
    private ToolStripStatusLabel linePositionLabel;
    private ToolStripStatusLabel zoomLabel;
    private ToolStripStatusLabel lineEndingsLabel;
    private ToolStripStatusLabel encodingLabel;
    private ToolStripDropDownButton themeDropDown;
    private ToolStripStatusLabel fileTypeLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tabStripWindow?.ReleaseHandle();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Font;
        Padding = new Padding(0);
        ClientSize = new Size(1000, 600);
        Text = "Text Editor";
        StartPosition = FormStartPosition.CenterScreen;

        // Menu Strip
        menuStrip = new MenuStrip();
        fileMenu = new ToolStripMenuItem("&File");
        newTabMenuItem = new ToolStripMenuItem("New Tab", null, NewTab_Click, "Ctrl+T");
        newWindowMenuItem = new ToolStripMenuItem("New Window", null, NewWindow_Click, "Ctrl+Shift+N");
        openMenuItem = new ToolStripMenuItem("&Open...", null, Open_Click, "Ctrl+O");
        recentMenuItem = new ToolStripMenuItem("Recent Files");
        saveMenuItem = new ToolStripMenuItem("&Save", null, Save_Click, "Ctrl+S");
        saveAsMenuItem = new ToolStripMenuItem("Save &As...", null, SaveAs_Click);
        saveAllMenuItem = new ToolStripMenuItem("Save A&ll", null, SaveAll_Click, "Ctrl+Shift+S");
        closeTabMenuItem = new ToolStripMenuItem("Close Tab", null, CloseTab_Click, "Ctrl+W");
        closeWindowMenuItem = new ToolStripMenuItem("Close Window", null, CloseWindow_Click);
        closeAllMenuItem = new ToolStripMenuItem("Close All", null, CloseAll_Click);
        exitMenuItem = new ToolStripMenuItem("E&xit", null, Exit_Click);
        // Build File menu
        fileMenu.DropDownItems.Add(newTabMenuItem);
        fileMenu.DropDownItems.Add(newWindowMenuItem);
        fileMenu.DropDownItems.Add(openMenuItem);
        fileMenu.DropDownItems.Add(recentMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(saveMenuItem);
        fileMenu.DropDownItems.Add(saveAsMenuItem);
        fileMenu.DropDownItems.Add(saveAllMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(closeTabMenuItem);
        fileMenu.DropDownItems.Add(closeWindowMenuItem);
        fileMenu.DropDownItems.Add(closeAllMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitMenuItem);

        editMenu = new ToolStripMenuItem("&Edit");
        undoMenuItem = new ToolStripMenuItem("&Undo", null, Undo_Click, "Ctrl+Z");
        cutMenuItem = new ToolStripMenuItem("Cu&t", null, Cut_Click, "Ctrl+X");
        copyMenuItem = new ToolStripMenuItem("&Copy", null, Copy_Click, "Ctrl+C");
        pasteMenuItem = new ToolStripMenuItem("&Paste", null, Paste_Click, "Ctrl+V");
        deleteMenuItem = new ToolStripMenuItem("&Delete", null, Delete_Click, "Del");
        findMenuItem = new ToolStripMenuItem("&Find...", null, Find_Click, "Ctrl+F");
        findNextMenuItem = new ToolStripMenuItem("Find &Next", null, FindNext_Click, "F3");
        findPreviousMenuItem = new ToolStripMenuItem("Find &Previous", null, FindPrevious_Click, "Shift+F3");
        replaceMenuItem = new ToolStripMenuItem("&Replace...", null, Replace_Click, "Ctrl+H");
        gotoMenuItem = new ToolStripMenuItem("&Go To...", null, Goto_Click, "Ctrl+G");
        selectAllMenuItem = new ToolStripMenuItem("Select &All", null, SelectAll_Click, "Ctrl+A");
        timeDateMenuItem = new ToolStripMenuItem("Time/&Date", null, TimeDate_Click, "F5");
        fontMenuItem = new ToolStripMenuItem("&Font...", null, Font_Click);
        #pragma warning disable CS0169
        bookmarkSeparator = new ToolStripMenuItem();
        foldingSeparator = new ToolStripMenuItem();
        #pragma warning restore CS0169
        toggleBookmarkMenuItem = new ToolStripMenuItem("Toggle Bookmark", null, ToggleBookmark_Click, "Ctrl+F2");
        nextBookmarkMenuItem = new ToolStripMenuItem("Next Bookmark", null, NextBookmark_Click, "F2");
        prevBookmarkMenuItem = new ToolStripMenuItem("Previous Bookmark", null, PrevBookmark_Click, "Shift+F2");
        clearBookmarksMenuItem = new ToolStripMenuItem("Clear Bookmarks", null, ClearAllBookmarks_Click);
        toggleFoldMenuItem = new ToolStripMenuItem("Toggle Fold", null, ToggleFold_Click);
        toggleAllFoldsMenuItem = new ToolStripMenuItem("Toggle All Folds", null, ToggleAllFolds_Click);
        #pragma warning restore CS0169
        // Build Edit menu
        editMenu.DropDownItems.Add(undoMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(cutMenuItem);
        editMenu.DropDownItems.Add(copyMenuItem);
        editMenu.DropDownItems.Add(pasteMenuItem);
        editMenu.DropDownItems.Add(deleteMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(findMenuItem);
        editMenu.DropDownItems.Add(findNextMenuItem);
        editMenu.DropDownItems.Add(findPreviousMenuItem);
        editMenu.DropDownItems.Add(replaceMenuItem);
        editMenu.DropDownItems.Add(gotoMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(selectAllMenuItem);
        editMenu.DropDownItems.Add(timeDateMenuItem);
        editMenu.DropDownItems.Add(fontMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(toggleBookmarkMenuItem);
        editMenu.DropDownItems.Add(nextBookmarkMenuItem);
        editMenu.DropDownItems.Add(prevBookmarkMenuItem);
        editMenu.DropDownItems.Add(clearBookmarksMenuItem);
        editMenu.DropDownItems.Add(bookmarkSeparator);
        editMenu.DropDownItems.Add(toggleFoldMenuItem);
        editMenu.DropDownItems.Add(toggleAllFoldsMenuItem);
        editMenu.DropDownItems.Add(foldingSeparator);

        viewMenu = new ToolStripMenuItem("&View");
        zoomMenu = new ToolStripMenuItem("&Zoom");
        zoomInMenuItem = new ToolStripMenuItem("Zoom &In", null, ZoomIn_Click, "Ctrl++");
        zoomOutMenuItem = new ToolStripMenuItem("Zoom &Out", null, ZoomOut_Click, "Ctrl+-");
        restoreDefaultZoomMenuItem = new ToolStripMenuItem("&Restore Default Zoom", null, RestoreDefaultZoom_Click, "Ctrl+0");
        // Build Zoom menu
        zoomMenu.DropDownItems.Add(zoomInMenuItem);
        zoomMenu.DropDownItems.Add(zoomOutMenuItem);
        zoomMenu.DropDownItems.Add(new ToolStripSeparator());
        zoomMenu.DropDownItems.Add(restoreDefaultZoomMenuItem);
        statusBarMenuItem = new ToolStripMenuItem("&Status Bar", null, StatusBar_Click);
        statusBarMenuItem.CheckOnClick = true;
        wordWrapMenuItem = new ToolStripMenuItem("&Word Wrap", null, WordWrap_Click);
        wordWrapMenuItem.CheckOnClick = true;
        syntaxHighlightingMenuItem = new ToolStripMenuItem("&Syntax Highlighting", null, SyntaxHighlighting_Click);
        syntaxHighlightingMenuItem.CheckOnClick = true;
        currentLineHighlightMenu = new ToolStripMenuItem("Current Line &Highlight");
        currentLineOffMenuItem = new ToolStripMenuItem("Off", null, CurrentLineHighlightMode_Click);
        currentLineNumberOnlyMenuItem = new ToolStripMenuItem("Line &Number Only", null, CurrentLineHighlightMode_Click);
        currentLineWholeLineMenuItem = new ToolStripMenuItem("&Whole Line", null, CurrentLineHighlightMode_Click);
        currentLineHighlightMenu.DropDownItems.AddRange(new ToolStripItem[] {
            currentLineOffMenuItem, currentLineNumberOnlyMenuItem, currentLineWholeLineMenuItem
        });
        insertSpacesMenuItem = new ToolStripMenuItem("&Insert Spaces", null, InsertSpaces_Click);
        insertSpacesMenuItem.CheckOnClick = true;
        tabSizeMenu = new ToolStripMenuItem("&Tab Size");
        tab2MenuItem = new ToolStripMenuItem("2", null, TabSize2_Click);
        tab4MenuItem = new ToolStripMenuItem("4", null, TabSize4_Click);
        tab6MenuItem = new ToolStripMenuItem("6", null, TabSize6_Click);
        tab8MenuItem = new ToolStripMenuItem("8", null, TabSize8_Click);
        tab10MenuItem = new ToolStripMenuItem("10", null, TabSize10_Click);
        tab12MenuItem = new ToolStripMenuItem("12", null, TabSize12_Click);
        tabSizeMenu.DropDownItems.AddRange(new ToolStripItem[] {
            tab2MenuItem, tab4MenuItem, tab6MenuItem, tab8MenuItem, tab10MenuItem, tab12MenuItem
        });
        autoIndentMenuItem = new ToolStripMenuItem("&Auto Indent", null, AutoIndent_Click);
        autoIndentMenuItem.CheckOnClick = true;
        smartTabsMenuItem = new ToolStripMenuItem("&Smart Tabs", null, SmartTabs_Click);
        smartTabsMenuItem.CheckOnClick = true;
        elasticTabsMenuItem = new ToolStripMenuItem("&Elastic Tabs", null, ElasticTabs_Click);
        elasticTabsMenuItem.CheckOnClick = true;
        columnGuideMenuItem = new ToolStripMenuItem("Column &Guide");
        col72MenuItem = new ToolStripMenuItem("Column &72", null, ColumnGuide_Click);
        col80MenuItem = new ToolStripMenuItem("Column &80", null, ColumnGuide_Click);
        col100MenuItem = new ToolStripMenuItem("Column &100", null, ColumnGuide_Click);
        col120MenuItem = new ToolStripMenuItem("Column &120", null, ColumnGuide_Click);
        col150MenuItem = new ToolStripMenuItem("Column &150", null, ColumnGuide_Click);
        colCustomMenuItem = new ToolStripMenuItem("&Custom...", null, ColumnGuide_Click);
        columnGuideMenuItem.DropDownItems.Add(col72MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col80MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col100MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col120MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col150MenuItem);
        columnGuideMenuItem.DropDownItems.Add(colCustomMenuItem);
        gutterMenuItem = new ToolStripMenuItem("&Gutter", null, GutterMenuItem_Click);
        gutterMenuItem.CheckOnClick = true;
        minimapMenuItem = new ToolStripMenuItem("Minimap", null, MinimapMenuItem_Click);
        minimapMenuItem.CheckOnClick = true;
        vimModeMenuItem = new ToolStripMenuItem("&Vim Mode", null, ToggleVimMode);
        vimModeMenuItem.CheckOnClick = true;
        themeMenu = new ToolStripMenuItem("&Theme");
        darkThemeMenuItem = new ToolStripMenuItem("&Dark", null, DarkTheme_Click);
        lightThemeMenuItem = new ToolStripMenuItem("&Light", null, LightTheme_Click);
        themeMenu.DropDownItems.AddRange(new ToolStripItem[] {
            darkThemeMenuItem, lightThemeMenuItem
        });
        viewMenu.DropDownItems.Add(zoomMenu);
        viewMenu.DropDownItems.Add(statusBarMenuItem);
        viewMenu.DropDownItems.Add(wordWrapMenuItem);
        viewMenu.DropDownItems.Add(syntaxHighlightingMenuItem);
        viewMenu.DropDownItems.Add(currentLineHighlightMenu);
        viewMenu.DropDownItems.Add(insertSpacesMenuItem);
        viewMenu.DropDownItems.Add(tabSizeMenu);
        viewMenu.DropDownItems.Add(autoIndentMenuItem);
        viewMenu.DropDownItems.Add(smartTabsMenuItem);
        viewMenu.DropDownItems.Add(elasticTabsMenuItem);
        viewMenu.DropDownItems.Add(columnGuideMenuItem);
        viewMenu.DropDownItems.Add(gutterMenuItem);
        viewMenu.DropDownItems.Add(minimapMenuItem);
        viewMenu.DropDownItems.Add(vimModeMenuItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(themeMenu);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(editMenu);
        menuStrip.Items.Add(viewMenu);
        menuStrip.Dock = DockStyle.Fill;

        // Tab Control for multi-file editing
        tabControl = new TabControl();
        tabControl.Dock = DockStyle.Fill;
        tabControl.Height = 30;
        tabControl.Multiline = true;
        tabControl.HotTrack = true;
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.Alignment = TabAlignment.Top;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(80, 30);
        tabControl.Padding = new Point(12, 4);
        tabControl.Cursor = Cursors.Hand;
        tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        tabControl.MouseDown += TabControl_MouseDown;
        tabControl.MouseMove += TabControl_MouseMove;
        tabControl.MouseLeave += TabControl_MouseLeave;
        tabControl.DrawItem += TabControl_DrawItem;
        tabControl.DoubleClick += TabControl_DoubleClick;
        tabControl.Paint += TabControl_Paint;
        tabControl.HandleCreated += TabControl_HandleCreated;
        tabControl.HandleDestroyed += TabControl_HandleDestroyed;

        // Main Table Layout (2 columns: gutter | editor+minimap panel)
        mainTable = new TableLayoutPanel();
        mainTable.ColumnCount = 2;
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainTable.RowCount = 1;
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainTable.Dock = DockStyle.Fill;
        mainTable.Margin = new Padding(0);
        mainTable.Padding = new Padding(0);

        // Editor panel wraps textEditor + minimap, with minimap docked to right
        editorPanel = new Panel();
        editorPanel.Dock = DockStyle.Fill;
        editorPanel.Margin = new Padding(0);
        editorPanel.Padding = new Padding(0);

        // Gutter Panel
        gutterPanel = new GutterPanel(this);
        gutterPanel.Dock = DockStyle.Fill;
        gutterPanel.Margin = new Padding(0);

        // Text Editor (RichTextBox) added to editorPanel
        textEditor = new HighlightRichTextBox();
        textEditor.Dock = DockStyle.Fill;
        textEditor.Multiline = true;
        textEditor.ScrollBars = RichTextBoxScrollBars.Both;
        textEditor.AcceptsTab = true;
        textEditor.Font = new Font("Consolas", 12);
        textEditor.BorderStyle = BorderStyle.None;
        textEditor.Margin = new Padding(0);
        textEditor.Padding = new Padding(0);
        textEditor.TextChanged += TextEditor_TextChanged;
        textEditor.VScroll += TextEditor_VScroll;
        textEditor.KeyDown += TextEditor_KeyDown;
        textEditor.MouseDown += TextEditor_MouseDown;

        // Column Guide Overlay (overlays editor, not in table)
        guidePanel = new ColumnGuidePanel();
        guidePanel.LinkedEditor = textEditor;
        guidePanel.GuideColumn = 80;
        guidePanel.ShowGuide = true;
        guidePanel.GuideColor = Color.FromArgb(60, 60, 60);
        guidePanel.Bounds = textEditor.ClientRectangle;
        guidePanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        textEditor.Controls.Add(guidePanel);
        guidePanel.BringToFront();

        // Minimap Control (docked right inside editorPanel, hidden by default)
        minimapControl = new MinimapControl();
        minimapControl.ViewportColor = Color.FromArgb(80, Color.DodgerBlue);
        minimapControl.ViewportBorderColor = Color.DodgerBlue;
        minimapControl.Margin = new Padding(0);
        minimapControl.Dock = DockStyle.Right;
        minimapControl.MinimapWidth = 100;
        minimapControl.Visible = false;

        // Assemble editor panel with textEditor + minimap
        editorPanel.Controls.Add(textEditor);
        editorPanel.Controls.Add(minimapControl);

        // Assemble table
        mainTable.Controls.Add(gutterPanel, 0, 0);
        mainTable.Controls.Add(editorPanel, 1, 0);

        // Status Strip
        statusStrip = new StatusStrip();
        statusStrip.Dock = DockStyle.Fill;
        statusStrip.RenderMode = ToolStripRenderMode.Professional;
        lineColLabel = new ToolStripStatusLabel("Ln 1, Col 1");
        charCountLabel = new ToolStripStatusLabel("0 characters");
        tabSizeDropDown = new ToolStripDropDownButton();
        tabSizeDropDown.Text = "Tab: 4";
        tabSizeDropDown.Width = 60;
        tabSizeDropDown.Alignment = ToolStripItemAlignment.Left;
        tabSizeDropDown.DropDownItems.Add("2", null, TabSize2_Click);
        tabSizeDropDown.DropDownItems.Add("4", null, TabSize4_Click);
        tabSizeDropDown.DropDownItems.Add("6", null, TabSize6_Click);
        tabSizeDropDown.DropDownItems.Add("8", null, TabSize8_Click);
        tabSizeDropDown.DropDownItems.Add("10", null, TabSize10_Click);
        tabSizeDropDown.DropDownItems.Add("12", null, TabSize12_Click);
        linePositionLabel = new ToolStripStatusLabel("1 / 1");
        zoomLabel = new ToolStripStatusLabel("100%");
        lineEndingsLabel = new ToolStripStatusLabel("Windows (CRLF)");
        encodingLabel = new ToolStripStatusLabel("UTF-8");
        themeDropDown = new ToolStripDropDownButton();
        themeDropDown.Text = "Theme";
        themeDropDown.Width = 60;
        themeDropDown.Alignment = ToolStripItemAlignment.Right;
        themeDropDown.DropDownItems.Add("Dark", null, StatusBarDarkTheme_Click);
        themeDropDown.DropDownItems.Add("Light", null, StatusBarLightTheme_Click);
        fileTypeLabel = new ToolStripStatusLabel("");
        fileTypeLabel.Spring = false;
        fileTypeLabel.AutoSize = false;
        fileTypeLabel.Width = 60;
        fileTypeLabel.TextAlign = ContentAlignment.MiddleRight;
        fileTypeLabel.Alignment = ToolStripItemAlignment.Right;

        statusStrip.Items.Add(lineColLabel);
        statusStrip.Items.Add(charCountLabel);
        statusStrip.Items.Add(tabSizeDropDown);
        statusStrip.Items.Add(new ToolStripStatusLabel() { Spring = true });
        statusStrip.Items.Add(linePositionLabel);
        statusStrip.Items.Add(zoomLabel);
        statusStrip.Items.Add(lineEndingsLabel);
        statusStrip.Items.Add(encodingLabel);
        statusStrip.Items.Add(themeDropDown);
        statusStrip.Items.Add(fileTypeLabel);

        const int itemPadding = 8;
        lineColLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        charCountLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        tabSizeDropDown.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        linePositionLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        zoomLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        lineEndingsLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        encodingLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);

        // Main layout: explicit row ordering
        mainLayout = new TableLayoutPanel();
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.ColumnCount = 1;
        mainLayout.RowCount = 4;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // row 0: menu
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // row 1: tabs
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // row 2: editor
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // row 3: status
        mainLayout.Margin = new Padding(0);
        mainLayout.Padding = new Padding(0);
        mainLayout.Controls.Add(menuStrip, 0, 0);
        mainLayout.Controls.Add(tabControl, 0, 1);
        mainLayout.Controls.Add(mainTable, 0, 2);
        mainLayout.Controls.Add(statusStrip, 0, 3);
        Controls.Add(mainLayout);

        MainMenuStrip = menuStrip;

        ResumeLayout(false);
        PerformLayout();
    }
}

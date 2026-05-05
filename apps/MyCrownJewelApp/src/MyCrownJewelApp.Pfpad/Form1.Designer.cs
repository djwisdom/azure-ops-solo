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
    private ToolStripMenuItem toggleBookmarkMenuItem;
    private ToolStripMenuItem nextBookmarkMenuItem;
    private ToolStripMenuItem prevBookmarkMenuItem;
    private ToolStripMenuItem clearBookmarksMenuItem;
    #pragma warning disable CS0169
    private ToolStripMenuItem toggleFoldMenuItem;
    private ToolStripMenuItem toggleAllFoldsMenuItem;
    #pragma warning restore CS0169

    private ToolStripMenuItem viewMenu;
    private ToolStripMenuItem panelMenu;
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
    internal ToolStripMenuItem whitespaceMenuItem;
    private ToolStripMenuItem vimModeMenuItem;
    private ToolStripMenuItem splitVMenuItem;
    private ToolStripMenuItem splitHMenuItem;
    internal ToolStripMenuItem terminalMenuItem;
    internal ToolStripMenuItem workspaceMenuItem;
    internal ToolStripMenuItem openFolderMenuItem;
    internal ToolStripMenuItem notificationCenterMenuItem;
    internal ToolStripMenuItem gitPanelMenuItem;
    internal ToolStripMenuItem symbolsMenuItem;
    internal ToolStripMenuItem problemsMenuItem;
    private ToolStripMenuItem themeMenu;
    private ToolStripMenuItem darkThemeMenuItem;
    private ToolStripMenuItem lightThemeMenuItem;

    private ToolStripMenuItem toolsMenu;
    internal ToolStripMenuItem configureToolsMenuItem;

    private TabControl tabControl;
    private TableLayoutPanel mainLayout;
    private SplitContainer splitContainer;
    private TableLayoutPanel mainTable;
    private Panel editorPanel;
    internal GutterPanel gutterPanel;
    internal WhitespaceOverlayForm whitespaceOverlay;
    internal HighlightRichTextBox textEditor;
    private MinimapControl minimapControl;
    private StatusStrip statusStrip;
    internal ToolStripStatusLabel lineColLabel;
    internal ToolStripStatusLabel charCountLabel;
    private ToolStripDropDownButton tabSizeDropDown;
    internal ToolStripStatusLabel linePositionLabel;
    internal ToolStripStatusLabel zoomLabel;
    private ToolStripStatusLabel lineEndingsLabel;
    private ToolStripStatusLabel encodingLabel;
    private ToolStripDropDownButton themeDropDown;
        internal ToolStripStatusLabel fileTypeLabel;
    internal ToolStripStatusLabel vimModeLabel;
    private ToolStripStatusLabel gitBranchLabel;
    internal ToolStripStatusLabel gitDirtyLabel;
    internal ToolStripStatusLabel gitSyncLabel;
    internal ToolStripProgressBar scanProgressBar;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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
        ClientSize = new Size(780, 540);
        MinimumSize = new Size(400, 250);
        Text = "Personal Flip Pad";
        StartPosition = FormStartPosition.CenterScreen;

        // Menu Strip
        menuStrip = new MenuStrip();
        fileMenu = new ToolStripMenuItem("&File");
        newTabMenuItem = new ToolStripMenuItem("New Tab", null, NewTab_Click, Keys.Control | Keys.T);
        newWindowMenuItem = new ToolStripMenuItem("New Window", null, NewWindow_Click, Keys.Control | Keys.Shift | Keys.N);
        openMenuItem = new ToolStripMenuItem("&Open...", null, Open_Click, Keys.Control | Keys.O);
        recentMenuItem = new ToolStripMenuItem("Recent Files");
        saveMenuItem = new ToolStripMenuItem("&Save", null, Save_Click, Keys.Control | Keys.S);
        saveAsMenuItem = new ToolStripMenuItem("Save &As...", null, SaveAs_Click, Keys.Control | Keys.Shift | Keys.S);
        saveAllMenuItem = new ToolStripMenuItem("Save A&ll", null, SaveAll_Click, Keys.Control | Keys.Alt | Keys.S);
        closeTabMenuItem = new ToolStripMenuItem("Close Tab", null, CloseTab_Click, Keys.Control | Keys.W);
        closeWindowMenuItem = new ToolStripMenuItem("Close Window", null, CloseWindow_Click, Keys.Control | Keys.Shift | Keys.W);
        closeAllMenuItem = new ToolStripMenuItem("Close All", null, CloseAll_Click, Keys.Control | Keys.Alt | Keys.W);
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
        undoMenuItem = new ToolStripMenuItem("&Undo", null, Undo_Click, Keys.Control | Keys.Z);
        cutMenuItem = new ToolStripMenuItem("Cu&t", null, Cut_Click, Keys.Control | Keys.X);
        copyMenuItem = new ToolStripMenuItem("&Copy", null, Copy_Click, Keys.Control | Keys.C);
        pasteMenuItem = new ToolStripMenuItem("&Paste", null, Paste_Click, Keys.Control | Keys.V);
        deleteMenuItem = new ToolStripMenuItem("&Delete", null, Delete_Click, Keys.Delete);
        findMenuItem = new ToolStripMenuItem("&Find...", null, Find_Click, Keys.Control | Keys.F);
        findNextMenuItem = new ToolStripMenuItem("Find &Next", null, FindNext_Click, Keys.F3);
        findPreviousMenuItem = new ToolStripMenuItem("Find &Previous", null, FindPrevious_Click, Keys.Shift | Keys.F3);
        replaceMenuItem = new ToolStripMenuItem("&Replace...", null, Replace_Click, Keys.Control | Keys.H);
        gotoMenuItem = new ToolStripMenuItem("&Go To...", null, Goto_Click, Keys.Control | Keys.G);
        var gotoDefMenuItem = new ToolStripMenuItem("Go to &Definition", null, GoToDefinition_Click, Keys.F12);
        editMenu.DropDownItems.Add(gotoDefMenuItem);
        var renameMenuItem = new ToolStripMenuItem("&Rename...", null, Rename_Click, Keys.F2);
        editMenu.DropDownItems.Add(renameMenuItem);
        var callHierarchyMenuItem = new ToolStripMenuItem("Call &Hierarchy", null, CallHierarchy_Click, Keys.Control | Keys.Shift | Keys.H);
        editMenu.DropDownItems.Add(callHierarchyMenuItem);
        selectAllMenuItem = new ToolStripMenuItem("Select &All", null, SelectAll_Click, Keys.Control | Keys.A);
        timeDateMenuItem = new ToolStripMenuItem("Time/&Date", null, TimeDate_Click, Keys.F5);
        fontMenuItem = new ToolStripMenuItem("&Font...", null, Font_Click, Keys.Control | Keys.Shift | Keys.F);
        toggleBookmarkMenuItem = new ToolStripMenuItem("Toggle Bookmark", null, ToggleBookmark_Click, "Ctrl+F2");
        nextBookmarkMenuItem = new ToolStripMenuItem("Next Bookmark", null, NextBookmark_Click, "F2");
        prevBookmarkMenuItem = new ToolStripMenuItem("Previous Bookmark", null, PrevBookmark_Click, "Shift+F2");
        clearBookmarksMenuItem = new ToolStripMenuItem("Clear Bookmarks", null, ClearAllBookmarks_Click);
        toggleFoldMenuItem = new ToolStripMenuItem("Toggle Fold", null, ToggleFold_Click, Keys.Control | Keys.Shift | Keys.OemOpenBrackets);
        toggleAllFoldsMenuItem = new ToolStripMenuItem("Toggle All Folds", null, ToggleAllFolds_Click, Keys.Control | Keys.Alt | Keys.OemOpenBrackets);
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
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(toggleFoldMenuItem);
        editMenu.DropDownItems.Add(toggleAllFoldsMenuItem);

        viewMenu = new ToolStripMenuItem("&View");
        zoomMenu = new ToolStripMenuItem("&Zoom");
        zoomInMenuItem = new ToolStripMenuItem("Zoom &In", null, ZoomIn_Click, Keys.Control | Keys.Oemplus);
        zoomOutMenuItem = new ToolStripMenuItem("Zoom &Out", null, ZoomOut_Click, Keys.Control | Keys.OemMinus);
        restoreDefaultZoomMenuItem = new ToolStripMenuItem("&Restore Default Zoom", null, RestoreDefaultZoom_Click, Keys.Control | Keys.D0);
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
        columnGuideMenuItem.CheckOnClick = true;
        columnGuideMenuItem.Checked = true;
        columnGuideMenuItem.Click += ToggleColumnGuide;
        col72MenuItem = new ToolStripMenuItem("72", null, ColumnGuide_Click);
        col80MenuItem = new ToolStripMenuItem("80", null, ColumnGuide_Click);
        col100MenuItem = new ToolStripMenuItem("100", null, ColumnGuide_Click);
        col120MenuItem = new ToolStripMenuItem("120", null, ColumnGuide_Click);
        col150MenuItem = new ToolStripMenuItem("150", null, ColumnGuide_Click);
        colCustomMenuItem = new ToolStripMenuItem("&Custom...", null, ColumnGuide_Click);
        columnGuideMenuItem.DropDownItems.Add(col72MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col80MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col100MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col120MenuItem);
        columnGuideMenuItem.DropDownItems.Add(col150MenuItem);
        columnGuideMenuItem.DropDownItems.Add(colCustomMenuItem);
        gutterMenuItem = new ToolStripMenuItem("&Gutter", null, GutterMenuItem_Click);
        gutterMenuItem.CheckOnClick = true;
        whitespaceMenuItem = new ToolStripMenuItem("&Show Whitespace", null, ToggleWhitespace_Click);
        whitespaceMenuItem.CheckOnClick = true;
        whitespaceMenuItem.Checked = true;
        minimapMenuItem = new ToolStripMenuItem("Minimap", null, MinimapMenuItem_Click);
        minimapMenuItem.CheckOnClick = true;
        vimModeMenuItem = new ToolStripMenuItem("&Vim Mode", null, ToggleVimMode, Keys.Control | Keys.Alt | Keys.V);
        vimModeMenuItem.CheckOnClick = true;
        themeMenu = new ToolStripMenuItem("&Theme");
        darkThemeMenuItem = new ToolStripMenuItem("&Dark", null, DarkTheme_Click);
        lightThemeMenuItem = new ToolStripMenuItem("&Light", null, LightTheme_Click);
        themeMenu.DropDownItems.AddRange(new ToolStripItem[] {
            darkThemeMenuItem, lightThemeMenuItem
        });
        viewMenu.DropDownItems.Add(zoomMenu);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
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
        viewMenu.DropDownItems.Add(whitespaceMenuItem);
        viewMenu.DropDownItems.Add(minimapMenuItem);
        viewMenu.DropDownItems.Add(vimModeMenuItem);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(fontMenuItem);
        viewMenu.DropDownItems.Add(themeMenu);
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        splitVMenuItem = new ToolStripMenuItem("Split &Vertical", null, SplitVertical_Click, Keys.Control | Keys.Shift | Keys.V);
        splitHMenuItem = new ToolStripMenuItem("Split &Horizontal", null, SplitHorizontal_Click, Keys.Control | Keys.Alt | Keys.H);
        viewMenu.DropDownItems.Add(splitVMenuItem);
        viewMenu.DropDownItems.Add(splitHMenuItem);

        // Panel menu — sidebar panel toggles
        panelMenu = new ToolStripMenuItem("&Panel");
        openFolderMenuItem = new ToolStripMenuItem("Open &Folder...", null, OpenFolder_Click, Keys.Control | Keys.Alt | Keys.O);
        workspaceMenuItem = new ToolStripMenuItem("&Workspace", null, ToggleWorkspace_Click, Keys.Control | Keys.Shift | Keys.W);
        workspaceMenuItem.CheckOnClick = true;
        panelMenu.DropDownItems.Add(openFolderMenuItem);
        panelMenu.DropDownItems.Add(workspaceMenuItem);
        panelMenu.DropDownItems.Add(new ToolStripSeparator());
        gitPanelMenuItem = new ToolStripMenuItem("&Source Control", null, ToggleGitPanel, Keys.Control | Keys.Alt | Keys.G);
        gitPanelMenuItem.CheckOnClick = true;
        panelMenu.DropDownItems.Add(gitPanelMenuItem);
        var gitFormMenuItem = new ToolStripMenuItem("Source Control &Window...", null, OpenGitForm, Keys.Control | Keys.Shift | Keys.G);
        panelMenu.DropDownItems.Add(gitFormMenuItem);
        symbolsMenuItem = new ToolStripMenuItem("&Symbols", null, ToggleSymbolPanel, Keys.Control | Keys.Alt | Keys.S);
        symbolsMenuItem.CheckOnClick = true;
        panelMenu.DropDownItems.Add(symbolsMenuItem);
        problemsMenuItem = new ToolStripMenuItem("&Problems", null, ToggleProblemsPanel, Keys.Control | Keys.Alt | Keys.P);
        problemsMenuItem.CheckOnClick = true;
        panelMenu.DropDownItems.Add(problemsMenuItem);
        panelMenu.DropDownItems.Add(new ToolStripSeparator());
        terminalMenuItem = new ToolStripMenuItem("&Terminal", null, ToggleTerminal_Click, Keys.Control | Keys.Oemtilde);
        terminalMenuItem.Checked = false;
        panelMenu.DropDownItems.Add(terminalMenuItem);
        panelMenu.DropDownItems.Add(new ToolStripSeparator());
        notificationCenterMenuItem = new ToolStripMenuItem("Notification &Center", null, ToggleNotificationCenter, Keys.Control | Keys.Shift | Keys.N);
        panelMenu.DropDownItems.Add(notificationCenterMenuItem);
        var notificationSettingsMenuItem = new ToolStripMenuItem("Notification &Settings...", null, ConfigureNotifications_Click);
        panelMenu.DropDownItems.Add(notificationSettingsMenuItem);

        menuStrip.Items.Add(fileMenu);
        menuStrip.Items.Add(editMenu);
        menuStrip.Items.Add(viewMenu);
        menuStrip.Items.Add(panelMenu);
        // Tools menu
        toolsMenu = new ToolStripMenuItem("&Tools");
        configureToolsMenuItem = new ToolStripMenuItem("External &Tools...", null, ConfigureTools_Click);
        configureToolsMenuItem.ShortcutKeys = Keys.Control | Keys.Alt | Keys.T;
        toolsMenu.DropDownItems.Add(configureToolsMenuItem);
        menuStrip.Items.Add(toolsMenu);
        // Help menu
        var helpMenu = new ToolStripMenuItem("&Help");
        var aboutMenuItem = new ToolStripMenuItem("&About Personal Flip Pad", null, About_Click);
        helpMenu.DropDownItems.Add(aboutMenuItem);
        menuStrip.Items.Add(helpMenu);
        menuStrip.Dock = DockStyle.Fill;

        // Tab Control for multi-file editing
        tabControl = new TabControl();
        tabControl.Dock = DockStyle.Fill;
        tabControl.Height = 30;
        tabControl.Multiline = true;
        tabControl.HotTrack = true;
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.Alignment = TabAlignment.Top;
        tabControl.SizeMode = TabSizeMode.Normal;
        tabControl.ItemSize = new Size(0, 30);
        tabControl.Padding = new Point(17, 4);
        tabControl.Cursor = Cursors.Hand;
        tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        tabControl.MouseDown += TabControl_MouseDown;
        tabControl.MouseUp += TabControl_MouseUp;
        tabControl.MouseMove += TabControl_MouseMove;
        tabControl.MouseLeave += TabControl_MouseLeave;
        tabControl.MouseWheel += TabControl_MouseWheel;
        tabControl.DrawItem += TabControl_DrawItem;
        tabControl.DoubleClick += TabControl_DoubleClick;
        tabControl.Paint += TabControl_Paint;
        tabControl.HandleCreated += TabControl_HandleCreated;
        tabControl.HandleDestroyed += TabControl_HandleDestroyed;

        // Main Table Layout (2 columns: gutter | editor+minimap panel)
        mainTable = new TableLayoutPanel();
        mainTable.ColumnCount = 2;
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
        textEditor.SelectionChanged += TextEditor_SelectionChanged;
        textEditor.MouseDown += TextEditor_MouseDown;
        textEditor.MouseWheel += TextEditor_MouseWheel;
        textEditor.Resize += TextEditor_Resize;

        // Guide properties handled directly on textEditor
        textEditor.ShowGuide = true;
        textEditor.GuideColumn = 80;
        textEditor.GuideColor = Color.FromArgb(100, 120, 120, 120);

        // Minimap Control (docked right inside editorPanel, hidden by default)
        minimapControl = new MinimapControl();
        minimapControl.ViewportColor = Color.FromArgb(80, Color.DodgerBlue);
        minimapControl.ViewportBorderColor = Color.DodgerBlue;
        minimapControl.Margin = new Padding(0);
        minimapControl.Dock = DockStyle.None;
        minimapControl.MinimapWidth = 200;
        minimapControl.Visible = false;

        // Whitespace overlay (transparent overlay form positioned over editor)
        whitespaceOverlay = new WhitespaceOverlayForm();
        whitespaceOverlay.ShowGlyphs = false;

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
        statusStrip.SizingGrip = false;
        statusStrip.Padding = new Padding(4, 1, 4, 1);
        lineColLabel = new ToolStripStatusLabel("Ln 1, Col 1");
        vimModeLabel = new ToolStripStatusLabel("");
        vimModeLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        vimModeLabel.Visible = false;
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
        // Git status labels (inserted after tab section)
        gitBranchLabel = new ToolStripStatusLabel("");
        gitBranchLabel.AutoSize = true;
        gitBranchLabel.BorderSides = ToolStripStatusLabelBorderSides.None;
        gitBranchLabel.Padding = new Padding(4, 1, 4, 1);
        gitDirtyLabel = new ToolStripStatusLabel("");
        gitDirtyLabel.AutoSize = true;
        gitDirtyLabel.Padding = new Padding(0, 1, 4, 1);
        gitSyncLabel = new ToolStripStatusLabel("");
        gitSyncLabel.AutoSize = true;
        gitSyncLabel.Padding = new Padding(0, 1, 4, 1);
        linePositionLabel = new ToolStripStatusLabel("1 / 1");
        zoomLabel = new ToolStripStatusLabel("100%");
        lineEndingsLabel = new ToolStripStatusLabel("Windows (CRLF)");
        encodingLabel = new ToolStripStatusLabel("UTF-8");
        scanProgressBar = new ToolStripProgressBar
        {
            Name = "scanProgressBar",
            Style = ProgressBarStyle.Continuous,
            Width = 100,
            Visible = false,
            Alignment = ToolStripItemAlignment.Right
        };
        themeDropDown = new ToolStripDropDownButton();
        themeDropDown.Text = "Theme";
        themeDropDown.Width = 60;
        themeDropDown.Padding = new Padding(4, 1, 4, 1);
        themeDropDown.Alignment = ToolStripItemAlignment.Right;
        themeDropDown.DropDownItems.Add("Dark", null, StatusBarDarkTheme_Click);
        themeDropDown.DropDownItems.Add("Light", null, StatusBarLightTheme_Click);
        fileTypeLabel = new ToolStripStatusLabel("");
        fileTypeLabel.Spring = false;
        fileTypeLabel.AutoSize = false;
        fileTypeLabel.Width = 60;
        fileTypeLabel.Padding = new Padding(4, 1, 4, 1);
        fileTypeLabel.TextAlign = ContentAlignment.MiddleRight;
        fileTypeLabel.Alignment = ToolStripItemAlignment.Right;

        statusStrip.Items.Add(vimModeLabel);
        statusStrip.Items.Add(lineColLabel);
        statusStrip.Items.Add(charCountLabel);
        statusStrip.Items.Add(tabSizeDropDown);
        statusStrip.Items.Add(gitBranchLabel);
        statusStrip.Items.Add(gitDirtyLabel);
        statusStrip.Items.Add(gitSyncLabel);
        statusStrip.Items.Add(new ToolStripStatusLabel() { Spring = true, Padding = new Padding(0, 1, 0, 1) });
        statusStrip.Items.Add(linePositionLabel);
        statusStrip.Items.Add(zoomLabel);
        statusStrip.Items.Add(lineEndingsLabel);
        statusStrip.Items.Add(encodingLabel);
        statusStrip.Items.Add(themeDropDown);
        statusStrip.Items.Add(fileTypeLabel);
        statusStrip.Items.Add(scanProgressBar);

        const int itemPadding = 8;
        lineColLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        charCountLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        tabSizeDropDown.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        linePositionLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        zoomLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        lineEndingsLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);
        encodingLabel.Padding = new Padding(itemPadding, 1, itemPadding, 1);

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

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

        private GutterPanel gutterPanel;
        private ColumnGuidePanel guidePanel;
        private TableLayoutPanel mainTable;
        internal HighlightRichTextBox textEditor;
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
        Padding = new Padding(0);
        ClientSize = new Size(1000, 600);
        Text = "Text Editor";
        StartPosition = FormStartPosition.CenterScreen;

        // Menu Strip
        menuStrip = new MenuStrip();
        menuStrip.Dock = DockStyle.Top;
        menuStrip.RenderMode = ToolStripRenderMode.Professional;
        fileMenu = new ToolStripMenuItem("&File");
        newTabMenuItem = new ToolStripMenuItem("New Tab", null, NewTab_Click);
        newWindowMenuItem = new ToolStripMenuItem("New Window", null, NewWindow_Click);
        openMenuItem = new ToolStripMenuItem("&Open...", null, Open_Click) { ShortcutKeys = Keys.Control | Keys.O };
        recentMenuItem = new ToolStripMenuItem("Recent");
        saveMenuItem = new ToolStripMenuItem("&Save", null, Save_Click) { ShortcutKeys = Keys.Control | Keys.S };
        saveAsMenuItem = new ToolStripMenuItem("Save &As...", null, SaveAs_Click) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.S };
        saveAllMenuItem = new ToolStripMenuItem("Save A&ll", null, SaveAll_Click);
        closeTabMenuItem = new ToolStripMenuItem("Close Tab", null, CloseTab_Click);
        closeWindowMenuItem = new ToolStripMenuItem("Close Window", null, CloseWindow_Click);
        closeAllMenuItem = new ToolStripMenuItem("Close All", null, CloseAll_Click);
        exitMenuItem = new ToolStripMenuItem("E&xit", null, Exit_Click) { ShortcutKeys = Keys.Alt | Keys.F4 };

        fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
            newTabMenuItem, newWindowMenuItem, openMenuItem, recentMenuItem,
            new ToolStripSeparator(), saveMenuItem, saveAsMenuItem, saveAllMenuItem,
            new ToolStripSeparator(), closeTabMenuItem, closeWindowMenuItem, closeAllMenuItem,
            new ToolStripSeparator(), exitMenuItem
        });

        editMenu = new ToolStripMenuItem("&Edit");
        undoMenuItem = new ToolStripMenuItem("&Undo", null, Undo_Click) { ShortcutKeys = Keys.Control | Keys.Z };
        cutMenuItem = new ToolStripMenuItem("Cu&t", null, Cut_Click) { ShortcutKeys = Keys.Control | Keys.X };
        copyMenuItem = new ToolStripMenuItem("&Copy", null, Copy_Click) { ShortcutKeys = Keys.Control | Keys.C };
        pasteMenuItem = new ToolStripMenuItem("&Paste", null, Paste_Click) { ShortcutKeys = Keys.Control | Keys.V };
        deleteMenuItem = new ToolStripMenuItem("&Delete", null, Delete_Click) { ShortcutKeys = Keys.Delete };
        findMenuItem = new ToolStripMenuItem("&Find...", null, Find_Click) { ShortcutKeys = Keys.Control | Keys.F };
        findNextMenuItem = new ToolStripMenuItem("Find &Next", null, FindNext_Click) { ShortcutKeys = Keys.F3 };
        findPreviousMenuItem = new ToolStripMenuItem("Find &Previous", null, FindPrevious_Click) { ShortcutKeys = Keys.Shift | Keys.F3 };
        replaceMenuItem = new ToolStripMenuItem("&Replace...", null, Replace_Click) { ShortcutKeys = Keys.Control | Keys.H };
        gotoMenuItem = new ToolStripMenuItem("&Go To...", null, Goto_Click) { ShortcutKeys = Keys.Control | Keys.G };
        selectAllMenuItem = new ToolStripMenuItem("Select &All", null, SelectAll_Click) { ShortcutKeys = Keys.Control | Keys.A };
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
        zoomInMenuItem = new ToolStripMenuItem("Zoom &In", null, ZoomIn_Click) { ShortcutKeys = Keys.Control | Keys.Add };
        zoomOutMenuItem = new ToolStripMenuItem("Zoom &Out", null, ZoomOut_Click) { ShortcutKeys = Keys.Control | Keys.Subtract };
        restoreDefaultZoomMenuItem = new ToolStripMenuItem("&Restore Default Zoom", null, RestoreDefaultZoom_Click) { ShortcutKeys = Keys.Control | Keys.D0 };

        zoomMenu.DropDownItems.AddRange(new ToolStripItem[] {
            zoomInMenuItem, zoomOutMenuItem, restoreDefaultZoomMenuItem
        });

        statusBarMenuItem = new ToolStripMenuItem("&Status Bar", null, StatusBar_Click) { ShortcutKeys = Keys.F8 };
        statusBarMenuItem.Checked = true;
        wordWrapMenuItem = new ToolStripMenuItem("&Word Wrap", null, WordWrap_Click) { ShortcutKeys = Keys.Control | Keys.W };
        wordWrapMenuItem.Checked = false;
        syntaxHighlightingMenuItem = new ToolStripMenuItem("&Syntax Highlighting", null, SyntaxHighlighting_Click) { ShortcutKeys = Keys.Control | Keys.Y };
        syntaxHighlightingMenuItem.Checked = false;
        
        // Current Line Highlight submenu
        currentLineHighlightMenu = new ToolStripMenuItem("Current Line &Highlight");
        currentLineOffMenuItem = new ToolStripMenuItem("&Off", null, CurrentLineHighlightMode_Click);
        currentLineNumberOnlyMenuItem = new ToolStripMenuItem("Number &Only", null, CurrentLineHighlightMode_Click);
        currentLineWholeLineMenuItem = new ToolStripMenuItem("&Whole Line", null, CurrentLineHighlightMode_Click);
        currentLineHighlightMenu.DropDownItems.AddRange(new ToolStripItem[] {
            currentLineOffMenuItem, currentLineNumberOnlyMenuItem, currentLineWholeLineMenuItem
        });
        
         // Tab handling: Insert Spaces toggle
         insertSpacesMenuItem = new ToolStripMenuItem("&Insert Spaces", null, InsertSpaces_Click);
         insertSpacesMenuItem.Checked = true;
         insertSpacesMenuItem.CheckOnClick = true;
         
         // Tab Size submenu
         tabSizeMenu = new ToolStripMenuItem("Tab &Size");
         tab2MenuItem = new ToolStripMenuItem("&2", null, (s, e) => SetTabSize(2));
         tab4MenuItem = new ToolStripMenuItem("&4", null, (s, e) => SetTabSize(4));
         tab6MenuItem = new ToolStripMenuItem("&6", null, (s, e) => SetTabSize(6));
         tab8MenuItem = new ToolStripMenuItem("&8", null, (s, e) => SetTabSize(8));
         tab10MenuItem = new ToolStripMenuItem("1&0", null, (s, e) => SetTabSize(10));
         tab12MenuItem = new ToolStripMenuItem("1&2", null, (s, e) => SetTabSize(12));
         tabSizeMenu.DropDownItems.AddRange(new ToolStripItem[] {
             tab2MenuItem, tab4MenuItem, tab6MenuItem, tab8MenuItem, tab10MenuItem, tab12MenuItem
         });
         
         // Auto Indent, Smart Tabs, Elastic Tab Stops toggles
         autoIndentMenuItem = new ToolStripMenuItem("Auto &Indent", null, AutoIndent_Click);
         autoIndentMenuItem.Checked = true;
         autoIndentMenuItem.CheckOnClick = true;
         
         smartTabsMenuItem = new ToolStripMenuItem("Smart &Tabs", null, SmartTabs_Click);
         smartTabsMenuItem.Checked = true;
         smartTabsMenuItem.CheckOnClick = true;
         
         elasticTabsMenuItem = new ToolStripMenuItem("&Elastic Tab Stops", null, ElasticTabs_Click);
         elasticTabsMenuItem.Checked = true;
         elasticTabsMenuItem.CheckOnClick = true;
         
          gutterMenuItem = new ToolStripMenuItem("&Gutter", null, GutterMenuItem_Click);
          gutterMenuItem.Checked = false;

          // Minimap toggle (initially unchecked)
          minimapMenuItem = new ToolStripMenuItem("Minimap", null, MinimapMenuItem_Click);
          minimapMenuItem.Checked = false; // Start hidden
          minimapMenuItem.CheckOnClick = true;

          // Vim mode toggle
          vimModeMenuItem = new ToolStripMenuItem("&Vim Mode", null, ToggleVimMode) { ShortcutKeys = Keys.Control | Keys.Alt | Keys.V };

         // Column Guide menu (checkable with dropdown for widths)
        columnGuideMenuItem = new ToolStripMenuItem("C&olumn Guide", null, ColumnGuide_Click);
        columnGuideMenuItem.Checked = false;
        columnGuideMenuItem.CheckOnClick = true;
        col72MenuItem = new ToolStripMenuItem("&72", null, (s, e) => SetGuideColumn(72));
        col80MenuItem = new ToolStripMenuItem("&80", null, (s, e) => SetGuideColumn(80));
        col100MenuItem = new ToolStripMenuItem("&100", null, (s, e) => SetGuideColumn(100));
        col120MenuItem = new ToolStripMenuItem("&120", null, (s, e) => SetGuideColumn(120));
        col150MenuItem = new ToolStripMenuItem("&150", null, (s, e) => SetGuideColumn(150));
        colCustomMenuItem = new ToolStripMenuItem("&Custom...", null, (s, e) => OpenCustomColumnDialog());
        columnGuideMenuItem.DropDownItems.AddRange(new ToolStripItem[] {
            col72MenuItem, col80MenuItem, col100MenuItem, col120MenuItem, col150MenuItem, new ToolStripSeparator(), colCustomMenuItem
        });

        themeMenu = new ToolStripMenuItem("&Theme");
        darkThemeMenuItem = new ToolStripMenuItem("&Dark", null, DarkTheme_Click);
        lightThemeMenuItem = new ToolStripMenuItem("&Light", null, LightTheme_Click);
        themeMenu.DropDownItems.AddRange(new ToolStripItem[] { darkThemeMenuItem, lightThemeMenuItem });

          viewMenu.DropDownItems.AddRange(new ToolStripItem[] {
              zoomMenu, new ToolStripSeparator(), statusBarMenuItem, wordWrapMenuItem,
              syntaxHighlightingMenuItem, currentLineHighlightMenu, insertSpacesMenuItem, tabSizeMenu,
              autoIndentMenuItem, smartTabsMenuItem, elasticTabsMenuItem,
              gutterMenuItem, columnGuideMenuItem, minimapMenuItem, vimModeMenuItem, new ToolStripSeparator(), themeMenu
          });

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, viewMenu });

        // Tab Control for multi-file editing
        tabControl = new TabControl();
        tabControl.Dock = DockStyle.Top;
        tabControl.Height = 30;
        tabControl.Multiline = true;
        tabControl.HotTrack = true;
        tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabControl.Alignment = TabAlignment.Top;
        tabControl.SizeMode = TabSizeMode.Fixed;
        tabControl.ItemSize = new Size(120, 30);
        tabControl.Padding = new Point(12, 4);
        tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        tabControl.MouseDown += TabControl_MouseDown;
        tabControl.MouseUp += TabControl_MouseUp;
        tabControl.MouseMove += TabControl_MouseMove;
        tabControl.DrawItem += TabControl_DrawItem;
        tabControl.DoubleClick += TabControl_DoubleClick;

        // Main Table Layout (2 columns: gutter | editor)
        mainTable = new TableLayoutPanel();
        mainTable.ColumnCount = 2;
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));    // Gutter column
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Editor column
        mainTable.RowCount = 1;
        mainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainTable.Dock = DockStyle.Fill;
        mainTable.Margin = new Padding(0);
        mainTable.Padding = new Padding(0);

        // Gutter Panel
        gutterPanel = new GutterPanel(this);
        gutterPanel.Dock = DockStyle.Fill;
        gutterPanel.Margin = new Padding(0);

        // Text Editor (RichTextBox)
        textEditor = new HighlightRichTextBox();
        textEditor.Dock = DockStyle.Fill;
        textEditor.Multiline = true;
        textEditor.ScrollBars = RichTextBoxScrollBars.Both;
        textEditor.AcceptsTab = true; // allow Tab key input (we handle it)
        textEditor.Font = new Font("Consolas", 12);
        textEditor.BorderStyle = BorderStyle.None;
        textEditor.Margin = new Padding(0);
        textEditor.Padding = new Padding(0);
        textEditor.TextChanged += TextEditor_TextChanged;
        textEditor.SelectionChanged += TextEditor_SelectionChanged;
        textEditor.VScroll += TextEditor_VScroll;
        textEditor.KeyDown += TextEditor_KeyDown;
        textEditor.Resize += TextEditor_Resize;

        // Minimap Control (overlay)
        minimapControl = new MinimapControl();
        minimapControl.MinimapWidth = 100;
        minimapControl.Scale = 1.0f;
        minimapControl.ShowColors = false;
        minimapControl.ViewportColor = Color.FromArgb(80, Color.DodgerBlue);
        minimapControl.ViewportBorderColor = Color.DodgerBlue;
        minimapControl.Margin = new Padding(0);
        minimapControl.Dock = DockStyle.None;
        minimapControl.Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        textEditor.Controls.Add(minimapControl);
        minimapControl.BringToFront();

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

        // Assemble table
        mainTable.Controls.Add(gutterPanel, 0, 0);
        mainTable.Controls.Add(textEditor, 1, 0);

        // Status Strip
        statusStrip = new StatusStrip();
        statusStrip.Dock = DockStyle.Bottom;
        statusStrip.RenderMode = ToolStripRenderMode.System; // flat
        lineColLabel = new ToolStripStatusLabel("Ln 1, Col 1");
        charCountLabel = new ToolStripStatusLabel("0 characters");
        // Tab size dropdown for status bar
        tabSizeDropDown = new ToolStripDropDownButton();
        tabSizeDropDown.Text = "Tab: 4";
        tabSizeDropDown.Width = 60;
        tabSizeDropDown.Alignment = ToolStripItemAlignment.Left;
        tabSizeDropDown.DropDownItems.Add("2", null, TabSize2_Click);
        tabSizeDropDown.DropDownItems.Add("4", null, TabSize4_Click);
        tabSizeDropDown.DropDownItems.Add("8", null, TabSize8_Click);
        linePositionLabel = new ToolStripStatusLabel("1 / 1");
        zoomLabel = new ToolStripStatusLabel("100%");
        lineEndingsLabel = new ToolStripStatusLabel("Windows (CRLF)");
        encodingLabel = new ToolStripStatusLabel("UTF-8");

        // Theme dropdown for status bar
        themeDropDown = new ToolStripDropDownButton();
        themeDropDown.Text = "Theme";
        themeDropDown.Width = 80;
        themeDropDown.Alignment = ToolStripItemAlignment.Left;
        themeDropDown.DropDownItems.Add("Dark", null, StatusBarDarkTheme_Click);
        themeDropDown.DropDownItems.Add("Light", null, StatusBarLightTheme_Click);

        fileTypeLabel = new ToolStripStatusLabel("");
        fileTypeLabel.Spring = false;
        fileTypeLabel.AutoSize = false;
        fileTypeLabel.Width = 80;
        fileTypeLabel.TextAlign = ContentAlignment.MiddleLeft;

        statusStrip.Items.AddRange(new ToolStripItem[] {
            lineColLabel, charCountLabel, tabSizeDropDown, new ToolStripStatusLabel { Spring = true },
            linePositionLabel, zoomLabel, lineEndingsLabel, encodingLabel, themeDropDown, fileTypeLabel
        });

        // Add padding between items for visual separation
        const int itemPadding = 8;
        lineColLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        charCountLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        tabSizeDropDown.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        linePositionLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        zoomLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        lineEndingsLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);
        encodingLabel.Padding = new Padding(itemPadding, 0, itemPadding, 0);

         // Add controls: Top (menu, tabs), Fill (main table), Bottom (status)
         Controls.Add(menuStrip);
         Controls.Add(tabControl);
         Controls.Add(mainTable);
         Controls.Add(statusStrip);

         // Set main menu strip
         MainMenuStrip = menuStrip;

         // Ensure proper z-order: menuStrip at top (index 0), then tabControl (index 1), then mainTable (index 2), then statusStrip (index 3)
         Controls.SetChildIndex(menuStrip, 0);
         Controls.SetChildIndex(tabControl, 1);
         Controls.SetChildIndex(mainTable, 2);
         Controls.SetChildIndex(statusStrip, 3);

         // Ensure layout is performed after all controls added
         ResumeLayout(false);
         PerformLayout();
    }
}

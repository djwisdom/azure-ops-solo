 using System;
 using System.Collections.Generic;
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
 using System.Linq;
 using System.Security.Cryptography;
 using System.Runtime.CompilerServices;

 [assembly: InternalsVisibleTo("MyCrownJewelApp.Tests")]

 namespace MyCrownJewelApp.Pfpad
 {
     public partial class Form1 : Form
     {
        // Win32 API for dark scrollbar support
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

        // Win32 API for non-client area file drop (title bar, etc.)
        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile,
            [Out] char[]? lpszFile, uint cch);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        private const int WM_DROPFILES = 0x0233;

        // RichTextBox messages
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;
        private const int SB_TOP = 6;

        // DWM API for native title bar dark mode (Windows 10 1809+)
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows 10 1809+, Windows 11

        private void ApplyScrollbarTheme()
        {
            string? themeName = isDarkTheme ? DARK_MODE_SCROLLBAR : null;
            if (textEditor != null && textEditor.IsHandleCreated)
                SetWindowTheme(textEditor.Handle, themeName, null);
            if (_splitEditor != null && _splitEditor.IsHandleCreated)
                SetWindowTheme(_splitEditor.Handle, themeName, null);
        }

        private void ApplyTitleBarTheme()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                int darkMode = isDarkTheme ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch { /* DWM not available or attribute not supported */ }
        }

        // State
        private HashSet<int> bookmarks = new();
        private HashSet<int> modifiedLines = new();
        private HashSet<int> collapsedRegions = new();
        internal bool gutterVisible = true;
        private bool statusBarVisible = true;
        internal bool wordWrapEnabled = false;
        internal bool syntaxHighlightingEnabled = false;
        private CurrentLineHighlightMode currentLineHighlightMode = CurrentLineHighlightMode.Off;
        internal int tabSize = 4;
        private bool insertSpaces = true;
        private Theme _currentTheme = Theme.Dark;
        internal bool isDarkTheme => !_currentTheme.IsLight;
        public bool IsDarkTheme => !_currentTheme.IsLight;
        internal float zoomFactor = 1.0f;
        private bool isHighlighting = false;
        private int lastHighlightedLine = -1;

        // Vim mode state
        private bool vimModeEnabled = false;
        private VimEngine? vimEngine;

        // Terminal state
        private TabControl? _terminalTabControl;
        private SplitContainer? _terminalSplitContainer;
        private Button? _terminalNewTabButton;
        private readonly List<TerminalPanel> _terminalTabs = new();
        private bool _terminalVisible = false;
        private int _terminalHeight = 200;
        private string _terminalShell = "";

        private TerminalPanel? ActiveTerminal =>
            _terminalTabs.Count > 0 && _terminalTabControl?.SelectedIndex >= 0
                ? _terminalTabs[_terminalTabControl.SelectedIndex]
                : null;

        // Minimap state
        private ToolStripMenuItem minimapMenuItem = null!;
        private bool _pendingMinimapVisible = false;

        // Properties for GutterPanel
        public CurrentLineHighlightMode LineHighlightMode => currentLineHighlightMode;

        // Tab behavior settings
        private bool autoIndentEnabled = true;
        private bool smartTabsEnabled = true;
        private bool elasticTabsEnabled = true;

        // Document management (tabs)
        public class Document
        {
            public string? FilePath { get; set; }
            public string Content { get; set; } = "";
            public bool IsDirty { get; set; }
            public HashSet<int> ModifiedLines { get; set; } = new();
            public HashSet<int> Bookmarks { get; set; } = new();
            public HashSet<int> CollapsedRegions { get; set; } = new();
            public string? SavedHash { get; set; }
            public DateTime? LastWriteTime { get; set; }
            public int SelectionStart { get; set; }
            public int SelectionLength { get; set; }
            public int FirstVisibleLine { get; set; }
            public SyntaxDefinition? Syntax { get; set; }
            public int? UntitledNumber { get; set; }  // null for non-untitled docs

            public string DisplayName =>
                string.IsNullOrEmpty(FilePath) && UntitledNumber.HasValue ? $"Untitled{UntitledNumber}" :
                string.IsNullOrEmpty(FilePath) ? "Untitled" :
                Path.GetFileName(FilePath);
        }

        internal List<Document> documents = new();
        internal int activeDocIndex = -1;
        private int nextUntitledNumber = 1;
        private int? hoveredTabIndex = null;
        private Rectangle? closeButtonBounds = null;
        private bool _closeButtonHovered = false;

        // Tab drag state for tear-away
        private int? _draggedTabIndex = null;
        private Point? _dragStartPoint = null;
        private bool _isDragging = false;

        // Split view state
        private RichTextBox? _splitEditor = null;
        private Document? _splitDocument = null;
        private string? _splitDocumentTitle = null;

        // Helper to get active document
        internal Document ActiveDoc => activeDocIndex >= 0 && activeDocIndex < documents.Count ? documents[activeDocIndex] : null!;

        // Suspend selection changed events during internal updates
        private bool _suspendSelectionChanged = false;

        // Elastic tab stops system
        private System.Windows.Forms.Timer? elasticTabTimer;
        private CancellationTokenSource? tabComputeCts;

        // Git integration
        private System.Windows.Forms.Timer? _gitPollTimer;
        private string _gitBranchName = "";
        private string _gitDirtyStatus = "";
        private string _gitSyncStatus = "";

        // Code folding
        private FoldingManager? _foldingManager;
        private bool _suppressFoldRescan = false;
        public FoldingManager? FoldingManager => _foldingManager;

        // Syntax highlighting
        internal SyntaxDefinition? currentSyntax;
        private IncrementalHighlighter? incrementalHighlighter;

        // Column guide state
        internal int guideColumn = 80;
        internal bool showGuide = true;
        private readonly Color guideColor = Color.FromArgb(60, 60, 60);

        // Theme management
        private ThemeManager _themeManager = ThemeManager.Instance;
        private string fontName = "Consolas";
        private float fontSize = 12f;
        
        // Colors - for syntax highlighting
        // Current file state
        internal string? currentFilePath;
        private bool isModified = false;
        private DateTime? lastFileWriteTime;

        // Per-buffer dirty-flag system: saved snapshot (hash)
        private string? savedContentHash = null;

        // Recent files
        private const int MaxRecentFiles = 10;
        private List<string> recentFiles = new List<string>();

        // Settings persistence
        private record AppSettings(
            bool WordWrapEnabled,
            bool GutterVisible,
            bool StatusBarVisible,
            bool ShowGuide,
            int GuideColumn,
            int TabSize,
            string FontName,
            float FontSize,
            bool InsertSpaces,
            bool AutoIndentEnabled,
            bool SmartTabsEnabled,
            bool ElasticTabsEnabled,
            CurrentLineHighlightMode CurrentLineHighlightMode,
            bool SyntaxHighlightingEnabled,
            bool MinimapVisible,
            bool TerminalVisible = false,
            int TerminalHeight = 200,
            string TerminalShellPath = "",
            string ThemeName = "Dark"
        );

        private string SettingsFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyCrownJewelApp",
                "TextEditor",
                "settings.json"
            );

        // Designer fields (accessible via Controls collection)
        public HashSet<int> Bookmarks => bookmarks;
        public HashSet<int> ModifiedLines => modifiedLines;
        public HashSet<int> CollapsedRegions => collapsedRegions;

        private Button _tabDropdownButton = null!;
        private bool _applyingHighlight;

    public Form1()
        : this(skipInitialDocument: false)
    {
    }

    /// <summary>
    /// Internal constructor used by tear-away to avoid creating an extra untitled document.
    /// </summary>
    internal Form1(bool skipInitialDocument)
    {
        InitializeComponent();
        this.KeyPreview = true;
        this.KeyDown += Form1_KeyDown;
        this.FormClosing += Form1_FormClosing;
        this.Activated += Form1_Activated;
        this.Shown += (s, e) =>
        {
            PositionTabDropdownButton();
        };

        // Enable file drop support (client area + non-client area)
        EnableFileDrop();

        // Initialize document list and process command line args
        documents = new List<Document>();
        activeDocIndex = -1;

        if (!skipInitialDocument)
        {
            // Process command line arguments (files)
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string path = args[i];
                    if (File.Exists(path))
                    {
                        OpenFileInNewTab(path);
                    }
                }
            }

            // If no documents were opened, create a new untitled document
            if (documents.Count == 0)
            {
                NewFile(isInitial: true);
            }
        }

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
            
            // Load persisted settings (overrides defaults below)
            LoadSettings();
            
            // Apply loaded font after settings are loaded
            try { textEditor.Font = new Font(fontName, fontSize); } catch { }
            
            // Subscribe to handle creation BEFORE any operations that might cause handle creation
            textEditor.HandleCreated += (s, e) =>
            {
                ApplyScrollbarTheme();
                PositionMinimap();
                if (syntaxHighlightingEnabled)
                {
                    CreateIncrementalHighlighter();
                }
            };
            textEditor.HandleDestroyed += (s, e) =>
            {
                incrementalHighlighter?.Dispose();
                incrementalHighlighter = null;
            };
            
            UpdateThemeColors(_currentTheme);

            // Add Catppuccin themes to View > Theme menu
            themeMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var name in ThemeManager.ThemeNames)
            {
                if (name == "Dark" || name == "Light") continue;
                var item = new ToolStripMenuItem(name.Replace("Catppuccin ", ""));
                item.Tag = name;
                item.Click += CatppuccinTheme_Click;
                themeMenu.DropDownItems.Add(item);
            }
            UpdateThemeDropDown();
            ApplyWordWrap();
            UpdateStatusBar();
            UpdateColumnGuideMenuChecked();
            UpdateTabSizeMenu();
            UpdateTabSizeDropdown();
            UpdateTabStops();
            UpdateCurrentLineHighlightMenu();
            if (textEditor != null)
            {
                textEditor.CurrentLineHighlightMode = currentLineHighlightMode;
            }

            // Initialize toggles to match loaded/default settings
            gutterMenuItem.Checked = gutterVisible;
            columnGuideMenuItem.Checked = showGuide;
            minimapMenuItem.Checked = _pendingMinimapVisible;
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            insertSpacesMenuItem.Checked = insertSpaces;
            autoIndentMenuItem.Checked = autoIndentEnabled;
            smartTabsMenuItem.Checked = smartTabsEnabled;
            elasticTabsMenuItem.Checked = elasticTabsEnabled;

            // Initialize incremental syntax highlighter if enabled
            if (syntaxHighlightingEnabled)
            {
                CreateIncrementalHighlighter();
            }

            // Apply visibility states
            gutterPanel.Visible = gutterVisible;
            if (showGuide)
            {
                textEditor!.ShowGuide = true;
                textEditor!.GuideColumn = guideColumn;
            }
            minimapControl.Visible = _pendingMinimapVisible;
            statusStrip.Visible = statusBarVisible;
            
            // Set initial column widths for visible state
            if (mainTable.ColumnCount >= 1 && gutterPanel != null)
            {
                if (gutterVisible)
                {
                    gutterPanel.UpdateLineNumberWidth();
                }
                else
                {
                    gutterPanel.Visible = false;
                }
            }
            
            // Attach minimap to editor
            if (minimapControl != null && textEditor != null)
            {
                minimapControl.ViewportChanged += MinimapControl_ViewportChanged;
                minimapControl.SetTokenProvider(GetTokensForLine);
                PositionMinimap();
            }

            // Tab dropdown menu button (rightmost corner of tab strip)
            _tabDropdownButton = new Button
            {
                Text = "\u25BC",
                Font = new Font("Segoe UI", 7),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(20, 30),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _tabDropdownButton.FlatAppearance.BorderSize = 0;
            _tabDropdownButton.Click += TabDropdownButton_Click;
            if (tabControl != null)
            {
                this.Controls.Add(_tabDropdownButton);
                _tabDropdownButton.BringToFront();
                tabControl.Resize += (s, e) => PositionTabDropdownButton();
                tabControl.ControlAdded += (s, e) => PositionTabDropdownButton();
                tabControl.ControlRemoved += (s, e) => PositionTabDropdownButton();
            }

            // Initialize folding manager and scan regions
            _foldingManager = new FoldingManager(textEditor!);
            textEditor!.FoldingManager = _foldingManager;
            textEditor!.TextChanged += (s, e) =>
            {
                if (!_suppressFoldRescan) _foldingManager?.ScanRegions();
                gutterPanel?.RefreshGutter();
            };
            _foldingManager.ScanRegions();
            
            // Elastic tab stops debounce timer
            elasticTabTimer = new System.Windows.Forms.Timer();
            elasticTabTimer.Interval = 250; // 250ms after last change
            elasticTabTimer.Tick += (s, e) => { elasticTabTimer.Stop(); if (elasticTabsEnabled) ComputeElasticTabStopsAsync(); };

             // Git status polling (every 3 seconds)
             _gitPollTimer = new System.Windows.Forms.Timer();
             _gitPollTimer.Interval = 3000;
             _gitPollTimer.Tick += (s, e) => PollGitStatus();
             _gitPollTimer.Start();

             // Initialize incremental highlighter (after colors are loaded)
             if (documents.Count > 0) CreateIncrementalHighlighter();

              // Initialize Vim engine
              vimEngine = new VimEngine(textEditor!);
              vimEngine.SaveRequested += () => { if (currentFilePath != null) SaveFile(); else SaveAsFile(); };
              vimEngine.SaveAsRequested += (filename) =>
              {
                  string dir = currentFilePath != null ? Path.GetDirectoryName(currentFilePath)!
                      : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                  string fullPath = Path.GetFullPath(Path.Combine(dir, filename));
                  try
                  {
                      File.WriteAllText(fullPath, textEditor.Text);
                      currentFilePath = fullPath;
                      lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                      currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
                      ClearDirtyAfterSave();
                      modifiedLines.Clear();
                      AddToRecentFiles(currentFilePath);
                      UpdateStatusBar();
                      if (syntaxHighlightingEnabled)
                          CreateIncrementalHighlighter();
                  }
                  catch (Exception ex)
                  {
                      MessageBox.Show($"Error saving file: {ex.Message}", "Save Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
                  }
              };
              vimEngine.CloseRequested += () => this.Close();
              vimEngine.VerticalSplitRequested += () =>
              {
                  if (documents.Count > 0)
                      SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Right);
              };
              vimEngine.HorizontalSplitRequested += () =>
              {
                  if (documents.Count > 0)
                      SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Bottom);
              };
              vimEngine.InsertSpacesRequested += (v) => { if (insertSpaces != v) ToggleInsertSpaces(); };
              vimEngine.TabSizeRequested += (s) => SetTabSize(s);
              vimEngine.AutoIndentRequested += (v) => { if (autoIndentEnabled != v) ToggleAutoIndent(); };
              vimEngine.SmartTabsRequested += (v) => { if (smartTabsEnabled != v) ToggleSmartTabs(); };
              vimEngine.GoToLineRequested += (line) => GoToLine(line);

             // Initialize integrated terminal with tab support
             var defaultShell = string.IsNullOrEmpty(_terminalShell) ? null : _terminalShell;

             // Tab control for multiple terminal sessions
             _terminalTabControl = new TabControl
             {
                 Dock = DockStyle.Fill,
                 Padding = new Point(12, 4),
                 Margin = new Padding(0),
                 ItemSize = new Size(140, 26),
                 HotTrack = true,
                 DrawMode = TabDrawMode.OwnerDrawFixed,
                 Alignment = TabAlignment.Top,
                 BackColor = _currentTheme.MenuBackground
             };
             _terminalTabControl.HandleCreated += (s, e) =>
             {
                 SetWindowTheme(_terminalTabControl.Handle, "", "");
                 int style = GetWindowLong(_terminalTabControl.Handle, GWL_STYLE);
                 style = style & ~WS_BORDER;
                 const int TCS_FLATBUTTONS = 0x0008;
                 style |= TCS_FLATBUTTONS;
                 SetWindowLong(_terminalTabControl.Handle, GWL_STYLE, style);
                 int exStyle = GetWindowLong(_terminalTabControl.Handle, GWL_EXSTYLE);
                 exStyle &= ~WS_EX_CLIENTEDGE;
                 SetWindowLong(_terminalTabControl.Handle, GWL_EXSTYLE, exStyle);
                 const uint SWP_FRAMECHANGED = 0x0020;
                 const uint SWP_NOACTIVATE = 0x0010;
                 const uint SWP_NOMOVE = 0x0002;
                 const uint SWP_NOSIZE = 0x0001;
                 const uint SWP_NOZORDER = 0x0004;
                 SetWindowPos(_terminalTabControl.Handle, IntPtr.Zero, 0, 0, 0, 0,
                     SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
                 var theme = _currentTheme;
                 SendMessage(_terminalTabControl.Handle, TCM_SETBKCOLOR, IntPtr.Zero,
                     ColorTranslator.ToWin32(theme.MenuBackground));
                 const int WM_UPDATEUISTATE = 0x0128;
                 const int UIS_SET = 1;
                 const int UISF_HIDEFOCUS = 0x1;
                 SendMessage(_terminalTabControl.Handle, WM_UPDATEUISTATE,
                     (UISF_HIDEFOCUS << 16) | UIS_SET, IntPtr.Zero);
                 _terminalTabStripWindow?.ReleaseHandle();
                 _terminalTabStripWindow = new TabStripBackgroundWindow(this, _terminalTabControl);
                 _terminalTabStripWindow.AssignHandle(_terminalTabControl.Handle);
             };
             _terminalTabControl.HandleDestroyed += (s, e) =>
             {
                 _terminalTabStripWindow?.ReleaseHandle();
                 _terminalTabStripWindow = null;
             };
             _terminalTabControl.DrawItem += TerminalTabControl_DrawItem;
             _terminalTabControl.MouseDown += TerminalTabControl_MouseDown;
             _terminalTabControl.SelectedIndexChanged += TerminalTabControl_SelectedIndexChanged;
             _terminalTabControl.Resize += (s, e) => PositionTerminalNewTabButton();

             // Create first terminal tab
             AddTerminalTab(defaultShell);

             // "+" button positioned in the tab strip area
             _terminalNewTabButton = new Button
             {
                 Text = "+",
                 Font = new Font("Segoe UI", 11, FontStyle.Bold),
                 FlatStyle = FlatStyle.Flat,
                 Size = new Size(22, 26),
                 Cursor = Cursors.Hand,
                 TabStop = false,
                 UseVisualStyleBackColor = false,
                 BackColor = _currentTheme.MenuBackground,
                 ForeColor = _currentTheme.Text
             };
             _terminalNewTabButton.FlatAppearance.BorderSize = 0;
             _terminalNewTabButton.Click += (s, e) => AddTerminalTab(defaultShell);

             // Replace editor row with a draggable SplitContainer (editor top, terminal bottom)
             _terminalSplitContainer = new SplitContainer
             {
                 Dock = DockStyle.Fill,
                 Orientation = Orientation.Horizontal,
                 Panel1MinSize = 100,
                 Panel2MinSize = 60,
                 SplitterWidth = 4,
                 SplitterIncrement = 8,
                 Panel2Collapsed = !_terminalVisible,
                 BorderStyle = BorderStyle.None
             };
             _terminalSplitContainer.HandleCreated += (s, e) =>
                 SetWindowTheme(_terminalSplitContainer.Handle, "", "");
             _terminalSplitContainer.SplitterMoved += (s, e) =>
                 _terminalHeight = Math.Max(60, _terminalSplitContainer.Height
                     - _terminalSplitContainer.SplitterDistance
                     - _terminalSplitContainer.SplitterWidth);

             // Panel1: existing editor (mainTable with gutter, editor, minimap)
             mainLayout.SuspendLayout();
             mainLayout.Controls.Remove(mainTable);
             _terminalSplitContainer.Panel1.Controls.Add(mainTable);
             mainTable.Dock = DockStyle.Fill;
             _terminalSplitContainer.Panel1.BackColor = _currentTheme.EditorBackground;

             // Panel2: tab control + new-tab button
             _terminalSplitContainer.Panel2.BackColor = _currentTheme.MenuBackground;
             _terminalSplitContainer.Panel2.Controls.Add(_terminalTabControl);
             _terminalSplitContainer.Panel2.Controls.Add(_terminalNewTabButton);

             // Insert split container into row 2, shift status up
             mainLayout.Controls.Add(_terminalSplitContainer, 0, 2);
             mainLayout.SetRow(statusStrip, 3);
             mainLayout.RowCount = 4;
             mainLayout.RowStyles.Clear();
             mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 0: menu
             mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // 1: tabs
             mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // 2: editor + terminal (split)
             mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 3: status
             mainLayout.ResumeLayout(true);

             // Set initial splitter position if terminal should be visible
             if (_terminalVisible)
             {
                 ShowTerminal();
             }

             // Apply terminal theme to match editor
             UpdateTerminalTheme();

             // Ensure initial dirty flag is clear after all initialization
             isModified = false;
         }

        #region File Drop Support

        /// <summary>
        /// Enables file drop support on the form and editor control.
        /// Supports both client area (standard OLE) and non-client area (title bar) via WM_DROPFILES.
        /// </summary>
        private void EnableFileDrop()
        {
            // Standard WinForms drag-drop (client area: form and editor control)
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Also enable on the editor control for precise targeting
            if (textEditor != null)
            {
                textEditor.AllowDrop = true;
                textEditor.DragEnter += MainForm_DragEnter;
                textEditor.DragDrop += MainForm_DragDrop;
            }

            // Non-client area drop (title bar, borders) handled via WM_DROPFILES in WndProc
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Enable file drop on non-client area (title bar, etc.)
            DragAcceptFiles(this.Handle, true);
            ApplyTitleBarTheme();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DROPFILES)
            {
                HandleWmDropFiles(m.WParam);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            // Accept only file drops
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    // Optional: restrict to text-like extensions
                    string ext = Path.GetExtension(files[0]);
                    bool isTextFile = string.IsNullOrEmpty(ext) ||
                        ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".c", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".cpp", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".h", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".hpp", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".bicep", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".tf", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".sh", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".bash", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".htm", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".jsonc", StringComparison.OrdinalIgnoreCase);

                    if (isTextFile)
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }

            e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return;

                string filePath = files[0]; // Handle first file (ignore additional)

                // Security: validate path exists and is not a directory
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("The file does not exist.", "File Drop Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Security: validate path is absolute (not relative or UNC traversal)
                try
                {
                    string fullPath = Path.GetFullPath(filePath);
                    if (!fullPath.StartsWith(Path.GetFullPath(Environment.CurrentDirectory)) &&
                        !fullPath.StartsWith(Path.GetPathRoot(fullPath) ?? fullPath))
                    {
                        // Path is suspicious; reject
                        MessageBox.Show("Invalid file path.", "File Drop Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch { }

                // Optional: file size limit (10MB)
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    const long maxSize = 10L * 1024 * 1024; // 10 MB
                    if (fileInfo.Length > maxSize)
                    {
                        MessageBox.Show($"File is too large ({fileInfo.Length / 1024 / 1024}MB). Maximum allowed is 10MB.",
                            "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    MessageBox.Show($"Cannot access file: {ex.Message}", "File Access Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Load file content(s) - each in its own tab
                foreach (string file in files)
                {
                    try
                    {
                        OpenFileInNewTab(file);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open '{file}': {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open dropped file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleWmDropFiles(IntPtr hDrop)
        {
            try
            {
                uint fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);

                if (fileCount > 0)
                {
                    for (uint i = 0; i < fileCount; i++)
                    {
                        char[] fileName = new char[260];
                        DragQueryFile(hDrop, i, fileName, 260);
                        string path = new string(fileName).TrimEnd('\0');

                        // Validate file existence
                        if (!File.Exists(path)) continue;

                        // Open each file in its own tab on UI thread
                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(() => OpenFileInNewTab(path)));
                        }
                        else
                        {
                            OpenFileInNewTab(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open dropped file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                DragFinish(hDrop);
            }
        }

        #endregion

        private void MinimapControl_ViewportChanged(object? sender, ViewportChangedEventArgs e)
        {
            // Optional: update status bar or perform other actions when viewport changes
            // Could sync with editor if needed; minimap already scrolls editor directly
        }

        #region Tab Stops

        // Compute fixed tab stops based on current font and tabSize (used when elastic tabs disabled)
        private void ComputeFixedTabStops()
        {
            if (textEditor == null || !textEditor.IsHandleCreated) return;
            if (elasticTabsEnabled) return;

            try
            {
                int charWidth;
                // Use editor's native character positioning for accurate tab stop calculation
                if (textEditor.TextLength > 0)
                {
                    Point p0 = textEditor.GetPositionFromCharIndex(0);
                    Point p1 = textEditor.GetPositionFromCharIndex(1);
                    charWidth = p1.X - p0.X;
                }
                else
                {
                    // Editor empty — measure a single character using TextRenderer (GDI)
                    var size = TextRenderer.MeasureText("0", textEditor.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    charWidth = size.Width;
                }

                if (charWidth <= 0) charWidth = 8; // sensible fallback

                int tabPixelWidth = Math.Max(1, charWidth * tabSize);
                var stops = new List<int>();
                for (int pos = tabPixelWidth; pos <= tabPixelWidth * 500; pos += tabPixelWidth)
                {
                    stops.Add(pos);
                }
                textEditor.SelectionTabs = stops.ToArray();
            }
            catch { }
        }

        private void UpdateTabStops()
        {
            if (elasticTabsEnabled)
            {
                elasticTabTimer?.Stop();
                elasticTabTimer?.Start();
            }
            else
            {
                ComputeFixedTabStops();
            }
        }

        #endregion

        #region Elastic Tab Stops

        // Compute and apply elastic tab stops for visible lines on background thread
        private void ComputeElasticTabStopsAsync()
        {
            if (textEditor == null || !textEditor.IsHandleCreated) return;
            
            tabComputeCts?.Cancel();
            var cts = new CancellationTokenSource();
            tabComputeCts = cts;
            var token = cts.Token;
            
            Task.Run(() =>
            {
                try
                {
                    // Capture needed UI data on UI thread (non-blocking)
                    string[] lines = Array.Empty<string>();
                    int firstVisible = 0;
                    int visibleLineCount = 0;
                    string? fontName = null;
                    float fontSize = 0;
                    FontStyle fontStyle = FontStyle.Regular;

                    var ar = textEditor.BeginInvoke(new Action(() =>
                    {
                        if (textEditor.IsDisposed) return;
                        lines = textEditor.Lines;
                        firstVisible = (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
                        using var g = Graphics.FromHwnd(textEditor.Handle);
                        int lineHeight = TextRenderer.MeasureText("X", textEditor.Font).Height;
                        visibleLineCount = textEditor.ClientSize.Height / lineHeight + 2;
                        fontName = textEditor.Font.Name;
                        fontSize = textEditor.Font.Size;
                        fontStyle = textEditor.Font.Style;
                    }));
                    ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (token.IsCancellationRequested) return;
                    if (lines.Length == 0) return;

                    int lastIndex = Math.Min(firstVisible + visibleLineCount, lines.Length) - 1;
                    if (lastIndex < firstVisible) return;

                    // Compute max cell width per column using captured font
                    var maxWidths = new Dictionary<int, int>();
                    using var font = new Font(fontName!, fontSize, fontStyle);
                    for (int lineIdx = firstVisible; lineIdx <= lastIndex; lineIdx++)
                    {
                        if (token.IsCancellationRequested) return;
                        string line = lines[lineIdx];
                        string[] cells = line.Split('\t');
                        for (int col = 0; col < cells.Length - 1; col++)
                        {
                            string cell = cells[col];
                            int w = TabMeasurementCache.GetStringWidth(cell, font);
                            lock (maxWidths)
                            {
                                if (!maxWidths.ContainsKey(col) || w > maxWidths[col])
                                    maxWidths[col] = w;
                            }
                        }
                    }

                    if (token.IsCancellationRequested) return;

                    // Build tab stop positions (cumulative widths + padding)
                    var stops = new List<int>();
                    int cumulative = 0;
                    for (int i = 0; ; i++)
                    {
                        if (!maxWidths.TryGetValue(i, out int w)) break;
                        cumulative += w + 2; // 2px padding between columns
                        stops.Add(cumulative);
                    }

                    if (token.IsCancellationRequested) return;

                    // Apply to editor on UI thread (fire-and-forget)
                    try
                    {
                        textEditor.BeginInvoke(new Action(() =>
                        {
                            if (!textEditor.IsDisposed && !textEditor.Disposing && stops.Count > 0)
                            {
                                textEditor.SelectionTabs = stops.ToArray();
                            }
                        }));
                    }
                    catch { }
                }
                catch { /* ignore */ }
            }, token);
        }

        #endregion

        #region Theme & Toggles

        private void UpdateThemeColors(Theme theme)
        {
            _currentTheme = theme;
            _themeManager.SetTheme(theme.Name);
            
            this.BackColor = theme.Background;
            this.ForeColor = theme.Text;
            if (menuStrip != null)
            {
                menuStrip.Renderer = new ThemeAwareMenuRenderer(theme);
                menuStrip.BackColor = theme.MenuBackground;
                menuStrip.ForeColor = theme.Text;
            }
            if (textEditor != null)
            {
                textEditor.BackColor = theme.EditorBackground;
                textEditor.ForeColor = theme.Text;
                textEditor.HighlightColor = theme.IsLight ? Color.FromArgb(80, 230, 230, 230) : Color.FromArgb(80, 60, 60, 60);
            }
            if (_splitEditor != null)
            {
                _splitEditor.BackColor = theme.EditorBackground;
                _splitEditor.ForeColor = theme.Text;
            }
            if (gutterPanel != null)
            {
                gutterPanel.BackColor = theme.EditorBackground;
                gutterPanel.ForeColor = theme.Text;
            }
            if (tabControl != null)
            {
                tabControl.BackColor = theme.MenuBackground;
            }
            if (minimapControl != null)
            {
                minimapControl.BackColor = theme.EditorBackground;
                minimapControl.ViewportColor = theme.IsLight ? Color.FromArgb(80, Color.LightBlue) : Color.FromArgb(100, Color.DodgerBlue);
                minimapControl.ViewportBorderColor = Color.DodgerBlue;
                minimapControl.RefreshNow();
            }
            textEditor!.GuideColor = Color.FromArgb(120, 120, 120);
            if (_tabDropdownButton != null)
            {
                _tabDropdownButton.BackColor = theme.MenuBackground;
                _tabDropdownButton.ForeColor = theme.Muted;
                _tabDropdownButton.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
            }
            textEditor!.Invalidate();
            if (textEditor != null)
            {
                textEditor.HighlightColor = theme.IsLight ? Color.FromArgb(80, 230, 230, 230) : Color.FromArgb(80, 60, 60, 60);
            }
            if (statusStrip != null)
            {
                statusStrip.Renderer = new ThemeAwareMenuRenderer(theme);
                statusStrip.BackColor = theme.PanelBackground;
                statusStrip.ForeColor = theme.Text;
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.BackColor = theme.PanelBackground;
                    item.ForeColor = theme.Text;
                }
                // Ensure theme dropdown uses theme-aware renderer
                if (themeDropDown != null && themeDropDown.DropDown != null)
                {
                    themeDropDown.DropDown.Renderer = new ThemeAwareMenuRenderer(theme);
                    themeDropDown.BackColor = theme.PanelBackground;
                    themeDropDown.ForeColor = theme.Text;
                    foreach (ToolStripItem item in themeDropDown.DropDownItems)
                    {
                        item.BackColor = theme.MenuBackground;
                        item.ForeColor = theme.Text;
                    }
                }
                // Ensure tab size dropdown uses theme-aware renderer
                if (tabSizeDropDown != null && tabSizeDropDown.DropDown != null)
                {
                    tabSizeDropDown.DropDown.Renderer = new ThemeAwareMenuRenderer(theme);
                    tabSizeDropDown.BackColor = theme.PanelBackground;
                    tabSizeDropDown.ForeColor = theme.Text;
                    foreach (ToolStripItem item in tabSizeDropDown.DropDownItems)
                    {
                        item.BackColor = theme.MenuBackground;
                        item.ForeColor = theme.Text;
                    }
                }
            }

            if (textEditor != null && textEditor.IsHandleCreated)
            {
                ApplyScrollbarTheme();
            }

            ApplyTitleBarTheme();
            
            if (syntaxHighlightingEnabled && incrementalHighlighter != null)
            {
                _applyingHighlight = true;
                ResetVisibleRangeToBase(theme.Text);
                _applyingHighlight = false;
                var (first, last) = GetVisibleLineRange();
                for (int l = first; l <= last; l++)
                    incrementalHighlighter.MarkDirty(l);
            }
        }

        private void TextEditor_Resize(object? sender, EventArgs e)
        {
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            if (syntaxHighlightingEnabled)
            {
                RequestVisibleHighlight();
            }
            PositionMinimap();
        }

        private void ToggleTheme()
        {
            var names = ThemeManager.ThemeNames;
            int idx = Array.IndexOf(names, _currentTheme.Name);
            idx = (idx + 1) % names.Length;
            string next = names[idx];
            _currentTheme = ThemeManager.Themes[next];
            _themeManager.SetTheme(next);
            UpdateThemeColors(_currentTheme);
            UpdateTerminalTheme();
            UpdateThemeDropDown();
        }

        private void OnThemeChanged(Theme theme)
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(() => OnThemeChanged(theme)));
            else
            {
                _currentTheme = theme;
                UpdateThemeColors(_currentTheme);
                UpdateTerminalTheme();
                UpdateThemeDropDown();
            }
        }

        private void LoadSettings()
        {
            try
            {
                string path = SettingsFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        string themeName = settings.ThemeName;
                        if (string.IsNullOrEmpty(themeName) || !ThemeManager.Themes.ContainsKey(themeName))
                            themeName = "Dark";
                        _currentTheme = ThemeManager.Themes.TryGetValue(themeName, out var t) ? t : Theme.Dark;
                        wordWrapEnabled = settings.WordWrapEnabled;
                        gutterVisible = settings.GutterVisible;
                        statusBarVisible = settings.StatusBarVisible;
                        showGuide = settings.ShowGuide;
                        guideColumn = settings.GuideColumn;
                        tabSize = settings.TabSize;
                        fontName = settings.FontName;
                        fontSize = settings.FontSize;
                        insertSpaces = settings.InsertSpaces;
                        autoIndentEnabled = settings.AutoIndentEnabled;
                        smartTabsEnabled = settings.SmartTabsEnabled;
                        elasticTabsEnabled = settings.ElasticTabsEnabled;
                        currentLineHighlightMode = settings.CurrentLineHighlightMode;
                        syntaxHighlightingEnabled = settings.SyntaxHighlightingEnabled;
                        _pendingMinimapVisible = settings.MinimapVisible;
                        _terminalVisible = settings.TerminalVisible;
                        _terminalHeight = Math.Max(60, Math.Min(600, settings.TerminalHeight));
                        _terminalShell = settings.TerminalShellPath ?? "";
                    }
                }
            }
            catch { /* ignore settings load errors */ }
        }

        private void SaveSettings()
        {
            try
            {
                string path = SettingsFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                 var settings = new AppSettings(
                     ThemeName: _currentTheme.Name,
                     WordWrapEnabled: wordWrapEnabled,
                     GutterVisible: gutterVisible,
                     StatusBarVisible: statusBarVisible,
                     ShowGuide: showGuide,
                     GuideColumn: guideColumn,
                     TabSize: tabSize,
                     FontName: fontName,
                     FontSize: fontSize,
                     InsertSpaces: insertSpaces,
                     AutoIndentEnabled: autoIndentEnabled,
                     SmartTabsEnabled: smartTabsEnabled,
                     ElasticTabsEnabled: elasticTabsEnabled,
                     CurrentLineHighlightMode: currentLineHighlightMode,
                     SyntaxHighlightingEnabled: syntaxHighlightingEnabled,
                     MinimapVisible: minimapMenuItem?.Checked ?? false,
                     TerminalVisible: _terminalVisible,
                     TerminalHeight: _terminalHeight,
                     TerminalShellPath: _terminalShell
                );
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
             catch { /* ignore settings save errors */ }
         }

          internal void ToggleGutter()
        {
            gutterVisible = !gutterVisible;
            gutterMenuItem.Checked = gutterVisible;
            gutterPanel.Visible = gutterVisible;
            if (mainTable.ColumnCount > 0)
            {
                if (gutterVisible)
                {
                    gutterPanel.Visible = true;
                    gutterPanel.UpdateLineNumberWidth();
                    mainTable.PerformLayout();
                }
                else
                {
                    gutterPanel.Visible = false;
                }
            }
            gutterPanel.RefreshGutter();
            SaveSettings();
        }

        private void ToggleStatusBar()
        {
            statusBarVisible = !statusBarVisible;
            statusBarMenuItem.Checked = statusBarVisible;
            statusStrip.Visible = statusBarVisible;
            // Form will auto-layout: statusStrip dock=Bottom, mainTable dock=Fill
            SaveSettings();
        }

        private void ToggleVimMode(object? sender, EventArgs e)
        {
            vimModeEnabled = !vimModeEnabled;
            if (vimEngine != null)
            {
                if (vimModeEnabled)
                {
                    vimEngine.Enabled = true;
                    vimEngine.EnterMode(VimMode.Normal);
                }
                else
                {
                    vimEngine.Enabled = false;
                    // Return to normal insert mode when disabling vim
                    // (this is handled by the engine disabling)
                }
            }
            
            // Update status bar to show Vim mode indicator
            UpdateStatusBar();
            SaveSettings();
        }

        private void ToggleTerminal_Click(object? sender, EventArgs e)
        {
            ToggleTerminal();
        }

        private void ToggleTerminal()
        {
            _terminalVisible = !_terminalVisible;
            terminalMenuItem.Checked = _terminalVisible;
            if (_terminalVisible)
                ShowTerminal();
            else
                HideTerminal();
            SaveSettings();
        }

        private void ShowTerminal()
        {
            if (_terminalSplitContainer == null) return;
            _terminalVisible = true;
            terminalMenuItem.Checked = true;

            foreach (var t in _terminalTabs)
                t.Start();

            _terminalSplitContainer.Panel2Collapsed = false;

            int available = _terminalSplitContainer.Height - _terminalSplitContainer.SplitterWidth;
            if (_terminalHeight >= available)
                _terminalHeight = Math.Min(available / 2, 400);
            _terminalSplitContainer.SplitterDistance = available - _terminalHeight;

            _terminalSplitContainer.PerformLayout();
            ActiveTerminal?.FocusInput();
            PositionTerminalNewTabButton();
        }

        private void HideTerminal()
        {
            if (_terminalSplitContainer == null) return;
            _terminalVisible = false;
            terminalMenuItem.Checked = false;

            _terminalHeight = Math.Max(60,
                _terminalSplitContainer.Height - _terminalSplitContainer.SplitterDistance - _terminalSplitContainer.SplitterWidth);

            _terminalSplitContainer.Panel2Collapsed = true;
            _terminalSplitContainer.PerformLayout();
            textEditor?.Focus();
        }

        private void UpdateTerminalTheme()
        {
            foreach (var t in _terminalTabs)
                t.SetTheme(_currentTheme);
            var theme = _currentTheme;
            if (_terminalSplitContainer != null)
            {
                _terminalSplitContainer.BackColor = theme.MenuBackground;
                _terminalSplitContainer.Panel1.BackColor = theme.EditorBackground;
                _terminalSplitContainer.Panel2.BackColor = theme.MenuBackground;
            }
            if (_terminalTabControl != null)
            {
                _terminalTabControl.BackColor = theme.MenuBackground;
                if (_terminalTabControl.IsHandleCreated)
                    SendMessage(_terminalTabControl.Handle, TCM_SETBKCOLOR, IntPtr.Zero,
                        (IntPtr)ColorTranslator.ToWin32(theme.MenuBackground));
            }
            if (_terminalNewTabButton != null)
            {
                _terminalNewTabButton.BackColor = theme.MenuBackground;
                _terminalNewTabButton.ForeColor = theme.Text;
            }
        }

        private TerminalPanel AddTerminalTab(string? shellPath)
        {
            var terminal = new TerminalPanel(shellPath);
            terminal.SetTheme(_currentTheme);

            int tabNumber = _terminalTabs.Count + 1;
            var page = new TabPage($"Terminal {tabNumber}")
            {
                BackColor = _currentTheme.EditorBackground,
                ToolTipText = shellPath ?? "Default shell"
            };
            terminal.Dock = DockStyle.Fill;
            terminal.HideTerminalRequested += () => CloseTerminalTab(terminal);
            page.Controls.Add(terminal);

            _terminalTabControl?.TabPages.Insert(
                _terminalTabControl.TabPages.Count, page);
            _terminalTabs.Add(terminal);
            if (_terminalTabControl != null)
                _terminalTabControl.SelectedTab = page;

            if (_terminalVisible)
                terminal.Start();

            PositionTerminalNewTabButton();
            return terminal;
        }

        private void CloseTerminalTab(TerminalPanel terminal)
        {
            int index = _terminalTabs.IndexOf(terminal);
            if (index < 0) return;

            terminal.Kill();
            _terminalTabControl?.TabPages.RemoveAt(index);
            _terminalTabs.RemoveAt(index);
            terminal.Dispose();

            if (_terminalTabs.Count == 0)
            {
                AddTerminalTab(string.IsNullOrEmpty(_terminalShell) ? null : _terminalShell);
            }
            PositionTerminalNewTabButton();
        }

        private void TerminalTabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender!;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            var page = tc.TabPages[e.Index];
            var bounds = tc.GetTabRect(e.Index);
            bounds.Height = tc.ItemSize.Height;

            var theme = _currentTheme;
            bool isSelected = (e.Index == tc.SelectedIndex);

            Color backColor = isSelected
                ? theme.EditorBackground
                : theme.MenuBackground;

            using var bgBrush = new SolidBrush(backColor);
            e.Graphics.FillRectangle(bgBrush, bounds);

            TextRenderer.DrawText(e.Graphics, page.Text, e.Font ?? tc.Font,
                new Rectangle(bounds.X + 4, bounds.Y + 3, bounds.Width - 22, bounds.Height - 4),
                isSelected ? theme.Text : theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var closeRect = new Rectangle(bounds.Right - 17, bounds.Y + 5, 14, 14);
            TextRenderer.DrawText(e.Graphics, "\u00D7", e.Font ?? tc.Font,
                closeRect, theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void TerminalTabControl_MouseDown(object? sender, MouseEventArgs e)
        {
            var tc = (TabControl)sender!;
            for (int i = 0; i < tc.TabPages.Count; i++)
            {
                var bounds = tc.GetTabRect(i);
                var closeRect = new Rectangle(bounds.Right - 17, bounds.Y + 5, 14, 14);
                if (closeRect.Contains(e.Location) && i < _terminalTabs.Count)
                {
                    CloseTerminalTab(_terminalTabs[i]);
                    return;
                }
            }
        }

        private void TerminalTabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var active = ActiveTerminal;
            if (active != null && _terminalSplitContainer is { Panel2Collapsed: false })
            {
                active.Start();
                active.FocusInput();
            }
            PositionTerminalNewTabButton();
        }

        private void PositionTerminalNewTabButton()
        {
            if (_terminalNewTabButton == null || _terminalTabControl == null) return;
            if (_terminalTabControl.Parent == null) return;

            var strip = _terminalTabControl;
            int x = strip.Left + strip.Width - _terminalNewTabButton.Width - 2;
            int y = strip.Top + 2;
            if (x < strip.Left) x = strip.Left;
            _terminalNewTabButton.Location = new Point(x, y);
            _terminalNewTabButton.BringToFront();
            _terminalNewTabButton.Visible = true;
        }

        private void MinimapMenuItem_Click(object? sender, EventArgs e)
        {
            _pendingMinimapVisible = minimapMenuItem.Checked;
            PositionMinimap();
            SaveSettings();
        }

        internal void ToggleSyntaxHighlighting()
        {
            syntaxHighlightingEnabled = !syntaxHighlightingEnabled;
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            if (syntaxHighlightingEnabled)
            {
                CreateIncrementalHighlighter();
            }
            else
            {
                incrementalHighlighter?.Dispose();
                incrementalHighlighter = null;
                var baseColor = _currentTheme.Text;
                ResetVisibleRangeToBase(baseColor);
            }
            SaveSettings();
        }

        // Tab handling
        private void InsertSpaces_Click(object? sender, EventArgs e) => ToggleInsertSpaces();
        internal void SetTabSize(int size)
        {
            tabSize = size;
            UpdateTabSizeMenu();
            UpdateTabStops();
            UpdateStatusBar();
            UpdateTabSizeDropdown();
            SaveSettings();
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

        private void UpdateTabSizeDropdown()
        {
            if (tabSizeDropDown == null) return;
            tabSizeDropDown.Text = $"Tab: {tabSize}";
            var items = tabSizeDropDown.DropDownItems;
            if (items.Count >= 6)
            {
                ((ToolStripMenuItem)items[0]).Checked = (tabSize == 2);
                ((ToolStripMenuItem)items[1]).Checked = (tabSize == 4);
                ((ToolStripMenuItem)items[2]).Checked = (tabSize == 6);
                ((ToolStripMenuItem)items[3]).Checked = (tabSize == 8);
                ((ToolStripMenuItem)items[4]).Checked = (tabSize == 10);
                ((ToolStripMenuItem)items[5]).Checked = (tabSize == 12);
            }
        }
        private void ToggleInsertSpaces()
        {
            insertSpaces = !insertSpaces;
            insertSpacesMenuItem.Checked = insertSpaces;
            SaveSettings();
        }

        internal void ToggleAutoIndent()
        {
            autoIndentEnabled = !autoIndentEnabled;
            autoIndentMenuItem.Checked = autoIndentEnabled;
            SaveSettings();
        }

        internal void ToggleSmartTabs()
        {
            smartTabsEnabled = !smartTabsEnabled;
            smartTabsMenuItem.Checked = smartTabsEnabled;
            SaveSettings();
        }

        private void AutoIndent_Click(object? sender, EventArgs e)
        {
            autoIndentEnabled = autoIndentMenuItem.Checked;
            SaveSettings();
        }

        private void SmartTabs_Click(object? sender, EventArgs e)
        {
            smartTabsEnabled = smartTabsMenuItem.Checked;
            SaveSettings();
        }

        private void ElasticTabs_Click(object? sender, EventArgs e)
        {
            elasticTabsEnabled = elasticTabsMenuItem.Checked;
            UpdateTabStops();
            SaveSettings();
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

            // Sync editor's highlight mode (new HighlightRichTextBox handles drawing)
            if (textEditor != null)
            {
                textEditor.CurrentLineHighlightMode = currentLineHighlightMode;
            }

            if (currentLineHighlightMode == CurrentLineHighlightMode.Off)
            {
                lastHighlightedLine = -1;
                gutterPanel?.RefreshGutter();
            }
            else
            {
                lastHighlightedLine = -1;
                HighlightCurrentLine();
            }
            SaveSettings();
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

        // Tab key handling
        private void TextEditor_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                HandleTab(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                HandleEnter(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HandleTab(KeyEventArgs e)
        {
            if (textEditor == null) return;
            
            BeginUndoUnit(textEditor);
            try
            {
                if (e.Shift)
                {
                    UnindentSelection();
                }
                else if (textEditor.SelectionLength > 0)
                {
                    IndentSelection();
                }
                else
                {
                     if (smartTabsEnabled)
                     {
                         // Check if caret is at line start or only whitespace before it
                         int lineIdx = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
                         int lineStartIdx = textEditor.GetFirstCharIndexFromLine(lineIdx);
                         int charsOnLineBeforeCaret = textEditor.SelectionStart - lineStartIdx;
                         string lineText = GetLineText(lineIdx);
                         if (charsOnLineBeforeCaret > lineText.Length)
                             charsOnLineBeforeCaret = lineText.Length;
                         string textBeforeCaret = lineText.Substring(0, charsOnLineBeforeCaret);
                        
                        if (string.IsNullOrEmpty(textBeforeCaret) || textBeforeCaret.All(char.IsWhiteSpace))
                        {
                            // Smart indent: move to next tab stop
                            int currentCol = 0;
                            foreach (char c in textBeforeCaret)
                            {
                                currentCol += (c == '\t') ? tabSize : 1;
                            }
                            int targetCol = ((currentCol / tabSize) + 1) * tabSize;
                            int needed = targetCol - currentCol;
                            string indent = IndentationHelper.ComputeMixedIndent(needed, tabSize, insertSpaces);
                            textEditor.SelectedText = indent;
                            UpdateStatusBar();
                            return;
                        }
                    }
                    
                    // Normal tab insertion (fallback)
                    if (insertSpaces)
                    {
                        textEditor.SelectedText = new string(' ', tabSize);
                    }
                    else
                    {
                        textEditor.SelectedText = "\t";
                    }
                }
            }
            finally
            {
                EndUndoUnit(textEditor);
            }
            
            UpdateStatusBar();
        }

        private void HandleEnter(KeyEventArgs e)
        {
            if (textEditor == null) return;
            
            BeginUndoUnit(textEditor);
            try
            {
                if (autoIndentEnabled)
                {
                    int selStart = textEditor.SelectionStart;
                    int currentLine = textEditor.GetLineFromCharIndex(selStart);
                    string prevLineText = "";
                    if (currentLine > 0)
                    {
                        prevLineText = GetLineText(currentLine - 1);
                    }
                    // Compute indent based on previous line
                    string indent = IndentationHelper.ComputeIndent(prevLineText, tabSize, insertSpaces);
                    // Insert newline + indent
                    string newText = Environment.NewLine + indent;
                    textEditor.SelectedText = newText;
                }
                else
                {
                    // Simple newline
                    textEditor.SelectedText = Environment.NewLine;
                }
            }
            finally
            {
                EndUndoUnit(textEditor);
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
            
            BeginUndoUnit(textEditor);
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                for (int line = endLine; line >= startLine; line--)
                {
                    if (line < 0 || line >= LineCount) continue;
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
                EndUndoUnit(textEditor);
            }
        }

        private void UnindentSelection()
        {
            if (textEditor == null) return;
            int start = textEditor.SelectionStart;
            int end = textEditor.SelectionStart + textEditor.SelectionLength;
            int startLine = textEditor.GetLineFromCharIndex(start);
            int endLine = textEditor.GetLineFromCharIndex(end);
            
            BeginUndoUnit(textEditor);
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                for (int line = endLine; line >= startLine; line--)
                {
                    if (line < 0 || line >= LineCount) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
                    if (lineStart < 0) continue;

                    string lineText = GetLineText(line);
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
                EndUndoUnit(textEditor);
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

        internal void SetGuideColumn(int column)
        {
            guideColumn = column;
            showGuide = true;
            columnGuideMenuItem.Checked = true;
            textEditor.ShowGuide = true;
            textEditor.GuideColumn = column;
            UpdateColumnGuideMenuChecked();
            SaveSettings();
        }

        private void ToggleColumnGuide(object? sender, EventArgs e)
        {
            showGuide = columnGuideMenuItem.Checked;
            textEditor.ShowGuide = showGuide;
            textEditor.Invalidate();
            SaveSettings();
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

        #endregion

        #region File Operations

        internal void NewFile()
        {
            NewFile(false);
        }

        private void OpenFile()
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "All Files|*.*";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                OpenFileInNewTab(ofd.FileName);
            }
        }

         internal void LoadFile(string path)
         {
              try
              {
                   // Suspend background workers during file load
                   elasticTabTimer?.Stop();

                 string content = File.ReadAllText(path);
                 
                 // Update fields without triggering dirty
                 textEditor.TextChanged -= TextEditor_TextChanged;
                 textEditor.Text = content;
                 textEditor.TextChanged += TextEditor_TextChanged;

                 currentFilePath = path;
                 currentSyntax = SyntaxDefinition.GetDefinitionForFile(path);
                 if (File.Exists(path))
                     lastFileWriteTime = File.GetLastWriteTimeUtc(path);
                 
                 ClearDirtyAfterSave();
                 AddToRecentFiles(path);
                 
                 // Ensure editor starts at top: set caret at 0
                 textEditor.SelectionStart = 0;
                 
                 // Defer scrolling and highlighting until control is fully laid out
                 if (textEditor.IsHandleCreated)
                 {
                     try
                     {
                         SendMessage(textEditor.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                     }
                     catch { }
                 }
                 
                 UpdateStatusBar();
                 
                 // Recreate syntax highlighter for new file type
                 CreateIncrementalHighlighter();
                 
                 // Defer syntax highlight request until UI is idle
                 if (textEditor.IsHandleCreated)
                 {
                     try
                     {
                         this.BeginInvoke(new Action(() =>
                         {
                             try { RequestVisibleHighlight(); } catch { }
                         }));
                     }
                     catch { }
                 }

                // Resume column guide updates after load completes
                   elasticTabTimer?.Start();
            }
             catch (Exception ex)
              {
                  MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }
         }

        internal void SaveFile()
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
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    modifiedLines.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal void SaveAsFile()
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
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    AddToRecentFiles(currentFilePath);
                    modifiedLines.Clear();
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

        internal bool IsModified()
        {
            // For now, compare with stored state; could also compare with disk
            return isModified;
        }

        private string ComputeContentHash()
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(textEditor.Text);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        internal void SetDirty()
        {
            if (isModified) return;
            isModified = true;
            if (activeDocIndex >= 0)
            {
                ActiveDoc.IsDirty = true;
            }
            UpdateWindowTitle();
            UpdateActiveTabTitle();
        }

        internal void ClearDirtyAfterSave()
        {
            isModified = false;
            savedContentHash = ComputeContentHash();
            modifiedLines.Clear();
            if (activeDocIndex >= 0)
            {
                ActiveDoc.IsDirty = false;
                ActiveDoc.SavedHash = savedContentHash;
                ActiveDoc.ModifiedLines = modifiedLines;
                ActiveDoc.LastWriteTime = lastFileWriteTime;
                ActiveDoc.FilePath = currentFilePath;
                ActiveDoc.Content = textEditor.Text;
                ActiveDoc.Syntax = currentSyntax;
            }
            UpdateWindowTitle();
            UpdateActiveTabTitle();
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = currentFilePath != null 
                ? $"Personal Flip Pad - {Path.GetFileName(currentFilePath)}"
                : "Personal Flip Pad - Untitled";
            this.Text = isModified ? "*" + baseTitle : baseTitle;
        }

        private DialogResult PromptSaveChanges()
        {
            // Only prompt if there are actual unsaved changes
            if (!isModified && (textEditor == null || string.IsNullOrEmpty(textEditor.Text) || savedContentHash == null))
                return DialogResult.No;

            var result = MessageBox.Show(
                "This file has unsaved changes. Save before proceeding?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                SaveFile();
                if (IsModified()) // Save failed
                    return DialogResult.Cancel;
            }

            return result;
        }

        internal void CheckIfClean()
        {
            if (!isModified || savedContentHash == null) return;

            string currentHash = ComputeContentHash();
            if (currentHash == savedContentHash)
            {
                isModified = false;
                modifiedLines.Clear();
                UpdateWindowTitle();
            }
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
                    var item = new ToolStripMenuItem(display, null, (s, e) => OpenFileInNewTab(filePath));
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
        private void CloseTab_Click(object? sender, EventArgs e) => CloseCurrentTab();
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
            if (textEditor.CanUndo) 
            {
                textEditor.Undo();
                CheckIfClean(); // May clear dirty if undo restored to saved state
            }
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
                fontName = fd.Font.Name;
                fontSize = fd.Font.Size;
                // Trigger gutter refresh to recalc line numbers
                if (gutterPanel != null) gutterPanel.RefreshGutter();
                SaveSettings();
                
                // Recompute tab stops with new font metrics
                UpdateTabStops();
            }
        }

        private void About_Click(object? sender, EventArgs e)
        {
            using var dlg = new AboutDialog();
            dlg.ShowDialog(this);
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
                _foldingManager.ScanRegions();
            }
            gutterPanel?.RefreshGutter();
        }

        internal void ToggleBookmark(int line)
        {
            if (bookmarks.Contains(line))
                bookmarks.Remove(line);
            else
                bookmarks.Add(line);
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
            if (gutterPanel != null)
                gutterPanel.UpdateLineNumberWidth();
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

        private void ApplyThemeByName(string name)
        {
            if (!ThemeManager.Themes.TryGetValue(name, out var theme)) return;
            _currentTheme = theme;
            _themeManager.SetTheme(name);
            UpdateThemeColors(_currentTheme);
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

        private void UpdateThemeDropDown()
        {
            if (themeDropDown != null)
            {
                themeDropDown.Text = _currentTheme.Name;
                // Rebuild theme items from the registry
                themeDropDown.DropDownItems.Clear();
                foreach (var name in ThemeManager.ThemeNames)
                {
                    var item = new ToolStripMenuItem(name);
                    item.Click += (s, e) => ApplyThemeByName(name);
                    if (name == _currentTheme.Name)
                        item.Text = "\u25CF " + name;
                    themeDropDown.DropDownItems.Add(item);
                }
            }
            // Update tab size dropdown checkmarks
            if (tabSizeDropDown != null && tabSizeDropDown.DropDownItems.Count >= 3)
            {
                tabSizeDropDown.DropDownItems[0].Text = tabSize == 2 ? "● 2" : "2";
                tabSizeDropDown.DropDownItems[1].Text = tabSize == 4 ? "● 4" : "4";
                tabSizeDropDown.DropDownItems[2].Text = tabSize == 8 ? "● 8" : "8";
            }
        }

        #endregion

        #region Document Management (Tabs)

        // Get first visible line in editor
        private int GetFirstVisibleLine()
        {
            if (!textEditor.IsHandleCreated) return 0;
            return (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        }

        // Compute hash of string content
        private string ComputeContentHash(string content)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        // Save current editor state into the active document
        private void SaveCurrentDocument()
        {
            if (activeDocIndex < 0) return;
            var doc = documents[activeDocIndex];
            doc.Content = textEditor.Text;
            doc.IsDirty = isModified;
            // modifiedLines, bookmarks, collapsedRegions are already references to doc's collections
            doc.FilePath = currentFilePath;
            doc.SavedHash = savedContentHash;
            doc.LastWriteTime = lastFileWriteTime;
            doc.Syntax = currentSyntax;
            doc.SelectionStart = textEditor.SelectionStart;
            doc.SelectionLength = textEditor.SelectionLength;
            doc.FirstVisibleLine = GetFirstVisibleLine();
        }

        // Create a new empty document
        private Document CreateNewDocument()
        {
            return new Document
            {
                FilePath = null,
                Content = "",
                IsDirty = false,
                ModifiedLines = new HashSet<int>(),
                Bookmarks = new HashSet<int>(),
                CollapsedRegions = new HashSet<int>(),
                SavedHash = null,
                LastWriteTime = null,
                SelectionStart = 0,
                SelectionLength = 0,
                FirstVisibleLine = 0,
                Syntax = null,
                UntitledNumber = nextUntitledNumber++
            };
        }

        // New file (untitled) - creates a new tab
        private void NewFile(bool isInitial = false)
        {
            if (!isInitial)
            {
                var result = PromptSaveChanges();
                if (result == DialogResult.Cancel) return;
            }

            // Save state of current document before switching
            if (activeDocIndex >= 0)
            {
                SaveCurrentDocument();
            }

            var newDoc = CreateNewDocument();
            documents.Add(newDoc);
            int newIndex = documents.Count - 1;

            // Create tab page and add to end
            var tabPage = new TabPage(newDoc.DisplayName) { Tag = newDoc };
            tabControl.TabPages.Add(tabPage);

            // Switch to new tab and ensure visible
            tabControl.SelectedIndex = newIndex;
            EnsureSelectedTabVisible();
        }

        // Open an existing file in a new tab
        internal void OpenFileInNewTab(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                string content = File.ReadAllText(path);
                var syntax = SyntaxDefinition.GetDefinitionForFile(path);
                var doc = new Document
                {
                    FilePath = path,
                    Content = content,
                    IsDirty = false,
                    ModifiedLines = new HashSet<int>(),
                    Bookmarks = new HashSet<int>(),
                    CollapsedRegions = new HashSet<int>(),
                    SavedHash = ComputeContentHash(content),
                    LastWriteTime = File.GetLastWriteTimeUtc(path),
                    SelectionStart = 0,
                    SelectionLength = 0,
                    FirstVisibleLine = 0,
                    Syntax = syntax
                };
                documents.Add(doc);
                int newIndex = documents.Count - 1;
                var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                tabControl.TabPages.Add(tabPage);
                tabControl.SelectedIndex = newIndex; // triggers SwitchToTab
                EnsureSelectedTabVisible();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Close current tab
        internal void CloseCurrentTab()
        {
            if (documents.Count <= 1) return; // always keep at least one tab
            if (activeDocIndex < 0 || activeDocIndex >= documents.Count) return;

            var result = PromptSaveChanges();
            if (result == DialogResult.Cancel) return;

            // Remove current document and its tab
            int closeIndex = activeDocIndex;
            if (closeIndex >= documents.Count || closeIndex >= tabControl.TabPages.Count) return;

            // Force activeDocIndex to -1 so that SelectedIndexChanged → SwitchToTab does not bail
            activeDocIndex = -1;

            documents.RemoveAt(closeIndex);
            tabControl.TabPages.RemoveAt(closeIndex);

            // Select another tab
            int newIndex = closeIndex < documents.Count ? closeIndex : documents.Count - 1;
            if (newIndex >= 0)
                SwitchToTab(newIndex);
        }

        // Switch to document at given index (0-based)
        internal void SwitchToTab(int index)
        {
            if (index < 0 || index >= documents.Count) return;
            if (activeDocIndex == index) return; // already active

            // Save current document state before leaving
            if (activeDocIndex >= 0)
            {
                SaveCurrentDocument();
            }

            activeDocIndex = index;
            var doc = documents[activeDocIndex];
            LoadDocument(doc);
            UpdateWindowTitle();
        }

        // Load document state into editor and UI
        private void LoadDocument(Document doc)
        {
            // Update core fields from document
            currentFilePath = doc.FilePath;
            isModified = doc.IsDirty;
            modifiedLines = doc.ModifiedLines;
            bookmarks = doc.Bookmarks;
            collapsedRegions = doc.CollapsedRegions;
            savedContentHash = doc.SavedHash;
            lastFileWriteTime = doc.LastWriteTime ?? DateTime.MinValue;
            currentSyntax = doc.Syntax;

            // Load text without triggering dirty flag
            textEditor.TextChanged -= TextEditor_TextChanged;
            textEditor.Text = doc.Content ?? "";
            textEditor.SelectionStart = doc.SelectionStart;
            textEditor.SelectionLength = doc.SelectionLength;
            textEditor.TextChanged += TextEditor_TextChanged;

            // Restore scroll position
            if (doc.FirstVisibleLine > 0)
            {
                SendMessage(textEditor.Handle, EM_LINESCROLL, (IntPtr)doc.FirstVisibleLine, IntPtr.Zero);
            }
            else
            {
                SendMessage(textEditor.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
            }

            // Recreate syntax highlighter based on current syntax
            CreateIncrementalHighlighter();

            // Update UI (must be after CreateIncrementalHighlighter so file type is current)
            UpdateStatusBar();
            UpdateTabTitle(activeDocIndex);
            UpdateThemeColors(_currentTheme);
        }

        // Update tab title for document at index
        private void UpdateTabTitle(int docIndex)
        {
            if (docIndex < 0 || docIndex >= documents.Count) return;
            var doc = documents[docIndex];
            // The corresponding TabPage is at same index in tabControl.TabPages (excluding + page)
            if (docIndex < tabControl.TabPages.Count)
            {
                var tabPage = tabControl.TabPages[docIndex];
                tabPage.Text = doc.IsDirty ? "*" + doc.DisplayName : doc.DisplayName;
            }
        }

        // Convenience for active tab
        private void UpdateActiveTabTitle()
        {
            UpdateTabTitle(activeDocIndex);
        }

        #endregion

        #region Tab Control Event Handlers

        private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < documents.Count)
            {
                SwitchToTab(tabControl.SelectedIndex);
            }
        }

        private void TabControl_DoubleClick(object? sender, EventArgs e)
        {
            // Only create new tab if not over an existing tab and no drag in progress
            if (_draggedTabIndex != null) return;
            var mousePos = tabControl.PointToClient(Control.MousePosition);
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                if (tabControl.GetTabRect(i).Contains(mousePos))
                    return; // clicked on a tab, not empty area
            }
            // Create new tab on double-click anywhere else on tab bar
            NewFile();
        }

        private void TabControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                CloseTabAtLocation(e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    var rect = tabControl.GetTabRect(i);
                    if (rect.Contains(e.Location))
                    {
                        // Check close button (×) — always visible, same position as draw
                        int btnSize = 14;
                        int btnX = rect.Right - 17;
                        int btnY = rect.Top + 5;
                        var btnRect = new Rectangle(btnX, btnY, btnSize, btnSize);
                        if (btnRect.Contains(e.Location))
                        {
                            tabControl.SelectedIndex = i;
                            CloseCurrentTab();
                            return;
                        }
                        // Otherwise start potential drag
                        _draggedTabIndex = i;
                        _dragStartPoint = tabControl.PointToScreen(e.Location);
                        _isDragging = false;
                        break;
                    }
                }
            }
        }

        private void TabControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // End drag; if not detached, cancel
                _draggedTabIndex = null;
                _isDragging = false;
            }
        }

        private void TabControl_MouseLeave(object? sender, EventArgs e)
        {
            if (hoveredTabIndex.HasValue || _closeButtonHovered)
            {
                hoveredTabIndex = null;
                closeButtonBounds = null;
                _closeButtonHovered = false;
                tabControl.Invalidate();
            }
        }

        private void TabControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (tabControl.TabCount <= 1) return;
            int dir = e.Delta > 0 ? -1 : 1;
            int next = tabControl.SelectedIndex + dir;
            if (next >= 0 && next < tabControl.TabCount)
            {
                tabControl.SelectedIndex = next;
                var tabRect = tabControl.GetTabRect(next);
                tabControl.Invalidate(tabRect);
            }
        }

        private void PositionTabDropdownButton()
        {
            if (_tabDropdownButton == null || tabControl == null || tabControl.IsDisposed) return;
            Point tabScreen = tabControl.PointToScreen(Point.Empty);
            Point formClient = this.PointToClient(tabScreen);
            int x = formClient.X + tabControl.Width - _tabDropdownButton.Width - 2;
            int y = formClient.Y + (tabControl.Height - _tabDropdownButton.Height) / 2;
            _tabDropdownButton.Location = new Point(Math.Max(0, x), Math.Max(0, y));
            _tabDropdownButton.Visible = tabControl.TabCount > 0;
        }

        private void TabDropdownButton_Click(object? sender, EventArgs e)
        {
            if (tabControl == null || tabControl.TabCount == 0 || _tabDropdownButton == null) return;
            var theme = _currentTheme;
            var cm = new ContextMenuStrip();
            cm.Font = new Font("Segoe UI", 10);
            cm.BackColor = theme.MenuBackground;
            cm.ForeColor = theme.Text;
            cm.Renderer = new ThemeAwareMenuRenderer(theme);
            for (int i = 0; i < tabControl.TabCount; i++)
            {
                int idx = i;
                var page = tabControl.TabPages[i];
                var item = cm.Items.Add(page.Text, null, (s, args) =>
                {
                    if (tabControl != null && idx < tabControl.TabCount && !tabControl.IsDisposed)
                    {
                        tabControl.SelectedIndex = idx;
                        EnsureSelectedTabVisible();
                    }
                });
                if (i == tabControl.SelectedIndex)
                    item.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            }
            cm.Show(_tabDropdownButton, new Point(_tabDropdownButton.Width / 2, _tabDropdownButton.Height / 2));
        }

        private void EnsureSelectedTabVisible()
        {
            if (tabControl == null || tabControl.IsDisposed) return;
            SuspendLayout();
            tabControl.SuspendLayout();
            tabControl.Multiline = false;
            tabControl.Multiline = true;
            tabControl.ResumeLayout();
            ResumeLayout();
            tabControl.Invalidate();
        }
        private void CloseTabAtLocation(Point location)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var rect = tabControl.GetTabRect(i);
                if (rect.Contains(location))
                {
                    tabControl.SelectedIndex = i;
                    CloseCurrentTab();
                    break;
                }
            }
        }

        private void DetachTabToNewWindow(int tabIndex, Point dragScreenPos)
        {
            if (tabIndex < 0 || tabIndex >= documents.Count) return;

            try
            {
                var doc = documents[tabIndex];

                // Create a new Form1 instance for the detached document (skip default untitled doc)
                var detachedForm = new Form1(skipInitialDocument: true);
                detachedForm.tabControl.TabPages.Clear();
                detachedForm.activeDocIndex = -1;

                // Add the dragged document to the new form
                detachedForm.documents.Add(doc);
                var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                detachedForm.tabControl.TabPages.Add(tabPage);
                detachedForm.tabControl.SelectedIndex = 0;
                detachedForm.activeDocIndex = 0;
                detachedForm.LoadDocument(doc);
                detachedForm.UpdateWindowTitle();
                detachedForm.UpdateTabTitle(0);
                detachedForm.UpdateTabSizeDropdown();

                // Position near the drag release point
                detachedForm.StartPosition = FormStartPosition.Manual;
                detachedForm.Location = new Point(dragScreenPos.X - 100, dragScreenPos.Y - 50);

                // Remove the document from the original window
                if (tabIndex < documents.Count && tabIndex < tabControl.TabPages.Count)
                {
                    documents.RemoveAt(tabIndex);
                    tabControl.TabPages.RemoveAt(tabIndex);
                }

                // Adjust active index in original window
                if (activeDocIndex >= documents.Count)
                    activeDocIndex = documents.Count - 1;
                if (activeDocIndex >= 0)
                    SwitchToTab(activeDocIndex);
                else if (documents.Count == 0)
                    NewFile(isInitial: true);

                // Show the detached window
                detachedForm.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetachTabToNewWindow error: {ex.Message}");
            }
        }

        private enum DragZone { None, Left, Right, Top, Bottom, Outside }

        private DragZone GetDragZone(Point screenPos)
        {
            var bounds = this.Bounds;
            const double edgeRatio = 0.25;
            int edgeW = (int)(bounds.Width * edgeRatio);
            int edgeH = (int)(bounds.Height * edgeRatio);

            if (!bounds.Contains(screenPos))
                return DragZone.Outside;

            int localX = screenPos.X - bounds.X;
            int localY = screenPos.Y - bounds.Y;

            if (localX < edgeW) return DragZone.Left;
            if (localX > bounds.Width - edgeW) return DragZone.Right;
            if (localY < edgeH) return DragZone.Top;
            if (localY > bounds.Height - edgeH) return DragZone.Bottom;

            return DragZone.None;
        }

        private void SplitTabToPane(int tabIndex, DragZone zone)
        {
            if (tabIndex < 0 || tabIndex >= documents.Count) return;
            if (zone is DragZone.None or DragZone.Outside) return;

            try
            {
                var doc = documents[tabIndex];

                // If a split is already active, close it first
                CloseSplit();

                // Store document reference for close/restore
                _splitDocument = doc;
                _splitDocumentTitle = doc.DisplayName;

                // Create the SplitContainer and swap mainTable into Panel1
                splitContainer = new SplitContainer();
                splitContainer.Dock = DockStyle.Fill;
                splitContainer.SplitterWidth = 4;
                splitContainer.Panel1MinSize = 50;
                splitContainer.Panel2MinSize = 100;
                splitContainer.TabStop = false;

                if (zone is DragZone.Left or DragZone.Right)
                    splitContainer.Orientation = Orientation.Vertical;
                else
                    splitContainer.Orientation = Orientation.Horizontal;

                // Move mainTable out of mainLayout into Panel1
                mainLayout.Controls.Remove(mainTable);
                splitContainer.Panel1.Controls.Add(mainTable);

                // Create a simple RichTextBox for the split pane in Panel2
                var theme = _currentTheme;
                _splitEditor = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = RichTextBoxScrollBars.Both,
                    AcceptsTab = true,
                    Font = textEditor.Font,
                    BorderStyle = BorderStyle.None,
                    BackColor = theme.EditorBackground,
                    ForeColor = theme.Text,
                    Text = doc.Content ?? ""
                };
                _splitEditor.HandleCreated += (s, e) => ApplyScrollbarTheme();
                splitContainer.Panel2.Controls.Add(_splitEditor);

                // Insert splitContainer into mainLayout where mainTable was
                mainLayout.Controls.Add(splitContainer, 0, 2);

                // Set splitter distance to split evenly (defer to layout)
                if (splitContainer.Width > 100)
                    splitContainer.SplitterDistance = splitContainer.Width / 2;

                // Remove the tab page from the tab control (document stays in memory)
                if (tabIndex >= 0 && tabIndex < tabControl.TabPages.Count)
                    tabControl.TabPages.RemoveAt(tabIndex);
                if (tabIndex >= 0 && tabIndex < documents.Count)
                    documents.RemoveAt(tabIndex);

                // Adjust active index
                if (activeDocIndex >= documents.Count)
                    activeDocIndex = documents.Count - 1;
                if (activeDocIndex >= 0)
                    SwitchToTab(activeDocIndex);
                else if (documents.Count == 0)
                    NewFile(isInitial: true);

                // Reset drag state
                _draggedTabIndex = null;
                _isDragging = false;
                _dragStartPoint = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SplitTabToPane error: {ex.Message}");
            }
        }

        private void CloseSplit()
        {
            if (splitContainer == null) return;

            // Save content back to document
            if (_splitEditor != null && _splitDocument != null)
            {
                _splitDocument.Content = _splitEditor.Text;
                _splitDocument.ModifiedLines = new HashSet<int>();
                _splitDocument.IsDirty = false;
            }

            // Move mainTable back to mainLayout, destroy splitContainer
            if (splitContainer.Panel1.Controls.Contains(mainTable))
                splitContainer.Panel1.Controls.Remove(mainTable);
            mainLayout.Controls.Remove(splitContainer);
            mainLayout.Controls.Add(mainTable, 0, 2);

            // Clean up split editor
            if (_splitEditor != null)
            {
                splitContainer.Panel2.Controls.Remove(_splitEditor);
                _splitEditor.Dispose();
                _splitEditor = null;
            }

            splitContainer.Dispose();
            splitContainer = null;

            // Re-add the document and tab page to the main window
            if (_splitDocument != null)
            {
                documents.Add(_splitDocument);
                int newIndex = documents.Count - 1;
                var tabPage = new TabPage(_splitDocumentTitle ?? _splitDocument.DisplayName) { Tag = _splitDocument };
                tabControl.TabPages.Add(tabPage);
                tabControl.SelectedIndex = newIndex;
            }

            _splitDocument = null;
            _splitDocumentTitle = null;
        }

        private void TabControl_MouseMove(object? sender, MouseEventArgs e)
        {
            int newHoverIndex = -1;
            Rectangle? newCloseBounds = null;
            bool overAnyTab = false;

            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var rect = tabControl.GetTabRect(i);
                if (rect.Contains(e.Location))
                {
                    overAnyTab = true;
                    newHoverIndex = i;
                    int buttonSize = 14;
                    int btnX = rect.Right - 17;
                    int btnY = rect.Top + 5;
                    newCloseBounds = new Rectangle(btnX, btnY, buttonSize, buttonSize);
                    break;
            }
            }

            int oldHover = hoveredTabIndex ?? -1;
            bool oldCloseHovered = closeButtonBounds.HasValue && closeButtonBounds.Value.Contains(e.Location);
            bool newCloseHovered = newCloseBounds.HasValue && newCloseBounds.Value.Contains(e.Location);

            bool needsRedraw = (newHoverIndex != oldHover) || (newCloseHovered != oldCloseHovered);

            hoveredTabIndex = overAnyTab ? newHoverIndex : null;
            closeButtonBounds = newCloseBounds;
            _closeButtonHovered = newCloseHovered;

            // Always show hand cursor on the tab strip — whole strip is interactive
            tabControl.Cursor = Cursors.Hand;

            if (needsRedraw)
            {
                tabControl.Invalidate();
            }

            // Tab tear-away: if dragging and mouse moved beyond threshold, start drag
            if (_draggedTabIndex.HasValue && !_isDragging)
            {
                var screenPos = tabControl.PointToScreen(e.Location);
                if (_dragStartPoint.HasValue && (Math.Abs(screenPos.X - _dragStartPoint.Value.X) > 20 || Math.Abs(screenPos.Y - _dragStartPoint.Value.Y) > 5))
                {
                    _isDragging = true;
                }
            }

            // If dragging, check if outside window to detach, or near edge to split
            if (_isDragging && _draggedTabIndex.HasValue)
            {
                var screenPos = tabControl.PointToScreen(e.Location);
                var zone = GetDragZone(screenPos);
                if (zone == DragZone.Outside)
                {
                    DetachTabToNewWindow(_draggedTabIndex.Value, screenPos);
                }
                else if (zone != DragZone.None)
                {
                    SplitTabToPane(_draggedTabIndex.Value, zone);
                }
            }
        }

        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (tabControl.TabPages.Count == 0) return;

            var theme = _currentTheme;
            var tabRect = e.Bounds;
            tabRect.Height = tabControl.ItemSize.Height;

            bool isSelected = (e.Index == tabControl.SelectedIndex);
            bool isHovered = (e.Index == hoveredTabIndex);

            Color backColor;
            if (isSelected)
                backColor = theme.EditorBackground;
            else if (isHovered)
                backColor = theme.ButtonHoverBackground;
            else
                backColor = theme.MenuBackground;

            using (var brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, tabRect);
            }

            // Tab text — reserve space for close button. Same positioning as terminal tabs.
            string text = tabControl.TabPages[e.Index].Text;
            var textRect = new Rectangle(
                tabRect.X + 4, tabRect.Y + 3,
                tabRect.Right - 17 - tabRect.X - 10, tabRect.Height - 4);

            TextRenderer.DrawText(e.Graphics, text, tabControl.Font, textRect,
                isSelected ? theme.Text : theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            // Close button (×) — always visible, same style as terminal tabs
            var closeRect = new Rectangle(tabRect.Right - 17, tabRect.Y + 5, 14, 14);
            using (var xFont = new Font("Segoe UI", 10, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, "\u00D7", xFont, closeRect, theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        #endregion

        #region Public API (called by dialogs)

        public void GoToLine(int lineNumber)
        {
             if (lineNumber < 1 || LineCount == 0) return;
             int targetIndex = lineNumber - 1;
             if (targetIndex >= LineCount) targetIndex = LineCount - 1;
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

        private void HighlightCurrentLine()
        {
            if (isHighlighting) return;
            if (currentLineHighlightMode == CurrentLineHighlightMode.Off) return;

            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            if (currentLine == lastHighlightedLine) return;

            lastHighlightedLine = currentLine;
            gutterPanel?.RefreshGutter(); // Redraw gutter for NumberOnly mode
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            if (_applyingHighlight) return;
            SetDirty();

            // Recalculate gutter width when line count changes
            gutterPanel?.UpdateLineNumberWidth();
            SyncGutterColumnWidth();

            // Track which line changed for gutter display
            int lineIndex = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            modifiedLines.Add(lineIndex);

            // Mark affected lines for incremental re-highlighting (line and following line for context)
            incrementalHighlighter?.MarkDirty(lineIndex);
            incrementalHighlighter?.MarkDirty(lineIndex + 1);

            UpdateStatusBar();

            // Restart debounce timer for elastic tab stops recompute
            elasticTabTimer?.Stop();
            elasticTabTimer?.Start();
        }

        private void TextEditor_SelectionChanged(object? sender, EventArgs e)
        {
            if (_suspendSelectionChanged) return;
            // Skip line highlight during mouse drag to avoid interfering with selection
            if (Control.MouseButtons == MouseButtons.Left)
            {
                gutterPanel?.RefreshGutter();
                UpdateStatusBar();
                return;
            }
            HighlightCurrentLine();
            gutterPanel?.RefreshGutter();
            UpdateStatusBar();
        }


        private void TextEditor_VScroll(object? sender, EventArgs e)
        {
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            textEditor.Invalidate();
        }

        private void TextEditor_MouseDown(object? sender, MouseEventArgs e)
        {
            // TODO: implement mouse handling if needed
        }

        private void TextEditor_MouseWheel(object? sender, MouseEventArgs e)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                // Ctrl+MouseWheel changes ZoomFactor natively — sync the gutter after it's applied
                this.BeginInvoke(new Action(() =>
                {
                    float newZoom = textEditor.ZoomFactor;
                    if (Math.Abs(newZoom - zoomFactor) > 0.01f)
                    {
                        zoomFactor = newZoom;
                        zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                        gutterPanel?.UpdateLineNumberWidth();
                        SyncGutterColumnWidth();
                        textEditor.SyncCaretWidth();
            textEditor!.Invalidate();
                    }
                }));
            }
        }

        private int _lastStatusLine = -1;
        private int _lastStatusCol = -1;

        internal void UpdateStatusBar()
        {
            if (statusStrip == null || textEditor == null) return;

            int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int col = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)) + 1;
            if (line != _lastStatusLine || col != _lastStatusCol)
            {
                _lastStatusLine = line;
                _lastStatusCol = col;
                lineColLabel.Text = $"Ln {line}, Col {col}";
            }

            // Character count
            charCountLabel.Text = $"{textEditor.Text.Length:N0} characters";

            // Tab size
            tabSizeDropDown.Text = $"Tab: {tabSize}";

            // Current line / total lines
            int currentLineNum = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int totalLines = (int)SendMessage(textEditor.Handle, EM_GETLINECOUNT, 0, 0);
            linePositionLabel.Text = $"{currentLineNum} / {totalLines}";

            // Scroll percentage (current line / total lines)
            int total = totalLines;
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

             // Vim mode indicator — dedicated bold label
              if (vimModeEnabled)
              {
                  vimModeLabel.Visible = true;
                  if (vimEngine?.CurrentMode == VimMode.Command)
                  {
                      vimModeLabel.Text = ":" + vimEngine.CommandText;
                  }
                  else
                  {
                      vimModeLabel.Text = vimEngine?.CurrentMode switch
                      {
                          VimMode.Normal => "-- NORMAL --",
                          VimMode.Insert => "-- INSERT --",
                          VimMode.Visual => "-- VISUAL --",
                          VimMode.VisualLine => "-- VISUAL LINE --",
                          VimMode.VisualBlock => "-- VISUAL BLOCK --",
                          _ => "-- VIM --"
                      };
                  }
              }
              else
              {
                  vimModeLabel.Visible = false;
              }

            // File type
            string fileType = "Plain Text";
            if (currentSyntax != null && !string.IsNullOrEmpty(currentSyntax.Name))
            {
                fileType = currentSyntax.Name;
            }
            else if (!string.IsNullOrEmpty(currentFilePath))
            {
                var def = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
                fileType = def?.Name ?? "Plain Text";
            }
            fileTypeLabel.Text = fileType;
}

        private void UpdateTabControlTheme()
        {
            if (tabControl == null) return;
            var theme = _currentTheme;
            tabControl.BackColor = theme.MenuBackground;
            tabControl.ForeColor = theme.Text;
            // Force redraw to apply themed background in owner-drawn tabs
            tabControl.Invalidate();
        }

        #endregion

        #region Minimap Methods

        /// <summary>
        /// Positions the minimap as an overlay within the textEditor control, anchored to the right edge.
        /// The minimap is positioned to the left of the vertical scrollbar (non-client area).
        /// </summary>
        private void PositionMinimap()
        {
            if (minimapControl == null || editorPanel == null) return;

            if (_pendingMinimapVisible)
            {
                minimapControl.Visible = true;
                minimapControl.BringToFront();
                if (textEditor != null && textEditor.IsHandleCreated)
                {
                    int scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
                    int mw = Math.Min(minimapControl.MinimapWidth,
                        textEditor.Width - scrollBarWidth - 10);
                    int x = textEditor.Right - scrollBarWidth - mw;
                    x = Math.Max(0, x);
                    minimapControl.Location = new Point(x, textEditor.Top);
                    minimapControl.Size = new Size(mw, textEditor.ClientSize.Height);
                    minimapControl.MinimapWidth = mw;
                    minimapControl.AttachEditor(textEditor);
                }
            }
            else
            {
                minimapControl.Visible = false;
                minimapControl.DetachEditor();
            }
        }

        #endregion

        #region Word Wrap Methods

        private void ApplyWordWrap()
        {
            if (textEditor != null)
            {
                textEditor.WordWrap = wordWrapEnabled;
            }
        }

        internal void ToggleWordWrap()
        {
             wordWrapEnabled = !wordWrapEnabled;
             wordWrapMenuItem.Checked = wordWrapEnabled;
             ApplyWordWrap();
             SaveSettings();
         }

        #endregion

        #region External Change Methods

        private void CheckExternalChange()
        {
            if (string.IsNullOrEmpty(currentFilePath) || !File.Exists(currentFilePath))
                return;

            try
            {
                var lastWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                if (lastFileWriteTime.HasValue && lastWriteTime > lastFileWriteTime.Value)
                {
                    // File has been modified externally
                    var result = MessageBox.Show(
                        $"The file '{Path.GetFileName(currentFilePath)}' has been modified outside of the editor.\n" +
                        "Do you want to reload it?",
                        "External File Change",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        LoadFile(currentFilePath);
                    }
                    else
                    {
                        // Update our stored timestamp to prevent repeated prompts
                        lastFileWriteTime = lastWriteTime;
                    }
                }
            }
            catch
            {
                // Ignore errors accessing the file
            }
        }

        private void TabControl_Paint(object? sender, PaintEventArgs e)
        {
            var theme = _currentTheme;
            e.Graphics.Clear(theme.MenuBackground);
        }

        private void TabControl_HandleCreated(object? sender, EventArgs e)
        {
            // Strip visual styles to remove the 3D raised border from the tab strip
            if (tabControl.IsHandleCreated)
                SetWindowTheme(tabControl.Handle, "", "");

            // Remove the classic 3D raised border style
            int style = GetWindowLong(tabControl.Handle, GWL_STYLE);
            style = style & ~WS_BORDER;

            // Use flat tab buttons (no 3D raised edges per tab)
            const int TCS_FLATBUTTONS = 0x0008;
            style |= TCS_FLATBUTTONS;

            SetWindowLong(tabControl.Handle, GWL_STYLE, style);

            // Remove any extended 3D border style
            int exStyle = GetWindowLong(tabControl.Handle, GWL_EXSTYLE);
            exStyle &= ~WS_EX_CLIENTEDGE;
            SetWindowLong(tabControl.Handle, GWL_EXSTYLE, exStyle);

            // Force non-client area recalculation to apply the border changes
            const uint SWP_FRAMECHANGED = 0x0020;
            const uint SWP_NOACTIVATE = 0x0010;
            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOZORDER = 0x0004;
            SetWindowPos(tabControl.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);

            // Hide the dashed focus rectangle on the active tab
            const int WM_UPDATEUISTATE = 0x0128;
            const int UIS_SET = 1;
            const int UISF_HIDEFOCUS = 0x1;
            int wParam = (UISF_HIDEFOCUS << 16) | UIS_SET;
            SendMessage(tabControl.Handle, WM_UPDATEUISTATE, wParam, 0);

            _tabStripWindow?.ReleaseHandle();
            _tabStripWindow = new TabStripBackgroundWindow(this, tabControl);
            _tabStripWindow.AssignHandle(tabControl.Handle);
        }

        private void TabControl_HandleDestroyed(object? sender, EventArgs e)
        {
            _tabStripWindow?.ReleaseHandle();
            _tabStripWindow = null;
        }

        // Native-window subclass that paints the tab strip background with the current theme.
        private TabStripBackgroundWindow? _tabStripWindow;
        private TabStripBackgroundWindow? _terminalTabStripWindow;

        private sealed class TabStripBackgroundWindow : NativeWindow
        {
            private const int WM_ERASEBKGND = 0x0014;
            private readonly Form1 _owner;
            private readonly TabControl _target;

            public TabStripBackgroundWindow(Form1 owner, TabControl target)
            {
                _owner = owner;
                _target = target;
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_ERASEBKGND:
                        var theme = _owner._currentTheme;
                        using (var g = Graphics.FromHdc(m.WParam))
                        using (var brush = new SolidBrush(theme.MenuBackground))
                        {
                            g.FillRectangle(brush, _target.ClientRectangle);
                        }
                        m.Result = (IntPtr)1;
                        return;
                }
                base.WndProc(ref m);
            }
        }


        #endregion
        #region Syntax Highlighting (async, incremental, visible-range)

        private Color GetKeywordColor() => _currentTheme.KeywordColor;
        private Color GetStringColor() => _currentTheme.StringColor;
        private Color GetCommentColor() => _currentTheme.CommentColor;
        private Color GetNumberColor() => _currentTheme.NumberColor;
        private Color GetPreprocessorColor() => _currentTheme.PreprocessorColor;

        private void CreateIncrementalHighlighter()
        {
            incrementalHighlighter?.Dispose();
            incrementalHighlighter = null;

            if (!textEditor.IsHandleCreated) return;

            if (currentFilePath != null)
                currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
            else
                currentSyntax = SyntaxDefinition.CSharp;

            if (!syntaxHighlightingEnabled || currentSyntax == null)
            {
                ResetVisibleRangeToBase(_currentTheme.Text);
                return;
            }

            incrementalHighlighter = new IncrementalHighlighter(textEditor, currentSyntax);
            incrementalHighlighter.PatchReady += ApplyHighlightPatches;
            RequestVisibleHighlight();
        }

        private void ApplyHighlightPatches(List<HighlightPatch> patches)
        {
            if (textEditor.IsDisposed || !textEditor.IsHandleCreated || _applyingHighlight) return;
            _applyingHighlight = true;
            try
            {
                int lineCount = textEditor.Lines.Length;
                foreach (var patch in patches)
                {
                    int line = patch.LineNumber;
                    if (line < 0 || line >= lineCount) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
                    if (lineStart < 0) continue;
                    int lineEnd = (line + 1 < lineCount)
                        ? textEditor.GetFirstCharIndexFromLine(line + 1)
                        : textEditor.TextLength;
                    int lineLen = lineEnd - lineStart;
                    if (lineLen <= 0) continue;
                    foreach (var token in patch.Tokens)
                    {
                        int idx = lineStart + token.StartIndex;
                        int len = Math.Min(token.Length, lineLen - token.StartIndex);
                        if (len <= 0) continue;
                        textEditor.Select(idx, len);
                        textEditor.SelectionColor = GetColorForToken(token.Type);
                    }
                }
            }
            finally { _applyingHighlight = false; }
        }

        private void RequestVisibleHighlight()
        {
            if (incrementalHighlighter == null || !syntaxHighlightingEnabled || !textEditor.IsHandleCreated) return;
            var (first, last) = GetVisibleLineRange();
            if (first <= last)
                incrementalHighlighter.RequestRange(first, last);
        }

        private Color GetColorForToken(SyntaxTokenType type) => type switch
        {
            SyntaxTokenType.Keyword => GetKeywordColor(),
            SyntaxTokenType.String => GetStringColor(),
            SyntaxTokenType.Comment => GetCommentColor(),
            SyntaxTokenType.Number => GetNumberColor(),
            SyntaxTokenType.Preprocessor => GetPreprocessorColor(),
            _ => (_currentTheme.Text)
        };

        // Get visible line range in the editor (no buffer — only truly visible lines)
        private (int firstLine, int lastLine) GetVisibleLineRange()
        {
            try
            {
                if (textEditor == null || !textEditor.IsHandleCreated) return (0, -1);

                int visibleStart = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, 0)));
                int visibleEnd = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, textEditor.ClientSize.Height)));
                int totalLines = (int)SendMessage(textEditor.Handle, EM_GETLINECOUNT, 0, 0);
                int first = Math.Max(0, visibleStart);
                int last = Math.Min(totalLines - 1, visibleEnd);
                if (first > last) return (0, -1);
                return (first, last);
            }
            catch
            {
                return (0, -1);
            }
        }


         // Reset all visible lines to base color (used when no syntax highlighting)
         private void ResetVisibleRangeToBase(Color baseColor)
         {
             if (textEditor.IsDisposed) return;
             int selStart = textEditor.SelectionStart, selLength = textEditor.SelectionLength;
              BeginUpdate(textEditor);
              textEditor.SuspendLayout();
              _suspendSelectionChanged = true;
              try
             {
                  var (firstLine, lastLine) = GetVisibleLineRange();
                  if (firstLine <= lastLine)
                  {
                      for (int lineNum = firstLine; lineNum <= lastLine; lineNum++)
                      {
                          if (lineNum < 0 || lineNum >= LineCount) continue;
                          int lineStart = textEditor.GetFirstCharIndexFromLine(lineNum);
                          if (lineStart < 0) continue;
                          string line = GetLineText(lineNum);
                          int lineLen = line.Length;
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
                  _suspendSelectionChanged = false;
              }
         }

        /// <summary>
        /// Returns tokens for a given line index, used by MinimapControl for syntax coloring.
        /// </summary>
         private IReadOnlyList<MyCrownJewelApp.Pfpad.TokenInfo> GetTokensForLine(int lineIndex)
         {
             if (currentSyntax == null) return Array.Empty<MyCrownJewelApp.Pfpad.TokenInfo>();
             if (!textEditor.IsHandleCreated) return Array.Empty<MyCrownJewelApp.Pfpad.TokenInfo>();
             if (lineIndex < 0 || lineIndex >= LineCount) return Array.Empty<MyCrownJewelApp.Pfpad.TokenInfo>();

            if (incrementalHighlighter?.GetTokens(lineIndex) is IReadOnlyList<TokenInfo> cached)
                return cached;

            return Array.Empty<MyCrownJewelApp.Pfpad.TokenInfo>();
        }

        // WinForms RichTextBox BeginUpdate/EndUpdate via native API (no forced invalidation)
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, StringBuilder lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_BORDER = 0x00800000;
        private const int WS_EX_CLIENTEDGE = 0x00000200;
        private const int TCM_SETBKCOLOR = 0x1301;

        private const int WM_SETREDRAW = 0x0B;
        private const int WM_GETTEXTLENGTH = 0x000E;
        private const int WM_VSCROLL = 0x115;
        private const int EM_SETSEL = 0x00B1;
        private const int EM_LINEINDEX = 0x00BB;
        private const int EM_STARTUNDOACTION = 0x00B7;
        private const int EM_ENDUNDOACTION = 0x00B8;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_GETLINE = 0x00C4;
        private const int EM_SETCHARFORMAT = 0x0444;
        private const int SCF_SELECTION = 0x0001;
        private const int CFM_COLOR = 0x40000000;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct CHARFORMAT2W
        {
            public int cbSize;
            public int dwMask;
            public int dwEffects;
            public int yHeight;
            public int yOffset;
            public int crTextColor;
            public byte bCharSet;
            public byte bPitchAndFamily;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szFaceName;
            public short wWeight;
            public short sSpacing;
            public int crBackColor;
            public int lcid;
            public int dwReserved;
            public short sStyle;
            public short wKerning;
            public byte bUnderlineType;
            public byte bAnimation;
            public byte bRevAuthor;
            public byte bReserved1;
        }

        private void SetSelectionColorRich(Color color)
        {
            var cf = new CHARFORMAT2W
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<CHARFORMAT2W>(),
                dwMask = CFM_COLOR,
                crTextColor = System.Drawing.ColorTranslator.ToWin32(color)
            };
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(cf.cbSize);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(cf, ptr, false);
                SendMessage(textEditor.Handle, EM_SETCHARFORMAT, new IntPtr(SCF_SELECTION), ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeCoTaskMem(ptr);
            }
        }

        // Get a single line without allocating the entire .Lines array
        private string GetLineText(int lineIndex)
        {
            if (!textEditor.IsHandleCreated || textEditor.IsDisposed) return string.Empty;
            var sb = new System.Text.StringBuilder(4096);
            sb.Length = sb.Capacity;
            int len = SendMessage(textEditor.Handle, EM_GETLINE, lineIndex, sb);
            return len > 0 ? sb.ToString(0, len) : string.Empty;
        }

        // Get total line count
        private int LineCount
        {
            get
            {
                if (!textEditor.IsHandleCreated || textEditor.IsDisposed) return 0;
                return (int)SendMessage(textEditor.Handle, EM_GETLINECOUNT, 0, 0);
            }
        }

        private void BeginUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUpdate(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
        }

        private void BeginUndoUnit(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, EM_STARTUNDOACTION, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUndoUnit(RichTextBox rtb)
        {
            SendMessage(rtb.Handle, EM_ENDUNDOACTION, IntPtr.Zero, IntPtr.Zero);
        }

        #region Git Integration

        private void PollGitStatus()
        {
            try
            {
                if (string.IsNullOrEmpty(currentFilePath) && documents.Count == 0) return;
                string? dir = FindGitRepo();
                if (dir == null) { ClearGitLabels(); return; }

                string branch = RunGit(dir, "rev-parse --abbrev-ref HEAD");
                string status = RunGit(dir, "status --porcelain");

                if (string.IsNullOrEmpty(branch) || branch.Contains("fatal"))
                {
                    ClearGitLabels();
                    return;
                }

                _gitBranchName = branch;
                bool hasChanges = !string.IsNullOrEmpty(status?.Trim());
                _gitDirtyStatus = hasChanges ? " ●" : "";

                string? upstream = RunGit(dir, "rev-parse --abbrev-ref --symbolic-full-name @{u}");
                if (!string.IsNullOrEmpty(upstream) && !upstream.Contains("fatal"))
                {
                    string ahead = RunGit(dir, "rev-list --count --right-only HEAD...@{u}");
                    string behind = RunGit(dir, "rev-list --count --left-only HEAD...@{u}");
                    int aheadCount = int.TryParse(ahead?.Trim(), out var a) ? a : 0;
                    int behindCount = int.TryParse(behind?.Trim(), out var b) ? b : 0;
                    if (aheadCount > 0 && behindCount > 0)
                        _gitSyncStatus = $" ↓{behindCount} ↑{aheadCount}";
                    else if (aheadCount > 0)
                        _gitSyncStatus = $" ↑{aheadCount}";
                    else if (behindCount > 0)
                        _gitSyncStatus = $" ↓{behindCount}";
                    else
                        _gitSyncStatus = " ✔";
                }
                else
                {
                    _gitSyncStatus = "";
                }

                UpdateGitLabels();
            }
            catch { ClearGitLabels(); }
        }

        private string? FindGitRepo()
        {
            string? dir = currentFilePath != null ? Path.GetDirectoryName(currentFilePath) : null;
            if (dir == null && documents.Count > 0 && documents[0].FilePath != null)
                dir = Path.GetDirectoryName(documents[0].FilePath);
            if (dir == null) return null;

            var d = new DirectoryInfo(dir);
            while (d != null)
            {
                if (Directory.Exists(Path.Combine(d.FullName, ".git")))
                    return d.FullName;
                d = d.Parent;
            }
            return null;
        }

        private string RunGit(string workingDir, string args)
        {
            try
            {
                using var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "git";
                proc.StartInfo.Arguments = args;
                proc.StartInfo.WorkingDirectory = workingDir;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(2000);
                return output?.TrimEnd('\n', '\r') ?? "";
            }
            catch { return ""; }
        }

        private void UpdateGitLabels()
        {
            if (this.IsHandleCreated)
                this.BeginInvoke(new Action(() =>
                {
                    if (gitBranchLabel == null) return;
                    gitBranchLabel.Text = _gitBranchName;
                    gitDirtyLabel.Text = _gitDirtyStatus;
                    gitSyncLabel.Text = _gitSyncStatus;
                }));
        }

        private void ClearGitLabels()
        {
            if (this.IsHandleCreated)
                this.BeginInvoke(new Action(() =>
                {
                    if (gitBranchLabel == null) return;
                    gitBranchLabel.Text = "";
                    gitDirtyLabel.Text = "";
                    gitSyncLabel.Text = "";
                }));
        }

        #endregion

        #region Fullscreen Toggle
        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F10)
            {
                ToggleMenuVisibility();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _isFullScreen)
            {
                ToggleFullScreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Oemtilde && e.Control && !e.Shift && !e.Alt)
            {
                ToggleTerminal();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private bool _isFullScreen = false;
        private FormWindowState _preFullScreenWindowState;
        private FormBorderStyle _preFullScreenBorderStyle;
        private Rectangle _preFullScreenBounds;

        private void ToggleFullScreen()
        {
            if (_isFullScreen)
            {
                // Restore
                this.WindowState = _preFullScreenWindowState;
                this.FormBorderStyle = _preFullScreenBorderStyle;
                this.Bounds = _preFullScreenBounds;
                this.TopMost = false;
                _isFullScreen = false;
            }
            else
            {
                // Save current state
                _preFullScreenWindowState = this.WindowState;
                _preFullScreenBorderStyle = this.FormBorderStyle;
                _preFullScreenBounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;

                // Go fullscreen
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = FormBorderStyle.None;
                this.Bounds = Screen.FromControl(this).Bounds;
                this.TopMost = true;
                _isFullScreen = true;
            }
        }

        private void ToggleMenuVisibility()
        {
            if (menuStrip != null)
                menuStrip.Visible = !menuStrip.Visible;
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (IsModified())
            {
                var result = PromptSaveChanges();
                if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            SaveSettings();
            elasticTabTimer?.Stop();
            elasticTabTimer?.Dispose();
            _gitPollTimer?.Stop();
            _gitPollTimer?.Dispose();
            incrementalHighlighter?.Dispose();
            foreach (var t in _terminalTabs) t.Dispose();
            _terminalTabs.Clear();
            _terminalTabControl?.Dispose();
            _terminalNewTabButton?.Dispose();
        }

        private void Form1_Activated(object? sender, EventArgs e)
        {
            CheckExternalChange();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (vimModeEnabled && vimEngine != null)
            {
                if (vimEngine.ProcessKey(keyData))
                {
                    UpdateStatusBar(); // refresh mode indicator on status bar
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #endregion

        // Tab measurement cache for elastic tab stops
        private static class TabMeasurementCache
        {
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _cache
                = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.Ordinal);
            
            public static int GetStringWidth(string s, Font font)
            {
                if (string.IsNullOrEmpty(s)) return 0;
                string key = $"{s}|{font.Name}|{font.Size}";
                return _cache.GetOrAdd(key, _ =>
                {
                    using var bmp = new Bitmap(1, 1);
                    using var g = Graphics.FromImage(bmp);
                    var size = g.MeasureString(s, font, new PointF(0, 0), StringFormat.GenericTypographic);
                    return (int)Math.Ceiling(size.Width);
                });
            }
        }

        // Custom renderer for menu theming
        private class ThemeColorTable : ProfessionalColorTable
        {
            private readonly bool _isDark;
            public ThemeColorTable(bool isDark) => _isDark = isDark;

            public override Color MenuStripGradientBegin => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
            public override Color MenuStripGradientEnd => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
            public override Color MenuItemSelected => _isDark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;
            public override Color MenuItemSelectedGradientBegin => _isDark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;
            public override Color MenuItemSelectedGradientEnd => _isDark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;
            public override Color MenuItemBorder => _isDark ? Color.FromArgb(0, 120, 215) : SystemColors.Highlight;
            public override Color ToolStripDropDownBackground => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.Window;
            public override Color ImageMarginGradientBegin => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
            public override Color ImageMarginGradientEnd => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
            public override Color ImageMarginGradientMiddle => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
            public override Color MenuBorder => _isDark ? Color.FromArgb(45, 45, 45) : SystemColors.MenuBar;
        }

    }
}

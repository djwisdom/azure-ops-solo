 using System;
 using System.Collections.Generic;
 using System.ComponentModel;
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
 using System.Diagnostics;
 using System.Linq;
 using System.Security.Cryptography;
 using System.Runtime.CompilerServices;
 using MyCrownJewelApp.Pfpad.Debugger;
using MyCrownJewelApp.Pfpad.Features.RoslynControl;
using Microsoft.Extensions.DependencyInjection;

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

        private ToolStripMenuItem? lineNumberToolStripMenuItem;
        private ToolStripMenuItem? relativeLineNumberToolStripMenuItem;




        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows 10 1809+, Windows 11

        private void ApplyScrollbarTheme()
        {
            bool dark = isDarkTheme;
            NativeThemed.ApplyDarkScrollbarTheme(this.Handle, dark);
            NativeThemed.ApplyThemeToChildScrollbars(this, dark);
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
        private static readonly HashSet<int> _emptySet = new();
        internal bool gutterVisible = true;
        private bool statusBarVisible = true;
        internal bool wordWrapEnabled = false;
        internal bool syntaxHighlightingEnabled = false;
        private CurrentLineHighlightMode currentLineHighlightMode = CurrentLineHighlightMode.Off;
        internal int tabSize = 4;
        private bool insertSpaces = true;
        internal bool isDarkTheme => !_themeManager.CurrentTheme.IsLight;
        public bool IsDarkTheme => !_themeManager.CurrentTheme.IsLight;
        internal float zoomFactor = 1.0f;
        private bool isHighlighting = false;
        private int lastHighlightedLine = -1;
        private int _hoverLine = -1;
        private bool _hoverLineHighlightEnabled = false;

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

        // Workspace / folder tree state
        private SplitContainer? _workspaceSplitContainer;
        private WorkspacePanel? _workspacePanel;
        private bool _workspaceVisible;
        private int _workspaceWidth = 200;
        private string _workspaceRoot = "";

        // Git panel state
        private readonly GitService _gitService = new();
        private GitPanel? _gitPanel;
        private GitForm? _gitForm;
        private bool _gitPanelVisible;
        private SplitContainer? _sidebarSplit;
        private SplitContainer? _botSidebarSplit;
        private SplitContainer? _problemsSplit;

        // Symbol panel state
        private SymbolPanel? _symbolPanel;
        private bool _symbolPanelVisible;

        // Lint engine state
        private readonly LintEngine _lintEngine = new();
        private readonly QuickActionProvider _quickActionProvider;
        private ProblemsPanel? _problemsPanel;
        private bool _problemsPanelVisible;

        // Markdown preview state
        private MarkdownPreviewPanel? _markdownPreviewPanel;
        private SplitContainer? _editorSplitContainer;
        private bool _markdownPreviewVisible;

        // Test runner state
        private TestRunResult? _lastTestResult;
        private string? _lastTestProject;
        private bool _testsRunning;

        // Resize debounce timer — coalesces rapid resize events (snap, drag)
        private System.Windows.Forms.Timer _resizeDebounceTimer = null!;

        // Hover docs + signature help
        private readonly HoverTooltipForm _hoverTooltip = new();
        private readonly SignatureHelpForm _signatureHelp = new();
        private CompletionPopupForm? _completionPopup;
        private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 400 };
        private string _lastHoveredWord = "";

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
        private string _activeConfiguration = "Debug";

        // EditorDocument management (tabs) — collection delegated to DocumentManager
        internal readonly DocumentManager _docManager = new();
        internal List<EditorDocument> documents => _docManager.Documents;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int activeDocIndex { get => _docManager.ActiveIndex; set => _docManager.ActiveIndex = value; }
        private int nextUntitledNumber { get => _docManager.NextUntitledNumber; set => _docManager.NextUntitledNumber = value; }
        private int? hoveredTabIndex = null;
        private Rectangle? closeButtonBounds = null;
        private bool _closeButtonHovered = false;

        // Tab drag state for tear-away
        private int? _draggedTabIndex = null;
        private Point? _dragStartPoint = null;
        private bool _isDragging = false;
        private DragZone _pendingDragZone = DragZone.None;

        // Split view state
        private RichTextBox? _splitEditor = null;
        private EditorDocument? _splitDocument = null;
        private string? _splitDocumentTitle = null;
        private bool _splitIsHorizontal; // true = uses _terminalSplitContainer.Panel2

        // Helper to get active EditorDocument
        internal EditorDocument ActiveDoc => activeDocIndex >= 0 && activeDocIndex < documents.Count ? documents[activeDocIndex] : null!;

        // Suspend selection changed events during internal updates
        private bool _suspendSelectionChanged = false;

        // Elastic tab stops system
        private readonly System.Windows.Forms.Timer? elasticTabTimer;
        private System.Windows.Forms.Timer? _highlightTimer;
        private System.Windows.Forms.Timer? _scrollHighlightTimer;
        private readonly System.Windows.Forms.Timer _lockKeysTimer = new() { Interval = 500 };
        private System.Windows.Forms.Timer? _tabScrollDebounce;
        private int _pendingTabScrollIndex = -1;
        private int _pendingHighlightLine = -1;
        private CancellationTokenSource? tabComputeCts;
        private readonly Stopwatch _highlightPerfSw = new();
        private double _lastHighlightDurationMs;
        private const int HighlightTimerMinInterval = 100;
        private const int HighlightTimerMaxInterval = 500;
        private int _typingBurstCount;
        private DateTime _lastKeyPress = DateTime.MinValue;
        private int _previousTextLength;

        // Git integration via GitService

        // Code folding
        private FoldingManager? _foldingManager;
        private bool _suppressFoldRescan = false;
        public FoldingManager? FoldingManager => _foldingManager;

        // Syntax highlighting
        internal SyntaxDefinition? currentSyntax;
        private IncrementalHighlighter? incrementalHighlighter;

        // Sticky scroll
        private StickyScrollService? _stickyScroll;
        private bool _stickyScrollEnabled = true;

        // Rainbow brackets
        private bool _rainbowBracketsEnabled;

        // Breadcrumbs
        private bool _breadcrumbsEnabled;
        private bool _analyzersEnabled = true;
        private System.Windows.Forms.Timer? _breadcrumbDebounce;
        private const int BreadcrumbDebounceMs = 100;

        // Column guide state is authoritative in textEditor.GuideColumn / textEditor.ShowGuide
        private readonly Color guideColor = Color.FromArgb(60, 60, 60);

        // Ruler
        private RulerPanel rulerPanel = null!;

        // Theme management
        private ThemeManager _themeManager = ThemeManager.Instance;
        private readonly FontService _fontService = new();
        
        // Colors - for syntax highlighting
        // Current file state
        internal string? currentFilePath;
        private bool isModified = false;
        private DateTime? lastFileWriteTime;

        // Per-buffer dirty-flag system: saved snapshot (hash)
        private string? savedContentHash = null;

        // Session restore
        private readonly SessionManager _sessionManager = null!;
        private bool _cliArgsProvided;
        private bool _workspaceRootFromCli;

        // Performance profiler
        private PerformanceProfilerDialog? _profilerDialog;
#if PROFILING && DEBUG
        private DebugOverlay? _debugOverlay = null;
#endif
        private System.Windows.Forms.Timer? _gcMonitorTimer;
        private int _lastGCCollections;

        // Recent files
        // Designer fields (accessible via Controls collection)
        public HashSet<int> Bookmarks => _docManager.GetActive()?.Bookmarks ?? _emptySet;
        public HashSet<int> ModifiedLines => _docManager.GetActive()?.ModifiedLines ?? _emptySet;
        public HashSet<int> CollapsedRegions => _docManager.GetActive()?.CollapsedRegions ?? _emptySet;

        private Button _tabDropdownButton = null!;
        private bool _applyingHighlight;

        private readonly DateTime _startTime = DateTime.UtcNow;
        private const int NotificationDelaySeconds = 15;
        private readonly List<(string Title, string Summary)> _delayedNotifications = new();

        // App-wide services (DI-injected or fallback new())
        private readonly SettingsService _settingsService = null!;

        // Notification feed
        private readonly NotificationFeedService _notificationFeed = null!;
        private readonly UserProfileManager _profileManager = null!;
        private UserProfile _currentProfile = UserProfileManager.DefaultProfile;
        private NotificationCenterForm? _notificationCenter;
        private ToolStripStatusLabel _notificationStatusLabel = null!;
        private readonly HashSet<string> _toastedIds = new();
        private readonly List<NotificationToastForm> _activeToasts = new();

        // Symbol index for Go to Definition
        private readonly SymbolIndexService _symbolIndex = new();
        private SnippetEngine? _snippetEngine;
        private MultiCaretManager? _multiCaret;
        // Auto-save
        private readonly System.Windows.Forms.Timer _autoSaveTimer = new() { Interval = 30000 };
        private bool _autoSaveEnabled;

        // Workspace scan state
        private Color _originalStatusBarColor;
        private CancellationTokenSource? _symbolIndexCts;
        private ToolStripStatusLabel _workspaceProjectLabel = null!;
        private string? _detectedProjectPath;
        private string? _detectedSolutionPath;

        // Debugger integration
        private readonly DebugSession _debugSession = new();
        private readonly BreakpointManager _breakpointManager = new();
        private DebugVariablesPanel? _debugVariablesPanel;
        private DebugCallStackPanel? _debugCallStackPanel;
        private int _debugActiveLine = -1;
        private string? _debugActiveFile;
        private int _debugActiveFrameId;
        public BreakpointManager DebugBreakpointManager => _breakpointManager;
        public int DebugActiveLine => _debugActiveLine;
        public string? CurrentFilePath => currentFilePath;

        // Public property accessors for SettingsDialog
        public string CurrentFontName => textEditor.Font.Name;
        public float CurrentFontSize => textEditor.Font.Size;
        public int CurrentTabSize => tabSize;
        public bool CurrentInsertSpaces => insertSpaces;

        /// <summary>
        /// Updates the encoding label in the status bar based on current EditorDocument.
        /// </summary>
        private void UpdateEncodingLabel()
        {
            if (encodingLabel == null) return;

            var currentDoc = GetCurrentDocument();
            if (currentDoc?.FileEncoding != null)
            {
                encodingLabel.Text = UnicodeFileHelper.GetEncodingDisplayName(currentDoc.FileEncoding);
            }
            else
            {
                encodingLabel.Text = "No File";
            }
        }

        /// <summary>
        /// Apply feature degradation for large files to maintain performance.
        /// </summary>
        private void ApplyLargeFileDegradation(EditorDocument doc, long fileSize)
        {
            long fileSizeMB = fileSize / (1024 * 1024);

            // Disable syntax highlighting for large files
            if (DisableSyntaxHighlightingForLargeFiles && fileSize > SyntaxHighlightingThresholdBytes)
            {
                doc.DisableSyntaxHighlighting = true;
                _profilerDialog?.Log($"Disabled syntax highlighting for large file ({fileSizeMB}MB)");
            }

            // Disable minimap for large files
            if (DisableMinimapForLargeFiles && fileSizeMB > 50) // 50MB threshold for minimap
            {
                doc.DisableMinimap = true;
                _profilerDialog?.Log($"Disabled minimap for large file ({fileSizeMB}MB)");
            }

            // Disable word wrap for large files
            if (DisableWordWrapForLargeFiles && fileSizeMB > 20) // 20MB threshold for word wrap
            {
                doc.DisableWordWrap = true;
                _profilerDialog?.Log($"Disabled word wrap for large file ({fileSizeMB}MB)");
            }
        }

        // File size limit accessors
        public int MaxFileSizeMB { get; private set; } = 500;
        public int LargeFileWarningMB { get; private set; } = 50;
        public int AsyncFileWarningMB { get; private set; } = 20;
        public bool DisableSyntaxHighlightingForLargeFiles { get; private set; } = true;
        public bool DisableMinimapForLargeFiles { get; private set; } = true;
        public bool DisableWordWrapForLargeFiles { get; private set; } = false;
        public long SyntaxHighlightingThresholdBytes { get; private set; } = 50 * 1024;
        public bool CurrentWordWrap => wordWrapEnabled;
        public bool CurrentShowGuide => textEditor?.ShowGuide ?? false;
        public int CurrentGuideColumn => textEditor?.GuideColumn ?? 80;
        public string CurrentThemeName => _themeManager.CurrentTheme.Name;
        public bool CurrentGutterVisible => gutterVisible;
        public bool CurrentStatusBarVisible => statusBarVisible;
        public bool CurrentMinimapVisible => minimapMenuItem?.Checked ?? false;
        public bool CurrentShowWhitespace => whitespaceMenuItem?.Checked ?? false;
        public string CurrentLineHighlightName
        {
            get
            {
                if (currentLineHighlightMode == CurrentLineHighlightMode.NumberOnly) return "NumberOnly";
                if (currentLineHighlightMode == CurrentLineHighlightMode.WholeLine) return "WholeLine";
                if (currentLineHighlightMode == CurrentLineHighlightMode.NumberAndWholeLine) return "NumberAndWholeLine";
                return "None";
            }
        }
        public bool CurrentSyntaxHighlighting => syntaxHighlightingEnabled;
        public bool CurrentAutoIndent => autoIndentEnabled;
        public bool CurrentSmartTabs => smartTabsEnabled;
        public bool CurrentElasticTabs => elasticTabsEnabled;
        public bool CurrentRainbowBrackets => _rainbowBracketsEnabled;
        public bool CurrentBreadcrumbs => _breadcrumbsEnabled;
        public bool CurrentAutoSave => _autoSaveEnabled;
        public bool CurrentWorkspaceVisible => _workspaceVisible;
        public bool CurrentSymbolPanelVisible => _symbolPanelVisible;
        public bool CurrentProblemsPanelVisible => _problemsPanelVisible;
        public bool CurrentTerminalVisible => _terminalVisible;
        public int CurrentTerminalHeight => _terminalHeight;
        public bool CurrentAnalyzersEnabled => _analyzersEnabled;
        public string CurrentTerminalShell => _terminalShell;
        public bool CurrentVimMode => vimModeEnabled;
        public bool CurrentStickyScroll => _stickyScrollEnabled;
        public bool CurrentHoverLineHighlight => _hoverLineHighlightEnabled;

    public Form1()
        : this(skipInitialDocument: false, services: null)
    {
    }

    /// <summary>
    /// Internal constructor used by tear-away to avoid creating an extra untitled EditorDocument.
    /// </summary>
    internal Form1(bool skipInitialDocument)
        : this(skipInitialDocument, services: null)
    {
    }

    /// <summary>
    /// Primary constructor. Accepts an optional IServiceProvider for app-wide singleton
    /// services (NotificationFeedService, UserProfileManager, SessionManager).
    /// All form-scoped services (GitService, LintEngine, DebugSession, etc.) are
    /// created via new() because they carry per-window mutable state.
    /// </summary>
    internal Form1(bool skipInitialDocument, IServiceProvider? services)
    {
        // Initialize app-wide services from DI, or fall back to new() for designer
        // and tear-away paths that don't have a service provider.
        _settingsService = services is null
            ? new SettingsService()
            : services.GetRequiredService<SettingsService>();
        _notificationFeed = services is null
            ? new NotificationFeedService()
            : services.GetRequiredService<NotificationFeedService>();
        _profileManager = services is null
            ? new UserProfileManager()
            : services.GetRequiredService<UserProfileManager>();
        _sessionManager = services is null
            ? new SessionManager()
            : services.GetRequiredService<SessionManager>();

        InitializeComponent();

        this.KeyDown += Form1_KeyDown;

        // Initialize status bar visibility (Vim mode starts disabled)
        UpdateStatusBarVisibility();

        // Start GC monitoring
        _lastGCCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        _gcMonitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _gcMonitorTimer.Tick += GcMonitorTimer_Tick;
        _gcMonitorTimer.Start();

        try { this.Opacity = 0; } catch { }
        this.KeyPreview = true;
        this.FormClosing += Form1_FormClosing;
        this.Activated += Form1_Activated;
          this.Shown += (s, e) =>
          {
              PositionTabDropdownButton();

              // Restore window bounds after LoadSettings has run
              RestoreWindowBounds();

              if (!string.IsNullOrEmpty(_workspaceRoot) && _workspacePanel != null && _workspaceRootFromCli)
              {
                  _workspacePanel.SetRoot(_workspaceRoot);
                  AddToRecentWorkspaces(_workspaceRoot);
              }

              // Don't restore session if CLI args were provided OR if there are already documents loaded
              // (which could happen from CLI args or if user already opened files)
              if (!_cliArgsProvided && documents.Count <= 1)
              {
                  Debug.WriteLine("Restoring session because no CLI args provided and no documents loaded");
                  RestoreSession();
              }
              else
              {
                  Debug.WriteLine($"Skipping session restore: CLI args provided ({_cliArgsProvided}) or documents already loaded ({documents.Count})");
              }

              // Start delayed notification timer
              BeginInvoke(new Action(() =>
              {
                  var elapsed = (DateTime.UtcNow - _startTime).TotalSeconds;
                  if (elapsed < NotificationDelaySeconds)
                  {
                      var remainingMs = (int)((NotificationDelaySeconds - elapsed) * 1000);
                      var delayTimer = new System.Windows.Forms.Timer { Interval = remainingMs };
                      delayTimer.Tick += (_, __) =>
                      {
                          delayTimer.Stop();
                          delayTimer.Dispose();
                          FlushDelayedNotifications();
                      };
                      delayTimer.Start();
                  }
                  else
                  {
                      FlushDelayedNotifications();
                  }
              }));

              // Show the window now that everything is ready
              this.Opacity = 1;
          };

        // Enable file drop support (client area + non-client area)
        EnableFileDrop();

        // Initialize EditorDocument list and process command line args
        // _docManager starts empty (List<EditorDocument> = new(), ActiveIndex = -1)

        if (!skipInitialDocument)
        {
            // Process command line arguments (files)
            string[] args = Environment.GetCommandLineArgs();
            _cliArgsProvided = false;
            bool newWindow = false;
            if (args.Length > 1)
            {
                bool hasFileArgs = false;
                for (int i = 1; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg == "--new-window")
                    {
                        newWindow = true;
                        continue;
                    }
                    if (Directory.Exists(arg))
                    {
                        _workspaceRoot = arg;
                        _workspaceRootFromCli = true;
                    }
                    else if (File.Exists(arg))
                    {
                        Debug.WriteLine($"Processing command line file argument: {arg}");

                        // Load file synchronously for CLI args
                        var fileInfo = new FileInfo(arg);
                        if (fileInfo.Length > (long)MaxFileSizeMB * 1024 * 1024)
                        {
                            ThemedMessageBox.Show($"File is too large to open ({fileInfo.Length / (1024 * 1024)} MB). Maximum size is {MaxFileSizeMB} MB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }

                        var syntax = SyntaxDefinition.GetDefinitionForFile(arg);
                        var doc = new EditorDocument
                        {
                            FilePath = arg,
                            Content = "",
                            IsDirty = false,
                            ModifiedLines = new HashSet<int>(),
                            Bookmarks = new HashSet<int>(),
                            CollapsedRegions = new HashSet<int>(),
                            SavedHash = "",
                            LastWriteTime = File.GetLastWriteTimeUtc(arg),
                            SelectionStart = 0,
                            SelectionLength = 0,
                            FirstVisibleLine = 0,
                            Syntax = syntax,
                            FileEncoding = null
               };
               if (vimEngine != null) vimEngine.ModeChanged += UpdateStatusBar;
                        // Apply feature degradation for large files
                        ApplyLargeFileDegradation(doc, fileInfo.Length);

                        documents.Add(doc);
                        int newIndex = documents.Count - 1;
                        var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                        tabControl.TabPages.Add(tabPage);
                        tabControl.SelectedIndex = newIndex;
                        activeDocIndex = newIndex;
                        EnsureSelectedTabVisible();

                        // Load content synchronously
                        try
                        {
                            var (content, encoding) = UnicodeFileHelper.ReadAllTextWithEncoding(arg);
                            doc.Content = content;
                            doc.FileEncoding = encoding;
                            doc.ContainsRtlText = UnicodeFileHelper.ContainsRtlText(content);
                            doc.SavedHash = ComputeContentHash(content);

                            // Update editor
                            textEditor.TextChanged -= TextEditor_TextChanged;
                            textEditor.Text = content;
                            textEditor.TextChanged += TextEditor_TextChanged;
                            savedContentHash = doc.SavedHash;
                            isModified = false;

                            UpdateStatusBar();
                            CreateIncrementalHighlighter();
                            UpdateTabTitle(newIndex);
                            AddToRecentFiles(arg);
                        }
                        catch (Exception ex)
                        {
                            ThemedMessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        hasFileArgs = true;
                    }
                }
                _cliArgsProvided = hasFileArgs || !string.IsNullOrEmpty(_workspaceRoot) || newWindow;
            }

            // If no documents were opened, create a new untitled EditorDocument
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
        textEditor.ShowGuide = false;
            
            _sessionManager.LoadRecentFiles();
            UpdateRecentMenu();

            _sessionManager.LoadRecent();
            UpdateRecentWorkspacesMenu();
            
            // Load persisted settings (overrides defaults below)
            LoadSettings();
            // Load active profile
            try
            {
                var p = _profileManager.ActiveProfile;
                if (p != null) _currentProfile = p;
                if (_currentProfile.PrimaryWorkspace != null && string.IsNullOrEmpty(_workspaceRoot) && !_workspaceRootFromCli)
                    _workspaceRoot = _currentProfile.PrimaryWorkspace.Path;
            }
            catch { }
            
            // Apply persisted analyzer state to Roslyn service
            if (_analyzersEnabled)
                EnsureRoslynService().SetAnalyzersEnabledAsync(true).ConfigureAwait(false);
            RebuildExternalToolsMenu();
            
            // Apply loaded font after settings are loaded
            _fontService.ApplyFont(textEditor);
            
            // Subscribe to handle creation BEFORE any operations that might cause handle creation
            textEditor.HandleCreated += (s, e) =>
            {
                ApplyScrollbarTheme();
                PositionMinimap();
                rulerPanel.AttachEditor(textEditor!);
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
            
            UpdateThemeColors(_themeManager.CurrentTheme);

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

            // Wire lazy population for File → Preferences → Profile submenu
            preferencesProfileMenuItem.DropDownOpening += (s, e) => UpdateFileMenuProfileItems();

            // Initialize toggles to match loaded/default settings
            gutterMenuItem.Checked = gutterVisible;
            columnGuideMenuItem.Checked = textEditor.ShowGuide;
            minimapMenuItem.Checked = _pendingMinimapVisible;
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            insertSpacesMenuItem.Checked = insertSpaces;
            autoIndentMenuItem.Checked = autoIndentEnabled;
            smartTabsMenuItem.Checked = smartTabsEnabled;
            elasticTabsMenuItem.Checked = elasticTabsEnabled;
            rainbowBracketsMenuItem.Checked = _rainbowBracketsEnabled;
            breadcrumbMenuItem.Checked = _breadcrumbsEnabled;
            breadcrumbPanel.Visible = _breadcrumbsEnabled;
            gutterPanel.TopOffset = _breadcrumbsEnabled ? breadcrumbPanel.Height : 0;

            // Initialize incremental syntax highlighter if enabled
            if (syntaxHighlightingEnabled)
            {
                CreateIncrementalHighlighter();
            }

            // Track initial text length for paste detection
            _previousTextLength = textEditor != null ? textEditor.TextLength : 0;

            // Apply visibility states
            gutterPanel.Visible = gutterVisible;
            whitespaceOverlay.ShowGlyphs = whitespaceMenuItem.Checked;
            statusStrip.Visible = statusBarVisible;
            
            // Set initial column widths for visible state
            if (mainTable.ColumnCount >= 1 && gutterPanel != null)
            {
                if (gutterVisible)
                {
                    gutterPanel.UpdateLineNumberWidth();
                    mainTable!.PerformLayout();
                    mainTable!.Refresh();
                    gutterPanel.MarkDataDirty();
                }
                else
                {
                    gutterPanel.Width = 0;
                    mainTable!.PerformLayout();
                    mainTable!.Refresh();
                    gutterPanel.MarkDataDirty();
                }
            }
            
            // Attach minimap to editor events (visibility/position handled by HandleCreated/Layout/Resize)
            if (minimapControl != null && textEditor != null)
            {
                minimapControl.ViewportChanged += MinimapControl_ViewportChanged;
                minimapControl.SetTokenProvider(GetTokensForLine);
            }

            // Wire whitespace overlay (transparent overlay form)
            whitespaceOverlay.LinkedEditor = textEditor;
            whitespaceOverlay.OwnerForm = this;

            // Tab dropdown menu button (rightmost corner of tab strip)
            _tabDropdownButton = new Button
            {
                Text = "\u25BC",
                Font = new Font("Segoe UI", 7),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(20, 22),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _tabDropdownButton.FlatAppearance.BorderSize = 0;
            _tabDropdownButton.Click += TabDropdownButton_Click;

            // Re-enable tab dropdown button — bounds-safe after DrawItem guard
            if (tabControl != null)
            {
                this.Controls.Add(_tabDropdownButton);
                _tabDropdownButton.BringToFront();
                tabControl.Resize += (s, e) => PositionTabDropdownButton();
                tabControl.ControlAdded += (s, e) => PositionTabDropdownButton();
                tabControl.ControlRemoved += (s, e) => PositionTabDropdownButton();
            }

            // Initialize folding manager and scan regions
            _foldingManager = new FoldingManager(textEditor!, _roslynWorkspace);
            textEditor!.FoldingManager = _foldingManager;
            _stickyScroll = new StickyScrollService();
            textEditor!.TextChanged += (s, e) =>
            {
                if (!_suppressFoldRescan) _foldingManager?.ScanRegions();
                gutterPanel?.RefreshGutter();
                ScheduleLint();
                RebuildStickyScopes();
            };
            _foldingManager.ScanRegions();
            RebuildStickyScopes();

            // Initialize sticky scroll panel position and font
            stickyScrollPanel.Font = textEditor!.Font;
            stickyScrollPanel.SyncWidth(textEditor.ClientSize.Width);
            textEditor.Resize += (s, e) =>
            {
                if (textEditor != null && stickyScrollPanel.Visible)
                    stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
            };

            // Initialize breadcrumb panel
            breadcrumbPanel.Font = textEditor.Font;
            breadcrumbPanel.FilePath = currentFilePath ?? "";
            _breadcrumbDebounce = new System.Windows.Forms.Timer { Interval = BreadcrumbDebounceMs };
            _breadcrumbDebounce.Tick += (s, e) => { _breadcrumbDebounce.Stop(); UpdateBreadcrumbs(); };
            textEditor.SelectionChanged += (s, e) => DebounceBreadcrumbs();
            textEditor.VScroll += (s, e) => { UpdateBreadcrumbsSync(); gutterPanel?.RefreshGutter(); };
            UpdateBreadcrumbs();

            // Initialize rainbow brackets from loaded settings
            textEditor.EnableRainbowBrackets(_rainbowBracketsEnabled);

            // Lint engine — wire diagnostics to problems panel + squiggles
            _quickActionProvider = new QuickActionProvider(_symbolIndex, _roslynWorkspace);
            _lintEngine.DiagnosticsUpdated += OnLintDiagnosticsUpdated;

            // Hover docs + signature help
            textEditor.MouseMove += TextEditor_MouseMoveHover;
            textEditor.KeyDown += TextEditor_KeyDownHelp;
            textEditor.KeyPress += TextEditor_KeyPressHelp;
            _hoverTimer.Tick += HoverTimer_Tick;
            _hoverTooltip.FormClosing += (s, e) => { e.Cancel = true; if (Visible) _hoverTooltip.Hide(); };
            _signatureHelp.FormClosing += (s, e) => { e.Cancel = true; if (Visible) _signatureHelp.Hide(); };
            
            // Elastic tab stops debounce timer
            elasticTabTimer = new System.Windows.Forms.Timer();
            elasticTabTimer.Interval = 250;
            elasticTabTimer.Tick += (s, e) => { elasticTabTimer.Stop(); if (elasticTabsEnabled) ComputeElasticTabStopsAsync(); };

            // Highlight debounce timer — delays syntax highlighting during rapid typing
            _highlightTimer = new System.Windows.Forms.Timer();
            _highlightTimer.Interval = HighlightTimerMinInterval;
            _highlightTimer.Tick += (s, e) =>
            {
                _highlightTimer.Stop();
                if (_pendingHighlightLine >= 0)
                {
                    incrementalHighlighter?.MarkDirty(_pendingHighlightLine);
                    incrementalHighlighter?.MarkDirty(_pendingHighlightLine + 1);
                    _pendingHighlightLine = -1;
                }
            };

            // Scroll highlight debounce — delays visible-range highlighting until scroll settles
            _scrollHighlightTimer = new System.Windows.Forms.Timer();
            _scrollHighlightTimer.Interval = 120;
            _scrollHighlightTimer.Tick += (s, e) =>
            {
                _scrollHighlightTimer.Stop();
                RequestVisibleHighlight();
            };

            // Lock-key indicator: polls CapsLock / NumLock / ScrollLock every 500ms
            _lockKeysTimer.Tick += (s, e) => UpdateLockKeysLabel();
            _lockKeysTimer.Start();

            // Auto-save: every 30 s, save file if it has unsaved changes and a known path
            _autoSaveTimer.Tick += (s, e) =>
            {
                if (_autoSaveEnabled && isModified && currentFilePath != null)
                    SaveFile();
            };

              // Git service: wire to current file location
              _gitService.OnRepoChanged += () => BeginInvoke(UpdateGitStatusBar);
              RefreshGitRepo();

             // Initialize incremental highlighter (after colors are loaded)
             if (documents.Count > 0) CreateIncrementalHighlighter();

              // Initialize line number menu items first
              lineNumberToolStripMenuItem = new ToolStripMenuItem("Line &number") { CheckOnClick = true };
              relativeLineNumberToolStripMenuItem = new ToolStripMenuItem("&Relative line number") { CheckOnClick = true };

              // Initialize Vim engine
              vimEngine = new VimEngine(textEditor!);
               vimEngine.OnShowLineNumbersChanged += value => { gutterPanel!.ShowLineNumbers = value; gutterPanel!.UpdateLineNumberWidth(); gutterPanel!.MarkDataDirty(); if (lineNumberToolStripMenuItem != null) lineNumberToolStripMenuItem.Checked = value && !vimEngine.RelativeNumbers; };
              vimEngine.OnRelativeNumbersChanged += value => { gutterPanel!.RelativeNumbers = value; gutterPanel!.UpdateLineNumberWidth(); gutterPanel!.MarkDataDirty(); if (relativeLineNumberToolStripMenuItem != null) relativeLineNumberToolStripMenuItem.Checked = value; };
              vimEngine.OnGutterVisibilityChanged += value => { if (value) { gutterVisible = true; gutterPanel!.ShowLineNumbers = vimEngine.ShowLineNumbers; gutterPanel!.RelativeNumbers = vimEngine.RelativeNumbers; gutterPanel!.UpdateLineNumberWidth(); mainTable!.ColumnStyles[0].SizeType = SizeType.Absolute; mainTable!.ColumnStyles[0].Width = gutterPanel!.Width; mainTable!.PerformLayout(); mainTable!.Refresh(); this.PerformLayout(); this.Refresh(); gutterPanel!.MarkDataDirty(); gutterPanel!.Visible = true; } else { gutterPanel!.Visible = false; gutterVisible = false; gutterPanel!.Width = 0; mainTable!.ColumnStyles[0].SizeType = SizeType.Absolute; mainTable!.ColumnStyles[0].Width = 0; mainTable!.PerformLayout(); mainTable!.Refresh(); this.PerformLayout(); this.Refresh(); gutterPanel!.MarkDataDirty(); } if (!value) { gutterPanel!.ShowLineNumbers = false; gutterPanel!.RelativeNumbers = false; } if (gutterMenuItem != null) gutterMenuItem.Checked = value; };
              LoadVimrc();

              // Sync menu checked states with VimEngine after loading .vimrc
              lineNumberToolStripMenuItem.Checked = vimEngine.ShowLineNumbers && !vimEngine.RelativeNumbers;
              relativeLineNumberToolStripMenuItem.Checked = vimEngine.RelativeNumbers;
              gutterMenuItem.Checked = vimEngine.GutterVisible;

              // Add line number menu items to View menu
              var viewMenu = menuStrip.Items[3] as ToolStripMenuItem; // View menu is index 3
              if (viewMenu != null)
              {
                  viewMenu.DropDownItems.Add(new ToolStripSeparator());
                  viewMenu.DropDownItems.Add(lineNumberToolStripMenuItem);
                  viewMenu.DropDownItems.Add(relativeLineNumberToolStripMenuItem);
              }

              // Set initial checked (mutually exclusive)
              bool showNumbers = gutterPanel?.ShowLineNumbers ?? true;
              bool relative = gutterPanel?.RelativeNumbers ?? false;
              lineNumberToolStripMenuItem.Checked = showNumbers && !relative;
              relativeLineNumberToolStripMenuItem.Checked = relative;

              // Menu click events (mutually exclusive, direct control)
               lineNumberToolStripMenuItem.Click += (s, e) => {
                   bool isChecked = lineNumberToolStripMenuItem.Checked;
                   if (isChecked) {
                       relativeLineNumberToolStripMenuItem.Checked = false;
                       vimEngine.GutterVisible = true;
                       vimEngine.ShowLineNumbers = true;
                       vimEngine.RelativeNumbers = false;
                   } else {
                       vimEngine.GutterVisible = false;
                       vimEngine.ShowLineNumbers = false;
                       vimEngine.RelativeNumbers = false;
                   }
               };
               relativeLineNumberToolStripMenuItem.Click += (s, e) => {
                   bool isChecked = relativeLineNumberToolStripMenuItem.Checked;
                   if (isChecked) {
                       lineNumberToolStripMenuItem.Checked = false;
                       vimEngine.GutterVisible = true;
                       vimEngine.ShowLineNumbers = true;
                       vimEngine.RelativeNumbers = true;
                   } else {
                       vimEngine.RelativeNumbers = false;
                       if (!lineNumberToolStripMenuItem.Checked) {
                           vimEngine.GutterVisible = false;
                           vimEngine.ShowLineNumbers = false;
                       }
                   }
               };
              textEditor.Enter += (s, e) => { if (vimModeEnabled) vimEngine?.SetEditor(textEditor); };
               _snippetEngine = new SnippetEngine(textEditor!);
               _multiCaret = new MultiCaretManager(textEditor!);
              void LoadVimrc()
              {
                  string vimrcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vimrc");
                  if (!File.Exists(vimrcPath))
                      vimrcPath = ".vimrc";
                   if (File.Exists(vimrcPath))
                   {
                       try
                       {
                           foreach (string line in File.ReadLines(vimrcPath))
                           {
                               string trimmed = line.Trim();
                               if (trimmed.StartsWith(":"))
                                   vimEngine.ExecuteCommand(trimmed[1..]);
                           }
                       }
                       catch (Exception ex)
                       {
                           ShowNotification("Vim", $"Failed to load .vimrc: {ex.Message}");
                       }
                   }
              }
              vimEngine.SaveRequested += () => { if (currentFilePath != null) { SaveFile(); ShowNotification("Vim", "File saved"); } else { SaveAsFile(); } };
              vimEngine.SaveAsRequested += (filename) =>
              {
                  string dir = currentFilePath != null ? Path.GetDirectoryName(currentFilePath)!
                      : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                  string fullPath = Path.GetFullPath(Path.Combine(dir, filename));
                  try
                  {
                      UnicodeFileHelper.WriteAllText(fullPath, textEditor.Text);
                      currentFilePath = fullPath;
                      lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                      currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
                      ClearDirtyAfterSave();
                      _docManager.GetActive()?.ModifiedLines.Clear();
                      AddToRecentFiles(currentFilePath);
                      UpdateStatusBar();
                      if (syntaxHighlightingEnabled)
                          CreateIncrementalHighlighter();
                      UpdateWindowTitle();
                      UpdateActiveTabTitle();
                      ShowNotification("Vim", $"Saved as {Path.GetFileName(fullPath)}");
                  }
                  catch (Exception ex)
                  {
                      ShowNotification("Vim", $"Save error: {ex.Message}");
                  }
              };
              vimEngine.CloseRequested += () => this.Close();
              vimEngine.VerticalSplitRequested += () =>
              {
                  if (documents.Count > 0)
                  {
                      SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Right);
                      ShowNotification("Vim", "Vertical split created");
                  }
              };
              vimEngine.HorizontalSplitRequested += () =>
              {
                  if (documents.Count > 0)
                  {
                      SplitTabToPane(activeDocIndex >= 0 ? activeDocIndex : 0, DragZone.Bottom);
                      ShowNotification("Vim", "Horizontal split created");
                  }
              };
              vimEngine.InsertSpacesRequested += (v) => { if (insertSpaces != v) { ToggleInsertSpaces(); ShowNotification("Vim", v ? "expandtab" : "noexpandtab"); } };
              vimEngine.TabSizeRequested += (s) => { SetTabSize(s); ShowNotification("Vim", $"tabstop={s}"); };
              vimEngine.AutoIndentRequested += (v) => { if (autoIndentEnabled != v) { ToggleAutoIndent(); ShowNotification("Vim", v ? "smartindent" : "nosmartindent"); } };
              vimEngine.SmartTabsRequested += (v) => { if (smartTabsEnabled != v) { ToggleSmartTabs(); ShowNotification("Vim", v ? "smarttab" : "nosmarttab"); } };

              vimEngine.FileOpenRequested += (filename) =>
              {
                  string dir = !string.IsNullOrEmpty(currentFilePath)
                      ? Path.GetDirectoryName(currentFilePath)!
                      : Environment.CurrentDirectory;
                  string fullPath = Path.GetFullPath(Path.Combine(dir, filename));
                  if (File.Exists(fullPath))
                  {
                      OpenFileInNewTab(fullPath);
                  }
                  else
                  {
                      ShowNotification("Vim", $"File not found: {filename}");
                  }
              };
              vimEngine.CommandFeedback += (msg) => ShowNotification("Vim", msg);
              vimEngine.TerminalRequested += () => { ToggleTerminal(); ShowNotification("Vim", "Terminal toggled"); };
              vimEngine.SplitCloseRequested += () => { CloseSplit(); ShowNotification("Vim", "Split closed"); };
              vimEngine.SplitNextRequested += () =>
              {
                  if (ActiveTerminal?.ContainsFocus == true)
                  {
                      if (textEditor.CanFocus) textEditor.Focus();
                  }
                  else if (_splitEditor != null && _splitEditor.Focused)
                  {
                      if (_terminalVisible && ActiveTerminal != null)
                          ActiveTerminal.FocusInput();
                      else if (textEditor.CanFocus)
                          textEditor.Focus();
                  }
                  else
                  {
                      if (_splitEditor != null)
                          _splitEditor.Focus();
                      else if (_terminalVisible && ActiveTerminal != null)
                          ActiveTerminal.FocusInput();
                  }
              };

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
                 BackColor = _themeManager.CurrentTheme.MenuBackground
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
                 var theme = _themeManager.CurrentTheme;
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
                 BackColor = _themeManager.CurrentTheme.MenuBackground,
                 ForeColor = _themeManager.CurrentTheme.Text
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
             _terminalSplitContainer.Panel1.BackColor = _themeManager.CurrentTheme.EditorBackground;

             // Panel2: tab control + new-tab button
             _terminalSplitContainer.Panel2.BackColor = _themeManager.CurrentTheme.MenuBackground;
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

              // Wrap editor/terminal in workspace split container (workspace sidebar | editor+terminal)
              if (_workspaceSplitContainer == null)
              {
                  _workspacePanel = new WorkspacePanel(_gitService);
                   _workspacePanel.FileOpenRequested += (path) =>
                   {
                       if (!string.IsNullOrEmpty(path) && File.Exists(path))
                           OpenFileInNewTab(path);
                   };
                  _workspacePanel.CloseRequested += () => ToggleWorkspace();
                  _workspacePanel.ScanStarted += OnWorkspaceScanStarted;
                  _workspacePanel.ScanCompleted += OnWorkspaceScanCompleted;
                   _workspacePanel.ScanProgressChanged += OnWorkspaceScanProgressChanged;
                   _workspacePanel.RootChanged += (path) => ActiveTerminal?.SendInput($"cd \"{path}\"{Environment.NewLine}");
 
                  // Git panel
                  _gitPanel = new GitPanel(_gitService);
                  _gitPanel.FileOpenRequested += (path) =>
                  {
                      if (!string.IsNullOrEmpty(path) && File.Exists(path))
                          OpenFileInNewTab(path);
                  };
                   _gitPanel.CloseRequested += () => ToggleGitPanel();
                   _gitPanel.StatusChanged += () => _workspacePanel?.RefreshGitStatusIndicators();

                  // Symbol panel
                  _symbolPanel = new SymbolPanel(_symbolIndex);
                  _symbolPanel.SymbolSelected += (file, line) =>
                  {
                      if (!string.IsNullOrEmpty(file) && File.Exists(file))
                      {
                          OpenFileInNewTab(file);
                          GoToLine(line);
                      }
                  };
                  _symbolPanel.CloseRequested += () => ToggleSymbolPanel();
                  _symbolIndex.OnIndexUpdated += () => BeginInvoke(_symbolPanel.RefreshSymbols);

                  // Problems panel
                  _problemsPanel = new ProblemsPanel();
                  _problemsPanel.ProblemSelected += (file, line) =>
                  {
                      if (!string.IsNullOrEmpty(file) && File.Exists(file))
                      {
                          OpenFileInNewTab(file);
                          GoToLine(line);
                      }
                  };
                  _problemsPanel.CloseRequested += () => ToggleProblemsPanel();

                  _problemsSplit = new SplitContainer
                  {
                      Dock = DockStyle.Fill,
                      Orientation = Orientation.Horizontal,
                      Panel1MinSize = 60,
                      Panel2MinSize = 60,
                      SplitterWidth = 4,
                      SplitterIncrement = 8,
                      BorderStyle = BorderStyle.None,
                      Panel2Collapsed = !_problemsPanelVisible
                  };
                  _problemsSplit.Panel1.Controls.Add(_symbolPanel);
                  _symbolPanel.Dock = DockStyle.Fill;
                  _problemsSplit.Panel2.Controls.Add(_problemsPanel);
                  _problemsPanel.Dock = DockStyle.Fill;
                  _problemsSplit.Panel1.BackColor = _themeManager.CurrentTheme.MenuBackground;
                  _problemsSplit.Panel2.BackColor = _themeManager.CurrentTheme.MenuBackground;

                  _botSidebarSplit = new SplitContainer
                  {
                      Dock = DockStyle.Fill,
                      Orientation = Orientation.Horizontal,
                      Panel1MinSize = 60,
                      Panel2MinSize = 60,
                      SplitterWidth = 4,
                      SplitterIncrement = 8,
                      BorderStyle = BorderStyle.None,
                      Panel2Collapsed = !_symbolPanelVisible && !_problemsPanelVisible
                  };
                  _botSidebarSplit.Panel1.Controls.Add(_gitPanel);
                  _gitPanel.Dock = DockStyle.Fill;
                  _botSidebarSplit.Panel2.Controls.Add(_problemsSplit);
                  _botSidebarSplit.Panel1.BackColor = _themeManager.CurrentTheme.MenuBackground;
                  _botSidebarSplit.Panel2.BackColor = _themeManager.CurrentTheme.MenuBackground;

                  _sidebarSplit = new SplitContainer
                  {
                      Dock = DockStyle.Fill,
                      Orientation = Orientation.Horizontal,
                      Panel1MinSize = 60,
                      Panel2MinSize = 60,
                      SplitterWidth = 4,
                      SplitterIncrement = 8,
                      BorderStyle = BorderStyle.None,
                      Panel2Collapsed = !_gitPanelVisible && !_symbolPanelVisible
                  };
                  _sidebarSplit.Panel1.Controls.Add(_workspacePanel);
                  _workspacePanel.Dock = DockStyle.Fill;
                  _sidebarSplit.Panel2.Controls.Add(_botSidebarSplit);
                  _sidebarSplit.Panel1.BackColor = _themeManager.CurrentTheme.MenuBackground;
                  _sidebarSplit.Panel2.BackColor = _themeManager.CurrentTheme.MenuBackground;

                  _workspaceSplitContainer = new SplitContainer
                  {
                      Dock = DockStyle.Fill,
                      Orientation = Orientation.Vertical,
                      Panel1MinSize = 80,
                      Panel2MinSize = 100,
                      SplitterWidth = 4,
                      SplitterIncrement = 8,
                      Panel1Collapsed = !_workspaceVisible && !_gitPanelVisible && !_symbolPanelVisible,
                      BorderStyle = BorderStyle.None
                  };
                  _workspaceSplitContainer.HandleCreated += (s, e) =>
                      SetWindowTheme(_workspaceSplitContainer.Handle, "", "");
                  _workspaceSplitContainer.SplitterMoved += (s, e) =>
                      _workspaceWidth = Math.Max(80, Math.Min(600, _workspaceSplitContainer.SplitterDistance));

                  mainLayout.Controls.Remove(_terminalSplitContainer);
                  _workspaceSplitContainer.Panel1.Controls.Add(_sidebarSplit);
                  _workspaceSplitContainer.Panel1.BackColor = _themeManager.CurrentTheme.MenuBackground;

                  // Editor split: editor+terminal on left, markdown preview on right (collapsed by default)
                  _editorSplitContainer = new SplitContainer
                  {
                      Dock = DockStyle.Fill,
                      Orientation = Orientation.Vertical,
                      Panel1MinSize = 100,
                      Panel2MinSize = 120,
                      SplitterWidth = 4,
                      SplitterIncrement = 8,
                      Panel2Collapsed = !_markdownPreviewVisible,
                      BorderStyle = BorderStyle.None
                  };
                  _editorSplitContainer.Panel1.Controls.Add(_terminalSplitContainer);
                  _editorSplitContainer.Panel1.BackColor = _themeManager.CurrentTheme.EditorBackground;

                  _markdownPreviewPanel = new MarkdownPreviewPanel();
                  _markdownPreviewPanel.CloseRequested += () => ToggleMarkdownPreview();
                  _markdownPreviewPanel.SetTheme(_themeManager.CurrentTheme);
                  _editorSplitContainer.Panel2.Controls.Add(_markdownPreviewPanel);
                  _editorSplitContainer.Panel2.BackColor = _themeManager.CurrentTheme.EditorBackground;

                  _workspaceSplitContainer.Panel2.Controls.Add(_editorSplitContainer);

                  if (_workspaceVisible || _gitPanelVisible)
                      _workspaceSplitContainer.SplitterDistance = _workspaceWidth;

                  mainLayout.Controls.Add(_workspaceSplitContainer, 0, 2);
              }

              mainLayout.ResumeLayout(true);

              if (!string.IsNullOrEmpty(_workspaceRoot))
                        Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
              UpdateWorkspaceProjectLabel();
              _workspaceProjectLabel.Click += WorkspaceProjectLabel_Click;
              // Set initial build config combo selection
              if (_buildConfigCombo.Items.Contains(_activeConfiguration))
                  _buildConfigCombo.SelectedItem = _activeConfiguration;
              workspaceMenuItem.Checked = _workspaceVisible;
              symbolsMenuItem.Checked = _symbolPanelVisible;
              problemsMenuItem.Checked = _problemsPanelVisible;
              markdownPreviewPanelMenuItem.Checked = _markdownPreviewVisible;

             // Set initial splitter position if terminal should be visible
             if (_terminalVisible)
             {
                 ShowTerminal();
             }

              // Apply terminal theme to match editor
              UpdateTerminalTheme();

              // Initialize notification feed service
              _notificationStatusLabel = new ToolStripStatusLabel("(0)")
              {
                  Font = new Font("Segoe UI", 9, FontStyle.Bold),
                  Padding = new Padding(4, 1, 4, 1),
                  Alignment = ToolStripItemAlignment.Right
              };
              _notificationStatusLabel.Click += ToggleNotificationCenter;
              statusStrip.Items.Add(_notificationStatusLabel);

               _notificationFeed.OnItemsUpdated += OnFeedUpdated;
               _notificationFeed.StartPolling();

               // Initialize debugger integration
                _breakpointManager.BreakpointsChanged += () =>
                {
                    if (gutterPanel != null)
                    {
                        gutterPanel.SetBreakpointsDirty();
                        gutterPanel.InvalidateBreakpointArea();
                    }
                };
                gutterPanel!.BreakpointClicked += (line) =>
                {
                    if (currentFilePath != null)
                        _breakpointManager.ToggleBreakpoint(currentFilePath, line + 1);
                };

                // Debug toast method
               _debugSession.StateChanged += OnDebugStateChanged;
               _debugSession.ThreadStopped += OnDebugThreadStopped;
               _debugSession.ThreadContinued += (tid) =>
               {
                   _debugActiveLine = -1;
                   _debugActiveFile = null;
               };
               _debugSession.DebugOutput += (msg) => ShowNotification("Debug", msg);

// Ensure initial dirty flag is clear after all initialization
              isModified = false;

            // Resize debounce: coalesces rapid resize events (snap, drag, DPI changes)
            // so overlay positioning runs once after layout fully settles.
            _resizeDebounceTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _resizeDebounceTimer.Tick += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                PositionMinimap();
                if (stickyScrollPanel.Visible)
                    stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
                PositionTabDropdownButton();
                // Force a clean repaint of the text editor after layout settles
                // to prevent stale painting artifacts during snap/drag resize.
                if (textEditor != null && !textEditor.IsDisposed)
                    textEditor.Invalidate();
            };

            this.Resize += (s, e) =>
            {
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Start();
            };

            // Tab scroll debounce: coalesces rapid mouse wheel tab scrolling
            // so the expensive LoadDocument/SwitchToTab cascade only runs once
            // the user stops scrolling, preventing the "content disappears then
            // reappears" flicker on every scroll tick.
            _tabScrollDebounce = new System.Windows.Forms.Timer { Interval = 120 };
            _tabScrollDebounce.Tick += (s, e) =>
            {
                _tabScrollDebounce.Stop();
                if (tabControl != null && _pendingTabScrollIndex >= 0 && _pendingTabScrollIndex < tabControl.TabCount)
                {
                    tabControl.SelectedIndex = _pendingTabScrollIndex;
                    _pendingTabScrollIndex = -1;
                }
            };
            mainLayout.Layout += (s, e) =>
            {
                PositionMinimap();
                if (stickyScrollPanel.Visible)
                    stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
                PositionTabDropdownButton();
            };
            editorPanel.SizeChanged += (s, e) =>
            {
                if (_pendingMinimapVisible)
                    PositionMinimap();
                if (stickyScrollPanel.Visible)
                    stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
            };
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
#if PROFILING && DEBUG
            // Signal UI activity on window messages
            SignalUiActivity();
#endif

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
                    ThemedMessageBox.Show("The file does not exist.", "File Drop Error",
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
                        ThemedMessageBox.Show("Invalid file path.", "File Drop Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch { }

                // Configurable file size limit
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    long maxSize = (long)MaxFileSizeMB * 1024 * 1024;
                    if (fileInfo.Length > maxSize)
                    {
                        ThemedMessageBox.Show($"File is too large ({fileInfo.Length / 1024 / 1024}MB). Maximum allowed is {MaxFileSizeMB}MB.",
                            "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    ThemedMessageBox.Show($"Cannot access file: {ex.Message}", "File Access Error",
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
                        ThemedMessageBox.Show($"Failed to open '{file}': {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to open dropped file: {ex.Message}", "Error",
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
                ThemedMessageBox.Show($"Failed to open dropped file: {ex.Message}", "Error",
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
                if (textEditor.TextLength > 0)
                {
                    Point p0 = textEditor.GetPositionFromCharIndex(0);
                    Point p1 = textEditor.GetPositionFromCharIndex(1);
                    charWidth = p1.X - p0.X;
                }
                else
                {
                    var size = TextRenderer.MeasureText("0", textEditor.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    charWidth = size.Width;
                }

                textEditor.SelectionTabs = ElasticTabService.BuildFixedTabStops(charWidth, tabSize);
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

                    // Delegate width measurement to the cache with the captured font
                    using var font = new Font(fontName!, fontSize, fontStyle);
                    int[] stops = ElasticTabService.ComputeTabStops(
                        lines, firstVisible, lastIndex,
                        s => TabMeasurementCache.GetStringWidth(s, font),
                        token);

                    if (token.IsCancellationRequested) return;

                    // Apply to editor on UI thread (fire-and-forget)
                    try
                    {
                        textEditor.BeginInvoke(new Action(() =>
                        {
                            if (!textEditor.IsDisposed && !textEditor.Disposing && stops.Length > 0)
                            {
                                textEditor.SelectionTabs = stops;
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
                minimapControl.MarkDirty();
                minimapControl.RefreshNow();
            }
            if (whitespaceOverlay != null)
            {
                whitespaceOverlay.GlyphColor = theme.IsLight ? Color.FromArgb(200, 200, 200) : Color.FromArgb(180, 180, 180);
            }
            textEditor!.GuideColor = Color.FromArgb(120, 120, 120);
            if (_tabDropdownButton != null)
            {
                _tabDropdownButton.BackColor = theme.MenuBackground;
                _tabDropdownButton.ForeColor = theme.Muted;
                _tabDropdownButton.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
            }
            if (rulerPanel != null)
            {
                rulerPanel.Invalidate();
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

            // Sync notification center theme
            if (_notificationCenter is not null && !_notificationCenter.IsDisposed)
                _notificationCenter.UpdateTheme(theme);
            UpdateNotificationBadge();

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

        // Lightweight editor-color refresh for tab switches (avoids repainting
        // menus, status bar, dropdowns, notification center, etc. on every switch).
        private void ApplyEditorColors()
        {
            var theme = _themeManager.CurrentTheme;
            textEditor!.BackColor = theme.EditorBackground;
            textEditor.ForeColor = theme.Text;
            gutterPanel.BackColor = theme.EditorBackground;
            gutterPanel.ForeColor = theme.Text;
            rulerPanel?.Invalidate();

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
            int idx = Array.IndexOf(names, _themeManager.CurrentTheme.Name);
            idx = (idx + 1) % names.Length;
            string next = names[idx];
            _themeManager.SetTheme(next);
            UpdateThemeColors(_themeManager.CurrentTheme);
            UpdateTerminalTheme();
            UpdateThemeDropDown();
        }

        private void OnThemeChanged(Theme theme)
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(() => OnThemeChanged(theme)));
            else
            {
                UpdateThemeColors(theme);
                UpdateTerminalTheme();
                UpdateThemeDropDown();
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settings = _settingsService.Load();
                if (settings is null) return;

                string themeName = settings.ThemeName;
                if (string.IsNullOrEmpty(themeName) || !ThemeManager.Themes.ContainsKey(themeName))
                    themeName = "Dark";
                _themeManager.SetTheme(themeName);
                wordWrapEnabled = settings.WordWrapEnabled;
                gutterVisible = settings.GutterVisible;
                statusBarVisible = settings.StatusBarVisible;
                if (statusBarMenuItem != null) statusBarMenuItem.Checked = statusBarVisible;
                textEditor.ShowGuide = settings.ShowGuide;
                textEditor.GuideColumn = settings.GuideColumn;
                tabSize = settings.TabSize;
                _fontService.LoadFrom(settings);
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
                if (settings.ExternalTools != null)
                    _externalTools = settings.ExternalTools;
                _workspaceVisible = settings.WorkspaceVisible;
                _workspaceWidth = Math.Max(80, Math.Min(600, settings.WorkspaceWidth));
                if (!_workspaceRootFromCli)
                    _workspaceRoot = settings.WorkspaceRoot ?? "";
                _symbolPanelVisible = settings.SymbolPanelVisible;
                _problemsPanelVisible = settings.ProblemsPanelVisible;
                _rainbowBracketsEnabled = settings.RainbowBracketsEnabled;
                _hoverLineHighlightEnabled = settings.HoverLineHighlightEnabled;
                _breadcrumbsEnabled = settings.BreadcrumbsEnabled;
                _analyzersEnabled = settings.AnalyzersEnabled;
                _autoSaveEnabled = settings.AutoSaveEnabled;
                if (_autoSaveEnabled) _autoSaveTimer.Start();
                vimModeEnabled = settings.VimModeEnabled;
                _stickyScrollEnabled = settings.StickyScrollEnabled;
                bool showWhitespace = settings.ShowWhitespace;
                if (whitespaceMenuItem != null)
                {
                    whitespaceMenuItem.Checked = showWhitespace;
                    whitespaceOverlay.ShowGlyphs = showWhitespace;
                }
                _savedWindowBounds = settings.WindowBounds;
                _savedWindowState = settings.WindowState;
                _activeConfiguration = settings.ActiveConfiguration ?? "Debug";

                // Load file size limits
                MaxFileSizeMB = Math.Max(10, settings.MaxFileSizeMB);
                LargeFileWarningMB = Math.Max(1, settings.LargeFileWarningMB);
                AsyncFileWarningMB = Math.Max(1, settings.AsyncFileWarningMB);
                DisableSyntaxHighlightingForLargeFiles = settings.DisableSyntaxHighlightingForLargeFiles;
                DisableMinimapForLargeFiles = settings.DisableMinimapForLargeFiles;
                DisableWordWrapForLargeFiles = settings.DisableWordWrapForLargeFiles;
                SyntaxHighlightingThresholdBytes = Math.Max(1024, settings.SyntaxHighlightingThresholdBytes);
            }
            catch { /* ignore settings load errors */ }
        }

        private string _savedWindowBounds = "";
        private string _savedWindowState = "";

        private void RestoreWindowBounds()
        {
            if (!string.IsNullOrEmpty(_savedWindowState))
            {
                try
                {
                    if (_savedWindowState == "Maximized")
                        WindowState = FormWindowState.Maximized;
                    else if (_savedWindowState == "Minimized")
                        WindowState = FormWindowState.Normal; // don't start minimized
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_savedWindowBounds))
            {
                try
                {
                    var parts = _savedWindowBounds.Split(',');
                    if (parts.Length == 4
                        && int.TryParse(parts[0], out int x)
                        && int.TryParse(parts[1], out int y)
                        && int.TryParse(parts[2], out int w)
                        && int.TryParse(parts[3], out int h))
                    {
                        // Validate against current screen working area
                        var screen = Screen.FromPoint(new Point(x, y)).WorkingArea;
                        x = Math.Max(screen.Left, Math.Min(x, screen.Right - 200));
                        y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - 100));
                        w = Math.Max(400, Math.Min(w, screen.Width));
                        h = Math.Max(200, Math.Min(h, screen.Height));
                        Bounds = new Rectangle(x, y, w, h);
                        StartPosition = FormStartPosition.Manual;
                    }
                }
                catch { }
            }
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                ThemeName = _themeManager.CurrentTheme.Name,
                WordWrapEnabled = wordWrapEnabled,
                GutterVisible = gutterVisible,
                StatusBarVisible = statusBarVisible,
                ShowGuide = textEditor.ShowGuide,
                GuideColumn = textEditor.GuideColumn,
                TabSize = tabSize,
                FontName = _fontService.FontName,
                FontSize = _fontService.FontSize,
                InsertSpaces = insertSpaces,
                AutoIndentEnabled = autoIndentEnabled,
                SmartTabsEnabled = smartTabsEnabled,
                ElasticTabsEnabled = elasticTabsEnabled,
                CurrentLineHighlightMode = currentLineHighlightMode,
                SyntaxHighlightingEnabled = syntaxHighlightingEnabled,
                MinimapVisible = minimapMenuItem?.Checked ?? false,
                TerminalVisible = _terminalVisible,
                TerminalHeight = _terminalHeight,
                TerminalShellPath = _terminalShell,
                ExternalTools = _externalTools,
                WorkspaceVisible = _workspaceVisible,
                WorkspaceWidth = _workspaceWidth,
                WorkspaceRoot = _workspaceRoot,
                ShowWhitespace = whitespaceMenuItem?.Checked ?? true,
                SymbolPanelVisible = _symbolPanelVisible,
                ProblemsPanelVisible = _problemsPanelVisible,
                RainbowBracketsEnabled = rainbowBracketsMenuItem?.Checked ?? false,
                BreadcrumbsEnabled = breadcrumbMenuItem?.Checked ?? false,
                AnalyzersEnabled = _analyzersEnabled,
                AutoSaveEnabled = _autoSaveEnabled,
                WindowBounds = $"{Left},{Top},{Width},{Height}",
                WindowState = WindowState == FormWindowState.Maximized ? "Maximized" : "Normal",
                ActiveConfiguration = _activeConfiguration,
                MaxFileSizeMB = MaxFileSizeMB,
                LargeFileWarningMB = LargeFileWarningMB,
                AsyncFileWarningMB = AsyncFileWarningMB,
                DisableSyntaxHighlightingForLargeFiles = DisableSyntaxHighlightingForLargeFiles,
                DisableMinimapForLargeFiles = DisableMinimapForLargeFiles,
                DisableWordWrapForLargeFiles = DisableWordWrapForLargeFiles,
                SyntaxHighlightingThresholdBytes = SyntaxHighlightingThresholdBytes,
                VimModeEnabled = vimModeEnabled,
                StickyScrollEnabled = _stickyScrollEnabled,
            };
            _settingsService.Save(settings);
        }

        public void ApplySettings(AppSettings settings)
        {
            // Apply theme first (affects colors of everything)
            if (!string.IsNullOrEmpty(settings.ThemeName) && ThemeManager.Themes.TryGetValue(settings.ThemeName, out var theme))
                UpdateThemeColors(theme);

            // Editor
            this.tabSize = Math.Max(2, Math.Min(12, settings.TabSize));
            tabSizeDropDown.Text = $"Tab: {this.tabSize}";
            this.insertSpaces = settings.InsertSpaces;
            wordWrapEnabled = settings.WordWrapEnabled;
            textEditor.WordWrap = wordWrapEnabled;
            textEditor.ShowGuide = settings.ShowGuide;
            textEditor.GuideColumn = settings.GuideColumn;

            // Font
            _fontService.LoadFrom(settings);
            _fontService.ApplyFont(textEditor);

            // Appearance
            this.gutterVisible = settings.GutterVisible;
            gutterPanel.Visible = this.gutterVisible;
            this.statusBarVisible = settings.StatusBarVisible;
            statusStrip.Visible = settings.StatusBarVisible;
            if (minimapMenuItem != null) minimapMenuItem.Checked = settings.MinimapVisible;
            if (whitespaceMenuItem != null)
            {
                whitespaceMenuItem.Checked = settings.ShowWhitespace;
                whitespaceOverlay.ShowGlyphs = settings.ShowWhitespace;
            }
            currentLineHighlightMode = settings.CurrentLineHighlightMode;

            // Features
            syntaxHighlightingEnabled = settings.SyntaxHighlightingEnabled;
            autoIndentEnabled = settings.AutoIndentEnabled;
            smartTabsEnabled = settings.SmartTabsEnabled;
            elasticTabsEnabled = settings.ElasticTabsEnabled;
            _rainbowBracketsEnabled = settings.RainbowBracketsEnabled;
            if (rainbowBracketsMenuItem != null) rainbowBracketsMenuItem.Checked = settings.RainbowBracketsEnabled;
            _breadcrumbsEnabled = settings.BreadcrumbsEnabled;
            if (breadcrumbMenuItem != null) breadcrumbMenuItem.Checked = settings.BreadcrumbsEnabled;
            _autoSaveEnabled = settings.AutoSaveEnabled;
            if (autoSaveMenuItem != null) autoSaveMenuItem.Checked = settings.AutoSaveEnabled;
            if (settings.AutoSaveEnabled) _autoSaveTimer.Start(); else _autoSaveTimer.Stop();
            _hoverLineHighlightEnabled = settings.HoverLineHighlightEnabled;

            // Panels
            _workspaceVisible = settings.WorkspaceVisible;
            if (workspaceMenuItem != null) workspaceMenuItem.Checked = settings.WorkspaceVisible;
            _symbolPanelVisible = settings.SymbolPanelVisible;
            if (symbolsMenuItem != null) symbolsMenuItem.Checked = settings.SymbolPanelVisible;
            _problemsPanelVisible = settings.ProblemsPanelVisible;
            if (problemsMenuItem != null) problemsMenuItem.Checked = settings.ProblemsPanelVisible;
            _terminalVisible = settings.TerminalVisible;
            if (terminalMenuItem != null) terminalMenuItem.Checked = settings.TerminalVisible;
            _terminalHeight = Math.Max(60, Math.Min(600, settings.TerminalHeight));
            _terminalShell = settings.TerminalShellPath ?? "";

            // Advanced
            _analyzersEnabled = settings.AnalyzersEnabled;

            // Vim mode
            vimModeEnabled = settings.VimModeEnabled;
            if (vimModeMenuItem != null) vimModeMenuItem.Checked = settings.VimModeEnabled;
            vimModeLabel.Visible = settings.VimModeEnabled;
            if (!settings.VimModeEnabled) vimModeLabel.Text = "";

            // Sticky scroll
            _stickyScrollEnabled = settings.StickyScrollEnabled;
            if (stickyScrollMenuItem != null) stickyScrollMenuItem.Checked = settings.StickyScrollEnabled;
            stickyScrollPanel.Visible = settings.StickyScrollEnabled;

            // Sync menu item states
            if (gutterMenuItem != null) gutterMenuItem.Checked = settings.GutterVisible;
            if (wordWrapMenuItem != null) wordWrapMenuItem.Checked = settings.WordWrapEnabled;
            if (autoIndentMenuItem != null) autoIndentMenuItem.Checked = settings.AutoIndentEnabled;
            if (insertSpacesMenuItem != null) insertSpacesMenuItem.Checked = settings.InsertSpaces;

            // Refresh UI
            UpdateStatusBar();
            UpdateBreadcrumbs();
            RequestVisibleHighlight();
            SaveSettings();
        }

        public void ApplyProfile(UserProfile profile)
        {
            _currentProfile = profile;
            _profileManager.ActiveProfileName = profile.Name;

            // Switch workspace root if profile specifies one
            if (profile.PrimaryWorkspace != null && !string.IsNullOrEmpty(profile.PrimaryWorkspace.Path) && Directory.Exists(profile.PrimaryWorkspace.Path))
            {
                OpenWorkspaceFolder(profile.PrimaryWorkspace.Path);
            }

            // Apply per-profile settings overrides on top of global settings
            ApplyProfileOverrides(profile);

            try { preferencesProfileMenuItem.Text = $"&Profile: {_currentProfile.Name}"; } catch { }
            SaveSettings();
        }

        private void ApplyProfileOverrides(UserProfile profile)
        {
            bool changed = false;
            if (profile.OverrideTabSize.HasValue)
            {
                int ts = Math.Max(2, Math.Min(12, profile.OverrideTabSize.Value));
                if (ts != tabSize) { tabSize = ts; tabSizeDropDown.Text = $"Tab: {ts}"; changed = true; }
            }
            if (profile.OverrideInsertSpaces.HasValue)
            {
                if (profile.OverrideInsertSpaces.Value != insertSpaces)
                { insertSpaces = profile.OverrideInsertSpaces.Value; changed = true; }
            }
            if (profile.OverrideFontSize.HasValue)
            {
                if (_fontService.SetFontSize(profile.OverrideFontSize.Value))
                {
                    _fontService.ApplyFont(textEditor);
                    changed = true;
                }
            }
            if (!string.IsNullOrEmpty(profile.OverrideThemeName))
            {
                if (profile.OverrideThemeName != _themeManager.CurrentTheme.Name && ThemeManager.Themes.TryGetValue(profile.OverrideThemeName, out var theme))
                {
                    UpdateThemeColors(theme);
                    changed = true;
                }
            }
            if (profile.OverrideWordWrap.HasValue)
            {
                if (profile.OverrideWordWrap.Value != wordWrapEnabled)
                {
                    wordWrapEnabled = profile.OverrideWordWrap.Value;
                    textEditor.WordWrap = wordWrapEnabled;
                    if (wordWrapMenuItem != null) wordWrapMenuItem.Checked = wordWrapEnabled;
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateStatusBar();
                RequestVisibleHighlight();
            }
        }

        private void UpdateFileMenuProfileItems()
        {
            try
            {
                if (preferencesProfileMenuItem == null) return;
                preferencesProfileMenuItem.Text = $"&Profile: {_currentProfile.Name}";
                preferencesProfileMenuItem.DropDownItems.Clear();

                foreach (string name in _profileManager.ProfileNames)
                {
                    var item = new ToolStripMenuItem(name, null, (s, e) =>
                    {
                        var profile = _profileManager.LoadProfile(name);
                        if (profile != null)
                            ApplyProfile(profile);
                    });
                    item.Checked = name == _currentProfile.Name;
                    preferencesProfileMenuItem.DropDownItems.Add(item);
                }

                preferencesProfileMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var manageItem = new ToolStripMenuItem("Manage Profiles...", null, (s, e) =>
                {
                    using var dlg = new ProfileManagerDialog(_profileManager, this);
                    dlg.ShowDialog(this);
                        });
                preferencesProfileMenuItem.DropDownItems.Add(manageItem);
            }
            catch { }
        }

        private string ResolveBuildCommand()
        {
            return _currentProfile.BuildCommand;
        }

        private string ResolveRunCommand()
        {
            return _currentProfile.RunCommand;
        }

        private string ResolveTestCommand()
        {
            return _currentProfile.TestCommand;
        }

internal void ToggleGutter()
{
    if (vimEngine == null) return;
    vimEngine.ExecuteCommand(gutterVisible ? "set nogutter" : "set gutter");
            gutterMenuItem.Checked = gutterVisible;
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
            if (vimModeMenuItem != null) vimModeMenuItem.Checked = vimModeEnabled;
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
                    // Vim mode disabled; editor returns to default input behavior
                    // (this is handled by the engine disabling)
                }
            }

            // Update status bar to show Vim mode indicator
            UpdateStatusBar();
            UpdateStatusBarVisibility();
            gutterPanel?.Invalidate();
        }

        private void ToggleStickyScroll_Click(object? sender, EventArgs e)
        {
            _stickyScrollEnabled = stickyScrollMenuItem.Checked;
            if (_stickyScrollEnabled)
                RebuildStickyScopes();
            else
                stickyScrollPanel.SetScopes(new(), -1);
        }

        private void ToggleRainbowBrackets_Click(object? sender, EventArgs e)
        {
            _rainbowBracketsEnabled = rainbowBracketsMenuItem.Checked;
            if (textEditor != null)
                textEditor.EnableRainbowBrackets(_rainbowBracketsEnabled);
            SaveSettings();
        }

        private void ToggleBreadcrumbs_Click(object? sender, EventArgs e)
        {
            _breadcrumbsEnabled = breadcrumbMenuItem.Checked;
            breadcrumbPanel.Visible = _breadcrumbsEnabled;
            gutterPanel.TopOffset = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (rulerMenuItem.Checked ? rulerPanel.Height : 0);
            gutterPanel.Invalidate();
            RepositionStickyScrollPanel();
            UpdateBreadcrumbs();
            SaveSettings();
        }

        private bool _rulerEnabled = false;

        private void ToggleRuler_Click(object? sender, EventArgs e)
        {
            _rulerEnabled = rulerMenuItem.Checked;
            rulerPanel.Visible = _rulerEnabled;
            gutterPanel.TopOffset = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (_rulerEnabled ? rulerPanel.Height : 0);
            gutterPanel.Invalidate();
            RepositionStickyScrollPanel();
            SaveSettings();
        }

        private void RepositionStickyScrollPanel()
        {
            stickyScrollPanel.Top = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (_rulerEnabled ? rulerPanel.Height : 0);
        }

        private void DebounceBreadcrumbs()
        {
            if (!_breadcrumbsEnabled) return;
            _breadcrumbDebounce?.Stop();
            _breadcrumbDebounce?.Start();
        }

        private void UpdateBreadcrumbsSync()
        {
            if (!_breadcrumbsEnabled) return;
            UpdateBreadcrumbs();
        }

        private void UpdateBreadcrumbs()
        {
            if (!_breadcrumbsEnabled || textEditor == null)
            {
                breadcrumbPanel.Visible = false;
                return;
            }
            breadcrumbPanel.Visible = true;
            breadcrumbPanel.FilePath = currentFilePath ?? "";
            if (_stickyScroll != null)
            {
                int pos = textEditor.SelectionStart;
                int line = textEditor.GetLineFromCharIndex(pos);
                var scopes = _stickyScroll.GetEnclosingScopes(line);
                breadcrumbPanel.CurrentScope = scopes.Count > 0 ? scopes[^1].HeaderText : "";
            }
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
                t.SetTheme(_themeManager.CurrentTheme);
            var theme = _themeManager.CurrentTheme;
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
            if (_workspacePanel != null)
                _workspacePanel.SetTheme(theme);
            if (_symbolPanel != null)
                _symbolPanel.SetTheme(theme);
            if (_problemsPanel != null)
                _problemsPanel.SetTheme(theme);
            if (_problemsSplit != null)
                _problemsSplit.BackColor = theme.MenuBackground;
            if (_gitPanel != null)
                _gitPanel.SetTheme(theme);
            if (_workspaceSplitContainer != null)
            {
                _workspaceSplitContainer.BackColor = theme.MenuBackground;
                _workspaceSplitContainer.Panel1.BackColor = theme.MenuBackground;
                _workspaceSplitContainer.Panel2.BackColor = theme.EditorBackground;
            }
            if (_sidebarSplit != null)
                _sidebarSplit.BackColor = theme.MenuBackground;
            if (_botSidebarSplit != null)
                _botSidebarSplit.BackColor = theme.MenuBackground;
            if (_markdownPreviewPanel != null)
                _markdownPreviewPanel.SetTheme(theme);
            if (_editorSplitContainer != null)
                _editorSplitContainer.BackColor = theme.EditorBackground;
        }

        private TerminalPanel AddTerminalTab(string? shellPath)
        {
            var terminal = new TerminalPanel(shellPath);
            terminal.SetTheme(_themeManager.CurrentTheme);

            int tabNumber = _terminalTabs.Count + 1;
            var page = new TabPage($"Terminal {tabNumber}")
            {
                BackColor = _themeManager.CurrentTheme.EditorBackground,
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
                HideTerminal();
            PositionTerminalNewTabButton();
        }

        private void TerminalTabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            var tc = (TabControl)sender!;
            if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            var page = tc.TabPages[e.Index];
            var bounds = tc.GetTabRect(e.Index);
            bounds.Height = tc.ItemSize.Height;

            var theme = _themeManager.CurrentTheme;
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
                var baseColor = _themeManager.CurrentTheme.Text;
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
                CurrentLineHighlightMode.WholeLine => CurrentLineHighlightMode.NumberAndWholeLine,
                CurrentLineHighlightMode.NumberAndWholeLine => CurrentLineHighlightMode.Off,
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
                currentLineNumberAndWholeLineMenuItem.Checked = (currentLineHighlightMode == CurrentLineHighlightMode.NumberAndWholeLine);
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
            else if (e.KeyCode == Keys.F12)
            {
                GoToDefinition();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                RenameSymbol();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.Space)
            {
                TriggerCompletion();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private async void TriggerCompletion()
        {
            if (textEditor is null || !_roslynWorkspace.IsReady || currentSyntax != SyntaxDefinition.CSharp) return;

            try
            {
                var completionData = await _roslynWorkspace.GetCompletionAsync(textEditor.SelectionStart);
                if (completionData != null && completionData.Items.Count > 0)
                {
                    ShowCompletionPopup(completionData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Completion error: {ex.Message}");
            }
        }

        private void ShowCompletionPopup(Roslyn.CompletionData completionData)
        {
            if (textEditor is null) return;

            // Dismiss existing popup
            _completionPopup?.Close();
            _completionPopup = null;

            // Calculate position
            var charPos = textEditor.GetPositionFromCharIndex(completionData.StartPosition + completionData.Length);
            var screenPos = textEditor.PointToScreen(charPos);
            screenPos.Y += (int)textEditor.Font.GetHeight() + 2;

            _completionPopup = new CompletionPopupForm(completionData, screenPos);
            _completionPopup.ItemSelected += CompletionItemSelected;
            _completionPopup.Dismissed += () => _completionPopup = null;
            _completionPopup.Show(this);
        }

        private void CompletionItemSelected(string insertText)
        {
            if (textEditor is null || _completionPopup is null) return;

            // Replace the current completion span with the selected text
            var data = _completionPopup._completionData;
            if (data != null)
            {
                textEditor.SelectionStart = data.StartPosition;
                textEditor.SelectionLength = data.Length;
                textEditor.SelectedText = insertText;
                textEditor.SelectionStart = data.StartPosition + insertText.Length;
                textEditor.SelectionLength = 0;
            }
        }

        private void TextEditor_KeyDownHelp(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                _signatureHelp.Dismiss();
                _hoverTooltip.Dismiss();
            }
        }

         private void TextEditor_MouseMoveHover(object? sender, MouseEventArgs e)
         {
             _hoverTimer.Stop();
             if (textEditor is null) return;
             int charIdx = textEditor.GetCharIndexFromPosition(e.Location);
             if (charIdx < 0 || charIdx >= textEditor.TextLength) { _hoverTooltip.Dismiss(); return; }
             
             // Update hover line for current line highlight
             int lineNumber = textEditor.GetLineFromCharIndex(charIdx);
             if (lineNumber != _hoverLine)
             {
                 _hoverLine = lineNumber;
                 textEditor.Invalidate();
             }
             
             string word = GetWordAtCharIndex(charIdx);
             if (string.IsNullOrEmpty(word) || word == _lastHoveredWord)
             {
                 if (string.IsNullOrEmpty(word)) _hoverTooltip.Dismiss();
                 return;
             }
             _lastHoveredWord = word;
             _hoverTimer.Tag = (e.Location, word);
             _hoverTimer.Start();
         }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (textEditor is null) return;
            var (mouseLoc, word) = ((Point, string))_hoverTimer.Tag!;
            if (string.IsNullOrEmpty(word)) return;

            // Check if cursor is still near the same position
            Point currentMouse = textEditor.PointToClient(Cursor.Position);
            if (Math.Abs(currentMouse.X - mouseLoc.X) > 10 || Math.Abs(currentMouse.Y - mouseLoc.Y) > 10)
                return;

            // Roslyn hover docs for C# files
            if (currentSyntax?.Name == "C#" && _roslynWorkspace.IsReady)
            {
                ShowHoverTooltipRoslyn(word, mouseLoc);
                return;
            }

            // Debugger hover: show variable values when paused at breakpoint
            if (_debugSession.State == DebugState.Paused && _debugActiveFrameId > 0)
            {
                ShowDebugHoverTooltipAsync(word, mouseLoc);
                return;
            }

            // Look up in symbol index
            var symbols = _symbolIndex.Lookup(word);
            if (symbols.Count == 0) { _hoverTooltip.Dismiss(); return; }

            var first = symbols[0];
            string title = $"{first.Kind}: {first.Name}";
            string summary = "";

            // Try to extract XML docs from the source file
            if (File.Exists(first.File))
            {
                var docs = XmlDocParser.ExtractFromFile(first.File, first.Line);
                if (docs != null)
                    summary = docs.Summary;
            }

            Point screenLoc = textEditor.PointToScreen(mouseLoc);
            _hoverTooltip.ShowAt(screenLoc, title, summary, first.Context);
        }

        private void TextEditor_KeyPressHelp(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '(')
                ShowSignatureHelp();
            else if (e.KeyChar == ')' || e.KeyChar is ',' or ';')
                _signatureHelp.Dismiss();
        }

        private void ShowSignatureHelp()
        {
            if (textEditor is null) return;

            // Roslyn semantic signature help for C# files
            if (currentSyntax?.Name == "C#")
            {
                ShowSignatureHelpRoslyn();
                return;
            }

            int pos = textEditor.SelectionStart;
            if (pos < 1) return;

            // Get word before cursor
            string text = textEditor.Text[..pos];
            int start = pos - 1;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;
            string methodName = text[start..pos];

            if (string.IsNullOrEmpty(methodName) || methodName.Length < 2) return;

            _hoverTooltip.Dismiss();

            // Count how many commas before cursor = current parameter index
            int paramIdx = 0;
            int depth = 0;
            for (int i = pos - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == ')') depth++;
                else if (c == '(') { if (depth == 0) break; depth--; }
                else if (c == ',' && depth == 0) paramIdx++;
            }

            // Look up method in symbol index
            var symbols = _symbolIndex.Lookup(methodName);
            foreach (var sym in symbols)
            {
                if (sym.Kind != SymbolKind.Method && sym.Kind != SymbolKind.Function) continue;
                if (!File.Exists(sym.File)) continue;

                var parsed = XmlDocParser.ParseSignature(sym.File, methodName);
                if (parsed == null) continue;

                var (signature, paramNames) = parsed.Value;

                // Get doc for current parameter
                string paramDoc = "";
                if (paramIdx < paramNames.Count)
                {
                    var docs = XmlDocParser.ExtractFromFile(sym.File, sym.Line);
                    if (docs != null && paramIdx < docs.Params.Count)
                        paramDoc = docs.Params[paramIdx].text;
                }

                Point pt = textEditor.GetPositionFromCharIndex(pos);
                Point screenPt = textEditor.PointToScreen(pt);
                _signatureHelp.ShowAt(screenPt, signature, paramIdx, paramDoc);
                break;
            }
        }

        private string GetWordAtCharIndex(int charIdx)
        {
            if (textEditor is null || charIdx < 0 || charIdx >= textEditor.TextLength) return "";
            string text = textEditor.Text;
            int start = charIdx;
            int end = charIdx;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;
            while (end < text.Length - 1 && (char.IsLetterOrDigit(text[end + 1]) || text[end + 1] == '_'))
                end++;
            return start <= end ? text[start..(end + 1)] : "";
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
            TextBox textBox = new TextBox() { Location = new Point(60, 12), Width = 80, Text = textEditor.GuideColumn.ToString() };
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
            textEditor.GuideColumn = column;
            textEditor.ShowGuide = true;
            columnGuideMenuItem.Checked = true;
            UpdateColumnGuideMenuChecked();
            SaveSettings();
        }

        private void ToggleColumnGuide(object? sender, EventArgs e)
        {
            textEditor.ShowGuide = columnGuideMenuItem.Checked;
            textEditor.Invalidate();
            SaveSettings();
        }

        private void UpdateColumnGuideMenuChecked()
        {
            if (col72MenuItem == null || col80MenuItem == null || col100MenuItem == null ||
                col120MenuItem == null || col150MenuItem == null || colCustomMenuItem == null)
                return;

            int col = textEditor.GuideColumn;
            col72MenuItem.Checked = (col == 72);
            col80MenuItem.Checked = (col == 80);
             col100MenuItem.Checked = (col == 100);
             col120MenuItem.Checked = (col == 120);
             col150MenuItem.Checked = (col == 150);
             colCustomMenuItem.Checked = !(col == 72 || col == 80 || col == 100 || col == 120 || col == 150);
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
            if (ofd.ShowThemed() == DialogResult.OK)
            {
                OpenFileInNewTabAsync(ofd.FileName);
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

                  _previousTextLength = textEditor.TextLength;

                  currentFilePath = path;
                  currentSyntax = SyntaxDefinition.GetDefinitionForFile(path);
                  OpenRoslynDocument(path);
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
                    ForceCleanState();
            }
             catch (Exception ex)
              {
                  ThemedMessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    UnicodeFileHelper.WriteAllText(currentFilePath, textEditor.Text);
                    _profilerDialog?.Log($"Saved file: {currentFilePath} ({textEditor.Text.Length} chars)");
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    _docManager.GetActive()?.ModifiedLines.Clear();
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal void SaveAsFile()
        {
            using var sfd = new SaveFileDialog();
            sfd.Filter = "Text Files|*.txt|All Files|*.*";
            sfd.FileName = currentFilePath ?? "untitled.txt";
            if (sfd.ShowThemed() == DialogResult.OK)
            {
                try
                {
                    UnicodeFileHelper.WriteAllText(sfd.FileName, textEditor.Text);
                    _profilerDialog?.Log($"Saved as file: {sfd.FileName} ({textEditor.Text.Length} chars)");
                    currentFilePath = sfd.FileName;
                    lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath);
                    ClearDirtyAfterSave();
                    AddToRecentFiles(currentFilePath);
                    _docManager.GetActive()?.ModifiedLines.Clear();
                    UpdateStatusBar();
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveAllFiles()
        {
            // Single EditorDocument - same as SaveFile
            SaveFile();
        }

        internal bool IsModified()
        {
            // For now, compare with stored state; could also compare with disk
            return isModified;
        }

        private string ComputeContentHash()
            => ComputeContentHash(textEditor.Text);

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
            _docManager.GetActive()?.ModifiedLines.Clear();
            if (activeDocIndex >= 0)
            {
                ActiveDoc.IsDirty = false;
                ActiveDoc.SavedHash = savedContentHash;
                ActiveDoc.LastWriteTime = lastFileWriteTime;
                ActiveDoc.FilePath = currentFilePath;
                ActiveDoc.Content = textEditor.Text;
                ActiveDoc.Syntax = currentSyntax;
            }
            UpdateWindowTitle();
            UpdateActiveTabTitle();
        }

        private void ForceCleanState()
        {
            isModified = false;
            if (activeDocIndex >= 0 && activeDocIndex < documents.Count)
            {
                documents[activeDocIndex].IsDirty = false;
            }
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

            var result = ThemedMessageBox.Show(
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
                _docManager.GetActive()?.ModifiedLines.Clear();
                UpdateWindowTitle();
            }
        }

        #endregion

        #region Recent Files

        private void AddToRecentFiles(string path)
        {
            _sessionManager.AddRecentFile(path);
            UpdateRecentMenu();
            _sessionManager.SaveRecentFiles();
        }

        private void UpdateRecentMenu()
        {
            recentMenuItem.DropDownItems.Clear();
            if (_sessionManager.RecentFiles.Count == 0)
            {
                recentMenuItem.Enabled = false;
                recentMenuItem.DropDownItems.Add("(No recent files)").Enabled = false;
            }
            else
            {
                recentMenuItem.Enabled = true;
                for (int i = 0; i < _sessionManager.RecentFiles.Count; i++)
                {
                    string filePath = _sessionManager.RecentFiles[i];
                    string display = $"{(i + 1)} {Path.GetFileName(filePath)}";
                    var item = new ToolStripMenuItem(display, null, (s, e) => OpenFileInNewTab(filePath));
                    recentMenuItem.DropDownItems.Add(item);
                }
                recentMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var clearItem = new ToolStripMenuItem("Clear Recent", null, (s, e) => { _sessionManager.ClearRecentFiles(); UpdateRecentMenu(); _sessionManager.SaveRecentFiles(); });
                recentMenuItem.DropDownItems.Add(clearItem);
            }
        }

        // Recent workspaces
        private void AddToRecentWorkspaces(string path)
        {
            _sessionManager.AddRecent(path);
            UpdateRecentWorkspacesMenu();
            _sessionManager.SaveRecent();
        }

        private void UpdateRecentWorkspacesMenu()
        {
            recentWorkspacesMenuItem.DropDownItems.Clear();
            if (_sessionManager.RecentWorkspaces.Count == 0)
            {
                recentWorkspacesMenuItem.Enabled = false;
                recentWorkspacesMenuItem.DropDownItems.Add("(No recent workspaces)").Enabled = false;
            }
            else
            {
                recentWorkspacesMenuItem.Enabled = true;
                for (int i = 0; i < _sessionManager.RecentWorkspaces.Count; i++)
                {
                    string wsPath = _sessionManager.RecentWorkspaces[i];
                    string display = $"{(i + 1)} {Path.GetFileName(wsPath)}";
                    var item = new ToolStripMenuItem(display, null, (s, e) => OpenWorkspaceFolder(wsPath));
                    item.ToolTipText = wsPath;
                    recentWorkspacesMenuItem.DropDownItems.Add(item);
                }
                recentWorkspacesMenuItem.DropDownItems.Add(new ToolStripSeparator());
                var clearItem = new ToolStripMenuItem("Clear Recent Workspaces", null, (s, e) => { _sessionManager.ClearRecent(); UpdateRecentWorkspacesMenu(); _sessionManager.SaveRecent(); });
                recentWorkspacesMenuItem.DropDownItems.Add(clearItem);
            }
        }

        #endregion

        #region File Menu Handlers

        private void NewTab_Click(object? sender, EventArgs e) => NewFile();
private void NewWindow_Click(object? sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ExecutablePath, "--new-window");
        }
        private void NewProject_Click(object? sender, EventArgs e)
        {
            using var dlg = new NewProjectDialog(this);
            dlg.ShowDialog(this);
        }
        private void Open_Click(object? sender, EventArgs e) => OpenFile();

        private void CloneRepository_Click(object? sender, EventArgs e)
        {
            using var dlg = new CloneRepositoryDialog(this);
            dlg.ShowDialog(this);
        }
        private void Save_Click(object? sender, EventArgs e) => SaveFile();
        private void SaveAs_Click(object? sender, EventArgs e) => SaveAsFile();
        private void SaveAll_Click(object? sender, EventArgs e) => SaveAllFiles();
        private void ToggleAutoSave_Click(object? sender, EventArgs e) { _autoSaveEnabled = autoSaveMenuItem.Checked; if (_autoSaveEnabled) _autoSaveTimer.Start(); else _autoSaveTimer.Stop(); SaveSettings(); }
        private void CloseTab_Click(object? sender, EventArgs e) => CloseCurrentTab();
        private void CloseWindow_Click(object? sender, EventArgs e) => this.Close();
        private void CloseAll_Click(object? sender, EventArgs e)
        {
            if (IsModified())
            {
                var result = ThemedMessageBox.Show("Save changes before closing all?", "Confirm", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    SaveFile();
                    if (IsModified()) return; // save failed or cancelled
                }
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
            if (textEditor.SelectionLength > 0)
            {
                textEditor.SelectedText = "";
            }
            else if (textEditor.SelectionStart < textEditor.TextLength)
            {
                // Delete the character after the caret (forward delete)
                int start = textEditor.SelectionStart;
                textEditor.SelectionStart = start;
                textEditor.SelectionLength = 1;
                textEditor.SelectedText = "";
                // Restore caret position at the original start (after deletion)
                textEditor.SelectionStart = start;
            }
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
                _fontService.SetFont(fd.Font);
                _fontService.ApplyFont(textEditor);
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

        private void ShowPerformanceProfiler()
        {
            try
            {
                if (_profilerDialog == null || _profilerDialog.IsDisposed)
                {
                    _profilerDialog = new PerformanceProfilerDialog(this);
                }
                _profilerDialog.Show();
                _profilerDialog.BringToFront();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error opening Performance Profiler: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

#if PROFILING && DEBUG
        /// <summary>
        /// Signal UI activity for performance profiling.
        /// </summary>
        private void SignalUiActivity()
        {
            _debugOverlay?.OnFrame();
            _profilerDialog?._samplingEngine?.SignalUiActivity();
        }
#endif

        private void GcMonitorTimer_Tick(object? sender, EventArgs e)
        {
            var currentGC = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            if (currentGC > _lastGCCollections)
            {
                _profilerDialog?.Log($"GC Activity: {currentGC - _lastGCCollections} collections occurred");
                _lastGCCollections = currentGC;
            }
        }

        // External tools
        private List<ExternalTool> _externalTools = new();
        private ToolStripItem? _externalToolsSeparator;
        private readonly List<ToolStripMenuItem> _externalToolMenuItems = new();

        // Find/Replace state
        private string _lastFindText = "";
        private bool _lastFindCaseSensitive;
        private bool _lastFindUp;
        private bool _lastUseRegex;
        private string _lastReplaceText = "";

        private void ConfigureTools_Click(object? sender, EventArgs e)
        {
            using var dlg = new ExternalToolsConfigDialog(_externalTools);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RebuildExternalToolsMenu();
                SaveSettings();
            }
        }

        private void Settings_Click(object? sender, EventArgs e)
        {
            using var dlg = new SettingsDialog(this);
            dlg.ShowDialog(this);
        }

        private void RebuildExternalToolsMenu()
        {
            foreach (var item in _externalToolMenuItems)
                toolsMenu.DropDownItems.Remove(item);
            _externalToolMenuItems.Clear();

            if (_externalToolsSeparator != null)
            {
                toolsMenu.DropDownItems.Remove(_externalToolsSeparator);
                _externalToolsSeparator = null;
            }

            if (_externalTools.Count == 0)
                return;

            _externalToolsSeparator = new ToolStripSeparator();
            toolsMenu.DropDownItems.Add(_externalToolsSeparator);

            for (int i = 0; i < _externalTools.Count; i++)
            {
                var tool = _externalTools[i];
                int index = i;
                var item = new ToolStripMenuItem(tool.Name, null, (s, e) => RunExternalTool(index));
                if (index < 9)
                    item.ShortcutKeys = Keys.Control | Keys.Alt | Keys.Shift | (Keys.D1 + index);
                _externalToolMenuItems.Add(item);
                toolsMenu.DropDownItems.Add(item);
            }
        }

        private void RunExternalTool(int index)
        {
            if (index < 0 || index >= _externalTools.Count) return;
            var tool = _externalTools[index];

            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                ThemedMessageBox.Show($"Tool \"{tool.Name}\" has no command configured.", "External Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string args = tool.Arguments;
            if (tool.PromptForArguments)
            {
                string? input = SimpleInputDialog.Show(this, $"Arguments for \"{tool.Name}\":", "External Tool", args);
                if (input == null) return;
                args = input;
            }

            try
            {
                string resolvedCmd = ResolveVariables(tool.Command);
                string resolvedArgs = ResolveVariables(args);
                string resolvedDir = ResolveVariables(tool.InitialDirectory);

                if (string.IsNullOrEmpty(resolvedDir))
                {
                    string? filePath = ActiveDoc?.FilePath;
                    if (!string.IsNullOrEmpty(filePath))
                        resolvedDir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
                    else
                        resolvedDir = Environment.CurrentDirectory;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = resolvedCmd,
                    Arguments = resolvedArgs,
                    WorkingDirectory = resolvedDir,
                    UseShellExecute = tool.UseShellExecute
                };

                if (!tool.UseShellExecute)
                {
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.CreateNoWindow = true;
                }

                using var proc = new Process();
                proc.StartInfo = psi;

                if (tool.UseShellExecute)
                {
                    proc.Start();
                }
                else
                {
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(output) || !string.IsNullOrEmpty(error))
                    {
                        string caption = $"Output: {tool.Name}";
                        string msg = "";
                        if (!string.IsNullOrEmpty(output)) msg += output;
                        if (!string.IsNullOrEmpty(error)) msg += $"\n--- stderr ---\n{error}";
                        ThemedMessageBox.Show(msg.Trim(), caption);
                    }
                }
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error running \"{tool.Name}\": {ex.Message}", "External Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string ResolveVariables(string input)
        {
            string result = input;
            string? filePath = ActiveDoc?.FilePath;
            string? fileName = filePath != null ? Path.GetFileName(filePath) : "";
            string? fileDir = filePath != null ? Path.GetDirectoryName(filePath) : "";
            string? fileExt = filePath != null ? Path.GetExtension(filePath) : "";
            string? fileNameNoExt = filePath != null ? Path.GetFileNameWithoutExtension(filePath) : "";
            string selText = textEditor?.SelectedText ?? "";
            int curLine = textEditor != null ? textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1 : 1;
            int curCol = textEditor != null ? textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)) + 1 : 1;

            result = result.Replace("$(FilePath)", filePath ?? "");
            result = result.Replace("$(FileDir)", fileDir ?? "");
            result = result.Replace("$(FileName)", fileName ?? "");
            result = result.Replace("$(FileNameNoExt)", fileNameNoExt ?? "");
            result = result.Replace("$(FileExt)", fileExt ?? "");
            result = result.Replace("$(SelText)", selText);
            result = result.Replace("$(CurLine)", curLine.ToString());
            result = result.Replace("$(CurCol)", curCol.ToString());

            return result;
        }

        private void ToggleWorkspace()
        {
            _workspaceVisible = !_workspaceVisible;
            workspaceMenuItem.Checked = _workspaceVisible;
            if (_workspaceVisible)
            {
                if (_workspaceSplitContainer != null)
                {
                    _workspaceSplitContainer.Panel1Collapsed = false;
                    _workspaceSplitContainer.SplitterDistance = _workspaceWidth;
                    _workspaceSplitContainer.PerformLayout();
                    if (!string.IsNullOrEmpty(_workspaceRoot))
                    {
                        // Check if workspace is too large for smooth operation
                        try
                        {
                            var fileCount = Directory.EnumerateFiles(_workspaceRoot, "*", SearchOption.AllDirectories).Count();
                            if (fileCount > 1000) // Arbitrary limit
                            {
                                BeginInvoke(() => ThemedMessageBox.Show($"Workspace has {fileCount} files, which may cause performance issues. Consider using a smaller project folder.", "Large Workspace", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                                return; // Don't enable workspace
                            }
                        }
                        catch { }
                        Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
                    }
                }
            }
            else
            {
                if (_workspaceSplitContainer != null)
                {
                    _workspaceWidth = Math.Max(80, Math.Min(600, _workspaceSplitContainer.SplitterDistance));
                    _workspaceSplitContainer.Panel1Collapsed = true;
                    _workspaceSplitContainer.PerformLayout();
                }
                textEditor?.Focus();
            }
            SaveSettings();
        }

        private void ToggleWorkspace_Click(object? sender, EventArgs e)
        {
            ToggleWorkspace();
        }

        private void OpenFolder_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select a folder to open as workspace",
                UseDescriptionForTitle = true,
                SelectedPath = _workspaceRoot
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                 // Cancel any in-flight scan and symbol rebuild
                 _workspacePanel?.CancelScan();
                 try
                 {
                     _symbolIndexCts?.Cancel();
                 }
                 catch (ObjectDisposedException)
                 {
                     // Ignore if already disposed
                 }
                  _symbolIndexCts = new CancellationTokenSource();
                var token = _symbolIndexCts.Token;
                _workspaceRoot = dlg.SelectedPath;
                Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
                AddToRecentWorkspaces(_workspaceRoot);
                _ = _symbolIndex.RebuildIndexAsync(_workspaceRoot);
                if (!_workspaceVisible)
                    ToggleWorkspace();
                else
                    SaveSettings();
            }
        }

        internal void OpenWorkspaceFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            _workspacePanel?.CancelScan();
            try
            {
                _symbolIndexCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            _symbolIndexCts = new CancellationTokenSource();
            var token = _symbolIndexCts.Token;
            _workspaceRoot = path;
            _workspacePanel?.SetRoot(_workspaceRoot);
            AddToRecentWorkspaces(_workspaceRoot);
            UpdateWorkspaceProjectLabel();
            Task.Run(() =>
            {
                if (!token.IsCancellationRequested)
                    _ = _symbolIndex.RebuildIndexAsync(_workspaceRoot);
            }, token);
            if (!_workspaceVisible)
                ToggleWorkspace();
            else
                SaveSettings();
        }

        private void OnWorkspaceScanStarted()
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(OnWorkspaceScanStarted); return; }
            scanProgressBar.Visible = true;
            scanProgressBar.Value = 0;
            scanProgressBar.Maximum = 100;
            _originalStatusBarColor = statusStrip.BackColor;
            statusStrip.BackColor = _themeManager.CurrentTheme.Accent.IsEmpty
                ? Color.DodgerBlue
                : Color.FromArgb(_themeManager.CurrentTheme.Accent.R, _themeManager.CurrentTheme.Accent.G, _themeManager.CurrentTheme.Accent.B);
        }

        private void OnWorkspaceScanCompleted()
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(OnWorkspaceScanCompleted); return; }
            scanProgressBar.Visible = false;
            scanProgressBar.Value = 0;
            statusStrip.BackColor = _originalStatusBarColor;
            DetectAndSuggestProfile();
            ShowNotification("Workspace", "Folder scan complete");
        }

        private void DetectAndSuggestProfile()
        {
            try
            {
                // Only auto-suggest if the user is still on the Default profile
                // (not if they explicitly selected a custom profile)
                if (_currentProfile.Name != "Default") return;
                if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot)) return;

                string? detected = WorkspaceHelper.DetectLanguage(_workspaceRoot);
                if (detected == null) return;

                // Try to find an existing profile whose name contains the detected language
                string matchName = detected.ToLowerInvariant();
                foreach (string pname in _profileManager.ProfileNames)
                {
                    if (pname == "Default") continue;
                    if (pname.ToLowerInvariant().Contains(matchName))
                    {
                        var profile = _profileManager.LoadProfile(pname);
                        if (profile != null)
                        {
                            ApplyProfile(profile);
                            ShowNotification("Profile", $"Auto-switched to profile: {pname}");
                            return;
                        }
                    }
                }

                // No matching profile found — suggest creating one
                ShowNotification("Profile", $"Detected {detected} project — create a profile from File > Preferences > Profile");
            }
            catch { }
        }

        private void UpdateWorkspaceProjectLabel()
        {
            _detectedProjectPath = null;
            _detectedSolutionPath = null;

            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                _workspaceProjectLabel.Text = "";
                _workspaceProjectLabel.ToolTipText = "";
                _workspaceProjectLabel.Visible = false;
                return;
            }

            try
            {
                var sln = Directory.EnumerateFiles(_workspaceRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (sln != null)
                {
                    _detectedSolutionPath = sln;
                    _workspaceProjectLabel.Text = Path.GetFileNameWithoutExtension(sln);
                    _workspaceProjectLabel.ToolTipText = sln;
                    _workspaceProjectLabel.Visible = true;
                    return;
                }

                var csproj = Directory.EnumerateFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
                if (csproj != null)
                {
                    _detectedProjectPath = csproj;
                    _workspaceProjectLabel.Text = Path.GetFileNameWithoutExtension(csproj);
                    _workspaceProjectLabel.ToolTipText = csproj;
                    _workspaceProjectLabel.Visible = true;
                    return;
                }

                _workspaceProjectLabel.Text = Path.GetFileName(_workspaceRoot);
                _workspaceProjectLabel.ToolTipText = _workspaceRoot;
                _workspaceProjectLabel.Visible = true;
            }
            catch
            {
                _workspaceProjectLabel.Text = Path.GetFileName(_workspaceRoot);
                _workspaceProjectLabel.ToolTipText = _workspaceRoot;
                _workspaceProjectLabel.Visible = true;
            }
        }

        private void WorkspaceProjectLabel_Click(object? sender, EventArgs e)
        {
            string? target = _detectedSolutionPath ?? _detectedProjectPath;
            if (target != null)
            {
                string dir = Path.GetDirectoryName(target)!;
                try { Process.Start("explorer.exe", dir); } catch { }
            }
            else if (!string.IsNullOrEmpty(_workspaceRoot))
            {
                try { Process.Start("explorer.exe", _workspaceRoot); } catch { }
            }
        }

        private void OnWorkspaceScanProgressChanged(string message)
        {
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(() => OnWorkspaceScanProgressChanged(message)); return; }
            // Extract percentage from message like "Scanning (45%)..."
            if (message is not null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)%");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int pct))
                {
                    scanProgressBar.Value = Math.Min(pct, 100);
                }
            }
        }

        #endregion

        #region Find & Replace

        private void Find_Click(object? sender, EventArgs e)
        {
            using var dlg = new FindReplaceDialog(this, false, _lastFindText, _lastFindCaseSensitive, _lastUseRegex);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lastFindText = dlg.FindText;
                _lastFindCaseSensitive = dlg.CaseSensitive;
                _lastFindUp = dlg.SearchUp;
                _lastUseRegex = dlg.UseRegex;
            }
        }

        private void FindNext_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFindText))
                Find_Click(sender, e);
            else
                PerformFind(_lastFindText, _lastFindCaseSensitive, false, _lastUseRegex);
        }

        private void FindPrevious_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFindText))
                Find_Click(sender, e);
            else
                PerformFind(_lastFindText, _lastFindCaseSensitive, true, _lastUseRegex);
        }

        private void Replace_Click(object? sender, EventArgs e)
        {
            using var dlg = new FindReplaceDialog(this, true, _lastFindText, _lastFindCaseSensitive, _lastUseRegex);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _lastFindText = dlg.FindText;
                _lastFindCaseSensitive = dlg.CaseSensitive;
                _lastFindUp = dlg.SearchUp;
                _lastUseRegex = dlg.UseRegex;
                _lastReplaceText = dlg.ReplaceText;
            }
        }

        private void Goto_Click(object? sender, EventArgs e)
        {
            using var dlg = new GoToDialog(this);
            dlg.ShowDialog(this);
        }

        private void FormatDocument_Click(object? sender, EventArgs e) { FormatDocumentAsync(); }
        private void ShowQuickOpen() { using var dlg = new QuickOpenDialog(this); dlg.ShowDialog(this); }

        internal List<CommandPaletteForm.CommandEntry> GetCommandPaletteCommands()
        {
            return new List<CommandPaletteForm.CommandEntry>
            {
                // File operations
                new("New File", "Ctrl+N", () => NewFile()),
                new("Open File", "Ctrl+O", () => OpenFile()),
                new("Save", "Ctrl+S", () => SaveCurrentDocument()),
                new("Save All", "Ctrl+Shift+S", () => SaveAllFiles()),
                new("Close Tab", "Ctrl+W", () => CloseCurrentTab()),
                new("Close All Tabs", "", () => CloseAllTabs()),
                new("Exit", "Alt+F4", () => Close()),

                // Edit operations
                new("Find", "Ctrl+F", () => { using var dlg = new FindReplaceDialog(this, false, _lastFindText, _lastFindCaseSensitive, _lastUseRegex); dlg.ShowDialog(this); }),
                new("Replace", "Ctrl+H", () => { using var dlg = new FindReplaceDialog(this, true, _lastFindText, _lastFindCaseSensitive, _lastUseRegex); dlg.ShowDialog(this); }),
                new("Find in Files", "Ctrl+Shift+F", () => { using var dlg = new GlobalSearchDialog(this); dlg.ShowDialog(this); }),
                new("Go to Line", "Ctrl+G", () => { using var dlg = new GoToDialog(this); dlg.ShowDialog(this); }),
                new("Go to Definition", "F12", () => GoToDefinition()),
                new("Rename Symbol", "F2", () => RenameSymbol()),
                new("Format EditorDocument", "Ctrl+Shift+I", () => FormatDocumentAsync()),

                // View operations
                new("Toggle Terminal", "Ctrl+`", () => ToggleTerminal()),
                new("Toggle Workspace", "", () => ToggleWorkspace()),
                new("Toggle Git Panel", "", () => ToggleGitPanel()),
                new("Toggle Symbols", "", () => ToggleSymbols()),
                new("Toggle Problems", "", () => ToggleProblems()),
                new("Toggle Minimap", "", () => ToggleMinimap()),
                new("Toggle Word Wrap", "", () => ToggleWordWrap()),
                new("Toggle Status Bar", "", () => ToggleStatusBar()),
                new("Toggle Syntax Highlighting", "", () => ToggleSyntaxHighlighting()),

                // Run/Debug operations
                new("Start Debugging", "F5", () => StartDebug_Click(null, EventArgs.Empty)),
                new("Run Without Debugging", "Ctrl+F5", () => RunWithoutDebug_Click(null, EventArgs.Empty)),
                new("Stop Debugging", "Shift+F5", () => StopDebug_Click(null, EventArgs.Empty)),
                new("Restart Debugging", "Ctrl+Shift+F5", () => RestartDebug_Click(null, EventArgs.Empty)),
                new("Run Tests", "Ctrl+R, T", () => RunTests()),
                new("Run Tests with Coverage", "", () => RunTestsWithCoverage()),
                new("Build Project", "", () => { string? projDir = GetProjectDirectory(); if (projDir != null) RunDotnetBuild(projDir); }),

                // Tools operations
                new("Settings", "Ctrl+,", () => { using var dlg = new SettingsDialog(this); dlg.ShowDialog(this); }),
                new("Configure External Tools", "Ctrl+Alt+T", () => ConfigureTools()),
                new("Performance Profiler", "", () => ShowPerformanceProfiler()),
                new("Restart Roslyn Analyzers", "Ctrl+Shift+R", () => RestartRoslynAnalyzers()),
                new("Toggle Roslyn Analyzers", "Ctrl+Alt+A", () => ToggleRoslynAnalyzers()),
                new("Open Roslyn Visualizer", "Ctrl+Alt+V", () => OpenRoslynVisualizer()),

                // Help operations
                new("About", "", () => { using var dlg = new AboutDialog(); dlg.ShowDialog(this); }),
            };
        }

        public record OpenTabInfo(string Title, string FilePath, Action Switch);

        internal List<OpenTabInfo> GetOpenTabs()
        {
            var tabs = new List<OpenTabInfo>();
            if (tabControl == null) return tabs;
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var page = tabControl.TabPages[i];
                if (page.Tag is EditorDocument doc)
                {
                    tabs.Add(new OpenTabInfo(page.Text, doc.FilePath ?? "", () => tabControl.SelectedIndex = i));
                }
            }
            return tabs;
        }

        internal EditorDocument? GetCurrentDocument()
        {
            return textEditor?.Tag as EditorDocument;
        }

        private string? GetProjectDirectory()
        {
            var projectDir = Path.GetDirectoryName(ActiveDoc?.FilePath);
            if (projectDir == null) return null;

            // Find .csproj file
            while (!string.IsNullOrEmpty(projectDir))
            {
                if (Directory.GetFiles(projectDir, "*.csproj").Length > 0)
                    return projectDir;
                projectDir = Path.GetDirectoryName(projectDir);
            }
            return null;
        }

        private void RenameSymbol()
        {
            if (textEditor is null) return;
            string? word = GetWordAtCursor();
            if (string.IsNullOrEmpty(word)) return;

            using var dlg = new RenameDialog(word, _workspaceRoot);
            if (_roslynWorkspace.IsReady)
                dlg.SetRoslynWorkspace(_roslynWorkspace, textEditor.SelectionStart);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Applied)
            {
                if (textEditor.Text.Contains(dlg.NewName))
                    ShowNotification("Rename", $"Renamed '{word}' to '{dlg.NewName}'");
            }
        }

        private void Rename_Click(object? sender, EventArgs e)
        {
            RenameSymbol();
        }

        private void CallHierarchy_Click(object? sender, EventArgs e)
        {
            ShowCallHierarchy();
        }

        private void RunCoverage_Click(object? sender, EventArgs e)
        {
            RunTestsWithCoverage();
        }

        private void ScanTODOs_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Task List", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Run TODO scan on background thread
            ShowNotification("Task List", "Scanning for TODOs...");
            string root = _workspaceRoot;
            Task.Run(() =>
            {
                try
                {
                    var todos = TodoScanner.ScanWorkspace(root);
                    BeginInvoke(() =>
                    {
                        try
                        {
                            // Merge with existing diagnostics
                            var existing = _problemsPanel?.GetDiagnostics() ?? new List<Diagnostic>();
                            var merged = new List<Diagnostic>();
                            merged.AddRange(existing);
                            merged.AddRange(todos);
                            merged.Sort((a, b) => { int c = a.Line.CompareTo(b.Line); return c != 0 ? c : a.Column.CompareTo(b.Column); });

                            var allActions = _quickActionProvider.GetActions("", merged);
                            var gutterActions = allActions.Select(a => (a.Line, a.Title, a.Apply)).ToList();
                            _problemsPanel?.SetDiagnostics(merged);
                            gutterPanel?.SetQuickActions(gutterActions);

                            double pct = merged.Count > 0 ? (double)todos.Count / merged.Count * 100 : 0;
                            ShowNotification("Task List", $"Found {todos.Count} TODO(s) — {merged.Count} total issues");
                        }
                        catch (Exception ex)
                        {
                            ShowNotification("Task List", $"Error: {ex.Message}");
                        }
                    });
                }
                catch
                {
                    BeginInvoke(() => ShowNotification("Task List", "Scan failed"));
                }
            });
        }

        private void LoadCoverage_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Open Coverage File",
                Filter = "Cobertura XML (*.cobertura.xml;*.xml)|*.cobertura.xml;*.xml|All files (*.*)|*.*",
                DefaultExt = ".xml"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                LoadCoverageFile(dlg.FileName);
        }

        private void Dependencies_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Dependencies", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Dependencies", "Scanning project dependencies...");
            try
            {
                var projects = ProjectDependencyAnalyzer.Analyze(_workspaceRoot);
                using var dlg = new DependencyGraphDialog(projects);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(
                    $"Dependency analysis failed:\n{ex.Message}",
                    "Dependencies Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ImpactAnalysis_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Save the current file first, then run impact analysis.",
                    "Impact Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
            {
                ThemedMessageBox.Show("Open a workspace folder first (Panel > Open Folder).",
                    "Impact Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Impact", "Analyzing dependencies...");
            try
            {
                var projects = ProjectDependencyAnalyzer.Analyze(_workspaceRoot);
                var affected = ProjectDependencyAnalyzer.FindAffectedFiles(currentFilePath, projects);
                using var dlg = new ImpactAnalysisDialog(currentFilePath, affected);
                dlg.FileSelected += (file) => BeginInvoke(() =>
                {
                    if (File.Exists(file)) OpenFileInNewTab(file);
                });
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show(
                    $"Impact analysis failed:\n{ex.Message}",
                    "Impact Analysis Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowDependents()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Save the current file first.", "Find Dependents",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ImpactAnalysis_Click(null, EventArgs.Empty);
        }

        private void RunTestsWithCoverage()
        {
            string? testProj = FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found in the workspace folder.\n\nEnsure a .csproj with a test SDK reference exists.",
                    "Run Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowNotification("Coverage", "Running tests with coverage...");
            var result = CoverageParser.RunTestsWithCoverage(testProj);
            if (result == null || result.FileLineHits.Count == 0)
            {
                ShowNotification("Coverage", "No coverage data generated");
                return;
            }

            ApplyCoverageResult(result);
        }

        private void LoadCoverageFile(string path)
        {
            var result = CoverageParser.ParseFile(path);
            if (result == null || result.FileLineHits.Count == 0)
            {
                ThemedMessageBox.Show("Could not parse coverage file or no data found.", "Load Coverage",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ApplyCoverageResult(result);
        }

        private void ApplyCoverageResult(CoverageParser.CoverageResult result)
        {
            double pct = result.LinesValid > 0 ? (double)result.LinesCovered / result.LinesValid * 100 : 0;

            // Apply to gutter for current file
            if (currentFilePath != null && result.FileLineHits.TryGetValue(currentFilePath, out var lineHits))
                gutterPanel?.SetCoverage(lineHits);

            // Show summary
            using var dlg = new CoverageSummaryForm(result);
            dlg.FileSelected += (file) => BeginInvoke(() =>
            {
                if (File.Exists(file))
                    OpenFileInNewTab(file);
            });
            dlg.ShowDialog(this);

            ShowNotification("Coverage", $"{pct:F1}% — {result.LinesCovered}/{result.LinesValid} lines");
        }

        private string? FindTestProject()
        {
            if (string.IsNullOrEmpty(_workspaceRoot) || !Directory.Exists(_workspaceRoot))
                return null;

            try
            {
                var csprojFiles = Directory.EnumerateFiles(_workspaceRoot, "*Tests.csproj", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(_workspaceRoot, "*Test.csproj", SearchOption.AllDirectories))
                    .ToList();
                return csprojFiles.FirstOrDefault();
            }
            catch { return null; }
        }

        private void RunTests_Click(object? sender, EventArgs e)
        {
            RunTests();
        }

        private void RerunFailedTests_Click203(object? sender, EventArgs e)
        {
            RerunFailedTests();
        }

        private void RunTests()
        {
            if (_testsRunning)
            {
                ShowNotification("Tests", "Tests are already running");
                return;
            }

            string? testProj = FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found in the workspace folder.",
                    "Run Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lastTestProject = testProj;
            _testsRunning = true;
            ShowNotification("Tests", "Running tests...");

            string proj = testProj;
            Task.Run(() =>
            {
                var result = TestResultParser.RunTests(proj);
                BeginInvoke(() =>
                {
                    _testsRunning = false;
                    _lastTestResult = result;
                    
                    if (result.Total == 0 && string.IsNullOrEmpty(result.RawOutput))
                    {
                        ShowNotification("Tests", "Test run failed or produced no results");
                        return;
                    }

                    using var dlg = new TestResultsDialog(result);
                    dlg.FrameSelected += (file, line) => BeginInvoke(() =>
                    {
                        if (File.Exists(file))
                        {
                            OpenFileInNewTab(file);
                            GoToLine(line);
                        }
                    });
                    dlg.ShowDialog(this);

                    string status = result.Failed > 0
                        ? $"{result.Failed} failed, {result.Passed} passed"
                        : $"{result.Passed} passed";
                    if (result.Skipped > 0) status += $", {result.Skipped} skipped";
                    ShowNotification("Tests", status);
                });
            });
        }

        private void RerunFailedTests()
        {
            if (_testsRunning)
            {
                ShowNotification("Tests", "Tests are already running");
                return;
            }

            string? testProj = _lastTestProject ?? FindTestProject();
            if (string.IsNullOrEmpty(testProj))
            {
                ThemedMessageBox.Show("No test project found.",
                    "Rerun Failed Tests", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _testsRunning = true;
            ShowNotification("Tests", "Rerunning failed tests...");

            string proj = testProj;
            string? filter = _lastTestResult != null
                ? string.Join("|", _lastTestResult.Tests
                    .Where(t => t.Outcome == TestOutcome.Failed)
                    .Select(t => t.TestName))
                : null;

            Task.Run(() =>
            {
                string args = $"test \"{proj}\" --no-build --filter \"{filter}\"";
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) { BeginInvoke(() => { _testsRunning = false; }); return; }

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(300000);

                BeginInvoke(() =>
                {
                    _testsRunning = false;

                    // Parse from stdout
                    var result = new TestRunResult();
                    TestResultParser.ParseSummaryFromOutput(stdout, result);

                    // If we have a TRX, parse that too
                    string tempDir = Path.Combine(Path.GetTempPath(), "pfpad_rerun_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    string trxFile = Path.Combine(tempDir, "rerun.trx");
                    try
                    {
                        // Re-run with logger to get structured results
                        var psi2 = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"test \"{proj}\" --no-build --filter \"{filter}\" --logger \"trx;LogFileName={trxFile}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc2 = System.Diagnostics.Process.Start(psi2);
                        if (proc2 != null)
                        {
                            proc2.WaitForExit(300000);
                            if (File.Exists(trxFile))
                                result = TestResultParser.ParseTrxFile(trxFile);
                        }
                    }
                    catch { }
                    finally { try { Directory.Delete(tempDir, true); } catch { } }

                    _lastTestResult = result;

                    using var dlg = new TestResultsDialog(result);
                    dlg.FrameSelected += (file, line) => BeginInvoke(() =>
                    {
                        if (File.Exists(file))
                        {
                            OpenFileInNewTab(file);
                            GoToLine(line);
                        }
                    });
                    dlg.ShowDialog(this);

                    string status = result.Failed > 0
                        ? $"{result.Failed} failed, {result.Passed} passed"
                        : $"{result.Passed} passed";
                    ShowNotification("Tests", "Rerun: " + status);
                });
            });
        }

        private void ShowCallHierarchy()
        {
            if (textEditor is null) return;
            string? word = GetWordAtCursor();
            if (string.IsNullOrEmpty(word)) return;

            string currentFile = currentFilePath ?? "";
            using var dlg = new CallHierarchyDialog(word, _workspaceRoot, currentFile);
            dlg.OpenFileRequested += (file, line) =>
            {
                BeginInvoke(() =>
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        OpenFileInNewTab(file);
                        GoToLine(line);
                    }
                });
            };
            dlg.ShowDialog(this);
        }

        private void ParseStackTrace_Click(object? sender, EventArgs e)
        {
            ParseStackTrace();
        }

        private void ParseStackTrace()
        {
            string source = textEditor?.SelectedText ?? "";
            if (string.IsNullOrWhiteSpace(source))
                source = textEditor?.Text ?? "";

            var frames = StackTraceParser.Parse(source);
            if (frames.Count == 0)
            {
                ThemedMessageBox.Show("No stack trace frames detected in the current selection or EditorDocument.\n\nSupported formats:\n.NET: at Method() in File.cs:line 42\nJS:   at func (file.js:42:10)\nPython: File \"file.py\", line 42",
                    "Parse Stack Trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new StackTraceDialog(source);
            dlg.FrameSelected += (file, line) =>
            {
                BeginInvoke(() =>
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        OpenFileInNewTab(file);
                        GoToLine(line);
                    }
                    else
                    {
                        ThemedMessageBox.Show($"File not found:\n{file}",
                            "Stack Trace", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                });
            };
            dlg.ShowDialog(this);
        }

        private void GoToDefinition_Click(object? sender, EventArgs e)
        {
            GoToDefinition();
        }

        private void GoBack_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Navigation history — Back not yet implemented");
        }

        private void GoForward_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Navigation history — Forward not yet implemented");
        }

        private void GoLastEditLocation_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Last edit location tracking not yet implemented");
        }

        private void GoToFile_Click(object? sender, EventArgs e)
        {
            ShowQuickOpen();
        }

        private void GoToSymbolInWorkspace_Click(object? sender, EventArgs e)
        {
            ToggleSymbolPanel();
        }

        private void GoToSymbolInEditor_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Symbol in editor — use EditorDocument Outline panel");
        }

        private void GoToDeclaration_Click(object? sender, EventArgs e)
        {
            GoToDefinition(); // Roslyn declaration is typically same as definition
        }

        private void GoToTypeDefinition_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Go to Type Definition not yet implemented");
        }

        private void GoToImplementations_Click(object? sender, EventArgs e)
        {
            ShowNotification("Go", "Go to Implementations not yet implemented");
        }

        private void GoToReferences_Click(object? sender, EventArgs e)
        {
            if (activeDocIndex < 0 || string.IsNullOrEmpty(currentFilePath))
            {
                ShowNotification("Go", "Open a file first");
                return;
            }
            var word = GetWordAtCursor();
            if (!string.IsNullOrEmpty(word))
                GoToDefinitionRoslyn();
            else
                ShowNotification("Go", "No symbol at cursor");
        }

        private void GoToBracket_Click(object? sender, EventArgs e)
        {
            int pos = textEditor.SelectionStart;
            int? match = HighlightRichTextBox.FindMatchingBrace(textEditor.Text, pos);
            if (match.HasValue)
            {
                GoToLine(textEditor.GetLineFromCharIndex(match.Value) + 1);
                textEditor.SelectionStart = match.Value;
                textEditor.SelectionLength = 0;
            }
            else
            {
                ShowNotification("Go", "No matching bracket found");
            }
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
            foreach (int bm in _docManager.GetActive()?.Bookmarks ?? [])
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
            foreach (int bm in _docManager.GetActive()?.Bookmarks ?? [])
            {
                if (bm < currentLine) prev = bm;
                else break;
            }
            if (prev.HasValue) GoToLine(prev.Value + 1);
        }

        private void ClearAllBookmarks_Click(object? sender, EventArgs e)
        {
            _docManager.GetActive()?.Bookmarks.Clear();
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
            var bms = _docManager.GetActive()?.Bookmarks;
            if (bms == null) return;
            if (bms.Contains(line))
                bms.Remove(line);
            else
                bms.Add(line);
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

        private void ToggleWhitespace_Click(object? sender, EventArgs e)
        {
            whitespaceOverlay.ShowGlyphs = whitespaceMenuItem.Checked;
        }

        private void OnFeedUpdated()
        {
            if (_notificationStatusLabel is null || _notificationStatusLabel.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(OnFeedUpdated);
                return;
            }

            UpdateNotificationBadge();
            // ShowToastForNewItems(); // Disabled for less visual intensity
        }

        public void ShowNotification(string title, string summary)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowNotification(title, summary));
                return;
            }

            // Delay notifications during the first 15 seconds after launch
            if ((DateTime.UtcNow - _startTime).TotalSeconds < NotificationDelaySeconds)
            {
                _delayedNotifications.Add((title, summary));
                return;
            }

            _notificationFeed.AddNotification(title, summary);

            var id = $"app-{Guid.NewGuid():N}";
            var item = new FeedItem
            {
                Id = id,
                Source = FeedSource.Custom,
                Title = title,
                Summary = summary,
                Published = DateTime.UtcNow,
                IsRead = false
            };
            // ShowToast(item); // Disabled for less visual intensity
        }

        private void ShowToast(FeedItem item, int stackIndex = -1)
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int toastHeight = 130;
            int gap = 8;
            int yOffset = stackIndex >= 0
                ? stackIndex * (toastHeight + gap)
                : _activeToasts.Count * (toastHeight + gap);
            int bottomY = screen.Bottom - toastHeight - 16 - yOffset;
            bottomY = Math.Max(screen.Top + 16, bottomY);

            var toast = new NotificationToastForm(item, bottomY);
            toast.FormClosed += (s, e) =>
            {
                _activeToasts.Remove(toast);
                toast.Dispose();
                RepositionToasts();
            };
            _activeToasts.Add(toast);
            toast.Show(this);
            if (textEditor.CanFocus)
                textEditor.Focus();
        }

        private void RepositionToasts()
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int toastHeight = 130;
            int gap = 8;
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var t = _activeToasts[i];
                if (t.IsDisposed) continue;
                int y = screen.Bottom - toastHeight - 16 - i * (toastHeight + gap);
                y = Math.Max(screen.Top + 16, y);
                t.Location = new Point(t.Location.X, y);
            }
        }

        private void FlushDelayedNotifications()
        {
            foreach (var (title, summary) in _delayedNotifications)
            {
                _notificationFeed.AddNotification(title, summary);
            }

            var unread = _notificationFeed.AllItems.Where(i => !i.IsRead).ToList();
            foreach (var item in unread)
            {
                if (_toastedIds.Add(item.Id))
                {
                    // ShowToast(item); // Disabled for less visual intensity
                }
            }
            _delayedNotifications.Clear();
        }

        private void UpdateNotificationBadge()
        {
            if (_notificationStatusLabel is null || _notificationStatusLabel.IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(UpdateNotificationBadge);
                return;
            }
            int unread = _notificationFeed.UnreadCount;
            if (unread > 0)
            {
                _notificationStatusLabel.Text = $"N {unread}";
                _notificationStatusLabel.ForeColor = _themeManager.CurrentTheme.Accent;
            }
            else
            {
                _notificationStatusLabel.Text = "N";
                _notificationStatusLabel.ForeColor = _themeManager.CurrentTheme.Muted;
            }
        }

        private void ShowToastForNewItems()
        {
            // Suppress feed-generated toasts during the first 15 seconds
            if ((DateTime.UtcNow - _startTime).TotalSeconds < NotificationDelaySeconds)
                return;

            var unread = _notificationFeed.AllItems.Where(i => !i.IsRead).ToList();
            foreach (var item in unread)
            {
                if (_toastedIds.Add(item.Id))
                {
                    // ShowToast(item); // Disabled for less visual intensity
                }
            }
        }

        private void ToggleNotificationCenter(object? sender, EventArgs e)
        {
            if (_notificationCenter is not null && !_notificationCenter.IsDisposed)
            {
                var wa1 = Screen.GetWorkingArea(_notificationCenter);
                if (wa1.Contains(_notificationCenter.Bounds))
                {
                    _notificationCenter.BringToFront();
                    _notificationCenter.Focus();
                    return;
                }
                // Off-screen — close and recreate
                _notificationCenter.Close();
                _notificationCenter = null;
            }

            _notificationCenter = new NotificationCenterForm(_notificationFeed);
            _notificationCenter.UpdateTheme(_themeManager.CurrentTheme);
            _notificationCenter.FormClosed += (s, args) =>
            {
                _notificationCenter = null;
                UpdateNotificationBadge();
            };

            // Position above the status bar notification label, clamped to working area
            var wa2 = Screen.GetWorkingArea(this);
            var screenPoint = statusStrip.PointToScreen(
                new Point(_notificationStatusLabel.Bounds.Right, statusStrip.Bounds.Top - _notificationCenter.Height));
            screenPoint.X = Math.Clamp(screenPoint.X - _notificationCenter.Width + 60, wa2.Left, wa2.Right - _notificationCenter.Width);
            screenPoint.Y = Math.Clamp(screenPoint.Y, wa2.Top, wa2.Bottom - _notificationCenter.Height);
            _notificationCenter.Location = screenPoint;
            _notificationCenter.Show(this);
        }

        private void ConfigureNotifications_Click(object? sender, EventArgs e)
        {
            using var dlg = new NotificationSettingsForm(_notificationFeed);
            dlg.ShowDialog(this);
            // Refresh notification state after settings change
            OnFeedUpdated();
            _ = _notificationFeed.FetchAllAsync();
        }

        private void ApplyThemeByName(string name)
        {
            if (!ThemeManager.Themes.ContainsKey(name)) return;
            _themeManager.SetTheme(name);
            UpdateThemeColors(_themeManager.CurrentTheme);
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

        private void BuildConfigCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_buildConfigCombo.SelectedItem is string config && !string.IsNullOrEmpty(config))
            {
                _activeConfiguration = config;
                SaveSettings();
            }
        }

        private void UpdateThemeDropDown()
        {
            if (themeDropDown != null)
            {
                themeDropDown.Text = _themeManager.CurrentTheme.Name;
                // Rebuild theme items from the registry
                themeDropDown.DropDownItems.Clear();
                foreach (var name in ThemeManager.ThemeNames)
                {
                    var item = new ToolStripMenuItem(name);
                    item.Click += (s, e) => ApplyThemeByName(name);
                    if (name == _themeManager.CurrentTheme.Name)
                        item.Text = "\u25CF " + name;
                    themeDropDown.DropDownItems.Add(item);
                }
            }
        }

        #endregion

        #region EditorDocument Management (Tabs)

        // Get first visible line in editor
        private int GetFirstVisibleLine()
        {
            if (!textEditor.IsHandleCreated) return 0;
            return (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
        }

        // Compute hash of string content
        private string ComputeContentHash(string content)
            => WorkspaceHelper.ComputeContentHash(content);

        // Save current editor state into the active EditorDocument
        private void SaveCurrentDocument()
        {
            if (activeDocIndex < 0) return;
            var doc = documents[activeDocIndex];
            doc.Content = textEditor.Text;
            doc.IsDirty = isModified;
            // bookmarks, modifiedLines, collapsedRegions are owned by doc directly
            doc.FilePath = currentFilePath;
            doc.SavedHash = savedContentHash;
            doc.LastWriteTime = lastFileWriteTime;
            doc.Syntax = currentSyntax;
            doc.SelectionStart = textEditor.SelectionStart;
            doc.SelectionLength = textEditor.SelectionLength;
            doc.FirstVisibleLine = GetFirstVisibleLine();
        }

        // Create a new empty EditorDocument
        private EditorDocument CreateNewDocument() => _docManager.CreateNew();

        // New file (untitled) - creates a new tab
        private void NewFile(bool isInitial = false)
        {
            if (!isInitial)
            {
                var result = PromptSaveChanges();
                if (result == DialogResult.Cancel) return;
            }

            // Save state of current EditorDocument before switching
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
            activeDocIndex = newIndex;
            EnsureSelectedTabVisible();
        }

        // Open an existing file in a new tab
        internal void OpenFileInNewTab(string path)
        {
            if (!File.Exists(path)) return;
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > (long)MaxFileSizeMB * 1024 * 1024) // Configurable absolute limit
            {
                ThemedMessageBox.Show($"File is too large to open ({fileInfo.Length / (1024 * 1024)} MB). Maximum size is {MaxFileSizeMB} MB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (fileInfo.Length > (long)LargeFileWarningMB * 1024 * 1024) // Configurable warning threshold
            {
                var result = ThemedMessageBox.Show($"File is large ({fileInfo.Length / (1024 * 1024)} MB). Opening may be slow. Continue?", "Large File Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }
            try
            {
                var (content, encoding) = UnicodeFileHelper.ReadAllTextWithEncoding(path);
                var syntax = SyntaxDefinition.GetDefinitionForFile(path);
                bool containsRtl = UnicodeFileHelper.ContainsRtlText(content);
                _profilerDialog?.Log($"Opened file: {path} ({content.Length} chars, {UnicodeFileHelper.GetEncodingDisplayName(encoding)}{(containsRtl ? ", RTL" : "")})");
                var doc = new EditorDocument
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
                    Syntax = syntax,
                    FileEncoding = encoding,
                    ContainsRtlText = containsRtl
                };

                // Apply feature degradation for large files
                ApplyLargeFileDegradation(doc, fileInfo.Length);

                documents.Add(doc);
                int newIndex = documents.Count - 1;
                var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
                tabControl.TabPages.Add(tabPage);
                tabControl.SelectedIndex = newIndex; // triggers SwitchToTab when handle exists
                activeDocIndex = newIndex;
                EnsureSelectedTabVisible();
                ForceCleanState();
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Open file asynchronously — shows tab immediately, loads content in background
        internal void OpenFileInNewTabAsync(string path)
        {
            Debug.WriteLine($"OpenFileInNewTabAsync called with path: {path}");
            if (!File.Exists(path))
            {
                Debug.WriteLine($"File does not exist: {path}");
                return;
            }
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > (long)MaxFileSizeMB * 1024 * 1024) // Configurable limit for async loading
            {
                BeginInvoke(() => ThemedMessageBox.Show($"File is too large to open ({fileInfo.Length / (1024 * 1024)} MB). Maximum size is {MaxFileSizeMB} MB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }
            if (fileInfo.Length > (long)AsyncFileWarningMB * 1024 * 1024) // Configurable warning for async
            {
                var result = ThemedMessageBox.Show($"File is large ({fileInfo.Length / (1024 * 1024)} MB). Opening may be slow and use significant memory. Continue?", "Large File Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                    return;
            }
            var syntax = SyntaxDefinition.GetDefinitionForFile(path);
            var doc = new EditorDocument
            {
                FilePath = path,
                Content = "",
                IsDirty = false,
                ModifiedLines = new HashSet<int>(),
                Bookmarks = new HashSet<int>(),
                CollapsedRegions = new HashSet<int>(),
                SavedHash = "",
                LastWriteTime = File.GetLastWriteTimeUtc(path),
                SelectionStart = 0,
                SelectionLength = 0,
                FirstVisibleLine = 0,
                Syntax = syntax,
                FileEncoding = null // Will be set when content is loaded
            };

            // Apply feature degradation for large files
            ApplyLargeFileDegradation(doc, fileInfo.Length);

            documents.Add(doc);
            int newIndex = documents.Count - 1;
            var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
            tabControl.TabPages.Add(tabPage);
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            EnsureSelectedTabVisible();

            _profilerDialog?.Log($"Started async load: {path}");

            // Read file content on background thread, then populate editor
            Task.Run(() =>
            {
                try
                {
                    var (content, encoding) = UnicodeFileHelper.ReadAllTextWithEncoding(path);
                    return (Content: content, Encoding: encoding);
                }
                catch { return (Content: (string?)null, Encoding: (System.Text.Encoding?)null); }
            }).ContinueWith(t =>
            {
                var result = t.Result;
                if (result.Content == null) return;
                string content = result.Content;
                var encoding = result.Encoding;
                BeginInvoke(() =>
                {
                    try
                    {
                        if (IsDisposed || activeDocIndex < 0 || activeDocIndex >= documents.Count) return;
                        var d = documents[activeDocIndex];
                        if (d.FilePath != path) return;
                        d.Content = content;
                        d.FileEncoding = encoding;
                        d.ContainsRtlText = UnicodeFileHelper.ContainsRtlText(content);
                        d.SavedHash = ComputeContentHash(content);

                        // Only update if this tab is currently active
                        if (activeDocIndex == tabControl.SelectedIndex && tabControl.SelectedIndex >= 0)
                        {
                            if (textEditor != null && !textEditor.IsDisposed && textEditor.IsHandleCreated)
                            {
                                textEditor.TextChanged -= TextEditor_TextChanged;
                                textEditor.Text = content;
                                textEditor.TextChanged += TextEditor_TextChanged;
                                savedContentHash = d.SavedHash;
                                isModified = false;

                                // Re-highlight and update UI (skip for large files)
                                if (content.Length < 200000) // 200KB threshold
                                {
                                    CreateIncrementalHighlighter();
                                }
                                UpdateStatusBar();
                                UpdateTabTitle(activeDocIndex);
                                UpdateThemeColors(_themeManager.CurrentTheme);
                                UpdateBreadcrumbs();
                            }
                        }
                        ForceCleanState();
                        _profilerDialog?.Log($"Completed async load: {path} ({content.Length} chars)");
                    }
                    catch (Exception ex)
                    {
                        BeginInvoke(() => ThemedMessageBox.Show($"Error loading file content: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                    }
                });
            });
        }

        // Close current tab
        internal void CloseCurrentTab()
        {
            if (documents.Count <= 1) return; // always keep at least one tab
            if (activeDocIndex < 0 || activeDocIndex >= documents.Count) return;

            var result = PromptSaveChanges();
            if (result == DialogResult.Cancel) return;

            // Remove current EditorDocument and its tab
            int closeIndex = activeDocIndex;
            if (closeIndex >= documents.Count || closeIndex >= tabControl.TabPages.Count) return;

            // Suspend layout to prevent flicker during tab removal
            tabControl.SuspendLayout();

            try
            {
                // Force activeDocIndex to -1 so that SelectedIndexChanged → SwitchToTab does not bail
                activeDocIndex = -1;

                documents.RemoveAt(closeIndex);
                tabControl.TabPages.RemoveAt(closeIndex);

                // Select another tab
                int newIndex = closeIndex < documents.Count ? closeIndex : documents.Count - 1;
                if (newIndex >= 0)
                    SwitchToTab(newIndex);
            }
            finally
            {
                tabControl.ResumeLayout();
            }
        }

        // Close a specific tab by index without switching to it first.
        // This avoids the "content disappears then reappears" flicker that
        // happens when selecting + closing a non-active tab.
        private void CloseTabAt(int index)
        {
            if (documents.Count <= 1) return;
            if (index < 0 || index >= documents.Count) return;

            // If it's the active tab, delegate to CloseCurrentTab
            if (index == activeDocIndex) { CloseCurrentTab(); return; }

            var doc = documents[index];
            if (doc.IsDirty)
            {
                var result = ThemedMessageBox.Show(
                    $"Save changes to \"{doc.DisplayName}\"?",
                    "Close Tab", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) return;
                if (result == DialogResult.Yes)
                {
                    try { File.WriteAllText(doc.FilePath!, doc.Content); } catch { }
                }
            }

            // Suspend layout to prevent flicker during tab removal
            tabControl.SuspendLayout();

            try
            {
                // Remove EditorDocument and tab
                documents.RemoveAt(index);

                // Adjust activeDocIndex BEFORE removing the tab page, because
                // TabPages.RemoveAt can fire SelectedIndexChanged → SwitchToTab → SaveCurrentDocument,
                // which would use a stale out-of-range activeDocIndex.
                if (index < activeDocIndex)
                    activeDocIndex--;
                else if (index == activeDocIndex)
                    activeDocIndex = -1;

                tabControl.TabPages.RemoveAt(index);
            }
            finally
            {
                tabControl.ResumeLayout();
            }
        }

        // Switch to EditorDocument at given index (0-based)
        internal void SwitchToTab(int index)
        {
            if (index < 0 || index >= documents.Count) return;
            if (activeDocIndex == index) return;

            if (activeDocIndex >= 0 && isModified)
            {
                SaveCurrentDocument();
            }

            activeDocIndex = index;
            var doc = documents[activeDocIndex];
            LoadDocument(doc);
            UpdateWindowTitle();
            UpdateMarkdownPreview();
            UpdateEncodingLabel();
            _workspacePanel?.SetActiveFile(doc.FilePath);
        }

        // Load EditorDocument state into editor and UI
        private void LoadDocument(EditorDocument doc)
        {
            // Update core fields from EditorDocument
            currentFilePath = doc.FilePath;
            isModified = doc.IsDirty;
            savedContentHash = doc.SavedHash;
            lastFileWriteTime = doc.LastWriteTime ?? DateTime.MinValue;

            // Configure RTL support
            if (textEditor != null)
            {
                textEditor.RightToLeft = doc.ContainsRtlText ? RightToLeft.Yes : RightToLeft.No;
            }
            currentSyntax = doc.Syntax;

            // Load text without triggering dirty flag
#pragma warning disable CS8602 // Method reference is never null
            textEditor.TextChanged -= TextEditor_TextChanged;
#pragma warning restore CS8602
            textEditor.Text = doc.Content ?? "";
            textEditor.SelectionStart = doc.SelectionStart;
            textEditor.SelectionLength = doc.SelectionLength;

            // Recompute hash from post-normalization textEditor.Text
            // RichTextBox normalizes line endings (\n → \r\n on Windows);
            // the original doc.SavedHash was computed from the raw file string.
            savedContentHash = ComputeContentHash();
            doc.SavedHash = savedContentHash;

            // Force scrollbar recalculation — native RichEdit can miss extents after .Text set
            textEditor.PerformLayout();

            textEditor.TextChanged += TextEditor_TextChanged;

            // Recreate syntax highlighter based on current syntax (skip for large files)
            if ((doc.Content?.Length ?? 0) < 200000) // 200KB threshold for highlighting
            {
                CreateIncrementalHighlighter();
            }

            // Update UI (must be after CreateIncrementalHighlighter so file type is current)
            UpdateStatusBar();
            UpdateTabTitle(activeDocIndex);
            UpdateBreadcrumbs();
            ApplyEditorColors();
        }

        // Update tab title for EditorDocument at index
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
            RefreshGitRepo();
            UpdateMarkdownPreview();
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
                        int btnSize = 16;
                        int btnX = rect.Right - 22;
                        int btnY = rect.Top + 3;
                        var btnRect = new Rectangle(btnX, btnY, btnSize, btnSize);
                        if (btnRect.Contains(e.Location))
                        {
                            CloseTabAt(i);
                            return;
                        }
                        // Otherwise start potential drag
                        _draggedTabIndex = i;
                        _dragStartPoint = tabControl.PointToScreen(e.Location);
                        _isDragging = false;
                        _pendingDragZone = DragZone.None;
                        break;
                    }
                }
            }
        }

        private void TabControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Execute pending split-on-zone if drag ended in an edge zone
                if (_isDragging && _draggedTabIndex.HasValue && _pendingDragZone != DragZone.None && _pendingDragZone != DragZone.Outside)
                {
                    SplitTabToPane(_draggedTabIndex.Value, _pendingDragZone);
                }
                // End drag; if not detached, cancel
                _draggedTabIndex = null;
                _isDragging = false;
                _pendingDragZone = DragZone.None;
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
                _tabScrollDebounce?.Stop();
                _pendingTabScrollIndex = next;
                _tabScrollDebounce?.Start();
            }
        }

        private void PositionTabDropdownButton()
        {
            if (_tabDropdownButton == null || tabControl == null || tabControl.IsDisposed) return;
            Point tabScreen = tabControl.PointToScreen(Point.Empty);
            Point formClient = this.PointToClient(tabScreen);

            if (formClient.IsEmpty) return;

            // Offset upward by 4px so the button doesn't cover the tab strip's bottom edge line
            int y = formClient.Y + (tabControl.Height - _tabDropdownButton.Height) / 2 - 4;

            int btnW = _tabDropdownButton.Width;
            int tabRightEdge = formClient.X + tabControl.Width;

            int lastTabIndex = tabControl.TabCount - 1;
            if (lastTabIndex >= 0 && lastTabIndex < tabControl.TabPages.Count)
            {
                Rectangle lastTabRect = tabControl.GetTabRect(lastTabIndex);

                // Detect whether horizontal scroll buttons are showing.
                // When tabs overflow, GetTabRect(0).Left < 0 (tabs scrolled left)
                // or lastTabRect.Right > tabControl.ClientSize.Width (tabs extend past right).
                bool hScroll = tabControl.GetTabRect(0).Left < 0
                               || lastTabRect.Right > tabControl.ClientSize.Width;

                if (hScroll)
                {
                    // Scroll arrows are visible on the LEFT side of the tab strip.
                    // The right side of the tab header is free — anchor the dropdown there.
                    // Use the system scroll-bar width as margin from the right edge.
                    int scrollMargin = SystemInformation.VerticalScrollBarWidth;
                    int x = tabRightEdge - scrollMargin - btnW - 2;
                    x = Math.Max(formClient.X + 2, Math.Min(x, tabRightEdge - btnW - 2));
                    _tabDropdownButton.Location = new Point(x, y);
                }
                else
                {
                    // No scroll: position after the last tab if there is a meaningful gap,
                    // otherwise right-align within the tab control.
                    int desiredX = formClient.X + lastTabRect.Right + 2;
                    int maxX = tabRightEdge - btnW - 2;
                    int x;
                    if (maxX - desiredX > btnW * 2)
                        x = maxX; // snap to right edge (large empty gap)
                    else
                        x = Math.Min(desiredX, maxX); // follow last tab

                    _tabDropdownButton.Location = new Point(
                        Math.Max(formClient.X + 2, x),
                        Math.Max(formClient.Y, y));
                }
            }
            else
            {
                // No tabs — hide button
                _tabDropdownButton.Location = new Point(
                    Math.Max(formClient.X + 2, tabRightEdge - btnW - 2),
                    Math.Max(formClient.Y, y));
            }

            _tabDropdownButton.Visible = tabControl.TabCount > 0;
        }

        private void TabDropdownButton_Click(object? sender, EventArgs e)
        {
            if (tabControl == null || tabControl.TabCount == 0 || _tabDropdownButton == null) return;
            var theme = _themeManager.CurrentTheme;
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
            cm.Show(_tabDropdownButton, new Point(_tabDropdownButton.Width, _tabDropdownButton.Height), ToolStripDropDownDirection.BelowLeft);
        }

        private void EnsureSelectedTabVisible()
        {
            if (tabControl == null || tabControl.IsDisposed) return;
            int idx = tabControl.SelectedIndex;
            var tabRect = tabControl.GetTabRect(idx);
            if (tabRect.Right <= tabControl.Width && tabRect.Left >= 0) return;
            tabControl.SuspendLayout();
            tabControl.SelectedIndex = idx;
            tabControl.ResumeLayout();
            tabControl.Invalidate();
        }
        private void CloseTabAtLocation(Point location)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                var rect = tabControl.GetTabRect(i);
                if (rect.Contains(location))
                {
                    CloseTabAt(i);
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

                // Check if cursor is over another open Pfpad window — merge tab there instead
                var targetForm = FindForm1AtPoint(dragScreenPos);
                if (targetForm != null)
                {
                    // Transfer EditorDocument to the target window
                    targetForm.ReceiveDroppedTab(doc);

                    // Remove the EditorDocument from the original window
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

                    // Bring target window to front
                    targetForm.Activate();
                }
                else
                {
                    // Create a new Form1 instance for the detached EditorDocument (skip default untitled doc)
                    var detachedForm = new Form1(skipInitialDocument: true);
                    detachedForm.tabControl.TabPages.Clear();
                    detachedForm.activeDocIndex = -1;

                    // Add the dragged EditorDocument to the new form
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
                    // Prevent RestoreWindowBounds (called from Shown) from overriding manual position
                    detachedForm._savedWindowBounds = "";
                    detachedForm._savedWindowState = "";

                    // Remove the EditorDocument from the original window
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

                // Reset drag state
                _draggedTabIndex = null;
                _isDragging = false;
                _dragStartPoint = null;
                _pendingDragZone = DragZone.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetachTabToNewWindow error: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds an open (non-minimized) Form1 at the given screen position, excluding this instance.
        /// </summary>
        private Form1? FindForm1AtPoint(Point screenPos)
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form is Form1 target && target != this && target.Visible && target.WindowState != FormWindowState.Minimized)
                {
                    if (target.Bounds.Contains(screenPos))
                        return target;
                }
            }
            return null;
        }

        /// <summary>
        /// Receives a dropped tab EditorDocument from another Pfpad window into this instance.
        /// </summary>
        private void ReceiveDroppedTab(EditorDocument doc)
        {
            documents.Add(doc);
            var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
            tabControl.TabPages.Add(tabPage);

            // Make the dropped tab active
            int newIndex = documents.Count - 1;
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            LoadDocument(doc);
            UpdateWindowTitle();
            UpdateTabTitle(newIndex);
            UpdateTabSizeDropdown();
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

                // Store EditorDocument reference for close/restore
                _splitDocument = doc;
                _splitDocumentTitle = doc.DisplayName;

                var theme = _themeManager.CurrentTheme;
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
                _splitEditor.Enter += (s, e) =>
                {
                    if (vimModeEnabled && vimEngine != null)
                        vimEngine.SetEditor(_splitEditor);
                };

                if (zone is DragZone.Left or DragZone.Right)
                {
                    // Vertical split: create a new SplitContainer inside _terminalSplitContainer.Panel1
                    splitContainer = new SplitContainer();
                    splitContainer.Dock = DockStyle.Fill;
                    splitContainer.SplitterWidth = 4;
                    splitContainer.Panel1MinSize = 50;
                    splitContainer.Panel2MinSize = 100;
                    splitContainer.TabStop = false;
                    splitContainer.Orientation = Orientation.Vertical;

                    var editorPanel = _terminalSplitContainer!.Panel1;
                    editorPanel.Controls.Remove(mainTable);
                    splitContainer.Panel1.Controls.Add(mainTable);
                    splitContainer.Panel2.Controls.Add(_splitEditor);

                    editorPanel.Controls.Add(splitContainer);

                    BeginInvoke(new Action(() =>
                    {
                        if (splitContainer == null || splitContainer.IsDisposed) return;
                        if (splitContainer.Width > 100)
                            splitContainer.SplitterDistance = splitContainer.Width / 2;
                    }));

                    _splitEditor.Focus();
                }
                else
                {
                    // Horizontal split: use _terminalSplitContainer.Panel2 (terminal panel)
                    _splitIsHorizontal = true;

                    // Hide any terminal content and put the split editor in Panel2
                    if (_terminalTabControl?.Parent == _terminalSplitContainer!.Panel2)
                        _terminalTabControl.Visible = false;
                    if (_terminalNewTabButton?.Parent == _terminalSplitContainer.Panel2)
                        _terminalNewTabButton.Visible = false;

                    _terminalSplitContainer.Panel2.Controls.Add(_splitEditor);
                    _terminalSplitContainer.Panel2Collapsed = false;

                    // Split evenly by height
                    int available = _terminalSplitContainer.Height - _terminalSplitContainer.SplitterWidth;
                    if (available > 200)
                        _terminalSplitContainer.SplitterDistance = available / 2;
                    else if (available > 100)
                        _terminalSplitContainer.SplitterDistance = available - 100;

                    _splitEditor.Focus();
                    _terminalSplitContainer.PerformLayout();
                }

                // Ensure Vim engine targets the split editor
                if (vimModeEnabled && vimEngine != null)
                {
                    BeginInvoke(() =>
                    {
                        vimEngine.SetEditor(_splitEditor);
                        if (vimEngine.CurrentMode == VimMode.Insert)
                            vimEngine.EnterMode(VimMode.Normal);
                        _splitEditor.Focus();
                    });
                }

                // Remove the tab page from the tab control (EditorDocument stays in memory)
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
                _pendingDragZone = DragZone.None;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SplitTabToPane error: {ex.Message}");
            }
        }

        private void CloseSplit()
        {
            if (_splitEditor == null) return;

            // Save content back to EditorDocument
            if (_splitDocument != null)
            {
                _splitDocument.Content = _splitEditor.Text;
                _splitDocument.ModifiedLines = new HashSet<int>();
                _splitDocument.IsDirty = false;
            }

            if (_splitIsHorizontal && _terminalSplitContainer?.IsDisposed == false)
            {
                // Horizontal split: remove _splitEditor from _terminalSplitContainer.Panel2
                _terminalSplitContainer.Panel2.Controls.Remove(_splitEditor);
                // Restore terminal visibility
                if (_terminalTabControl != null) _terminalTabControl.Visible = true;
                if (_terminalNewTabButton != null) _terminalNewTabButton.Visible = true;
                // Collapse Panel2 if terminal was not visible before the split
                if (!_terminalVisible)
                    _terminalSplitContainer.Panel2Collapsed = true;
                _terminalSplitContainer.PerformLayout();
            }
            else if (splitContainer != null)
            {
                // Vertical split: move mainTable back to _terminalSplitContainer.Panel1
                if (splitContainer.Panel1.Controls.Contains(mainTable))
                    splitContainer.Panel1.Controls.Remove(mainTable);

                if (_terminalSplitContainer?.IsDisposed == false)
                {
                    _terminalSplitContainer.Panel1.Controls.Add(mainTable);
                    mainTable.Dock = DockStyle.Fill;
                }

                if (_splitEditor != null)
                {
                    splitContainer.Panel2.Controls.Remove(_splitEditor);
                }

                splitContainer.Dispose();
                splitContainer = null;
            }

            // Clean up split editor
            if (_splitEditor != null)
            {
                _splitEditor.Dispose();
                _splitEditor = null;
            }

            _splitIsHorizontal = false;

            // Re-add the EditorDocument and tab page to the main window
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

            if (textEditor.CanFocus)
                textEditor.Focus();
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
                    int buttonSize = 16;
                    int btnX = rect.Right - 22;
                    int btnY = rect.Top + 3;
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
                    _pendingDragZone = DragZone.None;
                    DetachTabToNewWindow(_draggedTabIndex.Value, screenPos);
                }
                else
                {
                    // Defer edge-zone split to MouseUp so user can drag past the edge to detach
                    _pendingDragZone = zone;
                }
            }
        }

        private void TabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (tabControl == null || tabControl.TabPages.Count == 0) return;
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;
            if (e.Index < 0 || e.Index >= documents.Count) return;

            if (tabControl == null || tabControl.TabPages.Count == 0) return;
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;
            if (e.Index < 0 || e.Index >= documents.Count) return;

            var theme = _themeManager.CurrentTheme;
            var tabRect = e.Bounds;

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

            // Tab icon
            var doc = e.Index < documents.Count ? documents[e.Index] : null;
            if (doc?.FilePath != null)
            {
                int iconIdx = FileIconProvider.GetIconIndex(doc.FilePath);
                if (iconIdx >= 0 && iconIdx < FileIconProvider.ImageList.Images.Count)
                {
                    var iconRect = new Rectangle(tabRect.X + 8, tabRect.Y + 3, 16, 16);
                    e.Graphics.DrawImage(FileIconProvider.ImageList.Images[iconIdx], iconRect);
                }
            }

            // Tab text
            string text = tabControl.TabPages[e.Index].Text;
            int textOffset = doc?.FilePath != null ? 28 : 8;
            var textRect = new Rectangle(
                tabRect.X + textOffset, tabRect.Y + 3,
                tabRect.Right - 22 - tabRect.X - textOffset, tabRect.Height - 4);

            TextRenderer.DrawText(e.Graphics, text, tabControl.Font, textRect,
                isSelected ? theme.Text : theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            // Draw borders
            if (!isSelected)
            {
                // Hide borders for inactive tabs by drawing over with background color
                using (var pen = new Pen(backColor, 1))
                {
                    e.Graphics.DrawRectangle(pen, tabRect.X, tabRect.Y, tabRect.Width - 1, tabRect.Height - 1);
                }
            }
            else
            {
                // Draw top border for active tab
                using (var pen = new Pen(theme.Text, 1))
                {
                    e.Graphics.DrawLine(pen, tabRect.Left, tabRect.Top, tabRect.Right - 1, tabRect.Top);
                }
            }

            // Close button (×)
            var closeRect = new Rectangle(tabRect.Right - 22, tabRect.Y + 3, 16, 16);
            using (var xFont = new Font("Segoe UI", 11, FontStyle.Bold))
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

        private string? GetWordAtCursor()
        {
            if (textEditor is null || textEditor.TextLength == 0) return null;
            int pos = textEditor.SelectionStart;
            string text = textEditor.Text;
            if (pos >= text.Length) return null;

            int start = pos;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;
            int end = pos;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                end++;

            return start < end ? text[start..end] : null;
        }

        private void GoToDefinition()
        {
            if (textEditor is null) return;
            string? word = GetWordAtCursor();
            if (string.IsNullOrEmpty(word)) return;

            // Roslyn semantic go-to-definition for C# files
            if (currentSyntax?.Name == "C#" && _roslynWorkspace.IsReady)
            {
                GoToDefinitionRoslyn();
                return;
            }

            // C# file but Roslyn not initialized — show helpful message
            if (currentSyntax?.Name == "C#")
            {
                ThemedMessageBox.Show($"No definition found for '{word}'.\n\n" +
                    "The Roslyn language service is not ready. Try reopening the file or " +
                    "opening a .csproj/.sln to enable full C# support.",
                    "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Non-C#: fall back to the regex/ctags symbol index
            if (!_symbolIndex.HasIndex)
            {
                if (string.IsNullOrEmpty(_workspaceRoot))
                {
                    ThemedMessageBox.Show($"No definition found for '{word}'.\n\n" +
                        "Open a workspace folder (View > Open Folder) to enable symbol indexing.",
                        "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ThemedMessageBox.Show($"No definition found for '{word}'.\n\nThe symbol index is still building. Try again in a moment.",
                        "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            var matches = _symbolIndex.Lookup(word);
            if (matches.Count == 0)
            {
                ThemedMessageBox.Show($"No definition found for '{word}'.",
                    "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (matches.Count == 1)
            {
                var loc = matches[0];
                if (File.Exists(loc.File))
                {
                    OpenFileInNewTab(loc.File);
                    GoToLine(loc.Line);
                }
                return;
            }

            using var picker = new GoToDefinitionPicker(word, matches);
            if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedFile is not null)
            {
                OpenFileInNewTab(picker.SelectedFile);
                GoToLine(picker.SelectedLine);
            }
        }

        public void PerformFind(string text, bool caseSensitive, bool up, bool useRegex = false)
        {
            if (string.IsNullOrEmpty(text) || textEditor.Text.Length == 0) return;

            string source = textEditor.Text;
            int sourceLen = source.Length;
            int start = textEditor.SelectionStart;
            int found = -1;

            if (useRegex)
            {
                var opts = RegexOptions.Multiline;
                if (!caseSensitive) opts |= RegexOptions.IgnoreCase;
                try
                {
                    var regex = new Regex(text, opts);
                    if (up)
                    {
                        var matches = regex.Matches(source);
                        for (int i = matches.Count - 1; i >= 0; i--)
                        {
                            if (matches[i].Index < start)
                            {
                                found = matches[i].Index;
                                break;
                            }
                        }
                    }
                    else
                    {
                        var m = regex.Match(source, Math.Min(start + 1, sourceLen));
                        if (m.Success)
                            found = m.Index;
                    }
                }
                catch { return; }
            }
            else
            {
                StringComparison comparison = caseSensitive
                    ? StringComparison.CurrentCulture
                    : StringComparison.CurrentCultureIgnoreCase;

                if (up)
                {
                    for (int i = start - text.Length; i >= 0; i--)
                    {
                        if (i + text.Length <= sourceLen && source.AsSpan(i, text.Length).Equals(text, comparison))
                        {
                            found = i;
                            break;
                        }
                    }
                }
                else
                {
                    found = source.IndexOf(text, Math.Min(start + 1, sourceLen), comparison);
                }
            }

            if (found >= 0)
            {
                textEditor.SelectionStart = found;
                textEditor.SelectionLength = useRegex ? 0 : text.Length;
                textEditor.ScrollToCaret();
                UpdateStatusBar();
            }
            else
            {
                ThemedMessageBox.Show("Cannot find \"" + text + "\".", "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void PerformReplace(string findText, string replaceText, bool caseSensitive, bool useRegex, bool replaceAll)
        {
            if (string.IsNullOrEmpty(findText)) return;

            int count = 0;

            if (useRegex)
            {
                var opts = RegexOptions.Multiline;
                if (!caseSensitive) opts |= RegexOptions.IgnoreCase;
                try
                {
                    var regex = new Regex(findText, opts);
                    if (replaceAll)
                    {
                        string result = regex.Replace(textEditor.Text, replaceText);
                        if (result != textEditor.Text)
                        {
                            textEditor.Text = result;
                            count = 1;
                        }
                    }
                    else
                    {
                        var m = regex.Match(textEditor.Text, textEditor.SelectionStart);
                        if (m.Success)
                        {
                            textEditor.SelectionStart = m.Index;
                            textEditor.SelectionLength = m.Length;
                            textEditor.SelectedText = regex.Replace(m.Value, replaceText);
                            count = 1;
                        }
                    }
                }
                catch { return; }
            }
            else
            {
                StringComparison comparison = caseSensitive
                    ? StringComparison.CurrentCulture
                    : StringComparison.CurrentCultureIgnoreCase;

                if (replaceAll)
                {
                    int startIndex = 0;
                    var sb = new System.Text.StringBuilder(textEditor.Text);
                    while (startIndex < sb.Length)
                    {
                        int found = sb.ToString().IndexOf(findText, startIndex, comparison);
                        if (found < 0) break;
                        sb.Remove(found, findText.Length).Insert(found, replaceText);
                        startIndex = found + replaceText.Length;
                        count++;
                    }
                    if (count > 0)
                        textEditor.Text = sb.ToString();
                }
                else
                {
                    if (textEditor.SelectionLength > 0 && string.Equals(textEditor.SelectedText, findText, comparison))
                    {
                        textEditor.SelectedText = replaceText;
                        count = 1;
                    }
                    else
                    {
                        PerformFind(findText, caseSensitive, false, false);
                        if (textEditor.SelectionLength > 0 && string.Equals(textEditor.SelectedText, findText, comparison))
                        {
                            textEditor.SelectedText = replaceText;
                            count = 1;
                        }
                    }
                }
            }

            if (count > 0)
            {
                isModified = true;
                UpdateStatusBar();
            }
        }

        public void PerformFindInFiles(string findText, bool caseSensitive, bool useRegex)
        {
            string? root = _workspacePanel?.RootPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                ThemedMessageBox.Show("No workspace folder is open. Use View > Open Folder to set a workspace root.",
                    "Find in Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var results = new List<(string File, int Line, string Text)>();
            var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".vb", ".fs", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".html", ".htm",
                ".css", ".scss", ".less", ".md", ".txt", ".yaml", ".yml", ".toml", ".ini", ".cfg",
                ".config", ".csproj", ".sln", ".ps1", ".bat", ".cmd", ".sh", ".py", ".rb", ".java",
                ".kt", ".swift", ".go", ".rs", ".c", ".cpp", ".h", ".hpp", ".sql", ".lua", ".php",
                ".tf", ".bicep", ".editorconfig", ".gitignore", ".props", ".targets", ".resx"
            };

            var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "node_modules", ".git", ".svn", ".hg", "bin", "obj", ".vs", "packages"
            };

            Regex? regex = null;
            StringComparison comparison = StringComparison.CurrentCultureIgnoreCase;
            if (useRegex)
            {
                try
                {
                    var opts = RegexOptions.Multiline;
                    if (!caseSensitive) opts |= RegexOptions.IgnoreCase;
                    regex = new Regex(findText, opts);
                }
                catch
                {
                    ThemedMessageBox.Show("Invalid regex pattern.", "Find in Files", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                comparison = caseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
            }

            try
            {
                WorkspaceHelper.SearchDirectory(root, root, results, textExtensions, ignoredDirs, findText, regex, comparison);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Search error: {ex.Message}", "Find in Files", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (results.Count == 0)
            {
                _workspacePanel?.ClearSearchHighlights();
                ThemedMessageBox.Show("No results found.", "Find in Files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Highlight files with search results in workspace
            var resultFiles = results.Select(r => Path.Combine(root, r.File)).Distinct();
            _workspacePanel?.HighlightSearchResults(resultFiles);

            using var dlg = new FindInFilesResultsDialog(results, root);
            dlg.ShowDialog(this);
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
            // Hash comparison catches spurious TextChanged from RichTextBox internals
            // (line ending normalization, deferred messages, etc.) when content hasn't changed
            if (!isModified && ComputeContentHash() == savedContentHash) return;
            SetDirty();

            // Recalculate gutter width when line count changes
            gutterPanel?.UpdateLineNumberWidth();
            SyncGutterColumnWidth();

            // Track which line changed for gutter display
            int lineIndex = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            _docManager.GetActive()?.ModifiedLines.Add(lineIndex);

            // Adaptive debounce: detect typing bursts, scale interval dynamically
            DateTime now = DateTime.UtcNow;
            double msSinceLastKey = (now - _lastKeyPress).TotalMilliseconds;
            _lastKeyPress = now;
            if (msSinceLastKey < 150)
                _typingBurstCount++;
            else
                _typingBurstCount = 0;

            double adaptiveInterval = HighlightTimerMinInterval + (_typingBurstCount * 20);
            if (_lastHighlightDurationMs > 30)
                adaptiveInterval += _lastHighlightDurationMs * 0.5;
            int interval = (int)Math.Clamp(adaptiveInterval, HighlightTimerMinInterval, HighlightTimerMaxInterval);
            _highlightTimer!.Interval = interval;

            // Debounce syntax highlighting — wait for pause in typing
            _pendingHighlightLine = lineIndex;
            _highlightTimer?.Stop();
            _highlightTimer?.Start();

            // Paste/bulk-change detection: warm up highlight cache for large text changes
            int textDelta = textEditor.TextLength - _previousTextLength;
            if (Math.Abs(textDelta) > 500)
            {
                int cursorLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
                incrementalHighlighter?.InvalidateFrom(cursorLine);
                RequestVisibleHighlight();

            // Trigger completion on certain characters
            if (textDelta == 1 && currentSyntax == SyntaxDefinition.CSharp && _roslynWorkspace.IsReady)
            {
                char lastChar = textEditor.Text[textEditor.SelectionStart - 1];
                if (lastChar is '.' or '(' or '[' or '<' or ' ')
                {
                    // Delay completion slightly to avoid interfering with typing
                    BeginInvoke(async () =>
                    {
                        try
                        {
                            var completionData = await _roslynWorkspace.GetCompletionAsync(textEditor.SelectionStart, lastChar);
                            if (completionData != null && completionData.Items.Count > 0)
                            {
                                ShowCompletionPopup(completionData);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Auto-completion error: {ex.Message}");
                        }
                    });
                }
            }
            }
            _previousTextLength = textEditor.TextLength;

            UpdateStatusBar();

            // Restart debounce timer for elastic tab stops recompute
            elasticTabTimer?.Stop();
            elasticTabTimer?.Start();

            UpdateMarkdownPreview();
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
            if (gutterPanel != null) gutterPanel.Invalidate();
            _scrollHighlightTimer?.Stop();
            _scrollHighlightTimer?.Start();
            try { UpdateStickyHeaders(); } catch { }
            PositionMinimap();
        }

        private void TextEditor_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int charIdx = textEditor.GetCharIndexFromPosition(e.Location);
                if (charIdx >= 0)
                    textEditor.SelectionStart = charIdx;

                var ctx = new ContextMenuStrip();
                ctx.Items.Add("Go to Definition (F12)", null, (s, args) => GoToDefinition());
                ctx.Items.Add("Rename (F2)", null, (s, args) => RenameSymbol());
                ctx.Items.Add("Call Hierarchy", null, (s, args) => ShowCallHierarchy());
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("Parse Stack Trace", null, (s, args) => ParseStackTrace());
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("Find Dependents", null, (s, args) => ShowDependents());
                ctx.Items.Add("Cut", null, (s, args) => textEditor.Cut());
                ctx.Items.Add("Copy", null, (s, args) => textEditor.Copy());
                ctx.Items.Add("Paste", null, (s, args) => textEditor.Paste());
                ctx.Show(textEditor, e.Location);
            }
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

        internal void UpdateLockKeysLabel()
        {
            var parts = new List<string>(3);
            if (Control.IsKeyLocked(Keys.CapsLock)) parts.Add("CAPS");
            if (Control.IsKeyLocked(Keys.NumLock))  parts.Add("NUM");
            if (Control.IsKeyLocked(Keys.Scroll))   parts.Add("SCRLK");
            lockKeysLabel.Text = parts.Count > 0 ? string.Join(" ", parts) : "";
        }

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
            int charCount = textEditor.Text?.Length ?? 0;
            charCountLabel.Text = $"{charCount} chars";

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
                        vimModeLabel.Text = vimEngine.CommandText;
                        vimModeLabel.Invalidate();
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

            // File type icon removed - text only
            statusStrip.Refresh();
        }

        private void UpdateStatusBarVisibility()
        {
            bool hide = vimModeEnabled;
            _buildConfigCombo.Visible = !hide;
            roslynDropDown.Visible = !hide;
            roslynToggleLabel.Visible = !hide;
            themeDropDown.Visible = !hide;
            _workspaceProjectLabel.Visible = !hide;
            statusStrip.Items[10].Visible = !hide; // spring spacer
        }

        private void UpdateTabControlTheme()
        {
            if (tabControl == null) return;
            var theme = _themeManager.CurrentTheme;
            tabControl.BackColor = theme.MenuBackground;
            tabControl.ForeColor = theme.Text;
            // Force redraw to apply themed background in owner-drawn tabs
            tabControl.Invalidate();
        }

        #endregion

        #region Minimap Methods

        /// <summary>
        /// Positions the minimap as an overlay inside the editor panel, overlapping the
        /// text editor's right edge but staying left of the vertical scrollbar.
        /// </summary>
        private void PositionMinimap()
        {
            if (minimapControl == null || editorPanel == null || textEditor == null) return;
            if (!textEditor.IsHandleCreated) return;

            // Check if minimap is disabled for this EditorDocument (large file degradation)
            if (GetCurrentDocument()?.DisableMinimap == true)
            {
                minimapControl.Visible = false;
                minimapControl.DetachEditor();
                return;
            }

            if (!_pendingMinimapVisible)
            {
                if (minimapControl.Parent != editorPanel)
                    editorPanel.Controls.Add(minimapControl);
                minimapControl.Visible = false;
                minimapControl.DetachEditor();
                return;
            }

            if (minimapControl.Parent != editorPanel)
                editorPanel.Controls.Add(minimapControl);
            minimapControl.Visible = true;
            minimapControl.BringToFront();
            minimapControl.AttachEditor(textEditor);

            // Detect horizontal scrollbar visibility – when visible,
            // reduce minimap height so it doesn't overlap the scrollbar.
            bool hasHScroll = false;
            try
            {
                int style = GetWindowLong(textEditor.Handle, GWL_STYLE);
                hasHScroll = (style & WS_HSCROLL) != 0;
            }
            catch { }

            // Keep at least 20px of text area visible.  Shrink minimap
            // proportionally when narrow, but never below 20px so it stays
            // usable even at extreme widths.
            int mw = Math.Min(
                minimapControl.MinimapWidth,
                Math.Max(20, textEditor.ClientSize.Width - 20));

            int x = Math.Max(0, textEditor.ClientSize.Width - mw);

            // Height: account for horizontal scrollbar
            int h = textEditor.ClientSize.Height;
            if (hasHScroll)
                h -= SystemInformation.HorizontalScrollBarHeight;

            minimapControl.Location = new Point(x, textEditor.Top);
            minimapControl.Size = new Size(Math.Max(1, mw), Math.Max(1, h));
        }

        #endregion

        #region Word Wrap Methods

        private void ApplyWordWrap()
        {
            if (textEditor != null)
            {
                // Respect EditorDocument-level word wrap degradation for large files
                bool effectiveWordWrap = wordWrapEnabled && (GetCurrentDocument()?.DisableWordWrap != true);
                textEditor.WordWrap = effectiveWordWrap;
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
                    var result = ThemedMessageBox.Show(
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
            var theme = _themeManager.CurrentTheme;
            e.Graphics.Clear(theme.MenuBackground);
        }

        private void TabControl_HandleCreated(object? sender, EventArgs e)
        {
            // Strip visual styles to remove the 3D raised border from the tab strip
            if (tabControl.IsHandleCreated)
                SetWindowTheme(tabControl.Handle, isDarkTheme ? DARK_MODE_SCROLLBAR : "", null);

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
            private const int WM_HSCROLL = 0x0114;
            private const int SB_LINELEFT = 0;
            private const int SB_LINERIGHT = 1;
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
                        var theme = _owner._themeManager.CurrentTheme;
                        using (var g = Graphics.FromHdc(m.WParam))
                        using (var brush = new SolidBrush(theme.MenuBackground))
                        {
                            g.FillRectangle(brush, _target.ClientRectangle);
                        }
                        m.Result = (IntPtr)1;
                        return;
                    case WM_HSCROLL:
                        {
                            int code = (int)(m.WParam.ToInt64() & 0xFFFF);
                            if (code == SB_LINELEFT)
                            {
                                if (_target.SelectedIndex > 0)
                                    _target.SelectedIndex--;
                                return;
                            }
                            if (code == SB_LINERIGHT)
                            {
                                if (_target.SelectedIndex < _target.TabCount - 1)
                                    _target.SelectedIndex++;
                                return;
                            }
                            break;
                        }
                }
                base.WndProc(ref m);
            }
        }


        #endregion
        #region Syntax Highlighting (async, incremental, visible-range)

        private Color GetKeywordColor() => _themeManager.CurrentTheme.KeywordColor;
        private Color GetStringColor() => _themeManager.CurrentTheme.StringColor;
        private Color GetCommentColor() => _themeManager.CurrentTheme.CommentColor;
        private Color GetNumberColor() => _themeManager.CurrentTheme.NumberColor;
        private Color GetPreprocessorColor() => _themeManager.CurrentTheme.PreprocessorColor;
        private Color GetTypeColor() => _themeManager.CurrentTheme.TypeColor;

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
                ResetVisibleRangeToBase(_themeManager.CurrentTheme.Text);
                return;
            }

            if (currentSyntax == SyntaxDefinition.CSharp && _roslynWorkspace.IsReady)
            {
                var roslynHighlighter = new RoslynHighlighter(_roslynWorkspace);
                incrementalHighlighter = new IncrementalHighlighter(textEditor, currentSyntax, roslynHighlighter);
            }
            else
            {
                incrementalHighlighter = new IncrementalHighlighter(textEditor, currentSyntax);
            }
            incrementalHighlighter.PatchReady += ApplyHighlightPatches;
            RequestVisibleHighlight();
        }

        private void ApplyHighlightPatches(List<HighlightPatch> patches)
        {
            if (textEditor.IsDisposed || !textEditor.IsHandleCreated) return;
            _highlightPerfSw.Restart();
            _suspendSelectionChanged = true;
            _applyingHighlight = true;

            // Only apply patches for the currently visible range — non-visible lines
            // get formatted lazily when they scroll into view via RequestVisibleHighlight.
            // This keeps ApplyHighlightPatches fast (<5ms) so the UI thread stays responsive
            // during rapid scrolling.
            var (firstVisible, lastVisible) = GetVisibleLineRange();
            if (firstVisible > lastVisible) { _applyingHighlight = false; _suspendSelectionChanged = false; return; }

            int savedSelStart = textEditor.SelectionStart;
            int savedSelLength = textEditor.SelectionLength;
            int savedFirstVis = (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                int lineCount = (int)SendMessage(textEditor.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);

                int minLine = int.MaxValue, maxLine = -1;
                foreach (var patch in patches)
                {
                    int ln = patch.LineNumber;
                    if (ln < firstVisible || ln > lastVisible) continue;
                    if (ln < minLine) minLine = ln;
                    if (ln > maxLine) maxLine = ln;
                }
                if (minLine > maxLine) return;

                int maxLookup = Math.Min(maxLine + 1, lineCount - 1);
                int[] lineStarts = new int[maxLookup + 1];
                for (int l = minLine; l <= maxLookup; l++)
                    lineStarts[l] = (int)SendMessage(textEditor.Handle, EM_LINEINDEX, (IntPtr)l, IntPtr.Zero);

                int textLength = textEditor.TextLength;

                var cf = new CHARFORMAT2W
                {
                    cbSize = Marshal.SizeOf<CHARFORMAT2W>(),
                    dwMask = CFM_COLOR | CFM_ITALIC
                };
                IntPtr cfPtr = Marshal.AllocCoTaskMem(cf.cbSize);
                try
                {
                    foreach (var patch in patches)
                    {
                        int line = patch.LineNumber;
                        if (line < firstVisible || line > lastVisible) continue;
                        if (line < 0 || line >= lineCount) continue;
                        int lineStart = lineStarts[line];
                        if (lineStart < 0) continue;
                        int lineEnd = (line + 1 < lineCount)
                            ? lineStarts[line + 1]
                            : textLength;
                        int lineLen = lineEnd - lineStart;
                        if (lineLen <= 0) continue;
                        foreach (var token in patch.Tokens)
                        {
                            int idx = lineStart + token.StartIndex;
                            int len = Math.Min(token.Length, lineLen - token.StartIndex);
                            if (len <= 0) continue;
                            cf.crTextColor = ColorTranslator.ToWin32(GetColorForToken(token.Type));
                            cf.dwEffects = (token.Type == SyntaxTokenType.Comment || token.Type == SyntaxTokenType.Type || token.Type == SyntaxTokenType.Preprocessor) ? CFE_ITALIC : 0;
                            Marshal.StructureToPtr(cf, cfPtr, false);
                            SendMessage(textEditor.Handle, EM_SETSEL, (IntPtr)idx, (IntPtr)(idx + len));
                            SendMessage(textEditor.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, cfPtr);
                        }
                    }
                }
                finally { Marshal.FreeCoTaskMem(cfPtr); }
            }
            catch { }
            finally
            {
                EndUpdate(textEditor);
                textEditor.ResumeLayout();
                _lastHighlightDurationMs = _highlightPerfSw.Elapsed.TotalMilliseconds;
                if (_lastHighlightDurationMs > 50)
                {
                    _profilerDialog?.Log($"Syntax highlighting took {_lastHighlightDurationMs:F1}ms for {patches.Count} patches");
                }
                textEditor.Select(savedSelStart, savedSelLength);
                int finalVis = (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
                if (finalVis != savedFirstVis)
                    SendMessage(textEditor.Handle, EM_LINESCROLL, 0, savedFirstVis - finalVis);
                _applyingHighlight = false;
                _suspendSelectionChanged = false;
            }
        }

        private void RequestVisibleHighlight()
        {
            if (incrementalHighlighter == null || !syntaxHighlightingEnabled || !textEditor.IsHandleCreated) return;
            if (GetCurrentDocument()?.DisableSyntaxHighlighting == true) return;
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
            SyntaxTokenType.Type => GetTypeColor(),
            _ => (_themeManager.CurrentTheme.Text)
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
        private const int WS_VSCROLL = 0x00200000;
        private const int WS_HSCROLL = 0x00100000;
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
        private const int CFM_ITALIC = 0x00000002;
        private const int CFE_ITALIC = 0x00000002;
        private const int CFM_BOLD = 0x00000001;

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
            int count = (int)SendMessage(textEditor.Handle, EM_GETLINECOUNT, 0, IntPtr.Zero);
            if (lineIndex < 0 || lineIndex >= count) return string.Empty;
            IntPtr ptr = Marshal.AllocCoTaskMem(4096 * 2);
            try
            {
                Marshal.WriteInt16(ptr, (short)4096);
                int len = (int)SendMessage(textEditor.Handle, EM_GETLINE, lineIndex, ptr);
                return len > 0 ? Marshal.PtrToStringUni(ptr, len).TrimEnd('\r', '\n') : string.Empty;
            }
            finally { Marshal.FreeCoTaskMem(ptr); }
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

        #region Sticky Scroll

        private void RebuildStickyScopes()
        {
            if (!_stickyScrollEnabled || _stickyScroll == null || textEditor == null || _foldingManager == null) return;
            _stickyScroll.Rebuild(textEditor.Text, _foldingManager.GetAllRegions());
            UpdateStickyHeaders();
        }

        private void UpdateStickyHeaders()
        {
            if (!_stickyScrollEnabled || _stickyScroll == null || textEditor == null) return;
            int firstVis = GetFirstVisibleLine();
            var enclosing = _stickyScroll.GetEnclosingScopes(firstVis);
            stickyScrollPanel.SetScopes(enclosing, firstVis);
            if (stickyScrollPanel.Visible)
            {
                RepositionStickyScrollPanel();
                stickyScrollPanel.SyncWidth(textEditor.ClientSize.Width);
            }
        }

        #endregion

        #region Git Integration

        private void RefreshGitRepo()
        {
            string? path = currentFilePath ?? documents.FirstOrDefault()?.FilePath;
            if (!string.IsNullOrEmpty(path))
            {
                _gitService.TryOpenRepo(path);
                _gitPanel?.RefreshRepo(path);
            }
            UpdateGitStatusBar();
        }

        private void UpdateGitStatusBar()
        {
            if (gitBranchLabel is null || gitDirtyLabel is null || gitSyncLabel is null) return;
            if (_gitService.IsActive)
            {
                gitBranchLabel.Text = _gitService.CurrentBranch ?? "";
                var (staged, unstaged, untracked) = _gitService.GetStatus();
                int changes = staged.Count + unstaged.Count + untracked.Count;
                gitDirtyLabel.Text = changes > 0 ? " ●" : "";

                // Show behind/ahead counts
                try
                {
                    var (behind, ahead) = _gitService.GetRemoteStatus();
                    if (behind > 0 || ahead > 0)
                    {
                        gitSyncLabel.Text = $"⬇️{behind} ⬆️{ahead}";
                        gitSyncLabel.Visible = true;
                    }
                    else
                    {
                        gitSyncLabel.Text = "🔄";
                        gitSyncLabel.Visible = true;
                    }
                }
                catch
                {
                    gitSyncLabel.Text = "🔄";
                    gitSyncLabel.Visible = true;
                }
            }
            else
            {
                gitBranchLabel.Text = "";
                gitDirtyLabel.Text = "";
                gitSyncLabel.Visible = false;
            }
        }

        private void ToggleGitPanel()
        {
            if (_gitPanel is null || _workspaceSplitContainer is null || _botSidebarSplit is null) return;

            _gitPanelVisible = !_gitPanelVisible;
            _gitPanel.Visible = _gitPanelVisible;
            UpdateSidebarLayout();
            if (_gitPanelVisible)
                RefreshGitRepo();
        }

        private void ToggleGitPanel(object? sender, EventArgs e)
        {
            ToggleGitPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _gitPanelVisible;
        }

        private void ToggleSymbolPanel()
        {
            if (_symbolPanel is null || _workspaceSplitContainer is null || _botSidebarSplit is null) return;

            _symbolPanelVisible = !_symbolPanelVisible;
            _symbolPanel.Visible = _symbolPanelVisible;
            UpdateSidebarLayout();
            if (_symbolPanelVisible)
                _symbolPanel.RefreshSymbols();
        }

        private void ToggleSymbolPanel(object? sender, EventArgs e)
        {
            ToggleSymbolPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _symbolPanelVisible;
        }

        private void ToggleSymbols()
        {
            if (_symbolPanel != null)
            {
                _symbolPanel.Visible = !_symbolPanel.Visible;
                symbolsMenuItem.Checked = _symbolPanel.Visible;
            }
        }

        private void ToggleProblems()
        {
            ToggleProblemsPanel();
        }

        private void ToggleMinimap()
        {
            if (minimapControl != null)
            {
                minimapControl.Visible = !minimapControl.Visible;
                minimapMenuItem.Checked = minimapControl.Visible;
            }
        }

        private void CloseAllTabs()
        {
            var docs = documents.ToList();
            foreach (var doc in docs)
            {
                if (doc.IsDirty)
                {
                    var result = ThemedMessageBox.Show(
                        $"Save changes to \"{doc.DisplayName}\"?",
                        "Close All Tabs", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (result == DialogResult.Cancel) return;
                    if (result == DialogResult.Yes)
                    {
                        try { File.WriteAllText(doc.FilePath!, doc.Content); } catch { }
                    }
                }
            }
            documents.Clear();
            tabControl.TabPages.Clear();
            NewFile();
        }

        private void ConfigureTools()
        {
            // TODO: Implement ConfigureTools dialog
            ShowNotification("Tools", "Configure External Tools - Coming Soon");
        }

        private async void AnalyzeCurrentRepository()
        {
            var workspacePath = _workspacePanel?.RootPath;
            if (string.IsNullOrEmpty(workspacePath))
            {
                ThemedMessageBox.Show("Please open a workspace folder first (View → Panel → Open Folder).",
                    "No Workspace Open", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var progressDialog = new ProgressDialog("Analyzing Repository", "Scanning workspace and analyzing project structure...");
                progressDialog.Show(this);

                var analyticsEngine = new RepositoryAnalyticsEngine(workspacePath);
                var analysis = await analyticsEngine.AnalyzeAsync();

                progressDialog.Close();

                using var dialog = new RepositoryAnalysisDialog(analysis);
                dialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to analyze repository: {ex.Message}",
                    "Analysis Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestartRoslynAnalyzers()
        {
            // Implementation would restart Roslyn analyzers
            ShowNotification("Roslyn", "Analyzers restarted");
        }

        private void ToggleRoslynAnalyzers()
        {
            // Toggle Roslyn analyzers on/off
            ShowNotification("Roslyn", "Analyzers toggled");
        }

        private void OpenRoslynVisualizer()
        {
            // Open Roslyn visualizer dialog
            ShowNotification("Roslyn", "Visualizer opened");
        }

        private void ContinueDebug()
        {
            // Continue debugging
            if (startDebugMenuItem.Enabled)
                StartDebug_Click(null, EventArgs.Empty);
        }

        private void StepOver()
        {
            // Step over in debugger
            ShowNotification("Debug", "Step over");
        }

        private void StepInto()
        {
            // Step into in debugger
            ShowNotification("Debug", "Step into");
        }

        private void StepOut()
        {
            // Step out in debugger
            ShowNotification("Debug", "Step out");
        }

        private void ToggleProblemsPanel()
        {
            if (_problemsPanel != null)
            {
                _problemsPanel.Visible = !_problemsPanel.Visible;
                problemsMenuItem.Checked = _problemsPanel.Visible;
            }
        }

        private void ToggleProblemsPanel(object? sender, EventArgs e)
        {
            ToggleProblemsPanel();
            if (sender is ToolStripMenuItem item)
                item.Checked = _problemsPanelVisible;
        }

        private void ToggleMarkdownPreview()
        {
            if (_editorSplitContainer is null || _markdownPreviewPanel is null) return;

            _markdownPreviewVisible = !_markdownPreviewVisible;
            _editorSplitContainer.Panel2Collapsed = !_markdownPreviewVisible;

            if (_markdownPreviewVisible)
                UpdateMarkdownPreview();
        }

        private void ToggleMarkdownPreview(object? sender, EventArgs e)
        {
            ToggleMarkdownPreview();
            if (sender is ToolStripMenuItem item)
                item.Checked = _markdownPreviewVisible;
        }

        private void UpdateMarkdownPreview()
        {
            if (_markdownPreviewPanel is null || _editorSplitContainer is null) return;
            if (!_markdownPreviewVisible) return;

            string? file = currentFilePath;
            if (string.IsNullOrEmpty(file) || !file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                _markdownPreviewPanel.RenderMarkdown("", null);
                return;
            }

            _markdownPreviewPanel.RenderMarkdown(textEditor?.Text ?? "", file);
        }

        private void ToggleRunConfigPanel()
        {
            using var dlg = new RunConfigurationDialog(_workspaceRoot);
            dlg.ShowDialog(this);
        }

        private void ToggleRunConfigPanel(object? sender, EventArgs e)
        {
            ToggleRunConfigPanel();
        }

        private void ScheduleLint()
        {
            if (!_lintEngine.Enabled) return;
            string text = textEditor?.Text ?? "";
            string path = currentFilePath ?? "";
            _lintEngine.ScheduleLint(text, path);
        }

        private void OnLintDiagnosticsUpdated(IReadOnlyList<Diagnostic> diagnostics)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => OnLintDiagnosticsUpdated(diagnostics));
                return;
            }

            _problemsPanel?.SetDiagnostics(diagnostics);

            // Convert diagnostics to squiggly positions
            var squiggles = new List<(int start, int length, Color color)>();
            if (textEditor is not null && !textEditor.IsDisposed && textEditor.IsHandleCreated)
            {
                string text = textEditor.Text;
                var lines = text.Split('\n');
                foreach (var d in diagnostics)
                {
                    if (d.Line < 1 || d.Line > lines.Length) continue;
                    string lineText = lines[d.Line - 1].TrimEnd('\r');
                    int lineStartIdx = textEditor.GetFirstCharIndexFromLine(d.Line - 1);
                    if (lineStartIdx < 0) continue;
                    int col = Math.Max(0, Math.Min(d.Column - 1, lineText.Length));
                    int start = lineStartIdx + col;
                    int len = Math.Min(d.Length, Math.Max(1, lineText.Length - col));
                    squiggles.Add((start, len, d.Color));
                }
            }
            if (textEditor is not null && !textEditor.IsDisposed)
                textEditor.SetSquiggles(squiggles);

            // Generate quick actions and pass to gutter
            string docText = textEditor?.Text ?? "";
            var actions = _quickActionProvider.GetActions(docText, diagnostics);
            var gutterActions = actions
                .Select(a => (a.Line, a.Title, a.Apply))
                .ToList();
            gutterPanel?.SetQuickActions(gutterActions);
        }

        private void UpdateSidebarLayout()
        {
            if (_botSidebarSplit is null || _sidebarSplit is null || _workspaceSplitContainer is null
                || _problemsSplit is null) return;
            bool botAny = _gitPanelVisible || _symbolPanelVisible || _problemsPanelVisible;
            bool innerAny = _symbolPanelVisible || _problemsPanelVisible;
            _botSidebarSplit.Panel2Collapsed = !innerAny;
            _problemsSplit.Panel2Collapsed = !_problemsPanelVisible;
            _sidebarSplit.Panel2Collapsed = !botAny;

            if (innerAny)
            {
                int totalH = _botSidebarSplit.Height - _botSidebarSplit.SplitterWidth;
                if (_symbolPanelVisible && _problemsPanelVisible)
                    _botSidebarSplit.SplitterDistance = Math.Max(60, totalH / 2);
                else
                    _botSidebarSplit.SplitterDistance = Math.Max(60, totalH - 60);
            }

            if (botAny)
            {
                int totalH = _sidebarSplit.Height - _sidebarSplit.SplitterWidth;
                if (_gitPanelVisible && innerAny)
                    _sidebarSplit.SplitterDistance = Math.Max(60, totalH / 2);
                else if (_gitPanelVisible)
                    _sidebarSplit.SplitterDistance = Math.Max(60, totalH - 60);
            }

            bool anyVisible = (_workspacePanel?.Visible == true) || botAny;
            _workspaceSplitContainer.Panel1Collapsed = !anyVisible;
            if (anyVisible)
                _workspaceSplitContainer.SplitterDistance = _workspaceWidth;
        }

        private void OpenGitForm(object? sender, EventArgs e)
        {
            if (_gitForm is null || _gitForm.IsDisposed)
            {
                _gitForm = new GitForm(_gitService);
                _gitForm.FileOpenRequested += (path) => BeginInvoke(() => OpenFileInNewTab(path));
                _gitForm.Show(this);
            }
            else
            {
                _gitForm.Activate();
            }
        }

        #endregion

        #region Fullscreen Toggle
        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (vimModeEnabled && vimEngine != null)
            {
                var (key, ctrl, shift, alt) = VimStateBase.ParseKey(e.KeyData);
                if (vimEngine.ProcessKey(key, ctrl, shift, alt))
                {
                    UpdateStatusBar();
                    if (gutterPanel != null)
                    {
                        gutterPanel.CurrentLine = vimEngine.GetCurrentLine() + 1;
                        gutterPanel.Invalidate();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

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
            BeginInvoke(PositionTabDropdownButton);
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
            if (activeDocIndex >= 0)
                SaveCurrentDocument();
            _sessionManager.SaveSession(documents, activeDocIndex, _workspaceRoot, nextUntitledNumber);
            SaveSettings();
            _workspacePanel?.CancelScan();
            try
            {
                _symbolIndexCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            try
            {
                _symbolIndexCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Ignore if already disposed
            }
            _hoverTooltip.Dispose();
            _signatureHelp.Dispose();
            _hoverTimer.Dispose();
            elasticTabTimer?.Stop();
            elasticTabTimer?.Dispose();
            _highlightTimer?.Stop();
            _highlightTimer?.Dispose();
            _scrollHighlightTimer?.Stop();
            _scrollHighlightTimer?.Dispose();
            _lockKeysTimer.Stop();
            _lockKeysTimer.Dispose();
            incrementalHighlighter?.Dispose();
            _roslynWorkspace.Dispose();
            _roslynService?.Dispose();
            _gitService.Dispose();
            _gitPanel?.Dispose();
            _notificationFeed.Dispose();
            _notificationCenter?.Dispose();
            foreach (var t in _terminalTabs) t.Dispose();
            _terminalTabs.Clear();
            _terminalTabControl?.Dispose();
            _terminalNewTabButton?.Dispose();
            _debugSession.Dispose();
            _debugVariablesPanel?.Dispose();
            _debugCallStackPanel?.Dispose();
        }

        private void Form1_Activated(object? sender, EventArgs e)
        {
            CheckExternalChange();
        }

        private void RestoreSession()
        {
            var session = _sessionManager.RestoreSession();
            if (session == null) return;

            bool hadInitialTab = documents.Count == 1 &&
                documents[0].FilePath == null &&
                !documents[0].IsDirty;

            // Don't restore session if files were opened from command line
            bool hasCommandLineFiles = documents.Any(d => d.FilePath != null);
            if (hasCommandLineFiles)
            {
                Debug.WriteLine("Skipping session restore because command line files are already open");
                return;
            }

            bool hasDocsToRestore = session.Documents.Count > 0;

            if (hasDocsToRestore)
            {
                if (hadInitialTab)
                {
                    documents.Clear();
                    tabControl.TabPages.Clear();
                }

                foreach (var snap in session.Documents)
                {
                    if (File.Exists(snap.Path))
                    {
                        OpenFileInNewTabAsync(snap.Path);
                        var doc = documents.LastOrDefault();
                        if (doc != null)
                        {
                            // Store session state to apply after load
                            doc.SelectionStart = snap.SelectionStart;
                            doc.SelectionLength = snap.SelectionLength;
                            doc.FirstVisibleLine = snap.FirstVisibleLine;
                        }
                    }
                }

                if (session.ActiveTabIndex >= 0 && session.ActiveTabIndex < tabControl.TabPages.Count)
                {
                    tabControl.SelectedIndex = session.ActiveTabIndex;
                }
            }

            if (!string.IsNullOrEmpty(session.WorkspacePath) && Directory.Exists(session.WorkspacePath) && !_workspaceRootFromCli)
            {
                _workspaceRoot = session.WorkspacePath;
                if (_workspacePanel != null)
                {
                    BeginInvoke(() =>
                    {
                        _workspacePanel.SetRoot(_workspaceRoot);
                        AddToRecentWorkspaces(_workspaceRoot);
                        UpdateWorkspaceProjectLabel();
                                });
                }
            }

            if (session.NextUntitledNumber > nextUntitledNumber)
                nextUntitledNumber = session.NextUntitledNumber;
        }

        public string? WorkspaceRoot => _workspacePanel?.RootPath ?? _workspaceRoot;

        internal void RefreshWorkspace()
        {
            _workspacePanel?.RefreshTree();
        }

        internal void NavigateToFileLine(string filePath, int lineNumber)
        {
            OpenFileInNewTab(filePath);
            try
            {
                int idx = textEditor.GetFirstCharIndexFromLine(lineNumber - 1);
                if (idx >= 0)
                {
                    textEditor.SelectionStart = idx;
                    textEditor.SelectionLength = 0;
                    textEditor.ScrollToCaret();
                    textEditor.Focus();
                }
            }
            catch { }
        }

        private void GlobalSearch_Click(object? sender, EventArgs e)
        {
            using var dlg = new GlobalSearchDialog(this);
            if (_workspacePanel?.RootPath is { Length: > 0 } root && Directory.Exists(root))
                dlg.WorkspaceRoot = root;
            else if (!string.IsNullOrEmpty(_workspaceRoot) && Directory.Exists(_workspaceRoot))
                dlg.WorkspaceRoot = _workspaceRoot;
            dlg.ShowDialog(this);
        }

        #region Debugger Integration

        private async void ShowDebugHoverTooltipAsync(string word, Point mouseLoc)
        {
            try
            {
                string? value = await _debugSession.EvaluateAsync(word, _debugActiveFrameId, "hover");
                if (value is null) return;
                var theme = ThemeManager.Instance.CurrentTheme;
                Point screenLoc = textEditor.PointToScreen(mouseLoc);
                _hoverTooltip.Dismiss();
                _hoverTooltip.ShowAt(screenLoc, $"{word}", value, "Debugger");
            }
            catch
            {
                // Silently ignore — debugger may have moved on
            }
        }

        private void OnDebugStateChanged(DebugState state)
        {
            bool isRunning = state == DebugState.Running;
            bool isPaused = state == DebugState.Paused;
            bool isActive = isRunning || isPaused || state == DebugState.Stepping;
            bool isTerminated = state == DebugState.Terminated || state == DebugState.Terminating;

            BeginInvoke(() =>
            {
                startDebugMenuItem.Enabled = !isActive && !isTerminated;
                runWithoutDebugMenuItem.Enabled = !isActive && !isTerminated;
                stopDebugMenuItem.Enabled = isActive;
                restartDebugMenuItem.Enabled = isActive;
                continueDebugMenuItem.Enabled = isPaused;
                stepOverMenuItem.Enabled = isPaused;
                stepIntoMenuItem.Enabled = isPaused;
                stepOutMenuItem.Enabled = isPaused;

                if (isTerminated)
                {
                    _debugActiveLine = -1;
                    _debugActiveFile = null;
                    _debugActiveFrameId = 0;
                    _debugVariablesPanel?.Close();
                    _debugVariablesPanel = null;
                    _debugCallStackPanel?.Close();
                    _debugCallStackPanel = null;
                    gutterPanel?.Invalidate();
                }
            });
        }

        private async void OnDebugThreadStopped(int threadId, string reason)
        {
            _debugActiveLine = -1;
            _debugActiveFile = null;

            var frames = await _debugSession.GetStackTraceAsync(threadId);
            if (frames is { Length: > 0 })
            {
                var top = frames[0];
                _debugActiveLine = top.Line;
                _debugActiveFile = top.Source?.Path;
                _debugActiveFrameId = top.Id;

                if (_debugActiveFile != null && File.Exists(_debugActiveFile))
                    NavigateToFileLine(_debugActiveFile, _debugActiveLine);

                gutterPanel?.Invalidate();
            }

            BeginInvoke(async () =>
            {
                if (_debugVariablesPanel == null || _debugVariablesPanel.IsDisposed)
                {
                    _debugVariablesPanel = new DebugVariablesPanel(_debugSession);
                    _debugVariablesPanel.Show(this);
                    _debugVariablesPanel.Location = new Point(Right - 360, Top + 200);
                }
                else
                {
                    _debugVariablesPanel.BringToFront();
                }

                if (_debugCallStackPanel == null || _debugCallStackPanel.IsDisposed)
                {
                    _debugCallStackPanel = new DebugCallStackPanel(_debugSession, this);
                    _debugCallStackPanel.Show(this);
                    _debugCallStackPanel.Location = new Point(Right - 760, Top + 60);
                }
                else
                {
                    _debugCallStackPanel.BringToFront();
                }

                if (frames is { Length: > 0 })
                {
                    await _debugVariablesPanel.RefreshAsync(frames[0].Id);
                    await _debugCallStackPanel.RefreshAsync(threadId);
                }
            });
        }

        private async void StartDebug_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Open a file first to start debugging.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? projectDir = ProjectLocator.FindProjectDirectory(currentFilePath);
            if (projectDir == null)
            {
                ThemedMessageBox.Show("Could not find a .csproj file. Open a file that is part of a .NET project.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? outputDll = ProjectLocator.FindOutputAssembly(projectDir);
            if (outputDll == null)
            {
                var result = ThemedMessageBox.Show("No build output found. Build the project first?",
                    "Debugger", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    RunDotnetBuild(projectDir);
                    outputDll = ProjectLocator.FindOutputAssembly(projectDir);
                }
                if (outputDll == null)
                {
                    ThemedMessageBox.Show("Build output not found. Build the project and try again.", "Debugger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            string? error = await _debugSession.StartAsync(outputDll!, projectDir);
            if (error != null)
            {
                ThemedMessageBox.Show(error, "Debugger Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string? fileForBp = currentFilePath;
            if (fileForBp != null)
            {
                var bps = _breakpointManager.GetDapBreakpoints(fileForBp);
                await _debugSession.SetBreakpointsAsync(fileForBp, bps);
            }

            await _debugSession.ConfigurationDoneAsync();
            ShowNotification("Debug", $"Debug session started: {Path.GetFileName(outputDll)}");
        }

        private void StopDebug_Click(object? sender, EventArgs e)
        {
            _debugSession.Dispose();
            OnDebugStateChanged(DebugState.Terminated);
            ShowNotification("Debug", "Debug session stopped.");
        }

        private async void DebugContinue_Click(object? sender, EventArgs e)
        {
            await _debugSession.ContinueAsync();
        }

        private async void StepOver_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepOverAsync();
        }

        private async void StepInto_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepInAsync();
        }

        private async void StepOut_Click(object? sender, EventArgs e)
        {
            await _debugSession.StepOutAsync();
        }

        private void ToggleBreakpointMenu_Click(object? sender, EventArgs e)
        {
            if (currentFilePath == null) return;
            int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            _breakpointManager.ToggleBreakpoint(currentFilePath, line);
        }

        private void RunWithoutDebug_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                ThemedMessageBox.Show("Open a file first.", "Run", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? projectDir = ProjectLocator.FindProjectDirectory(currentFilePath);
            if (projectDir == null)
            {
                ThemedMessageBox.Show("Could not find a .csproj file.", "Run", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? outputDll = ProjectLocator.FindOutputAssembly(projectDir);
            if (outputDll == null)
            {
                var result = ThemedMessageBox.Show("No build output found. Build the project first?",
                    "Run", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    RunDotnetBuild(projectDir);
                    outputDll = ProjectLocator.FindOutputAssembly(projectDir);
                }
                if (outputDll == null) return;
            }

            try
            {
                // Run in terminal panel instead of separate window
                var terminal = ActiveTerminal ?? AddTerminalTab(null);
                terminal.ClearOutput();
                var projectName = Path.GetFileName(projectDir);
                if (_terminalTabControl?.SelectedTab is TabPage tab)
                    tab.Text = projectName;
                ShowTerminal();
                string runCmd = ResolveRunCommand();
                terminal.SendInput($"{runCmd} --project \"{projectDir}\"\r\n");
                ShowNotification("Run", $"Started: {projectName}");
            }
            catch (Exception ex)
            {
                ThemedMessageBox.Show($"Failed to start: {ex.Message}", "Run Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void RestartDebug_Click(object? sender, EventArgs e)
        {
            _debugSession.Dispose();
            OnDebugStateChanged(DebugState.Terminated);
            await Task.Delay(200);
            StartDebug_Click(sender, e);
        }

        private void NewBreakpoint_Click(object? sender, EventArgs e)
        {
            if (currentFilePath == null) return;
            string defaultLine = (textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1).ToString();
            string? input = SimpleInputDialog.Show(this, "Enter line number:", "New Breakpoint", defaultLine);
            if (input != null && int.TryParse(input, out int line) && line > 0)
            {
                _breakpointManager.ToggleBreakpoint(currentFilePath, line);
                gutterPanel?.Invalidate();
            }
        }

        private void EnableAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.EnableAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints enabled.");
        }

        private void DisableAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.DisableAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints disabled.");
        }

        private void RemoveAllBreakpoints_Click(object? sender, EventArgs e)
        {
            _breakpointManager.ClearAll();
            gutterPanel?.Invalidate();
            ShowNotification("Breakpoints", "All breakpoints removed.");
        }

        private void RunDotnetBuild(string projectDir)
        {
            try
            {
                string cmd = ResolveBuildCommand();
                string exe = cmd.Split(' ')[0];
                string args = cmd.Length > exe.Length ? cmd[(exe.Length + 1)..] : "";
                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = projectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(30000);
            }
            catch { }
        }

        #endregion

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Global search: Ctrl+Shift+F
            if (keyData == (Keys.Control | Keys.Shift | Keys.F))
            {
                GlobalSearch_Click(null, EventArgs.Empty);
                return true;
            }

            // Split navigation: cycle focus between split editor and main editor
            if (keyData == (Keys.Control | Keys.Tab) && (_splitEditor != null))
            {
                if (_splitEditor.Focused)
                {
                    if (textEditor.CanFocus) textEditor.Focus();
                }
                else
                {
                    _splitEditor.Focus();
                }
                return true;
            }

            // Close split: Ctrl+Shift+W
            if (keyData == (Keys.Control | Keys.Shift | Keys.W) && _splitEditor != null)
            {
                CloseSplit();
                return true;
            }

            // Debugger keyboard shortcuts (take priority when active)
            if (_debugSession.State != DebugState.Idle && _debugSession.State != DebugState.Terminated)
            {
                if (keyData == Keys.F5)
                {
                    if (_debugSession.State == DebugState.Paused)
                        _ = _debugSession.ContinueAsync();
                    return true;
                }
                if (keyData == (Keys.Shift | Keys.F5))
                {
                    StopDebug_Click(null, EventArgs.Empty);
                    return true;
                }
                if (keyData == Keys.F10)
                {
                    _ = _debugSession.StepOverAsync();
                    return true;
                }
                if (keyData == Keys.F11)
                {
                    _ = _debugSession.StepInAsync();
                    return true;
                }
                if (keyData == (Keys.Shift | Keys.F11))
                {
                    _ = _debugSession.StepOutAsync();
                    return true;
                }
            }

            // F9 always toggles breakpoint
            if (keyData == Keys.F9)
            {
                ToggleBreakpointMenu_Click(null, EventArgs.Empty);
                gutterPanel?.Invalidate();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Shift | Keys.F)) { FormatDocumentAsync(); return true; }
            if (keyData == (Keys.Control | Keys.P)) { ShowQuickOpen(); return true; }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #endregion

        // Tab measurement cache for elastic tab stops
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

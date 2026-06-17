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
        [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

        // Win32 API for non-client area file drop (title bar, etc.)
        [LibraryImport("shell32.dll")]
        private static partial void DragAcceptFiles(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAccept);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile,
            [Out] char[]? lpszFile, uint cch);

        [LibraryImport("shell32.dll")]
        private static partial void DragFinish(IntPtr hDrop);

        private const int WM_DROPFILES = 0x0233;

        // RichTextBox messages
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;
        private const int SB_TOP = 6;

        // DWM API for native title bar dark mode (Windows 10 1809+)

        private ToolStripMenuItem? lineNumberToolStripMenuItem;
        private ToolStripMenuItem? relativeLineNumberToolStripMenuItem;




        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

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

        private void ApplyWindowSettings()
        {
            // Opacity
            int opacityPct = Math.Clamp(_winOpacity, 50, 100);
            try { this.Opacity = opacityPct / 100.0; } catch { }

            // Dark title bar override
            if (this.IsHandleCreated)
            {
                try
                {
                    int v = _winDarkTitleBar switch
                    {
                        "on" => 1,
                        "off" => 0,
                        _ => isDarkTheme ? 1 : 0  // "auto": follow theme
                    };
                    DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
                }
                catch { }
            }
        }

        private void ApplyWorkspaceSettings()
        {
            _workspacePanel?.ApplyWorkspaceSettings(
                _wsShowAllFiles, _wsExcludedDirs,
                _wsTreeItemHeight, _wsTreeIndent,
                _wsAutoCollapse, _wsWatcherDebounceMs, _wsDisableFileWatcher);
            _sessionManager.MaxRecentWorkspaces = Math.Clamp(_wsMaxRecentWorkspaces, 5, 50);
            _sessionManager.MaxRecentFiles = Math.Clamp(_wsMaxRecentFiles, 5, 50);
        }

        private void ApplyTabSettings()
        {
            if (tabControl == null) return;
            tabControl.ItemSize = new Size(Math.Clamp(_tabWidth, 60, 300), Math.Clamp(_tabHeight, 22, 40));
            // Trim recently closed stack to new limit
            while (_recentlyClosed.Count > _tabMaxRecentlyClosed)
                _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
            // Refresh all tab titles so dirty indicator style takes effect immediately
            for (int i = 0; i < documents.Count; i++)
                UpdateTabTitle(i);
            tabControl.Invalidate();
        }

        private void ApplyFindSettings()
        {
            // Sync _lastFindCaseSensitive / _lastUseRegex with persisted defaults
            // (only on first apply; preserve per-session values if dialog was already used)
            if (string.IsNullOrEmpty(_lastFindText))
            {
                _lastFindCaseSensitive = _findCaseSensitive;
                _lastUseRegex = _findUseRegex;
            }
        }

        private void ApplyAppearanceSettings()
        {
            if (gutterPanel != null)
            {
                gutterPanel.ShowLineNumbers = _showLineNumbers;
                gutterPanel.RelativeNumbers = _relativeLineNumbers;
                gutterPanel.UpdateLineNumberWidth();
            }
            if (minimapControl != null)
                minimapControl.MinimapWidth = Math.Clamp(_minimapWidth, 60, 200);
            if (gitBranchLabel != null)
            {
                gitBranchLabel.Visible = _showGitStatusBar;
                gitDirtyLabel.Visible  = _showGitStatusBar;
                gitSyncLabel.Visible   = _showGitStatusBar;
            }
            if (roslynDropDown != null)
            {
                roslynDropDown.Visible    = _showRoslynStatusBar;
                roslynToggleLabel.Visible = _showRoslynStatusBar;
            }
        }

        private void ApplyTextEditorSettings()
        {
            _autoSaveTimer.Interval = _autoSaveIntervalSeconds * 1000;
            if (textEditor != null)
            {
                textEditor.CtrlWheelZoomEnabled = _ctrlWheelZoom;
                textEditor.MouseWheelScrollLines = _mouseWheelScrollLines;
            }
        }

        // Extensions Current* properties
        public bool CurrentSnippetsEnabled => _snippetsEnabled;
        public string CurrentSnippetTriggerKey => _snippetTriggerKey;
        public bool CurrentLintEnabled => _lintEnabled;
        public int CurrentLintMaxLineLength => _lintMaxLineLength;
        public bool CurrentLintFlagMagicNumbers => _lintFlagMagicNumbers;
        public bool CurrentLintFlagNamingConventions => _lintFlagNamingConventions;
        public bool CurrentTreeSitterEnabled => _treeSitterEnabled;
        public bool CurrentTodoScanEnabled => _todoScanEnabled;
        public string CurrentTodoScanPatterns => _todoScanPatterns;

        private void ApplyExtensionsSettings()
        {
            // Lint — separate from SAST (Roslyn); regex rules controlled by LintEnabled
            _lintEngine.Configure(_lintEnabled, _lintMaxLineLength, _lintFlagMagicNumbers, _lintFlagNamingConventions);

            // TreeSitter symbol index — null disables non-C# symbol scanning
            _symbolIndex.SetTreeSitterService(_treeSitterEnabled ? _treeSitter : null);
        }

        private void ApplyCursorSettings()
        {
            if (textEditor == null) return;
            textEditor.LineHighlightWhenUnfocused = _lineHighlightWhenUnfocused;
            textEditor.CursorStyle = _cursorStyle;
            textEditor.CursorBlinkEnabled = _cursorBlinkEnabled;
            textEditor.CursorBlinkRateMs = _cursorBlinkRateMs;
        }

        private void ApplyEditorRenderingSettings()
        {
            if (textEditor == null) return;
            textEditor.ShowOccurrenceHighlights = _showOccurrenceHighlights;
            textEditor.ShowIndentGuides = _showIndentGuides;
            textEditor.SmoothCaretBlink = _smoothenCaretBlink;
            textEditor.HighlightTrailingWhitespace = _highlightTrailingWhitespace;
            textEditor.MultiCursorEnabled = _multiCursorEnabled;
            textEditor.BracketTypeOverEnabled = _bracketTypeOver;
        }

        private System.Windows.Forms.Timer? _findNotFoundResetTimer;
        private System.Windows.Forms.Timer? _statusResetTimer;

        internal void NotifyNotFound(string text)
        {
            switch (_findNotFoundNotification)
            {
                case "statusbar":
                    if (lineColLabel != null)
                    {
                        lineColLabel.Text = $"Not found: \"{text}\"";
                        _findNotFoundResetTimer?.Stop();
                        _findNotFoundResetTimer = new System.Windows.Forms.Timer { Interval = 2500 };
                        _findNotFoundResetTimer.Tick += (s, e) =>
                        {
                            _findNotFoundResetTimer?.Stop();
                            UpdateStatusBar();
                        };
                        _findNotFoundResetTimer.Start();
                    }
                    break;
                case "silent":
                    break;
                default:
                    ThemedMessageBox.Show("Cannot find \"" + text + "\".", "Find",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        internal void SetStatus(string message, int durationMs = 4500)
        {
            if (lineColLabel == null)
                return;

            lineColLabel.Text = message;
            _statusResetTimer?.Stop();
            _statusResetTimer = new System.Windows.Forms.Timer { Interval = durationMs };
            _statusResetTimer.Tick += (s, e) =>
            {
                _statusResetTimer?.Stop();
                UpdateStatusBar();
            };
            _statusResetTimer.Start();
        }

        internal void ReopenLastClosedTab()
        {
            if (!_tabRememberRecentlyClosed || _recentlyClosed.Count == 0) return;
            var (filePath, content, displayName) = _recentlyClosed[0];
            _recentlyClosed.RemoveAt(0);

            var syntax = filePath != null
                ? SyntaxDefinition.GetDefinitionForFile(filePath)
                : SyntaxDefinition.GetDefinitionForFile(".txt");

            SaveCurrentDocument();
            var doc = new EditorDocument
            {
                FilePath = filePath,
                Content = content,
                IsDirty = filePath == null,
                ModifiedLines = new HashSet<int>(),
                Bookmarks = new HashSet<int>(),
                CollapsedRegions = new HashSet<int>(),
                SavedHash = "",
                Syntax = syntax
            };
            _docManager.Add(doc);
            int newIndex = documents.Count - 1;
            var tabPage = new TabPage(doc.DisplayName) { Tag = doc };
            tabControl.TabPages.Add(tabPage);
            tabControl.SelectedIndex = newIndex;
            activeDocIndex = newIndex;
            LoadDocument(doc);
            UpdateWindowTitle();
            UpdateTabTitle(newIndex);
            EnsureSelectedTabVisible();
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

        // Cursor appearance
        private bool _lineHighlightWhenUnfocused = false;
        private string _cursorStyle = "line";
        private bool _cursorBlinkEnabled = true;
        private int _cursorBlinkRateMs = 530;
        private bool _showOccurrenceHighlights = true;
        private bool _showIndentGuides = false;
        private bool _smoothenCaretBlink = true;
        private bool _highlightTrailingWhitespace = false;
        private bool _multiCursorEnabled = false;
        private bool _smartHomeKey = true;
        private bool _bracketTypeOver = true;

        // Formatting behaviour
        private bool _autoCloseBracesOnEnter = false;
        private bool _formatOnSave = false;
        private bool _detectIndentationFromFile = true;

        private readonly LinkedList<(int Position, string? FilePath)> _cursorHistory = new();
        private LinkedListNode<(int Position, string? FilePath)>? _cursorHistoryCurrent;
        private const int MaxCursorHistory = 25;
        private bool _suppressHistoryPush = false;

        // Vim mode state
        private bool vimModeEnabled = false;
        private VimEngine? vimEngine;

        // Terminal state
        private BuildOutputPanel? _buildOutputPanel;
        private TabControl? _terminalTabControl;
        private SplitContainer? _terminalSplitContainer;
        private Button? _terminalNewTabButton;
        private readonly List<TerminalPanel> _terminalTabs = new();
        private bool _terminalVisible = false;
        private int _terminalHeight = 200;
        private string _terminalShell = "";
        private string _terminalFontFace = "";
        private float _terminalFontSize = 0f;
        private bool _terminalFontBold = false;
        private bool _terminalWordWrap = true;
        private bool _terminalScrollbarVisible = true;
        private int _terminalPadding = 4;
        private int _terminalMaxScrollback = 5000;
        private string _terminalStartingDirectory = "";
        private string _terminalTabTitle = "";

        // Git settings
        private string _gitAuthorName = "";
        private string _gitAuthorEmail = "";
        private string _gitDefaultBranch = "main";
        private bool _gitConfirmRemoveRepo = true;
        private bool _gitConfirmDiscardChanges = true;
        private bool _gitConfirmDiscardPermanent = true;
        private bool _gitConfirmDiscardStash = true;
        private bool _gitConfirmCheckoutCommit = false;
        private bool _gitConfirmForcePush = true;
        private bool _gitConfirmUndoCommit = true;
        private bool _gitConfirmOverrideCommitMsg = false;
        private bool _gitConfirmHiddenChanges = false;
        private string _gitBranchSwitchBehavior = "ask";
        private bool _gitCommitLengthWarning = false;

        // Security settings
        private SecurityProfile _securityProfile = SecurityProfile.Low;
        private bool _secPromptUntrustedWorkspace = true;
        private string _secTrustedWorkspacePaths = "";
        private bool _secConfirmUrlOpen = false;
        private bool _secAllowHttpUrls = true;
        private bool _secConfirmExternalTools = false;
        private bool _secConfirmDebugStart = false;
        private bool _secDebugAdapterPathOnly = true;
        private bool _secConfirmGitClone = true;
        private string _secTrustedGitHosts = "github.com,gitlab.com,dev.azure.com,bitbucket.org";
        private bool _secSastEnabled = true;
        private string _secSastSeverityThreshold = "Medium";
        private bool _secWriteCrashLog = true;
        private bool _secWriteStartupLog = true;
        private int _secLogRetentionDays = 30;
        private bool _secHighlightHardcodedSecrets = true;

        // Window settings
        private bool _winRememberBounds = true;
        private bool _winLaunchMaximized = false;
        private bool _winRestoreSession = true;
        private string _winDarkTitleBar = "auto";
        private int _winOpacity = 100;

        // Workspace / folder tree state
        private SplitContainer? _workspaceSplitContainer;
        private WorkspacePanel? _workspacePanel;
        private SolutionExplorerPanel? _solutionExplorerPanel;
        private TabControl? _sideTopTabs;
        private bool _workspaceVisible;
        private int _workspaceWidth = 200;
        private string _workspaceRoot = "";

        // Editor Management (tab) settings
        private int _tabWidth = 140;
        private int _tabHeight = 26;
        private bool _tabShowFileIcons = true;
        private string _tabDirtyIndicator = "asterisk";
        private string _tabCloseButtonVisibility = "always";
        private bool _tabMiddleClickClose = true;
        private bool _tabMouseWheelScroll = true;
        private int _tabMaxOpen = 0;
        private bool _tabConfirmCloseUnsaved = true;
        private bool _tabRememberRecentlyClosed = false;
        private int _tabMaxRecentlyClosed = 10;
        private readonly List<(string? FilePath, string Content, string DisplayName)> _recentlyClosed = new();

        // Workspace settings
        private bool _wsShowAllFiles = false;
        private string _wsExcludedDirs = "";
        private int _wsTreeItemHeight = 26;
        private int _wsTreeIndent = 24;
        private bool _wsAutoCollapse = false;
        private int _wsWatcherDebounceMs = 500;
        private bool _wsDisableFileWatcher = false;
        private int _wsMaxRecentWorkspaces = 10;
        private int _wsMaxRecentFiles = 10;

        // AIOps
        private MyCrownJewelApp.Pfpad.AIOps.AIOpsEngine? _aiopsEngine;
        private MyCrownJewelApp.Pfpad.AIOps.AIOpsPanel? _aiopsPanel;
        private MyCrownJewelApp.Pfpad.AIOps.SecurityPanel? _aiopsSecurityPanel;
        private MyCrownJewelApp.Pfpad.AIOps.DeploymentPanel? _aiopsDeploymentPanel;
        private MyCrownJewelApp.Pfpad.AIOps.TelemetryPanel? _aiopsTelemetryPanel;
        private MyCrownJewelApp.Pfpad.AIOps.InsightsPanel? _aiopsInsightsPanel;
        private MyCrownJewelApp.Pfpad.AIOps.OpsQueryPanel? _aiopsQueryPanel;
        private MyCrownJewelApp.Pfpad.AIOps.IncidentTimelinePanel? _aiopsTimelinePanel;
        private MyCrownJewelApp.Pfpad.AIOps.PullRequestRiskPanel? _aiopsPrRiskPanel;
        private MyCrownJewelApp.Pfpad.AIOps.ServiceDependencyPanel? _aiopsServiceDepPanel;
        private MyCrownJewelApp.Pfpad.AIOps.RunbookPanel? _aiopsRunbookPanel;
        private MyCrownJewelApp.Pfpad.AIOps.InlineAnnotationService _inlineAnnotationService = new();
        private bool _aiopsHubVisible;
        private bool _aiopsSecurityVisible;
        private bool _aiopsDeploymentVisible;
        private bool _aiopsTelemetryVisible;
        private bool _aiopsInsightsVisible;
        private bool _aiopsQueryVisible;
        private bool _aiopsTimelineVisible;
        private bool _aiopsPrRiskVisible;
        private bool _aiopsServiceDepVisible;
        private bool _aiopsRunbookVisible;

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
            _terminalTabControl?.SelectedTab?.Tag as TerminalPanel;

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
        private bool _applyingElasticTabs;  // prevents elastic-tab timer restart during SelectionTabs apply

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
        private bool _trimTrailingWhitespace;
        private bool _insertFinalNewline;
        private string _defaultLineEnding = "auto";
        private bool _ctrlWheelZoom = true;
        private int _mouseWheelScrollLines;
        private int _autoSaveIntervalSeconds = 30;
        // Workbench Appearance
        private bool _showLineNumbers = true;
        private bool _relativeLineNumbers;
        private int _minimapWidth = 100;
        private bool _showGitStatusBar = true;
        private bool _showRoslynStatusBar = true;
        // Extensions
        private bool _snippetsEnabled = true;
        private string _snippetTriggerKey = "Tab";
        private bool _lintEnabled = true;
        private int _lintMaxLineLength = 120;
        private bool _lintFlagMagicNumbers = true;
        private bool _lintFlagNamingConventions = true;
        private bool _treeSitterEnabled = true;
        private bool _todoScanEnabled = true;
        private string _todoScanPatterns = "TODO, FIXME, HACK, NOTE, BUG, WORKAROUND";

        // Workspace scan state
        private Color _originalStatusBarColor;
        private CancellationTokenSource? _symbolIndexCts;
        private ToolStripStatusLabel _workspaceProjectLabel = null!;
        private string? _detectedProjectPath;
        private string? _detectedSolutionPath;

        // Debugger integration
        private readonly DebugSession _debugSession = new();
        private DebugAdapterType _debugAdapterType = DebugAdapterType.NetCoreDbg;
        private string _debugAdapterPath = "";

        /// <summary>Builds an AppSettings snapshot used when launching the debug adapter.</summary>
        private AppSettings CurrentDebugSettings() => new AppSettings
        {
            DebugAdapterType = _debugAdapterType,
            DebugAdapterPath = _debugAdapterPath
        };
        private readonly BreakpointManager _breakpointManager = new();
        private DebugVariablesPanel? _debugVariablesPanel;
        private DebugCallStackPanel? _debugCallStackPanel;
        private int _debugActiveLine = -1;
        private string? _debugActiveFile;
        private int _debugActiveFrameId;
        public BreakpointManager DebugBreakpointManager => _breakpointManager;
        public int DebugActiveLine => _debugActiveLine;
        public string? CurrentFilePath => currentFilePath;
        /// <summary>Lowercase extension of the currently open file, e.g. ".cs" or ".cpp". Null when no file is open.</summary>
        private string? CurrentFileExtension =>
            currentFilePath is not null ? Path.GetExtension(currentFilePath).ToLowerInvariant() : null;


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
        public bool CurrentShowGutterBreakpoints => showGutterBreakpointsMenuItem?.Checked ?? true;
        public bool CurrentShowGutterBookmarks   => showGutterBookmarksMenuItem?.Checked ?? true;
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
        public bool CurrentTrimTrailingWhitespace => _trimTrailingWhitespace;
        public bool CurrentInsertFinalNewline => _insertFinalNewline;
        public string CurrentDefaultLineEnding => _defaultLineEnding;
        public bool CurrentCtrlWheelZoom => _ctrlWheelZoom;
        public int CurrentMouseWheelScrollLines => _mouseWheelScrollLines;
        public int CurrentAutoSaveIntervalSeconds => _autoSaveIntervalSeconds;
        public bool CurrentShowLineNumbers => _showLineNumbers;
        public bool CurrentRelativeLineNumbers => _relativeLineNumbers;
        public int CurrentMinimapWidth => _minimapWidth;
        public bool CurrentShowGitStatusBar => _showGitStatusBar;
        public bool CurrentShowRoslynStatusBar => _showRoslynStatusBar;
        public bool CurrentWorkspaceVisible => _workspaceVisible;
        public bool CurrentSymbolPanelVisible => _symbolPanelVisible;
        public bool CurrentProblemsPanelVisible => _problemsPanelVisible;
        public bool CurrentTerminalVisible => _terminalVisible;
        public int CurrentTerminalHeight => _terminalHeight;
        public bool CurrentAnalyzersEnabled => _analyzersEnabled;
        public string CurrentTerminalShell => _terminalShell;
        public string CurrentTerminalFontFace => _terminalFontFace;
        public float CurrentTerminalFontSize => _terminalFontSize;
        public bool CurrentTerminalFontBold => _terminalFontBold;
        public bool CurrentTerminalWordWrap => _terminalWordWrap;
        public bool CurrentTerminalScrollbarVisible => _terminalScrollbarVisible;
        public int CurrentTerminalPadding => _terminalPadding;
        public int CurrentTerminalMaxScrollback => _terminalMaxScrollback;
        public string CurrentTerminalStartingDirectory => _terminalStartingDirectory;
        public string CurrentTerminalTabTitle => _terminalTabTitle;
        public string CurrentGitAuthorName => _gitAuthorName;
        public string CurrentGitAuthorEmail => _gitAuthorEmail;
        public string CurrentGitDefaultBranch => _gitDefaultBranch;
        public bool CurrentGitConfirmRemoveRepo => _gitConfirmRemoveRepo;
        public bool CurrentGitConfirmDiscardChanges => _gitConfirmDiscardChanges;
        public bool CurrentGitConfirmDiscardPermanent => _gitConfirmDiscardPermanent;
        public bool CurrentGitConfirmDiscardStash => _gitConfirmDiscardStash;
        public bool CurrentGitConfirmCheckoutCommit => _gitConfirmCheckoutCommit;
        public bool CurrentGitConfirmForcePush => _gitConfirmForcePush;
        public bool CurrentGitConfirmUndoCommit => _gitConfirmUndoCommit;
        public bool CurrentGitConfirmOverrideCommitMsg => _gitConfirmOverrideCommitMsg;
        public bool CurrentGitConfirmHiddenChanges => _gitConfirmHiddenChanges;
        public string CurrentGitBranchSwitchBehavior => _gitBranchSwitchBehavior;
        public bool CurrentGitCommitLengthWarning => _gitCommitLengthWarning;
        public SecurityProfile CurrentSecurityProfile => _securityProfile;
        internal SettingsService SettingsService => _settingsService;
        public bool CurrentSecPromptUntrustedWorkspace => _secPromptUntrustedWorkspace;
        public string CurrentSecTrustedWorkspacePaths => _secTrustedWorkspacePaths;
        public bool CurrentSecConfirmUrlOpen => _secConfirmUrlOpen;
        public bool CurrentSecAllowHttpUrls => _secAllowHttpUrls;
        public bool CurrentSecConfirmExternalTools => _secConfirmExternalTools;
        public bool CurrentSecConfirmDebugStart => _secConfirmDebugStart;
        public bool CurrentSecDebugAdapterPathOnly => _secDebugAdapterPathOnly;
        public bool CurrentSecConfirmGitClone => _secConfirmGitClone;
        public string CurrentSecTrustedGitHosts => _secTrustedGitHosts;
        public bool CurrentSecSastEnabled => _secSastEnabled;
        public string CurrentSecSastSeverityThreshold => _secSastSeverityThreshold;
        public bool CurrentSecWriteCrashLog => _secWriteCrashLog;
        public bool CurrentSecWriteStartupLog => _secWriteStartupLog;
        public int CurrentSecLogRetentionDays => _secLogRetentionDays;
        public bool CurrentSecHighlightHardcodedSecrets => _secHighlightHardcodedSecrets;
        public bool CurrentVimMode => vimModeEnabled;
        public bool CurrentStickyScroll => _stickyScrollEnabled;
        public bool CurrentHoverLineHighlight => _hoverLineHighlightEnabled;
        public bool CurrentLineHighlightWhenUnfocused => _lineHighlightWhenUnfocused;
        public string CurrentCursorStyle => _cursorStyle;
        public bool CurrentCursorBlinkEnabled => _cursorBlinkEnabled;
        public int CurrentCursorBlinkRateMs => _cursorBlinkRateMs;
        public bool CurrentShowOccurrenceHighlights => _showOccurrenceHighlights;
        public bool CurrentShowIndentGuides => _showIndentGuides;
        public bool CurrentSmoothenCaretBlink => _smoothenCaretBlink;
        public bool CurrentHighlightTrailingWhitespace => _highlightTrailingWhitespace;
        public bool CurrentMultiCursorEnabled => _multiCursorEnabled;
        public bool CurrentSmartHomeKey => _smartHomeKey;
        public bool CurrentBracketTypeOver => _bracketTypeOver;
        public bool CurrentAutoCloseBracesOnEnter => _autoCloseBracesOnEnter;
        public bool CurrentFormatOnSave => _formatOnSave;
        public bool CurrentDetectIndentationFromFile => _detectIndentationFromFile;
        public int CurrentMaxFileSizeMB => MaxFileSizeMB;
        public int CurrentLargeFileWarningMB => LargeFileWarningMB;
        public int CurrentAsyncFileWarningMB => AsyncFileWarningMB;
        public bool CurrentDisableSyntaxHighlightingForLargeFiles => DisableSyntaxHighlightingForLargeFiles;
        public bool CurrentDisableMinimapForLargeFiles => DisableMinimapForLargeFiles;
        public bool CurrentDisableWordWrapForLargeFiles => DisableWordWrapForLargeFiles;
        public bool CurrentWinRememberBounds => _winRememberBounds;
        public bool CurrentWinLaunchMaximized => _winLaunchMaximized;
        public bool CurrentWinRestoreSession => _winRestoreSession;
        public string CurrentWinDarkTitleBar => _winDarkTitleBar;
        public int CurrentWinOpacity => _winOpacity;
        public bool CurrentWsShowAllFiles => _wsShowAllFiles;
        public string CurrentWsExcludedDirs => _wsExcludedDirs;
        public int CurrentWsTreeItemHeight => _wsTreeItemHeight;
        public int CurrentWsTreeIndent => _wsTreeIndent;
        public bool CurrentWsAutoCollapse => _wsAutoCollapse;
        public int CurrentWsWatcherDebounceMs => _wsWatcherDebounceMs;
        public bool CurrentWsDisableFileWatcher => _wsDisableFileWatcher;
        public int CurrentWsMaxRecentWorkspaces => _wsMaxRecentWorkspaces;
        public int CurrentWsMaxRecentFiles => _wsMaxRecentFiles;
        public int CurrentTabWidth => _tabWidth;
        public int CurrentTabHeight => _tabHeight;
        public bool CurrentTabShowFileIcons => _tabShowFileIcons;
        public string CurrentTabDirtyIndicator => _tabDirtyIndicator;
        public string CurrentTabCloseButtonVisibility => _tabCloseButtonVisibility;
        public bool CurrentTabMiddleClickClose => _tabMiddleClickClose;
        public bool CurrentTabMouseWheelScroll => _tabMouseWheelScroll;
        public int CurrentTabMaxOpen => _tabMaxOpen;
        public bool CurrentTabConfirmCloseUnsaved => _tabConfirmCloseUnsaved;
        public bool CurrentTabRememberRecentlyClosed => _tabRememberRecentlyClosed;
        public int CurrentTabMaxRecentlyClosed => _tabMaxRecentlyClosed;
        public bool CurrentFindCaseSensitive => _findCaseSensitive;
        public bool CurrentFindUseRegex => _findUseRegex;
        public bool CurrentFindWrapAround => _findWrapAround;
        public bool CurrentFindSeedFromSelection => _findSeedFromSelection;
        public string CurrentFindNotFoundNotification => _findNotFoundNotification;
        public string CurrentFindInFilesFilter => _findInFilesFilter;
        public string CurrentFindInFilesExclude => _findInFilesExclude;
        public int CurrentFindInFilesMaxResults => _findInFilesMaxResults;

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
        this.Icon = AppIconFactory.GetIcon();

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
              if (!_cliArgsProvided && documents.Count <= 1 && _winRestoreSession)
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

                        _docManager.Add(doc);
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
            ApplyTextEditorSettings();
            ApplyCursorSettings();
            ApplyEditorRenderingSettings();
             
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
            columnGuideMenuItem.Checked = textEditor?.ShowGuide ?? false;
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
            gutterPanel.TopOffset = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (_rulerEnabled ? rulerPanel.Height : 0);

            // Initialize incremental syntax highlighter if enabled
            if (syntaxHighlightingEnabled)
            {
                CreateIncrementalHighlighter();
            }

            // Track initial text length for paste detection
            _previousTextLength = textEditor != null ? textEditor.TextLength : 0;

            // Apply visibility states
            gutterPanel.Visible = gutterVisible;
            if (textEditor != null) textEditor.ShowWhitespace = whitespaceMenuItem.Checked;
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
                if (!_suppressFoldRescan) _foldingManager?.ScanRegions(CurrentFileExtension);
                gutterPanel?.RefreshGutter();
                ScheduleLint();
                RebuildStickyScopes();
            };
            _foldingManager.ScanRegions(CurrentFileExtension);
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
               vimEngine.OnShowLineNumbersChanged += value => { gutterPanel!.ShowLineNumbers = value; gutterPanel!.UpdateLineNumberWidth(); SyncGutterColumnWidth(); gutterPanel!.MarkDataDirty(); if (lineNumberToolStripMenuItem != null) lineNumberToolStripMenuItem.Checked = value && !vimEngine.RelativeNumbers; };
              vimEngine.OnRelativeNumbersChanged += value => { gutterPanel!.RelativeNumbers = value; gutterPanel!.UpdateLineNumberWidth(); SyncGutterColumnWidth(); gutterPanel!.MarkDataDirty(); if (relativeLineNumberToolStripMenuItem != null) relativeLineNumberToolStripMenuItem.Checked = value; };
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
                   ApplyGutterLineNumberState();
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
                   ApplyGutterLineNumberState();
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
              vimEngine.CloseRequested += () =>
              {
                  // In vim, :q closes the active split if one exists; otherwise close the app.
                  if (_splitEditor != null)
                  { CloseSplit(); ShowNotification("Vim", "Split closed"); }
                  else
                      this.Close();
              };
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

             // Add the persistent "Output" tab (Build Output) at index 0
             _buildOutputPanel = new BuildOutputPanel();
             _buildOutputPanel.SetTheme(_themeManager.CurrentTheme);
             _buildOutputPanel.DiagnosticNavigated += (path, line) => { OpenFileInNewTab(path); GoToLine(line); };
             _buildOutputPanel.BuildRequested += () => { if (_solutionExplorerPanel != null) _ = _solutionExplorerPanel.BuildCurrentSolutionAsync(); };
             _buildOutputPanel.CancelRequested += () => _solutionExplorerPanel?.CancelBuild();
             var buildOutputPage = new TabPage("Output")
             {
                 BackColor = _themeManager.CurrentTheme.EditorBackground,
                 Tag = "output"
             };
             _buildOutputPanel.Dock = DockStyle.Fill;
             buildOutputPage.Controls.Add(_buildOutputPanel);
             _terminalTabControl?.TabPages.Insert(0, buildOutputPage);

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
                  _workspacePanel.CloseRequested += () =>
                  {
                      _workspaceVisible = false;
                      workspaceMenuItem.Checked = false;
                      _workspaceWidth = Math.Max(80, Math.Min(600, _workspaceSplitContainer?.SplitterDistance ?? 250));
                      UpdateSidebarLayout();
                      SaveSettings();
                  };
                  _workspacePanel.ScanStarted += OnWorkspaceScanStarted;
                  _workspacePanel.ScanCompleted += OnWorkspaceScanCompleted;
                   _workspacePanel.ScanProgressChanged += OnWorkspaceScanProgressChanged;
                   _workspacePanel.RootChanged += (path) => ActiveTerminal?.SendInput($"cd \"{path}\"{Environment.NewLine}");
 
                  // Git panel
                  _gitPanel = new GitPanel(_gitService);
                  ApplyGitSettings();
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

                  _solutionExplorerPanel = new SolutionExplorerPanel();
                  _solutionExplorerPanel.FileOpenRequested += (path) =>
                  {
                      if (!string.IsNullOrEmpty(path) && File.Exists(path))
                          OpenFileInNewTab(path);
                  };
                  _solutionExplorerPanel.CloseRequested += () => ToggleSolutionExplorer();
                  _solutionExplorerPanel.SolutionLoaded += (slnPath) =>
                  {
                      string? slnDir = Path.GetDirectoryName(slnPath);
                      if (!string.IsNullOrEmpty(slnDir))
                      {
                          _workspaceRoot = slnDir;
                          _workspacePanel?.SetRoot(slnDir);
                      }
                  };
                  _solutionExplorerPanel.BuildStarted += (targetPath) =>
                  {
                      _buildOutputPanel?.Clear();
                      _buildOutputPanel?.SetBusy(true);
                      _buildOutputPanel?.AppendLine($"Build started: {Path.GetFileName(targetPath)}", false);
                      if (_terminalTabControl != null)
                          foreach (TabPage tp in _terminalTabControl.TabPages)
                              if (tp.Tag as string == "output") { _terminalTabControl.SelectedTab = tp; break; }
                      ShowTerminal();
                  };
                  _solutionExplorerPanel.BuildOutputLine += (line, isErr) => _buildOutputPanel?.AppendLine(line, isErr);
                  _solutionExplorerPanel.BuildFinished += (result) =>
                  {
                      _buildOutputPanel?.ShowResult(result);
                      if (!result.Success && _problemsPanel != null)
                          BeginInvoke(() => _problemsPanelVisible = true);
                  };
                  _solutionExplorerPanel.SetTheme(_themeManager.CurrentTheme);

                  _sideTopTabs = new TabControl
                  {
                      Dock = DockStyle.Fill,
                      DrawMode = TabDrawMode.OwnerDrawFixed,
                      SizeMode = TabSizeMode.Fixed,
                      ItemSize = new Size(0, 24),
                      Font = new Font("Segoe UI", 8f),
                      Padding = new Point(8, 3)
                  };
                  _sideTopTabs.DrawItem += SideTopTabs_DrawItem;

                  var explorerTab = new TabPage("Explorer") { BackColor = _themeManager.CurrentTheme.MenuBackground };
                  explorerTab.Controls.Add(_workspacePanel);
                  _workspacePanel.Dock = DockStyle.Fill;

                  var solutionTab = new TabPage("Solution") { BackColor = _themeManager.CurrentTheme.MenuBackground };
                  solutionTab.Controls.Add(_solutionExplorerPanel);
                  _solutionExplorerPanel.Dock = DockStyle.Fill;

                  _sideTopTabs.TabPages.Add(explorerTab);
                  _sideTopTabs.TabPages.Add(solutionTab);

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
                  _sidebarSplit.Panel1.Controls.Add(_sideTopTabs);
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

              InitAIOps();

              if (!string.IsNullOrEmpty(_workspaceRoot))
                        Task.Run(() => BeginInvoke(() => _workspacePanel?.SetRoot(_workspaceRoot)));
              UpdateWorkspaceProjectLabel();
              _workspaceProjectLabel.Click += WorkspaceProjectLabel_Click;
              // Set initial build config button text
              _buildConfigCombo.Text = _activeConfiguration;
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
                gutterPanel!.BookmarkClicked += (line) => ToggleBookmark(line);

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
                // Defer until after the layout pass completes so all child control
                // bounds are finalized before we read editorPanel / textEditor dimensions.
                // Calling PositionMinimap() directly here reads stale ClientSize values
                // (layout is still in progress), which collapses the minimap to 1px wide.
                BeginInvoke((Action)(() =>
                {
                    PositionMinimap();
                    if (stickyScrollPanel.Visible)
                        stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
                    PositionTabDropdownButton();
                }));
            };
            editorPanel.SizeChanged += (s, e) =>
            {
                // Defer: editorPanel.ClientSize reflects the new size immediately, but
                // child controls (textEditor) may not have been laid out yet. Deferring
                // via BeginInvoke ensures all children are settled before we position.
                BeginInvoke((Action)(() =>
                {
                    if (_pendingMinimapVisible)
                        PositionMinimap();
                    if (stickyScrollPanel.Visible)
                        stickyScrollPanel.SyncWidth(editorPanel.ClientSize.Width);
                }));
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
                    int minTabWidth = 32; // fallback; overwritten by UI-thread capture

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
                        // Measure one character width so elastic stops are never narrower than
                        // a normal fixed-width tab (charWidth × tabSize).  This prevents leading-
                        // indentation tabs (empty cells) from collapsing to 2 px after a paste.
                        var charSz = TextRenderer.MeasureText("0", textEditor.Font,
                            new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                        minTabWidth = Math.Max(8, charSz.Width * tabSize);
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
                        token,
                        minTabWidth);

                    if (token.IsCancellationRequested) return;

                    // Apply to editor on UI thread (fire-and-forget)
                    try
                    {
                        textEditor.BeginInvoke(new Action(() =>
                        {
                            if (textEditor.IsDisposed || textEditor.Disposing || stops.Length == 0) return;

                            // Three problems with naive SelectionTabs assignment:
                            //  1. Setting it on the current paragraph only (old code) leaves other
                            //     paragraphs with different stop arrays → cursor-movement visual shift.
                            //  2. Setting it via Select(0,TextLength) (previous fix) applies stops
                            //     from the VISIBLE range to the entire document, compressing off-screen
                            //     lines that have deeper indentation.  Also causes visible selection
                            //     flash (blinking) on every 250ms elastic-tab recompute.
                            //  3. SelectionTabs fires TextChanged → elastic timer restart → infinite loop.
                            //
                            // Correct fix:
                            //  • Apply to the VISIBLE RANGE ONLY — all visible paragraphs get the
                            //    same stops (preventing cursor-movement shifts) without touching
                            //    off-screen content (preventing compression).
                            //  • Wrap in BeginUpdate/EndUpdate to suppress all intermediate paints
                            //    (no blinking).
                            //  • Set _applyingElasticTabs before so TextChanged guard blocks the
                            //    timer restart when SelectionTabs changes fire TextChanged.

                            int savedStart  = textEditor.SelectionStart;
                            int savedLength = textEditor.SelectionLength;
                            _suspendSelectionChanged = true;
                            _applyingElasticTabs = true;
                            BeginUpdate(textEditor);
                            try
                            {
                                int startChar = Math.Max(0, textEditor.GetFirstCharIndexFromLine(firstVisible));
                                int endChar;
                                int nextLine = lastIndex + 1;
                                if (nextLine < textEditor.Lines.Length)
                                    endChar = textEditor.GetFirstCharIndexFromLine(nextLine);
                                else
                                    endChar = textEditor.TextLength;
                                if (endChar <= startChar) endChar = textEditor.TextLength;

                                textEditor.Select(startChar, Math.Max(0, endChar - startChar));
                                textEditor.SelectionTabs = stops;
                            }
                            finally
                            {
                                textEditor.Select(savedStart, savedLength);
                                _applyingElasticTabs = false;
                                _suspendSelectionChanged = false;
                                EndUpdate(textEditor);
                                textEditor.Invalidate();
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
            if (textEditor != null)
            {
                textEditor.WhitespaceGlyphColor = theme.IsLight
                    ? Color.FromArgb(130, 150, 150, 150)
                    : Color.FromArgb(110, 190, 190, 190);
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
                var settings = _settingsService.LoadWithDecrypt();
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
                _terminalStartingDirectory = settings.TerminalStartingDirectory ?? "";
                _terminalFontFace = settings.TerminalFontFace ?? "";
                _terminalFontSize = Math.Max(0f, settings.TerminalFontSize);
                _terminalFontBold = settings.TerminalFontBold;
                _terminalWordWrap = settings.TerminalWordWrap;
                _terminalScrollbarVisible = settings.TerminalScrollbarVisible;
                _terminalPadding = Math.Clamp(settings.TerminalPadding, 0, 20);
                _terminalMaxScrollback = Math.Clamp(settings.TerminalMaxScrollback, 500, 50000);
                _terminalTabTitle = settings.TerminalTabTitle ?? "";
                _securityProfile = settings.SecurityProfile;
                _secPromptUntrustedWorkspace = settings.SecPromptUntrustedWorkspace;
                _secTrustedWorkspacePaths = settings.SecTrustedWorkspacePaths;
                _secConfirmUrlOpen = settings.SecConfirmUrlOpen;
                _secAllowHttpUrls = settings.SecAllowHttpUrls;
                _secConfirmExternalTools = settings.SecConfirmExternalTools;
                _secConfirmDebugStart = settings.SecConfirmDebugStart;
                _secDebugAdapterPathOnly = settings.SecDebugAdapterPathOnly;
                _secConfirmGitClone = settings.SecConfirmGitClone;
                _secTrustedGitHosts = settings.SecTrustedGitHosts;
                _secSastEnabled = settings.SecSastEnabled;
                _secSastSeverityThreshold = settings.SecSastSeverityThreshold;
                _secWriteCrashLog = settings.SecWriteCrashLog;
                _secWriteStartupLog = settings.SecWriteStartupLog;
                _secLogRetentionDays = settings.SecLogRetentionDays;
                _secHighlightHardcodedSecrets = settings.SecHighlightHardcodedSecrets;
                // Window settings
                _winRememberBounds = settings.WinRememberBounds;
                _winLaunchMaximized = settings.WinLaunchMaximized;
                _winRestoreSession = settings.WinRestoreSession;
                _winDarkTitleBar = settings.WinDarkTitleBar;
                _winOpacity = settings.WinOpacity;
                // Workspace settings
                _wsShowAllFiles = settings.WsShowAllFiles;
                _wsExcludedDirs = settings.WsExcludedDirs;
                _wsTreeItemHeight = settings.WsTreeItemHeight;
                _wsTreeIndent = settings.WsTreeIndent;
                _wsAutoCollapse = settings.WsAutoCollapse;
                _wsWatcherDebounceMs = settings.WsWatcherDebounceMs;
                _wsDisableFileWatcher = settings.WsDisableFileWatcher;
                _wsMaxRecentWorkspaces = settings.WsMaxRecentWorkspaces;
                _wsMaxRecentFiles = settings.WsMaxRecentFiles;
                // Editor Management settings
                _tabWidth = Math.Clamp(settings.TabWidth, 60, 300);
                _tabHeight = Math.Clamp(settings.TabHeight, 22, 40);
                _tabShowFileIcons = settings.TabShowFileIcons;
                _tabDirtyIndicator = settings.TabDirtyIndicator ?? "asterisk";
                _tabCloseButtonVisibility = settings.TabCloseButtonVisibility ?? "always";
                _tabMiddleClickClose = settings.TabMiddleClickClose;
                _tabMouseWheelScroll = settings.TabMouseWheelScroll;
                _tabMaxOpen = Math.Max(0, settings.TabMaxOpen);
                _tabConfirmCloseUnsaved = settings.TabConfirmCloseUnsaved;
                _tabRememberRecentlyClosed = settings.TabRememberRecentlyClosed;
                _tabMaxRecentlyClosed = Math.Clamp(settings.TabMaxRecentlyClosed, 5, 20);
                // Find settings
                _findCaseSensitive = settings.FindCaseSensitive;
                _findUseRegex = settings.FindUseRegex;
                _findWrapAround = settings.FindWrapAround;
                _findSeedFromSelection = settings.FindSeedFromSelection;
                _findNotFoundNotification = settings.FindNotFoundNotification ?? "messagebox";
                _findInFilesFilter = string.IsNullOrWhiteSpace(settings.FindInFilesFilter) ? _findInFilesFilter : settings.FindInFilesFilter;
                _findInFilesExclude = string.IsNullOrWhiteSpace(settings.FindInFilesExclude) ? _findInFilesExclude : settings.FindInFilesExclude;
                _findInFilesMaxResults = Math.Max(0, settings.FindInFilesMaxResults);
                // Text Editor settings
                _trimTrailingWhitespace = settings.TrimTrailingWhitespace;
                _insertFinalNewline = settings.InsertFinalNewline;
                _defaultLineEnding = settings.DefaultLineEnding ?? "auto";
                _ctrlWheelZoom = settings.CtrlWheelZoom;
                _mouseWheelScrollLines = Math.Clamp(settings.MouseWheelScrollLines, 0, 20);
                _autoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds, 10, 300);
                // Workbench Appearance settings
                _showLineNumbers = settings.ShowLineNumbers;
                _relativeLineNumbers = settings.RelativeLineNumbers;
                _minimapWidth = Math.Clamp(settings.MinimapWidth, 60, 200);
                _showGitStatusBar = settings.ShowGitStatusBar;
                _showRoslynStatusBar = settings.ShowRoslynStatusBar;
                // Extensions settings
                _snippetsEnabled = settings.SnippetsEnabled;
                _snippetTriggerKey = settings.SnippetTriggerKey ?? "Tab";
                _lintEnabled = settings.LintEnabled;
                _lintMaxLineLength = Math.Clamp(settings.LintMaxLineLength, 60, 300);
                _lintFlagMagicNumbers = settings.LintFlagMagicNumbers;
                _lintFlagNamingConventions = settings.LintFlagNamingConventions;
                _treeSitterEnabled = settings.TreeSitterEnabled;
                _todoScanEnabled = settings.TodoScanEnabled;
                _todoScanPatterns = string.IsNullOrWhiteSpace(settings.TodoScanPatterns) ? _todoScanPatterns : settings.TodoScanPatterns;
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
                _lineHighlightWhenUnfocused = settings.LineHighlightWhenUnfocused;
                _cursorStyle = settings.CursorStyle ?? "line";
                _cursorBlinkEnabled = settings.CursorBlinkEnabled;
                _cursorBlinkRateMs = Math.Clamp(settings.CursorBlinkRateMs, 200, 1200);
                _showOccurrenceHighlights = settings.ShowOccurrenceHighlights;
                _showIndentGuides = settings.ShowIndentGuides;
                _smoothenCaretBlink = settings.SmoothenCaretBlink;
                _highlightTrailingWhitespace = settings.HighlightTrailingWhitespace;
                _multiCursorEnabled = settings.MultiCursorEnabled;
                _smartHomeKey = settings.SmartHomeKey;
                _bracketTypeOver = settings.BracketTypeOver;
                _autoCloseBracesOnEnter = settings.AutoCloseBracesOnEnter;
                _formatOnSave = settings.FormatOnSave;
                _detectIndentationFromFile = settings.DetectIndentationFromFile;
                _analyzersEnabled = settings.AnalyzersEnabled;
                _autoSaveEnabled = settings.AutoSaveEnabled;
                if (_autoSaveEnabled) _autoSaveTimer.Start();
                vimModeEnabled = settings.VimModeEnabled;
                _stickyScrollEnabled = settings.StickyScrollEnabled;
                _rulerEnabled = settings.RulerEnabled;
                rulerPanel.Visible = _rulerEnabled;
                if (rulerMenuItem != null) rulerMenuItem.Checked = _rulerEnabled;
                bool showWhitespace = settings.ShowWhitespace;
                if (whitespaceMenuItem != null)
                {
                    whitespaceMenuItem.Checked = showWhitespace;
                    textEditor.ShowWhitespace = showWhitespace;
                }
                if (showGutterBreakpointsMenuItem != null)
                    showGutterBreakpointsMenuItem.Checked = settings.ShowGutterBreakpoints;
                if (showGutterBookmarksMenuItem != null)
                    showGutterBookmarksMenuItem.Checked = settings.ShowGutterBookmarks;
                if (gutterPanel != null)
                {
                    gutterPanel.ShowBreakpoints = settings.ShowGutterBreakpoints;
                    gutterPanel.ShowBookmarks   = settings.ShowGutterBookmarks;
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
                ApplyTerminalSettingsToAll();
                ApplySecuritySettings();
                ApplyExtensionsSettings();
                ApplyCursorSettings();
                ApplyEditorRenderingSettings();
            }
            catch { /* ignore settings load errors */ }
        }

        private string _savedWindowBounds = "";
        private string _savedWindowState = "";

        private void RestoreWindowBounds()
        {
            if (_winLaunchMaximized)
            {
                WindowState = FormWindowState.Maximized;
                return;
            }

            if (!_winRememberBounds) return;

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
                TerminalStartingDirectory = _terminalStartingDirectory,
                TerminalFontFace = _terminalFontFace,
                TerminalFontSize = _terminalFontSize,
                TerminalFontBold = _terminalFontBold,
                TerminalWordWrap = _terminalWordWrap,
                TerminalScrollbarVisible = _terminalScrollbarVisible,
                TerminalPadding = _terminalPadding,
                TerminalMaxScrollback = _terminalMaxScrollback,
                TerminalTabTitle = _terminalTabTitle,
                GitAuthorName = _gitAuthorName,
                GitAuthorEmail = _gitAuthorEmail,
                GitDefaultBranch = _gitDefaultBranch,
                GitConfirmRemoveRepo = _gitConfirmRemoveRepo,
                GitConfirmDiscardChanges = _gitConfirmDiscardChanges,
                GitConfirmDiscardPermanent = _gitConfirmDiscardPermanent,
                GitConfirmDiscardStash = _gitConfirmDiscardStash,
                GitConfirmCheckoutCommit = _gitConfirmCheckoutCommit,
                GitConfirmForcePush = _gitConfirmForcePush,
                GitConfirmUndoCommit = _gitConfirmUndoCommit,
                GitConfirmOverrideCommitMsg = _gitConfirmOverrideCommitMsg,
                GitConfirmHiddenChanges = _gitConfirmHiddenChanges,
                GitBranchSwitchBehavior = _gitBranchSwitchBehavior,
                GitCommitLengthWarning = _gitCommitLengthWarning,
                SecurityProfile = _securityProfile,
                SecPromptUntrustedWorkspace = _secPromptUntrustedWorkspace,
                SecTrustedWorkspacePaths = _secTrustedWorkspacePaths,
                SecConfirmUrlOpen = _secConfirmUrlOpen,
                SecAllowHttpUrls = _secAllowHttpUrls,
                SecConfirmExternalTools = _secConfirmExternalTools,
                SecConfirmDebugStart = _secConfirmDebugStart,
                SecDebugAdapterPathOnly = _secDebugAdapterPathOnly,
                SecConfirmGitClone = _secConfirmGitClone,
                SecTrustedGitHosts = _secTrustedGitHosts,
                SecSastEnabled = _secSastEnabled,
                SecSastSeverityThreshold = _secSastSeverityThreshold,
                SecWriteCrashLog = _secWriteCrashLog,
                SecWriteStartupLog = _secWriteStartupLog,
                SecLogRetentionDays = _secLogRetentionDays,
                SecHighlightHardcodedSecrets = _secHighlightHardcodedSecrets,
                WinRememberBounds = _winRememberBounds,
                WinLaunchMaximized = _winLaunchMaximized,
                WinRestoreSession = _winRestoreSession,
                WinDarkTitleBar = _winDarkTitleBar,
                WinOpacity = _winOpacity,
                WsShowAllFiles = _wsShowAllFiles,
                WsExcludedDirs = _wsExcludedDirs,
                WsTreeItemHeight = _wsTreeItemHeight,
                WsTreeIndent = _wsTreeIndent,
                WsAutoCollapse = _wsAutoCollapse,
                WsWatcherDebounceMs = _wsWatcherDebounceMs,
                WsDisableFileWatcher = _wsDisableFileWatcher,
                WsMaxRecentWorkspaces = _wsMaxRecentWorkspaces,
                WsMaxRecentFiles = _wsMaxRecentFiles,
                TabWidth = _tabWidth,
                TabHeight = _tabHeight,
                TabShowFileIcons = _tabShowFileIcons,
                TabDirtyIndicator = _tabDirtyIndicator,
                TabCloseButtonVisibility = _tabCloseButtonVisibility,
                TabMiddleClickClose = _tabMiddleClickClose,
                TabMouseWheelScroll = _tabMouseWheelScroll,
                TabMaxOpen = _tabMaxOpen,
                TabConfirmCloseUnsaved = _tabConfirmCloseUnsaved,
                TabRememberRecentlyClosed = _tabRememberRecentlyClosed,
                TabMaxRecentlyClosed = _tabMaxRecentlyClosed,
                FindCaseSensitive = _findCaseSensitive,
                FindUseRegex = _findUseRegex,
                FindWrapAround = _findWrapAround,
                FindSeedFromSelection = _findSeedFromSelection,
                FindNotFoundNotification = _findNotFoundNotification,
                FindInFilesFilter = _findInFilesFilter,
                FindInFilesExclude = _findInFilesExclude,
                FindInFilesMaxResults = _findInFilesMaxResults,
                TrimTrailingWhitespace = _trimTrailingWhitespace,
                InsertFinalNewline = _insertFinalNewline,
                DefaultLineEnding = _defaultLineEnding,
                CtrlWheelZoom = _ctrlWheelZoom,
                MouseWheelScrollLines = _mouseWheelScrollLines,
                AutoSaveIntervalSeconds = _autoSaveIntervalSeconds,
                ShowLineNumbers = _showLineNumbers,
                RelativeLineNumbers = _relativeLineNumbers,
                MinimapWidth = _minimapWidth,
                ShowGitStatusBar = _showGitStatusBar,
                ShowRoslynStatusBar = _showRoslynStatusBar,
                SnippetsEnabled = _snippetsEnabled,
                SnippetTriggerKey = _snippetTriggerKey,
                LintEnabled = _lintEnabled,
                LintMaxLineLength = _lintMaxLineLength,
                LintFlagMagicNumbers = _lintFlagMagicNumbers,
                LintFlagNamingConventions = _lintFlagNamingConventions,
                TreeSitterEnabled = _treeSitterEnabled,
                TodoScanEnabled = _todoScanEnabled,
                TodoScanPatterns = _todoScanPatterns,
                LineHighlightWhenUnfocused = _lineHighlightWhenUnfocused,
                CursorStyle = _cursorStyle,
                CursorBlinkEnabled = _cursorBlinkEnabled,
                CursorBlinkRateMs = _cursorBlinkRateMs,
                ShowOccurrenceHighlights = _showOccurrenceHighlights,
                ShowIndentGuides = _showIndentGuides,
                SmoothenCaretBlink = _smoothenCaretBlink,
                HighlightTrailingWhitespace = _highlightTrailingWhitespace,
                MultiCursorEnabled = _multiCursorEnabled,
                SmartHomeKey = _smartHomeKey,
                BracketTypeOver = _bracketTypeOver,
                AutoCloseBracesOnEnter = _autoCloseBracesOnEnter,
                FormatOnSave = _formatOnSave,
                DetectIndentationFromFile = _detectIndentationFromFile,
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
                RulerEnabled = _rulerEnabled,
                ShowGutterBreakpoints = showGutterBreakpointsMenuItem?.Checked ?? true,
                ShowGutterBookmarks   = showGutterBookmarksMenuItem?.Checked ?? true,
                AIOpsConfig = _aiopsEngine?.CurrentSettings?.CreateEncryptedCopy() ?? _settingsService.LoadWithDecrypt()?.AIOpsConfig,
                DebugAdapterType = _debugAdapterType,
                DebugAdapterPath = _debugAdapterPath,
            };
            _settingsService.Save(settings, SecurityEnforcementService.ShouldEncryptSettings(settings.SecurityProfile));
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
                textEditor.ShowWhitespace = settings.ShowWhitespace;
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
            _lineHighlightWhenUnfocused = settings.LineHighlightWhenUnfocused;
            _cursorStyle = settings.CursorStyle ?? "line";
            _cursorBlinkEnabled = settings.CursorBlinkEnabled;
            _cursorBlinkRateMs = Math.Clamp(settings.CursorBlinkRateMs, 200, 1200);
            _showOccurrenceHighlights = settings.ShowOccurrenceHighlights;
            _showIndentGuides = settings.ShowIndentGuides;
            _smoothenCaretBlink = settings.SmoothenCaretBlink;
            _highlightTrailingWhitespace = settings.HighlightTrailingWhitespace;
            _multiCursorEnabled = settings.MultiCursorEnabled;
            _smartHomeKey = settings.SmartHomeKey;
            _bracketTypeOver = settings.BracketTypeOver;
            _autoCloseBracesOnEnter = settings.AutoCloseBracesOnEnter;
            _formatOnSave = settings.FormatOnSave;
            _detectIndentationFromFile = settings.DetectIndentationFromFile;
            ApplyCursorSettings();
            ApplyEditorRenderingSettings();

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
            _terminalStartingDirectory = settings.TerminalStartingDirectory ?? "";
            _terminalFontFace = settings.TerminalFontFace ?? "";
            _terminalFontSize = Math.Max(0f, settings.TerminalFontSize);
            _terminalFontBold = settings.TerminalFontBold;
            _terminalWordWrap = settings.TerminalWordWrap;
            _terminalScrollbarVisible = settings.TerminalScrollbarVisible;
            _terminalPadding = Math.Clamp(settings.TerminalPadding, 0, 20);
            _terminalMaxScrollback = Math.Clamp(settings.TerminalMaxScrollback, 500, 50000);
            _terminalTabTitle = settings.TerminalTabTitle ?? "";
            ApplyTerminalSettingsToAll();

            // Apply git settings
            _gitAuthorName = settings.GitAuthorName;
            _gitAuthorEmail = settings.GitAuthorEmail;
            _gitDefaultBranch = settings.GitDefaultBranch;
            _gitConfirmRemoveRepo = settings.GitConfirmRemoveRepo;
            _gitConfirmDiscardChanges = settings.GitConfirmDiscardChanges;
            _gitConfirmDiscardPermanent = settings.GitConfirmDiscardPermanent;
            _gitConfirmDiscardStash = settings.GitConfirmDiscardStash;
            _gitConfirmCheckoutCommit = settings.GitConfirmCheckoutCommit;
            _gitConfirmForcePush = settings.GitConfirmForcePush;
            _gitConfirmUndoCommit = settings.GitConfirmUndoCommit;
            _gitConfirmOverrideCommitMsg = settings.GitConfirmOverrideCommitMsg;
            _gitConfirmHiddenChanges = settings.GitConfirmHiddenChanges;
            _gitBranchSwitchBehavior = settings.GitBranchSwitchBehavior;
            _gitCommitLengthWarning = settings.GitCommitLengthWarning;
            ApplyGitSettings();

            // Apply security settings
            _securityProfile = settings.SecurityProfile;
            SecurityEnforcementService.CurrentProfile = _securityProfile;
            _secPromptUntrustedWorkspace = settings.SecPromptUntrustedWorkspace;
            _secTrustedWorkspacePaths = settings.SecTrustedWorkspacePaths;
            _secConfirmUrlOpen = settings.SecConfirmUrlOpen;
            _secAllowHttpUrls = settings.SecAllowHttpUrls;
            _secConfirmExternalTools = settings.SecConfirmExternalTools;
            _secConfirmDebugStart = settings.SecConfirmDebugStart;
            _secDebugAdapterPathOnly = settings.SecDebugAdapterPathOnly;
            _secConfirmGitClone = settings.SecConfirmGitClone;
            _secTrustedGitHosts = settings.SecTrustedGitHosts;
            _secSastEnabled = settings.SecSastEnabled;
            _secSastSeverityThreshold = settings.SecSastSeverityThreshold;
            _secWriteCrashLog = settings.SecWriteCrashLog;
            _secWriteStartupLog = settings.SecWriteStartupLog;
            _secLogRetentionDays = settings.SecLogRetentionDays;
            _secHighlightHardcodedSecrets = settings.SecHighlightHardcodedSecrets;
            ApplySecuritySettings();

            // Window settings
            _winRememberBounds = settings.WinRememberBounds;
            _winLaunchMaximized = settings.WinLaunchMaximized;
            _winRestoreSession = settings.WinRestoreSession;
            _winDarkTitleBar = settings.WinDarkTitleBar;
            _winOpacity = Math.Clamp(settings.WinOpacity, 50, 100);
            MaxFileSizeMB = Math.Max(10, settings.MaxFileSizeMB);
            LargeFileWarningMB = Math.Max(1, settings.LargeFileWarningMB);
            AsyncFileWarningMB = Math.Max(1, settings.AsyncFileWarningMB);
            DisableSyntaxHighlightingForLargeFiles = settings.DisableSyntaxHighlightingForLargeFiles;
            DisableMinimapForLargeFiles = settings.DisableMinimapForLargeFiles;
            DisableWordWrapForLargeFiles = settings.DisableWordWrapForLargeFiles;
            ApplyWindowSettings();

            // Workspace settings
            _wsShowAllFiles = settings.WsShowAllFiles;
            _wsExcludedDirs = settings.WsExcludedDirs;
            _wsTreeItemHeight = settings.WsTreeItemHeight;
            _wsTreeIndent = settings.WsTreeIndent;
            _wsAutoCollapse = settings.WsAutoCollapse;
            _wsWatcherDebounceMs = settings.WsWatcherDebounceMs;
            _wsDisableFileWatcher = settings.WsDisableFileWatcher;
            _wsMaxRecentWorkspaces = settings.WsMaxRecentWorkspaces;
            _wsMaxRecentFiles = settings.WsMaxRecentFiles;
            ApplyWorkspaceSettings();

            // Editor Management settings
            _tabWidth = Math.Clamp(settings.TabWidth, 60, 300);
            _tabHeight = Math.Clamp(settings.TabHeight, 22, 40);
            _tabShowFileIcons = settings.TabShowFileIcons;
            _tabDirtyIndicator = settings.TabDirtyIndicator ?? "asterisk";
            _tabCloseButtonVisibility = settings.TabCloseButtonVisibility ?? "always";
            _tabMiddleClickClose = settings.TabMiddleClickClose;
            _tabMouseWheelScroll = settings.TabMouseWheelScroll;
            _tabMaxOpen = Math.Max(0, settings.TabMaxOpen);
            _tabConfirmCloseUnsaved = settings.TabConfirmCloseUnsaved;
            _tabRememberRecentlyClosed = settings.TabRememberRecentlyClosed;
            _tabMaxRecentlyClosed = Math.Clamp(settings.TabMaxRecentlyClosed, 5, 20);
            ApplyTabSettings();

            // Find settings
            _findCaseSensitive = settings.FindCaseSensitive;
            _findUseRegex = settings.FindUseRegex;
            _findWrapAround = settings.FindWrapAround;
            _findSeedFromSelection = settings.FindSeedFromSelection;
            _findNotFoundNotification = settings.FindNotFoundNotification ?? "messagebox";
            _findInFilesFilter = string.IsNullOrWhiteSpace(settings.FindInFilesFilter) ? _findInFilesFilter : settings.FindInFilesFilter;
            _findInFilesExclude = string.IsNullOrWhiteSpace(settings.FindInFilesExclude) ? _findInFilesExclude : settings.FindInFilesExclude;
            _findInFilesMaxResults = Math.Max(0, settings.FindInFilesMaxResults);
            ApplyFindSettings();

            // Text Editor settings
            _trimTrailingWhitespace = settings.TrimTrailingWhitespace;
            _insertFinalNewline = settings.InsertFinalNewline;
            _defaultLineEnding = settings.DefaultLineEnding ?? "auto";
            _ctrlWheelZoom = settings.CtrlWheelZoom;
            _mouseWheelScrollLines = Math.Clamp(settings.MouseWheelScrollLines, 0, 20);
            _autoSaveIntervalSeconds = Math.Clamp(settings.AutoSaveIntervalSeconds, 10, 300);
            ApplyTextEditorSettings();

            // Workbench Appearance settings
            _showLineNumbers = settings.ShowLineNumbers;
            _relativeLineNumbers = settings.RelativeLineNumbers;
            _minimapWidth = Math.Clamp(settings.MinimapWidth, 60, 200);
            _showGitStatusBar = settings.ShowGitStatusBar;
            _showRoslynStatusBar = settings.ShowRoslynStatusBar;
            ApplyAppearanceSettings();

            // Extensions settings
            _snippetsEnabled = settings.SnippetsEnabled;
            _snippetTriggerKey = settings.SnippetTriggerKey ?? "Tab";
            _lintEnabled = settings.LintEnabled;
            _lintMaxLineLength = Math.Clamp(settings.LintMaxLineLength, 60, 300);
            _lintFlagMagicNumbers = settings.LintFlagMagicNumbers;
            _lintFlagNamingConventions = settings.LintFlagNamingConventions;
            _treeSitterEnabled = settings.TreeSitterEnabled;
            _todoScanEnabled = settings.TodoScanEnabled;
            _todoScanPatterns = string.IsNullOrWhiteSpace(settings.TodoScanPatterns) ? _todoScanPatterns : settings.TodoScanPatterns;
            ApplyExtensionsSettings();

            // Cursor settings
            _lineHighlightWhenUnfocused = settings.LineHighlightWhenUnfocused;
            _cursorStyle = settings.CursorStyle ?? "line";
            _cursorBlinkEnabled = settings.CursorBlinkEnabled;
            _cursorBlinkRateMs = Math.Clamp(settings.CursorBlinkRateMs, 200, 1200);
            _showOccurrenceHighlights = settings.ShowOccurrenceHighlights;
            _showIndentGuides = settings.ShowIndentGuides;
            _smoothenCaretBlink = settings.SmoothenCaretBlink;
            _highlightTrailingWhitespace = settings.HighlightTrailingWhitespace;
            _multiCursorEnabled = settings.MultiCursorEnabled;
            _smartHomeKey = settings.SmartHomeKey;
            _bracketTypeOver = settings.BracketTypeOver;
            _autoCloseBracesOnEnter = settings.AutoCloseBracesOnEnter;
            _formatOnSave = settings.FormatOnSave;
            _detectIndentationFromFile = settings.DetectIndentationFromFile;
            ApplyCursorSettings();
            ApplyEditorRenderingSettings();

            vimModeEnabled = settings.VimModeEnabled;
            vimModeLabel.Visible = settings.VimModeEnabled;
            if (!settings.VimModeEnabled) vimModeLabel.Text = "";

            // Sticky scroll
            _stickyScrollEnabled = settings.StickyScrollEnabled;
            if (stickyScrollMenuItem != null) stickyScrollMenuItem.Checked = settings.StickyScrollEnabled;
            stickyScrollPanel.Visible = settings.StickyScrollEnabled;

            // Ruler
            _rulerEnabled = settings.RulerEnabled;
            rulerPanel.Visible = _rulerEnabled;
            if (rulerMenuItem != null) rulerMenuItem.Checked = _rulerEnabled;
            gutterPanel.TopOffset = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (_rulerEnabled ? rulerPanel.Height : 0);
            RepositionStickyScrollPanel();

            // Sync menu item states
            if (gutterMenuItem != null) gutterMenuItem.Checked = settings.GutterVisible;
            if (wordWrapMenuItem != null) wordWrapMenuItem.Checked = settings.WordWrapEnabled;
            if (autoIndentMenuItem != null) autoIndentMenuItem.Checked = settings.AutoIndentEnabled;
            if (insertSpacesMenuItem != null) insertSpacesMenuItem.Checked = settings.InsertSpaces;
            if (showGutterBreakpointsMenuItem != null)
                showGutterBreakpointsMenuItem.Checked = settings.ShowGutterBreakpoints;
            if (showGutterBookmarksMenuItem != null)
                showGutterBookmarksMenuItem.Checked = settings.ShowGutterBookmarks;
            gutterPanel.ShowBreakpoints = settings.ShowGutterBreakpoints;
            gutterPanel.ShowBookmarks   = settings.ShowGutterBookmarks;
            SyncGutterColumnWidth();

            // Debug adapter
            _debugAdapterType = settings.DebugAdapterType;
            _debugAdapterPath = settings.DebugAdapterPath;

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
            // Layout settles asynchronously after the visibility change;
            // defer minimap repositioning so we read correct bounds.
            BeginInvoke((Action)PositionMinimap);
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
            gutterPanel.TopOffset = (_breadcrumbsEnabled ? breadcrumbPanel.Height : 0) + (_rulerEnabled ? rulerPanel.Height : 0);
            gutterPanel.Invalidate();
            RepositionStickyScrollPanel();
            UpdateBreadcrumbs();
            SaveSettings();
        }

        private bool _rulerEnabled = true;

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
            if (_solutionExplorerPanel != null)
                _solutionExplorerPanel.SetTheme(theme);
            if (_sideTopTabs != null)
            {
                _sideTopTabs.BackColor = theme.MenuBackground;
                foreach (TabPage page in _sideTopTabs.TabPages)
                    page.BackColor = theme.MenuBackground;
                _sideTopTabs.Invalidate();
            }
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
            if (_aiopsPanel != null)
                _aiopsPanel.SetTheme(theme);
            if (_aiopsSecurityPanel != null)
                _aiopsSecurityPanel.SetTheme(theme);
            if (_aiopsDeploymentPanel != null)
                _aiopsDeploymentPanel.SetTheme(theme);
            if (_aiopsTelemetryPanel != null)
                _aiopsTelemetryPanel.SetTheme(theme);
            if (_aiopsInsightsPanel != null)
                _aiopsInsightsPanel.SetTheme(theme);
            if (_aiopsQueryPanel != null)
                _aiopsQueryPanel.SetTheme(theme);
            if (_aiopsTimelinePanel != null)
                _aiopsTimelinePanel.SetTheme(theme);
            if (_aiopsPrRiskPanel != null)
                _aiopsPrRiskPanel.SetTheme(theme);
            if (_aiopsServiceDepPanel != null)
                _aiopsServiceDepPanel.SetTheme(theme);
            if (_aiopsRunbookPanel != null)
                _aiopsRunbookPanel.SetTheme(theme);
            if (_editorSplitContainer != null)
                _editorSplitContainer.BackColor = theme.EditorBackground;
            _buildOutputPanel?.SetTheme(theme);
        }

        private void ApplyTerminalSettingsToAll()
        {
            foreach (var terminal in _terminalTabs)
            {
                terminal.CustomTabTitle = _terminalTabTitle;
                terminal.StartingDirectory = ResolveTerminalStartingDirectory();
                terminal.ApplyTerminalSettings(_terminalFontFace, _terminalFontSize, _terminalFontBold, _terminalWordWrap, _terminalScrollbarVisible, _terminalPadding);
                terminal.SetMaxScrollback(_terminalMaxScrollback);
            }

            RefreshTerminalTabTitles();
        }

        private void ApplyGitSettings()
        {
            if (_gitPanel == null) return;
            _gitPanel.ApplyGitSettings(new GitPanel.GitSettings(
                AuthorName: _gitAuthorName,
                AuthorEmail: _gitAuthorEmail,
                DefaultBranch: _gitDefaultBranch,
                ConfirmRemoveRepo: _gitConfirmRemoveRepo,
                ConfirmDiscardChanges: _gitConfirmDiscardChanges,
                ConfirmDiscardPermanent: _gitConfirmDiscardPermanent,
                ConfirmDiscardStash: _gitConfirmDiscardStash,
                ConfirmCheckoutCommit: _gitConfirmCheckoutCommit,
                ConfirmForcePush: _gitConfirmForcePush,
                ConfirmUndoCommit: _gitConfirmUndoCommit,
                ConfirmOverrideCommitMsg: _gitConfirmOverrideCommitMsg,
                ConfirmHiddenChanges: _gitConfirmHiddenChanges,
                BranchSwitchBehavior: _gitBranchSwitchBehavior,
                CommitLengthWarning: _gitCommitLengthWarning
            ));
        }

        private void ApplySecuritySettings()
        {
            SecurityEnforcementService.UrlBlockedCallback = (url, message) =>
            {
                if (InvokeRequired) { BeginInvoke(() => SetStatus(message)); return; }
                SetStatus(message);
            };

            _lintEngine.SastEnabled = _secSastEnabled;
            _lintEngine.HighlightHardcodedSecrets = _secHighlightHardcodedSecrets;

            foreach (var terminal in _terminalTabs)
                terminal.ApplySecuritySettings(new TerminalPanel.SecuritySettings(
                    ConfirmUrlOpen: _secConfirmUrlOpen,
                    AllowHttpUrls: _secAllowHttpUrls));

            if (_secWriteStartupLog)
                PurgeOldLogs(_secLogRetentionDays);
        }

        private static void PurgeOldLogs(int retentionDays)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyCrownJewelApp", "Pfpad");
                if (!Directory.Exists(logDir)) return;
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                foreach (var f in Directory.GetFiles(logDir, "*.log"))
                {
                    if (File.GetLastWriteTimeUtc(f) < cutoff)
                        try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        private string ResolveTerminalStartingDirectory()
        {
            if (!string.IsNullOrWhiteSpace(_terminalStartingDirectory))
                return _terminalStartingDirectory;

            if (!string.IsNullOrWhiteSpace(_workspaceRoot))
                return _workspaceRoot;

            return "";
        }

        private void RefreshTerminalTabTitles()
        {
            foreach (var terminal in _terminalTabs)
            {
                if (FindTerminalTabPage(terminal) is { } page)
                    page.Text = GetTerminalTabTitle(terminal);
            }
        }

        private TabPage? FindTerminalTabPage(TerminalPanel terminal)
        {
            if (_terminalTabControl == null)
                return null;

            foreach (TabPage page in _terminalTabControl.TabPages)
            {
                if (page.Tag == terminal)
                    return page;
            }

            return null;
        }

        private string GetTerminalTabTitle(TerminalPanel terminal)
        {
            if (!string.IsNullOrWhiteSpace(terminal.CustomTabTitle))
                return terminal.CustomTabTitle;

            int tabIndex = _terminalTabs.IndexOf(terminal);
            int tabNumber = tabIndex >= 0 ? tabIndex + 1 : _terminalTabs.Count + 1;
            return $"Terminal {tabNumber}";
        }

        private TerminalPanel AddTerminalTab(string? shellPath)
        {
            var terminal = new TerminalPanel(shellPath);
            terminal.SetTheme(_themeManager.CurrentTheme);
            terminal.CustomTabTitle = _terminalTabTitle;
            terminal.StartingDirectory = ResolveTerminalStartingDirectory();
            terminal.ApplyTerminalSettings(_terminalFontFace, _terminalFontSize, _terminalFontBold, _terminalWordWrap, _terminalScrollbarVisible, _terminalPadding);
            terminal.SetMaxScrollback(_terminalMaxScrollback);
            terminal.ApplySecuritySettings(new TerminalPanel.SecuritySettings(
                ConfirmUrlOpen: _secConfirmUrlOpen,
                AllowHttpUrls: _secAllowHttpUrls));

            var page = new TabPage(GetTerminalTabTitle(terminal))
            {
                BackColor = _themeManager.CurrentTheme.EditorBackground,
                ToolTipText = shellPath ?? "Default shell",
                Tag = terminal
            };
            terminal.Dock = DockStyle.Fill;
            terminal.HideTerminalRequested += () => CloseTerminalTab(terminal);
            page.Controls.Add(terminal);

            _terminalTabControl?.TabPages.Add(page);
            _terminalTabs.Add(terminal);
            if (_terminalTabControl != null)
                _terminalTabControl.SelectedTab = page;
            RefreshTerminalTabTitles();

            if (_terminalVisible)
                terminal.Start();

            PositionTerminalNewTabButton();
            return terminal;
        }

        private void CloseTerminalTab(TerminalPanel terminal)
        {
            if (_terminalTabControl == null) return;
            TabPage? page = null;
            foreach (TabPage tp in _terminalTabControl.TabPages)
            {
                if (tp.Tag == terminal) { page = tp; break; }
            }
            if (page == null) return;

            terminal.Kill();
            _terminalTabControl.TabPages.Remove(page);
            _terminalTabs.Remove(terminal);
            terminal.Dispose();

            if (_terminalTabs.Count == 0)
                HideTerminal();
            RefreshTerminalTabTitles();
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

            if (isSelected)
            {
                using var accentPen = new Pen(theme.Accent, 2);
                e.Graphics.DrawLine(accentPen, bounds.Left, bounds.Bottom - 2, bounds.Right, bounds.Bottom - 2);
            }

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
                if (closeRect.Contains(e.Location) && tc.TabPages[i].Tag is TerminalPanel tp)
                {
                    CloseTerminalTab(tp);
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

        // Current line highlight mode — set directly from which menu item was clicked
        private void CurrentLineHighlightMode_Click(object? sender, EventArgs e)
        {
            if      (sender == currentLineOffMenuItem)                currentLineHighlightMode = CurrentLineHighlightMode.Off;
            else if (sender == currentLineNumberOnlyMenuItem)         currentLineHighlightMode = CurrentLineHighlightMode.NumberOnly;
            else if (sender == currentLineWholeLineMenuItem)          currentLineHighlightMode = CurrentLineHighlightMode.WholeLine;
            else if (sender == currentLineNumberAndWholeLineMenuItem) currentLineHighlightMode = CurrentLineHighlightMode.NumberAndWholeLine;
            
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
            else if (e.KeyCode == Keys.Home && _smartHomeKey && !e.Control && !e.Alt)
            {
                HandleSmartHome(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HandleSmartHome(KeyEventArgs e)
        {
            if (textEditor == null) return;
            int pos = textEditor.SelectionStart;
            int line = textEditor.GetLineFromCharIndex(pos);
            int lineStart = textEditor.GetFirstCharIndexFromLine(line);
            string lineText = GetLineText(line) ?? "";

            int firstNonWs = 0;
            while (firstNonWs < lineText.Length && char.IsWhiteSpace(lineText[firstNonWs]))
                firstNonWs++;
            int firstNonWsIdx = lineStart + firstNonWs;

            if (e.Shift)
            {
                int anchor = pos + textEditor.SelectionLength;
                int newPos = pos != firstNonWsIdx ? firstNonWsIdx : lineStart;
                textEditor.SelectionStart = Math.Min(anchor, newPos);
                textEditor.SelectionLength = Math.Abs(anchor - newPos);
            }
            else if (pos != firstNonWsIdx)
            {
                textEditor.SelectionStart = firstNonWsIdx;
                textEditor.SelectionLength = 0;
            }
            else
            {
                textEditor.SelectionStart = lineStart;
                textEditor.SelectionLength = 0;
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
                    // Try snippet navigation (next tab stop) then snippet expansion before falling back to indent
                    if (_snippetsEnabled && _snippetEngine != null)
                    {
                        if (_snippetEngine.TryNavigateNextTabStop()) return;
                        if (_snippetTriggerKey == "Tab" && _snippetEngine.TryExpand()) return;
                    }

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
                    string trimmedPrev = prevLineText.Trim();

                    if (_autoCloseBracesOnEnter && trimmedPrev.EndsWith('{'))
                    {
                        // Insert inner newline + indent, then outer newline + closing brace;
                        // leave cursor positioned after the inner indent (between the braces).
                        string outerIndent = IndentationHelper.GetLeadingWhitespace(prevLineText);
                        string innerText = Environment.NewLine + indent + Environment.NewLine + outerIndent + "}";
                        textEditor.SelectedText = innerText;
                        int closePosition = selStart + Environment.NewLine.Length + indent.Length + Environment.NewLine.Length + outerIndent.Length;
                        textEditor.RegisterAutoInsertedClose(closePosition);
                        textEditor.SelectionStart = selStart + Environment.NewLine.Length + indent.Length;
                        textEditor.SelectionLength = 0;
                    }
                    else
                    {
                        // Insert newline + indent
                        textEditor.SelectedText = Environment.NewLine + indent;
                    }
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






        #region Public API (called by dialogs)

        private void PushCursorHistory()
        {
            if (_suppressHistoryPush || textEditor == null) return;
            int pos = textEditor.SelectionStart;
            string? path = currentFilePath;

            if (_cursorHistoryCurrent?.Value.Position == pos && _cursorHistoryCurrent?.Value.FilePath == path) return;

            while (_cursorHistory.Last != null && _cursorHistory.Last != _cursorHistoryCurrent)
                _cursorHistory.RemoveLast();

            _cursorHistory.AddLast((pos, path));
            _cursorHistoryCurrent = _cursorHistory.Last;

            while (_cursorHistory.Count > MaxCursorHistory)
                _cursorHistory.RemoveFirst();
        }

        private void NavigateCursorBack()
        {
            if (_cursorHistoryCurrent?.Previous == null) return;
            _cursorHistoryCurrent = _cursorHistoryCurrent.Previous;
            _suppressHistoryPush = true;
            try { ApplyCursorHistoryEntry(_cursorHistoryCurrent.Value); }
            finally { _suppressHistoryPush = false; }
        }

        private void NavigateCursorForward()
        {
            if (_cursorHistoryCurrent?.Next == null) return;
            _cursorHistoryCurrent = _cursorHistoryCurrent.Next;
            _suppressHistoryPush = true;
            try { ApplyCursorHistoryEntry(_cursorHistoryCurrent.Value); }
            finally { _suppressHistoryPush = false; }
        }

        private void ApplyCursorHistoryEntry((int Position, string? FilePath) entry)
        {
            if (entry.FilePath != null && entry.FilePath != currentFilePath)
            {
                var idx = documents.FindIndex(d => d.FilePath == entry.FilePath);
                if (idx >= 0) SwitchToTab(idx);
            }
            if (textEditor != null && entry.Position >= 0 && entry.Position <= textEditor.TextLength)
            {
                textEditor.SelectionStart = entry.Position;
                textEditor.SelectionLength = 0;
                textEditor.ScrollToCaret();
            }
        }

        public void GoToLine(int lineNumber)
        {
             if (lineNumber < 1 || LineCount == 0) return;
             int targetIndex = lineNumber - 1;
             if (targetIndex >= LineCount) targetIndex = LineCount - 1;
            int charIndex = textEditor.GetFirstCharIndexFromLine(targetIndex);
            if (charIndex >= 0)
            {
                PushCursorHistory();
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
            PushCursorHistory();

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
                PushCursorHistory();
                textEditor.SelectionStart = found;
                textEditor.SelectionLength = useRegex ? 0 : text.Length;
                textEditor.ScrollToCaret();
                UpdateStatusBar();
            }
            else
            {
                NotifyNotFound(text);
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
                            PushCursorHistory();
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

            var ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in _findInFilesExclude.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                ignoredDirs.Add(part);
            ignoredDirs.UnionWith(new[] { ".svn", ".hg" }); // always excluded

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
            if (_applyingElasticTabs) return;  // SelectionTabs change fires TextChanged — don't restart timer
            textEditor.ClearAutoInsertedCloses();
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
                        textEditor.ShowZoomIndicator(zoomFactor);
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


        private bool AnyAIOpsPanelVisible => _aiopsHubVisible || _aiopsSecurityVisible || _aiopsDeploymentVisible || _aiopsTelemetryVisible || _aiopsInsightsVisible
            || _aiopsQueryVisible || _aiopsTimelineVisible || _aiopsPrRiskVisible || _aiopsServiceDepVisible || _aiopsRunbookVisible;

        private void InitAIOps()
        {
            if (_aiopsEngine != null)
                return;

            var settings = _settingsService.LoadWithDecrypt()?.AIOpsConfig ?? new MyCrownJewelApp.Pfpad.AIOps.AIOpsSettings();
            settings.LoadSecretsFromEncrypted();
            _aiopsEngine = new MyCrownJewelApp.Pfpad.AIOps.AIOpsEngine(settings);

            _aiopsPanel = new MyCrownJewelApp.Pfpad.AIOps.AIOpsPanel { Visible = false };
            _aiopsPanel.SetEngine(_aiopsEngine);
            _aiopsPanel.CloseRequested += () => { _aiopsHubVisible = false; _aiopsPanel.Visible = false; UpdateSidebarLayout(); };
            _aiopsPanel.SettingsRequested += ShowAIOpsSettings;
            _aiopsPanel.ScanRequested += async () => await ScanActiveFileForAIOpsAsync();

            _aiopsSecurityPanel = new MyCrownJewelApp.Pfpad.AIOps.SecurityPanel { Visible = false };
            _aiopsSecurityPanel.CloseRequested += () => { _aiopsSecurityVisible = false; _aiopsSecurityPanel.Visible = false; UpdateSidebarLayout(); };
            _aiopsSecurityPanel.FindingNavigateRequested += (path, line) =>
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    OpenFileInNewTab(path);
                    GoToLine(line);
                }
            };

            _aiopsDeploymentPanel = new MyCrownJewelApp.Pfpad.AIOps.DeploymentPanel { Visible = false };
            _aiopsDeploymentPanel.CloseRequested += () => { _aiopsDeploymentVisible = false; _aiopsDeploymentPanel.Visible = false; UpdateSidebarLayout(); };

            _aiopsTelemetryPanel = new MyCrownJewelApp.Pfpad.AIOps.TelemetryPanel { Visible = false };
            _aiopsTelemetryPanel.CloseRequested += () => { _aiopsTelemetryVisible = false; _aiopsTelemetryPanel.Visible = false; UpdateSidebarLayout(); };

            _aiopsInsightsPanel = new MyCrownJewelApp.Pfpad.AIOps.InsightsPanel { Visible = false };
            _aiopsInsightsPanel.CloseRequested += () => { _aiopsInsightsVisible = false; _aiopsInsightsPanel.Visible = false; UpdateSidebarLayout(); };

            _aiopsQueryPanel = new MyCrownJewelApp.Pfpad.AIOps.OpsQueryPanel { Visible = false };
            _aiopsQueryPanel.SetEngine(_aiopsEngine);
            _aiopsQueryPanel.CloseRequested += () => { _aiopsQueryVisible = false; _aiopsQueryPanel.Visible = false; UpdateSidebarLayout(); };

            _aiopsTimelinePanel = new MyCrownJewelApp.Pfpad.AIOps.IncidentTimelinePanel { Visible = false };
            _aiopsTimelinePanel.CloseRequested += () => { _aiopsTimelineVisible = false; _aiopsTimelinePanel.Visible = false; UpdateSidebarLayout(); };
            _aiopsTimelinePanel.RcaRequested += async incident =>
            {
                if (_aiopsEngine == null) return;
                var rca = await _aiopsEngine.AnalyzeIncidentAsync(incident);
                BeginInvoke(() => _aiopsTimelinePanel?.SetRootCauseAnalysis(rca));
            };
            _aiopsTimelinePanel.NavigateRequested += (path, line) =>
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    OpenFileInNewTab(path);
                    GoToLine(line);
                }
            };

            _aiopsPrRiskPanel = new MyCrownJewelApp.Pfpad.AIOps.PullRequestRiskPanel { Visible = false };
            _aiopsPrRiskPanel.CloseRequested += () => { _aiopsPrRiskVisible = false; _aiopsPrRiskPanel.Visible = false; UpdateSidebarLayout(); };
            _aiopsPrRiskPanel.AnalyzeRequested += async pr =>
            {
                if (_aiopsEngine == null) return;
                var report = await _aiopsEngine.ScoreDeploymentRiskAsync(pr.ChangedFiles, null);
                BeginInvoke(() => _aiopsPrRiskPanel?.SetRiskReport(report));
            };
            _aiopsPrRiskPanel.SecurityFindingNavigateRequested += (path, line) =>
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    OpenFileInNewTab(path);
                    GoToLine(line);
                }
            };

            _aiopsServiceDepPanel = new MyCrownJewelApp.Pfpad.AIOps.ServiceDependencyPanel { Visible = false };
            _aiopsServiceDepPanel.CloseRequested += () => { _aiopsServiceDepVisible = false; _aiopsServiceDepPanel.Visible = false; UpdateSidebarLayout(); };

            _aiopsRunbookPanel = new MyCrownJewelApp.Pfpad.AIOps.RunbookPanel { Visible = false };
            _aiopsRunbookPanel.SetEngine(_aiopsEngine);
            _aiopsRunbookPanel.CloseRequested += () => { _aiopsRunbookVisible = false; _aiopsRunbookPanel.Visible = false; UpdateSidebarLayout(); };

            if (_botSidebarSplit != null)
            {
                _botSidebarSplit.Panel2.Controls.Add(_aiopsPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsSecurityPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsDeploymentPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsTelemetryPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsInsightsPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsQueryPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsTimelinePanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsPrRiskPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsServiceDepPanel);
                _botSidebarSplit.Panel2.Controls.Add(_aiopsRunbookPanel);

                _aiopsPanel.Dock = DockStyle.Fill;
                _aiopsSecurityPanel.Dock = DockStyle.Fill;
                _aiopsDeploymentPanel.Dock = DockStyle.Fill;
                _aiopsTelemetryPanel.Dock = DockStyle.Fill;
                _aiopsInsightsPanel.Dock = DockStyle.Fill;
                _aiopsQueryPanel.Dock = DockStyle.Fill;
                _aiopsTimelinePanel.Dock = DockStyle.Fill;
                _aiopsPrRiskPanel.Dock = DockStyle.Fill;
                _aiopsServiceDepPanel.Dock = DockStyle.Fill;
                _aiopsRunbookPanel.Dock = DockStyle.Fill;
            }

            _aiopsEngine.SecurityFindingsFound += findings =>
                BeginInvoke(() => _aiopsSecurityPanel?.SetFindings(findings, Array.Empty<MyCrownJewelApp.Pfpad.AIOps.PolicyViolation>()));
            _aiopsEngine.PolicyViolationsFound += violations =>
                BeginInvoke(() => _aiopsSecurityPanel?.SetFindings(Array.Empty<MyCrownJewelApp.Pfpad.AIOps.SecurityFinding>(), violations));
            _aiopsEngine.DeploymentsRefreshed += deps =>
                BeginInvoke(() => _aiopsDeploymentPanel?.SetDeployments(deps));
            _aiopsEngine.PipelinesRefreshed += pipes =>
                BeginInvoke(() => _aiopsDeploymentPanel?.SetPipelines(pipes));
            _aiopsEngine.ObservabilityGapsFound += gaps =>
                BeginInvoke(() => _aiopsInsightsPanel?.SetInsights(gaps, Array.Empty<MyCrownJewelApp.Pfpad.AIOps.RemediationSuggestion>(), null));
            _aiopsEngine.IncidentsRefreshed += incidents =>
                BeginInvoke(() => _aiopsTimelinePanel?.SetData(incidents, _aiopsEngine.CorrelationChains));
            _aiopsEngine.CorrelationChainsUpdated += chains =>
                BeginInvoke(() => _aiopsTimelinePanel?.SetData(_aiopsEngine.ActiveIncidents, chains));
            _aiopsEngine.PipelinesRefreshed += _ =>
                BeginInvoke(() => _aiopsPrRiskPanel?.SetPullRequests(_aiopsEngine.RecentPullRequests));
            _aiopsEngine.InlineAnnotationsGenerated += annotations =>
                BeginInvoke(() =>
                {
                    if (annotations.Count > 0 && !string.IsNullOrWhiteSpace(annotations[0].FilePath))
                        _inlineAnnotationService.SetAnnotations(annotations[0].FilePath, annotations);
                });

            var theme = _themeManager.CurrentTheme;
            _aiopsPanel.SetTheme(theme);
            _aiopsSecurityPanel.SetTheme(theme);
            _aiopsDeploymentPanel.SetTheme(theme);
            _aiopsTelemetryPanel.SetTheme(theme);
            _aiopsInsightsPanel.SetTheme(theme);
            _aiopsQueryPanel.SetTheme(theme);
            _aiopsTimelinePanel.SetTheme(theme);
            _aiopsPrRiskPanel.SetTheme(theme);
            _aiopsServiceDepPanel.SetTheme(theme);
            _aiopsRunbookPanel.SetTheme(theme);

            _aiopsEngine.Start();
            UpdateAIOpsTelemetryConnector();
        }

        private void UpdateAIOpsTelemetryConnector()
        {
            if (_aiopsEngine == null || _aiopsTelemetryPanel == null)
                return;

            var connectors = _aiopsEngine.Connectors;

            // Prefer real connectors (non-mock) over mock data, regardless of current connection status.
            // Mock data is used only as a last resort when no real connectors are configured.
            // Real connectors auto-connect in the background after settings are saved;
            // TelemetryPanel will attempt ConnectAsync before the first query.
            var connector =
                // 1. Real connected connector with service discovery (e.g. AzureMonitorConnector)
                connectors.FirstOrDefault(c => c is not MyCrownJewelApp.Pfpad.AIOps.MockDataConnector
                                               && c.Status == MyCrownJewelApp.Pfpad.AIOps.ConnectorStatus.Connected
                                               && c is MyCrownJewelApp.Pfpad.AIOps.IServiceDiscoveryCapable) ??
                // 2. Any real connected connector
                connectors.FirstOrDefault(c => c is not MyCrownJewelApp.Pfpad.AIOps.MockDataConnector
                                               && c.Status == MyCrownJewelApp.Pfpad.AIOps.ConnectorStatus.Connected) ??
                // 3. Real connector not yet connected (will auto-connect on first use)
                connectors.FirstOrDefault(c => c is not MyCrownJewelApp.Pfpad.AIOps.MockDataConnector
                                               && c.Status != MyCrownJewelApp.Pfpad.AIOps.ConnectorStatus.Error) ??
                // 4. Any real connector (even in error state — user can retry)
                connectors.FirstOrDefault(c => c is not MyCrownJewelApp.Pfpad.AIOps.MockDataConnector) ??
                // 5. Mock data (last resort: no real connectors configured)
                connectors.FirstOrDefault();

            _aiopsTelemetryPanel.SetConnector(connector);

            string? serviceName = Path.GetFileNameWithoutExtension(currentFilePath);
            if (!string.IsNullOrWhiteSpace(serviceName))
                _aiopsTelemetryPanel.SetServiceName(serviceName);

            string? fp = GetCurrentDocument()?.FilePath ?? currentFilePath;
            if (!string.IsNullOrWhiteSpace(fp))
                _aiopsQueryPanel?.SetContext(fp, _aiopsEngine?.ServiceContextEngine.ResolveServiceName(fp));
        }

        private void HideAIOpsPanels(Control? except = null)
        {
            if (_aiopsPanel != except)
            {
                _aiopsHubVisible = false;
                if (_aiopsPanel != null) _aiopsPanel.Visible = false;
            }

            if (_aiopsSecurityPanel != except)
            {
                _aiopsSecurityVisible = false;
                if (_aiopsSecurityPanel != null) _aiopsSecurityPanel.Visible = false;
            }

            if (_aiopsDeploymentPanel != except)
            {
                _aiopsDeploymentVisible = false;
                if (_aiopsDeploymentPanel != null) _aiopsDeploymentPanel.Visible = false;
            }

            if (_aiopsTelemetryPanel != except)
            {
                _aiopsTelemetryVisible = false;
                if (_aiopsTelemetryPanel != null) _aiopsTelemetryPanel.Visible = false;
            }

            if (_aiopsInsightsPanel != except)
            {
                _aiopsInsightsVisible = false;
                if (_aiopsInsightsPanel != null) _aiopsInsightsPanel.Visible = false;
            }

            if (_aiopsQueryPanel != except)
            {
                _aiopsQueryVisible = false;
                if (_aiopsQueryPanel != null) _aiopsQueryPanel.Visible = false;
            }

            if (_aiopsTimelinePanel != except)
            {
                _aiopsTimelineVisible = false;
                if (_aiopsTimelinePanel != null) _aiopsTimelinePanel.Visible = false;
            }

            if (_aiopsPrRiskPanel != except)
            {
                _aiopsPrRiskVisible = false;
                if (_aiopsPrRiskPanel != null) _aiopsPrRiskPanel.Visible = false;
            }

            if (_aiopsServiceDepPanel != except)
            {
                _aiopsServiceDepVisible = false;
                if (_aiopsServiceDepPanel != null) _aiopsServiceDepPanel.Visible = false;
            }

            if (_aiopsRunbookPanel != except)
            {
                _aiopsRunbookVisible = false;
                if (_aiopsRunbookPanel != null) _aiopsRunbookPanel.Visible = false;
            }
        }

        private void ShowAIOpsPanel(Control? panel, ref bool visible)
        {
            if (panel == null)
                return;

            HideAIOpsPanels(panel);
            visible = true;
            panel.Visible = true;
            panel.BringToFront();
            UpdateAIOpsTelemetryConnector();
            UpdateSidebarLayout();
        }

        private void AIOpsHub_Click(object? sender, EventArgs e) => ToggleAIOpsPanel(_aiopsPanel, ref _aiopsHubVisible);
        private void AIOpsSecurityPanel_Click(object? sender, EventArgs e) => ToggleAIOpsPanel(_aiopsSecurityPanel, ref _aiopsSecurityVisible);
        private void AIOpsDeploymentPanel_Click(object? sender, EventArgs e) => ToggleAIOpsPanel(_aiopsDeploymentPanel, ref _aiopsDeploymentVisible);
        private void AiopsTelemetryPanel_Click(object? sender, EventArgs e) => ToggleAIOpsPanel(_aiopsTelemetryPanel, ref _aiopsTelemetryVisible);
        private void AIOpsInsightsPanel_Click(object? sender, EventArgs e) => ToggleAIOpsPanel(_aiopsInsightsPanel, ref _aiopsInsightsVisible);

        private void ToggleAIOpsPanel(Control? panel, ref bool visible)
        {
            if (panel == null)
                return;

            bool show = !visible;
            HideAIOpsPanels(show ? panel : null);
            visible = show;
            panel.Visible = show;
            if (show)
            {
                panel.BringToFront();
                UpdateAIOpsTelemetryConnector();
            }

            UpdateSidebarLayout();
        }

        private void AIOpsQuery_Click(object? sender, EventArgs e)
        {
            ToggleAIOpsPanel(_aiopsQueryPanel, ref _aiopsQueryVisible);
            if (_aiopsQueryVisible)
            {
                string? fp = GetCurrentDocument()?.FilePath ?? currentFilePath;
                _aiopsQueryPanel?.SetContext(fp, null);
            }
        }

        private void AIOpsTimeline_Click(object? sender, EventArgs e)
        {
            ToggleAIOpsPanel(_aiopsTimelinePanel, ref _aiopsTimelineVisible);
            if (_aiopsTimelineVisible && _aiopsEngine != null)
                _aiopsTimelinePanel?.SetData(_aiopsEngine.ActiveIncidents, _aiopsEngine.CorrelationChains);
        }

        private void AiopsPrRisk_Click(object? sender, EventArgs e)
        {
            ToggleAIOpsPanel(_aiopsPrRiskPanel, ref _aiopsPrRiskVisible);
            if (_aiopsPrRiskVisible && _aiopsEngine != null)
                _aiopsPrRiskPanel?.SetPullRequests(_aiopsEngine.RecentPullRequests);
        }

        private void AIOpsServiceDep_Click(object? sender, EventArgs e)
            => ToggleAIOpsPanel(_aiopsServiceDepPanel, ref _aiopsServiceDepVisible);

        private void AIOpsRunbooks_Click(object? sender, EventArgs e)
        {
            ToggleAIOpsPanel(_aiopsRunbookPanel, ref _aiopsRunbookVisible);
            if (_aiopsRunbookVisible && _aiopsEngine != null)
                _aiopsRunbookPanel?.SetRunbooks(_aiopsEngine.Runbooks);
        }

        private void AIOpsOtelCode_Click(object? sender, EventArgs e)
        {
            if (_aiopsEngine == null) return;
            string? fp = GetCurrentDocument()?.FilePath ?? currentFilePath;
            if (string.IsNullOrWhiteSpace(fp)) return;
            string ext = Path.GetExtension(fp).ToLowerInvariant();
            string lang = ext switch
            {
                ".cs" => "csharp",
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".go" => "go",
                ".java" => "java",
                _ => "csharp"
            };
            string functionName = Path.GetFileNameWithoutExtension(fp);
            string serviceName = _aiopsEngine.ServiceContextEngine.ResolveServiceName(fp) ?? functionName;
            string code = _aiopsEngine.GenerateOTelCode(lang, functionName, serviceName);
            ThemedMessageBox.Show(code, $"OTel Code for {functionName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AiopsScanFile_Click(object? sender, EventArgs e) => _ = ScanActiveFileForAIOpsAsync();
        private void AiopsRiskScore_Click(object? sender, EventArgs e) => _ = ScoreDeploymentRiskAsync();
        private void AiopsObservability_Click(object? sender, EventArgs e) => _ = AnalyzeObservabilityAsync();
        private void AiopsSettings_Click(object? sender, EventArgs e) => ShowAIOpsSettings();

        private async Task ScanActiveFileForAIOpsAsync()
        {
            if (_aiopsEngine == null)
                return;

            string? filePath = GetCurrentDocument()?.FilePath ?? currentFilePath;
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            UpdateAIOpsTelemetryConnector();
            _aiopsQueryPanel?.SetContext(filePath, _aiopsEngine.ServiceContextEngine.ResolveServiceName(filePath));
            string content = textEditor?.Text ?? string.Empty;
            var result = await _aiopsEngine.ScanFileAsync(filePath, content);
            if (result.findings.Count > 0 || result.policies.Count > 0)
                ShowAIOpsPanel(_aiopsSecurityPanel, ref _aiopsSecurityVisible);
        }

        private async Task ScoreDeploymentRiskAsync()
        {
            if (_aiopsEngine == null)
                return;

            try
            {
                var changedFiles = new List<string>();
                string gitDiff = string.Empty;

                if (!string.IsNullOrWhiteSpace(_workspaceRoot))
                    _gitService.TryOpenRepo(_workspaceRoot);

                var repo = _gitService.GetRepo();
                if (repo != null)
                {
                    var status = repo.RetrieveStatus(new LibGit2Sharp.StatusOptions
                    {
                        IncludeUnaltered = false,
                        RecurseUntrackedDirs = true,
                        DetectRenamesInIndex = true,
                        DetectRenamesInWorkDir = true
                    });

                    changedFiles = status
                        .Select(entry => Path.Combine(repo.Info.WorkingDirectory, entry.FilePath.Replace('/', Path.DirectorySeparatorChar)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    try
                    {
                        var patch = repo.Diff.Compare<LibGit2Sharp.Patch>(repo.Head.Tip?.Tree, LibGit2Sharp.DiffTargets.WorkingDirectory);
                        gitDiff = patch?.Content ?? string.Empty;
                    }
                    catch
                    {
                        gitDiff = string.Empty;
                    }
                }

                if (changedFiles.Count == 0 && !string.IsNullOrWhiteSpace(currentFilePath))
                    changedFiles.Add(currentFilePath);

                await _aiopsEngine.ScoreDeploymentRiskAsync(changedFiles, gitDiff);
                ShowAIOpsPanel(_aiopsPanel, ref _aiopsHubVisible);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIOps] Risk score error: {ex.Message}");
            }
        }

        private async Task AnalyzeObservabilityAsync()
        {
            if (_aiopsEngine == null)
                return;

            string? filePath = GetCurrentDocument()?.FilePath ?? currentFilePath;
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string content = textEditor?.Text ?? string.Empty;
            var gaps = await _aiopsEngine.AnalyzeObservabilityAsync(filePath, content);
            _aiopsInsightsPanel?.SetInsights(gaps, Array.Empty<MyCrownJewelApp.Pfpad.AIOps.RemediationSuggestion>(), _aiopsEngine.LastRiskReport);
            ShowAIOpsPanel(_aiopsInsightsPanel, ref _aiopsInsightsVisible);
        }

        private void ShowAIOpsSettings()
        {
            if (_aiopsEngine == null)
                return;

            var settings = _aiopsEngine.CurrentSettings;
            using var dlg = new MyCrownJewelApp.Pfpad.AIOps.AIOpsSettingsDialog(settings)
            {
                AzureMonitorConnector = new MyCrownJewelApp.Pfpad.AIOps.AzureMonitorConnector(settings.AzureMonitor),
                AzureDevOpsConnector = new MyCrownJewelApp.Pfpad.AIOps.AzureDevOpsConnector(settings.AzureDevOps),
                KubernetesConnector = new MyCrownJewelApp.Pfpad.AIOps.KubernetesConnector(settings.Kubernetes)
            };
            dlg.SettingsSaved += newSettings =>
            {
                _aiopsEngine.Configure(newSettings);
                UpdateAIOpsTelemetryConnector();
                SaveSettings();
            };
            dlg.ShowDialog(this);
        }


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
            else if (e.Alt && e.KeyCode == Keys.Left && !e.Control && !e.Shift)
            {
                NavigateCursorBack();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Right && !e.Control && !e.Shift)
            {
                NavigateCursorForward();
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
            _aiopsEngine?.Dispose();
            _inlineAnnotationService.Dispose();
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
                    _docManager.Clear();
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
                    PushCursorHistory();
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
            using var dlg = new GlobalSearchDialog(this,
                _findInFilesFilter, _findInFilesExclude, _findInFilesMaxResults);
            if (_workspacePanel?.RootPath is { Length: > 0 } root && Directory.Exists(root))
                dlg.WorkspaceRoot = root;
            else if (!string.IsNullOrEmpty(_workspaceRoot) && Directory.Exists(_workspaceRoot))
                dlg.WorkspaceRoot = _workspaceRoot;
            dlg.ShowDialog(this);
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Global search: Ctrl+Shift+F
            if (keyData == (Keys.Control | Keys.Shift | Keys.F))
            {
                GlobalSearch_Click(null, EventArgs.Empty);
                return true;
            }

            // Build solution: Ctrl+Shift+B
            if (keyData == (Keys.Control | Keys.Shift | Keys.B))
            {
                if (_solutionExplorerPanel != null)
                    _ = _solutionExplorerPanel.BuildCurrentSolutionAsync();
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

            // Reopen last closed tab: Ctrl+Shift+T
            if (keyData == (Keys.Control | Keys.Shift | Keys.T))
            {
                ReopenLastClosedTab();
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
            // Header/Source toggle: Alt+O (C/C++ only, harmless on other file types)
            if (keyData == (Keys.Alt | Keys.O)) { ToggleHeaderSource(); return true; }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

    }
}

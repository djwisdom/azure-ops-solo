using System.Collections.Generic;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Persisted user preferences for Pfpad. Pure data — no WinForms dependency.
/// </summary>
public record AppSettings
{
    public bool WordWrapEnabled { get; init; } = false;
    public bool GutterVisible { get; init; } = true;
    public bool StatusBarVisible { get; init; } = true;
    public bool ShowGuide { get; init; } = false;
    public int GuideColumn { get; init; } = 80;
    public int TabSize { get; init; } = 4;
    public string FontName { get; init; } = "Consolas";
    public float FontSize { get; init; } = 10f;
    public bool InsertSpaces { get; init; } = true;
    public bool AutoIndentEnabled { get; init; } = true;
    public bool SmartTabsEnabled { get; init; } = true;
    public bool ElasticTabsEnabled { get; init; } = true;
    public CurrentLineHighlightMode CurrentLineHighlightMode { get; init; } = MyCrownJewelApp.Pfpad.CurrentLineHighlightMode.Off;
    public bool SyntaxHighlightingEnabled { get; init; } = true;
    public bool MinimapVisible { get; init; } = false;
    public bool TerminalVisible { get; init; } = false;
    public int TerminalHeight { get; init; } = 200;
    public string TerminalShellPath { get; init; } = "";
    public string TerminalStartingDirectory { get; init; } = "";
    public string TerminalFontFace { get; init; } = "";
    public float TerminalFontSize { get; init; } = 0f;
    public bool TerminalFontBold { get; init; } = false;
    public bool TerminalWordWrap { get; init; } = true;
    public bool TerminalScrollbarVisible { get; init; } = true;
    public int TerminalPadding { get; init; } = 4;
    public int TerminalMaxScrollback { get; init; } = 5000;
    public string TerminalTabTitle { get; init; } = "";
    public string ThemeName { get; init; } = "Dark";
    public List<ExternalTool>? ExternalTools { get; init; } = null;
    public bool WorkspaceVisible { get; init; } = false;
    public int WorkspaceWidth { get; init; } = 200;
    public string WorkspaceRoot { get; init; } = "";
    public bool ShowWhitespace { get; init; } = true;
    public bool SymbolPanelVisible { get; init; } = false;
    public bool ProblemsPanelVisible { get; init; } = false;
    public bool RainbowBracketsEnabled { get; init; } = false;
    public bool BreadcrumbsEnabled { get; init; } = false;
    public bool AnalyzersEnabled { get; init; } = true;
    public bool AutoSaveEnabled { get; init; } = false;
    public bool HoverLineHighlightEnabled { get; init; } = false;
    public List<string>? RecentWorkspaces { get; init; } = null;
    public string WindowBounds { get; init; } = "";
    public string WindowState { get; init; } = "";
    public string ActiveConfiguration { get; init; } = "Debug";
    public int MaxFileSizeMB { get; init; } = 500;
    public int LargeFileWarningMB { get; init; } = 50;
    public int AsyncFileWarningMB { get; init; } = 20;
    public bool DisableSyntaxHighlightingForLargeFiles { get; init; } = true;
    public bool DisableMinimapForLargeFiles { get; init; } = true;
    public bool DisableWordWrapForLargeFiles { get; init; } = false;
    public long SyntaxHighlightingThresholdBytes { get; init; } = 200 * 1024;
    public bool VimModeEnabled { get; init; } = false;
    public bool StickyScrollEnabled { get; init; } = true;
    public bool RulerEnabled { get; init; } = true;
    public bool ShowGutterBreakpoints { get; init; } = true;
    public bool ShowGutterBookmarks { get; init; } = true;

    // ── Text Editor on-save and interaction configuration ─────────────────────
    /// <summary>Remove trailing spaces and tabs from every line when the file is saved.</summary>
    public bool TrimTrailingWhitespace { get; init; } = false;
    /// <summary>Append a newline at the end of the file on save if not already present.</summary>
    public bool InsertFinalNewline { get; init; } = false;
    /// <summary>Line ending style when saving: "CRLF", "LF", "CR", or "auto" (preserve as-is).</summary>
    public string DefaultLineEnding { get; init; } = "auto";
    /// <summary>Allow Ctrl+MouseWheel to zoom the editor font size.</summary>
    public bool CtrlWheelZoom { get; init; } = true;
    /// <summary>Lines to scroll per mouse wheel tick (1–20). 0 = Windows system default.</summary>
    public int MouseWheelScrollLines { get; init; } = 0;
    /// <summary>Seconds between auto-save writes when Auto Save is enabled (10–300).</summary>
    public int AutoSaveIntervalSeconds { get; init; } = 30;

    // ── Workbench Appearance configuration ────────────────────────────────────
    /// <summary>Show line numbers in the gutter.</summary>
    public bool ShowLineNumbers { get; init; } = true;
    /// <summary>Show relative line numbers (distance from current line) instead of absolute numbers.</summary>
    public bool RelativeLineNumbers { get; init; } = false;
    /// <summary>Width of the minimap in pixels (60–200).</summary>
    public int MinimapWidth { get; init; } = 100;
    /// <summary>Show git branch / dirty / sync indicators in the status bar.</summary>
    public bool ShowGitStatusBar { get; init; } = true;
    /// <summary>Show the Roslyn workspace indicator in the status bar.</summary>
    public bool ShowRoslynStatusBar { get; init; } = true;

    // ── Window configuration ───────────────────────────────────────────────────
    /// <summary>Re-apply saved window position and size on next launch.</summary>
    public bool WinRememberBounds { get; init; } = true;
    /// <summary>Always start maximized (takes priority over remembered bounds).</summary>
    public bool WinLaunchMaximized { get; init; } = false;
    /// <summary>Restore the previous editing session (open files, cursors) on launch.</summary>
    public bool WinRestoreSession { get; init; } = true;
    /// <summary>"auto" follows the theme; "on" forces dark; "off" forces light title bar.</summary>
    public string WinDarkTitleBar { get; init; } = "auto";
    /// <summary>Main window opacity 50–100 %. 100 = fully opaque.</summary>
    public int WinOpacity { get; init; } = 100;

    // ── Workspace configuration ────────────────────────────────────────────────
    /// <summary>Show all files in the workspace tree by default (not just recognized text types).</summary>
    public bool WsShowAllFiles { get; init; } = false;
    /// <summary>Comma-separated directory names to hide in addition to the built-in exclusion list.</summary>
    public string WsExcludedDirs { get; init; } = "";
    /// <summary>Workspace tree row height in pixels (16–40). Default 26.</summary>
    public int WsTreeItemHeight { get; init; } = 26;
    /// <summary>Workspace tree indent per level in pixels (8–48). Default 24.</summary>
    public int WsTreeIndent { get; init; } = 24;
    /// <summary>Collapse sibling folders when a file is opened.</summary>
    public bool WsAutoCollapse { get; init; } = false;
    /// <summary>Milliseconds to wait after a file-system change before refreshing the tree.</summary>
    public int WsWatcherDebounceMs { get; init; } = 500;
    /// <summary>Disable the FileSystemWatcher and fall back to manual refresh only.</summary>
    public bool WsDisableFileWatcher { get; init; } = false;
    /// <summary>Maximum number of recent workspaces to remember (5–50).</summary>
    public int WsMaxRecentWorkspaces { get; init; } = 10;
    /// <summary>Maximum number of recent files to remember (5–50).</summary>
    public int WsMaxRecentFiles { get; init; } = 10;
    /// <summary>Auto-reveal and highlight the active editor file in the workspace tree when switching tabs.</summary>
    public bool WsFollowActiveFile { get; init; } = true;
    /// <summary>Sort folders above files at each level of the workspace tree.</summary>
    public bool WsSortFoldersFirst { get; init; } = true;
    /// <summary>Open a file in the editor with a single click instead of requiring a double-click.</summary>
    public bool WsSingleClickOpen { get; init; } = false;
    /// <summary>Show a confirmation dialog before permanently deleting a file or folder from the workspace tree.</summary>
    public bool WsConfirmDelete { get; init; } = true;
    /// <summary>Show git-status color decorations (Modified/Added/Deleted/Untracked) on tree nodes.</summary>
    public bool WsShowGitStatus { get; init; } = true;
    /// <summary>Show git-ignored files in the tree greyed out instead of hiding them entirely.</summary>
    public bool WsShowIgnoredFiles { get; init; } = false;
    /// <summary>Collapse single-child folder paths into one node (e.g. src/components shown as one level).</summary>
    public bool WsCompactFolders { get; init; } = true;
    /// <summary>Show child file/folder count badge on folder nodes.</summary>
    public bool WsShowFileCount { get; init; } = false;

    // ── Browser Panel configuration ────────────────────────────────────────────
    /// <summary>URL loaded when the Browser tab is opened. Defaults to about:blank.</summary>
    public string BrowserHomepage { get; init; } = "about:blank";
    /// <summary>Use an in-memory (ephemeral) profile — no cookies, cache, or storage persist across sessions.</summary>
    public bool BrowserEphemeral { get; init; } = false;
    /// <summary>Maximum number of visited URLs kept in the in-panel history list (10–5000).</summary>
    public int BrowserMaxHistory { get; init; } = 200;
    /// <summary>Allow navigation to localhost, 127.0.0.1, and [::1] addresses.</summary>
    public bool BrowserAllowLocalhost { get; init; } = true;
    /// <summary>Default zoom level for all pages (0.25–5.0; 1.0 = 100 %).</summary>
    public double BrowserDefaultZoom { get; init; } = 1.0;
    /// <summary>Show the current page title in the main window title bar when the Browser tab is active.</summary>
    public bool BrowserShowInTitlebar { get; init; } = false;
    /// <summary>Enable the built-in ad/tracker content filter.</summary>
    public bool BrowserContentFilterEnabled { get; init; } = true;
    /// <summary>Use the EasyList ad-blocking list.</summary>
    public bool BrowserFilterEasyList { get; init; } = true;
    /// <summary>Use the EasyPrivacy tracking-protection list.</summary>
    public bool BrowserFilterEasyPrivacy { get; init; } = true;
    /// <summary>Use Peter Lowe's ad-server list.</summary>
    public bool BrowserFilterPeterLowe { get; init; } = true;
    /// <summary>Use Fanboy's Annoyance List (cookie banners, newsletter pop-ups, social-share bars).</summary>
    public bool BrowserFilterFanboyAnnoyance { get; init; } = true;
    /// <summary>Use "I Don't Care About Cookies" (GDPR/cookie-consent dialog removal).</summary>
    public bool BrowserFilterIdontcareaboutcookies { get; init; } = true;
    /// <summary>Use Hagezi Pro unified hosts list (ads, trackers, telemetry, malware).</summary>
    public bool BrowserFilterHageziPro { get; init; } = true;
    /// <summary>Use URLhaus malware-distribution hosts list.</summary>
    public bool BrowserFilterUrlhaus { get; init; } = true;
    /// <summary>Preset user-agent: "default" (Edge), "chrome", "firefox", "safari", or "custom".</summary>
    public string BrowserUserAgentPreset { get; init; } = "default";
    /// <summary>Custom user-agent string used when <see cref="BrowserUserAgentPreset"/> is "custom".</summary>
    public string BrowserCustomUserAgent { get; init; } = "";
    /// <summary>Show the favorites/bookmarks bar below the browser toolbar.</summary>
    public bool BrowserShowFavoritesBar { get; init; } = false;
    /// <summary>Source for the favorites bar: "edge" or "chrome".</summary>
    public string BrowserFavoritesSource { get; init; } = "edge";

    // ── Find / Replace configuration ───────────────────────────────────────────
    /// <summary>Default case-sensitive matching when opening the Find/Replace dialog.</summary>
    public bool FindCaseSensitive { get; init; } = false;
    /// <summary>Default regular-expression mode when opening the Find/Replace dialog.</summary>
    public bool FindUseRegex { get; init; } = false;
    /// <summary>Default wrap-around behaviour when opening the Find/Replace dialog.</summary>
    public bool FindWrapAround { get; init; } = true;
    /// <summary>Auto-fill the Find field from the current editor selection when the dialog opens.</summary>
    public bool FindSeedFromSelection { get; init; } = true;
    /// <summary>How to report "not found": "messagebox", "statusbar", or "silent".</summary>
    public string FindNotFoundNotification { get; init; } = "messagebox";
    /// <summary>Default file-type filter for Find in Files (comma-separated globs).</summary>
    public string FindInFilesFilter { get; init; } = "*.cs, *.ts, *.js, *.json, *.md, *.xml, *.yaml, *.html, *.css, *.py, *.go, *.rs, *.tf, *.ps1, *.sh";
    /// <summary>Default directory names to exclude from Find in Files.</summary>
    public string FindInFilesExclude { get; init; } = "node_modules, .git, bin, obj, .vs, packages, .terraform";
    /// <summary>Maximum matches collected in Find in Files (0 = unlimited).</summary>
    public int FindInFilesMaxResults { get; init; } = 5000;

    // ── Editor Management (Tab) configuration ─────────────────────────────────    /// <summary>Editor tab width in pixels (60–300). Default 140.</summary>
    public int TabWidth { get; init; } = 140;
    /// <summary>Editor tab height in pixels (22–40). Default 26.</summary>
    public int TabHeight { get; init; } = 26;
    /// <summary>Show file-type icons in editor tabs.</summary>
    public bool TabShowFileIcons { get; init; } = true;
    /// <summary>How to mark unsaved tabs: "asterisk" (*prefix), "dot" (●suffix), "both", or "none".</summary>
    public string TabDirtyIndicator { get; init; } = "asterisk";
    /// <summary>When to show the × close button: "always", "active", "hover", or "never".</summary>
    public string TabCloseButtonVisibility { get; init; } = "always";
    /// <summary>Close the tab under the cursor on middle-mouse click.</summary>
    public bool TabMiddleClickClose { get; init; } = true;
    /// <summary>Scroll between editor tabs on mouse wheel over the tab bar.</summary>
    public bool TabMouseWheelScroll { get; init; } = true;
    /// <summary>Maximum simultaneously open editor tabs. 0 = unlimited.</summary>
    public int TabMaxOpen { get; init; } = 0;
    /// <summary>Show a save/discard prompt when closing a tab with unsaved changes.</summary>
    public bool TabConfirmCloseUnsaved { get; init; } = true;
    /// <summary>Remember recently closed tabs for Ctrl+Shift+T reopen.</summary>
    public bool TabRememberRecentlyClosed { get; init; } = false;
    /// <summary>How many recently closed tabs to remember (5–20).</summary>
    public int TabMaxRecentlyClosed { get; init; } = 10;

    // ── Git configuration ──────────────────────────────────────────────────────
    public string GitAuthorName { get; init; } = "";
    public string GitAuthorEmail { get; init; } = "";
    public string GitDefaultBranch { get; init; } = "main";
    public bool GitConfirmRemoveRepo { get; init; } = true;
    public bool GitConfirmDiscardChanges { get; init; } = true;
    public bool GitConfirmDiscardPermanent { get; init; } = true;
    public bool GitConfirmDiscardStash { get; init; } = true;
    public bool GitConfirmCheckoutCommit { get; init; } = false;
    public bool GitConfirmForcePush { get; init; } = true;
    public bool GitConfirmUndoCommit { get; init; } = true;
    public bool GitConfirmOverrideCommitMsg { get; init; } = false;
    public bool GitConfirmHiddenChanges { get; init; } = false;
    public string GitBranchSwitchBehavior { get; init; } = "ask";
    public bool GitCommitLengthWarning { get; init; } = false;
    public bool GitRunSecretScanOnCommit { get; init; } = true;
    public bool GitRunHooksOnCommit { get; init; } = true;
    public bool GitShowPreCommitReview { get; init; } = true;

    // ── Application Security ────────────────────────────────────────────────────
    /// <summary>Runtime security hardening level. Controls URL validation, TLS, path canonicalization, and encryption.</summary>
    public SecurityProfile SecurityProfile { get; init; } = SecurityProfile.Low;
    public bool SecPromptUntrustedWorkspace { get; init; } = true;
    public string SecTrustedWorkspacePaths { get; init; } = "";
    public bool SecConfirmUrlOpen { get; init; } = false;
    public bool SecAllowHttpUrls { get; init; } = true;
    public bool SecConfirmExternalTools { get; init; } = false;
    public bool SecConfirmDebugStart { get; init; } = false;
    public bool SecDebugAdapterPathOnly { get; init; } = true;
    public bool SecConfirmGitClone { get; init; } = true;
    public string SecTrustedGitHosts { get; init; } = "github.com,gitlab.com,dev.azure.com,bitbucket.org";
    public bool SecSastEnabled { get; init; } = true;
    public string SecSastSeverityThreshold { get; init; } = "Medium";
    public bool SecWriteCrashLog { get; init; } = true;
    public bool SecWriteStartupLog { get; init; } = true;
    public int SecLogRetentionDays { get; init; } = 30;
    public bool SecHighlightHardcodedSecrets { get; init; } = true;

    // ── AIOps configuration ────────────────────────────────────────────────────
    /// <summary>
    /// AIOps engine settings: connector endpoints, auth, polling intervals.
    /// Stored as a mutable class (not record) because connectors are configured at runtime.
    /// </summary>
    public AIOps.AIOpsSettings? AIOpsConfig { get; init; } = null;

    // ── Extensions configuration ───────────────────────────────────────────────
    /// <summary>Enable Tab-triggered snippet expansion for all built-in snippet languages.</summary>
    public bool SnippetsEnabled { get; init; } = true;
    /// <summary>Key to trigger snippet expansion: "Tab" or "CtrlSpace".</summary>
    public string SnippetTriggerKey { get; init; } = "Tab";
    /// <summary>Enable the regex-based lint engine (line length, magic numbers, naming conventions, etc.).</summary>
    public bool LintEnabled { get; init; } = true;
    /// <summary>Maximum line length before the LineTooLong rule fires (60–300). Default 120.</summary>
    public int LintMaxLineLength { get; init; } = 120;
    /// <summary>Flag hardcoded numeric literals (magic numbers) as lint warnings.</summary>
    public bool LintFlagMagicNumbers { get; init; } = true;
    /// <summary>Flag naming convention violations as lint hints.</summary>
    public bool LintFlagNamingConventions { get; init; } = true;
    /// <summary>Use Tree-sitter for symbol indexing in JS/TS/Python/Go/Rust and other non-C# files.</summary>
    public bool TreeSitterEnabled { get; init; } = true;
    /// <summary>Scan files for TODO/FIXME/HACK comments and show them in the Problems panel.</summary>
    public bool TodoScanEnabled { get; init; } = true;
    /// <summary>Comma-separated comment tags to scan for (case-insensitive).</summary>
    public string TodoScanPatterns { get; init; } = "TODO, FIXME, HACK, NOTE, BUG, WORKAROUND";

    // ── Formatting configuration ───────────────────────────────────────────────
    /// <summary>Auto-insert a matching closing brace on the line below and position the cursor between them when Enter is pressed on a line ending with an opening brace.</summary>
    public bool AutoCloseBracesOnEnter { get; init; } = false;
    /// <summary>Run the Roslyn document formatter before writing the file when a C# workspace is loaded. Has no effect on non-C# files or when no workspace is open.</summary>
    public bool FormatOnSave { get; init; } = false;
    /// <summary>Scan the first 100 lines of a newly opened file to detect its indentation style (tabs vs spaces, and indent width). Overrides the global Tab Size and Insert Spaces for that document while it is active.</summary>
    public bool DetectIndentationFromFile { get; init; } = true;

    // ── Editor advanced rendering ───────────────────────────────────────────────
    public bool ShowOccurrenceHighlights { get; init; } = true;
    public bool ShowIndentGuides { get; init; } = false;
    public bool SmoothenCaretBlink { get; init; } = true;
    public bool HighlightTrailingWhitespace { get; init; } = false;
    public bool MultiCursorEnabled { get; init; } = false;
    public bool SmartHomeKey { get; init; } = true;
    public bool BracketTypeOver { get; init; } = true;

    // ── Cursor configuration ───────────────────────────────────────────────────
    /// <summary>Keep the current-line highlight visible even when the editor does not have keyboard focus.</summary>
    public bool LineHighlightWhenUnfocused { get; init; } = false;
    /// <summary>Caret shape: "line" (2 px vertical bar), "block" (full-character fill), or "underline" (2 px horizontal bar at bottom).</summary>
    public string CursorStyle { get; init; } = "line";
    /// <summary>Animate the caret by blinking. Disable for a steady non-blinking cursor.</summary>
    public bool CursorBlinkEnabled { get; init; } = true;
    /// <summary>Caret blink interval in milliseconds (200–1200). Default 530 ms matches the Windows system default.</summary>
    public int CursorBlinkRateMs { get; init; } = 530;

    // ── Debug adapter configuration ────────────────────────────────────────────
    /// <summary>
    /// Which debug adapter to use. Defaults to NetCoreDbg (C#/.NET).
    /// Set to CppVsDbg or Gdb for C/C++ projects.
    /// </summary>
    public DebugAdapterType DebugAdapterType { get; init; } = DebugAdapterType.NetCoreDbg;

    /// <summary>
    /// Optional full path to the debug adapter executable.
    /// Leave empty to auto-discover from PATH or well-known install locations.
    /// </summary>
    public string DebugAdapterPath { get; init; } = "";
}

/// <summary>Identifies which DAP adapter binary pfpad should launch for debugging.</summary>
public enum DebugAdapterType
{
    /// <summary>netcoredbg — for C#/.NET projects. Auto-discovered if not set.</summary>
    NetCoreDbg,
    /// <summary>cppvsdbg — Visual Studio C/C++ debugger. Requires VS Build Tools or VS 2019+.</summary>
    CppVsDbg,
    /// <summary>gdb — GNU debugger via MI/DAP bridge (MinGW, MSYS2, or WSL).</summary>
    Gdb,
}

/// <summary>Graded security hardening level applied at runtime.</summary>
public enum SecurityProfile
{
    /// <summary>No runtime security enforcement. Legacy/development use only.</summary>
    NotHardened = 0,
    /// <summary>Basic runtime enforcement: URL scheme validation, TLS validation, path canonicalization.</summary>
    Low = 1,
    /// <summary>All Low controls plus DPAPI settings encryption, SDK credential flows, log secret masking.</summary>
    Mid = 2,
    /// <summary>All Mid controls plus strict URL allowlist (no http://), enforced HTTPS for AIOps endpoints.</summary>
    Max = 3,
}

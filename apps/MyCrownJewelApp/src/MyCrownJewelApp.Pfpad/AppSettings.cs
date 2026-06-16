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

    // ── Application Security ────────────────────────────────────────────────────
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

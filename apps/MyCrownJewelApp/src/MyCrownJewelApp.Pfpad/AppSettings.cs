using System.Collections.Generic;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Persisted user preferences for Pfpad. Pure data — no WinForms dependency.
/// </summary>
public record AppSettings(
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
    string ThemeName = "Dark",
    List<ExternalTool>? ExternalTools = null,
    bool WorkspaceVisible = false,
    int WorkspaceWidth = 200,
    string WorkspaceRoot = "",
    bool ShowWhitespace = true,
    bool SymbolPanelVisible = false,
    bool ProblemsPanelVisible = false,
    bool RainbowBracketsEnabled = false,
    bool BreadcrumbsEnabled = false,
    bool AnalyzersEnabled = true,
    bool AutoSaveEnabled = false,
    bool HoverLineHighlightEnabled = false,
    List<string>? RecentWorkspaces = null,
    string WindowBounds = "",
    string WindowState = "",
    string ActiveConfiguration = "Debug",
    // File size limits (in MB)
    int MaxFileSizeMB = 500,
    int LargeFileWarningMB = 50,
    int AsyncFileWarningMB = 20,
    // Feature degradation settings
    bool DisableSyntaxHighlightingForLargeFiles = true,
    bool DisableMinimapForLargeFiles = true,
    bool DisableWordWrapForLargeFiles = false,
    long SyntaxHighlightingThresholdBytes = 200 * 1024 // 200 KB
);

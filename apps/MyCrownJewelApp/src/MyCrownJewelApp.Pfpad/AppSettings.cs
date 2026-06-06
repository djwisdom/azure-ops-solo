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
}

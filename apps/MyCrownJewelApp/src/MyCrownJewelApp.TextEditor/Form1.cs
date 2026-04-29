using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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

namespace MyCrownJewelApp.TextEditor
{
    public partial class Form1 : Form
    {
        // Win32 API for dark scrollbar support
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

        private void ApplyScrollbarTheme()
        {
            if (textEditor != null && textEditor.IsHandleCreated)
            {
                if (isDarkTheme)
                    SetWindowTheme(textEditor.Handle, DARK_MODE_SCROLLBAR, null);
                else
                    SetWindowTheme(textEditor.Handle, null, null);
            }
        }

        // State
        private HashSet<int> bookmarks = new();
        private HashSet<int> modifiedLines = new();
        private HashSet<int> collapsedRegions = new();
        private bool gutterVisible = true;
        private bool statusBarVisible = true;
        private bool wordWrapEnabled = false;
        private bool syntaxHighlightingEnabled = false;
        private CurrentLineHighlightMode currentLineHighlightMode = CurrentLineHighlightMode.Off;
        private int tabSize = 4;
        private bool insertSpaces = true;
        private bool isDarkTheme = true;
        private float zoomFactor = 1.0f;
        private bool isHighlighting = false;
        private int lastHighlightedLine = -1;
        private bool isFullScreen = false;
        private Rectangle normalBounds;
        private FormWindowState normalWindowState;
        private FormBorderStyle normalBorderStyle;
        private string fontName = "Consolas";
        private float fontSize = 12f;
        
        // Feature toggles
        private bool autoIndentEnabled = true;
        private bool smartTabsEnabled = true;
        private bool elasticTabsEnabled = true;
        
        // Suspend selection changed events during internal updates
        private bool _suspendSelectionChanged = false;

        // Elastic tab stops system
        private System.Windows.Forms.Timer? elasticTabTimer;
        private CancellationTokenSource? tabComputeCts;

        // Syntax highlighting
        private SyntaxDefinition? currentSyntax;
        private IncrementalHighlighter? incrementalHighlighter;

        // Column guide state
        private int guideColumn = 80;
        private bool showGuide = true;
        private readonly Color guideColor = Color.FromArgb(60, 60, 60);

        // Current file state
        private string? currentFilePath;
        private bool isModified = false;
        private DateTime? lastFileWriteTime;

        // Per-buffer dirty-flag system: saved snapshot (hash)
        private string? savedContentHash = null;

        // Recent files
        private const int MaxRecentFiles = 10;
        private List<string> recentFiles = new List<string>();

        // Settings persistence
        private record AppSettings(
            bool IsDarkTheme,
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
            bool MinimapVisible
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

        // Colors
        private Color darkBackColor = Color.FromArgb(30, 30, 30);
        private Color darkForeColor = Color.FromArgb(220, 220, 220);
        private Color darkMenuBackColor = Color.FromArgb(45, 45, 45);
        private Color darkMenuForeColor = Color.FromArgb(220, 220, 220);
        private Color darkEditorBackColor = Color.FromArgb(30, 30, 30);
        private Color darkEditorForeColor = Color.FromArgb(220, 220, 220);

        private Color lightBackColor = Color.White;
        private Color lightForeColor = Color.Black;
        private Color lightMenuBackColor = SystemColors.MenuBar;
        private Color lightMenuForeColor = SystemColors.MenuText;
        private Color lightEditorBackColor = Color.White;
        private Color lightEditorForeColor = Color.Black;

        private Color keywordColor = Color.Blue;
        private Color stringColor = Color.Maroon;
        private Color commentColor = Color.Green;
        private Color numberColor = Color.DarkRed;
        private Color preprocessorColor = Color.Gray;

        private System.Windows.Forms.Timer? highlightTimer;

        // Incremental highlight cache
        private Dictionary<string, (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor)> compiledRegexCache = new();

        public Form1()
        {
            InitializeComponent();
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.FormClosing += Form1_FormClosing;
            this.Activated += Form1_Activated;

            isDarkTheme = true;
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
            textEditor.HandleCreated += (s, e) => { ApplyScrollbarTheme(); UpdateTabStops(); };
            
            UpdateThemeColors(isDarkTheme);
            ApplyWordWrap();
            UpdateStatusBar();
            UpdateColumnGuideMenuChecked();
            UpdateTabSizeMenu();
            UpdateTabStops();
            UpdateCurrentLineHighlightMenu();
            
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
            guidePanel.Visible = showGuide;
            if (guidePanel != null)
            {
                guidePanel.ShowGuide = showGuide;
                guidePanel.GuideColumn = guideColumn;
            }
            minimapControl.Visible = _pendingMinimapVisible;
            statusStrip.Visible = statusBarVisible;
            
            // Set initial column widths for visible state
            if (mainTable.ColumnCount >= 1)
                mainTable.ColumnStyles[0].Width = gutterVisible ? 60 : 0;
            
            // Attach minimap to editor
            if (minimapControl != null)
            {
                minimapControl.AttachEditor(textEditor);
                minimapControl.ViewportChanged += MinimapControl_ViewportChanged;
                minimapControl.SetTokenProvider(GetTokensForLine);
                PositionMinimap(); // initial placement
            }
            
            // Initialize syntax highlighting debounce timer
            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 150; // 150ms debounce for responsiveness
            highlightTimer.Tick += (s, e) =>
            {
                highlightTimer.Stop();
                if (syntaxHighlightingEnabled)
                {
                    RequestVisibleHighlight();
                }
            };

            // Elastic tab stops debounce timer
            elasticTabTimer = new System.Windows.Forms.Timer();
            elasticTabTimer.Interval = 250; // 250ms after last change
            elasticTabTimer.Tick += (s, e) => { elasticTabTimer.Stop(); if (elasticTabsEnabled) ComputeElasticTabStopsAsync(); };

            // Initialize incremental highlighter (after colors are loaded)
            CreateIncrementalHighlighter();

            // Apply syntax highlighting state after timer is created
            if (syntaxHighlightingEnabled)
            {
                highlightTimer.Start();
            }
        }

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
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);
                var size = g.MeasureString("0", textEditor.Font);
                int charWidth = (int)Math.Ceiling(size.Width);
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
                    // Capture needed UI data on UI thread
                    string[] lines = Array.Empty<string>();
                    int firstVisible = 0;
                    int visibleLineCount = 0;
                    
                    textEditor.Invoke(new Action(() =>
                    {
                        if (textEditor.IsDisposed) return;
                        lines = textEditor.Lines;
                        firstVisible = (int)SendMessage(textEditor.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
                        using var g = Graphics.FromHwnd(textEditor.Handle);
                        int lineHeight = TextRenderer.MeasureText("X", textEditor.Font).Height;
                        visibleLineCount = textEditor.ClientSize.Height / lineHeight + 2; // +2 for buffer
                    }));
                    
                    if (token.IsCancellationRequested) return;
                    if (lines.Length == 0) return;
                    
                    int lastIndex = Math.Min(firstVisible + visibleLineCount, lines.Length) - 1;
                    if (lastIndex < firstVisible) return;
                    
                    // Compute max cell width per column (i.e., width of text before each tab)
                    var maxWidths = new Dictionary<int, int>();
                    for (int lineIdx = firstVisible; lineIdx <= lastIndex; lineIdx++)
                    {
                        if (token.IsCancellationRequested) return;
                        string line = lines[lineIdx];
                        // Split: each tab produces a cell before it; we consider all cells except the final one
                        string[] cells = line.Split('\t');
                        for (int col = 0; col < cells.Length - 1; col++)
                        {
                            string cell = cells[col];
                            int w = TabMeasurementCache.GetStringWidth(cell, textEditor.Font);
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
                    
                    // Apply to editor on UI thread
                    textEditor.Invoke(new Action(() =>
                    {
                        if (!textEditor.IsDisposed && !textEditor.Disposing && stops.Count > 0)
                        {
                            textEditor.SelectionTabs = stops.ToArray();
                        }
                    }));
                }
                catch { /* ignore */ }
            }, token);
        }

        #endregion

        #region Theme & Toggles

        private void UpdateThemeColors(bool isDark)
        {
            var backColor = isDark ? darkBackColor : lightBackColor;
            var foreColor = isDark ? darkForeColor : lightForeColor;
            var editorBack = isDark ? darkEditorBackColor : lightEditorBackColor;
            var editorFore = isDark ? darkEditorForeColor : lightEditorForeColor;

            this.BackColor = backColor;
            this.ForeColor = foreColor;
            if (menuStrip != null)
            {
                menuStrip.BackColor = editorBack;
                menuStrip.ForeColor = editorFore;
            }
            if (textEditor != null)
            {
                textEditor.BackColor = editorBack;
                textEditor.ForeColor = editorFore;
            }
            if (gutterPanel != null)
            {
                gutterPanel.BackColor = editorBack;
            }
            if (minimapControl != null)
            {
                minimapControl.BackColor = editorBack;
                minimapControl.ViewportColor = isDark ? Color.FromArgb(100, Color.DodgerBlue) : Color.FromArgb(80, Color.LightBlue);
                minimapControl.ViewportBorderColor = Color.DodgerBlue;
                minimapControl.RefreshNow();
            }
            if (guidePanel != null)
            {
                guidePanel.GuideColor = isDark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(120, 120, 120);
            }
            if (statusStrip != null)
            {
                statusStrip.BackColor = editorBack;
                statusStrip.ForeColor = editorFore;
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.BackColor = editorBack;
                    item.ForeColor = editorFore;
                }
            }

            // Apply scrollbar theme immediately if handle exists
            if (textEditor != null && textEditor.IsHandleCreated)
            {
                if (isDark)
                    SetWindowTheme(textEditor.Handle, DARK_MODE_SCROLLBAR, null);
                else
                    SetWindowTheme(textEditor.Handle, null, null);
            }

            // Update selection colors to match theme
            if (textEditor != null && textEditor.IsHandleCreated)
            {
                if (isDark)
                {
                    textEditor.SelectionBackColor = Color.FromArgb(0, 120, 215);
                    textEditor.SelectionColor = Color.White;
                }
                else
                {
                    textEditor.SelectionBackColor = SystemColors.Highlight;
                    textEditor.SelectionColor = SystemColors.HighlightText;
                }
            }

            darkThemeMenuItem.Checked = isDark;
            lightThemeMenuItem.Checked = !isDark;

            // Recreate highlighter with new theme colors
            CreateIncrementalHighlighter();

            // Refresh current line highlight and gutter with new theme colors
            if (currentLineHighlightMode != CurrentLineHighlightMode.Off)
            {
                _suspendSelectionChanged = true;
                try
                {
                    ClearCurrentLineHighlight();
                    lastHighlightedLine = -1;
                    HighlightCurrentLine();
                }
                finally
                {
                    _suspendSelectionChanged = false;
                }
            }
            gutterPanel?.RefreshGutter();
            textEditor?.Invalidate();
        }

        private void ToggleTheme()
        {
            isDarkTheme = !isDarkTheme;
            UpdateThemeColors(isDarkTheme);
            // Update theme menu checkmarks
            darkThemeMenuItem.Checked = isDarkTheme;
            lightThemeMenuItem.Checked = !isDarkTheme;
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
                        isDarkTheme = settings.IsDarkTheme;
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
                    IsDarkTheme: isDarkTheme,
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
                    MinimapVisible: minimapMenuItem?.Checked ?? false
                );
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { /* ignore settings save errors */ }
        }

        private bool _pendingMinimapVisible = false; // set by LoadSettings, applied after controls exist

        private void ToggleGutter()
        {
            gutterVisible = !gutterVisible;
            gutterMenuItem.Checked = gutterVisible;
            gutterPanel.Visible = gutterVisible;
            // Adjust column width: 60 when visible, 0 when hidden
            if (mainTable.ColumnCount > 0)
            {
                mainTable.ColumnStyles[0].Width = gutterVisible ? 60 : 0;
            }
            // Force redraw after layout change
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

        private void ToggleMinimap()
        {
            bool visible = minimapMenuItem.Checked;
            if (minimapControl != null)
            {
                minimapControl.Visible = visible;
                if (visible) PositionMinimap();
            }
            SaveSettings();
        }

        private void PositionMinimap()
        {
            if (minimapControl == null || !minimapControl.Visible) return;
            
            int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
            int x = textEditor.ClientSize.Width - scrollbarWidth - minimapControl.Width;
            if (x < 0) x = 0;
            minimapControl.Location = new Point(x, 0);
            minimapControl.Height = textEditor.ClientSize.Height;
        }

        private void MinimapMenuItem_Click(object? sender, EventArgs e)
        {
            ToggleMinimap();
        }

        private void ToggleWordWrap()
        {
            wordWrapEnabled = !wordWrapEnabled;
            wordWrapMenuItem.Checked = wordWrapEnabled;
            ApplyWordWrap();
            SaveSettings();
        }

        private void ToggleSyntaxHighlighting()
        {
            syntaxHighlightingEnabled = !syntaxHighlightingEnabled;
            syntaxHighlightingMenuItem.Checked = syntaxHighlightingEnabled;
            if (syntaxHighlightingEnabled)
            {
                highlightTimer?.Stop();
                highlightTimer?.Start();
            }
            else
            {
                incrementalHighlighter?.Dispose();
                incrementalHighlighter = null;
                var baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
                ResetVisibleRangeToBase(baseColor);
            }
            SaveSettings();
        }

        // Tab handling
        private void InsertSpaces_Click(object? sender, EventArgs e) => ToggleInsertSpaces();
        private void SetTabSize(int size)
        {
            tabSize = size;
            UpdateTabSizeMenu();
            UpdateTabStops();
            UpdateStatusBar();
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
        private void ToggleInsertSpaces()
        {
            insertSpaces = !insertSpaces;
            insertSpacesMenuItem.Checked = insertSpaces;
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
            
            if (currentLineHighlightMode == CurrentLineHighlightMode.Off)
            {
                ClearCurrentLineHighlight();
            }
            else
            {
                lastHighlightedLine = -1;
                HighlightCurrentLine();
                gutterPanel?.RefreshGutter();
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

        private void ClearCurrentLineHighlight()
        {
            if (lastHighlightedLine >= 0 && lastHighlightedLine < textEditor.Lines.Length)
            {
                int prevStart = textEditor.GetFirstCharIndexFromLine(lastHighlightedLine);
                int prevLen = textEditor.Lines[lastHighlightedLine].Length;
                if (prevStart >= 0 && prevLen > 0)
                {
                    textEditor.Select(prevStart, prevLen);
                    textEditor.SelectionBackColor = textEditor.BackColor;
                }
            }
            lastHighlightedLine = -1;
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
                        string lineText = textEditor.Lines[lineIdx];
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
                        prevLineText = textEditor.Lines[currentLine - 1];
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
                    if (line < 0 || line >= textEditor.Lines.Length) continue;
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
                    if (line < 0 || line >= textEditor.Lines.Length) continue;
                    int lineStart = textEditor.GetFirstCharIndexFromLine(line);
                    if (lineStart < 0) continue;
                    
                    string lineText = textEditor.Lines[line];
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

        private void SetGuideColumn(int column)
        {
            guideColumn = column;
            showGuide = true;
            columnGuideMenuItem.Checked = true;
            UpdateColumnGuideMenuChecked();
            guidePanel?.Invalidate();
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

        private void ApplyWordWrap()
        {
            if (textEditor == null) return;
            textEditor.WordWrap = wordWrapEnabled;
            textEditor.ScrollBars = wordWrapEnabled ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        #endregion

        #region File Operations

        private void NewFile()
        {
            var result = PromptSaveChanges();
            if (result == DialogResult.Cancel) return;

            textEditor.Clear();
            currentFilePath = null;
            isModified = false;
            savedContentHash = null;
            lastFileWriteTime = null;
             modifiedLines.Clear();
             UpdateWindowTitle();
             UpdateStatusBar();
             
             // Request highlighting for empty buffer (clears any previous highlights)
             RequestVisibleHighlight();
         }

        private void OpenFile()
        {
            var result = PromptSaveChanges();
            if (result == DialogResult.Cancel) return;

            using var ofd = new OpenFileDialog();
            ofd.Filter = "Text Files|*.txt|All Files|*.*";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFile(ofd.FileName);
            }
        }

        internal void LoadFile(string path)
        {
            try
            {
                textEditor.Text = File.ReadAllText(path);
                currentFilePath = path;
                if (File.Exists(path))
                    lastFileWriteTime = File.GetLastWriteTimeUtc(path);
                ClearDirtyAfterSave();
                AddToRecentFiles(path);
                
                // Ensure editor starts at top: set caret at 0 and scroll via Win32
                textEditor.SelectionStart = 0;
                if (textEditor.IsHandleCreated)
                {
                    SendMessage(textEditor.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
                }
                 
                 UpdateStatusBar();
                 
                 // Request syntax highlighting for newly loaded content
                 RequestVisibleHighlight();
             }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFile()
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

        private void SaveAsFile()
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

        private void SetDirty()
        {
            if (isModified) return; // Already dirty

            isModified = true;
            UpdateWindowTitle();
            // Note: modifiedLines will be updated by TextEditor_TextChanged or selection logic
        }

        internal void ClearDirtyAfterSave()
        {
            isModified = false;
            savedContentHash = ComputeContentHash();
            modifiedLines.Clear();
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string baseTitle = currentFilePath != null 
                ? $"MyCrownJewelApp TextEditor - {Path.GetFileName(currentFilePath)}"
                : "MyCrownJewelApp TextEditor - Untitled";
            this.Text = isModified ? "*" + baseTitle : baseTitle;
        }

        private DialogResult PromptSaveChanges()
        {
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
                    var item = new ToolStripMenuItem(display, null, (s, e) => LoadFile(filePath));
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
        private void CloseTab_Click(object? sender, EventArgs e) => NewFile();  // For now, clear
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
                
                // Recompute elastic tab stops with new font
                elasticTabTimer?.Stop();
                elasticTabTimer?.Start();
            }
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

        private void ToggleAllFolds_Click(object? sender, EventArgs e)
        {
            // Toggle collapse on all #region/#endregion
            for (int i = 0; i < textEditor.Lines.Length; i++)
            {
                string line = textEditor.Lines[i];
                if (line.TrimStart().StartsWith("#region") || line.TrimStart().StartsWith("#endregion"))
                {
                    if (collapsedRegions.Contains(i))
                        collapsedRegions.Remove(i);
                    else
                        collapsedRegions.Add(i);
                }
            }
            // In a real editor would collapse/expand text
        }

        internal void ToggleBookmark(int line)
        {
            if (bookmarks.Contains(line))
                bookmarks.Remove(line);
            else
                bookmarks.Add(line);
            if (gutterPanel != null) gutterPanel.RefreshGutter();
        }

        internal void ToggleFold(int line)
        {
            if (collapsedRegions.Contains(line))
                collapsedRegions.Remove(line);
            else
                collapsedRegions.Add(line);
            // In real editor, would collapse code here
        }

        #endregion

        #region View Menu Handlers

        private void ZoomIn_Click(object? sender, EventArgs e)
        {
            if (zoomFactor < 5.0f)
            {
                zoomFactor += 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                if (gutterPanel != null) gutterPanel.RefreshGutter();
                guidePanel?.Invalidate();
            }
        }

        private void ZoomOut_Click(object? sender, EventArgs e)
        {
            if (zoomFactor > 0.5f)
            {
                zoomFactor -= 0.1f;
                textEditor.ZoomFactor = zoomFactor;
                zoomLabel.Text = $"{(int)(zoomFactor * 100)}%";
                if (gutterPanel != null) gutterPanel.RefreshGutter();
                guidePanel?.Invalidate();
            }
        }

        private void RestoreDefaultZoom_Click(object? sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            textEditor.ZoomFactor = zoomFactor;
            zoomLabel.Text = "100%";
            if (gutterPanel != null) gutterPanel.RefreshGutter();
            guidePanel?.Invalidate();
        }

        private void StatusBar_Click(object? sender, EventArgs e) => ToggleStatusBar();

        private void ColumnGuide_Click(object? sender, EventArgs e)
        {
            // Menu item's Checked state is the new desired state
            bool visible = columnGuideMenuItem.Checked;
            showGuide = visible;
            if (guidePanel != null) guidePanel.ShowGuide = visible;
            SaveSettings();
        }

        private void SyntaxHighlighting_Click(object? sender, EventArgs e) => ToggleSyntaxHighlighting();

        private void WordWrap_Click(object? sender, EventArgs e) => ToggleWordWrap();
        private void GutterMenuItem_Click(object? sender, EventArgs e) => ToggleGutter();

        private void DarkTheme_Click(object? sender, EventArgs e)
        {
            isDarkTheme = true;
            UpdateThemeColors(isDarkTheme);
            SaveSettings();
        }

        private void LightTheme_Click(object? sender, EventArgs e)
        {
            isDarkTheme = false;
            UpdateThemeColors(isDarkTheme);
            SaveSettings();
        }

        #endregion

        #region Public API (called by dialogs)

        public void GoToLine(int lineNumber)
        {
            if (lineNumber < 1 || textEditor.Lines.Length == 0) return;
            int targetIndex = lineNumber - 1;
            if (targetIndex >= textEditor.Lines.Length) targetIndex = textEditor.Lines.Length - 1;
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

        private Color GetCurrentLineHighlightColor()
        {
            // VS Code style: subtle lighter/darker than background
            return isDarkTheme ? Color.FromArgb(60, 60, 60) : Color.FromArgb(230, 230, 230);
        }

        private void HighlightCurrentLine()
        {
            if (isHighlighting) return;
            if (currentLineHighlightMode == CurrentLineHighlightMode.Off) return;

            int currentLine = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            if (currentLine == lastHighlightedLine) return;

            isHighlighting = true;
            try
            {
                BeginUpdate(textEditor);
                try
                {
                    // Save current selection
                    int savedStart = textEditor.SelectionStart;
                    int savedLength = textEditor.SelectionLength;

                    // Clear previous highlight
                    if (lastHighlightedLine >= 0 && lastHighlightedLine < textEditor.Lines.Length)
                    {
                        int prevStart = textEditor.GetFirstCharIndexFromLine(lastHighlightedLine);
                        int prevLen = textEditor.Lines[lastHighlightedLine].Length;
                        if (prevStart >= 0 && prevLen > 0)
                        {
                            textEditor.Select(prevStart, prevLen);
                            textEditor.SelectionBackColor = textEditor.BackColor;
                        }
                    }

                    // Apply new highlight based on mode
                    if (currentLineHighlightMode == CurrentLineHighlightMode.WholeLine)
                    {
                        if (currentLine >= 0 && currentLine < textEditor.Lines.Length)
                        {
                            int start = textEditor.GetFirstCharIndexFromLine(currentLine);
                            int len = textEditor.Lines[currentLine].Length;
                            if (start >= 0)
                            {
                                textEditor.Select(start, Math.Max(len, 1));
                                textEditor.SelectionBackColor = GetCurrentLineHighlightColor();
                            }
                        }
                    }
                    // NumberOnly mode: no text background highlight; gutter will draw bold number

                    lastHighlightedLine = currentLine;

                    // Restore original selection (no forced ScrollToCaret to avoid jumping)
                    textEditor.SelectionStart = savedStart;
                    textEditor.SelectionLength = savedLength;
                }
                finally
                {
                    EndUpdate(textEditor);
                    // Ensure full repaint to avoid artifacts
                    textEditor.Invalidate();
                }
            }
            finally
            {
                isHighlighting = false;
            }
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            SetDirty();

            // Track which line changed for gutter display
            int lineIndex = textEditor.GetLineFromCharIndex(textEditor.SelectionStart);
            modifiedLines.Add(lineIndex);

            // Mark affected lines for incremental re-highlighting (line and following line for context)
            incrementalHighlighter?.MarkDirty(lineIndex);
            incrementalHighlighter?.MarkDirty(lineIndex + 1);

            UpdateStatusBar();

            // Restart debounce timer for syntax highlighting (only if enabled)
            if (syntaxHighlightingEnabled && highlightTimer != null)
            {
                highlightTimer.Stop();
                highlightTimer.Start();
            }

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
            if (syntaxHighlightingEnabled)
            {
                highlightTimer?.Stop();
                highlightTimer?.Start();
            }
            guidePanel?.Invalidate();
        }

         private void TextEditor_Resize(object? sender, EventArgs e)
         {
             if (gutterPanel != null) gutterPanel.RefreshGutter();
            if (syntaxHighlightingEnabled)
            {
                CreateIncrementalHighlighter();
                highlightTimer?.Stop();
                highlightTimer?.Start();
            }
             guidePanel?.Invalidate();
             PositionMinimap();
         }

         private void UpdateStatusBar()
        {
            if (statusStrip == null || textEditor == null) return;

            // Line and column (1-based)
            int line = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int col = textEditor.SelectionStart - textEditor.GetFirstCharIndexFromLine(textEditor.GetLineFromCharIndex(textEditor.SelectionStart)) + 1;
            lineColLabel.Text = $"Ln {line}, Col {col}";

            // Character count
            charCountLabel.Text = $"{textEditor.Text.Length:N0} characters";

            // Tab size
            tabSizeLabel.Text = $"Tab: {tabSize}";

            // Current line / total lines
            int currentLineNum = textEditor.GetLineFromCharIndex(textEditor.SelectionStart) + 1;
            int totalLines = textEditor.Lines.Length;
            linePositionLabel.Text = $"{currentLineNum} / {totalLines}";

            // Scroll percentage (current line / total lines)
            int total = textEditor.Lines.Length;
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
        }

        #endregion

        #region Syntax Highlighting (async, incremental, visible-range)

        private Color GetKeywordColor() => keywordColor;
        private Color GetStringColor() => stringColor;
        private Color GetCommentColor() => commentColor;
        private Color GetNumberColor() => numberColor;
        private Color GetPreprocessorColor() => preprocessorColor;

        private void CreateIncrementalHighlighter()
        {
            incrementalHighlighter?.Dispose();
            incrementalHighlighter = null;

            // Detect syntax based on current file path or default
            if (currentFilePath != null)
            {
                currentSyntax = SyntaxDefinition.GetDefinitionForFile(currentFilePath);
            }
            else
            {
                currentSyntax = SyntaxDefinition.CSharp; // default
            }

            if (!syntaxHighlightingEnabled || currentSyntax == null)
            {
                var baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
                ResetVisibleRangeToBase(baseColor);
                return;
            }

            var baseColorCurrent = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
            incrementalHighlighter = new IncrementalHighlighter(
                textEditor,
                currentSyntax,
                baseColorCurrent,
                GetKeywordColor(),
                GetStringColor(),
                GetCommentColor(),
                GetNumberColor(),
                GetPreprocessorColor());

            incrementalHighlighter.PatchReady += ApplyHighlightPatch;

            // Request visible range after creation
            RequestVisibleHighlight();
        }

        // Request highlighting of currently visible lines
        private void RequestVisibleHighlight()
        {
            if (incrementalHighlighter == null || !syntaxHighlightingEnabled) return;
            var (first, last) = GetVisibleLineRange();
            if (first <= last)
            {
                incrementalHighlighter.RequestRange(first, last);
            }
        }

        private void ApplyHighlightPatch(object? sender, HighlightPatch patch)
        {
            if (textEditor.IsDisposed) return;
            int line = patch.LineNumber;
            if (line < 0 || line >= textEditor.Lines.Length) return;

            int lineStart = textEditor.GetFirstCharIndexFromLine(line);
            int lineLen = textEditor.Lines[line].Length;
            if (lineLen == 0) return;

            var baseColor = isDarkTheme ? darkEditorForeColor : lightEditorForeColor;
            BeginUpdate(textEditor);
            textEditor.SuspendLayout();
            try
            {
                // Reset line to base color
                textEditor.SelectionStart = lineStart;
                textEditor.SelectionLength = lineLen;
                textEditor.SelectionColor = baseColor;

                // Apply token colors
                foreach (var token in patch.Tokens)
                {
                    int idx = lineStart + token.StartIndex;
                    int len = token.Length;
                    if (idx >= lineStart && idx + len <= lineStart + lineLen)
                    {
                        textEditor.SelectionStart = idx;
                        textEditor.SelectionLength = len;
                        textEditor.SelectionColor = GetColorForToken(token.Type);
                    }
                }
            }
            finally
            {
                textEditor.ResumeLayout();
                EndUpdate(textEditor);
            }
        }

        private Color GetColorForToken(SyntaxTokenType type) => type switch
        {
            SyntaxTokenType.Keyword => GetKeywordColor(),
            SyntaxTokenType.String => GetStringColor(),
            SyntaxTokenType.Comment => GetCommentColor(),
            SyntaxTokenType.Number => GetNumberColor(),
            SyntaxTokenType.Preprocessor => GetPreprocessorColor(),
            _ => (isDarkTheme ? darkEditorForeColor : lightEditorForeColor)
        };

        // Get visible line range in the editor (no buffer — only truly visible lines)
        private (int firstLine, int lastLine) GetVisibleLineRange()
        {
            try
            {
                int visibleStart = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, 0)));
                int visibleEnd = textEditor.GetLineFromCharIndex(textEditor.GetCharIndexFromPosition(new Point(0, textEditor.ClientSize.Height)));
                int totalLines = textEditor.Lines.Length;
                // No buffer: only highlight lines that are actually visible
                int first = Math.Max(0, visibleStart);
                int last = Math.Min(totalLines - 1, visibleEnd);
                // If viewport is invalid (e.g., control not yet laid out), return empty range
                if (first > last) return (0, -1);
                return (first, last);
            }
            catch
            {
                // Return empty range on any error
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
             try
             {
                 var (firstLine, lastLine) = GetVisibleLineRange();
                 if (firstLine <= lastLine)
                 {
                     for (int lineNum = firstLine; lineNum <= lastLine; lineNum++)
                     {
                         if (lineNum < 0 || lineNum >= textEditor.Lines.Length) continue;
                         int lineStart = textEditor.GetFirstCharIndexFromLine(lineNum);
                         if (lineStart < 0) continue;
                         int lineLen = textEditor.Lines[lineNum].Length;
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
             }
         }

        /// <summary>
        /// Returns tokens for a given line index, used by MinimapControl for syntax coloring.
        /// </summary>
        private IReadOnlyList<MyCrownJewelApp.TextEditor.TokenInfo> GetTokensForLine(int lineIndex)
        {
            if (currentSyntax == null) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();
            if (lineIndex < 0 || lineIndex >= textEditor.Lines.Length) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();

            // Try incremental highlighter cache first
            if (incrementalHighlighter?.GetTokens(lineIndex) is IReadOnlyList<TokenInfo> cached)
                return cached;

            // Fallback: synchronous tokenization (no state)
            return TokenizeLineSynchronously(lineIndex);
        }

        private IReadOnlyList<MyCrownJewelApp.TextEditor.TokenInfo> TokenizeLineSynchronously(int lineIndex)
        {
            string line = textEditor.Lines[lineIndex];
            if (string.IsNullOrEmpty(line)) return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();

            var tokens = new List<MyCrownJewelApp.TextEditor.TokenInfo>();
            var colored = new bool[line.Length];

            var regexes = GetOrCreateCompiledRegexes(currentSyntax, CancellationToken.None);

            void AddMatches(System.Text.RegularExpressions.Regex? regex, MyCrownJewelApp.TextEditor.SyntaxTokenType type)
            {
                if (regex == null) return;
                var matches = regex.Matches(line);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (!m.Success) continue;
                    int start = m.Index;
                    int len = m.Length;
                    if (start < 0 || start >= colored.Length) continue;
                    if (start + len > colored.Length) len = colored.Length - start;
                    if (len <= 0) continue;

                    // Check if region is completely free
                    bool free = true;
                    for (int i = start; i < start + len; i++)
                    {
                        if (colored[i])
                        {
                            free = false;
                            break;
                        }
                    }
                    if (free)
                    {
                        for (int i = start; i < start + len; i++)
                            colored[i] = true;
                        tokens.Add(new MyCrownJewelApp.TextEditor.TokenInfo
                        {
                            Type = type,
                            Text = line.Substring(start, len),
                            StartIndex = start,
                            Length = len
                        });
                    }
                }
            }

            // Priority order
            AddMatches(regexes.preprocessor, MyCrownJewelApp.TextEditor.SyntaxTokenType.Preprocessor);
            AddMatches(regexes.comment, MyCrownJewelApp.TextEditor.SyntaxTokenType.Comment);
            AddMatches(regexes.stringRegex, MyCrownJewelApp.TextEditor.SyntaxTokenType.String);
            AddMatches(regexes.number, MyCrownJewelApp.TextEditor.SyntaxTokenType.Number);
            AddMatches(regexes.keywords, MyCrownJewelApp.TextEditor.SyntaxTokenType.Keyword);
            AddMatches(regexes.types, MyCrownJewelApp.TextEditor.SyntaxTokenType.Keyword);

            return tokens;
        }

        // Helper: mark a range as colored
        private static void MarkRange(bool[] colored, int start, int length)
        {
            for (int i = start; i < start + length && i < colored.Length; i++)
                colored[i] = true;
        }

        // Helper: check if range is free
        private static bool IsRangeFree(bool[] colored, int start, int length)
        {
            for (int i = start; i < start + length && i < colored.Length; i++)
                if (colored[i]) return false;
            return true;
        }

        // WinForms RichTextBox BeginUpdate/EndUpdate via native API (no forced invalidation)
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x0B;
        private const int EM_LINESCROLL = 0xB6;
        private const int WM_VSCROLL = 0x115;
        private const int SB_TOP = 6;
        private const int EM_STARTUNDOACTION = 0x00B7;
        private const int EM_ENDUNDOACTION = 0x00B8;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_GETLINECOUNT = 0x00BA;

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

        // Compile and cache regexes for a syntax definition
        private static Regex? BuildRegex(string? pattern, RegexOptions options)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            return new Regex(pattern, options | RegexOptions.Compiled);
        }

        private (Regex? keywords, Regex? types, Regex? stringRegex, Regex? comment, Regex? number, Regex? preprocessor) GetOrCreateCompiledRegexes(SyntaxDefinition syntax, CancellationToken token)
        {
            string key = syntax.Name; // simple unique key
            lock (compiledRegexCache)
            {
                if (compiledRegexCache.TryGetValue(key, out var existing))
                {
                    return existing;
                }
            }

            var keywords = BuildRegex(@"\b(" + string.Join("|", syntax.Keywords.Select(Regex.Escape)) + @")\b", RegexOptions.None);
            var types = BuildRegex(@"\b(" + string.Join("|", syntax.Types.Select(Regex.Escape)) + @")\b", RegexOptions.None);
            var stringRegex = BuildRegex(syntax.StringPattern, RegexOptions.Singleline);
            var comment = BuildRegex(syntax.CommentPattern, RegexOptions.Multiline);
            var number = BuildRegex(syntax.NumberPattern, RegexOptions.None);
            var preprocessor = syntax.Preprocessor?.Length > 0
                ? BuildRegex(@"^\s*(" + string.Join("|", syntax.Preprocessor.Select(Regex.Escape)) + @")\b", RegexOptions.Multiline)
                : null;

            var tuple = (keywords, types, stringRegex, comment, number, preprocessor);
            lock (compiledRegexCache) { compiledRegexCache[key] = tuple; }
            return tuple;
        }

        // Apply token matches to line ranges, respecting priority (colored[] tracks occupied spans)
        private static void ApplyTokenMatches(MatchCollection matches, bool[] colored, Dictionary<int, List<(int start, int length, Color color)>> ranges, int lineNum, Color color)
        {
            foreach (Match m in matches)
            {
                if (m.Success && m.Index >= 0)
                {
                    int len = Math.Min(m.Length, colored.Length - m.Index);
                    if (len <= 0) continue;
                    bool free = true;
                    for (int i = m.Index; i < m.Index + len && i < colored.Length; i++)
                    {
                        if (colored[i]) { free = false; break; }
                    }
                    if (free)
                    {
                        for (int i = m.Index; i < m.Index + len && i < colored.Length; i++)
                            colored[i] = true;
                        if (!ranges.ContainsKey(lineNum)) ranges[lineNum] = new List<(int, int, Color)>();
                        ranges[lineNum].Add((m.Index, len, color));
                    }
                }
            }
        }

        #region Fullscreen Toggle
        private void Form1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
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
        }

        private void Form1_Activated(object? sender, EventArgs e)
        {
            if (CheckExternalChange())
            {
                var result = MessageBox.Show(
                    "The file has been modified by an external program. Reload?",
                    "External Change Detected",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    LoadFile(currentFilePath!);
                }
                else
                {
                    // Update our stored timestamp to the current file time to suppress further prompts
                    try { lastFileWriteTime = File.GetLastWriteTimeUtc(currentFilePath!); } catch { }
                }
            }
        }

        internal bool CheckExternalChange()
        {
            if (currentFilePath == null || !File.Exists(currentFilePath) || lastFileWriteTime == null)
                return false;

            try
            {
                var currentWrite = File.GetLastWriteTimeUtc(currentFilePath);
                return currentWrite != lastFileWriteTime;
            }
            catch
            {
                return false;
            }
        }

        private void ToggleFullScreen()
        {
            if (isFullScreen)
            {
                // Restore previous state
                this.FormBorderStyle = normalBorderStyle;
                this.WindowState = FormWindowState.Normal;
                if (normalWindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    this.Bounds = normalBounds;
                }
                this.TopMost = false;
                isFullScreen = false;
            }
            else
            {
                // Save current state
                normalBounds = this.Bounds;
                normalWindowState = this.WindowState;
                normalBorderStyle = this.FormBorderStyle;
                // Enter fullscreen: borderless, covers working area
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;
                this.Bounds = Screen.GetWorkingArea(this);
                this.TopMost = true;
                isFullScreen = true;
            }
        }

        // Current line highlight modes
        public enum CurrentLineHighlightMode
        {
            Off,
            NumberOnly,
            WholeLine
        }

        internal CurrentLineHighlightMode LineHighlightMode => currentLineHighlightMode;

        public bool IsDarkTheme => isDarkTheme;

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

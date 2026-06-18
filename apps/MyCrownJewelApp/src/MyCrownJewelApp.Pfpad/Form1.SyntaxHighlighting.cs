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
namespace MyCrownJewelApp.Pfpad
{
    public partial class Form1
    {
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

            // Use editorPanel.ClientSize.Width as the panel width reference — this is the
            // true current width of the parent panel at call time (correct even when called
            // from editorPanel.SizeChanged, where textEditor.Width may still be stale).
            int mw = minimapControl.MinimapWidth;
            int panelW = editorPanel.ClientSize.Width;
            if (panelW <= 0) return;   // panel not yet laid out; will be called again shortly

            // RichTextBox.ClientSize excludes non-client elements (vertical scrollbar).
            // Position the minimap within the text area so it never overlaps the scrollbar.
            // Fall back to panelW if textEditor hasn't completed its first layout pass.
            int textAreaW = textEditor.IsHandleCreated && textEditor.ClientSize.Width > 0
                ? textEditor.ClientSize.Width
                : panelW;

            int x = Math.Max(0, textAreaW - mw);

            // ClientSize.Height already excludes the horizontal scrollbar.
            int h = textEditor.ClientSize.Height;

            minimapControl.Location = new Point(x, textEditor.Top);
            minimapControl.Size = new Size(Math.Max(1, Math.Min(mw, textAreaW)), Math.Max(1, h));
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
            // Fill the entire client rect (covers COMCTL separator line and any unfilled gaps)
            using var bg = new SolidBrush(theme.MenuBackground);
            e.Graphics.FillRectangle(bg, tabControl.ClientRectangle);
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
        private TerminalTabBgWindow? _terminalTabStripWindow;

        private sealed class TabStripBackgroundWindow : NativeWindow
        {
            private const int WM_PAINT      = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;
            private const int WM_HSCROLL    = 0x0114;
            private const int SB_LINELEFT   = 0;
            private const int SB_LINERIGHT  = 1;
            private readonly Form1 _owner;
            private readonly TabControl _target;

            [DllImport("user32.dll")]
            private static extern bool ValidateRect(IntPtr hWnd, IntPtr lpRect);

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
                        m.Result = (IntPtr)1;   // prevent flicker; WM_PAINT fills everything
                        return;
                    case WM_PAINT:
                        // Validate dirty region so Windows doesn't loop WM_PAINT
                        ValidateRect(Handle, IntPtr.Zero);
                        // Paint entirely ourselves — zero COMCTL rendering, zero borders
                        using (var g = Graphics.FromHwnd(Handle))
                            _owner.PaintTabStrip(g);
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

        /// <summary>
        /// Lightweight NativeWindow subclass for the terminal tab strip.
        /// Fills the strip background with the theme colour on WM_ERASEBKGND,
        /// then lets WinForms handle WM_PAINT normally so DrawItem fires per tab.
        /// </summary>
        private sealed class TerminalTabBgWindow : NativeWindow
        {
            private const int WM_ERASEBKGND = 0x0014;
            private readonly ThemeManager _themeManager;

            public TerminalTabBgWindow(ThemeManager tm) => _themeManager = tm;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_ERASEBKGND)
                {
                    using var g = Graphics.FromHdc(m.WParam);
                    g.Clear(_themeManager.CurrentTheme.MenuBackground);
                    m.Result = (IntPtr)1;
                    return;
                }
                base.WndProc(ref m);
            }
        }


        #endregion

        /// <summary>
        /// TabControl subclass that applies TCS_BUTTONS via CreateParams.
        /// With TCS_BUTTONS there is no content-area border below the tab strip,
        /// so the tab buttons fill the exact control height with no reserved panel.
        /// </summary>
        private sealed class FlatButtonTabControl : TabControl
        {
            private const int TCS_BUTTONS      = 0x0100;
            private const int TCS_FLATBUTTONS  = 0x0008;
            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    // TCS_BUTTONS removes the content-area border entirely.
                    // TCS_FLATBUTTONS removes the inter-button gap/raised border.
                    cp.Style |= TCS_BUTTONS | TCS_FLATBUTTONS;
                    return cp;
                }
            }
        }

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

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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

        #endregion

    }
}

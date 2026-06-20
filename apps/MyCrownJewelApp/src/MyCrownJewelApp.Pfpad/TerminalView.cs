using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Custom terminal rendering control.  Replaces the RichTextBox + TextBox pair used in
/// the old TerminalPanel.  Renders a <see cref="TerminalBuffer"/> grid of fixed-width
/// cells using GDI+, handles keyboard input (translating to VT sequences) and exposes
/// mouse-drag text selection.
/// </summary>
internal sealed class TerminalView : Control
{
    // ── Fields ───────────────────────────────────────────────────────────────────

    private TerminalBuffer? _buf;
    private VtParser?       _parser;

    private Font   _font;
    private int    _cellW;
    private int    _cellH;

    private Color _defaultBg = Color.FromArgb(12, 12, 12);
    private Color _defaultFg = Color.FromArgb(204, 204, 204);

    // Cursor blink
    private readonly System.Windows.Forms.Timer _cursorTimer;
    private bool _cursorVisible = true;

    // Selection — stored as flat TerminalCell indices in the combined scrollback+screen view
    private bool _selecting;
    private int  _selStart = -1;
    private int  _selEnd   = -1;

    // Scrollbar
    private readonly VScrollBar _scrollBar;

    // Context menu (wired by TerminalPanel)
    // (Uses the inherited ContextMenuStrip property)

    /// <summary>Fired when the user presses Ctrl+Shift+V with clipboard text ready to paste.</summary>
    public event Action<string>? PasteRequested;
    /// <summary>Fired when Ctrl+Shift+N is pressed — request a new terminal tab.</summary>
    public event Action? NewTabRequested;
    /// <summary>Fired when Ctrl+F4 is pressed — request to close this terminal tab.</summary>
    public event Action? CloseTabRequested;

    // ── Events ───────────────────────────────────────────────────────────────────

    /// <summary>Fired when the user types — bytes to write to the ConPTY stdin pipe.</summary>
    public event Action<byte[]>? DataToSend;

    // ── Construction ─────────────────────────────────────────────────────────────

    public TerminalView()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint             |
            ControlStyles.AllPaintingInWmPaint  |
            ControlStyles.Selectable,
            true);
        TabStop = true;

        _font  = new Font("Cascadia Mono", 10f, FontStyle.Regular, GraphicsUnit.Point);
        MeasureCell();

        _scrollBar = new VScrollBar
        {
            Dock    = DockStyle.Right,
            Visible = false,
        };
        _scrollBar.Scroll += (_, e) =>
        {
            if (_buf != null)
            {
                _buf.ScrollOffset = _scrollBar.Maximum - e.NewValue;
                Invalidate();
            }
        };
        Controls.Add(_scrollBar);

        _cursorTimer = new System.Windows.Forms.Timer { Interval = 530 };
        _cursorTimer.Tick += (_, _) => { _cursorVisible = !_cursorVisible; InvalidateCursor(); };
        _cursorTimer.Start();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Attach(TerminalBuffer buffer, VtParser parser)
    {
        _buf    = buffer;
        _parser = parser;
        _selStart = _selEnd = -1;
        _scrollBar.Visible = false;
        Invalidate();
    }

    public void SetTheme(Color bg, Color fg)
    {
        _defaultBg = bg;
        _defaultFg = fg;
        BackColor  = bg;
        Invalidate();
    }

    public void SetFont(string face, float size, bool bold)
    {
        float pt = size <= 0f ? 11f : Math.Clamp(size, 6f, 72f);
        var newFont = new Font(
            string.IsNullOrWhiteSpace(face) ? "Cascadia Mono" : face,
            pt,
            bold ? FontStyle.Bold : FontStyle.Regular,
            GraphicsUnit.Point);

        Font old = _font;
        _font = newFont;
        old.Dispose();
        MeasureCell();
        Invalidate();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasSelection => _selStart >= 0 && _selEnd >= 0 && _selStart != _selEnd;

    public string GetSelectedText()
    {
        if (_buf == null || !HasSelection) return string.Empty;

        int a = Math.Min(_selStart, _selEnd);
        int b = Math.Max(_selStart, _selEnd);

        int cols = _buf.Cols;
        var sb = new StringBuilder();
        int lastRowWritten = -1;

        for (int idx = a; idx <= b; idx++)
        {
            var (row, col) = FlatToCell(idx);

            // Insert newline when we cross a row boundary
            if (lastRowWritten >= 0 && row != lastRowWritten)
                sb.Append('\n');
            lastRowWritten = row;

            sb.Append(GetCellChar(row, col));
        }

        // Trim trailing spaces on each line for clean output
        string raw = sb.ToString();
        var lines = raw.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd();
        return string.Join('\n', lines).TrimEnd();
    }

    public void ClearSelection()
    {
        _selStart = _selEnd = -1;
        Invalidate();
    }

    /// <summary>Called by TerminalPanel after the buffer is mutated to trigger a repaint.</summary>
    public void Refresh(bool scrollToBottom)
    {
        if (_buf == null) return;
        if (scrollToBottom) _buf.ScrollOffset = 0;
        UpdateScrollBar();
        Invalidate();
        Update();   // force immediate paint so input echoes appear without waiting for next message pump cycle
    }

    // ── Painting ─────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        if (_buf == null || _cellW <= 0 || _cellH <= 0)
        {
            g.Clear(_defaultBg);
            return;
        }

        g.Clear(_defaultBg);

        int viewRows = ViewRows;
        int cols     = _buf.Cols;

        // Determine which rows to render
        int sbLines  = _buf.Scrollback.Count;
        int offset   = _buf.ScrollOffset;         // 0 = live, >0 = scrolled up
        // Total "virtual" rows above live screen: sbLines
        // We display viewRows worth: from (sbLines - offset - viewRows) .. (sbLines - offset + liveRows - 1)
        // Simplify: show scrollback lines (newest first) then live screen

        using var brush = new SolidBrush(_defaultFg);

        for (int vr = 0; vr < viewRows; vr++)
        {
            int y = vr * _cellH;
            // Map virtual row to actual data
            var (isScrollback, dataRow) = MapVirtualRow(vr, viewRows, sbLines, offset);

            if (isScrollback)
            {
                TerminalCell[] sbRow = _buf.Scrollback[dataRow];
                RenderRowFromScrollback(g, brush, sbRow, vr, y, cols);
            }
            else
            {
                RenderRowFromGrid(g, brush, _buf.ActiveGrid, dataRow, vr, y, cols);
            }
        }

        // Draw cursor (only in live view, only when DECTCEM says it's visible)
        if (offset == 0 && _cursorVisible && Focused && !_buf.CursorHidden)
        {
            int cr = _buf.CursorRow;
            int cc = _buf.CursorCol;
            if (cr < viewRows && cc < cols)
            {
                var cursorRect = new Rectangle(cc * _cellW, cr * _cellH, _cellW, _cellH);
                using var cursorBrush = new SolidBrush(Color.FromArgb(160, _defaultFg));
                g.FillRectangle(cursorBrush, cursorRect);

                // Draw the character on top in inverted color
                ref var TerminalCell = ref _buf.ActiveGrid[cr, cc];
                if (TerminalCell.Char > ' ')
                {
                    using var invBrush = new SolidBrush(_defaultBg);
                    DrawChar(g, TerminalCell.Char, cc * _cellW, cr * _cellH, invBrush, _font);
                }
            }
        }
    }

    private void RenderRowFromScrollback(Graphics g, SolidBrush defaultBrush,
        TerminalCell[] row, int vr, int y, int cols)
    {
        // Scrollback rows may be shorter than the current buffer width after a resize.
        int rowCols = Math.Min(cols, row.Length);
        if (rowCols == 0) return;

        int x = 0;
        int runStart = 0;
        Color runFg = row[0].Fg, runBg = row[0].Bg;

        for (int c = 0; c <= rowCols; c++)
        {
            bool flush = c == rowCols;
            if (!flush)
            {
                Color cellFg = row[c].Fg;
                Color cellBg = row[c].Bg;
                if (row[c].Reverse) (cellFg, cellBg) = (cellBg, cellFg);
                flush = cellFg != runFg || cellBg != runBg;
                if (!flush) continue;
                FlushScrollbackRun(g, defaultBrush, row, runStart, c - 1, y, runBg, runFg);
                runStart = c; runFg = cellFg; runBg = cellBg;
            }
            else
            {
                FlushScrollbackRun(g, defaultBrush, row, runStart, c - 1, y, runBg, runFg);
            }
        }

        // Selection highlight
        HighlightSelection(g, vr, cols, y);
    }

    private void FlushScrollbackRun(Graphics g, SolidBrush defaultBrush,
        TerminalCell[] row, int from, int to, int y, Color bg, Color fg)
    {
        if (from > to) return;
        int x = from * _cellW;
        int w = (to - from + 1) * _cellW;
        if (bg != Color.Empty && bg != _defaultBg)
        {
            using var bgBrush = new SolidBrush(bg);
            g.FillRectangle(bgBrush, x, y, w, _cellH);
        }
        using var fgBrush = new SolidBrush(fg);
        for (int c = from; c <= to; c++)
        {
            if (row[c].Char > ' ')
                DrawChar(g, row[c].Char, c * _cellW, y, fgBrush, row[c].Bold ? BoldFont() : _font);
        }
    }

    private void RenderRowFromGrid(Graphics g, SolidBrush defaultBrush,
        TerminalCell[,] grid, int dataRow, int vr, int y, int cols)
    {
        if (dataRow < 0 || dataRow >= _buf!.Rows) return;

        // Render color runs for efficiency
        int  runStart = 0;
        Color runFg = grid[dataRow, 0].Fg;
        Color runBg = grid[dataRow, 0].Bg;
        bool  runRev = grid[dataRow, 0].Reverse;
        if (runRev) (runFg, runBg) = (runBg, runFg);

        for (int c = 1; c <= cols; c++)
        {
            bool flush = c == cols;
            if (!flush)
            {
                Color cellFg = grid[dataRow, c].Fg;
                Color cellBg = grid[dataRow, c].Bg;
                bool  cellRev = grid[dataRow, c].Reverse;
                if (cellRev) (cellFg, cellBg) = (cellBg, cellFg);
                flush = cellFg != runFg || cellBg != runBg;
                if (!flush) continue;
                FlushGridRun(g, grid, dataRow, runStart, c - 1, y, runBg, runFg);
                runStart = c; runFg = cellFg; runBg = cellBg;
            }
            else
            {
                FlushGridRun(g, grid, dataRow, runStart, c - 1, y, runBg, runFg);
            }
        }

        HighlightSelection(g, vr, cols, y);
    }

    private void FlushGridRun(Graphics g, TerminalCell[,] grid, int row,
        int from, int to, int y, Color bg, Color fg)
    {
        if (from > to) return;
        int x = from * _cellW;
        int w = (to - from + 1) * _cellW;
        if (bg != Color.Empty && bg != _defaultBg)
        {
            using var bgBrush = new SolidBrush(bg);
            g.FillRectangle(bgBrush, x, y, w, _cellH);
        }
        using var fgBrush = new SolidBrush(fg);
        for (int c = from; c <= to; c++)
        {
            char ch = grid[row, c].Char;
            if (ch > ' ')
                DrawChar(g, ch, c * _cellW, y, fgBrush, grid[row, c].Bold ? BoldFont() : _font);
            if (grid[row, c].Underline)
            {
                int uy = y + _cellH - 1;
                g.DrawLine(Pens.White, c * _cellW, uy, (c + 1) * _cellW, uy);
            }
        }
    }

    private void HighlightSelection(Graphics g, int vr, int cols, int y)
    {
        if (!HasSelection) return;
        int a = Math.Min(_selStart, _selEnd);
        int b = Math.Max(_selStart, _selEnd);

        for (int c = 0; c < cols; c++)
        {
            int idx = CellToFlat(vr, c);
            if (idx >= a && idx <= b)
            {
                using var hlBrush = new SolidBrush(Color.FromArgb(80, 120, 200, 255));
                g.FillRectangle(hlBrush, c * _cellW, y, _cellW, _cellH);
            }
        }
    }

    private void DrawChar(Graphics g, char ch, int x, int y, SolidBrush brush, Font font)
    {
        TextRenderer.DrawText(g, ch.ToString(), font,
            new Point(x, y), brush.Color,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    // ── TerminalCell/row mapping ─────────────────────────────────────────────────────────

    private (bool isScrollback, int dataRow) MapVirtualRow(int vr, int viewRows, int sbLines, int offset)
    {
        if (_buf == null) return (false, vr);

        if (offset == 0)
            // Live view: virtual rows map directly to buffer rows
            return (false, vr);

        // Scrolled up: show scrollback lines filling the viewport
        // The "bottom" of the view corresponds to scrollback[sbLines-offset + liveRow]
        // We treat the viewport as a window into (scrollback ++ live):
        int liveRows = _buf.Rows;
        int totalVirtual = sbLines + liveRows;
        int viewTop = totalVirtual - offset - viewRows;
        int absRow  = viewTop + vr;

        if (absRow < 0) return (false, -1);              // above all data
        if (absRow < sbLines) return (true, absRow);      // in scrollback
        return (false, absRow - sbLines);                 // in live buffer
    }

    private int  CellToFlat(int row, int col) => _buf == null ? 0 : row * _buf.Cols + col;
    private (int row, int col) FlatToCell(int idx) => _buf == null ? (0, 0) : (idx / _buf.Cols, idx % _buf.Cols);

    private char GetCellChar(int row, int col)
    {
        if (_buf == null) return ' ';
        int sbLines = _buf.Scrollback.Count;
        if (row < sbLines)
        {
            var sbRow = _buf.Scrollback[row];
            if (col >= sbRow.Length) return ' ';
            char ch = sbRow[col].Char;
            return ch == '\0' ? ' ' : ch;
        }
        int liveRow = row - sbLines;
        if (liveRow >= 0 && liveRow < _buf.Rows && col < _buf.Cols)
        {
            char ch = _buf.ActiveGrid[liveRow, col].Char;
            return ch == '\0' ? ' ' : ch;
        }
        return ' ';
    }

    // ── Keyboard input ───────────────────────────────────────────────────────────

    protected override bool IsInputKey(Keys keyData)
    {
        // Capture all keys so WinForms doesn't intercept arrows, Tab, etc.
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _cursorVisible = true;
        _cursorTimer.Stop();
        _cursorTimer.Start();

        bool ctrl  = (e.Modifiers & Keys.Control) != 0;
        bool shift = (e.Modifiers & Keys.Shift)   != 0;

        // ── Terminal-level shortcuts (not forwarded to PTY/shell) ────────────────

        if (ctrl && shift)
        {
            switch (e.KeyCode)
            {
                case Keys.C:  // copy selection (or all when nothing selected)
                    if (HasSelection) CopySelectionToClipboard();
                    else CopyAllToClipboard();
                    e.Handled = e.SuppressKeyPress = true;
                    return;
                case Keys.V:  // paste from clipboard
                    string clipText = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                    if (clipText.Length > 0) PasteRequested?.Invoke(clipText);
                    e.Handled = e.SuppressKeyPress = true;
                    return;
                case Keys.N:  // new terminal tab
                    NewTabRequested?.Invoke();
                    e.Handled = e.SuppressKeyPress = true;
                    return;
            }
        }

        if (ctrl && !shift && e.KeyCode == Keys.F4)
        {
            CloseTabRequested?.Invoke();
            e.Handled = e.SuppressKeyPress = true;
            return;
        }

        // ── VT translation + forward to shell ────────────────────────────────────

        bool app = _parser?.ApplicationCursorKeys ?? false;
        byte[]? seq = TranslateKey(e.KeyCode, e.Modifiers, app);
        if (seq != null)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DataToSend?.Invoke(seq);
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        char ch = e.KeyChar;
        // '\b' (backspace) is handled by OnKeyDown (Keys.Back → 0x7F).
        // Including it here causes a double-send, so exclude it.
        if (ch >= ' ' || ch == '\r' || ch == '\t')
        {
            e.Handled = true;
            DataToSend?.Invoke(Encoding.UTF8.GetBytes(ch.ToString()));
        }
    }

    private static byte[]? TranslateKey(Keys key, Keys mods, bool appCursor)
    {
        bool ctrl  = (mods & Keys.Control) != 0;
        bool shift = (mods & Keys.Shift)   != 0;

        if (ctrl && !shift)
        {
            switch (key)
            {
                case Keys.C: return "\x03"u8.ToArray();
                case Keys.D: return "\x04"u8.ToArray();
                case Keys.L: return "\x0C"u8.ToArray();
                case Keys.Z: return "\x1A"u8.ToArray();
                case Keys.A: return "\x01"u8.ToArray();
                case Keys.E: return "\x05"u8.ToArray();
                case Keys.U: return "\x15"u8.ToArray();
                case Keys.K: return "\x0B"u8.ToArray();
                case Keys.W: return "\x17"u8.ToArray();
                case Keys.R: return "\x12"u8.ToArray();
                // Ctrl+A..Z generic fallback
                default:
                    if (key >= Keys.A && key <= Keys.Z)
                        return new[] { (byte)(key - Keys.A + 1) };
                    break;
            }
        }

        switch (key)
        {
            case Keys.Up:     return Encode(appCursor ? "\x1BOA" : "\x1B[A");
            case Keys.Down:   return Encode(appCursor ? "\x1BOB" : "\x1B[B");
            case Keys.Right:  return Encode(appCursor ? "\x1BOC" : "\x1B[C");
            case Keys.Left:   return Encode(appCursor ? "\x1BOD" : "\x1B[D");
            case Keys.Home:   return Encode(appCursor ? "\x1BOH" : "\x1B[H");
            case Keys.End:    return Encode(appCursor ? "\x1BOF" : "\x1B[F");
            case Keys.Prior:  return Encode("\x1B[5~");   // Page Up
            case Keys.Next:   return Encode("\x1B[6~");   // Page Down
            case Keys.Insert: return Encode("\x1B[2~");
            case Keys.Delete: return Encode("\x1B[3~");
            case Keys.Tab when shift: return Encode("\x1B[Z");
            case Keys.Escape: return Encode("\x1B");
            case Keys.Back:   return new[] { (byte)0x7F };
            case Keys.Enter:  return "\r"u8.ToArray();
            case Keys.F1:  return Encode("\x1BOP");
            case Keys.F2:  return Encode("\x1BOQ");
            case Keys.F3:  return Encode("\x1BOR");
            case Keys.F4:  return Encode("\x1BOS");
            case Keys.F5:  return Encode("\x1B[15~");
            case Keys.F6:  return Encode("\x1B[17~");
            case Keys.F7:  return Encode("\x1B[18~");
            case Keys.F8:  return Encode("\x1B[19~");
            case Keys.F9:  return Encode("\x1B[20~");
            case Keys.F10: return Encode("\x1B[21~");
            case Keys.F11: return Encode("\x1B[23~");
            case Keys.F12: return Encode("\x1B[24~");
        }
        return null;
    }

    private static byte[] Encode(string s) => Encoding.UTF8.GetBytes(s);

    // ── Mouse (selection) ────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            _selecting = true;
            _selStart  = HitTest(e.X, e.Y);
            _selEnd    = _selStart;
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_selecting)
        {
            _selEnd = HitTest(e.X, e.Y);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _selecting = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (_buf == null || _cellW <= 0) return;
        int flat   = HitTest(e.X, e.Y);
        var (row, col) = FlatToCell(flat);
        char ch    = GetCellChar(row, col);
        if (!char.IsLetterOrDigit(ch) && ch != '_') return;

        // Expand left
        int left = col;
        while (left > 0 && IsWordChar(GetCellChar(row, left - 1))) left--;
        // Expand right
        int right = col;
        int cols  = _buf.Cols;
        while (right < cols - 1 && IsWordChar(GetCellChar(row, right + 1))) right++;

        _selStart = CellToFlat(row, left);
        _selEnd   = CellToFlat(row, right);
        Invalidate();
    }

    private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.' || ch == '-';

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_buf == null) return;
        int delta = e.Delta > 0 ? 3 : -3;
        _buf.ScrollOffset = Math.Clamp(_buf.ScrollOffset + delta, 0, _buf.Scrollback.Count);
        UpdateScrollBar();
        Invalidate();
    }

    private int HitTest(int px, int py)
    {
        if (_buf == null || _cellW <= 0 || _cellH <= 0) return 0;
        int col = Math.Clamp(px / _cellW, 0, _buf.Cols - 1);
        int row = Math.Clamp(py / _cellH, 0, ViewRows - 1);
        return CellToFlat(row, col);
    }

    // ── Scrollbar ────────────────────────────────────────────────────────────────

    private void UpdateScrollBar()
    {
        if (!IsHandleCreated) return;
        if (_buf == null) { _scrollBar.Visible = false; return; }
        int sbCount = _buf.Scrollback.Count;
        if (sbCount == 0) { _scrollBar.Visible = false; return; }
        _scrollBar.Minimum    = 0;
        _scrollBar.Maximum    = sbCount;
        _scrollBar.SmallChange = 1;
        _scrollBar.LargeChange = Math.Max(1, ViewRows);  // must set before Value
        _scrollBar.Value      = Math.Clamp(sbCount - _buf.ScrollOffset, 0, Math.Max(0, sbCount - _scrollBar.LargeChange + 1));
        _scrollBar.Visible    = true;
    }

    // ── Layout / resize ───────────────────────────────────────────────────────────

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)  { base.OnGotFocus(e);  _cursorTimer.Start(); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _cursorTimer.Stop();  Invalidate(); }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private int ViewRows => _cellH > 0 ? Math.Max(1, ClientSize.Height / _cellH) : 24;

    public int CellWidth  => _cellW;
    public int CellHeight => _cellH;

    private void MeasureCell()
    {
        Size s;
        if (IsHandleCreated)
        {
            using var g = CreateGraphics();
            s = TextRenderer.MeasureText(g, "W", _font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        else
        {
            s = TextRenderer.MeasureText("W", _font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
        _cellW = Math.Max(1, s.Width);
        _cellH = Math.Max(1, _font.Height);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        MeasureCell();
        Invalidate();
    }

    private void InvalidateCursor()
    {
        if (!IsHandleCreated || _buf == null || _cellW <= 0 || _cellH <= 0) return;
        int cr = _buf.CursorRow;
        int cc = _buf.CursorCol;
        Invalidate(new Rectangle(cc * _cellW, cr * _cellH, _cellW, _cellH));
    }

    private Font? _boldFont;
    private Font BoldFont()
    {
        if (_boldFont == null || _boldFont.Size != _font.Size || _boldFont.FontFamily != _font.FontFamily)
        {
            _boldFont?.Dispose();
            _boldFont = new Font(_font, FontStyle.Bold);
        }
        return _boldFont;
    }

    // ── WndProc — capture Ctrl+Shift+C for copy ──────────────────────────────────

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
    }

    // ── Clipboard helpers (called by TerminalPanel) ───────────────────────────────

    public void CopySelectionToClipboard()
    {
        string text = GetSelectedText();
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    public void CopyAllToClipboard()
    {
        if (_buf == null) return;
        string text = ExtractAllText(_buf);
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    /// <summary>
    /// Extracts the complete terminal history: scrollback (oldest → newest) followed
    /// by the live screen rows.  Trailing spaces on each row are stripped; the
    /// overall result is trimmed at the end.
    /// </summary>
    private static string ExtractAllText(TerminalBuffer buf)
    {
        var sb = new StringBuilder();

        // ── Scrollback (oldest = index 0) ────────────────────────────────────────
        foreach (var row in buf.Scrollback)
        {
            int last = row.Length - 1;
            while (last >= 0 && (row[last].Char == '\0' || row[last].Char == ' ')) last--;
            for (int c = 0; c <= last; c++)
                sb.Append(row[c].Char == '\0' ? ' ' : row[c].Char);
            sb.Append('\n');
        }

        // ── Live screen ───────────────────────────────────────────────────────────
        for (int r = 0; r < buf.Rows; r++)
        {
            int last = buf.Cols - 1;
            while (last >= 0 && (buf.ActiveGrid[r, last].Char == '\0' || buf.ActiveGrid[r, last].Char == ' ')) last--;
            for (int c = 0; c <= last; c++)
            {
                char ch = buf.ActiveGrid[r, c].Char;
                sb.Append(ch == '\0' ? ' ' : ch);
            }
            if (r < buf.Rows - 1) sb.Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    public void SelectAll()
    {
        if (_buf == null) return;
        int totalRows = _buf.Scrollback.Count + _buf.Rows;
        _selStart = 0;
        _selEnd   = Math.Max(0, totalRows * _buf.Cols - 1);
        Invalidate();
    }

    // ── Dispose ──────────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cursorTimer.Stop();
            _cursorTimer.Dispose();
            _font.Dispose();
            _boldFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}

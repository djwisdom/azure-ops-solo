using System.Drawing;

namespace MyCrownJewelApp.Pfpad;

internal struct TerminalCell
{
    public char  Char;
    public Color Fg;
    public Color Bg;
    public bool  Bold;
    public bool  Underline;
    public bool  Reverse;
}

/// <summary>
/// A 2-D grid of terminal cells with primary/alternate screens, scrollback ring buffer,
/// cursor state, and scroll-region support.  All mutations happen on the UI thread (via
/// BeginInvoke in TerminalPanel) so no locking is required.
/// </summary>
internal sealed class TerminalBuffer
{
    // ── Fields ───────────────────────────────────────────────────────────────────

    private TerminalCell[,] _primary;
    private TerminalCell[,] _alternate;
    private bool    _useAlternate;

    private int _savedPriRow, _savedPriCol;
    private int _savedAltRow, _savedAltCol;

    private int _scrollTop;
    private int _scrollBottom;

    private bool _pendingWrap;

    private Color _defaultFg;
    private Color _defaultBg;

    private readonly List<TerminalCell[]> _scrollback = new();
    private int _maxScrollback = 5000;
    private int _scrollOffset;  // 0 = live view

    // ── Properties ──────────────────────────────────────────────────────────────

    public int Rows { get; private set; }
    public int Cols { get; private set; }

    public int CursorRow { get; private set; }
    public int CursorCol { get; private set; }

    public Color CurrentFg  { get; set; }
    public Color CurrentBg  { get; set; }
    public bool  CurrentBold      { get; set; }
    public bool  CurrentUnderline { get; set; }
    public bool  CurrentReverse   { get; set; }

    public bool AutoWrap { get; set; } = true;

    public bool CursorHidden { get; set; }  // set by DECTCEM (CSI ? 25 l/h)

    public Color DefaultFg => _defaultFg;
    public Color DefaultBg => _defaultBg;

    public TerminalCell[,] ActiveGrid => _useAlternate ? _alternate : _primary;
    public bool IsAlternate   => _useAlternate;

    public IReadOnlyList<TerminalCell[]> Scrollback => _scrollback;

    public int ScrollOffset
    {
        get => _scrollOffset;
        set => _scrollOffset = Math.Clamp(value, 0, _scrollback.Count);
    }

    // ── Construction ─────────────────────────────────────────────────────────────

    public TerminalBuffer(int rows, int cols, Color defaultFg, Color defaultBg)
    {
        Rows = rows;
        Cols = cols;
        _defaultFg = defaultFg;
        _defaultBg = defaultBg;
        CurrentFg  = defaultFg;
        CurrentBg  = defaultBg;
        _primary   = new TerminalCell[rows, cols];
        _alternate = new TerminalCell[rows, cols];
        _scrollTop    = 0;
        _scrollBottom = rows - 1;
        FillGrid(_primary,   rows, cols);
        FillGrid(_alternate, rows, cols);
    }

    // ── Default-colour management ────────────────────────────────────────────────

    public void UpdateDefaultColors(Color fg, Color bg)
    {
        _defaultFg = fg;
        _defaultBg = bg;
        // Keep current SGR defaults in sync if they were still the old defaults
        if (CurrentFg == fg || CurrentFg == _defaultFg) CurrentFg = fg;
        if (CurrentBg == bg || CurrentBg == _defaultBg) CurrentBg = bg;
    }

    // ── Write operations ─────────────────────────────────────────────────────────

    public void WriteChar(char ch)
    {
        if (_pendingWrap && AutoWrap)
        {
            // Flush pending wrap: move to next row (scroll if at bottom of region)
            if (CursorRow == _scrollBottom)
                ScrollUp(1);
            else
                CursorRow++;
            CursorCol    = 0;
            _pendingWrap = false;
        }

        int row = Math.Clamp(CursorRow, 0, Rows - 1);
        int col = Math.Clamp(CursorCol, 0, Cols - 1);

        ref TerminalCell TerminalCell = ref ActiveGrid[row, col];
        TerminalCell.Char      = ch;
        TerminalCell.Fg        = CurrentFg;
        TerminalCell.Bg        = CurrentBg;
        TerminalCell.Bold      = CurrentBold;
        TerminalCell.Underline = CurrentUnderline;
        TerminalCell.Reverse   = CurrentReverse;

        if (CursorCol >= Cols - 1)
            _pendingWrap = AutoWrap;
        else
            CursorCol++;
    }

    // ── Cursor movement ───────────────────────────────────────────────────────────

    public void SetCursor(int row, int col)
    {
        _pendingWrap = false;
        CursorRow = Math.Clamp(row, 0, Rows - 1);
        CursorCol = Math.Clamp(col, 0, Cols - 1);
    }

    public void SetCursorRow(int row) => SetCursor(Math.Clamp(row, 0, Rows - 1), CursorCol);
    public void SetCursorCol(int col) => SetCursor(CursorRow, Math.Clamp(col, 0, Cols - 1));

    public void MoveCursorUp(int n)    { _pendingWrap = false; CursorRow = Math.Max(_scrollTop,    CursorRow - n); }
    public void MoveCursorDown(int n)  { _pendingWrap = false; CursorRow = Math.Min(_scrollBottom, CursorRow + n); }
    public void MoveCursorRight(int n) { _pendingWrap = false; CursorCol = Math.Min(Cols - 1,      CursorCol + n); }
    public void MoveCursorLeft(int n)  { _pendingWrap = false; CursorCol = Math.Max(0,             CursorCol - n); }

    // ── C0 control handlers ───────────────────────────────────────────────────────

    public void CarriageReturn() { _pendingWrap = false; CursorCol = 0; }

    public void LineFeed()
    {
        _pendingWrap = false;
        if (CursorRow == _scrollBottom)
            ScrollUp(1);
        else
            CursorRow = Math.Min(Rows - 1, CursorRow + 1);
    }

    public void ReverseLineFeed()
    {
        _pendingWrap = false;
        if (CursorRow == _scrollTop)
            ScrollDown(1);
        else
            CursorRow = Math.Max(0, CursorRow - 1);
    }

    public void Backspace() { _pendingWrap = false; CursorCol = Math.Max(0, CursorCol - 1); }

    public void Tab()
    {
        _pendingWrap = false;
        CursorCol = Math.Min(Cols - 1, ((CursorCol / 8) + 1) * 8);
    }

    // ── Erase operations ──────────────────────────────────────────────────────────

    public void EraseInLine(int mode)
    {
        TerminalCell[,] g = ActiveGrid;
        int row = CursorRow;
        switch (mode)
        {
            case 0: for (int c = CursorCol; c < Cols; c++) Blank(ref g[row, c]); break;
            case 1: for (int c = 0; c <= CursorCol; c++) Blank(ref g[row, c]); break;
            case 2: for (int c = 0; c < Cols; c++) Blank(ref g[row, c]); break;
        }
    }

    public void EraseInDisplay(int mode)
    {
        TerminalCell[,] g = ActiveGrid;
        switch (mode)
        {
            case 0:
                EraseInLine(0);
                for (int r = CursorRow + 1; r < Rows; r++)
                    for (int c = 0; c < Cols; c++) Blank(ref g[r, c]);
                break;
            case 1:
                for (int r = 0; r < CursorRow; r++)
                    for (int c = 0; c < Cols; c++) Blank(ref g[r, c]);
                EraseInLine(1);
                break;
            case 2:
            case 3:
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++) Blank(ref g[r, c]);
                if (mode == 2) SetCursor(0, 0);
                break;
        }
    }

    public void EraseChars(int n)
    {
        TerminalCell[,] g = ActiveGrid;
        int row = CursorRow;
        for (int i = 0; i < n && CursorCol + i < Cols; i++)
            Blank(ref g[row, CursorCol + i]);
    }

    // ── Insert / delete ───────────────────────────────────────────────────────────

    public void InsertLines(int n)
    {
        _pendingWrap = false;
        int row = CursorRow;
        if (row < _scrollTop || row > _scrollBottom) return;
        n = Math.Min(n, _scrollBottom - row + 1);
        TerminalCell[,] g = ActiveGrid;
        for (int r = _scrollBottom; r >= row + n; r--)
            CopyRow(g, r - n, r);
        for (int r = row; r < row + n; r++)
            for (int c = 0; c < Cols; c++) Blank(ref g[r, c]);
    }

    public void DeleteLines(int n)
    {
        _pendingWrap = false;
        int row = CursorRow;
        if (row < _scrollTop || row > _scrollBottom) return;
        n = Math.Min(n, _scrollBottom - row + 1);
        TerminalCell[,] g = ActiveGrid;
        for (int r = row; r <= _scrollBottom - n; r++)
            CopyRow(g, r + n, r);
        for (int r = _scrollBottom - n + 1; r <= _scrollBottom; r++)
            for (int c = 0; c < Cols; c++) Blank(ref g[r, c]);
    }

    public void InsertChars(int n)
    {
        _pendingWrap = false;
        n = Math.Min(n, Cols - CursorCol);
        TerminalCell[,] g = ActiveGrid;
        int row = CursorRow;
        for (int c = Cols - 1; c >= CursorCol + n; c--)
            g[row, c] = g[row, c - n];
        for (int c = CursorCol; c < CursorCol + n; c++) Blank(ref g[row, c]);
    }

    public void DeleteChars(int n)
    {
        _pendingWrap = false;
        n = Math.Min(n, Cols - CursorCol);
        TerminalCell[,] g = ActiveGrid;
        int row = CursorRow;
        for (int c = CursorCol; c < Cols - n; c++)
            g[row, c] = g[row, c + n];
        for (int c = Cols - n; c < Cols; c++) Blank(ref g[row, c]);
    }

    // ── Scrolling ────────────────────────────────────────────────────────────────

    public void ScrollUp(int n)
    {
        TerminalCell[,] g = ActiveGrid;
        for (int i = 0; i < n; i++)
        {
            // Push top line of scroll region into scrollback (primary only, full-screen scroll)
            if (!_useAlternate && _scrollTop == 0)
            {
                var line = new TerminalCell[Cols];
                for (int c = 0; c < Cols; c++) line[c] = g[0, c];
                _scrollback.Add(line);
                if (_scrollback.Count > _maxScrollback)
                    _scrollback.RemoveAt(0);
            }

            for (int r = _scrollTop; r < _scrollBottom; r++)
                CopyRow(g, r + 1, r);
            for (int c = 0; c < Cols; c++) Blank(ref g[_scrollBottom, c]);
        }
    }

    public void ScrollDown(int n)
    {
        TerminalCell[,] g = ActiveGrid;
        for (int i = 0; i < n; i++)
        {
            for (int r = _scrollBottom; r > _scrollTop; r--)
                CopyRow(g, r - 1, r);
            for (int c = 0; c < Cols; c++) Blank(ref g[_scrollTop, c]);
        }
    }

    // ── Scroll region ────────────────────────────────────────────────────────────

    public void SetScrollRegion(int top, int bottom)
    {
        _scrollTop    = Math.Clamp(top, 0, Rows - 1);
        _scrollBottom = Math.Clamp(bottom, 0, Rows - 1);
        if (_scrollTop >= _scrollBottom) { _scrollTop = 0; _scrollBottom = Rows - 1; }
        SetCursor(0, 0);
    }

    // ── Alternate screen ──────────────────────────────────────────────────────────

    public void SwitchToAlternate()
    {
        if (_useAlternate) return;
        _savedPriRow = CursorRow;
        _savedPriCol = CursorCol;
        _useAlternate = true;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++) Blank(ref _alternate[r, c]);
        _scrollTop    = 0;
        _scrollBottom = Rows - 1;
        SetCursor(0, 0);
    }

    public void SwitchToPrimary()
    {
        if (!_useAlternate) return;
        _useAlternate = false;
        _scrollTop    = 0;
        _scrollBottom = Rows - 1;
        _scrollOffset = 0;
        SetCursor(_savedPriRow, _savedPriCol);
    }

    // ── Cursor save / restore ────────────────────────────────────────────────────

    public void SaveCursor()
    {
        if (_useAlternate) { _savedAltRow = CursorRow; _savedAltCol = CursorCol; }
        else               { _savedPriRow = CursorRow; _savedPriCol = CursorCol; }
    }

    public void RestoreCursor()
    {
        if (_useAlternate) SetCursor(_savedAltRow, _savedAltCol);
        else               SetCursor(_savedPriRow, _savedPriCol);
    }

    // ── Resize ───────────────────────────────────────────────────────────────────

    public void Resize(int newRows, int newCols)
    {
        var newPrimary   = new TerminalCell[newRows, newCols];
        var newAlternate = new TerminalCell[newRows, newCols];
        FillGrid(newPrimary,   newRows, newCols);
        FillGrid(newAlternate, newRows, newCols);

        // Copy primary — bottom-align so the most recent content is visible
        int srcStart  = Math.Max(0, Rows - newRows);
        int dstStart  = Math.Max(0, newRows - Rows);
        for (int r = srcStart; r < Rows; r++)
        {
            int dr = r - srcStart + dstStart;
            for (int c = 0; c < Math.Min(Cols, newCols); c++)
                newPrimary[dr, c] = _primary[r, c];
        }

        // Copy alternate (top-align)
        for (int r = 0; r < Math.Min(Rows, newRows); r++)
            for (int c = 0; c < Math.Min(Cols, newCols); c++)
                newAlternate[r, c] = _alternate[r, c];

        _primary   = newPrimary;
        _alternate = newAlternate;
        Rows = newRows;
        Cols = newCols;

        _scrollTop    = 0;
        _scrollBottom = newRows - 1;
        CursorRow = Math.Clamp(CursorRow, 0, newRows - 1);
        CursorCol = Math.Clamp(CursorCol, 0, newCols - 1);
        _pendingWrap = false;
    }

    // ── Scrollback control ────────────────────────────────────────────────────────

    public void SetMaxScrollback(int lines)
    {
        _maxScrollback = Math.Max(500, lines);
        while (_scrollback.Count > _maxScrollback)
            _scrollback.RemoveAt(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void Blank(ref TerminalCell cell)
    {
        cell.Char      = ' ';
        cell.Fg        = _defaultFg;
        cell.Bg        = Color.Empty;   // sentinel: render with current _defaultBg, not a hardcoded color
        cell.Bold      = false;
        cell.Underline = false;
        cell.Reverse   = false;
    }

    private static void CopyRow(TerminalCell[,] g, int src, int dst)
    {
        int cols = g.GetLength(1);
        for (int c = 0; c < cols; c++)
            g[dst, c] = g[src, c];
    }

    private void FillGrid(TerminalCell[,] g, int rows, int cols)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                g[r, c].Char      = ' ';
                g[r, c].Fg        = _defaultFg;
                g[r, c].Bg        = Color.Empty;   // sentinel: use _defaultBg at render time
                g[r, c].Bold      = false;
                g[r, c].Underline = false;
                g[r, c].Reverse   = false;
            }
    }
}

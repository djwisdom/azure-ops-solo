using System.Text;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// GNU readline-style interactive line editor for legacy (pipe) terminal mode.
/// Provides line editing, cursor movement, command history navigation, and tab
/// completion entirely client-side — no ConPTY or real console required.
/// </summary>
/// <remarks>
/// All methods must be called on the UI thread.
/// <para>
/// <b>feedVt</b> is invoked synchronously; it should call <see cref="VtParser.Feed"/>
/// and trigger a view repaint directly (no BeginInvoke needed — caller is already
/// on the UI thread from a keyboard event).
/// </para>
/// <para>
/// <b>execute</b> is invoked on Enter with the complete command string terminated by
/// "\r\n", or with a bare control byte such as "\x03" for Ctrl+C and "\x04" for
/// Ctrl+D.
/// </para>
/// </remarks>
internal sealed class LegacyReadline
{
    // ── Line buffer ───────────────────────────────────────────────────────────────

    private readonly StringBuilder _line   = new();
    private int                    _cursor = 0;     // caret offset within _line (0..Length)

    // ── History ───────────────────────────────────────────────────────────────────

    private readonly List<string> _history = new();
    private int    _histIdx   = -1;  // -1 = current; 0 = most-recent; Count-1 = oldest
    private string _histSaved = "";  // snapshot of input before history browse

    // ── Kill ring ─────────────────────────────────────────────────────────────────

    private string _killBuf = "";

    // ── Tab completion ────────────────────────────────────────────────────────────

    private string[] _completions    = [];
    private int      _completionIdx  = 0;
    private string   _completionWord = "";
    private bool     _completing     = false;

    // ── Callbacks ─────────────────────────────────────────────────────────────────

    private readonly Action<string>                  _feedVt;
    private readonly Action<string>                  _execute;
    private readonly Func<string, string, string[]>? _completer;

    // ── Public properties ─────────────────────────────────────────────────────────

    /// <summary>Working directory used as the root for file/path tab completions.</summary>
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

    public IReadOnlyList<string> History => _history;

    // ── Construction ─────────────────────────────────────────────────────────────

    /// <param name="feedVt">
    ///   Emits a raw VT/ANSI string directly into the terminal buffer (called on UI thread).
    /// </param>
    /// <param name="execute">
    ///   Sends the completed line (or a bare control byte) to the shell's stdin.
    /// </param>
    /// <param name="completer">
    ///   Optional tab-completion provider: (word, workingDir) → sorted candidate strings.
    /// </param>
    public LegacyReadline(
        Action<string> feedVt,
        Action<string> execute,
        Func<string, string, string[]>? completer = null)
    {
        _feedVt    = feedVt;
        _execute   = execute;
        _completer = completer;
    }

    // ── Reset ─────────────────────────────────────────────────────────────────────

    /// <summary>Clears current input/kill/completion state while preserving history.</summary>
    public void Reset()
    {
        _line.Clear();
        _cursor    = 0;
        _histIdx   = -1;
        _histSaved = "";
        _killBuf   = "";
        _completing = false;
    }

    /// <summary>
    /// Inserts clipboard text at the current cursor position, echoing it locally.
    /// Does NOT send anything to the shell; call Execute() (Enter) when done.
    /// </summary>
    public void PasteText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _completing = false;
        foreach (char ch in text)
        {
            if (ch == '\r' || ch == '\n')
            {
                // Treat embedded newline as "submit" — matches how real shells handle paste
                Submit();
                return;
            }
            if (!char.IsControl(ch))
                InsertChar(ch);
        }
    }

    // ── Feed ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Process a chunk of keyboard bytes as produced by <see cref="TerminalView"/>
    /// (TranslateKey or OnKeyPress).  Always called on the UI thread.
    /// </summary>
    public void Feed(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);

        // Any key except Tab breaks a running completion cycle
        if (text != "\t") _completing = false;

        if (bytes[0] == 0x1B)
        {
            HandleEscape(text);
            return;
        }

        if (bytes.Length == 1)
        {
            switch (bytes[0])
            {
                case 0x01: GotoLineStart();         return;   // Ctrl+A
                case 0x02: MoveLeft(1);              return;   // Ctrl+B
                case 0x03: HandleCtrlC();            return;   // Ctrl+C
                case 0x04: HandleCtrlD();            return;   // Ctrl+D
                case 0x05: GotoLineEnd();            return;   // Ctrl+E
                case 0x06: MoveRight(1);             return;   // Ctrl+F
                case 0x07: return;                             // Ctrl+G (cancel) — no-op
                case 0x08: case 0x7F: BackDelete();  return;   // Backspace
                case 0x09: TabComplete();            return;   // Tab
                case 0x0A: case 0x0D: Submit();      return;   // Enter
                case 0x0B: KillRight();              return;   // Ctrl+K
                case 0x0C: ClearScreen();            return;   // Ctrl+L
                case 0x0E: HistoryMove(+1);          return;   // Ctrl+N (next)
                case 0x10: HistoryMove(-1);          return;   // Ctrl+P (prev)
                case 0x15: KillLeft();               return;   // Ctrl+U
                case 0x17: KillWordLeft();           return;   // Ctrl+W
                case 0x19: Yank();                   return;   // Ctrl+Y
            }
            byte b = bytes[0];
            if (b >= 0x20 && b < 0x7F) InsertChar((char)b);
        }
        else
        {
            // Multi-byte UTF-8 character (accented, CJK, …)
            foreach (char ch in text)
                if (!char.IsControl(ch)) InsertChar(ch);
        }
    }

    // ── Escape sequence dispatch ──────────────────────────────────────────────────

    private void HandleEscape(string seq)
    {
        switch (seq)
        {
            // Arrow keys — ANSI and SS3 (application-cursor-keys) forms
            case "\x1B[A": case "\x1BOA": HistoryMove(-1);  return;   // Up → older
            case "\x1B[B": case "\x1BOB": HistoryMove(+1);  return;   // Down → newer
            case "\x1B[D": case "\x1BOD": MoveLeft(1);       return;   // Left
            case "\x1B[C": case "\x1BOC": MoveRight(1);      return;   // Right

            // Home / End
            case "\x1B[H": case "\x1BOH": case "\x1B[1~": GotoLineStart(); return;
            case "\x1B[F": case "\x1BOF": case "\x1B[4~": GotoLineEnd();   return;

            // Delete key
            case "\x1B[3~": FwdDelete(); return;

            // Ctrl+Left / Ctrl+Right (xterm and others)
            case "\x1B[1;5D": case "\x1B[5D": case "\x1Bb": WordLeft();  return;   // Alt+B
            case "\x1B[1;5C": case "\x1B[5C": case "\x1Bf": WordRight(); return;   // Alt+F

            // Ctrl+Backspace / Alt+Backspace — kill word left
            case "\x1B\x7F": case "\x1B\b": KillWordLeft();  return;

            // Alt+D — kill word right
            case "\x1Bd": KillWordRight(); return;

            // Shift+Tab — reverse tab completion cycle
            case "\x1B[Z": TabComplete(reverse: true); return;

            // Page Up/Down — let TerminalView's scroll handler deal with these
            case "\x1B[5~": case "\x1B[6~": return;
        }
    }

    // ── Character insertion ───────────────────────────────────────────────────────

    private void InsertChar(char ch)
    {
        _histIdx = -1;
        _line.Insert(_cursor, ch);
        _cursor++;

        if (_cursor == _line.Length)
        {
            Emit(ch.ToString());        // append at EOL: just echo the char
        }
        else
        {
            // Mid-line: CSI @ inserts a blank (shifts remaining chars right),
            // then writing the char overwrites that blank.
            Emit("\x1B[@");
            Emit(ch.ToString());
        }
    }

    // ── Deletion ──────────────────────────────────────────────────────────────────

    private void BackDelete()
    {
        if (_cursor <= 0) { Bell(); return; }
        _cursor--;
        _line.Remove(_cursor, 1);
        if (_cursor == _line.Length)
            Emit("\b \b");          // at EOL: cursor-left, overwrite, cursor-left
        else
            Emit("\b\x1B[P");       // mid-line: cursor-left + CSI P (delete+shift)
    }

    private void FwdDelete()
    {
        if (_cursor >= _line.Length) { Bell(); return; }
        _line.Remove(_cursor, 1);
        Emit("\x1B[P");             // CSI P — delete char at cursor, shift left
    }

    // ── Cursor movement ───────────────────────────────────────────────────────────

    private void MoveLeft(int n)
    {
        n = Math.Min(n, _cursor);
        if (n <= 0) return;
        _cursor -= n;
        Emit($"\x1B[{n}D");
    }

    private void MoveRight(int n)
    {
        n = Math.Min(n, _line.Length - _cursor);
        if (n <= 0) return;
        _cursor += n;
        Emit($"\x1B[{n}C");
    }

    private void GotoLineStart()
    {
        if (_cursor == 0) return;
        Emit($"\x1B[{_cursor}D");
        _cursor = 0;
    }

    private void GotoLineEnd()
    {
        int tail = _line.Length - _cursor;
        if (tail <= 0) return;
        Emit($"\x1B[{tail}C");
        _cursor = _line.Length;
    }

    // ── Word navigation ───────────────────────────────────────────────────────────

    private void WordLeft()
    {
        int p = _cursor;
        while (p > 0 && !IsWordChar(_line[p - 1])) p--;
        while (p > 0 && IsWordChar(_line[p - 1]))  p--;
        MoveLeft(_cursor - p);
    }

    private void WordRight()
    {
        int p = _cursor, len = _line.Length;
        while (p < len && !IsWordChar(_line[p])) p++;
        while (p < len && IsWordChar(_line[p]))  p++;
        MoveRight(p - _cursor);
    }

    private static bool IsWordChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.';

    // ── Kill ring ─────────────────────────────────────────────────────────────────

    private void KillRight()
    {
        if (_cursor >= _line.Length) return;
        _killBuf = _line.ToString(_cursor, _line.Length - _cursor);
        _line.Remove(_cursor, _line.Length - _cursor);
        Emit("\x1B[K");             // CSI K — erase from cursor to EOL
    }

    private void KillLeft()
    {
        if (_cursor <= 0) return;
        _killBuf = _line.ToString(0, _cursor);
        int n = _cursor;
        Emit($"\x1B[{n}D\x1B[{n}P");   // cursor-left n then delete n chars
        _line.Remove(0, n);
        _cursor = 0;
    }

    private void KillWordLeft()
    {
        int p = _cursor;
        while (p > 0 && !IsWordChar(_line[p - 1])) p--;
        while (p > 0 && IsWordChar(_line[p - 1]))  p--;
        int n = _cursor - p;
        if (n <= 0) return;
        _killBuf = _line.ToString(p, n);
        Emit($"\x1B[{n}D\x1B[{n}P");
        _line.Remove(p, n);
        _cursor = p;
    }

    private void KillWordRight()
    {
        int p = _cursor, len = _line.Length;
        while (p < len && !IsWordChar(_line[p])) p++;
        while (p < len && IsWordChar(_line[p]))  p++;
        int n = p - _cursor;
        if (n <= 0) return;
        _killBuf = _line.ToString(_cursor, n);
        Emit($"\x1B[{n}P");
        _line.Remove(_cursor, n);
    }

    private void Yank()
    {
        if (_killBuf.Length == 0) { Bell(); return; }
        foreach (char ch in _killBuf) InsertChar(ch);
    }

    // ── History ───────────────────────────────────────────────────────────────────

    /// <param name="delta">-1 = older (Up arrow / Ctrl+P), +1 = newer (Down arrow / Ctrl+N)</param>
    private void HistoryMove(int delta)
    {
        if (delta < 0) // Up — move toward older entries
        {
            if (_history.Count == 0) return;
            if (_histIdx == -1) _histSaved = _line.ToString();
            int next = _histIdx + 1;
            if (next >= _history.Count) return; // already at oldest
            _histIdx = next;
        }
        else           // Down — move toward newer / current
        {
            if (_histIdx < 0) return;
            _histIdx--;
        }

        string target = _histIdx < 0
            ? _histSaved
            : _history[_history.Count - 1 - _histIdx]; // _history[^1] is most-recent (idx 0)

        SetLine(target);
    }

    private void SetLine(string text)
    {
        if (_cursor > 0)           Emit($"\x1B[{_cursor}D"); // go to input start
        if (text.Length > 0)       Emit(text);                // overwrite with new text
        if (text.Length < _line.Length) Emit("\x1B[K");       // erase residual old chars
        _line.Clear();
        _line.Append(text);
        _cursor = text.Length;
    }

    // ── Tab completion ────────────────────────────────────────────────────────────

    private void TabComplete(bool reverse = false)
    {
        if (_completer == null) { Bell(); return; }

        if (!_completing)
        {
            _completionWord = GetCurrentWord();
            _completions    = _completer(_completionWord, WorkingDirectory);
            _completionIdx  = reverse ? _completions.Length : -1;
            _completing     = _completions.Length > 0;
            if (!_completing) { Bell(); return; }
        }

        // Cycle through candidates
        if (!reverse)
            _completionIdx = (_completionIdx + 1) % _completions.Length;
        else
            _completionIdx = ((_completionIdx - 1) + _completions.Length) % _completions.Length;

        // Remove the typed word prefix and insert the selected completion
        int wordLen = _completionWord.Length;
        if (wordLen > 0)
        {
            Emit($"\x1B[{wordLen}D\x1B[{wordLen}P");
            _line.Remove(_cursor - wordLen, wordLen);
            _cursor -= wordLen;
        }

        string match = _completions[_completionIdx];
        // Auto-quote if the completion contains spaces
        if (match.Contains(' ') && !match.StartsWith('"'))
            match = $"\"{match}\"";

        foreach (char ch in match) InsertChar(ch);

        // Track so the next Tab press cycles from the same base word
        _completionWord = _completions[_completionIdx];
    }

    private string GetCurrentWord()
    {
        int p = _cursor;
        while (p > 0)
        {
            char c = _line[p - 1];
            if (c is ' ' or ';' or '|' or '>' or '<' or '&' or '(' or ')') break;
            p--;
        }
        return _line.ToString(p, _cursor - p);
    }

    // ── Special control actions ───────────────────────────────────────────────────

    private void HandleCtrlC()
    {
        Emit("^C\r\n");
        Reset();
        _execute("\x03");           // interrupt signal — sent outside the line buffer
    }

    private void HandleCtrlD()
    {
        if (_line.Length == 0)
            _execute("\x04");       // EOF when line is empty → shell should exit
        else
            FwdDelete();
    }

    private void ClearScreen()
    {
        Emit("\x1B[2J\x1B[H");     // clear screen + cursor home
        // Re-display any pending input so the user can continue editing
        if (_line.Length > 0)
        {
            Emit(_line.ToString());
            int tail = _line.Length - _cursor;
            if (tail > 0) Emit($"\x1B[{tail}D");
        }
    }

    private void Submit()
    {
        string cmd = _line.ToString();
        Emit("\r\n");

        if (cmd.Length > 0 && (_history.Count == 0 || _history[^1] != cmd))
            _history.Add(cmd);

        _execute(cmd + "\r\n");

        _line.Clear();
        _cursor    = 0;
        _histIdx   = -1;
        _histSaved = "";
        _completing = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private void Bell() => Emit("\a");
    private void Emit(string vt) => _feedVt(vt);
}

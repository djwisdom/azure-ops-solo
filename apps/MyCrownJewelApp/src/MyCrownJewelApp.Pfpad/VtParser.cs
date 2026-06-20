using System.Drawing;
using System.Text;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Full VT/xterm state machine (Paul Williams model, simplified).
/// Accepts decoded UTF-16 chars and mutates a <see cref="TerminalBuffer"/> directly.
/// </summary>
internal sealed class VtParser
{
    // ── State machine ────────────────────────────────────────────────────────────

    private enum State
    {
        Ground, Escape, EscapeIntermediate,
        CsiEntry, CsiParam, CsiIntermediate,
        OscString, DcsIgnore
    }

    private readonly TerminalBuffer _buf;
    private State _state = State.Ground;

    private readonly StringBuilder _paramBuf       = new(32);
    private readonly StringBuilder _oscBuf         = new(256);
    private char                   _csiIntermediate;
    private bool                   _appCursorKeys;

    public bool ApplicationCursorKeys => _appCursorKeys;
    public bool BracketedPaste        { get; private set; }
    /// <summary>
    /// Line New Mode (ONLCR): when <c>true</c>, every LF automatically performs a
    /// carriage return first — identical to a real PTY's NL→CR+NL translation.
    /// Set to <c>true</c> for legacy pipe mode; <c>false</c> for ConPTY (which
    /// already does the translation in the pseudo-console driver).
    /// </summary>
    public bool AutoLineFeedMode { get; set; } = false;

    public event Action<string>? TitleChanged;
    /// <summary>Fired when the terminal receives ESC[6n (DSR). Host must write ESC[row;colR back to PTY input.</summary>
    public event Action<string>? ReportRequested;

    // ── xterm-256 palette ────────────────────────────────────────────────────────

    private static readonly Color[] s_ansi16 = BuildAnsi16();
    private static readonly Color[] s_xterm256 = BuildXterm256();

    // ── Construction ─────────────────────────────────────────────────────────────

    public VtParser(TerminalBuffer buffer) => _buf = buffer;

    // ── Entry point ───────────────────────────────────────────────────────────────

    public void Feed(ReadOnlySpan<char> data)
    {
        foreach (char ch in data)
            Step(ch);
    }

    // ── Main dispatch ────────────────────────────────────────────────────────────

    private void Step(char ch)
    {
        // ESC is handled in any state
        if (ch == '\x1B')
        {
            _paramBuf.Clear();
            _csiIntermediate = '\0';
            _state = State.Escape;
            return;
        }

        // Per VT spec: C0 controls are executed immediately from any state except
        // OscString and DcsIgnore (where they're collected/ignored per those rules).
        // This is the "execute anywhere" rule — CAN/SUB also cancel the sequence.
        if (ch < 0x20 && _state != State.OscString && _state != State.DcsIgnore)
        {
            switch (ch)
            {
                case '\r':                 _buf.CarriageReturn(); break;
                case '\n': case '\x0B': case '\x0C':
                    // ONLCR: auto-CR before LF in legacy pipe mode.
                    // Skip if cursor is already at col 0 (handles \r\n without double-CR).
                    if (AutoLineFeedMode && _buf.CursorCol != 0) _buf.CarriageReturn();
                    _buf.LineFeed(); break;
                case '\b':                 _buf.Backspace();      break;
                case '\t':                 _buf.Tab();            break;
                case '\x18': case '\x1A':  _state = State.Ground; break; // CAN/SUB cancel sequence
                // \x07 BEL, \x0E SO, \x0F SI — ignore
            }
            return;
        }

        if (_state == State.OscString)
        {
            if (ch == '\x07')                                       // BEL terminates OSC
            { FlushOsc(); _state = State.Ground; return; }
            if (ch == '\x9C')                                       // ST
            { FlushOsc(); _state = State.Ground; return; }
            _oscBuf.Append(ch);
            return;
        }

        if (_state == State.Ground)
        {
            // C0 controls already handled above; only printable chars reach here.
            // DEL (0x7F) acts as backspace in some terminals.
            if (ch == '\x7F') { _buf.Backspace(); return; }
            if (ch >= ' ' && ch != '\x7F') _buf.WriteChar(ch);
            return;
        }

        if (_state == State.DcsIgnore)
        {
            if (ch == '\x9C') _state = State.Ground;
            return;
        }

        switch (_state)
        {
            case State.Escape:           HandleEscape(ch);           break;
            case State.EscapeIntermediate: HandleEscapeIntermediate(ch); break;
            case State.CsiEntry:
            case State.CsiParam:         HandleCsiInput(ch);          break;
            case State.CsiIntermediate:  HandleCsiIntermediate(ch);   break;
        }
    }

    // ── ESC ───────────────────────────────────────────────────────────────────────

    private void HandleEscape(char ch)
    {
        switch (ch)
        {
            case '[': _state = State.CsiEntry; _paramBuf.Clear(); _csiIntermediate = '\0'; break;
            case ']': _state = State.OscString; _oscBuf.Clear(); break;
            case 'P': _state = State.DcsIgnore; break;
            case '7': _buf.SaveCursor();          _state = State.Ground; break;
            case '8': _buf.RestoreCursor();       _state = State.Ground; break;
            case 'M': _buf.ReverseLineFeed();     _state = State.Ground; break;
            case 'D': _buf.LineFeed();            _state = State.Ground; break;
            case 'E': _buf.CarriageReturn(); _buf.LineFeed(); _state = State.Ground; break;
            case 'c': HardReset();                _state = State.Ground; break;
            default:
                if (ch >= 0x20 && ch <= 0x2F) { _csiIntermediate = ch; _state = State.EscapeIntermediate; }
                else _state = State.Ground;
                break;
        }
    }

    private void HandleEscapeIntermediate(char ch)
    {
        // ESC <intermediate> <final> — e.g. charset designations; ignore safely
        _state = State.Ground;
    }

    // ── CSI input accumulation ───────────────────────────────────────────────────

    private void HandleCsiInput(char ch)
    {
        if (ch >= '0' && ch <= '9' || ch == ';' || ch == ':')
        {
            _paramBuf.Append(ch);
            _state = State.CsiParam;
        }
        else if (ch >= 0x20 && ch <= 0x2F)
        {
            _csiIntermediate = ch;
            _state = State.CsiIntermediate;
        }
        else if (ch >= 0x40 && ch <= 0x7E)
        {
            ExecuteCsi(_paramBuf.ToString(), '\0', ch);
            _state = State.Ground;
        }
    }

    private void HandleCsiIntermediate(char ch)
    {
        if (ch >= 0x40 && ch <= 0x7E)
        {
            ExecuteCsi(_paramBuf.ToString(), _csiIntermediate, ch);
            _state = State.Ground;
        }
    }

    // ── CSI dispatch ─────────────────────────────────────────────────────────────

    private void ExecuteCsi(string paramStr, char intermediate, char final)
    {
        bool priv = paramStr.Length > 0 && paramStr[0] == '?';
        string p  = priv ? paramStr[1..] : paramStr;

        int[] pars = ParseParams(p);
        int   p0   = pars.Length > 0 ? pars[0] : 0;
        int   p1   = pars.Length > 1 ? pars[1] : 0;

        switch (final)
        {
            // Cursor movement
            case 'A': _buf.MoveCursorUp(Math.Max(1, p0));    break;
            case 'B': _buf.MoveCursorDown(Math.Max(1, p0));  break;
            case 'C': _buf.MoveCursorRight(Math.Max(1, p0)); break;
            case 'D': _buf.MoveCursorLeft(Math.Max(1, p0));  break;
            case 'E': _buf.SetCursor(Math.Min(_buf.Rows - 1, _buf.CursorRow + Math.Max(1, p0)), 0); break;
            case 'F': _buf.SetCursor(Math.Max(0,             _buf.CursorRow - Math.Max(1, p0)), 0); break;
            case 'G': _buf.SetCursorCol(Math.Max(1, p0) - 1); break;
            case 'H':
            case 'f': _buf.SetCursor(Math.Max(1, p0) - 1, Math.Max(1, p1) - 1); break;
            case 'd': _buf.SetCursorRow(Math.Max(1, p0) - 1); break;

            // Erase
            case 'J': _buf.EraseInDisplay(p0); break;
            case 'K': _buf.EraseInLine(p0);    break;
            case 'X': _buf.EraseChars(Math.Max(1, p0)); break;

            // Insert / delete
            case 'L': _buf.InsertLines(Math.Max(1, p0));  break;
            case 'M': _buf.DeleteLines(Math.Max(1, p0));  break;
            case 'P': _buf.DeleteChars(Math.Max(1, p0));  break;
            case '@': _buf.InsertChars(Math.Max(1, p0));  break;

            // Scroll
            case 'S': _buf.ScrollUp(Math.Max(1, p0));   break;
            case 'T': _buf.ScrollDown(Math.Max(1, p0));  break;

            // Scroll region
            case 'r':
                _buf.SetScrollRegion(
                    Math.Max(1, p0) - 1,
                    (p1 == 0 ? _buf.Rows : p1) - 1);
                break;

            // Save / restore cursor
            case 's': _buf.SaveCursor();    break;
            case 'u': _buf.RestoreCursor(); break;

            // SGR
            case 'm': ProcessSgr(pars); break;

            // Private modes
            case 'h' when priv: SetPrivateMode(p0, true);  break;
            case 'l' when priv: SetPrivateMode(p0, false); break;

            // DSR – Device Status Report (ESC[6n = cursor position query)
            case 'n' when !priv && p0 == 6:
                ReportRequested?.Invoke($"\x1B[{_buf.CursorRow + 1};{_buf.CursorCol + 1}R");
                break;

            // Ignored (DA, other DSR variants, window ops, etc.)
            case 'c': case 'n': case 't': break;

            // DECSCUSR — cursor shape: CSI Ps SP q  (intermediate = ' ')
            case 'q' when intermediate == ' ': break; // accept, shape not yet rendered
        }
    }

    // ── Private modes ────────────────────────────────────────────────────────────

    private void SetPrivateMode(int mode, bool enable)
    {
        switch (mode)
        {
            case 1:    _appCursorKeys  = enable;              break; // DECCKM
            case 7:    _buf.AutoWrap   = enable;              break; // DECAWM
            case 25:   _buf.CursorHidden = !enable;           break; // DECTCEM
            case 47:   case 1047:
                if (enable) _buf.SwitchToAlternate(); else _buf.SwitchToPrimary(); break;
            case 1049:
                if (enable) { _buf.SaveCursor(); _buf.SwitchToAlternate(); }
                else        { _buf.SwitchToPrimary(); _buf.RestoreCursor(); }
                break;
            case 2004: BracketedPaste = enable;               break; // bracketed paste
        }
    }

    // ── SGR (Select Graphic Rendition) ───────────────────────────────────────────

    private void ProcessSgr(int[] pars)
    {
        if (pars.Length == 0) { ResetSgr(); return; }

        for (int i = 0; i < pars.Length; i++)
        {
            int code = pars[i];
            switch (code)
            {
                case 0:  ResetSgr();                    break;
                case 1:  _buf.CurrentBold      = true;  break;
                case 3:                                  break; // italic – not rendered
                case 4:  _buf.CurrentUnderline = true;  break;
                case 7:  _buf.CurrentReverse   = true;  break;
                case 22: _buf.CurrentBold      = false; break;
                case 24: _buf.CurrentUnderline = false; break;
                case 27: _buf.CurrentReverse   = false; break;

                case int c when c >= 30 && c <= 37:  _buf.CurrentFg = s_ansi16[c - 30]; break;
                case 38: _buf.CurrentFg = ReadExtColor(pars, ref i); break;
                case 39: _buf.CurrentFg = _buf.DefaultFg; break;

                case int c when c >= 40 && c <= 47:  _buf.CurrentBg = s_ansi16[c - 40]; break;
                case 48: _buf.CurrentBg = ReadExtColor(pars, ref i); break;
                case 49: _buf.CurrentBg = _buf.DefaultBg; break;

                case int c when c >= 90 && c <= 97:   _buf.CurrentFg = s_ansi16[c - 90  + 8]; break;
                case int c when c >= 100 && c <= 107: _buf.CurrentBg = s_ansi16[c - 100 + 8]; break;
            }
        }
    }

    private void ResetSgr()
    {
        _buf.CurrentFg        = _buf.DefaultFg;
        _buf.CurrentBg        = _buf.DefaultBg;
        _buf.CurrentBold      = false;
        _buf.CurrentUnderline = false;
        _buf.CurrentReverse   = false;
    }

    private static Color ReadExtColor(int[] pars, ref int i)
    {
        if (i + 1 >= pars.Length) return Color.White;
        int sub = pars[i + 1];
        if (sub == 2 && i + 4 < pars.Length)       // true color
        {
            var c = Color.FromArgb(
                Math.Clamp(pars[i + 2], 0, 255),
                Math.Clamp(pars[i + 3], 0, 255),
                Math.Clamp(pars[i + 4], 0, 255));
            i += 4;
            return c;
        }
        if (sub == 5 && i + 2 < pars.Length)       // 256-color
        {
            var c = s_xterm256[Math.Clamp(pars[i + 2], 0, 255)];
            i += 2;
            return c;
        }
        return Color.White;
    }

    // ── OSC (Operating System Command) ───────────────────────────────────────────

    private void FlushOsc()
    {
        string osc  = _oscBuf.ToString();
        _oscBuf.Clear();
        int semi = osc.IndexOf(';');
        if (semi < 0) return;
        if (!int.TryParse(osc[..semi], out int ps)) return;
        string pt = osc[(semi + 1)..];
        if (ps == 0 || ps == 2) TitleChanged?.Invoke(pt);  // set window/tab title
    }

    // ── Misc ─────────────────────────────────────────────────────────────────────

    private void HardReset()
    {
        _buf.EraseInDisplay(2);
        _buf.SetCursor(0, 0);
        _buf.SwitchToPrimary();
        ResetSgr();
        _appCursorKeys = false;
        _buf.AutoWrap  = true;
    }

    // ── Parameter parser ─────────────────────────────────────────────────────────

    private static int[] ParseParams(string p)
    {
        if (string.IsNullOrEmpty(p)) return Array.Empty<int>();
        var parts  = p.Split(';');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            int.TryParse(parts[i], out result[i]);
        return result;
    }

    // ── Colour tables ────────────────────────────────────────────────────────────

    private static Color[] BuildAnsi16() => new[]
    {
        Color.FromArgb(  0,   0,   0),  // 0  Black
        Color.FromArgb(194,  54,  33),  // 1  Red
        Color.FromArgb( 37, 188,  36),  // 2  Green
        Color.FromArgb(173, 173,  39),  // 3  Yellow
        Color.FromArgb( 73,  46, 225),  // 4  Blue
        Color.FromArgb(211,  56, 211),  // 5  Magenta
        Color.FromArgb( 51, 187, 200),  // 6  Cyan
        Color.FromArgb(203, 204, 205),  // 7  White
        Color.FromArgb(129, 131, 131),  // 8  Bright Black
        Color.FromArgb(252,  57,  31),  // 9  Bright Red
        Color.FromArgb( 49, 231,  34),  // 10 Bright Green
        Color.FromArgb(234, 236,  35),  // 11 Bright Yellow
        Color.FromArgb( 88,  51, 255),  // 12 Bright Blue
        Color.FromArgb(249,  53, 248),  // 13 Bright Magenta
        Color.FromArgb( 20, 240, 240),  // 14 Bright Cyan
        Color.FromArgb(233, 235, 235),  // 15 Bright White
    };

    private static Color[] BuildXterm256()
    {
        var colors = new Color[256];
        var ansi16 = BuildAnsi16();
        for (int i = 0; i < 16; i++) colors[i] = ansi16[i];
        // 16-231: 6×6×6 cube
        for (int i = 0; i < 216; i++)
        {
            int r = (i / 36) % 6, g = (i / 6) % 6, b = i % 6;
            colors[16 + i] = Color.FromArgb(
                r == 0 ? 0 : r * 40 + 55,
                g == 0 ? 0 : g * 40 + 55,
                b == 0 ? 0 : b * 40 + 55);
        }
        // 232-255: grayscale
        for (int i = 0; i < 24; i++)
            colors[232 + i] = Color.FromArgb(i * 10 + 8, i * 10 + 8, i * 10 + 8);
        return colors;
    }
}

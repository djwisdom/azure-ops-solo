using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    public enum VimMode
    {
        Normal, Insert, Visual, VisualLine, VisualBlock, Command
    }

    public class VimEngine
    {
        public VimMode CurrentMode { get; private set; } = VimMode.Normal;
        public bool Enabled { get; set; }
        public string CommandText => _cmdBuffer.ToString();

        private readonly RichTextBox _tb;
        private readonly StringBuilder _cmdBuffer = new();
        private string? _lastYank;
        private int _repeatCount = 1;

        public event Action? SaveRequested;
        public event Action? CloseRequested;
        public event Action? VerticalSplitRequested;
        public event Action? HorizontalSplitRequested;
        public event Action<bool>? InsertSpacesRequested;
        public event Action<int>? TabSizeRequested;
        public event Action<bool>? AutoIndentRequested;
        public event Action<bool>? SmartTabsRequested;
        public event Action<int>? GoToLineRequested;

        private static readonly HashSet<Keys> MotionKeys = new()
        {
            Keys.H, Keys.J, Keys.K, Keys.L,
            Keys.W, Keys.B, Keys.E,
            Keys.D0,
            Keys.Up, Keys.Down, Keys.Left, Keys.Right,
            Keys.PageUp, Keys.PageDown,
            Keys.Home, Keys.End
        };

        public VimEngine(RichTextBox textBox)
        {
            _tb = textBox;
        }

        public void EnterMode(VimMode mode)
        {
            CurrentMode = mode;
            _cmdBuffer.Clear();
            _repeatCount = 1;
            if (mode == VimMode.Normal && _tb.SelectionLength > 0)
                _tb.SelectionLength = 0;
        }

        public bool ProcessKey(Keys keyData)
        {
            if (!Enabled) return false;

            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0;
            bool shift = (keyData & Keys.Shift) != 0;
            bool alt = (keyData & Keys.Alt) != 0;

            if (alt) return false;

            if (CurrentMode == VimMode.Insert)
                return ProcessInsertMode(key, ctrl, shift);

            if (CurrentMode == VimMode.Visual || CurrentMode == VimMode.VisualLine)
                return ProcessVisualMode(key, ctrl, shift);

            if (CurrentMode == VimMode.Command)
                return ProcessCommandMode(key, ctrl, shift);

            return ProcessNormalMode(key, ctrl, shift);
        }

        private bool ProcessInsertMode(Keys key, bool ctrl, bool shift)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                _tb.SelectionLength = 0;
                EnterMode(VimMode.Normal);
                return true;
            }
            return false; // let the key through to normal typing
        }

        private bool ProcessVisualMode(Keys key, bool ctrl, bool shift)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                _tb.SelectionLength = 0;
                EnterMode(VimMode.Normal);
                return true;
            }

            bool linewise = CurrentMode == VimMode.VisualLine;

            switch (key)
            {
                case Keys.H: case Keys.Left: case Keys.Back:
                    if (!linewise) MoveSelection(-1, 0); return true;
                case Keys.J: case Keys.Down: MoveSelection(0, 1); return true;
                case Keys.K: case Keys.Up: MoveSelection(0, -1); return true;
                case Keys.L: case Keys.Right:
                    if (!linewise) MoveSelection(1, 0); return true;
                case Keys.D0: MoveToLineStartVisual(); return true;
                case Keys.D4:
                    if (shift) { MoveToLineEndVisual(); return true; }
                    return false;
                case Keys.W: MoveWordForwardVisual(); return true;
                case Keys.B: MoveWordBackwardVisual(); return true;

                case Keys.D: // delete selection
                case Keys.X:
                    CutSelection(); EnterMode(VimMode.Normal); return true;
                case Keys.Y:
                    YankSelection(); _tb.SelectionLength = 0; EnterMode(VimMode.Normal); return true;
                case Keys.C:
                    CutSelection(); EnterMode(VimMode.Insert); return true;
                case Keys.OemPeriod:
                    IndentSelection(1); EnterMode(VimMode.Normal); return true;
                case Keys.Oemcomma:
                    IndentSelection(-1); EnterMode(VimMode.Normal); return true;
                case Keys.V:
                    if (CurrentMode == VimMode.VisualLine)
                        EnterMode(VimMode.Visual);
                    else
                        EnterMode(VimMode.VisualLine);
                    return true;
            }
            return false;
        }

        private bool ProcessCommandMode(Keys key, bool ctrl, bool shift)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                _cmdBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                ExecuteCommand(_cmdBuffer.ToString().Trim());
                _cmdBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (_cmdBuffer.Length > 0)
                    _cmdBuffer.Remove(_cmdBuffer.Length - 1, 1);
                return true;
            }

            // Accept typed characters into the command buffer
            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                _cmdBuffer.Append(ch.Value);
                return true;
            }

            return false;
        }

        private bool ProcessNormalMode(Keys key, bool ctrl, bool shift)
        {
            // Repeat count: digits before a command
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int d = key - Keys.D0;
                if (_cmdBuffer.Length > 0 && char.IsDigit(_cmdBuffer[0]))
                {
                    int val = int.Parse(_cmdBuffer.ToString());
                    _repeatCount = val * 10 + d;
                    _cmdBuffer.Append(d.ToString());
                }
                else if (d > 0)
                {
                    _repeatCount = d;
                    _cmdBuffer.Append(d.ToString());
                }
                return true;
            }

            char? ch = KeyToChar(key, shift);
            _cmdBuffer.Append(ch);

            string buf = _cmdBuffer.ToString();

            // Single-key commands
            switch (key)
            {
                case Keys.I: EnterMode(VimMode.Insert); ResetBuffer(); return true;
                case Keys.A: MoveRight(); EnterMode(VimMode.Insert); ResetBuffer(); return true;
            }

            if (shift && ch == 'I') { MoveToLineStart(); EnterMode(VimMode.Insert); ResetBuffer(); return true; }
            if (shift && ch == 'A') { MoveToLineEnd(); EnterMode(VimMode.Insert); ResetBuffer(); return true; }

            // Enter command mode on ':'
            if (ch == ':') { EnterMode(VimMode.Command); _cmdBuffer.Clear(); return true; }

            // Handle the buffer
            bool handled = HandleNormalBuffer(buf, key, shift);
            if (handled) { ResetBuffer(); return true; }

            // If buffer doesn't match any command, reset it
            if (!IsPrefixOfCommand(buf))
            {
                ResetBuffer();
                return false;
            }

            // Buffer is a valid prefix of a multi-key command (e.g. "g" waiting for "gg").
            // Consume the key to prevent it from being typed into the editor.
            return true;
        }

        private bool HandleNormalBuffer(string buf, Keys key, bool shift)
        {
            switch (buf)
            {
                // Motions
                case "h": case "\0h": MoveLeft(); return true;
                case "j": MoveDown(); return true;
                case "k": MoveUp(); return true;
                case "l": case "\0l": MoveRight(); return true;

                case "w": MoveWordForward(); return true;
                case "b": MoveWordBackward(); return true;
                case "e": MoveToWordEnd(); return true;

                case "0": MoveToLineStart(); return true;
                case "$": MoveToLineEnd(); return true;
                case "^": MoveToFirstNonBlank(); return true;

                case "G": MoveToLastLine(); return true;
                case "gg": MoveToFirstLine(); return true;

                case "%": JumpToMatchingBracket(); return true;

                case " ": MoveRight(); return true;

                // Insert
                case "i": EnterMode(VimMode.Insert); return true;
                case "a": MoveRight(); EnterMode(VimMode.Insert); return true;
                case "I": MoveToLineStart(); EnterMode(VimMode.Insert); return true;
                case "A": MoveToLineEnd(); EnterMode(VimMode.Insert); return true;
                case "o": OpenLineBelow(); return true;
                case "O": OpenLineAbove(); return true;

                // Delete
                case "x": DeleteChar(); return true;
                case "X": DeleteCharBefore(); return true;
                case "dd": DeleteLine(); return true;
                case "D": case "d$": DeleteToLineEnd(); return true;
                case "dw": DeleteWord(); return true;
                case "diw": DeleteInnerWord(); return true;
                case "d0": DeleteToLineStart(); return true;
                case "d^": DeleteToLineStart(); return true;

                // Yank & Paste
                case "yy": case "Y": YankLine(); return true;
                case "yw": YankWord(); return true;
                case "p": PasteAfter(); return true;
                case "P": PasteBefore(); return true;

                // Change
                case "cc": DeleteLine(); EnterMode(VimMode.Insert); return true;
                case "C": DeleteToLineEnd(); EnterMode(VimMode.Insert); return true;
                case "cw": DeleteWord(); EnterMode(VimMode.Insert); return true;
                case "ciw": DeleteInnerWord(); EnterMode(VimMode.Insert); return true;
                case "s": DeleteChar(); EnterMode(VimMode.Insert); return true;
                case "S": DeleteLine(); EnterMode(VimMode.Insert); return true;

                // Visual
                case "v": EnterMode(VimMode.Visual); _tb.SelectionLength = 0; return true;
                case "V": EnterMode(VimMode.VisualLine); _tb.SelectionLength = 0; return true;

                // Undo / Redo
                case "u": SendCtrlZ(); return true;
                case "\x12": SendCtrlR(); return true;

                // Indent
                case ">>": IndentLine(1); return true;
                case "<<": IndentLine(-1); return true;

                // Toggle case
                case "~": ToggleCase(); return true;

                // Join
                case "J": JoinLines(); return true;

                // Repeat
                case ".": RepeatLast(); return true;

                // Search
                case "/": _cmdBuffer.Clear(); _cmdBuffer.Append('/'); return true;
                case "?": _cmdBuffer.Clear(); _cmdBuffer.Append('?'); return true;
                case "n": FindNext(); return true;
                case "N": FindPrevious(); return true;

                // Page scroll
                case "\x0C": PageDown(); return true; // Ctrl+F
                case "\x02": PageUp(); return true;   // Ctrl+B
                case "\x04": HalfPageDown(); return true; // Ctrl+D
                case "\x15": HalfPageUp(); return true;   // Ctrl+U

                // Bracket insert mode (Ctrl+[ = Esc)
                case "\x1B": return true;

                // Operator-pending: d, y, c followed by motion
                case "d":
                case "y":
                case "c":
                case "g":
                case "<":
                case ">":
                    return false; // wait for more chars
            }

            // Handle d{motion}, y{motion}, c{motion}
            if (buf.Length == 2)
            {
                char op = buf[0];
                char motion = buf[1];
                if ((op == 'd' || op == 'y' || op == 'c') && MotionKeys.Contains(KeyFromChar(motion)))
                {
                    ExecuteWithMotion(op, motion);
                    return true;
                }
            }

            return false;
        }

        private void ExecuteWithMotion(char op, char motion)
        {
            int start = _tb.SelectionStart;
            int origSelStart = start;
            bool moved = false;

            switch (motion)
            {
                case 'h': MoveLeft(); moved = true; break;
                case 'j': MoveDown(); moved = true; break;
                case 'k': MoveUp(); moved = true; break;
                case 'l': MoveRight(); moved = true; break;
                case 'w': MoveWordForward(); moved = true; break;
                case 'b': MoveWordBackward(); moved = true; break;
                case 'e': MoveToWordEnd(); moved = true; break;
                case '0': MoveToLineStart(); moved = true; break;
                case '$': MoveToLineEnd(); moved = true; break;
                case '^': MoveToFirstNonBlank(); moved = true; break;
                case 'G': MoveToLastLine(); moved = true; break;
                case 'g': MoveToFirstLine(); moved = true; break;
            }

            if (!moved) return;

            int end = _tb.SelectionStart;
            _tb.SelectionStart = Math.Min(origSelStart, end);
            _tb.SelectionLength = Math.Abs(end - origSelStart);

            switch (op)
            {
                case 'd': _tb.SelectedText = ""; break;
                case 'y': _lastYank = _tb.SelectedText; break;
                case 'c': _tb.SelectedText = ""; EnterMode(VimMode.Insert); break;
            }

            _tb.SelectionLength = 0;
        }

        private void UpdateCommandLine() { /* status bar indicator managed by Form1 */ }
        private void ResetBuffer() { _cmdBuffer.Clear(); _repeatCount = 1; }

        private bool IsPrefixOfCommand(string buf)
        {
            if (buf.Length == 1)
            {
                char c = buf[0];
                return "hjkl wbe0$^G% iIaAoOxX DdYyPp CcSsVv uU J. /? nN ~<>".Contains(c)
                    || char.IsDigit(c) || c == 'd' || c == 'y' || c == 'c' || c == 'g'
                    || c == '<' || c == '>' || c == '"' || c == 'z' || c == 'Z';
            }
            if (buf.Length == 2)
            {
                char op = buf[0];
                char m = buf[1];
                if ((op == 'd' || op == 'y' || op == 'c') && "hjklwbe0$^G".Contains(m))
                    return true;
                if (buf == "gg" || buf == "dd" || buf == "yy" || buf == "cc"
                    || buf == ">>" || buf == "<<" || buf == "di" || buf == "ci" || buf == "yi")
                    return true;
            }
            if (buf.Length == 3)
            {
                if (buf == "diw" || buf == "ciw" || buf == "yiw" || buf == "daw")
                    return true;
            }
            return false;
        }

        private void ExecuteCommand(string cmd)
        {
            if (cmd.StartsWith("set "))
            {
                ExecuteSet(cmd[4..].Trim());
                return;
            }

            if (int.TryParse(cmd, out int line) && line > 0)
            {
                GoToLineRequested?.Invoke(line);
                return;
            }

            switch (cmd)
            {
                case "w":
                case "write":
                    SaveRequested?.Invoke();
                    break;
                case "wq":
                case "x":
                    SaveRequested?.Invoke();
                    CloseRequested?.Invoke();
                    break;
                case "q":
                    CloseRequested?.Invoke();
                    break;
                case "q!":
                case "quit":
                    CloseRequested?.Invoke();
                    break;
                case "w!":
                    SaveRequested?.Invoke();
                    break;
                case "wq!":
                    SaveRequested?.Invoke();
                    CloseRequested?.Invoke();
                    break;
                case "e!":
                    // Reload — not implemented
                    break;
                case "vsp":
                case "vsplit":
                    VerticalSplitRequested?.Invoke();
                    break;
                case "sp":
                case "split":
                    HorizontalSplitRequested?.Invoke();
                    break;
            }
        }

        private void ExecuteSet(string args)
        {
            foreach (var part in args.Split(','))
            {
                string arg = part.Trim();
                if (arg == "smartindent") { AutoIndentRequested?.Invoke(true); }
                else if (arg == "nosmartindent") { AutoIndentRequested?.Invoke(false); }
                else if (arg == "smarttab") { SmartTabsRequested?.Invoke(true); }
                else if (arg == "nosmarttab") { SmartTabsRequested?.Invoke(false); }
                else if (arg == "expandtab") { InsertSpacesRequested?.Invoke(true); }
                else if (arg == "noexpandtab") { InsertSpacesRequested?.Invoke(false); }
                else if (arg.StartsWith("tabstop=") || arg.StartsWith("shiftwidth=") || arg.StartsWith("softtabstop="))
                {
                    int eq = arg.IndexOf('=');
                    if (int.TryParse(arg[(eq + 1)..], out int ts) && ts >= 1 && ts <= 20)
                        TabSizeRequested?.Invoke(ts);
                }
            }
        }

        #region Movement
        private int GetCurrentLine() => _tb.GetLineFromCharIndex(_tb.SelectionStart);
        private int GetLineStart(int line) => _tb.GetFirstCharIndexFromLine(line);
        private int GetLineEnd(int line)
        {
            int next = _tb.GetFirstCharIndexFromLine(line + 1);
            if (next >= 0) return Math.Max(0, next - 1);
            return Math.Max(0, _tb.TextLength);
        }
        private string GetCurrentLineText()
        {
            int l = GetCurrentLine();
            int s = GetLineStart(l);
            int e = GetLineEnd(l);
            if (s < 0 || e <= s || e > _tb.TextLength) return "";
            return _tb.Text.Substring(s, e - s);
        }
        private int GetLineLength(int line)
        {
            int s = GetLineStart(line);
            int e = GetLineEnd(line);
            if (s < 0 || e < 0) return 0;
            return Math.Max(0, e - s);
        }
        private int GetRepeat() => Math.Max(1, _repeatCount);

        private void MoveLeft() => _tb.SelectionStart = Math.Max(0, _tb.SelectionStart - GetRepeat());
        private void MoveRight() => _tb.SelectionStart = Math.Min(_tb.TextLength, _tb.SelectionStart + GetRepeat());
        private void MoveUp()
        {
            int line = GetCurrentLine();
            int col = _tb.SelectionStart - GetLineStart(line);
            int target = Math.Max(0, line - GetRepeat());
            _tb.SelectionStart = Math.Min(GetLineEnd(target), GetLineStart(target) + col);
        }
        private void MoveDown()
        {
            int line = GetCurrentLine();
            int col = _tb.SelectionStart - GetLineStart(line);
            int target = Math.Min(_tb.GetLineFromCharIndex(_tb.TextLength), line + GetRepeat());
            _tb.SelectionStart = Math.Min(GetLineEnd(target), GetLineStart(target) + col);
        }
        private void MoveToLineStart() => _tb.SelectionStart = GetLineStart(GetCurrentLine());
        private void MoveToLineEnd() => _tb.SelectionStart = Math.Max(0, GetLineEnd(GetCurrentLine()));
        private void MoveToFirstNonBlank()
        {
            int s = GetLineStart(GetCurrentLine());
            int e = GetLineEnd(GetCurrentLine());
            int pos = s;
            while (pos < e && (_tb.Text[pos] == ' ' || _tb.Text[pos] == '\t')) pos++;
            _tb.SelectionStart = pos;
        }
        private void MoveWordForward()
        {
            int p = _tb.SelectionStart;
            for (int r = 0; r < GetRepeat(); r++)
                p = FindNextWord(p);
            _tb.SelectionStart = p;
        }
        private void MoveWordBackward()
        {
            int p = _tb.SelectionStart;
            for (int r = 0; r < GetRepeat(); r++)
                p = FindPrevWord(p);
            _tb.SelectionStart = p;
        }
        private void MoveToWordEnd()
        {
            int p = _tb.SelectionStart;
            for (int r = 0; r < GetRepeat(); r++)
                p = FindWordEnd(p);
            _tb.SelectionStart = p;
        }
        private void MoveToFirstLine() => _tb.SelectionStart = 0;
        private void MoveToLastLine() => _tb.SelectionStart = Math.Max(0, _tb.TextLength - 1);

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private int FindNextWord(int pos)
        {
            string t = _tb.Text;
            if (pos >= t.Length) return pos;
            // Skip current word
            while (pos < t.Length && IsWordChar(t[pos])) pos++;
            // Skip whitespace
            while (pos < t.Length && !IsWordChar(t[pos]) && t[pos] != '\n') pos++;
            return Math.Min(pos, t.Length);
        }
        private int FindPrevWord(int pos)
        {
            string t = _tb.Text;
            if (pos <= 0) return 0;
            pos = Math.Max(0, pos - 1);
            // Skip whitespace backward
            while (pos > 0 && !IsWordChar(t[pos]) && t[pos] != '\n') pos--;
            // Skip word backward
            while (pos > 0 && IsWordChar(t[pos - 1])) pos--;
            return pos;
        }
        private int FindWordEnd(int pos)
        {
            string t = _tb.Text;
            if (pos >= t.Length) return pos;
            // Skip whitespace forward
            while (pos < t.Length && !IsWordChar(t[pos]) && t[pos] != '\n') pos++;
            // Move to end of word
            while (pos + 1 < t.Length && IsWordChar(t[pos + 1])) pos++;
            return Math.Min(pos + 1, t.Length);
        }
        private void JumpToMatchingBracket()
        {
            int p = _tb.SelectionStart;
            if (p >= _tb.TextLength) return;
            char c = _tb.Text[p];
            char match = c switch { '(' => ')', ')' => '(', '{' => '}', '}' => '{', '[' => ']', ']' => '[', _ => '\0' };
            if (match == '\0') return;
            int dir = (c == '(' || c == '{' || c == '[') ? 1 : -1;
            int depth = 0;
            int i = p;
            while (i >= 0 && i < _tb.TextLength)
            {
                if (_tb.Text[i] == c) depth++;
                else if (_tb.Text[i] == match) { depth--; if (depth == 0) { _tb.SelectionStart = i; return; } }
                i += dir;
            }
        }
        #endregion

        #region Selection (Visual Mode)
        private void MoveSelection(int dx, int dy)
        {
            int start = _tb.SelectionStart;
            int len = _tb.SelectionLength;
            int anchor = len > 0 ? start : start;
            int newEnd = start + len;
            if (dy != 0)
            {
                int line = _tb.GetLineFromCharIndex(newEnd);
                int col = newEnd - GetLineStart(line);
                int target = Math.Max(0, Math.Min(_tb.GetLineFromCharIndex(_tb.TextLength), line + dy));
                newEnd = Math.Min(GetLineEnd(target), GetLineStart(target) + col);
            }
            else
            {
                newEnd = Math.Max(0, Math.Min(_tb.TextLength, newEnd + dx));
            }
            _tb.SelectionStart = Math.Min(anchor, newEnd);
            _tb.SelectionLength = Math.Abs(newEnd - anchor);
        }
        private void MoveToLineStartVisual()
        {
            int anchor = _tb.SelectionStart;
            int end = GetLineStart(GetCurrentLine());
            _tb.SelectionStart = Math.Min(anchor, end);
            _tb.SelectionLength = Math.Abs(end - anchor);
        }
        private void MoveToLineEndVisual()
        {
            int anchor = _tb.SelectionStart;
            int end = GetLineEnd(GetCurrentLine());
            _tb.SelectionStart = Math.Min(anchor, end);
            _tb.SelectionLength = Math.Abs(end - anchor);
        }
        private void MoveWordForwardVisual()
        {
            int anchor = _tb.SelectionStart;
            int end = FindNextWord(anchor + _tb.SelectionLength);
            _tb.SelectionStart = Math.Min(anchor, end);
            _tb.SelectionLength = Math.Abs(end - anchor);
        }
        private void MoveWordBackwardVisual()
        {
            int anchor = _tb.SelectionStart + _tb.SelectionLength;
            int end = FindPrevWord(anchor);
            _tb.SelectionStart = Math.Min(_tb.SelectionStart, end);
            _tb.SelectionLength = Math.Abs(end - _tb.SelectionStart);
        }
        #endregion

        #region Editing
        private void DeleteChar()
        {
            int len = Math.Min(GetRepeat(), _tb.TextLength - _tb.SelectionStart);
            if (len <= 0) return;
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }
        private void DeleteCharBefore()
        {
            int p = _tb.SelectionStart;
            if (p <= 0) return;
            _tb.SelectionStart = Math.Max(0, p - GetRepeat());
            _tb.SelectionLength = Math.Min(GetRepeat(), p);
            _tb.SelectedText = "";
        }
        private void DeleteLine()
        {
            int count = GetRepeat();
            int line = GetCurrentLine();
            int lastTotal = _tb.GetLineFromCharIndex(Math.Max(0, _tb.TextLength - 1));
            int endLine = Math.Min(line + count - 1, lastTotal);
            int start = GetLineStart(line);
            int end = GetLineEnd(endLine) + 1;
            if (end > _tb.TextLength) end = _tb.TextLength;
            int len = end - start;
            if (len <= 0) return;
            _lastYank = _tb.Text.Substring(start, len);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }
        private void DeleteToLineEnd()
        {
            int start = _tb.SelectionStart;
            int end = GetLineEnd(GetCurrentLine());
            if (end <= start) return;
            _lastYank = _tb.Text.Substring(start, end - start);
            _tb.SelectionLength = end - start;
            _tb.SelectedText = "";
        }
        private void DeleteWord()
        {
            int p = _tb.SelectionStart;
            int end = FindNextWord(p);
            if (end <= p) return;
            _tb.SelectionLength = end - p;
            _tb.SelectedText = "";
        }
        private void DeleteInnerWord()
        {
            int p = _tb.SelectionStart;
            int s = FindPrevWord(p);
            int e = FindNextWord(p);
            if (s >= e) return;
            _tb.SelectionStart = s == p ? FindPrevWord(s) : s;
            _tb.SelectionLength = e - _tb.SelectionStart;
            _tb.SelectedText = "";
        }
        private void DeleteToLineStart()
        {
            int start = GetLineStart(GetCurrentLine());
            int end = _tb.SelectionStart;
            if (end <= start) return;
            _tb.SelectionStart = start;
            _tb.SelectionLength = end - start;
            _tb.SelectedText = "";
        }
        private void YankLine()
        {
            int count = GetRepeat();
            int line = GetCurrentLine();
            int lastTotal = _tb.GetLineFromCharIndex(Math.Max(0, _tb.TextLength - 1));
            int endLine = Math.Min(line + count - 1, lastTotal);
            int start = GetLineStart(line);
            int end = GetLineEnd(endLine) + 1;
            if (end > _tb.TextLength) end = _tb.TextLength;
            _lastYank = start >= 0 && end > start ? _tb.Text.Substring(start, end - start) : "";
        }
        private void YankWord()
        {
            int p = _tb.SelectionStart;
            int e = FindNextWord(p);
            if (e > p) _lastYank = _tb.Text.Substring(p, e - p);
        }
        private void PasteAfter()
        {
            if (_lastYank == null) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(_lastYank.Length * count);
            for (int i = 0; i < count; i++) sb.Append(_lastYank);
            _tb.SelectionStart = p;
            _tb.SelectedText = sb.ToString();
            _tb.SelectionStart = p;
        }
        private void PasteBefore()
        {
            if (_lastYank == null) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(_lastYank.Length * count);
            for (int i = 0; i < count; i++) sb.Append(_lastYank);
            _tb.SelectionStart = p;
            _tb.SelectedText = sb.ToString();
            _tb.SelectionStart = p + sb.Length;
        }
        private void OpenLineBelow()
        {
            int line = GetCurrentLine();
            int end = GetLineEnd(line);
            _tb.SelectionStart = Math.Max(0, end);
            _tb.SelectedText = "\n";
            EnterMode(VimMode.Insert);
        }
        private void OpenLineAbove()
        {
            int line = GetCurrentLine();
            int start = GetLineStart(line);
            _tb.SelectionStart = start;
            _tb.SelectedText = "\n";
            _tb.SelectionStart = start;
            EnterMode(VimMode.Insert);
        }
        private void ToggleCase()
        {
            int p = _tb.SelectionStart;
            if (p >= _tb.TextLength) return;
            char c = _tb.Text[p];
            _tb.SelectionLength = 1;
            _tb.SelectedText = char.IsUpper(c) ? char.ToLower(c).ToString() : char.ToUpper(c).ToString();
            _tb.SelectionStart = p + 1;
        }
        private void IndentLine(int dir)
        {
            int line = GetCurrentLine();
            int start = GetLineStart(line);
            _tb.SelectionStart = start;
            _tb.SelectedText = dir > 0 ? "\t" : "";
            if (dir < 0)
            {
                int len = Math.Min(GetLineLength(line), 1);
                if (len <= 0) return;
                string txt = _tb.Text.Substring(start, len);
                if (txt == "\t" || txt == " ")
                {
                    _tb.SelectionStart = start;
                    _tb.SelectionLength = txt.Length;
                    _tb.SelectedText = "";
                }
            }
        }
        private void IndentSelection(int dir)
        {
            if (_tb.SelectionLength <= 0) { IndentLine(dir); return; }
            int s = _tb.SelectionStart;
            int e = s + _tb.SelectionLength;
            int sl = _tb.GetLineFromCharIndex(s);
            int el = _tb.GetLineFromCharIndex(e);
            for (int l = sl; l <= el; l++)
            {
                int ls = GetLineStart(l);
                _tb.SelectionStart = ls;
                if (dir > 0) _tb.SelectedText = "\t";
                else
                {
                    int lineLen = GetLineLength(l);
                    if (lineLen <= 0) continue;
                    string txt = _tb.Text.Substring(ls, Math.Min(lineLen, 1));
                    if (txt == "\t" || txt == " ")
                    {
                        _tb.SelectionStart = ls;
                        _tb.SelectionLength = txt.Length;
                        _tb.SelectedText = "";
                    }
                }
            }
            _tb.SelectionStart = s;
            _tb.SelectionLength = e - s;
        }
        private void JoinLines()
        {
            int line = GetCurrentLine();
            int end = GetLineEnd(line);
            if (end < 0 || end >= _tb.TextLength) return;
            int nextStart = end + 1;
            if (nextStart < _tb.TextLength)
            {
                _tb.SelectionStart = end;
                _tb.SelectionLength = nextStart - end;
                _tb.SelectedText = " ";
            }
        }
        private void CutSelection()
        {
            if (_tb.SelectionLength <= 0) return;
            _lastYank = _tb.SelectedText;
            _tb.SelectedText = "";
        }
        private void YankSelection()
        {
            if (_tb.SelectionLength > 0)
                _lastYank = _tb.SelectedText;
        }
        private void RepeatLast() { /* TODO: replay last non-motion command */ }
        #endregion

        #region Scrolling
        private void PageDown() { /* Editor handles this natively */ }
        private void PageUp() { /* Editor handles this natively */ }
        private void HalfPageDown() { /* Editor handles this natively */ }
        private void HalfPageUp() { /* Editor handles this natively */ }
        #endregion

        #region Undo/Redo
        private void SendCtrlZ() { _tb.Undo(); }
        private void SendCtrlR() { } // RichTextBox has no built-in Redo() method
        #endregion

        #region Search
        private void FindNext() { }
        private void FindPrevious() { }
        #endregion

        #region Char handling
        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return shift ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));
            if (key >= Keys.D0 && key <= Keys.D9)
                return shift ? ")!@#$%^&*("[key - Keys.D0] : (char)('0' + (key - Keys.D0));
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.Oemcomma) return shift ? '<' : ',';
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.Oemplus) return shift ? '+' : '=';
            if (key == Keys.OemOpenBrackets) return shift ? '{' : '[';
            if (key == Keys.OemCloseBrackets) return shift ? '}' : ']';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';
            if (key == Keys.OemBackslash) return shift ? '|' : '\\';
            if (key == Keys.OemSemicolon) return shift ? ':' : ';';
            if (key == Keys.OemQuotes) return shift ? '"' : '\'';
            if (key == Keys.Oemtilde) return shift ? '~' : '`';
            if (key == Keys.D7) return shift ? '&' : '7';
            if (key == Keys.Tab) return '\t';
            if (key == Keys.Escape) return '\x1B';
            return null;
        }

        private static Keys KeyFromChar(char c)
        {
            return c switch
            {
                'h' => Keys.H, 'j' => Keys.J, 'k' => Keys.K, 'l' => Keys.L,
                'w' => Keys.W, 'b' => Keys.B, 'e' => Keys.E,
                '0' => Keys.D0,
                'G' => Keys.G, 'g' => Keys.G, _ => Keys.None
            };
        }

        #endregion
    }
}

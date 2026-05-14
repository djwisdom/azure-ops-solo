using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    /// <summary>
    /// Represents the different modes in Vim.
    /// </summary>
    public enum VimMode
    {
        Normal, Insert, Visual, VisualLine, VisualBlock, Command,
        SearchForward, SearchBackward
    }

    /// <summary>
    /// Interface for Vim state classes in the State design pattern.
    /// Each mode implements this to handle its specific key processing and behavior.
    /// </summary>
    public interface IVimState
    {
        /// <summary>
        /// Processes a key press in the context of this state.
        /// </summary>
        /// <param name="keyData">The key data from the key event.</param>
        /// <param name="engine">Reference to the VimEngine for shared state and operations.</param>
        /// <returns>True if the key was consumed, false if it should be passed through.</returns>
        bool ProcessKey(Keys keyData, object engine);

        /// <summary>
        /// Called when entering this state.
        /// </summary>
        /// <param name="engine">Reference to the VimEngine.</param>
        void Enter(object engine);

        /// <summary>
        /// Called when exiting this state.
        /// </summary>
        /// <param name="engine">Reference to the VimEngine.</param>
        void Exit(object engine);
    }

    /// <summary>
    /// Base class for Vim states, providing common functionality.
    /// </summary>
    public abstract class VimStateBase : IVimState
    {
        public abstract bool ProcessKey(Keys keyData, object engine);
        public virtual void Enter(object engine) { }
        public virtual void Exit(object engine) { }

        /// <summary>
        /// Helper to extract key and modifiers from keyData.
        /// </summary>
        protected static (Keys key, bool ctrl, bool shift, bool alt) ParseKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0;
            bool shift = (keyData & Keys.Shift) != 0;
            bool alt = (keyData & Keys.Alt) != 0;
            return (key, ctrl, shift, alt);
        }

        /// <summary>
        /// Helper to convert key to char, considering shift.
        /// </summary>
        protected static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return shift ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));
            if (key >= Keys.D0 && key <= Keys.D9)
                return shift ? ")!@#$%^&*("[key - Keys.D0] : (char)('0' + (key - Keys.D0));
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemSemicolon) return shift ? ':' : ';';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.Oemcomma) return shift ? '<' : ',';
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            return null;
        }
    }

    /// <summary>
    /// Normal mode state: Default command mode for navigation and operations.
    /// </summary>
    public class NormalState : VimStateBase
    {
        private StringBuilder CommandBuffer = new();
        private int RepeatCount = 1;

        public override void Enter(object engine)
        {
            CommandBuffer.Clear();
            RepeatCount = 1;
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (alt) return false;

            // Repeat count
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                // Handle repeat count logic (similar to original)
                if (CommandBuffer.Length > 0 && char.IsDigit(CommandBuffer[0]))
                {
                    int val = int.Parse(CommandBuffer.ToString());
                    RepeatCount = val * 10 + (key - Keys.D0);
                    CommandBuffer.Append((char)('0' + (key - Keys.D0)));
                }
                else if (key != Keys.D0)
                {
                    RepeatCount = key - Keys.D0;
                    CommandBuffer.Append((char)('0' + (key - Keys.D0)));
                }
                return true;
            }

            char? ch = KeyToChar(key, shift);
            CommandBuffer.Append(ch);

            string buf = CommandBuffer.ToString();

            // Strip leading digits
            string stripped = buf;
            while (stripped.Length > 0 && char.IsDigit(stripped[0]))
                stripped = stripped[1..];

            // Handle commands (delegate to engine for complex logic)
            e.RepeatCount = RepeatCount;
            bool handled = e.HandleNormalBuffer(stripped, key, shift);
            if (handled)
            {
                ResetBuffer();
                return true;
            }

            if (!e.IsPrefixOfCommand(stripped))
            {
                ResetBuffer();
                return false;
            }

            return true;
        }

        public void ResetBuffer() { CommandBuffer.Clear(); RepeatCount = 1; }
    }

    /// <summary>
    /// Insert mode state: Typing mode where keys insert text.
    /// </summary>
    public class InsertState : VimStateBase
    {
        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.SelectionLength = 0;
                e.EnterMode(VimMode.Normal);
                return true;
            }
            return false; // Let key through for typing
        }
    }

    /// <summary>
    /// Visual mode state: Character-wise selection mode.
    /// </summary>
    public class VisualState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.SelectionLength = 0; // Start with no selection
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.SelectionLength = 0;
                e.EnterMode(VimMode.Normal);
                return true;
            }

            // Handle motions and operations (delegate to engine)
            return e.HandleVisualMode(key, ctrl, shift, false);
        }
    }

    /// <summary>
    /// Visual line mode state: Line-wise selection mode.
    /// </summary>
    public class VisualLineState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.SelectionLength = 0;
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.SelectionLength = 0;
                e.EnterMode(VimMode.Normal);
                return true;
            }
            return e.HandleVisualMode(key, ctrl, shift, true);
        }
    }

    /// <summary>
    /// Command mode state: Ex commands like :w, :q.
    /// </summary>
    public class CommandState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.CommandBuffer.Clear();
            e.CommandBuffer.Append(':');
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                e.ExecuteCommand(e.CommandBuffer.ToString().Trim());
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (e.CommandBuffer.Length > 1)
                    e.CommandBuffer.Remove(e.CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                e.CommandBuffer.Append(ch.Value);
                return true;
            }
            return false;
        }
            if (key == Keys.Enter)
            {
                engine.ExecuteCommand(engine.CommandBuffer.ToString().Trim());
                engine.CommandBuffer.Clear();
                engine.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (engine.CommandBuffer.Length > 1)
                    engine.CommandBuffer.Remove(engine.CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                engine.CommandBuffer.Append(ch.Value);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Search forward state: / search mode.
    /// </summary>
    public class SearchForwardState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.CommandBuffer.Clear();
            e.CommandBuffer.Append('/');
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                string pattern = e.CommandBuffer.Length > 1 ? e.CommandBuffer.ToString(1, e.CommandBuffer.Length - 1) : e.LastSearchPattern;
                if (!string.IsNullOrEmpty(pattern))
                {
                    e.LastSearchPattern = pattern;
                    e.LastSearchForward = true;
                    e.ExecuteSearch(e.SelectionStart + e.SelectionLength, true);
                }
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (e.CommandBuffer.Length > 1)
                    e.CommandBuffer.Remove(e.CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                e.CommandBuffer.Append(ch.Value);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Search backward state: ? search mode.
    /// </summary>
    public class SearchBackwardState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.CommandBuffer.Clear();
            e.CommandBuffer.Append('?');
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            VimEngine e = (VimEngine)engine;
            var (key, ctrl, shift, alt) = ParseKey(keyData);
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                string pattern = e.CommandBuffer.Length > 1 ? e.CommandBuffer.ToString(1, e.CommandBuffer.Length - 1) : e.LastSearchPattern;
                if (!string.IsNullOrEmpty(pattern))
                {
                    e.LastSearchPattern = pattern;
                    e.LastSearchForward = false;
                    e.ExecuteSearch(e.SelectionStart, false);
                }
                e.CommandBuffer.Clear();
                e.EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (e.CommandBuffer.Length > 1)
                    e.CommandBuffer.Remove(e.CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                e.CommandBuffer.Append(ch.Value);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Visual block mode state: Rectangular selection (placeholder for future enhancement).
    /// </summary>
    public class VisualBlockState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.SelectionLength = 0;
        }

        public override bool ProcessKey(Keys keyData, object engine)
        {
            // For now, delegate to VisualState (can enhance later)
            return new VisualState().ProcessKey(keyData, engine);
        }
    }

    /// <summary>
    /// Main Vim engine class, refactored to use the State pattern.
    /// </summary>
    public class VimEngine
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int EM_REDO = 0x042D;

        // State pattern: current state
        private IVimState _currentState;

        public VimMode CurrentMode { get; private set; } = VimMode.Normal;
        public bool Enabled { get; set; }
        public string CommandText => CommandBuffer.ToString();

        public event Action? ModeChanged;

        // Exposed for states
        public StringBuilder CommandBuffer { get; } = new();
        public string? LastYank { get; set; }
        public string LastSearchPattern { get; set; } = "";
        public bool LastSearchForward { get; set; } = true;
        public (string Action, string? Data, int Repeat)? LastAction { get; set; }
        public int RepeatCount { get; set; } = 1;

        // TextBox access
        public RichTextBox TextBox => _tb;
        public int SelectionStart { get => _tb.SelectionStart; set => _tb.SelectionStart = value; }
        public int SelectionLength { get => _tb.SelectionLength; set => _tb.SelectionLength = value; }
        public string Text => _tb.Text;
        public void Select(int start, int length) => _tb.Select(start, length);

        private RichTextBox _tb;
        private bool _pendingWindowCommand; // after Ctrl+W, waiting for second key

        // Multi-layered undo/redo system
        private class UndoPoint
        {
            public string Text { get; set; }
            public int SelectionStart { get; set; }
            public int SelectionLength { get; set; }
            public UndoPoint(string text, int selectionStart, int selectionLength)
            {
                Text = text;
                SelectionStart = selectionStart;
                SelectionLength = selectionLength;
            }
        }

        private readonly List<UndoPoint> _undoStack = new();
        private readonly List<UndoPoint> _redoStack = new();
        private UndoPoint? _lastSavedState;
        private bool _inUndoRedoOperation;

        public event Action? SaveRequested;
        public event Action<string>? SaveAsRequested;
        public event Action? CloseRequested;
        public event Action? VerticalSplitRequested;
        public event Action? HorizontalSplitRequested;
        public event Action? SplitCloseRequested;
        public event Action? SplitNextRequested;
        public event Action<bool>? InsertSpacesRequested;
        public event Action<int>? TabSizeRequested;
        public event Action<bool>? AutoIndentRequested;
        public event Action<bool>? SmartTabsRequested;
        public event Action<int>? GoToLineRequested;
        public event Action? TerminalRequested;
        public event Action<string>? CommandFeedback;
        public event Action<string>? FileOpenRequested;

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
            _currentState = new NormalState();
            // Create initial undo point
            CreateUndoPoint();
        }

        public void SetEditor(RichTextBox textBox)
        {
            _tb = textBox;
            // Reset undo/redo when switching editors
            ClearUndoRedoHistory();
            _currentState = new NormalState();
            CreateUndoPoint();
        }

        public void ClearUndoRedoHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _lastSavedState = null;
        }

        public void EnterMode(VimMode mode)
        {
            // Create undo point when entering/leaving insert mode
            if ((CurrentMode == VimMode.Insert && mode != VimMode.Insert) ||
                (CurrentMode != VimMode.Insert && mode == VimMode.Insert))
            {
                CreateUndoPoint();
            }

            _currentState?.Exit(this);
            CurrentMode = mode;
            ModeChanged?.Invoke();
            _currentState = mode switch
            {
                VimMode.Normal => new NormalState(),
                VimMode.Insert => new InsertState(),
                VimMode.Visual => new VisualState(),
                VimMode.VisualLine => new VisualLineState(),
                VimMode.VisualBlock => new VisualBlockState(),
                VimMode.Command => new CommandState(),
                VimMode.SearchForward => new SearchForwardState(),
                VimMode.SearchBackward => new SearchBackwardState(),
                _ => new NormalState()
            };
            CommandBuffer.Clear();
            RepeatCount = 1;
            _pendingWindowCommand = false;
            if (mode == VimMode.Normal && _tb.SelectionLength > 0)
                _tb.SelectionLength = 0;
        _currentState.Enter(this);
        }

        public bool ProcessKey(Keys keyData)
        {
            if (!Enabled) return false;

            Keys key = keyData & Keys.KeyCode;
            bool ctrl = (keyData & Keys.Control) != 0;
            bool shift = (keyData & Keys.Shift) != 0;
            bool alt = (keyData & Keys.Alt) != 0;

            if (alt) return false;

            // Ctrl+W prefix: consume and wait for the next key
            if (ctrl && key == Keys.W)
            {
                _pendingWindowCommand = true;
                return true;
            }

            // If a Ctrl+W window command is pending, handle the second key
            if (_pendingWindowCommand)
            {
                _pendingWindowCommand = false;
                if (key == Keys.W || key == Keys.Tab)
                {
                    SplitNextRequested?.Invoke();
                    return true;
                }
                if (key == Keys.C || key == Keys.Q)
                {
                    SplitCloseRequested?.Invoke();
                    return true;
                }
                if (key == Keys.Escape)
                    return true; // cancel window command
                // For any other key, fall through to normal processing
            }

            // Delegate to current state
            return _currentState.ProcessKey(keyData, this);
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

        public bool HandleVisualMode(Keys key, bool ctrl, bool shift, bool linewise)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                SelectionLength = 0;
                EnterMode(VimMode.Normal);
                return true;
            }

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
                CommandBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                ExecuteCommand(CommandBuffer.ToString().Trim());
                CommandBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (engine.CommandBuffer.Length > 1)
                    engine.CommandBuffer.Remove(engine.CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                engine.CommandBuffer.Append(ch.Value);
                return true;
            }

            return false;
        }

        private void EnterSearchMode(bool forward)
        {
            EnterMode(forward ? VimMode.SearchForward : VimMode.SearchBackward);
            CommandBuffer.Clear();
            CommandBuffer.Append(forward ? '/' : '?');
        }

        private bool ProcessSearchMode(Keys key, bool ctrl, bool shift)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                CommandBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Enter)
            {
                string pattern = CommandBuffer.Length > 1 ? CommandBuffer.ToString(1, CommandBuffer.Length - 1) : LastSearchPattern;
                if (!string.IsNullOrEmpty(pattern))
                {
                    LastSearchPattern = pattern;
                    LastSearchForward = CurrentMode == VimMode.SearchForward;
                    ExecuteSearch(LastSearchForward? 0 : _tb.TextLength, LastSearchForward);
                }
                CommandBuffer.Clear();
                EnterMode(VimMode.Normal);
                return true;
            }
            if (key == Keys.Back)
            {
                if (CommandBuffer.Length > 1)
                    CommandBuffer.Remove(CommandBuffer.Length - 1, 1);
                return true;
            }

            char? ch = KeyToChar(key, shift);
            if (ch != null && ch >= 32)
            {
                CommandBuffer.Append(ch.Value);
                return true;
            }

            return false;
        }

        public bool ExecuteSearch(int startFrom, bool forward)
        {
            if (string.IsNullOrEmpty(LastSearchPattern)) return false;
            string text = _tb.Text;
            // Update stored direction for n/N
            LastSearchForward = forward;

            // Determine case sensitivity: case-sensitive if pattern contains uppercase
            bool ignoreCase = !LastSearchPattern.Any(char.IsUpper);
            RegexOptions options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;

            try
            {
                if (forward)
                {
                    // Forward search from startFrom
                    string searchText = startFrom < text.Length ? text[startFrom..] : "";
                    Match match = Regex.Match(searchText, LastSearchPattern, options, TimeSpan.FromSeconds(1));
                    if (match.Success)
                    {
                        int found = startFrom + match.Index;
                        _tb.SelectionStart = found;
                        _tb.SelectionLength = match.Length;
                        _tb.ScrollToCaret();
                        return true;
                    }
                }
                else
                {
                    // Backward search: find the last match before startFrom
                    string searchText = startFrom > 0 ? text[..startFrom] : "";
                    MatchCollection matches = Regex.Matches(searchText, LastSearchPattern, options, TimeSpan.FromSeconds(1));
                    if (matches.Count > 0)
                    {
                        Match lastMatch = matches[^1];
                        _tb.SelectionStart = lastMatch.Index;
                        _tb.SelectionLength = lastMatch.Length;
                        _tb.ScrollToCaret();
                        return true;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Pattern too complex or causes timeout, fall back to plain search
                return ExecutePlainSearch(startFrom, forward);
            }
            return false;
        }

        private bool ExecutePlainSearch(int startFrom, bool forward)
        {
            string text = _tb.Text;
            var comparison = StringComparison.CurrentCultureIgnoreCase;
            if (forward)
            {
                int found = text.IndexOf(LastSearchPattern, Math.Min(startFrom, text.Length), comparison);
                if (found >= 0)
                {
                    _tb.SelectionStart = found;
                    _tb.SelectionLength = LastSearchPattern.Length;
                    _tb.ScrollToCaret();
                    return true;
                }
            }
            else
            {
                int found = -1;
                int searchUpTo = Math.Min(startFrom, text.Length);
                for (int i = 0; i <= searchUpTo - LastSearchPattern.Length; i++)
                {
                    if (text.AsSpan(i, LastSearchPattern.Length).Equals(LastSearchPattern, comparison))
                        found = i;
                }
                if (found >= 0)
                {
                    _tb.SelectionStart = found;
                    _tb.SelectionLength = LastSearchPattern.Length;
                    _tb.ScrollToCaret();
                    return true;
                }
            }
            return false;
        }

        private bool ProcessNormalMode(Keys key, bool ctrl, bool shift)
        {
            // Repeat count: digits before a command
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int d = key - Keys.D0;
                if (CommandBuffer.Length > 0 && char.IsDigit(CommandBuffer[0]))
                {
                    int val = int.Parse(CommandBuffer.ToString());
                    RepeatCount = val * 10 + d;
                    CommandBuffer.Append(d.ToString());
                }
                else if (d > 0)
                {
                    RepeatCount = d;
                    CommandBuffer.Append(d.ToString());
                }
                return true;
            }

            char? ch = KeyToChar(key, shift);
            CommandBuffer.Append(ch);

            string buf = CommandBuffer.ToString();

            // Single-key commands
            switch (key)
            {
                case Keys.I: EnterMode(VimMode.Insert); ResetBuffer(); return true;
                case Keys.A: MoveRight(); EnterMode(VimMode.Insert); ResetBuffer(); return true;
            }

            if (shift && ch == 'I') { MoveToLineStart(); EnterMode(VimMode.Insert); ResetBuffer(); return true; }
            if (shift && ch == 'A') { MoveToLineEnd(); EnterMode(VimMode.Insert); ResetBuffer(); return true; }

            // Enter command mode on ':'
            if (ch == ':') { EnterMode(VimMode.Command); CommandBuffer.Clear(); return true; }

            // Strip leading digit prefix (repeat count) before matching commands
            string stripped = buf;
            while (stripped.Length > 0 && char.IsDigit(stripped[0]))
                stripped = stripped[1..];

            // Handle the buffer
            bool handled = HandleNormalBuffer(stripped, key, shift);
            if (handled) { ResetBuffer(); return true; }

            // If buffer doesn't match any command, reset it
            if (!IsPrefixOfCommand(stripped))
            {
                ResetBuffer();
                return false;
            }

            // Buffer is a valid prefix of a multi-key command (e.g. "g" waiting for "gg").
            // Consume the key to prevent it from being typed into the editor.
            return true;
        }

        public bool HandleNormalBuffer(string buf, Keys key, bool shift)
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
                case "x": DeleteChar(); RecordAction("delete-char"); return true;
                case "X": DeleteCharBefore(); RecordAction("delete-char-before"); return true;
                case "dd": DeleteLine(); RecordAction("delete-line"); return true;
                case "D": case "d$": DeleteToLineEnd(); RecordAction("delete-to-line-end"); return true;
                case "dw": DeleteWord(); RecordAction("delete-word"); return true;
                case "diw": DeleteInnerWord(); RecordAction("delete-inner-word"); return true;
                case "d0": DeleteToLineStart(); RecordAction("delete-to-line-start"); return true;
                case "d^": DeleteToLineStart(); RecordAction("delete-to-line-start"); return true;

                // Yank & Paste
                case "yy": case "Y": YankLine(); RecordAction("yank-line"); return true;
                case "yw": YankWord(); return true;
                case "p": PasteAfter(); RecordAction("paste-after"); return true;
                case "P": PasteBefore(); RecordAction("paste-before"); return true;

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
                case ">>": IndentLine(1); RecordAction("indent"); return true;
                case "<<": IndentLine(-1); RecordAction("outdent"); return true;

                // Toggle case
                case "~": ToggleCase(); return true;

                // Join
                case "J": JoinLines(); return true;

                // Repeat
                case ".": RepeatLast(); return true;

                // Search
                case "/": EnterSearchMode(true); return true;
                case "?": EnterSearchMode(false); return true;
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
                case 'y': LastYank = _tb.SelectedText; break;
                case 'c': _tb.SelectedText = ""; EnterMode(VimMode.Insert); break;
            }

            _tb.SelectionLength = 0;
        }

        private void UpdateCommandLine() { /* status bar indicator managed by Form1 */ }
        private void ResetBuffer() { CommandBuffer.Clear(); RepeatCount = 1; }

        public bool IsPrefixOfCommand(string buf)
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

        public void ExecuteCommand(string cmd)
        {
            cmd = cmd.TrimStart(':');
            if (cmd == "set")
            {
                ShowNotification("Usage: :set option[=value], :set {option}?");
                return;
            }

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

            bool handled = true;

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
                case "close":
                    SplitCloseRequested?.Invoke();
                    break;
                case "e!":
                    ShowNotification("e! not implemented");
                    break;
                case "e":
                case "edit":
                    ShowNotification("Usage: :e <filename> or :e!");
                    break;
                case "w!":
                    SaveRequested?.Invoke();
                    break;
                case "wq!":
                    SaveRequested?.Invoke();
                    CloseRequested?.Invoke();
                    break;
                case "sp":
                case "split":
                    HorizontalSplitRequested?.Invoke();
                    break;
                case "vsp":
                case "vs":
                case "vsplit":
                    VerticalSplitRequested?.Invoke();
                    break;
                case "term":
                case "terminal":
                    TerminalRequested?.Invoke();
                    break;
                default:
                    handled = false;
                    break;
            }

            if (handled) return;

            // Handle :w filename and :write filename (save as)
            if (cmd.StartsWith("w ") || cmd.StartsWith("write "))
            {
                string filename = cmd[(cmd[0] == 'w' ? 2 : 6)..].Trim();
                if (!string.IsNullOrEmpty(filename))
                {
                    SaveAsRequested?.Invoke(filename);
                    return;
                }
            }

            // Handle :w filename and :write filename (save as)
            if (cmd.StartsWith("w ") || cmd.StartsWith("write "))
            {
                string filename = cmd[(cmd[0] == 'w' ? 2 : 6)..].Trim();
                if (!string.IsNullOrEmpty(filename))
                {
                    SaveAsRequested?.Invoke(filename);
                    return;
                }
            }

            // Handle :wq filename (save as then close)
            if (cmd.StartsWith("wq ") && cmd.Length > 3)
            {
                string filename = cmd[3..].Trim();
                if (!string.IsNullOrEmpty(filename))
                {
                    SaveAsRequested?.Invoke(filename);
                    CloseRequested?.Invoke();
                }
            }

            // Handle :e filename and :edit filename (open file)
            if (cmd.StartsWith("e ") || cmd.StartsWith("edit "))
            {
                string filename = cmd[cmd[0] == 'e' ? 2.. : 5..].Trim();
                if (!string.IsNullOrEmpty(filename))
                {
                    FileOpenRequested?.Invoke(filename);
                    return;
                }
            }

            if (!handled)
                ShowNotification($"Unknown command: {cmd}");
        }

        private void ShowNotification(string message)
        {
            CommandFeedback?.Invoke(message);
        }

        public void ExecuteSet(string args)
        {
            foreach (var part in args.Split(','))
            {
                string arg = part.Trim();

                if (arg.EndsWith("?"))
                {
                    string opt = arg[..^1];
                    ShowNotification($"set {opt}? — see :set {opt}=<value> to change");
                    continue;
                }

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
        public int GetCurrentLine() => TextBox.GetLineFromCharIndex(SelectionStart);
        public int GetLineStart(int line) => TextBox.GetFirstCharIndexFromLine(line);
        public int GetLineEnd(int line)
        {
            int next = TextBox.GetFirstCharIndexFromLine(line + 1);
            if (next >= 0) return Math.Max(0, next - 1);
            return Math.Max(0, Text.Length);
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
        public int GetRepeat() => Math.Max(1, RepeatCount);

        public void MoveLeft() => SelectionStart = Math.Max(0, SelectionStart - RepeatCount);
        public void MoveRight() => SelectionStart = Math.Min(Text.Length, SelectionStart + RepeatCount);
        public void MoveUp()
        {
            int line = GetCurrentLine();
            int col = SelectionStart - GetLineStart(line);
            int target = Math.Max(0, line - RepeatCount);
            SelectionStart = Math.Min(GetLineEnd(target), GetLineStart(target) + col);
        }
        public void MoveDown()
        {
            int line = GetCurrentLine();
            int col = SelectionStart - GetLineStart(line);
            int target = Math.Min(TextBox.GetLineFromCharIndex(Text.Length), line + RepeatCount);
            SelectionStart = Math.Min(GetLineEnd(target), GetLineStart(target) + col);
        }
        public void MoveToLineStart() => SelectionStart = GetLineStart(GetCurrentLine());
        public void MoveToLineEnd() => SelectionStart = Math.Max(0, GetLineEnd(GetCurrentLine()));
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
            // Skip whitespace and newlines
            while (pos < t.Length && !IsWordChar(t[pos])) pos++;
            return Math.Min(pos, t.Length);
        }
        private int FindPrevWord(int pos)
        {
            string t = _tb.Text;
            if (pos <= 0) return 0;
            pos = Math.Max(0, pos - 1);
            // Skip whitespace and newlines backward
            while (pos > 0 && !IsWordChar(t[pos])) pos--;
            // Skip word backward
            while (pos > 0 && IsWordChar(t[pos - 1])) pos--;
            return pos;
        }
        private int FindWordEnd(int pos)
        {
            string t = _tb.Text;
            if (pos >= t.Length) return pos;
            // Skip whitespace and newlines
            while (pos < t.Length && !IsWordChar(t[pos])) pos++;
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
            LastYank = _tb.Text.Substring(start, len);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }
        private void DeleteToLineEnd()
        {
            int start = _tb.SelectionStart;
            int end = GetLineEnd(GetCurrentLine());
            if (end <= start) return;
            LastYank = _tb.Text.Substring(start, end - start);
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
            LastYank = start >= 0 && end > start ? _tb.Text.Substring(start, end - start) : "";
        }
        private void YankWord()
        {
            int p = _tb.SelectionStart;
            int e = FindNextWord(p);
            if (e > p) LastYank = _tb.Text.Substring(p, e - p);
        }
        private void PasteAfter()
        {
            if (LastYank == null) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(LastYank.Length * count);
            for (int i = 0; i < count; i++) sb.Append(LastYank);
            _tb.SelectionStart = p;
            _tb.SelectedText = sb.ToString();
            _tb.SelectionStart = p;
        }
        private void PasteBefore()
        {
            if (LastYank == null) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(LastYank.Length * count);
            for (int i = 0; i < count; i++) sb.Append(LastYank);
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
            LastYank = _tb.SelectedText;
            _tb.SelectedText = "";
        }
        private void YankSelection()
        {
            if (_tb.SelectionLength > 0)
                LastYank = _tb.SelectedText;
        }
        private void RecordAction(string action, string? data = null)
        {
            LastAction = (action, data, 1);
            // Create undo point after operations that change text
            CreateUndoPoint();
        }

        private void RepeatLast()
        {
            if (LastAction == null) return;
            var (action, data, _) = LastAction.Value;
            switch (action)
            {
                case "delete-char": DeleteChar(); break;
                case "delete-char-before": DeleteCharBefore(); break;
                case "delete-line": DeleteLine(); break;
                case "delete-to-line-end": DeleteToLineEnd(); break;
                case "delete-word": DeleteWord(); break;
                case "delete-inner-word": DeleteInnerWord(); break;
                case "delete-to-line-start": DeleteToLineStart(); break;
                case "yank-line": YankLine(); break;
                case "paste-after": PasteAfter(); break;
                case "paste-before": PasteBefore(); break;
                case "indent": IndentLine(1); break;
                case "outdent": IndentLine(-1); break;
                case "toggle-case": ToggleCase(); break;
                case "join": JoinLines(); break;
            }
        }
        #endregion

        #region Scrolling
        private void PageDown()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height);
            for (int i = 0; i < lines; i++) MoveDown();
        }
        private void PageUp()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height);
            for (int i = 0; i < lines; i++) MoveUp();
        }
        private void HalfPageDown()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height / 2);
            for (int i = 0; i < lines; i++) MoveDown();
        }
        private void HalfPageUp()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height / 2);
            for (int i = 0; i < lines; i++) MoveUp();
        }
        #endregion

        #region Undo/Redo
        public void CreateUndoPoint()
        {
            if (_inUndoRedoOperation) return;

            var currentState = new UndoPoint(_tb.Text, _tb.SelectionStart, _tb.SelectionLength);

            // Don't create undo point if nothing changed
            if (_lastSavedState != null &&
                _lastSavedState.Text == currentState.Text &&
                _lastSavedState.SelectionStart == currentState.SelectionStart &&
                _lastSavedState.SelectionLength == currentState.SelectionLength)
            {
                return;
            }

            _undoStack.Add(currentState);
            _lastSavedState = currentState;
            _redoStack.Clear(); // Clear redo stack when new change is made
        }

        private void VimUndo()
        {
            if (_undoStack.Count == 0) return;

            _inUndoRedoOperation = true;

            // Save current state to redo stack
            var currentState = new UndoPoint(_tb.Text, _tb.SelectionStart, _tb.SelectionLength);
            _redoStack.Add(currentState);

            // Restore previous state
            var undoState = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            _tb.Text = undoState.Text;
            _tb.SelectionStart = undoState.SelectionStart;
            _tb.SelectionLength = undoState.SelectionLength;

            _lastSavedState = undoState;

            _inUndoRedoOperation = false;
        }

        private void VimRedo()
        {
            if (_redoStack.Count == 0) return;

            _inUndoRedoOperation = true;

            // Save current state to undo stack
            var currentState = new UndoPoint(_tb.Text, _tb.SelectionStart, _tb.SelectionLength);
            _undoStack.Add(currentState);

            // Restore next state
            var redoState = _redoStack[^1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            _tb.Text = redoState.Text;
            _tb.SelectionStart = redoState.SelectionStart;
            _tb.SelectionLength = redoState.SelectionLength;

            _lastSavedState = redoState;

            _inUndoRedoOperation = false;
        }

        public void SendCtrlZ() { VimUndo(); }
        public void SendCtrlR() { VimRedo(); }
        #endregion

        #region Search
        private void FindNext()
        {
            if (string.IsNullOrEmpty(LastSearchPattern)) return;
            int start = _tb.SelectionStart + _tb.SelectionLength;
            if (start >= _tb.TextLength) start = 0;
            if (!ExecuteSearch(start, true))
            {
                // Wrap around
                if (start > 0) ExecuteSearch(0, true);
            }
        }

        private void FindPrevious()
        {
            if (string.IsNullOrEmpty(LastSearchPattern)) return;
            int start = _tb.SelectionStart;
            if (!ExecuteSearch(start, false))
            {
                // Wrap around from end
                int len = _tb.TextLength;
                if (len > 0) ExecuteSearch(len, false);
            }
        }
        #endregion
    }
}

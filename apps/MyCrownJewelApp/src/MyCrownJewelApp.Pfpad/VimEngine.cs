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
        /// <param name="key">The key.</param>
        /// <param name="ctrl">True if Ctrl is pressed.</param>
        /// <param name="shift">True if Shift is pressed.</param>
        /// <param name="alt">True if Alt is pressed.</param>
        /// <param name="engine">Reference to the VimEngine for shared state and operations.</param>
        /// <returns>True if the key was consumed, false if it should be passed through.</returns>
        bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine);

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
        public abstract bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine);
        public virtual void Enter(object engine) { }
        public virtual void Exit(object engine) { }

        /// <summary>
        /// Helper to extract key and modifiers from keyData.
        /// </summary>
        public static (Keys key, bool ctrl, bool shift, bool alt) ParseKey(Keys keyData)
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
            {
                int d = key - Keys.D0;
                if (shift)
                {
                    return d switch
                    {
                        0 => ')',
                        1 => '!',
                        2 => '@',
                        3 => '#',
                        4 => '$',
                        5 => '%',
                        6 => '^',
                        7 => '&',
                        8 => '*',
                        9 => '(',
                        _ => null
                    };
                }
                else
                {
                    return (char)('0' + d);
                }
            }
            if (key == Keys.Space) return ' ';
            if (key == Keys.OemSemicolon) return shift ? ':' : ';';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.Oemcomma) return shift ? '<' : ',';
            if (key == Keys.OemMinus) return shift ? '_' : '-';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';
            if (key == Keys.OemQuotes) return shift ? '"' : '\'';
            if (key == Keys.Oemplus) return shift ? '+' : '=';
            if (key == Keys.OemOpenBrackets) return shift ? '{' : '[';
            if (key == Keys.OemCloseBrackets) return shift ? '}' : ']';
            if (key == Keys.OemPipe) return shift ? '|' : '\\';
            if (key == Keys.Oemtilde) return shift ? '~' : '`';
            return null;
        }

        /// <summary>
        /// Helper to convert char to key.
        /// </summary>
        protected static Keys KeyFromChar(char c)
        {
            if (c >= 'a' && c <= 'z') return (Keys)(Keys.A + (c - 'a'));
            if (c >= 'A' && c <= 'Z') return (Keys)(Keys.A + (c - 'A'));
            if (c >= '0' && c <= '9') return (Keys)(Keys.D0 + (c - '0'));
            if (c == ' ') return Keys.Space;
            if (c == ';') return Keys.OemSemicolon;
            if (c == ':') return Keys.OemSemicolon; // shift
            if (c == '.') return Keys.OemPeriod;
            if (c == ',') return Keys.Oemcomma;
            if (c == '-') return Keys.OemMinus;
            if (c == '_') return Keys.OemMinus; // shift
            return Keys.None;
        }
    }

    /// <summary>
    /// Normal mode state: Default command mode for navigation and operations.
    /// </summary>
    public class NormalState : VimStateBase
    {
        private StringBuilder CommandBuffer = new();
        private int RepeatCount = 1;
        private bool IsRecording = false;
        private char CurrentRecordingRegister = '\0';
        private StringBuilder RecordingBuffer = new();
        private bool WaitingForRecordRegister = false;
        private bool WaitingForPlaybackRegister = false;
        private bool WaitingForMarkRegister = false;
        private bool WaitingForJumpMark = false;

        public override void Enter(object engine)
        {
            CommandBuffer.Clear();
            RepeatCount = 1;
        }

        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
            if (alt) return false;

            // Escape (or Ctrl+[) in Normal mode: clear pending buffer
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                ResetBuffer();
                return true;
            }

            // Ctrl+key bindings — handled before char processing to avoid polluting the command buffer
            if (ctrl)
            {
                switch (key)
                {
                    case Keys.R: e.SendCtrlR(); ResetBuffer(); return true;       // redo
                    case Keys.Z: e.SendCtrlZ(); ResetBuffer(); return true;       // undo
                    case Keys.F: e.PageDown(); ResetBuffer(); return true;        // page down
                    case Keys.B: e.PageUp(); ResetBuffer(); return true;          // page up
                    case Keys.D: e.HalfPageDown(); ResetBuffer(); return true;    // half page down
                    case Keys.U: e.HalfPageUp(); ResetBuffer(); return true;      // half page up
                }
                return false; // pass unknown Ctrl+key through (Ctrl+S, Ctrl+C, etc.)
            }

            char? ch = KeyToChar(key, shift);

            // Handle key mappings
            if (ch.HasValue)
            {
                string potential = CommandBuffer.ToString() + ch.Value;
                if (e.KeyMappings.TryGetValue(potential, out string? mapped))
                {
                    var keys = e.ParseKeys(mapped);
                    foreach (var k in keys)
                    {
                        e.EnqueuePending(k.key, k.ctrl, k.shift, k.alt);
                    }
                    ResetBuffer();
                    return true;
                }
            }

            // Handle macro recording/playback before buffer
            if (ch.HasValue)
            {
                if (IsRecording && ch == 'q')
                {
                    // Stop recording
                    e.Registers[CurrentRecordingRegister] = RecordingBuffer.ToString();
                    IsRecording = false;
                    CurrentRecordingRegister = '\0';
                    RecordingBuffer.Clear();
                    ResetBuffer();
                    return true;
                }

                if (WaitingForRecordRegister)
                {
                    if (char.IsLetter(ch.Value))
                    {
                        CurrentRecordingRegister = ch.Value;
                        if (!e.Registers.ContainsKey(CurrentRecordingRegister)) e.Registers[CurrentRecordingRegister] = "";
                        IsRecording = true;
                        WaitingForRecordRegister = false;
                        RecordingBuffer.Clear();
                        return true;
                    }
                    else
                    {
                        WaitingForRecordRegister = false;
                        ResetBuffer();
                        return false;
                    }
                }

                if (WaitingForPlaybackRegister)
                {
                    if (char.IsLetter(ch.Value))
                    {
                        if (e.Registers.TryGetValue(ch.Value, out string? macro) && macro != null)
                        {
                            e.PlaybackMacro(macro);
                        }
                        WaitingForPlaybackRegister = false;
                        ResetBuffer();
                        return true;
                    }
                    else
                    {
                        WaitingForPlaybackRegister = false;
                        ResetBuffer();
                        return false;
                    }
                }

                if (WaitingForMarkRegister)
                {
                    if (char.IsLetter(ch.Value))
                    {
                        e.Marks[ch.Value] = e.TextBox.SelectionStart;
                        WaitingForMarkRegister = false;
                        ResetBuffer();
                        return true;
                    }
                    else
                    {
                        WaitingForMarkRegister = false;
                        ResetBuffer();
                        return false;
                    }
                }

                if (WaitingForJumpMark)
                {
                    if (char.IsLetter(ch.Value) && e.Marks.TryGetValue(ch.Value, out int pos))
                    {
                        e.TextBox.SelectionStart = pos;
                        e.TextBox.SelectionLength = 0;
                        WaitingForJumpMark = false;
                        ResetBuffer();
                        return true;
                    }
                    else
                    {
                        WaitingForJumpMark = false;
                        ResetBuffer();
                        return false;
                    }
                }

                if (ch == 'q' && !IsRecording)
                {
                    WaitingForRecordRegister = true;
                    ResetBuffer();
                    return true;
                }

                if (ch == '@')
                {
                    WaitingForPlaybackRegister = true;
                    ResetBuffer();
                    return true;
                }

                if (ch == 'm' && !WaitingForMarkRegister)
                {
                    WaitingForMarkRegister = true;
                    ResetBuffer();
                    return true;
                }

                if (ch == '\'')
                {
                    WaitingForJumpMark = true;
                    ResetBuffer();
                    return true;
                }

                if (ch == '`')
                {
                    WaitingForJumpMark = true;
                    ResetBuffer();
                    return true;
                }
            }

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
                if (IsRecording) RecordingBuffer.Append(ch);
                return true;
            }

            if (ch.HasValue)
            {
                CommandBuffer.Append(ch.Value);

            string buf = CommandBuffer.ToString();

            // Strip leading digits
            string stripped = buf;
            while (stripped.Length > 0 && char.IsDigit(stripped[0]))
                stripped = stripped[1..];

            // Handle register prefix
            if (stripped.StartsWith('"') && stripped.Length > 1)
            {
                char reg = stripped[1];
                if (char.IsLetter(reg))
                {
                    e.CurrentRegister = char.ToLower(reg);
                    e.IsAppendRegister = char.IsUpper(reg);
                    stripped = stripped[2..];
                }
                else
                {
                    ResetBuffer();
                    return false;
                }
            }

            // Register prefix consumed but no operator yet — keep waiting
            if (stripped.Length == 0)
            {
                if (IsRecording && ch.HasValue) RecordingBuffer.Append(ch);
                return true;
            }

            // Handle commands (delegate to engine for complex logic)
            e.RepeatCount = RepeatCount;
            bool handled = e.HandleNormalBuffer(stripped, key, shift);
            if (handled)
            {
                ResetBuffer();
                e.CurrentRegister = '"';
                e.IsAppendRegister = false;
                if (IsRecording && ch.HasValue) RecordingBuffer.Append(ch);
                return true;
            }

                if (!e.IsPrefixOfCommand(stripped))
                {
                    ResetBuffer();
                    return false;
                }

                if (IsRecording) RecordingBuffer.Append(ch);
                return true;
            }

            return false;
        }

        public void ResetBuffer() { CommandBuffer.Clear(); RepeatCount = 1; }
    }

    /// <summary>
    /// Insert mode state: Typing mode where keys insert text.
    /// </summary>
    public class InsertState : VimStateBase
    {
        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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

        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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

        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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

        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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
    }

    /// <summary>
    /// Search forward state: / search mode.
    /// </summary>
    public class SearchForwardState : VimStateBase
    {
        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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
        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
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
    /// Visual block mode state: Rectangular (column) selection.
    /// </summary>
    public class VisualBlockState : VimStateBase
    {
        public override void Enter(object engine)
        {
            VimEngine e = (VimEngine)engine;
            e.SelectionLength = 0;
            int line = e.GetCurrentLine();
            int col = e.GetCurrentColumn();
            e.BlockMinLine = e.BlockMaxLine = line;
            e.BlockMinCol = e.BlockMaxCol = col;
        }

        public override bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt, object engine)
        {
            VimEngine e = (VimEngine)engine;
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                e.EnterMode(VimMode.Normal);
                return true;
            }
            return e.HandleVisualBlockMode(key, ctrl, shift);
        }
    }

    /// <summary>
    /// Main Vim engine class, refactored to use the State pattern.
    /// </summary>
    public partial class VimEngine
    {
        [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
        private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int EM_REDO = 0x042D;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;

        // State pattern: current state
        private IVimState _currentState;
        private bool _pendingWindowCommand = false;
        private bool _inMacroPlayback = false;
        public Dictionary<char, string> Registers = new();
        public char CurrentRegister = '"';
        public bool IsAppendRegister = false;
        public Dictionary<char, int> Marks = new();
        public int BlockMinLine, BlockMaxLine, BlockMinCol, BlockMaxCol;
        public event Action<bool>? OnShowLineNumbersChanged;
        public event Action<bool>? OnRelativeNumbersChanged;
        public event Action<bool>? OnGutterVisibilityChanged;
        public Dictionary<string, string> KeyMappings = new();

        public struct PendingKey
        {
            public Keys key;
            public bool ctrl, shift, alt;
        }
        public Queue<PendingKey> PendingKeys = new();


        public void SetRegister(char reg, bool append, string text)
        {
            if (reg == '"')
                LastYank = text;
            else
            {
                if (append)
                    Registers[reg] = Registers.GetValueOrDefault(reg, "") + text;
                else
                    Registers[reg] = text;
            }
        }

        public string GetRegister(char reg)
        {
            if (reg == '"')
                return LastYank ?? "";
            else
                return Registers.GetValueOrDefault(reg, "");
        }

        public int GetCurrentColumn()
        {
            int lineStart = GetLineStart(GetCurrentLine());
            return _tb.SelectionStart - lineStart;
        }

        public VimMode CurrentMode { get; private set; } = VimMode.Normal;
        public bool Enabled { get; set; }
        public string CommandText => CommandBuffer.ToString();

        public event Action? ModeChanged;

        public static Keys KeyFromChar(char c)
        {
            if (c >= 'a' && c <= 'z') return (Keys)(Keys.A + (c - 'a'));
            if (c >= 'A' && c <= 'Z') return (Keys)(Keys.A + (c - 'A'));
            if (c >= '0' && c <= '9') return (Keys)(Keys.D0 + (c - '0'));
            if (c == ' ') return Keys.Space;
            if (c == ';') return Keys.OemSemicolon;
            if (c == ':') return Keys.OemSemicolon; // shift
            if (c == '.') return Keys.OemPeriod;
            if (c == ',') return Keys.Oemcomma;
            if (c == '-') return Keys.OemMinus;
            if (c == '_') return Keys.OemMinus; // shift
            return Keys.None;
        }

        // Exposed for states
        public StringBuilder CommandBuffer { get; } = new();
        public string? LastYank { get; set; }
        public string LastSearchPattern { get; set; } = "";
        public bool LastSearchForward { get; set; } = true;
        public (string Action, string? Data, int Repeat)? LastAction { get; set; }
        private string? _lastTextObject = null;
        private int _lastInsertPosition = -1;
        public int RepeatCount { get; set; } = 1;
        public char LastFindChar { get; set; } = '\0';
        public bool LastFindForward { get; set; } = true;
        public bool LastFindTill { get; set; } = false;

        // TextBox access
        public RichTextBox TextBox => _tb;
        public int SelectionStart { get => _tb.SelectionStart; set => _tb.SelectionStart = value; }
        public int SelectionLength { get => _tb.SelectionLength; set => _tb.SelectionLength = value; }
        public string Text => _tb.Text;
        public void Select(int start, int length) => _tb.Select(start, length);

        private RichTextBox _tb;

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

        public event Action? TerminalRequested;
        public event Action<string>? CommandFeedback;

        public bool ShowLineNumbers { get; set; } = true;
        public bool RelativeNumbers { get; set; } = false;
        public bool GutterVisible { get; set; } = true;
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
                if (CurrentMode == VimMode.Insert)
                    _lastInsertPosition = _tb.SelectionStart;
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

        public bool ProcessKey(Keys key, bool ctrl, bool shift, bool alt)
        {
            if (!Enabled) return false;
            if (PendingKeys.Count > 0)
            {
                var pk = PendingKeys.Dequeue();
                return ProcessKey(pk.key, pk.ctrl, pk.shift, pk.alt);
            }

            if (ctrl && key == Keys.W)
            {
                _pendingWindowCommand = true;
                return true;
            }

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

            return _currentState.ProcessKey(key, ctrl, shift, alt, this);
        }

        public void EnqueuePending(Keys key, bool ctrl, bool shift, bool alt)
        {
            PendingKeys.Enqueue(new PendingKey { key = key, ctrl = ctrl, shift = shift, alt = alt });
        }



        public PendingKey[] ParseKeys(string s)
        {
            List<PendingKey> keys = new();
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int end = s.IndexOf('>', i);
                    if (end > i)
                    {
                        string special = s[(i + 1)..end];
                        Keys k = Keys.None;
                        bool c = false, sh = false, a = false;
                        switch (special)
                        {
                            case "Esc": k = Keys.Escape; break;
                            case "CR": case "Enter": k = Keys.Enter; break;
                            case "Tab": k = Keys.Tab; break;
                            case "BS": case "Backspace": k = Keys.Back; break;
                            case "Space": k = Keys.Space; break;
                            case "C-": // Ctrl
                                if (end + 1 < s.Length)
                                {
                                    char next = s[end + 1];
                                    k = CharToKey(next);
                                    c = true;
                                    i = end + 2;
                                    continue;
                                }
                                break;
                        }
                        if (k != Keys.None)
                        {
                            keys.Add(new PendingKey { key = k, ctrl = c, shift = sh, alt = a });
                        }
                        i = end + 1;
                    }
                    else
                    {
                        keys.Add(new PendingKey { key = (Keys)s[i], ctrl = false, shift = false, alt = false });
                        i++;
                    }
                }
                else
                {
                    keys.Add(new PendingKey { key = (Keys)s[i], ctrl = false, shift = false, alt = false });
                    i++;
                }
            }
            return keys.ToArray();
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
                    if (shift) { IndentSelection(1); EnterMode(VimMode.Normal); return true; }
                    return false;
                case Keys.Oemcomma:
                    if (shift) { IndentSelection(-1); EnterMode(VimMode.Normal); return true; }
                    return false;
                case Keys.V:
                    if (CurrentMode == VimMode.VisualLine)
                        EnterMode(VimMode.Visual);
                    else
                        EnterMode(VimMode.VisualLine);
                    return true;
            }
            return false;
        }

        public bool HandleVisualBlockMode(Keys key, bool ctrl, bool shift)
        {
            if (key == Keys.Escape || (ctrl && key == Keys.OemOpenBrackets))
            {
                EnterMode(VimMode.Normal);
                return true;
            }

            int minLine = Math.Min(BlockMinLine, BlockMaxLine);
            int maxLine = Math.Max(BlockMinLine, BlockMaxLine);
            int minCol = Math.Min(BlockMinCol, BlockMaxCol);
            int maxCol = Math.Max(BlockMinCol, BlockMaxCol);

            switch (key)
            {
                case Keys.H: case Keys.Left:  BlockMaxCol = Math.Max(0, BlockMaxCol - 1); UpdateBlockSelection(); return true;
                case Keys.L: case Keys.Right: BlockMaxCol = BlockMaxCol + 1; UpdateBlockSelection(); return true;
                case Keys.J: case Keys.Down:  BlockMaxLine = Math.Min(_tb.GetLineFromCharIndex(_tb.TextLength - 1), BlockMaxLine + 1); UpdateBlockSelection(); return true;
                case Keys.K: case Keys.Up:    BlockMaxLine = Math.Max(0, BlockMaxLine - 1); UpdateBlockSelection(); return true;
                case Keys.D: case Keys.X:     DeleteBlock(); return true;
                case Keys.Y:                  YankBlock(); return true;
                case Keys.I:                  BlockInsert(); return true;
                case Keys.A:                  BlockAppend(); return true;
                case Keys.C:                  DeleteBlock(); EnterMode(VimMode.Insert); return true;
            }
            return false;
        }

        private void UpdateBlockSelection()
        {
            int startLine = Math.Min(BlockMinLine, BlockMaxLine);
            int endLine = Math.Max(BlockMinLine, BlockMaxLine);
            int startCol = Math.Min(BlockMinCol, BlockMaxCol);
            int endCol = Math.Max(BlockMinCol, BlockMaxCol);

            int start = GetLineStart(startLine) + startCol;
            int end = GetLineStart(endLine) + endCol;

            if (end >= _tb.TextLength) end = _tb.TextLength - 1;

            _tb.SelectionStart = Math.Min(start, end);
            _tb.SelectionLength = Math.Abs(end - start);
        }

        public void BlockInsert()
        {
            // Insert at the left edge of the block on every line
            int col = Math.Min(BlockMinCol, BlockMaxCol);
            for (int line = BlockMinLine; line <= BlockMaxLine; line++)
            {
                int pos = GetLineStart(line) + col;
                _tb.SelectionStart = pos;
                _tb.SelectedText = "";
            }
            EnterMode(VimMode.Insert);
        }

        public void BlockAppend()
        {
            // Append at the right edge of the block on every line
            int col = Math.Max(BlockMinCol, BlockMaxCol) + 1;
            for (int line = BlockMinLine; line <= BlockMaxLine; line++)
            {
                int pos = Math.Min(GetLineStart(line) + col, GetLineEnd(line));
                _tb.SelectionStart = pos;
                _tb.SelectedText = "";
            }
            EnterMode(VimMode.Insert);
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

                // Paragraph motions
                case "{": MoveParaBackward(); return true;
                case "}": MoveParaForward(); return true;

                // Line motions
                case "-": MoveToPrevLineFirstNonBlank(); return true;
                case "+": MoveToNextLineFirstNonBlank(); return true;
                case "_": MoveToFirstNonBlank(); return true;
                case "|": GoToColumn(RepeatCount); return true;

                // Repeat last f/F/t/T
                case ";": RepeatFindChar(false); return true;
                case ",": RepeatFindChar(true); return true;

                // Insert
                case "i": EnterMode(VimMode.Insert); return true;
                case "a": MoveRight(); EnterMode(VimMode.Insert); return true;
                case "gi": if (_lastInsertPosition >= 0) _tb.SelectionStart = _lastInsertPosition; EnterMode(VimMode.Insert); return true;
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
                case "di\"": DeleteInnerQuotes('"'); return true;
                case "di'": DeleteInnerQuotes('\''); return true;
                case "di`": DeleteInnerQuotes('`'); return true;
                case "di(": case "di)": DeleteInnerBrackets('(', ')'); return true;
                case "di[": case "di]": DeleteInnerBrackets('[', ']'); return true;
                case "di{": case "di}": DeleteInnerBrackets('{', '}'); return true;
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
                case "ci\"": ChangeInnerQuotes('"'); return true;
                case "ci'": ChangeInnerQuotes('\''); return true;
                case "ci`": ChangeInnerQuotes('`'); return true;
                case "ci(": case "ci)": ChangeInnerBrackets('(', ')'); return true;
                case "ci[": case "ci]": ChangeInnerBrackets('[', ']'); return true;
                case "ci{": case "ci}": ChangeInnerBrackets('{', '}'); return true;
                case "s": DeleteChar(); EnterMode(VimMode.Insert); return true;
                case "S": DeleteLine(); EnterMode(VimMode.Insert); return true;

                // Visual
                case "v": EnterMode(VimMode.Visual); _tb.SelectionLength = 0; return true;
                case "V": EnterMode(VimMode.VisualLine); _tb.SelectionLength = 0; return true;
                case "\x16": EnterMode(VimMode.VisualBlock); return true;

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
                case "/": EnterMode(VimMode.SearchForward); return true;
                case "?": EnterMode(VimMode.SearchBackward); return true;
                case "n": FindNext(); return true;
                case "N": FindPrevious(); return true;
                case "zz": CenterCurrentLine(); return true;
                case "zt": ScrollCurrentLineToTop(); return true;
                case "zb": ScrollCurrentLineToBottom(); return true;
                case "*": SearchWordUnderCursor(true); return true;
                case "#": SearchWordUnderCursor(false); return true;

                // Command mode
                case ":": EnterMode(VimMode.Command); return true;

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
                case "f": case "F": case "t": case "T":  // find-char motions
                case "r":                                  // replace char
                case "[": case "]":                        // bracket jumps
                case "=":                                  // auto-indent operator
                    return false; // wait for more chars
            }

            // Handle d{motion}, y{motion}, c{motion}
            if (buf.Length == 2)
            {
                char op = buf[0];
                char arg = buf[1];

                // f/F/t/T find-char; r replace-char; [x ]x bracket jumps; == auto-indent
                switch (op)
                {
                    case 'f': FindCharOnLine(arg, true, false); return true;
                    case 'F': FindCharOnLine(arg, false, false); return true;
                    case 't': FindCharOnLine(arg, true, true); return true;
                    case 'T': FindCharOnLine(arg, false, true); return true;
                    case 'r': ReplaceChar(arg, RepeatCount); RecordAction("replace-char", arg.ToString()); return true;
                    case '[':
                        if (arg == '[') { MoveParaBackward(); return true; }
                        if (arg == '{') { JumpToUnmatchedBracket('{', '}', false); return true; }
                        if (arg == '(') { JumpToUnmatchedBracket('(', ')', false); return true; }
                        return true; // consume unknown [x
                    case ']':
                        if (arg == ']') { MoveParaForward(); return true; }
                        if (arg == '}') { JumpToUnmatchedBracket('{', '}', true); return true; }
                        if (arg == ')') { JumpToUnmatchedBracket('(', ')', true); return true; }
                        return true; // consume unknown ]x
                    case '=':
                        if (arg == '=') { AutoIndentLine(); RecordAction("auto-indent"); return true; }
                        return true;
                }

                // d/y/c + motion key
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
                    || c == '<' || c == '>' || c == '"' || c == 'z' || c == 'Z'
                    || c == 'f' || c == 'F' || c == 't' || c == 'T'  // find-char
                    || c == 'r'                                         // replace-char
                    || c == '[' || c == ']'                            // bracket jumps
                    || c == '=';                                        // auto-indent operator
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
                if (buf == "diw" || buf == "ciw" || buf == "yiw" || buf == "daw" ||
                    buf.StartsWith("di\"") || buf.StartsWith("ci\"") ||
                    buf.StartsWith("di'") || buf.StartsWith("ci'") ||
                    buf.StartsWith("di`") || buf.StartsWith("ci`") ||
                    buf.StartsWith("di(") || buf.StartsWith("ci(") ||
                    buf.StartsWith("di[") || buf.StartsWith("ci[") ||
                    buf.StartsWith("di{") || buf.StartsWith("ci{"))
                    return true;
            }
            return false;
        }

        public void ExecuteCommand(string cmd)
        {
            bool handled = false;
            cmd = cmd.TrimStart(':');
            if (cmd == "set")
            {
                ShowNotification("Usage: :set option[=value], :set {option}?");
                return;
            }

            if (cmd.StartsWith("set "))
            {
                string setCmd = cmd[4..].Trim();
                switch (setCmd)
                {
                    case "nu":
                        ShowLineNumbers = true;
                        RelativeNumbers = false;
                        OnShowLineNumbersChanged?.Invoke(true);
                        OnRelativeNumbersChanged?.Invoke(false);
                        CommandFeedback?.Invoke("nu=on rnu=off gutter=on");
                        handled = true;
                        break;
                    case "nonu":
                        ShowLineNumbers = false;
                        RelativeNumbers = false;
                        OnShowLineNumbersChanged?.Invoke(false);
                        OnRelativeNumbersChanged?.Invoke(false);
                        CommandFeedback?.Invoke("nu=off rnu=off gutter=on");
                        handled = true;
                        break;
                    case "rnu":
                        ShowLineNumbers = true;
                        RelativeNumbers = true;
                        OnShowLineNumbersChanged?.Invoke(true);
                        OnRelativeNumbersChanged?.Invoke(true);
                        CommandFeedback?.Invoke("nu=on rnu=on gutter=on");
                        handled = true;
                        break;
                    case "nornu":
                        RelativeNumbers = false;
                        OnRelativeNumbersChanged?.Invoke(false);
                        CommandFeedback?.Invoke("nu=on rnu=off gutter=on");
                        handled = true;
                        break;
                    case "gutter":
                        GutterVisible = true;
                        OnGutterVisibilityChanged?.Invoke(true);
                        CommandFeedback?.Invoke("nu=on rnu=off gutter=on");
                        handled = true;
                        break;
                    case "nogutter":
                        GutterVisible = false;
                        OnGutterVisibilityChanged?.Invoke(false);
                        CommandFeedback?.Invoke("nu=on rnu=off gutter=off");
                        handled = true;
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
                case "set nu":
                    ShowLineNumbers = true;
                    RelativeNumbers = false;
                    OnShowLineNumbersChanged?.Invoke(true);
                    OnRelativeNumbersChanged?.Invoke(false);
                    CommandFeedback?.Invoke($"nu=on rnu=off gutter={(GutterVisible ? "on" : "off")}");
                    handled = true;
                    break;
                case "set nonu":
                    ShowLineNumbers = false;
                    RelativeNumbers = false;
                    OnShowLineNumbersChanged?.Invoke(false);
                    OnRelativeNumbersChanged?.Invoke(false);
                    CommandFeedback?.Invoke($"nu=off rnu=off gutter={(GutterVisible ? "on" : "off")}");
                    handled = true;
                    break;
                case "set rnu":
                    ShowLineNumbers = true;
                    RelativeNumbers = true;
                    OnShowLineNumbersChanged?.Invoke(true);
                    OnRelativeNumbersChanged?.Invoke(true);
                    CommandFeedback?.Invoke($"nu=on rnu=on gutter={(GutterVisible ? "on" : "off")}");
                    handled = true;
                    break;
                case "set nornu":
                    RelativeNumbers = false;
                    OnRelativeNumbersChanged?.Invoke(false);
                    CommandFeedback?.Invoke($"nu={(ShowLineNumbers ? "on" : "off")} rnu=off gutter={(GutterVisible ? "on" : "off")}");
                    handled = true;
                    break;
                case "set gutter":
                    GutterVisible = true;
                    OnGutterVisibilityChanged?.Invoke(true);
                    CommandFeedback?.Invoke($"nu={(ShowLineNumbers ? "on" : "off")} rnu={(RelativeNumbers ? "on" : "off")} gutter=on");
                    handled = true;
                    break;
                case "set nogutter":
                    GutterVisible = false;
                    OnGutterVisibilityChanged?.Invoke(false);
                    CommandFeedback?.Invoke($"nu={(ShowLineNumbers ? "on" : "off")} rnu={(RelativeNumbers ? "on" : "off")} gutter=off");
                    handled = true;
                    break;

                case "term":
                case "terminal":
                    TerminalRequested?.Invoke();
                    break;
                case "noh":
                case "nohlsearch":
                    LastSearchPattern = "";
                    CommandFeedback?.Invoke("search highlight cleared");
                    handled = true;
                    break;
                case "ls":
                case "buffers":
                    CommandFeedback?.Invoke("current file only (multi-buffer list not yet exposed)");
                    handled = true;
                    break;

                case var m when m.StartsWith("map ") || m.StartsWith("noremap "):
                    HandleKeyMapping(m);
                    handled = true;
                    break;

                case var cmd2 when cmd2.StartsWith("%s/") || cmd2.StartsWith("s/"):
                    ExecuteSubstitute(cmd2);
                    handled = true;
                    break;
                default:
                    handled = false;
                    break;
            }
            }

            if (handled) return;

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



            // Handle :q and :quit (close)
            if (cmd == "q" || cmd == "quit")
            {
                CloseRequested?.Invoke();
                return;
            }

            // Handle :wq (save then close)
            if (cmd == "wq")
            {
                SaveRequested?.Invoke();
                CloseRequested?.Invoke();
                return;
            }

            // Handle :w (save)
            if (cmd == "w")
            {
                SaveRequested?.Invoke();
                return;
            }

            // Handle :version
            if (cmd == "version")
            {
                CommandFeedback?.Invoke("Pfpad Vim Mode 1.0");
                return;
            }

            if (handled) return;

            if (cmd == "set")
            {
                CommandFeedback?.Invoke($"nu={(ShowLineNumbers ? "on" : "off")} rnu={(RelativeNumbers ? "on" : "off")} gutter={(GutterVisible ? "on" : "off")}");
                return;
            }

            if (cmd == "version")
            {
                CommandFeedback?.Invoke("Pfpad Vim Mode 1.0");
                return;
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

        private void HandleKeyMapping(string cmd)
        {
            // :map lhs rhs   or   :noremap lhs rhs
            string[] parts = cmd.Split(' ', 3);
            if (parts.Length < 3)
            {
                CommandFeedback?.Invoke("Usage: :map lhs rhs");
                return;
            }
            string lhs = parts[1];
            string rhs = parts[2];
            KeyMappings[lhs] = rhs;
            CommandFeedback?.Invoke($"mapped {lhs} → {rhs}");
        }

        private void ExecuteSubstitute(string cmd)
        {
            // Basic :%s/old/new/g or :s/old/new/g
            // Format: %s/pat/repl/flags  or s/pat/repl/flags
            try
            {
                string pattern = cmd;
                if (pattern.StartsWith("%s/")) pattern = pattern[3..];
                else if (pattern.StartsWith("s/")) pattern = pattern[2..];

                string[] parts = pattern.Split('/');
                if (parts.Length < 2) { CommandFeedback?.Invoke("Invalid substitute syntax"); return; }

                string pat = parts[0];
                string repl = parts.Length > 1 ? parts[1] : "";
                string flags = parts.Length > 2 ? parts[2] : "";

                bool global = flags.Contains('g');
                bool confirm = flags.Contains('c');

                string text = _tb.Text;
                var regex = new Regex(pat, RegexOptions.None, TimeSpan.FromSeconds(2));

                int count = 0;
                string result = global
                    ? regex.Replace(text, m => { count++; return repl; })
                    : regex.Replace(text, m => { if (count == 0) { count++; return repl; } return m.Value; }, 1);

                if (count > 0)
                {
                    _tb.Text = result;
                    CommandFeedback?.Invoke($"Replaced {count} occurrence(s)");
                }
                else
                {
                    CommandFeedback?.Invoke("No matches found");
                }
            }
            catch (Exception ex)
            {
                CommandFeedback?.Invoke($"Substitute error: {ex.Message}");
            }
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
        private void MoveToLastLine()
        {
            int lastLine = TextBox.GetLineFromCharIndex(Math.Max(0, _tb.TextLength - 1));
            if (RepeatCount > 1)
            {
                int targetLine = Math.Min(RepeatCount - 1, lastLine);
                _tb.SelectionStart = GetLineStart(targetLine);
                MoveToFirstNonBlank();
            }
            else
            {
                _tb.SelectionStart = GetLineStart(lastLine);
            }
        }

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

        #region Text Objects
        private (int start, int end) FindInnerQuotes(char quote)
        {
            string t = _tb.Text;
            int p = SelectionStart;
            int start = -1, end = -1;
            for (int i = p - 1; i >= 0; i--) { if (t[i] == quote) { start = i + 1; break; } }
            if (start == -1) return (-1, -1);
            for (int i = start; i < t.Length; i++) { if (t[i] == quote) { end = i; break; } }
            return end == -1 ? (-1, -1) : (start, end);
        }

        private (int start, int end) FindInnerBrackets(char open, char close)
        {
            string t = _tb.Text;
            int p = SelectionStart;
            int depth = 0, start = -1;
            for (int i = p - 1; i >= 0; i--)
            {
                if (t[i] == close) depth++;
                else if (t[i] == open) { if (depth == 0) { start = i + 1; break; } depth--; }
            }
            if (start == -1) return (-1, -1);
            depth = 0;
            for (int i = start; i < t.Length; i++)
            {
                if (t[i] == open) depth++;
                else if (t[i] == close) { if (depth == 0) return (start, i); depth--; }
            }
            return (-1, -1);
        }

        public void ChangeInnerQuotes(char q) { var r = FindInnerQuotes(q); if (r.start != -1) { _tb.Select(r.start, r.end - r.start); _tb.SelectedText = ""; _lastTextObject = $"ci{q}"; EnterMode(VimMode.Insert); } }
        public void DeleteInnerQuotes(char q) { var r = FindInnerQuotes(q); if (r.start != -1) { _tb.Select(r.start, r.end - r.start); _tb.SelectedText = ""; _lastTextObject = $"di{q}"; } }
        public void ChangeInnerBrackets(char o, char c) { var r = FindInnerBrackets(o, c); if (r.start != -1) { _tb.Select(r.start, r.end - r.start); _tb.SelectedText = ""; _lastTextObject = $"ci{o}"; EnterMode(VimMode.Insert); } }
        public void DeleteInnerBrackets(char o, char c) { var r = FindInnerBrackets(o, c); if (r.start != -1) { _tb.Select(r.start, r.end - r.start); _tb.SelectedText = ""; _lastTextObject = $"di{o}"; } }
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
            string text = _tb.Text.Substring(_tb.SelectionStart, len);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }

        // Ensure all operations use the active register
        private void SetActiveRegister(string text) => SetRegister(CurrentRegister, IsAppendRegister, text);
        private void DeleteCharBefore()
        {
            int p = _tb.SelectionStart;
            if (p <= 0) return;
            int start = Math.Max(0, p - GetRepeat());
            int len = Math.Min(GetRepeat(), p);
            string text = _tb.Text.Substring(start, len);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
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
            string text = _tb.Text.Substring(start, len);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }
        private void DeleteToLineEnd()
        {
            int start = _tb.SelectionStart;
            int end = GetLineEnd(GetCurrentLine());
            if (end <= start) return;
            string text = _tb.Text.Substring(start, end - start);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionLength = end - start;
            _tb.SelectedText = "";
        }
        private void DeleteWord()
        {
            int p = _tb.SelectionStart;
            int end = FindNextWord(p);
            if (end <= p) return;
            string text = _tb.Text.Substring(p, end - p);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionLength = end - p;
            _tb.SelectedText = "";
        }
        private void DeleteInnerWord()
        {
            int p = _tb.SelectionStart;
            int s = FindPrevWord(p);
            int e = FindNextWord(p);
            if (s >= e) return;
            int start = s == p ? FindPrevWord(s) : s;
            int len = e - start;
            string text = _tb.Text.Substring(start, len);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
            _tb.SelectedText = "";
        }
        private void DeleteToLineStart()
        {
            int start = GetLineStart(GetCurrentLine());
            int end = _tb.SelectionStart;
            if (end <= start) return;
            int len = end - start;
            string text = _tb.Text.Substring(start, len);
            SetRegister(CurrentRegister, IsAppendRegister, text);
            _tb.SelectionStart = start;
            _tb.SelectionLength = len;
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
            string text = start >= 0 && end > start ? _tb.Text.Substring(start, end - start) : "";
            SetRegister(CurrentRegister, IsAppendRegister, text);
        }
        private void YankWord()
        {
            int p = _tb.SelectionStart;
            int e = FindNextWord(p);
            if (e > p) SetRegister(CurrentRegister, IsAppendRegister, _tb.Text.Substring(p, e - p));
        }
        private void PasteAfter()
        {
            string text = GetRegister(CurrentRegister);
            if (string.IsNullOrEmpty(text)) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(text.Length * count);
            for (int i = 0; i < count; i++) sb.Append(text);
            _tb.SelectionStart = p;
            _tb.SelectedText = sb.ToString();
            _tb.SelectionStart = p;
        }
        private void PasteBefore()
        {
            string text = GetRegister(CurrentRegister);
            if (string.IsNullOrEmpty(text)) return;
            int p = _tb.SelectionStart;
            int count = GetRepeat();
            var sb = new StringBuilder(text.Length * count);
            for (int i = 0; i < count; i++) sb.Append(text);
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
                SetRegister(CurrentRegister, IsAppendRegister, _tb.SelectedText);
        }
        private void RecordAction(string action, string? data = null)
        {
            LastAction = (action, data, 1);
            // Create undo point after operations that change text
            CreateUndoPoint();
        }

        private void RepeatLast()
        {
            if (LastAction == null && _lastTextObject == null) return;

            if (_lastTextObject != null)
            {
                switch (_lastTextObject)
                {
                    case "ci\"": ChangeInnerQuotes('"'); return;
                    case "ci'": ChangeInnerQuotes('\''); return;
                    case "ci`": ChangeInnerQuotes('`'); return;
                    case "ci(": ChangeInnerBrackets('(', ')'); return;
                    case "ci[": ChangeInnerBrackets('[', ']'); return;
                    case "ci{": ChangeInnerBrackets('{', '}'); return;
                    case "di\"": DeleteInnerQuotes('"'); return;
                    case "di'": DeleteInnerQuotes('\''); return;
                    case "di`": DeleteInnerQuotes('`'); return;
                    case "di(": DeleteInnerBrackets('(', ')'); return;
                    case "di[": DeleteInnerBrackets('[', ']'); return;
                    case "di{": DeleteInnerBrackets('{', '}'); return;
                }
            }

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
                case "replace-char":
                    if (data?.Length > 0) ReplaceChar(data[0], 1);
                    break;
                case "auto-indent": AutoIndentLine(); break;
            }
        }
        #endregion

        #region Scrolling
        public void PageDown()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height);
            for (int i = 0; i < lines; i++) MoveDown();
        }
        public void PageUp()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height);
            for (int i = 0; i < lines; i++) MoveUp();
        }
        public void HalfPageDown()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height / 2);
            for (int i = 0; i < lines; i++) MoveDown();
        }
        public void HalfPageUp()
        {
            int lines = Math.Max(1, _tb.ClientSize.Height / _tb.Font.Height / 2);
            for (int i = 0; i < lines; i++) MoveUp();
        }

        public void CenterCurrentLine()
        {
            int line = GetCurrentLine();
            int visible = Math.Max(1, _tb.ClientSize.Height / Math.Max(1, _tb.Font.Height));
            int target = Math.Max(0, line - visible / 2);
            _tb.SelectionStart = GetLineStart(target);
            _tb.ScrollToCaret();
        }

        public void ScrollCurrentLineToTop()
        {
            int currentLine = GetCurrentLine();
            int firstVisible = (int)SendMessage(_tb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            int delta = currentLine - firstVisible;
            if (delta != 0) SendMessage(_tb.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
        }

        public void ScrollCurrentLineToBottom()
        {
            int currentLine = GetCurrentLine();
            int visibleLines = Math.Max(1, _tb.ClientSize.Height / Math.Max(1, _tb.Font.Height));
            int firstVisible = (int)SendMessage(_tb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            int delta = currentLine - (firstVisible + visibleLines - 1);
            if (delta != 0) SendMessage(_tb.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
        }
        #endregion

        #region FindChar / Replace / Paragraph / Column Motions
        private void DoFindChar(char c, bool forward, bool till, int repeat)
        {
            string t = _tb.Text;
            int p = _tb.SelectionStart;
            int line = GetCurrentLine();
            int lineStart = GetLineStart(line);
            int lineEnd = GetLineEnd(line);

            int found = -1;
            if (forward)
            {
                int count = 0;
                for (int i = p + 1; i < lineEnd; i++)
                    if (t[i] == c && ++count == repeat) { found = i; break; }
                if (found >= 0)
                    _tb.SelectionStart = till ? Math.Max(p, found - 1) : found;
            }
            else
            {
                int count = 0;
                for (int i = p - 1; i >= lineStart; i--)
                    if (t[i] == c && ++count == repeat) { found = i; break; }
                if (found >= 0)
                    _tb.SelectionStart = till ? Math.Min(t.Length - 1, found + 1) : found;
            }
        }

        public void FindCharOnLine(char c, bool forward, bool till)
        {
            LastFindChar = c;
            LastFindForward = forward;
            LastFindTill = till;
            DoFindChar(c, forward, till, Math.Max(1, RepeatCount));
        }

        public void RepeatFindChar(bool reverse)
        {
            if (LastFindChar == '\0') return;
            bool actualForward = reverse ? !LastFindForward : LastFindForward;
            DoFindChar(LastFindChar, actualForward, LastFindTill, Math.Max(1, RepeatCount));
        }

        public void ReplaceChar(char c, int repeat)
        {
            int p = _tb.SelectionStart;
            int count = Math.Min(Math.Max(1, repeat), _tb.TextLength - p);
            if (count <= 0) return;
            _tb.SelectionStart = p;
            _tb.SelectionLength = count;
            _tb.SelectedText = new string(c, count);
            _tb.SelectionStart = p + count - 1;
            _tb.SelectionLength = 0;
            CreateUndoPoint();
        }

        public void MoveParaForward()
        {
            int repeat = Math.Max(1, RepeatCount);
            int line = GetCurrentLine();
            int totalLines = _tb.GetLineFromCharIndex(Math.Max(0, _tb.TextLength - 1));
            for (int r = 0; r < repeat; r++)
            {
                // skip current non-empty lines
                while (line < totalLines && !string.IsNullOrWhiteSpace(_tb.Lines[line])) line++;
                // skip empty lines
                while (line < totalLines && string.IsNullOrWhiteSpace(_tb.Lines[line])) line++;
            }
            _tb.SelectionStart = GetLineStart(Math.Min(line, totalLines));
        }

        public void MoveParaBackward()
        {
            int repeat = Math.Max(1, RepeatCount);
            int line = GetCurrentLine();
            for (int r = 0; r < repeat; r++)
            {
                if (line > 0) line--;
                // skip empty lines backward
                while (line > 0 && string.IsNullOrWhiteSpace(_tb.Lines[line])) line--;
                // skip non-empty lines backward
                while (line > 0 && !string.IsNullOrWhiteSpace(_tb.Lines[line - 1])) line--;
            }
            _tb.SelectionStart = GetLineStart(Math.Max(0, line));
        }

        public void GoToColumn(int col)
        {
            int line = GetCurrentLine();
            int lineStart = GetLineStart(line);
            int lineLen = Math.Max(0, GetLineEnd(line) - lineStart);
            _tb.SelectionStart = lineStart + Math.Min(Math.Max(0, col - 1), lineLen);
        }

        private void MoveToNextLineFirstNonBlank() { MoveDown(); MoveToFirstNonBlank(); }
        private void MoveToPrevLineFirstNonBlank() { MoveUp(); MoveToFirstNonBlank(); }

        public void AutoIndentLine()
        {
            int line = GetCurrentLine();
            int refLine = line - 1;
            while (refLine >= 0 && string.IsNullOrWhiteSpace(_tb.Lines[refLine])) refLine--;
            if (refLine < 0) return;

            string refText = _tb.Lines[refLine];
            int indentEnd = 0;
            while (indentEnd < refText.Length && (refText[indentEnd] == ' ' || refText[indentEnd] == '\t')) indentEnd++;
            string indent = refText[..indentEnd];

            string curText = _tb.Lines[line];
            int curIndentEnd = 0;
            while (curIndentEnd < curText.Length && (curText[curIndentEnd] == ' ' || curText[curIndentEnd] == '\t')) curIndentEnd++;

            int lineStart = GetLineStart(line);
            _tb.SelectionStart = lineStart;
            _tb.SelectionLength = curIndentEnd;
            _tb.SelectedText = indent;
            CreateUndoPoint();
        }

        private void JumpToUnmatchedBracket(char openChar, char closeChar, bool toClose)
        {
            int p = _tb.SelectionStart;
            string t = _tb.Text;
            int depth = 0;
            if (toClose)
            {
                for (int i = p; i < t.Length; i++)
                {
                    if (t[i] == openChar) depth++;
                    else if (t[i] == closeChar) { if (depth == 0) { _tb.SelectionStart = i; return; } depth--; }
                }
            }
            else
            {
                for (int i = p; i >= 0; i--)
                {
                    if (t[i] == closeChar) depth++;
                    else if (t[i] == openChar) { if (depth == 0) { _tb.SelectionStart = i; return; } depth--; }
                }
            }
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

        private void SearchWordUnderCursor(bool forward)
        {
            string word = GetWordUnderCursor();
            if (string.IsNullOrEmpty(word)) return;
            LastSearchPattern = word;
            LastSearchForward = forward;
            int start = forward ? SelectionStart + SelectionLength : SelectionStart;
            ExecuteSearch(start, forward);
        }

        private string GetWordUnderCursor()
        {
            int p = SelectionStart;
            string t = _tb.Text;
            if (p >= t.Length) return "";
            int start = p;
            while (start > 0 && IsWordChar(t[start - 1])) start--;
            int end = p;
            while (end < t.Length && IsWordChar(t[end])) end++;
            return t.Substring(start, end - start);
        }

        public void PlaybackMacro(string macro)
        {
            if (_inMacroPlayback) return;
            _inMacroPlayback = true;
            try
            {
                foreach (char c in macro)
                {
                    Keys k = CharToKey(c);
                    if (k == Keys.None) continue;
                    bool ctrl2 = (k & Keys.Control) != 0;
                    bool shift2 = (k & Keys.Shift) != 0;
                    bool alt2 = (k & Keys.Alt) != 0;
                    Keys baseKey = k & Keys.KeyCode;
                    ProcessKey(baseKey, ctrl2, shift2, alt2);
                }
            }
            finally
            {
                _inMacroPlayback = false;
            }
        }

        public void DeleteBlock()
        {
            for (int line = BlockMinLine; line <= BlockMaxLine; line++)
            {
                int lineStart = GetLineStart(line);
                int lineEnd = GetLineEnd(line);
                int delStart = lineStart + BlockMinCol;
                int delEnd = Math.Min(lineStart + BlockMaxCol + 1, lineEnd);
                if (delStart < delEnd)
                {
                    _tb.SelectionStart = delStart;
                    _tb.SelectionLength = delEnd - delStart;
                    _tb.SelectedText = "";
                }
            }
            EnterMode(VimMode.Normal);
        }

        public void YankBlock()
        {
            StringBuilder sb = new();
            for (int line = BlockMinLine; line <= BlockMaxLine; line++)
            {
                int lineStart = GetLineStart(line);
                int lineEnd = GetLineEnd(line);
                int start = lineStart + BlockMinCol;
                int end = Math.Min(lineStart + BlockMaxCol + 1, lineEnd);
                if (start < end)
                {
                    sb.Append(_tb.Text.Substring(start, end - start));
                }
                if (line < BlockMaxLine) sb.Append('\n');
            }
            SetRegister(CurrentRegister, IsAppendRegister, sb.ToString());
            EnterMode(VimMode.Normal);
        }

        private Keys CharToKey(char c)
        {
            if (char.IsLower(c))
            {
                return Keys.A + (c - 'a');
            }
            if (char.IsUpper(c))
            {
                return Keys.A + (c - 'A') | Keys.Shift;
            }
            if (char.IsDigit(c))
            {
                return Keys.D0 + (c - '0');
            }
            switch (c)
            {
                case ' ': return Keys.Space;
                case '\n': return Keys.Enter;
                case '\t': return Keys.Tab;
                case '\b': return Keys.Back;
                case '\r': return Keys.Enter;
                case '/': return Keys.OemQuestion; // / is shift of ?
                case '?': return Keys.OemQuestion;
                case ':': return Keys.OemSemicolon | Keys.Shift;
                case ';': return Keys.OemSemicolon;
                case '.': return Keys.OemPeriod;
                case ',': return Keys.Oemcomma;

                case '[': return Keys.OemOpenBrackets;
                case ']': return Keys.OemCloseBrackets;
                case '{': return Keys.OemOpenBrackets | Keys.Shift;
                case '}': return Keys.OemCloseBrackets | Keys.Shift;
                case '<': return Keys.Oemcomma | Keys.Shift;
                case '>': return Keys.OemPeriod | Keys.Shift;
                case '=': return Keys.Oemplus | Keys.Shift;
                case '+': return Keys.Oemplus;
                case '-': return Keys.OemMinus;
                case '_': return Keys.OemMinus | Keys.Shift;
                case '"': return Keys.OemQuotes | Keys.Shift;
                case '\'': return Keys.OemQuotes;
                case '\\': return Keys.OemPipe | Keys.Shift;
                case '|': return Keys.OemPipe;
                case '~': return Keys.Oemtilde;
                case '`': return Keys.Oemtilde | Keys.Shift;
                case '!': return Keys.D1 | Keys.Shift;
                case '@': return Keys.D2 | Keys.Shift;
                case '#': return Keys.D3 | Keys.Shift;
                case '$': return Keys.D4 | Keys.Shift;
                case '%': return Keys.D5 | Keys.Shift;
                case '^': return Keys.D6 | Keys.Shift;
                case '&': return Keys.D7 | Keys.Shift;
                case '*': return Keys.D8 | Keys.Shift;
                case '(': return Keys.D9 | Keys.Shift;
                case ')': return Keys.D0 | Keys.Shift;
                default: return Keys.None;
            }
        }

        #endregion

        // Overload for test compatibility and simple macro playback
        public bool ProcessKey(Keys key) => ProcessKey(key, false, false, false);
    }
}

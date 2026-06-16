using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MyCrownJewelApp.Pfpad;

internal sealed partial class TerminalPanel : UserControl, IDisposable
{
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";
    private const int EM_SETLINKCOLOR = 0x0423;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint INFINITE = 0xFFFFFFFF;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;
    private static readonly bool _conPtyAvailable = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, ref IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);

    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    private readonly RichTextBox _outputBox;
    private readonly TextBox _inputBox;
    private readonly Panel _inputContainer;
    private Process? _legacyProcess;
    private StreamWriter? _legacyStdin;
    private FileStream? _ptyIn;
    private FileStream? _ptyOut;
    private Thread? _ptyReadThread;
    private IntPtr _hPC;
    private IntPtr _hProcess;
    private IntPtr _hThread;
    private volatile bool _processExited;
    private bool _conPtyMode;
    private bool _disposed;
    private bool _shellStarted;
    private DateTime _conPtyLaunchTime;
    private static bool s_conPtyBlocked;  // once blocked by security agent, stay in legacy mode
    private bool _isDark = true;
    private Color _inputBg;
    private Color _inputBgFocused;
    private readonly string _shellPath;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private readonly ToolStrip _headerStrip;
    private readonly ToolStripLabel _shellLabel;
    private readonly ToolStripButton _closeButton;
    private readonly ToolStripButton _clearButton;
    private readonly ToolStripButton _stopButton;

    public event Action? ProcessExited;
    public event Action? HideTerminalRequested;

    public bool IsRunning => _conPtyMode ? (_hProcess != IntPtr.Zero && !_processExited) : (_legacyProcess is { HasExited: false });

    public string ShellName => Path.GetFileNameWithoutExtension(_shellPath);

    public TerminalPanel(string? shellPath = null)
    {
        _shellPath = ResolveShell(shellPath);

        Padding = new Padding(0);
        MinimumSize = new Size(200, 60);

        _outputBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = GetMonospaceFont(),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            WordWrap = true,
            TabStop = false,
            Margin = new Padding(0),
            Padding = new Padding(4, 2, 4, 2),
            DetectUrls = true
        };
        _outputBox.LinkClicked += (s, e) => OpenUrl(e.LinkText);
        _outputBox.HandleCreated += (s, e) =>
        {
            _outputBox.SetLinkColor(Color.FromArgb(80, 140, 255));
            SetWindowTheme(_outputBox.Handle, _isDark ? DARK_MODE_SCROLLBAR : "", null);
        };

        _inputBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = GetMonospaceFont(),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0),
            Padding = new Padding(4, 1, 4, 1),
            TabStop = true
        };
        _inputBox.KeyDown += InputBox_KeyDown;
        _inputBox.GotFocus += (s, e) => _inputBox.BackColor = _inputBgFocused;
        _inputBox.LostFocus += (s, e) => _inputBox.BackColor = _inputBg;

        _inputContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Padding = new Padding(2, 1, 2, 2),
            Margin = new Padding(0)
        };
        _inputContainer.Controls.Add(_inputBox);

        _closeButton = new ToolStripButton
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Alignment = ToolStripItemAlignment.Right,
            Margin = new Padding(0, 0, 2, 0),
            AutoSize = false,
            Width = 22,
            Height = 22
        };
        _closeButton.Click += (s, e) => HideTerminalRequested?.Invoke();

        _shellLabel = new ToolStripLabel
        {
            Text = $"Terminal  [{ShellName}]",
            Font = new Font("Segoe UI", 8.25f),
            Margin = new Padding(4, 0, 0, 0)
        };

        _clearButton = new ToolStripButton
        {
            Text = "\u2399",
            Font = new Font("Segoe UI", 10),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Clear output"
        };
        _clearButton.Click += (s, e) => ClearOutput();

        _stopButton = new ToolStripButton
        {
            Text = "\u25A0",
            Font = new Font("Segoe UI", 10),
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = 22,
            Height = 22,
            ToolTipText = "Send Ctrl+C to interrupt"
        };
        _stopButton.Click += (s, e) => SendCtrlC();

        _headerStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(2, 0, 0, 0),
            AutoSize = false,
            Height = 24,
            Renderer = new FlatToolStripRenderer()
        };

        _headerStrip.Items.Add(_shellLabel);
        _headerStrip.Items.Add(_stopButton);
        _headerStrip.Items.Add(new ToolStripSeparator { Alignment = ToolStripItemAlignment.Right });
        _headerStrip.Items.Add(_clearButton);
        _headerStrip.Items.Add(_closeButton);

        Controls.Add(_outputBox);
        Controls.Add(_inputContainer);
        Controls.Add(_headerStrip);

        SetTheme(Theme.Dark);
    }

    public void Start()
    {
        if (_shellStarted || _disposed) return;
        _shellStarted = true;
        StartShell();
    }

    public void SetTheme(Theme theme)
    {
        _isDark = !theme.IsLight;

        Color bg = theme.TerminalBackground;
        Color fg = theme.TerminalForeground;
        Color headerBg = theme.TerminalHeaderBackground;
        Color mutedFg = theme.Muted;
        Color border = theme.Border;
        _inputBg = theme.TerminalInputBackground;
        _inputBgFocused = theme.IsLight ? ControlPaint.Light(theme.TerminalInputBackground) : ControlPaint.LightLight(theme.TerminalInputBackground);

        BackColor = bg;

        _outputBox.BackColor = bg;
        _outputBox.ForeColor = fg;
        if (_outputBox.IsHandleCreated)
        {
            _outputBox.SetLinkColor(Color.FromArgb(80, 140, 255));
            SetWindowTheme(_outputBox.Handle, _isDark ? DARK_MODE_SCROLLBAR : "", null);
        }

        _inputBox.BackColor = _inputBox.Focused ? _inputBgFocused : _inputBg;
        _inputBox.ForeColor = fg;

        _inputContainer.BackColor = bg;

        _headerStrip.BackColor = headerBg;
        _headerStrip.ForeColor = fg;

        _shellLabel.ForeColor = mutedFg;

        _closeButton.ForeColor = fg;
        _clearButton.ForeColor = mutedFg;
        _stopButton.ForeColor = theme.IsLight ? Color.FromArgb(200, 50, 50) : Color.FromArgb(255, 100, 100);
    }

    private void StartShell()
    {
        if (_conPtyAvailable && !s_conPtyBlocked)
        {
            try
            {
                StartConPtyShell();
                return;
            }
            catch (Exception ex)
            {
                AppendAnsiText($"\x1B[93m[Terminal] ConPTY unavailable, falling back to pipes: {ex.Message}\x1B[0m\n");
                KillConPty();
            }
        }

        StartLegacyShell();
    }

    private void StartLegacyShell()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _shellPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.Environment["TERM"] = "xterm-256color";

            _legacyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _legacyProcess.OutputDataReceived += OnLegacyOutputData;
            _legacyProcess.ErrorDataReceived += OnLegacyErrorData;
            _legacyProcess.Exited += OnLegacyProcessExited;
            _legacyProcess.Start();

            _legacyStdin = _legacyProcess.StandardInput;
            _legacyProcess.BeginOutputReadLine();
            _legacyProcess.BeginErrorReadLine();
            _conPtyMode = false;
            _processExited = false;
        }
        catch (Exception ex)
        {
            AppendAnsiText($"\x1B[90m[Terminal] Failed to start {_shellPath}: {ex.Message}\x1B[0m");
        }
    }

    private void StartConPtyShell()
    {
        SafeFileHandle? inRead = null;
        SafeFileHandle? inWrite = null;
        SafeFileHandle? outRead = null;
        SafeFileHandle? outWrite = null;
        IntPtr attrList = IntPtr.Zero;

        try
        {
            if (!CreatePipe(out inRead, out inWrite, IntPtr.Zero, 0))
                ThrowLastWin32Exception("CreatePipe(stdin)");
            if (!CreatePipe(out outRead, out outWrite, IntPtr.Zero, 0))
                ThrowLastWin32Exception("CreatePipe(stdout)");

            int hr = CreatePseudoConsole(GetTerminalSize(), inRead.DangerousGetHandle(), outWrite.DangerousGetHandle(), 0, out _hPC);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            inRead.Dispose();
            inRead = null;
            outWrite.Dispose();
            outWrite = null;

            _ptyIn = new FileStream(inWrite, FileAccess.Write, 1, false);
            inWrite = null;
            _ptyOut = new FileStream(outRead, FileAccess.Read, 4096, false);
            outRead = null;

            IntPtr attrSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
            attrList = Marshal.AllocHGlobal(attrSize);
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref attrSize))
                ThrowLastWin32Exception("InitializeProcThreadAttributeList");

            IntPtr hPcValue = _hPC;
            if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, ref hPcValue, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                ThrowLastWin32Exception("UpdateProcThreadAttribute");

            STARTUPINFOEX si = default;
            si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            si.lpAttributeList = attrList;

            StringBuilder commandLine = new($"\"{_shellPath}\"");

            // Suppress WER crash dialog for the child process: if security software (e.g. SIPAgent)
            // causes the child to crash immediately, we handle it via exit-code detection below.
            SetThreadErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX, out uint prevErrorMode);
            bool procCreated = CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, Environment.CurrentDirectory, ref si, out PROCESS_INFORMATION pi);
            SetThreadErrorMode(prevErrorMode, out _);

            if (!procCreated)
                ThrowLastWin32Exception("CreateProcess");

            _hProcess = pi.hProcess;
            _hThread = pi.hThread;
            _processExited = false;
            _conPtyMode = true;
            _conPtyLaunchTime = DateTime.UtcNow;

            StartConPtyReader();
            StartConPtyExitWatcher();
        }
        catch
        {
            _processExited = true;
            throw;
        }
        finally
        {
            if (attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
            }

            inRead?.Dispose();
            inWrite?.Dispose();
            outRead?.Dispose();
            outWrite?.Dispose();
        }
    }

    private void StartConPtyReader()
    {
        var stream = _ptyOut;
        if (stream == null)
            return;

        _ptyReadThread = new Thread(() => ReadConPtyOutput(stream))
        {
            IsBackground = true,
            Name = "TerminalPanel-ConPTY-Read"
        };
        _ptyReadThread.Start();
    }

    private void ReadConPtyOutput(FileStream stream)
    {
        byte[] buffer = new byte[4096];
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        Decoder decoder = Encoding.UTF8.GetDecoder();

        try
        {
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                int charCount = decoder.GetChars(buffer, 0, read, chars, 0, flush: false);
                if (charCount <= 0)
                    continue;

                string text = new(chars, 0, charCount);
                if (IsHandleCreated && !_disposed)
                    BeginInvoke(() => AppendAnsiText(text));
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private void StartConPtyExitWatcher()
    {
        IntPtr processHandle = _hProcess;
        DateTime launchTime = _conPtyLaunchTime;
        Task.Run(() =>
        {
            if (processHandle == IntPtr.Zero)
                return;

            WaitForSingleObject(processHandle, INFINITE);
            _processExited = true;

            uint exitCode = 0;
            GetExitCodeProcess(processHandle, out exitCode);

            // Detect security-agent-caused crash: process dies in < 1s with STATUS_DLL_INIT_FAILED or similar
            bool earlyBlocked = (DateTime.UtcNow - launchTime).TotalMilliseconds < 1200
                             && exitCode is 0xC0000142 or 0xC0000005 or 0xC0000034;

            if (!IsHandleCreated || _disposed)
                return;

            BeginInvoke(() =>
            {
                if (earlyBlocked)
                {
                    s_conPtyBlocked = true;
                    AppendAnsiText("\x1B[93m[Terminal] ConPTY blocked by security software on this system.\x1B[0m\n");
                    AppendAnsiText("\x1B[93m[Terminal] Switching to compatibility mode (some interactive CLIs may open in a separate window).\x1B[0m\n");
                    KillConPty();
                    _shellStarted = false;
                    StartLegacyShell();
                    return;
                }

                AppendAnsiText($"\x1B[90m[Process exited (code: {exitCode})]\x1B[0m");
                ProcessExited?.Invoke();
            });
        });
    }

    private void OnLegacyOutputData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data != null && IsHandleCreated)
            BeginInvoke(() => AppendAnsiText(e.Data + "\n"));
    }

    private void OnLegacyErrorData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data != null && IsHandleCreated)
            BeginInvoke(() => AppendAnsiText($"\x1B[91m{e.Data}\x1B[0m" + "\n"));
    }

    private void OnLegacyProcessExited(object? sender, EventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            AppendAnsiText($"\x1B[90m[Process exited (code: {(_legacyProcess?.ExitCode.ToString() ?? "unknown")})]\x1B[0m");
            _legacyStdin = null;
            ProcessExited?.Invoke();
        });
    }

    private static readonly Color[] _ansiColors = new[]
    {
        Color.FromArgb(0, 0, 0),       // 0 Black
        Color.FromArgb(230, 60, 60),   // 1 Red
        Color.FromArgb(60, 230, 60),   // 2 Green
        Color.FromArgb(230, 200, 40),  // 3 Yellow
        Color.FromArgb(80, 140, 255),  // 4 Blue
        Color.FromArgb(230, 100, 230), // 5 Magenta
        Color.FromArgb(60, 210, 210),  // 6 Cyan
        Color.FromArgb(210, 210, 210), // 7 White
        Color.FromArgb(80, 80, 80),    // 8 Bright Black
        Color.FromArgb(255, 100, 100), // 9 Bright Red
        Color.FromArgb(100, 255, 100), // 10 Bright Green
        Color.FromArgb(255, 255, 80),  // 11 Bright Yellow
        Color.FromArgb(120, 170, 255), // 12 Bright Blue
        Color.FromArgb(255, 140, 255), // 13 Bright Magenta
        Color.FromArgb(100, 255, 255), // 14 Bright Cyan
        Color.FromArgb(255, 255, 255), // 15 Bright White
    };

    private int _ansiFg = -1;
    private int _ansiBg = -1;
    private bool _ansiBold;

    private void AppendAnsiText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        int crPos = text.IndexOf('\r');
        if (crPos >= 0)
        {
            int searchStart = 0;
            while (true)
            {
                crPos = text.IndexOf('\r', searchStart);
                if (crPos < 0)
                {
                    AppendAnsiParsed(text.Substring(searchStart));
                    break;
                }
                if (crPos > searchStart)
                    AppendAnsiParsed(text.Substring(searchStart, crPos - searchStart));
                GoToLineStart();
                searchStart = crPos + 1;
            }
            ScrollToBottom();
            return;
        }

        AppendAnsiParsed(text);
        ScrollToBottom();
    }

    private void AppendAnsiParsed(string text)
    {
        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.SelectionLength = 0;

        int pos = 0;
        while (pos < text.Length)
        {
            if (text[pos] == '\x1B')
            {
                if (TryHandleAnsiEscape(text, ref pos))
                    continue;

                pos++;
                continue;
            }

            if (text[pos] == '\b' && _outputBox.TextLength > 0)
            {
                _outputBox.SelectionStart = _outputBox.TextLength - 1;
                _outputBox.SelectionLength = 1;
                _outputBox.SelectedText = "";
                pos++;
                continue;
            }

            int nextSpecial = FindNextSpecial(text, pos);
            int len = nextSpecial - pos;
            if (len > 0)
            {
                string segment = text.Substring(pos, len);
                ApplyAnsiFormatting();
                _outputBox.AppendText(segment);
            }

            pos = nextSpecial;
        }

        _outputBox.SelectionColor = _outputBox.ForeColor;
        _outputBox.SelectionBackColor = _outputBox.BackColor;
    }

    private bool TryHandleAnsiEscape(string text, ref int pos)
    {
        if (pos + 1 >= text.Length)
            return false;

        char kind = text[pos + 1];
        switch (kind)
        {
            case '[':
                return TryHandleCsi(text, ref pos);
            case ']':
                return TrySkipOsc(text, ref pos);
            case 'O':
                pos = Math.Min(text.Length, pos + 3);
                return true;
            case '(':
            case ')':
                pos = Math.Min(text.Length, pos + 3);
                return true;
            case 'J':
                ClearOutput();
                pos += 2;
                return true;
            default:
                return false;
        }
    }

    private bool TryHandleCsi(string text, ref int pos)
    {
        int end = pos + 2;
        while (end < text.Length && !IsCsiFinalByte(text[end]))
            end++;

        if (end >= text.Length)
        {
            pos = text.Length;
            return true;
        }

        string param = text.Substring(pos + 2, end - pos - 2);
        char cmd = text[end];
        pos = end + 1;

        if (param.Length > 0 && param[0] == '?')
            return true;

        if (cmd == 'm')
        {
            ProcessAnsiSgr(param);
            return true;
        }

        if (cmd == 'K')
        {
            HandleAnsiEraseLine();
            return true;
        }

        if (cmd == 'J')
        {
            if (string.IsNullOrEmpty(param) || param == "0")
                return true;

            if (param == "2")
                ClearOutput();

            return true;
        }

        return true;
    }

    private bool TrySkipOsc(string text, ref int pos)
    {
        int i = pos + 2;
        while (i < text.Length)
        {
            if (text[i] == '\a')
            {
                pos = i + 1;
                return true;
            }

            if (text[i] == '\x1B' && i + 1 < text.Length && text[i + 1] == '\\')
            {
                pos = i + 2;
                return true;
            }

            i++;
        }

        pos = text.Length;
        return true;
    }

    private static int FindNextSpecial(string text, int start)
    {
        int nextEsc = text.IndexOf('\x1B', start);
        int nextBackspace = text.IndexOf('\b', start);

        if (nextEsc < 0) return nextBackspace < 0 ? text.Length : nextBackspace;
        if (nextBackspace < 0) return nextEsc;
        return Math.Min(nextEsc, nextBackspace);
    }

    private static bool IsCsiFinalByte(char ch) => ch >= 0x40 && ch <= 0x7E;

    private void GoToLineStart()
    {
        int pos = _outputBox.TextLength;
        if (pos == 0) return;
        int lineStart = _outputBox.Text.LastIndexOf('\n', pos - 1);
        if (lineStart < 0) lineStart = 0;
        else lineStart++;
        _outputBox.SelectionStart = lineStart;
        _outputBox.SelectionLength = pos - lineStart;
        _outputBox.SelectedText = "";
    }

    private void ProcessAnsiSgr(string param)
    {
        if (string.IsNullOrEmpty(param))
        {
            ResetAnsiState();
            return;
        }

        var codes = param.Split(';');
        for (int i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out int code))
                continue;

            if (code == 0) ResetAnsiState();
            else if (code == 1) _ansiBold = true;
            else if (code == 22) _ansiBold = false;
            else if (code >= 30 && code <= 37) _ansiFg = code - 30;
            else if (code == 38 && i + 2 < codes.Length && codes[i + 1] == "5") { _ansiFg = int.TryParse(codes[i + 2], out int c256) ? ClampAnsi(c256) : _ansiFg; i += 2; }
            else if (code == 38) { }
            else if (code == 39) _ansiFg = -1;
            else if (code >= 40 && code <= 47) _ansiBg = code - 40;
            else if (code == 48) { }
            else if (code == 49) _ansiBg = -1;
            else if (code >= 90 && code <= 97) _ansiFg = code - 90 + 8;
            else if (code >= 100 && code <= 107) _ansiBg = code - 100 + 8;
        }
    }

    private void HandleAnsiEraseLine()
    {
        int pos = _outputBox.TextLength;
        if (pos == 0) return;
        _outputBox.SelectionStart = pos;
        _outputBox.SelectionLength = 0;
    }

    private void ResetAnsiState()
    {
        _ansiFg = -1;
        _ansiBg = -1;
        _ansiBold = false;
    }

    private static int ClampAnsi(int c) => c < 0 ? 0 : c >= _ansiColors.Length ? _ansiColors.Length - 1 : c;

    private void ApplyAnsiFormatting()
    {
        if (_ansiFg >= 0)
        {
            Color c = _ansiColors[ClampAnsi(_ansiFg)];
            if (_ansiBold) c = ControlPaint.Light(c);
            _outputBox.SelectionColor = c;
        }
        else
        {
            _outputBox.SelectionColor = _outputBox.ForeColor;
        }

        if (_ansiBg >= 0)
        {
            Color raw = _ansiColors[ClampAnsi(_ansiBg)];
            _outputBox.SelectionBackColor = BlendWithBg(raw);
        }
        else
        {
            _outputBox.SelectionBackColor = _outputBox.BackColor;
        }
    }

    private Color BlendWithBg(Color fg)
    {
        Color bg = _outputBox.BackColor;
        int alpha = 70;
        int r = Math.Clamp((fg.R * alpha + bg.R * (255 - alpha)) / 255, 0, 255);
        int g = Math.Clamp((fg.G * alpha + bg.G * (255 - alpha)) / 255, 0, 255);
        int b = Math.Clamp((fg.B * alpha + bg.B * (255 - alpha)) / 255, 0, 255);
        return Color.FromArgb(r, g, b);
    }

    private void ScrollToBottom()
    {
        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.ScrollToCaret();
    }

    public void ClearOutput()
    {
        _outputBox.Clear();
        ResetAnsiState();
    }

    public void SendInput(string text)
    {
        if (!IsRunning)
        {
            AppendAnsiText("\x1B[93m[Terminal] Not running.\x1B[0m");
            return;
        }

        try
        {
            if (_conPtyMode)
            {
                if (_ptyIn == null)
                {
                    AppendAnsiText("\x1B[93m[Terminal] Not running.\x1B[0m");
                    return;
                }

                byte[] bytes = Encoding.UTF8.GetBytes(text);
                _ptyIn.Write(bytes, 0, bytes.Length);
                _ptyIn.Flush();
                return;
            }

            if (_legacyStdin == null)
            {
                AppendAnsiText("\x1B[93m[Terminal] Not running.\x1B[0m");
                return;
            }

            _legacyStdin.Write(text);
            _legacyStdin.Flush();
        }
        catch (Exception ex)
        {
            AppendAnsiText($"\x1B[91m[Terminal] Write error: {ex.Message}\x1B[0m");
        }
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x03");
            return;
        }

        if (e.Control && e.KeyCode == Keys.D)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x04");
            return;
        }

        if (e.Control && e.KeyCode == Keys.L)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x0C");
            return;
        }

        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\t");
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x1B");
            return;
        }

        if (e.KeyCode == Keys.Left && _inputBox.TextLength == 0 && IsRunning)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x1B[D");
            return;
        }

        if (e.KeyCode == Keys.Right && _inputBox.TextLength == 0 && IsRunning)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SendInput("\x1B[C");
            return;
        }

        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            string cmd = _inputBox.Text;
            _inputBox.Clear();

            if (string.IsNullOrEmpty(cmd))
            {
                SendInput("\r");
                return;
            }

            cmd = cmd.TrimEnd();

            if (cmd.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals("cls", StringComparison.OrdinalIgnoreCase))
            {
                ClearOutput();
                return;
            }

            if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                SendInput("exit\r");
                HideTerminalRequested?.Invoke();
                return;
            }

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;
            SendInput(cmd + "\r");
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (_inputBox.TextLength > 0)
            {
                NavigateHistoryUp();
                return;
            }

            if (_conPtyMode && IsRunning)
            {
                SendInput("\x1B[A");
                return;
            }

            NavigateHistoryUp();
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (_inputBox.TextLength > 0)
            {
                NavigateHistoryDown();
                return;
            }

            if (_conPtyMode && IsRunning)
            {
                SendInput("\x1B[B");
                return;
            }

            NavigateHistoryDown();
        }
    }

    private void NavigateHistoryUp()
    {
        if (_commandHistory.Count > 0 && _historyIndex > 0)
        {
            _historyIndex--;
            _inputBox.Text = _commandHistory[_historyIndex];
            _inputBox.SelectionStart = _inputBox.TextLength;
        }
    }

    private void NavigateHistoryDown()
    {
        if (_commandHistory.Count > 0 && _historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            _inputBox.Text = _commandHistory[_historyIndex];
            _inputBox.SelectionStart = _inputBox.TextLength;
        }
        else
        {
            _historyIndex = _commandHistory.Count;
            _inputBox.Clear();
        }
    }

    public void SendCtrlC()
    {
        SendInput("\x03");
    }

    public void FocusInput()
    {
        _inputBox?.Select();
        _inputBox?.Focus();
    }

    public void RestartShell()
    {
        Kill();
        ClearOutput();
        StartShell();
    }

    public void Kill()
    {
        if (_conPtyMode)
        {
            KillConPty();
            return;
        }

        KillLegacy();
    }

    private void KillLegacy()
    {
        if (_legacyProcess != null)
        {
            try
            {
                if (!_legacyProcess.HasExited)
                {
                    SendInput("exit" + Environment.NewLine);
                    if (!_legacyProcess.WaitForExit(2000))
                        _legacyProcess.Kill(true);
                }
            }
            catch { }
            finally
            {
                _legacyProcess.Dispose();
                _legacyProcess = null;
            }
        }

        _legacyStdin = null;
    }

    private void KillConPty()
    {
        _processExited = true;

        var outStream = _ptyOut;
        _ptyOut = null;
        try { outStream?.Dispose(); } catch { }

        if (_hProcess != IntPtr.Zero)
        {
            try
            {
                TerminateProcess(_hProcess, 1);
                WaitForSingleObject(_hProcess, 2000);
            }
            catch { }
        }

        if (_ptyReadThread is { IsAlive: true })
        {
            try { _ptyReadThread.Join(250); } catch { }
        }
        _ptyReadThread = null;

        if (_hThread != IntPtr.Zero)
        {
            try { CloseHandle(_hThread); } catch { }
            _hThread = IntPtr.Zero;
        }

        if (_hProcess != IntPtr.Zero)
        {
            try { CloseHandle(_hProcess); } catch { }
            _hProcess = IntPtr.Zero;
        }

        if (_hPC != IntPtr.Zero)
        {
            try { ClosePseudoConsole(_hPC); } catch { }
            _hPC = IntPtr.Zero;
        }

        var inStream = _ptyIn;
        _ptyIn = null;
        try { inStream?.Dispose(); } catch { }

        _conPtyMode = false;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (_conPtyMode && _hPC != IntPtr.Zero)
        {
            try
            {
                ResizePseudoConsole(_hPC, GetTerminalSize());
            }
            catch { }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
            {
                Kill();
                _outputBox?.Dispose();
                _inputBox?.Dispose();
                _inputContainer?.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private COORD GetTerminalSize()
    {
        Size size = _outputBox.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            return new COORD { X = 120, Y = 30 };

        Size charSize = TextRenderer.MeasureText("W", _outputBox.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        int charWidth = Math.Max(1, charSize.Width);
        int charHeight = Math.Max(1, _outputBox.Font.Height);
        int columns = Math.Max(1, size.Width / charWidth);
        int rows = Math.Max(1, size.Height / charHeight);
        return new COORD { X = (short)columns, Y = (short)rows };
    }

    private static void ThrowLastWin32Exception(string operation)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static string ResolveShell(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string pwsh = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwsh))
                return pwsh;

            string pwshX86 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwshX86))
                return pwshX86;

            string pwshCore = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(pwshCore))
                return pwshCore;

            return "cmd.exe";
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/bin/zsh" : "/bin/bash";
    }

    private static Font GetMonospaceFont()
    {
        string[] preferred = { "Cascadia Code", "Cascadia Mono", "Consolas", "Source Code Pro", "Courier New" };
        foreach (var name in preferred)
        {
            try
            {
                var f = new Font(name, 10.5f);
                if (f.Name == name) return f;
                f.Dispose();
            }
            catch { }
        }
        return new Font("Consolas", 10.5f);
    }
}

internal static partial class RichTextBoxExtensions
{
    private const int EM_SETLINKCOLOR = 0x0423;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static void SetLinkColor(this RichTextBox rtb, Color color)
    {
        if (!rtb.IsHandleCreated) return;
        int c = ColorTranslator.ToWin32(color);
        SendMessage(rtb.Handle, EM_SETLINKCOLOR, IntPtr.Zero, (IntPtr)c);
    }
}

internal sealed class FlatToolStripRenderer : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.BackColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(Color.FromArgb(60, 60, 60));
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }
    }

    protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e) { }

    protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e) { }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var r = new Rectangle(0, 2, 1, e.Item.Height - 4);
        using var brush = new SolidBrush(Color.FromArgb(80, 80, 80));
        e.Graphics.FillRectangle(brush, r);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
}

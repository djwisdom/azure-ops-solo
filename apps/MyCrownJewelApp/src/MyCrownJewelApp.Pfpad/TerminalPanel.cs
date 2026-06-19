using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MyCrownJewelApp.Pfpad;

internal sealed partial class TerminalPanel : UserControl, IDisposable
{
    public record SecuritySettings(bool ConfirmUrlOpen, bool AllowHttpUrls);

    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";
    private const int EM_SETLINKCOLOR = 0x0423;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint INFINITE = 0xFFFFFFFF;
    private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;
    private static readonly bool _conPtyAvailable = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    // Path to a flag file written when ConPTY is permanently blocked on this machine (e.g. by
    // enterprise security software that crashes DLL injection under EXTENDED_STARTUPINFO_PRESENT).
    private static readonly string _conPtyBlockedFlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyCrownJewelApp", "TextEditor", ".conpty-blocked");

    private static bool s_conPtyBlocked = File.Exists(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp", "TextEditor", ".conpty-blocked"));

    // Count of consecutive ConPTY crashes this session. Only persist the block flag after 2 failures
    // so a single transient crash on a personal machine doesn't permanently disable ConPTY.
    private static int s_conPtyFailCount;

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

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);

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
    private bool _explicitFontSet;   // true once we've applied our own font (safe to dispose old one)
    private DateTime _conPtyLaunchTime;
    private bool _isDark = true;
    private Color _inputBg;
    private Color _inputBgFocused;
    private readonly string _shellPath;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private int _maxScrollback = 5000;
    private SecuritySettings _securitySettings = new(ConfirmUrlOpen: false, AllowHttpUrls: true);
    // Output that arrives before the window handle is created (early-init terminal) is buffered
    // here and flushed once the handle becomes available.
    private readonly List<string> _preHandleOutputBuffer = new();
    private readonly object _preHandleBufferLock = new();

    public event Action? ProcessExited;
    public event Action? HideTerminalRequested;

    public bool IsRunning => _conPtyMode ? (_hProcess != IntPtr.Zero && !_processExited) : (_legacyProcess is { HasExited: false });

    public string ShellName => Path.GetFileNameWithoutExtension(_shellPath);

    /// <summary>
    /// Sends a <c>cd</c> command to the running shell to navigate to <paramref name="path"/>.
    /// Uses the appropriate syntax for cmd.exe vs PowerShell/Unix shells and the correct
    /// line-ending for ConPTY vs legacy pipe mode.
    /// </summary>
    public void ChangeDirectory(string path)
    {
        if (!IsRunning || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        string nl = _conPtyMode ? "\r" : "\n";
        // cmd.exe needs /d for cross-drive navigation; PowerShell and bash accept plain cd
        string cmd = ShellName.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            ? $"cd /d \"{path}\""
            : $"cd \"{path}\"";
        SendInput(cmd + nl);
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string StartingDirectory { get; set; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CustomTabTitle { get; set; }

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
            ScrollBars = RichTextBoxScrollBars.Vertical,
            TabStop = false,
            Margin = new Padding(0),
            Padding = new Padding(4),
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

        // Right-click context menu on the output box
        var outputMenu = new ContextMenuStrip();
        var menuCopySelection = new ToolStripMenuItem("Copy Selection\tCtrl+Shift+C");
        var menuCopyAll = new ToolStripMenuItem("Copy All");
        var menuSelectAll = new ToolStripMenuItem("Select All\tCtrl+A");
        var menuClear = new ToolStripMenuItem("Clear Terminal");
        var menuResetConPty = new ToolStripMenuItem("🔄 Reset to ConPTY Mode");
        menuCopySelection.Click += (_, _) => { if (_outputBox.SelectionLength > 0) Clipboard.SetText(_outputBox.SelectedText); };
        menuCopyAll.Click += (_, _) => { if (_outputBox.TextLength > 0) Clipboard.SetText(_outputBox.Text); };
        menuSelectAll.Click += (_, _) => _outputBox.SelectAll();
        menuClear.Click += (_, _) => ClearOutput();
        menuResetConPty.Click += (_, _) => RestartWithConPty();
        outputMenu.Items.AddRange(new ToolStripItem[] { menuCopySelection, menuCopyAll, new ToolStripSeparator(), menuSelectAll, new ToolStripSeparator(), menuClear, new ToolStripSeparator(), menuResetConPty });
        outputMenu.Opening += (_, _) =>
        {
            menuCopySelection.Enabled = _outputBox.SelectionLength > 0;
            menuResetConPty.Visible = !_conPtyMode;
        };
        _outputBox.ContextMenuStrip = outputMenu;

        // Ctrl+Shift+C when input box is focused copies selected output text
        _inputBox.KeyDown += InputBox_CopyShortcut;

        Controls.Add(_outputBox);
        Controls.Add(_inputContainer);

        // Flush any output buffered before the window handle was available.
        HandleCreated += (_, _) => FlushPreHandleOutputBuffer();

        SetTheme(Theme.Dark);
    }

    public void ApplyTerminalSettings(string fontFace, float fontSize, bool fontBold, bool wordWrap, bool scrollbarVisible, int padding)
    {
        // Track whether we previously set an explicit font so we only dispose fonts WE created.
        Font terminalFont = CreateTerminalFont(fontFace, fontSize, fontBold);
        Font? oldOutput = _explicitFontSet ? _outputBox.Font : null;
        Font? oldInput  = _explicitFontSet ? _inputBox.Font  : null;
        _explicitFontSet = true;

        _outputBox.Font = terminalFont;
        _inputBox.Font  = (Font)terminalFont.Clone();

        oldOutput?.Dispose();
        oldInput?.Dispose();

        _outputBox.WordWrap = wordWrap;
        _outputBox.ScrollBars = scrollbarVisible ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None;
        _outputBox.Padding = new Padding(Math.Clamp(padding, 0, 20));

        if (_conPtyMode && _hPC != IntPtr.Zero)
        {
            try
            {
                ResizePseudoConsole(_hPC, GetTerminalSize());
            }
            catch { }
        }
    }

    public void ApplySecuritySettings(SecuritySettings settings)
    {
        _securitySettings = settings;
    }

    public void SetMaxScrollback(int lines)
    {
        _maxScrollback = Math.Clamp(lines, 500, 50000);
        TrimScrollback();
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

        if (_outputBox.ContextMenuStrip is { } menu)
        {
            menu.BackColor = theme.MenuBackground;
            menu.ForeColor = fg;
        }
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
        else if (s_conPtyBlocked && _conPtyAvailable)
        {
            AppendAnsiText("\x1B[90m[Terminal] Running in compatibility mode (ConPTY was previously blocked). Right-click → Reset to ConPTY Mode to retry.\x1B[0m\n");
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
                WorkingDirectory = GetWorkingDirectory(),
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

            // Suppress the OS hard-error dialog in the child process. SetErrorMode is inherited
            // by child processes (unlike SetThreadErrorMode which is thread-local only).
            // This prevents the "application was unable to start (0xC0000142)" popup when
            // security software DLL injection fails under EXTENDED_STARTUPINFO_PRESENT.
            uint prevErrorMode = SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
            bool procCreated = CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, EXTENDED_STARTUPINFO_PRESENT, IntPtr.Zero, GetWorkingDirectory(), ref si, out PROCESS_INFORMATION pi);
            SetErrorMode(prevErrorMode);

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

            // Detect security-agent-caused crash: process dies in < 1.5s with STATUS_DLL_INIT_FAILED or similar.
            bool earlyBlocked = (DateTime.UtcNow - launchTime).TotalMilliseconds < 1500
                             && exitCode is 0xC0000142 or 0xC0000005 or 0xC0000034;

            if (earlyBlocked)
            {
                s_conPtyBlocked = true;
                s_conPtyFailCount++;

                // Only persist to disk after 2 consecutive failures — prevents a single transient
                // crash (e.g. on a personal machine with no security software) from permanently
                // disabling ConPTY for all future sessions.
                if (s_conPtyFailCount >= 2)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_conPtyBlockedFlagPath)!);
                        File.WriteAllText(_conPtyBlockedFlagPath, "ConPTY blocked by security agent");
                    }
                    catch { }
                }

                // UI work: show message and start legacy shell.
                // If the handle exists, do it now; otherwise defer to HandleCreated.
                if (IsHandleCreated && !_disposed)
                {
                    BeginInvoke(FallbackToLegacy);
                }
                else if (!_disposed)
                {
                    // Control hasn't been shown yet — subscribe to HandleCreated so we can
                    // start the legacy shell as soon as the control has a window handle.
                    HandleCreated += ConPtyBlockedHandleCreated;
                }
                return;
            }

            if (!IsHandleCreated || _disposed)
                return;

            BeginInvoke(() =>
            {
                AppendAnsiText($"\x1B[90m[Process exited (code: {exitCode})]\x1B[0m");
                ProcessExited?.Invoke();
            });
        });
    }

    private void ConPtyBlockedHandleCreated(object? sender, EventArgs e)
    {
        HandleCreated -= ConPtyBlockedHandleCreated;
        if (!_disposed)
            BeginInvoke(FallbackToLegacy);
    }

    private void FallbackToLegacy()
    {
        AppendAnsiText("\x1B[93m[Terminal] ConPTY blocked by security software on this system.\x1B[0m\n");
        AppendAnsiText("\x1B[93m[Terminal] Switching to compatibility mode (some interactive CLIs may open in a separate window).\x1B[0m\n");
        KillConPty();
        _shellStarted = false;
        StartLegacyShell();
    }

    private void OnLegacyOutputData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;
        BufferOrAppend(e.Data + "\n");
    }

    private void OnLegacyErrorData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;
        BufferOrAppend($"\x1B[91m{e.Data}\x1B[0m\n");
    }

    private void BufferOrAppend(string text)
    {
        if (IsHandleCreated && !_disposed)
        {
            BeginInvoke(() => AppendAnsiText(text));
            return;
        }

        lock (_preHandleBufferLock)
            _preHandleOutputBuffer.Add(text);
    }

    private void FlushPreHandleOutputBuffer()
    {
        string[] lines;
        lock (_preHandleBufferLock)
        {
            if (_preHandleOutputBuffer.Count == 0) return;
            lines = _preHandleOutputBuffer.ToArray();
            _preHandleOutputBuffer.Clear();
        }
        BeginInvoke(() => { foreach (var line in lines) AppendAnsiText(line); });
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
            TrimScrollback();
            ScrollToBottom();
            return;
        }

        AppendAnsiParsed(text);
        TrimScrollback();
        ScrollToBottom();
    }

    private void TrimScrollback()
    {
        if (_outputBox.IsDisposed)
            return;

        int lineCount = _outputBox.Lines.Length;
        if (lineCount <= _maxScrollback)
            return;

        int firstCharToKeep = _outputBox.GetFirstCharIndexFromLine(lineCount - _maxScrollback);
        if (firstCharToKeep <= 0)
            return;

        _outputBox.Select(0, firstCharToKeep);
        _outputBox.SelectedText = string.Empty;
        _outputBox.SelectionStart = _outputBox.TextLength;
        _outputBox.SelectionLength = 0;
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

    public void CopyOutputSelection()
    {
        if (_outputBox.SelectionLength > 0)
            Clipboard.SetText(_outputBox.SelectedText);
        else if (_outputBox.TextLength > 0)
            Clipboard.SetText(_outputBox.Text);
    }

    private void InputBox_CopyShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            CopyOutputSelection();
        }
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

            // PTY mode: \r is correct (line discipline translates to \r\n).
            // Pipe mode: stdin is a raw pipe; .NET ReadLine() requires \n or \r\n.
            string nl = _conPtyMode ? "\r" : "\n";

            string cmd = _inputBox.Text;
            _inputBox.Clear();

            if (string.IsNullOrEmpty(cmd))
            {
                SendInput(nl);
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
                SendInput("exit" + nl);
                HideTerminalRequested?.Invoke();
                return;
            }

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;
            SendInput(cmd + nl);
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
        _shellStarted = false;
        Start();
    }

    /// <summary>Clears the persisted ConPTY block flag so ConPTY is retried on next shell start.</summary>
    public static void ResetConPtyBlock()
    {
        s_conPtyBlocked = false;
        s_conPtyFailCount = 0;
        try { File.Delete(_conPtyBlockedFlagPath); } catch { }
    }

    /// <summary>Clears the ConPTY block flag and immediately restarts the shell using ConPTY.</summary>
    public void RestartWithConPty()
    {
        ResetConPtyBlock();
        Kill();
        ClearOutput();
        _shellStarted = false;
        Start();
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

    private void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (!_securitySettings.AllowHttpUrls &&
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            AppendAnsiText($"\x1B[93m[Security] Blocked http:// URL (allow http disabled): {url}\x1B[0m\n");
            return;
        }

        if (_securitySettings.ConfirmUrlOpen)
        {
            if (ThemedMessageBox.Show($"Open URL in browser?\n\n{url}", "Open URL",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

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

    private string GetWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(StartingDirectory) && Directory.Exists(StartingDirectory))
            return StartingDirectory;

        return Environment.CurrentDirectory;
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

    private static Font CreateTerminalFont(string? fontFace, float fontSize, bool fontBold)
    {
        float resolvedFontSize = fontSize > 0f ? fontSize : 10f;
        FontStyle style = fontBold ? FontStyle.Bold : FontStyle.Regular;

        if (!string.IsNullOrWhiteSpace(fontFace))
        {
            try
            {
                var customFont = new Font(fontFace, resolvedFontSize, style);
                if (string.Equals(customFont.Name, fontFace, StringComparison.OrdinalIgnoreCase))
                    return customFont;
                customFont.Dispose();
            }
            catch { }
        }

        return GetMonospaceFont(resolvedFontSize, fontBold);
    }

    private static Font GetMonospaceFont(float fontSize = 10f, bool fontBold = false)
    {
        string[] preferred = { "Cascadia Code", "Cascadia Mono", "Consolas", "Source Code Pro", "Courier New" };
        FontStyle style = fontBold ? FontStyle.Bold : FontStyle.Regular;
        foreach (var name in preferred)
        {
            try
            {
                var f = new Font(name, fontSize, style);
                if (f.Name == name) return f;
                f.Dispose();
            }
            catch { }
        }
        return new Font("Consolas", fontSize, style);
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

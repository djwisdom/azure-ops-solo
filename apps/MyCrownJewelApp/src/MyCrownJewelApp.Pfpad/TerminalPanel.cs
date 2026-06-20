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

    // ── New TUI renderer ─────────────────────────────────────────────────────────
    private TerminalView    _view   = null!;
    private TerminalBuffer  _buf    = null!;
    private VtParser        _parser = null!;

    // Pre-handle output buffer — chars that arrive before the window handle exists
    private readonly List<string> _preHandleOutputBuffer = new();
    private readonly object       _preHandleBufferLock   = new();

    // ── Process / pipe state ─────────────────────────────────────────────────────
    private Process?       _legacyProcess;
    private StreamWriter?  _legacyStdin;
    private LegacyReadline? _readline;      // GNU readline-style editor (legacy pipe mode only)
    private bool           _suppressingClearError; // true while swallowing a Clear-Host console-handle error block
    private FileStream?   _ptyIn;
    private FileStream?   _ptyOut;
    private Thread?       _ptyReadThread;
    private IntPtr _hPC;
    private IntPtr _hProcess;
    private IntPtr _hThread;
    private volatile bool _processExited;
    private bool _conPtyMode;
    private bool _disposed;
    private bool _shellStarted;
    private DateTime _conPtyLaunchTime;
    private bool _isDark = true;
    private readonly string _shellPath;
    private int _maxScrollback = 5000;
    private SecuritySettings _securitySettings = new(ConfirmUrlOpen: false, AllowHttpUrls: true);

    public event Action? ProcessExited;
    public event Action? HideTerminalRequested;
    /// <summary>Fires when the shell updates the terminal/tab title via OSC (or when <see cref="CustomTabTitle"/> is set).</summary>
    public event Action<string>? TabTitleChanged;
    /// <summary>Relayed from the view's Ctrl+Shift+N shortcut — request to open a new terminal tab.</summary>
    public event Action? NewTabRequested;
    /// <summary>Relayed from the view's Ctrl+F4 shortcut — request to close this terminal tab.</summary>
    public event Action? CloseTabRequested;

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
        string nl = _conPtyMode ? "\r" : "\r\n";
        string cmd = ShellName.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            ? $"cd /d \"{path}\""
            : $"cd \"{path}\"";
        // Programmatic navigation bypasses readline; update readline's WD directly.
        if (_readline != null) _readline.WorkingDirectory = path;
        SendInput(cmd + nl);
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? StartingDirectory { get; set; } = "";

    private string? _customTabTitle;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CustomTabTitle
    {
        get => _customTabTitle;
        set
        {
            if (_customTabTitle == value) return;
            _customTabTitle = value;
            TabTitleChanged?.Invoke(value ?? "");
        }
    }

    public TerminalPanel(string? shellPath = null)
    {
        _shellPath  = ResolveShell(shellPath);
        Padding     = new Padding(0);
        MinimumSize = new Size(200, 60);

        // Screen buffer + VT parser
        _buf    = new TerminalBuffer(24, 80, Color.FromArgb(204, 204, 204), Color.FromArgb(12, 12, 12));
        _parser = new VtParser(_buf);
        _parser.TitleChanged    += title  => { if (IsHandleCreated) BeginInvoke(() => CustomTabTitle = title); };

        // TerminalView — full-screen cell renderer + keyboard handler
        _view = new TerminalView { Dock = DockStyle.Fill };
        _view.Attach(_buf, _parser);
        _view.DataToSend += SendRawBytes;
        _view.PasteRequested      += SendPaste;
        _view.NewTabRequested     += () => NewTabRequested?.Invoke();
        _view.CloseTabRequested   += () => CloseTabRequested?.Invoke();
        _view.HandleCreated += (_, _) =>
        {
            SetWindowTheme(_view.Handle, _isDark ? DARK_MODE_SCROLLBAR : "", null);
        };

        // Right-click context menu
        var viewMenu          = new ContextMenuStrip();
        var menuCopySelection    = new ToolStripMenuItem("Copy Selection\tCtrl+Shift+C");
        var menuCopyAll          = new ToolStripMenuItem("Copy All");
        var menuSelectAll        = new ToolStripMenuItem("Select All");
        var menuClear            = new ToolStripMenuItem("Clear Terminal");
        var menuResetConPty      = new ToolStripMenuItem("🔄 Reset to ConPTY Mode");
        var menuOpenExternal     = new ToolStripMenuItem("🚀 Open in Windows Terminal");
        menuCopySelection.Click += (_, _) => _view.CopySelectionToClipboard();
        menuCopyAll.Click       += (_, _) => _view.CopyAllToClipboard();
        menuSelectAll.Click     += (_, _) => _view.SelectAll();
        menuClear.Click         += (_, _) => ClearOutput();
        menuResetConPty.Click   += (_, _) => RestartWithConPty();
        menuOpenExternal.Click  += (_, _) => OpenInWindowsTerminal();
        viewMenu.Items.AddRange(new ToolStripItem[]
        {
            menuCopySelection, menuCopyAll,
            new ToolStripSeparator(), menuSelectAll,
            new ToolStripSeparator(), menuClear,
            new ToolStripSeparator(), menuOpenExternal,
            new ToolStripSeparator(), menuResetConPty
        });
        viewMenu.Opening += (_, _) =>
        {
            menuCopySelection.Enabled = _view.HasSelection;
            menuResetConPty.Visible   = !_conPtyMode;
        };
        _view.ContextMenuStrip = viewMenu;

        Controls.Add(_view);

        HandleCreated += (_, _) => FlushPreHandleOutputBuffer();

        SetTheme(Theme.Dark);
    }

    public void ApplyTerminalSettings(string fontFace, float fontSize, bool fontBold,
        bool wordWrap, bool scrollbarVisible, int padding)
    {
        _view.SetFont(fontFace, fontSize, fontBold);
        _view.Padding = new Padding(Math.Clamp(padding, 0, 20));

        if (_conPtyMode && _hPC != IntPtr.Zero)
        {
            try { ResizePseudoConsole(_hPC, GetTerminalSize()); } catch { }
        }
    }

    public void ApplySecuritySettings(SecuritySettings settings)
    {
        _securitySettings = settings;
    }

    public void SetMaxScrollback(int lines)
    {
        _maxScrollback = Math.Clamp(lines, 500, 50000);
        _buf.SetMaxScrollback(_maxScrollback);
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

        BackColor = bg;
        _buf.UpdateDefaultColors(fg, bg);
        _view.SetTheme(bg, fg);

        if (_view.ContextMenuStrip is { } menu)
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
                FeedText($"\x1B[93m[Terminal] ConPTY unavailable, falling back to pipes: {ex.Message}\x1B[0m\r\n");
                KillConPty();
            }
        }
        else if (s_conPtyBlocked && _conPtyAvailable)
        {
            FeedText("\x1B[90m[Terminal] Running in compatibility mode (ConPTY was previously blocked). Right-click → Reset to ConPTY Mode to retry.\x1B[0m\r\n");
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
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow = true,
                WorkingDirectory = GetWorkingDirectory(),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8
            };

            psi.Environment["TERM"] = "xterm-256color";

            _legacyProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _legacyProcess.ErrorDataReceived += OnLegacyErrorData;
            _legacyProcess.Exited += OnLegacyProcessExited;
            _legacyProcess.Start();

            _legacyStdin = _legacyProcess.StandardInput;

            // Create readline once; preserve history across shell restarts.
            _readline ??= new LegacyReadline(
                feedVt:    vt  => { _parser.Feed(vt.AsSpan()); _view?.Refresh(false); },
                execute:   cmd =>
                {
                    try { _legacyStdin?.Write(cmd); _legacyStdin?.Flush(); }
                    catch { }
                    TrackCdCommand(cmd);
                },
                completer: GetFileSystemCompletions
            );
            _readline.Reset();
            _readline.WorkingDirectory = GetWorkingDirectory();

            // Read stdout as a raw byte stream so the prompt (no trailing \n) arrives
            // immediately and the cursor stays on the same line as the prompt.
            var stdoutStream = _legacyProcess.StandardOutput.BaseStream;
            new Thread(() => ReadLegacyStream(stdoutStream))
                { IsBackground = true, Name = "LegacyStdout" }.Start();

            _legacyProcess.BeginErrorReadLine();
            _conPtyMode = false;
            _processExited = false;
            // Enable ONLCR: legacy pipe has no PTY to translate \n → \r\n for us
            _parser.AutoLineFeedMode = true;
        }
        catch (Exception ex)
        {
            FeedText($"\x1B[90m[Terminal] Failed to start {_shellPath}: {ex.Message}\x1B[0m\r\n");
        }
    }

    private void ReadLegacyStream(Stream stream)
    {
        byte[] buf     = new byte[4096];
        char[] chars   = new char[Encoding.UTF8.GetMaxCharCount(buf.Length)];
        var    decoder = Encoding.UTF8.GetDecoder();
        try
        {
            while (true)
            {
                int read = stream.Read(buf, 0, buf.Length);
                if (read <= 0) break;
                int n = decoder.GetChars(buf, 0, read, chars, 0, flush: false);
                if (n > 0) FeedText(new string(chars, 0, n));
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
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
            _parser.AutoLineFeedMode = false;  // PTY driver performs NL→CRLF natively

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
        byte[] buffer  = new byte[4096];
        char[] chars   = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
        Decoder decoder = Encoding.UTF8.GetDecoder();

        try
        {
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;

                int charCount = decoder.GetChars(buffer, 0, read, chars, 0, flush: false);
                if (charCount <= 0) continue;

                string text = new(chars, 0, charCount);
                FeedText(text);
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
                FeedText($"\x1B[90m[Process exited (code: {exitCode})]\x1B[0m\r\n");
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
        FeedText("\x1B[93m[Terminal] ConPTY blocked by security software on this system.\x1B[0m\r\n");
        FeedText("\x1B[93m[Terminal] Switching to compatibility mode — interactive CLIs (e.g. copilot) need a real terminal.\x1B[0m\r\n");
        FeedText("\x1B[90m           Right-click → Open in Windows Terminal  to launch a full terminal.\x1B[0m\r\n");
        KillConPty();
        _shellStarted = false;
        StartLegacyShell();
    }

    private void OnLegacyErrorData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data == null) return;

        // PowerShell's Clear-Host/cls tries to set Console cursor position via Win32 API,
        // which fails without a real console handle. The error line reads:
        //   "Set-ConsoleCursorPosition : The handle is invalid."
        // Detect that specific combination, suppress the whole PS error block (terminated by
        // a blank line), and instead apply a VT clear-screen so cls works visually.
        if (!_suppressingClearError &&
            e.Data.Contains("CursorPosition", StringComparison.OrdinalIgnoreCase) &&
            e.Data.Contains("handle is invalid", StringComparison.OrdinalIgnoreCase))
        {
            _suppressingClearError = true;
            FeedText("\x1B[2J\x1B[H");  // clear screen + cursor home
        }

        if (_suppressingClearError)
        {
            if (string.IsNullOrWhiteSpace(e.Data))   // blank line = end of PS error block
                _suppressingClearError = false;
            return;
        }

        FeedText($"\x1B[91m{e.Data}\x1B[0m\r\n");
    }

    // ── Text feed pipeline ────────────────────────────────────────────────────────

    /// <summary>Routes decoded text through the VT parser and schedules a view refresh.</summary>
    private void FeedText(string text)
    {
        if (IsHandleCreated && !_disposed)
        {
            BeginInvoke(() =>
            {
                _parser.Feed(text.AsSpan());
                _view.Refresh(true);
            });
            return;
        }
        lock (_preHandleBufferLock)
            _preHandleOutputBuffer.Add(text);
    }

    private void FlushPreHandleOutputBuffer()
    {
        string[] pending;
        lock (_preHandleBufferLock)
        {
            if (_preHandleOutputBuffer.Count == 0) return;
            pending = _preHandleOutputBuffer.ToArray();
            _preHandleOutputBuffer.Clear();
        }
        BeginInvoke(() =>
        {
            foreach (string chunk in pending) _parser.Feed(chunk.AsSpan());
            _view.Refresh(true);
        });
    }

    private void OnLegacyProcessExited(object? sender, EventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            FeedText($"\x1B[90m[Process exited (code: {(_legacyProcess?.ExitCode.ToString() ?? "unknown")})]\x1B[0m\r\n");
            _legacyStdin = null;
            ProcessExited?.Invoke();
        });
    }

    // ── Output / input public API ─────────────────────────────────────────────────

    public void ClearOutput()
    {
        _buf.EraseInDisplay(2);
        _buf.SetCursor(0, 0);
        _view.Refresh(true);
    }

    public void CopyOutputSelection()
    {
        if (_view.HasSelection)
            _view.CopySelectionToClipboard();
        else
            _view.CopyAllToClipboard();
    }

    public void SendInput(string text)
    {
        if (!IsRunning)
        {
            FeedText("\x1B[93m[Terminal] Not running.\x1B[0m\r\n");
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        try
        {
            if (_conPtyMode && _ptyIn != null)
            {
                _ptyIn.Write(bytes, 0, bytes.Length);
                _ptyIn.Flush();
            }
            else if (_legacyStdin != null)
            {
                // Programmatic send (ChangeDirectory, KillLegacy exit, etc.) bypasses
                // readline so it does not pollute the line buffer or history.
                _legacyStdin.Write(text);
                _legacyStdin.Flush();
            }
        }
        catch (Exception ex)
        {
            if (IsHandleCreated)
                BeginInvoke(() => FeedText($"\x1B[91m[Terminal] Write error: {ex.Message}\x1B[0m\r\n"));
        }
    }

    private void SendRawBytes(byte[] bytes)
    {
        // Called exclusively from TerminalView keyboard events (UI thread).
        if (!IsRunning) return;
        try
        {
            if (_conPtyMode && _ptyIn != null)
            {
                _ptyIn.Write(bytes, 0, bytes.Length);
                _ptyIn.Flush();
            }
            else if (_legacyStdin != null && _readline != null)
            {
                // All keyboard input flows through the readline line-editor.
                // It echoes locally via VtParser, then sends the complete line on Enter.
                _readline.Feed(bytes);
            }
        }
        catch (Exception ex)
        {
            if (IsHandleCreated)
                BeginInvoke(() => FeedText($"\x1B[91m[Terminal] Write error: {ex.Message}\x1B[0m\r\n"));
        }
    }

    public void SendCtrlC()
    {
        // In legacy mode route through readline so it displays "^C", clears the line,
        // and then sends the interrupt byte to the shell.
        if (!_conPtyMode && _readline != null)
            _readline.Feed(new byte[] { 0x03 });
        else
            SendInput("\x03");
    }

    public void FocusInput()
    {
        _view?.Select();
        _view?.Focus();
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

    /// <summary>
    /// Opens Windows Terminal (wt.exe) at the current working directory.
    /// Falls back to a plain pwsh.exe / cmd.exe window if wt.exe is not installed.
    /// Useful when ConPTY is blocked and the user needs to run an interactive CLI (e.g. copilot).
    /// </summary>
    public void OpenInWindowsTerminal()
    {
        string dir = GetWorkingDirectory() ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        try
        {
            // Prefer Windows Terminal; fall back to pwsh then cmd
            string[] candidates = ["wt.exe", "pwsh.exe", "cmd.exe"];
            foreach (string exe in candidates)
            {
                try
                {
                    string args = exe switch
                    {
                        "wt.exe"   => $"-d \"{dir}\"",
                        "pwsh.exe" => $"-NoExit -Command \"Set-Location '{dir}'\"",
                        _          => $"/K cd /d \"{dir}\""
                    };
                    Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
                    return;
                }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        catch (Exception ex)
        {
            FeedText($"\x1B[91m[Terminal] Could not open external terminal: {ex.Message}\x1B[0m\r\n");
        }
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
        _readline?.Reset();
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

        // Resize the screen buffer to match the new view dimensions
        if (_view != null && _buf != null)
        {
            int cw = Math.Max(1, _view.CellWidth);
            int ch = Math.Max(1, _view.CellHeight);
            Size vSize = _view.ClientSize;
            if (vSize.Width > 0 && vSize.Height > 0)
            {
                int newCols = Math.Max(1, vSize.Width  / cw);
                int newRows = Math.Max(1, vSize.Height / ch);
                if (newCols != _buf.Cols || newRows != _buf.Rows)
                    _buf.Resize(newRows, newCols);
            }
        }

        if (_conPtyMode && _hPC != IntPtr.Zero)
        {
            try { ResizePseudoConsole(_hPC, GetTerminalSize()); } catch { }
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
                _view?.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private COORD GetTerminalSize()
    {
        if (_view == null) return new COORD { X = 120, Y = 30 };
        Size size = _view.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            return new COORD { X = 120, Y = 30 };

        int cw = Math.Max(1, _view.CellWidth);
        int ch = Math.Max(1, _view.CellHeight);
        return new COORD
        {
            X = (short)Math.Max(1, size.Width  / cw),
            Y = (short)Math.Max(1, size.Height / ch)
        };
    }

    private static void ThrowLastWin32Exception(string operation)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    // ── Readline helpers (legacy mode) ────────────────────────────────────────────

    /// <summary>
    /// Tab-completion provider passed to <see cref="LegacyReadline"/>.
    /// Returns file-system entries in <paramref name="workDir"/> whose names
    /// start with <paramref name="word"/> (case-insensitive on Windows).
    /// Directories are listed first and get a trailing backslash so a second
    /// Tab press descends into them naturally.
    /// </summary>
    private static string[] GetFileSystemCompletions(string word, string workDir)
    {
        if (string.IsNullOrEmpty(word)) return [];
        try
        {
            string dir  = Path.GetDirectoryName(word) ?? "";
            string file = Path.GetFileName(word);
            string root = string.IsNullOrEmpty(dir)
                ? workDir
                : Path.IsPathRooted(dir) ? dir : Path.Combine(workDir, dir);

            if (!Directory.Exists(root)) return [];

            var results = new List<string>();
            string prefix = string.IsNullOrEmpty(dir) ? "" : dir.TrimEnd('\\', '/') + "\\";

            foreach (string d in Directory.GetDirectories(root, file + "*"))
                results.Add(prefix + Path.GetFileName(d) + "\\");

            foreach (string f in Directory.GetFiles(root, file + "*"))
                results.Add(prefix + Path.GetFileName(f));

            return [.. results];
        }
        catch { return []; }
    }

    /// <summary>
    /// Parses a submitted command for <c>cd</c> / <c>Set-Location</c> so the
    /// readline's working directory stays in sync for file-completion.
    /// </summary>
    /// <summary>
    /// Sends clipboard text to the shell, wrapping in bracketed-paste sequences
    /// when the active process has enabled that mode (CSI ? 2004 h).
    /// </summary>
    private void SendPaste(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        bool bracketed = _parser?.BracketedPaste ?? false;
        if (_conPtyMode)
        {
            string payload = bracketed ? $"\x1B[200~{text}\x1B[201~" : text;
            SendInput(payload);
        }
        else
        {
            // Legacy readline mode — feed each char through readline so it echoes correctly
            // For paste bypass readline and send raw: bracketed sequences confuse legacy shells
            _readline?.PasteText(text);
        }
    }


    private void TrackCdCommand(string cmdLine)
    {
        if (_readline == null) return;
        string trimmed = cmdLine.Trim().TrimEnd('\r', '\n');
        if (!trimmed.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("Set-Location ", StringComparison.OrdinalIgnoreCase))
            return;

        string arg = trimmed[(trimmed.IndexOf(' ') + 1)..].Trim(' ', '"', '\'');
        if (string.IsNullOrEmpty(arg)) return;
        try
        {
            string newDir = Path.IsPathRooted(arg)
                ? arg
                : Path.GetFullPath(Path.Combine(_readline.WorkingDirectory, arg));
            if (Directory.Exists(newDir))
                _readline.WorkingDirectory = newDir;
        }
        catch { }
    }

    private void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (!_securitySettings.AllowHttpUrls &&
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            FeedText($"\x1B[93m[Security] Blocked http:// URL (allow http disabled): {url}\x1B[0m\r\n");
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

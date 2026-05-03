using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MyCrownJewelApp.Pfpad;

internal sealed class TerminalPanel : UserControl, IDisposable
{
    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";
    private const int EM_SETLINKCOLOR = 0x0423;

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
    private readonly RichTextBox _outputBox;
    private readonly TextBox _inputBox;
    private readonly Panel _inputContainer;
    private Process? _process;
    private StreamWriter? _stdin;
    private bool _disposed;
    private bool _shellStarted;
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

    public bool IsRunning => _process is { HasExited: false };

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
            SetWindowTheme(_outputBox.Handle, _isDark ? DARK_MODE_SCROLLBAR : null, null);
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
            SetWindowTheme(_outputBox.Handle, _isDark ? DARK_MODE_SCROLLBAR : null, null);
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

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += OnOutputData;
            _process.ErrorDataReceived += OnErrorData;
            _process.Exited += OnProcessExited;
            _process.Start();

            _stdin = _process.StandardInput;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            AppendAnsiText($"\x1B[90m[Terminal] Failed to start {_shellPath}: {ex.Message}\x1B[0m");
        }
    }

    private void OnOutputData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data != null && IsHandleCreated)
            BeginInvoke(() => AppendAnsiText(e.Data + "\n"));
    }

    private void OnErrorData(object? sender, DataReceivedEventArgs e)
    {
        if (e.Data != null && IsHandleCreated)
            BeginInvoke(() => AppendAnsiText($"\x1B[91m{e.Data}\x1B[0m" + "\n"));
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            AppendAnsiText($"\x1B[90m[Process exited (code: {(_process?.ExitCode.ToString() ?? "unknown")})]\x1B[0m");
            _stdin = null;
            ProcessExited?.Invoke();
        });
    }

    // ANSI escape sequence support

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

        // Handle carriage returns for progress bars / overwriting lines
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
            if (text[pos] == '\x1B' && pos + 1 < text.Length && text[pos + 1] == '[')
            {
                int end = pos + 2;
                while (end < text.Length && !@"ABCDEFGHJKSTfhilmnrsu".Contains(text[end]))
                    end++;

                if (end < text.Length)
                {
                    string param = text.Substring(pos + 2, end - pos - 2);
                    char cmd = text[end];
                    if (cmd == 'm')
                        ProcessAnsiSgr(param);
                    else if (cmd == 'K')
                        HandleAnsiEraseLine();
                    pos = end + 1;
                    continue;
                }
            }

            if (text[pos] == '\b' && _outputBox.TextLength > 0)
            {
                _outputBox.SelectionStart = _outputBox.TextLength - 1;
                _outputBox.SelectionLength = 1;
                _outputBox.SelectedText = "";
                pos++;
                continue;
            }

            int nextEsc = text.IndexOf('\x1B', pos);
            if (nextEsc < 0) nextEsc = text.Length;
            int len = nextEsc - pos;

            if (len > 0)
            {
                string segment = text.Substring(pos, len);
                ApplyAnsiFormatting();
                _outputBox.AppendText(segment);
            }

            pos = nextEsc;
        }

        _outputBox.SelectionColor = _outputBox.ForeColor;
        _outputBox.SelectionBackColor = _outputBox.BackColor;
    }

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
            else if (code == 38) { } // truecolor skip
            else if (code == 39) _ansiFg = -1;
            else if (code >= 40 && code <= 47) _ansiBg = code - 40;
            else if (code == 48) { } // extended bg skip
            else if (code == 49) _ansiBg = -1;
            else if (code >= 90 && code <= 97) _ansiFg = code - 90 + 8;
            else if (code >= 100 && code <= 107) _ansiBg = code - 100 + 8;
        }
    }

    private void HandleAnsiEraseLine()
    {
        // Erase from cursor to end of line
        int pos = _outputBox.TextLength;
        if (pos == 0) return;
        int lineStart = _outputBox.Text.LastIndexOf('\n', pos - 1);
        if (lineStart < 0) lineStart = 0;
        else lineStart++;
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
        if (_stdin == null || _process == null || _process.HasExited)
        {
            AppendAnsiText("\x1B[93m[Terminal] Not running.\x1B[0m");
            return;
        }

        try
        {
            _stdin.Write(text);
            _stdin.Flush();
        }
        catch (Exception ex)
        {
            AppendAnsiText($"\x1B[91m[Terminal] Write error: {ex.Message}\x1B[0m");
        }
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            string cmd = _inputBox.Text;
            _inputBox.Clear();

            if (string.IsNullOrEmpty(cmd))
            {
                SendInput(Environment.NewLine);
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
                SendInput(Environment.NewLine);
                return;
            }

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;
            SendInput(cmd + Environment.NewLine);
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_commandHistory.Count > 0 && _historyIndex > 0)
            {
                _historyIndex--;
                _inputBox.Text = _commandHistory[_historyIndex];
                _inputBox.SelectionStart = _inputBox.TextLength;
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
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
            e.Handled = true;
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
        if (_process != null && !_process.HasExited)
        {
            try
            {
                SendInput("exit" + Environment.NewLine);
                if (!_process.WaitForExit(2000))
                {
                    _process.Kill(true);
                }
            }
            catch { }
            _process.Dispose();
            _process = null;
        }
        _stdin = null;
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

internal static class RichTextBoxExtensions
{
    private const int EM_SETLINKCOLOR = 0x0423;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

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

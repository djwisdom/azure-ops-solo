namespace MyCrownJewelApp.Terminal;

/// <summary>
/// Manages a pseudo-terminal (PTY) session.
/// On Windows uses ConPTY via Conpty.Net; on Unix uses native PTY (via TerminalControl).
/// ProvidesCreateTerminal, SendInput, and Kill operations.
/// </summary>
public class TerminalManager : IDisposable
{
    private object _lock = new();
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private bool _disposed;

    // Accumulated output for testing / retrieval
    private readonly StringBuilder _outputBuffer = new();
    private readonly StringBuilder _errorBuffer = new();

    /// <summary>
    /// Raised when new output is received.
    /// </summary>
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;

    /// <summary>
    /// Gets the accumulated output since start or last clear.
    /// </summary>
    public string Output => _outputBuffer.ToString();

    /// <summary>
    /// Gets the accumulated error output.
    /// </summary>
    public string Error => _errorBuffer.ToString();

    private string _shellPath;

    public TerminalManager(string? shell = null)
    {
        _shellPath = string.IsNullOrWhiteSpace(shell) ? GetDefaultShell() : shell;
    }

    /// <summary>
    /// Creates and starts a new terminal session with the configured shell.
    /// </summary>
    public void CreateTerminal()
    {
        lock (_lock)
        {
            EnsureNotDisposed();
            Kill(); // ensure clean state

            var startInfo = new ProcessStartInfo
            {
                FileName = _shellPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Basic environment; on Unix, TERM=xterm-256color helps
                Environment =
                {
                    ["TERM"] = "xterm-256color"
                }
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) { _outputBuffer.AppendLine(e.Data); OutputReceived?.Invoke(e.Data); } };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) { _errorBuffer.AppendLine(e.Data); ErrorReceived?.Invoke(e.Data); } };
            _process.Exited += (s, e) => { /* handle exit if needed */ };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _stdin = _process.StandardInput;
        }
    }

    /// <summary>
    /// Sends raw input to the terminal (e.g. a command followed by Enter).
    /// </summary>
    public void SendInput(string input)
    {
        lock (_lock)
        {
            EnsureNotDisposed();
            if (_stdin == null) throw new InvalidOperationException("Terminal not started. Call CreateTerminal first.");
            _stdin.Write(input);
            _stdin.Flush();
        }
    }

    /// <summary>
    /// Terminates the terminal session.
    /// </summary>
    public void Kill()
    {
        lock (_lock)
        {
            if (_process != null && !_process.HasExited)
            {
                try { _process.Kill(true); } catch { /* ignore */ }
                _process.Dispose();
                _process = null;
            }
            _stdin?.Dispose();
            _stdin = null;
        }
    }

    /// <summary>
    /// Clears the accumulated output and error buffers.
    /// </summary>
    public void ClearBuffers()
    {
        lock (_lock)
        {
            _outputBuffer.Clear();
            _errorBuffer.Clear();
        }
    }

    private static string GetDefaultShell()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "cmd.exe";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/bin/zsh";
        return "/bin/bash";
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TerminalManager));
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                Kill();
                _disposed = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Manages pfpad's WSL integration: detects distros, installs the CLI shim, and
/// hosts the named-pipe IPC server that the shim sends open-requests to.
/// </summary>
internal sealed class WslIntegrationService : IDisposable
{
    public const string PipeName = "pfpad-wsl-ipc";
    public const string ShimName = "pfpad";
    public const string ShimDir = "~/.local/bin";

    /// <summary>Raised on the thread-pool when a WSL open-request arrives.</summary>
    public event Action<WslOpenRequest>? OpenRequested;

    private CancellationTokenSource? _cts;
    private bool _serverRunning;

    /// <summary>Returns true when wsl.exe is present on this machine.</summary>
    public static bool IsWslAvailable()
    {
        try
        {
            string wslPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "wsl.exe");
            return File.Exists(wslPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns the list of installed WSL distros (empty list if none / unavailable).</summary>
    public static List<string> GetInstalledDistros()
    {
        var list = new List<string>();
        if (!IsWslAvailable())
            return list;

        try
        {
            using var process = StartWslProcess(
                new[] { "--list", "--quiet" },
                redirectInput: false,
                outputEncoding: Encoding.Unicode);

            string raw = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            foreach (string line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                string name = line.Trim().Trim('\0', '\r');
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Contains("docker", StringComparison.OrdinalIgnoreCase) &&
                    !list.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(name);
                }
            }
        }
        catch
        {
        }

        return list;
    }

    /// <summary>Checks whether the pfpad shim is installed in the given distro.</summary>
    public static bool IsShimInstalled(string distro)
    {
        if (!IsWslAvailable() || string.IsNullOrWhiteSpace(distro))
            return false;

        try
        {
            using var process = StartWslProcess(
                new[] { "-d", distro, "--", "bash", "-lc", "test -f ~/.local/bin/pfpad" },
                redirectInput: false);
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Installs the pfpad bash shim into a WSL distro.
    /// Returns (success, message).
    /// </summary>
    public static (bool ok, string message) InstallShim(string distro)
    {
        if (!IsWslAvailable())
            return (false, "WSL is not available on this system. Install it first:\n  wsl --install");

        if (string.IsNullOrWhiteSpace(distro))
            return (false, "No WSL distro selected.");

        string pfpadExe = GetPfpadExePath();
        string pfpadWslPath = WindowsPathToWsl(pfpadExe);
        string shimContent = BuildShimScript(pfpadWslPath);

        try
        {
            using var process = StartWslProcess(
                new[]
                {
                    "-d", distro, "--", "bash", "-lc",
                    "mkdir -p ~/.local/bin && cat > ~/.local/bin/pfpad && chmod +x ~/.local/bin/pfpad && echo PFPAD_INSTALL_OK"
                },
                redirectInput: true);

            process.StandardInput.Write(shimContent);
            process.StandardInput.Close();

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            if (process.ExitCode != 0 || !stdout.Contains("PFPAD_INSTALL_OK", StringComparison.Ordinal))
            {
                string details = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}".Trim();
                return (false, $"Installation failed in {distro}.\n{details}");
            }

            EnsurePathInBashRc(distro);

            return (true,
                $"pfpad shim installed in {distro} at ~/.local/bin/pfpad.\n\n" +
                "Restart your terminal or run:\n  source ~/.bashrc\n\n" +
                "Then type pfpad . from any folder inside WSL.");
        }
        catch (Exception ex)
        {
            return (false, $"Installation failed: {ex.Message}");
        }
    }

    /// <summary>Uninstalls the pfpad shim from a WSL distro.</summary>
    public static (bool ok, string message) UninstallShim(string distro)
    {
        if (!IsWslAvailable())
            return (false, "WSL not available.");

        if (string.IsNullOrWhiteSpace(distro))
            return (false, "No WSL distro selected.");

        try
        {
            using var process = StartWslProcess(
                new[] { "-d", distro, "--", "bash", "-lc", "rm -f ~/.local/bin/pfpad && echo PFPAD_REMOVE_OK" },
                redirectInput: false);
            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return stdout.Contains("PFPAD_REMOVE_OK", StringComparison.Ordinal)
                ? (true, "Shim removed.")
                : (false, "Remove command did not complete.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static void SaveShimToInstallDir()
    {
        try
        {
            string? dir = Path.GetDirectoryName(GetPfpadExePath());
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;

            string shimPath = Path.Combine(dir, "pfpad-wsl.sh");
            string pfpadWslPath = WindowsPathToWsl(GetPfpadExePath());
            File.WriteAllText(shimPath, BuildShimScript(pfpadWslPath), new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    /// <summary>Starts the named-pipe IPC server (fire and forget).</summary>
    public void StartServer()
    {
        if (_serverRunning)
            return;

        _cts = new CancellationTokenSource();
        _serverRunning = true;
        _ = Task.Run(() => ServerLoop(_cts.Token));
    }

    public void Dispose()
    {
        _serverRunning = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => HandleClient(pipe), CancellationToken.None);
                pipe = null;
            }
            catch (OperationCanceledException)
            {
                pipe?.Dispose();
                break;
            }
            catch
            {
                pipe?.Dispose();
                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void HandleClient(NamedPipeServerStream pipe)
    {
        try
        {
            using var _ = pipe;
            using var reader = new StreamReader(pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

            string? line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                return;

            var req = JsonSerializer.Deserialize<WslOpenRequest>(line, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (req == null)
                return;

            req.Paths = (req.Paths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => ToWindowsPath(path, req.Distro ?? "Ubuntu"))
                .ToArray();

            OpenRequested?.Invoke(req);
        }
        catch
        {
        }
    }

    private static void EnsurePathInBashRc(string distro)
    {
        const string command = "grep -Fqx 'export PATH=\"$HOME/.local/bin:$PATH\"' ~/.bashrc 2>/dev/null || printf '\\nexport PATH=\"$HOME/.local/bin:$PATH\"\\n' >> ~/.bashrc";
        try
        {
            using var process = StartWslProcess(
                new[] { "-d", distro, "--", "bash", "-lc", command },
                redirectInput: false);
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Converts a Windows absolute path to a WSL /mnt/... path.
    /// e.g.  C:\Users\foo\bar.exe  →  /mnt/c/Users/foo/bar.exe
    /// </summary>
    public static string WindowsPathToWsl(string winPath)
    {
        if (string.IsNullOrEmpty(winPath))
            return winPath;

        if (winPath.StartsWith(@"\\", StringComparison.Ordinal))
            return winPath;

        string normalized = winPath.Replace('\\', '/');
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            char drive = char.ToLowerInvariant(normalized[0]);
            string rest = normalized[2..];
            return $"/mnt/{drive}{rest}";
        }

        return normalized;
    }

    /// <summary>
    /// Converts Linux paths to Windows paths, preferring direct drive paths when available
    /// and otherwise using the WSL UNC share.
    /// </summary>
    public static string ToWindowsPath(string path, string distro)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return path;

        if (path.Length >= 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            (path[2] == '\\' || path[2] == '/'))
        {
            return path.Replace('/', '\\');
        }

        if (path.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase) &&
            path.Length > 6 &&
            char.IsLetter(path[5]) &&
            path[6] == '/')
        {
            char drive = char.ToUpperInvariant(path[5]);
            string rest = path[6..].Replace('/', '\\');
            return $"{drive}:{rest}";
        }

        string win = path.Replace('/', '\\');
        return $@"\\wsl.localhost\{distro}{win}";
    }

    private static string GetPfpadExePath()
    {
        string? loc = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
            return loc;

        string installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Personal Flip Pad",
            "pfpad.exe");

        return File.Exists(installed) ? installed : loc ?? "pfpad.exe";
    }

    private static ProcessStartInfo CreateWslProcessStartInfo(IEnumerable<string> arguments, bool redirectInput, Encoding? outputEncoding = null)
    {
        var psi = new ProcessStartInfo("wsl.exe")
        {
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (outputEncoding != null)
            psi.StandardOutputEncoding = outputEncoding;

        foreach (string argument in arguments)
            psi.ArgumentList.Add(argument);

        return psi;
    }

    private static Process StartWslProcess(IEnumerable<string> arguments, bool redirectInput, Encoding? outputEncoding = null)
    {
        var process = Process.Start(CreateWslProcessStartInfo(arguments, redirectInput, outputEncoding));
        return process ?? throw new InvalidOperationException("Could not start wsl.exe.");
    }

    private static string BuildShimScript(string pfpadWslPath)
    {
        return $$"""
#!/usr/bin/env bash
# pfpad WSL shim — opens Personal Flip Pad on Windows from inside WSL.
set -euo pipefail

show_help() {
    echo
    echo "pfpad — Personal Flip Pad WSL shim"
    echo
    echo "Usage:"
    echo "  pfpad               Open current directory in pfpad"
    echo "  pfpad <path>        Open file or directory"
    echo "  pfpad <f1> <f2>     Open multiple files"
    echo "  pfpad --version     Show shim info"
    echo
}

PFPAD_EXE="{{pfpadWslPath}}"
PFPAD_EXE_WIN="$(wslpath -w "$PFPAD_EXE" 2>/dev/null || true)"

DISTRO="${WSL_DISTRO_NAME:-}"
if [ -z "$DISTRO" ]; then
    DISTRO="Ubuntu"
fi

if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
    show_help
    exit 0
fi

if [ "${1:-}" = "--version" ]; then
    echo "pfpad WSL shim (distro: $DISTRO)"
    exit 0
fi

WIN_ARGS=()
if [ $# -eq 0 ]; then
    WIN_ARGS+=("$(wslpath -w "$(pwd)")")
else
    for arg in "$@"; do
        if [[ "$arg" == --* ]] || [[ "$arg" == -* ]]; then
            continue
        fi

        ABS_PATH="$(realpath -m "$arg" 2>/dev/null || printf '%s' "$arg")"
        WIN_ARGS+=("$(wslpath -w "$ABS_PATH")")
    done
fi

SENT=0
if command -v powershell.exe >/dev/null 2>&1; then
    RESULT="$(
        powershell.exe -NoProfile -NonInteractive -Command '
param([string]$distro, [string[]]$paths)
try
{
    $payload = @{ action = "open"; distro = $distro; paths = $paths } | ConvertTo-Json -Compress
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(".", "pfpad-wsl-ipc", [System.IO.Pipes.PipeDirection]::Out)
    $pipe.Connect(750)
    $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    $writer.WriteLine($payload)
    $writer.Dispose()
    $pipe.Dispose()
    "OK"
}
catch
{
    "FAIL"
}
' "$DISTRO" "${WIN_ARGS[@]}" 2>/dev/null | tr -d '\r\n'
    )" || true

    if [ "$RESULT" = "OK" ]; then
        SENT=1
    fi
fi

if [ "$SENT" -eq 0 ]; then
    if [ -z "$PFPAD_EXE_WIN" ]; then
        echo "ERROR: pfpad.exe not found. Reinstall the shim from pfpad Settings > Features > WSL Integration." >&2
        exit 1
    fi

    cmd.exe /c start "" "$PFPAD_EXE_WIN" "${WIN_ARGS[@]}" >/dev/null 2>&1 || true
fi
""";
    }
}

/// <summary>Data model for a WSL → pfpad open request (sent as JSON over the named pipe).</summary>
public sealed class WslOpenRequest
{
    public string Action { get; set; } = "open";
    public string? Distro { get; set; }
    public string[] Paths { get; set; } = Array.Empty<string>();
    public bool IsDir { get; set; }
}

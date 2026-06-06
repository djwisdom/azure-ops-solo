using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.Debugger;

public sealed class DebugAdapterClient : IDisposable
{
    // System.Threading.Lock — new in .NET 9; more efficient than monitor locks.
    private readonly Lock _pendingLock = new();
    private readonly Lock _stdinLock   = new();

    private Process? _process;
    private Stream? _stdin;
    private StreamReader? _stdoutReader;
    private Thread? _readerThread;
    private int _nextSeq;
    private readonly Dictionary<int, TaskCompletionSource<string>> _pending = new();
    private volatile bool _disposed;

    /// <summary>Default timeout for each DAP request. Prevents hang if the adapter crashes.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public event Action<Dap.Event>? EventReceived;
    public event Action<string>? ErrorReceived;

    public Task StartAsync(string adapterPath, string[] args)
    {
        var psi = new ProcessStartInfo(adapterPath, string.Join(" ", args))
        {
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        _process = new Process { StartInfo = psi };
        _process.Start();

        _stdin        = _process.StandardInput.BaseStream;
        _stdoutReader = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);
        _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "DAP Reader" };
        _readerThread.Start();

        var errThread = new Thread(() =>
        {
            try
            {
                string? line;
                while ((line = _process.StandardError.ReadLine()) != null)
                    ErrorReceived?.Invoke(line);
            }
            catch { }
        }) { IsBackground = true };
        errThread.Start();

        return Task.CompletedTask;
    }

    public Task<JsonElement?> SendRequest(string command, object? args = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return Task.FromResult<JsonElement?>(null);

        int seq = Interlocked.Increment(ref _nextSeq);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pendingLock) { _pending[seq] = tcs; }

        var req = new { type = "request", seq, command, arguments = args };
        string json   = JsonSerializer.Serialize(req, Dap.JsonOpts) + "\n";
        string header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";

        lock (_stdinLock)
        {
            if (_stdin is null || _disposed) return Task.FromResult<JsonElement?>(null);
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] jsonBytes   = Encoding.UTF8.GetBytes(json);
            _stdin.Write(headerBytes, 0, headerBytes.Length);
            _stdin.Write(jsonBytes,   0, jsonBytes.Length);
            _stdin.Flush();
        }

        // Timeout: cancel the TCS if the adapter doesn't respond in time.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);
        cts.Token.Register(() =>
        {
            TaskCompletionSource<string>? entry;
            lock (_pendingLock)
            {
                _pending.TryGetValue(seq, out entry);
                _pending.Remove(seq);
            }
            entry?.TrySetCanceled(cts.Token);
        });

        return tcs.Task.ContinueWith(t =>
        {
            if (t.IsCanceled) return (JsonElement?)null;
            if (t.IsFaulted)  throw t.Exception!.InnerException!;
            string body = t.Result;
            if (string.IsNullOrEmpty(body)) return (JsonElement?)null;
            return JsonSerializer.Deserialize<JsonElement>(body, Dap.JsonOpts);
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    private void ReaderLoop()
    {
        try
        {
            while (!_disposed && _stdoutReader is not null)
            {
                string? line = _stdoutReader.ReadLine();
                if (line is null) break;

                if (!line.StartsWith("Content-Length:", StringComparison.Ordinal)) continue;

                if (!int.TryParse(line.AsSpan("Content-Length: ".Length).Trim(), out int len) || len <= 0)
                    continue;

                _stdoutReader.ReadLine(); // blank separator

                char[] buffer    = new char[len];
                int    totalRead = 0;
                while (totalRead < len)
                {
                    int read = _stdoutReader.Read(buffer, totalRead, len - totalRead);
                    if (read <= 0) break;
                    totalRead += read;
                }

                if (totalRead > 0)
                    DispatchMessage(new string(buffer, 0, totalRead));
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex) when (!_disposed)
        {
            ErrorReceived?.Invoke($"DAP reader error: {ex.Message}");
        }
        finally
        {
            // Cancel all waiting requests if the reader exits unexpectedly.
            CancelAllPending();
        }
    }

    private void DispatchMessage(string raw)
    {
        try
        {
            using var doc  = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString() ?? "";

            if (type == "response")
            {
                int  requestSeq = root.GetProperty("request_seq").GetInt32();
                bool success    = root.GetProperty("success").GetBoolean();
                string? message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                JsonElement? body = root.TryGetProperty("body", out var b) ? b : (JsonElement?)null;

                TaskCompletionSource<string>? tcs;
                lock (_pendingLock)
                {
                    _pending.TryGetValue(requestSeq, out tcs);
                    _pending.Remove(requestSeq);
                }

                if (tcs is not null)
                {
                    if (success)
                        tcs.TrySetResult(body?.GetRawText() ?? "");
                    else
                        tcs.TrySetException(new Exception(message ?? "DAP request failed"));
                }
            }
            else if (type == "event")
            {
                var evt = JsonSerializer.Deserialize<Dap.Event>(raw, Dap.JsonOpts);
                if (evt is not null) EventReceived?.Invoke(evt);
            }
        }
        catch (JsonException ex)
        {
            ErrorReceived?.Invoke($"DAP parse error: {ex.Message}");
        }
    }

    private void CancelAllPending()
    {
        Dictionary<int, TaskCompletionSource<string>> snapshot;
        lock (_pendingLock)
        {
            snapshot = new Dictionary<int, TaskCompletionSource<string>>(_pending);
            _pending.Clear();
        }
        foreach (var tcs in snapshot.Values)
            tcs.TrySetCanceled();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            int seq = Interlocked.Increment(ref _nextSeq);
            var req = new { type = "request", seq, command = "disconnect" };
            string json   = JsonSerializer.Serialize(req, Dap.JsonOpts) + "\n";
            string header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";

            lock (_stdinLock)
            {
                if (_stdin is not null)
                {
                    byte[] hb = Encoding.UTF8.GetBytes(header);
                    byte[] jb = Encoding.UTF8.GetBytes(json);
                    _stdin.Write(hb, 0, hb.Length);
                    _stdin.Write(jb, 0, jb.Length);
                    _stdin.Flush();
                }
            }
        }
        catch { }

        CancelAllPending();

        try { _process?.Kill(); }          catch { }
        try { _process?.WaitForExit(1000); } catch { }
        _process?.Dispose();
    }
}

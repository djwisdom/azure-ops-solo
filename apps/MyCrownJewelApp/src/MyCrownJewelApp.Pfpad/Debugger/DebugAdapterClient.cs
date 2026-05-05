using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.Debugger;

public sealed class DebugAdapterClient : IDisposable
{
    private Process? _process;
    private Stream? _stdin;
    private StreamReader? _stdoutReader;
    private Thread? _readerThread;
    private int _nextSeq;
    private readonly Dictionary<int, TaskCompletionSource<string>> _pending = new();
    private bool _disposed;

    public event Action<Dap.Event>? EventReceived;
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;

    public async Task StartAsync(string adapterPath, string[] args)
    {
        var psi = new ProcessStartInfo(adapterPath, string.Join(" ", args))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi };
        _process.Start();

        _stdin = _process.StandardInput.BaseStream;
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
    }

    public Task<JsonElement?> SendRequest(string command, object? args = null)
    {
        int seq = Interlocked.Increment(ref _nextSeq);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pending) { _pending[seq] = tcs; }

        var req = new
        {
            type = "request",
            seq,
            command,
            arguments = args
        };
        string json = JsonSerializer.Serialize(req, Dap.JsonOpts) + "\n";
        string header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";

        lock (_stdin!)
        {
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            _stdin.Write(headerBytes, 0, headerBytes.Length);
            _stdin.Write(jsonBytes, 0, jsonBytes.Length);
            _stdin.Flush();
        }

        return tcs.Task.ContinueWith(t =>
        {
            if (t.IsFaulted) throw t.Exception!.InnerException!;
            string body = t.Result;
            if (string.IsNullOrEmpty(body)) return (JsonElement?)null;
            return JsonSerializer.Deserialize<JsonElement>(body, Dap.JsonOpts);
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    private void ReaderLoop()
    {
        try
        {
            while (!_disposed && _stdoutReader != null)
            {
                string? line = _stdoutReader.ReadLine();
                if (line == null) break;

                if (!line.StartsWith("Content-Length:")) continue;
                int len = int.Parse(line.AsSpan("Content-Length: ".Length));
                _stdoutReader.ReadLine();

                char[] buffer = new char[len];
                int totalRead = 0;
                while (totalRead < len)
                {
                    int read = _stdoutReader.Read(buffer, totalRead, len - totalRead);
                    if (read <= 0) break;
                    totalRead += read;
                }

                string msg = new string(buffer, 0, totalRead);
                DispatchMessage(msg);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke($"Reader error: {ex.Message}");
        }
    }

    private void DispatchMessage(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        string type = root.GetProperty("type").GetString() ?? "";

        if (type == "response")
        {
            int requestSeq = root.GetProperty("request_seq").GetInt32();
            bool success = root.GetProperty("success").GetBoolean();
            string? message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            JsonElement? body = root.TryGetProperty("body", out var b) ? b : null;

            TaskCompletionSource<string>? tcs;
            lock (_pending)
            {
                if (_pending.TryGetValue(requestSeq, out tcs))
                    _pending.Remove(requestSeq);
            }

            if (tcs != null)
            {
                if (success)
                {
                    string bodyStr = body?.GetRawText() ?? "";
                    tcs.TrySetResult(bodyStr);
                }
                else
                {
                    tcs.TrySetException(new Exception(message ?? "DAP request failed"));
                }
            }
        }
        else if (type == "event")
        {
            var evt = JsonSerializer.Deserialize<Dap.Event>(raw, Dap.JsonOpts);
            if (evt != null)
                EventReceived?.Invoke(evt);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var tcs = new TaskCompletionSource<string>();
            int seq = Interlocked.Increment(ref _nextSeq);
            lock (_pending) { _pending[seq] = tcs; }

            var req = new { type = "request", seq, command = "disconnect" };
            string json = JsonSerializer.Serialize(req, Dap.JsonOpts) + "\n";
            string header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";

            lock (_stdin!)
            {
                byte[] hb = Encoding.UTF8.GetBytes(header);
                byte[] jb = Encoding.UTF8.GetBytes(json);
                _stdin.Write(hb, 0, hb.Length);
                _stdin.Write(jb, 0, jb.Length);
                _stdin.Flush();
            }
        }
        catch { }

        try { _process?.Kill(); } catch { }
        try { _process?.WaitForExit(1000); } catch { }
        _process?.Dispose();
    }
}

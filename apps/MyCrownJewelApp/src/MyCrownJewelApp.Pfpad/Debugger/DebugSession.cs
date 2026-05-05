using System.Text.Json;
using Dap = MyCrownJewelApp.Pfpad.Debugger.Dap;

namespace MyCrownJewelApp.Pfpad.Debugger;

public enum DebugState { Idle, Initializing, Running, Paused, Stepping, Terminating, Terminated }

public sealed class DebugSession : IDisposable
{
    private readonly DebugAdapterClient _client = new();
    private DebugState _state = DebugState.Idle;
    private int _activeThreadId;
    private int? _currentFrameId;
    private string? _stoppedReason;
    private string _programPath = "";
    private string? _cwd;

    public DebugState State => _state;
    public int ActiveThreadId => _activeThreadId;
    public int? CurrentFrameId => _currentFrameId;
    public string? StoppedReason => _stoppedReason;
    public DebugAdapterClient Client => _client;

    public event Action<DebugState>? StateChanged;
    public event Action<string>? DebugOutput;
    public event Action<int, string>? ThreadStopped;
    public event Action<int>? ThreadContinued;
    public event Action<int>? ProcessExited;

    public async Task<string?> StartAsync(string programPath, string? cwd = null, string[]? args = null)
    {
        _programPath = programPath;
        _cwd = cwd;
        string adapterPath = FindNetCoreDbg();
        if (adapterPath == null)
            return "netcoredbg not found. Install it and ensure it's on PATH or at %LOCALAPPDATA%\\netcoredbg\\netcoredbg.exe";

        _client.EventReceived += OnEvent;
        _client.OutputReceived += msg => DebugOutput?.Invoke(msg);
        _client.ErrorReceived += msg => DebugOutput?.Invoke($"[ERR] {msg}");

        try
        {
            await _client.StartAsync(adapterPath, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return $"Failed to start netcoredbg: {ex.Message}";
        }

        SetState(DebugState.Initializing);

        var initArgs = new Dap.InitializeRequestArguments();
        var initResp = await _client.SendRequest("initialize", initArgs);
        if (initResp == null)
            return "initialize request failed";

        var launchArgs = new Dap.LaunchRequestArguments
        {
            Program = programPath,
            Cwd = cwd ?? Path.GetDirectoryName(programPath),
            Args = args,
            Type = "coreclr"
        };
        var launchResp = await _client.SendRequest("launch", launchArgs);
        if (launchResp == null)
            return "launch request failed";

        return null;
    }

    public async Task SetBreakpointsAsync(string filePath, List<Dap.SourceBreakpoint> bps)
    {
        var args = new Dap.SetBreakpointsArguments
        {
            Source = new Dap.Source { Path = filePath },
            Breakpoints = bps.ToArray()
        };
        await _client.SendRequest("setBreakpoints", args);
    }

    public async Task ConfigurationDoneAsync()
    {
        await _client.SendRequest("configurationDone");
        SetState(DebugState.Running);
    }

    public async Task ContinueAsync()
    {
        if (_activeThreadId <= 0) return;
        SetState(DebugState.Running);
        await _client.SendRequest("continue", new Dap.ContinueArguments { ThreadId = _activeThreadId });
    }

    public async Task PauseAsync()
    {
        if (_activeThreadId <= 0) return;
        SetState(DebugState.Stepping);
        await _client.SendRequest("pause", new Dap.PauseArguments { ThreadId = _activeThreadId });
    }

    public async Task StepOverAsync()
    {
        if (_activeThreadId <= 0) return;
        SetState(DebugState.Stepping);
        await _client.SendRequest("next", new Dap.NextArguments { ThreadId = _activeThreadId });
    }

    public async Task StepInAsync()
    {
        if (_activeThreadId <= 0) return;
        SetState(DebugState.Stepping);
        await _client.SendRequest("stepIn", new Dap.StepInArguments { ThreadId = _activeThreadId });
    }

    public async Task StepOutAsync()
    {
        if (_activeThreadId <= 0) return;
        SetState(DebugState.Stepping);
        await _client.SendRequest("stepOut", new Dap.StepOutArguments { ThreadId = _activeThreadId });
    }

    public async Task<Dap.StackFrame[]?> GetStackTraceAsync(int? threadId = null, int startFrame = 0, int levels = 20)
    {
        int tid = threadId ?? _activeThreadId;
        if (tid <= 0) return null;

        var resp = await _client.SendRequest("stackTrace", new Dap.StackTraceArguments
        {
            ThreadId = tid,
            StartFrame = startFrame,
            Levels = levels
        });
        if (resp == null) return null;

        var body = JsonSerializer.Deserialize<Dap.StackTraceResponseBody>(resp.Value.GetRawText(), Dap.JsonOpts);
        return body?.StackFrames;
    }

    public async Task<Dap.Scope[]?> GetScopesAsync(int frameId)
    {
        var resp = await _client.SendRequest("scopes", new Dap.ScopesArguments { FrameId = frameId });
        if (resp == null) return null;
        var body = JsonSerializer.Deserialize<Dap.ScopesResponseBody>(resp.Value.GetRawText(), Dap.JsonOpts);
        return body?.Scopes;
    }

    public async Task<Dap.Variable[]?> GetVariablesAsync(int variablesReference)
    {
        var resp = await _client.SendRequest("variables", new Dap.VariablesArguments
        {
            VariablesReference = variablesReference
        });
        if (resp == null) return null;
        var body = JsonSerializer.Deserialize<Dap.VariablesResponseBody>(resp.Value.GetRawText(), Dap.JsonOpts);
        return body?.Variables;
    }

    public async Task<string?> EvaluateAsync(string expression, int? frameId = null, string? context = null)
    {
        var resp = await _client.SendRequest("evaluate", new Dap.EvaluateArguments
        {
            Expression = expression,
            FrameId = frameId,
            Context = context
        });
        if (resp == null) return null;
        var body = JsonSerializer.Deserialize<Dap.EvaluateResponseBody>(resp.Value.GetRawText(), Dap.JsonOpts);
        return body?.Result;
    }

    public async Task<Dap.Thread[]?> GetThreadsAsync()
    {
        var resp = await _client.SendRequest("threads");
        if (resp == null) return null;
        var body = JsonSerializer.Deserialize<Dap.ThreadsResponseBody>(resp.Value.GetRawText(), Dap.JsonOpts);
        return body?.Threads;
    }

    private void OnEvent(Dap.Event evt)
    {
        switch (evt.EventType)
        {
            case "initialized":
                break;

            case "stopped":
                var stopBody = DeserializeBody<Dap.StoppedEventBody>(evt.Body);
                if (stopBody != null)
                {
                    _activeThreadId = stopBody.ThreadId ?? 0;
                    _stoppedReason = stopBody.Reason;
                    SetState(DebugState.Paused);
                    ThreadStopped?.Invoke(_activeThreadId, stopBody.Reason);
                }
                break;

            case "continued":
                var contBody = DeserializeBody<Dap.ContinuedEventBody>(evt.Body);
                if (contBody != null)
                {
                    _activeThreadId = contBody.ThreadId;
                    SetState(DebugState.Running);
                    ThreadContinued?.Invoke(contBody.ThreadId);
                }
                break;

            case "output":
                var outBody = DeserializeBody<Dap.OutputEventBody>(evt.Body);
                if (outBody != null)
                    DebugOutput?.Invoke($"[{outBody.Category}] {outBody.Output}");
                break;

            case "exited":
                var exitBody = DeserializeBody<Dap.ExitedEventBody>(evt.Body);
                SetState(DebugState.Terminated);
                ProcessExited?.Invoke(exitBody?.ExitCode ?? -1);
                break;

            case "terminated":
                SetState(DebugState.Terminated);
                break;

            case "process":
                break;
        }
    }

    private void SetState(DebugState newState)
    {
        _state = newState;
        StateChanged?.Invoke(newState);
    }

    private static T? DeserializeBody<T>(JsonElement? element) where T : class
    {
        if (element == null) return null;
        return JsonSerializer.Deserialize<T>(element.Value.GetRawText(), Dap.JsonOpts);
    }

    private static string? FindNetCoreDbg()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates =
        {
            "netcoredbg.exe",
            Path.Combine(appDir, "netcoredbg", "netcoredbg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "netcoredbg", "netcoredbg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "netcoredbg", "netcoredbg.exe"),
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                string full = Path.Combine(dir.Trim(), "netcoredbg.exe");
                if (File.Exists(full)) return full;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _client.EventReceived -= OnEvent;
        _client.Dispose();
    }
}

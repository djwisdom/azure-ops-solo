using System.Text.Json;

namespace MyCrownJewelApp.Pfpad.Debugger;

public sealed record BreakpointData
{
    public string File { get; init; } = "";
    public int Line { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Condition { get; init; }
    public string? HitCondition { get; init; }
    public string? LogMessage { get; init; }
}

public sealed class BreakpointManager
{
    // Static options — allocated once; avoids per-call overhead.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<string, List<BreakpointData>> _breakpoints
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _configPath;

    public BreakpointManager()
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCrownJewelApp", "Pfpad");
        Directory.CreateDirectory(appData);
        _configPath = Path.Combine(appData, "breakpoints.json");
        Load();
    }

    public IReadOnlyDictionary<string, List<BreakpointData>> All => _breakpoints;
    public event Action? BreakpointsChanged;

    public bool HasBreakpoint(string filePath, int line)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return false;
        return bps.Any(bp => bp.Line == line);
    }

    public BreakpointData? GetBreakpoint(string filePath, int line)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return null;
        return bps.FirstOrDefault(bp => bp.Line == line);
    }

    public void ToggleBreakpoint(string filePath, int line)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps))
        {
            _breakpoints[filePath] = [new() { File = filePath, Line = line }];
        }
        else
        {
            var existing = bps.FirstOrDefault(bp => bp.Line == line);
            if (existing != null)
                bps.Remove(existing);
            else
                bps.Add(new BreakpointData { File = filePath, Line = line });

            if (bps.Count == 0) _breakpoints.Remove(filePath);
        }

        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public void RemoveBreakpoint(string filePath, int line)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return;
        bps.RemoveAll(bp => bp.Line == line);
        if (bps.Count == 0) _breakpoints.Remove(filePath);
        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public void ClearAll()
    {
        _breakpoints.Clear();
        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public void EnableAll()
    {
        foreach (var (file, list) in _breakpoints)
            for (int i = 0; i < list.Count; i++)
                list[i] = list[i] with { Enabled = true };
        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public void DisableAll()
    {
        foreach (var (file, list) in _breakpoints)
            for (int i = 0; i < list.Count; i++)
                list[i] = list[i] with { Enabled = false };
        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public void UpdateBreakpoint(string filePath, int line, bool? enabled = null,
        string? condition = null, string? hitCondition = null, string? logMessage = null)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return;
        var bp = bps.FirstOrDefault(b => b.Line == line);
        if (bp == null) return;

        int idx = bps.IndexOf(bp);
        bps[idx] = bp with
        {
            Enabled      = enabled      ?? bp.Enabled,
            Condition    = condition    ?? bp.Condition,
            HitCondition = hitCondition ?? bp.HitCondition,
            LogMessage   = logMessage   ?? bp.LogMessage
        };

        SaveAsync();
        BreakpointsChanged?.Invoke();
    }

    public List<Dap.SourceBreakpoint> GetDapBreakpoints(string filePath)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return [];
        return bps
            .Where(b => b.Enabled)
            .Select(b => new Dap.SourceBreakpoint
            {
                Line         = b.Line,
                Condition    = b.Condition,
                HitCondition = b.HitCondition,
                LogMessage   = b.LogMessage
            })
            .ToList();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            string json = File.ReadAllText(_configPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<BreakpointData>>>(json, JsonOpts);
            if (data is null) return;
            _breakpoints.Clear();
            foreach (var (k, v) in data) _breakpoints[k] = v;
        }
        catch { }
    }

    /// <summary>Persists breakpoints asynchronously so callers never block on disk I/O.</summary>
    private void SaveAsync()
    {
        // Take a snapshot so the async work doesn't race against future mutations.
        var snapshot = _breakpoints.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);

        _ = Task.Run(async () =>
        {
            try
            {
                string json = JsonSerializer.Serialize(snapshot, JsonOpts);
                await File.WriteAllTextAsync(_configPath, json).ConfigureAwait(false);
            }
            catch { }
        });
    }
}

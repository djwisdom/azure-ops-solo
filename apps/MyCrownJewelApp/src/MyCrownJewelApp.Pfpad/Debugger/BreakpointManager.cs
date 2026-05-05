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
    private readonly Dictionary<string, List<BreakpointData>> _breakpoints = new(StringComparer.OrdinalIgnoreCase);
    private string _configPath;

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
            _breakpoints[filePath] = new List<BreakpointData>
            {
                new() { File = filePath, Line = line }
            };
        }
        else
        {
            var existing = bps.FirstOrDefault(bp => bp.Line == line);
            if (existing != null)
                bps.Remove(existing);
            else
                bps.Add(new BreakpointData { File = filePath, Line = line });
        }

        Save();
        BreakpointsChanged?.Invoke();
    }

    public void RemoveBreakpoint(string filePath, int line)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return;
        bps.RemoveAll(bp => bp.Line == line);
        if (bps.Count == 0) _breakpoints.Remove(filePath);
        Save();
        BreakpointsChanged?.Invoke();
    }

    public void ClearAll()
    {
        _breakpoints.Clear();
        Save();
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
            Enabled = enabled ?? bp.Enabled,
            Condition = condition ?? bp.Condition,
            HitCondition = hitCondition ?? bp.HitCondition,
            LogMessage = logMessage ?? bp.LogMessage
        };

        Save();
        BreakpointsChanged?.Invoke();
    }

    public List<Dap.SourceBreakpoint> GetDapBreakpoints(string filePath)
    {
        if (!_breakpoints.TryGetValue(filePath, out var bps)) return new();
        return bps.Where(b => b.Enabled).Select(b => new Dap.SourceBreakpoint
        {
            Line = b.Line,
            Condition = b.Condition,
            HitCondition = b.HitCondition,
            LogMessage = b.LogMessage
        }).ToList();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            string json = File.ReadAllText(_configPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<BreakpointData>>>(json);
            if (data != null)
            {
                _breakpoints.Clear();
                foreach (var kvp in data)
                    _breakpoints[kvp.Key] = kvp.Value;
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_breakpoints, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyCrownJewelApp.Pfpad;

public sealed record DocumentSnapshot
{
    public string Path { get; init; } = "";
    public int CursorLine { get; init; }
    public int CursorColumn { get; init; }
    public int FirstVisibleLine { get; init; }
    public int SelectionStart { get; init; }
    public int SelectionLength { get; init; }
}

public sealed record SessionData
{
    public List<DocumentSnapshot> Documents { get; init; } = new();
    public int ActiveTabIndex { get; init; }
    public string? WorkspacePath { get; init; }
    public int NextUntitledNumber { get; init; } = 1;
    public DateTime LastSession { get; init; }
}

public sealed class SessionManager
{
    public const int MaxRecentWorkspaces = 10;

    private readonly string _sessionPath;
    private readonly string _recentWorkspacesPath;
    private readonly List<string> _recentWorkspaces = new();

    public IReadOnlyList<string> RecentWorkspaces => _recentWorkspaces.AsReadOnly();

    public SessionManager()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp",
            "TextEditor");
        Directory.CreateDirectory(dir);
        _sessionPath = Path.Combine(dir, "session.json");
        _recentWorkspacesPath = Path.Combine(dir, "recentWorkspaces.json");
    }

    public void LoadRecent()
    {
        try
        {
            if (!File.Exists(_recentWorkspacesPath)) return;
            string json = File.ReadAllText(_recentWorkspacesPath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null)
            {
                _recentWorkspaces.Clear();
                _recentWorkspaces.AddRange(list);
            }
        }
        catch { }
    }

    public void SaveRecent()
    {
        try
        {
            string json = JsonSerializer.Serialize(_recentWorkspaces.Take(MaxRecentWorkspaces).ToList());
            string tmp = _recentWorkspacesPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _recentWorkspacesPath, overwrite: true);
        }
        catch { }
    }

    public void AddRecent(string path)
    {
        _recentWorkspaces.RemoveAll(w => string.Equals(w, path, StringComparison.OrdinalIgnoreCase));
        _recentWorkspaces.Insert(0, path);
        if (_recentWorkspaces.Count > MaxRecentWorkspaces)
            _recentWorkspaces.RemoveRange(MaxRecentWorkspaces, _recentWorkspaces.Count - MaxRecentWorkspaces);
    }

    public void ClearRecent()
    {
        _recentWorkspaces.Clear();
    }

    public void SaveSession(List<EditorDocument> documents, int activeIndex, string? workspaceRoot, int nextUntitledNumber)
    {
        try
        {
            var snapshots = new List<DocumentSnapshot>();
            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.FilePath) && File.Exists(doc.FilePath))
                {
                    snapshots.Add(new DocumentSnapshot
                    {
                        Path = doc.FilePath,
                        CursorLine = doc.SelectionStart >= 0
                            ? doc.SelectionStart
                            : 0,
                        CursorColumn = 0,
                        FirstVisibleLine = doc.FirstVisibleLine,
                        SelectionStart = doc.SelectionStart,
                        SelectionLength = doc.SelectionLength
                    });
                }
            }

            var session = new SessionData
            {
                Documents = snapshots,
                ActiveTabIndex = activeIndex >= 0 && activeIndex < documents.Count ? activeIndex : 0,
                WorkspacePath = workspaceRoot,
                NextUntitledNumber = nextUntitledNumber,
                LastSession = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            string tmpPath = _sessionPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(_sessionPath))
                File.Delete(_sessionPath);
            File.Move(tmpPath, _sessionPath);
        }
        catch
        {
        }
    }

    public SessionData? RestoreSession()
    {
        try
        {
            if (!File.Exists(_sessionPath))
                return null;

            string json = File.ReadAllText(_sessionPath);
            var session = JsonSerializer.Deserialize<SessionData>(json);
            if (session?.Documents == null)
                return null;

            session.Documents.RemoveAll(d => string.IsNullOrEmpty(d.Path) || !File.Exists(d.Path));

            if (session.Documents.Count == 0 && string.IsNullOrEmpty(session.WorkspacePath))
                return null;

            return session;
        }
        catch
        {
            try
            {
                string corruptPath = _sessionPath + ".corrupt";
                if (File.Exists(_sessionPath))
                {
                    if (File.Exists(corruptPath))
                        File.Delete(corruptPath);
                    File.Move(_sessionPath, corruptPath);
                }
            }
            catch { }
            return null;
        }
    }
}

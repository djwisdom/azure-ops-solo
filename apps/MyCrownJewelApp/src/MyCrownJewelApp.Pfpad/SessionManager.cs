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
    private readonly string _sessionPath;

    public SessionManager()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyCrownJewelApp",
            "TextEditor");
        Directory.CreateDirectory(dir);
        _sessionPath = Path.Combine(dir, "session.json");
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

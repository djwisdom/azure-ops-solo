using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace MyCrownJewelApp.Pfpad;

public sealed class GitService : IDisposable
{
    private Repository? _repo;
    private string? _repoPath;
    private bool _disposed;

    public event Action? OnRepoChanged;
    public event Action<string>? OnError;

    public bool IsActive => _repo is not null;
    public string? RepoPath => _repoPath;
    public string? CurrentBranch => _repo?.Head?.FriendlyName;
    public bool IsDetached => _repo?.Head?.IsCurrentRepositoryHead == false;

    public bool TryOpenRepo(string? filePath)
    {
        if (_repo is not null && _repoPath is not null && filePath is not null &&
            filePath.StartsWith(_repoPath, StringComparison.OrdinalIgnoreCase))
            return true;

        CloseRepo();
        var dir = FindRepoRoot(filePath);
        if (dir is null) return false;

        try
        {
            _repo = new Repository(dir);
            _repoPath = dir;
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Could not open repository: {ex.Message}");
            return false;
        }
    }

    public bool InitRepo(string path)
    {
        CloseRepo();
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var initPath = Repository.Init(path);
            _repo = new Repository(initPath);
            _repoPath = initPath;
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Could not initialize repository: {ex.Message}");
            return false;
        }
    }

    private static string? FindRepoRoot(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        var dir = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
        if (dir is null) return null;

        var d = new DirectoryInfo(dir);
        while (d is not null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, ".git")))
                return d.FullName;
            d = d.Parent;
        }
        return null;
    }

    public void Refresh()
    {
        if (_repo is null) return;
        try { OnRepoChanged?.Invoke(); }
        catch { }
    }

    public (List<StatusEntry> Staged, List<StatusEntry> Unstaged, List<StatusEntry> Untracked) GetStatus()
    {
        var staged = new List<StatusEntry>();
        var unstaged = new List<StatusEntry>();
        var untracked = new List<StatusEntry>();

        if (_repo is null) return (staged, unstaged, untracked);

        try
        {
            var status = _repo.RetrieveStatus(new StatusOptions
            {
                IncludeUnaltered = false,
                RecurseUntrackedDirs = true,
                DetectRenamesInIndex = true,
                DetectRenamesInWorkDir = true
            });

            foreach (var entry in status)
            {
                var se = new StatusEntry(entry.FilePath, entry.State);
                switch (entry.State)
                {
                    case FileStatus.NewInIndex:
                    case FileStatus.ModifiedInIndex:
                    case FileStatus.DeletedFromIndex:
                    case FileStatus.RenamedInIndex:
                    case FileStatus.TypeChangeInIndex:
                        if (entry.State.HasFlag(FileStatus.NewInWorkdir))
                            unstaged.Add(se); // staged AND dirty — show in both
                        else
                            staged.Add(se);
                        break;
                }

                bool isModified = entry.State.HasFlag(FileStatus.ModifiedInWorkdir);
                bool isDeleted = entry.State.HasFlag(FileStatus.DeletedFromWorkdir);
                bool isNew = entry.State.HasFlag(FileStatus.NewInWorkdir);
                bool isRenamed = entry.State.HasFlag(FileStatus.RenamedInWorkdir);
                bool isTypeChanged = entry.State.HasFlag(FileStatus.TypeChangeInWorkdir);

                if (isModified || isDeleted || isNew || isRenamed || isTypeChanged)
                    unstaged.Add(se);

                if (entry.State.HasFlag(FileStatus.NewInWorkdir) && !entry.State.HasFlag(FileStatus.NewInIndex))
                    untracked.Add(se);
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Could not read status: {ex.Message}");
        }

        return (staged, unstaged, untracked);
    }

    public bool Stage(string path)
    {
        if (_repo is null) return false;
        try { _repo.Index.Add(path); _repo.Index.Write(); return true; }
        catch (Exception ex) { OnError?.Invoke($"Could not stage '{path}': {ex.Message}"); return false; }
    }

    public bool Unstage(string path)
    {
        if (_repo is null) return false;
        try { _repo.Index.Remove(path); _repo.Index.Write(); return true; }
        catch (Exception ex) { OnError?.Invoke($"Could not unstage '{path}': {ex.Message}"); return false; }
    }

    public bool StageAll()
    {
        if (_repo is null) return false;
        try
        {
            var status = _repo.RetrieveStatus(new StatusOptions { IncludeUnaltered = false });
            foreach (var entry in status)
            {
                if (entry.State.HasFlag(FileStatus.NewInWorkdir) ||
                    entry.State.HasFlag(FileStatus.ModifiedInWorkdir) ||
                    entry.State.HasFlag(FileStatus.DeletedFromWorkdir))
                    _repo.Index.Add(entry.FilePath);
            }
            _repo.Index.Write();
            return true;
        }
        catch (Exception ex) { OnError?.Invoke($"Could not stage all: {ex.Message}"); return false; }
    }

    public bool Commit(string message, string? authorName = null, string? authorEmail = null)
    {
        if (_repo is null) return false;
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            { OnError?.Invoke("Commit message cannot be empty."); return false; }

            var author = authorName is not null
                ? new Signature(authorName, authorEmail ?? "user@local", DateTimeOffset.Now)
                : new Signature("Personal Flip Pad", "git@pfpad.local", DateTimeOffset.Now);

            _repo.Commit(message, author, author);
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Commit failed: {ex.Message}");
            return false;
        }
    }

    public List<CommitEntry> GetLog(int count = 50)
    {
        var result = new List<CommitEntry>();
        if (_repo is null) return result;

        try
        {
            var filter = new CommitFilter
            {
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                IncludeReachableFrom = _repo.Head
            };

            foreach (var c in _repo.Commits.QueryBy(filter).Take(count))
            {
                result.Add(new CommitEntry(
                    c.Sha[..7],
                    c.Author.Name,
                    c.Author.When.ToString("yyyy-MM-dd HH:mm"),
                    c.MessageShort));
            }
        }
        catch (Exception ex) { OnError?.Invoke($"Could not read log: {ex.Message}"); }

        return result;
    }

    public List<BranchEntry> GetBranches()
    {
        var result = new List<BranchEntry>();
        if (_repo is null) return result;

        try
        {
            foreach (var b in _repo.Branches.Where(b => !b.IsRemote))
                result.Add(new BranchEntry(b.FriendlyName, b.IsCurrentRepositoryHead, b.Tip?.Sha[..7] ?? ""));
        }
        catch (Exception ex) { OnError?.Invoke($"Could not list branches: {ex.Message}"); }

        return result;
    }

    public bool HasUncommittedChanges()
    {
        if (_repo is null) return false;
        try
        {
            var status = _repo.RetrieveStatus(new StatusOptions());
            return status.IsDirty;
        }
        catch { return false; }
    }

    private const int MaxDiffSizeBytes = 512 * 1024;

    public string GetDiffContent(string path, bool staged)
    {
        if (_repo is null) return string.Empty;
        try
        {
            string? oldContent = null;
            string? newContent = null;
            string oldLabel, newLabel;

            if (staged)
            {
                oldLabel = $"a/{path}";
                newLabel = $"b/{path}";
                // HEAD → Index
                oldContent = ReadBlobContent(_repo.Head.Tip?.Tree, path);
                newContent = ReadIndexContent(path);
                if (oldContent is null && newContent is null)
                    return "(no changes)";
                if (oldContent is null)
                    return FormatNewFileDiff(path, newContent!);
                if (newContent is null)
                    return FormatDeletedFileDiff(path, oldContent);
            }
            else
            {
                oldLabel = $"a/{path}";
                newLabel = $"b/{path}";
                // Index → Working Directory
                oldContent = ReadIndexContent(path);
                if (!File.Exists(Path.Combine(_repoPath!, path)))
                {
                    if (oldContent is null) return "(file removed from index and disk)";
                    return FormatDeletedFileDiff(path, oldContent);
                }
                newContent = File.ReadAllText(Path.Combine(_repoPath!, path));
                if (oldContent is null)
                    return FormatNewFileDiff(path, newContent);
            }

            if (oldContent == newContent)
                return "(no changes)";

            return ComputeUnifiedDiff(path, oldLabel, newLabel, oldContent!, newContent!);
        }
        catch (Exception ex)
        {
            return $"Error generating diff: {ex.Message}";
        }
    }

    /// <summary>Returns per-new-line diff status: 0=none, 1=added, 2=modified.</summary>
    public Dictionary<int, byte> GetLineDiffs(string path)
    {
        var result = new Dictionary<int, byte>();
        if (_repo is null) return result;
        try
        {
            string? headContent = ReadBlobContent(_repo.Head.Tip?.Tree, path);
            if (headContent is null) return result;

            string? wdContent = File.Exists(Path.Combine(_repo.Info.WorkingDirectory, path))
                ? File.ReadAllText(Path.Combine(_repo.Info.WorkingDirectory, path))
                : null;
            if (wdContent is null) return result;

            var headLines = headContent.Split('\n');
            var wdLines = wdContent.Split('\n');
            var hunks = ComputeHunks(headLines, wdLines);

            foreach (var hunk in hunks)
            {
                int newLine = hunk.NewStart;
                foreach (var line in hunk.Lines)
                {
                    if (line.Type == DiffLineType.Added)
                        result[newLine] = 1;
                    else if (line.Type == DiffLineType.Removed)
                    { }
                    else
                        newLine++;
                }
            }
        }
        catch { }
        return result;
    }

    private string? ReadBlobContent(Tree? tree, string path)
    {
        var entry = tree?[path];
        return (entry?.Target as Blob)?.GetContentText();
    }

    private string? ReadIndexContent(string path)
    {
        var idxEntry = _repo!.Index[path];
        if (idxEntry is null) return null;
        var blob = _repo.Lookup<Blob>(idxEntry.Id);
        return blob?.GetContentText();
    }

    private string FormatNewFileDiff(string path, string content)
    {
        var lines = content.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- /dev/null");
        sb.AppendLine($"+++ b/{path}");
        sb.AppendLine($"@@ -0,0 +1,{lines.Length} @@");
        foreach (var line in lines)
            sb.AppendLine("+" + line);
        if (content.EndsWith("\n")) sb.AppendLine("+\\ No newline at end of file");
        return sb.ToString();
    }

    private string FormatDeletedFileDiff(string path, string content)
    {
        var lines = content.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- a/{path}");
        sb.AppendLine("+++ /dev/null");
        sb.AppendLine($"@@ -1,{lines.Length} +0,0 @@");
        foreach (var line in lines)
            sb.AppendLine("-" + line);
        return sb.ToString();
    }

    private string ComputeUnifiedDiff(string path, string oldLabel, string newLabel, string oldContent, string newContent)
    {
        var oldLines = oldContent.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var newLines = newContent.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- {oldLabel}");
        sb.AppendLine($"+++ {newLabel}");

        // Simple LCS-based diff for producing hunks
        var hunks = ComputeHunks(oldLines, newLines);
        foreach (var hunk in hunks)
        {
            if (sb.Length > MaxDiffSizeBytes)
            {
                sb.AppendLine($"\n(diff truncated at {MaxDiffSizeBytes / 1024}KB)");
                break;
            }

            sb.AppendLine($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
            foreach (var line in hunk.Lines)
            {
                char prefix = line.Type switch
                {
                    DiffLineType.Added => '+',
                    DiffLineType.Removed => '-',
                    _ => ' '
                };
                sb.AppendLine(prefix + line.Text);
            }
        }

        return sb.ToString();
    }

    private enum DiffLineType { Context, Added, Removed }

    private sealed record DiffLine(DiffLineType Type, string Text);

    private sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, List<DiffLine> Lines);

    private List<DiffHunk> ComputeHunks(string[] oldLines, string[] newLines)
    {
        var hunks = new List<DiffHunk>();
        int oldLen = oldLines.Length;
        int newLen = newLines.Length;

        // Build LCS table
        int[,] lcs = new int[oldLen + 1, newLen + 1];
        for (int i = 1; i <= oldLen; i++)
            for (int j = 1; j <= newLen; j++)
                if (oldLines[i - 1] == newLines[j - 1])
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);

        // Walk the LCS to build diff hunks
        var diffLines = new List<DiffLine>();
        int oi = oldLen, ni = newLen;
        var reversed = new List<DiffLine>();
        while (oi > 0 || ni > 0)
        {
            if (oi > 0 && ni > 0 && oldLines[oi - 1] == newLines[ni - 1])
            {
                reversed.Add(new DiffLine(DiffLineType.Context, oldLines[oi - 1]));
                oi--; ni--;
            }
            else if (ni > 0 && (oi == 0 || lcs[oi, ni - 1] >= lcs[oi - 1, ni]))
            {
                reversed.Add(new DiffLine(DiffLineType.Added, newLines[ni - 1]));
                ni--;
            }
            else
            {
                reversed.Add(new DiffLine(DiffLineType.Removed, oldLines[oi - 1]));
                oi--;
            }
        }

        reversed.Reverse();
        diffLines = reversed;

        // Group into hunks
        int contextBefore = 3;
        int contextAfter = 3;
        int i2 = 0;
        while (i2 < diffLines.Count)
        {
            // Find next change
            while (i2 < diffLines.Count && diffLines[i2].Type == DiffLineType.Context) i2++;
            if (i2 >= diffLines.Count) break;

            int hunkStart = Math.Max(0, i2 - contextBefore);
            int hunkEnd = i2;
            while (hunkEnd < diffLines.Count && diffLines[hunkEnd].Type != DiffLineType.Context) hunkEnd++;
            hunkEnd = Math.Min(diffLines.Count, hunkEnd + contextAfter);

            var hunkLines = diffLines.GetRange(hunkStart, hunkEnd - hunkStart);

            // Count total context/added/removed in hunk
            int oldCount = 0, newCount = 0, oldStart = 0, newStart = 0;
            for (int ci = 0; ci < hunkLines.Count; ci++)
            {
                if (hunkLines[ci].Type != DiffLineType.Added) oldCount++;
                if (hunkLines[ci].Type != DiffLineType.Removed) newCount++;
                if (oldStart == 0 && hunkLines[ci].Type != DiffLineType.Added)
                    oldStart = CountPrecedingOldLines(diffLines, hunkStart + ci);
                if (newStart == 0 && hunkLines[ci].Type != DiffLineType.Removed)
                    newStart = CountPrecedingNewLines(diffLines, hunkStart + ci);
            }

            if (oldStart == 0) oldStart = 1;
            if (newStart == 0) newStart = 1;

            hunks.Add(new DiffHunk(oldStart, oldCount, newStart, newCount, hunkLines));
            i2 = hunkEnd;
        }

        if (hunks.Count == 0)
        {
            // Fallback: show entire diff as one hunk
            int oldStart = 1, newStart = 1;
            int oldC = 0, newC = 0;
            var allLines = new List<DiffLine>();
            // Simple one-hunk diff
            for (int idx = 0; idx < Math.Max(oldLen, newLen); idx++)
            {
                if (idx < oldLen && idx < newLen && oldLines[idx] == newLines[idx])
                {
                    allLines.Add(new DiffLine(DiffLineType.Context, oldLines[idx]));
                    oldC++; newC++;
                }
                else if (idx < oldLen)
                {
                    allLines.Add(new DiffLine(DiffLineType.Removed, oldLines[idx]));
                    oldC++;
                }
                if (idx < newLen)
                {
                    allLines.Add(new DiffLine(DiffLineType.Added, newLines[idx]));
                    newC++;
                }
            }
            hunks.Add(new DiffHunk(oldStart, oldC, newStart, newC, allLines));
        }

        return hunks;
    }

    private int CountPrecedingOldLines(List<DiffLine> allLines, int upTo)
    {
        int count = 0;
        for (int i = 0; i < upTo && i < allLines.Count; i++)
            if (allLines[i].Type != DiffLineType.Added) count++;
        return count + 1;
    }

    private int CountPrecedingNewLines(List<DiffLine> allLines, int upTo)
    {
        int count = 0;
        for (int i = 0; i < upTo && i < allLines.Count; i++)
            if (allLines[i].Type != DiffLineType.Removed) count++;
        return count + 1;
    }

    public (bool Success, string Message) Stash(string? message = null)
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            var signature = new Signature("Personal Flip Pad", "git@pfpad.local", DateTimeOffset.Now);
            _repo.Stashes.Add(signature, message, StashModifiers.Default);
            OnRepoChanged?.Invoke();
            return (true, "Changes stashed.");
        }
        catch (Exception ex) { return (false, $"Stash failed: {ex.Message}"); }
    }

    public (bool Success, string Message) StashPop()
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            if (!_repo.Stashes.Any()) return (false, "No stashes to pop.");
            _repo.Stashes.Pop(0);
            OnRepoChanged?.Invoke();
            return (true, "Stash popped.");
        }
        catch (Exception ex) { return (false, $"Stash pop failed: {ex.Message}"); }
    }

    public bool UnstageAll()
    {
        if (_repo is null) return false;
        try
        {
            var status = _repo.RetrieveStatus(new StatusOptions { IncludeUnaltered = false });
            foreach (var entry in status)
            {
                if (entry.State.HasFlag(FileStatus.NewInIndex) ||
                    entry.State.HasFlag(FileStatus.ModifiedInIndex) ||
                    entry.State.HasFlag(FileStatus.DeletedFromIndex) ||
                    entry.State.HasFlag(FileStatus.RenamedInIndex))
                    _repo.Index.Remove(entry.FilePath);
            }
            _repo.Index.Write();
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex) { OnError?.Invoke($"Could not unstage all: {ex.Message}"); return false; }
    }

    public (bool Success, string Message) DiscardFile(string path)
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            var fullPath = Path.Combine(_repoPath!, path);
            if (!File.Exists(fullPath)) return (false, "File does not exist on disk.");

            var tip = _repo.Head.Tip;
            if (tip is null) return (false, "No commits in this branch yet — cannot restore from HEAD.");

            var blob = tip[path]?.Target as Blob;
            if (blob is null)
            {
                _repo.Index.Remove(path);
                _repo.Index.Write();
                File.Delete(fullPath);
            }
            else
            {
                var content = blob.GetContentText();
                File.WriteAllText(fullPath, content);
                _repo.Index.Remove(path);
                _repo.Index.Write();
            }

            OnRepoChanged?.Invoke();
            return (true, $"Discarded changes in '{path}'.");
        }
        catch (Exception ex) { return (false, $"Could not discard '{path}': {ex.Message}"); }
    }

    public List<ConflictEntry> GetConflicts()
    {
        var result = new List<ConflictEntry>();
        if (_repo is null) return result;

        try
        {
            foreach (var conflict in _repo.Index.Conflicts)
            {
                var path = conflict.Ancestor?.Path ?? conflict.Ours?.Path ?? conflict.Theirs?.Path ?? "";
                result.Add(new ConflictEntry(path));
            }
        }
        catch { }

        return result;
    }

    public bool ResolveConflictOurs(string path)
    {
        if (_repo is null) return false;
        try
        {
            var conflict = _repo.Index.Conflicts[path];
            if (conflict?.Ours is null) return false;

            var ourBlob = _repo.Lookup<Blob>(conflict.Ours.Id);
            if (ourBlob is null) return false;

            var content = ourBlob.GetContentText();
            var fullPath = Path.Combine(_repoPath!, path);
            File.WriteAllText(fullPath, content);

            _repo.Index.Add(path);
            _repo.Index.Remove(path);
            _repo.Index.Write();
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex) { OnError?.Invoke($"Could not resolve '{path}': {ex.Message}"); return false; }
    }

    public bool ResolveConflictTheirs(string path)
    {
        if (_repo is null) return false;
        try
        {
            var conflict = _repo.Index.Conflicts[path];
            if (conflict?.Theirs is null) return false;

            var theirBlob = _repo.Lookup<Blob>(conflict.Theirs.Id);
            if (theirBlob is null) return false;

            var content = theirBlob.GetContentText();
            var fullPath = Path.Combine(_repoPath!, path);
            File.WriteAllText(fullPath, content);

            _repo.Index.Add(path);
            _repo.Index.Remove(path);
            _repo.Index.Write();
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex) { OnError?.Invoke($"Could not resolve '{path}': {ex.Message}"); return false; }
    }

    public int GetBehindCount(string remoteName = "origin")
    {
        if (_repo is null) return -1;
        try
        {
            var branch = _repo.Head;
            var remoteBranch = _repo.Branches[$"{remoteName}/{branch.FriendlyName}"];
            if (remoteBranch is null) return -1;

            var behind = _repo.ObjectDatabase.CalculateHistoryDivergence(
                branch.Tip, remoteBranch.Tip);
            return behind?.BehindBy ?? -1;
        }
        catch { return -1; }
    }

    public (int Behind, int Ahead) GetRemoteStatus(string remoteName = "origin")
    {
        if (_repo is null) return (-1, -1);
        try
        {
            var branch = _repo.Head;
            var remoteBranch = _repo.Branches[$"{remoteName}/{branch.FriendlyName}"];
            if (remoteBranch is null) return (-1, -1);

            var divergence = _repo.ObjectDatabase.CalculateHistoryDivergence(
                branch.Tip, remoteBranch.Tip);
            return (divergence?.BehindBy ?? -1, divergence?.AheadBy ?? -1);
        }
        catch { return (-1, -1); }
    }

    public bool SwitchBranch(string name)
    {
        if (_repo is null) return false;
        try
        {
            var branch = _repo.Branches[name];
            if (branch is null)
            { OnError?.Invoke($"Branch '{name}' not found."); return false; }

            LibGit2Sharp.Commands.Checkout(_repo, branch);
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Could not switch to '{name}': {ex.Message}");
            return false;
        }
    }

    public bool Fetch(string remoteName = "origin")
    {
        if (_repo is null) return false;
        try
        {
            var remote = _repo.Network.Remotes[remoteName];
            if (remote is null)
            { OnError?.Invoke($"Remote '{remoteName}' not found."); return false; }

            var refSpecs = remote.FetchRefSpecs.Select(s => s.Specification);
            _repo.Network.Fetch(remoteName, refSpecs);
            OnRepoChanged?.Invoke();
            return true;
        }
        catch (Exception ex) { OnError?.Invoke($"Fetch failed: {ex.Message}"); return false; }
    }

    public (bool Success, string Message) Pull(string remoteName = "origin")
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            var remote = _repo.Network.Remotes[remoteName];
            if (remote is null) return (false, $"Remote '{remoteName}' not found.");

            var mergeResult = Commands.Pull(_repo,
                new Signature("Personal Flip Pad", "git@pfpad.local", DateTimeOffset.Now),
                new PullOptions());

            var msg = mergeResult.Status switch
            {
                MergeStatus.UpToDate => "Already up to date.",
                MergeStatus.FastForward => "Fast-forward merge completed.",
                MergeStatus.NonFastForward => "Merge completed (non-fast-forward).",
                _ => $"Pull completed: {mergeResult.Status}."
            };

            OnRepoChanged?.Invoke();
            return (true, msg);
        }
        catch (Exception ex) { return (false, $"Pull failed: {ex.Message}"); }
    }

    public (bool Success, string Message) Push(string remoteName = "origin", string? branchName = null, bool force = false)
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            var branch = branchName ?? _repo.Head.FriendlyName;
            var refspec = force
                ? $"+refs/heads/{branch}:refs/heads/{branch}"
                : $"refs/heads/{branch}:refs/heads/{branch}";
            _repo.Network.Push(_repo.Network.Remotes[remoteName],
                refspec,
                new PushOptions());
            OnRepoChanged?.Invoke();
            return (true, force
                ? $"Force pushed '{branch}' to '{remoteName}'."
                : $"Pushed '{branch}' to '{remoteName}'.");
        }
        catch (Exception ex) { return (false, $"Push failed: {ex.Message}"); }
    }

    public void CloseRepo()
    {
        _repo?.Dispose();
        _repo = null;
        _repoPath = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseRepo();
    }
}

public sealed record StatusEntry(string Path, FileStatus State)
{
    public string StateLabel => (State switch
    {
        FileStatus.NewInIndex => "A",
        FileStatus.ModifiedInIndex => "M",
        FileStatus.DeletedFromIndex => "D",
        FileStatus.RenamedInIndex => "R",
        FileStatus.TypeChangeInIndex => "T",
        FileStatus.NewInWorkdir => "A",
        FileStatus.ModifiedInWorkdir => "M",
        FileStatus.DeletedFromWorkdir => "D",
        FileStatus.RenamedInWorkdir => "R",
        FileStatus.TypeChangeInWorkdir => "T",
        FileStatus.Conflicted => "C",
        FileStatus.Ignored => "!",
        _ => "?"
    }) + (State.HasFlag(FileStatus.NewInIndex) ? "" : "");

    public bool IsStaged =>
        State.HasFlag(FileStatus.NewInIndex) || State.HasFlag(FileStatus.ModifiedInIndex) ||
        State.HasFlag(FileStatus.DeletedFromIndex) || State.HasFlag(FileStatus.RenamedInIndex);

    public bool IsUnstaged =>
        State.HasFlag(FileStatus.ModifiedInWorkdir) || State.HasFlag(FileStatus.DeletedFromWorkdir) ||
        State.HasFlag(FileStatus.NewInWorkdir) || State.HasFlag(FileStatus.RenamedInWorkdir);
}

public sealed record CommitEntry(string Sha, string Author, string Date, string Message);
public sealed record BranchEntry(string Name, bool IsCurrent, string TipSha);
public sealed record ConflictEntry(string Path);

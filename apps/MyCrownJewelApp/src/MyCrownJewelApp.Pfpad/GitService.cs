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

    public (bool Success, string Message) Push(string remoteName = "origin", string? branchName = null)
    {
        if (_repo is null) return (false, "No repository open.");
        try
        {
            var branch = branchName ?? _repo.Head.FriendlyName;
            _repo.Network.Push(_repo.Network.Remotes[remoteName],
                $"refs/heads/{branch}",
                new PushOptions());
            OnRepoChanged?.Invoke();
            return (true, $"Pushed '{branch}' to '{remoteName}'.");
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

# Built-in Git Support — Deep Dive Analysis

**Assumption:** No `git.exe` available. Everything must parse the `.git` directory directly.

## Current State

The editor has a thin git status bar layer in `Form1.cs:4594-4704` that shells out to `git.exe` every 3 seconds to display branch name, dirty indicator, and ahead/behind counts. That's the full extent — no blame, diff, staging, commit, log, or network operations exist. The code wraps `Process.Start("git", ...)` which immediately breaks when git.exe is absent.

---

## What the `.git` Directory Contains

A raw `.git` directory is just files on disk. These are the formats a pure-C# engine must parse:

| Path | Format | Purpose |
|------|--------|---------|
| `HEAD` | Text: `ref: refs/heads/main` or a commit hash | Current branch/Detached HEAD |
| `config` | INI-like text | Remote URLs, user name, aliases |
| `refs/heads/` | Text files containing 40-char SHA | Branch tips |
| `refs/tags/` | Text files containing SHA + optional annotated tag object | Tags |
| `objects/XX/XXXX...` | Zlib-compressed blob/tree/commit/tag | All history data |
| `index` | Binary (12 header + N × 64-byte entries) | Staging area |
| `pack/` | `.pack` + `.idx` files | Packed objects (compressed delta chains) |
| `FETCH_HEAD` | Text | Last fetch results |
| `MERGE_HEAD` | Text (during conflicts) | Current merge state |
| `logs/` | Text | Reflog |

---

## Core Components to Implement

### Layer 1: Object Store (~800 lines)

The fundamental building block. Everything in git is content-addressed by SHA-1 hash.

**Needed types:**
```csharp
enum GitObjectType { Blob, Tree, Commit, Tag }

interface IGitObjectStore
{
    byte[] ReadRaw(string sha);          // Decompress from loose or pack
    string Write(GitObjectType type, byte[] content);  // Deflate + store
    bool Exists(string sha);
}

struct GitBlob  { byte[] Data; }                    // Just file content
struct GitTree  { List<TreeEntry> Entries; }        // Directory listing
struct GitCommit{ string Tree, string[] Parents, string Author, string Message; }
```

**Sub-components:**
- **SHA-1 hash computation** (`System.Security.Cryptography.SHA1` — built-in)
- **Zlib deflate/inflate** (`System.IO.Compression.DeflateStream` — built-in, but RFC 1950 wrapper is needed since .NET's raw Deflate lacks the 2-byte zlib header)
- **Loose object parser**: read file → skip 2 zlib header bytes → DeflateStream → parse header (`blob N\0`) → content
- **Pack file reader**: parse `.idx` (SHA → offset map), then `.pack` (decompress delta-encoded objects). This is the hardest part — git uses **xdiff** for binary deltas which must be implemented from scratch
- **Multi-pack index** (`.midx`) for modern repos

### Layer 2: Reference Management (~300 lines)

```csharp
interface IRefManager
{
    string ResolveHEAD();              // Follows ref chain
    string? GetBranch(string name);    // Returns SHA
    Dictionary<string, string> GetAllBranches();
    Dictionary<string, string> GetAllTags();
    void SetBranch(string name, string sha);
}
```

- Parse `HEAD` — may be symbolic (`ref: refs/heads/main`) or direct (detached)
- Walk `refs/heads/`, `refs/tags/`, `refs/remotes/` directories
- Handle packed-refs (older git creates a single `packed-refs` text file)
- Follow symbolic refs recursively

### Layer 3: Index (Staging Area) (~400 lines)

The index is a binary file with the format:
- 12-byte header (signature `DIRC`, version 2/3, entry count)
- N × 64-byte entries (ctime, mtime, dev, ino, mode, uid, gid, size, SHA, flags, name)
- Optional extensions (tree cache, untracked cache, FSCache, etc.)

**Needed for:**
- Determining which files are staged vs. modified vs. untracked
- Showing git status without parsing the working tree every cycle
- Stage/unstage operations
- Tree diff for commit creation

### Layer 4: Diff Engine (~600 lines)

The most complex UI-facing component:

```csharp
interface IGitDiff
{
    List<DiffHunk> DiffBlobs(byte[] oldContent, byte[] newContent, string path);
    List<DiffFile> DiffWorkingTree(string repoPath);
    List<DiffFile> DiffIndex(string repoPath);       // staged vs HEAD
    List<DiffFile> DiffWorkingVsIndex(string repoPath); // unstaged
}
```

**Sub-components:**
- **Myers diff algorithm** (O(ND) or O(NP)) — ~200 lines for a basic implementation
- **Diff-to-hunk conversion** — splitting into line ranges with context
- **Binary detection** — check for NUL bytes
- **Rename/copy detection** — similarity scoring (used in `git status --porcelain`)

Git's actual diff output for blame and inline editor annotations (green/red gutters) requires character-level diff within lines, not just file-level hunks.

### Layer 5: Git Status (~500 lines)

Combines refs + index + working tree + ignore rules:

```csharp
struct GitStatusEntry {
    string Path;
    StatusKind Kind; // Modified, Added, Deleted, Untracked, Renamed, Conflicted
    string? OldPath; // for renames
}

interface IGitStatus
{
    List<GitStatusEntry> GetStatus(string repoPath);
    bool HasUncommitted(string repoPath);
}
```

**Sub-components:**
- **`.gitignore` parser**: glob-style pattern matching with negation (`!`), directory-only (`/`), anchored vs. unanchored patterns. Must handle nested `.gitignore` files per directory
- **Working tree walker**: enumerate files, compare mtime/size against index, SHA-1 hash changed files
- **Untracked file detection**: files not in index and not gitignored
- **Submodule handling**: `.gitmodules` parsing, detecting submodule entries

### Layer 6: Commit/Revert/Staging Operations (~400 lines)

```csharp
interface ICommitOperations
{
    string CreateCommit(string repoPath, string message, string author, string email);
    void Stage(string repoPath, string path);
    void Unstage(string repoPath, string path);
    void RevertFile(string repoPath, string path);
    List<GitCommit> GetLog(string repoPath, int count = 50, string? branch = null);
}
```

- **Write tree from index**: traverse index entries → build tree objects → write to object store
- **Write commit object**: tree SHA + parent SHAs + author/committer + message → write to object store
- **Update ref**: write SHA to `refs/heads/branch`
- **Log parsing**: walk commit graph (commit → parent → grandparent)

### Layer 7: Blame (~400 lines)

```csharp
interface IGitBlame
{
    GitBlameEntry[] BlameFile(string repoPath, string filePath);
}

struct GitBlameEntry {
    int LineStart, LineCount;
    string CommitSha, Author, DateTime, Summary;
}
```

This is computationally expensive: for each line, walk backwards through history until the line was introduced. Requires:
- Full commit graph traversal
- Content-based line matching across revisions
- Incremental caching (only re-blame lines that changed since last cache)

### Layer 8: Network Operations (~800 lines)

The most complex layer:

```csharp
interface IRemoteOperations
{
    void Clone(string url, string localPath);
    void Fetch(string repoPath, string remote = "origin");
    void Push(string repoPath, string remote = "origin");
}
```

**Smart HTTP protocol (most common):**
1. `GET /repo.git/info/refs?service=git-upload-pack` → get ref advertisement
2. Parse pkt-line format (length-prefixed packets)
3. Negotiate wants/haves (which objects the client already has)
4. Download pack file → index it → extract objects

**SSH protocol:** requires SSH client integration (libssh2 or similar)

**Git protocol (9418):** deprecated

---

## Comparison: Pure C# vs. Existing Libraries

| Approach | Lines of Code | Time | Risk |
|----------|--------------|------|------|
| Shell out to `git.exe` (current) | ~150 | Done | Breaks without git |
| Wrap **LibGit2Sharp** (C bindings) | ~500 | Days | C dependency, cross-platform pain |
| Use **GitRead .NET** (pure C#, read-only) | ~100 | Hours | Read-only, no commit/stage |
| Full custom implementation | ~4,000–5,000 | Months | Total control, maximum effort |

**LibGit2Sharp** is the pragmatic middle ground: wraps libgit2 (a C library used by GitHub, GitLab, Visual Studio). It provides the full git API — read, write, commit, push, pull, clone, blame, diff, merge. Adding it requires:
```
dotnet add package LibGit2Sharp
```
The downside: libgit2 ships native binaries (~3 MB per platform) which must be included with the installer.

A **pure C# implementation** avoids native dependencies but means reimplementing everything git has evolved over 20 years: pack-file deltas, the full gitignore grammar, rename detection heuristics, SSH handshakes, etc.

---

## Recommended Feature Scope for an Editor

Not all git features matter in an editor context:

| Must-Have | Nice-to-Have | Skip |
|-----------|--------------|------|
| Branch display | Staging UI | Rebase |
| Dirty file indicator | Blame annotations | Bisect |
| Basic status (modified/untracked) | Diff view | Notes |
| Open from repo | Commit from editor | Submodules |
| Recent commits (log) | .gitignore parsing | Grafts/replace |
| File history | Branch switching | Worktrees |

---

## Implementation Roadmap (Pure C#)

Estimated ~4,000–5,000 lines of production code:

```
Phase 1 — Read-Only Core (1,200 lines, ~2 weeks)
├── SHA-1 + zlib wrapper (80 lines)
├── Loose object reader (150 lines)
├── Pack file reader (350 lines)
├── Ref manager (200 lines)
├── Index parser (200 lines)
└── Tree-to-path resolver (120 lines)

Phase 2 — Status & Diff (1,200 lines, ~2 weeks)  
├── Gitignore parser (250 lines)
├── Working tree scanner (200 lines)
├── Myers diff (200 lines)
├── Status computation (300 lines)
└── Diff rendering (250 lines)

Phase 3 — Commit & History (1,000 lines, ~1 week)
├── Tree writer (150 lines)
├── Commit creator (150 lines)
├── Log walker (300 lines)
├── Blame engine (400 lines)

Phase 4 — Network (800 lines, ~1 week)
├── pkt-line parser (100 lines)
├── HTTP smart protocol (400 lines)
├── Pack downloader (200 lines)
└── Remote ref management (100 lines)

Phase 5 — UI Integration (800 lines, ~1 week)
├── Git panel (tool window with status/log) (300 lines)
├── Inline blame gutter annotations (150 lines)
├── Commit dialog (100 lines)
├── Branch switcher (100 lines)
└── Settings / credential storage (150 lines)

Total: ~5,000 lines, ~7 weeks full-time
```

---

## Key Gotchas

1. **Zlib wrapper**: .NET's `DeflateStream` does **not** handle the 2-byte zlib header (RFC 1950). You need a custom `ZlibStream` that reads/strips the header and computes the ADLER-32 checksum on decompression.

2. **Pack file deltas**: Git uses a custom `xdiff` binary delta format. The `libgit2` implementation is ~500 lines of C. Reimplementing it in C# is ~300-400 lines and is the single hardest parsing problem.

3. **SHA-256**: Git is migrating from SHA-1 to SHA-256 (stage: experimental). A future-proof implementation needs both.

4. **Performance**: Reading a pack file for a repo like dotnet/runtime (~500,000 objects) means decompressing thousands of delta chains. Without aggressive caching, blame can take 30+ seconds.

5. **Encoding**: Filenames in tree objects can be UTF-8, ISO-8859-1, or just raw bytes. Commit messages can be any encoding (declared in the object header). Blame output must handle mixed-encoding files.

---

## Recommendation

**For this project**, the practical path is:

1. **Short term**: Add a `try/catch` around the existing `Process.Start("git", ...)` calls so the editor degrades gracefully when git.exe is absent (the status bar labels stay empty). This is a 15-minute fix.

2. **Medium term**: Wrap **LibGit2Sharp**. It adds ~3 MB to the installer but gives a complete, battle-tested git implementation in ~500 lines of C# wrapper code. All features (status, blame, commit, diff, log, clone) work out of the box.

3. **Only if needed**: Pure C# implementation. The value proposition is avoiding the native dependency, but the cost is reimplementing 20 years of git internals. Unless the editor will be a standalone git GUI, it's hard to justify.

The existing polling approach at `Form1.cs:4660` (`RunGit`) is the single point of failure. Replacing the `Process.Start("git", ...)` call with a `LibGit2Sharp` equivalent (`Repository.Status()`, `Repository.Head`, `Branch.TrackingDetails`) would be a localized change affecting only ~50 lines in `PollGitStatus()`.

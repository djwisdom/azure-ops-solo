# .NET 10 + WinForms Migration Analysis & Re-implementation Plan
## Personal Flip Pad (Pfpad) — MyCrownJewelApp v1.0.35.0

**Analysis date:** 2026-06-06 (original) · **Updated:** 2026-06-16  
**Analyst:** GitHub Copilot CLI (deep-dive scan)  
**Prior TFM:** `net8.0-windows` → `net9.0-windows` (v1.0.34.0) → **`net10.0-windows` ✅ (v1.0.35.0, 2026-06-16)**  
**SDK on machine:** .NET SDK `10.0.300` (stable, confirmed installed)  

---

## ✅ MIGRATION STATUS: COMPLETE (2026-06-16)

### Environment Verification

| Component | Status | Version |
|-----------|--------|---------|
| .NET 10 SDK | ✅ Installed | 10.0.300 (stable) |
| Microsoft.NETCore.App 10.x | ✅ Installed | 10.0.8, 10.0.9 |
| Microsoft.WindowsDesktop.App 10.x | ✅ Installed | 10.0.8 (WinForms runtime) |
| Microsoft.AspNetCore.App 10.x | ✅ Installed | 10.0.8 |
| global.json | ✅ Updated | Pins 10.0.300 + latestPatch |

### Files Changed (2026-06-16)

| File | Change |
|------|--------|
| `apps/MyCrownJewelApp/global.json` | SDK pin: 9.0.314 → 10.0.300 |
| `src/.../MyCrownJewelApp.Pfpad.csproj` | TFM: net9.0-windows → net10.0-windows; Roslyn 4.8.0 → 5.3.*; M.E.* 9.* → 10.* |
| `tests/MyCrownJewelApp.Tests.csproj` | TFM: net9.0-windows → net10.0-windows |
| `pipelines/deploy-app.yml` | dotnetSdkVersion: "9.0.x" → "10.0.x" |
| `tests/.../ElasticTabServiceTests.cs` | Added `minTabWidth: 0` to 4 elastic-tab tests broken by a prior production code change |

### Build & Test Results (Post-Migration)

| Metric | Result |
|--------|--------|
| Build errors | **0** |
| Build warnings | 15 (pre-existing nullable + CS8848, all pre-date this migration) |
| Total tests | 250 |
| Passed | **246–248** (varies by run) |
| Skipped | 1 (STA thread — pre-existing) |
| Failed | 0–3 (flaky timing tests — pre-existing, pass in isolation) |
| C# language version | **C# 14** (auto-selected by net10.0-windows) |

### Package Versions (Final State)

| Package | Before | After |
|---------|--------|-------|
| Microsoft.CodeAnalysis.* | 4.8.0 | **5.3.*** |
| Microsoft.Extensions.DependencyInjection | 9.* | **10.*** |
| Microsoft.Extensions.Logging | 9.* | **10.*** |
| Microsoft.Extensions.Logging.Debug | 9.* | **10.*** |
| LibGit2Sharp | 0.31.0 | 0.31.0 (current) |
| TreeSitter.DotNet | 1.3.0 | 1.3.0 (current) |

### Known Flaky Tests (Pre-existing, Not Migration-Related)

These 3 tests pass in isolation but are timing-sensitive under full-suite load due to async worker scheduling on a shared machine. They are pre-existing and not caused by the .NET 10 migration:

| Test | Root Cause |
|------|-----------|
| `IncrementalHighlighterTests.Tokenizer_ProducesCSharpTokens` | `GetTokens()` returns null when async worker hasn't flushed within 1000ms timeout under load |
| `SyntaxHighlightRegressionTests.C_Syntax_KeywordsHighlighted` | Same — 500ms sleep not enough under full-suite load |
| `SyntaxHighlightRegressionTests.CSharp_MultipleLineTypes` | Same — 1000ms sleep not enough under full-suite load |
| `IncrementalHighlighterTests.Highlighter_MarksDirty_AndTokenizes` | Marked `[Fact(Skip = ...)]` — kept skipped |

**Fix recommendation:** Replace `Thread.Sleep` + polling in these tests with `ManualResetEventSlim` or a `TaskCompletionSource` wired to the `PatchReady` event.

### P/Invoke Audit

23 files use `[DllImport]` (uxtheme, shell32, dwmapi, user32). All are forward-compatible with .NET 10. Modernising to `[LibraryImport]` is a nice-to-have (eliminates managed-to-native marshalling overhead) but is not a migration blocker.

---



## 1. Executive Summary

Pfpad is a capable solo-developer WinForms code editor with 100 C# source files (~1.76 MB), 22 themes, Roslyn IntelliSense, DAP debugging, Git integration, Vim mode, and a terminal. The codebase is functional (81/81 tests pass) but carries several structural debts that compound as features grow.

**Honest Assessment:**

| Area | Rating | Notes |
|---|---|---|
| Feature breadth | ✅ Excellent | Rivals lightweight commercial editors |
| Code correctness | ✅ Good | Channels-based async, timeout guards |
| Architecture | ⚠️ Warning | Form1.cs is a 380 KB God Class |
| Testability | ⚠️ Warning | 81 tests but majority test utility classes, not UI logic |
| Upgrade readiness | ✅ Ready | .NET 10 SDK already installed; migration is low-risk |
| Package freshness | ⚠️ Warning | Roslyn 4.8.0 is 2 major versions behind current |
| Installer config | ❌ Issue | MSIX flag + Inno Setup conflict in csproj |

**Recommendation:** Upgrade to `net10.0-windows` now (it's a 3-line change + package updates). Pursue architectural refactoring incrementally — do NOT rewrite from scratch.

---

## 2. Current State Inventory

### 2.1 Project File Analysis (`MyCrownJewelApp.Pfpad.csproj`)

```xml
<!-- CURRENT -->
<TargetFramework>net8.0-windows</TargetFramework>
<WindowsPackageType>MSIX</WindowsPackageType>                   <!-- ❌ CONFLICT with Inno Setup -->
<NoWarn>$(NoWarn);NETSDK1057</NoWarn>                           <!-- ❌ suppresses legit preview warning -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />   <!-- ⚠️ old -->
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Features" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Features" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />
<PackageReference Include="LibGit2Sharp" Version="0.31.0" />
<PackageReference Include="TreeSitter.DotNet" Version="1.3.0" />
```

### 2.2 Source Scale

| Metric | Value |
|---|---|
| Total .cs files | 100 |
| Total source size | ~1.76 MB |
| Form1.cs size | **380 KB (~6,900 lines)** |
| Test files | 8 xUnit files |
| Tests passing | 81 / 81 (1 skipped) |
| Subdirectory assemblies | None (single project only) |

### 2.3 Key Source Files (Hot Files)

| File | Role | Lines (est.) |
|---|---|---|
| `Form1.cs` | Main shell — God Class | ~6,900 |
| `Form1.Designer.cs` | WinForms designer-generated layout | ~800 |
| `Form1.Roslyn.cs` | Roslyn event wiring (partial class) | ~300 |
| `IncrementalHighlighter.cs` | Async highlight engine | ~600 |
| `HighlightRichTextBox.cs` | Custom RichTextBox (rendering) | ~800 |
| `ThemeManager.cs` | Theme singleton (22 themes) | ~400 |
| `VimEngine.cs` | Vim state machine | ~1,200 |
| `GitService.cs` + `GitPanel.cs` | LibGit2Sharp integration | ~800 |
| `SessionManager.cs` | Session save/restore | ~200 |
| `Debugger/DebugSession.cs` | DAP session state machine | ~500 |

---

## 3. Architectural Issues (Honest Critique)

### 3.1 Form1.cs — God Class (Critical)

**Problem:** Form1.cs is 380 KB. It owns state and logic for:
- Document management (multi-tab, dirty flag, encoding)
- Settings persistence (AppSettings record + 40+ fields)
- Syntax highlighting coordination
- Theme application
- Terminal, Workspace, Git, Symbol, Problems panel visibility/layout
- Vim mode toggle
- Roslyn workspace wiring
- Debugger session management
- Auto-save, session restore
- Notification feed
- User profiles
- Rainbow brackets
- Elastic tab stops
- Minimap
- Breadcrumbs
- Sticky scroll

**Impact:**
- Every new feature adds more state to an already overloaded class.
- Unit testing Form1 logic requires instantiating the entire form (STA thread, WinForms pump).
- Partial classes (`Form1.Roslyn.cs`) help slightly but don't address the root cause.
- A bug in one area can corrupt unrelated state.

**Correct approach:** Decompose via services (see Section 5).

### 3.2 Missing Dependency Injection

All services are `new`-ed inline in Form1's constructor or field initializers:
```csharp
private readonly GitService _gitService = new();
private readonly LintEngine _lintEngine = new();
private readonly SymbolIndexService _symbolIndex = new();
private readonly DebugSession _debugSession = new();
private readonly BreakpointManager _breakpointManager = new();
private readonly NotificationFeedService _notificationFeed = new();
private readonly UserProfileManager _profileManager = new();
private readonly SessionManager _sessionManager = new();
```

This makes services non-swappable, non-mockable for testing, and couples lifetime management to Form1.

### 3.3 Package Version Debt

| Package | Current | Latest (Jun 2026) | Gap |
|---|---|---|---|
| Microsoft.CodeAnalysis.* | 4.8.0 | 4.14.x | 6 minor versions |
| LibGit2Sharp | 0.31.0 | 0.31.0 | Current |
| TreeSitter.DotNet | 1.3.0 | 1.3.0 | Current |

Roslyn 4.8.0 misses improvements in:
- `SemanticModel` performance (4.9)
- Better nullable annotation flow (4.10)
- Incremental source generator perf (4.11+)
- .NET 10 language features C# 14 (4.14)

### 3.4 MSIX/Inno Setup Conflict

```xml
<WindowsPackageType>MSIX</WindowsPackageType>
```

This property tells the .NET SDK to package the app as MSIX — but the actual installer is Inno Setup (`installer.iss`, `PersonalFlipPad-Setup-1.0.18.0.exe`). This property:
- Adds MSIX-specific build targets that may produce unexpected output
- Can interfere with the `dotnet publish` output structure
- Should be **removed**

### 3.5 Leftover Migration Artifacts

These files in `src/MyCrownJewelApp.Pfpad/` should be deleted:
- `patch_form.py`
- `patch_form2.py`
- `patch_form3.py`
- `temp_HighlightRichTextBox.cs` (root of repo)
- `temp.txt` (root of repo)
- `debug_output.txt` (root of repo)
- `Continue` (empty file at root)

---

## 4. .NET 10 Migration — Detailed Plan

### 4.1 What .NET 10 Brings for WinForms

| Improvement | Relevance to Pfpad |
|---|---|
| High DPI improvements | Minimap, GutterPanel rendering at 150%+ |
| `RichTextBox` API additions | Better selection range APIs |
| `System.Threading.Channels` perf | IncrementalHighlighter worker loop |
| `Span<T>` / `ReadOnlySpan<T>` in BCL | UnicodeFileHelper optimizations |
| C# 14 language features | `params ReadOnlySpan<T>`, `field` keyword |
| Improved `System.Text.Json` | Settings, session, and DAP serialization |
| Better `async` machinery | Cancellation improvements for background services |
| Trimming support improvements | Installer size reduction (if self-contained) |

### 4.2 Migration Steps

#### Step 1: Update TFM (Low Risk)

**File:** `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/MyCrownJewelApp.Pfpad.csproj`

```xml
<!-- CHANGE -->
<TargetFramework>net10.0-windows</TargetFramework>

<!-- REMOVE (no longer needed) -->
<!-- <NoWarn>$(NoWarn);NETSDK1057</NoWarn> -->

<!-- REMOVE (Inno Setup is the installer, not MSIX) -->
<!-- <WindowsPackageType>MSIX</WindowsPackageType> -->
```

**File:** `apps/MyCrownJewelApp/tests/MyCrownJewelApp.Tests.csproj`

```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

#### Step 2: Update Package References

```xml
<!-- Roslyn: bump to latest 4.14.x -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.14.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Features" Version="4.14.*" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Features" Version="4.14.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.14.*" />

<!-- LibGit2Sharp: stays at 0.31.0 (current) -->
<PackageReference Include="LibGit2Sharp" Version="0.31.0" />

<!-- TreeSitter.DotNet: stays at 1.3.0 (current) -->
<PackageReference Include="TreeSitter.DotNet" Version="1.3.0" />

<!-- ADD: DI + logging (for Phase 2 refactor) -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.*" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.*" />
<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.*" />
```

#### Step 3: Fix C# 14 Compatibility Breaks (Expected: None)

The codebase uses C# 10/11 features (`record`, file-level namespaces, `null!`). All are forward-compatible with C# 14. Run `dotnet build` and address any new warnings.

Known pre-existing patterns that may generate new warnings on .NET 10:
- `catch { }` empty catch blocks (Roslyn now warns CA1031 more aggressively)
- `HashSet<int> bookmarks = new()` in initializers (style only, not a break)

#### Step 4: Update Build Script

**File:** `apps/MyCrownJewelApp/build.ps1`

Update any hardcoded `net8.0-windows` path references to `net10.0-windows`.

#### Step 5: Update Pipeline YAML

**File:** `pipelines/deploy-app.yml`

```yaml
# Update dotnet-version
- task: UseDotNet@2
  inputs:
    version: '10.x'
```

#### Step 6: Validate

```powershell
cd apps/MyCrownJewelApp
dotnet restore
dotnet build
dotnet test tests/MyCrownJewelApp.Tests.csproj
```

Expected result: 81+ tests pass (same as before).

---

## 5. Architectural Re-implementation Recommendations

### 5.1 Priority Order

| Priority | Change | Effort | Risk |
|---|---|---|---|
| P0 | .NET 10 TFM + package updates | 1 hour | Low |
| P0 | Remove MSIX flag, clean up temp files | 30 min | Low |
| P1 | Introduce DI container in `Program.cs` | 1 day | Low |
| P1 | Extract `DocumentManager` from Form1 | 3 days | Medium |
| P2 | Extract `EditorLayoutManager` from Form1 | 2 days | Medium |
| P2 | Extract `SettingsService` from Form1 | 1 day | Low |
| P3 | Replace `File.AppendAllText` logging with `ILogger` | 0.5 days | Low |
| P3 | Solution Explorer + build service (per SOLUTION_IDE_ASSESSMENT.md) | 10-14 days | High |

### 5.2 P0: Immediate Cleanup (Do Today)

```powershell
# Delete leftover migration artifacts
Remove-Item "apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/patch_form.py"
Remove-Item "apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/patch_form2.py"
Remove-Item "apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/patch_form3.py"
Remove-Item "temp_HighlightRichTextBox.cs"
Remove-Item "temp.txt"
Remove-Item "debug_output.txt"
Remove-Item "Continue"
```

### 5.3 P1: Introduce Dependency Injection

**Pattern:** Add `Microsoft.Extensions.DependencyInjection`. Wire services in `Program.cs`. Pass `IServiceProvider` to `Form1` constructor, or use constructor injection directly.

```csharp
// Program.cs — PROPOSED
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<ThemeManager>(_ => ThemeManager.Instance);
        services.AddSingleton<SessionManager>();
        services.AddSingleton<GitService>();
        services.AddSingleton<LintEngine>();
        services.AddSingleton<SymbolIndexService>();
        services.AddSingleton<BreakpointManager>();
        services.AddSingleton<DebugSession>();
        services.AddSingleton<NotificationFeedService>();
        services.AddSingleton<UserProfileManager>();
        services.AddSingleton<SnippetEngine>();
        services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddTransient<Form1>();

        var sp = services.BuildServiceProvider();
        Application.Run(sp.GetRequiredService<Form1>());
    }
}
```

**Impact:** Each service becomes independently testable. Form1 receives injected services instead of newing them, reducing coupling and enabling mocking.

### 5.4 P1: Extract DocumentManager

The document/tab subsystem in Form1 (~800 lines across document management methods) should become a distinct class:

```csharp
// NEW FILE: DocumentManager.cs
public sealed class DocumentManager
{
    public IReadOnlyList<Document> Documents { get; }
    public Document? ActiveDocument { get; }
    public int ActiveIndex { get; }

    public event Action<Document>? DocumentOpened;
    public event Action<Document>? DocumentClosed;
    public event Action<Document>? DocumentActivated;
    public event Action<Document>? DocumentDirtied;

    public Document NewDocument();
    public Task<Document?> OpenAsync(string path, CancellationToken ct = default);
    public Task<bool> SaveAsync(Document doc, CancellationToken ct = default);
    public Task<bool> SaveAsAsync(Document doc, string path, CancellationToken ct = default);
    public bool Close(Document doc, bool force = false);
    public void SetActive(int index);
}
```

Form1 subscribes to events and updates UI. This separates document state from WinForms rendering.

### 5.5 P2: Extract SettingsService

The `AppSettings` record + load/save logic (~200 lines in Form1) should move to:

```csharp
// NEW FILE: SettingsService.cs
public sealed class SettingsService
{
    public AppSettings Current { get; private set; }
    public event Action<AppSettings>? SettingsChanged;

    public void Load();
    public void Save();
    public void Update(Func<AppSettings, AppSettings> mutator);
}
```

### 5.6 P3: Replace Startup Logging

```csharp
// CURRENT (Program.cs) — file appends scattered everywhere
File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Application starting...");

// PROPOSED — use ILogger
_logger.LogInformation("Application starting");
```

Route `ILogger` to a file sink (e.g., `Microsoft.Extensions.Logging.File` or write a simple `FileLoggerProvider`). This gives structured, leveled, filterable logs instead of raw appended text.

---

## 6. What NOT to Change

### 6.1 Do Not Rewrite from Scratch

The editor has 100+ files and deep WinForms integration. A full rewrite (e.g., to WPF, WinUI 3, MAUI, or Blazor Hybrid) would:
- Take 6-12+ months for feature parity
- Lose all the carefully tuned P/Invoke rendering (DWM, scrollbar theming, caret)
- Reintroduce all the edge cases already solved (large files, RTL text, encoding detection)

WinForms on .NET 10 is fully supported, performant, and actively developed. There is no compelling technical reason to migrate the UI framework.

### 6.2 Do Not Split into Multiple Projects

Creating `MyCrownJewelApp.Core`, `MyCrownJewelApp.Terminal`, etc. would add build complexity without benefit for a solo developer. The single-project structure is appropriate for this scale. Services should be extracted as classes within the same project.

### 6.3 Do Not Add More Features Before Fixing Form1

Every new feature added to Form1 makes the God Class worse. The P1 DI + DocumentManager extraction should be done before implementing the Solution Explorer, Build service, or NuGet Manager.

---

## 7. Complete Proposed .csproj (Target State)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <AssemblyName>MyCrownJewelApp.Pfpad</AssemblyName>
    <RootNamespace>MyCrownJewelApp.Pfpad</RootNamespace>
    <ApplicationVersion>1.0.34.0</ApplicationVersion>
    <Version>1.0.34.0</Version>
    <AssemblyVersion>1.0.34.0</AssemblyVersion>
    <FileVersion>1.0.34.0</FileVersion>
    <Company>Personal Flip Pad</Company>
    <Description>A comprehensive code editor with advanced features.</Description>
    <Copyright>Copyright (c) 2026</Copyright>
    <!-- Removed: WindowsPackageType=MSIX (conflicts with Inno Setup) -->
    <!-- Removed: NETSDK1057 suppression (no longer needed on net10) -->
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <DefineConstants>$(DefineConstants);PROFILING</DefineConstants>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)'=='Release'">
    <DefineConstants>$(DefineConstants);PROFILING;DEBUG</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <!-- Git -->
    <PackageReference Include="LibGit2Sharp" Version="0.31.0" />

    <!-- Roslyn (updated to latest 4.14.x for .NET 10 / C# 14) -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.*" />
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.Common" Version="4.14.*" />
    <PackageReference Include="Microsoft.CodeAnalysis.Features" Version="4.14.*" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Features" Version="4.14.*" />
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.14.*" />

    <!-- Tree-sitter -->
    <PackageReference Include="TreeSitter.DotNet" Version="1.3.0" />

    <!-- DI + Logging (NEW for P1 refactor) -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.*" />
  </ItemGroup>

  <Target Name="GenerateBuildMetadata" BeforeTargets="CoreCompile">
    <Exec Command="git rev-parse HEAD" ConsoleToMSBuild="true" IgnoreExitCode="true"
          StandardOutputImportance="low">
      <Output TaskParameter="ConsoleOutput" PropertyName="GitCommitHash" />
    </Exec>
    <PropertyGroup>
      <BuildDate>$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss'))</BuildDate>
      <_MetadataOutputPath>$([System.IO.Path]::Combine('$(MSBuildProjectDirectory)',
        '$(IntermediateOutputPath)', 'BuildMetadata.g.cs'))</_MetadataOutputPath>
    </PropertyGroup>
    <Exec Command="powershell -NoProfile -ExecutionPolicy Bypass -File
      &quot;$(MSBuildProjectDirectory)\GenerateBuildMetadata.ps1&quot;
      -OutputPath &quot;$(_MetadataOutputPath)&quot;
      -CommitHash &quot;$(GitCommitHash)&quot;
      -BuildDate &quot;$(BuildDate)&quot;" />
    <ItemGroup>
      <Compile Include="$(_MetadataOutputPath)" />
    </ItemGroup>
  </Target>

</Project>
```

---

## 8. Migration Checklist

### Phase 0 — Cleanup (30 min)
- [ ] Delete `patch_form.py`, `patch_form2.py`, `patch_form3.py`
- [ ] Delete `temp_HighlightRichTextBox.cs`, `temp.txt`, `debug_output.txt`, `Continue` from repo root
- [ ] Pop stash `stash@{0}` (Tab fix) and commit it

### Phase 1 — .NET 10 TFM (1–2 hours)
- [ ] Update `MyCrownJewelApp.Pfpad.csproj`: TFM to `net10.0-windows`, remove MSIX, remove NETSDK1057 suppression
- [ ] Update `MyCrownJewelApp.Tests.csproj`: TFM to `net10.0-windows`
- [ ] Update Roslyn packages to `4.14.*`
- [ ] `dotnet restore && dotnet build`
- [ ] `dotnet test` — confirm 81 pass
- [ ] Update `build.ps1` and `pipelines/deploy-app.yml` dotnet version
- [ ] Update `README.md` references from `.NET 8` to `.NET 10`
- [ ] Update `MEMORY.md` target framework entry

### Phase 2 — DI + Logging ✅ (2026-06-06)

**What was implemented:**
- `Program.cs` rebuilt with `IServiceCollection` / `IServiceProvider`
- Three app-wide singletons registered: `NotificationFeedService`, `UserProfileManager`, `SessionManager`
- `StartupFileLoggerProvider.cs` — minimal `ILoggerProvider` writing to `%LocalAppData%\MyCrownJewelApp\Pfpad\startup.log`, replacing scattered `File.AppendAllText` calls
- `ILoggerFactory` + `ILogger` wired via `AddLogging(b => b.AddDebug().AddProvider(StartupFileLoggerProvider))`
- `Form1(bool skipInitialDocument, IServiceProvider? services)` — new primary constructor. Initializes the 3 singletons from DI when `services != null`, falls back to `new T()` for designer and tear-away windows
- `Form1(bool)` → `Form1(bool, services: null)` (chained)
- `Form1()` → `Form1(false, null)` (chained, designer-safe)
- `ServiceProvider` disposed via `using` after `Application.Run(form)` exits
- Form-scoped services (`GitService`, `LintEngine`, `SymbolIndexService`, `DebugSession`, `BreakpointManager`) kept as `new()` field initializers — they carry per-window mutable state and are not safe as singletons

**Why only 3 singletons (not all services):**
The rubber-duck review identified that `GitService`, `LintEngine`, `SymbolIndexService`, and `DebugSession` track per-window state (active repo, Roslyn workspace, debug process). Making them singletons would cause one window to overwrite another window's workspace. Only the 3 services with no per-window state were promoted to DI singletons.

### Phase 3 — DocumentManager Extraction (3 days)
- [ ] Create `DocumentManager.cs` with full document/tab lifecycle
- [ ] Move `Form1.Document` nested class to `Document.cs` (top-level)
- [ ] Wire Form1 UI to DocumentManager events
- [ ] Add unit tests for DocumentManager (no WinForms required)

### Phase 4 — SettingsService Extraction (1 day)
- [ ] Create `SettingsService.cs`
- [ ] Move `AppSettings` record to `SettingsService.cs`
- [ ] Replace Form1 direct settings field access with `SettingsService.Current`

### Phase 5 — Solution IDE (4–6 weeks, per SOLUTION_IDE_ASSESSMENT.md)
- [ ] Solution file parser/writer
- [ ] Solution explorer tree
- [ ] Async build service + output panel
- [ ] Run-in-terminal integration
- [ ] Debug toolbar + startup project

---

## 9. Verdict

**Upgrade to .NET 10: Yes, do it immediately.** The SDK is already on the machine, the migration is 3 lines in the csproj plus a package update, and the risk is minimal. WinForms on .NET 10 is the correct long-term target for this tool.

**Architectural rewrite: No.** The codebase works, tests pass, and features are well-implemented. The structural issues (Form1 God Class, no DI) are real but manageable through incremental extraction — not a full rewrite.

**WinForms as UI framework: Yes, keep it.** The editor has deep, correct Win32 integration (DWM title bar, uxtheme scrollbars, P/Invoke for high-performance RichTextBox manipulation). Replacing this with WPF, WinUI 3, or Blazor Hybrid would cost months for no user-visible gain. WinForms on .NET 10 is fully supported and not deprecated.

**Next single action:** Run the Phase 0 cleanup and Phase 1 .NET 10 upgrade. Total time: 2 hours. No functional changes to the editor.

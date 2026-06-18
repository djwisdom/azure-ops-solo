# Personal Flip Pad — User Manual

**Version 1.0.36.0**

Personal Flip Pad (pfpad) is a flat-themed, AIOps-aware developer editor and IDE for Windows. It combines code editing, DevSecOps scanning, observability intelligence, Git integration, and AI-assisted operational insights into a single workflow. pfpad also includes a built-in graded security hardening system that lets you progressively harden your environment as your needs grow.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Editor](#editor)
- [Workspace](#workspace)
- [Solution Explorer](#solution-explorer)
- [Git Integration](#git-integration)
- [AIOps Features](#aiops-features)
- [Panels](#panels)
- [Built-in Browser](#built-in-browser)
- [Themes and Display](#themes-and-display)
- [Settings](#settings)
- [Security Hardening](#security-hardening)
- [Keyboard Shortcuts](#keyboard-shortcuts)
- [Language Support](#language-support)
- [Quick Open](#quick-open)
- [Vim Mode](#vim-mode)

---

## Getting Started

### Opening Files

- **File > Open** — open a single file
- **File > Open Folder** — load a folder as a workspace
- **File > Recent Files** — reopen previously opened files
- Drag and drop files or folders onto the editor window

### Creating Files

- **File > New** (`Ctrl+N`) — create a blank document
- **File > New from Template** — choose a language-specific starter template

### Saving

- **File > Save** (`Ctrl+S`) — save current file
- **File > Save As** (`Ctrl+Shift+S`) — save with a new name
- **File > Save All** — save all open files
- The title bar shows a `●` indicator when a file has unsaved changes

---

## Editor

### Core Editing

The editor provides a full-featured code editing experience:

- **Syntax highlighting** — 30+ languages supported
- **Code folding** — collapse/expand functions, classes, blocks
- **Elastic tabs** — tab stops align automatically to column content
- **Multi-caret editing** — place multiple cursors with `Alt+Click`
- **Column selection** — hold `Alt` and drag to select a rectangular region
- **Word wrap** — toggle via View > Word Wrap (`Alt+Z`)
- **Auto-indent** — language-aware indentation on Enter
- **Bracket matching** — highlights matching brackets
- **IntelliSense** — Roslyn-powered completions for C# files

### Line Numbers and Gutter

- **View > Line Number** — show absolute line numbers
- **View > Relative Line Number** — show Vim-style relative numbers
- The gutter also shows:
  - Breakpoints (click gutter margin)
  - Git change indicators (added/modified/deleted lines)
  - Fold markers

### Current Line Highlight

**View > Display > Current Line Highlight** provides four modes:

| Mode | Effect |
|------|--------|
| Off | No highlight |
| Line Number | Highlights the line number in the gutter |
| Whole Line | Highlights the full editor line |
| Number + Whole Line | Both gutter and editor line highlighted |

### Minimap

A scrollable document overview appears on the right. Toggle it via **View > Minimap** (`Ctrl+Shift+\`). Click the minimap to jump to any position in the document.

### Find and Replace

- **Edit > Find** (`Ctrl+F`) — find in current file
- **Edit > Replace** (`Ctrl+H`) — find and replace
- **Edit > Find in Files** (`Ctrl+Shift+F`) — search across workspace
- Supports regex, case-sensitive, and whole-word options

### Go To

- **Go > Go to Line** (`Ctrl+G`) — jump to a specific line
- **Go > Go to Symbol** (`Ctrl+Shift+O`) — jump to a function or class
- **Go > Go to Definition** (`F12`) — navigate to symbol definition
- **Go > Back / Forward** — navigate edit history

---

## Workspace

The Workspace panel (left sidebar) provides a file tree for the currently open folder. Open it via **Panel > Workspace** (`Ctrl+Shift+W`).

### Features

- **Filter bar** — type to search files by name
- **Refresh** — reload the file tree from disk
- **New File / New Folder** — create files or folders in-place
- **Rename** — press `F2` or right-click
- **Delete** — press `Delete` or right-click
- **Multi-select** — hold `Ctrl` or `Shift` to select multiple files
- **Drag and drop** — move files between folders

### Context Menu

Right-click any file or folder for:

- Open, Open With, Rename, Delete
- Copy path, Reveal in Explorer
- Git: Stage, Unstage, Diff

---

## Solution Explorer

The Solution Explorer (Panel > Solution Explorer) provides a Visual Studio-style view of your project structure organised by projects and solution files.

### Features

- Shows `.sln`, `.csproj`, `.vbproj`, `.fsproj`, `.pyproject`, `package.json`, `go.mod`, `Cargo.toml` and `*.tf` as project roots
- Expand project nodes to browse source, references, and assets
- **Right-click** a project for: Build, Run Tests, Add File, Properties
- **Right-click** a file for: Open, Rename, Delete, Copy Relative Path
- Tracks active document — automatically highlights the current file in the tree
- Supports rename in-place (`F2`)

### Difference from Workspace

| Feature | Workspace | Solution Explorer |
|---------|-----------|------------------|
| View type | Raw file tree | Project / solution tree |
| Filters | Name search | Project hierarchy |
| Best for | General file navigation | Structured project work |

---

## Git Integration

The Git panel provides a fully integrated source control experience.

### Opening the Git Panel

**Panel > Git** or click the branch indicator in the status bar.

### Features

- **Stage / Unstage** — individual files or all at once
- **Commit** — enter message and commit staged changes
- **Push / Pull / Fetch** — sync with remote
- **Branch management** — create, switch, delete, merge branches
- **Diff view** — side-by-side or inline diff for changed files
- **History** — browse commit log with author, date, and message
- **Blame** — see who changed each line
- **Conflict resolution** — shows conflicts inline with accept/reject controls

### Status Bar

The bottom status bar shows:

- Current branch name
- Ahead/behind commit counts
- Pending change count

---

## AIOps Features

AIOps capabilities connect your code with production telemetry, incidents, deployments, security findings, and CI/CD results.

### Opening AIOps

- **AIOps > AIOps Hub** (`Ctrl+Alt+A`) — main AIOps dashboard
- All panels and actions are accessible from the **AIOps** menu

---

### AIOps Hub — `Ctrl+Alt+A`

The hub provides a unified situational awareness view:

- **Active Incidents** — incidents linked to the current service
- **Recent Deployments** — deployment history with health status
- **Risk Score** — current deployment risk assessment
- **Security Findings** — open vulnerabilities and policy violations

Start here for a consolidated production health snapshot before making changes.

---

### Security Panel — `Ctrl+Alt+E`

**AIOps > Security Panel** scans for:

- Hardcoded secrets and credentials
- SQL injection and XSS risks
- Insecure authentication patterns
- Dependency vulnerabilities (CVE lookup)
- Overly permissive IAM policies
- Container and Dockerfile misconfigurations
- Terraform / Bicep / ARM security issues

Findings include severity, file location, and remediation suggestions — without leaving the editor.

---

### Deployment Panel — `Ctrl+Alt+D`

**AIOps > Deployment Panel** shows:

- CI/CD pipeline status (Azure DevOps, GitHub Actions, GitLab CI)
- Recent deployment history
- Rollback options (requires approval)
- Canary deployment health

Correlate a production issue directly with the deployment that introduced it.

---

### Telemetry Panel — `Ctrl+Alt+T`

**AIOps > Telemetry Panel** provides live observability data from your connected backend (Azure Monitor, Application Insights, Prometheus, etc.):

- Log search with natural language queries
- Trace viewer with span waterfall
- Metric time-series graphs
- Anomaly detection alerts

Use **Browse…** to discover service names from your connected workspace. Set a time range and click **Query** to pull live data.

---

### Insights Panel — `Ctrl+Alt+I`

**AIOps > Insights Panel** offers AI-generated:

- Root cause analysis hypotheses (confidence-scored)
- Deployment risk summaries
- Observability gap recommendations
- Code-to-production correlation

---

### AI Query — `Ctrl+Alt+Q`

**AIOps > AI Query** — ask natural language questions backed by real data:

```
Why is checkout-service failing in production?
Which deployment introduced the latency spike?
Generate OpenTelemetry instrumentation for this function.
```

Responses are evidence-based and confidence-scored. The AI cites logs, metrics, traces, and Git history — it will never guess.

---

### Incident Timeline — `Ctrl+Alt+L`

**AIOps > Incident Timeline** shows a correlated chronological timeline:

- Commits → Builds → Deployments → Metric changes → Incidents

Click any incident to request an AI-generated root cause analysis (RCA) report.

---

### PR Risk Analysis — `Ctrl+Alt+P`

**AIOps > PR Risk Analysis** evaluates pull requests for:

- Test coverage of changed code
- Blast radius (affected services)
- Historical incident patterns for changed modules
- Security findings in the diff
- Infrastructure change risk (Terraform/Bicep)

Helps prioritise careful review of high-risk PRs before merge.

---

### Service Dependencies — `Ctrl+Alt+W`

**AIOps > Service Dependencies** visualises the dependency graph between services in your system. Use it to understand blast radius — if one service fails, which downstream services are impacted?

---

### Runbooks — `Ctrl+Alt+B`

**AIOps > Runbooks** — launch and track operational runbooks directly from the editor. Production-impacting actions (rollback, scaling, firewall changes) require explicit approval before execution.

---

### Action Commands

These are one-shot commands that run against the current file or diff:

#### Scan Active File — `Ctrl+Alt+S`

Immediately scans the currently open file for security vulnerabilities and policy violations. Results appear in the Security Panel. Run this before committing any file.

#### Score Deployment Risk — `Ctrl+Alt+R`

Analyses the current git diff/staged changes and produces a deployment risk score (0–100). Factors in incident history, SLO burn, security findings, and code change volume. Provides a go/no-go signal before pushing to production.

#### Analyze Observability Gaps — `Ctrl+Alt+O`

Scans the active workspace for services that lack logging, metrics, or tracing instrumentation. Produces a report in the Insights Panel with specific recommendations per service — identifying what is blind in production.

#### Generate OTel Code — `Ctrl+Alt+G`

Generates OpenTelemetry (OTel) instrumentation boilerplate for the current file — traces, metrics counters, and log statements in the correct language (C#, Python, Go, etc.). Paste-ready code to add observability with minimal effort.

---

### AIOps Settings — `Ctrl+Alt+,`

**AIOps > AIOps Settings** configures connectors to:

- Azure Monitor / Application Insights
- Azure DevOps
- Kubernetes
- Prometheus
- PagerDuty / Opsgenie
- GitHub Actions

Supports both **Service Principal** (Client ID + Secret) and **Azure CLI** (`az login`) authentication for Azure connectors. Use **Import from AZ CLI** to auto-fill Subscription ID and Tenant ID, **Import Workspaces** to pick a Log Analytics workspace, and **Import App Insights** to select an Application Insights resource.

#### DPAPI Encryption

All API keys and tokens are encrypted using Windows DPAPI before storage. Encrypted values are tied to the current Windows user account on this machine and are stored as `EncryptedAuthKey` entries in the settings file.

Visual indicators in the settings dialog:
- `🔒 Encrypted` — value has been saved and is encrypted
- `🔒 Will encrypt on save` — you have typed a new value; it will be encrypted when you save
- `🔓 Not set` — no value configured

---

## Panels

Access panels from the **Panel** menu:

| Panel | Shortcut | Description |
|-------|----------|-------------|
| Workspace | `Ctrl+Shift+W` | File tree for open folder |
| Solution Explorer | — | Project/solution tree (Panel menu) |
| Source Control | `Ctrl+Alt+G` | Git panel |
| Source Control Window | `Ctrl+Shift+G` | Full Git window |
| Problems | `Ctrl+Alt+P` | Errors, warnings, hints |
| Terminal | `` Ctrl+` `` | Integrated terminal |
| Symbols | `Ctrl+Alt+S` | Document symbol tree |
| Markdown Preview | `Ctrl+Shift+M` | Live markdown rendering |
| Notification Center | `Ctrl+Shift+N` | Alerts and notifications |

---

## Built-in Browser

Pfpad includes a WebView2-powered browser that opens as tabs **directly in the main editor tab strip**, side-by-side with your code files.

### Opening a Browser Tab

- **File > New Browser Tab** (`Ctrl+Shift+T`) — opens a new browser tab
- Type a URL or search query in the address bar and press **Enter** or click **Go**
- Click a tab's **×** to close it; the last file tab is always kept

### Toolbar Controls

| Control | Action |
|---|---|
| ← Back | Navigate back (Alt+Left) |
| → Forward | Navigate forward (Alt+Right) |
| ↺ Refresh | Reload page (F5) |
| Address bar | Type URL or search term; **Enter** navigates |
| Go | Navigate to the typed URL |
| 🛠 DevTools | Open Chromium Developer Tools (F12) |
| ⧉ | Open current page in system browser |

The address bar uses a **rounded Edge-style design** that highlights with an accent border on focus.

### Favorites Bar

When enabled in **Settings > Features > Browser > Show Favorites Bar**, the bar below the toolbar displays your Edge bookmarks:

- Items that fit appear as inline buttons
- Overflow items appear in a **»** dropdown menu (including folder submenus)
- The source file (`Bookmarks`) is read from the Edge profile you configure in Settings

### Content Filtering

When **Settings > Features > Browser > Content Filtering** is enabled, the browser blocks requests to known ad and tracker domains using:

- **EasyList** — ads and banners
- **EasyPrivacy** — tracking scripts
- **Peter Lowe's list** — ad servers

Lists are fetched and cached on first use. Blocking happens via `NavigationStarting` interception — no extension required.

### Browser Settings

All browser settings live under **Settings > Features > Browser**:

| Setting | Description |
|---|---|
| Home page | URL loaded when opening a new browser tab |
| Max history entries | Cap on navigation history saved per tab |
| Ephemeral storage | Discard cookies, cache, and storage after each session |
| Allow localhost links | Enable `localhost`, `127.0.0.1`, `[::1]` navigation |
| Default zoom | Default zoom level (%) for all pages |
| Show in title bar | Mirror page title in the main window title bar |
| Content filtering | Enable/disable blocklist-based ad blocking |
| Custom user agent | Override the browser UA string sent to sites |
| Show favorites bar | Show/hide the Edge bookmarks bar |
| Favorites source | Path to the Edge `Bookmarks` JSON file |

### Privacy Notes

- By default the browser uses a **persistent profile** in `%AppData%\MyCrownJewelApp\WebView2`.
- Enable **Ephemeral storage** in Settings for untrusted workspaces — no cookies or cache survive the session.
- The custom user agent prevents sites from fingerprinting the browser as Microsoft Edge.

---

## Themes and Display

### Changing Theme

**View > Theme** or the theme picker in the status bar. pfpad includes 23 built-in themes:

- **Dark themes**: Dark, One Dark, Dracula, Nord, Tokyo Night, Catppuccin Mocha, Gruvbox Dark, Material Dark, Solarized Dark, Monokai
- **Light themes**: Light, GitHub Light, Solarized Light, Catppuccin Latte
- **High contrast**: High Contrast Dark, High Contrast Light

All UI elements — menus, panels, dialogs, gutter, minimap, status bar — respond to the active theme in real time.

### Display Settings

**View > Display** contains:

- Word Wrap
- Current Line Highlight (Off / Line Number / Whole Line / Number + Whole Line)
- Whitespace characters
- Indentation guides
- Column ruler

---

## Settings

Open with **Tools > Settings** (`Ctrl+,`).

Settings are organized by category:

- **Editor** — font, size, tab width, insert spaces, word wrap, line endings, encoding
- **Appearance** — theme, minimap, status bar, gutter
- **Cursor** — line highlight mode, hover highlight, blink rate
- **Git** — user name, email, default branch
- **Security** — runtime hardening profile (Not Hardened / Low / Mid / Max), workspace trust, URL validation, SAST scanning, logging
- **AIOps** — connector endpoints and credentials
- **Extensions** — external tools and plugins
- **Keybindings** — view and customize keyboard shortcuts

Settings are saved per-user and persisted across sessions.

---

## Security Hardening

pfpad includes a **built-in, graded runtime security hardening system** accessible from
**Settings → Security → Security Profile**.

### Profiles

| Profile | Purpose |
|---------|---------|
| **Not Hardened** | No enforcement — local development / testing only |
| **Low** *(default)* | URL scheme allowlist, TLS preference, safe process launch |
| **Mid** | Low + DPAPI-encrypted settings.json, SDK credential flows, log masking |
| **Max** | Mid + HTTPS-only AIOps endpoints, `http://` links blocked |

### Transition Wizard

Changing the profile opens a guided **transition wizard** that:

- Lists every migration step with a plain-English description
- Shows live status icons (⬜ pending → ⏳ running → ✅ done / ⚠️ warning / ❌ failed)
- Creates backups before any file mutation (`settings.json.bak`, `settings.json.enc.bak`)
- Rolls back all completed steps automatically if a step fails
- Reverts the profile selector if you cancel

### Key user-visible behaviours by profile

| Behaviour | Not Hardened | Low | Mid | Max |
|-----------|:-----------:|:---:|:---:|:---:|
| `javascript:` / `vbscript:` links blocked | ✗ | ✅ | ✅ | ✅ |
| `http://` links blocked | ✗ | ✗ | ✗ | ✅ |
| `settings.json` DPAPI-encrypted | ✗ | ✗ | ✅ | ✅ |
| Status-bar feedback on blocked links | ✗ | ✅ | ✅ | ✅ |
| Blocked link feedback in status bar | *silent* | ✅ | ✅ | ✅ |

### Settings encryption (Mid+)

When you upgrade to Mid, `settings.json` is encrypted with **Windows DPAPI** (tied to your
user account on this machine). Use **About → Open settings.json** to export a readable copy
for inspection if needed. Downgrading back to Low decrypts the file automatically.

For full details, see the **🔒 Security Hardening** tab in **Help > Manual**.

---

## Keyboard Shortcuts

### File Operations

| Action | Shortcut |
|--------|----------|
| New tab | `Ctrl+T` |
| Open file | `Ctrl+O` |
| Open folder | `Ctrl+Alt+O` |
| Open solution | `Ctrl+Shift+O` |
| Save | `Ctrl+S` |
| Save As | `Ctrl+Shift+S` |
| Save All | `Ctrl+Alt+S` |
| Close tab | `Ctrl+W` |
| Close split | `Ctrl+Shift+W` |
| Close all tabs | `Ctrl+Alt+W` |

### Editing

| Action | Shortcut |
|--------|----------|
| Undo | `Ctrl+Z` |
| Cut | `Ctrl+X` |
| Copy | `Ctrl+C` |
| Paste | `Ctrl+V` |
| Select All | `Ctrl+A` |
| Delete | `Delete` |
| Format document | `Alt+Shift+F` |
| Toggle fold | `Ctrl+Shift+[` |
| Toggle all folds | `Ctrl+Alt+[` |
| Rename symbol | `F2` |

### Navigation

| Action | Shortcut |
|--------|----------|
| Go to line/column | `Ctrl+G` |
| Go to file (quick open) | `Ctrl+P` |
| Go to symbol in editor | `Ctrl+.` |
| Go to symbol in workspace | `Ctrl+Shift+O` |
| Go to definition | `F12` |
| Go to declaration | `Ctrl+F12` |
| Go to references | `Shift+F12` |
| Navigate back | `Alt+Left` |
| Navigate forward | `Alt+Right` |
| Last edit location | `Ctrl+-` |
| Go to bracket | `Ctrl+Shift+Backspace` |
| Toggle header/source (C/C++) | `Alt+O` |

### Search

| Action | Shortcut |
|--------|----------|
| Find | `Ctrl+F` |
| Replace | `Ctrl+H` |
| Find in files | `Ctrl+Shift+F` |
| Find next | `F3` |
| Find previous | `Shift+F3` |

### View and Panels

| Action | Shortcut |
|--------|----------|
| Toggle Workspace panel | `Ctrl+Shift+W` |
| Toggle Source Control panel | `Ctrl+Alt+G` |
| Open Source Control window | `Ctrl+Shift+G` |
| Toggle Symbols panel | `Ctrl+Alt+S` |
| Toggle Problems panel | `Ctrl+Alt+P` |
| Toggle Terminal | `` Ctrl+` `` |
| Toggle Markdown Preview | `Ctrl+Shift+M` |
| Toggle Notification Center | `Ctrl+Shift+N` |
| Zoom in | `Ctrl++` |
| Zoom out | `Ctrl+-` |
| Restore default zoom | `Ctrl+0` |
| Split vertical | `Ctrl+Shift+V` |
| Split horizontal | `Ctrl+Alt+H` |
| Close split | `Ctrl+Shift+W` |
| Font picker | `Ctrl+Shift+F` |
| Toggle Vim mode | `Ctrl+Alt+V` |

### Run and Debug

| Action | Shortcut |
|--------|----------|
| Start debugging | `F5` |
| Run without debugging | `Ctrl+F5` |
| Stop debugging | `Shift+F5` |
| Restart debugging | `Ctrl+Shift+F5` |
| Step over | `F10` |
| Step into | `F11` |
| Step out | `Shift+F11` |
| Toggle breakpoint | `F9` |
| New breakpoint | `Ctrl+B` |
| Run tests | `Ctrl+Alt+F5` |
| Rerun failed tests | `Ctrl+Alt+F6` |

### AIOps

| Action | Shortcut |
|--------|----------|
| AIOps Hub | `Ctrl+Alt+A` |
| Security Panel | `Ctrl+Alt+E` |
| Deployment Panel | `Ctrl+Alt+D` |
| Telemetry Panel | `Ctrl+Alt+T` |
| Insights Panel | `Ctrl+Alt+I` |
| AI Query | `Ctrl+Alt+Q` |
| Incident Timeline | `Ctrl+Alt+L` |
| PR Risk Analysis | `Ctrl+Alt+P` |
| Service Dependencies | `Ctrl+Alt+W` |
| Runbooks | `Ctrl+Alt+B` |
| Scan Active File | `Ctrl+Alt+S` |
| Score Deployment Risk | `Ctrl+Alt+R` |
| Analyze Observability Gaps | `Ctrl+Alt+O` |
| Generate OTel Code | `Ctrl+Alt+G` |
| AIOps Settings | `Ctrl+Alt+,` |

---

## Language Support

pfpad provides syntax highlighting, code folding, and file associations for:

| Category | Languages |
|----------|-----------|
| **General purpose** | C#, C, C++, Java, Python, Go, Ruby, Rust, Swift |
| **Web** | JavaScript, TypeScript, HTML, CSS, SCSS, Less |
| **Data** | JSON, YAML, XML, TOML, CSV |
| **Scripting** | PowerShell, Bash, Batch, Lua |
| **IaC / DevOps** | Terraform (HCL), Bicep, ARM templates, Kubernetes YAML, Helm, Dockerfile |
| **CI/CD** | GitHub Actions, Azure Pipelines, GitLab CI |
| **Query** | SQL, GraphQL |
| **Docs** | Markdown (with live preview), reStructuredText |

### C# IntelliSense

For C# files, pfpad uses Roslyn to provide:

- Type-aware code completion
- Inline diagnostics (errors and warnings)
- Go to definition / find references
- Semantic syntax highlighting
- Code actions and quick fixes

---

## Quick Open

Press `Ctrl+P` to open Quick Open. Type to search and jump to any file in the workspace by name.

---

## Vim Mode

Enable Vim mode via **View > Vim Mode** or `Ctrl+Alt+V`.

### Supported Features

- **Normal mode**: navigation (`h`, `j`, `k`, `l`, `w`, `b`, `e`, `0`, `$`), delete (`d`), yank (`y`), paste (`p`), undo (`u`)
- **Insert mode**: enter with `i`, `a`, `o`, `O`, exit with `Esc`
- **Visual mode**: character (`v`), line (`V`), block (`Ctrl+V`)
- **Command mode**: `:w`, `:q`, `:wq`, `:e`, `:set`, `:nohl`
- **Macros**: record with `q{register}`, replay with `@{register}`
- **Marks**: set with `m{letter}`, jump with `` `{letter} ``
- **Search**: `/pattern`, `n`/`N` to navigate
- **Relative line numbers**: `:set relativenumber` / `:set norelativenumber`

### .vimrc Support

pfpad reads `~/.vimrc` on startup. Supported settings:

```
set number
set relativenumber
set tabstop=4
set expandtab
set wrap
set ignorecase
set smartcase
```

---

## Installer and Data Locations

### Per-User Install (Recommended)

When installed for the current user only:

- **Application**: `%LOCALAPPDATA%\Programs\Personal Flip Pad\`
- **Settings**: `%APPDATA%\Personal Flip Pad\settings.json`
- **Profiles**: `%APPDATA%\Personal Flip Pad\profiles\`
- **AIOps config**: `%APPDATA%\Personal Flip Pad\aiops-settings.json`

### Per-Machine Install

- **Application**: `%ProgramFiles%\Personal Flip Pad\`
- **Settings**: `%APPDATA%\Personal Flip Pad\settings.json` (per-user still)

### Uninstall

Use **Add or Remove Programs** in Windows Settings, or run the uninstaller from the Start menu shortcut group.

---

## Troubleshooting

### Editor is blank after opening a file

- Check that the file is not empty
- Try **View > Reset Layout**
- Check **Problems** panel for load errors

### AIOps panel shows "not configured"

- Open **AIOps > AIOps Settings**
- Enter the endpoint URL and API key for each connector
- Click **Save** — keys are encrypted with DPAPI automatically

### Git panel shows "not a repository"

- Open a folder that contains a `.git` directory via **File > Open Folder**
- Or initialize a repository with **Git > Initialize Repository**

### Syntax highlighting not working

- Check **View > Syntax Highlighting** is enabled
- Save the file with the correct extension so the language is detected
- Use the language selector in the status bar to manually override

### IntelliSense not available for C#

- Open the folder containing the `.csproj` file as the workspace root
- Wait a few seconds for Roslyn to initialize (progress shown in status bar)
- Check that .NET SDK is installed: `dotnet --version` in the terminal

---

*For issues or contributions, visit the project repository.*

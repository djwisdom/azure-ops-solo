using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed partial class AboutDialog : Form
{
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    private readonly Theme _theme;
    private readonly float _scale;
    private readonly int _dialogWidth;
    private readonly int _dialogHeight;
    private readonly int _titleBarHeight;
    private readonly int _footerHeight;
    private readonly int _margin;
    private readonly int _sectionGap;
    private readonly int _rowGap;
    private readonly int _contentWidth;
    private readonly int _openFileCount;
    private readonly string? _workspaceRoot;
    private readonly string _version;
    private readonly string _description;
    private readonly string _copyright;
    private readonly string _commit;
    private readonly string _buildDate;
    private readonly string _clrVersion;
    private readonly string _arch;
    private readonly string _osArch;
    private readonly string _osDesc;
    private readonly string _runtimeLabel;
    private readonly string _buildFlavor;
    private readonly int _processorCount;
    private readonly long _totalRam;
    private readonly long _workingSet;
    private readonly long _managedHeap;
    private readonly string _uptimeText;
    private readonly string _sessionStats;
    private readonly string _themeName;

    private Panel _titleBar = null!;
    private Button _closeButton = null!;
    private Panel _scrollPanel = null!;
    private Panel _innerContent = null!;
    private Panel _pageHost = null!;
    private readonly List<Button> _tabButtons = new();
    private readonly Dictionary<Button, Panel> _tabPages = new();
    private readonly Dictionary<Button, Panel> _tabUnderlines = new();
    private Label? _securityProfileLabel;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialog"/> class.
    /// </summary>
    public AboutDialog(int openFileCount = 0, string? workspaceRoot = null)
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        _scale = Math.Max(1f, graphics.DpiX / 96f);
        _theme = ThemeManager.Instance.CurrentTheme;
        _openFileCount = Math.Max(0, openFileCount);
        _workspaceRoot = workspaceRoot;

        var asm = typeof(AboutDialog).Assembly;
        var verAttrs = asm.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
        _version = verAttrs.Length > 0
            ? ((AssemblyFileVersionAttribute)verAttrs[0]).Version
            : asm.GetName().Version?.ToString() ?? "0.0.0.0";
        _description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;
        _copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        _commit = GetMetadata("CommitHash", "unknown");
        _buildDate = GetMetadata("BuildDate", "unknown");
        _clrVersion = Environment.Version.ToString();
        _arch = RuntimeInformation.ProcessArchitecture.ToString();
        _osArch = RuntimeInformation.OSArchitecture.ToString();
        _osDesc = string.IsNullOrWhiteSpace(RuntimeInformation.OSDescription)
            ? Environment.OSVersion.VersionString
            : RuntimeInformation.OSDescription.Trim();
        _runtimeLabel = $".NET {Environment.Version.Major} (net{Environment.Version.Major}.0-windows) • {_arch}";
        _processorCount = Environment.ProcessorCount;
        _totalRam = Math.Max(0L, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
        _managedHeap = Math.Max(0L, GC.GetTotalMemory(false));
        using (var process = Process.GetCurrentProcess())
        {
            _workingSet = Math.Max(0L, process.WorkingSet64);
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            _uptimeText = uptime.TotalHours >= 1
                ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
                : $"{uptime.Minutes}m {uptime.Seconds}s";
        }
        _themeName = _theme.Name;
        _sessionStats = BuildSessionStats(_openFileCount, _workspaceRoot);

#if DEBUG
        _buildFlavor = "Debug";
#else
        _buildFlavor = "Release";
#endif

        _dialogWidth = Scale(920);
        _dialogHeight = Scale(700);
        _titleBarHeight = Scale(44);
        _footerHeight = Scale(60);
        _margin = Scale(24);
        _sectionGap = Scale(16);
        _rowGap = Scale(10);
        _contentWidth = _dialogWidth - (_margin * 2) - Scale(8);

        BackColor = _theme.Background;
        ForeColor = _theme.Text;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;
        DoubleBuffered = true;
        KeyPreview = true;
        Size = new Size(_dialogWidth, _dialogHeight);
        MinimumSize = Size;
        MaximumSize = Size;

        BuildLayout();
        PositionCloseButton();

        Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };
    }

    private void BuildLayout()
    {
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = _titleBarHeight,
            BackColor = _theme.PanelBackground,
            Cursor = Cursors.SizeAll
        };
        AttachDrag(_titleBar);

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "About Personal Flip Pad",
            Font = new Font("Segoe UI", Scale(13), FontStyle.Bold),
            ForeColor = _theme.Text,
            BackColor = Color.Transparent,
            Location = new Point(_margin, Scale(10))
        };
        AttachDrag(titleLabel);
        _titleBar.Controls.Add(titleLabel);

        _closeButton = CreateFlatButton("×", Scale(36), Scale(32));
        _closeButton.Font = new Font("Segoe UI", Scale(14), FontStyle.Regular);
        _closeButton.ForeColor = _theme.Muted;
        _closeButton.BackColor = Color.Transparent;
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.FlatAppearance.MouseOverBackColor = _theme.Accent;
        _closeButton.Click += (_, _) => Close();
        _titleBar.Resize += (_, _) => PositionCloseButton();

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = _footerHeight,
            BackColor = _theme.Background,
            Padding = new Padding(_margin / 2, Scale(8), _margin / 2, Scale(12))
        };

        var okButton = CreateFlatButton("OK", Scale(96), Scale(34));
        okButton.Anchor = AnchorStyles.None;
        okButton.Click += (_, _) => Close();
        AcceptButton = okButton;
        CancelButton = okButton;

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        footerLayout.Controls.Add(okButton, 1, 0);
        footer.Controls.Add(footerLayout);

        _scrollPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
            BackColor = _theme.Background
        };

        _innerContent = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = _theme.Background,
            Location = new Point(_margin, _margin),
            MinimumSize = new Size(_contentWidth, 0),
            MaximumSize = new Size(_contentWidth, 0)
        };

        var tabStrip = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Location = new Point(0, 0),
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _pageHost = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = _theme.Background,
            Width = _contentWidth,
            Location = new Point(0, Scale(52))
        };

        var aboutPage = CreateAboutPage();
        var featuresPage = CreateFeaturesPage();
        var systemPage = CreateSystemPage();
        var creditsPage = CreateCreditsPage();

        var aboutButton = CreateTabButton("About", aboutPage);
        var featuresButton = CreateTabButton("Features", featuresPage);
        var systemButton = CreateTabButton("System", systemPage);
        var creditsButton = CreateTabButton("Credits", creditsPage);

        tabStrip.Controls.Add(aboutButton);
        tabStrip.Controls.Add(featuresButton);
        tabStrip.Controls.Add(systemButton);
        tabStrip.Controls.Add(creditsButton);

        _innerContent.Controls.Add(tabStrip);
        _innerContent.Controls.Add(_pageHost);
        _scrollPanel.Controls.Add(_innerContent);

        Controls.Add(_scrollPanel);
        Controls.Add(footer);
        Controls.Add(_titleBar);
        _titleBar.Controls.Add(_closeButton);

        SelectTab(aboutButton);
    }

    private Panel CreateAboutPage()
    {
        var page = CreatePagePanel();
        var layout = CreatePageLayout();

        // Compute column widths explicitly — Percent columns in AutoSize TLPs have no
        // defined denominator and produce incorrect offsets. Use Absolute instead.
        int gap = Scale(20);
        int leftColW = (int)Math.Round(_contentWidth * 0.58);
        int rightColW = _contentWidth - leftColW - gap;

        var header = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Width = _contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, leftColW));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightColW + gap));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Left column — fixed width, height auto-grows
        var leftColumn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = leftColW - Scale(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, Scale(12), 0)
        };

        var identityRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };

        var appIcon = new PictureBox
        {
            Image = AppIconFactory.CreateBitmap(Scale(40)),
            SizeMode = PictureBoxSizeMode.Normal,
            Size = new Size(Scale(40), Scale(40)),
            BackColor = Color.Transparent,
            Margin = new Padding(0, Scale(2), Scale(12), 0)
        };

        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        titleStack.Controls.Add(CreateLabel("Personal Flip Pad", "Segoe UI", Scale(18), FontStyle.Bold, _theme.Text));
        titleStack.Controls.Add(CreateLabel($"v{_version}", "Segoe UI", Scale(11), FontStyle.Bold, _theme.Text));

        var versionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        versionRow.Controls.Add(CreateLabel($"{_buildFlavor} build", "Segoe UI", Scale(9), FontStyle.Regular, _theme.Muted, new Padding(0, Scale(1), 0, 0)));
        titleStack.Controls.Add(versionRow);

        identityRow.Controls.Add(appIcon);
        identityRow.Controls.Add(titleStack);
        leftColumn.Controls.Add(identityRow);
        leftColumn.Controls.Add(CreateLabel(_runtimeLabel, "Segoe UI", Scale(10), FontStyle.Bold, _theme.Accent));
        leftColumn.Controls.Add(CreateLabel($"Commit: {_commit}", "Segoe UI", Scale(10), FontStyle.Regular, _theme.Muted));
        leftColumn.Controls.Add(CreateLabel($"Build: {_buildDate}", "Segoe UI", Scale(10), FontStyle.Regular, _theme.Muted));
        if (!string.IsNullOrWhiteSpace(_description))
            leftColumn.Controls.Add(CreateWrappingLabel(_description, leftColW - Scale(12), _theme.Muted));
        if (!string.IsNullOrWhiteSpace(_copyright))
            leftColumn.Controls.Add(CreateWrappingLabel(_copyright, leftColW - Scale(12), _theme.Disabled));

        // Right column — fixed width, height auto-grows, gap provided by left padding
        var rightColumn = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = rightColW,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(gap, 0, 0, 0)
        };
        rightColumn.Controls.Add(CreateLabel("Session", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text));
        rightColumn.Controls.Add(CreateWrappingLabel(_sessionStats, rightColW - Scale(4), _theme.Muted));
        rightColumn.Controls.Add(CreateLabel($"Theme: {_themeName}", "Segoe UI", Scale(10), FontStyle.Regular, _theme.Muted, new Padding(0, _rowGap, 0, 0)));
        rightColumn.Controls.Add(CreateLabel($"Build flavor: {_buildFlavor}", "Segoe UI", Scale(10), FontStyle.Regular, _theme.Muted));
        rightColumn.Controls.Add(CreateLabel("Press Esc to close", "Segoe UI", Scale(10), FontStyle.Regular, _theme.Disabled, new Padding(0, _rowGap, 0, 0)));

        header.Controls.Add(leftColumn, 0, 0);
        header.Controls.Add(rightColumn, 1, 0);

        AddPageRow(layout, header, _sectionGap);
        AddPageRow(layout, CreateSeparator(), _sectionGap);
        AddPageRow(layout, CreateLabel("Quick links", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text), _rowGap);

        var quickLinks = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Width = _contentWidth
        };
        quickLinks.Controls.Add(CreateActionButton("Copy Info", (_, _) => CopyInfoToClipboard()));
        quickLinks.Controls.Add(CreateActionButton("Open Log Folder", (_, _) => OpenLogFolder()));
        quickLinks.Controls.Add(CreateActionButton("Open settings.json", (_, _) => OpenSettingsFile()));
        AddPageRow(layout, quickLinks, _rowGap);

        AddPageRow(layout, CreateWrappingLabel("Use this dialog to confirm build details, inspect session context, and jump to local support files without leaving the editor.", _contentWidth, _theme.Disabled));
        page.Controls.Add(layout);
        return page;
    }

    private Panel CreateFeaturesPage()
    {
        var page = CreatePagePanel();
        var layout = CreatePageLayout();

        AddPageRow(layout, CreateLabel("Features & dependencies", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text), _rowGap);
        AddPageRow(layout, CreateWrappingLabel("Core editor capabilities, modernization work, and runtime package inventory currently visible in this process.", _contentWidth, _theme.Muted), _rowGap);

        var knownPackagePrefixes = new[]
        {
            "LibGit2Sharp",
            "Microsoft.CodeAnalysis",
            "TreeSitter",
            "System.IO.Hashing"
        };

        var knownPackages = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly =>
            {
                try
                {
                    var name = assembly.GetName();
                    return (Name: name.Name, Version: name.Version?.ToString() ?? string.Empty);
                }
                catch
                {
                    return (Name: string.Empty, Version: string.Empty);
                }
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && knownPackagePrefixes.Any(prefix => item.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Name)
            .ToList();

        var extraPackages = new[]
        {
            (Name: "System.IO.Hashing", Version: "BCL"),
            (Name: "LibGit2Sharp", Version: "runtime"),
            (Name: "TreeSitter.DotNet", Version: "runtime")
        };

        var displayed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var featuresText = new StringBuilder();
        featuresText.AppendLine("Features:");
        featuresText.AppendLine("• Flat-themed code editor with syntax highlighting, minimap, and custom WinForms chrome");
        featuresText.AppendLine("• Roslyn-powered C# analysis with semantic highlighting and background workspace loading");
        featuresText.AppendLine("• Git integration for status, diff, history, blame, and branch workflows");
        featuresText.AppendLine("• Debug Adapter Protocol integration with breakpoint persistence and session controls");
        featuresText.AppendLine("• Vim mode, snippets, command palette, folding, bookmarks, and multi-tab session restore");
        featuresText.AppendLine("• Large-file degradation safeguards to keep the editor responsive");
        featuresText.AppendLine();
        featuresText.AppendLine("Built-in browser (v1.0.36):");
        featuresText.AppendLine("• WebView2-powered browser tabs in the main editor tab strip — side-by-side with file tabs");
        featuresText.AppendLine("• Multiple independent browser tabs (Ctrl+Shift+T); each with full navigation history");
        featuresText.AppendLine("• Edge-style rounded address bar with focus accent, owner-drawn in theme colours");
        featuresText.AppendLine("• Large distinct nav buttons (Back, Forward, Refresh) matching Edge sizing");
        featuresText.AppendLine("• Go button + Developer Tools (F12) with Segoe MDL2 Assets glyphs");
        featuresText.AppendLine("• Favorites bar imported from Edge with overflow '»' dropdown for off-screen items");
        featuresText.AppendLine("• Enterprise ad/tracker blocking via EasyList, EasyPrivacy, Peter Lowe blocklists");
        featuresText.AppendLine("• Configurable user agent to prevent Edge fingerprinting");
        featuresText.AppendLine("• Ephemeral profile mode for untrusted workspaces (no cookies/cache persisted)");
        featuresText.AppendLine("• Per-session settings: homepage, max history, localhost links, default zoom, title bar display");
        featuresText.AppendLine();
        featuresText.AppendLine("Security hardening:");
        featuresText.AppendLine("• Graded security profiles: Not Hardened → Low → Mid → Max");
        featuresText.AppendLine("• Guided transition wizard with per-step live status, rollback on failure, and auto-backups");
        featuresText.AppendLine("• DPAPI encryption for settings.json at Mid+ (tied to Windows user account)");
        featuresText.AppendLine("• URL scheme allowlist blocks javascript:, vbscript:, ms-msdt: at Low+");
        featuresText.AppendLine("• Status-bar feedback on blocked links — no silent failures");
        featuresText.AppendLine("• Safe process launch via ArgumentList — eliminates shell injection in AIOps CLI calls");
        featuresText.AppendLine("• Build-time status indicators: code signing, CI gates, installer signing");
        featuresText.AppendLine();
        featuresText.AppendLine(".NET modernization:");
        featuresText.AppendLine($"• Target framework: net{Environment.Version.Major}.0-windows  •  CLR {_clrVersion}");
        featuresText.AppendLine("• Source-generated P/Invoke via LibraryImport for native interop paths");
        featuresText.AppendLine("• Service-backed startup infrastructure with persisted session and profile data");
        featuresText.AppendLine("• Theme-aware shell, diagnostics, notifications, and workspace tooling");
        featuresText.AppendLine();
        featuresText.AppendLine("NuGet dependencies:");

        foreach (var package in knownPackages)
        {
            if (package.Name == null || !displayed.Add(package.Name))
                continue;

            featuresText.AppendLine($"  {package.Name}  {package.Version}");
        }

        foreach (var (name, version) in extraPackages)
        {
            if (!displayed.Add(name))
                continue;

            featuresText.AppendLine($"  {name}  {version}");
        }

        featuresText.AppendLine($"  .NET Runtime  {_clrVersion}  ({_arch})  — net{Environment.Version.Major}.0-windows");

        var featuresBox = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            Font = new Font("Consolas", Scale(9), FontStyle.Regular),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            WordWrap = true,
            Width = _contentWidth,
            Height = Scale(430),
            Margin = Padding.Empty,
            Text = featuresText.ToString()
        };

        AddPageRow(layout, featuresBox);
        page.Controls.Add(layout);
        return page;
    }

    private Panel CreateSystemPage()
    {
        var page = CreatePagePanel();
        var layout = CreatePageLayout();

        AddPageRow(layout, CreateLabel("System", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text), _rowGap);
        AddPageRow(layout, CreateWrappingLabel("Live runtime and process information for the current Personal Flip Pad session.", _contentWidth, _theme.Muted), _sectionGap);

        int labelColW = (int)Math.Round(_contentWidth * 0.30);
        int valueColW = _contentWidth - labelColW;

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Width = _contentWidth,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelColW));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, valueColW));

        AddInfoRow(grid, "OS", _osDesc + $" ({_osArch})");
        AddInfoRow(grid, ".NET", _runtimeLabel + $"  •  CLR {_clrVersion}");
        AddInfoRow(grid, "CPU", $"{_processorCount} logical processors");
        AddInfoRow(grid, "RAM", FormatBytes(_totalRam));
        AddInfoRow(grid, "Working set", $"{ToMegabytes(_workingSet)} MB");
        AddInfoRow(grid, "Managed heap", $"{ToMegabytes(_managedHeap)} MB");
        AddInfoRow(grid, "Uptime", _uptimeText);
        AddInfoRow(grid, "Theme", _themeName);
        AddInfoRow(grid, "Open files", _openFileCount.ToString());
        AddInfoRow(grid, "Workspace", string.IsNullOrWhiteSpace(_workspaceRoot) ? "Not set" : _workspaceRoot!);

        // Build security profile row inline so we can keep a live reference to the value label.
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int secRowIndex = grid.RowCount++;
        var secKey = CreateLabel("Security profile", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text);
        secKey.Margin = new Padding(0, 0, Scale(12), _rowGap);
        _securityProfileLabel = CreateWrappingLabel(SecurityEnforcementService.CurrentProfile.ToString(), valueColW - Scale(4), _theme.Muted);
        _securityProfileLabel.Margin = new Padding(0, 0, 0, _rowGap);
        grid.Controls.Add(secKey, 0, secRowIndex);
        grid.Controls.Add(_securityProfileLabel, 1, secRowIndex);

        AddInfoRow(grid, "Commit", _commit);
        AddInfoRow(grid, "Build date", _buildDate);

        AddPageRow(layout, grid);
        page.Controls.Add(layout);
        return page;
    }

    private Panel CreateCreditsPage()
    {
        var page = CreatePagePanel();
        var layout = CreatePageLayout();

        AddPageRow(layout, CreateLabel("Credits", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text), _rowGap);
        if (!string.IsNullOrWhiteSpace(_copyright))
            AddPageRow(layout, CreateWrappingLabel(_copyright, _contentWidth, _theme.Muted), _rowGap);

        AddPageRow(layout, CreateWrappingLabel("Personal Flip Pad is built as a WinForms editor shell with .NET, custom controls, and theme-aware UI infrastructure.", _contentWidth, _theme.Muted), _sectionGap);
        AddPageRow(layout, CreateLabel("Third-party acknowledgements", "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text), _rowGap);
        AddPageRow(layout, CreateWrappingLabel("• Microsoft .NET / WinForms for the application platform\r\n• Roslyn (Microsoft.CodeAnalysis) for language services\r\n• Microsoft WebView2 for the integrated browser\r\n• LibGit2Sharp for Git operations\r\n• TreeSitter.DotNet for optional parsing support\r\n• System.IO.Hashing for hashing utilities\r\n• Windows DPAPI (System.Security.Cryptography) for at-rest settings encryption\r\n• EasyList, EasyPrivacy, Peter Lowe blocklists for browser content filtering\r\n• App icon generated at runtime via GDI+ (AppIconFactory)", _contentWidth, _theme.Muted), _rowGap);
        AddPageRow(layout, CreateWrappingLabel("Thanks to the broader OSS ecosystem that makes desktop developer tooling possible.", _contentWidth, _theme.Disabled));

        page.Controls.Add(layout);
        return page;
    }

    private Button CreateTabButton(string text, Panel page)
    {
        var button = CreateFlatButton(text, Scale(120), Scale(40));
        button.Margin = new Padding(0, 0, Scale(8), 0);
        button.BackColor = _theme.Background;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = _theme.Muted;
        button.TextAlign = ContentAlignment.MiddleCenter;

        var underline = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = Scale(2),
            BackColor = _theme.Accent,
            Visible = false
        };
        button.Controls.Add(underline);

        button.Click += (_, _) => SelectTab(button);
        _tabButtons.Add(button);
        _tabPages[button] = page;
        _tabUnderlines[button] = underline;
        return button;
    }

    private void SelectTab(Button selectedButton)
    {
        foreach (var button in _tabButtons)
        {
            bool isSelected = ReferenceEquals(button, selectedButton);
            button.BackColor = isSelected ? _theme.PanelBackground : _theme.Background;
            button.ForeColor = isSelected ? _theme.Text : _theme.Muted;
            if (_tabUnderlines.TryGetValue(button, out var underline))
                underline.Visible = isSelected;
        }

        // Refresh live fields before displaying the selected page.
        if (_securityProfileLabel != null)
            _securityProfileLabel.Text = SecurityEnforcementService.CurrentProfile.ToString();

        _pageHost.SuspendLayout();
        _pageHost.Controls.Clear();
        if (_tabPages.TryGetValue(selectedButton, out var page))
        {
            page.Visible = true;
            page.Location = new Point(0, 0);
            _pageHost.Controls.Add(page);
        }
        _pageHost.ResumeLayout(true);
    }

    private Panel CreatePagePanel()
    {
        return new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = _theme.Background,
            Width = _contentWidth,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private TableLayoutPanel CreatePageLayout()
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Width = _contentWidth,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _contentWidth));
        return layout;
    }

    private FlowLayoutPanel CreateVerticalFlow()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
    }

    private void AddPageRow(TableLayoutPanel layout, Control control, int bottomMargin = 0)
    {
        control.Margin = new Padding(0, 0, 0, bottomMargin);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(control, 0, layout.RowCount);
        layout.RowCount++;
    }

    private void AddInfoRow(TableLayoutPanel grid, string label, string value)
    {
        // Derive value column width from the grid's second column (Absolute style)
        int valueColW = grid.ColumnStyles.Count >= 2 && grid.ColumnStyles[1].SizeType == SizeType.Absolute
            ? (int)grid.ColumnStyles[1].Width
            : Math.Max(Scale(420), (int)Math.Round(_contentWidth * 0.62));

        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var rowIndex = grid.RowCount;
        grid.RowCount++;

        var keyLabel = CreateLabel(label, "Segoe UI", Scale(10), FontStyle.Bold, _theme.Text);
        keyLabel.Margin = new Padding(0, 0, Scale(12), _rowGap);
        var valueLabel = CreateWrappingLabel(value, valueColW - Scale(4), _theme.Muted);
        valueLabel.Margin = new Padding(0, 0, 0, _rowGap);

        grid.Controls.Add(keyLabel, 0, rowIndex);
        grid.Controls.Add(valueLabel, 1, rowIndex);
    }

    private Label CreateLabel(string text, string fontFamily, int size, FontStyle style, Color color, Padding? margin = null)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = new Font(fontFamily, size, style),
            ForeColor = color,
            BackColor = Color.Transparent,
            Margin = margin ?? Padding.Empty
        };
    }

    private Label CreateWrappingLabel(string text, int maxWidth, Color color)
    {
        return new Label
        {
            AutoSize = true,
            MaximumSize = new Size(maxWidth, 0),
            Text = text,
            Font = new Font("Segoe UI", Scale(10), FontStyle.Regular),
            ForeColor = color,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
    }

    private Button CreateFlatButton(string text, int width, int height)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(width, height),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", Scale(10), FontStyle.Regular),
            BackColor = _theme.PanelBackground,
            ForeColor = _theme.Text,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Margin = Padding.Empty,
            TabStop = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = _theme.Border;
        button.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        button.FlatAppearance.MouseDownBackColor = _theme.ButtonHoverBackground;
        return button;
    }

    private Button CreateActionButton(string text, EventHandler onClick)
    {
        var button = CreateFlatButton(text, Scale(150), Scale(32));
        button.Margin = new Padding(0, 0, Scale(8), Scale(8));
        button.Click += onClick;
        return button;
    }

    private Control CreateSeparator()
    {
        return new Panel
        {
            Height = 1,
            Width = _contentWidth,
            BackColor = _theme.Muted,
            Margin = Padding.Empty
        };
    }

    private void PositionCloseButton()
    {
        if (_titleBar is null || _closeButton is null)
            return;

        _closeButton.Location = new Point(_titleBar.Width - _closeButton.Width - Scale(6), Math.Max(0, (_titleBar.Height - _closeButton.Height) / 2));
    }

    private void AttachDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        };
    }

    private void CopyInfoToClipboard()
    {
        var text = new StringBuilder()
            .AppendLine($"Personal Flip Pad v{_version}")
            .AppendLine($"Commit: {_commit}")
            .AppendLine($"Build: {_buildDate}")
            .AppendLine($".NET: {_clrVersion} ({_arch})")
            .AppendLine($"OS: {_osDesc}")
            .AppendLine($"RAM: {FormatBytes(_totalRam)}")
            .ToString();

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
        }
    }

    private void OpenLogFolder()
    {
        try
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyCrownJewelApp", "Pfpad");
            if (Directory.Exists(logPath))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logPath}\"") { UseShellExecute = false });
        }
        catch
        {
        }
    }

    private void OpenSettingsFile()
    {
        try
        {
            string settingsPath = SettingsService.DefaultSettingsPath();
            if (!File.Exists(settingsPath))
            {
                MessageBox.Show("settings.json has not been created yet.\nLaunch and configure the app first.",
                    "Settings File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool isEncrypted = false;
            try
            {
                string content = File.ReadAllText(settingsPath);
                System.Text.Json.JsonDocument.Parse(content);
            }
            catch
            {
                isEncrypted = true;
            }

            if (isEncrypted)
            {
                var result = MessageBox.Show(
                    "settings.json is protected by Windows DPAPI (Security Profile: Mid or Max).\n\n" +
                    "The file cannot be opened directly in a text editor.\n\n" +
                    "Would you like to export a plain-text copy to your Desktop for inspection?\n" +
                    "(The exported copy will NOT be encrypted — handle with care.)",
                    "Settings File — Encrypted",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    ExportDecryptedSettings(settingsPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{settingsPath}\"") { UseShellExecute = false });
            }
        }
        catch
        {
        }
    }

    private static void ExportDecryptedSettings(string settingsPath)
    {
        try
        {
            var svc = new SettingsService(settingsPath);
            var settings = svc.LoadWithDecrypt();
            if (settings == null)
            {
                MessageBox.Show("Could not decrypt settings.", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pfpad-settings-export.json");
            string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(exportPath, json);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{exportPath}\"") { UseShellExecute = false });
            MessageBox.Show($"Exported to Desktop as pfpad-settings-export.json.\nDelete this file after inspection.",
                "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private int Scale(int value)
    {
        return Math.Max(1, (int)Math.Round(value * _scale));
    }

    private static int ToMegabytes(long bytes)
    {
        return (int)Math.Max(0, Math.Round(bytes / 1024d / 1024d));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "unknown";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.#} {units[unitIndex]}";
    }

    private static string BuildSessionStats(int openFileCount, string? workspaceRoot)
    {
        var text = $"{Math.Max(0, openFileCount)} files open";
        if (!string.IsNullOrWhiteSpace(workspaceRoot))
            text += $"  •  workspace: {ShortenPath(workspaceRoot)}";

        return text;
    }

    private static string ShortenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return path;

        return $"...\\{parts[^2]}\\{parts[^1]}";
    }

    private static string GetMetadata(string key, string fallback)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var attr in asm.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
            {
                if (attr is AssemblyMetadataAttribute metadata && metadata.Key == key && !string.IsNullOrEmpty(metadata.Value))
                    return metadata.Value;
            }
        }
        catch
        {
        }

        return fallback;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

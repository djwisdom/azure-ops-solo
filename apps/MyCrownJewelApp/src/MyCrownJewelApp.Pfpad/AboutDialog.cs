using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed partial class AboutDialog : Form
{
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReleaseCapture();

    public AboutDialog()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;

        var asm = typeof(AboutDialog).Assembly;
        var verAttrs = asm.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
        string version = verAttrs.Length > 0
            ? ((AssemblyFileVersionAttribute)verAttrs[0]).Version
            : asm.GetName().Version?.ToString() ?? "0.0.0.0";
        string description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";
        string copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        string commit = GetMetadata("CommitHash", "unknown");
        string buildDate = GetMetadata("BuildDate", "unknown");

        string osDesc = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? $"Windows_NT {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor} build {Environment.OSVersion.Version.Build}"
            : Environment.OSVersion.VersionString;
        string clrVersion = Environment.Version.ToString();
        string arch = RuntimeInformation.ProcessArchitecture.ToString();
        string osArch = RuntimeInformation.OSArchitecture.ToString();
        string procCount = Environment.ProcessorCount.ToString();
        long memBytes = 0;
        try { memBytes = (long)new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory; } catch { }

        int margin = 30;
        int lineGap = 3;

        // Title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = theme.PanelBackground,
            Cursor = Cursors.SizeAll
        };
        titleBar.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); };

        var titleLabel = new Label
        {
            Text = "Personal Flip Pad",
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            ForeColor = theme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(margin, 9)
        };
        titleBar.Controls.Add(titleLabel);

        var closeBtn = new Button
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 15, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.Transparent,
            ForeColor = theme.Muted,
            Size = new Size(34, 34),
            Location = new Point(700, 5),
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.FlatAppearance.MouseOverBackColor = theme.Accent;
        titleBar.Controls.Add(closeBtn);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = theme.Background,
            Padding = new Padding(margin, 12, margin, margin)
        };

        int y = 0;
        int contentWidth = 560; // totalWidth (620) - 2*margin (60)

        void AddLine(string text, FontStyle style, Color? color = null, int extraTop = 0, bool wordWrap = false)
        {
            y += extraTop;
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10, style),
                ForeColor = color ?? theme.Text,
                BackColor = Color.Transparent,
                Location = new Point(margin, y),
                MaximumSize = new Size(contentWidth, 0),
                AutoSize = wordWrap,
                AutoEllipsis = !wordWrap
            };
            if (!wordWrap)
                lbl.AutoSize = true;
            content.Controls.Add(lbl);
            y = lbl.Bottom + lineGap;
        }

        void AddSectionHeader(string text)
        {
            y += 4;
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = theme.Text,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(margin, y)
            };
            content.Controls.Add(lbl);
            y = lbl.Bottom + lineGap;
        }

        void AddSeparator()
        {
            y += 6;
            var sep = new Label
            {
                Text = "",
                BorderStyle = BorderStyle.None,
                BackColor = theme.Muted,
                Height = 1,
                Width = contentWidth
            };
            sep.Location = new Point(margin, y);
            content.Controls.Add(sep);
            y = sep.Bottom;
        }

        // --- App info ---
        AddLine("Personal Flip Pad", FontStyle.Bold, theme.Text, 6);
        AddLine($"Version {version}", FontStyle.Regular, theme.Muted);
        // Highlight the target framework prominently
        string runtimeLabel = $".NET {Environment.Version.Major} (net{Environment.Version.Major}.0-windows)  •  {arch}";
        AddLine(runtimeLabel, FontStyle.Bold, theme.Accent);
        if (!string.IsNullOrEmpty(description))
            AddLine(description, FontStyle.Regular, theme.Muted, 0, wordWrap: true);
        AddLine($"Commit: {commit}", FontStyle.Regular, theme.Muted);
        AddLine($"Build: {buildDate} UTC", FontStyle.Regular, theme.Muted);
        if (!string.IsNullOrEmpty(copyright))
            AddLine(copyright, FontStyle.Regular, theme.Disabled);

        AddSeparator();

        var knownPackagePrefixes = new[]
        {
            "LibGit2Sharp", "Microsoft.CodeAnalysis", "TreeSitter", "xunit", "coverlet"
        };
        var knownPackages = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a =>
            {
                try
                {
                    var n = a.GetName();
                    return (Name: n.Name, Version: n.Version?.ToString() ?? "");
                }
                catch { return (Name: "", Version: ""); }
            })
            .Where(a => !string.IsNullOrEmpty(a.Name) && knownPackagePrefixes.Any(p => a.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.Name)
            .ToList();

        var extraPackages = new[] { ("xunit.core", "2.8.*"), ("xunit.runner.visualstudio", "2.8.*"), ("coverlet.collector", "6.0.*") };
        var displayed = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Features and Dependencies in a scrollable RichTextBox
        y += 20; // Extra spacing similar to commit section
        var featuresLabel = new Label
        {
            Text = "Features & Dependencies",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = theme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(margin, y)
        };
        content.Controls.Add(featuresLabel);
        y = featuresLabel.Bottom + lineGap;

        var featuresBox = new RichTextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            Font = new Font("Consolas", 9),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Location = new Point(margin, y),
            Size = new Size(contentWidth, 240),
            WordWrap = true
        };

        var featuresText = new System.Text.StringBuilder();
        featuresText.AppendLine("Features:");
        featuresText.AppendLine("• Flat-themed code editor with syntax highlighting and minimap");
        featuresText.AppendLine("• AIOps intelligence layer — observability, incident, deployment, security");
        featuresText.AppendLine("• AI-assisted root cause analysis, risk scoring, OTel code generation");
        featuresText.AppendLine("• DPAPI-encrypted connector settings (tied to Windows user account)");
        featuresText.AppendLine("• DevSecOps scanning: secrets, CVEs, IaC risks, policy violations");
        featuresText.AppendLine("• Vim mode with macros, marks, visual/block selection, .vimrc support");
        featuresText.AppendLine("• Git integration with diff, blame, history, conflict resolution");
        featuresText.AppendLine("• Workspace panel and Visual Studio-style Solution Explorer");
        featuresText.AppendLine("• Roslyn-powered IntelliSense and semantic highlighting for C#");
        featuresText.AppendLine("• Code folding, elastic tabs, multi-caret, column selection");
        featuresText.AppendLine("• Performance profiler with flame graph analysis");
        featuresText.AppendLine("• Command palette with 40+ searchable commands (Ctrl+Shift+P)");
        featuresText.AppendLine("• Language support: C#, C/C++, Python, JS/TS, Go, Rust, Terraform, Bicep, YAML");
        featuresText.AppendLine("• 23 built-in themes with full flat, theme-aware UI");
        featuresText.AppendLine("• Unicode, BOM detection, RTL text support");
        featuresText.AppendLine("• Streaming build pipeline with MSBuild diagnostic parsing (Ctrl+Shift+B)");
        featuresText.AppendLine("• Notification center with PeriodicTimer feed polling and live toasts");
        featuresText.AppendLine();
        featuresText.AppendLine(".NET 10 Modernisation:");
        featuresText.AppendLine($"• Target framework: net{Environment.Version.Major}.0-windows  •  CLR {clrVersion}");
        featuresText.AppendLine("• [LibraryImport] source-generated P/Invoke (42 converted, trim/AOT safe)");
        featuresText.AppendLine("• Form1 partial-class decomposition — 9 522 → 4 972 lines in core file");
        featuresText.AppendLine("• DocumentManager with events (DocumentAdded/Removed/ActiveChanged)");
        featuresText.AppendLine("• SettingsService with atomic write and corrupt-backup recovery");
        featuresText.AppendLine("• Task.WhenEach (.NET 9+) for progressive feed updates");
        featuresText.AppendLine("• PeriodicTimer for drift-free background polling");
        featuresText.AppendLine();
        featuresText.AppendLine("NuGet Dependencies:");

        foreach (var p in knownPackages)
        {
            if (p.Name == null || !displayed.Add(p.Name)) continue;
            featuresText.AppendLine($"  {p.Name}  {p.Version}");
        }
        foreach (var (name, ver) in extraPackages)
        {
            if (!displayed.Add(name)) continue;
            featuresText.AppendLine($"  {name}  {ver}");
        }

        featuresText.AppendLine($"  .NET Runtime  {clrVersion}  ({arch})  — net{Environment.Version.Major}.0-windows");

        featuresBox.Text = featuresText.ToString();
        content.Controls.Add(featuresBox);
        y = featuresBox.Bottom + lineGap;

        AddSeparator();
        AddSectionHeader("System");
        AddLine($"  OS: {osDesc}  ({osArch})", FontStyle.Regular, theme.Muted);
        AddLine($"  CPU: {procCount} logical processors", FontStyle.Regular, theme.Muted);
        if (memBytes > 0)
        {
            double memGb = memBytes / (1024.0 * 1024.0 * 1024.0);
            AddLine($"  RAM: {memGb:F1} GB", FontStyle.Regular, theme.Muted);
        }

        y += 4;
        var okBtn = new Button
        {
            Text = "OK",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            Size = new Size(90, 30),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderColor = theme.Muted, MouseOverBackColor = theme.ButtonHoverBackground },
            Anchor = AnchorStyles.Top
        };
        okBtn.Click += (s, e) => Close();
        AcceptButton = okBtn;
        int finalContentWidth = 620 - 2 * margin; // matches totalWidth minus left/right margins
        okBtn.Location = new Point((finalContentWidth - okBtn.Width) / 2, y);
        content.Controls.Add(okBtn);
        y = okBtn.Bottom + margin; // Add extra margin below the OK button

        Controls.Add(content);
        Controls.Add(titleBar);

        // Size the dialog to fit all content without scrollbars
        int totalHeight = titleBar.Height + y;
        totalHeight = Math.Max(510, Math.Min(totalHeight, 660));
        int totalWidth = 620;
        Size = new Size(totalWidth, totalHeight);
        MinimumSize = Size;
        MaximumSize = Size;

        Paint += (s, e) =>
        {
            using var p = new Pen(theme.Muted, 1);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        };
    }

    private static string GetMetadata(string key, string fallback)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var attr in asm.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
            {
                if (attr is AssemblyMetadataAttribute m && m.Key == key && !string.IsNullOrEmpty(m.Value))
                    return m.Value;
            }
        }
        catch { }
        return fallback;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}
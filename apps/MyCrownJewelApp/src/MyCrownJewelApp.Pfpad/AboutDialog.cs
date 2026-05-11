using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class AboutDialog : Form
{
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 2;

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

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
                Location = new Point(0, y)
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
                Width = 550
            };
            sep.Location = new Point(0, y);
            content.Controls.Add(sep);
            y = sep.Bottom;
        }

        // --- App info ---
        AddLine("Personal Flip Pad", FontStyle.Bold, theme.Text, 6);
        AddLine($"Version {version}", FontStyle.Regular, theme.Muted);
        if (!string.IsNullOrEmpty(description))
            AddLine(description, FontStyle.Regular, theme.Muted, 0, wordWrap: true);
        AddLine($"Commit: {commit}", FontStyle.Regular, theme.Muted);
        AddLine($"Build: {buildDate} UTC", FontStyle.Regular, theme.Muted);
        if (!string.IsNullOrEmpty(copyright))
            AddLine(copyright, FontStyle.Regular, theme.Disabled);

        AddSeparator();
        AddSectionHeader("NuGet Dependencies");

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
        foreach (var p in knownPackages)
        {
            if (p.Name == null || !displayed.Add(p.Name)) continue;
            AddLine($"  {p.Name}  {p.Version}", FontStyle.Regular, theme.Muted);
        }
        foreach (var (name, ver) in extraPackages)
        {
            if (!displayed.Add(name)) continue;
            AddLine($"  {name}  {ver}", FontStyle.Regular, theme.Muted);
        }

        AddLine($"  .NET Runtime  {clrVersion}  ({arch})", FontStyle.Regular, theme.Muted);

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
        y = okBtn.Bottom;

        Controls.Add(content);
        Controls.Add(titleBar);

        // Size the dialog to fit all content without scrollbars
        int totalHeight = titleBar.Height + y + margin;
        totalHeight = Math.Max(420, Math.Min(totalHeight, 720));
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
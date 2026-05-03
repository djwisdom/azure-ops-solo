using System;
using System.Drawing;
using System.Drawing.Text;
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
        Size = new Size(420, 380);
        MinimumSize = Size;
        MaximumSize = Size;
        ShowInTaskbar = false;
        ShowIcon = false;

        var os = Environment.OSVersion;
        string osDesc = os.Platform == PlatformID.Win32NT
            ? $"Windows_NT {os.Version.Major}.{os.Version.Minor} build {os.Version.Build}"
            : os.VersionString;

        string version = "0.5.55";
        string commit = GetGitCommitHash();
        string date = GetCompileDate();

        int pad = 28;

        // Draggable title bar
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = theme.PanelBackground,
            Cursor = Cursors.SizeAll
        };
        titleBar.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); };

        var titleLabel = new Label
        {
            Text = "Personal Flip Pad",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = theme.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(pad, 8)
        };
        titleBar.Controls.Add(titleLabel);

        var closeBtn = new Button
        {
            Text = "\u00D7",
            Font = new Font("Segoe UI", 14, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Color.Transparent,
            ForeColor = theme.Muted,
            Size = new Size(32, 32),
            Location = new Point(Width - 40, 4),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        closeBtn.Click += (s, e) => Close();
        closeBtn.FlatAppearance.MouseOverBackColor = theme.Accent;
        titleBar.Controls.Add(closeBtn);

        // Content panel
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = theme.Background,
            Padding = new Padding(pad, 10, pad, pad)
        };

        void AddLine(string text, FontStyle style, Color? color = null, int extraY = 0)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10, style),
                ForeColor = color ?? theme.Text,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(0, content.Controls.Count == 0 ? 0
                    : content.Controls[^1].Bottom + (extraY > 0 ? extraY : 4)),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            content.Controls.Add(lbl);
        }

        AddLine("Personal Flip Pad", FontStyle.Bold, theme.Text, 6);
        AddLine($"Version {version}", FontStyle.Regular, theme.Muted);
        AddLine($"Commit: {commit}", FontStyle.Regular, theme.Muted);
        AddLine($"Date: {date}", FontStyle.Regular, theme.Muted);

        // Separator
        var sep = new Label
        {
            Text = "",
            BorderStyle = BorderStyle.None,
            BackColor = theme.Muted,
            Height = 1,
            Width = Width - pad * 2 - pad,
            Location = new Point(0, content.Controls[^1].Bottom + 12)
        };
        content.Controls.Add(sep);

        AddLine("Built with:", FontStyle.Bold, theme.Text, 8);
        AddLine("  .NET 8.0 (Windows Forms)", FontStyle.Regular, theme.Muted);
        AddLine("  xUnit.net (testing)", FontStyle.Regular, theme.Muted);

        // OS
        var sep2 = new Label
        {
            Text = "",
            BorderStyle = BorderStyle.None,
            BackColor = theme.Muted,
            Height = 1,
            Width = Width - pad * 2 - pad,
            Location = new Point(0, content.Controls[^1].Bottom + 12)
        };
        content.Controls.Add(sep2);
        AddLine($"OS: {osDesc}", FontStyle.Regular, theme.Muted, 8);

        Controls.Add(content);
        Controls.Add(titleBar);

        var okBtn = new Button
        {
            Text = "OK",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            Size = new Size(80, 30),
            Location = new Point(Width - pad - 80, Height - titleBar.Height - pad - 40),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        okBtn.FlatAppearance.BorderColor = theme.Muted;
        okBtn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        okBtn.Click += (s, e) => Close();
        content.Controls.Add(okBtn);
        AcceptButton = okBtn;

        Paint += (s, e) =>
        {
            using var p = new Pen(theme.Muted, 1);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        };
    }

    private static string GetGitCommitHash()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var attr in asm.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
            {
                if (attr is System.Reflection.AssemblyMetadataAttribute m && m.Key == "CommitHash" && !string.IsNullOrEmpty(m.Value))
                    return m.Value;
            }
        }
        catch { }
        return "unknown";
    }

    private static string GetCompileDate()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var attr in asm.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
            {
                if (attr is System.Reflection.AssemblyMetadataAttribute m && m.Key == "BuildDate" && !string.IsNullOrEmpty(m.Value))
                    return m.Value + " UTC";
            }
        }
        catch { }
        return "unknown";
    }
}

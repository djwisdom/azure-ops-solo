using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class HoverTooltipForm : Form
{
    private readonly RichTextBox _content;

    public HoverTooltipForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(180, 20);
        MaximumSize = new Size(500, 300);

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.TerminalHeaderBackground;

        _content = new RichTextBox
        {
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Text,
            Font = new Font("Segoe UI", 9),
            AutoSize = true,
            MinimumSize = new Size(160, 16),
            MaximumSize = new Size(480, 280),
            Margin = new Padding(6),
            DetectUrls = false
        };
        Controls.Add(_content);
    }

    public void ShowAt(Point screenLocation, string title, string summary, string context)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        _content.BackColor = theme.TerminalHeaderBackground;
        _content.ForeColor = theme.Text;
        _content.Clear();
        _content.SelectionFont = new Font("Segoe UI", 9, FontStyle.Bold);
        _content.AppendText(title + "\n");
        if (!string.IsNullOrEmpty(context))
        {
            _content.SelectionFont = new Font("Consolas", 8.5f);
            _content.SelectionColor = theme.Accent;
            _content.AppendText(context + "\n");
        }
        if (!string.IsNullOrEmpty(summary))
        {
            _content.SelectionFont = new Font("Segoe UI", 8.5f);
            _content.SelectionColor = theme.Text;
            _content.AppendText(summary);
        }

        Location = new Point(
            Math.Max(0, Math.Min(screenLocation.X, Screen.PrimaryScreen.WorkingArea.Width - Width)),
            Math.Max(0, screenLocation.Y + 24));

        if (!Visible) Show();
        else BringToFront();
    }

    public void Dismiss()
    {
        if (Visible) Hide();
    }
}

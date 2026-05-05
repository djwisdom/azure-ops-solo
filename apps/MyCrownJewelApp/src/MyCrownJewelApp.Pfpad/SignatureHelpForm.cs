using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class SignatureHelpForm : Form
{
    private readonly Label _sigLabel;
    private readonly Label _docLabel;

    public SignatureHelpForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(200, 20);
        MaximumSize = new Size(600, 200);

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.TerminalHeaderBackground;

        _sigLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(580, 80),
            Font = new Font("Consolas", 10),
            ForeColor = theme.Text,
            BackColor = Color.Transparent
        };

        _docLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(580, 100),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = theme.Muted,
            BackColor = Color.Transparent
        };

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            BackColor = Color.Transparent,
            Padding = new Padding(6)
        };
        flow.Controls.Add(_sigLabel);
        flow.Controls.Add(_docLabel);
        Controls.Add(flow);
    }

    public void ShowAt(Point screenLocation, string signature, int currentParam, string paramDoc)
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        _sigLabel.Text = signature;
        _sigLabel.ForeColor = theme.Text;
        _docLabel.Text = paramDoc;
        _docLabel.ForeColor = theme.Muted;
        _docLabel.Visible = !string.IsNullOrEmpty(paramDoc);

        Location = new Point(
            Math.Max(0, Math.Min(screenLocation.X, Screen.PrimaryScreen.WorkingArea.Width - Width)),
            Math.Max(0, screenLocation.Y - Height - 4));

        if (!Visible) Show();
        else BringToFront();
    }

    public void Dismiss()
    {
        if (Visible) Hide();
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

#pragma warning disable CS8602

public sealed class RainbowBracketDemoForm : Form
{
    private readonly HighlightRichTextBox _editor;
    private readonly CheckBox _enableToggle;
    private readonly CheckBox _highContrastToggle;
    private readonly CheckBox _accessoryGuidesToggle;
    private readonly Button _customPaletteButton;
    private readonly TableLayoutPanel _layout;
    private Color[] _currentPalette = RainbowBracketEngine.DefaultPalette;
    private bool _paletteCustomized;

    public RainbowBracketDemoForm()
    {
        Text = "Rainbow Brackets Demo";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.FromArgb(220, 220, 220);
        Font = new Font("Segoe UI", 10);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var controlsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10),
            AutoSize = true,
            BackColor = Color.FromArgb(45, 45, 45)
        };

        _enableToggle = new CheckBox
        {
            Text = "Enable Rainbow Brackets",
            Checked = true,
            ForeColor = ForeColor,
            AutoSize = true,
            Padding = new Padding(4)
        };
        var enableToggle = _enableToggle;
        enableToggle.CheckedChanged += (_, _) =>
        {
            _editor.EnableRainbowBrackets(enableToggle.Checked);
        };

        _highContrastToggle = new CheckBox
        {
            Text = "High Contrast Palette",
            Checked = false,
            ForeColor = ForeColor,
            AutoSize = true,
            Padding = new Padding(4)
        };
        var highContrastToggle = _highContrastToggle;
        highContrastToggle.CheckedChanged += (_, _) =>
        {
            _editor.SetHighContrastPalette(highContrastToggle.Checked);
            _paletteCustomized = false;
        };
        highContrastToggle.Enabled = _enableToggle.Checked;

        _accessoryGuidesToggle = new CheckBox
        {
            Text = "Show Shapes/Underlines",
            Checked = false,
            ForeColor = ForeColor,
            AutoSize = true,
            Padding = new Padding(4)
        };
        var guidesToggle = _accessoryGuidesToggle;
        guidesToggle.CheckedChanged += (_, _) =>
        {
            _editor.SetAccessoryGuides(guidesToggle.Checked);
        };
        guidesToggle.Enabled = _enableToggle.Checked;

        _customPaletteButton = new Button
        {
            Text = "Custom Palette...",
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = Color.FromArgb(60, 60, 60),
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4)
        };
        _customPaletteButton.FlatAppearance.BorderSize = 0;
        _customPaletteButton.Click += CustomPalette_Click;
        _customPaletteButton.Enabled = _enableToggle.Checked;

        enableToggle.CheckedChanged += (_, _) =>
        {
            _editor.EnableRainbowBrackets(enableToggle.Checked);
            highContrastToggle.Enabled = enableToggle.Checked;
            guidesToggle.Enabled = enableToggle.Checked;
            _customPaletteButton.Enabled = enableToggle.Checked;
        };

        controlsPanel.Controls.Add(enableToggle);
        controlsPanel.Controls.Add(highContrastToggle);
        controlsPanel.Controls.Add(guidesToggle);
        controlsPanel.Controls.Add(_customPaletteButton);

        _editor = new HighlightRichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 11),
            Text = GetDemoText(),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Margin = new Padding(0),
            Padding = new Padding(0),
            BorderStyle = BorderStyle.None,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both
        };

        _editor.EnableRainbowBrackets(true);

        _layout.Controls.Add(controlsPanel, 0, 0);
        _layout.Controls.Add(_editor, 0, 1);
        Controls.Add(_layout);
    }

    private void CustomPalette_Click(object? sender, EventArgs e)
    {
        using var dialog = new ColorDialog
        {
            AllowFullOpen = true,
            AnyColor = true,
            SolidColorOnly = false
        };

        if (!_paletteCustomized)
            _currentPalette = (Color[])RainbowBracketEngine.DefaultPalette.Clone();

        var custom = new Color[8];
        for (int i = 0; i < 8; i++)
        {
            dialog.Color = _currentPalette[i];
            if (dialog.ShowDialog(this) == DialogResult.OK)
                custom[i] = dialog.Color;
            else
                custom[i] = _currentPalette[i];
        }

        _currentPalette = custom;
        _paletteCustomized = true;
        _highContrastToggle.Checked = false;
        _editor.SetPalette(custom);
    }

    private static string GetDemoText()
    {
        return @"using System;
using System.Collections.Generic;

namespace Demo
{
    public class RainbowBracketsExample
    {
        private static Dictionary<string, List<int>> _data = new();

        public static void Main(string[] args)
        {
            var result = ProcessData(
                new[] { 1, 2, 3 },
                new Dictionary<string, object>
                {
                    [""key1""] = new List<int> { 10, 20, 30 },
                    [""key2""] = new List<int> { 40, 50, 60 }
                }
            );

            // Nested examples:
            var matrix = new int[][]
            {
                new int[] { 1, 2, 3 },
                new int[] { 4, (5 + 3) * 2, 6 },
                new int[] { 7, 8, 9 }
            };

            // Mismatch example (uncomment to see):
            // var x = new int[)];

            // String with braces should be skipped:
            var template = ""{{name}} said ""hello"" to {{target}}"";
            var regex = new System.Text.RegularExpressions.Regex(""[{}]"");
        }
    }
}
";
    }
}

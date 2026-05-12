using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Color picker dialog with a palette of preset colors and a custom color option.
/// Returns the selected color via <see cref="SelectedColor"/> property.
/// </summary>
internal sealed class ColorPickerDialog : Form
{
    private readonly List<Color> _presetColors = new()
    {
        Color.FromArgb(0x0078D4), // VS Code blue
        Color.FromArgb(0x106EBE),
        Color.FromArgb(0x00B294), // teal
        Color.FromArgb(0x1D8F5A), // green
        Color.FromArgb(0xCE9178), // orange
        Color.FromArgb(0xC586C0), // purple
        Color.FromArgb(0x569CD6), // light blue
        Color.FromArgb(0xDCDCAA), // yellow
        Color.FromArgb(0x4EC9B0), // cyan
        Color.FromArgb(0x9CDCFE), // pale blue
        Color.FromArgb(0xD16969), // red
        Color.FromArgb(0xD7BA7D), // tan
    };

    public Color SelectedColor { get; private set; }
    private FlowLayoutPanel _flow = null!;
    private readonly Color _originalColor;

    public ColorPickerDialog(Color currentColor)
    {
        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] Constructor called with color: {currentColor}");

        _originalColor = currentColor;
        SelectedColor = currentColor;
        Text = "Choose Profile Color";
        Size = new Size(280, 240);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = ThemeManager.Instance.CurrentTheme.Background;
        ForeColor = ThemeManager.Instance.CurrentTheme.Text;
        Font = new Font("Segoe UI", 9);

        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] Initializing form...");
        InitializeForm();

        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] Constructor completed. SelectedColor: {SelectedColor}");
    }

    private void InitializeForm()
    {
        // Preset color buttons
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };

        foreach (var color in _presetColors)
        {
            var btn = CreateColorButton(color);
            btn.Click += (s, e) => SelectColor(color);
            _flow.Controls.Add(btn);
        }

        // Custom color button
        var customBtn = new Button
        {
            Text = "Custom…",
            Size = new Size(40, 40),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.Instance.CurrentTheme.PanelBackground,
            ForeColor = ThemeManager.Instance.CurrentTheme.Text,
            Font = new Font("Segoe UI", 8)
        };
        customBtn.Click += (s, e) => ChooseCustomColor();
        _flow.Controls.Add(customBtn);

        _flow.Location = new Point(0, 0);
        Controls.Add(_flow);

        // OK/Cancel buttons at bottom
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(110, 200),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.Instance.CurrentTheme.Accent,
            ForeColor = Color.White
        };
        ok.Click += (s, e) => {
            System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] OK clicked, returning SelectedColor: {SelectedColor}");
            DialogResult = DialogResult.OK;
            Close();
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(200, 200),
            Size = new Size(80, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeManager.Instance.CurrentTheme.PanelBackground,
            ForeColor = ThemeManager.Instance.CurrentTheme.Text
        };
        cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(ok);
        Controls.Add(cancel);

        AcceptButton = ok;
        CancelButton = cancel;

        UpdateSelectedIndicator();
    }

    private Button CreateColorButton(Color color)
    {
        var btn = new Button
        {
            Size = new Size(32, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            Tag = color
        };
        btn.FlatAppearance.BorderSize = 2;
        btn.FlatAppearance.BorderColor = ThemeManager.Instance.CurrentTheme.Border;
        return btn;
    }

    private void SelectColor(Color color)
    {
        SelectedColor = color;
        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] Selected preset color: {color}");
        UpdateSelectedIndicator();
    }

    private void UpdateSelectedIndicator()
    {
        if (_flow == null) return;
        foreach (Control child in _flow.Controls)
        {
            if (child is Button btn && btn.Tag is Color c)
            {
                btn.FlatAppearance.BorderSize = c.ToArgb() == SelectedColor.ToArgb() ? 4 : 2;
                btn.Invalidate();
            }
        }
    }

    private void ChooseCustomColor()
    {
        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] ChooseCustomColor called. Current SelectedColor: {SelectedColor}");

        // Simple color cycling for now - cycles through preset colors
        var customColors = new[]
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Yellow,
            Color.Purple,
            Color.Orange,
            Color.Pink,
            Color.Cyan
        };

        // Find current color index or start from 0
        int currentIndex = Array.IndexOf(customColors, SelectedColor);
        if (currentIndex == -1) currentIndex = 0;

        // Move to next color
        int nextIndex = (currentIndex + 1) % customColors.Length;
        SelectedColor = customColors[nextIndex];

        System.Diagnostics.Debug.WriteLine($"[ColorPickerDialog] Cycled to new color: {SelectedColor} (index {nextIndex})");
    }
}

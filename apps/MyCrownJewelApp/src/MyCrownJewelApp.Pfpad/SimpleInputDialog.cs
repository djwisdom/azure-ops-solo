namespace MyCrownJewelApp.Pfpad;

internal sealed class SimpleInputDialog : Form
{
    private readonly TextBox _inputBox;

    public string? Result { get; private set; }

    private SimpleInputDialog(string prompt, string title, string defaultValue)
    {
        Text = title;
        Size = new Size(400, 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        var promptLabel = new Label
        {
            Text = prompt,
            Location = new Point(12, 12),
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = theme.Text
        };

        _inputBox = new TextBox
        {
            Text = defaultValue,
            Location = new Point(12, 35),
            Width = 360,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };
        _inputBox.SelectAll();

        Button okBtn = new()
        {
            Text = "OK",
            Location = new Point(216, 70),
            Width = 75,
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Border, MouseOverBackColor = theme.ButtonHoverBackground },
            DialogResult = DialogResult.OK
        };
        okBtn.Click += (s, e) => { Result = _inputBox.Text; };

        Button cancelBtn = new()
        {
            Text = "Cancel",
            Location = new Point(300, 70),
            Width = 75,
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Border, MouseOverBackColor = theme.ButtonHoverBackground },
            DialogResult = DialogResult.Cancel
        };

        Controls.AddRange(new Control[] { promptLabel, _inputBox, okBtn, cancelBtn });
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    public static string? Show(IWin32Window owner, string prompt, string title, string defaultValue = "")
    {
        using var dlg = new SimpleInputDialog(prompt, title, defaultValue);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Result : null;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }
}

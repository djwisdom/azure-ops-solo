namespace MyCrownJewelApp.Pfpad;

internal sealed class SimpleInputDialog : Form
{
    private readonly TextBox _inputBox;

    public string? Result { get; private set; }

    private SimpleInputDialog(string prompt, string title, string defaultValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        const int margin = 14;
        const int gap = 8;
        const int btnW = 80;
        const int btnH = 28;
        const int formW = 400;

        var promptLabel = new Label
        {
            Text = prompt,
            Left = margin,
            Top = margin,
            Width = formW - margin * 2,
            AutoSize = false,
            Height = 18,
            BackColor = Color.Transparent,
            ForeColor = theme.Text
        };

        _inputBox = new TextBox
        {
            Text = defaultValue,
            Left = margin,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.None,
            Width = formW - margin * 2
        };
        // Wrapper positions itself at the textbox Location; adjust top after label
        int inputTop = promptLabel.Bottom + gap;
        _inputBox.Top = inputTop + 2; // inside wrapper padding
        var inputBoxWrapper = FlatUiHelper.WrapFlat(_inputBox, theme);
        inputBoxWrapper.SetBounds(margin, inputTop, formW - margin * 2, _inputBox.PreferredHeight + 6);
        _inputBox.SelectAll();

        int btnTop = inputBoxWrapper.Bottom + margin;

        Button okBtn = new()
        {
            Text = "OK",
            Size = new Size(btnW, btnH),
            Location = new Point(formW - margin - btnW * 2 - gap, btnTop),
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
            Size = new Size(btnW, btnH),
            Location = new Point(formW - margin - btnW, btnTop),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Border, MouseOverBackColor = theme.ButtonHoverBackground },
            DialogResult = DialogResult.Cancel
        };

        int clientH = btnTop + btnH + margin;
        ClientSize = new Size(formW, clientH);

        Controls.AddRange(new Control[] { promptLabel, inputBoxWrapper, okBtn, cancelBtn });
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

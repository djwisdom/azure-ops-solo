namespace MyCrownJewelApp.Pfpad;

public partial class FindReplaceDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly bool _isReplace;

    private TextBox findTextBox = null!;
    private TextBox replaceTextBox = null!;
    private CheckBox caseSensitiveCheckBox = null!;
    private CheckBox regexCheckBox = null!;
    private GroupBox directionGroup = null!;
    private RadioButton upRadioButton = null!;
    private RadioButton downRadioButton = null!;
    private CheckBox wrapCheckBox = null!;
    private Button findNextButton = null!;
    private Button replaceButton = null!;
    private Button replaceAllButton = null!;
    private Button findInFilesButton = null!;
    private Button cancelButton = null!;

    public string FindText => findTextBox.Text;
    public bool CaseSensitive => caseSensitiveCheckBox.Checked;
    public bool SearchUp => upRadioButton.Checked;
    public bool UseRegex => regexCheckBox.Checked;
    public string ReplaceText => replaceTextBox.Text;
    public bool WrapAround => wrapCheckBox.Checked;

    public FindReplaceDialog(Form1 form, bool replaceMode, string? initialFind = null, bool initialCaseSensitive = false, bool initialRegex = false)
    {
        _mainForm = form;
        _isReplace = replaceMode;
        InitializeForm(initialFind ?? "");
        ApplyTheme();
        if (!string.IsNullOrEmpty(initialFind))
        {
            caseSensitiveCheckBox.Checked = initialCaseSensitive;
            regexCheckBox.Checked = initialRegex;
        }
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        static void StyleTextBox(TextBox tb)
        {
            var t = ThemeManager.Instance.CurrentTheme;
            tb.BackColor = t.EditorBackground;
            tb.ForeColor = t.Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        static void StyleButton(Button btn)
        {
            var t = ThemeManager.Instance.CurrentTheme;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = t.PanelBackground;
            btn.ForeColor = t.Text;
            btn.FlatAppearance.BorderColor = t.Border;
            btn.FlatAppearance.MouseOverBackColor = t.ButtonHoverBackground;
        }
        static void StyleCheckBox(CheckBox cb)
        {
            cb.BackColor = Color.Transparent;
            cb.ForeColor = ThemeManager.Instance.CurrentTheme.Text;
        }

        StyleTextBox(findTextBox);
        StyleTextBox(replaceTextBox);
        StyleCheckBox(caseSensitiveCheckBox);
        StyleCheckBox(regexCheckBox);
        StyleCheckBox(wrapCheckBox);

        directionGroup.BackColor = Color.Transparent;
        directionGroup.ForeColor = theme.Text;
        upRadioButton.BackColor = Color.Transparent;
        upRadioButton.ForeColor = theme.Text;
        downRadioButton.BackColor = Color.Transparent;
        downRadioButton.ForeColor = theme.Text;

        StyleButton(findNextButton);
        StyleButton(replaceButton);
        StyleButton(replaceAllButton);
        StyleButton(findInFilesButton);
        StyleButton(cancelButton);
    }

    private void InitializeForm(string initialFind)
    {
        Text = _isReplace ? "Replace" : "Find";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12;
        int labelW = 90;
        int inputW = 270;
        int leftCol = 10 + labelW;

        // Find what
        var findLabel = new Label { Text = "Fi&nd what:", Location = new Point(10, y), AutoSize = true };
        findTextBox = new TextBox { Text = initialFind, Location = new Point(leftCol, y - 3), Width = inputW };
        findTextBox.TextChanged += (s, e) => UpdateButtons();
        findTextBox.SelectAll();
        y += 28;

        // Replace with
        var replaceLabel = new Label { Text = "Re&place with:", Location = new Point(10, y), AutoSize = true };
        replaceTextBox = new TextBox { Location = new Point(leftCol, y - 3), Width = inputW };
        replaceTextBox.Visible = _isReplace;
        replaceLabel.Visible = _isReplace;
        y += _isReplace ? 28 : 0;

        // Options row
        caseSensitiveCheckBox = new CheckBox { Text = "Match &case", Location = new Point(leftCol, y), AutoSize = true };
        int cx = leftCol + 100;
        regexCheckBox = new CheckBox { Text = "Use rege&x", Location = new Point(cx, y), AutoSize = true };
        y += 26;

        // Direction group
        directionGroup = new GroupBox
        {
            Text = "Direction",
            Location = new Point(leftCol, y),
            Size = new Size(160, 50)
        };
        upRadioButton = new RadioButton { Text = "&Up", Location = new Point(12, 22), AutoSize = true };
        downRadioButton = new RadioButton { Text = "&Down", Location = new Point(80, 22), AutoSize = true };
        downRadioButton.Checked = true;
        directionGroup.Controls.AddRange(new[] { upRadioButton, downRadioButton });

        wrapCheckBox = new CheckBox { Text = "&Wrap", Location = new Point(leftCol + 166, y + 14), AutoSize = true };
        wrapCheckBox.Checked = true;
        y += 60;

        // Buttons
        int btnW = 85;
        int btnH = 26;
        int btnRight = leftCol + inputW;

        findNextButton = new Button { Text = "&Find Next", Location = new Point(btnRight - btnW, y), Width = btnW, Height = btnH, Enabled = !string.IsNullOrEmpty(initialFind) };
        findNextButton.Click += (s, e) => PerformFindNext();
        y += btnH + 4;

        int replaceBtnRight = btnRight;
        if (_isReplace)
        {
            replaceButton = new Button { Text = "&Replace", Location = new Point(replaceBtnRight - btnW, y), Width = btnW, Height = btnH, Enabled = !string.IsNullOrEmpty(initialFind) };
            replaceButton.Click += (s, e) => PerformReplace();
            y += btnH + 4;

            replaceAllButton = new Button { Text = "Replace &All", Location = new Point(replaceBtnRight - btnW, y), Width = btnW, Height = btnH, Enabled = !string.IsNullOrEmpty(initialFind) };
            replaceAllButton.Click += (s, e) => PerformReplaceAll();
            y += btnH + 4;
        }
        else
        {
            replaceButton = new Button { Visible = false };
            replaceAllButton = new Button { Visible = false };
        }

        findInFilesButton = new Button { Text = "Find in &Files", Location = new Point(leftCol, y), Width = 100, Height = btnH };
        findInFilesButton.Click += (s, e) => PerformFindInFiles();

        cancelButton = new Button { Text = "Cancel", Location = new Point(replaceBtnRight - 75, y), Width = 75, Height = btnH };
        cancelButton.Click += (s, e) => Close();

        y += btnH + 12;
        ClientSize = new Size(leftCol + inputW + 16, y);

        Controls.AddRange(new Control[]
        {
            findLabel, findTextBox, replaceLabel, replaceTextBox,
            caseSensitiveCheckBox, regexCheckBox,
            directionGroup, wrapCheckBox,
            findNextButton, replaceButton, replaceAllButton,
            findInFilesButton, cancelButton
        });

        AcceptButton = _isReplace ? replaceButton : findNextButton;
        CancelButton = cancelButton;
    }

    private void UpdateButtons()
    {
        bool hasText = !string.IsNullOrEmpty(findTextBox.Text);
        findNextButton.Enabled = hasText;
        replaceButton.Enabled = hasText;
        replaceAllButton.Enabled = hasText;
    }

    private void PerformFindNext()
    {
        _mainForm.PerformFind(FindText, CaseSensitive, SearchUp, UseRegex);
    }

    private void PerformReplace()
    {
        _mainForm.PerformReplace(FindText, ReplaceText, CaseSensitive, UseRegex, false);
    }

    private void PerformReplaceAll()
    {
        _mainForm.PerformReplace(FindText, ReplaceText, CaseSensitive, UseRegex, true);
    }

    private void PerformFindInFiles()
    {
        _mainForm.PerformFindInFiles(FindText, CaseSensitive, UseRegex);
    }
}

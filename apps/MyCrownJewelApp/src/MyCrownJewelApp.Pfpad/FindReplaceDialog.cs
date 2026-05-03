namespace MyCrownJewelApp.Pfpad;

#pragma warning disable CS8618 // All fields assigned in InitializeForm/InitializeComponent
public partial class FindReplaceDialog : Form
{
    private Form1 mainForm;
    private bool isReplace;

    private Label findLabel;
    private TextBox findTextBox;
    private CheckBox caseSensitiveCheckBox;
    private GroupBox directionGroup;
    private RadioButton upRadioButton;
    private RadioButton downRadioButton;
    private CheckBox wrapCheckBox;
    private Button findNextButton;
    private Button replaceButton;
    private Button replaceAllButton;
    private Button cancelButton;
#pragma warning disable CS0169 // Intentionally unused layout spacer
    private Label spacerLabel;
#pragma warning restore CS0169

    public FindReplaceDialog(Form1 form, bool replaceMode)
    {
        mainForm = form;
        isReplace = replaceMode;
        InitializeForm();
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        void StyleLabel(Label lbl)
        {
            lbl.BackColor = Color.Transparent;
            lbl.ForeColor = theme.Text;
        }
        void StyleTextBox(TextBox tb)
        {
            tb.BackColor = theme.EditorBackground;
            tb.ForeColor = theme.Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = theme.PanelBackground;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.BorderColor = theme.Border;
            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        }
        void StyleCheckBox(CheckBox cb)
        {
            cb.BackColor = Color.Transparent;
            cb.ForeColor = theme.Text;
        }

        StyleLabel(findLabel);
        StyleTextBox(findTextBox);
        StyleCheckBox(caseSensitiveCheckBox);
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
        StyleButton(cancelButton);
    }

    private void InitializeForm()
    {
        Text = isReplace ? "Replace" : "Find";
        Size = new Size(400, 280);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        findLabel = new Label { Text = "Fi&nd what:", Location = new Point(10, 15), AutoSize = true };
        findTextBox = new TextBox { Location = new Point(100, 12), Width = 270 };
        findTextBox.TextChanged += (s, e) => UpdateButtons();

        caseSensitiveCheckBox = new CheckBox { Text = "Match &case", Location = new Point(100, 45), AutoSize = true };

        directionGroup = new GroupBox
        {
            Text = "Direction",
            Location = new Point(100, 75),
            Size = new Size(150, 80)
        };
        upRadioButton = new RadioButton { Text = "&Up", Location = new Point(15, 30), AutoSize = true };
        downRadioButton = new RadioButton { Text = "&Down", Location = new Point(80, 30), AutoSize = true };
        downRadioButton.Checked = true;
        directionGroup.Controls.AddRange(new Control[] { upRadioButton, downRadioButton });

        wrapCheckBox = new CheckBox { Text = "&Wrap around", Location = new Point(260, 90), AutoSize = true };
        wrapCheckBox.Checked = true;

        findNextButton = new Button { Text = "&Find Next", Location = new Point(200, 150), Width = 80 };
        findNextButton.Click += (s, e) => PerformFindNext();
        findNextButton.Enabled = false;

        replaceButton = new Button { Text = "&Replace", Location = new Point(290, 150), Width = 80 };
        replaceButton.Click += (s, e) => PerformReplace();
        replaceButton.Enabled = false;

        replaceAllButton = new Button { Text = "Replace &All", Location = new Point(200, 185), Width = 170 };
        replaceAllButton.Click += (s, e) => PerformReplaceAll();

        cancelButton = new Button { Text = "Cancel", Location = new Point(290, 220), Width = 80 };
        cancelButton.Click += (s, e) => Close();

        Controls.AddRange(new Control[]
        {
            findLabel, findTextBox, caseSensitiveCheckBox,
            directionGroup, wrapCheckBox,
            findNextButton, replaceButton, replaceAllButton, cancelButton
        });

        if (!isReplace)
        {
            replaceButton.Visible = false;
            replaceAllButton.Visible = false;
            ClientSize = new Size(400, 200);
            cancelButton.Location = new Point(290, 150);
        }

        AcceptButton = isReplace ? replaceButton : findNextButton;
        CancelButton = cancelButton;
    }

    private void UpdateButtons()
    {
        bool hasText = !string.IsNullOrEmpty(findTextBox.Text);
        findNextButton.Enabled = hasText;
        replaceButton.Enabled = hasText;
    }

    private void PerformFindNext()
    {
        mainForm.PerformFind(findTextBox.Text, caseSensitiveCheckBox.Checked, upRadioButton.Checked);
    }

    private void PerformReplace()
    {
        mainForm.PerformReplace(findTextBox.Text, "", caseSensitiveCheckBox.Checked, false);
    }

    private void PerformReplaceAll()
    {
        mainForm.PerformReplace(findTextBox.Text, "", caseSensitiveCheckBox.Checked, true);
    }
}
#pragma warning restore CS8618

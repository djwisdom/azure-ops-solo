namespace MyCrownJewelApp.Pfpad;

#pragma warning disable CS8618 // Fields assigned in InitializeComponent()
public partial class GoToDialog : Form
{
    private Form1 mainForm;
    private Label lineLabel;
    private TextBox lineTextBox;
    private Button goToButton;
    private Button cancelButton;

    public GoToDialog(Form1 form)
    {
        mainForm = form;
        InitializeComponent();
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        lineLabel.BackColor = Color.Transparent;
        lineLabel.ForeColor = theme.Text;

        lineTextBox.BackColor = theme.EditorBackground;
        lineTextBox.ForeColor = theme.Text;
        lineTextBox.BorderStyle = BorderStyle.FixedSingle;

        void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = theme.PanelBackground;
            btn.ForeColor = theme.Text;
            btn.FlatAppearance.BorderColor = theme.Border;
            btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
        }

        StyleButton(goToButton);
        StyleButton(cancelButton);
    }

    private void InitializeComponent()
    {
        Text = "Go To Line";
        Size = new Size(280, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        lineLabel = new Label { Text = "Line &number:", Location = new Point(10, 15), AutoSize = true };
        lineTextBox = new TextBox { Location = new Point(90, 12), Width = 150 };
#pragma warning disable CS8622 // Event subscription — sender may be null at attach time
        lineTextBox.KeyPress += LineTextBox_KeyPress;
#pragma warning restore CS8622

        goToButton = new Button { Text = "&Go To", Location = new Point(90, 50), Width = 80 };
#pragma warning disable CS8622
        goToButton.Click += GoToButton_Click;
#pragma warning restore CS8622

        cancelButton = new Button { Text = "Cancel", Location = new Point(180, 50), Width = 80 };
        cancelButton.Click += (s, e) => Close();

        Controls.AddRange(new Control[] { lineLabel, lineTextBox, goToButton, cancelButton });

        AcceptButton = goToButton;
        CancelButton = cancelButton;
    }

    private void LineTextBox_KeyPress(object sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private void GoToButton_Click(object sender, EventArgs e)
    {
        if (int.TryParse(lineTextBox.Text, out int lineNumber))
        {
            mainForm.GoToLine(lineNumber);
            Close();
        }
        else
        {
            ThemedMessageBox.Show("Please enter a valid line number.", "Invalid Input", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
#pragma warning restore CS8618

namespace MyCrownJewelApp.TextEditor;

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
        lineTextBox.KeyPress += LineTextBox_KeyPress;

        goToButton = new Button { Text = "&Go To", Location = new Point(90, 50), Width = 80 };
        goToButton.Click += GoToButton_Click;

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
            MessageBox.Show("Please enter a valid line number.", "Invalid Input", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

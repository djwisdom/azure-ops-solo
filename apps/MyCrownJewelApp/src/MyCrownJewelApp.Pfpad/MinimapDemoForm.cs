using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    /// <summary>
    /// Example form demonstrating basic MinimapControl integration.
    /// This is a minimal editor window with a RichTextBox and a MinimapControl docked to the right.
    /// </summary>
    public class MinimapDemoForm : Form
    {
        private RichTextBox textEditor = null!;
        private MinimapControl minimap = null!;
        private MenuStrip menuStrip = null!;
        private ToolStripMenuItem viewMenu = null!;
        private ToolStripMenuItem minimapMenuItem = null!;

        public MinimapDemoForm()
        {
            InitializeComponent();
            SetupMinimap();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.ClientSize = new Size(1000, 600);
            this.Text = "Minimap Demo - WinForms Text Editor";
            this.StartPosition = FormStartPosition.CenterScreen;

            // Menu strip
            menuStrip = new MenuStrip();
            viewMenu = new ToolStripMenuItem("&View");
            minimapMenuItem = new ToolStripMenuItem("&Minimap", null, (s, e) => ToggleMinimap());
            minimapMenuItem.Checked = true;
            minimapMenuItem.CheckOnClick = true;

            viewMenu.DropDownItems.Add(minimapMenuItem);
            menuStrip.Items.Add(viewMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // RichTextBox editor
            textEditor = new RichTextBox();
            textEditor.Dock = DockStyle.Fill;
            textEditor.Multiline = true;
            textEditor.ScrollBars = RichTextBoxScrollBars.Both;
            textEditor.Font = new Font("Consolas", 12);
            this.Controls.Add(textEditor);

            // Load sample text
            textEditor.Text = SampleText();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupMinimap()
        {
            // Create minimap control
            minimap = new MinimapControl();
            minimap.Dock = DockStyle.Right;
            minimap.MinimapWidth = 100;
            minimap.Scale = 0.5f;
            minimap.ShowColors = false; // Set true to enable syntax coloring
            minimap.ViewportColor = Color.FromArgb(80, Color.DodgerBlue);
            minimap.ViewportBorderColor = Color.DodgerBlue;

            // Attach to editor
            minimap.AttachEditor(textEditor);

            // Optional: provide a token provider if ShowColors = true and you want coloring
            // minimap.SetTokenProvider(GetTokensForLine);
            // minimap.ShowColors = true;

            // Add to form after editor so it sits on top-right
            this.Controls.Add(minimap);
            minimap.BringToFront();
        }

        private void ToggleMinimap()
        {
            // Simple show/hide by adjusting width
            if (minimapMenuItem.Checked)
            {
                minimap.Width = 100;
                minimap.Visible = true;
            }
            else
            {
                minimap.Visible = false;
            }
        }

        private string SampleText()
        {
            return @"using System;
using System.Windows.Forms;

namespace Demo
{
    public class Program
    {
        static void Main()
        {
            MessageBox.Show(""Hello, Minimap!"");
        }
    }
}
";
        }

        // Example token provider - requires linking with your syntax definitions
        // private IReadOnlyList<MyCrownJewelApp.TextEditor.TokenInfo> GetTokensForLine(int lineIndex)
        // {
        //     // Implement using the same tokenization as Form1's syntax highlighter
        //     // or a simplified regex-based tokenizer.
        //     return Array.Empty<MyCrownJewelApp.TextEditor.TokenInfo>();
        // }
    }
}

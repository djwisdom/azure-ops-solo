using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    public enum VimMode
    {
        Normal,
        Insert,
        Visual,
        VisualLine,
        VisualBlock,
        Command,
        OperatorPending
    }

    public class VimEngine
    {
        public VimMode CurrentMode { get; private set; } = VimMode.Normal;
        public bool Enabled { get; set; }

        private readonly RichTextBox _textBox;

        public VimEngine(RichTextBox textBox)
        {
            _textBox = textBox;
        }

        public void EnterMode(VimMode mode)
        {
            CurrentMode = mode;
        }

        public bool ProcessKey(Keys keyData)
        {
            // Not implemented - stub returns false to allow normal processing
            return false;
        }
    }
}

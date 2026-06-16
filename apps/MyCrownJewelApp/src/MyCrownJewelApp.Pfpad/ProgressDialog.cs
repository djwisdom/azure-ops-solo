using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    internal sealed class ProgressDialog : Form
    {
        private readonly Label _messageLabel;
        private readonly ProgressBar _progressBar;

        public ProgressDialog(string title, string message)
        {
            Text = title;
            Size = new Size(400, 120);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ShowIcon = false;

            var theme = ThemeManager.Instance.CurrentTheme;
            BackColor = theme.Background;
            ForeColor = theme.Text;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(20)
            };

            _messageLabel = new Label
            {
                Text = message,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10),
                ForeColor = theme.Text
            };

            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Dock = DockStyle.Fill,
                MarqueeAnimationSpeed = 30
            };
            _progressBar.ApplyTheme(theme);

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Size = new Size(80, 30)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            layout.Controls.Add(_messageLabel, 0, 0);
            layout.Controls.Add(_progressBar, 0, 1);
            layout.Controls.Add(cancelButton, 0, 2);

            Controls.Add(layout);
        }

        public void UpdateMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => _messageLabel.Text = message);
            }
            else
            {
                _messageLabel.Text = message;
            }
        }
    }
}
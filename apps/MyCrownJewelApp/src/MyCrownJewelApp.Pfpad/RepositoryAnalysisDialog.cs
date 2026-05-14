using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    internal sealed class RepositoryAnalysisDialog : Form
    {
        private readonly RepositoryAnalysis _analysis;

        public RepositoryAnalysisDialog(RepositoryAnalysis analysis)
        {
            _analysis = analysis;

            Text = $"🔬 Deep Repository Intelligence - {analysis.RepositoryName}";
            Size = new Size(1000, 800);
            MinimumSize = new Size(900, 700);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;

            var theme = ThemeManager.Instance.CurrentTheme;

            BackColor = theme.Background;
            ForeColor = theme.Text;

            InitializeComponents(theme);
        }

        private void InitializeComponents(Theme theme)
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(20),
                RowStyles =
                {
                    new RowStyle(SizeType.Absolute, 80),  // Header
                    new RowStyle(SizeType.Percent, 100),  // Content
                    new RowStyle(SizeType.Absolute, 60)   // Footer
                }
            };

            // Header Section
            var headerPanel = CreateHeaderPanel(theme);
            mainLayout.Controls.Add(headerPanel, 0, 0);

            // Content Section - Single comprehensive report
            var contentBox = CreateReportContent(theme);
            mainLayout.Controls.Add(contentBox, 0, 1);

            // Footer Section
            var footerPanel = CreateFooterPanel(theme);
            mainLayout.Controls.Add(footerPanel, 0, 2);

            Controls.Add(mainLayout);
        }

        private Panel CreateHeaderPanel(Theme theme)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 10)
            };

            var titleLabel = new Label
            {
                Text = $"📊 {_analysis.RepositoryName}",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = theme.Accent,
                Location = new Point(0, 0),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = $"{_analysis.ProjectType} • {_analysis.PrimaryLanguage}",
                Font = new Font("Segoe UI", 10),
                ForeColor = theme.Muted,
                Location = new Point(0, 35),
                AutoSize = true
            };

            var pathLabel = new Label
            {
                Text = $"📁 {_analysis.WorkspaceRoot}",
                Font = new Font("Segoe UI", 8),
                ForeColor = theme.Disabled,
                Location = new Point(0, 55),
                AutoSize = true,
                MaximumSize = new Size(800, 20)
            };

            panel.Controls.AddRange(new Control[] { titleLabel, subtitleLabel, pathLabel });
            return panel;
        }



        private RichTextBox CreateReportContent(Theme theme)
        {
            var contentBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = theme.Background,
                ForeColor = theme.Text,
                Font = new Font("Segoe UI", 10),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            contentBox.Text = _analysis.Summary;

            // Apply rich formatting
            ApplyDeepWikiFormatting(contentBox, theme);

            return contentBox;
        }



        private Panel CreateFooterPanel(Theme theme)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 20, 10)
            };

            var exportButton = new Button
            {
                Text = "📄 Export Report",
                Size = new Size(120, 35),
                Location = new Point(panel.Width - 280, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = theme.ButtonHoverBackground,
                ForeColor = theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 35),
                Location = new Point(panel.Width - 150, 0),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.OK,
                BackColor = theme.ButtonHoverBackground,
                ForeColor = theme.Text,
                FlatStyle = FlatStyle.Flat
            };

            exportButton.Click += (s, e) => ExportReport();
            AcceptButton = closeButton;

            panel.Controls.AddRange(new Control[] { exportButton, closeButton });
            return panel;
        }

        private void ApplyDeepWikiFormatting(RichTextBox rtb, Theme theme)
        {
            var lines = rtb.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var start = rtb.GetFirstCharIndexFromLine(i);
                var length = line.Length;

                if (line.StartsWith("# "))
                {
                    // Main title
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 18, FontStyle.Bold);
                    rtb.SelectionColor = theme.Accent;
                }
                else if (line.StartsWith("## "))
                {
                    // Section headers
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("### "))
                {
                    // Subsection headers
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("|") && line.Contains("|"))
                {
                    // Table rows
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Consolas", 9, FontStyle.Regular);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("- ") || line.Contains("•"))
                {
                    // List items
                    rtb.Select(start, 2);
                    rtb.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                    rtb.SelectionColor = theme.KeywordColor;
                }
                else if (line.Contains("**") && line.Contains("**"))
                {
                    // Bold text in markdown
                    var boldStart = line.IndexOf("**");
                    var boldEnd = line.IndexOf("**", boldStart + 2);
                    if (boldEnd > boldStart)
                    {
                        rtb.Select(start + boldStart, boldEnd - boldStart + 2);
                        rtb.SelectionFont = new Font(rtb.SelectionFont ?? new Font("Consolas", 9), FontStyle.Bold);
                        rtb.SelectionColor = theme.Accent;
                    }
                }
                else if (line.Contains("✅") || line.Contains("❌") || line.Contains("⚠️"))
                {
                    // Status indicators
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                }
                else if (line.Contains("---"))
                {
                    // Separator lines
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 8, FontStyle.Regular);
                    rtb.SelectionColor = theme.Muted;
                }
                else if (line.Contains("*Analysis generated by*"))
                {
                    // Footer
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 8, FontStyle.Italic);
                    rtb.SelectionColor = theme.Muted;
                }
            }

            rtb.Select(0, 0); // Reset selection
        }

        private void ExportReport()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Export Deep Repository Intelligence Report",
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"{_analysis.RepositoryName}-deep-analysis-{DateTime.Now:yyyyMMdd}.md"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, _analysis.Summary);
                    ThemedMessageBox.Show("Deep Intelligence Report exported successfully!", "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show($"Failed to export report: {ex.Message}", "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
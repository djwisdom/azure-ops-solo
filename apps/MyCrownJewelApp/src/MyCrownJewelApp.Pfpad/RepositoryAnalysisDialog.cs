using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad
{
    internal sealed class RepositoryAnalysisDialog : Form
    {
        private readonly RepositoryAnalysis _analysis;
        private readonly Theme _theme;
        private readonly RichTextBox _contentBox;
        private readonly Button _overviewTab;
        private readonly Button _filesTab;
        private readonly Button _dependenciesTab;
        private readonly Button _qualityTab;
        private readonly Button _gitTab;
        private readonly Panel _tabStrip;

        private enum AnalysisTab
        {
            Overview,
            Files,
            Dependencies,
            Quality,
            Git
        }

        private AnalysisTab _activeTab = AnalysisTab.Overview;

        public RepositoryAnalysisDialog(RepositoryAnalysis analysis)
        {
            _analysis = analysis;
            _theme = ThemeManager.Instance.CurrentTheme;

            Text = $"🔬 Deep Repository Intelligence - {analysis.RepositoryName}";
            FormBorderStyle = FormBorderStyle.Sizable;
            Size = new Size(960, 720);
            MinimumSize = new Size(800, 580);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowIcon = false;
            ShowInTaskbar = false;
            BackColor = _theme.Background;
            ForeColor = _theme.Text;

            _contentBox = CreateReportContent();
            _overviewTab = CreateTabButton("Overview", AnalysisTab.Overview, 0, active: true);
            _filesTab = CreateTabButton("Files", AnalysisTab.Files, _overviewTab.Width, active: false);
            _dependenciesTab = CreateTabButton("Dependencies", AnalysisTab.Dependencies, _overviewTab.Width + _filesTab.Width, active: false);
            _qualityTab = CreateTabButton("Quality", AnalysisTab.Quality, _overviewTab.Width + _filesTab.Width + _dependenciesTab.Width, active: false);
            _gitTab = CreateTabButton("Git", AnalysisTab.Git, _overviewTab.Width + _filesTab.Width + _dependenciesTab.Width + _qualityTab.Width, active: false);
            _tabStrip = CreateTabStrip();

            InitializeComponents();
            RenderActiveSection();

            Load += (_, _) => NativeThemed.ApplyThemeToChildScrollbars(this, !_theme.IsLight);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_theme.IsLight)
                NativeThemed.ApplyDarkModeToWindow(Handle);
        }

        private void InitializeComponents()
        {
            Controls.Add(CreateContentHost());
            Controls.Add(CreateFooterBar());
            Controls.Add(_tabStrip);
            Controls.Add(CreateHeaderPanel());
        }

        private Panel CreateHeaderPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(18, 14, 18, 12)
            };
            panel.Paint += (_, e) =>
            {
                using var brush = new LinearGradientBrush(
                    panel.ClientRectangle,
                    Blend(_theme.PanelBackground, _theme.Background, 0.12f),
                    _theme.PanelBackground,
                    LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(brush, panel.ClientRectangle);
                using var pen = new Pen(_theme.Border);
                e.Graphics.DrawLine(pen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            };

            var metricsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 330,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };
            metricsPanel.Controls.Add(CreateMetricCard("Files", _analysis.TotalFiles.ToString("N0"), "Total repository files"));
            metricsPanel.Controls.Add(CreateMetricCard("Languages", GetLanguageMetric().ToString("N0"), "Inferred from extensions"));
            metricsPanel.Controls.Add(CreateMetricCard("Code Files", _analysis.CodeFiles.ToString("N0"), "Tracked code documents"));

            var infoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 20, 0)
            };

            var pathLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = $"📁 {_analysis.WorkspaceRoot}",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = _theme.Disabled,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var subtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = $"{_analysis.ProjectType} • {_analysis.PrimaryLanguage}",
                Font = new Font("Segoe UI", 10f),
                ForeColor = _theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = _analysis.RepositoryName,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = _theme.Accent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            infoPanel.Controls.Add(pathLabel);
            infoPanel.Controls.Add(subtitleLabel);
            infoPanel.Controls.Add(titleLabel);

            panel.Controls.Add(infoPanel);
            panel.Controls.Add(metricsPanel);
            return panel;
        }

        private Panel CreateTabStrip()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = _theme.MenuBackground
            };
            panel.Controls.AddRange(new Control[] { _overviewTab, _filesTab, _dependenciesTab, _qualityTab, _gitTab });
            panel.Paint += (_, e) =>
            {
                using var borderPen = new Pen(_theme.Border);
                e.Graphics.DrawLine(borderPen, 0, panel.Height - 1, panel.Width, panel.Height - 1);

                var activeButton = GetActiveTabButton();
                using var accentPen = new Pen(_theme.Accent, 2);
                int y = panel.Height - 2;
                e.Graphics.DrawLine(accentPen, activeButton.Left, y, activeButton.Right, y);
            };
            return panel;
        }

        private Control CreateContentHost()
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 16, 16, 12),
                BackColor = _theme.Background
            };

            var borderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(1),
                BackColor = _theme.Border
            };

            var innerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                BackColor = _theme.EditorBackground
            };

            innerPanel.Controls.Add(_contentBox);
            borderPanel.Controls.Add(innerPanel);
            host.Controls.Add(borderPanel);
            return host;
        }

        private RichTextBox CreateReportContent()
        {
            return new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = true,
                BorderStyle = BorderStyle.None,
                BackColor = _theme.EditorBackground,
                ForeColor = _theme.Text,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                HideSelection = false,
                Margin = new Padding(0)
            };
        }

        private Panel CreateFooterBar()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(16, 6, 16, 6),
                BackColor = _theme.PanelBackground
            };
            panel.Paint += (_, e) =>
            {
                using var pen = new Pen(_theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent
            };

            var exportButton = CreateActionButton("📄 Export Markdown", 150);
            exportButton.Click += (_, _) => ExportReport();

            var copyButton = CreateActionButton("📋 Copy", 100);
            copyButton.Click += (_, _) => Clipboard.SetText(_analysis.Summary ?? string.Empty);

            var closeButton = CreateActionButton("Close", 90);
            closeButton.DialogResult = DialogResult.OK;
            AcceptButton = closeButton;

            buttons.Controls.Add(exportButton);
            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(closeButton);
            panel.Controls.Add(buttons);
            return panel;
        }

        private Button CreateActionButton(string text, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = _theme.PanelBackground,
                ForeColor = _theme.Text,
                Cursor = Cursors.Hand,
                TabStop = true
            };
            button.FlatAppearance.BorderColor = _theme.Border;
            button.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
            button.FlatAppearance.MouseDownBackColor = Blend(_theme.ButtonHoverBackground, _theme.Accent, 0.15f);
            return button;
        }

        private Button CreateTabButton(string text, AnalysisTab tab, int left, bool active)
        {
            var button = new Button
            {
                Text = text,
                Left = left,
                Top = 0,
                Width = tab == AnalysisTab.Dependencies ? 120 : 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = active ? _theme.Text : _theme.Muted,
                Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.Transparent;
            button.Click += (_, _) => SwitchTab(tab);
            return button;
        }

        private MetricCardPanel CreateMetricCard(string title, string value, string subtitle)
        {
            var card = new MetricCardPanel(_theme)
            {
                Size = new Size(102, 58),
                Margin = new Padding(8, 10, 0, 0)
            };

            var subtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 16,
                Text = subtitle,
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = _theme.Disabled,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            var valueLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = value,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = _theme.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var titleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 16,
                Text = title,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = _theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            card.Controls.Add(subtitleLabel);
            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private int GetLanguageMetric()
        {
            if (_analysis.FileExtensions.Count == 0)
                return string.IsNullOrWhiteSpace(_analysis.PrimaryLanguage) ? 0 : 1;

            return _analysis.FileExtensions.Keys
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Select(ext => ext.Trim().TrimStart('.'))
                .Where(ext => ext.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private void SwitchTab(AnalysisTab tab)
        {
            if (_activeTab == tab)
                return;

            _activeTab = tab;
            UpdateTabState();
            RenderActiveSection();
            _tabStrip.Invalidate();
        }

        private void UpdateTabState()
        {
            SetTabActive(_overviewTab, _activeTab == AnalysisTab.Overview);
            SetTabActive(_filesTab, _activeTab == AnalysisTab.Files);
            SetTabActive(_dependenciesTab, _activeTab == AnalysisTab.Dependencies);
            SetTabActive(_qualityTab, _activeTab == AnalysisTab.Quality);
            SetTabActive(_gitTab, _activeTab == AnalysisTab.Git);
        }

        private void SetTabActive(Button button, bool active)
        {
            button.ForeColor = active ? _theme.Text : _theme.Muted;
            button.Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular);
        }

        private Button GetActiveTabButton() => _activeTab switch
        {
            AnalysisTab.Files => _filesTab,
            AnalysisTab.Dependencies => _dependenciesTab,
            AnalysisTab.Quality => _qualityTab,
            AnalysisTab.Git => _gitTab,
            _ => _overviewTab
        };

        private void RenderActiveSection()
        {
            _contentBox.Clear();
            _contentBox.Text = GetFilteredSummary(_activeTab);
            ApplyDeepWikiFormatting(_contentBox, _theme);
            _contentBox.Select(0, 0);
            _contentBox.ScrollToCaret();
        }

        private string GetFilteredSummary(AnalysisTab tab)
        {
            string summary = _analysis.Summary ?? string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
                return "No analysis summary is available.";

            var lines = summary.Replace("\r\n", "\n").Split('\n');
            var preamble = new List<string>();
            var sections = new List<(string Heading, List<string> Lines)>();
            List<string>? currentSectionLines = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    currentSectionLines = new List<string> { line };
                    sections.Add((line[3..].Trim(), currentSectionLines));
                    continue;
                }

                if (currentSectionLines == null)
                    preamble.Add(line);
                else
                    currentSectionLines.Add(line);
            }

            string[] keywords = tab switch
            {
                AnalysisTab.Files => new[] { "files", "file" },
                AnalysisTab.Dependencies => new[] { "depend", "package" },
                AnalysisTab.Quality => new[] { "quality", "health", "issue" },
                AnalysisTab.Git => new[] { "git", "commit", "branch" },
                _ => new[] { "overview", "summary" }
            };

            var matches = sections
                .Where(section => keywords.Any(keyword => section.Heading.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (tab == AnalysisTab.Overview)
            {
                if (matches.Count == 0)
                    return summary;

                var builder = new StringBuilder();
                AppendLines(builder, preamble);
                foreach (var match in matches)
                    AppendSection(builder, match.Lines);
                return builder.ToString().Trim();
            }

            if (matches.Count == 0)
                return summary;

            var filtered = new StringBuilder();
            foreach (var match in matches)
                AppendSection(filtered, match.Lines);
            return filtered.ToString().Trim();
        }

        private static void AppendLines(StringBuilder builder, IEnumerable<string> lines)
        {
            foreach (var line in lines)
                builder.AppendLine(line);
        }

        private static void AppendSection(StringBuilder builder, IEnumerable<string> lines)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            foreach (var line in lines)
                builder.AppendLine(line);
        }

        private void ApplyDeepWikiFormatting(RichTextBox rtb, Theme theme)
        {
            var lines = rtb.Lines;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var start = rtb.GetFirstCharIndexFromLine(i);
                var length = line.Length;

                if (length == 0 || start < 0)
                    continue;

                if (line.StartsWith("# "))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 18, FontStyle.Bold);
                    rtb.SelectionColor = theme.Accent;
                }
                else if (line.StartsWith("## "))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("### "))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 12, FontStyle.Bold);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("|") && line.Contains("|"))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Consolas", 9, FontStyle.Regular);
                    rtb.SelectionColor = theme.Text;
                }
                else if (line.StartsWith("- ") || line.Contains("•"))
                {
                    int bulletLength = Math.Min(2, length);
                    rtb.Select(start, bulletLength);
                    rtb.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
                    rtb.SelectionColor = theme.KeywordColor;
                }
                else if (line.Contains("**", StringComparison.Ordinal))
                {
                    int boldStart = line.IndexOf("**", StringComparison.Ordinal);
                    int boldEnd = line.IndexOf("**", boldStart + 2, StringComparison.Ordinal);
                    if (boldStart >= 0 && boldEnd > boldStart)
                    {
                        rtb.Select(start + boldStart, boldEnd - boldStart + 2);
                        rtb.SelectionFont = new Font(rtb.SelectionFont ?? new Font("Consolas", 9), FontStyle.Bold);
                        rtb.SelectionColor = theme.Accent;
                    }
                }
                else if (line.Contains("✅") || line.Contains("❌") || line.Contains("⚠️"))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 10, FontStyle.Regular);
                }
                else if (line.Contains("---", StringComparison.Ordinal))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 8, FontStyle.Regular);
                    rtb.SelectionColor = theme.Muted;
                }
                else if (line.Contains("*Analysis generated by*", StringComparison.OrdinalIgnoreCase))
                {
                    rtb.Select(start, length);
                    rtb.SelectionFont = new Font("Segoe UI", 8, FontStyle.Italic);
                    rtb.SelectionColor = theme.Muted;
                }
            }

            rtb.Select(0, 0);
        }

        private void ExportReport()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Export Deep Repository Intelligence Report",
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"{_analysis.RepositoryName}-deep-analysis-{DateTime.Now:yyyyMMdd}.md"
            };

            if (sfd.ShowThemed() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, _analysis.Summary);
                    ThemedMessageBox.Show(this, "Deep Intelligence Report exported successfully!", "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    ThemedMessageBox.Show(this, $"Failed to export report: {ex.Message}", "Export Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static Color Blend(Color baseColor, Color mixColor, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = (int)(baseColor.R + ((mixColor.R - baseColor.R) * amount));
            int g = (int)(baseColor.G + ((mixColor.G - baseColor.G) * amount));
            int b = (int)(baseColor.B + ((mixColor.B - baseColor.B) * amount));
            return Color.FromArgb(r, g, b);
        }

        private sealed class MetricCardPanel : Panel
        {
            private readonly Theme _theme;

            public MetricCardPanel(Theme theme)
            {
                _theme = theme;
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Padding = new Padding(10, 8, 10, 8);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = CreateRoundedRect(ClientRectangle, 10);
                using var fill = new SolidBrush(Blend(_theme.EditorBackground, _theme.PanelBackground, 0.35f));
                using var pen = new Pen(Blend(_theme.Border, _theme.Accent, 0.18f));
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(pen, path);
            }

            private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                int diameter = radius * 2;
                var arc = new Rectangle(rect.Location, new Size(diameter, diameter));
                path.AddArc(arc, 180, 90);
                arc.X = rect.Right - diameter - 1;
                path.AddArc(arc, 270, 90);
                arc.Y = rect.Bottom - diameter - 1;
                path.AddArc(arc, 0, 90);
                arc.X = rect.Left;
                path.AddArc(arc, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}

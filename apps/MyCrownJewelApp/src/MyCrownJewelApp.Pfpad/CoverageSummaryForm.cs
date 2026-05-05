using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class CoverageSummaryForm : Form
{
    private readonly ListView _fileList;
    private readonly Label _summaryLabel;

    public CoverageSummaryForm(CoverageParser.CoverageResult result)
    {
        Text = "Code Coverage Summary";
        Size = new Size(600, 400);
        MinimumSize = new Size(400, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        double pct = result.LinesValid > 0 ? (double)result.LinesCovered / result.LinesValid * 100 : 0;
        _summaryLabel = new Label
        {
            Text = $"Coverage: {pct:F1}%  ({result.LinesCovered}/{result.LinesValid} lines)   Branch rate: {result.BranchRate:P1}",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = pct >= 80 ? Color.FromArgb(80, 200, 80) : pct >= 50 ? Color.FromArgb(220, 200, 50) : Color.FromArgb(220, 60, 60)
        };

        _fileList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            MultiSelect = false
        };
        _fileList.Columns.Add("File", 300);
        _fileList.Columns.Add("Coverage", 80);
        _fileList.Columns.Add("Covered", 60);
        _fileList.Columns.Add("Total", 50);
        _fileList.Columns.Add("Status", 60);

        _fileList.BeginUpdate();
        foreach (var kvp in result.FileLineHits)
        {
            int covered = 0, total = 0;
            foreach (int hits in kvp.Value.Values)
            {
                total++;
                if (hits > 0) covered++;
            }
            double fpct = total > 0 ? (double)covered / total * 100 : 0;
            Color fg = fpct >= 80 ? Color.FromArgb(80, 200, 80) : fpct >= 50 ? Color.FromArgb(220, 200, 50) : Color.FromArgb(220, 60, 60);
            var item = new ListViewItem(new[] {
                kvp.Key,
                $"{fpct:F0}%",
                covered.ToString(),
                total.ToString(),
                covered == total ? "\u2713" : "\u2717"
            })
            {
                Tag = kvp.Key,
                ForeColor = fg
            };
            _fileList.Items.Add(item);
        }
        _fileList.EndUpdate();

        var closeBtn = new Button
        {
            Text = "Close",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Bottom,
            Size = new Size(80, 28),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Muted },
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => Close();

        Controls.Add(_fileList);
        Controls.Add(_summaryLabel);
        Controls.Add(closeBtn);
    }

    public event Action<string>? FileSelected;

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (_fileList.SelectedItems.Count > 0 && _fileList.SelectedItems[0].Tag is string file)
            FileSelected?.Invoke(file);
    }
}

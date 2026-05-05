using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class ImpactAnalysisDialog : Form
{
    private readonly ListView _fileList;
    private readonly Label _statusLabel;

    public ImpactAnalysisDialog(string targetFile, List<string> affectedFiles)
    {
        Text = $"Impact Analysis: {Path.GetFileName(targetFile)}";
        Size = new Size(700, 400);
        MinimumSize = new Size(400, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _statusLabel = new Label
        {
            Text = $"{affectedFiles.Count} file(s) affected by changes to {Path.GetFileName(targetFile)}",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Muted
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
        _fileList.Columns.Add("File", 400);
        _fileList.Columns.Add("Project", 200);
        _fileList.DoubleClick += (s, e) => OpenSelected();
        _fileList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSelected(); };

        _fileList.BeginUpdate();
        foreach (var file in affectedFiles)
        {
            string projName = GuessProject(file);
            var item = new ListViewItem(new[] { file, projName })
            {
                Tag = file,
                ForeColor = theme.Text
            };
            _fileList.Items.Add(item);
        }
        _fileList.EndUpdate();

        if (affectedFiles.Count == 0)
        {
            _statusLabel.Text = $"No affected files found for {Path.GetFileName(targetFile)}.";
        }

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
        Controls.Add(_statusLabel);
        Controls.Add(closeBtn);
    }

    public event Action<string>? FileSelected;

    private void OpenSelected()
    {
        if (_fileList.SelectedItems.Count > 0 && _fileList.SelectedItems[0].Tag is string file)
            FileSelected?.Invoke(file);
    }

    private static string GuessProject(string filePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(filePath) ?? "");
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.csproj").Any())
                return Path.GetFileNameWithoutExtension(dir.EnumerateFiles("*.csproj").First().Name);
            dir = dir.Parent;
        }
        return "(unknown)";
    }
}

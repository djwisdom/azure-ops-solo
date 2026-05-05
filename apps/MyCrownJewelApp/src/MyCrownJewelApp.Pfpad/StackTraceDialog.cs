using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class StackTraceDialog : Form
{
    private readonly ListView _frameList;
    private readonly Label _statusLabel;
    private readonly List<(string file, int line, string method)> _frames;

    public StackTraceDialog(string stackText)
    {
        _frames = StackTraceParser.Parse(stackText);

        Text = $"Stack Trace — {_frames.Count} frame(s)";
        Size = new Size(750, 400);
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
            Text = $"{_frames.Count} stack frame(s) parsed. Double-click to navigate.",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Muted
        };

        _frameList = new ListView
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
        _frameList.Columns.Add("Method", 160);
        _frameList.Columns.Add("File", 350);
        _frameList.Columns.Add("Line", 60);
        _frameList.Columns.Add("Exists", 50);
        _frameList.DoubleClick += FrameList_DoubleClick;
        _frameList.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) NavigateSelected(); };

        // Source text at bottom (collapsible)
        var sourceBox = new TextBox
        {
            Text = stackText,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Populate frames
        _frameList.BeginUpdate();
        foreach (var (file, line, method) in _frames)
        {
            bool exists = File.Exists(file);
            var item = new ListViewItem(new[]
            {
                method,
                file,
                line.ToString(),
                exists ? "\u2713" : "\u2717"
            })
            {
                Tag = (file, line),
                ForeColor = exists ? theme.Text : theme.Muted
            };
            _frameList.Items.Add(item);
        }
        _frameList.EndUpdate();

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

        Controls.Add(_frameList);
        Controls.Add(_statusLabel);
        Controls.Add(sourceBox);
        Controls.Add(closeBtn);
    }

    public event Action<string, int>? FrameSelected;

    private void FrameList_DoubleClick(object? sender, EventArgs e)
    {
        NavigateSelected();
    }

    private void NavigateSelected()
    {
        if (_frameList.SelectedItems.Count == 0) return;
        var tag = ((string file, int line))_frameList.SelectedItems[0].Tag!;
        FrameSelected?.Invoke(tag.file, tag.line);
    }
}

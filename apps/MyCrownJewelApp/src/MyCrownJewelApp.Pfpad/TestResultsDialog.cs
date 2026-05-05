using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class TestResultsDialog : Form
{
    private readonly Label _summaryLabel;
    private readonly ListBox _testList;
    private readonly RichTextBox _detailBox;
    private readonly Button _runButton;
    private readonly Button _rerunFailedButton;
    private readonly Button _closeButton;
    private readonly TestRunResult _result;

    public event Action<string, int>? FrameSelected;

    public TestResultsDialog(TestRunResult result)
    {
        _result = result;
        Text = "Test Results";
        Size = new Size(750, 550);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        // --- Summary label ---
        string colorTag = result.Failed > 0 ? "Failed" : result.Passed > 0 ? "Passed" : "No tests";
        _summaryLabel = new Label
        {
            Text = $"{colorTag}: {result.Passed} passed, {result.Failed} failed, {result.Skipped} skipped — {result.Total} total",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = result.Failed > 0 ? Color.FromArgb(220, 60, 60)
                : result.Passed > 0 ? Color.FromArgb(80, 200, 80)
                : theme.Muted
        };

        // --- Split: test list (left) + detail (right) ---
        _testList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawVariable,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            ItemHeight = 24,
            Cursor = Cursors.Hand,
            Width = 300
        };
        _testList.DrawItem += TestList_DrawItem;
        _testList.MeasureItem += (s, e) => e.ItemHeight = 24;
        _testList.SelectedIndexChanged += TestList_SelectedIndexChanged;
        _testList.HandleCreated += (s, e) => ApplyScrollbarTheme(_testList.Handle);

        _detailBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            Font = new Font("Consolas", 9),
            WordWrap = false
        };
        _detailBox.LinkClicked += DetailBox_LinkClicked;
        _detailBox.MouseDoubleClick += DetailBox_MouseDoubleClick;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 3,
            SplitterDistance = 300
        };
        split.Panel1.Controls.Add(_testList);
        split.Panel2.Controls.Add(_detailBox);

        // --- Buttons ---
        _runButton = new Button
        {
            Text = "\u25B6 Run All",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(100, 28),
            Cursor = Cursors.Hand
        };
        _runButton.Enabled = false;

        _rerunFailedButton = new Button
        {
            Text = "\u21BA Rerun Failed",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Size = new Size(110, 28),
            Cursor = Cursors.Hand,
            Enabled = result.Failed > 0
        };
        _rerunFailedButton.Enabled = false;

        _closeButton = new Button
        {
            Text = "Close",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Size = new Size(80, 28),
            Cursor = Cursors.Hand
        };
        _closeButton.Click += (s, e) => Close();

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(6),
            Height = 40,
            BackColor = theme.Background
        };
        btnPanel.Controls.Add(_closeButton);
        btnPanel.Controls.Add(_rerunFailedButton);
        btnPanel.Controls.Add(_runButton);

        Controls.Add(split);
        Controls.Add(_summaryLabel);
        Controls.Add(btnPanel);

        PopulateList();
        ApplyTheme(theme);
    }

    private void PopulateList()
    {
        _testList.BeginUpdate();
        _testList.Items.Clear();
        foreach (var t in _result.Tests)
            _testList.Items.Add(t);
        _testList.EndUpdate();

        if (_testList.Items.Count > 0)
            _testList.SelectedIndex = 0;
    }

    private void TestList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _testList.Items.Count) return;
        if (_testList.Items[e.Index] is not TestResult t) return;

        var theme = ThemeManager.Instance.CurrentTheme;
        var rect = e.Bounds;
        bool isSelected = (e.State & DrawItemState.Selected) != 0;

        using var bg = new SolidBrush(isSelected ? theme.ButtonHoverBackground : theme.MenuBackground);
        e.Graphics.FillRectangle(bg, rect);

        // Outcome badge
        Color badgeColor = t.Outcome switch
        {
            TestOutcome.Passed => Color.FromArgb(80, 200, 80),
            TestOutcome.Failed => Color.FromArgb(220, 60, 60),
            TestOutcome.Skipped => Color.FromArgb(180, 180, 50),
            _ => theme.Muted
        };
        string badge = t.Outcome switch
        {
            TestOutcome.Passed => "\u2713",
            TestOutcome.Failed => "\u2717",
            TestOutcome.Skipped => "\u25CB",
            _ => "?"
        };

        var badgeRect = new Rectangle(rect.X + 4, rect.Y + 4, 16, 16);
        using var badgeBrush = new SolidBrush(badgeColor);
        e.Graphics.FillEllipse(badgeBrush, badgeRect);
        using var badgeFont = new Font("Segoe UI", 8, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, badge, badgeFont, badgeRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Test name
        using var nameFont = new Font("Segoe UI", 8.5f);
        TextRenderer.DrawText(e.Graphics, t.TestName, nameFont,
            new Rectangle(rect.X + 24, rect.Y + 2, rect.Width - 100, 18),
            theme.Text, TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);

        // Duration
        string dur = t.DurationMs >= 1000 ? $"{t.DurationMs / 1000.0:F1}s" : $"{t.DurationMs}ms";
        using var durFont = new Font("Segoe UI", 7.5f);
        int durWidth = TextRenderer.MeasureText(dur, durFont).Width;
        TextRenderer.DrawText(e.Graphics, dur, durFont,
            new Point(rect.Right - durWidth - 4, rect.Y + 2),
            theme.Muted, TextFormatFlags.NoPadding);
    }

    private void TestList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_testList.SelectedItem is not TestResult t) return;

        _detailBox.Clear();
        _detailBox.ForeColor = ThemeManager.Instance.CurrentTheme.Text;
        _detailBox.BackColor = ThemeManager.Instance.CurrentTheme.EditorBackground;

        var theme = ThemeManager.Instance.CurrentTheme;

        // Header
        _detailBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Bold);
        _detailBox.AppendText($"{t.TestName}\n\n");

        // Outcome + duration
        _detailBox.SelectionFont = new Font("Consolas", 9);
        _detailBox.SelectionColor = t.Outcome switch
        {
            TestOutcome.Passed => Color.FromArgb(80, 200, 80),
            TestOutcome.Failed => Color.FromArgb(220, 60, 60),
            TestOutcome.Skipped => Color.FromArgb(180, 180, 50),
            _ => theme.Text
        };
        _detailBox.AppendText($"[{t.Outcome}]  {t.DurationMs}ms\n\n");

        // Error message
        if (!string.IsNullOrEmpty(t.ErrorMessage))
        {
            _detailBox.SelectionFont = new Font("Consolas", 9, FontStyle.Bold);
            _detailBox.SelectionColor = Color.FromArgb(220, 60, 60);
            _detailBox.AppendText("Error:\n");
            _detailBox.SelectionFont = new Font("Consolas", 9);
            _detailBox.SelectionColor = theme.Text;
            _detailBox.AppendText($"{t.ErrorMessage}\n\n");
        }

        // Stack trace
        if (!string.IsNullOrEmpty(t.StackTrace))
        {
            _detailBox.SelectionFont = new Font("Consolas", 9, FontStyle.Bold);
            _detailBox.SelectionColor = theme.Muted;
            _detailBox.AppendText("Stack Trace:\n");
            _detailBox.SelectionFont = new Font("Consolas", 8.5f);
            _detailBox.SelectionColor = theme.Muted;
            _detailBox.AppendText(t.StackTrace);
        }
    }

    private void DetailBox_LinkClicked(object? sender, LinkClickedEventArgs e)
    {
        // Handle file:line links
        var parts = e.LinkText.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out int line))
            FrameSelected?.Invoke(parts[0], line);
    }

    private void DetailBox_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        // Detect file:line under cursor and navigate
        int charIdx = _detailBox.GetCharIndexFromPosition(e.Location);
        int line = _detailBox.GetLineFromCharIndex(charIdx);
        string lineText = _detailBox.Lines[line];

        var m = System.Text.RegularExpressions.Regex.Match(lineText, @"([A-Za-z]:\\[^\s:]+):(\d+)");
        if (m.Success)
        {
            string file = m.Groups[1].Value;
            if (int.TryParse(m.Groups[2].Value, out int ln))
                FrameSelected?.Invoke(file, ln);
        }
    }

    public void UpdateResult(TestRunResult result)
    {
        PopulateList();
        _rerunFailedButton.Enabled = result.Failed > 0;
        // Update summary
    }

    private void ApplyTheme(Theme theme)
    {
        _testList.BackColor = theme.MenuBackground;
        _testList.ForeColor = theme.Text;
        _detailBox.BackColor = theme.EditorBackground;
        _detailBox.ForeColor = theme.Text;
        _runButton.BackColor = theme.Accent;
        _runButton.ForeColor = Color.White;
        _runButton.FlatAppearance.BorderSize = 0;
        _rerunFailedButton.BackColor = theme.Background;
        _rerunFailedButton.ForeColor = theme.Text;
        _rerunFailedButton.FlatAppearance.BorderColor = theme.Muted;
        _closeButton.BackColor = theme.Background;
        _closeButton.ForeColor = theme.Text;
        _closeButton.FlatAppearance.BorderColor = theme.Muted;
        ApplyScrollbarTheme(_testList.Handle);
    }

    private void ApplyScrollbarTheme(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        var theme = ThemeManager.Instance.CurrentTheme;
        SetWindowTheme(handle, !theme.IsLight ? "DarkMode_Explorer" : null, null);
    }

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);
}

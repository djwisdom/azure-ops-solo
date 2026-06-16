using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;

namespace MyCrownJewelApp.Pfpad;

public sealed class QuickOpenDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private readonly List<QuickOpenItem> _allItems;
    private List<QuickOpenItem> _filtered;

    public sealed record QuickOpenItem(string Display, string Type, string? Detail, Action Execute);

    public QuickOpenDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _allItems = new List<QuickOpenItem>();
        _filtered = new List<QuickOpenItem>();

        // Load instant items first
        LoadInstantItems();

        var theme = ThemeManager.Instance.CurrentTheme;

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = theme.MenuBackground;
        ClientSize = new Size(600, 500);
        ShowInTaskbar = false;
        TopMost = true;

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 30,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 11),
        };
        _searchBox.TextChanged += OnSearchChanged;
        _searchBox.KeyDown += OnSearchKeyDown;

        _resultsList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.FromArgb(220, 220, 220),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10),
            IntegralHeight = false,
            ItemHeight = 48,
        };
        _resultsList.DrawMode = DrawMode.OwnerDrawFixed;
        _resultsList.DrawItem += OnDrawItem;
        _resultsList.MouseDoubleClick += (_, _) => ExecuteSelected();
        _resultsList.PreviewKeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.IsInputKey = true; }
        };
        _resultsList.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { ExecuteSelected(); e.Handled = true; }
        };

        var searchBoxWrapper = FlatUiHelper.WrapFlat(_searchBox, theme);
        searchBoxWrapper.Dock = DockStyle.Top;
        searchBoxWrapper.Height = 30;

        Controls.Add(_resultsList);
        Controls.Add(searchBoxWrapper);

        PopulateList();

        // Load files asynchronously
        Task.Run(() => LoadFilesAsync());

        // Escape to close
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }

    private void LoadInstantItems()
    {
        // Commands
        var commands = _mainForm.GetCommandPaletteCommands();
        foreach (var cmd in commands)
        {
            _allItems.Add(new QuickOpenItem(cmd.Name, "command", cmd.Shortcut, cmd.Execute));
        }

        // Buffers (open tabs)
        foreach (var tab in _mainForm.GetOpenTabs())
        {
            _allItems.Add(new QuickOpenItem(tab.Title, "buffer", tab.FilePath, tab.Switch));
        }

        // Lines in current file (if open)
        var currentDoc = _mainForm.GetCurrentDocument();
        if (currentDoc != null)
        {
            var lines = currentDoc.Content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string preview = lines[i].Trim();
                if (preview.Length > 50) preview = preview.Substring(0, 47) + "...";
                _allItems.Add(new QuickOpenItem($"Line {i + 1}: {preview}", "line", currentDoc.FilePath, () => _mainForm.GoToLine(i + 1)));
            }
        }

        _filtered = _allItems;
    }

        private async Task LoadFilesAsync()
        {
            if (string.IsNullOrEmpty(_mainForm.WorkspaceRoot) || !Directory.Exists(_mainForm.WorkspaceRoot))
                return;

            var files = await Task.Run(() =>
                Directory.GetFiles(_mainForm.WorkspaceRoot, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("\\.git\\") && !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                    .Select(f => Path.GetRelativePath(_mainForm.WorkspaceRoot, f).Replace('\\', '/'))
                    .OrderBy(f => f)
                    .ToList()
            );

            // Add files in batches to avoid UI thread flooding
            const int batchSize = 100;
            for (int i = 0; i < files.Count; i += batchSize)
            {
                var batch = files.Skip(i).Take(batchSize).ToList();
                await Task.Delay(1); // Yield to keep UI responsive
                BeginInvoke(() =>
                {
                    foreach (var file in batch)
                    {
                        _allItems.Add(new QuickOpenItem(file, "file", null, () => _mainForm.OpenFileInNewTabAsync(Path.Combine(_mainForm.WorkspaceRoot, file))));
                    }
                    // Re-filter if there's current search
                    if (i + batchSize >= files.Count) // Only on last batch
                        OnSearchChanged(null, EventArgs.Empty);
                });
            }
        }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _searchBox.Focus();
    }

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        string query = _searchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            _filtered = _allItems;
        }
        else
        {
            _filtered = _allItems
                .Select(item => new { Item = item, Score = FuzzyScore(item.Display, query) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Display.Length)
                .Select(x => x.Item)
                .ToList();
        }
        PopulateList();
    }

    private void PopulateList()
    {
        _resultsList.BeginUpdate();
        _resultsList.Items.Clear();
        foreach (var item in _filtered)
            _resultsList.Items.Add(item);
        _resultsList.EndUpdate();
        if (_resultsList.Items.Count > 0)
            _resultsList.SelectedIndex = 0;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            if (_resultsList.SelectedIndex < _resultsList.Items.Count - 1)
                _resultsList.SelectedIndex++;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_resultsList.SelectedIndex > 0)
                _resultsList.SelectedIndex--;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ExecuteSelected()
    {
        if (_resultsList.SelectedItem is QuickOpenItem item)
        {
            Close();
            item.Execute();
        }
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _resultsList.Items.Count) return;
        var item = (QuickOpenItem)_resultsList.Items[e.Index];

        e.DrawBackground();

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color textColor = selected ? Color.White : Color.FromArgb(200, 200, 200);
        Color typeColor = selected ? Color.FromArgb(150, 150, 150) : Color.FromArgb(100, 100, 100);

        if (selected)
        {
            using var bgBrush = new SolidBrush(Color.FromArgb(60, 100, 180));
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        using var nameFont = new Font("Segoe UI", 10);
        using var detailFont = new Font("Consolas", 9);
        using var nameBrush = new SolidBrush(textColor);
        using var typeBrush = new SolidBrush(typeColor);

        var nameBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, e.Bounds.Width - 170, 20);
        e.Graphics.DrawString(item.Display, nameFont, nameBrush, nameBounds);

        string typeText = $"[{item.Type}]";
        var typeBounds = new Rectangle(e.Bounds.Right - 80, e.Bounds.Y + 4, 70, 20);
        e.Graphics.DrawString(typeText, detailFont, typeBrush, typeBounds);

        if (!string.IsNullOrEmpty(item.Detail))
        {
            var detailBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 24, e.Bounds.Width - 170, 20);
            e.Graphics.DrawString(item.Detail, detailFont, typeBrush, detailBounds);
        }

        e.DrawFocusRectangle();
    }

    // Simple fuzzy scoring: sum of positions of consecutive matches, bonus for start
    private static int FuzzyScore(string text, string query)
    {
        if (string.IsNullOrEmpty(query)) return 1000;
        text = text.ToLowerInvariant();
        query = query.ToLowerInvariant();

        int score = 0;
        int textIndex = 0;
        foreach (char c in query)
        {
            int pos = text.IndexOf(c, textIndex);
            if (pos < 0) return 0;
            score += 1000 - pos; // Higher score for earlier matches
            if (pos == textIndex) score += 100; // Bonus for consecutive
            textIndex = pos + 1;
        }
        return score;
    }
}
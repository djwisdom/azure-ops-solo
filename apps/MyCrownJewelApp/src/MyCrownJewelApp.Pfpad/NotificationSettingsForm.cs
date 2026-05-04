using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class NotificationSettingsForm : Form
{
    private readonly NotificationFeedService _feed;
    private readonly DataGridView _grid;
    private readonly Button _addBtn;
    private readonly Button _removeBtn;
    private Theme _theme;

    public NotificationSettingsForm(NotificationFeedService feed)
    {
        _feed = feed;
        _theme = ThemeManager.Instance.CurrentTheme;

        Text = "Notification Feed Settings";
        Size = new Size(720, 480);
        MinimumSize = new Size(600, 350);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        ShowIcon = false;

        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        // Toolbar
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = _theme.PanelBackground,
            Padding = new Padding(10, 6, 10, 4)
        };

        _addBtn = new Button
        {
            Text = "+ Add",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = _theme.Muted },
            BackColor = _theme.Background,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            Size = new Size(80, 28),
            Location = new Point(0, 0),
            Cursor = Cursors.Hand
        };
        _addBtn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        _addBtn.Click += AddFeed;
        toolbar.Controls.Add(_addBtn);

        _removeBtn = new Button
        {
            Text = "Remove",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = _theme.Muted },
            BackColor = _theme.Background,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            Size = new Size(80, 28),
            Location = new Point(90, 0),
            Cursor = Cursors.Hand
        };
        _removeBtn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        _removeBtn.Click += RemoveFeed;
        toolbar.Controls.Add(_removeBtn);

        // Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            BackgroundColor = _theme.Background,
            ForeColor = _theme.Text,
            GridColor = _theme.Border,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _grid.CellPainting += Grid_CellPainting;
        _grid.ColumnHeaderMouseClick += (s, e) => { };

        // Columns
        var enabledCol = new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "On",
            Width = 40,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };
        var labelCol = new DataGridViewTextBoxColumn
        {
            Name = "label",
            HeaderText = "Label",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var urlCol = new DataGridViewTextBoxColumn
        {
            Name = "url",
            HeaderText = "Feed URL",
            Width = 280,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var intervalCol = new DataGridViewTextBoxColumn
        {
            Name = "interval",
            HeaderText = "Interval (min)",
            Width = 100,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var sourceCol = new DataGridViewTextBoxColumn
        {
            Name = "source",
            HeaderText = "Type",
            Width = 60,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        _grid.Columns.AddRange(enabledCol, labelCol, urlCol, intervalCol, sourceCol);

        // Bottom buttons
        var bottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = _theme.PanelBackground,
            Padding = new Padding(10, 8, 10, 8)
        };

        var okBtn = new Button
        {
            Text = "OK",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = _theme.Muted },
            BackColor = _theme.Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(90, 30),
            Location = new Point(bottomBar.Width - 200, 0),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Cursor = Cursors.Hand
        };
        okBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_theme.Accent);
        okBtn.Click += (s, e) => { SaveAndClose(); };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 1, BorderColor = _theme.Muted },
            BackColor = _theme.Background,
            ForeColor = _theme.Text,
            Font = new Font("Segoe UI", 9),
            Size = new Size(90, 30),
            Location = new Point(bottomBar.Width - 100, 0),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.MouseOverBackColor = _theme.ButtonHoverBackground;
        cancelBtn.Click += (s, e) => DialogResult = DialogResult.Cancel;

        bottomBar.Controls.Add(okBtn);
        bottomBar.Controls.Add(cancelBtn);
        bottomBar.Resize += (s, e) =>
        {
            okBtn.Location = new Point(bottomBar.Width - 210, 8);
            cancelBtn.Location = new Point(bottomBar.Width - 110, 8);
        };

        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(bottomBar);

        LoadGrid();
    }

    private void LoadGrid()
    {
        _grid.Rows.Clear();
        foreach (var src in _feed.Sources)
        {
            int rowIdx = _grid.Rows.Add(
                src.Enabled,
                src.DisplayLabel,
                src.Url,
                src.PollIntervalMinutes,
                src.Source == FeedSource.Custom ? "" : src.SourceLabel
            );
            var row = _grid.Rows[rowIdx];
            row.Tag = src;
            row.DefaultCellStyle.BackColor = _theme.Background;
            row.DefaultCellStyle.ForeColor = _theme.Text;
            row.DefaultCellStyle.SelectionBackColor = _theme.ButtonHoverBackground;
            row.DefaultCellStyle.SelectionForeColor = _theme.Text;
        }
    }

    private void AddFeed(object? sender, EventArgs e)
    {
        int rowIdx = _grid.Rows.Add(true, "New Feed", "https://", 15, "");
        var row = _grid.Rows[rowIdx];
        row.Tag = new FeedSourceConfig
        {
            Source = FeedSource.Custom,
            Url = "https://",
            Label = "New Feed",
            Enabled = true,
            PollIntervalMinutes = 15,
            MaxItems = 10
        };
        row.DefaultCellStyle.BackColor = _theme.Background;
        row.DefaultCellStyle.ForeColor = _theme.Text;
        row.DefaultCellStyle.SelectionBackColor = _theme.ButtonHoverBackground;
        row.DefaultCellStyle.SelectionForeColor = _theme.Text;
        _grid.CurrentCell = _grid.Rows[rowIdx].Cells["label"];
        _grid.BeginEdit(true);
    }

    private void RemoveFeed(object? sender, EventArgs e)
    {
        if (_grid.CurrentRow is null || _grid.CurrentRow.IsNewRow) return;
        _grid.Rows.RemoveAt(_grid.CurrentRow.Index);
    }

    private void SaveAndClose()
    {
        var sources = new List<FeedSourceConfig>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;

            var enabled = row.Cells["enabled"].Value is true;
            var label = row.Cells["label"].Value as string ?? "";
            var url = row.Cells["url"].Value as string ?? "";
            var intervalStr = row.Cells["interval"].Value as string ?? "15";
            int.TryParse(intervalStr, out int interval);
            if (interval < 1) interval = 15;

            var existing = row.Tag as FeedSourceConfig;
            var source = existing?.Source ?? FeedSource.Custom;

            sources.Add(new FeedSourceConfig
            {
                Source = source,
                Url = url,
                Label = label,
                Enabled = enabled,
                PollIntervalMinutes = interval,
                MaxItems = existing?.MaxItems ?? 10
            });
        }

        _feed.UpdateSources(sources);
        DialogResult = DialogResult.OK;
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
        {
            e.CellStyle.BackColor = _theme.Background;
            e.CellStyle.ForeColor = _theme.Text;
            e.CellStyle.SelectionBackColor = _theme.ButtonHoverBackground;
            e.CellStyle.SelectionForeColor = _theme.Text;
        }
        if (e.RowIndex == -1 && e.ColumnIndex >= 0)
        {
            e.CellStyle.BackColor = _theme.PanelBackground;
            e.CellStyle.ForeColor = _theme.Text;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(_theme.Border, 1);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

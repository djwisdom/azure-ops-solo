using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Collapsible panel with VS Code-style section header.
/// Header displays title and toggle chevron (▼/▶). Click to expand/collapse.
/// Fully theme-aware with proper colors and smooth animation.
/// </summary>
internal sealed class CollapsibleSection : Panel
{
    private readonly Label _headerLabel = null!;
    private readonly Button _toggleButton = null!;
    private readonly Panel _contentPanel = null!;
    private bool _expanded = true;
    private readonly int _collapsedHeight = 26;
    private Theme _theme;

    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded != value)
            {
                _expanded = value;
                UpdateState();
                ExpandedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? ExpandedChanged;

    public string Title
    {
        get => _headerLabel.Text;
        set => _headerLabel.Text = value;
    }

    public Control[] ContentControls
    {
        set
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.AddRange(value);
        }
    }

    public Panel ContentPanel => _contentPanel;

    public CollapsibleSection(Theme theme, string title, bool startExpanded = true)
    {
        _theme = theme;
        _expanded = startExpanded;

        // Panel setup
        BackColor = theme.Background;
        BorderStyle = BorderStyle.None;
        Height = _collapsedHeight;

        // Header label (clickable)
        _headerLabel = new Label
        {
            Text = title,
            Location = new Point(8, 4),
            AutoSize = true,
            ForeColor = theme.Text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _headerLabel.Click += (s, e) => Expanded = !_expanded;

        // Toggle chevron button
        _toggleButton = new Button
        {
            Text = _expanded ? "▼" : "▶",
            Location = new Point(_headerLabel.Right + 6, 2),
            Size = new Size(20, 20),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Muted,
            TabStop = false
        };
        _toggleButton.FlatAppearance.BorderSize = 0;
        _toggleButton.Click += (s, e) => Expanded = !_expanded;

        // Content panel (holds section controls)
        _contentPanel = new Panel
        {
            Location = new Point(0, _collapsedHeight),
            Width = Width,
            BackColor = theme.Background,
            Visible = _expanded
        };
        _contentPanel.ControlAdded += (s, e) => AdjustContentHeight();

        Controls.AddRange(new Control[] { _headerLabel, _toggleButton, _contentPanel });

        UpdateState();
    }

    private void AdjustContentHeight()
    {
        if (_expanded)
        {
            int contentHeight = 0;
            foreach (Control c in _contentPanel.Controls)
            {
                contentHeight = Math.Max(contentHeight, c.Bottom);
            }
            Height = _collapsedHeight + contentHeight + 6;
        }
    }

    private void UpdateState()
    {
        _contentPanel.Visible = _expanded;
        _toggleButton.Text = _expanded ? "▼" : "▶";
        if (!_expanded)
        {
            Height = _collapsedHeight;
        }
        else
        {
            AdjustContentHeight();
        }
    }

    public void UpdateTheme(Theme theme)
    {
        _theme = theme;
        BackColor = theme.Background;
        _headerLabel.ForeColor = theme.Text;
        _contentPanel.BackColor = theme.Background;
        _toggleButton.BackColor = theme.PanelBackground;
        _toggleButton.ForeColor = theme.Muted;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_contentPanel == null) return;
        _contentPanel.Width = Width;
        AdjustContentHeight();
    }
}

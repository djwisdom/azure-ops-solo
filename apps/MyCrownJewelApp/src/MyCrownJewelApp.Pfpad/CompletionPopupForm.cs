using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class CompletionPopupForm : Form
{
    private readonly ListBox _listBox;
    private readonly Label _descriptionLabel;
    private readonly Panel _descriptionPanel;
    public readonly Roslyn.CompletionData? _completionData;
    private int _selectedIndex = 0;

    public event Action<string>? ItemSelected;
    public event Action? Dismissed;

    public string SelectedText => _selectedIndex >= 0 && _selectedIndex < _listBox.Items.Count
        ? ((Roslyn.CompletionItem)_listBox.Items[_selectedIndex]).InsertText
        : "";

    public CompletionPopupForm(Roslyn.CompletionData completionData, Point location)
    {
        _completionData = completionData;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = location;
        Size = new Size(400, 250);
        BackColor = ThemeManager.Instance.CurrentTheme.MenuBackground;

        _listBox = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 200,
            BorderStyle = BorderStyle.None,
            BackColor = ThemeManager.Instance.CurrentTheme.MenuBackground,
            ForeColor = ThemeManager.Instance.CurrentTheme.Text,
            SelectionMode = SelectionMode.One
        };

        _listBox.DrawMode = DrawMode.OwnerDrawVariable;
        _listBox.DrawItem += ListBox_DrawItem;
        _listBox.SelectedIndexChanged += (s, e) => UpdateDescription();
        _listBox.DoubleClick += (s, e) => SelectCurrentItem();

        foreach (var item in completionData.Items)
        {
            _listBox.Items.Add(item);
        }

        if (_listBox.Items.Count > 0)
        {
            _listBox.SelectedIndex = 0;
        }

        _descriptionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ThemeManager.Instance.CurrentTheme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle
        };

        _descriptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(4),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = ThemeManager.Instance.CurrentTheme.Text,
            BackColor = ThemeManager.Instance.CurrentTheme.PanelBackground
        };

        _descriptionPanel.Controls.Add(_descriptionLabel);

        var mainPanel = new Panel { Dock = DockStyle.Fill };
        mainPanel.Controls.Add(_descriptionPanel);
        mainPanel.Controls.Add(_listBox);
        Controls.Add(mainPanel);

        UpdateDescription();
    }

    private void ListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _listBox.Items.Count) return;

        var item = (Roslyn.CompletionItem)_listBox.Items[e.Index];
        var theme = ThemeManager.Instance.CurrentTheme;

        Color backColor = (e.State & DrawItemState.Selected) != 0
            ? theme.ButtonHoverBackground
            : theme.MenuBackground;

        using var brush = new SolidBrush(backColor);
        e.Graphics.FillRectangle(brush, e.Bounds);

        string kindSymbol = GetKindSymbol(item.Kind);
        string displayText = $"{kindSymbol} {item.DisplayText}";

        using var textBrush = new SolidBrush(
            (e.State & DrawItemState.Selected) != 0 ? Color.White : theme.Text);

        e.Graphics.DrawString(displayText, e.Font ?? Font, textBrush, e.Bounds.X + 2, e.Bounds.Y + 2);
    }

    private string GetKindSymbol(string kind) => kind switch
    {
        "Class" => "🄲",
        "Struct" => "🅂",
        "Interface" => "🄸",
        "Enum" => "🄴",
        "Method" => "🄼",
        "Property" => "🄿",
        "Field" => "🄵",
        "Event" => "🄴",
        "Namespace" => "🄽",
        _ => "▪"
    };

    private void UpdateDescription()
    {
        if (_listBox.SelectedIndex >= 0 && _listBox.SelectedIndex < _listBox.Items.Count)
        {
            var item = (Roslyn.CompletionItem)_listBox.Items[_listBox.SelectedIndex];
            _descriptionLabel.Text = item.Description ?? "No description available";
        }
        else
        {
            _descriptionLabel.Text = "";
        }
    }

    public void SelectNext() => MoveSelection(1);
    public void SelectPrevious() => MoveSelection(-1);

    private void MoveSelection(int delta)
    {
        int newIndex = _selectedIndex + delta;
        if (newIndex >= 0 && newIndex < _listBox.Items.Count)
        {
            _selectedIndex = newIndex;
            _listBox.SelectedIndex = _selectedIndex;
        }
    }

    public void SelectCurrentItem()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _listBox.Items.Count)
        {
            ItemSelected?.Invoke(SelectedText);
        }
        Close();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Dismissed?.Invoke();
        Close();
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                Dismissed?.Invoke();
                Close();
                return true;
            case Keys.Enter:
                SelectCurrentItem();
                return true;
            case Keys.Up:
                SelectPrevious();
                return true;
            case Keys.Down:
                SelectNext();
                return true;
        }
        return base.ProcessDialogKey(keyData);
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class NewProjectDialog : Form
{
    // ── Template model ────────────────────────────────────────────────────────
    private sealed record ProjectTemplate(
        string Name,
        string ShortName,
        string Language,        // "C#", "C", "C++"
        string Tags,
        string? DotnetShortName,                              // non-null → use dotnet new
        IReadOnlyList<(string RelPath, string Content)> Files // for C/C++
    );

    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly Form1 _mainForm;
    private ComboBox _langFilter = null!;
    private TextBox _searchBox = null!;
    private ListView _templateList = null!;
    private TextBox _nameTextBox = null!;
    private TextBox _locationTextBox = null!;
    private Button _browseButton = null!;
    private CheckBox _solutionCheckBox = null!;
    private Label _frameworkLabel = null!;
    private ComboBox _frameworkCombo = null!;
    private CheckBox _gitCheckBox = null!;
    private Label _standardLabel = null!;
    private ComboBox _standardCombo = null!;
    private Button _createButton = null!;
    private Button _cancelButton = null!;
    private Label _statusLabel = null!;
    private CheckBox _openInNewWindowCheckBox = null!;
    private Label _tplNameLabel = null!;
    private Label _tplTagsLabel = null!;
    private Label _pathPreviewLabel = null!;
    private Panel _optionsPanel = null!;
    private Panel _templateHeaderPanel = null!;
    private Panel _tabStripPanel = null!;
    private Panel _footerBar = null!;
    private string _activeLangTab = "All";
    private readonly List<Button> _langTabButtons = new();
    private bool _creating;

    private readonly List<ProjectTemplate> _allTemplates = new();

    public NewProjectDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        Text = "New Project";
        Size = new Size(860, 620);
        MinimumSize = new Size(720, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        InitializeForm();
        ApplyTheme();
        BuildBuiltInTemplates();
        LoadDotnetTemplates();
        ApplySearchAndFilter();
        LoadDotnetFrameworks();

        Load += (_, _) => NativeThemed.ApplyThemeToChildScrollbars(this, !ThemeManager.Instance.CurrentTheme.IsLight);
    }

    private void InitializeForm()
    {
        var theme = ThemeManager.Instance.CurrentTheme;

        SuspendLayout();
        _langFilter = new ComboBox();

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            PlaceholderText = "🔍  Search templates…"
        };
        _searchBox.TextChanged += (_, _) => ApplySearchAndFilter();

        _templateList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Tile,
            MultiSelect = false,
            FullRowSelect = true,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            HeaderStyle = ColumnHeaderStyle.None,
            OwnerDraw = true,
            ShowGroups = false,
            UseCompatibleStateImageBehavior = false,
            TileSize = new Size(280, 48)
        };
        _templateList.SelectedIndexChanged += TemplateList_SelectedIndexChanged;
        _templateList.DrawItem += TemplateList_DrawItem;
        _templateList.Resize += (_, _) => UpdateTemplateListTileSize();

        _nameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Text = "MyApp"
        };
        _nameTextBox.TextChanged += (_, _) => UpdatePathPreview();

        _locationTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };
        _locationTextBox.TextChanged += (_, _) => UpdatePathPreview();

        _browseButton = new Button
        {
            Text = "&Browse...",
            Dock = DockStyle.Fill,
            Width = 80,
            Margin = new Padding(10, 0, 0, 0)
        };
        _browseButton.Click += BrowseButton_Click;

        string? wsRoot = _mainForm.WorkspaceRoot;
        if (!string.IsNullOrEmpty(wsRoot))
        {
            var parent = Directory.GetParent(wsRoot);
            if (parent != null)
                _locationTextBox.Text = parent.FullName;
        }
        if (string.IsNullOrEmpty(_locationTextBox.Text))
            _locationTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        _solutionCheckBox = new CheckBox
        {
            Text = "Create &solution file (.sln)",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 4, 14, 0)
        };
        _frameworkLabel = new Label
        {
            Text = "&Framework:",
            AutoSize = true,
            Margin = new Padding(0, 7, 6, 0)
        };
        _frameworkCombo = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 14, 0)
        };

        _gitCheckBox = new CheckBox
        {
            Text = "Initialize &Git repository",
            AutoSize = true,
            Checked = true,
            Visible = false,
            Margin = new Padding(0, 4, 14, 0)
        };
        _standardLabel = new Label
        {
            Text = "&Standard:",
            AutoSize = true,
            Visible = false,
            Margin = new Padding(0, 7, 6, 0)
        };
        _standardCombo = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible = false,
            Margin = Padding.Empty
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _openInNewWindowCheckBox = new CheckBox
        {
            Text = "&Open in new window (blank slate editor — no previous tabs or terminal)",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(0, 8, 0, 0)
        };

        _createButton = new Button
        {
            Text = "&Create",
            AutoSize = true,
            Width = 96,
            Height = 32,
            Margin = new Padding(10, 0, 0, 0)
        };
        _createButton.Click += CreateButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            Width = 96,
            Height = 32,
            Margin = Padding.Empty,
            DialogResult = DialogResult.Cancel
        };

        var footerButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        footerButtons.Controls.Add(_cancelButton);
        footerButtons.Controls.Add(_createButton);

        _footerBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Padding = new Padding(16, 10, 16, 10)
        };
        _footerBar.Paint += (_, e) =>
        {
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            using var pen = new Pen(currentTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, _footerBar.Width, 0);
        };
        _footerBar.Controls.Add(footerButtons);
        _footerBar.Controls.Add(_statusLabel);

        var searchRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(12, 6, 12, 4),
            BackColor = Color.Transparent
        };
        searchRow.Controls.Add(_searchBox);

        _tabStripPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(6, 0, 6, 0)
        };
        var tabFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoScroll = false
        };
        foreach (var tabName in new[] { "All", "C#", "C/C++", "Web", "Script", "IaC" })
            tabFlow.Controls.Add(CreateLanguageTabButton(tabName));
        _tabStripPanel.Controls.Add(tabFlow);

        var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        leftPanel.Controls.Add(_templateList);
        leftPanel.Controls.Add(_tabStripPanel);
        leftPanel.Controls.Add(searchRow);

        _tplNameLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Height = 28,
            Text = "Select a template →"
        };
        _tplTagsLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Height = 18,
            Text = "Choose a template to configure your new project."
        };

        _templateHeaderPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(14),
            BackColor = theme.PanelBackground
        };
        _templateHeaderPanel.Controls.Add(_tplTagsLabel);
        _templateHeaderPanel.Controls.Add(_tplNameLabel);

        var bodyTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        bodyTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bodyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 5; i++)
            bodyTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label CreateFieldLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 14)
        };

        var locationRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 8),
            Padding = Padding.Empty
        };
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        locationRow.Controls.Add(_locationTextBox, 0, 0);
        locationRow.Controls.Add(_browseButton, 1, 0);

        _pathPreviewLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };

        _optionsPanel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = theme.PanelBackground
        };
        _optionsPanel.Paint += (_, e) =>
        {
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            using var pen = new Pen(currentTheme.Border);
            var rect = _optionsPanel.ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;
            e.Graphics.DrawRectangle(pen, rect);
        };

        var optionsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        optionsFlow.Controls.Add(_solutionCheckBox);
        optionsFlow.Controls.Add(_frameworkLabel);
        optionsFlow.Controls.Add(_frameworkCombo);
        optionsFlow.Controls.Add(_gitCheckBox);
        optionsFlow.Controls.Add(_standardLabel);
        optionsFlow.Controls.Add(_standardCombo);
        _optionsPanel.Controls.Add(optionsFlow);

        bodyTable.Controls.Add(CreateFieldLabel("Project name"), 0, 0);
        bodyTable.Controls.Add(_nameTextBox, 1, 0);
        bodyTable.Controls.Add(CreateFieldLabel("Location"), 0, 1);
        bodyTable.Controls.Add(locationRow, 1, 1);
        bodyTable.Controls.Add(_pathPreviewLabel, 0, 2);
        bodyTable.SetColumnSpan(_pathPreviewLabel, 2);
        bodyTable.Controls.Add(_optionsPanel, 0, 3);
        bodyTable.SetColumnSpan(_optionsPanel, 2);
        bodyTable.Controls.Add(_openInNewWindowCheckBox, 0, 4);
        bodyTable.SetColumnSpan(_openInNewWindowCheckBox, 2);

        var formBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            BackColor = Color.Transparent,
            AutoScroll = true
        };
        formBody.Controls.Add(bodyTable);

        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        rightPanel.Controls.Add(formBody);
        rightPanel.Controls.Add(_templateHeaderPanel);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false,
            Panel1MinSize = 260,
            Panel2MinSize = 340,
            BorderStyle = BorderStyle.None,
            SplitterWidth = 6
        };
        splitContainer.Panel1.Controls.Add(leftPanel);
        splitContainer.Panel2.Controls.Add(rightPanel);

        Controls.Add(splitContainer);
        Controls.Add(_footerBar);

        AcceptButton = _createButton;
        CancelButton = _cancelButton;
        Load += (_, _) =>
        {
            int maxDistance = Math.Max(splitContainer.Panel1MinSize, splitContainer.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth);
            splitContainer.SplitterDistance = Math.Min(320, maxDistance);
            UpdateTemplateListTileSize();
        };

        UpdatePathPreview();
        ResumeLayout(performLayout: true);
    }

    private void TemplateList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_templateList.SelectedItems.Count == 0)
        {
            _tplNameLabel.Text = "Select a template →";
            _tplTagsLabel.Text = "Choose a template to configure your new project.";
            _solutionCheckBox.Visible = false;
            _frameworkLabel.Visible = false;
            _frameworkCombo.Visible = false;
            _gitCheckBox.Visible = false;
            _standardLabel.Visible = false;
            _standardCombo.Visible = false;
            _optionsPanel.Visible = false;
            return;
        }

        var tpl = _templateList.SelectedItems[0].Tag as ProjectTemplate;
        _tplNameLabel.Text = tpl?.Name ?? "Select a template →";
        _tplTagsLabel.Text = string.IsNullOrWhiteSpace(tpl?.Tags)
            ? "Choose a template to configure your new project."
            : $"{tpl!.Language} • {tpl.Tags}";

        bool isCsOrDotnet = tpl == null || tpl.Language == "C#";
        bool isCOrCpp = tpl?.Language is "C" or "C++";
        bool isIaC = tpl?.Language is "Bicep" or "Terraform";
        bool isNativeOrScript = tpl?.Language is "Python" or "JavaScript" or "TypeScript" or "Go" or "Ruby" or "Shell" or "PowerShell" or "YAML" or "SQL";

        _solutionCheckBox.Visible = isCsOrDotnet;
        _frameworkLabel.Visible = isCsOrDotnet;
        _frameworkCombo.Visible = isCsOrDotnet;
        _gitCheckBox.Visible = isCOrCpp || isIaC || isNativeOrScript;
        _standardLabel.Visible = isCOrCpp;
        _standardCombo.Visible = isCOrCpp;
        _optionsPanel.Visible = isCsOrDotnet || isCOrCpp || isIaC || isNativeOrScript;

        if (isCOrCpp)
        {
            _standardCombo.Items.Clear();
            if (tpl!.Language == "C")
                _standardCombo.Items.AddRange(new object[] { "c17", "c11", "c99", "c89" });
            else
                _standardCombo.Items.AddRange(new object[] { "c++20", "c++17", "c++14", "c++11" });
            if (_standardCombo.Items.Count > 0)
                _standardCombo.SelectedIndex = 0;
        }
    }

    private void ApplySearchAndFilter()
    {
        string query = _searchBox.Text.Trim().ToLowerInvariant();
        string langFilter = _activeLangTab switch
        {
            "C#" => "C#",
            "C/C++" => "C/C++",
            "Web" => "Web",
            "Script" => "Script",
            "IaC" => "IaC",
            _ => "All"
        };
        PopulateTemplateList(langFilter, query);
    }

    private Button CreateLanguageTabButton(string tabName)
    {
        var button = new Button
        {
            Text = tabName,
            AutoSize = true,
            Height = 28,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            TabStop = false,
            Tag = tabName,
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) =>
        {
            _activeLangTab = tabName;
            ApplyTheme();
            ApplySearchAndFilter();
        };
        button.Paint += LanguageTabButton_Paint;
        _langTabButtons.Add(button);
        return button;
    }

    private void LanguageTabButton_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button)
            return;

        var theme = ThemeManager.Instance.CurrentTheme;
        bool active = string.Equals(button.Tag as string, _activeLangTab, StringComparison.Ordinal);
        if (!active)
            return;

        using var brush = new SolidBrush(theme.Accent);
        e.Graphics.FillRectangle(brush, 0, button.Height - 2, button.Width, 2);
    }

    private void TemplateList_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        if (e.Item.Tag is not ProjectTemplate tpl)
        {
            e.DrawDefault = true;
            return;
        }

        var theme = ThemeManager.Instance.CurrentTheme;
        bool selected = e.Item.Selected;
        var bounds = e.Bounds;

        using (var backgroundBrush = new SolidBrush(selected ? Blend(theme.PanelBackground, theme.Accent, 0.18f) : theme.EditorBackground))
            e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(theme.Accent);
            e.Graphics.FillRectangle(accentBrush, new Rectangle(bounds.Left, bounds.Top, 3, bounds.Height));
        }

        var badgeRect = new Rectangle(bounds.Left + 12, bounds.Top + Math.Max(10, (bounds.Height - 28) / 2), 28, 28);
        var badgeColor = GetLanguageBadgeColor(tpl.Language, theme);
        var previousSmoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = CreateRoundedRectanglePath(badgeRect, 8))
        using (var badgeBrush = new SolidBrush(badgeColor))
        {
            e.Graphics.FillPath(badgeBrush, path);
        }
        e.Graphics.SmoothingMode = previousSmoothing;

        using var badgeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var tagsFont = new Font("Segoe UI", 8f, FontStyle.Regular);

        TextRenderer.DrawText(
            e.Graphics,
            GetLanguageBadgeText(tpl.Language),
            badgeFont,
            badgeRect,
            GetReadableTextColor(badgeColor),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        int textLeft = badgeRect.Right + 12;
        int textWidth = Math.Max(40, bounds.Right - textLeft - 12);
        var nameBounds = new Rectangle(textLeft, bounds.Top + 7, textWidth, 20);
        var tagsBounds = new Rectangle(textLeft, bounds.Top + 24, textWidth, 16);

        TextRenderer.DrawText(
            e.Graphics,
            tpl.Name,
            nameFont,
            nameBounds,
            theme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        TextRenderer.DrawText(
            e.Graphics,
            tpl.Tags,
            tagsFont,
            tagsBounds,
            theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        if (selected && _templateList.Focused)
            e.DrawFocusRectangle();
    }

    private void UpdatePathPreview()
    {
        string combinedPath = Path.Combine(_locationTextBox.Text.Trim(), _nameTextBox.Text.Trim());
        _pathPreviewLabel.Text = combinedPath;
    }

    private void UpdateTemplateListTileSize()
    {
        if (_templateList.Width > 0)
            _templateList.TileSize = new Size(Math.Max(220, _templateList.ClientSize.Width - 4), 48);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var path = new GraphicsPath();
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color GetLanguageBadgeColor(string language, Theme theme) => language switch
    {
        "C#" => Color.FromArgb(104, 33, 122),
        "C" or "C++" => Color.FromArgb(0, 94, 167),
        "JavaScript" or "TypeScript" => Color.FromArgb(241, 156, 31),
        "Go" => Color.FromArgb(0, 173, 181),
        "Python" => Color.FromArgb(52, 120, 246),
        _ => theme.Accent
    };

    private static string GetLanguageBadgeText(string language) => language switch
    {
        "JavaScript" => "JS",
        "TypeScript" => "TS",
        "PowerShell" => "PS",
        "Python" => "Py",
        "Terraform" => "TF",
        "Bicep" => "Bp",
        "Shell" => "Sh",
        "Ruby" => "Rb",
        _ when language.Length <= 3 => language,
        _ => language[..Math.Min(2, language.Length)]
    };

    private static Color Blend(Color baseColor, Color mixColor, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        int r = (int)Math.Round(baseColor.R + ((mixColor.R - baseColor.R) * amount));
        int g = (int)Math.Round(baseColor.G + ((mixColor.G - baseColor.G) * amount));
        int b = (int)Math.Round(baseColor.B + ((mixColor.B - baseColor.B) * amount));
        return Color.FromArgb(r, g, b);
    }

    private static Color GetReadableTextColor(Color color)
    {
        double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
        return luminance >= 150 ? Color.Black : Color.White;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!ThemeManager.Instance.CurrentTheme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);
    }

    // ── Built-in templates ────────────────────────────────────────────────────

    private void BuildBuiltInTemplates()
    {
        // ── C templates ───────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Console App (C)", ShortName: "c-console", Language: "C",
            Tags: "Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.c", @"#include <stdio.h>

int main(int argc, char *argv[]) {
    printf(""Hello, World!\n"");
    return 0;
}
"),
                ("Makefile", @"CC      = gcc
CFLAGS  = -Wall -Wextra -g -std=$(STD)
STD     = c17
TARGET  = app
SRCS    = $(wildcard src/*.c)
OBJS    = $(SRCS:.c=.o)

.PHONY: all clean run

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CC) $(CFLAGS) -o $@ $^

%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

run: all
	./$(TARGET)

clean:
	rm -f $(OBJS) $(TARGET)
"),
                (".gitignore", "app\n*.o\n*.d\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Static Library (C)", ShortName: "c-staticlib", Language: "C",
            Tags: "Library", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.h", @"#ifndef {UNAME}_H
#define {UNAME}_H

int {name}_add(int a, int b);

#endif /* {UNAME}_H */
"),
                ("src/{name}.c", @"#include ""{name}.h""

int {name}_add(int a, int b) {
    return a + b;
}
"),
                ("examples/main.c", @"#include <stdio.h>
#include ""{name}.h""

int main(void) {
    printf(""%d\n"", {name}_add(2, 3));
    return 0;
}
"),
                ("Makefile", @"CC      = gcc
CFLAGS  = -Wall -Wextra -g -std=c17
AR      = ar
LIB     = lib{name}.a
SRCS    = $(wildcard src/*.c)
OBJS    = $(SRCS:.c=.o)

.PHONY: all clean

all: $(LIB)

$(LIB): $(OBJS)
	$(AR) rcs $@ $^

%.o: %.c
	$(CC) $(CFLAGS) -Iinclude -c -o $@ $<

clean:
	rm -f $(OBJS) $(LIB)
"),
                (".gitignore", "*.o\n*.a\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Makefile Project (C)", ShortName: "c-makefile", Language: "C",
            Tags: "Build", DotnetShortName: null,
            Files: new[]
            {
                ("main.c", @"#include <stdio.h>

int main(void) {
    printf(""Hello from {name}!\n"");
    return 0;
}
"),
                ("Makefile", @"CC     = gcc
CFLAGS = -Wall -Wextra -g
TARGET = {name}

all: $(TARGET)

$(TARGET): main.c
	$(CC) $(CFLAGS) -o $@ $<

clean:
	rm -f $(TARGET)
"),
                (".gitignore", "{name}\n*.o\n")
            }
        ));

        // ── C++ templates ─────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Console App (C++)", ShortName: "cpp-console", Language: "C++",
            Tags: "Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.cpp", @"#include <iostream>

int main(int argc, char* argv[]) {
    std::cout << ""Hello, World!\n"";
    return 0;
}
"),
                ("Makefile", @"CXX     = g++
CXXFLAGS = -Wall -Wextra -g -std=$(STD)
STD      = c++17
TARGET   = app
SRCS     = $(wildcard src/*.cpp)
OBJS     = $(SRCS:.cpp=.o)

.PHONY: all clean run

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CXX) $(CXXFLAGS) -o $@ $^

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c -o $@ $<

run: all
	./$(TARGET)

clean:
	rm -f $(OBJS) $(TARGET)
"),
                (".gitignore", "app\n*.o\nbuild/\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "CMake App (C++)", ShortName: "cpp-cmake", Language: "C++",
            Tags: "CMake/Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.cpp", @"#include <iostream>

int main() {
    std::cout << ""Hello from {name}!\n"";
    return 0;
}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable({name} src/main.cpp)

target_compile_options({name} PRIVATE -Wall -Wextra)
"),
                (".gitignore", "build/\nCMakeFiles/\ncmake_install.cmake\nCMakeCache.txt\n"),
                ("README.md", @"# {name}

## Build

```bash
cmake -B build
cmake --build build
./build/{name}
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "CMake Library (C++)", ShortName: "cpp-cmakelib", Language: "C++",
            Tags: "CMake/Library", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.hpp", @"#pragma once

namespace {name} {{

int add(int a, int b);

}} // namespace {name}
"),
                ("src/{name}.cpp", @"#include ""{name}.hpp""

namespace {name} {{

int add(int a, int b) {{
    return a + b;
}}

}} // namespace {name}
"),
                ("examples/main.cpp", @"#include <iostream>
#include ""{name}.hpp""

int main() {{
    std::cout << {name}::add(2, 3) << '\n';
    return 0;
}}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library({name} src/{name}.cpp)
target_include_directories({name} PUBLIC include)

add_executable({name}_example examples/main.cpp)
target_link_libraries({name}_example PRIVATE {name})
"),
                (".gitignore", "build/\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Header-Only Library (C++)", ShortName: "cpp-header-only", Language: "C++",
            Tags: "Library/Header-Only", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.hpp", @"#pragma once
#include <type_traits>

namespace {name} {{

template<typename T>
T clamp(T value, T lo, T hi) {{
    static_assert(std::is_arithmetic_v<T>, ""clamp requires arithmetic type"");
    return value < lo ? lo : value > hi ? hi : value;
}}

}} // namespace {name}
"),
                ("examples/main.cpp", @"#include <iostream>
#include ""{name}.hpp""

int main() {{
    std::cout << {name}::clamp(15, 0, 10) << '\n'; // 10
    return 0;
}}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library({name} INTERFACE)
target_include_directories({name} INTERFACE include)

add_executable({name}_example examples/main.cpp)
target_link_libraries({name}_example PRIVATE {name})
"),
                (".gitignore", "build/\n")
            }
        ));

        // ── Bicep templates ───────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Resource Group Deployment", ShortName: "bicep-rg-deploy", Language: "Bicep",
            Tags: "Azure/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.bicep", @"@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Project name used as prefix for resource names')
param projectName string = '{name}'

// Storage account example
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${projectName}${environment}sa'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
  tags: {
    environment: environment
    project: projectName
    managedBy: 'bicep'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
"),
                ("parameters.dev.json", @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {
    ""environment"": { ""value"": ""dev"" },
    ""projectName"": { ""value"": ""{name}"" }
  }
}
"),
                ("parameters.prod.json", @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {
    ""environment"": { ""value"": ""prod"" },
    ""projectName"": { ""value"": ""{name}"" }
  }
}
"),
                (".gitignore", "*.json.local\n.azure/\n"),
                ("README.md", @"# {name}

Azure Bicep infrastructure deployment.

## Prerequisites
- Azure CLI: `winget install Microsoft.AzureCLI`
- Bicep CLI: `az bicep install`

## Deploy

```bash
# Login
az login

# Create resource group (first time)
az group create --name {name}-dev-rg --location eastus

# Build (compile to ARM)
az bicep build --file main.bicep

# Deploy
az deployment group create \
  --resource-group {name}-dev-rg \
  --template-file main.bicep \
  --parameters @parameters.dev.json

# Validate (lint)
az bicep lint --file main.bicep
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Bicep Module Library", ShortName: "bicep-modules", Language: "Bicep",
            Tags: "Azure/Modules/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.bicep", @"@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name')
param environment string = 'dev'

module storage 'modules/storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    environment: environment
  }
}

module network 'modules/network.bicep' = {
  name: 'networkDeployment'
  params: {
    location: location
    environment: environment
  }
}

output storageAccountName string = storage.outputs.storageAccountName
"),
                ("modules/storage.bicep", @"@description('Azure region')
param location string

@description('Environment')
param environment string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'st${environment}${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountName string = storageAccount.name
"),
                ("modules/network.bicep", @"@description('Azure region')
param location string

@description('Environment')
param environment string

resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: 'vnet-${environment}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.0.0.0/16']
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '10.0.0.0/24'
        }
      }
    ]
  }
}

output vnetId string = vnet.id
"),
                (".gitignore", "*.json.local\n.azure/\n"),
                ("README.md", "# {name}\n\nBicep module library.\n\n```bash\naz bicep build --file main.bicep\naz bicep lint --file main.bicep\n```\n")
            }
        ));

        // ── Terraform templates ───────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Azure Infrastructure (Terraform)", ShortName: "tf-azure", Language: "Terraform",
            Tags: "Azure/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"resource ""azurerm_resource_group"" ""main"" {
  name     = ""${var.project_name}-${var.environment}-rg""
  location = var.location

  tags = local.common_tags
}

resource ""azurerm_storage_account"" ""main"" {
  name                     = ""${var.project_name}${var.environment}sa""
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""
  min_tls_version          = ""TLS1_2""

  tags = local.common_tags
}
"),
                ("providers.tf", @"terraform {
  required_version = "">= 1.5""

  required_providers {
    azurerm = {
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }
  }
}

provider ""azurerm"" {
  features {}
}
"),
                ("variables.tf", @"variable ""project_name"" {
  type        = string
  description = ""Project name used as prefix for resource names""
  default     = ""{name}""
}

variable ""environment"" {
  type        = string
  description = ""Deployment environment (dev, staging, prod)""
  default     = ""dev""

  validation {
    condition     = contains([""dev"", ""staging"", ""prod""], var.environment)
    error_message = ""Environment must be dev, staging, or prod.""
  }
}

variable ""location"" {
  type        = string
  description = ""Azure region""
  default     = ""eastus""
}
"),
                ("outputs.tf", @"output ""resource_group_name"" {
  description = ""Name of the created resource group""
  value       = azurerm_resource_group.main.name
}

output ""storage_account_name"" {
  description = ""Name of the storage account""
  value       = azurerm_storage_account.main.name
}
"),
                ("locals.tf", @"locals {
  common_tags = {
    environment = var.environment
    project     = var.project_name
    managed_by  = ""terraform""
  }
}
"),
                (".gitignore", "# Terraform state and secrets\n.terraform/\n*.tfstate\n*.tfstate.backup\n*.tfstate.*.backup\n.terraform.lock.hcl\nterraform.tfvars\n*.auto.tfvars\ncrash.log\n"),
                ("terraform.tfvars.example", "project_name = \"{name}\"\nenvironment  = \"dev\"\nlocation     = \"eastus\"\n"),
                ("README.md", @"# {name}

Azure infrastructure managed with Terraform.

## Prerequisites
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)

## Usage

```bash
# Login
az login

# Copy and edit variables
cp terraform.tfvars.example terraform.tfvars

# Initialize
terraform init

# Plan
terraform plan

# Apply
terraform apply

# Destroy
terraform destroy
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "AWS Infrastructure (Terraform)", ShortName: "tf-aws", Language: "Terraform",
            Tags: "AWS/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"resource ""aws_s3_bucket"" ""main"" {
  bucket = ""${var.project_name}-${var.environment}-${random_id.suffix.hex}""

  tags = local.common_tags
}

resource ""aws_s3_bucket_versioning"" ""main"" {
  bucket = aws_s3_bucket.main.id
  versioning_configuration {
    status = ""Enabled""
  }
}

resource ""random_id"" ""suffix"" {
  byte_length = 4
}
"),
                ("providers.tf", @"terraform {
  required_version = "">= 1.5""

  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
    random = {
      source  = ""hashicorp/random""
      version = ""~> 3.0""
    }
  }
}

provider ""aws"" {
  region = var.aws_region
}
"),
                ("variables.tf", @"variable ""project_name"" {
  type        = string
  description = ""Project name""
  default     = ""{name}""
}

variable ""environment"" {
  type        = string
  description = ""Deployment environment""
  default     = ""dev""
}

variable ""aws_region"" {
  type        = string
  description = ""AWS region""
  default     = ""us-east-1""
}
"),
                ("outputs.tf", @"output ""bucket_name"" {
  description = ""S3 bucket name""
  value       = aws_s3_bucket.main.bucket
}

output ""bucket_arn"" {
  description = ""S3 bucket ARN""
  value       = aws_s3_bucket.main.arn
}
"),
                ("locals.tf", @"locals {
  common_tags = {
    Environment = var.environment
    Project     = var.project_name
    ManagedBy   = ""terraform""
  }
}
"),
                (".gitignore", "# Terraform\n.terraform/\n*.tfstate\n*.tfstate.backup\n.terraform.lock.hcl\nterraform.tfvars\n*.auto.tfvars\ncrash.log\n"),
                ("terraform.tfvars.example", "project_name = \"{name}\"\nenvironment  = \"dev\"\naws_region   = \"us-east-1\"\n"),
                ("README.md", "# {name}\n\nAWS infrastructure managed with Terraform.\n\n```bash\nterraform init\nterraform plan\nterraform apply\n```\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Reusable Module (Terraform)", ShortName: "tf-module", Language: "Terraform",
            Tags: "Module/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"# Module: {name}
# Place your resource definitions here.
# Example:
# resource ""azurerm_resource_group"" ""this"" {
#   name     = var.resource_group_name
#   location = var.location
# }
"),
                ("variables.tf", @"variable ""name"" {
  type        = string
  description = ""Resource name""
}

variable ""location"" {
  type        = string
  description = ""Azure region / AWS region""
  default     = ""eastus""
}

variable ""tags"" {
  type        = map(string)
  description = ""Tags to apply to all resources""
  default     = {}
}
"),
                ("outputs.tf", @"# output ""resource_id"" {
#   description = ""ID of the created resource""
#   value       = azurerm_xxx.this.id
# }
"),
                ("versions.tf", @"terraform {
  required_version = "">= 1.5""
  required_providers {
    # Add your providers here
  }
}
"),
                ("examples/main.tf", @"module ""{name}"" {
  source = ""..""

  name     = ""example""
  location = ""eastus""
  tags = {
    environment = ""dev""
    managedBy   = ""terraform""
  }
}
"),
                ("README.md", @"# {name} Terraform Module

## Usage

```hcl
module ""{name}"" {{
  source = ""path/to/{name}""

  name     = ""my-resource""
  location = ""eastus""
}}
```

## Inputs

| Name | Type | Default | Description |
|------|------|---------|-------------|
| name | string | — | Resource name |
| location | string | eastus | Region |
| tags | map(string) | {} | Resource tags |

## Outputs

| Name | Description |
|------|-------------|
"),
                (".gitignore", ".terraform/\n*.tfstate\n*.tfstate.backup\n.terraform.lock.hcl\n")
            }
        ));

        // ── Python templates ──────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Python Script", ShortName: "py-script", Language: "Python",
            Tags: "Script/CLI", DotnetShortName: null,
            Files: new[]
            {
                ("main.py", @"#!/usr/bin/env python3
""""""
{name} — entry point
""""""
import argparse
import logging

logging.basicConfig(level=logging.INFO, format='%(levelname)s: %(message)s')
log = logging.getLogger(__name__)


def main() -> None:
    parser = argparse.ArgumentParser(description='{name}')
    parser.add_argument('--verbose', '-v', action='store_true')
    args = parser.parse_args()
    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)
    log.info('Hello from {name}!')


if __name__ == '__main__':
    main()
"),
                (".gitignore", @"__pycache__/
*.pyc
*.pyo
.venv/
venv/
dist/
*.egg-info/
.env
"),
                ("requirements.txt", "# Add your dependencies here\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Python Package", ShortName: "py-package", Language: "Python",
            Tags: "Package/Library", DotnetShortName: null,
            Files: new[]
            {
                ("src/{name}/__init__.py", @"""""""
{name} package
""""""

__version__ = '0.1.0'
"),
                ("src/{name}/main.py", @"def hello() -> str:
    return 'Hello from {name}!'
"),
                ("tests/__init__.py", ""),
                ("tests/test_main.py", @"from {name}.main import hello


def test_hello():
    assert hello() == 'Hello from {name}!'
"),
                ("pyproject.toml", @"[build-system]
requires = ['setuptools>=68', 'wheel']
build-backend = 'setuptools.backends.legacy:build'

[project]
name = '{name}'
version = '0.1.0'
description = '{name}'
requires-python = '>=3.11'
dependencies = []

[project.optional-dependencies]
dev = ['pytest']
"),
                (".gitignore", @"__pycache__/
*.pyc
.venv/
venv/
dist/
*.egg-info/
.env
")
            }
        ));

        // ── JavaScript templates ──────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Node.js App", ShortName: "js-node", Language: "JavaScript",
            Tags: "Node/CLI", DotnetShortName: null,
            Files: new[]
            {
                ("src/index.js", @"'use strict';

async function main() {
  console.log('Hello from {name}!');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
"),
                ("package.json", @"{
  ""name"": ""{name}"",
  ""version"": ""1.0.0"",
  ""description"": ""{name}"",
  ""main"": ""src/index.js"",
  ""scripts"": {
    ""start"": ""node src/index.js"",
    ""test"": ""jest""
  },
  ""license"": ""MIT""
}
"),
                (".gitignore", "node_modules/\ndist/\n.env\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Express API", ShortName: "js-express", Language: "JavaScript",
            Tags: "Node/Web/API", DotnetShortName: null,
            Files: new[]
            {
                ("src/app.js", @"'use strict';

const express = require('express');
const app = express();

app.use(express.json());

app.get('/health', (_req, res) => res.json({ status: 'ok' }));

module.exports = app;
"),
                ("src/index.js", @"'use strict';

const app = require('./app');
const PORT = process.env.PORT ?? 3000;

app.listen(PORT, () => console.log(`{name} listening on :${PORT}`));
"),
                ("package.json", @"{
  ""name"": ""{name}"",
  ""version"": ""1.0.0"",
  ""main"": ""src/index.js"",
  ""scripts"": {
    ""start"": ""node src/index.js"",
    ""dev"": ""nodemon src/index.js"",
    ""test"": ""jest""
  },
  ""dependencies"": { ""express"": ""^4.19.2"" },
  ""license"": ""MIT""
}
"),
                (".gitignore", "node_modules/\ndist/\n.env\n")
            }
        ));

        // ── TypeScript templates ──────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "TypeScript Node App", ShortName: "ts-node", Language: "TypeScript",
            Tags: "Node/TypeScript", DotnetShortName: null,
            Files: new[]
            {
                ("src/index.ts", @"async function main(): Promise<void> {
  console.log('Hello from {name}!');
}

main().catch((err: unknown) => {
  console.error(err);
  process.exit(1);
});
"),
                ("tsconfig.json", @"{
  ""compilerOptions"": {
    ""target"": ""ES2022"",
    ""module"": ""CommonJS"",
    ""outDir"": ""./dist"",
    ""rootDir"": ""./src"",
    ""strict"": true,
    ""esModuleInterop"": true
  },
  ""include"": [""src""]
}
"),
                ("package.json", @"{
  ""name"": ""{name}"",
  ""version"": ""1.0.0"",
  ""scripts"": {
    ""build"": ""tsc"",
    ""start"": ""node dist/index.js"",
    ""dev"": ""ts-node src/index.ts"",
    ""test"": ""jest""
  },
  ""devDependencies"": { ""typescript"": ""^5.4.5"", ""ts-node"": ""^10.9.2"" },
  ""license"": ""MIT""
}
"),
                (".gitignore", "node_modules/\ndist/\n.env\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "TypeScript Library", ShortName: "ts-lib", Language: "TypeScript",
            Tags: "Library/TypeScript", DotnetShortName: null,
            Files: new[]
            {
                ("src/index.ts", @"export function hello(name = '{name}'): string {
  return 'Hello from ' + name + '!';
}
"),
                ("src/index.test.ts", @"import { hello } from '.';

test('hello returns greeting', () => {
  expect(hello()).toBe('Hello from {name}!');
});
"),
                ("tsconfig.json", @"{
  ""compilerOptions"": {
    ""target"": ""ES2020"",
    ""module"": ""CommonJS"",
    ""declaration"": true,
    ""outDir"": ""./dist"",
    ""rootDir"": ""./src"",
    ""strict"": true
  },
  ""include"": [""src""]
}
"),
                ("package.json", @"{
  ""name"": ""{name}"",
  ""version"": ""1.0.0"",
  ""main"": ""dist/index.js"",
  ""types"": ""dist/index.d.ts"",
  ""scripts"": {
    ""build"": ""tsc"",
    ""test"": ""jest""
  },
  ""devDependencies"": { ""typescript"": ""^5.4.5"", ""jest"": ""^29.0.0"" },
  ""license"": ""MIT""
}
"),
                (".gitignore", "node_modules/\ndist/\n.env\n")
            }
        ));

        // ── Go templates ──────────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Go CLI App", ShortName: "go-cli", Language: "Go",
            Tags: "CLI/Console", DotnetShortName: null,
            Files: new[]
            {
                ("main.go", @"package main

import (
	""flag""
	""fmt""
	""os""
)

func main() {
	verbose := flag.Bool(""verbose"", false, ""enable verbose output"")
	flag.Parse()
	if *verbose {
		fmt.Fprintln(os.Stderr, ""verbose mode on"")
	}
	fmt.Println(""Hello from {name}!"")
}
"),
                ("go.mod", @"module github.com/your-org/{name}

go 1.22
"),
                (".gitignore", @"# Binaries
{name}
*.exe

# Test cache
/vendor/
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Go HTTP Server", ShortName: "go-http", Language: "Go",
            Tags: "Web/API", DotnetShortName: null,
            Files: new[]
            {
                ("main.go", @"package main

import (
	""encoding/json""
	""fmt""
	""log""
	""net/http""
)

func main() {
	http.HandleFunc(""/health"", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set(""Content-Type"", ""application/json"")
		json.NewEncoder(w).Encode(map[string]string{""status"": ""ok""})
	})
	addr := "":8080""
	fmt.Println(""{name} listening on"", addr)
	log.Fatal(http.ListenAndServe(addr, nil))
}
"),
                ("go.mod", @"module github.com/your-org/{name}

go 1.22
"),
                (".gitignore", @"# Binaries
{name}
*.exe

/vendor/
")
            }
        ));

        // ── Ruby templates ────────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Ruby Script", ShortName: "rb-script", Language: "Ruby",
            Tags: "Script/CLI", DotnetShortName: null,
            Files: new[]
            {
                ("{name}.rb", @"#!/usr/bin/env ruby
# frozen_string_literal: true

require 'optparse'

options = {}
OptionParser.new do |opts|
  opts.banner = 'Usage: {name}.rb [options]'
  opts.on('-v', '--verbose', 'Run verbosely') { options[:verbose] = true }
end.parse!

puts 'Hello from {name}!'
"),
                ("Gemfile", @"# frozen_string_literal: true

source 'https://rubygems.org'

gem 'rspec', '~> 3.0', group: :development
"),
                (".gitignore", ".bundle/\nvendor/bundle/\n*.gem\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Ruby Gem", ShortName: "rb-gem", Language: "Ruby",
            Tags: "Gem/Library", DotnetShortName: null,
            Files: new[]
            {
                ("lib/{name}.rb", @"# frozen_string_literal: true

module {UNAME}
  VERSION = '0.1.0'

  def self.hello
    'Hello from {name}!'
  end
end
"),
                ("spec/{name}_spec.rb", @"# frozen_string_literal: true

require '{name}'

RSpec.describe {UNAME} do
  it 'returns a greeting' do
    expect({UNAME}.hello).to eq('Hello from {name}!')
  end
end
"),
                ("{name}.gemspec", @"# frozen_string_literal: true

Gem::Specification.new do |spec|
  spec.name    = '{name}'
  spec.version = {UNAME}::VERSION
  spec.summary = '{name}'
  spec.files   = Dir['lib/**/*.rb']
end
"),
                ("Gemfile", @"# frozen_string_literal: true

source 'https://rubygems.org'
gemspec
"),
                (".gitignore", "*.gem\n.bundle/\nvendor/\n")
            }
        ));

        // ── Shell templates ───────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Bash Script", ShortName: "bash-script", Language: "Shell",
            Tags: "Script/Bash", DotnetShortName: null,
            Files: new[]
            {
                ("{name}.sh", @"#!/usr/bin/env bash
# {name} — description
# Usage: {name}.sh [--verbose] [--help]
set -euo pipefail
IFS=$'\n\t'

SCRIPT_DIR=""$(cd ""$(dirname ""${BASH_SOURCE[0]}"")"" && pwd)""

log()  { echo ""[INFO]  $*""; }
warn() { echo ""[WARN]  $*"" >&2; }
err()  { echo ""[ERROR] $*"" >&2; exit 1; }

usage() { echo ""Usage: $0 [--verbose] [--help]""; exit 0; }

VERBOSE=false
while [[ $# -gt 0 ]]; do
  case $1 in
    -v|--verbose) VERBOSE=true ;;
    -h|--help)    usage ;;
    *) err ""Unknown argument: $1"" ;;
  esac
  shift
done

main() {
  log ""Hello from {name}!""
}

main ""$@""
"),
                (".gitignore", "*.log\n*.tmp\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Bash Library", ShortName: "bash-lib", Language: "Shell",
            Tags: "Library/Bash", DotnetShortName: null,
            Files: new[]
            {
                ("lib/{name}.sh", @"#!/usr/bin/env bash
# {name} library — sourced by other scripts
# shellcheck shell=bash

{UNAME}_hello() {
  echo ""Hello from {name}!""
}
"),
                ("tests/test_{name}.bats", @"#!/usr/bin/env bats

load '../lib/{name}.sh'

@test '{UNAME}_hello outputs greeting' {
  run {UNAME}_hello
  [ ""$output"" = 'Hello from {name}!' ]
}
"),
                (".gitignore", "*.log\n")
            }
        ));

        // ── PowerShell templates ──────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "PowerShell Script", ShortName: "ps-script", Language: "PowerShell",
            Tags: "Script/Automation", DotnetShortName: null,
            Files: new[]
            {
                ("{name}.ps1", @"#Requires -Version 7
<#
.SYNOPSIS
    {name} — short description
.DESCRIPTION
    Longer description.
.PARAMETER Verbose
    Enable verbose output.
.EXAMPLE
    ./{name}.ps1 -Verbose
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Log {
    param([string]$Message)
    Write-Host ""[INFO] $Message""
}

function Main {
    Write-Log 'Hello from {name}!'
}

Main
"),
                (".gitignore", "*.log\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "PowerShell Module", ShortName: "ps-module", Language: "PowerShell",
            Tags: "Module/Automation", DotnetShortName: null,
            Files: new[]
            {
                ("{name}.psm1", @"#Requires -Version 7
Set-StrictMode -Version Latest

function Get-{UNAME}Greeting {
    <#
    .SYNOPSIS Returns a greeting from {name}.
    #>
    [CmdletBinding()]
    param([string]$Name = '{name}')
    ""Hello from $Name!""
}

Export-ModuleMember -Function 'Get-{UNAME}Greeting'
"),
                ("{name}.psd1", @"@{
    ModuleVersion     = '1.0.0'
    RootModule        = '{name}.psm1'
    FunctionsToExport = @('Get-{UNAME}Greeting')
    Description       = '{name} module'
}
"),
                ("tests/{name}.Tests.ps1", @"#Requires -Modules Pester
Import-Module ""$PSScriptRoot/../{name}.psd1"" -Force

Describe 'Get-{UNAME}Greeting' {
    It 'returns greeting' {
        Get-{UNAME}Greeting | Should -Be 'Hello from {name}!'
    }
}
"),
                (".gitignore", "*.log\n")
            }
        ));

        // ── YAML templates ────────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "GitHub Actions Workflow", ShortName: "yaml-ghaction", Language: "YAML",
            Tags: "CI/CD/GitHub", DotnetShortName: null,
            Files: new[]
            {
                (".github/workflows/{name}.yml", @"name: {name}

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up environment
        run: echo 'Configure your build environment here'

      - name: Build
        run: echo 'Run your build command here'

      - name: Test
        run: echo 'Run your tests here'
"),
                ("README.md", @"# {name}

GitHub Actions workflow for {name}.
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Docker Compose", ShortName: "yaml-docker-compose", Language: "YAML",
            Tags: "Docker/Compose", DotnetShortName: null,
            Files: new[]
            {
                ("docker-compose.yml", @"services:
  app:
    build: .
    ports:
      - ""8080:8080""
    environment:
      - DATABASE_URL=postgres://user:pass@db:5432/{name}
    depends_on:
      - db

  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: {name}
      POSTGRES_USER: user
      POSTGRES_PASSWORD: pass
    volumes:
      - db-data:/var/lib/postgresql/data

volumes:
  db-data:
"),
                (".gitignore", "*.log\n.env\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Kubernetes Manifests", ShortName: "yaml-k8s", Language: "YAML",
            Tags: "Kubernetes/Deploy", DotnetShortName: null,
            Files: new[]
            {
                ("k8s/deployment.yaml", @"apiVersion: apps/v1
kind: Deployment
metadata:
  name: {name}
  labels:
    app: {name}
spec:
  replicas: 2
  selector:
    matchLabels:
      app: {name}
  template:
    metadata:
      labels:
        app: {name}
    spec:
      containers:
        - name: {name}
          image: your-registry/{name}:latest
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: {name}-config
"),
                ("k8s/service.yaml", @"apiVersion: v1
kind: Service
metadata:
  name: {name}
spec:
  selector:
    app: {name}
  ports:
    - port: 80
      targetPort: 8080
  type: ClusterIP
"),
                ("k8s/configmap.yaml", @"apiVersion: v1
kind: ConfigMap
metadata:
  name: {name}-config
data:
  APP_ENV: production
  LOG_LEVEL: info
"),
                (".gitignore", "*.log\n.env\n")
            }
        ));

        // ── SQL templates ─────────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "SQL Schema", ShortName: "sql-schema", Language: "SQL",
            Tags: "Database/Schema", DotnetShortName: null,
            Files: new[]
            {
                ("schema.sql", @"-- {name} schema
-- Run: sqlite3 {name}.db < schema.sql

CREATE TABLE IF NOT EXISTS users (
    id         INTEGER      PRIMARY KEY AUTOINCREMENT,
    username   VARCHAR(100) NOT NULL UNIQUE,
    email      VARCHAR(255) NOT NULL UNIQUE,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS items (
    id         INTEGER      PRIMARY KEY AUTOINCREMENT,
    user_id    INTEGER      NOT NULL REFERENCES users(id),
    title      VARCHAR(255) NOT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_items_user_id ON items(user_id);

CREATE VIEW IF NOT EXISTS user_items AS
    SELECT u.username, i.title, i.created_at
    FROM items i
    JOIN users u ON i.user_id = u.id;
"),
                ("seed.sql", @"-- Seed data for {name}
INSERT INTO users (username, email) VALUES
    ('alice', 'alice@example.com'),
    ('bob',   'bob@example.com');
"),
                (".gitignore", "*.db\n*.sqlite\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "SQL Migrations", ShortName: "sql-migrations", Language: "SQL",
            Tags: "Database/Migrations", DotnetShortName: null,
            Files: new[]
            {
                ("migrations/001_initial.sql", @"-- Migration 001: initial schema for {name}

CREATE TABLE IF NOT EXISTS schema_migrations (
    version    INTEGER  PRIMARY KEY,
    applied_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS {name}_records (
    id         INTEGER      PRIMARY KEY AUTOINCREMENT,
    name       VARCHAR(255) NOT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO schema_migrations (version) VALUES (1);
"),
                ("migrations/002_add_status.sql", @"-- Migration 002: add status column

ALTER TABLE {name}_records ADD COLUMN status VARCHAR(50) NOT NULL DEFAULT 'active';

INSERT INTO schema_migrations (version) VALUES (2);
"),
                ("run_migrations.sh", @"#!/usr/bin/env bash
set -euo pipefail
DB=""{name}.db""
for f in migrations/*.sql; do
    echo ""Applying $f...""
    sqlite3 ""$DB"" < ""$f""
done
echo ""Done.""
"),
                (".gitignore", "*.db\n*.sqlite\n")
            }
        ));
    }

    // ── Template list population ──────────────────────────────────────────────

    private void PopulateTemplateList(string langFilter, string query = "")
    {
        ProjectTemplate? selectedTemplate = _templateList.SelectedItems.Count > 0
            ? _templateList.SelectedItems[0].Tag as ProjectTemplate
            : null;

        _templateList.BeginUpdate();
        _templateList.Items.Clear();

        IEnumerable<ProjectTemplate> filtered = _allTemplates.Where(t => MatchesLanguageFilter(t, langFilter));
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var tpl in filtered)
        {
            var item = new ListViewItem(tpl.Name) { Tag = tpl };
            _templateList.Items.Add(item);
        }

        _templateList.EndUpdate();
        UpdateTemplateListTileSize();

        if (_templateList.Items.Count == 0)
        {
            _tplNameLabel.Text = "Select a template →";
            _tplTagsLabel.Text = string.IsNullOrWhiteSpace(query)
                ? "No templates available for this category."
                : "No templates match your search.";
            _optionsPanel.Visible = false;
            return;
        }

        var selectedItem = selectedTemplate != null
            ? _templateList.Items.Cast<ListViewItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, selectedTemplate))
            : null;
        (selectedItem ?? _templateList.Items[0]).Selected = true;
        (selectedItem ?? _templateList.Items[0]).Focused = true;
    }

    private static bool MatchesLanguageFilter(ProjectTemplate template, string langFilter)
    {
        return langFilter switch
        {
            "All" => true,
            "C/C++" => template.Language is "C" or "C++",
            "Web" => template.Tags.Contains("Web", StringComparison.OrdinalIgnoreCase)
                || template.Name.Contains("Web", StringComparison.OrdinalIgnoreCase)
                || template.Name.Contains("HTTP", StringComparison.OrdinalIgnoreCase)
                || template.Name.Contains("Express", StringComparison.OrdinalIgnoreCase),
            "Script" => template.Tags.Contains("Script", StringComparison.OrdinalIgnoreCase)
                || template.Language is "Python" or "JavaScript" or "TypeScript" or "Go" or "Ruby" or "Shell" or "PowerShell",
            "IaC" => template.Language is "Bicep" or "Terraform"
                || template.Tags.Contains("IaC", StringComparison.OrdinalIgnoreCase),
            _ => template.Language == langFilter,
        };
    }

    private void LoadDotnetTemplates()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "new list")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(20000);
            if (proc.ExitCode != 0) return;
            ParseDotnetTemplateList(stdout);
        }
        catch { /* fallback: built-in .NET templates already in list */ }

        // Ensure at least common C# fallbacks
        if (!_allTemplates.Any(t => t.Language == "C#"))
        {
            foreach (var (shortName, name, tags) in new[]
            {
                ("console",  "Console App",          "Common/Console"),
                ("classlib", "Class Library",         "Common/Library"),
                ("webapi",   "ASP.NET Core Web API",  "Web/API"),
                ("mstest",   "MSTest Test Project",   "Test/MSTest"),
            })
            {
                _allTemplates.Add(new ProjectTemplate(name, shortName, "C#", tags, shortName,
                    Array.Empty<(string, string)>()));
            }
        }
    }

    private void ParseDotnetTemplateList(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool inData = false;
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("---")) { inData = true; continue; }
            if (!inData || string.IsNullOrEmpty(line)) continue;

            var parts = Regex.Split(line, @"\s{2,}");
            if (parts.Length < 2) continue;

            string tplName  = parts[0].Trim();
            string shortName = parts.Length > 1 ? parts[1].Trim() : "";
            string language  = parts.Length > 2 ? parts[2].Trim().Trim('[', ']') : "C#";
            string tags      = parts.Length > 3 ? parts[3].Trim() : "";

            if (!language.Equals("C#", StringComparison.OrdinalIgnoreCase)) continue;

            _allTemplates.Add(new ProjectTemplate(tplName, shortName, "C#", tags, shortName,
                Array.Empty<(string, string)>()));
        }
    }

    private void LoadDotnetFrameworks()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            var versions = new SortedSet<string>(Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase)));
            foreach (Match m in Regex.Matches(stdout, @"(\d+\.\d+)\.\d+"))
                versions.Add($"net{m.Groups[1].Value}");

            foreach (var v in versions) _frameworkCombo.Items.Add(v);
        }
        catch { }

        if (_frameworkCombo.Items.Count == 0)
        {
            _frameworkCombo.Items.Add("net10.0");
            _frameworkCombo.Items.Add("net9.0");
            _frameworkCombo.Items.Add("net8.0");
        }
        if (_frameworkCombo.Items.Count > 0) _frameworkCombo.SelectedIndex = 0;
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        void ApplyToTree(Control control)
        {
            switch (control)
            {
                case TextBox tb:
                    tb.BackColor = theme.EditorBackground;
                    tb.ForeColor = theme.Text;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox cb:
                    cb.BackColor = theme.EditorBackground;
                    cb.ForeColor = theme.Text;
                    cb.FlatStyle = FlatStyle.Flat;
                    break;
                case Button btn:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.ForeColor = theme.Text;
                    btn.BackColor = theme.PanelBackground;
                    btn.FlatAppearance.BorderColor = theme.Border;
                    btn.FlatAppearance.MouseOverBackColor = theme.ButtonHoverBackground;
                    btn.FlatAppearance.MouseDownBackColor = Blend(theme.PanelBackground, theme.Accent, 0.18f);
                    break;
                case CheckBox chk:
                    chk.ForeColor = theme.Text;
                    chk.BackColor = Color.Transparent;
                    break;
                case Label lbl:
                    lbl.ForeColor = theme.Text;
                    lbl.BackColor = Color.Transparent;
                    break;
                case ListView lv:
                    lv.BackColor = theme.EditorBackground;
                    lv.ForeColor = theme.Text;
                    lv.BorderStyle = BorderStyle.None;
                    break;
                case SplitContainer split:
                    split.BackColor = theme.Border;
                    break;
                case FlowLayoutPanel flow:
                    flow.BackColor = Color.Transparent;
                    break;
                case TableLayoutPanel table:
                    table.BackColor = Color.Transparent;
                    break;
                case Panel panel:
                    panel.BackColor = theme.Background;
                    break;
            }

            foreach (Control child in control.Controls)
                ApplyToTree(child);
        }

        ApplyToTree(this);

        _footerBar.BackColor = theme.Background;
        _tabStripPanel.BackColor = theme.MenuBackground;
        _templateHeaderPanel.BackColor = theme.PanelBackground;
        _optionsPanel.BackColor = theme.PanelBackground;

        _tplNameLabel.ForeColor = theme.Accent;
        _tplTagsLabel.ForeColor = theme.Muted;
        _pathPreviewLabel.ForeColor = theme.Muted;
        if (string.IsNullOrEmpty(_statusLabel.Text) || _statusLabel.ForeColor.ToArgb() == Color.DimGray.ToArgb())
            _statusLabel.ForeColor = theme.Muted;

        _createButton.BackColor = theme.Accent;
        _createButton.ForeColor = GetReadableTextColor(theme.Accent);
        _createButton.FlatAppearance.BorderColor = theme.Border;
        _createButton.FlatAppearance.MouseOverBackColor = Blend(theme.Accent, Color.White, theme.IsLight ? 0.08f : 0.12f);
        _createButton.FlatAppearance.MouseDownBackColor = Blend(theme.Accent, Color.Black, theme.IsLight ? 0.08f : 0.12f);

        _cancelButton.BackColor = theme.PanelBackground;
        _cancelButton.ForeColor = theme.Text;
        _cancelButton.FlatAppearance.BorderColor = theme.Border;

        foreach (var tabButton in _langTabButtons)
        {
            bool active = string.Equals(tabButton.Tag as string, _activeLangTab, StringComparison.Ordinal);
            tabButton.BackColor = theme.MenuBackground;
            tabButton.ForeColor = active ? theme.Text : theme.Muted;
            tabButton.Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular);
            tabButton.FlatAppearance.BorderSize = 0;
            tabButton.FlatAppearance.MouseOverBackColor = Blend(theme.MenuBackground, theme.Accent, 0.08f);
            tabButton.FlatAppearance.MouseDownBackColor = Blend(theme.MenuBackground, theme.Accent, 0.14f);
            tabButton.Invalidate();
        }

        _templateList.Invalidate();
        _optionsPanel.Invalidate();
        _footerBar.Invalidate();
    }


    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = _locationTextBox.Text };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _locationTextBox.Text = dlg.SelectedPath;
    }

    // ── Project creation ──────────────────────────────────────────────────────

    private async void CreateButton_Click(object? sender, EventArgs e)
    {
        if (_creating) return;

        var tpl = _templateList.SelectedItems.Count > 0
            ? _templateList.SelectedItems[0].Tag as ProjectTemplate : null;
        string projectName = _nameTextBox.Text.Trim();
        string location    = _locationTextBox.Text.Trim();

        if (tpl == null)       { _statusLabel.Text = "Please select a template."; return; }
        if (string.IsNullOrEmpty(projectName)) { _statusLabel.Text = "Please enter a project name."; return; }
        if (!Directory.Exists(location))       { _statusLabel.Text = "Please select a valid location."; return; }

        _creating = true;
        _createButton.Enabled = false;
        _cancelButton.Enabled = false;
        _statusLabel.ForeColor = Color.DimGray;

        string projectDir = Path.Combine(location, projectName);

        try
        {
            if (tpl.DotnetShortName != null)
                await CreateDotnetProjectAsync(tpl, projectName, projectDir, location);
            else
                await CreateNativeProjectAsync(tpl, projectName, projectDir);

            _statusLabel.Text = "Project created successfully!";
            _statusLabel.ForeColor = FlatUiHelper.SuccessColor(ThemeManager.Instance.CurrentTheme);

            if (_openInNewWindowCheckBox.Checked)
            {
                // Spawn a clean pfpad instance focused on the new project.
                // --blank suppresses session restore and forces workspace panel open.
                string exe = Application.ExecutablePath;
                var psi = new ProcessStartInfo(exe, $"\"{projectDir}\" --blank")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            else
            {
                _mainForm.BeginInvoke(() => _mainForm.OpenWorkspaceFolder(projectDir));
            }

            await Task.Delay(500);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = FlatUiHelper.ErrorColor(ThemeManager.Instance.CurrentTheme);
            _statusLabel.Text = $"Error: {ex.Message}";
            _creating = false;
            _createButton.Enabled = true;
            _cancelButton.Enabled = true;
        }
    }

    private async Task CreateDotnetProjectAsync(ProjectTemplate tpl, string projectName, string projectDir, string location)
    {
        _statusLabel.Text = $"Creating {tpl.Language} project '{projectName}'...";
        string? framework = _frameworkCombo.SelectedItem as string;

        string newArgs = $"new \"{tpl.DotnetShortName}\" -n \"{projectName}\" -o \"{projectDir}\"";
        if (!string.IsNullOrEmpty(framework))
            newArgs += $" --framework {framework}";

        await RunProcessAsync("dotnet", newArgs);

        if (_solutionCheckBox.Checked)
        {
            _statusLabel.Text = "Creating solution file...";
            await RunProcessAsync("dotnet", $"new sln -o \"{location}\"");
            string? csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj != null)
            {
                _statusLabel.Text = "Adding project to solution...";
                await RunProcessAsync("dotnet", $"sln \"{location}\" add \"{csproj}\"");
            }
        }
    }

    private async Task CreateNativeProjectAsync(ProjectTemplate tpl, string projectName, string projectDir)
    {
        _statusLabel.Text = $"Creating {tpl.Language} project '{projectName}'...";

        Directory.CreateDirectory(projectDir);

        string uname = projectName.ToUpperInvariant();
        // _standardCombo is only visible/relevant for C/C++ — IaC templates don't use {std}
        string std = _standardCombo.Visible
            ? (_standardCombo.SelectedItem as string ?? (tpl.Language == "C" ? "c17" : "c++17"))
            : "";

        foreach (var (relPath, rawContent) in tpl.Files)
        {
            // Expand template variables
            string content = rawContent
                .Replace("{name}", projectName)
                .Replace("{UNAME}", uname)
                .Replace("{std}", std);

            // Expand relPath too (e.g. include/{name}.h)
            string expandedPath = relPath
                .Replace("{name}", projectName)
                .Replace("{UNAME}", uname);

            string fullPath = Path.Combine(projectDir, expandedPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content);
        }

        if (_gitCheckBox.Checked)
        {
            _statusLabel.Text = "Initializing git repository...";
            try { await RunProcessAsync("git", "init", projectDir); } catch { /* git not on PATH, skip */ }
        }
    }

    private static async Task<string> RunProcessAsync(string exe, string args, string? workDir = null)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true,
        };
        if (workDir != null) psi.WorkingDirectory = workDir;

        using var proc = Process.Start(psi) ?? throw new Exception($"Failed to start {exe}");
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new Exception(string.IsNullOrWhiteSpace(stderr) ? $"{exe} exited with code {proc.ExitCode}" : stderr.Trim());

        return stdout;
    }
}

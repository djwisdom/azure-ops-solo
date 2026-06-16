using System.Drawing.Drawing2D;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Pfpad.AIOps;

public sealed class OpsQueryPanel : UserControl
{
    private sealed class MessageBubbleState
    {
        public required Panel Wrapper { get; init; }
        public required Panel Bubble { get; init; }
        public required bool IsUser { get; init; }
    }

    private readonly Panel _header;
    private readonly Label _titleLabel;
    private readonly Button _clearButton;
    private readonly Button _closeButton;
    private readonly Panel _contextBar;
    private readonly Label _fileLabel;
    private readonly TextBox _fileTextBox;
    private readonly Label _serviceLabel;
    private readonly Panel _messagesScrollPanel;
    private readonly FlowLayoutPanel _messagesHost;
    private readonly Panel _inputPanel;
    private readonly TextBox _questionTextBox;
    private readonly Button _askButton;
    private readonly Label _hintLabel;
    private readonly Panel _contextHintBar;
    private readonly Label _contextHintLabel;
    private readonly List<MessageBubbleState> _messageStates = new();

    private Theme _theme;
    private AIOpsEngine? _engine;
    private string? _serviceName;
    private Panel? _loadingWrapper;

    public event Action? CloseRequested;

    public string? CurrentFilePath { get; private set; }

    public OpsQueryPanel()
    {
        AutoScaleMode = AutoScaleMode.Font;
        DoubleBuffered = true;
        MinimumSize = new Size(420, 320);
        _theme = ThemeManager.Instance.CurrentTheme;

        _header = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(6, 4, 6, 4) };
        _titleLabel = new Label { AutoSize = true, Text = "🤖 OPS QUERY", Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(6, 7) };
        _clearButton = AIOpsUiHelper.CreateHeaderButton("Clear", (s, e) => ClearConversation(), 58);
        _closeButton = AIOpsUiHelper.CreateHeaderButton("✕ Close", (s, e) => CloseRequested?.Invoke(), 74);
        _header.Resize += (_, _) => LayoutHeaderButtons();
        _header.Controls.AddRange([_titleLabel, _clearButton, _closeButton]);

        _contextBar = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(6, 4, 6, 4) };
        _fileLabel = new Label { AutoSize = true, Text = "File:", Location = new Point(6, 6) };
        _fileTextBox = new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None, Location = new Point(38, 3), Height = 22, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _serviceLabel = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Width = 120, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _contextBar.Resize += (_, _) => LayoutContextBar();
        _contextBar.Controls.AddRange([_fileLabel, _fileTextBox, _serviceLabel]);

        _messagesScrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
        _messagesHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _messagesScrollPanel.Controls.Add(_messagesHost);
        _messagesScrollPanel.Resize += (_, _) => ReflowMessages();

        _inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(8, 6, 8, 6) };
        _questionTextBox = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Why is checkout-service failing?  •  Analyze deployment risk  •  Show recent exceptions  •  Add tracing to this function"
        };
        _askButton = new Button
        {
            Text = "Ask",
            Width = 56,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _hintLabel = new Label
        {
            AutoSize = false,
            Height = 18,
            Text = "Enter sends • Shift+Enter adds a new line",
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _inputPanel.Resize += (_, _) => LayoutInputPanel();
        _questionTextBox.KeyDown += QuestionTextBox_KeyDown;
        _askButton.Click += (_, _) => SubmitFromInput();
        _inputPanel.Controls.AddRange([_questionTextBox, _askButton, _hintLabel]);

        Controls.Add(_messagesScrollPanel);
        Controls.Add(_inputPanel);
        Controls.Add(_contextBar);
        (_contextHintBar, _contextHintLabel) = AIOpsUiHelper.CreateHintBar("ⓘ Evidence-based answers from real connector data — logs, metrics, traces, Git history. Never hallucinated.");
        Controls.Add(_contextHintBar);
        Controls.Add(_header);

        ThemeManager.Instance.ThemeChanged += SetTheme;
        SetTheme(_theme);
        LayoutHeaderButtons();
        LayoutContextBar();
        LayoutInputPanel();
    }

    public void SetEngine(AIOpsEngine engine) => _engine = engine;

    public void SetContext(string? filePath, string? serviceName)
    {
        CurrentFilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
        _serviceName = string.IsNullOrWhiteSpace(serviceName) ? null : serviceName;
        _fileTextBox.Text = CurrentFilePath ?? string.Empty;
        _serviceLabel.Text = string.IsNullOrWhiteSpace(_serviceName) ? string.Empty : $"Service: {_serviceName}";
    }

    public void AddUserMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        AddMessageBubble(text.Trim(), true, null, null, null, null, null);
    }

    public void AddAssistantMessage(OpsQueryResponse response)
    {
        AddMessageBubble(response.Answer, false, response, response.Evidence, response.Recommendations, response.Disclaimer, response.IsGuess);
    }

    public async void AskQuestion(string question)
    {
        if (_engine is null)
        {
            AddSystemMessage("Connect the AIOps engine before asking a question.");
            return;
        }

        string trimmed = question.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        AddUserMessage(trimmed);
        _questionTextBox.Clear();
        ShowLoading();
        _questionTextBox.Enabled = false;
        _askButton.Enabled = false;

        try
        {
            OpsQueryResponse response = await Task.Run(() => _engine.Chat(trimmed, CurrentFilePath));
            HideLoading();
            AddAssistantMessage(response);
        }
        catch (Exception ex)
        {
            HideLoading();
            AddSystemMessage($"Unable to analyze the question: {ex.Message}");
        }
        finally
        {
            _questionTextBox.Enabled = true;
            _askButton.Enabled = true;
            _questionTextBox.Focus();
        }
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        AIOpsUiHelper.ApplyControlTheme(this, theme);
        BackColor = theme.MenuBackground;
        _header.BackColor = theme.MenuBackground;
        _contextBar.BackColor = theme.MenuBackground;
        AIOpsUiHelper.SetHintBarTheme(_contextHintBar, _contextHintLabel, theme);
        _messagesScrollPanel.BackColor = theme.EditorBackground;
        _messagesHost.BackColor = theme.EditorBackground;
        _inputPanel.BackColor = theme.MenuBackground;
        _titleLabel.ForeColor = theme.Text;
        _fileLabel.ForeColor = theme.Muted;
        _serviceLabel.ForeColor = theme.Muted;
        _hintLabel.ForeColor = theme.Muted;
        _clearButton.BackColor = Color.Transparent;
        _closeButton.BackColor = Color.Transparent;
        _clearButton.ForeColor = theme.Text;
        _closeButton.ForeColor = theme.Text;
        _askButton.BackColor = theme.PanelBackground;
        _askButton.ForeColor = theme.Text;
        _askButton.FlatAppearance.BorderColor = theme.Border;
        _fileTextBox.ApplyTheme(theme);
        _questionTextBox.ApplyTheme(theme);
        RefreshMessageThemes();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ThemeManager.Instance.ThemeChanged -= SetTheme;

        base.Dispose(disposing);
    }

    private void LayoutHeaderButtons()
    {
        _closeButton.Location = new Point(_header.ClientSize.Width - _closeButton.Width - 4, 4);
        _clearButton.Location = new Point(_closeButton.Left - _clearButton.Width - 4, 4);
    }

    private void LayoutContextBar()
    {
        _serviceLabel.Location = new Point(Math.Max(200, _contextBar.ClientSize.Width - 146), 5);
        _fileTextBox.Width = Math.Max(120, _serviceLabel.Left - _fileTextBox.Left - 8);
    }

    private void LayoutInputPanel()
    {
        _askButton.Location = new Point(_inputPanel.ClientSize.Width - _askButton.Width - 8, 8);
        _questionTextBox.Location = new Point(8, 6);
        _questionTextBox.Size = new Size(Math.Max(120, _askButton.Left - 16), 36);
        _hintLabel.Location = new Point(8, 46);
        _hintLabel.Width = _inputPanel.ClientSize.Width - 16;
    }

    private void QuestionTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            SubmitFromInput();
        }
    }

    private void SubmitFromInput()
    {
        if (_askButton.Enabled)
            AskQuestion(_questionTextBox.Text);
    }

    private void ClearConversation()
    {
        _engine?.OpsQueryEngine.ClearHistory();
        _messagesHost.SuspendLayout();
        try
        {
            _messageStates.Clear();
            _messagesHost.Controls.Clear();
            _loadingWrapper = null;
        }
        finally
        {
            _messagesHost.ResumeLayout();
        }
    }

    private void ShowLoading()
    {
        HideLoading();
        _loadingWrapper = AddMessageBubble("⏳ Analyzing...", false, null, null, null, null, null);
    }

    private void HideLoading()
    {
        if (_loadingWrapper is null)
            return;

        _messagesHost.Controls.Remove(_loadingWrapper);
        MessageBubbleState? state = _messageStates.FirstOrDefault(s => s.Wrapper == _loadingWrapper);
        if (state is not null)
            _messageStates.Remove(state);
        _loadingWrapper.Dispose();
        _loadingWrapper = null;
        ReflowMessages();
    }

    private void AddSystemMessage(string text) => AddMessageBubble(text, false, null, null, null, null, null);

    private Panel AddMessageBubble(
        string text,
        bool isUser,
        OpsQueryResponse? response,
        IReadOnlyList<Evidence>? evidence,
        IReadOnlyList<RemediationSuggestion>? recommendations,
        string? disclaimer,
        bool? isGuess)
    {
        Panel wrapper = new() { Width = GetMessageWidth(), Margin = new Padding(0, 0, 0, 8), Padding = Padding.Empty };
        Panel bubble = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8), BorderStyle = BorderStyle.None };
        bubble.Paint += (_, e) => PaintBubble(e.Graphics, bubble.ClientRectangle, isUser);

        FlowLayoutPanel content = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };

        int textWidth = Math.Max(180, GetMessageWidth() - 96);
        Label bodyLabel = CreateMessageLabel(text, textWidth, _theme.Text, new Font("Segoe UI", 9f));
        content.Controls.Add(bodyLabel);

        if (!isUser && response is not null)
        {
            Label confidenceLabel = CreateMessageLabel($"Confidence: {Math.Round(Math.Clamp(response.ConfidenceScore, 0d, 1d) * 100)}%", textWidth, _theme.Muted, new Font("Segoe UI", 8f));
            confidenceLabel.Padding = new Padding(0, 4, 0, 0);
            content.Controls.Add(confidenceLabel);

            if (isGuess == true)
            {
                Label warningLabel = CreateMessageLabel("⚠ Low confidence", textWidth, Color.Goldenrod, new Font("Segoe UI", 8f, FontStyle.Bold));
                content.Controls.Add(warningLabel);
            }

            if (!string.IsNullOrWhiteSpace(disclaimer))
            {
                Label disclaimerLabel = CreateMessageLabel(disclaimer, textWidth, Color.DarkOrange, new Font("Segoe UI", 8f, FontStyle.Italic));
                disclaimerLabel.Padding = new Padding(0, 2, 0, 0);
                content.Controls.Add(disclaimerLabel);
            }

            if (evidence is { Count: > 0 })
                content.Controls.Add(CreateEvidenceSection(evidence, textWidth));

            if (recommendations is { Count: > 0 })
                content.Controls.Add(CreateRecommendationsSection(recommendations, textWidth));
        }

        bubble.Controls.Add(content);
        wrapper.Controls.Add(bubble);
        _messagesHost.Controls.Add(wrapper);

        var state = new MessageBubbleState { Wrapper = wrapper, Bubble = bubble, IsUser = isUser };
        _messageStates.Add(state);
        PositionBubble(state);
        ScrollMessagesToBottom();
        return wrapper;
    }

    private Control CreateEvidenceSection(IReadOnlyList<Evidence> evidence, int width)
    {
        FlowLayoutPanel panel = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        LinkLabel toggle = new()
        {
            AutoSize = true,
            Text = $"Evidence ({evidence.Count}) ▸",
            LinkColor = AIOpsUiHelper.Accent(_theme),
            ActiveLinkColor = AIOpsUiHelper.Accent(_theme),
            VisitedLinkColor = AIOpsUiHelper.Accent(_theme),
            Margin = Padding.Empty
        };
        FlowLayoutPanel evidencePanel = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0),
            Padding = Padding.Empty,
            Visible = false,
            BackColor = Color.Transparent
        };
        foreach (Evidence item in evidence)
        {
            string age = item.Timestamp is null ? string.Empty : $" ({AIOpsUiHelper.FormatAge(item.Timestamp.Value)})";
            evidencePanel.Controls.Add(CreateMessageLabel($"• {item.Source}: {item.Description}{age}", width, _theme.Muted, new Font("Segoe UI", 8f)));
        }
        toggle.LinkClicked += (_, _) =>
        {
            evidencePanel.Visible = !evidencePanel.Visible;
            toggle.Text = evidencePanel.Visible ? $"Evidence ({evidence.Count}) ▾" : $"Evidence ({evidence.Count}) ▸";
            ReflowMessages();
        };
        panel.Controls.Add(toggle);
        panel.Controls.Add(evidencePanel);
        return panel;
    }

    private Control CreateRecommendationsSection(IReadOnlyList<RemediationSuggestion> recommendations, int width)
    {
        FlowLayoutPanel panel = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        panel.Controls.Add(CreateMessageLabel("Suggested Actions:", width, _theme.Text, new Font("Segoe UI", 8.5f, FontStyle.Bold)));
        foreach (RemediationSuggestion recommendation in recommendations)
        {
            Color bulletColor = recommendation.Risk switch
            {
                RemediationRisk.ProductionImpact => Color.DarkRed,
                RemediationRisk.ReviewRequired => Color.Goldenrod,
                _ => Color.ForestGreen
            };
            Label item = CreateMessageLabel($"● {recommendation.Title}: {recommendation.Description}", width, _theme.Text, new Font("Segoe UI", 8f));
            item.ForeColor = bulletColor;
            panel.Controls.Add(item);
        }
        return panel;
    }

    private Label CreateMessageLabel(string text, int width, Color color, Font font)
        => new()
        {
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Text = text,
            ForeColor = color,
            Font = font,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };

    private void RefreshMessageThemes()
    {
        foreach (MessageBubbleState state in _messageStates)
        {
            state.Bubble.BackColor = state.IsUser
                ? Color.FromArgb(40, _theme.KeywordColor)
                : _theme.MenuBackground;
            state.Bubble.Invalidate();
            ApplyThemeToMessageControls(state.Bubble.Controls);
        }
    }

    private void ApplyThemeToMessageControls(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            if (control is LinkLabel link)
            {
                link.LinkColor = AIOpsUiHelper.Accent(_theme);
                link.ActiveLinkColor = AIOpsUiHelper.Accent(_theme);
                link.VisitedLinkColor = AIOpsUiHelper.Accent(_theme);
            }
            else if (control is Label label && label.ForeColor == Color.Empty)
            {
                label.ForeColor = _theme.Text;
            }

            if (control.HasChildren)
                ApplyThemeToMessageControls(control.Controls);
        }
    }

    private void PaintBubble(Graphics graphics, Rectangle bounds, bool isUser)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = Rectangle.Inflate(bounds, -1, -1);
        using GraphicsPath path = CreateRoundedPath(rect, 10);
        using SolidBrush brush = new(isUser ? Color.FromArgb(40, _theme.KeywordColor) : _theme.MenuBackground);
        using Pen pen = new(isUser ? Color.FromArgb(90, _theme.KeywordColor) : _theme.Border);
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private int GetMessageWidth() => Math.Max(220, _messagesScrollPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 24);

    private void ReflowMessages()
    {
        int width = GetMessageWidth();
        foreach (MessageBubbleState state in _messageStates)
        {
            state.Wrapper.Width = width;
            state.Bubble.MaximumSize = new Size(Math.Max(220, width - 28), 0);
            PositionBubble(state);
            state.Wrapper.Height = state.Bubble.Height + 2;
        }
        ScrollMessagesToBottom();
    }

    private void PositionBubble(MessageBubbleState state)
    {
        state.Bubble.PerformLayout();
        state.Bubble.Location = state.IsUser
            ? new Point(Math.Max(0, state.Wrapper.ClientSize.Width - state.Bubble.Width - 4), 0)
            : new Point(0, 0);
        state.Wrapper.Height = state.Bubble.Height + 2;
    }

    private void ScrollMessagesToBottom()
    {
        if (!IsHandleCreated)
            return;

        AIOpsUiHelper.SafeBeginInvoke(this, () =>
        {
            if (_messagesScrollPanel.IsDisposed)
                return;
            _messagesScrollPanel.VerticalScroll.Value = _messagesScrollPanel.VerticalScroll.Maximum;
            _messagesScrollPanel.PerformLayout();
        });
    }
}
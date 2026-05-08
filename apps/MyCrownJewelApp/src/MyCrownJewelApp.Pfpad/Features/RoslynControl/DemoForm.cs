using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad.Features.RoslynControl;

public sealed class DemoForm : Form
{
    private readonly RoslynService _roslynService;
    private readonly RoslynWorkspaceService _workspace;
    private readonly RichTextBox _editor;
    private readonly Button _restartBtn;
    private readonly Button _toggleBtn;
    private readonly Button _visualizerBtn;
    private readonly Label _statusLabel;
    private readonly Label _toggleStateLabel;

    public DemoForm()
    {
        Text = "Roslyn Control Demo";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        _workspace = new RoslynWorkspaceService();
        _roslynService = new RoslynService(_workspace);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
        };

        _restartBtn = new Button { Text = "Restart Analyzers (Ctrl+Shift+R)", AutoSize = true };
        _restartBtn.Click += async (s, e) => await _roslynService.RestartAnalyzersAsync();

        _toggleBtn = new Button { Text = "Toggle Analyzers", AutoSize = true };

        _visualizerBtn = new Button { Text = "Open Visualizer (Ctrl+Alt+V)", AutoSize = true };
        _visualizerBtn.Click += (s, e) =>
        {
            var form = new RoslynVisualizerForm();
            form.OnRefreshRequested += async () =>
            {
                var snapshot = await _roslynService.GetVisualizerSnapshotAsync();
                form.BeginInvoke(() => form.UpdateSnapshot(snapshot));
            };
            form.Show(this);
        };

        _toggleStateLabel = new Label { Text = "Analyzers: ON", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
        _statusLabel = new Label { Text = "Ready", AutoSize = true, ForeColor = Color.Gray };

        toolbar.Controls.AddRange([
            _restartBtn, _toggleBtn, _visualizerBtn,
            new Label { Text = "  ", AutoSize = true },
            _toggleStateLabel,
            new Label { Text = "  ", AutoSize = true },
            _statusLabel,
        ]);

        _toggleBtn.Click += async (s, e) =>
        {
            bool newState = !_roslynService.AreAnalyzersEnabled;
            await _roslynService.SetAnalyzersEnabledAsync(newState);
            _toggleBtn.Text = newState ? "Toggle Analyzers (ON)" : "Toggle Analyzers (OFF)";
            _toggleStateLabel.Text = newState ? "Analyzers: ON" : "Analyzers: OFF";
        };

        _editor = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            Text = @"using System;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello, Roslyn!"");
        }
    }
}"
        };
        _editor.TextChanged += (s, e) =>
        {
            if (_workspace.IsReady)
                _workspace.UpdateDocumentText(_editor.Text);
        };

        var eventLog = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
        };

        _roslynService.ProgressReported += msg =>
        {
            BeginInvoke(() =>
            {
                eventLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                _statusLabel.Text = msg;
            });
        };
        _roslynService.ErrorReported += msg =>
        {
            BeginInvoke(() =>
            {
                eventLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ERROR: {msg}");
                _statusLabel.Text = $"ERROR: {msg}";
                _statusLabel.ForeColor = Color.Red;
            });
        };
        _roslynService.DiagnosticsUpdated += diags =>
        {
            BeginInvoke(() =>
            {
                eventLog.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Diagnostics: {diags.Length} found");
            });
        };

        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        mainPanel.Controls.Add(toolbar, 0, 0);
        mainPanel.Controls.Add(_editor, 0, 1);
        mainPanel.Controls.Add(new Label { Text = "Event Log:", Dock = DockStyle.Fill }, 0, 2);
        mainPanel.Controls.Add(eventLog, 0, 3);

        Controls.Add(mainPanel);

        // Open the demo text as a Roslyn document
        _workspace.OpenDocument("demo.cs");

        Load += async (s, e) =>
        {
            await _roslynService.RestartAnalyzersAsync();
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _roslynService.Dispose();
        _workspace.Dispose();
        base.OnFormClosing(e);
    }
}

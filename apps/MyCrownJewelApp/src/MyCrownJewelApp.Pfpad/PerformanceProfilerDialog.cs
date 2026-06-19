using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class PerformanceProfilerDialog : Form
{
    private readonly Form1 _mainForm;
    private readonly Theme _theme;
    private readonly Panel _tabStrip;
    private readonly Panel _tabContent;
    private readonly Label _statusLabel;
    private readonly Label _durationLabel;
    private readonly System.Windows.Forms.Timer _sessionTimer;

    private readonly Button _timelineTabButton;
    private readonly Button _flameGraphTabButton;
    private readonly Button _memoryTabButton;
    private readonly Button _consoleTabButton;

    private readonly Panel _timelineTab;
    private readonly Panel _flameGraphTab;
    private readonly Panel _memoryTab;
    private readonly Panel _consoleTab;

#if PROFILING
    public SamplingEngine? _samplingEngine;
    private BinaryLogger? _binaryLogger;
#endif
#if PROFILING && DEBUG
    private DebugOverlay? _debugOverlay;
#endif

    private Button _startSamplingBtn = null!;
    private Button _stopSamplingBtn = null!;
    private Button _showOverlayBtn = null!;
    private ListView _timelineList = null!;
    private TextBox _timelineSummary = null!;

    private ListView _flameGraphList = null!;
    private TreeView _callStackTree = null!;
    private TextBox _flameSummary = null!;

    private Button _takeSnapshotBtn = null!;
    private Button _compareSnapshotsBtn = null!;
    private ListView _memoryList = null!;
    private TextBox _memorySummary = null!;

    private TextBox _consoleText = null!;

    private readonly List<MemorySnapshot> _snapshots = new();
    private readonly StringBuilder _consoleLogs = new();
    private DateTime? _samplingStartedAt;

    private enum ProfilerTab
    {
        Timeline,
        FlameGraph,
        Memory,
        Console
    }

    private ProfilerTab _activeTab = ProfilerTab.Timeline;

    public sealed record PerformanceEvent(
        string Name,
        DateTime StartTime,
        long DurationMs,
        string Category,
        string[] CallStack
    );

    public sealed record MemorySnapshot(
        DateTime Timestamp,
        long TotalMemory,
        long GCMemory,
        Dictionary<string, object> Details
    );

    public PerformanceProfilerDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        _theme = GetCurrentTheme();

        Text = "Performance Profiler";
        Size = new Size(1000, 700);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ShowIcon = false;
        BackColor = _theme.Background;
        ForeColor = _theme.Text;

        _timelineTabButton = CreateTabButton("⏱ Timeline", ProfilerTab.Timeline, active: true);
        _flameGraphTabButton = CreateTabButton("🔥 Flame Graph", ProfilerTab.FlameGraph, active: false);
        _memoryTabButton = CreateTabButton("💾 Memory", ProfilerTab.Memory, active: false);
        _consoleTabButton = CreateTabButton("📋 Console", ProfilerTab.Console, active: false);
        _tabStrip = CreateTabStrip();
        _tabContent = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Background, Padding = new Padding(12, 10, 12, 10) };
        _statusLabel = new Label { Dock = DockStyle.Left, Width = 220, TextAlign = ContentAlignment.MiddleLeft, ForeColor = _theme.Text };
        _durationLabel = new Label { Dock = DockStyle.Right, Width = 120, TextAlign = ContentAlignment.MiddleRight, ForeColor = _theme.Muted, Text = "00:00:00" };
        _sessionTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _sessionTimer.Tick += (_, _) => UpdateDurationLabel();

        _timelineTab = CreateTimelinePage();
        _flameGraphTab = CreateFlameGraphPage();
        _memoryTab = CreateMemoryPage();
        _consoleTab = CreateConsolePage();

#if PROFILING
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCrownJewelApp",
            "Pfpad",
            "PerformanceLogs"
        );
        _binaryLogger = new BinaryLogger(logDir);
        _samplingEngine = new SamplingEngine(_binaryLogger);
#endif

        _tabContent.Controls.AddRange(new Control[] { _consoleTab, _memoryTab, _flameGraphTab, _timelineTab });
        Controls.Add(_tabContent);
        Controls.Add(CreateStatusBar());
        Controls.Add(_tabStrip);

        UpdateTabState();
        SetStatus("● Ready", _theme.Text);
        Log("Performance Profiler initialized");

        Load += (_, _) => NativeThemed.ApplyThemeToChildScrollbars(this, !_theme.IsLight);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_theme.IsLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);

        ApplyScrollbarTheme();
    }

    private static Theme GetCurrentTheme()
    {
        try
        {
            return ThemeManager.Instance.CurrentTheme;
        }
        catch
        {
            return Theme.Light;
        }
    }

    private Panel CreateStatusBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = _theme.PanelBackground
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(_theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
        };

        panel.Controls.Add(_durationLabel);
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private Panel CreateTabStrip()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = _theme.MenuBackground
        };

        int left = 0;
        foreach (var button in new[] { _timelineTabButton, _flameGraphTabButton, _memoryTabButton, _consoleTabButton })
        {
            button.Left = left;
            button.Top = 0;
            left += button.Width;
            panel.Controls.Add(button);
        }

        panel.Paint += (_, e) =>
        {
            using var borderPen = new Pen(_theme.Border);
            e.Graphics.DrawLine(borderPen, 0, panel.Height - 1, panel.Width, panel.Height - 1);
            var activeButton = GetActiveTabButton();
            using var accentPen = new Pen(_theme.Accent, 2);
            int y = panel.Height - 2;
            e.Graphics.DrawLine(accentPen, activeButton.Left, y, activeButton.Right, y);
        };

        return panel;
    }

    private Button CreateTabButton(string text, ProfilerTab tab, bool active)
    {
        var button = new Button
        {
            Text = text,
            Width = tab == ProfilerTab.FlameGraph ? 160 : 140,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = active ? _theme.EditorBackground : Color.Transparent,
            ForeColor = active ? _theme.Text : _theme.Muted,
            Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = _theme.EditorBackground;
        button.Click += (_, _) => SwitchTab(tab);
        return button;
    }

    private Panel CreateTimelinePage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Background };
        var toolbar = CreateToolbarPanel();

        _startSamplingBtn = CreateToolbarButton("▶ Start Sampling", 150, isAccent: true, accentBackColor: Color.FromArgb(30, 180, 30), accentForeColor: Color.White);
        _startSamplingBtn.Click += StartSampling;
        _stopSamplingBtn = CreateToolbarButton("■ Stop", 100, isAccent: true, accentBackColor: Color.FromArgb(180, 40, 40), accentForeColor: Color.White);
        _stopSamplingBtn.Enabled = false;
        _stopSamplingBtn.Click += StopSampling;
        _showOverlayBtn = CreateToolbarButton("👁 Overlay", 110);
        _showOverlayBtn.Click += ShowOverlay;

        toolbar.Controls.Add(CreateToolbarFlow(_startSamplingBtn, _stopSamplingBtn, _showOverlayBtn));

        var split = CreateVerticalSplit();
        _timelineList = CreateListView();
        _timelineList.Columns.Add("Time", 120);
        _timelineList.Columns.Add("UI Thread", 80);
        _timelineList.Columns.Add("BG Thread", 80);
        _timelineList.Columns.Add("Total", 80);
        split.Panel1.Controls.Add(_timelineList);

        _timelineSummary = CreateReadOnlyTextBox();
        split.Panel2.Controls.Add(_timelineSummary);

        page.Controls.Add(split);
        page.Controls.Add(toolbar);
        return page;
    }

    private Panel CreateFlameGraphPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Background, Visible = false };

        var infoBar = CreateToolbarPanel();
        var infoLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Select a session from Timeline first",
            ForeColor = _theme.Muted,
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        infoBar.Controls.Add(infoLabel);

        var topSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            Panel1MinSize = 100,
            Panel2MinSize = 100,
            BackColor = _theme.Border
        };

        _flameGraphList = CreateListView();
        _flameGraphList.Columns.Add("Function", 250);
        _flameGraphList.Columns.Add("Samples", 80);
        _flameGraphList.Columns.Add("Total Time (ms)", 120);
        _flameGraphList.Columns.Add("Avg Time (ms)", 100);
        topSplit.Panel1.Controls.Add(_flameGraphList);

        _callStackTree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = _theme.EditorBackground,
            ForeColor = _theme.Text,
            BorderStyle = BorderStyle.None,
            HideSelection = false
        };
        topSplit.Panel2.Controls.Add(_callStackTree);

        var mainSplit = CreateVerticalSplit();
        mainSplit.Panel1.Controls.Add(topSplit);
        _flameSummary = CreateReadOnlyTextBox();
        mainSplit.Panel2.Controls.Add(_flameSummary);

        page.Controls.Add(mainSplit);
        page.Controls.Add(infoBar);
        return page;
    }

    private Panel CreateMemoryPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Background, Visible = false };
        var toolbar = CreateToolbarPanel();

        _takeSnapshotBtn = CreateToolbarButton("📷 Take Snapshot", 150);
        _takeSnapshotBtn.Click += TakeMemorySnapshot;
        _compareSnapshotsBtn = CreateToolbarButton("⬡ Compare", 110);
        _compareSnapshotsBtn.Enabled = false;
        _compareSnapshotsBtn.Click += CompareSnapshots;
        toolbar.Controls.Add(CreateToolbarFlow(_takeSnapshotBtn, _compareSnapshotsBtn));

        var split = CreateVerticalSplit();
        _memoryList = CreateListView();
        _memoryList.Columns.Add("Time", 100);
        _memoryList.Columns.Add("Total (MB)", 100);
        _memoryList.Columns.Add("GC (MB)", 100);
        _memoryList.Columns.Add("Details", 300);
        split.Panel1.Controls.Add(_memoryList);

        _memorySummary = CreateReadOnlyTextBox();
        split.Panel2.Controls.Add(_memorySummary);

        page.Controls.Add(split);
        page.Controls.Add(toolbar);
        return page;
    }

    private Panel CreateConsolePage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = _theme.Background, Visible = false };
        var toolbar = CreateToolbarPanel();
        var clearButton = CreateToolbarButton("🗑 Clear Console", 130);
        clearButton.Click += (_, _) =>
        {
            _consoleLogs.Clear();
            _consoleText.Clear();
            Log("Console cleared");
        };
        clearButton.Dock = DockStyle.Right;
        toolbar.Controls.Add(clearButton);

        _consoleText = CreateReadOnlyTextBox();
        _consoleText.Font = new Font("Consolas", 9f);

        page.Controls.Add(_consoleText);
        page.Controls.Add(toolbar);
        return page;
    }

    private Panel CreateToolbarPanel() => new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        Padding = new Padding(8, 5, 8, 5),
        BackColor = _theme.PanelBackground
    };

    private FlowLayoutPanel CreateToolbarFlow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.LeftToRight
        };
        flow.Controls.AddRange(controls);
        return flow;
    }

    private Button CreateToolbarButton(string text, int width, bool isAccent = false, Color? accentBackColor = null, Color? accentForeColor = null)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 28,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = isAccent ? accentBackColor ?? _theme.PanelBackground : _theme.PanelBackground,
            ForeColor = isAccent ? accentForeColor ?? _theme.Text : _theme.Text,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = _theme.Border;
        button.FlatAppearance.MouseOverBackColor = isAccent ? button.BackColor : _theme.ButtonHoverBackground;
        button.FlatAppearance.MouseDownBackColor = isAccent ? button.BackColor : _theme.ButtonHoverBackground;
        return button;
    }

    private SplitContainer CreateVerticalSplit() => new()
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Horizontal,
        SplitterWidth = 4,
        Panel1MinSize = 100,
        Panel2MinSize = 50,
        BackColor = _theme.Border,
        SplitterDistance = 380
    };

    private ListView CreateListView() => new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
        BorderStyle = BorderStyle.None,
        BackColor = _theme.EditorBackground,
        ForeColor = _theme.Text,
        HideSelection = false
    };

    private TextBox CreateReadOnlyTextBox() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        ScrollBars = ScrollBars.Vertical,
        BackColor = _theme.EditorBackground,
        ForeColor = _theme.Text,
        Font = new Font("Segoe UI", 9f)
    };

    private void SwitchTab(ProfilerTab tab)
    {
        if (_activeTab == tab)
            return;

        _activeTab = tab;
        UpdateTabState();
        _tabStrip.Invalidate();
    }

    private void UpdateTabState()
    {
        SetTabActive(_timelineTabButton, _activeTab == ProfilerTab.Timeline);
        SetTabActive(_flameGraphTabButton, _activeTab == ProfilerTab.FlameGraph);
        SetTabActive(_memoryTabButton, _activeTab == ProfilerTab.Memory);
        SetTabActive(_consoleTabButton, _activeTab == ProfilerTab.Console);

        _timelineTab.Visible = _activeTab == ProfilerTab.Timeline;
        _flameGraphTab.Visible = _activeTab == ProfilerTab.FlameGraph;
        _memoryTab.Visible = _activeTab == ProfilerTab.Memory;
        _consoleTab.Visible = _activeTab == ProfilerTab.Console;

        GetActivePage().BringToFront();
    }

    private void SetTabActive(Button button, bool active)
    {
        button.BackColor = active ? _theme.EditorBackground : Color.Transparent;
        button.ForeColor = active ? _theme.Text : _theme.Muted;
        button.Font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular);
    }

    private Button GetActiveTabButton() => _activeTab switch
    {
        ProfilerTab.FlameGraph => _flameGraphTabButton,
        ProfilerTab.Memory => _memoryTabButton,
        ProfilerTab.Console => _consoleTabButton,
        _ => _timelineTabButton
    };

    private Panel GetActivePage() => _activeTab switch
    {
        ProfilerTab.FlameGraph => _flameGraphTab,
        ProfilerTab.Memory => _memoryTab,
        ProfilerTab.Console => _consoleTab,
        _ => _timelineTab
    };

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }

    private void UpdateDurationLabel()
    {
        if (_samplingStartedAt.HasValue)
            _durationLabel.Text = (DateTime.Now - _samplingStartedAt.Value).ToString(@"hh\:mm\:ss");
        else
            _durationLabel.Text = "00:00:00";
    }

    private void ApplyScrollbarTheme()
    {
        bool isDark = !_theme.IsLight;
        foreach (var handle in new[]
                 {
                     _timelineList?.Handle ?? IntPtr.Zero,
                     _flameGraphList?.Handle ?? IntPtr.Zero,
                     _callStackTree?.Handle ?? IntPtr.Zero,
                     _memoryList?.Handle ?? IntPtr.Zero,
                     _timelineSummary?.Handle ?? IntPtr.Zero,
                     _flameSummary?.Handle ?? IntPtr.Zero,
                     _memorySummary?.Handle ?? IntPtr.Zero,
                     _consoleText?.Handle ?? IntPtr.Zero
                 })
        {
            if (handle != IntPtr.Zero)
                NativeThemed.ApplyDarkScrollbarTheme(handle, isDark);
        }
    }

    private void StartSampling(object? sender, EventArgs e)
    {
#if PROFILING
        if (_samplingEngine != null)
        {
            _samplingEngine.StartSampling(Thread.CurrentThread.ManagedThreadId);
            _startSamplingBtn.Enabled = false;
            _stopSamplingBtn.Enabled = true;
            _samplingStartedAt = DateTime.Now;
            _sessionTimer.Start();
            UpdateDurationLabel();
            SetStatus("● Sampling…", FlatUiHelper.SuccessColor(_theme));
            Log("Sampling started");
            return;
        }
#endif
        Log("Sampling is not available in this build.");
    }

    private async void StopSampling(object? sender, EventArgs e)
    {
#if PROFILING
        if (_samplingEngine != null)
        {
            _samplingEngine.StopSampling();

            _startSamplingBtn.Enabled = true;
            _stopSamplingBtn.Enabled = false;
            _sessionTimer.Stop();
            UpdateDurationLabel();
            SetStatus("● Stopped", FlatUiHelper.ErrorColor(_theme));
            Log("Sampling stopped");

            try
            {
                if (_binaryLogger != null)
                {
                    await _binaryLogger.FlushAsync();
                    string? logFilePath = _binaryLogger.CurrentFilePath;
                    _binaryLogger.CloseCurrentFile();
                    LoadAndAnalyzeSamples(logFilePath);
                }
            }
            catch (Exception ex)
            {
                Log($"Error closing log file: {ex.Message}");
            }
            finally
            {
                _samplingStartedAt = null;
                UpdateDurationLabel();
            }
            return;
        }
#endif
        Log("No active sampling session to stop.");
    }

    private void ShowOverlay(object? sender, EventArgs e)
    {
#if PROFILING && DEBUG
        if (_debugOverlay == null)
        {
            _debugOverlay = new DebugOverlay(_mainForm);
            _debugOverlay.Show();
            _showOverlayBtn.Text = "👁 Hide Overlay";
        }
        else
        {
            _debugOverlay.Close();
            _debugOverlay = null;
            _showOverlayBtn.Text = "👁 Overlay";
        }
#else
        Log("Overlay is only available in PROFILING + DEBUG builds.");
#endif
    }

    private void LoadAndAnalyzeSamples(string? logFilePath = null)
    {
#if PROFILING
        if (logFilePath == null && _binaryLogger?.CurrentFilePath == null) return;
        logFilePath ??= _binaryLogger?.CurrentFilePath;
        if (logFilePath == null) return;

        try
        {
            var samples = ReadPerformanceSamples(logFilePath);
            if (samples.Count == 0)
            {
                _timelineSummary.Text = "No performance samples found in log file.";
                return;
            }

            var analysis = AnalyzeSamples(samples);
            UpdateTimelineView(samples, analysis);
            UpdateFlameGraphView(samples, analysis);
            UpdateStatistics(analysis);
            Log($"Log analysis completed: {samples.Count} samples processed from {logFilePath}");
        }
        catch (Exception ex)
        {
            Log($"Error analyzing samples: {ex.Message}");
            _timelineSummary.Text = $"Error analyzing log file: {ex.Message}";
        }
#endif
    }

#if PROFILING
    private List<PerformanceSample> ReadPerformanceSamples(string filePath)
    {
        var samples = new List<PerformanceSample>();
        try
        {
            using var reader = new StreamReader(filePath);
            string? line;
            bool headerRead = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(line);
                    var root = jsonDoc.RootElement;

                    if (!headerRead && root.TryGetProperty("Header", out _))
                    {
                        headerRead = true;
                        continue;
                    }

                    var sample = new PerformanceSample
                    {
                        Timestamp = root.GetProperty("Timestamp").GetInt64(),
                        ThreadId = root.GetProperty("ThreadId").GetInt32(),
                        IsUiThread = root.GetProperty("IsUiThread").GetBoolean(),
                        CpuTimeTicks = root.GetProperty("CpuTimeTicks").GetInt64(),
                        WallTimeTicks = root.GetProperty("WallTimeTicks").GetInt64(),
                        GcCollectionCount = root.GetProperty("GcCollectionCount").GetInt32(),
                        GcMemoryBytes = root.GetProperty("GcMemoryBytes").GetInt64(),
                        ManagedThreadId = root.GetProperty("ManagedThreadId").GetInt32(),
                        ActivityId = root.GetProperty("ActivityId").GetUInt32(),
                        Flags = (PerformanceSample.SampleFlags)root.GetProperty("Flags").GetInt32()
                    };

                    if (root.TryGetProperty("StackTrace", out var stackTraceElement))
                    {
                        var stackLines = stackTraceElement.GetString()?.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                                                        ?? Array.Empty<string>();
                        sample.StackTraceFrames = stackLines;
                        sample.StackFrameCount = stackLines.Length;
                    }
                    else
                    {
                        sample.StackTraceFrames = Array.Empty<string>();
                        sample.StackFrameCount = 0;
                    }

                    samples.Add(sample);
                }
                catch (Exception ex)
                {
                    Log($"Error parsing sample: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error reading log file: {ex.Message}");
        }

        return samples;
    }

    private class SampleAnalysis
    {
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public int TotalSamples { get; set; }
        public int UniqueThreads { get; set; }
        public Dictionary<string, int> MethodCounts { get; } = new();
        public Dictionary<string, FlameNode> FlameGraph { get; } = new();
        public List<(long Timestamp, int ThreadId, string TopMethod)> TimelineEvents { get; } = new();
    }

    private SampleAnalysis AnalyzeSamples(List<PerformanceSample> samples)
    {
        var analysis = new SampleAnalysis();

        if (samples.Count == 0) return analysis;

        analysis.StartTimestamp = samples.Min(s => s.Timestamp);
        analysis.EndTimestamp = samples.Max(s => s.Timestamp);
        analysis.TotalSamples = samples.Count;
        analysis.UniqueThreads = samples.Select(s => s.ManagedThreadId).Distinct().Count();

        double ticksPerSecond = Stopwatch.Frequency;
        analysis.Duration = TimeSpan.FromSeconds((analysis.EndTimestamp - analysis.StartTimestamp) / ticksPerSecond);

        foreach (var sample in samples)
        {
            string topMethod = sample.StackFrameCount > 0 ? sample.StackTraceFrames[0] : "<unknown>";
            analysis.TimelineEvents.Add((sample.Timestamp, sample.ManagedThreadId, topMethod));

            foreach (var frame in sample.StackTraceFrames)
                analysis.MethodCounts[frame] = analysis.MethodCounts.GetValueOrDefault(frame, 0) + 1;

            BuildFlameNode(analysis.FlameGraph, sample.StackTraceFrames, 0);
        }

        return analysis;
    }

    private void BuildFlameNode(Dictionary<string, FlameNode> nodes, string[] stackTrace, int depth)
    {
        if (depth >= stackTrace.Length) return;

        string method = stackTrace[depth];
        if (!nodes.TryGetValue(method, out var node))
        {
            node = new FlameNode { Name = method, Count = 0 };
            nodes[method] = node;
        }

        node.Count++;
        BuildFlameNode(node.Children, stackTrace, depth + 1);
    }

    private void UpdateTimelineView(List<PerformanceSample> samples, SampleAnalysis analysis)
    {
        _timelineList.Items.Clear();

        long ticksPerWindow = Stopwatch.Frequency / 10;
        var windows = new Dictionary<long, List<PerformanceSample>>();

        foreach (var sample in samples)
        {
            long windowKey = sample.Timestamp / ticksPerWindow;
            if (!windows.TryGetValue(windowKey, out var windowSamples))
            {
                windowSamples = new List<PerformanceSample>();
                windows[windowKey] = windowSamples;
            }
            windowSamples.Add(sample);
        }

        foreach (var kvp in windows.OrderBy(kvp => kvp.Key))
        {
            long windowStart = kvp.Key * ticksPerWindow;
            double timeSeconds = (windowStart - analysis.StartTimestamp) / (double)Stopwatch.Frequency;

            var samplesInWindow = kvp.Value;
            int uiThreadSamples = samplesInWindow.Count(s => s.IsUiThread);
            int backgroundThreadSamples = samplesInWindow.Count - uiThreadSamples;

            var item = new ListViewItem(new[]
            {
                $"{timeSeconds:F2}s",
                uiThreadSamples.ToString(),
                backgroundThreadSamples.ToString(),
                samplesInWindow.Count.ToString()
            });
            _timelineList.Items.Add(item);
        }

        double samplesPerSecond = analysis.Duration.TotalSeconds > 0
            ? analysis.TotalSamples / analysis.Duration.TotalSeconds
            : analysis.TotalSamples;

        _timelineSummary.Text = $"Performance Analysis Summary:\r\n" +
                               $"Duration: {analysis.Duration.TotalSeconds:F2} seconds\r\n" +
                               $"Total Samples: {analysis.TotalSamples}\r\n" +
                               $"Unique Threads: {analysis.UniqueThreads}\r\n" +
                               $"Samples/Second: {samplesPerSecond:F1}\r\n" +
                               $"\r\nTop Methods:\r\n" +
                               string.Join("\r\n", analysis.MethodCounts
                                   .OrderByDescending(kvp => kvp.Value)
                                   .Take(10)
                                   .Select(kvp => $"{kvp.Key}: {kvp.Value} samples"));
    }

    private void UpdateFlameGraphView(List<PerformanceSample> samples, SampleAnalysis analysis)
    {
        _flameGraphList.Items.Clear();
        _callStackTree.Nodes.Clear();

        var rootNode = _callStackTree.Nodes.Add("Root", "Call Stacks");

        foreach (var kvp in analysis.FlameGraph.OrderByDescending(kvp => kvp.Value.Count))
        {
            var methodNode = rootNode.Nodes.Add(kvp.Key, $"{kvp.Key} ({kvp.Value.Count})");
            AddFlameChildren(methodNode, kvp.Value.Children);
        }

        _callStackTree.ExpandAll();

        foreach (var kvp in analysis.MethodCounts.OrderByDescending(kvp => kvp.Value))
        {
            var item = new ListViewItem(new[]
            {
                kvp.Key,
                kvp.Value.ToString(),
                kvp.Value.ToString(),
                "—"
            });
            _flameGraphList.Items.Add(item);
        }

        _flameSummary.Text = $"Flame Graph Analysis:\r\n" +
                             $"Total Methods: {analysis.MethodCounts.Count}\r\n" +
                             $"Max Stack Depth: {GetMaxDepth(analysis.FlameGraph)}\r\n" +
                             $"\r\nHot Path (most frequent call stack):\r\n" +
                             GetHottestPath(analysis.FlameGraph);
    }

    private void AddFlameChildren(TreeNode parent, Dictionary<string, FlameNode> children)
    {
        foreach (var kvp in children.OrderByDescending(kvp => kvp.Value.Count))
        {
            var childNode = parent.Nodes.Add(kvp.Key, $"{kvp.Key} ({kvp.Value.Count})");
            AddFlameChildren(childNode, kvp.Value.Children);
        }
    }

    private int GetMaxDepth(Dictionary<string, FlameNode> nodes)
    {
        if (nodes.Count == 0) return 0;
        return 1 + nodes.Values.Max(node => GetMaxDepth(node.Children));
    }

    private string GetHottestPath(Dictionary<string, FlameNode> nodes)
    {
        if (nodes.Count == 0) return "No data";

        var hottest = nodes.OrderByDescending(kvp => kvp.Value.Count).First();
        var path = new List<string> { hottest.Key };
        var currentChildren = hottest.Value.Children;

        while (currentChildren.Count > 0)
        {
            var next = currentChildren.OrderByDescending(kvp => kvp.Value.Count).First();
            path.Add(next.Key);
            currentChildren = next.Value.Children;
        }

        return string.Join(" → ", path);
    }

    private void UpdateStatistics(SampleAnalysis analysis)
    {
        Log($"Analysis complete: {analysis.TotalSamples} samples, {analysis.MethodCounts.Count} methods");
    }
#endif

    private class FlameNode
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public Dictionary<string, FlameNode> Children { get; } = new();
    }

    private void ClearPerformanceData()
    {
        _timelineList?.Items.Clear();
        _flameGraphList?.Items.Clear();
        _callStackTree?.Nodes.Clear();
        _timelineSummary?.Clear();
        _flameSummary?.Clear();
        Log("Performance data cleared");
    }

    private void TakeMemorySnapshot(object? sender, EventArgs e)
    {
        var snapshot = new MemorySnapshot(
            DateTime.Now,
            GC.GetTotalMemory(false),
            GC.GetTotalMemory(true) - GC.GetTotalMemory(false),
            new Dictionary<string, object>
            {
                ["GC Collections"] = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
                ["Process Memory"] = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024
            }
        );
        _snapshots.Add(snapshot);
        UpdateMemoryUI();
        _compareSnapshotsBtn.Enabled = _snapshots.Count >= 2;
        Log($"Memory snapshot taken: {snapshot.TotalMemory / 1024 / 1024} MB");
    }

    private void CompareSnapshots(object? sender, EventArgs e)
    {
        if (_snapshots.Count < 2) return;
        var last = _snapshots[^1];
        var prev = _snapshots[^2];
        var diff = last.TotalMemory - prev.TotalMemory;
        _memorySummary.Text = $"Memory Comparison:\r\n" +
                              $"Previous: {prev.TotalMemory / 1024 / 1024} MB\r\n" +
                              $"Current: {last.TotalMemory / 1024 / 1024} MB\r\n" +
                              $"Difference: {diff / 1024 / 1024} MB {(diff > 0 ? "increase" : "decrease")}\r\n" +
                              $"GC Collections: {last.Details["GC Collections"]} total";
    }

    private void UpdateMemoryUI()
    {
        _memoryList.BeginUpdate();
        _memoryList.Items.Clear();
        foreach (var snap in _snapshots)
        {
            var item = new ListViewItem(snap.Timestamp.ToString("HH:mm:ss"));
            item.SubItems.Add((snap.TotalMemory / 1024 / 1024d).ToString("F2"));
            item.SubItems.Add((snap.GCMemory / 1024 / 1024d).ToString("F2"));
            item.SubItems.Add(string.Join(", ", snap.Details.Select(kv => $"{kv.Key}: {kv.Value}")));
            _memoryList.Items.Add(item);
        }
        _memoryList.EndUpdate();
    }

    public void Log(string message)
    {
        var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n";
        _consoleLogs.Append(logLine);
        if (_consoleText.IsHandleCreated)
            BeginInvoke(() => _consoleText.AppendText(logLine));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionTimer.Dispose();
#if PROFILING
            _samplingEngine?.Dispose();
            _binaryLogger?.Dispose();
#endif
#if PROFILING && DEBUG
            _debugOverlay?.Dispose();
#endif
        }
        base.Dispose(disposing);
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.IO;

namespace MyCrownJewelApp.Pfpad;

public sealed class PerformanceProfilerDialog : Form
{
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly Form1 _mainForm;

    private readonly TabControl _tabControl;
    private readonly TabPage _timelineTab;
    private readonly TabPage _flameGraphTab;
    private readonly TabPage _memoryTab;
    private readonly TabPage _consoleTab;

#if PROFILING
    // Sampling profiler components
    public SamplingEngine? _samplingEngine;
    private BinaryLogger? _binaryLogger;
#endif
#if PROFILING && DEBUG
    private DebugOverlay? _debugOverlay;
#endif

    // Timeline tab controls
    private Button _startSamplingBtn;
    private Button _stopSamplingBtn;
    private Button _showOverlayBtn;
    private ListView _timelineList;
    private TextBox _timelineSummary;

    // Flame graph tab controls
    private ListView _flameGraphList;
    private TreeView _callStackTree;
    private TextBox _flameSummary;

    // Memory tab controls (legacy - will be replaced)
    private Button _takeSnapshotBtn;
    private Button _compareSnapshotsBtn;
    private ListView _memoryList;
    private TextBox _memorySummary;

    // Console tab
    private TextBox _consoleText;

    // Profiling data
    private readonly List<MemorySnapshot> _snapshots = new();
    private readonly StringBuilder _consoleLogs = new();

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
        Text = "Performance Profiler";
        Size = new Size(1000, 700);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;

        Theme theme;
        try
        {
            theme = ThemeManager.Instance.CurrentTheme;
        }
        catch
        {
            // Fallback to light theme
            theme = Theme.Light;
        }
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _tabControl = new TabControl { Dock = DockStyle.Fill, BackColor = theme.Background, ForeColor = theme.Text };
        Controls.Add(_tabControl);

        // Timeline Tab
        _timelineTab = new TabPage("Timeline") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_timelineTab);

        // Flame Graph Tab
        _flameGraphTab = new TabPage("Flame Graph") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_flameGraphTab);

        // Memory Tab
        _memoryTab = new TabPage("Memory") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_memoryTab);

        // Console Tab
        _consoleTab = new TabPage("Console") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_consoleTab);

#if PROFILING
        // Initialize sampling profiler
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyCrownJewelApp",
            "Pfpad",
            "PerformanceLogs"
        );
        _binaryLogger = new BinaryLogger(logDir);
        _samplingEngine = new SamplingEngine(_binaryLogger);
#endif

        _consoleText = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            ScrollBars = ScrollBars.Vertical,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text
        };
        _consoleTab.Controls.Add(_consoleText);

        // Timeline tab UI - make it resizable with SplitContainer
        var timelineButtonPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.Background };
        _startSamplingBtn = new Button { Text = "Start Sampling", Dock = DockStyle.Left, Width = 120, BackColor = theme.Background, ForeColor = theme.Text };
        _startSamplingBtn.Click += StartSampling;

        _stopSamplingBtn = new Button { Text = "Stop Sampling", Dock = DockStyle.Left, Width = 120, Enabled = false, BackColor = theme.Background, ForeColor = theme.Text };
        _stopSamplingBtn.Click += StopSampling;

        _showOverlayBtn = new Button { Text = "Show Overlay", Dock = DockStyle.Left, Width = 100, BackColor = theme.Background, ForeColor = theme.Text };
        _showOverlayBtn.Click += ShowOverlay;

        timelineButtonPanel.Controls.Add(_startSamplingBtn);
        timelineButtonPanel.Controls.Add(_stopSamplingBtn);
        timelineButtonPanel.Controls.Add(_showOverlayBtn);

        var timelineSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 4,
            Panel1MinSize = 100,
            Panel2MinSize = 50
        };

        _timelineList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _timelineList.Columns.Add("Time", 120);
        _timelineList.Columns.Add("UI Thread", 80);
        _timelineList.Columns.Add("BG Thread", 80);
        _timelineList.Columns.Add("Total", 80);
        timelineSplit.Panel1.Controls.Add(_timelineList);

        _timelineSummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text, ScrollBars = ScrollBars.Vertical };
        timelineSplit.Panel2.Controls.Add(_timelineSummary);

        // Set initial splitter position (70% top, 30% bottom)
        timelineSplit.SplitterDistance = (int)(timelineSplit.Height * 0.7f);

        var timelineContainer = new Panel { Dock = DockStyle.Fill };
        timelineContainer.Controls.Add(timelineSplit);
        timelineContainer.Controls.Add(timelineButtonPanel);

        _timelineTab.Controls.Add(timelineContainer);

        // Flame Graph tab UI - make it resizable
        var flameTopSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 4,
            Panel1MinSize = 100,
            Panel2MinSize = 100
        };

        _flameGraphList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _flameGraphList.Columns.Add("Function", 250);
        _flameGraphList.Columns.Add("Samples", 80);
        _flameGraphList.Columns.Add("Total Time (ms)", 120);
        _flameGraphList.Columns.Add("Avg Time (ms)", 100);
        flameTopSplit.Panel1.Controls.Add(_flameGraphList);

        _callStackTree = new TreeView { Dock = DockStyle.Fill, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        flameTopSplit.Panel2.Controls.Add(_callStackTree);

        // Set initial splitter position (70% left, 30% right)
        flameTopSplit.SplitterDistance = (int)(flameTopSplit.Width * 0.7f);

        var flameMainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 4,
            Panel1MinSize = 100,
            Panel2MinSize = 50
        };

        flameMainSplit.Panel1.Controls.Add(flameTopSplit);

        _flameSummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text, ScrollBars = ScrollBars.Vertical };
        flameMainSplit.Panel2.Controls.Add(_flameSummary);

        // Set initial splitter position (70% top, 30% bottom)
        flameMainSplit.SplitterDistance = (int)(flameMainSplit.Height * 0.7f);

        _flameGraphTab.Controls.Add(flameMainSplit);

        // Memory tab UI - make it resizable
        var memButtonPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = theme.Background };
        _takeSnapshotBtn = new Button { Text = "Take Snapshot", Dock = DockStyle.Left, Width = 120, BackColor = theme.Background, ForeColor = theme.Text };
        _takeSnapshotBtn.Click += TakeMemorySnapshot;
        memButtonPanel.Controls.Add(_takeSnapshotBtn);

        _compareSnapshotsBtn = new Button { Text = "Compare", Dock = DockStyle.Left, Width = 120, Enabled = false, BackColor = theme.Background, ForeColor = theme.Text };
        _compareSnapshotsBtn.Click += CompareSnapshots;
        memButtonPanel.Controls.Add(_compareSnapshotsBtn);

        var memSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 4,
            Panel1MinSize = 100,
            Panel2MinSize = 50
        };

        _memoryList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _memoryList.Columns.Add("Time", 100);
        _memoryList.Columns.Add("Total (MB)", 100);
        _memoryList.Columns.Add("GC (MB)", 100);
        _memoryList.Columns.Add("Details", 300);
        memSplit.Panel1.Controls.Add(_memoryList);

        _memorySummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text, ScrollBars = ScrollBars.Vertical };
        memSplit.Panel2.Controls.Add(_memorySummary);

        // Set initial splitter position (70% top, 30% bottom)
        memSplit.SplitterDistance = (int)(memSplit.Height * 0.7f);

        var memContainer = new Panel { Dock = DockStyle.Fill };
        memContainer.Controls.Add(memSplit);
        memContainer.Controls.Add(memButtonPanel);

        _memoryTab.Controls.Add(memContainer);

        // Start logging
        Log("Performance Profiler initialized");
    }

    private void StartSampling(object? sender, EventArgs e)
    {
#if PROFILING
        if (_samplingEngine != null)
        {
            _samplingEngine.StartSampling(Thread.CurrentThread.ManagedThreadId);
            _startSamplingBtn.Enabled = false;
            _stopSamplingBtn.Enabled = true;
            Log("Sampling started");
        }
#endif
    }

    private async void StopSampling(object? sender, EventArgs e)
    {
#if PROFILING
        if (_samplingEngine != null)
        {
            _samplingEngine.StopSampling();

            _startSamplingBtn.Enabled = true;
            _stopSamplingBtn.Enabled = false;
            Log("Sampling stopped");

            // Ensure the log file is properly closed before reading (async to avoid blocking UI)
            try
            {
                if (_binaryLogger != null)
                {
                    await _binaryLogger.FlushAsync();
                    string? logFilePath = _binaryLogger.CurrentFilePath; // Store path before closing
                    _binaryLogger.CloseCurrentFile();

                    // Load and analyze the log file
                    LoadAndAnalyzeSamples(logFilePath);
                }
            }
            catch (Exception ex)
            {
                Log($"Error closing log file: {ex.Message}");
            }
        }
#endif
    }

    private void ShowOverlay(object? sender, EventArgs e)
    {
#if PROFILING && DEBUG
        if (_debugOverlay == null)
        {
            _debugOverlay = new DebugOverlay(_mainForm);
            _debugOverlay.Show();
            _showOverlayBtn.Text = "Hide Overlay";
        }
        else
        {
            _debugOverlay.Close();
            _debugOverlay = null;
            _showOverlayBtn.Text = "Show Overlay";
        }
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
            // Read and analyze the JSONL log file
            var samples = ReadPerformanceSamples(logFilePath);
            if (samples.Count == 0)
            {
                _timelineSummary.Text = "No performance samples found in log file.";
                return;
            }

            // Analyze the samples
            var analysis = AnalyzeSamples(samples);

            // Update UI with results
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
                    // Parse JSON line
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(line);
                    var root = jsonDoc.RootElement;

                    // Skip header
                    if (!headerRead && root.TryGetProperty("Header", out _))
                    {
                        headerRead = true;
                        continue;
                    }

                    // Parse sample
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

                    // Parse stack trace
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

        // Convert timestamps to TimeSpan for duration calculation
        double ticksPerSecond = Stopwatch.Frequency;
        analysis.Duration = TimeSpan.FromSeconds((analysis.EndTimestamp - analysis.StartTimestamp) / ticksPerSecond);

        // Analyze methods and build flame graph
        foreach (var sample in samples)
        {
            // Extract top method for timeline
            string topMethod = sample.StackFrameCount > 0 ? sample.StackTraceFrames[0] : "<unknown>";
            analysis.TimelineEvents.Add((sample.Timestamp, sample.ManagedThreadId, topMethod));

            // Count method occurrences
            foreach (var frame in sample.StackTraceFrames)
            {
                analysis.MethodCounts[frame] = analysis.MethodCounts.GetValueOrDefault(frame, 0) + 1;
            }

            // Build flame graph
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

        // Group samples by time windows (e.g., 100ms windows)
        long ticksPerWindow = Stopwatch.Frequency / 10; // 100ms windows
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

            var item = new ListViewItem(new[] {
                $"{timeSeconds:F2}s",
                uiThreadSamples.ToString(),
                backgroundThreadSamples.ToString(),
                samplesInWindow.Count.ToString()
            });
            _timelineList.Items.Add(item);
        }

        _timelineSummary.Text = $"Performance Analysis Summary:\r\n" +
                               $"Duration: {analysis.Duration.TotalSeconds:F2} seconds\r\n" +
                               $"Total Samples: {analysis.TotalSamples}\r\n" +
                               $"Unique Threads: {analysis.UniqueThreads}\r\n" +
                               $"Samples/Second: {analysis.TotalSamples / analysis.Duration.TotalSeconds:F1}\r\n" +
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

        // Build tree view of flame graph
        var rootNode = _callStackTree.Nodes.Add("Root", "Call Stacks");

        foreach (var kvp in analysis.FlameGraph.OrderByDescending(kvp => kvp.Value.Count))
        {
            var methodNode = rootNode.Nodes.Add(kvp.Key, $"{kvp.Key} ({kvp.Value.Count})");
            AddFlameChildren(methodNode, kvp.Value.Children);
        }

        _callStackTree.ExpandAll();

        // Build list view of top methods
        foreach (var kvp in analysis.MethodCounts.OrderByDescending(kvp => kvp.Value))
        {
            var item = new ListViewItem(new[] { kvp.Key, kvp.Value.ToString() });
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
        // Could add more statistical views here
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
            item.SubItems.Add((snap.TotalMemory / 1024 / 1024).ToString("F2"));
            item.SubItems.Add((snap.GCMemory / 1024 / 1024).ToString("F2"));
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
        {
            BeginInvoke(() => _consoleText.AppendText(logLine));
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme theme;
        try
        {
            theme = ThemeManager.Instance.CurrentTheme;
        }
        catch
        {
            theme = Theme.Light;
        }
        var isLight = theme.IsLight;
        if (!isLight)
            NativeThemed.ApplyDarkModeToWindow(Handle);

        // Apply scrollbar themes
        SetWindowTheme(_tabControl.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_timelineList?.Handle ?? IntPtr.Zero, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_flameGraphList?.Handle ?? IntPtr.Zero, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_callStackTree?.Handle ?? IntPtr.Zero, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_memoryList.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_timelineSummary?.Handle ?? IntPtr.Zero, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_flameSummary?.Handle ?? IntPtr.Zero, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_memorySummary.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        SetWindowTheme(_consoleText.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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
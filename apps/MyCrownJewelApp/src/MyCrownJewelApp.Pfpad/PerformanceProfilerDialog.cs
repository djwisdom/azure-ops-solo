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

        // Timeline tab UI
        var timelineLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = theme.Background
        };
        timelineLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Buttons
        timelineLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70)); // Timeline
        timelineLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Summary

        var timelineButtonPanel = new Panel { Dock = DockStyle.Fill };
        _startSamplingBtn = new Button { Text = "Start Sampling", Dock = DockStyle.Left, Width = 120, BackColor = theme.Background, ForeColor = theme.Text };
        _startSamplingBtn.Click += StartSampling;

        _stopSamplingBtn = new Button { Text = "Stop Sampling", Dock = DockStyle.Left, Width = 120, Enabled = false, BackColor = theme.Background, ForeColor = theme.Text };
        _stopSamplingBtn.Click += StopSampling;

        _showOverlayBtn = new Button { Text = "Show Overlay", Dock = DockStyle.Left, Width = 100, BackColor = theme.Background, ForeColor = theme.Text };
        _showOverlayBtn.Click += ShowOverlay;

        timelineButtonPanel.Controls.Add(_startSamplingBtn);
        timelineButtonPanel.Controls.Add(_stopSamplingBtn);
        timelineButtonPanel.Controls.Add(_showOverlayBtn);
        timelineLayout.Controls.Add(timelineButtonPanel, 0, 0);

        _timelineList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _timelineList.Columns.Add("Time", 120);
        _timelineList.Columns.Add("Thread", 80);
        _timelineList.Columns.Add("CPU (ms)", 80);
        _timelineList.Columns.Add("GC Mem (MB)", 100);
        _timelineList.Columns.Add("Flags", 100);
        _timelineList.Columns.Add("Stack", 300);
        timelineLayout.Controls.Add(_timelineList, 1, 0);

        _timelineSummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        timelineLayout.Controls.Add(_timelineSummary, 2, 0);

        _timelineTab.Controls.Add(timelineLayout);

        // Flame Graph tab UI
        var flameLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 2,
            BackColor = theme.Background
        };
        flameLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 70)); // Charts
        flameLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Summary
        flameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); // Left column
        flameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); // Right column

        _flameGraphList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _flameGraphList.Columns.Add("Function", 250);
        _flameGraphList.Columns.Add("Samples", 80);
        _flameGraphList.Columns.Add("Total Time (ms)", 120);
        _flameGraphList.Columns.Add("Avg Time (ms)", 100);
        flameLayout.Controls.Add(_flameGraphList, 0, 0);

        _callStackTree = new TreeView { Dock = DockStyle.Fill, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        flameLayout.Controls.Add(_callStackTree, 1, 0);

        _flameSummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        flameLayout.Controls.Add(_flameSummary, 0, 1);
        flameLayout.SetColumnSpan(_flameSummary, 2);

        _flameGraphTab.Controls.Add(flameLayout);

        // Memory tab UI
        var memLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = theme.Background
        };
        memLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Buttons
        memLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); // List
        memLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Summary

        var memButtonPanel = new Panel { Dock = DockStyle.Fill };
        _takeSnapshotBtn = new Button { Text = "Take Snapshot", Dock = DockStyle.Left, Width = 120, BackColor = theme.Background, ForeColor = theme.Text };
        _takeSnapshotBtn.Click += TakeMemorySnapshot;
        memButtonPanel.Controls.Add(_takeSnapshotBtn);

        _compareSnapshotsBtn = new Button { Text = "Compare", Dock = DockStyle.Left, Width = 120, Enabled = false, BackColor = theme.Background, ForeColor = theme.Text };
        _compareSnapshotsBtn.Click += CompareSnapshots;
        memButtonPanel.Controls.Add(_compareSnapshotsBtn);

        memLayout.Controls.Add(memButtonPanel, 0, 0);

        _memoryList = new ListView { Dock = DockStyle.Fill, View = View.Details, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        _memoryList.Columns.Add("Time", 100);
        _memoryList.Columns.Add("Total (MB)", 100);
        _memoryList.Columns.Add("GC (MB)", 100);
        _memoryList.Columns.Add("Details", 300);
        memLayout.Controls.Add(_memoryList, 0, 1);

        _memorySummary = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = theme.EditorBackground, ForeColor = theme.Text };
        memLayout.Controls.Add(_memorySummary, 0, 2);

        _memoryTab.Controls.Add(memLayout);

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

    private void StopSampling(object? sender, EventArgs e)
    {
#if PROFILING
        if (_samplingEngine != null)
        {
            _samplingEngine.StopSampling();
            _startSamplingBtn.Enabled = true;
            _stopSamplingBtn.Enabled = false;
            Log("Sampling stopped");

            // Load and analyze the log file
            LoadAndAnalyzeSamples();
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

    private void LoadAndAnalyzeSamples()
    {
#if PROFILING
        if (_binaryLogger?.CurrentFilePath == null) return;

        try
        {
            // In a real implementation, you would read the binary log file
            // For now, show a placeholder message
            _timelineSummary.Text = $"Log file created: {_binaryLogger.CurrentFilePath}\r\n\r\n" +
                                   "To analyze the binary log file, you would implement a log reader\r\n" +
                                   "that deserializes PerformanceSample structs and builds\r\n" +
                                   "timeline views, flame graphs, and statistical summaries.\r\n\r\n" +
                                   "This would include:\r\n" +
                                   "- Timeline visualization of thread activity\r\n" +
                                   "- Flame graph construction from stack traces\r\n" +
                                   "- Statistical analysis of performance metrics\r\n" +
                                   "- Identification of performance bottlenecks";

            Log($"Log analysis completed: {_binaryLogger.CurrentFilePath}");
        }
        catch (Exception ex)
        {
            Log($"Error analyzing samples: {ex.Message}");
        }
#endif
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
            _debugOverlay?.Dispose();
#endif
        }
        base.Dispose(disposing);
    }
}
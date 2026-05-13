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

namespace MyCrownJewelApp.Pfpad;

public sealed class PerformanceProfilerDialog : Form
{
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private const string DARK_MODE_SCROLLBAR = "DarkMode_Explorer";

    private readonly Form1 _mainForm;

    private readonly TabControl _tabControl;
    private readonly TabPage _performanceTab;
    private readonly TabPage _memoryTab;
    private readonly TabPage _consoleTab;

    // Performance tab controls
    private Button? _startRecordingBtn;
    private Button? _stopRecordingBtn;
    private ListView? _flameChartList;
    private TreeView? _callTreeView;
    private TextBox? _summaryText;

    // Memory tab controls
    private Button? _takeSnapshotBtn;
    private Button? _compareSnapshotsBtn;
    private ListView? _memoryList;
    private TextBox? _memorySummary;

    // Console tab
    private TextBox? _consoleText;
#pragma warning restore CS8618, CS0649

    // Profiling data
    private readonly List<PerformanceEvent> _events = new();
    private readonly List<MemorySnapshot> _snapshots = new();
    private readonly StringBuilder _consoleLogs = new();
    private bool _isRecording;
    private Stopwatch _sessionTimer = new();

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

        // Performance Tab
        _performanceTab = new TabPage("Performance") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_performanceTab);

        // Memory Tab
        _memoryTab = new TabPage("Memory") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_memoryTab);

        // Console Tab
        _consoleTab = new TabPage("Console") { BackColor = theme.Background, ForeColor = theme.Text };
        _tabControl.TabPages.Add(_consoleTab);

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

        // Start logging
        Log("Performance Profiler initialized");
    }

    private void StartRecording(object? sender, EventArgs e)
    {
        _isRecording = true;
        _sessionTimer.Restart();
        if (_startRecordingBtn != null) _startRecordingBtn.Enabled = false;
        if (_stopRecordingBtn != null) _stopRecordingBtn.Enabled = true;
        _events.Clear();
        Log("Recording started");
    }

    private void StopRecording(object? sender, EventArgs e)
    {
        _isRecording = false;
        _sessionTimer.Stop();
        if (_startRecordingBtn != null) _startRecordingBtn.Enabled = true;
        if (_stopRecordingBtn != null) _stopRecordingBtn.Enabled = false;
        UpdatePerformanceUI();
        Log("Recording stopped");
    }

    private void ClearPerformanceData()
    {
        _events.Clear();
        if (_flameChartList != null) _flameChartList.Items.Clear();
        if (_callTreeView != null) _callTreeView.Nodes.Clear();
        if (_summaryText != null) _summaryText.Clear();
        Log("Performance data cleared");
    }

    public void RecordEvent(string name, string category, Action action)
    {
        if (!_isRecording) { action(); return; }

        var sw = Stopwatch.StartNew();
        var startTime = DateTime.Now;
        var callStack = new StackTrace().GetFrames().Select(f => f.GetMethod()?.Name ?? "").ToArray();

        try
        {
            action();
        }
        finally
        {
            sw.Stop();
            var duration = sw.ElapsedMilliseconds;
            if (duration > 50) // Long task threshold
            {
                _events.Add(new PerformanceEvent(name, startTime, duration, category, callStack));
                Log($"Long task: {name} ({duration}ms)");
            }
        }
    }

    private void UpdatePerformanceUI()
    {
        if (_flameChartList != null)
        {
            _flameChartList.BeginUpdate();
            _flameChartList.Items.Clear();
            foreach (var evt in _events.OrderByDescending(e => e.DurationMs))
            {
                var item = new ListViewItem(evt.Name);
                item.SubItems.Add(evt.DurationMs.ToString());
                item.SubItems.Add(evt.StartTime.ToString("HH:mm:ss.fff"));
                item.SubItems.Add(evt.Category);
                item.Tag = evt;
                _flameChartList.Items.Add(item);
            }
            _flameChartList.EndUpdate();
        }

        // Build call tree
        if (_callTreeView != null)
        {
            _callTreeView.Nodes.Clear();
            var root = _callTreeView.Nodes.Add("Root");
            foreach (var evt in _events)
            {
                var node = root.Nodes.Add($"{evt.Name} ({evt.DurationMs}ms)");
                foreach (var frame in evt.CallStack.Take(5))
                {
                    node.Nodes.Add(frame);
                }
            }
            _callTreeView.ExpandAll();
        }

        // Summary
        if (_summaryText != null)
        {
            var totalTime = _events.Sum(e => e.DurationMs);
            var longTasks = _events.Count(e => e.DurationMs > 50);
            _summaryText.Text = $"Total recorded time: {_sessionTimer.Elapsed.TotalSeconds:F2}s\r\n" +
                               $"Long tasks (>50ms): {longTasks}\r\n" +
                               $"Total long task time: {totalTime}ms\r\n" +
                               $"Average long task: {(longTasks > 0 ? totalTime / longTasks : 0)}ms";
        }
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
        if (_compareSnapshotsBtn != null) _compareSnapshotsBtn.Enabled = _snapshots.Count >= 2;
        Log($"Memory snapshot taken: {snapshot.TotalMemory / 1024 / 1024} MB");
    }

    private void CompareSnapshots(object? sender, EventArgs e)
    {
        if (_snapshots.Count < 2 || _memorySummary == null) return;
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
        if (_memoryList == null) return;
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
        if (_consoleText != null && _consoleText.IsHandleCreated)
        {
            BeginInvoke(() => _consoleText?.AppendText(logLine));
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
        if (_tabControl != null) SetWindowTheme(_tabControl.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_flameChartList != null) SetWindowTheme(_flameChartList.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_callTreeView != null) SetWindowTheme(_callTreeView.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_memoryList != null) SetWindowTheme(_memoryList.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_summaryText != null) SetWindowTheme(_summaryText.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_memorySummary != null) SetWindowTheme(_memorySummary.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
        if (_consoleText != null) SetWindowTheme(_consoleText.Handle, isLight ? "" : DARK_MODE_SCROLLBAR, null);
    }
}
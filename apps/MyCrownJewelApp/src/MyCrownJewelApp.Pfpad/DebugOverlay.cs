using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace MyCrownJewelApp.Pfpad;

#if PROFILING && DEBUG

/// <summary>
/// Debug-only real-time overlay showing FPS and memory usage.
/// Positioned over the main form with transparent background.
/// </summary>
public sealed class DebugOverlay : Form
{
    private readonly Form _mainForm;
    private readonly Timer _updateTimer;
    private readonly Label _fpsLabel;
    private readonly Label _memoryLabel;
    private readonly Label _gcLabel;

    private long _lastFrameTime;
    private int _frameCount;
    private float _currentFps;

    public DebugOverlay(Form mainForm)
    {
        _mainForm = mainForm;

        // Overlay form setup
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(180, 0, 0, 0); // Semi-transparent black
        TransparencyKey = Color.FromArgb(0, 255, 0, 0); // Use magenta as transparent
        Size = new Size(300, 80);
        StartPosition = FormStartPosition.Manual;

        // Position in top-right corner of main form
        Location = new Point(
            _mainForm.Location.X + _mainForm.Width - Width - 10,
            _mainForm.Location.Y + 10
        );

        // Labels
        _fpsLabel = new Label
        {
            ForeColor = Color.Lime,
            BackColor = Color.Transparent,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 5)
        };

        _memoryLabel = new Label
        {
            ForeColor = Color.Cyan,
            BackColor = Color.Transparent,
            Font = new Font("Consolas", 9),
            AutoSize = true,
            Location = new Point(10, 25)
        };

        _gcLabel = new Label
        {
            ForeColor = Color.Yellow,
            BackColor = Color.Transparent,
            Font = new Font("Consolas", 9),
            AutoSize = true,
            Location = new Point(10, 45)
        };

        Controls.AddRange(new Control[] { _fpsLabel, _memoryLabel, _gcLabel });

        // Update timer
        _updateTimer = new Timer { Interval = 1000 }; // Update every second
        _updateTimer.Tick += UpdateStats;
        _updateTimer.Start();

        // Handle main form movement
        _mainForm.LocationChanged += MainForm_LocationChanged;
        _mainForm.Resize += MainForm_Resize;

        _lastFrameTime = Stopwatch.GetTimestamp();
    }

    private void MainForm_LocationChanged(object? sender, EventArgs e)
    {
        UpdatePosition();
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_mainForm.WindowState == FormWindowState.Minimized) return;

        Location = new Point(
            _mainForm.Location.X + _mainForm.Width - Width - 10,
            _mainForm.Location.Y + 10
        );
    }

    /// <summary>
    /// Call this method on each UI frame to track FPS.
    /// </summary>
    public void OnFrame()
    {
        _frameCount++;
        long now = Stopwatch.GetTimestamp();
        long elapsedTicks = now - _lastFrameTime;

        if (elapsedTicks >= Stopwatch.Frequency) // Update FPS every second
        {
            _currentFps = (float)(_frameCount * Stopwatch.Frequency) / elapsedTicks;
            _frameCount = 0;
            _lastFrameTime = now;
        }
    }

    private void UpdateStats(object? sender, EventArgs e)
    {
        try
        {
            // FPS
            _fpsLabel.Text = $"FPS: {_currentFps:F1}";

            // Memory
            using var process = Process.GetCurrentProcess();
            long workingSet = process.WorkingSet64;
            long privateMemory = process.PrivateMemorySize64;
            _memoryLabel.Text = $"Mem: {workingSet / 1024 / 1024}MB WS, {privateMemory / 1024 / 1024}MB PM";

            // GC
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            long gcMemory = GC.GetTotalMemory(false);
            _gcLabel.Text = $"GC: G0:{gen0} G1:{gen1} G2:{gen2} Mem:{gcMemory / 1024 / 1024}MB";
        }
        catch
        {
            // Ignore errors in debug overlay
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _updateTimer.Stop();
        _updateTimer.Dispose();
        _mainForm.LocationChanged -= MainForm_LocationChanged;
        _mainForm.Resize -= MainForm_Resize;
        base.OnFormClosing(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_NOACTIVATE prevents the overlay from stealing focus
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
}

#endif
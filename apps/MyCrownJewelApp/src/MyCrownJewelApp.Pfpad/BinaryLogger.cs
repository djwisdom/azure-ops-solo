using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

#if PROFILING

/// <summary>
/// Binary logger for efficient writing of performance samples to disk.
/// Uses asynchronous file I/O for low overhead.
/// </summary>
public sealed class BinaryLogger : IDisposable
{
    private readonly string _logDirectory;
    private readonly string _baseFileName;
    private Stream? _currentStream;
    private string? _currentFilePath;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    private bool _disposed;

    // File header structure for versioning and metadata
    private const string LOG_HEADER = "PFPD_PERF_LOG_V1";
    private const int BUFFER_SIZE = 64 * 1024; // 64KB write buffer

    public BinaryLogger(string logDirectory, string baseFileName = "perf_log")
    {
        _logDirectory = logDirectory;
        _baseFileName = baseFileName;
        Directory.CreateDirectory(logDirectory);
    }

    /// <summary>
    /// Start a new log session with header.
    /// </summary>
    public async Task StartSessionAsync()
    {
        await _writeSemaphore.WaitAsync();
        try
        {
            CloseCurrentFile();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFilePath = Path.Combine(_logDirectory, $"{_baseFileName}_{timestamp}.jsonl");
            _currentStream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, BUFFER_SIZE, true);

            // Write header
            var header = new
            {
                Header = LOG_HEADER,
                StartTime = DateTime.UtcNow,
                StartTimestamp = Stopwatch.GetTimestamp(),
                SampleType = "PerformanceSample"
            };

            string headerJson = JsonSerializer.Serialize(header) + Environment.NewLine;
            byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(headerJson);
            await _currentStream.WriteAsync(headerBytes);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Write a batch of performance samples asynchronously.
    /// </summary>
    public async Task WriteSamplesAsync(PerformanceSample[] samples)
    {
        if (_disposed || _currentStream == null || samples.Length == 0) return;

        await _writeSemaphore.WaitAsync();
        try
        {
            if (_currentStream == null) return;

            // Write each sample as a JSON line
            foreach (var sample in samples)
            {
                var jsonSample = new
                {
                    sample.Timestamp,
                    sample.ThreadId,
                    sample.IsUiThread,
                    sample.CpuTimeTicks,
                    sample.WallTimeTicks,
                    sample.GcCollectionCount,
                    sample.GcMemoryBytes,
                    sample.ManagedThreadId,
                    sample.ActivityId,
                    StackTrace = sample.GetStackTraceString(),
                    sample.Flags
                };

                string json = JsonSerializer.Serialize(jsonSample) + Environment.NewLine;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await _currentStream.WriteAsync(bytes);
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Flush any pending writes to disk.
    /// </summary>
    public async Task FlushAsync()
    {
        if (_currentStream == null) return;

        await _writeSemaphore.WaitAsync();
        try
        {
            if (_currentStream != null)
            {
                await _currentStream.FlushAsync();
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    /// <summary>
    /// Close the current log file.
    /// </summary>
    public void CloseCurrentFile()
    {
        if (_currentStream != null)
        {
            _currentStream.Dispose();
            _currentStream = null;
            _currentFilePath = null;
        }
    }

    /// <summary>
    /// Get the current log file path.
    /// </summary>
    public string? CurrentFilePath => _currentFilePath;



    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CloseCurrentFile();
        _writeSemaphore.Dispose();
    }
}

#endif
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MyCrownJewelApp.Pfpad;

#if PROFILING

/// <summary>
/// Zero-allocation performance sample structure for sampling-based profiling.
/// </summary>
public struct PerformanceSample
{
    /// <summary>Timestamp in Stopwatch ticks</summary>
    public long Timestamp;

    /// <summary>Native thread ID</summary>
    public int ThreadId;

    /// <summary>True if this is the UI thread</summary>
    public bool IsUiThread;

    /// <summary>Process CPU time in ticks</summary>
    public long CpuTimeTicks;

    /// <summary>Wall clock time in ticks</summary>
    public long WallTimeTicks;

    /// <summary>GC collection count (sum of all generations)</summary>
    public int GcCollectionCount;

    /// <summary>GC memory pressure in bytes</summary>
    public long GcMemoryBytes;

    /// <summary>Managed thread ID</summary>
    public int ManagedThreadId;

    /// <summary>Async activity ID (0 if none)</summary>
    public uint ActivityId;

    /// <summary>Stack trace frames</summary>
    public string[] StackTraceFrames;

    /// <summary>Number of stack frames captured</summary>
    public int StackFrameCount;

    /// <summary>Sample type flags</summary>
    public SampleFlags Flags;

    [Flags]
    public enum SampleFlags
    {
        None = 0,
        UiThreadBlocked = 1 << 0,
        GcInProgress = 1 << 1,
        HighCpu = 1 << 2,
        LongWait = 1 << 3
    }

    public static PerformanceSample Create()
    {
        var sample = new PerformanceSample
        {
            Timestamp = Stopwatch.GetTimestamp(),
            ThreadId = Thread.CurrentThread.ManagedThreadId,
            IsUiThread = false, // Will be set by sampling engine
            CpuTimeTicks = 0, // Set by sampling engine
            WallTimeTicks = Stopwatch.GetTimestamp(),
            GcCollectionCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            GcMemoryBytes = GC.GetTotalMemory(false),
            ManagedThreadId = Thread.CurrentThread.ManagedThreadId,
            ActivityId = 0, // Set by sampling engine
            StackTraceFrames = Array.Empty<string>(),
            StackFrameCount = 0,
            Flags = SampleFlags.None
        };

        // Capture limited stack trace (top 8 frames)
        CaptureStackTrace(ref sample);
        return sample;
    }

    private static void CaptureStackTrace(ref PerformanceSample sample)
    {
        try
        {
            var trace = new StackTrace(1, false); // Skip this frame, no file info for speed
            var frames = trace.GetFrames();
            if (frames == null) return;

            int frameCount = Math.Min(8, frames.Length);
            sample.StackTraceFrames = new string[frameCount];
            sample.StackFrameCount = frameCount;

            for (int i = 0; i < frameCount; i++)
            {
                var method = frames[i].GetMethod();
                if (method == null) continue;

                sample.StackTraceFrames[i] = $"{method.DeclaringType?.Name ?? "<unknown>"}.{method.Name}";
            }
        }
        catch
        {
            // Ignore stack trace failures to maintain robustness
            sample.StackTraceFrames = Array.Empty<string>();
            sample.StackFrameCount = 0;
        }
    }

    public readonly string GetStackTraceString()
    {
        if (StackFrameCount == 0 || StackTraceFrames == null) return "<no stack trace>";

        return string.Join(Environment.NewLine, StackTraceFrames);
    }
}

#endif
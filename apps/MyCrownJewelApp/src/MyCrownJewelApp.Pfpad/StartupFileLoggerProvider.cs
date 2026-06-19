using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Minimal ILoggerProvider that writes log entries to a rolling startup.log file
/// under %LocalAppData%\Personal Flip Pad\. Replaces the old File.AppendAllText
/// startup logging in Program.cs.
/// </summary>
internal sealed class StartupFileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;

    public StartupFileLoggerProvider(int retentionDays = 30)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Personal Flip Pad");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "startup.log");
        PurgeOldLogs(dir, retentionDays);
    }

    private static void PurgeOldLogs(string dir, int retentionDays)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            foreach (var f in Directory.GetFiles(dir, "*.log"))
                if (File.GetLastWriteTimeUtc(f) < cutoff)
                    try { File.Delete(f); } catch { }
        }
        catch { }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_logPath, categoryName);

    public void Dispose() { }

    private sealed class FileLogger(string path, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            try
            {
                string line = $"[{DateTime.UtcNow:u}] [{level,-11}] {category}: {formatter(state, exception)}";
                if (exception != null) line += $"{Environment.NewLine}{exception}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { }
        }
    }
}

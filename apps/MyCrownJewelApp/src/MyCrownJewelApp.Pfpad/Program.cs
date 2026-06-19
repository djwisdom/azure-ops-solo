using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyCrownJewelApp.Pfpad;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool writeStartupLog = true;
        bool writeCrashLog = true;
        int logRetentionDays = 30;
        try
        {
            string settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyCrownJewelApp", "TextEditor", "settings.json");
            if (File.Exists(settingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("SecWriteStartupLog", out var el1))
                    writeStartupLog = el1.GetBoolean();
                if (doc.RootElement.TryGetProperty("SecWriteCrashLog", out var el2))
                    writeCrashLog = el2.GetBoolean();
                if (doc.RootElement.TryGetProperty("SecLogRetentionDays", out var el3))
                    logRetentionDays = el3.GetInt32();
            }
        }
        catch { }

        var services = new ServiceCollection();
        services.AddSingleton<NotificationFeedService>();
        services.AddSingleton<UserProfileManager>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<SettingsService>();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
            if (writeStartupLog)
                builder.AddProvider(new StartupFileLoggerProvider(logRetentionDays));
        });
        using var sp = services.BuildServiceProvider();

        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Pfpad.Program");

        try
        {
            logger.LogInformation("Application starting");
            ApplicationConfiguration.Initialize();
            logger.LogInformation("ApplicationConfiguration initialized");

            var form = new Form1(skipInitialDocument: false, services: sp);
            logger.LogInformation("Form created");

            Application.Run(form);
            logger.LogInformation("Application exited normally");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unhandled fatal exception");

            if (writeCrashLog)
            {
                string crashPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Personal Flip Pad", "crash.log");
                try { File.WriteAllText(crashPath, $"[{DateTime.UtcNow:u}] FATAL: {ex}"); } catch { }
                MessageBox.Show(
                    $"Application error: {ex.Message}\n\nSee log at: {crashPath}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show($"Application error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

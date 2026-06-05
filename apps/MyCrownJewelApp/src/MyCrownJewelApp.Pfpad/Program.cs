using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyCrownJewelApp.Pfpad;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Build DI container for truly app-wide services.
        // Form-scoped services (GitService, LintEngine, DebugSession, etc.)
        // remain as direct new() in Form1 — they carry per-window mutable state.
        var services = new ServiceCollection();
        services.AddSingleton<NotificationFeedService>();
        services.AddSingleton<UserProfileManager>();
        services.AddSingleton<SessionManager>();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddDebug();
            builder.AddProvider(new StartupFileLoggerProvider());
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

            string crashPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyCrownJewelApp", "Pfpad", "crash.log");
            File.WriteAllText(crashPath, $"[{DateTime.UtcNow:u}] FATAL: {ex}");
            MessageBox.Show(
                $"Application error: {ex.Message}\n\nSee log at: {crashPath}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

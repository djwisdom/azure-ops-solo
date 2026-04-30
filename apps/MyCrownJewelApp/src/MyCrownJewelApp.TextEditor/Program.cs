using System;
using System.IO;
using System.Windows.Forms;

namespace MyCrownJewelApp.TextEditor;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            // Early logging
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyCrownJewelApp",
                "TextEditor");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "startup.log");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Application starting{Environment.NewLine}");

            ApplicationConfiguration.Initialize();
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] After Initialize{Environment.NewLine}");

            var form = new Form1();
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Form created{Environment.NewLine}");

            Application.Run(form);
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Application exited normally{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyCrownJewelApp",
                "TextEditor",
                "crash.log");
            File.WriteAllText(logPath, $"[{DateTime.UtcNow:u}] FATAL: {ex}");
            MessageBox.Show($"Application error: {ex.Message}\n\nSee log at: {logPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
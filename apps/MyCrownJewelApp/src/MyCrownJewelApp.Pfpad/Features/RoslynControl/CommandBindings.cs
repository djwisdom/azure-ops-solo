namespace MyCrownJewelApp.Pfpad.Features.RoslynControl;

public static class CommandBindings
{
    public const string RestartAnalyzersShortcut = "Ctrl+Shift+R";
    public const string ToggleAnalyzersShortcut = "Ctrl+Alt+A";
    public const string OpenVisualizerShortcut = "Ctrl+Alt+V";

    public static ToolStripMenuItem CreateRestartAnalyzersItem(EventHandler onClick)
    {
        return new ToolStripMenuItem(
            "&Restart Analyzers",
            null,
            onClick,
            Keys.Control | Keys.Shift | Keys.R)
        {
            ToolTipText = "Restart Roslyn analyzers and source generators",
        };
    }

    public static ToolStripMenuItem CreateToggleAnalyzersItem(EventHandler onClick)
    {
        return new ToolStripMenuItem(
            "&Toggle Analyzers",
            null,
            onClick,
            Keys.Control | Keys.Alt | Keys.A)
        {
            ToolTipText = "Enable or disable Roslyn analyzers",
            CheckOnClick = true,
            Checked = true,
        };
    }

    public static ToolStripMenuItem CreateOpenVisualizerItem(EventHandler onClick)
    {
        return new ToolStripMenuItem(
            "&Open Roslyn Visualizer",
            null,
            onClick,
            Keys.Control | Keys.Alt | Keys.V)
        {
            ToolTipText = "Open the Roslyn visualizer tool window",
        };
    }

    public static ToolStrip CreateToolbar(EventHandler onRestart, EventHandler onToggle, EventHandler onVisualizer)
    {
        var toolbar = new ToolStrip { Dock = DockStyle.None, GripStyle = ToolStripGripStyle.Hidden };

        var restartBtn = new ToolStripButton(
            "Restart Analyzers", null, onRestart)
        {
            ToolTipText = "Restart analyzers and generators (Ctrl+Shift+R)",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
        };

        var toggleBtn = new ToolStripButton(
            "Analyzers: ON", null, onToggle)
        {
            ToolTipText = "Toggle analyzers on/off (Ctrl+Alt+A)",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            CheckOnClick = true,
            Checked = true,
        };

        var visualizerBtn = new ToolStripButton(
            "Visualizer", null, onVisualizer)
        {
            ToolTipText = "Open Roslyn Visualizer (Ctrl+Alt+V)",
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
        };

        toolbar.Items.Add(restartBtn);
        toolbar.Items.Add(toggleBtn);
        toolbar.Items.Add(visualizerBtn);

        return toolbar;
    }

    public static void UpdateToggleButtonText(ToolStripButton btn, bool enabled)
    {
        btn.Text = enabled ? "Analyzers: ON" : "Analyzers: OFF";
        btn.Checked = enabled;
    }
}

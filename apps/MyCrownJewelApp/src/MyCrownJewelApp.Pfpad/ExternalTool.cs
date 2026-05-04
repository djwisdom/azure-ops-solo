namespace MyCrownJewelApp.Pfpad;

public sealed class ExternalTool
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string InitialDirectory { get; set; } = "";
    public bool PromptForArguments { get; set; }
    public bool UseShellExecute { get; set; } = true;
}

namespace Totoro.Plugins;

public class PluginInfo
{
    public PluginInfo()
    {
    }

    required public string Name { get; init; }
    required public string DisplayName { get; init; }
    required public Version Version { get; init; }
    public string? Description { get; init; }
    public Stream? Icon { get; init; }
}

public record PluginInfoSlim(string FileName, string Version);

namespace Totoro.Plugins.Options;

public class SelectablePluginOption : PluginOption
{
    required public IEnumerable<string> AllowedValues { get; init; }
}

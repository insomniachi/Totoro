using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Totoro.Plugins.Options;

public class PluginOption : ReactiveObject
{
    required public string Name { get; init; }
    required public string DisplayName { get; init; }
    [Reactive] required public string Value { get; set; }
}

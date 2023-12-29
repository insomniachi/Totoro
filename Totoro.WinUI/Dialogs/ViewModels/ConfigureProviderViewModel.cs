using Totoro.Plugins;
using Totoro.Plugins.Options;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureProviderViewModel : DialogViewModel
{
    [Reactive] public PluginInfo PluginInfo { get; set; }
    [ObservableAsProperty] public string ProviderType { get; }
    [ObservableAsProperty] public PluginOptions Config { get; }
    public ICommand Save { get; }

    public ConfigureProviderViewModel()
    {
        this.WhenAnyValue(x => x.PluginInfo)
            .WhereNotNull()
            .Select(x => x.Name)
            .ToPropertyEx(this, x => x.ProviderType);

        this.WhenAnyValue(x => x.ProviderType)
            .WhereNotNull()
            .Select(PluginFactory<AnimeModel>.Instance.GetCurrentConfig)
            .ToPropertyEx(this, x => x.Config);
    }
}

using Totoro.Plugins;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureProviderViewModel : DialogViewModel
{
    private readonly IPluginManager _pluginManager;

    [Reactive] public string ProviderType { get; set; }
    [ObservableAsProperty] public PluginOptions Config { get; }
    public ICommand Save { get; }

    public ConfigureProviderViewModel(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;

        Save = ReactiveCommand.Create(OnSave);

        this.WhenAnyValue(x => x.ProviderType)
            .WhereNotNull()
            .Select(PluginFactory<AnimeModel>.Instance.GetOptions)
            .ToPropertyEx(this, x => x.Config);
    }

    void OnSave()
    {
        //_pluginManager.SaveConfig(ProviderType, Config);
    }
}

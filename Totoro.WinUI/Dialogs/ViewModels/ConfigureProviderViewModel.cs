using AnimDL.Core;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureProviderViewModel : DialogViewModel
{
    private readonly IPluginManager _pluginManager;

    [Reactive] public string ProviderType { get; set; }
    [ObservableAsProperty] public ProviderOptions Config { get; }
    public ICommand Save { get; }

    public ConfigureProviderViewModel(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;

        Save = ReactiveCommand.Create(OnSave);

        this.WhenAnyValue(x => x.ProviderType)
            .WhereNotNull()
            .Select(ProviderFactory.Instance.GetOptions)
            .ToPropertyEx(this, x => x.Config);
    }

    void OnSave()
    {
        _pluginManager.SaveConfig(ProviderType, Config);
    }
}

using AnimDL.Core;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class ConfigureProviderViewModel : DialogViewModel
{
    private readonly IPluginManager _pluginManager;

    [Reactive] public string ProviderType { get; set; }
	[ObservableAsProperty] public ConfigModel Config { get; }
	public ICommand Save { get; }

	public ConfigureProviderViewModel(IPluginManager pluginManager)
	{
		Save = ReactiveCommand.Create(OnSave);

		this.WhenAnyValue(x => x.ProviderType)
			.WhereNotNull()
			.Select(ProviderFactory.Instance.GetConfiguration)
			.Select(config => new ConfigModel(config))
			.ToPropertyEx(this, x => x.Config);
        _pluginManager = pluginManager;
    }

	void OnSave()
	{
		_pluginManager.SaveConfig(ProviderType, Config.ToParameters());
	}
}

public class ConfigModel
{
	public List<ConfigItemModel> Items { get; } = new();

	public ConfigModel(IParameters parameters)
	{
		foreach (var item in parameters)
		{
			Items.Add(new ConfigItemModel
			{
				Key = item.Key,
				Value = item.Value
			});
		}
	}

	public Parameters ToParameters()
	{
		var parameters = new Parameters();
		Items.ForEach(x => parameters.Add(new KeyValuePair<string, object>(x.Key, x.Value)));
		return parameters;
	}
}

public class ConfigItemModel
{
	public string Key { get; set; }
	[Reactive] public object Value { get; set; }
}
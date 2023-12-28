using Splat;
using Totoro.Plugins;
using Totoro.Plugins.Options;

namespace Totoro.Core.Services;

public class PluginOptionWrapper : ReactiveObject
{
    [Reactive] required public PluginOptions Options { get; init; }
    required public string PluginName { get; init; }
    public event EventHandler<string> OptionsChaged;

    public PluginOptionWrapper()
    {
        this.WhenAnyValue(x => x.Options)
            .WhereNotNull()
            .SelectMany(x => x.WhenChanged())
            .Subscribe(_ => OptionsChaged?.Invoke(Options, PluginName));

    }
}

public interface IPluginOptionsStorage<T>
{
    void Initialize();
    PluginOptionWrapper GetOptions(string pluginName);
    void ResetConfig(string pluginName);
}

internal class PluginOptionStorage<T>(ISettings settings,
                                      ILocalSettingsService localSettingsService) : IPluginOptionsStorage<T>, IEnableLogger
{
    private readonly Dictionary<string, PluginOptionWrapper> _configs = [];
    private readonly Dictionary<string, Dictionary<string, string>> _configValues = localSettingsService.ReadSetting<Dictionary<string, Dictionary<string, string>>>($"{typeof(T).Name}Configs", []);
    private readonly ISettings _settings = settings;
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;

    public PluginOptionWrapper GetOptions(string pluginName) => _configs[pluginName];

    public void Initialize()
    {
        var hasNewConfig = false;
        foreach (var item in PluginFactory<T>.Instance.Plugins)
        {
            var baseConfig = PluginFactory<T>.Instance.GetCurrentConfig(item.Name);
            _configs.Add(item.Name, new PluginOptionWrapper
            {
                Options = baseConfig,
                PluginName = item.Name
            });

            if (!_configValues.TryGetValue(item.Name, out Dictionary<string, string> value))
            {
                hasNewConfig = true;
            }
            else
            {
                foreach (var option in value)
                {
                    baseConfig.TrySetValue(option.Key, option.Value);
                }

                PluginFactory<T>.Instance.SetOptions(item.Name, baseConfig);
            }

            this.Log().Info($"Loaded plugin {item.DisplayName}");
        }

        if (!_settings.AllowSideLoadingPlugins)
        {
            foreach (var key in _configValues.Keys.Except(PluginFactory<T>.Instance.Plugins.Select(x => x.Name)))
            {
                _configValues.Remove(key);
                hasNewConfig = true;
            }
        }

        if (hasNewConfig)
        {
            SaveConfig();
        }

        foreach (var item in _configs.Values)
        {
            item.OptionsChaged += Item_OptionsChaged;
        }
    }

    private void Item_OptionsChaged(object sender, string name)
    {
        PluginFactory<T>.Instance.SetOptions(name, (PluginOptions)sender);
        SaveConfig();
    }

    public void ResetConfig(string pluginName)
    {
        var defaultConfig = PluginFactory<T>.Instance.GetDefaultConfig(pluginName);
        var existingOptions = _configs[pluginName];

        foreach (var item in defaultConfig)
        {
            existingOptions.Options.TrySetValue(item.Name, item.Value);
        }
    }

    private void SaveConfig()
    {
        var configValues = _configs.ToDictionary(x => x.Key, x => x.Value.Options.ToDictionary(x => x.Name, x => x.Value));
        _localSettingsService.SaveSetting($"{typeof(T).Name}Configs", configValues);
    }

}
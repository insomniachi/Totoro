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
}

internal class PluginOptionStorage<T> : IPluginOptionsStorage<T>, IEnableLogger
{
    private readonly Dictionary<string, PluginOptionWrapper> _configs = new();
    private readonly Dictionary<string, Dictionary<string, string>> _configValues = new();
    private readonly ISettings _settings;
    private readonly ILocalSettingsService _localSettingsService;

    public PluginOptionStorage(ISettings settings,
                               ILocalSettingsService localSettingsService)
    {
        _settings = settings;
        _localSettingsService = localSettingsService;
        _configValues = localSettingsService.ReadSetting<Dictionary<string, Dictionary<string, string>>>($"{typeof(T).Name}Configs", new());
    }

    public PluginOptionWrapper GetOptions(string pluginName) => _configs[pluginName];

    public void Initialize()
    {
        var hasNewConfig = false;
        foreach (var item in PluginFactory<T>.Instance.Plugins)
        {
            var baseConfig = PluginFactory<T>.Instance.GetOptions(item.Name);
            _configs.Add(item.Name, new PluginOptionWrapper
            {
                Options = baseConfig,
                PluginName = item.Name
            });
            
            if (!_configValues.ContainsKey(item.Name))
            {
                hasNewConfig = true;
            }
            else
            {
                foreach (var option in _configValues[item.Name])
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
            item.OptionsChaged += (option, name) =>
            {
                PluginFactory<T>.Instance.SetOptions(name, (PluginOptions)option);
                SaveConfig();
            };
        }
    }

    private void SaveConfig()
    {
        var configValues = _configs.ToDictionary(x => x.Key, x => x.Value.Options.ToDictionary(x => x.Name, x => x.Value));
        _localSettingsService.SaveSetting($"{typeof(T).Name}Configs", configValues);
    }
}
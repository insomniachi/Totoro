using System.Diagnostics;
using System.Text.Json.Nodes;
using AnimDL.Core;
using Splat;

namespace Totoro.Core.Services;

public class PluginManager : IPluginManager, IEnableLogger
{
    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _baseUrl = "https://raw.githubusercontent.com/insomniachi/AnimDL/master/Binaries";
    private const string _defaultApplicationDataFolder = "Totoro/ApplicationData";
    private readonly string _pluginFolder;
    private readonly HttpClient _httpClient;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly ISettings _settings;
    private readonly Dictionary<string, ProviderOptions> _configs;

    record PluginInfo(string FileName, string Version);

    public PluginManager(HttpClient httpClient,
                         ILocalSettingsService localSettingsService,
                         ISettings settings)
    {
        _pluginFolder = Path.Combine(_localApplicationData, _defaultApplicationDataFolder, "Plugins");
        _configs = localSettingsService.ReadSetting("ProviderConfigs", new Dictionary<string, ProviderOptions>());
        Directory.CreateDirectory(_pluginFolder);
        _httpClient = httpClient;
        _localSettingsService = localSettingsService;
        _settings = settings;
    }

    public void SaveConfig(string provider, ProviderOptions config)
    {
        ProviderFactory.Instance.SetOptions(provider, config);
        _configs[provider] = config;
        SaveConfig();
    }

    public void SaveConfig()
    {
        _localSettingsService.SaveSetting("ProviderConfigs", _configs);
    }

    private async Task<IEnumerable<PluginInfo>> GetListedPlugins()
    {
        try
        {
            var json = await _httpClient.GetStringAsync($"{_baseUrl}/plugins.json");
            var pluginInfos = new List<PluginInfo>();
            foreach (var item in JsonNode.Parse(json)?.AsArray())
            {
                var version = $"{item?["Version"]}";
                var name = Path.GetFileName($"{item?["Url"]}");
                pluginInfos.Add(new PluginInfo(name, version));
            }
            return pluginInfos;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return Enumerable.Empty<PluginInfo>();
        }
    }

    public async Task Initialize()
    {
        try
        {
            var localPlugins = Directory.GetFiles(_pluginFolder).Select(x => new PluginInfo(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion)).ToList();
            var listedPlugins = await GetListedPlugins();
            var newReleases = listedPlugins.Except(localPlugins).ToList();
            var hasNewConfig = false;
            foreach (var item in newReleases)
            {
                var path = Path.Combine(_pluginFolder, item.FileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                var url = $"{_baseUrl}/{item.FileName}";
                using var s = await _httpClient.GetStreamAsync(url);
                using var fs = new FileStream(path, FileMode.OpenOrCreate);
                await s.CopyToAsync(fs);
                hasNewConfig = true;
            }

            if (!_settings.AllowSideLoadingPlugins && listedPlugins.Any())
            {
                localPlugins = Directory.GetFiles(_pluginFolder).Select(x => new PluginInfo(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion)).ToList();
                foreach (var item in localPlugins.Except(listedPlugins))
                {
                    this.Log().Info($"Removing plugin : {item.FileName}");
                    File.Delete(Path.Combine(_pluginFolder, item.FileName));
                }
            }

            ProviderFactory.Instance.LoadPlugins(_pluginFolder);

            foreach (var item in ProviderFactory.Instance.Providers)
            {
                var baseConfig = ProviderFactory.Instance.GetOptions(item.Name);
                if (!_configs.ContainsKey(item.Name))
                {
                    _configs.Add(item.Name, baseConfig);
                    hasNewConfig = true;
                }
                else
                {
                    foreach (var option in _configs[item.Name].Where(x => baseConfig.FirstOrDefault(y => y.Name == x.Name) is { }))
                    {
                        baseConfig.TrySetValue(option.Name, option.Value);
                    }

                    ProviderFactory.Instance.SetOptions(item.Name, baseConfig);
                }
                this.Log().Info($"Loaded plugin {item.DisplayName}");
            }

            if (!_settings.AllowSideLoadingPlugins)
            {
                foreach (var key in _configs.Keys.Except(ProviderFactory.Instance.Providers.Select(x => x.Name)))
                {
                    _configs.Remove(key);
                    hasNewConfig = true;
                }
            }

            if (hasNewConfig)
            {
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }
}

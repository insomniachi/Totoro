using System.Diagnostics;
using System.Text.Json.Nodes;
using AnimDL.Core;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly Dictionary<string, Parameters> _configs;

    record PluginInfo(string FileName, string Version);

    public PluginManager(HttpClient httpClient,
                         ILocalSettingsService localSettingsService,
                         ISettings settings)
    {
        _pluginFolder = Path.Combine(_localApplicationData, _defaultApplicationDataFolder, "Plugins");
        _configs = localSettingsService.ReadSetting("ProviderConfigs", new Dictionary<string, Parameters>()).ToDictionary(x => x.Key, x => Clean(x.Value));
        Directory.CreateDirectory(_pluginFolder);
        _httpClient = httpClient;
        _localSettingsService = localSettingsService;
        _settings = settings;
    }

    private static Parameters Clean(Parameters paramters)
    {
        foreach (var item in paramters)
        {
            if(item.Value is JsonElement je)
            {
                paramters[item.Key] = je.ToString();
            }
        }
        return paramters;
    }

    public void SaveConfig(string provider, IParameters config)
    {
        ProviderFactory.Instance.SetConfiguration(provider, config);
        _configs[provider] = config as Parameters;
        SaveConfig();
    }

    public void SaveConfig()
    {
        _localSettingsService.SaveSetting("ProviderConfigs", _configs);
    }

    public async Task Initialize()
    {
        try
        {
            var localPlugins = Directory.GetFiles(_pluginFolder).Select(x => new PluginInfo(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion)).ToList();
            var json = await _httpClient.GetStringAsync($"{_baseUrl}/plugins.json");

            var pluginInfos = new List<PluginInfo>();
            foreach (var item in JsonNode.Parse(json)?.AsArray())
            {
                var version = $"{item?["Version"]}";
                var name = Path.GetFileName($"{item?["Url"]}");
                pluginInfos.Add(new PluginInfo(name, version));
            }

            var newReleases = pluginInfos.Except(localPlugins).ToList();
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

            if (!_settings.AllowSideLoadingPlugins)
            {
                localPlugins = Directory.GetFiles(_pluginFolder).Select(x => new PluginInfo(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion)).ToList();
                foreach (var item in localPlugins.Except(pluginInfos))
                {
                    this.Log().Info($"Removing plugin : {item.FileName}");
                    File.Delete(Path.Combine(_pluginFolder, item.FileName));
                }
            }

            ProviderFactory.Instance.LoadPlugins(_pluginFolder);

            foreach (var item in ProviderFactory.Instance.Providers)
            {
                var baseConfig = (Parameters)ProviderFactory.Instance.GetConfiguration(item.Name);
                if (!_configs.ContainsKey(item.Name))
                {
                    _configs.Add(item.Name, baseConfig);
                    hasNewConfig = true;
                }
                else
                {
                    foreach (var kv in _configs[item.Name].Where(x => baseConfig.ContainsKey(x.Key)))
                    {
                        baseConfig[kv.Key] = kv.Value;  
                    }

                    ProviderFactory.Instance.SetConfiguration(item.Name, baseConfig);
                }
                this.Log().Info($"Loaded plugin {item.DisplayName}");
            }

            if(!_settings.AllowSideLoadingPlugins)
            {
                foreach (var key in _configs.Keys.Except(ProviderFactory.Instance.Providers.Select(x => x.Name)))
                {
                    _configs.Remove(key);
                    hasNewConfig = true;
                }
            }

            if(hasNewConfig)
            {
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
           this.Log().Error(ex);
        }
    }

    public async Task<(string, DateTime)> AuthenticateKamy()
    {
        var url = QueryHelpers.AddQueryString("https://api.kamyroll.tech/auth/v1/token", new Dictionary<string, string>
        {
            ["device_id"] = "whatvalueshouldbeforweb",
            ["device_type"] = "com.service.data",
            ["access_token"] = "HMbQeThWmZq4t7w"
        });
        var json = await _httpClient.GetStringAsync(url);
        var time = DateTime.Now;
        if(int.TryParse($"{JsonNode.Parse(json)?["access_token"]}", out int secs))
        {
            time = DateTime.Now + TimeSpan.FromSeconds(secs);
        }

        return ($"{JsonNode.Parse(json)?["access_token"]}", time);
    }
}

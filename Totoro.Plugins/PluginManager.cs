using System.Diagnostics;
using Flurl;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins;

public class PluginManager : IPluginManager, IEnableLogger
{
    private readonly HttpClient _httpClient;
    private readonly IPluginFactory _animePluginFactory;
    private readonly string _baseUrl = "https://raw.githubusercontent.com/insomniachi/Totoro/feature/plugin_refactor/Plugins";
    private List<PluginInfoSlim> _localAnimePlugins = new();
    private string _folder = "";

    public static bool AllowSideLoadingPlugins { get; set; } = false;

    class PluginIndex
    {
        public List<PluginInfoSlim> Anime { get; set; } = new();
        public List<PluginInfoSlim> Manga { get; set; } = new();
        public List<PluginInfoSlim> Torrent { get; set; } = new();
    }

    public PluginManager(HttpClient httpClient,
                         IPluginFactory animePluginFactory)
    {
        _httpClient = httpClient;
        _animePluginFactory = animePluginFactory;
    }

    public async Task Initialize(string folder)
    {
        _folder = folder;

        var animeFolder = Path.Combine(folder, "Anime");

        if(Directory.Exists(animeFolder))
        {
            _localAnimePlugins = Directory
                .GetFiles(animeFolder)
                .Select(x =>
                {
                    var name = Path.GetFileName(x);
                    var version = FileVersionInfo.GetVersionInfo(x).FileVersion!;
                    return new PluginInfoSlim(name, version);
                })
                .ToList();
        }

        var listedPlugins = await GetListedPlugins();
        await InitializeAnimePlugins(listedPlugins.Anime);
    }

    private async Task InitializeAnimePlugins(List<PluginInfoSlim> plugins)
    {
        if(plugins is not { Count : > 0})
        {
            return;
        }

        var folder = Path.Combine(_folder, "Anime");
        var newReleases = plugins.Except(_localAnimePlugins).ToList();
        foreach (var item in newReleases)
        {
            var path = Path.Combine(folder, item.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var url = $"{_baseUrl}/{item.FileName}";
            using var s = await _httpClient.GetStreamAsync(url);
            using var fs = new FileStream(path, FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);
        }

        if (AllowSideLoadingPlugins)
        {
            var localPlugins = Directory.GetFiles(folder).Select(x => new PluginInfoSlim(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion!)).ToList();
            foreach (var item in localPlugins.Except(plugins))
            {
                this.Log().Info($"Removing plugin : {item.FileName}");
                File.Delete(Path.Combine(folder, item.FileName));
            }
        }

        _animePluginFactory.LoadPlugins(folder);
    }


    private async Task<PluginIndex> GetListedPlugins()
    {
        try
        {
            var str = await _baseUrl.AppendPathSegment("plugins.json").GetStringAsync();
            return await _baseUrl.AppendPathSegment("plugins.json").GetJsonAsync<PluginIndex>();
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return new();
        }
    }
}

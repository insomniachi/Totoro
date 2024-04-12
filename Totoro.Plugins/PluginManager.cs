using System.Diagnostics;
using System.Globalization;
using Flurl;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins;

public class PluginIndex
{
    public List<PluginInfoSlim> Anime { get; set; } = [];
    public List<PluginInfoSlim> Manga { get; set; } = [];
    public List<PluginInfoSlim> Torrent { get; set; } = [];
    public List<PluginInfoSlim> MediaDetection { get; set; } = [];
}

public class PluginManager : IPluginManager, IEnableLogger
{
    private readonly IPluginFactory _animePluginFactory;
    private readonly IPluginFactory _mangaPluginFactory;
    private readonly IPluginFactory _torrentPluginFactory;
    private readonly IPluginFactory? _mediaDetectionPluginFactory;
    private readonly string _baseUrl = "https://raw.githubusercontent.com/insomniachi/Totoro/main";
    private PluginIndex? _listedPlugins;
    private readonly bool _autoDownloadAllPlugins = true;

    public static bool AllowSideLoadingPlugins { get; set; } = false;

    public PluginManager(IPluginFactory animePluginFactory,
                         IPluginFactory mangaPluginFactory,
                         IPluginFactory torrentPluginFactory,
                         IPluginFactory? mediaDetectionPluginFactory)
    {
        _animePluginFactory = animePluginFactory;
        _mangaPluginFactory = mangaPluginFactory;
        _torrentPluginFactory = torrentPluginFactory;
        _mediaDetectionPluginFactory = mediaDetectionPluginFactory;

        if(RegionInfo.CurrentRegion.EnglishName.Equals("India", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = "https://rawgithubusercontent.deno.dev/insomniachi/Totoro/main";
        }
    }

    public async ValueTask<PluginIndex?> GetAllPlugins()
    {
        return _listedPlugins ?? await GetListedPlugins();
    }

    public async Task Initialize(string folder)
    {
        var animeFolder = Path.Combine(folder, "Anime");
        var mangaFolder = Path.Combine(folder, "Manga");
        var torrentsFolder = Path.Combine(folder, "Torrents");
        var mediaDetectionFolder = Path.Combine(folder, "Media Detection");

        _listedPlugins ??= await GetListedPlugins();

        if (_autoDownloadAllPlugins && _listedPlugins is not null)
        {
            var localAnimePlugins = GetLocalPlugins(animeFolder);
            var localTorrentsPlugins = GetLocalPlugins(torrentsFolder);
            var localMediaDetectionPlugins = GetLocalPlugins(mediaDetectionFolder);
            var localMangaPlugins = GetLocalPlugins(mangaFolder);

            await DownloadOrUpdatePlugins(_listedPlugins.Anime, localAnimePlugins, animeFolder);
            await DownloadOrUpdatePlugins(_listedPlugins.Manga, localMangaPlugins, mangaFolder);
            await DownloadOrUpdatePlugins(_listedPlugins.Torrent, localTorrentsPlugins, torrentsFolder);
            await DownloadOrUpdatePlugins(_listedPlugins.MediaDetection, localMediaDetectionPlugins, mediaDetectionFolder);
        }

        _animePluginFactory.LoadPlugins(animeFolder);
        _torrentPluginFactory.LoadPlugins(torrentsFolder);
        _mangaPluginFactory.LoadPlugins(mangaFolder);
        _mediaDetectionPluginFactory?.LoadPlugins(mediaDetectionFolder);
    }

    private static List<PluginInfoSlim> GetLocalPlugins(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory
            .GetFiles(folder)
            .Select(x =>
            {
                var name = Path.GetFileName(x);
                var version = FileVersionInfo.GetVersionInfo(x).FileVersion!;
                return new PluginInfoSlim(name, version);
            })
            .ToList();
    }

    private async Task DownloadOrUpdatePlugins(List<PluginInfoSlim> listedPlugins, IEnumerable<PluginInfoSlim> localPlugins, string folder)
    {
        if (listedPlugins is not { Count: > 0 })
        {
            return;
        }

        var comparer = new PluginInfoEqualityComparer();

        Directory.CreateDirectory(folder);

        var newReleases = listedPlugins.Except(localPlugins, comparer).ToList();
        foreach (var item in newReleases)
        {
            var path = Path.Combine(folder, item.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            this.Log().Info($"Downloading plugin : {item.FileName} {item.Version}");
            var url = Url.Combine(_baseUrl, "Plugins Store", item.FileName);
            var response = await url.GetAsync();

            if(response.StatusCode < 300)
            {
                await using var s = await response.GetStreamAsync();
                await using var fs = new FileStream(path, FileMode.OpenOrCreate);
                await s.CopyToAsync(fs);
            }
            else
            {
                this.Log().Info($" Unable to download : {item.FileName} {item.Version}");
            }
        }

        if (!AllowSideLoadingPlugins)
        {
            localPlugins = Directory.GetFiles(folder).Select(x => new PluginInfoSlim(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion!));
            foreach (var item in localPlugins.Except(listedPlugins, comparer))
            {
                this.Log().Info($"Removing plugin : {item.FileName}");
                File.Delete(Path.Combine(folder, item.FileName));
            }
        }

    }


    private async Task<PluginIndex?> GetListedPlugins()
    {
        try
        {
            var response = await _baseUrl.AppendPathSegment("plugins.json").GetAsync();
            if(response.StatusCode < 300)
            {
                var plugins = await response.GetJsonAsync<PluginIndex>();
                return plugins;
            }

            return null;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return new();
        }
    }


}

using System.Diagnostics;
using Flurl;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins;

public class PluginManager : IPluginManager, IEnableLogger
{
    private readonly IPluginFactory _animePluginFactory;
    private readonly IPluginFactory _torrentPluginFactory;
    private readonly IPluginFactory _mediaDetectionPluginFactory;
    private readonly string _baseUrl = "https://raw.githubusercontent.com/insomniachi/Totoro/main";

    public static bool AllowSideLoadingPlugins { get; set; } = false;

    class PluginIndex
    {
        public List<PluginInfoSlim> Anime { get; set; } = new();
        public List<PluginInfoSlim> Manga { get; set; } = new();
        public List<PluginInfoSlim> Torrent { get; set; } = new();
        public List<PluginInfoSlim> MediaDetection { get; set; } = new();
    }

    public PluginManager(IPluginFactory animePluginFactory,
                         IPluginFactory torrentPluginFactory,
                         IPluginFactory mediaDetectionPluginFactory)
    {
        _animePluginFactory = animePluginFactory;
        _torrentPluginFactory = torrentPluginFactory;
        _mediaDetectionPluginFactory = mediaDetectionPluginFactory;
    }

    public async Task Initialize(string folder)
    {
        var animeFolder = Path.Combine(folder, "Anime");
        var torrentsFolder = Path.Combine(folder, "Torrents");
        var mediaDetectionFolder = Path.Combine(folder, "Media Detection");

        var localAnimePlugins = GetLocalPlugins(animeFolder);
        var localTorrentsPlugins = GetLocalPlugins(torrentsFolder);
        var localMediaDetectionPlugins = GetLocalPlugins(mediaDetectionFolder);
        var listedPlugins = await GetListedPlugins();
        
        await DownloadOrUpdatePlugins(listedPlugins.Anime, localAnimePlugins, animeFolder);
        await DownloadOrUpdatePlugins(listedPlugins.Torrent, localTorrentsPlugins, torrentsFolder);
        await DownloadOrUpdatePlugins(listedPlugins.MediaDetection, localMediaDetectionPlugins, mediaDetectionFolder);

        _animePluginFactory.LoadPlugins(animeFolder);
        _torrentPluginFactory.LoadPlugins(torrentsFolder);
        _mediaDetectionPluginFactory.LoadPlugins(mediaDetectionFolder);
    }

    private static List<PluginInfoSlim> GetLocalPlugins(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return new();
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
        if(listedPlugins is not { Count : > 0})
        {
            return;
        }

        Directory.CreateDirectory(folder);

        var newReleases = listedPlugins.Except(localPlugins).ToList();
        foreach (var item in newReleases)
        {
            var path = Path.Combine(folder, item.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var url = Url.Combine(_baseUrl, "Plugins", item.FileName);
            using var s = await url.GetStreamAsync();
            using var fs = new FileStream(path, FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);
        }

        if (AllowSideLoadingPlugins)
        {
            localPlugins = Directory.GetFiles(folder).Select(x => new PluginInfoSlim(Path.GetFileName(x), FileVersionInfo.GetVersionInfo(x).FileVersion!));
            foreach (var item in localPlugins.Except(listedPlugins))
            {
                this.Log().Info($"Removing plugin : {item.FileName}");
                File.Delete(Path.Combine(folder, item.FileName));
            }
        }

    }


    private async Task<PluginIndex> GetListedPlugins()
    {
        try
        {
            var plugins = await _baseUrl.AppendPathSegment("plugins.json").GetJsonAsync<PluginIndex>();
            return plugins;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return new();
        }
    }


}

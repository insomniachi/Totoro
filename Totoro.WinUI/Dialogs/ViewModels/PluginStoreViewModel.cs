using System.IO;
using Flurl;
using Flurl.Http;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class PluginStoreViewModel : DialogViewModel
{
    private readonly IPluginManager _pluginManager;
    private readonly IKnownFolders _knownFolders;
    private readonly string _baseUrl = "https://raw.githubusercontent.com/insomniachi/Totoro/main";
    private string _type;

    public PluginStoreViewModel(IPluginManager pluginManager,
                                IKnownFolders knownFolders)
    {
        _pluginManager = pluginManager;
        _knownFolders = knownFolders;
    }

    public async Task Initalize(string type)
    {
        _type = type;
        var plugins = await _pluginManager.GetAllPlugins();
        foreach (var item in GetPluginsOfType(plugins, type))
        {
            var exists = File.Exists(Path.Combine(_knownFolders.Plugins, type, item.FileName));
            Plugins.Add(item with { Exists = exists });
        }
    }

    public ObservableCollection<PluginInfoSlim> Plugins { get; } = new();

    private static List<PluginInfoSlim> GetPluginsOfType(PluginIndex index, string type)
    {
        return type switch
        {
            "Manga" => index.Manga,
            "Torrent" => index.Torrent,
            "Media Detection" => index.MediaDetection,
            _ => index.Anime
        };
    }

    public async Task DownloadPlugin(string fileName)
    {
        if (GetPluginFactory(_type) is not { } factory)
        {
            return;
        }

        var dll = await Download(fileName);
        factory.LoadPlugin(dll);
    }

    public async Task<string> Download(string fileName)
    {
        var path = Path.Combine(_knownFolders.Plugins, _type, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var url = Url.Combine(_baseUrl, "Plugins Store", fileName);
        using var s = await url.GetStreamAsync();
        using var fs = new FileStream(path, FileMode.OpenOrCreate);
        await s.CopyToAsync(fs);

        return path;
    }

    private IPluginFactory GetPluginFactory(string type)
    {
        return type switch
        {
            "Anime" => PluginFactory<AnimeProvider>.Instance,
            "Manga" => PluginFactory<MangaProvider>.Instance,
            "Torrent" => PluginFactory<ITorrentTracker>.Instance,
            "Media Detection" => PluginFactory<INativeMediaPlayer>.Instance,
            _ => throw new NotSupportedException()
        };
    }
}

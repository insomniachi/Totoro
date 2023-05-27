using System.Diagnostics.CodeAnalysis;
using MonoTorrent.Client;
using Splat;
using Totoro.Core.ViewModels;
using Totoro.Plugins;
using Totoro.Plugins.Torrents.Models;
using YoutubeExplode;
using YoutubeExplode.Videos;
using TorrentState = Totoro.Plugins.Torrents.Models.TorrentState;
using Video = Totoro.Core.Models.Video;

namespace Totoro.Core;

[ExcludeFromCodeCoverage]
public class TotoroCommands : IEnableLogger
{
    private readonly YoutubeClient _youtubeClient = new();

    public TotoroCommands(IViewService viewService,
                          INavigationService navigationService,
                          IDebridServiceContext debridServiceContext,
                          ITorrentEngine torrentEngine,
                          ISettings settings)
    {
        UpdateTracking = ReactiveCommand.CreateFromTask<AnimeModel>(viewService.UpdateTracking);

        Watch = ReactiveCommand.Create<object>(param =>
        {
            switch (param)
            {
                case AnimeModel anime:
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() 
                    {
                        ["Anime"] = anime ,
                        ["Provider"] = settings.DefaultProviderType
                    });
                    break;
                case long id:
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() 
                    {
                        ["Id"] = id,
                        ["Provider"] = settings.DefaultProviderType
                    });
                    break;
                case (AnimeModel anime, string providerType):
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>()
                    {
                        ["Anime"] = anime,
                        ["Provider"] = providerType
                    });
                    break;
            }
        });

        PlayVideo = ReactiveCommand.Create<object>(param =>
        {
            switch (param)
            {
                case AnimeSound theme:
                    viewService.PlayVideo(theme.SongName, theme.Url);
                    break;
                case Video preview:
                    _ = PlayYoutubeVideo(preview, viewService.PlayVideo);
                    break;
            }
        });

        More = ReactiveCommand.Create<object>(param =>
        {
            switch (param)
            {
                case AnimeModel anime:
                    navigationService.NavigateTo<AboutAnimeViewModel>(parameter: new Dictionary<string, object>() { ["Id"] = anime.Id });
                    break;
                case long id:
                    navigationService.NavigateTo<AboutAnimeViewModel>(parameter: new Dictionary<string, object>() { ["Id"] = id });
                    break;
            }
        });

        StreamWithDebrid = ReactiveCommand.CreateFromTask<TorrentModel>(async model =>
        {
            switch (model.State)
            {
                case TorrentState.Unknown:
                    var isCached = await debridServiceContext.Check(model.Magnet);
                    model.State = isCached ? TorrentState.Unknown : TorrentState.NotCached;
                    if (isCached)
                    {
                        navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>()
                        {
                            ["TorrentModel"] = model,
                            ["UseDebrid"] = true,
                        });
                    }
                    break;
                case TorrentState.NotCached:
                    _ = await debridServiceContext.CreateTransfer(model.Magnet);
                    model.State = TorrentState.Requested;
                    break;
            }
        });

        TorrentCommand = ReactiveCommand.Create<TorrentModel>(model =>
        {
            navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>()
            {
                ["TorrentModel"] = model,
                ["UseDebrid"] = false,
            });
        });

        SearchTorrent = ReactiveCommand.Create<object>(param =>
        {
            switch (param)
            {
                case AnimeModel model:
                    navigationService.NavigateTo<TorrentingViewModel>(parameter: new Dictionary<string, object>() 
                    {
                        ["Anime"] = model,
                        ["Indexer"] = settings.DefaultTorrentTrackerType
                    });
                    break;
                case (AnimeModel model, string indexer):
                    navigationService.NavigateTo<TorrentingViewModel>(parameter: new Dictionary<string, object>()
                    {
                        ["Anime"] = model,
                        ["Indexer"] = indexer
                    });
                    break;
            }

        });

        ConfigureProvider = ReactiveCommand.CreateFromTask<PluginInfo>(viewService.ConfigureProvider);
        RemoveTorrent = ReactiveCommand.CreateFromTask<string>(async name => await torrentEngine.RemoveTorrent(name, false));
        RemoveTorrentWithFiles = ReactiveCommand.CreateFromTask<string>(async name =>
        {
            var result = await viewService.Question(name, $"Are you sure you want to remove torrent with downloaded files?");
            if (result)
            {
                await torrentEngine.RemoveTorrent(name, true);
            }
        });
        PlayLocalFolder = ReactiveCommand.Create<TorrentManager>(torrentManager =>
        {
            navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>
            {
                ["TorrentManager"] = torrentManager,
            });
        });
        DownloadTorrentCommand = ReactiveCommand.Create<TorrentModel>(torrent =>
        {
            var title = AnitomySharp.AnitomySharp.Parse(torrent.Name).FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementAnimeTitle).Value;

            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            torrentEngine.DownloadFromMagnet(torrent.Magnet, Path.Combine(settings.UserTorrentsDownloadDirectory, title));
        });
        AnimeCard = ReactiveCommand.Create<AnimeModel>(anime =>
        {
            if (settings.AnimeCardClickAction == "Watch")
            {
                Watch.Execute(anime);
            }
            else
            {
                More.Execute(anime.Id);
            }
        });
    }

    public ICommand UpdateTracking { get; }
    public ICommand More { get; }
    public ICommand Watch { get; }
    public ICommand PlayVideo { get; }
    public ICommand ConfigureProvider { get; }
    public ICommand TorrentCommand { get; }
    public ICommand StreamWithDebrid { get; }
    public ICommand DownloadTorrentCommand { get; }
    public ICommand SearchTorrent { get; }
    public ICommand RemoveTorrent { get; }
    public ICommand RemoveTorrentWithFiles { get; }
    public ICommand PlayLocalFolder { get; }
    public ICommand AnimeCard { get; }

    private async Task PlayYoutubeVideo(Video video, Func<string, string, Task> playVideo)
    {
        try
        {
            var videoId = VideoId.Parse(video.Url);
            var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);
            var url = manifest.GetMuxedStreams().LastOrDefault().Url;
            await playVideo(video.Title, url);
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }
}

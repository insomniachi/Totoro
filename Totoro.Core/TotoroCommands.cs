using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using Splat;
using Totoro.Core.Torrents;
using Totoro.Core.ViewModels;
using YoutubeExplode;
using YoutubeExplode.Videos;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;
using Video = Totoro.Core.Models.Video;

namespace Totoro.Core;

[ExcludeFromCodeCoverage]
public class TotoroCommands : IEnableLogger
{
    private readonly YoutubeClient _youtubeClient = new();

    public TotoroCommands(IViewService viewService,
                          INavigationService navigationService,
                          IDebridServiceContext debridServiceContext,
                          ITorrentEngine torrentEngine)
    {
        UpdateTracking = ReactiveCommand.CreateFromTask<AnimeModel>(viewService.UpdateTracking);

        Watch = ReactiveCommand.Create<object>(param =>
        {
            switch (param)
            {
                case AnimeModel anime:
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Anime"] = anime });
                    break;
                case long id:
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Id"] = id });
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

        TorrentCommand = ReactiveCommand.CreateFromTask<TorrentModel>(async model =>
        {
            if (debridServiceContext.IsAuthenticated)
            {
                switch (model.State)
                {
                    case TorrentState.Unknown:
                        var isCached = await debridServiceContext.Check(model.MagnetLink);
                        model.State = isCached ? TorrentState.Unknown : TorrentState.NotCached;
                        if (isCached)
                        {
                            navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>()
                            {
                                ["Torrent"] = model,
                                ["UseDebrid"] = true,
                            });
                        }
                        break;
                    case TorrentState.NotCached:
                        _ = await debridServiceContext.CreateTransfer(model.MagnetLink);
                        model.State = TorrentState.Requested;
                        break;
                }
            }
            else
            {
                navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>()
                {
                    ["Torrent"] = model,
                    ["UseDebrid"] = false,
                });
            }
        });

        SearchTorrent = ReactiveCommand.Create<AnimeModel>(model =>
        {
            navigationService.NavigateTo<TorrentingViewModel>(parameter: new Dictionary<string, object>() { ["Anime"] = model });
        });

        ConfigureProvider = ReactiveCommand.CreateFromTask<ProviderInfo>(viewService.ConfigureProvider);
        RemoveTorrent = ReactiveCommand.CreateFromTask<string>(async name => await torrentEngine.RemoveTorrent(name, false));
        RemoveTorrentWithFiles = ReactiveCommand.CreateFromTask<string>(async name =>
        {
            var result = await viewService.Question(name, $"Are you sure you want to remove torrent with downloaded files?");
            if (result)
            {
                await torrentEngine.RemoveTorrent(name, true);
            }
        });
        PlayLocalFolder = ReactiveCommand.Create<string>(file => navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>
        {
            ["LocalFolder"] = file
        }));
    }

    public ICommand UpdateTracking { get; }
    public ICommand More { get; }
    public ICommand Watch { get; }
    public ICommand PlayVideo { get; }
    public ICommand ConfigureProvider { get; }
    public ICommand TorrentCommand { get; }
    public ICommand SearchTorrent { get; }
    public ICommand RemoveTorrent { get; }
    public ICommand RemoveTorrentWithFiles { get; }
    public ICommand PlayLocalFolder { get; }

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

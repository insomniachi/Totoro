using System.Diagnostics.CodeAnalysis;
using MalApi;
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
                          IDebridServiceContext debridServiceContext)
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
            switch (model.State)
            {
                case TorrentState.Unknown:
                    var isCached = await debridServiceContext.Check(model.MagnetLink);
                    model.State = isCached ? TorrentState.Cached : TorrentState.NotCached;
                    if(isCached)
                    {
                        navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Torrent"] = model });
                    }
                    break;
                case TorrentState.Cached:
                    navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Torrent"] = model });
                    break;
                case TorrentState.NotCached:
                    var id = await debridServiceContext.CreateTransfer(model.MagnetLink);
                    model.State = TorrentState.Requested;
                    break;
            }
        });

        ConfigureProvider = ReactiveCommand.CreateFromTask<ProviderInfo>(viewService.ConfigureProvider);
    }

    public ICommand UpdateTracking { get; }
    public ICommand More { get; }
    public ICommand Watch { get; }
    public ICommand PlayVideo { get; }
    public ICommand ConfigureProvider { get; }
    public ICommand TorrentCommand { get; }

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

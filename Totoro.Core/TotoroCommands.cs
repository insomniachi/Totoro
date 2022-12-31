using System.Diagnostics.CodeAnalysis;
using Totoro.Core.ViewModels;
using YoutubeExplode;
using YoutubeExplode.Videos;
using Video = Totoro.Core.Models.Video;

namespace Totoro.Core;

[ExcludeFromCodeCoverage]
public class TotoroCommands
{
    private readonly YoutubeClient _youtubeClient = new();

    public TotoroCommands(IViewService viewService,
                          INavigationService navigationService)
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
    }

    public ICommand UpdateTracking { get; }
    public ICommand More { get; }
    public ICommand Watch { get; }
    public ICommand PlayVideo { get; }

    private async Task PlayYoutubeVideo(Video video, Func<string, string, Task> playVideo)
    {
        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(VideoId.Parse(video.Url));
        var url = manifest.GetMuxedStreams().LastOrDefault().Url;
        await playVideo(video.Title, url);
    }
}

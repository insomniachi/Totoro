using Totoro.Core.Contracts;
using Totoro.Core.ViewModels;
using YoutubeExplode;
using YoutubeExplode.Videos;
using Video = Totoro.Core.Models.Video;

namespace Totoro.Core;

public class TotoroCommands
{
    private readonly YoutubeClient _youtubeClient = new();

	public TotoroCommands(IViewService viewService,
                          INavigationService navigationService)
	{
        UpdateTracking = ReactiveCommand.CreateFromTask<AnimeModel>(viewService.UpdateTracking);
        PlayTheme = ReactiveCommand.CreateFromTask<AnimeSound>(s => viewService.PlayVideo(s.Name, s.Url));
        PlayPreview = ReactiveCommand.CreateFromTask<Video>(v => PlayYoutubeVideo(v, viewService.PlayVideo));
        Watch = ReactiveCommand.Create<AnimeModel>(anime => navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>() { ["Anime"] = anime }));
        More = ReactiveCommand.Create<long>(id => navigationService.NavigateTo<AboutAnimeViewModel>(parameter: new Dictionary<string, object>() { ["Id"] = id }));
    }

    public ICommand UpdateTracking { get; }
    public ICommand PlayTheme { get; }
    public ICommand PlayPreview { get; }
    public ICommand More { get; }
    public ICommand Watch { get; }

    private async Task PlayYoutubeVideo(Video video, Func<string,string,Task> playVideo)
    {
        var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(VideoId.Parse(video.Url));
        var url = manifest.GetMuxedStreams().LastOrDefault().Url;
        await playVideo(video.Title, url);
    }
}

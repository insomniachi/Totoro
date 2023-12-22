using System.Reactive.Subjects;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Services;

public class AiredEpisodeNotifier : IAiredEpisodeNotifier
{
    private List<IAiredAnimeEpisode> _previousState = [];
    private readonly Subject<IAiredAnimeEpisode> _onNewEpisode = new();
    private readonly ISettings _settings;
    private bool _isStarted;

    public IObservable<IAiredAnimeEpisode> OnNewEpisode => _onNewEpisode;

    public AiredEpisodeNotifier(ISettings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        var provider = PluginFactory<AnimeProvider>.Instance.CreatePlugin(_settings.DefaultProviderType);

        Observable
        .Timer(TimeSpan.Zero, TimeSpan.FromMinutes(30))
        .SelectMany(_ => provider.AiredAnimeEpisodeProvider.GetRecentlyAiredEpisodes().ToListAsync().AsTask())
        .Do(list =>
        {
            list.RemoveAll(_previousState.Contains);
            _previousState = list;
            foreach (var item in list)
            {
                _onNewEpisode.OnNext(item);
            }
        })
        .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);
    }
}


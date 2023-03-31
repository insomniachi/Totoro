using System.Reactive.Subjects;

namespace Totoro.Core.Services;

public class AiredEpisodeNotifier : IAiredEpisodeNotifier
{
    private List<AiredEpisode> _previousState = new();
    private readonly Subject<AiredEpisode> _onNewEpisode = new();
    private readonly ISettings _settings;
    private readonly IProviderFactory _providerFactory;
    private bool _isStarted;

    public IObservable<AiredEpisode> OnNewEpisode => _onNewEpisode;

    public AiredEpisodeNotifier(ISettings settings,
                                IProviderFactory providerFactory)
    {
        _settings = settings;
        _providerFactory = providerFactory;
    }

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        var provider = _providerFactory.GetProvider(_settings.DefaultProviderType);

        Observable
        .Timer(TimeSpan.Zero, TimeSpan.FromMinutes(30))
        .SelectMany(_ => provider.AiredEpisodesProvider.GetRecentlyAiredEpisodes())
        .Select(enumerable => enumerable.ToList())
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


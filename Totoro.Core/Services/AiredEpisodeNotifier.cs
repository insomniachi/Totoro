using System.Reactive.Subjects;
using AnimDL.Api;

namespace Totoro.Core.Services;

public class AiredEpisodeNotifier : IAiredEpisodeNotifier
{
    private List<AiredEpisode> _previousState = new();
    private readonly Subject<AiredEpisode> _onNewEpisode = new();

    public IObservable<AiredEpisode> OnNewEpisode => _onNewEpisode;

    public AiredEpisodeNotifier(ISettings settings,
                                IProviderFactory providerFactory)
    {
        var provider = providerFactory.GetProvider(settings.DefaultProviderType);

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


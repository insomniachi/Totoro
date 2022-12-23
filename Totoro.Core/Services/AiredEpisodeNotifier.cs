using System.Reactive.Subjects;
using Splat;

namespace Totoro.Core.Services;

public class AiredEpisodeNotifier : IAiredEpisodeNotifier
{
    private List<AiredEpisode> _previousState = new();
    private readonly Subject<AiredEpisode> _onNewEpisode = new();

    public IObservable<AiredEpisode> OnNewEpisode => _onNewEpisode;

    public AiredEpisodeNotifier(IRecentEpisodesProvider recentEpisodesProvider)
    {
        Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromMinutes(30))
            .SelectMany(_ => recentEpisodesProvider.GetRecentlyAiredEpisodes())
            .Select(enumerable => enumerable.ToList())
            .Do(async list =>
            {
                list.RemoveAll(_previousState.Contains);
                _previousState = list;
                foreach (var item in list)
                {
                    item.MalId = await recentEpisodesProvider.GetMalId(item);
                    _onNewEpisode.OnNext(item);
                }
            })
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);
    }
}


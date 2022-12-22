namespace Totoro.Core.Contracts;

public interface IAiredEpisodeNotifier
{
    IObservable<AiredEpisode> OnNewEpisode { get; }
}

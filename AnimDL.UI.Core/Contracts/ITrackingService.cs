namespace AnimDL.UI.Core.Contracts;

public interface ITrackingService
{
    IObservable<Tracking> Update(long id, Tracking tracking);
    IObservable<IEnumerable<AnimeModel>> GetAnime();
    IObservable<IEnumerable<ScheduledAnimeModel>> GetCurrentlyAiringTrackedAnime();
}

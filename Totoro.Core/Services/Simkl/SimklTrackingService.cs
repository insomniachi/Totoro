
namespace Totoro.Core.Services.Simkl;

internal class SimklTrackingService : ITrackingService
{
    private readonly ISimklClient _simklClient;

    public SimklTrackingService(ISimklClient simklClient,
                                ILocalSettingsService localSettingsService)
    {
        _simklClient = simklClient;
        IsAuthenticated = !string.IsNullOrEmpty(localSettingsService.ReadSetting<string>("SimklToken"));
    }

    public ListServiceType Type => ListServiceType.Simkl;

    public bool IsAuthenticated { get; private set; }

    public IObservable<bool> Delete(long id)
    {
        return Observable.Create<bool>(observer =>
        {
            _simklClient.RemoveItems(new SimklMutateListBody
            {
                Shows = new List<SimklMetaDataSlim>
                {
                    new()
                    {
                        Ids = new SimklIds
                        {
                            Simkl = id
                        }
                    }
                }
            });

            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var items = await _simklClient.GetAllItems(ItemType.Anime, SimklWatchStatus.Watching);
            observer.OnNext(items.Anime.Select(SimklToAnimeModelConverter.Convert));

            items = await _simklClient.GetAllItems(ItemType.Anime, null);
            observer.OnNext(items.Anime.Select(SimklToAnimeModelConverter.Convert));
            observer.OnCompleted();

            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        return Observable.Empty<IEnumerable<AnimeModel>>();
    }

    public void SetAccessToken(string accessToken)
    {
        IsAuthenticated = true;
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        return Observable.Create<Tracking>(async observer =>
        {
            var episodes = new List<EpisodeSlim>();

            if (tracking.WatchedEpisodes is > 0)
            {
                var eps = await _simklClient.GetEpisodes(id);
                if (eps.FirstOrDefault(x => x.EpisodeNumber == tracking.WatchedEpisodes) is { } epInfo)
                {
                    episodes.Add(new EpisodeSlim
                    {
                        Ids = new SimklIds
                        {
                            Simkl = epInfo.Ids.Simkl
                        }
                    });
                }
            }

            if (episodes.Count > 0 || tracking.Score is { })
            {
                await _simklClient.AddItems(new SimklMutateListBody
                {
                    Shows = new List<SimklMetaDataSlim>()
                    {
                        new()
                        {
                            Ids = new SimklIds
                            {
                                Simkl = id
                            },
                            Rating = tracking.Score
                        }
                    },
                    Episodes = episodes
                });
            }

            if (tracking.Status is { } status)
            {
                await _simklClient.MoveItems(new SimklMutateListBody
                {
                    Shows = new List<SimklMetaDataSlim>()
                    {
                        new()
                        {
                            Ids = new SimklIds
                            {
                                Simkl = id
                            },
                            To = status switch
                            {
                                AnimeStatus.PlanToWatch => "plantowatch",
                                AnimeStatus.Watching => "watching",
                                AnimeStatus.OnHold => "hold",
                                AnimeStatus.Completed => "completed",
                                AnimeStatus.Dropped => "dropped",
                                _ => null
                            }
                        }
                    },
                });
            }

            observer.OnNext(tracking);
            return Disposable.Empty;
        });
    }
}

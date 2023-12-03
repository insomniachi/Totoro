
namespace Totoro.Core.Services.Simkl;

internal class SimklTrackingService : ITrackingService
{
    private readonly ISimklClient _simklClient;
    private readonly IAnilistService _anilistService;
    private readonly SimklWatchStatus[] _status = new[] { SimklWatchStatus.Watching, SimklWatchStatus.PlanToWatch, SimklWatchStatus.Completed, SimklWatchStatus.Hold, SimklWatchStatus.Dropped };

    public SimklTrackingService(ISimklClient simklClient,
                                IAnilistService anilistService,
                                ILocalSettingsService localSettingsService)
    {
        _simklClient = simklClient;
        _anilistService = anilistService;
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
            foreach (var status in _status)
            {
                var items = await _simklClient.GetAllItems(ItemType.Anime, status);
                var watchedItems = new List<AnimeModel>();
                foreach (var item in items.Anime)
                {
                    var model = SimklToAnimeModelConverter.Convert(item);
                    if (item.Status == "watching" && model.AiringStatus == AiringStatus.CurrentlyAiring)
                    {
                        var epAndTime = await _anilistService.GetNextAiringEpisode(item.Show.Id.Simkl ?? item.Show.Id.Simkl2 ?? 0);
                        model.AiredEpisodes = epAndTime.Episode - 1 ?? 0;
                        model.NextEpisodeAt = epAndTime.Time;
                    }
                    watchedItems.Add(model);
                }
                observer.OnNext(watchedItems);
            }

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

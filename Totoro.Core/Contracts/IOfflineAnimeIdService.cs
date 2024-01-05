using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Contracts;

public interface IOfflineAnimeIdService
{
    bool IsAvailable { get; }
    void Initialize();
    Task UpdateOfflineMappings();
    AnimeIdExtended GetId(ListServiceType from, ListServiceType to, long id);
    AnimeIdExtended GetId(long id);
}

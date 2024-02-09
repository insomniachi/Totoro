using Refit;

namespace Totoro.Core.Services.Simkl;

internal interface ISimklClient
{
    [Get("/search/{type}?q={query}&extended=full&client_id={clientId}")]
    Task<List<SimklMetaData>> Search(string query, ItemType type, string clientId);

    [Get("/search/id?{service}={id}&client_id={clientId}")]
    Task<List<SimklMetaData>> Search(string service, long id, string clientId);

    [Get("/sync/all-items/{type}/{status}")]
    Task<SimklAllItems> GetAllItems(ItemType type, SimklWatchStatus? status);

    [Get("/anime/{id}?client_id={clientId}&extended=full")]
    Task<SimklMetaData> GetSummary(long id, string clientId);

    [Get("/anime/episodes/{id}?client_id={clientId}&extended=full")]
    Task<List<Episode>> GetEpisodes(long id, string clientId);

    [Post("/sync/history")]
    Task AddItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Post("/sync/add-to-list")]
    Task MoveItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Post("/sync/history/remove")]
    Task RemoveItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Get("/anime/airing?client_id={clientId}")]
    Task<List<SimklMetaData>> GetAiringAnime(string clientId);

    [Post("/users/settings")]
    Task<UserSettings> GetUserSettings();
}
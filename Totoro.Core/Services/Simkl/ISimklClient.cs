using Refit;

namespace Totoro.Core.Services.Simkl;

internal interface ISimklClient
{
    [Get("/search/{type}?q={query}&extended=full&client_id={clientId}")]
    Task<List<SimklMetaData>> Search(string query, ItemType type, string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");

    [Get("/search/id?{service}={id}&client_id={clientId}")]
    Task<List<SimklMetaData>> Search(string service, long id, string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");

    [Get("/sync/all-items/{type}/{status}")]
    Task<SimklAllItems> GetAllItems(ItemType type, SimklWatchStatus? status);

    [Get("/anime/{id}?client_id={clientId}&extended=full")]
    Task<SimklMetaData> GetSummary(long id, string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");

    [Get("/anime/episodes/{id}?client_id={clientId}&extended=full")]
    Task<List<Episode>> GetEpisodes(long id, string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");

    [Post("/sync/history")]
    Task AddItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Post("/sync/add-to-list")]
    Task MoveItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Post("/sync/history/remove")]
    Task RemoveItems([Body(BodySerializationMethod.Serialized)] SimklMutateListBody items);

    [Get("/anime/airing?client_id={clientId}")]
    Task<List<SimklMetaData>> GetAiringAnime(string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");

    [Post("/users/settings")]
    Task<UserSettings> GetUserSettings();
}
using System.Text.Json.Nodes;
using Flurl.Http;
using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly ISettings _settings;
    private readonly ISimklService _simklService;
    private readonly IKnownFolders _knownFolders;
    private readonly IFileService _fileService;
    private readonly string _dbUrl = @"https://raw.githubusercontent.com/Fribb/anime-lists/master/anime-offline-database-reduced.json";
    private readonly string _fileName = @"ids.json";
    private readonly List<AnimeIdExtended> _ids;
    private bool _isUpdating;

    public AnimeIdService(ISettings settings,
                          ISimklService simklService,
                          IKnownFolders knownFolders,
                          IFileService fileService)
    {
        _settings = settings;
        _simklService = simklService;
        _knownFolders = knownFolders;
        _fileService = fileService;

        _ids = fileService.Read<List<AnimeIdExtended>>(knownFolders.ApplicationData, _fileName) ?? new();
    }

    public async Task<AnimeIdExtended> GetId(ListServiceType serviceType, long id)
    {
        if(_isUpdating || _ids.FirstOrDefault(GetSelector(serviceType, id)) is not { } idExtended)
        {
            return await _simklService.GetId(serviceType, id);
        }

        return idExtended;
    }

    private static Func<AnimeIdExtended, bool> GetSelector(ListServiceType type, long serviceId)
    {
        return type switch
        {
            ListServiceType.AniList => (AnimeIdExtended id) => id.AniList == serviceId,
            ListServiceType.MyAnimeList => (AnimeIdExtended id) => id.MyAnimeList == serviceId,
            ListServiceType.Kitsu => (AnimeIdExtended id) => id.Kitsu == serviceId,
            ListServiceType.AniDb => (AnimeIdExtended id) => id.AniDb == serviceId,
            _ => (AnimeIdExtended id) => false,
        };
    }

    public Task<AnimeIdExtended> GetId(long id) => GetId(_settings.DefaultListService, id);

    public async Task UpdateOfflineMappings()
    {
        var stream = await _dbUrl.GetStreamAsync();
        var array = JsonNode.Parse(stream).AsArray();
        var keys = new[]
        {
            "livechart_id",
            "anidb_id",
            "kitsu_id",
            "mal_id",
            "anilist_id"
        };

        _isUpdating = true;
        _ids.Clear();

        foreach (var item in array)
        {
            var id = new AnimeIdExtended();
            var obj = item.AsObject();

            foreach (var key in keys)
            {
                if(obj.ContainsKey(key))
                {
                    var value = obj[key].GetValue<long>();
                    switch (key)
                    {
                        case "livechart_id":
                            id.LiveChart = value;
                            break;
                        case "anidb_id":
                            id.AniDb = value;
                            break;
                        case "kitsu_id":
                            id.Kitsu = value;
                            break;
                        case "mal_id":
                            id.MyAnimeList = value;
                            break;
                        case "anilist_id":
                            id.AniList = value;
                            break;
                    }
                }
            }

            _ids.Add(id);
        }
        _isUpdating = false;
        _fileService.Save(_knownFolders.ApplicationData, _fileName, _ids);
    }
}

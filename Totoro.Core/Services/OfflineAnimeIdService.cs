using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Flurl.Http;
using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Services;

public class OfflineAnimeIdService : IOfflineAnimeIdService
{
    private readonly IFileService _fileService;
    private readonly IKnownFolders _knownFolders;
    private readonly ISettings _settings;
    private readonly string _dbUrl = @"https://raw.githubusercontent.com/Fribb/anime-lists/master/anime-offline-database-reduced.json";
    private readonly string _fileName = @"ids.json";
    private List<AnimeIdExtended> _ids;
    private FrozenDictionary<long, AnimeIdExtended> _idsMap;

    public OfflineAnimeIdService(IFileService fileService,
                        IKnownFolders knownFolders,
                        ISettings settings)
    {
        _fileService = fileService;
        _knownFolders = knownFolders;
        _settings = settings;
    }

    public bool IsAvailable { get; set; } = true;

    public void Initialize()
    {
        _ids = _fileService.Read<List<AnimeIdExtended>>(_knownFolders.ApplicationData, _fileName) ?? [];
        _idsMap = GetMapping(_settings.DefaultListService);
    }

    public AnimeIdExtended GetId(ListServiceType serviceType, long id)
    {
        if (!IsAvailable || _ids.FirstOrDefault(GetSelector(serviceType, id)) is not { } idExtended)
        {
            return null;
        }

        return idExtended;
    }

    public AnimeIdExtended GetId(long id)
    {
        if(!IsAvailable || !_idsMap.TryGetValue(id, out var idExtended))
        {
            return null;
        }

        return idExtended;
    }

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

        IsAvailable = false;
        _ids.Clear();

        foreach (var item in array)
        {
            var id = new AnimeIdExtended();
            var obj = item.AsObject();

            foreach (var key in keys)
            {
                if (obj.ContainsKey(key))
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
        _idsMap = GetMapping(_settings.DefaultListService);
        IsAvailable = true;
        _fileService.Save(_knownFolders.ApplicationData, _fileName, _ids);
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

    private FrozenDictionary<long, AnimeIdExtended> GetMapping(ListServiceType type)
    {
        return type switch
        {
            ListServiceType.MyAnimeList => _ids.Where(x => x.MyAnimeList.HasValue).ToFrozenDictionary(x => x.MyAnimeList.Value),
            ListServiceType.AniList => _ids.Where(x => x.AniList.HasValue).ToFrozenDictionary(x => x.AniList.Value),
            ListServiceType.Kitsu => _ids.Where(x => x.Kitsu.HasValue).ToFrozenDictionary(x => x.Kitsu.Value),
            ListServiceType.AniDb => _ids.Where(x => x.AniDb.HasValue).ToFrozenDictionary(x => x.AniDb.Value),
            _ => FrozenDictionary<long, AnimeIdExtended>.Empty,
        };
    }
}

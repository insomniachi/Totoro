using Splat;
using Totoro.Core.Services.Simkl;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.Services
{
    public partial class StreamPageMapper : IStreamPageMapper, IEnableLogger
    {
        private readonly IPluginFactory<AnimeProvider> _providerFactory;
        private readonly IAnimeIdService _animeIdService;
        private readonly ISettings _settings;

        public StreamPageMapper(IPluginFactory<AnimeProvider> providerFactory,
                                IAnimeIdService animeIdService,
                                ISettings settings)
        {
            _providerFactory = providerFactory;
            _animeIdService = animeIdService;
            _settings = settings;
        }

        public async Task<long?> GetIdFromUrl(string url, string provider)
        {
            try
            {
                var instance = _providerFactory.CreatePlugin(provider);

                if (instance is null || instance.IdMapper is null)
                {
                    return null;
                }

                var id = await instance.IdMapper.MapId(url);

                if (id.MyAnimeList is null || id.AniList is null || _settings.DefaultListService is ListServiceType.Simkl)
                {
                    var fullId = await GetFullId(id);
                    return GetId(fullId);
                }

                return GetId(id);
            }
            catch (Exception ex)
            {
                this.Log().Warn(ex);
                return null;
            }
        }

        private long GetId(AnimeId animeId)
        {
            return _settings.DefaultListService switch
            {
                ListServiceType.AniList => animeId.AniList ?? throw new ArgumentException("Anilist id not found"),
                ListServiceType.MyAnimeList => animeId.MyAnimeList ?? throw new ArgumentException("MAL id not found"),
                ListServiceType.Kitsu => animeId.Kitsu ?? throw new ArgumentException("Kitsu id not found"),
                ListServiceType.AniDb => animeId.AniDb ?? throw new ArgumentException("AniDb id not found"),
                _ => throw new NotSupportedException()
            };
        }

        private long GetId(AnimeIdExtended animeId)
        {
            return _settings.DefaultListService switch
            {
                ListServiceType.AniList or ListServiceType.MyAnimeList or ListServiceType.Kitsu or ListServiceType.AniDb => GetId((AnimeId)animeId),
                ListServiceType.Simkl => animeId.Simkl ?? throw new ArgumentException("Simkl id not found"),
                _ => throw new NotSupportedException()
            };
        }

        private async Task<AnimeIdExtended> GetFullId(AnimeId id)
        {
            (ListServiceType type, long listId) = id switch
            {
                { MyAnimeList: not null } => (ListServiceType.MyAnimeList, id.MyAnimeList.Value),
                { AniList: not null } => (ListServiceType.AniList, id.AniList.Value),
                { Kitsu: not null } => (ListServiceType.Kitsu, id.Kitsu.Value),
                { AniDb: not null } => (ListServiceType.AniDb, id.AniDb.Value)
            };

            return await _animeIdService.GetId(type, listId);
        }
    }
}

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.Services
{
    public partial class StreamPageMapper : IStreamPageMapper, IEnableLogger
    {
        private readonly IPluginFactory<AnimeProvider> _providerFactory;
        private readonly ISettings _settings;

        class SearchResult : ICatalogItem
        {
            required public string Title { get; init; }
            required public string Url { get; init; }
        }

        public StreamPageMapper(IPluginFactory<AnimeProvider> providerFactory,
                                ISettings settings)
        {
            _providerFactory = providerFactory;
            _settings = settings;
        }

        public async Task<long?> GetIdFromUrl(string url, string provider)
        {
            try
            {
                var instance = _providerFactory.CreatePlugin(provider);

                if(instance is null || instance.IdMapper is null)
                {
                    return null;
                }

                var id = await instance.IdMapper.MapId(url);

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
    }
}

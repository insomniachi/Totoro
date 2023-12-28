using FuzzySharp;
using Splat;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.WinUI.Services
{
    internal class ExternalMediaPlayerLauncher : ReactiveObject
    {
        private readonly IVideoStreamResolverFactory _videoStreamResolverFactory;
        private readonly ISettings _settings;
        private readonly IViewService _viewService;
        private INativeMediaPlayer _mediaPlayer;
        private IVideoStreamModelResolver _videoStreamResolver;
        private string _title;

        public AnimeProvider Provider { get; private set; }
        public string ProviderType { get; private set; }
        [Reactive] public EpisodeModelCollection EpisodeModels { get; set; }


        public ExternalMediaPlayerLauncher(IVideoStreamResolverFactory videoStreamResolverFactory,
                                           ISettings settings,
                                           IViewService viewService)
        {
            _videoStreamResolverFactory = videoStreamResolverFactory;
            _settings = settings;
            _viewService = viewService;

            this.WhenAnyValue(x => x.EpisodeModels)
                .WhereNotNull()
                .SelectMany(x => x.WhenAnyValue(x => x.Current))
                .WhereNotNull()
                .SelectMany(epModel => epModel.IsSpecial
                        ? ((ISpecialVideoStreamModelResolver)_videoStreamResolver).ResolveSpecialEpisode(epModel.SpecialEpisodeNumber, StreamType.Subbed(Languages.English))
                        : _videoStreamResolver.ResolveEpisode(epModel.EpisodeNumber, StreamType.Subbed(Languages.English)))
                .Subscribe(stream =>
                {
                    var quality = GetDefaultQuality(stream.Qualities);
                    ((ICanLaunch)_mediaPlayer).Launch(_title, stream.GetStreamModel(quality).StreamUrl);
                }, RxApp.DefaultExceptionHandler.OnError);
        }

        public async Task Initialize(AnimeModel anime, string providerType, string mediaPlayerType)
        {
            ProviderType = providerType;
            Provider = PluginFactory<AnimeProvider>.Instance.CreatePlugin(providerType);
            _mediaPlayer = PluginFactory<INativeMediaPlayer>.Instance.CreatePlugin(mediaPlayerType);
            var result = await SearchProvider(anime.Title);
            var hasSubDub = result is { Dub: { }, Sub: { } };
            var searResult = _settings.PreferSubs ? result.Dub ?? result.Sub : result.Sub;

            if (hasSubDub)
            {
                _videoStreamResolver = _videoStreamResolverFactory.CreateSubDubResolver(ProviderType, result.Sub.Url, result.Dub.Url);
            }
            else
            {
                _videoStreamResolver = _videoStreamResolverFactory.CreateAnimDLResolver(ProviderType, searResult.Url);
            }

            EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(StreamType.Subbed(Languages.English));
            var currentEp = (anime?.Tracking?.WatchedEpisodes ?? 0) + 1;
            _title = $"{anime.Title} - {currentEp.ToString().PadLeft(2, '0')}";
            EpisodeModels.SelectEpisode(currentEp);
        }

        private async Task<(ICatalogItem Sub, ICatalogItem Dub)> SearchProvider(string title)
        {
            var results = await Provider.Catalog.Search(title).ToListAsync();

            if (results.Count == 0)
            {
                return (null, null);
            }

            if (results.Count == 1)
            {
                return (results[0], null);
            }
            else if (results.Count == 2 && ProviderType is "gogo-anime") // gogo anime has separate listing for sub/dub
            {
                return (results[0], results[1]);
            }
            else if (results.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase)) is { } exactMatch)
            {
                return (exactMatch, null);
            }
            else
            {
                var suggested = results.MaxBy(x => Fuzz.PartialRatio(x.Title, title));
                this.Log().Debug($"{results.Count} entries found, suggested entry : {suggested.Title}({suggested.Url}) Confidence : {Fuzz.PartialRatio(suggested.Title, title)}");
                return (await _viewService.ChoooseSearchResult(suggested, results, ProviderType), null);
            }
        }

        private string GetDefaultQuality(IEnumerable<string> qualities)
        {
            var list = qualities.ToList();

            if (list.Count == 1)
            {
                return list.First();
            }
            else if (list.Contains("auto") && _settings.DefaultStreamQualitySelection == StreamQualitySelection.Auto)
            {
                return "auto";
            }
            else
            {
                return list.Where(x => x != "hardsub").OrderBy(x => x.Length).ThenBy(x => x).LastOrDefault();
            }
        }
    }
}

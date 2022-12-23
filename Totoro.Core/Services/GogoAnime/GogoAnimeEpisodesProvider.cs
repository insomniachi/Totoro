using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AnimDL.Api;
using AnimDL.Core;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;

namespace Totoro.Core.Services.GogoAnime;

public partial class GogoAnimeEpisodesProvider : IRecentEpisodesProvider
{
    public const string AJAX_URL = "https://ajax.gogo-load.com/ajax/page-recent-release.html?page=1&type=1";
    private readonly HttpClient _httpClient;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly string _urlStripped;
    private readonly HtmlWeb _web = new();

    public GogoAnimeEpisodesProvider(HttpClient httpClient,
                                     IStreamPageMapper streamPageMapper)
    {
        _httpClient = httpClient;
        _streamPageMapper = streamPageMapper;
        _urlStripped = DefaultUrl.GogoAnime.EndsWith("/") || DefaultUrl.GogoAnime.EndsWith("\\") ? DefaultUrl.GogoAnime[..^1] : DefaultUrl.GogoAnime;
    }

    public IObservable<long> GetMalId(AiredEpisode ep)
    {
        var uri = new Uri(ep.EpisodeUrl);
        var match = IdentifierRegex().Match(uri.AbsolutePath);

        if (!match.Success)
        {
            return Observable.Return((long)0);
        }

        return _streamPageMapper.GetMalId(match.Groups[1].Value, ProviderType.GogoAnime).ToObservable();
    }

    public IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes()
    {
        return Observable.Create<IEnumerable<AiredEpisode>>(async observer =>
        {
            var doc = await _web.LoadFromWebAsync(AJAX_URL);

            var nodes = doc.QuerySelectorAll(".items li");
            var list = new List<AiredEpisode>();

            foreach (var item in nodes)
            {
                var title = item.SelectSingleNode("div/a").Attributes["title"].Value;
                var url = _urlStripped + item.SelectSingleNode("div/a").Attributes["href"].Value;
                var img = item.SelectSingleNode("div/a/img").Attributes["src"].Value;
                var ep = item.QuerySelector(".episode").InnerText.Trim();
                list.Add(new AiredEpisode
                {
                    Anime = title,
                    EpisodeUrl = url,
                    Image = img,
                    InfoText = ep
                });
            }

            observer.OnNext(list);
            observer.OnCompleted();
        });
    }

    [GeneratedRegex("/?(.+)-episode-\\d+")]
    private static partial Regex IdentifierRegex();
}

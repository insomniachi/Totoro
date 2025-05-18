using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimePahe;

internal class AnimeHeavenExtractor : IVideoExtractor
{
	public async IAsyncEnumerable<VideoSource> Extract(Uri url)
	{
		var response = await url
			.WithHeader(HeaderNames.UserAgent, FlurlExtensions.USER_AGENT)
			.GetStreamAsync();

		var htmlDoc = new HtmlDocument();
		htmlDoc.Load(response);

		var items = htmlDoc.DocumentNode.QuerySelectorAll("source") ?? [];

		foreach (var item in items)
		{
			yield return new VideoSource
			{
				Url = new Uri(item.GetAttributeValue("src", "")),
			};
		}
	}
}

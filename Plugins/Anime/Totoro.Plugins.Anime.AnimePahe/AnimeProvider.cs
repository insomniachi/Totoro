using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimePahe;

internal class AnimeHeavenProvider : IAnimeProvider
{
	public const string BaseUrl = "https://animeheaven.me";

	class AnimeHeavenSearchResult : ICatalogItem
	{
		public required string Title { get; set; }
		public required string Url { get; set; }
		public required string Image { get; set; }	
	}

	public async IAsyncEnumerable<ICatalogItem> SearchAsync(string query)
	{
		var response = await BaseUrl
			.AppendPathSegment("fastsearch.php")
			.SetQueryParams(new
			{
				xhr = 1,
				s = query
			})
			.GetStreamAsync();

		var htmlDoc = new HtmlDocument();
		htmlDoc.Load(response);

		var items = htmlDoc.DocumentNode.SelectNodes("a");
		if (items is null)
		{
			yield break;
		}

		foreach (var node in items)
		{
			var name = node.QuerySelector(".fastname").InnerText;
			var image = Url.Combine(BaseUrl, node.QuerySelector("img").GetAttributeValue("src", ""));
			var url = Url.Combine(BaseUrl, node.GetAttributeValue("href", ""));

			yield return new AnimeHeavenSearchResult
			{
				Title = name,
				Image = image,
				Url = url
			};
		}
	}

	public async IAsyncEnumerable<Episode> GetEpisodes(string animeId)
	{
		var response = await BaseUrl
			.AppendPathSegment($"/anime.php?{animeId}")
			.WithHeader(HeaderNames.UserAgent, FlurlExtensions.USER_AGENT)
			.GetStreamAsync();

		var htmlDoc = new HtmlDocument();
		htmlDoc.Load(response);

		var items = htmlDoc.DocumentNode.QuerySelectorAll(".ac3") ?? [];

		foreach (var item in items.Reverse())
		{
			yield return new Episode
			{
				Id = item.GetAttributeValue("href", "").Split("?").Last(),
				Number = float.Parse(item.QuerySelector(".watch2 .bc").InnerHtml)
			};
		}
	}

	public async IAsyncEnumerable<VideoServer> GetServers(Uri uri, string episodeId)
	{
		yield return new VideoServer
		{
			Name = "AnimeHeaven",
			Url = new Uri(Url.Combine(uri.ToString(), $"/episode.php?{episodeId}")),
		};

		await Task.CompletedTask;
	}
}

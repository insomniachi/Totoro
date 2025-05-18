using System.Text.Json.Nodes;
using Flurl;
using FlurlGraphQL;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using static Totoro.Plugins.Anime.AllAnime.Catalog;

namespace Totoro.Plugins.Anime.AllAnime
{
	internal class AnimeProviderImpl(ConfigNew config) : IAnimeProvider
	{
		public IAsyncEnumerable<Episode> GetEpisodes(string animeId)
		{
			throw new NotImplementedException();
		}

		public IAsyncEnumerable<VideoServer> GetServers(Uri uri, string episodeId)
		{
			throw new NotImplementedException();
		}

		public async IAsyncEnumerable<ICatalogItem> SearchAsync(string query)
		{
			var jObject = await ConfigNew.Api
				.WithGraphQLQuery(SEARCH_QUERY)
				.SetGraphQLVariables(new
				{
					search = new
					{
						allowAdult = true,
						allowUnknown = true,
						query
					},
					limit = 40
				})
				.PostGraphQLQueryAsync()
				.ReceiveGraphQLRawSystemTextJsonResponse();

			foreach (var item in jObject?["shows"]?["edges"]?.AsArray().OfType<JsonObject>() ?? [])
			{
				_ = long.TryParse($"{item?["malId"]}", out long malId);
				_ = long.TryParse($"{item?["aniListId"]}", out long aniListId);
				var title = $"{item?["name"]}";
				var url = Url.Combine(config.Url, $"/anime/{item?["_id"]}");
				var season = "";
				var year = "";
				if (item?.ContainsKey(@"season") == true)
				{
					season = $"{item?["season"]?["quarter"]}";
					year = $"{item?["season"]?["year"]}";
				}
				var rating = $"{item?["score"]}";
				var image = $"{item?["thumbnail"]}";

				yield return new SearchResult
				{
					Title = title,
					Url = url,
					Season = season,
					Year = year,
					Rating = rating,
					Image = image,
					MalId = malId,
					AnilistId = aniListId,
				};
			}
		}
	}
}

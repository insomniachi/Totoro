using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Jellyfin;

internal class StreamProvider : IAnimeStreamProvider
{
	public async Task<int> GetNumberOfStreams(string url)
	{
		var episodes = await url
			.WithHeader("X-Emby-Token", ConfigManager<Config>.Current.ApiKey)
			.GetJsonAsync<Root>();

		return episodes.Items.Max(x => x.IndexNumber);
	}

	public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
	{
		var episodes = await url
			.WithHeader("X-Emby-Token", ConfigManager<Config>.Current.ApiKey)
			.GetJsonAsync<Root>();

		var total = episodes.Items.Max(x => x.IndexNumber);

		var (start, end) = episodeRange.Extract(total);

		for (var i = start; i <= end; i++)
		{
			var episode = episodes.Items.FirstOrDefault(x => x.IndexNumber == i);

			if(episode is null)
			{
				continue;
			}

			yield return new VideoStreamsForEpisode
			{
				Episode = i,
				AdditionalInformation =
				{
					Title = episode.Name
				},
				Streams =
				{
					new VideoStream
					{
						Resolution = "default",
						Url =  ConfigManager<Config>.Current.BaseUrl.AppendPathSegment($"/Items/{episode.Id}/Download").SetQueryParam("api_key", ConfigManager<Config>.Current.ApiKey)
					}
				}
			};
		}
	}
}

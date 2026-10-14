using System.Globalization;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.Senshi;

internal sealed class SenshiProvider(
	IHttpClientFactory httpClientFactory,
	IModuleSettings<Settings> moduleSettings,
	ILogger<SenshiProvider> logger) : IAnimeProvider
{
	public Guid Id => SenshiModule.Id;

	public string Name => "Senshi";

	private Settings _settings = moduleSettings.Value;

	public void UseSettings(DataContainerConfig settings)
	{
		_settings = (Settings)settings;
	}

	public async Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct)
	{
		try
		{
			using var client = CreateClient();
			var response = await client.Request("anime", "filter")
									   .PostJsonAsync(new
									   {
										   searchTerm = query,
										   page = 1,
										   limit = 10
									   }, cancellationToken: ct)
									   .ReceiveJson<SenshiSearchResponse>();

			if (response?.Items is null)
			{
				return [];
			}

			return
			[
				.. response.Items
						   .Select(item => new ProviderShow(item.Id, item.Title, TitleMatch.Score(query, item.Title)))
						   .OrderByDescending(x => x.Score)
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Senshi search for \"{Query}\" failed", query);
			return [];
		}
	}

	public async Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct)
	{
		try
		{
			using var client = CreateClient();
			var info = await client.Request("anime", showId)
								   .GetJsonAsync<SenshiItem>(cancellationToken: ct);

			if (info is null)
			{
				return [];
			}

			var episodes = await client.Request("episodes", info.InternalId)
									   .GetJsonAsync<List<SenshiEpisode>>(cancellationToken: ct);

			if (episodes is null)
			{
				return [];
			}

			return
			[
				.. episodes.Select(episode => new ProviderEpisode(
					info.InternalId.ToString(CultureInfo.InvariantCulture),
					$"{episode.Episode}",
					episode.Episode))
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't fetch episodes for show {ShowId}", showId);
			return [];
		}
	}

	public async Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct)
	{
		try
		{
			using var client = CreateClient();
			var servers = await client.Request("episode-embeds", episode.ShowId, episode.Id)
									  .GetJsonAsync<List<SenshiStream>>(cancellationToken: ct);

			if (servers is null)
			{
				return [];
			}

			var preferred = _settings.Audio == AudioType.Dub ? "dub" : "sub";

			return
			[
				.. servers.Where(server => Uri.TryCreate(server.StreamUrl, UriKind.Absolute, out _))
						  .OrderByDescending(server => server.Type.Contains(preferred, StringComparison.OrdinalIgnoreCase))
						  .Select(server => new VideoServer(server.Type, new Uri(server.StreamUrl))
						  {
							  Headers = new Dictionary<string, string>
							  {
								  ["Referer"] = client.BaseUrl ?? "https://senshi.live/",
								  ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
							  }
						  })
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't fetch servers for {ShowId} ep {Episode}", episode.ShowId, episode.Id);
			return [];
		}
	}

	private FlurlClient CreateClient()
	{
		var client = httpClientFactory.CreateClient("Senshi");
		var flurl = new FlurlClient(client);
		if (!string.IsNullOrWhiteSpace(_settings.Site))
		{
			flurl.BaseUrl = _settings.Site.Trim().TrimEnd('/');
		}
		return flurl;
	}
}

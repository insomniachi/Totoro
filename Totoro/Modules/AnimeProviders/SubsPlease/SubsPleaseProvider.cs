using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Core.Module.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.SubsPlease;

/// <summary>
/// SubsPlease: English-subtitled simulcasts shortly after they air. Videos are torrents (hosted on nyaa.si), one per
/// resolution, which the player streams itself (mpv with a webtorrent script).
/// </summary>
internal sealed partial class SubsPleaseProvider(IModuleSettings<Settings> settings) : IAnimeProvider
{
	public Guid Id => SubsPleaseModule.Id;

	public string Name => "SubsPlease";

	public Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct)
	{
		return SubsPleaseCatalog.SearchAsync(query, ct);
	}

	public async Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct)
	{
		var episodes = await GetReleasesAsync(showId, ct);
		return
		[
			.. episodes.Select(x => float.TryParse(x.Value.Episode, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
										? new ProviderEpisode(showId, x.Key, number)
										: null)
					   .OfType<ProviderEpisode>()
		];
	}

	public async Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct)
	{
		var releases = await GetReleasesAsync(episode.ShowId, ct);
		if (!releases.TryGetValue(episode.Id, out var release))
		{
			return [];
		}

		var preferred = settings.Value.Quality.ToResolution();
		return
		[
			.. release.Downloads
					  .Select(x => (Resolution: int.TryParse(x.Resolution, out var resolution) ? resolution : 0, Download: x))
					  // The preferred quality first, then the best of the rest.
					  .OrderByDescending(x => x.Resolution == preferred)
					  .ThenByDescending(x => x.Resolution)
					  .Select(x => ToDownloadUrl(x.Download.Torrent) is { } url
									   ? new VideoServer($"{x.Download.Resolution}p", url, VideoServerKind.Torrent)
									   : null)
					  .OfType<VideoServer>()
		];
	}

	/// <summary>The show's releases (episodes), by name, e.g. "Sousou no Frieren - 28".</summary>
	private static async Task<Dictionary<string, Release>> GetReleasesAsync(string showId, CancellationToken ct)
	{
		// The API wants the show's numeric id, which is only on its page.
		var html = await $"https://subsplease.org/shows/{showId}/".GetStringAsync(cancellationToken: ct);
		var document = new HtmlDocument();
		document.LoadHtml(html);

		var sid = document.QuerySelector("#show-release-table")?.GetAttributeValue("sid", "");
		if (string.IsNullOrEmpty(sid))
		{
			return [];
		}

		// The time zone only affects the release dates, which aren't used; IANA, as the site expects.
		TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var timeZone);
		var json = await "https://subsplease.org/api"
						 .AppendQueryParam("f", "show")
						 .AppendQueryParam("tz", timeZone ?? "UTC")
						 .AppendQueryParam("sid", sid)
						 .GetStringAsync(cancellationToken: ct);

		using var response = JsonDocument.Parse(json);
		return response.RootElement.TryGetProperty("episode", out var episodes) && episodes.ValueKind == JsonValueKind.Object
			? episodes.Deserialize<Dictionary<string, Release>>() ?? []
			: [];
	}

	/// <summary>SubsPlease links to nyaa.si's page for a torrent; the file itself is at a different address.</summary>
	private static Uri? ToDownloadUrl(string url)
	{
		var match = NyaaViewUrl().Match(url);
		return match.Success ? new Uri($"https://nyaa.si/download/{match.Groups["id"].Value}.torrent") : null;
	}

	[GeneratedRegex(@"^https?://nyaa\.si/view/(?<id>\d+)", RegexOptions.IgnoreCase)]
	private static partial Regex NyaaViewUrl();

	private sealed class Release
	{
		[JsonPropertyName("episode")] public string Episode { get; set; } = "";
		[JsonPropertyName("downloads")] public List<Download> Downloads { get; set; } = [];
	}

	private sealed class Download
	{
		[JsonPropertyName("res")] public string Resolution { get; set; } = "";
		[JsonPropertyName("torrent")] public string Torrent { get; set; } = "";
	}
}

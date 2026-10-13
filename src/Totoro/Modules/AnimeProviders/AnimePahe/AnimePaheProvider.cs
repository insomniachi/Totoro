using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Core.Web;

namespace Totoro.Modules.AnimeProviders.AnimePahe;

/// <summary>
/// AnimePahe (after the Android extension at github.com/yuzono/anime-extensions, src/en/animepahe): its JSON API for
/// searching and episodes, and its episode pages for the videos, which are on Kwik as HLS streams. The site is behind
/// Cloudflare, which turns away anything but a browser, so it's all loaded through one (<see cref="IWebPageLoader"/>).
/// </summary>
/// <remarks>
/// A show's id here is <c>"{AnimePahe's anime id}:{title}"</c>: the anime id is stable, but the site addresses shows by
/// a session id that changes every few days (see <see cref="AnimePaheSessions"/>), and the title finds the new one.
/// </remarks>
internal sealed partial class AnimePaheProvider(
	IModuleSettings<Settings> moduleSettings,
	AnimePaheSessions sessions,
	IWebPageLoader pages,
	ILogger<AnimePaheProvider> logger) : IAnimeProvider
{
	public Guid Id => AnimePaheModule.Id;

	public string Name => "AnimePahe";

	// The module's settings, unless an anime's own are used instead.
	private Settings _settings = moduleSettings.Value;

	public void UseSettings(DataContainerConfig settings)
	{
		_settings = (Settings)settings;
	}

	private Uri Site => Uri.TryCreate(_settings.Site.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var site)
		? site
		: new Uri("https://animepahe.pw/");

	public async Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct)
	{
		var shows = await SearchShowsAsync(query, ct);
		return
		[
			.. shows.Select(x => new ProviderShow($"{x.AnimeId}:{x.Title}", x.Title, TitleMatch.Score(query, x.Title)))
					.OrderByDescending(x => x.Score)
		];
	}

	public Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct)
	{
		// None yet (an upcoming show) is an answer too, once the session's known to be current.
		return WithSessionAsync(showId, session => GetEpisodesAsync(showId, session, ct), [], ct);
	}

	public Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct)
	{
		return WithSessionAsync(episode.ShowId, session => GetServersAsync(session, episode.Id, ct), [], ct);
	}

	/// <summary>
	/// Runs <paramref name="action"/> with the show's session; when AnimePahe has nothing for it (the session is stale,
	/// or unknown), finds the current one by searching for the show, and runs it again.
	/// </summary>
	private async Task<T> WithSessionAsync<T>(string showId, Func<string, Task<T?>> action, T nothing, CancellationToken ct)
		where T : class
	{
		var (animeId, title) = ParseShowId(showId);
		var cached = sessions.Get(animeId)?.Session;
		if (cached is not null && await action(cached) is { } result)
		{
			return result;
		}

		// Searching updates the sessions of every show it finds.
		await SearchShowsAsync(title, ct);
		var current = sessions.Get(animeId)?.Session;
		if (current is null)
		{
			throw new InvalidOperationException($"AnimePahe doesn't have \"{title}\" any more. Pick its show again.");
		}

		return current == cached ? nothing : await action(current) ?? nothing;
	}

	private static (string AnimeId, string Title) ParseShowId(string showId)
	{
		var separator = showId.IndexOf(':');
		return separator < 0 ? (showId, "") : (showId[..separator], showId[(separator + 1)..]);
	}

	/// <summary>The API's search results, whose sessions are kept for later.</summary>
	private async Task<List<(string AnimeId, string Title)>> SearchShowsAsync(string query, CancellationToken ct)
	{
		using var json = await GetJsonAsync($"api?m=search&q={Uri.EscapeDataString(query)}", ct);
		List<(string, string)> shows = [];
		foreach (var item in Items(json))
		{
			if (item.TryGetProperty("id", out var id) && item.TryGetProperty("session", out var session) &&
				item.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } name)
			{
				var animeId = id.ToString();
				sessions.Set(animeId, session.GetString() ?? "", name);
				shows.Add((animeId, name));
			}
		}

		return shows;
	}

	/// <summary>The show's episodes, from every page of them; null if AnimePahe has none for the session.</summary>
	private async Task<List<ProviderEpisode>?> GetEpisodesAsync(string showId, string session, CancellationToken ct)
	{
		List<ProviderEpisode> episodes = [];
		for (var page = 1; ; page++)
		{
			using var json = await GetJsonAsync($"api?m=release&id={Uri.EscapeDataString(session)}&sort=episode_asc&page={page}", ct);
			var items = Items(json).ToList();
			if (items.Count == 0)
			{
				return page == 1 ? null : episodes;
			}

			foreach (var item in items)
			{
				if (item.TryGetProperty("session", out var episode) && item.TryGetProperty("episode", out var number) &&
					number.ValueKind == JsonValueKind.Number)
				{
					episodes.Add(new ProviderEpisode(showId, episode.GetString() ?? "", number.GetSingle()));
				}
			}

			var root = json!.RootElement;
			if (!root.TryGetProperty("last_page", out var last) || !last.TryGetInt32(out var lastPage) || page >= lastPage)
			{
				return episodes;
			}

			// The API limits how often it's asked.
			await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
		}
	}

	/// <summary>
	/// The episode's video in the preferred audio and quality (or the closest there is), resolved to its stream; null if
	/// AnimePahe has no episode page for the session. Only one: each takes a page load to resolve.
	/// </summary>
	private async Task<List<VideoServer>?> GetServersAsync(string session, string episode, CancellationToken ct)
	{
		var page = await pages.LoadAsync(new Uri(Site, $"play/{session}/{episode}"), Site, ct);
		var document = new HtmlDocument();
		document.LoadHtml(page.Content);
		var buttons = document.DocumentNode.SelectNodes("//div[@id='resolutionMenu']//button[@data-src]");
		if (buttons is null)
		{
			return null;
		}

		var audio = _settings.Audio.ToCode();
		var resolution = _settings.Quality.ToResolution();
		var videos = buttons.Select(x => new Video(
								x.GetAttributeValue("data-src", ""),
								int.TryParse(x.GetAttributeValue("data-resolution", ""), CultureInfo.InvariantCulture, out var height) ? height : 0,
								x.GetAttributeValue("data-audio", ""),
								x.GetAttributeValue("data-av1", "0") == "1",
								HtmlEntity.DeEntitize(x.GetAttributeValue("data-fansub", "")).Trim()))
							.Where(x => Uri.IsWellFormedUriString(x.Kwik, UriKind.Absolute))
							// The preferred audio, then quality; H.264 before AV1, which not every graphics card plays well.
							.OrderByDescending(x => x.Audio == audio)
							.ThenByDescending(x => x.Resolution == resolution)
							.ThenBy(x => x.Av1)
							.ThenByDescending(x => x.Resolution);

		foreach (var video in videos)
		{
			try
			{
				return [await ResolveAsync(video, ct)];
			}
			catch (Exception ex) when (ex is not OperationCanceledException and not WebPageException)
			{
				logger.LogWarning(ex, "Couldn't get the {Resolution}p video from Kwik", video.Resolution);
			}
		}

		return [];
	}

	/// <summary>The video's HLS stream: Kwik's player page has it in a packed script, and wants Kwik as the referrer.</summary>
	private async Task<VideoServer> ResolveAsync(Video video, CancellationToken ct)
	{
		var kwik = new Uri(video.Kwik);
		var page = await pages.LoadAsync(kwik, Site, ct);
		var stream = JsUnpacker.Unpack(page.Content)
								.Select(x => StreamSource().Match(x))
								.FirstOrDefault(x => x.Success)?.Groups["url"].Value
					 ?? throw new InvalidOperationException("Kwik's page has no video address.");

		var origin = $"{kwik.Scheme}://{kwik.Authority}";
		var name = $"{video.Resolution}p{(video.Audio == "eng" ? " English" : "")}{(video.Fansub.Length > 0 ? $" ({video.Fansub})" : "")}";
		return new VideoServer(name, new Uri(stream))
		{
			Headers = new Dictionary<string, string>
			{
				["Referer"] = $"{origin}/",
				["Origin"] = origin,
			},
		};
	}

	/// <summary>The API response's items (its "data"), none if it isn't one, e.g. for a session it doesn't know.</summary>
	private static IEnumerable<JsonElement> Items(JsonDocument? json)
	{
		return json?.RootElement is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("data", out var data) &&
			   data.ValueKind == JsonValueKind.Array
			? data.EnumerateArray()
			: [];
	}

	/// <summary>An API call's JSON; null when the answer isn't JSON (a missing page, say).</summary>
	private async Task<JsonDocument?> GetJsonAsync(string path, CancellationToken ct)
	{
		var page = await pages.LoadAsync(new Uri(Site, path), Site, ct);
		if (!page.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		try
		{
			return JsonDocument.Parse(page.Content);
		}
		catch (JsonException ex)
		{
			logger.LogWarning(ex, "AnimePahe's API answered {Path} with something that isn't JSON", path);
			return null;
		}
	}

	/// <param name="Kwik">The player page on Kwik.</param>
	/// <param name="Audio">"jpn" or "eng".</param>
	private sealed record Video(string Kwik, int Resolution, string Audio, bool Av1, string Fansub);

	// In the unpacked player script: const source='https://.../uwu.m3u8' (quotes perhaps escaped).
	[GeneratedRegex(@"source\s*=\s*\\?['""](?<url>https?://[^'""\\\s]+)")]
	private static partial Regex StreamSource();
}

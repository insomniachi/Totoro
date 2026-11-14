using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.Core.Services;

public enum AnimeSongKind
{
	Opening,
	Ending,
	Insert,
}

[Description("A song of an anime: an opening, an ending or an insert song.")]
public sealed record AnimeSong(
	AnimeSongKind Kind,
	[property: Description("Its number among its kind, e.g. 2 for the second opening; null if it's the only one, or not numbered.")]
	int? Number,
	string Title,
	[property: Description("Who sings it; empty if not known.")]
	string Artist,
	[property: Description("The episodes it's in, e.g. \"1–16\"; empty if not known.")]
	string Episodes,
	[property: Description("Its creditless video, if there is one.")]
	Uri? Video,
	[property: Description("Its audio alone, if there is one.")]
	Uri? Audio)
{
	/// <summary>e.g. "OP2", "ED", "IN".</summary>
	public string Label => Kind switch
	{
		AnimeSongKind.Opening => $"OP{Number}",
		AnimeSongKind.Ending => $"ED{Number}",
		_ => $"IN{Number}",
	};
}

/// <summary>A song found by the title bar's search, and the anime it's from (its title, cover and database ids only).</summary>
public sealed record SongMatch(AnimeSong Song, AnimeModel Anime);

/// <summary>
/// Plays a song's audio within the app (e.g. in the Music tab's card of it), with its spectrum for a visualization.
/// One song at a time; playing another replaces it.
/// </summary>
public interface ISongPlayer
{
	/// <summary>The song playing or paused; null when none is.</summary>
	AnimeSong? Current { get; }

	bool IsPlaying { get; }

	/// <summary>Whether the song is starting: playing, but its audio isn't heard yet (it's still coming).</summary>
	bool IsLoading { get; }

	TimeSpan Position { get; }

	/// <summary>The song's length; zero until it's known.</summary>
	TimeSpan Duration { get; }

	Task PlayAsync(AnimeSong song);

	void Pause();

	void Resume();

	void Stop();

	/// <summary>The loudness of the sound playing in each of <paramref name="bands"/>' frequency bands (low to high), from 0 to 1.</summary>
	void GetLevels(Span<float> bands);
}

/// <summary>
/// Sent (with WeakReferenceMessenger.Default, on the UI thread) when the <see cref="ISongPlayer"/> starts, pauses, stops,
/// moves on, or another song plays.
/// </summary>
public sealed record SongPlayerChangedMessage(ISongPlayer Player);

/// <summary>
/// An anime's songs, from AnimeThemes: its openings and endings (and the odd insert song), with romanised titles, who
/// sings them, the episodes they're in, and a creditless video and the audio alone of each. Looked up by the anime's
/// MyAnimeList id; kept for as long as the app runs. A song plays in the app (its audio) or in the media player (its
/// video).
/// </summary>
public sealed class AnimeSongService(IFactory<IMediaPlayer, Guid> players, ISongPlayer songPlayer, IPreferences preferences)
{
	// AnimeThemes refuses requests without one.
	private const string UserAgent = "Totoro (anime tracker)";

	private readonly ConcurrentDictionary<long, IReadOnlyList<AnimeSong>> _found = new();
	private readonly ConcurrentDictionary<AnimeSong, SongLyrics> _lyrics = new();
	private LyricReadings? _readings;

	/// <summary>The readings shown with Japanese lyrics; remembered.</summary>
	public LyricReadings Readings
	{
		get => _readings ??= preferences.LyricReadings;
		set => preferences.LyricReadings = (_readings = value).Value;
	}

	/// <summary>
	/// The song's lyrics with their times, from LRCLIB, for its audio of length <paramref name="duration"/>; none if
	/// they aren't known. Kept for as long as the app runs.
	/// </summary>
	// shortcut: when LRCLIB has only the full song, assumes the TV cut starts where it does; add a per-song offset if lyrics run off.
	public async Task<SongLyrics> GetLyricsAsync(AnimeSong song, TimeSpan duration, CancellationToken ct = default)
	{
		if (_lyrics.TryGetValue(song, out var known))
		{
			return known;
		}

		if (song.Title.Length == 0)
		{
			return SongLyrics.None;
		}

		try
		{
			var response = await "https://lrclib.net/api/search"
								 .SetQueryParam("track_name", song.Title)
								 // The first of them; none (null) leaves the parameter out.
								 .SetQueryParam("artist_name", song.Artist.Length > 0 ? song.Artist.Split(", ")[0] : null)
								 .WithHeader("User-Agent", UserAgent)
								 .GetStringAsync(cancellationToken: ct);
			var records = (JsonNode.Parse(response) as JsonArray ?? [])
				.Where(x => x?["lyricsfile"] is not null)
				.Select(x => (Seconds: x!["duration"]?.GetValue<double>() ?? 0, Lines: Lyrics.Parse(x["lyricsfile"]!.GetValue<string>())))
				.Where(x => x.Lines.Count > 0)
				.OrderByDescending(x => Lyrics.Score(x.Seconds, x.Lines, duration))
				.ToList();
			// The English ones time the song's English version, which may be sung a little apart from this one.
			return _lyrics[song] = new SongLyrics(
				records.FirstOrDefault().Lines ?? [],
				records.FirstOrDefault(x => Lyrics.IsEnglish(x.Lines)).Lines ?? []);
		}
		catch (Exception ex) when (ex is FlurlHttpException or System.Text.Json.JsonException or InvalidOperationException)
		{
			return SongLyrics.None;
		}
	}

	/// <summary>The anime's openings, then its endings, then its insert songs; none if they aren't known.</summary>
	public async Task<IReadOnlyList<AnimeSong>> GetSongsAsync(AnimeModel anime, CancellationToken ct = default)
	{
		return await anime.FindIdAsync(nameof(AnimeId.MyAnimeList), ct) is { } malId ? await GetSongsAsync(malId, ct) : [];
	}

	/// <summary>The songs of the anime with MyAnimeList id <paramref name="malId"/>, as <see cref="GetSongsAsync(AnimeModel, CancellationToken)"/>.</summary>
	public async Task<IReadOnlyList<AnimeSong>> GetSongsAsync(long malId, CancellationToken ct = default)
	{
		if (_found.TryGetValue(malId, out var known))
		{
			return known;
		}

		var songs = await GetAnimeThemesAsync(malId, ct);
		return _found[malId] = [.. songs.OrderBy(x => x.Kind).ThenBy(x => x.Number ?? 0)];
	}

	/// <summary>Plays the song's audio in the app.</summary>
	public Task PlayAudioAsync(AnimeSong song)
	{
		return song.Audio is null
			? throw new InvalidOperationException($"There's no audio of {song.Title}.")
			: songPlayer.PlayAsync(song);
	}

	/// <summary>Plays the song's creditless video in the selected media player.</summary>
	public void PlayVideo(AnimeModel anime, AnimeSong song)
	{
		if (song.Video is null)
		{
			throw new InvalidOperationException($"There's no video of {song.Title}.");
		}

		if (players.CreateDefault() is not { } player || !player.IsAvailable())
		{
			throw new InvalidOperationException("The media player isn't set up.");
		}

		songPlayer.Stop();
		player.Play(new MediaItem(song.Video, $"{anime.Title} - {song.Label}: {song.Title}", IsClip: true), TimeSpan.Zero);
	}

	/// <summary>The songs AnimeThemes has, with their first version's episodes, video and audio.</summary>
	private static async Task<IReadOnlyList<AnimeSong>> GetAnimeThemesAsync(long malId, CancellationToken ct)
	{
		// The GraphQL API, as the REST one (api.animethemes.moe) is down.
		var query = $$"""
			{ findAnimeByExternalSite(site: MAL, id: {{malId}}) { animethemes { {{ThemeFields}} } } }
			""";
		var response = await "https://graphql.animethemes.moe/"
							 .WithHeader("User-Agent", UserAgent)
							 .PostJsonAsync(new { query }, cancellationToken: ct)
							 .ReceiveString();
		if (JsonNode.Parse(response)?["data"]?["findAnimeByExternalSite"]?[0]?["animethemes"] is not JsonArray themes)
		{
			return [];
		}

		return [.. themes.Select(ToSong).OfType<AnimeSong>()];
	}

	// What a song search, and an anime's songs, ask for each song.
	private const string ThemeFields = """
		type sequence
		song { title { romaji } performances { artist { ... on Artist { name { main } } } } }
		animethemeentries { episodes videos { nodes { link audio { link } } } }
		""";

	/// <summary>
	/// Songs whose title (or singer, or anime) matches <paramref name="query"/>, from AnimeThemes, each with the anime
	/// it's from: its title, cover and database ids. None if the search fails.
	/// </summary>
	public async Task<IReadOnlyList<SongMatch>> SearchSongsAsync(string query, int count, CancellationToken ct = default)
	{
		var graphQl = $$"""
			query ($search: String!, $first: Int) { search(search: $search, first: $first) { animethemes { {{ThemeFields}}
			  anime { title { romaji } resources { nodes { site externalId } } images { nodes { facet link } } } } } }
			""";
		try
		{
			var response = await "https://graphql.animethemes.moe/"
								 .WithHeader("User-Agent", UserAgent)
								 .PostJsonAsync(new { query = graphQl, variables = new { search = query, first = count } }, cancellationToken: ct)
								 .ReceiveString();
			return ParseSongMatches(response);
		}
		catch (Exception ex) when (ex is FlurlHttpException or System.Text.Json.JsonException or InvalidOperationException)
		{
			return [];
		}
	}

	/// <summary>A song search's response as its songs, each with its anime; songs of no known kind are left out.</summary>
	public static IReadOnlyList<SongMatch> ParseSongMatches(string response)
	{
		if (JsonNode.Parse(response)?["data"]?["search"]?["animethemes"] is not JsonArray themes)
		{
			return [];
		}

		List<SongMatch> matches = [];
		foreach (var theme in themes)
		{
			if (ToSong(theme) is not { } song || theme?["anime"] is not { } anime)
			{
				continue;
			}

			var ids = new AnimeId();
			foreach (var resource in anime["resources"]?["nodes"] as JsonArray ?? [])
			{
				var id = resource?["externalId"]?.GetValue<long?>() ?? 0;
				switch (resource?["site"]?.GetValue<string>())
				{
					case "MAL": ids.MyAnimeList = id; break;
					case "ANILIST": ids.Anilist = id; break;
					case "KITSU": ids.Kitsu = id; break;
					case "ANIDB": ids.AniDb = id; break;
				}
			}

			var images = anime["images"]?["nodes"] as JsonArray ?? [];
			var cover = images.FirstOrDefault(x => x?["facet"]?.GetValue<string>() == "LARGE_COVER") ?? images.FirstOrDefault();
			matches.Add(new SongMatch(song, new AnimeModel
			{
				Title = anime["title"]?["romaji"]?.GetValue<string>() ?? "",
				ExternalIds = ids,
				Image = cover?["link"]?.GetValue<string>() ?? "",
			}));
		}

		return matches;
	}

	/// <summary>One of AnimeThemes' themes as a song, with its first version's episodes, video and audio; null if it's of no known kind.</summary>
	private static AnimeSong? ToSong(JsonNode? theme)
	{
		var kind = theme?["type"]?.GetValue<string>() switch
		{
			"OP" => AnimeSongKind.Opening,
			"ED" => AnimeSongKind.Ending,
			"IN" => AnimeSongKind.Insert,
			_ => (AnimeSongKind?)null,
		};
		if (kind is null || theme?["song"] is not { } song)
		{
			return null;
		}

		var artists = song["performances"] is JsonArray list ? string.Join(", ", list.Select(x => x?["artist"]?["name"]?["main"]?.GetValue<string>()).OfType<string>()) : "";
		var entry = First(theme["animethemeentries"]);
		var video = First(entry?["videos"]?["nodes"]);
		return new AnimeSong(
			kind.Value,
			theme["sequence"]?.GetValue<int?>(),
			song["title"]?["romaji"]?.GetValue<string>() ?? "",
			artists,
			entry?["episodes"]?.GetValue<string>()?.Replace("-", "–") ?? "",
			ToUri(video?["link"]),
			ToUri(video?["audio"]?["link"]));
	}

	// A JSON array's [0] throws when it's empty (a song with no video yet), rather than giving null.
	private static JsonNode? First(JsonNode? list) => list is JsonArray { Count: > 0 } array ? array[0] : null;

	private static Uri? ToUri(JsonNode? link) => Uri.TryCreate(link?.GetValue<string>(), UriKind.Absolute, out var uri) ? uri : null;
}

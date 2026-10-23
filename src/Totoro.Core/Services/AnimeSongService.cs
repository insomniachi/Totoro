using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;

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
public sealed class AnimeSongService(IFactory<IMediaPlayer, Guid> players, ISongPlayer songPlayer)
{
	// AnimeThemes refuses requests without one.
	private const string UserAgent = "Totoro (anime tracker)";

	private readonly ConcurrentDictionary<long, IReadOnlyList<AnimeSong>> _found = new();

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
		var response = await "https://api.animethemes.moe/anime"
							 .SetQueryParam("filter[has]", "resources")
							 .SetQueryParam("filter[site]", "MyAnimeList")
							 .SetQueryParam("filter[external_id]", malId)
							 .SetQueryParam("include", "animethemes.song.artists,animethemes.animethemeentries.videos.audio")
							 .WithHeader("User-Agent", UserAgent)
							 .GetStringAsync(cancellationToken: ct);
		if (JsonNode.Parse(response)?["anime"]?[0]?["animethemes"] is not JsonArray themes)
		{
			return [];
		}

		List<AnimeSong> songs = [];
		foreach (var theme in themes)
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
				continue;
			}

			var artists = song["artists"] is JsonArray list ? string.Join(", ", list.Select(x => x?["name"]?.GetValue<string>()).OfType<string>()) : "";
			var entry = theme["animethemeentries"]?[0];
			var video = entry?["videos"]?[0];
			songs.Add(new AnimeSong(
				kind.Value,
				theme["sequence"]?.GetValue<int?>(),
				song["title"]?.GetValue<string>() ?? "",
				artists,
				entry?["episodes"]?.GetValue<string>()?.Replace("-", "–") ?? "",
				ToUri(video?["link"]),
				ToUri(video?["audio"]?["link"])));
		}

		return songs;
	}

	private static Uri? ToUri(JsonNode? link) => Uri.TryCreate(link?.GetValue<string>(), UriKind.Absolute, out var uri) ? uri : null;
}

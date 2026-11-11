using Totoro.Core.Modules.Anime;

namespace Totoro.Playback;

/// <summary>The status bar's text, from what's playing.</summary>
internal static class PlaybackStatusText
{
	/// <summary>Metadata tags, e.g. "TV · Fall 2023 · ★ 8.8 · Madhouse".</summary>
	public static string Metadata(AnimeModel anime)
	{
		List<string> parts = [];
		if (anime.MediaFormat != AnimeMediaFormat.Unknown)
		{
			parts.Add(anime.MediaFormat.ToString().ToUpperInvariant());
		}

		if (anime.Season is { } season)
		{
			parts.Add($"{season.SeasonName} {season.Year}");
		}

		if (anime.MeanScore is > 0 and var score)
		{
			parts.Add($"★ {score / 10f:0.#}");
		}

		if (anime.Studios.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) is { } studio)
		{
			parts.Add(studio);
		}

		return string.Join(" · ", parts);
	}

	/// <summary>Source and player tags, e.g. "AnimePahe · VLC".</summary>
	public static string Source(string? providerName, string? playerName)
	{
		List<string> parts = [];
		if (!string.IsNullOrWhiteSpace(providerName))
		{
			parts.Add(providerName);
		}

		if (!string.IsNullOrWhiteSpace(playerName))
		{
			parts.Add(playerName);
		}

		return string.Join(" · ", parts);
	}

	/// <summary>m:ss, or h:mm:ss for something an hour or longer (a movie).</summary>
	public static string Time(TimeSpan time, TimeSpan duration)
	{
		return time.ToString(duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
	}
}

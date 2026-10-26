using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Totoro.Core.Modules.Streaming;

/// <summary>
/// How downloaded episodes are named, for a provider of local files to read back: a folder for each anime in the download
/// folder, named with its title, e.g. <c>Sousou no Frieren</c>, and in it each episode as <c>Sousou no Frieren - 05.mkv</c>.
/// The anime's ids are in its <see cref="InfoFile"/>.
/// </summary>
public static partial class DownloadNames
{
	/// <summary>The anime's <see cref="DownloadInfo"/>, as JSON, in its folder.</summary>
	public const string InfoFile = "info.json";

	/// <summary>The anime's cover in its folder, without the extension (the image's own).</summary>
	public const string CoverFile = "cover";

	/// <summary>The anime's folder name.</summary>
	public static string Folder(string title) => Clean(title);

	/// <summary>An episode's file name, without the extension.</summary>
	public static string Episode(string title, int number) => $"{Clean(title)} - {number:00}";

	/// <summary>The episode number in a file name made by <see cref="Episode"/>, or null.</summary>
	public static int? ParseEpisode(string fileName) =>
		EpisodePattern().Match(Path.GetFileNameWithoutExtension(fileName)) is { Success: true } match
			? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
			: null;

	/// <summary>
	/// The name of the episode's <paramref name="index"/>th subtitle file, beside the episode <paramref name="episode"/> (its
	/// file name without the extension), without its own extension: e.g. <c>Sousou no Frieren - 05.01.English.en</c>, whose
	/// last part players read as the language.
	/// </summary>
	public static string Subtitle(string episode, int index, SubtitleTrack track) =>
		track.Language is { Length: > 0 } language
			? $"{episode}.{index:00}.{Clean(track.Name)}.{Clean(language)}"
			: $"{episode}.{index:00}.{Clean(track.Name)}";

	/// <summary>The subtitle files <see cref="Subtitle"/> named beside the episode file <paramref name="episodeFile"/>, in order.</summary>
	public static List<SubtitleTrack> FindSubtitles(string episodeFile)
	{
		var folder = Path.GetDirectoryName(episodeFile);
		var prefix = Path.GetFileNameWithoutExtension(episodeFile) + ".";
		if (folder is null || !Directory.Exists(folder))
		{
			return [];
		}

		return
		[
			.. Directory.EnumerateFiles(folder, prefix + "*")
						.Where(x => s_subtitleExtensions.Contains(Path.GetExtension(x)))
						// index.name[.language]
						.Select(x => (File: x, Parts: Path.GetFileNameWithoutExtension(x)[prefix.Length..].Split('.')))
						.Where(x => x.Parts.Length > 1 && int.TryParse(x.Parts[0], CultureInfo.InvariantCulture, out _))
						.OrderBy(x => int.Parse(x.Parts[0], CultureInfo.InvariantCulture))
						.Select(x => x.Parts.Length > 2
							? new SubtitleTrack(string.Join('.', x.Parts[1..^1]), new Uri(x.File), x.Parts[^1])
							: new SubtitleTrack(x.Parts[1], new Uri(x.File)))
		];
	}

	private static readonly HashSet<string> s_subtitleExtensions = new([".vtt", ".srt", ".ass", ".ssa"], StringComparer.OrdinalIgnoreCase);

	/// <summary>What the anime's folder's info file says; null if it has none, or one without a title. Throws <see cref="JsonException"/> for a broken one.</summary>
	public static DownloadInfo? ReadInfo(string folder)
	{
		var path = Path.Combine(folder, InfoFile);
		return File.Exists(path) && JsonSerializer.Deserialize<DownloadInfo>(File.ReadAllText(path)) is { Title.Length: > 0 } info
			? info with { OtherTitles = info.OtherTitles ?? [] }
			: null;
	}

	/// <summary>Writes the anime's info file in its folder, replacing any.</summary>
	public static void WriteInfo(string folder, DownloadInfo info) => File.WriteAllText(Path.Combine(folder, InfoFile), JsonSerializer.Serialize(info));

	/// <summary>Without characters Windows doesn't allow in names.</summary>
	private static string Clean(string title)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var clean = new string([.. title.Select(x => invalid.Contains(x) ? ' ' : x)]);
		// Windows trims trailing dots and spaces from names.
		return Spaces().Replace(clean, " ").Trim().TrimEnd('.');
	}

	[GeneratedRegex(@" - (\d+)$")]
	private static partial Regex EpisodePattern();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}

/// <summary>What a downloaded anime's folder says about it, for finding it by any of its titles or its ids.</summary>
/// <param name="OtherTitles">Its romaji and English titles and synonyms, besides <paramref name="Title"/>.</param>
/// <param name="AnilistId">Its AniList id, when known.</param>
/// <param name="MalId">Its MyAnimeList id, when known.</param>
/// <param name="Segments">Each downloaded episode's opening and ending, by episode number, where they were found.</param>
public sealed record DownloadInfo(string Title, IReadOnlyList<string> OtherTitles, long? AnilistId = null, long? MalId = null,
								  IReadOnlyDictionary<int, IReadOnlyList<Media.MediaSegment>>? Segments = null);

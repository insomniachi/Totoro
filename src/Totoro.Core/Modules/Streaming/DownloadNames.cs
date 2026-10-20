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
public sealed record DownloadInfo(string Title, IReadOnlyList<string> OtherTitles, long? AnilistId = null, long? MalId = null);

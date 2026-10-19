using System.Globalization;
using System.Text.RegularExpressions;

namespace Totoro.Core.Modules.Streaming;

/// <summary>
/// How downloaded episodes are named, for a provider of local files to read back: a folder for each anime in the download
/// folder, named with its ids, e.g. <c>Sousou no Frieren [anilist-154587] [mal-52991]</c>, and in it each episode as
/// <c>Sousou no Frieren - 05.mkv</c>.
/// </summary>
public static partial class DownloadNames
{
	/// <summary>The anime's folder name: its title, then whichever of its ids are known.</summary>
	public static string Folder(string title, long? anilistId, long? malId) =>
		Clean(title) + (anilistId is > 0 ? $" [anilist-{anilistId}]" : "") + (malId is > 0 ? $" [mal-{malId}]" : "");

	/// <summary>An episode's file name, without the extension.</summary>
	public static string Episode(string title, int number) => $"{Clean(title)} - {number:00}";

	/// <summary>The ids in a folder name made by <see cref="Folder"/>; null where it has none.</summary>
	public static (long? AnilistId, long? MalId) ParseIds(string folderName) =>
		(Id(AnilistPattern().Match(folderName)), Id(MalPattern().Match(folderName)));

	/// <summary>The episode number in a file name made by <see cref="Episode"/>, or null.</summary>
	public static int? ParseEpisode(string fileName) =>
		EpisodePattern().Match(Path.GetFileNameWithoutExtension(fileName)) is { Success: true } match
			? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
			: null;

	private static long? Id(Match match) => match.Success ? long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;

	/// <summary>Without characters Windows doesn't allow in names, nor brackets (which hold the ids).</summary>
	private static string Clean(string title)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var clean = new string([.. title.Select(x => invalid.Contains(x) || x is '[' or ']' ? ' ' : x)]);
		// Windows trims trailing dots and spaces from names.
		return Spaces().Replace(clean, " ").Trim().TrimEnd('.');
	}

	[GeneratedRegex(@"\[anilist-(\d+)\]")]
	private static partial Regex AnilistPattern();

	[GeneratedRegex(@"\[mal-(\d+)\]")]
	private static partial Regex MalPattern();

	[GeneratedRegex(@" - (\d+)$")]
	private static partial Regex EpisodePattern();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}

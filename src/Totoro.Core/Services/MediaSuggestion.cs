using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Navigation;

namespace Totoro.Core.Services;

/// <summary>An anime or manga offered by the title bar's search: its cover, title, facts, and where it is on the user's list.</summary>
public sealed class MediaSuggestion(IMediaModel media)
{
	public IMediaModel Media { get; } = media;

	/// <summary>Its title, and its English one if that's different, for the tooltip.</summary>
	public string FullTitle => string.IsNullOrWhiteSpace(Media.EngTitle) || Media.EngTitle == Media.Title
		? Media.Title
		: $"{Media.Title}\n{Media.EngTitle}";

	/// <summary>What it is, then the anime's or manga's facts, e.g. "Anime · ★ 9.1 · TV · Fall 2023" or "Manga · ★ 8.2 · Manhwa · 140 chapters".</summary>
	public string Facts => Media switch
	{
		AnimeModel anime => Join("Anime", new ChatAnimeItem(anime).Facts),
		MangaModel manga => Join("Manga",
			manga.MeanScore is > 0 and var score ? $"★ {Math.Round(score / 10, 1):0.0}" : null,
			// "Manga · Manga" says nothing; "Manga · Manhwa" does.
			manga.Format is MangaFormat.Unknown or MangaFormat.Manga ? null : FormatName(manga.Format),
			manga.TotalChapters is > 0 and var chapters ? $"{chapters} chapters" : null),
		_ => "",
	};

	/// <summary>The user's list entry, e.g. "Watching, 4 of 13" or "Reading, 12 of ?"; "" when it isn't on their list.</summary>
	public string ListStatus => Media.ListStatus is null ? ""
		: Media.Progress is > 0 and var done ? $"{MediaText.Status(Media.ListStatus, Media.Kind)}, {done} of {Media.Total?.ToString() ?? "?"}"
		: MediaText.Status(Media.ListStatus, Media.Kind);

	private static string FormatName(MangaFormat format) =>
		typeof(MangaFormat).GetField(format.ToString())?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
			.OfType<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? format.ToString();

	private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(x => !string.IsNullOrEmpty(x)));
}

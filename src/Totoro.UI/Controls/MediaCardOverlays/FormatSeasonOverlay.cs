using Microsoft.UI.Xaml;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;

namespace Totoro.UI.Controls.MediaCardOverlays;

/// <summary>
/// An anime's format and when it aired (e.g. "TV · Fall 2024"), or a manga's and the year it began ("Manhwa · 2018"), on
/// its card's bottom left; none when neither is known.
/// </summary>
public sealed class FormatSeasonOverlay : LiveMediaCardOverlay<LabelBadge>
{
	internal static bool IsShown(IMediaModel item) => Text(item).Length > 0;

	internal static string Text(IMediaModel? item)
	{
		string?[] parts = item switch
		{
			AnimeModel anime => [MediaText.Format(anime.MediaFormat), anime.Season is { Year: > 1900 } season ? $"{season.SeasonName} {season.Year}" : null],
			// The manga's own StartDate (publication), not IMediaModel's (the user's tracking).
			MangaModel manga => [MediaText.Format(manga.Format), manga.StartDate is { Year: > 1900 } start ? $"{start.Year}" : null],
			_ => [],
		};
		return string.Join(" · ", parts.OfType<string>());
	}

	protected override LabelBadge Create() => new(HorizontalAlignment.Left, VerticalAlignment.Bottom);

	protected override void Update(LabelBadge badge, IMediaModel? item, DateTime now)
	{
		var text = Text(item);
		badge.Set("", text, item is MangaModel ? $"Published: {text}" : $"Aired: {text}");
	}
}

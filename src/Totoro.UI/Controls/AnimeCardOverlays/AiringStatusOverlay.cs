using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// Whether an anime is airing or yet to air (a manga publishing or yet to be), as a coloured dot on its card's bottom left.
/// Finished ones, most of them, get none, nor do those being watched or read: an airing anime being watched has its next
/// episode countdown (NextEpisodeOverlay), which says as much.
/// </summary>
public sealed class AiringStatusOverlay : LiveAnimeCardOverlay<AiringStatusBadge>
{
	internal static bool IsShown(IMediaModel item) =>
		Status(item) is AiringStatus.CurrentlyAiring or AiringStatus.NotYetAired && item.ListStatus is not ListItemStatus.Watching;

	// Manga reuse the anime status (Currently Airing is "Publishing").
	private static AiringStatus? Status(IMediaModel? item) => item switch
	{
		AnimeModel anime => anime.AiringStatus,
		MangaModel manga => manga.Status,
		_ => null,
	};

	protected override AiringStatusBadge Create() => new();

	protected override void Update(AiringStatusBadge badge, IMediaModel? item, DateTime now)
	{
		if (Status(item) is { } status)
		{
			badge.Set(status, item!.Kind);
		}
	}
}

using Microsoft.UI.Xaml;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>An anime's format and when it aired (e.g. "TV · Fall 2024"), on its card's bottom left; none when neither is known.</summary>
public sealed class FormatSeasonOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	protected override LabelBadge Create() => new(HorizontalAlignment.Left, VerticalAlignment.Bottom);

	protected override void Update(LabelBadge badge, AnimeModel? anime, DateTime now)
	{
		string?[] parts =
		[
			anime is null ? null : MediaText.Format(anime.MediaFormat),
			anime?.Season is { Year: > 1900 } season ? $"{season.SeasonName} {season.Year}" : null,
		];
		var text = string.Join(" · ", parts.OfType<string>());
		badge.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
		if (text.Length > 0)
		{
			badge.Set("", text, $"Aired: {text}");
		}
	}
}

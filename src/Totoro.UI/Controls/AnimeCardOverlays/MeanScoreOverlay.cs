using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>An anime's mean score out of 10 (e.g. "★ 8.4"), on its card's top right; none while it has no score.</summary>
public sealed class MeanScoreOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	protected override LabelBadge Create() => new(HorizontalAlignment.Right, VerticalAlignment.Top);

	protected override void Update(LabelBadge badge, AnimeModel? anime, DateTime now)
	{
		var shown = anime?.MeanScore is > 0;
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			// MeanScore is out of 100.
			var score = $"{anime!.MeanScore / 10:0.0}";
			badge.Set("", score, $"Mean score {score} out of 10");
		}
	}
}

using Microsoft.UI.Xaml;
using Totoro.Core.Modules;

namespace Totoro.UI.Controls.MediaCardOverlays;

/// <summary>An anime's or manga's mean score out of 10 (e.g. "★ 8.4"), on its card's top right; none while it has no score.</summary>
public sealed class MeanScoreOverlay : LiveMediaCardOverlay<LabelBadge>
{
	internal static bool IsShown(IMediaModel item) => item.MeanScore is > 0;

	protected override LabelBadge Create() => new(HorizontalAlignment.Right, VerticalAlignment.Top);

	protected override void Update(LabelBadge badge, IMediaModel? item, DateTime now)
	{
		if (item?.MeanScore is { } meanScore)
		{
			// MeanScore is out of 100.
			var score = $"{meanScore / 10:0.0}";
			badge.Set("", score, $"Mean score {score} out of 10");
		}
	}
}

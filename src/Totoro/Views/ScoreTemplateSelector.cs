using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>
/// Picks the score control for the tracker's <see cref="ScoreStyle"/>. Only the picked one is created: hidden ones would stay
/// bound and write their own clamped values over the score.
/// </summary>
public sealed partial class ScoreTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Number { get; set; }

	public DataTemplate? Stars { get; set; }

	public DataTemplate? Smileys { get; set; }

	protected override DataTemplate? SelectTemplateCore(object item) => (item as TrackingDialogViewModel)?.Form.ScoreStyle switch
	{
		ScoreStyle.Stars => Stars,
		ScoreStyle.Smileys => Smileys,
		_ => Number,
	};

	protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

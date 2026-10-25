using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;

namespace Totoro.Views;

/// <summary>Picks the control for a tracker's own <see cref="TrackingField"/> by its kind.</summary>
public sealed partial class TrackingFieldTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Text { get; set; }

	public DataTemplate? Number { get; set; }

	public DataTemplate? Toggle { get; set; }

	public DataTemplate? Choice { get; set; }

	protected override DataTemplate? SelectTemplateCore(object item) => item switch
	{
		TextField => Text,
		NumberField => Number,
		ToggleField => Toggle,
		ChoiceField => Choice,
		_ => null,
	};

	protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

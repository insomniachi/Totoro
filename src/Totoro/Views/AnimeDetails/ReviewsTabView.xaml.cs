using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Reviews tab.</summary>
public sealed partial class ReviewsTabView : UserControl
{
	public ReviewsTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ReviewsTab? ViewModel { get; set; }
}

/// <summary>A review that has more than its summary expands to it; one that hasn't (e.g. a Kitsu reaction) is a plain card.</summary>
public sealed partial class ReviewTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Expandable { get; set; }

	public DataTemplate? Plain { get; set; }

	protected override DataTemplate? SelectTemplateCore(object item) => item is ReviewItem { IsExpandable: false } ? Plain : Expandable;
}

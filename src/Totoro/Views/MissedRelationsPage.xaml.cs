using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class MissedRelationsPage : Page
{
	public MissedRelationsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MissedRelationsViewModel? ViewModel { get; set; }

	/// <summary>e.g. "Sequel · TV · Fall 2023 · 12 episodes · ★ 7.8"; what's unknown is left out.</summary>
	public static string Details(AnimeRelationType? relation, AnimeMediaFormat format, Season? season, int? episodes, float? score) =>
		string.Join(" · ", new[]
		{
			relation switch { AnimeRelationType.SideStory => "Side story", AnimeRelationType.SpinOff => "Spin-off", { } x => x.ToString(), null => null },
			MediaText.Format(format),
			season is { Year: > 1900 } ? $"{season.SeasonName} {season.Year}" : null,
			episodes is > 0 ? $"{episodes} episode{(episodes == 1 ? "" : "s")}" : null,
			// Out of 100; shown out of 10, as the cards' score.
			score is > 0 ? $"★ {score / 10:0.0}" : null,
		}.OfType<string>());

	private void OnRelationsChanged(object sender, SelectionChangedEventArgs e) => Toggle(e, ViewModel?.HiddenRelations);

	private void OnFormatsChanged(object sender, SelectionChangedEventArgs e) => Toggle(e, ViewModel?.HiddenFormats);

	// A pill switched on shows its kinds again; one switched off hides them. Before the view model is set (the pills
	// selected at first), there's nothing to change: it starts with the same ones hidden.
	private void Toggle<T>(SelectionChangedEventArgs e, HashSet<T>? hidden) where T : struct, Enum
	{
		if (hidden is null)
		{
			return;
		}

		static IEnumerable<T> Values(IEnumerable<object> pills) =>
			pills.OfType<FrameworkElement>().SelectMany(x => ((string)x.Tag).Split(',')).Select(Enum.Parse<T>);
		hidden.ExceptWith(Values(e.AddedItems));
		hidden.UnionWith(Values(e.RemovedItems));
		ViewModel!.Refresh();
	}
}

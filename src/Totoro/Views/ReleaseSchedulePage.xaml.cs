using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Services;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class ReleaseSchedulePage : Page
{
	public ReleaseSchedulePage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ReleaseScheduleViewModel? ViewModel { get; set; }

	private static Brush Resource(string key) => (Brush)Application.Current.Resources[key];

	// Days of the weeks around the month are faded.
	public static double DayOpacity(bool isInMonth) => isInMonth ? 1 : 0.4;

	public static Brush DayBorder(bool isToday) => Resource(isToday ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush");

	public static Brush TodayBackground(bool isToday) => isToday ? Resource("AccentFillColorDefaultBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

	public static Brush TodayForeground(bool isToday) => Resource(isToday ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorPrimaryBrush");

	public static Visibility VisibleIfAny(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

	public static double WatchedOpacity(bool isWatched) => isWatched ? 0.6 : 1;

	public static string MarkerGlyph(ReleaseMarker marker, bool isWatched) => marker switch
	{
		ReleaseMarker.Premiere => "",
		ReleaseMarker.Finale => "",
		_ when isWatched => "",
		_ => "",
	};

	public static string EpisodeLabel(int first, int last) => first == last ? $"Ep. {first}" : $"Ep. {first}–{last}";

	public static string Describe(string title, int first, int last, DateTime airingAt, ReleaseMarker marker)
	{
		var number = first == last ? $"episode {first}" : $"episodes {first} to {last}";
		var kind = marker switch
		{
			ReleaseMarker.Premiere => ", season premiere",
			ReleaseMarker.Finale => ", season finale",
			_ => "",
		};
		return $"{title}, {number}{kind}, {airingAt:t}";
	}
}

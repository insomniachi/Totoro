using System.Numerics;
using CommunityToolkit.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DiscoverPage : Page
{
	private readonly DispatcherTimer _spotlightTimer = new() { Interval = TimeSpan.FromSeconds(8) };

	public DiscoverPage()
	{
		InitializeComponent();
		_spotlightTimer.Tick += (_, _) =>
		{
			if (Spotlight.Items.Count > 1)
			{
				Spotlight.SelectedIndex = (Spotlight.SelectedIndex + 1) % Spotlight.Items.Count;
			}
		};
		Loaded += (_, _) => _spotlightTimer.Start();
		Unloaded += (_, _) => _spotlightTimer.Stop();
		// Hold the spotlight still while it's being looked at.
		Spotlight.PointerEntered += (_, _) => _spotlightTimer.Stop();
		Spotlight.PointerExited += (_, _) => _spotlightTimer.Start();
	}

	[GeneratedDependencyProperty]
	public partial DiscoverViewModel? ViewModel { get; set; }

	// A card and the gap after it, as in the XAML's CardTemplate (Width) and rows' StackLayout (Spacing).
	private const double CardStride = 190 + 16;

	// A row's buttons scroll it (their Tag) by the cards that fully fit, so a card cut off at the edge starts the next set.
	private void OnPreviousPage(object sender, RoutedEventArgs e) => ScrollRow(sender, -1);

	private void OnNextPage(object sender, RoutedEventArgs e) => ScrollRow(sender, 1);

	private static void ScrollRow(object button, int direction)
	{
		var row = (ScrollView)((Button)button).Tag;
		var cards = Math.Max(1, Math.Floor((row.ViewportWidth + 16) / CardStride));
		// Clamped: past either end, the animation runs to the unclamped spot and snaps back.
		row.ScrollTo(Math.Clamp(row.HorizontalOffset + direction * cards * CardStride, 0, row.ScrollableWidth), 0);
	}

	// ScrollView's own animation takes a second and slides the whole distance: instead the row jumps most of the way
	// and slides just the last part in, quickly. Its animation is changed rather than replaced, which the ScrollView ignores.
	private const float SlideFraction = 0.15f;

	private void OnRowScrollAnimationStarting(ScrollView sender, ScrollingScrollAnimationStartingEventArgs args)
	{
		if (args.Animation is not Vector3KeyFrameAnimation animation)
		{
			return;
		}

		var end = new Vector3(args.EndPosition, 0);
		var start = new Vector3(args.StartPosition, 0);
		var easing = animation.Compositor.CreateCubicBezierEasingFunction(new(0.1f, 0.9f), new(0.2f, 1f));
		animation.InsertKeyFrame(0, end - (end - start) * SlideFraction);
		animation.InsertKeyFrame(1, end, easing);
		animation.Duration = TimeSpan.FromMilliseconds(300);
	}

	/// <summary>A row's buttons are enabled only while there are cards that way: it scrolled, resized, or its cards changed.</summary>
	private void OnRowChanged(object sender, object e)
	{
		(ScrollView Row, Button Previous, Button Next)[] rows =
		[
			(TrendingRow, TrendingPrevious, TrendingNext),
			(AiringTodayRow, AiringTodayPrevious, AiringTodayNext),
			(NextSeasonRow, NextSeasonPrevious, NextSeasonNext),
		];
		var (row, previous, next) = rows.First(x => x.Row == sender);

		// A pixel of slack: offsets are fractional at some scales.
		previous.IsEnabled = row.HorizontalOffset > 1;
		next.IsEnabled = row.HorizontalOffset < row.ScrollableWidth - 1;
	}

	/// <summary>The spotlight's list button: it adds an anime that isn't on the list, and edits one that is.</summary>
	public static string TrackingButtonText(Tracking? tracking) => tracking?.Status is null ? "Add to list" : "Edit tracking";

	/// <summary>E.g. "Action, Fantasy · ★ 8.4 · 24 episodes · Fall 2026", leaving out what isn't known.</summary>
	public static string Details(IReadOnlyCollection<string>? genres, float? meanScore, int? totalEpisodes, Season? season)
	{
		string?[] parts =
		[
			genres is { Count: > 0 } ? string.Join(", ", genres.Take(3)) : null,
			meanScore is > 0 ? $"★ {meanScore / 10:0.0}" : null,
			totalEpisodes is > 0 ? $"{totalEpisodes} episodes" : null,
			season is null ? null : $"{season.SeasonName} {season.Year}",
		];
		return string.Join(" · ", parts.OfType<string>());
	}
}

using System.Globalization;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Services;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>The anime Search page's Image tab: the image pasted, its matches, and the one picked (see <see cref="ImageSearchViewModel"/>).</summary>
public sealed partial class ImageSearchView : UserControl
{
	public ImageSearchView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ImageSearchViewModel? ViewModel { get; set; }

	// For the view's x:Bind function bindings.

	public static Visibility VisibleIfAny(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

	/// <summary>Where in the anime the image is, e.g. "Episode 1 at 14:17 · 97% alike".</summary>
	public static string Where(int? episode, TimeSpan at, double similarity)
	{
		var time = at.ToString(at.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
		return $"{(episode is { } number ? $"Episode {number} at" : "At")} {time} · {similarity:P0} alike";
	}

	/// <summary>Under the scene: where it is, and the release it was found in.</summary>
	public static string Scene(ImageMatch? match) =>
		match is null ? "" : string.Join(" · ", new[] { Where(match.Episode, match.At, match.Similarity), match.FileName }.Where(x => x.Length > 0));

	/// <summary>What the anime is, e.g. "TV · Spring 2016 · 13 episodes · ★ 7.8 · Comedy, Drama".</summary>
	public static string Info(ImageMatch? match) => match?.Anime is not { } anime ? ""
		: string.Join(" · ", new[]
		{
			MissedRelationsPage.Details(null, anime.MediaFormat, anime.Season, anime.TotalEpisodes, anime.MeanScore),
			string.Join(", ", anime.Genres),
		}.Where(x => x.Length > 0));

	/// <summary>The scene's play button, e.g. "Play episode 1 from 14:15": a couple of seconds before the frame.</summary>
	public static string PlaySceneLabel(ImageMatch? match)
	{
		if (match is null)
		{
			return "";
		}

		var time = match.PlayFrom.ToString(match.PlayFrom.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture);
		return match.Episode is { } episode ? $"Play episode {episode} from {time}" : $"Play from {time}";
	}
}

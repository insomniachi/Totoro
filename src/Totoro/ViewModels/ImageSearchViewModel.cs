using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Totoro.Core.Services;
using Totoro.Helpers;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>The anime Search page's Image tab: the anime a screenshot pasted from the clipboard is from.</summary>
public sealed partial class ImageSearchViewModel(ImageSearchService search, WatchService watch, ILogger<ImageSearchViewModel> logger) : NavigatableViewModel
{
	/// <summary>Plays the match's episode from just before its scene (the first, for a movie).</summary>
	[RelayCommand]
	private Task PlaySceneAsync(ImageMatch? match) =>
		match is null ? Task.CompletedTask : watch.WatchAsync(match.Anime, match.Episode ?? 1, startAt: match.PlayFrom);

	[ObservableProperty]
	public partial IReadOnlyList<ImageMatch> Matches { get; private set; } = [];

	/// <summary>The match shown in detail: the best one, until another is picked.</summary>
	[ObservableProperty]
	public partial ImageMatch? Selected { get; set; }

	/// <summary>The selected match's scene, playing over and over; null while it loads, or if it can't.</summary>
	[ObservableProperty]
	public partial IMediaPlaybackSource? Clip { get; private set; }

	partial void OnSelectedChanged(ImageMatch? value) => _ = LoadClipAsync(value);

	private async Task LoadClipAsync(ImageMatch? match)
	{
		Clip = null;
		if (match is not { Video.Length: > 0 })
		{
			return;
		}

		try
		{
			var clip = await search.GetClipAsync(match);
			if (ReferenceEquals(match, Selected))
			{
				var source = MediaSource.CreateFromStream(new MemoryStream(clip).AsRandomAccessStream(), "video/mp4");
				Clip = new MediaPlaybackList { AutoRepeatEnabled = true, Items = { new MediaPlaybackItem(source) } };
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load the scene of {Anime}", match.Anime.Title);
		}
	}

	/// <summary>The image searched with, as pasted; null before one is.</summary>
	[ObservableProperty]
	public partial ImageSource? Pasted { get; private set; }

	/// <summary>What to do, or why there are no matches; empty while there are.</summary>
	[ObservableProperty]
	public partial string Message { get; private set; } = "Copy a screenshot of an anime, then press Ctrl+V here.";

	[RelayCommand]
	private async Task PasteAsync()
	{
		if (IsBusy)
		{
			return;
		}

		using (BeginBusy())
		{
			try
			{
				if (await ClipboardImage.OpenAsync() is not var (image, contentType))
				{
					Matches = [];
					Pasted = null;
					Message = "There's no image in the clipboard. Copy a screenshot, then press Ctrl+V.";
					return;
				}

				Message = "";
				Matches = [];
				using (image)
				{
					var preview = new BitmapImage();
					await preview.SetSourceAsync(image.AsRandomAccessStream());
					image.Position = 0;
					Pasted = preview;
					Matches = await search.FindAsync(image, contentType);
					// After the list has them: picked before, the list would reject it and clear it.
					Selected = Matches.FirstOrDefault();
				}

				Message = Matches.Count == 0 ? "No anime matched this image. Try a full, uncropped frame of the episode." : "";
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Image search failed");
				Matches = [];
				Message = ex.Message;
			}
		}
	}
}

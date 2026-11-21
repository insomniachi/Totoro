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
public sealed partial class ImageSearchViewModel(ImageSearchService search, CharacterSearchService characters, WatchService watch, ILogger<ImageSearchViewModel> logger) : NavigatableViewModel
{
	/// <summary>Plays the match's episode from just before its scene (the first, for a movie).</summary>
	[RelayCommand]
	private Task PlaySceneAsync(ImageMatch? match) =>
		match is null ? Task.CompletedTask : watch.WatchAsync(match.Anime, match.Episode ?? 1, startAt: match.PlayFrom);

	[ObservableProperty]
	public partial IReadOnlyList<ImageMatch> Matches { get; private set; } = [];

	/// <summary>The characters in the image and what they're from, left to right.</summary>
	[ObservableProperty]
	public partial IReadOnlyList<CharacterMatch> Characters { get; private set; } = [];

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
	public partial string Message { get; private set; } = "Copy a screenshot or a picture of anime characters, then press Ctrl+V here.";

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
					Characters = [];
					Pasted = null;
					Message = "There's no image in the clipboard. Copy a screenshot, then press Ctrl+V.";
					return;
				}

				Message = "";
				Matches = [];
				Characters = [];
				byte[] bytes;
				using (image)
				{
					bytes = new byte[image.Length];
					await image.ReadExactlyAsync(bytes);
				}

				var preview = new BitmapImage();
				await preview.SetSourceAsync(new MemoryStream(bytes).AsRandomAccessStream());
				Pasted = preview;

				// The scene (trace.moe) and the characters (AnimeTrace) at once, each from its own copy: one failing
				// doesn't hide what the other found.
				var scenes = search.FindAsync(new MemoryStream(bytes), contentType);
				var people = characters.FindAsync(new MemoryStream(bytes), contentType);
				try
				{
					await Task.WhenAll(scenes, people);
				}
				catch (Exception)
				{
					// Each is looked at below.
				}

				Matches = scenes.IsCompletedSuccessfully ? scenes.Result : [];
				// After the list has them: picked before, the list would reject it and clear it.
				Selected = Matches.FirstOrDefault();
				Characters = people.IsCompletedSuccessfully ? people.Result : [];
				foreach (var failed in new Task[] { scenes, people }.Where(x => x.IsFaulted))
				{
					logger.LogWarning(failed.Exception, "Image search failed");
				}

				Message = Matches.Count > 0 || Characters.Count > 0 ? ""
					: (scenes.Exception ?? people.Exception)?.InnerException?.Message
					  ?? "Nothing matched this image. For the scene, try a full, uncropped frame of the episode.";
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Image search failed");
				Matches = [];
				Characters = [];
				Message = ex.Message;
			}
		}
	}
}

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;

namespace Totoro.Playback;

/// <summary>
/// Windows notifications for downloads the user would want to hear about (a range they asked for, a new episode downloaded
/// unasked), shown whether or not the window is: once all of them are done, with buttons to open their folder or play them.
/// Also a new episode that's out on its provider but wasn't downloaded, with a button to stream it.
/// </summary>
public sealed class DownloadNotifier(WatchService watch, Totoro.Core.Services.ReleaseScheduleService schedule, ILogger<DownloadNotifier> logger)
{
	// The anime notified of this session, by id, for the Play button; a notification from an earlier session only opens the app.
	private readonly Dictionary<long, AnimeModel> _anime = [];

	/// <summary>
	/// Notifies once <paramref name="items"/> (episodes <paramref name="from"/> on, as <see cref="DownloadService.DownloadEpisodesAsync"/>
	/// returns them) are done, if any of them downloaded.
	/// </summary>
	public async Task NotifyWhenDoneAsync(AnimeModel anime, int from, IReadOnlyList<DownloadItem> items)
	{
		await Task.WhenAll(items.Select(WhenDoneAsync));
		var done = items.Select((x, i) => (Item: x, Number: from + i)).Where(x => x.Item.State == DownloadState.Completed).ToList();
		if (done.Count == 0)
		{
			return;
		}

		var first = done[0].Number;
		var last = done[^1].Number;
		var text = first == last ? $"Episode {first} downloaded" : $"Episodes {first}–{last} downloaded";
		if (done.Count < items.Count)
		{
			text += $" ({items.Count - done.Count} couldn't be)";
		}

		_anime[anime.Id] = anime;
		var folder = done[0].Item.Folder;
		try
		{
			var builder = new AppNotificationBuilder();
			// The cover and banner the download saved with the episodes (notifications only show images from this PC).
			if (Directory.Exists(folder))
			{
				if (Directory.EnumerateFiles(folder, DownloadNames.CoverFile + ".*").FirstOrDefault() is { } cover)
				{
					builder.SetAppLogoOverride(new Uri(cover));
				}

				if (Directory.EnumerateFiles(folder, DownloadNames.BannerFile + ".*").FirstOrDefault() is { } banner)
				{
					// Inline, not the hero: that crops to 2:1, and banners are much wider.
					builder.SetInlineImage(new Uri(banner));
				}
			}

			AppNotificationManager.Default.Show(builder
				.AddText(anime.Title)
				.AddText(text)
				.AddButton(new AppNotificationButton("Play").AddArgument("action", "play").AddArgument("anime", anime.Id.ToString()).AddArgument("episode", first.ToString()))
				.AddButton(new AppNotificationButton("Open folder").AddArgument("action", "folder").AddArgument("folder", folder))
				.BuildNotification());
		}
		catch (Exception ex)
		{
			// E.g. notifications turned off for the app.
			logger.LogWarning(ex, "Couldn't show the notification for {Anime}'s downloads", anime.Title);
		}
	}

	/// <summary>Tells the user episode <paramref name="number"/> of <paramref name="anime"/> is out on its provider, with a button to play it.</summary>
	public void NotifyAvailable(AnimeModel anime, int number) => Show(anime.Title, $"Episode {number} is available", anime, number);

	/// <summary>
	/// A notification for the user to see that they get them: like a new episode's, of the last episode they watched of an
	/// anime they're watching (so its Play button plays something), or on its own if there's none. Returns whether Windows took it.
	/// </summary>
	public async Task<bool> NotifyTestAsync()
	{
		AnimeModel? anime = null;
		try
		{
			anime = (await schedule.GetWatchingAsync()).FirstOrDefault(x => x.Tracking?.WatchedEpisodes > 0);
		}
		catch (Exception ex)
		{
			// E.g. offline: the notification's shown without an anime.
			logger.LogWarning(ex, "Couldn't get an anime being watched for the test notification");
		}

		var number = anime?.Tracking?.WatchedEpisodes ?? 0;
		return Show(anime?.Title ?? "Totoro", anime is null ? "A test: new episodes are announced here" : $"A test: episode {number} is available", anime, number);
	}

	private bool Show(string title, string text, AnimeModel? anime, int number)
	{
		try
		{
			// shortcut: no cover, unpackaged toasts only show local images and nothing's downloaded here; add when there's an image cache
			var builder = new AppNotificationBuilder().AddText(title).AddText(text);
			if (anime is not null)
			{
				_anime[anime.Id] = anime;
				builder.AddButton(new AppNotificationButton("Play").AddArgument("action", "watch").AddArgument("anime", anime.Id.ToString()).AddArgument("episode", number.ToString()));
			}

			AppNotificationManager.Default.Show(builder.BuildNotification());
			return true;
		}
		catch (Exception ex)
		{
			// E.g. notifications turned off for the app.
			logger.LogWarning(ex, "Couldn't show the notification {Title}: {Text}", title, text);
			return false;
		}
	}

	/// <summary>Does what a notification's button asks, on the UI thread; returns whether the window should show (for a click on the rest of it, or Play).</summary>
	public bool Handle(AppNotificationActivatedEventArgs args)
	{
		switch (args.Arguments.TryGetValue("action", out var action) ? action : null)
		{
			case "folder":
				Process.Start("explorer.exe", $"\"{args.Arguments["folder"]}\"");
				return false;
			case "play" when long.TryParse(args.Arguments["anime"], out var id) && _anime.TryGetValue(id, out var anime):
				_ = watch.WatchAsync(anime, int.Parse(args.Arguments["episode"]), binge: true, fromDownloads: true);
				return true;
			case "watch" when long.TryParse(args.Arguments["anime"], out var id) && _anime.TryGetValue(id, out var anime):
				_ = watch.WatchAsync(anime, int.Parse(args.Arguments["episode"]), binge: true);
				return true;
			default:
				return true;
		}
	}

	private static Task WhenDoneAsync(DownloadItem item)
	{
		var done = new TaskCompletionSource();
		void Changed(object? sender, PropertyChangedEventArgs e)
		{
			if (!item.IsActive)
			{
				item.PropertyChanged -= Changed;
				done.TrySetResult();
			}
		}

		item.PropertyChanged += Changed;
		Changed(null, new(nameof(DownloadItem.State)));
		return done.Task;
	}
}

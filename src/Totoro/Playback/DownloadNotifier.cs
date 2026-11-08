using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Totoro.Core.Modules.Anime;

namespace Totoro.Playback;

/// <summary>
/// Windows notifications for downloads the user would want to hear about (a range they asked for, a new episode downloaded
/// unasked), shown whether or not the window is: once all of them are done, with buttons to open their folder or play them.
/// </summary>
public sealed class DownloadNotifier(WatchService watch, ILogger<DownloadNotifier> logger)
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
		try
		{
			AppNotificationManager.Default.Show(new AppNotificationBuilder()
				.AddText(anime.Title)
				.AddText(text)
				.AddButton(new AppNotificationButton("Play").AddArgument("action", "play").AddArgument("anime", anime.Id.ToString()).AddArgument("episode", first.ToString()))
				.AddButton(new AppNotificationButton("Open folder").AddArgument("action", "folder").AddArgument("folder", done[0].Item.Folder))
				.BuildNotification());
		}
		catch (Exception ex)
		{
			// E.g. notifications turned off for the app.
			logger.LogWarning(ex, "Couldn't show the notification for {Anime}'s downloads", anime.Title);
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

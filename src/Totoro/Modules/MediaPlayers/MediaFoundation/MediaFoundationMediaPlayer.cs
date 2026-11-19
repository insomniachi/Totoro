using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.UI.Player;
using Totoro.ViewModels.Player;

namespace Totoro.Modules.MediaPlayers.MediaFoundation;

/// <summary>The built-in player that plays with Windows' own media engine, in the app's own window.</summary>
internal sealed class MediaFoundationMediaPlayer(IPlayerWindowFactory windows, IModuleSettings<Settings> settings, IPreferences preferences, ILogger<MediaFoundationMediaPlayer> logger)
	: WindowMediaPlayer(windows, settings, preferences, logger)
{
	public override Guid Id => MediaFoundationModule.Id;

	public override string Name => "the built-in player (Windows)";

	// Part of Windows.
	public override bool IsAvailable() => true;

	protected override PlayerViewModel CreateViewModel()
	{
		return new MediaFoundationPlayerViewModel(Settings.Volume, Preferences.PlayerAspectRatio, Settings.AudioLanguage, Settings.SubtitleLanguage, Logger);
	}

	public override void Play(MediaItem media, TimeSpan startPosition)
	{
		var viewModel = Open();
		Proxy.Clear();

		var uri = media.Uri;
		var subtitles = media.Subtitles;
		// Windows can't be given headers to send, so any asked for go through the proxy.
		// shortcut: hosts behind Cloudflare's bot checks may turn the proxy away (AnimePahe's: 403), as with libVLC's player
		// when it needs the proxy; fetching with Windows.Web.Http (AdaptiveMediaSource) would be the fix if it matters.
		if (media.Headers is { Count: > 0 } headers)
		{
			uri = Proxy.Serve(uri, headers);
			subtitles = [.. subtitles.Select(x => x.Url.IsFile ? x : x with { Url = Proxy.Serve(x.Url, headers) })];
		}

		_ = viewModel.PlayAsync(uri, media.Title, [], startPosition, media.IsClip, subtitles);
	}
}

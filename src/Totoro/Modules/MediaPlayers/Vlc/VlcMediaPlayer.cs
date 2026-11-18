using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.ViewModels.Player;

namespace Totoro.Modules.MediaPlayers.Vlc;

/// <summary>The built-in player that plays with libvlc (VLC's engine), in the app's own window.</summary>
internal sealed class VlcMediaPlayer(IModuleSettings<Settings> settings, IPreferences preferences, ILogger<VlcMediaPlayer> logger)
	: WindowMediaPlayer(settings, preferences, logger)
{
	// The headers libvlc can send itself; any others go through the proxy.
	private static readonly Dictionary<string, string> s_ownHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		["User-Agent"] = ":http-user-agent",
		["Referer"] = ":http-referrer",
	};

	public override Guid Id => VlcModule.Id;

	public override string Name => "the built-in player (libVLC)";

	/// <summary>
	/// Whether libvlc came with the app (it's copied to the output by its package). Called as watching starts, so it also
	/// starts loading libvlc, while the show and the video are found: the window then opens straight away.
	/// </summary>
	public override bool IsAvailable()
	{
		if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64", "libvlc.dll")))
		{
			return false;
		}

		LibVlcLoader.Preload();
		return true;
	}

	protected override PlayerViewModel CreateViewModel() => new VlcPlayerViewModel(Settings.Volume, Preferences.PlayerAspectRatio, Logger);

	public override void Play(MediaItem media, TimeSpan startPosition)
	{
		var viewModel = Open();
		Proxy.Clear();

		var uri = media.Uri;
		List<string> options = [];
		var headers = media.Headers ?? new Dictionary<string, string>();
		var subtitles = media.Subtitles;
		// libvlc fetches it itself when it can send what's asked (Origin aside when there's a referrer: hosts that want
		// both, like AnimePahe's, settle for the referrer; KickAssAnime's, wanting Origin alone, don't). Through the proxy,
		// .NET does the fetching, which hosts behind Cloudflare's bot checks may turn away by how it connects (AnimePahe's,
		// uwucdn, does: 403), where they let libvlc (and mpv) in.
		var hasReferrer = headers.ContainsKey("Referer");
		if (headers.Keys.Where(x => !(hasReferrer && x.Equals("Origin", StringComparison.OrdinalIgnoreCase))).All(s_ownHeaders.ContainsKey))
		{
			options.AddRange(headers.Where(x => s_ownHeaders.ContainsKey(x.Key)).Select(x => $"{s_ownHeaders[x.Key]}={x.Value}"));
		}
		else
		{
			uri = Proxy.Serve(uri, headers);
			subtitles = [.. subtitles.Select(x => x.Url.IsFile ? x : x with { Url = Proxy.Serve(x.Url, headers) })];
		}

		if (Settings.AudioLanguage.Length > 0)
		{
			options.Add($":audio-language={Settings.AudioLanguage}");
		}

		if (Settings.SubtitleLanguage.Length > 0)
		{
			options.Add($":sub-language={Settings.SubtitleLanguage}");
		}

		_ = viewModel.PlayAsync(uri, media.Title, options, startPosition, media.IsClip, subtitles);
	}
}

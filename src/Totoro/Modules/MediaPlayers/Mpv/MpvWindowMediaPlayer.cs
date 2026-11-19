using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.UI.Player;
using Totoro.ViewModels.Player;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// The MPV module's player when it plays in the app's own window (<see cref="Settings.PlayInApp"/>): libmpv, mpv's engine,
/// with mpv's config, the app's controls over it.
/// </summary>
internal sealed class MpvWindowMediaPlayer(IPlayerWindowFactory windows, IModuleSettings<Settings> settings, IPreferences preferences, ILogger<MpvWindowMediaPlayer> logger)
	: WindowMediaPlayer(windows, settings, preferences, logger)
{
	public override Guid Id => MpvModule.Id;

	public override string Name => "mpv (in Totoro's window)";

	/// <summary>Whether libmpv was downloaded with mpv (see <see cref="MpvInstaller"/>).</summary>
	public override bool IsAvailable() => File.Exists(LibMpvNative.FilePath);

	/// <summary>Anime4K's modes, "Off" first, as the window's Anime4K button offers them; none while it isn't open.</summary>
	public IReadOnlyList<string> ShaderModes => ViewModel?.ShaderModes ?? [];

	/// <summary>The index in <see cref="ShaderModes"/> of the one in use; -1 if it's none of them.</summary>
	public int GetShaderMode() => ViewModel?.GetShaderMode() ?? -1;

	public void SelectShaderMode(int index) => ViewModel?.SelectShaderMode(index);

	protected override PlayerViewModel CreateViewModel() => new MpvPlayerViewModel(Settings.Volume, Preferences.PlayerAspectRatio, Logger);

	public override void Play(MediaItem media, TimeSpan startPosition)
	{
		var viewModel = Open();

		// mpv's per-file options: it sends the headers itself, so no proxy. The user agent and referrer have options of
		// their own; the rest are a comma-separated list.
		List<string> options = [];
		var headers = media.Headers ?? new Dictionary<string, string>();
		var others = new List<string>();
		foreach (var (name, value) in headers)
		{
			if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
			{
				options.Add($"user-agent={value}");
			}
			else if (name.Equals("Referer", StringComparison.OrdinalIgnoreCase))
			{
				options.Add($"referrer={value}");
			}
			else
			{
				others.Add($"{name}: {value}");
			}
		}

		if (others.Count > 0)
		{
			// shortcut: a header value with a comma would be split in two, as with the MPV player; none sent has one yet.
			options.Add($"http-header-fields={string.Join(",", others)}");
		}

		if (Settings.AudioLanguage.Length > 0)
		{
			options.Add($"alang={Settings.AudioLanguage}");
		}

		if (Settings.SubtitleLanguage.Length > 0)
		{
			options.Add($"slang={Settings.SubtitleLanguage}");
		}

		_ = viewModel.PlayAsync(media.Uri, media.Title, options, startPosition, media.IsClip, media.Subtitles);
	}
}

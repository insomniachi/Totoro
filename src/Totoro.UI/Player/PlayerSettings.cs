using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Totoro.Core.Settings;

namespace Totoro.UI.Player;

/// <summary>
/// The settings every player shares (shortcuts and languages for the app's own window, see <see cref="WindowMediaPlayer"/>;
/// volume and full screen for mpv's own window too): one set, registered by the app, on a settings page of its own (Settings › Playback › Player settings).
/// </summary>
// Not sealed: a plugin built before these were shared derives its own settings from it.
public class PlayerSettings : DataContainerConfig
{
	[DisplayName("Start in full screen")]
	public bool LaunchFullScreen { get; set; }

	[DisplayName("Audio language")]
	[Description("The audio track to pick when there's a choice: language codes in order of preference, e.g. jpn,ja.")]
	public string AudioLanguage { get; set; } = "jpn,ja";

	[DisplayName("Subtitle language")]
	[Description("The subtitle track to pick when there's a choice: language codes in order of preference, e.g. eng,en.")]
	public string SubtitleLanguage { get; set; } = "eng,en";

	[DisplayName("Play or pause shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string PlayPauseKeys { get; set; } = "Space";

	[DisplayName("Back 5 seconds shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SeekBackKeys { get; set; } = "Left";

	[DisplayName("Forward 5 seconds shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SeekForwardKeys { get; set; } = "Right";

	[DisplayName("Volume up shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string VolumeUpKeys { get; set; } = "Up";

	[DisplayName("Volume down shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string VolumeDownKeys { get; set; } = "Down";

	[DisplayName("Mute shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string MuteKeys { get; set; } = "M";

	[DisplayName("Full screen shortcut")]
	[Description("Esc always leaves full screen.")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string FullScreenKeys { get; set; } = "F";

	[DisplayName("Skip shortcut")]
	[Description("Skips the opening or ending, or takes what's offered over the video.")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SkipKeys { get; set; } = "S";

	[DisplayName("Next episode shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string NextEpisodeKeys { get; set; } = "N";

	[DisplayName("Episodes shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string EpisodesKeys { get; set; } = "E";

	[DisplayName("Aspect ratio shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string AspectRatioKeys { get; set; } = "A";

	[DisplayName("Close player shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string CloseKeys { get; set; } = "Q";

	[DisplayName("Volume")]
	[Description("Changing it in the player changes it here too.")]
	[Range(0, 100)]
	[Editor(DataContainerEditors.Slider, "")]
	public int Volume { get; set; } = 100;
}

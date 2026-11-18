using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Totoro.Core.Settings;

namespace Totoro.Modules.MediaPlayers;

/// <summary>
/// The settings of a player that plays in the app's own window (<see cref="Views.Player.PlayerWindow"/>). Each such player
/// derives its own type from it: module settings are kept by type.
/// </summary>
public abstract class PlayerSettings : DataContainerConfig
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
	public string PlayPauseKeys { get; set; } = "Space, K";

	[DisplayName("Back 5 seconds shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SeekBackKeys { get; set; } = "Left, J";

	[DisplayName("Forward 5 seconds shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SeekForwardKeys { get; set; } = "Right, L";

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
	public string FullScreenKeys { get; set; } = "F, F11";

	[DisplayName("Skip shortcut")]
	[Description("Skips the opening or ending, or takes what's offered over the video.")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string SkipKeys { get; set; } = "PageUp, S";

	[DisplayName("Next episode shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string NextEpisodeKeys { get; set; } = "N";

	[DisplayName("Episodes shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string EpisodesKeys { get; set; } = "E";

	[DisplayName("Aspect ratio shortcut")]
	[Editor(DataContainerEditors.Shortcut, "")]
	public string AspectRatioKeys { get; set; } = "A";

	[DisplayName("Volume")]
	[Description("Changing it in the player changes it here too.")]
	[Range(0, 100)]
	[Editor(DataContainerEditors.Slider, "")]
	public int Volume { get; set; } = 100;
}

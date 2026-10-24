using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.Modules.MediaPlayers.Vlc;

internal class VlcModule : IModule<Settings>
{
	public static Guid Id { get; } = new("5e0a7c1f-3b9d-4e62-8a4f-2d7c9b1e6f30");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Built-in player",
		Description = "Plays videos in Totoro's own window, with libvlc (VLC's engine): skipping the opening and the next episode are offered over the video.",
		DataFolder = "Vlc",
		Image = @"Assets\Totoro.png"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		// One player app-wide: it owns the window, so a second Play replaces what's playing.
		services.AddSingleton<VlcMediaPlayer>();
		services.AddKeyedSingleton<IMediaPlayer>(Id, (sp, _) => sp.GetRequiredService<VlcMediaPlayer>());
	}
}

public sealed class Settings : DataContainerConfig
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
}

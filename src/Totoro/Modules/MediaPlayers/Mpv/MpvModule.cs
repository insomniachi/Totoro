using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.Modules.MediaPlayers.Mpv;

internal class MpvModule : IModule<Settings>
{
	public static Guid Id { get; } = new("b8c3f0d2-1c5e-4f6a-9b7d-3f8e1c5f0d2a");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "MPV",
		Description = "Plays videos in mpv, a free and open-source media player.",
		EntryPoint = "Mpv",
		Image = @"Assets\Modules\Mpv.png"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		// One player app-wide: it owns the mpv process, so a second Play replaces what's playing.
		services.AddKeyedSingleton<IMediaPlayer, MpvMediaPlayer>(Id);
		services.AddSingleton<MpvInstaller>();
		services.AddAgentTools<MpvTools>();
	}
}

public sealed class Settings : DataContainerConfig
{
	// The settings are made by the JSON serializer, not the container: the installer is found when they're shown.
	// Its description is the build installed, once there is one (see MpvInstaller).
	[DisplayName("Mpv Build")]
	[Description("mpv, FFmpeg and yt-dlp, and Anime4K as set below")]
	[Editor(DataContainerEditors.Button, "")]
	[JsonIgnore]
	public SettingsAction Download => Locator.GetRequiredService<MpvInstaller>().Action;

	[DisplayName("Install Anime4K")]
	[Parent(nameof(Download))]
	[Description("Add Anime4K's upscaling to mpv's config (in mpv, Ctrl+1 to 6 pick a mode, Ctrl+0 turns it off). Download or update to apply a change.")]
	public bool InstallAnime4K { get; set; } = true;

	[DisplayName("Anime4K GPU")]
	[Parent(nameof(Download))]
	[Description("Lower-end (e.g. GTX 1060, RX 570) plays its fast shaders; higher-end (e.g. RTX 3060, RX 6600 XT) its high-quality ones.")]
	public Anime4KGpu Anime4KGpu { get; set; } = Anime4KGpu.LowerEnd;

	[DisplayName("Add to Open with")]
	[Parent(nameof(Download))]
	[Description("Offer the mpv downloaded here in Windows' Open with menu for videos.")]
	public bool AddToOpenWith { get; set; } = true;

	[DisplayName("Executable")]
	[Description("Leave empty to use the downloaded or installed mpv.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string FileName { get; set; } = "";

	[DisplayName("Start in full screen")]
	public bool LaunchFullScreen { get; set; } = true;
}

/// <summary>Which of Anime4K's templates to install: its fast shaders, or its high-quality ones.</summary>
public enum Anime4KGpu
{
	LowerEnd,
	HigherEnd
}

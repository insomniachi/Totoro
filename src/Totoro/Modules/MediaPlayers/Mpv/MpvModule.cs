using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.UI.Player;

namespace Totoro.Modules.MediaPlayers.Mpv;

internal class MpvModule : IModule<Settings>
{
	public static Guid Id { get; } = new("b8c3f0d2-1c5e-4f6a-9b7d-3f8e1c5f0d2a");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "MPV",
		Description = "Plays videos in mpv, a free and open-source media player: in mpv's own window, or in Totoro's with its controls over the video.",
		DataFolder = "Mpv",
		Image = @"Assets\Modules\Mpv.png"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		// One of each app-wide: each owns its mpv (process or window), so a second Play replaces what's playing. Which one
		// plays is the setting's at the time: the player is asked for as each video starts.
		services.AddSingleton<MpvMediaPlayer>();
		services.AddSingleton<MpvWindowMediaPlayer>();
		services.AddKeyedTransient<IMediaPlayer>(Id, (sp, _) => sp.GetRequiredService<IModuleSettings<Settings>>().Value.PlayInApp
			? sp.GetRequiredService<MpvWindowMediaPlayer>()
			: sp.GetRequiredService<MpvMediaPlayer>());
		services.AddSingleton<MpvInstaller>();
		services.AddAgentTools<MpvTools>();
	}
}

/// <summary>
/// A built-in player's settings too: the shortcuts, languages... are for playing in Totoro's window. Songs play at its
/// volume too.
/// </summary>
public sealed class Settings : PlayerSettings
{
	// mpv's own window has always started full screen.
	public Settings() => LaunchFullScreen = true;

	[DisplayName("Play in Totoro's window")]
	[Description("Play in Totoro's own window, with its controls, skipping and the next episode over the video, still with mpv's engine and config (Anime4K too). Off: mpv's own window. Needs mpv downloaded here.")]
	public bool PlayInApp { get; set; } = true;

	// The settings are made by the JSON serializer, not the container: the installer is found when they're shown.
	// Its description is the build installed, once there is one (see MpvInstaller).
	[DisplayName("Mpv Build")]
	[Description("mpv, FFmpeg and yt-dlp, and Anime4K as set below")]
	[Editor(DataContainerEditors.Button, "")]
	[JsonIgnore]
	public SettingsAction Download => Locator.GetRequiredService<MpvInstaller>().Action;

	[DisplayName("Check for updates")]
	[Parent(nameof(Download))]
	[Description("How often Totoro looks for a newer mpv as it starts (it's downloaded the first time Totoro starts).")]
	public UpdateInterval UpdateInterval { get; set; } = UpdateInterval.Weekly;

	[DisplayName("Install Anime4K")]
	[Parent(nameof(Download))]
	[Description("Add Anime4K's upscaling to mpv's config (in mpv, Ctrl+1 to 6 pick a mode, Ctrl+0 turns it off). Download or update to apply a change.")]
	public bool InstallAnime4K { get; set; } = true;

	[DisplayName("Anime4K GPU")]
	[Parent(nameof(Download))]
	[Description("Auto picks from the graphics card's memory (8 GB or more is higher-end; under 4 GB, e.g. most integrated graphics, gets no Anime4K). Lower-end (e.g. GTX 1060, RX 570) plays its fast shaders; higher-end (e.g. RTX 3060, RX 6600 XT) its high-quality ones.")]
	public Anime4KGpu Anime4KGpu { get; set; } = Anime4KGpu.Auto;

	[DisplayName("Add to Open with")]
	[Parent(nameof(Download))]
	[Description("Offer the mpv downloaded here in Windows' Open with menu for videos.")]
	public bool AddToOpenWith { get; set; } = true;

	[DisplayName("Executable")]
	[Description("Leave empty to use the downloaded or installed mpv.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string FileName { get; set; } = "";

	// mpv amplifies up to its volume-max, 130 by default; the other players stop at 100.
	protected override void ConfigureProperty(DataContainerPropertyBuilder propertyBuilder, System.Reflection.PropertyInfo info)
	{
		if (info.Name == nameof(Volume))
		{
			propertyBuilder.WithRange(0, 130);
		}
	}
}

/// <summary>Which of Anime4K's templates to install: its fast shaders, or its high-quality ones.</summary>
public enum Anime4KGpu
{
	LowerEnd,
	HigherEnd,
	// Last, so settings saved as numbers keep their meaning.
	Auto
}

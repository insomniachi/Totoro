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
	[Description("mpv, FFmpeg and yt-dlp")]
	[Editor(DataContainerEditors.Button, "")]
	[JsonIgnore]
	public SettingsAction Download => Locator.GetRequiredService<MpvInstaller>().Action;

	[DisplayName("Add to Open with")]
	[Description("Offer the mpv downloaded here in Windows' Open with menu for videos.")]
	public bool AddToOpenWith { get; set; } = true;

	[DisplayName("Executable")]
	[Description("Your own mpv.exe. Leave it empty to use the one downloaded here, or else one that's installed.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string FileName { get; set; } = "";

	[DisplayName("Start in full screen")]
	public bool LaunchFullScreen { get; set; } = true;
}

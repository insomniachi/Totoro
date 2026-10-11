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
		EntryPoint = "Vlc"
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
}

using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;

namespace Totoro.Modules.MediaPlayers.Vlc;

internal class VlcModule : IModule<Settings>
{
	public static Guid Id { get; } = new("5e0a7c1f-3b9d-4e62-8a4f-2d7c9b1e6f30");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Built-in player (libVLC)",
		Description = "Plays videos in Totoro's own window, with libvlc (VLC's engine): skipping the opening and the next episode are offered over the video.",
		DataFolder = "Vlc",
		Image = @"Assets\Modules\Vlc.png"
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

/// <summary>Songs play at its volume too.</summary>
public sealed class Settings : PlayerSettings;

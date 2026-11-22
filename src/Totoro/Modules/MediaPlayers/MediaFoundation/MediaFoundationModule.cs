using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.UI.Player;

namespace Totoro.Modules.MediaPlayers.MediaFoundation;

internal class MediaFoundationModule : IModule
{
	public static Guid Id { get; } = new("9d4b2e71-6c3a-4f85-b0e9-1a7f3c5d8e42");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Built-in player (Windows)",
		Description = "Plays videos in Totoro's own window, with Windows' own media engine (MediaPlayerElement), with the same controls as mpv in Totoro's window. "
			+ "Plays what Windows has codecs for: subtitles in SRT, WebVTT or TTML, not ASS.",
		DataFolder = "MediaFoundation",
		Image = @"Assets\Modules\WindowsMediaPlayer.png"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		// One player app-wide: it owns the window, so a second Play replaces what's playing.
		services.AddKeyedSingleton<IMediaPlayer, MediaFoundationMediaPlayer>(Id);
	}
}

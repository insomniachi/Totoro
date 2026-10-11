using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
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
		EntryPoint = "Mpv"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		// One player app-wide: it owns the mpv process, so a second Play replaces what's playing.
		services.AddKeyedSingleton<IMediaPlayer, MpvMediaPlayer>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Executable")]
	[Description("mpv.exe. Found automatically when mpv is installed; set it here otherwise.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string FileName { get; set; } = "";

	[DisplayName("Start in full screen")]
	public bool LaunchFullScreen { get; set; } = true;
}

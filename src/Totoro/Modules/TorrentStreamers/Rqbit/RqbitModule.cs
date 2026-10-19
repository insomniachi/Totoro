using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.TorrentStreamers.Rqbit;

internal class RqbitModule : IModule<Settings>
{
	public static Guid Id { get; } = new("5f0c2a8e-7d3b-4e91-a6c4-2b8d9e1f3a70");

	public static Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "rqbit",
		Description = "Streams torrents on this PC with rqbit, a fast BitTorrent client, so any media player can play them.",
		EntryPoint = "Rqbit",
		Image = @"Assets\Modules\Rqbit.png"
	};

	Descriptor IModule.Descriptor => Descriptor;

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		// One app-wide: it owns the rqbit server, which streams every torrent.
		services.AddKeyedSingleton<ITorrentStreamer, RqbitStreamer>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Keep downloaded episodes")]
	[Description(@"In %LOCALAPPDATA%\Totoro\Torrents. Otherwise they're deleted when you stop watching.")]
	public bool KeepDownloads { get; set; }
}

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.SubsPlease;

internal class SubsPleaseModule : IModule<Settings>
{
	public static Guid Id { get; } = new("9b7a71c8-7633-46e8-be5e-288c19ac1330");

	public static Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "SubsPlease",
		Description = "SubsPlease's goal is to provide fast and timely English subtitled anime immediately after they are simulcasted.",
		EntryPoint = "SubsPlease"
	};

	Descriptor IModule.Descriptor => Descriptor;

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		services.AddKeyedTransient<IAnimeProvider, SubsPleaseProvider>(Id);
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Preferred quality")]
	[Description("Used when the episode has it; otherwise the best there is.")]
	public VideoQuality Quality { get; set; } = VideoQuality.FullHd;
}

public enum VideoQuality
{
	[Description("1080p")] FullHd,
	[Description("720p")] Hd,
	[Description("480p")] Sd,
}

internal static class VideoQualityExtensions
{
	public static int ToResolution(this VideoQuality quality) => quality switch
	{
		VideoQuality.Hd => 720,
		VideoQuality.Sd => 480,
		_ => 1080,
	};
}

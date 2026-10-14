using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.Senshi;

internal class SenshiModule : IModule<Settings>
{
	public static Guid Id { get; } = new("c6066fda-8e30-4545-9599-8e6c3e917312");

	public static Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Senshi",
		Description = "Senshi streams anime directly, subtitled and dubbed.",
		EntryPoint = "Senshi"
	};

	Descriptor IModule.Descriptor => Descriptor;

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddModuleSettings(this);
		services.AddKeyedTransient<IAnimeProvider, SenshiProvider>(Id);
		services.AddHttpClient("Senshi", (sp, client) =>
		{
			var settings = sp.GetRequiredService<IModuleSettings<Settings>>().Value;
			client.BaseAddress = new Uri(settings.Site.Trim().TrimEnd('/') + "/");
		});
	}
}

public sealed class Settings : DataContainerConfig
{
	[DisplayName("Site")]
	[Description("Senshi's address. Change it if the site has moved.")]
	public string Site { get; set; } = "https://senshi.live";

	[DisplayName("Audio")]
	[Description("Preferred audio type: subbed or dubbed.")]
	public AudioType Audio { get; set; } = AudioType.Sub;
}

public enum AudioType
{
	[Description("Sub (Japanese, with subtitles)")] Sub,
	[Description("Dub (English dubbed)")] Dub,
}

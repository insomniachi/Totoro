using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.AnimePahe;

public class Plugin : Plugin<AnimeProvider, Config>
{
    public override AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider(),
        IdMapper = new IdMapper(),
    };

    public override PluginInfo GetInfo() => new()
    {
        DisplayName = "Anime Pahe",
        Name = "anime-pahe",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimePahe.anime-pahe-logo.png"),
        Description = "AnimePahe is an encode \"group\", was founded in July 2014. encodes on-going anime, completed anime and anime movie."
    };
}

public class Module : ModuleBase
{
	public static PluginDescriptor AnimeHeaven { get; } = new()
	{
		Type = PluginType.Anime,
		DisplayName = "Anime Heaven",
		Id = Guid.Parse("19b63f37-34d6-4ad3-8ceb-f50bca62c32f"),
		Version = Assembly.GetExecutingAssembly().GetName().Version!,
		Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimePahe.anime-pahe-logo.png"),
	};


	public override void RegisterServices(IServiceCollection services)
	{
		services.AddKeyedTransient<IAnimeProvider, AnimeHeavenProvider>(AnimeHeaven.Id);
        services.AddKeyedTransient<IVideoExtractor, AnimeHeavenExtractor>("AnimeHeaven");
	}

	protected override IEnumerable<PluginDescriptor> GetPlugins()
	{
        return [AnimeHeaven];
	}
}

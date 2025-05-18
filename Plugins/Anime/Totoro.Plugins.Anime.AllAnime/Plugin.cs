using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.AllAnime;

[ExcludeFromCodeCoverage]
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
        DisplayName = "AllAnime",
        Name = "allanime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AllAnime.allanime-icon.png"),
        Description = "AllAnime's goal is to provide you with the highest possible amount of daily anime episodes/manga chapters for free and without any kind of limitation."
    };
}

public class AllAnimeModule : ModuleBase
{
    public PluginDescriptor Descriptor { get; } = new()
    {
        Id = Guid.Parse("480f33f1-f303-43e4-b10e-1d1f2a319eb3"),
        DisplayName = "AllAnime",
		Version = Assembly.GetExecutingAssembly().GetName().Version!,
		Type = PluginType.Anime,
        Description = "AllAnime's goal is to provide you with the highest possible amount of daily anime episodes/manga chapters for free and without any kind of limitation.",
        Icon = typeof(AllAnimeModule).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AllAnime.allanime-icon.png")
    };

	public override void RegisterServices(IServiceCollection services)
	{
        services.AddPluginOption<ConfigNew>(Descriptor.Id);
        services.AddTransient<IAnimeProvider, AnimeProviderImpl>();
	}

	protected override IEnumerable<PluginDescriptor> GetPlugins()
	{
        return [Descriptor];
	}
}

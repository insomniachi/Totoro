using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.Jellyfin;

[ExcludeFromCodeCoverage]
public class Plugin : Plugin<AnimeProvider, Config>
{
    public override AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
    };

    public override PluginInfo GetInfo() => new()
    {
        DisplayName = "Jellyfin",
        Name = "jellyfin",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
		Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.Jellyfin.jellyfin-icon.png"),
		Description = "Access media from your self hosted instance"
    };
}

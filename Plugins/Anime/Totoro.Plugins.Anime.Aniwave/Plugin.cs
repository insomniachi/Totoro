using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Aniwave;

public class Plugin : IPlugin<AnimeProvider>
{
    public AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Aniwave",
        Name = "aniwave",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Description = "previously 9anime"
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x =>
            {
               return x.WithNameAndValue(Config.Url)
                       .WithDisplayName("Url")
                       .WithDescription("Url to home page")
                       .WithGlyph(Glyphs.Url)
                       .ToPluginOption();
            });
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}
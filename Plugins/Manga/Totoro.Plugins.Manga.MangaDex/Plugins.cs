using System.Reflection;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Manga.MangaDex;

public class Plugin : Plugin<MangaProvider, Config>
{
    public override MangaProvider Create() => new()
    {
        Catalog = new Catalog(),
        ChapterProvider = new ChapterProvider(),
    };

    public override PluginInfo GetInfo() => new()
    {
        DisplayName = "Manga Dex",
        Name = "manga-dex",
        Version = Assembly.GetExecutingAssembly().GetName().Version!
    };
}

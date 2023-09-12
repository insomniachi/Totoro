using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Manga.MangaDex
{
    public class Plugin : IPlugin<MangaProvider>
    {
        public MangaProvider Create() => new()
        {
            Catalog = new Catalog(),
            ChapterProvider = new ChapterProvider(),
        };

        public PluginInfo GetInfo() => new()
        {
            DisplayName = "Manga Dex",
            Name = "manga-dex",
        };

        public PluginOptions GetOptions() => new();

        public void SetOptions(PluginOptions options) { }

        object IPlugin.Create() => Create();
    }
}

using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Plugins.Torrents.AnimeTosho;

public class Plugin : IPlugin<ITorrentTracker>
{
    public ITorrentTracker Create() => new Tracker();

    public PluginInfo GetInfo() => new()
    {
        Name = "anime-tosho",
        DisplayName = "Anime Tosho",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Description = "Anime Tosho is a free, completely automated service which mirrors most torrents posted on TokyoTosho, Nyaa and AniDex",
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Torrents.AnimeTosho.anime-tosho-icon.ico")
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x => x.WithNameAndValue(Config.Url)
                             .WithGlyph(Glyphs.Url)
                             .ToPluginOption())
            .AddOption(x => x.WithNameAndValue(Config.Filter)
                             .WithAllowedValues<Filter>()
                             .WithGlyph(Glyphs.Filter)
                             .ToSelectablePluginOption())
            .AddOption(x => x.WithNameAndValue(Config.Sort)
                             .WithDisplayName("Sort By")
                             .WithAllowedValues<Sort>()
                             .WithGlyph(Glyphs.Sort)
                             .ToSelectablePluginOption());

    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
        Config.Filter = options.GetEnum(nameof(Config.Filter), Config.Filter);
        Config.Sort = options.GetEnum(nameof(Config.Sort), Config.Sort);
    }

    object IPlugin.Create() => Create();
}

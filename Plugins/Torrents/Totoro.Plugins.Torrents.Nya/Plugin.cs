using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Plugins.Torrents.Nya;

public class Plugin : IPlugin<ITorrentTracker>
{
    public ITorrentTracker Create() => new Tracker();

    public PluginInfo GetInfo() => new()
    {
        Name = "nya",
        DisplayName = "Nya",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Description = "A BitTorrent community focused on Eastern Asian media including anime, manga, music, and more",
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Torrents.Nya.nya-logo.png")
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
            .AddOption(x => x.WithNameAndValue(Config.Category)
                             .WithAllowedValues<Category>()
                             .WithGlyph(Glyphs.Category)
                             .ToSelectablePluginOption())
            .AddOption(x => x.WithNameAndValue(Config.SortBy)
                             .WithDisplayName("Sort By")
                             .WithAllowedValues<SortBy>()
                             .WithGlyph(Glyphs.Sort)
                             .ToSelectablePluginOption())
            .AddOption(x => x.WithNameAndValue(Config.SortDirection)
                             .WithDisplayName("Sort Order")
                             .WithAllowedValues<SortDirection>()
                             .WithGlyph(Glyphs.SortDirection)
                             .ToSelectablePluginOption());

    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
        Config.Filter = options.GetEnum(nameof(Config.Filter), Config.Filter);
        Config.Category = options.GetEnum(nameof(Config.Category), Config.Category);
        Config.SortBy = options.GetEnum(nameof(Config.SortBy), Config.SortBy);
        Config.SortDirection = options.GetEnum(nameof(Config.SortDirection), Config.SortDirection);
    }

    object IPlugin.Create() => Create();
}

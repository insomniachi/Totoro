using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Models;

public class AnimePreferences : ReactiveObject
{
    [Reactive] public string Alias { get; set; }
    [Reactive] public string Provider { get; set; }
    [Reactive] public StreamType StreamType { get; set; } = StreamType.Subbed(Languages.English);
    [Reactive] public bool PreferDubs { get; set; }
}

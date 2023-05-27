using ReactiveUI.Fody.Helpers;
using ReactiveUI;

namespace Totoro.Plugins.Torrents.Models;

public class TorrentModel : ReactiveObject
{
    required public string Name { get; init; }
    public string? Link { get; set; }
    public string? Magnet { get; set; }
    public string? Size { get; set; }
    public DateTime? Date { get; set; }
    public int? Seeders { get; set; }
    public int? Leeches { get; set; }
    public int? Completed { get; set; }
    public string? CategoryImage { get; set; }
    public string? Category { get; set; }
    public string? Link2 { get; set; }
    [Reactive] public TorrentState State { get; set; }
}

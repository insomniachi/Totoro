using System.Runtime.Serialization;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Manga.MangaDex;

public class Config : ConfigObject
{
    [IgnoreDataMember]
    public string Api { get; set; } = "https://api.mangadex.org/";
}

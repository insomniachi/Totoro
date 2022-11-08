using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Totoro.Core.Models
{
    [DebuggerDisplay("{Title}")]
    public class ShanaProjectCatalogItem
    {
        [JsonPropertyName("value")]
        public string Title { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }
    }


    [DebuggerDisplay("{Title} - Episode {Episode} ({Size})")]
    public class ShanaProjectDownloadableContent
    {
        public string Title { get; set; }
        public string Episode { get; set; }
        public string Quality { get; set; }
        public string Size { get; set; }
        public string Url { get; set; }
    }
}

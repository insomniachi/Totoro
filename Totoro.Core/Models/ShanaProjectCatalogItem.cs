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
}

using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Totoro.Core.Models
{
    [DebuggerDisplay("{Title}")]
    public class ShanaProjectCatalogItem
    {
        [JsonPropertyName("value")] public string Title { get; set; }
        [JsonPropertyName("id")] public long Id { get; set; }
        public override string ToString() => Title;
    }


    [DebuggerDisplay("{Title} - Episode {Episode} ({Size})")]
    public class ShanaProjectDownloadableContent : IDownloadableContent
    {
        public string Title { get; set; }
        public string Episode { get; set; }
        public string Quality { get; set; }
        public string Size { get; set; }
        public string Url { get; set; }
        public string Subber { get; set; }
        public ReactiveCommand<Unit,Unit> Download { get; } = ReactiveCommand.Create<Unit, Unit>(_ => Unit.Default);
    }

    public interface IDownloadableContent
    {
        string Title { get; }
        string Url { get; }
    }
}

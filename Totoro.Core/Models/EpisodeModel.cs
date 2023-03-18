using System.Diagnostics;

namespace Totoro.Core.Models
{
    [DebuggerDisplay("{DisplayName}")]
    public class EpisodeModel : ReactiveObject
    {
        public int EpisodeNumber { get; init; }
        public string SpecialEpisodeNumber { get; init; }
        public bool IsSpecial { get; init; }
        [Reactive] public string EpisodeTitle { get; set; }

        public string DisplayName
        {
            get
            {
                return string.IsNullOrEmpty(EpisodeTitle)
                    ? $"{EpisodeNumber}"
                    : $"{EpisodeNumber} - {EpisodeTitle}";
            }
        }
    }
}

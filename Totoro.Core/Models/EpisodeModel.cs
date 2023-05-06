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
        [ObservableAsProperty] public string DisplayName { get; }

        public EpisodeModel()
        {
            this.WhenAnyValue(x => x.EpisodeTitle)
                .Select(title =>
                {
                    return string.IsNullOrEmpty(title)
                            ? $"{EpisodeNumber}"
                            : $"{EpisodeNumber} - {title}";
                })
                .ToPropertyEx(this, x => x.DisplayName);
        }
    }
}

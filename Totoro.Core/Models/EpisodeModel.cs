using System.Diagnostics;

namespace Totoro.Core.Models
{
    [DebuggerDisplay("{DisplayName}")]
    public class EpisodeModel : ReactiveObject
    {

        [Reactive] public int EpisodeNumber { get; set; }
        [Reactive] public string SpecialEpisodeNumber { get; set; }
        [Reactive] public bool IsSpecial { get; set; }
        [Reactive] public string EpisodeTitle { get; set; }
        [Reactive] public bool IsFillter { get; set; }
        [ObservableAsProperty] public string DisplayName { get; }

        public EpisodeModel()
        {
            this.WhenAnyPropertyChanged()
                .Select(_ =>
                {
                    return string.IsNullOrEmpty(EpisodeTitle)
                            ? $"{(IsSpecial ? SpecialEpisodeNumber : EpisodeNumber)}"
                            : $"{(IsSpecial ? SpecialEpisodeNumber : EpisodeNumber)} - {EpisodeTitle}";
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToPropertyEx(this, x => x.DisplayName);
        }
    }
}

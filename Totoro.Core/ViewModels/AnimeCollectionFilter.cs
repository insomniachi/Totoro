using System.Text.RegularExpressions;

namespace Totoro.Core.ViewModels;

public partial class AnimeCollectionFilter : ReactiveObject
{
    [Reactive] public AnimeStatus? ListStatus { get; set; } = AnimeStatus.Watching;
    [Reactive] public string SearchText { get; set; }
    [Reactive] public string Year { get; set; }
    [Reactive] public AiringStatus? AiringStatus { get; set; }
    [Reactive] public ObservableCollection<string> Genres { get; set; } = [];

    [GeneratedRegex(@"(19[5-9][0-9])|(20\d{2})")]
    private partial Regex YearRegex();

    public bool IsVisible(AnimeModel model)
    {
        if (model.Tracking is null)
        {
            return false;
        }

        var listStatusCheck = ListStatus == AnimeStatus.Watching
            ? model.Tracking.Status is AnimeStatus.Watching or AnimeStatus.Rewatching
            : model.Tracking.Status == ListStatus;

        var searchTextStatus = string.IsNullOrEmpty(SearchText) ||
                               model.Title.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase) ||
                               model.AlternativeTitles.Any(x => x.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase));
        var yearCheck = string.IsNullOrEmpty(Year) || !YearRegex().IsMatch(Year) || model.Season.Year.ToString() == Year;
        var genresCheck = !Genres.Any() || Genres.All(x => model.Genres.Any(y => string.Equals(y, x, StringComparison.InvariantCultureIgnoreCase)));
        var airingStatusCheck = AiringStatus is null || AiringStatus == model.AiringStatus;

        return listStatusCheck && searchTextStatus && yearCheck && genresCheck && airingStatusCheck;
    }
}

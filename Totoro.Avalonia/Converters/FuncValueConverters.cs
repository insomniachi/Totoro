using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Avalonia.Helpers;
using Totoro.Core.Contracts;
using Totoro.Core.Helpers;
using Totoro.Core.Models;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Avalonia.Converters;

public static class FuncValueConverters
{
    private static readonly ISettings _settings = App.Services.GetRequiredService<ISettings>();
    private static readonly string[] _ratingNames =
    [
        "(0) - No Score",
        "(1) - Appalling",
        "(2) - Horrible",
        "(3) - Very Bad",
        "(4) - Bad",
        "(5) - Average",
        "(6) - Fine",
        "(7) - Good",
        "(8) - Very Good",
        "(9) - Great",
        "(10) - Masterpiece"
    ];

    public static FuncValueConverter<string, Task<Bitmap?>> UrlToBitmapConverter { get; } =
        new(x => ImageHelper.LoadFromWeb(x!));

    public static FuncValueConverter<IAiredAnimeEpisode, string> GetTimeOfAiring { get; } = new(episode =>
    {
        if (episode is IHaveCreatedTime iHaveCreatedTime)
        {
            return iHaveCreatedTime.CreatedAt.Humanize();
        }

        return string.Empty;
    });

    public static FuncValueConverter<IAiredAnimeEpisode, bool> IsTimeOfAiringVisible { get; } =
        new(episode => episode is IHaveEpisodes);

    public static FuncValueConverter<ICatalogItem, string> CatalogItemToImage { get; } = new(catalog =>
        catalog is not IHaveImage ihv ? string.Empty : ihv.Image);

    public static FuncValueConverter<ICatalogItem, string> CatalogItemToRating { get; } = new(catalog =>
        catalog is not IHaveRating ihr ? string.Empty : ihr.Rating);

    public static FuncValueConverter<ICatalogItem, string> CatalogItemToAdditionalInfo { get; } = new(catalog =>
    {
        StringBuilder sb = new();
        switch (catalog)
        {
            case IHaveSeason ihs when !string.IsNullOrEmpty(ihs.Season):
                sb.Append($"{ihs.Season} {ihs.Year}");
                break;
            case IHaveYear ihy when int.TryParse(ihy.Year, out var year) && year > 0:
                sb.Append(ihy.Year);
                break;
        }

        return sb.ToString();
    });

    public static FuncValueConverter<AnimeStatus, string> ToDisplayString { get; } = new(status => status switch
    {
        AnimeStatus.Watching => "Watching",
        AnimeStatus.PlanToWatch => "Plan to watch",
        AnimeStatus.OnHold => "On Hold",
        AnimeStatus.Completed => "Completed",
        AnimeStatus.Dropped => "Dropped",
        AnimeStatus.Rewatching => "Re watching",
        AnimeStatus.None => "",
        _ => throw new UnreachableException()
    });

    public static FuncValueConverter<AnimeModel, string> HumanizeAiringTime { get; } = new(anime =>
    {
        if (anime is null)
        {
            return string.Empty;
        }

        return anime.NextEpisodeAt is null
            ? string.Empty
            : $"EP{anime.AiredEpisodes + 1}: {(anime.NextEpisodeAt.Value - DateTime.Now).HumanizeTimeSpan()}";
    });

    public static FuncValueConverter<AnimeModel, string> AnimeToTitle { get; } = new(anime =>
    {
        if (anime is null)
        {
            return string.Empty;
        }

        return _settings.UseEnglishTitles ? anime.EngTitle : anime.RomajiTitle;
    });

    public static FuncValueConverter<AnimeModel, bool> IsRewatching { get; } =
        new(anime => anime?.Tracking?.Status == AnimeStatus.Rewatching);

    public static FuncValueConverter<AnimeModel, bool> HasTracking { get; } = new(anime => anime?.Tracking is not null);

    public static FuncMultiValueConverter<object, string> WatchProgressDisplayString { get; } = new(values =>
    {
        var list = values.ToList();
        if (list[0] == AvaloniaProperty.UnsetValue || list[1] == AvaloniaProperty.UnsetValue)
        {
            return "";
        }

        var tracking = list[0] as Tracking;
        var total = (int?)list[1];

        return AnimeHelpers.Progress(tracking, total);
    });

    public static FuncValueConverter<AnimeModel?, MenuFlyout?> RatingsFlyout { get; } = new(anime =>
    {
        if (anime is null)
        {
            return null;
        }

        var score = anime.Tracking?.Score ?? 0;
        if (score > 10)
        {
            return null;
        }

        var flyout = new MenuFlyout();
        for (var i = 0; i < _ratingNames.Length; i++)
        {
            flyout.Items.Add(new MenuItem()
            {
                // Command = UpdateRating,
                CommandParameter = i,
                Header = _ratingNames[i],
                Icon = new RadioButton
                {
                    GroupName = anime.Id.ToString(),
                    IsChecked = score == i,
                }
            });
        }
        return flyout;
    });

    
    private static string HumanizeTimeSpan(this TimeSpan ts)
    {
        var sb = new StringBuilder();
        var week = ts.Days / 7;
        var days = ts.Days % 7;
        if (week > 0)
        {
            sb.Append($"{week}w ");
        }

        if (days > 0)
        {
            sb.Append($"{days}d ");
        }

        if (ts.Hours > 0)
        {
            sb.Append($"{ts.Hours.ToString().PadLeft(2, '0')}h ");
        }

        if (ts.Minutes > 0)
        {
            sb.Append($"{ts.Minutes.ToString().PadLeft(2, '0')}m ");
        }

        if (ts.Seconds > 0)
        {
            sb.Append($"{ts.Seconds.ToString().PadLeft(2, '0')}s ");
        }

        return sb.ToString().TrimEnd();
    }
}
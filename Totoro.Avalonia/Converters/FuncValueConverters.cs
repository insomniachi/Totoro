using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Humanizer;
using Totoro.Avalonia.Helpers;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Avalonia.Converters;

public static class FuncValueConverters
{
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
}
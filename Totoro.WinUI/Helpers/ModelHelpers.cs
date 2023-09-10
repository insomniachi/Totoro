using System.Text;
using Humanizer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.WinUI.Helpers;

public static class ModelHelpers
{
    public static string GetTimeOfAiring(this IAiredAnimeEpisode episode)
    {
        if (episode is IHaveCreatedTime ihct)
        {
            return ihct.CreatedAt.Humanize();
        }

        return string.Empty;
    }

    public static Visibility GetTimeOfAiringVisibility(this IAiredAnimeEpisode episode) => Converters.BooleanToVisibility(episode is IHaveCreatedTime);

    public static string GetImage(this ICatalogItem searchResult) => searchResult is IHaveImage ihi ? ihi.Image : string.Empty;
    public static ImageSource GetImageSource(this ICatalogItem searchResult)
    {
        if (searchResult is not IHaveImage ihi)
        {
            return null;
        }

        try
        {
            var uri = new Uri(ihi.Image, UriKind.Absolute);
            var imageSource = new BitmapImage { UriSource = uri };
            return imageSource;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource GetImageSource(this Plugins.Manga.Contracts.ICatalogItem searchResult)
    {
        try
        {
            var uri = new Uri(searchResult.Image, UriKind.Absolute);
            var imageSource = new BitmapImage { UriSource = uri };
            return imageSource;
        }
        catch
        {
            return null;
        }
    }

    public static string GetAdditionalInformation(this ICatalogItem searchResult)
    {
        StringBuilder sb = new();
        if (searchResult is IHaveSeason ihs)
        {
            sb.Append($"{ihs.Season} {ihs.Year}");
        }
        else if (searchResult is IHaveYear ihy)
        {
            sb.Append(ihy.Year);
        }

        return sb.ToString();
    }
    public static string GetRating(this ICatalogItem searchResult) => searchResult is IHaveRating ihr ? ihr.Rating : string.Empty;
}

using System.Text;
using AnimDL.Core.Models.Interfaces;
using Humanizer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Totoro.WinUI.Helpers;

public static class ModelHelpers
{
    public static string GetTimeOfAiring(this AiredEpisode episode)
    {
        if (episode is IHaveCreatedTime ihct)
        {
            return ihct.CreatedAt.Humanize();
        }

        return string.Empty;
    }

    public static Visibility GetTimeOfAiringVisibility(this AiredEpisode episode) => Converters.BooleanToVisibility(episode is IHaveCreatedTime);

    public static string GetImage(this SearchResult searchResult) => searchResult is IHaveImage ihi ? ihi.Image : string.Empty;
    public static ImageSource GetImageSource(this SearchResult searchResult)
    {
        if (searchResult is not IHaveImage ihi)
        {
            return null;
        }

        var uri = new Uri(ihi.Image, UriKind.Absolute);
        var imageSource = new BitmapImage { UriSource = uri };
        return imageSource;
    }

    public static string GetAdditionalInformation(this SearchResult searchResult)
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
    public static string GetRating(this SearchResult searchResult) => searchResult is IHaveRating ihr ? ihr.Rating : string.Empty;
}

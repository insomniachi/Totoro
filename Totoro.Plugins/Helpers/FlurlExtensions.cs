using Flurl;
using Flurl.Http;
using HtmlAgilityPack;

namespace Totoro.Plugins.Helpers;

public static class FlurlExtensions
{
    public const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.54";

    public static async Task<HtmlDocument> GetHtmlDocumentAsync(this string url)
    {
        var stream = await url.GetStreamAsync();
        var doc = new HtmlDocument();
        doc.Load(stream);
        return doc;
    }

    public static async Task<HtmlDocument> GetHtmlDocumentAsync(this IFlurlRequest request)
    {
        var stream = await request.GetStreamAsync();
        var doc = new HtmlDocument();
        doc.Load(stream);
        return doc;
    }

    public static async Task<HtmlDocument> GetHtmlDocumentAsync(this Task<IFlurlResponse> responseTask)
    {
        var response = await responseTask;
        var stream = await response.GetStreamAsync();
        var doc = new HtmlDocument();
        doc.Load(stream);
        return doc;
    }

    public static async Task<HtmlDocument> GetHtmlDocumentAsync(this Url url)
    {
        var stream = await url.GetStreamAsync();
        var doc = new HtmlDocument();
        doc.Load(stream);
        return doc;
    }

    public static IFlurlRequest WithReferer(this string url, string referrer)
    {
        return url.WithHeader(HeaderNames.Referer, referrer);
    }

    public static IFlurlRequest WithReferer(this IFlurlRequest request, string referrer)
    {
        return request.WithHeader(HeaderNames.Referer, referrer);
    }

    public static IFlurlRequest WithDefaultUserAgent(this string url)
    {
        return url.WithHeader(HeaderNames.UserAgent, USER_AGENT);
    }

    public static IFlurlRequest WithDefaultUserAgent(this IFlurlRequest request)
    {
        return request.WithHeader(HeaderNames.UserAgent, USER_AGENT);
    }

    public static IFlurlRequest WithDefaultUserAgent(this Url url)
    {
        return url.WithHeader(HeaderNames.UserAgent, USER_AGENT);
    }
}

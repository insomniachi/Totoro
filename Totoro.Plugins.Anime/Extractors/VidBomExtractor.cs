using System.Text.RegularExpressions;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Extractors;

public static partial class VidBomExtractor
{
    [GeneratedRegex(@"file:""(?<File>.*)""")]
    private static partial Regex SourcesRegex();

    public static async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        foreach (var match in SourcesRegex().Matches(doc.Text).OfType<Match>())
        {
            return new VideoStreamsForEpisode
            {
                Streams =
                {
                    new VideoStream
                    {
                        Url = match.Groups["File"].Value
                    }
                }
            };
        }

        return null;
    }
}

public static partial class UqLoadExtractor
{
    [GeneratedRegex(@"sources: \[.*\]")]
    private static partial Regex SourcesRegex();

    public static async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();

        var match = SourcesRegex().Match(doc.Text);

        if(!match.Success)
        {
            return null;
        }

        var stream = match.Value.Replace("sources: [\"", "").Replace("\"]", "");
        return new VideoStreamsForEpisode
        {
            Streams =
            {
                new VideoStream
                {
                    Url = stream,
                }
            },
        };
    }
}
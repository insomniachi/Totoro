using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl.Http;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Extractors;

public static partial class SoraPlayExtractor
{
    [GeneratedRegex(@"sources: (?<Sources>\[[^\]]+\])")]
    private static partial Regex SourcesRegex();

    public static async Task<VideoStreamsForEpisode?> Extract(string url, string referer = "")
    {
        var options = new JsonDocumentOptions { AllowTrailingCommas = true };
        var html = await url.WithReferer(referer).GetStringAsync();
        var sourcesJson = SourcesRegex().Match(html).Groups["Sources"].Value;
        var jsonNode = JsonNode.Parse(sourcesJson, documentOptions: options)!.AsArray();

        var streams = new VideoStreamsForEpisode();
        foreach (var node in jsonNode)
        {
            streams.Streams.Add(new VideoStream()
            {
                Url = node!["file"]!.ToString(),
                Resolution = node!["label"]!.ToString()
            });
        }

        return streams;
    }
}

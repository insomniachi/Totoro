using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Manga.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Manga.MangaDex;

internal class ChapterProvider : IChapterProvider
{
    public async IAsyncEnumerable<ChapterModel> GetChapters(string url)
    {
        var response = await ConfigManager<Config>.Current.Api
            .WithDefaultUserAgent()
            .AppendPathSegment($"/manga/{url}/feed")
            .SetQueryParam("translatedLanguage[]", "en")
            .SetQueryParam("order[chapter]", "asc")
            .GetStreamAsync();

        var jObject = JsonNode.Parse(response);

        foreach (var item in jObject?[@"data"]?.AsArray() ?? [])
        {
            var volume = float.Parse(item?["attributes"]?["volume"]?.ToString()!);
            var chapter = float.Parse(item?["attributes"]?["chapter"]?.ToString()!);
            var title = item?["attributes"]?["title"]?.ToString();
            var id = item?["id"]?.ToString();

            yield return new ChapterModel
            {
                Volume = volume,
                Id = id,
                Chapter = chapter,
                Title = title
            };
        }
    }

    public async IAsyncEnumerable<string> GetImages(ChapterModel chapterModel)
    {
        var response = await ConfigManager<Config>.Current.Api
            .WithDefaultUserAgent()
            .AppendPathSegment("/at-home/server")
            .AppendPathSegment(chapterModel.Id)
            .GetStreamAsync();

        var jObject = JsonNode.Parse(response);

        string? hash = jObject?["chapter"]?["hash"]?.ToString();

        foreach (string? name in jObject?["chapter"]?["data"]?.AsArray() ?? [])
        {
            yield return Url.Combine(jObject?["baseUrl"]?.ToString(), "data", hash, name);
        }
    }
}

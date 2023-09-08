using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Manga.Contracts;

namespace Totoro.Plugins.Manga.MangaDex;

internal class ChapterProvider : IChapterProvider
{
    public async IAsyncEnumerable<ChapterModel> GetChapters(string url)
    {
        var response = await Config.Api
            .WithDefaultUserAgent()
            .AppendPathSegment("manga")
            .AppendPathSegment(url)
            .AppendPathSegment("feed")
            .SetQueryParam("translatedLanguage[]", "en")
            .SetQueryParam("order[volume]", "asc")
            .SetQueryParam("order[chapter]", "asc")
            .GetStringAsync();

        var jObject = JsonNode.Parse(response);

        foreach (var item in jObject?["data"]?.AsArray() ?? new JsonArray())
        {
            var volume = int.Parse(item?["attributes"]?["volume"]?.ToString() ?? "0");
            var chapter = int.Parse(item?["attributes"]?["chapter"]?.ToString() ?? "0");
            var id = item?["id"]?.ToString();

            yield return new ChapterModel
            {
                Volume = volume,
                Id = id,
                Chapter = chapter
            };
        }
    }

    public async IAsyncEnumerable<string> GetImages(ChapterModel chapterModel)
    {
        var response = await Config.Api
            .WithDefaultUserAgent()
            .AppendPathSegment("/at-home/server")
            .AppendPathSegment(chapterModel.Id)
            .GetJsonAsync();

        string hash = response.chapter.hash;

        foreach (string name in response.chapter.data)
        {
            yield return Url.Combine(response.baseUrl, "data", hash, name);
        }
    }
}

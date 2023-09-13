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
            .AppendPathSegment($"/manga/{url}/feed")
            .SetQueryParam("translatedLanguage[]", "en")
            .SetQueryParam("order[chapter]", "asc")
            .GetJsonAsync();

        foreach (var item in response.data)
        {
            var volume = float.Parse(item.attributes.volume);
            var chapter = float.Parse(item.attributes.chapter);
            var title = item.attributes.title;
            var id = item.id;

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

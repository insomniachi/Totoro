using System.Text.Json.Nodes;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.AspNetCore.WebUtilities;

namespace Totoro.Core.Services.ShanaProject
{
    public class ShanaProjectService : IShanaProjectService
    {
        private readonly HttpClient _httpClient;

        public ShanaProjectService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<ShanaProjectCatalogItem>> Search(string term)
        {
            var url = QueryHelpers.AddQueryString("https://www.shanaproject.com/ajax/search/title/", new Dictionary<string, string>
            {
                ["term"] = term,
            });

            var stream = await _httpClient.GetStreamAsync(url);
            var jsonArrary = JsonNode.Parse(stream);
            return jsonArrary.Deserialize<List<ShanaProjectCatalogItem>>();
        }

        public async Task<ShanaProjectPage> Search(long id, int page = 1)
        {
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync($"https://www.shanaproject.com/series/{id}/{page}");

            var nodes = doc.QuerySelectorAll(".release_block");

            var result = new ShanaProjectPage
            {
                HasNextPage = doc.DocumentNode.InnerHtml.Contains($"/series/{id}/{page + 1}"),
                HasPreviousPage = doc.DocumentNode.InnerHtml.Contains($"/series/{id}/{page - 1}"),
                DownloadableContents = new List<ShanaProjectDownloadableContent>()
            };

            foreach (var releaseItem in nodes)
            {
                if (string.IsNullOrEmpty(releaseItem.Id) || releaseItem.Id?.Contains("rel") == false)
                {
                    continue;
                }

                var contentId = releaseItem.Id[3..];
                var title = releaseItem.SelectSingleNode("div[1]/div[4]/div[1]/text()").InnerText;
                var quality = releaseItem.SelectSingleNode("div[1]/div[4]/div[1]/span")?.InnerText;
                var ep = releaseItem.SelectSingleNode("div[1]/div[3]").InnerText;
                var size = releaseItem.SelectSingleNode("div[1]/div[6]").InnerText;
                var subber = releaseItem.SelectSingleNode("div[1]/div[5]/div/a[1]").InnerText;

                result.DownloadableContents.Add(new ShanaProjectDownloadableContent
                {
                    Title = title,
                    Episode = ep,
                    Quality = quality,
                    Size = size,
                    Subber = subber,
                    Url = $"https://www.shanaproject.com/download/{contentId}/"
                });
            }

            return result;
        }
    }
}

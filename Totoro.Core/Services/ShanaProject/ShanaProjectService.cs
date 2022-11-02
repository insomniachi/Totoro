using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
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
    }
}

using System.Text.Json;
using Totoro.Core.Torrents;
using Xunit.Abstractions;

namespace Totoro.Core.Tests.Services;

public class TorrentCatalogTests
{
    private readonly HttpClient _httpClient = new();
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    public TorrentCatalogTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("")]
    [InlineData("Attack on titan")]
    public async Task NyaaInk(string query)
    {
        var catalog = new NyaaCatalog(_httpClient, Mock.Of<ILocalSettingsService>());
        await foreach (var item in catalog.Search(query))
        {
            _testOutputHelper.WriteLine(JsonSerializer.Serialize(item, _serializerOptions));
        }
    }

    [Fact]
    public async Task ToshoSubtitles()
    {
        var catalog = new AnimeToshoCatalog(_httpClient);
        var result = await catalog.GetSubtitles("https://mirror.animetosho.org/view/subsplease-dungeon-ni-deai-wo-motomeru-no-wa.1746767");
    }
}

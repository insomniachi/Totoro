using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.AllAnime.Tests;

[ExcludeFromCodeCoverage]
public class CatalogTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };

    [Theory]
    [InlineData("hyouka")]
    [InlineData("hyakkano")]
    public async Task Search(string query)
    {
        // arrange
        var sut = new Catalog();

        // act
        var result = await sut.Search(query).ToListAsync();

        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.Aniwave.Tests;

[ExcludeFromCodeCoverage]
public class AiredEpisodesProviderTests
{
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };

    public AiredEpisodesProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetRecentlyAiredEpisodes()
    {
        // arrange
        var sut = new AiredEpisodesProvider();

        // act 
        var result = await sut.GetRecentlyAiredEpisodes(1).ToListAsync();

        //assert
        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }
}

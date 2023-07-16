using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Flurl;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.AnimeDao.Tests;

[ExcludeFromCodeCoverage]
public class StreamProviderTests
{
    public const string Hyouka = "hyouka";
    public const string Zom100 = "Zom100";
    public const string HeavenlyDelusion = "HeavenlyDelusion";

    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };
    private readonly Dictionary<string, string> _urlMap = new()
    {
        { Hyouka, Url.Combine(Config.Url, "/anime/hyouka/") },
        { Zom100, Url.Combine(Config.Url, "/anime/zom-100-bucket-list-of-the-dead/") },
        { HeavenlyDelusion, Url.Combine(Config.Url, "/anime/heavenly-delusion/") }
    };
    private readonly bool _allEpisodes = false;

    public StreamProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(Hyouka, 22)]
    public async Task GetNumberOfEpisodes(string key, int expected)
    {        
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var actual = await sut.GetNumberOfStreams(url);

        // assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Hyouka)]
    [InlineData(Zom100)]
    [InlineData(HeavenlyDelusion)]
    public async Task GetStreams(string key)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var result = await sut.GetStreams(url, _allEpisodes ? Range.All : 1..1).ToListAsync();

        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }
}

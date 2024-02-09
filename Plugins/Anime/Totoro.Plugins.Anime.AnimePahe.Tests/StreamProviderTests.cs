using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Flurl;
using Totoro.Plugins.Options;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.AnimePahe.Tests;

[ExcludeFromCodeCoverage]
public class StreamProviderTests
{
    public const string Hyouka = "hyouka";
    public const string OshiNoKo = "oshi no ko";
    public const string RentAGFS3 = "Rent a Girlfriend Season 3";
    public const string JjkS2 = "Jujutsu Kaisen 2nd Season";

    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };
    private readonly Dictionary<string, string> _urlMap = new()
    {
        { Hyouka, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/79997f02-ab7a-b12e-51bf-ca4454374da1") },
        { OshiNoKo, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/ae95c1fc-25d9-d824-1340-d46440e9652e") },
        { RentAGFS3, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/e53e054a-c233-f2ba-45d5-d762e183cc96") },
        { JjkS2, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/c24ef525-a643-7dc5-1882-a6b27b2421c2") }
    };

    private readonly bool _allEpisodes = false;

    public StreamProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(Hyouka, 22)]
    [InlineData(OshiNoKo, 10)]
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
    [InlineData(OshiNoKo)]
    [InlineData(RentAGFS3)]
    [InlineData(JjkS2)]
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

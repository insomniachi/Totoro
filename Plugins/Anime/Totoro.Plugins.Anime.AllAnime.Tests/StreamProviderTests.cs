using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Flurl;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Options;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.AllAnime.Tests;

[ExcludeFromCodeCoverage]
public class StreamProviderTests(ITestOutputHelper output)
{
    public const string Hyouka = "hyouka";
    public const string Hyakkano = "hyakkano";

    private readonly ITestOutputHelper _output = output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };
    private readonly Dictionary<string, string> _urlMap = new()
    {
        { Hyouka, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/dxxqKsaMhdrdQxczP") },
        { Hyakkano, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/pp9g8Qt7iem4RMjbJ") }
    };
    private readonly bool _allEpisodes = false;

    [Theory]
    [InlineData(Hyouka, 22)]
    [InlineData(Hyakkano, 12)]
    public async Task GetNumberOfEpisodes(string key, int expected)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var actual = await sut.GetNumberOfStreams(url, Models.StreamType.Subbed(Languages.English));

        // assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Hyouka)]
    [InlineData(Hyakkano)]
    public async Task GetStreams(string key)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var result = await sut.GetStreams(url, _allEpisodes ? Range.All : 12..12, Models.StreamType.Subbed(Languages.English)).ToListAsync();

        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }
}

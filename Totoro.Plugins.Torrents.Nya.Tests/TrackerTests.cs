using System.Text.Json;
using Xunit.Abstractions;

namespace Totoro.Plugins.Torrents.Nya.Tests;

public class TrackerTests
{
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };

    public TrackerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Recent()
    {
        // arrange
        var sut = new Tracker();

        // act
        var result = await sut.Recents().ToListAsync();

        // assert
        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, _searializerOption));
        }
    }

    [Theory]
    [InlineData("Demon Slayer")]
    public async Task Search(string query)
    {
        // arrange
        var sut = new Tracker();

        // act
        var result = await sut.Search(query).ToListAsync();

        // assert
        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, _searializerOption));
        }
    }

}

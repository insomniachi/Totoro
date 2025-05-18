using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.AnimePahe.Tests;

[ExcludeFromCodeCoverage]
public class CatalogTests
{
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };

    public CatalogTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("hyouka")]
    [InlineData("oshi no ko")]
    [InlineData("Rent a Girlfriend Season 3")]
    [InlineData("Jujutsu Kaisen Season 2")]
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

    [Fact]
	public async Task SearchNew()
    {
		var services = new ServiceCollection();
        var module = new Module();
        module.RegisterServices(services);
        services.AddTransient<IPluginConfiguration, FakePluginConfig>();
		var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredKeyedService<IAnimeProvider>(Module.AnimeHeaven.Id);

        var results = await catalog.SearchAsync("hyouka").ToListAsync();

	}
}

public class FakePluginConfig : IPluginConfiguration
{
	public JsonObject GetConfiguration(Guid id)
	{
		throw new NotImplementedException();
	}

	public void Update()
	{
		throw new NotImplementedException();
	}
}

using Totoro.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Plugins;
using Xunit;

namespace Totoro.Tests;

public class AnimePluginTesterTests
{
	private sealed class MockAnimeProvider : IAnimeProvider
	{
		public Guid Id { get; init; } = Guid.NewGuid();
		public string Name { get; init; } = "MockProvider";

		public Func<string, Task<List<ProviderShow>>>? OnSearch { get; init; }
		public Func<string, Task<List<ProviderEpisode>>>? OnGetEpisodes { get; init; }
		public Func<ProviderEpisode, Task<List<VideoServer>>>? OnGetServers { get; init; }

		public Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct) =>
			OnSearch?.Invoke(query) ?? Task.FromResult<List<ProviderShow>>([new("show-1", "Frieren: Beyond Journey's End", 1.0)]);

		public Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct) =>
			OnGetEpisodes?.Invoke(showId) ?? Task.FromResult<List<ProviderEpisode>>([new(showId, "ep-1", 1f), new(showId, "ep-2", 2f)]);

		public Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct) =>
			OnGetServers?.Invoke(episode) ?? Task.FromResult<List<VideoServer>>([new("1080p Direct", new Uri("https://example.com/stream.mp4"), VideoServerKind.Direct)]);
	}

	private sealed class MockFactory<TService>(params TService[] items) : IFactory<TService, Guid>
		where TService : notnull
	{
		public TService? Create(Guid id) => items.FirstOrDefault();
		public TService? CreateDefault() => items.FirstOrDefault();
		public IEnumerable<TService> CreateAll() => items;
		public Guid GetDefaultId() => Guid.Empty;
	}

	[Fact]
	public async Task TestProvider_AllStagesPass_ReturnsSuccess()
	{
		var provider = new MockAnimeProvider();
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", ct: CancellationToken.None);

		Assert.True(result.Passed);
		Assert.Equal("MockProvider", result.ProviderName);
		Assert.Equal(PluginTestStageStatus.Passed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Passed, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Passed, result.ServersStage.Status);
		Assert.NotNull(result.Shows);
		Assert.NotEmpty(result.Shows);
		Assert.NotNull(result.Episodes);
		Assert.NotEmpty(result.Episodes);
		Assert.NotNull(result.Servers);
		Assert.NotEmpty(result.Servers);
	}

	[Fact]
	public async Task TestProvider_SearchReturnsZeroShows_FailsAtSearchAndSkipsSubsequent()
	{
		var provider = new MockAnimeProvider
		{
			OnSearch = _ => Task.FromResult(new List<ProviderShow>())
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "NonExistentShow", ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Failed, result.SearchStage.Status);
		Assert.Contains("0 shows", result.SearchStage.Message);
		Assert.Equal(PluginTestStageStatus.Skipped, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestProvider_SearchThrowsException_FailsAtSearchAndSkipsSubsequent()
	{
		var provider = new MockAnimeProvider
		{
			OnSearch = _ => throw new HttpRequestException("Cloudflare challenge blocked request")
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Failed, result.SearchStage.Status);
		Assert.Contains("Cloudflare challenge", result.SearchStage.Message);
		Assert.Equal(PluginTestStageStatus.Skipped, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestProvider_GetEpisodesThrows_FailsAtEpisodesAndSkipsServers()
	{
		var provider = new MockAnimeProvider
		{
			OnGetEpisodes = _ => throw new InvalidOperationException("API rate limit exceeded")
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Passed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Failed, result.EpisodesStage.Status);
		Assert.Contains("API rate limit", result.EpisodesStage.Message);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestProvider_GetServersReturnsEmpty_FailsAtServers()
	{
		var provider = new MockAnimeProvider
		{
			OnGetServers = _ => Task.FromResult(new List<VideoServer>())
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Passed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Passed, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Failed, result.ServersStage.Status);
		Assert.Contains("No video servers", result.ServersStage.Message);
	}

	[Fact]
	public async Task TestProvider_EmptyQuery_FailsImmediately()
	{
		var provider = new MockAnimeProvider();
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "   ", ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Failed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Skipped, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestAllProviders_AggregatesResultsCorrectly()
	{
		var provider1 = new MockAnimeProvider { Name = "GoodProvider" };
		var provider2 = new MockAnimeProvider
		{
			Name = "BadProvider",
			OnSearch = _ => Task.FromResult(new List<ProviderShow>())
		};

		var factory = new MockFactory<IAnimeProvider>(provider1, provider2);
		var service = new AnimePluginTesterService(factory);

		var summary = await service.TestAllProvidersAsync("Frieren", ct: CancellationToken.None);

		Assert.Equal(2, summary.TotalCount);
		Assert.Equal(1, summary.PassedCount);
		Assert.Equal(1, summary.FailedCount);
		Assert.False(summary.AllPassed);
	}

	[Fact]
	public async Task AnimePluginTools_ExecutesSuccessfullyAndReturnsReport()
	{
		var provider = new MockAnimeProvider { Name = "TestPlugin" };
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);
		var tools = new AnimePluginTools(service);

		var report = await tools.TestAnimePluginsAsync("Frieren");

		Assert.Equal("Frieren", report.ShowTitle);
		Assert.Equal(1, report.TotalTested);
		Assert.Equal(1, report.PassedCount);
		Assert.Equal(0, report.FailedCount);
		var plugin = Assert.Single(report.Plugins);
		Assert.True(plugin.Passed);
		Assert.Equal("TestPlugin", plugin.Name);
	}

	[Fact]
	public async Task TestProvider_SearchTimesOut_FailsAndSkipsSubsequent()
	{
		var provider = new MockAnimeProvider
		{
			OnSearch = async _ =>
			{
				await Task.Delay(TimeSpan.FromSeconds(30));
				return [];
			}
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", timeout: TimeSpan.FromMilliseconds(50), ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Failed, result.SearchStage.Status);
		Assert.Contains("timed out", result.SearchStage.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(PluginTestStageStatus.Skipped, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestProvider_GetEpisodesTimesOut_FailsAndSkipsServers()
	{
		var provider = new MockAnimeProvider
		{
			OnGetEpisodes = async _ =>
			{
				await Task.Delay(TimeSpan.FromSeconds(30));
				return [];
			}
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", timeout: TimeSpan.FromMilliseconds(50), ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Passed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Failed, result.EpisodesStage.Status);
		Assert.Contains("timed out", result.EpisodesStage.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(PluginTestStageStatus.Skipped, result.ServersStage.Status);
	}

	[Fact]
	public async Task TestProvider_GetServersTimesOut_FailsAtServers()
	{
		var provider = new MockAnimeProvider
		{
			OnGetServers = async _ =>
			{
				await Task.Delay(TimeSpan.FromSeconds(30));
				return [];
			}
		};
		var factory = new MockFactory<IAnimeProvider>(provider);
		var service = new AnimePluginTesterService(factory);

		var result = await service.TestProviderAsync(provider, "Frieren", timeout: TimeSpan.FromMilliseconds(50), ct: CancellationToken.None);

		Assert.False(result.Passed);
		Assert.Equal(PluginTestStageStatus.Passed, result.SearchStage.Status);
		Assert.Equal(PluginTestStageStatus.Passed, result.EpisodesStage.Status);
		Assert.Equal(PluginTestStageStatus.Failed, result.ServersStage.Status);
		Assert.Contains("timed out", result.ServersStage.Message, StringComparison.OrdinalIgnoreCase);
	}
}

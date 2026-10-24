using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class AutoDownloadTests
{
	private static readonly DateTime s_now = new(2026, 10, 24, 12, 0, 0);

	[Fact]
	public void IsDue_ByTheAnimesSchedule()
	{
		var airing = new AnimeModel { AiringStatus = AiringStatus.CurrentlyAiring, AiredEpisodes = 4, NextEpisodeAt = s_now.AddHours(1) };
		Assert.True(AutoDownloader.IsDue(airing, 4, s_now));
		Assert.False(AutoDownloader.IsDue(airing, 5, s_now));
		Assert.True(AutoDownloader.IsDue(airing, 5, s_now.AddHours(2)));

		// Its own schedule says when each episode airs.
		airing.AiringSchedule = new Dictionary<int, DateTime> { [5] = s_now.AddMinutes(-1), [6] = s_now.AddDays(7) };
		Assert.True(AutoDownloader.IsDue(airing, 5, s_now));
		Assert.False(AutoDownloader.IsDue(airing, 6, s_now));

		Assert.True(AutoDownloader.IsDue(new AnimeModel { AiringStatus = AiringStatus.FinishedAiring, TotalEpisodes = 12 }, 12, s_now));
		// Not aired, and no time known.
		Assert.False(AutoDownloader.IsDue(new AnimeModel { AiringStatus = AiringStatus.NotYetAired }, 1, s_now));
	}

	[Fact]
	public void RetryDelay_BacksOffToFourHours()
	{
		Assert.Equal([15, 30, 60, 120, 240, 240], Enumerable.Range(1, 6).Select(x => AutoDownloader.RetryDelay(x).TotalMinutes));
	}

	[Fact]
	public void CreateStreaming_NeverDownloads()
	{
		var downloads = new Provider(DownloadsModule.Id);
		var streaming = new Provider(Guid.NewGuid());
		var factory = new Factory([downloads, streaming], defaultId: DownloadsModule.Id);

		// An anime's old setting, and the old default, naming Downloads are passed over.
		Assert.Same(streaming, factory.CreateStreaming(DownloadsModule.Id));
		Assert.Same(streaming, factory.CreateStreaming());
		Assert.Same(streaming, factory.CreateStreaming(streaming.Id));
		Assert.Null(new Factory([downloads], defaultId: DownloadsModule.Id).CreateStreaming());
	}

	private sealed class Provider(Guid id) : IAnimeProvider
	{
		public Guid Id => id;
		public string Name => id.ToString();
		public Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct) => Task.FromResult<List<ProviderShow>>([]);
		public Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct) => Task.FromResult<List<ProviderEpisode>>([]);
		public Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct) => Task.FromResult<List<VideoServer>>([]);
	}

	private sealed class Factory(IAnimeProvider[] items, Guid defaultId) : IFactory<IAnimeProvider, Guid>
	{
		public IAnimeProvider? Create(Guid id) => items.FirstOrDefault(x => x.Id == id);
		public IAnimeProvider? CreateDefault() => Create(defaultId);
		public IEnumerable<IAnimeProvider> CreateAll() => items;
		public Guid GetDefaultId() => defaultId;
	}
}

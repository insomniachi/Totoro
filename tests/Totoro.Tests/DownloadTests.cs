using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class DownloadTests
{
	[Fact]
	public void Names_RoundTrip()
	{
		Assert.Equal("Re Zero [Director's Cut]", DownloadNames.Folder("Re:Zero [Director's Cut]?"));

		Assert.Equal(5, DownloadNames.ParseEpisode(DownloadNames.Episode("Frieren", 5) + ".mkv"));
		Assert.Equal(112, DownloadNames.ParseEpisode("One Piece - 112.mp4"));
	}

	[Fact]
	public void SubtitleNames_RoundTripInOrder()
	{
		var folder = Directory.CreateTempSubdirectory().FullName;
		try
		{
			var episode = DownloadNames.Episode("Frieren", 5);
			var file = Path.Combine(folder, episode + ".mp4");
			File.WriteAllText(file, "");
			File.WriteAllText(Path.Combine(folder, DownloadNames.Subtitle(episode, 1, new SubtitleTrack("English", new Uri("https://x/a.vtt"), "en")) + ".vtt"), "");
			File.WriteAllText(Path.Combine(folder, DownloadNames.Subtitle(episode, 2, new SubtitleTrack("Arabic", new Uri("https://x/b.vtt"))) + ".vtt"), "");
			File.WriteAllText(Path.Combine(folder, episode + ".nfo"), "");

			Assert.Equal([("English", "en"), ("Arabic", null)], DownloadNames.FindSubtitles(file).Select(x => (x.Name, x.Language)));
			// Not episodes themselves.
			Assert.All(Directory.GetFiles(folder, "*.vtt"), x => Assert.Null(DownloadNames.ParseEpisode(x)));
		}
		finally
		{
			Directory.Delete(folder, recursive: true);
		}
	}

	[Fact]
	public void Describe_ShowsWhatsKnown()
	{
		var mb = 1 << 20;
		Assert.Null(DownloadService.Size(null));
		Assert.Equal("640 KB", DownloadService.Size(640 * 1024));
		Assert.Equal($"{1.5:0.0} GB", DownloadService.Size(1.5 * (1L << 30)));
		Assert.Equal($"{0.25:P0} · {120.0:0.0} MB of {480.0:0.0} MB · {6.0:0.0} MB/s",
					 DownloadService.Describe(new DownloadProgress(0.25, 6.0 * mb, 120.0 * mb, 480.0 * mb)));
		Assert.Equal($"{3.0:0.0} MB", DownloadService.Describe(new DownloadProgress(-1, null, 3.0 * mb)));
		Assert.Equal("Downloading…", DownloadService.Describe(new DownloadProgress(-1, null)));
	}

	[Fact]
	public async Task SpeedMeter_MeasuresSinceTheFirstSample()
	{
		var meter = new SpeedMeter();
		Assert.Null(meter.Next(0));
		await Task.Delay(500);
		Assert.InRange(meter.Next(1000)!.Value, 1000, 2100);
		// Started over: nothing to measure from yet.
		Assert.Null(meter.Next(10));
		Assert.Null(meter.Next(null));
	}

	[Fact]
	public async Task DownloadsProvider_FindsShowById()
	{
		var root = Path.Combine(Path.GetTempPath(), "Totoro.Tests", Guid.NewGuid().ToString("N"));
		var withIds = Directory.CreateDirectory(Path.Combine(root, "Sousou no Frieren")).FullName;
		File.WriteAllText(Path.Combine(withIds, DownloadNames.InfoFile), JsonSerializer.Serialize(new DownloadInfo("Sousou no Frieren", [], 154587, 52991)));
		Directory.CreateDirectory(Path.Combine(root, "Old Download"));
		var preferences = new PluginLoaderTests.FakePreferences();
		preferences.DownloadFolder = root;
		IAnimeProvider provider = new DownloadsProvider(preferences, NullLogger<DownloadsProvider>.Instance);

		Assert.Equal("Sousou no Frieren", (await provider.FindShowAsync(new AnimeId { Anilist = 154587 }, default))?.Id);
		Assert.Equal("Sousou no Frieren", (await provider.FindShowAsync(new AnimeId { MyAnimeList = 52991 }, default))?.Id);
		Assert.Null(await provider.FindShowAsync(new AnimeId { Anilist = 1 }, default));
		// A folder without ids is still found by title.
		var byTitle = await WatchService.SearchShowsAsync(provider, new AnimeModel { Title = "Old Download", ExternalIds = new AnimeId { Anilist = 1 } }, default);
		Assert.Equal("Old Download", Assert.Single(byTitle).Id);
	}

	[Fact]
	public void IdScore_MatchesByIdsTheyShare()
	{
		var anime = new AnimeId { Anilist = 154587, MyAnimeList = 52991 };
		Assert.Equal(1, WatchService.IdScore(new ProviderShow("a", "x", 0.2) { Ids = new AnimeId { Anilist = 154587 } }, anime));
		Assert.Equal(1, WatchService.IdScore(new ProviderShow("a", "x", 0.2) { Ids = new AnimeId { MyAnimeList = 52991 } }, anime));
		// Same title, another anime (a remake, a sequel).
		Assert.True(WatchService.IdScore(new ProviderShow("a", "x", 1) { Ids = new AnimeId { Anilist = 1 } }, anime) < 1);
		// No id in common: the title decides.
		Assert.Equal(0.7, WatchService.IdScore(new ProviderShow("a", "x", 0.7), anime));
		Assert.Equal(0.7, WatchService.IdScore(new ProviderShow("a", "x", 0.7) { Ids = new AnimeId { Kitsu = 5 } }, anime));
	}

	[Fact]
	public async Task CancelAll_DeletesWhatWasDownloadedSoFar()
	{
		var folder = Directory.CreateTempSubdirectory().FullName;
		var free = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		free.Start();
		var port = ((System.Net.IPEndPoint)free.LocalEndpoint).Port;
		free.Stop();
		using var listener = new System.Net.HttpListener { Prefixes = { $"http://localhost:{port}/" } };
		listener.Start();
		// A video that sends a little, then never finishes.
		_ = Task.Run(async () =>
		{
			var context = await listener.GetContextAsync();
			context.Response.ContentType = "video/mp4";
			context.Response.ContentLength64 = 1 << 30;
			await context.Response.OutputStream.WriteAsync(new byte[1 << 16]);
			await context.Response.OutputStream.FlushAsync();
			await Task.Delay(Timeout.Infinite);
		});
		try
		{
			var preferences = new PluginLoaderTests.FakePreferences();
			preferences.DownloadFolder = folder;
			using var downloads = new DownloadService(null!, null!, null!, null!, preferences, null!, NullLogger<DownloadService>.Instance);
			var item = downloads.Download(null, "Frieren", 5, new VideoServer("x", new Uri($"http://localhost:{port}/e.mp4")), "Test");
			var queued = downloads.Download(null, "Frieren", 6, new VideoServer("x", new Uri($"http://localhost:{port}/e.mp4")), "Test");
			var part = Path.Combine(item.Folder, DownloadNames.Episode("Frieren", 5) + ".part");
			for (var i = 0; i < 100 && !(File.Exists(part) && new FileInfo(part).Length > 0); i++)
			{
				await Task.Delay(100);
			}

			Assert.True(File.Exists(part));
			await downloads.CancelAllAsync(TimeSpan.FromSeconds(10));

			Assert.Equal(DownloadState.Cancelled, item.State);
			Assert.Equal(DownloadState.Cancelled, queued.State);
			Assert.Empty(Directory.GetFiles(item.Folder, "*.part*"));
		}
		finally
		{
			listener.Stop();
			Directory.Delete(folder, recursive: true);
		}
	}
}

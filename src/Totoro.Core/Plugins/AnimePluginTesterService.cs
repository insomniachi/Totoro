using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;

namespace Totoro.Core.Plugins;

public sealed class AnimePluginTesterService(
	IFactory<IAnimeProvider, Guid> providers,
	ILogger<AnimePluginTesterService>? logger = null) : IAnimePluginTesterService
{
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
	private readonly ILogger<AnimePluginTesterService> _logger = logger ?? NullLogger<AnimePluginTesterService>.Instance;

	public IReadOnlyList<IAnimeProvider> GetAvailableProviders()
	{
		return [.. providers.CreateAll().GroupBy(p => p.Id).Select(g => g.First())];
	}

	public async Task<AnimePluginTestResult> TestProviderAsync(
		IAnimeProvider provider,
		string showTitle,
		IProgress<PluginTestProgress>? progress = null,
		TimeSpan timeout = default,
		CancellationToken ct = default)
	{
		var effectiveTimeout = timeout > TimeSpan.Zero ? timeout : DefaultTimeout;
		var totalStopwatch = Stopwatch.StartNew();
		var query = showTitle.Trim();

		if (string.IsNullOrWhiteSpace(query))
		{
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: new PluginStageResult("Search", PluginTestStageStatus.Failed, "Show title cannot be empty.", TimeSpan.Zero),
				EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Skipped, "Skipped.", TimeSpan.Zero),
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped.", TimeSpan.Zero),
				TotalDuration: TimeSpan.Zero);
		}

		// Stage 1: Search
		progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "Search", PluginTestStageStatus.Running, $"Searching for \"{query}\"..."));
		var stageSw = Stopwatch.StartNew();
		List<ProviderShow>? shows = null;
		PluginStageResult searchStage;

		try
		{
			shows = await ExecuteWithTimeoutAsync(token => provider.SearchAsync(query, token), effectiveTimeout, ct);
			stageSw.Stop();

			if (shows is null || shows.Count == 0)
			{
				searchStage = new PluginStageResult("Search", PluginTestStageStatus.Failed, $"Search returned 0 shows for \"{query}\".", stageSw.Elapsed);
				progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "Search", PluginTestStageStatus.Failed, searchStage.Message));

				totalStopwatch.Stop();
				return new AnimePluginTestResult(
					provider.Id,
					provider.Name,
					query,
					Passed: false,
					SearchStage: searchStage,
					EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Skipped, "Skipped: Search found no shows.", TimeSpan.Zero),
					ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: Search found no shows.", TimeSpan.Zero),
					TotalDuration: totalStopwatch.Elapsed,
					Shows: shows ?? []);
			}

			var bestShow = shows.OrderByDescending(s => s.Score).First();
			searchStage = new PluginStageResult("Search", PluginTestStageStatus.Passed, $"Found {shows.Count} show(s). Selected: \"{bestShow.Title}\"", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "Search", PluginTestStageStatus.Passed, searchStage.Message));
		}
		catch (TimeoutException)
		{
			stageSw.Stop();
			_logger.LogWarning("Provider {Provider} SearchAsync timed out after {Timeout}s for {Query}", provider.Name, effectiveTimeout.TotalSeconds, query);
			searchStage = new PluginStageResult("Search", PluginTestStageStatus.Failed, $"Search timed out after {effectiveTimeout.TotalSeconds:0}s.", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "Search", PluginTestStageStatus.Failed, searchStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Skipped, "Skipped: Search timed out.", TimeSpan.Zero),
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: Search timed out.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			stageSw.Stop();
			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: new PluginStageResult("Search", PluginTestStageStatus.Failed, "Search cancelled.", stageSw.Elapsed),
				EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Skipped, "Skipped: Cancelled.", TimeSpan.Zero),
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: Cancelled.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed);
		}
		catch (Exception ex)
		{
			stageSw.Stop();
			_logger.LogWarning(ex, "Provider {Provider} SearchAsync failed for {Query}", provider.Name, query);
			searchStage = new PluginStageResult("Search", PluginTestStageStatus.Failed, $"Search error: {ex.Message}", stageSw.Elapsed, ex.ToString());
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "Search", PluginTestStageStatus.Failed, searchStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Skipped, "Skipped: Search failed.", TimeSpan.Zero),
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: Search failed.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows);
		}

		var targetShow = shows.OrderByDescending(s => s.Score).First();

		// Stage 2: GetEpisodes
		progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetEpisodes", PluginTestStageStatus.Running, $"Fetching episodes for \"{targetShow.Title}\"..."));
		stageSw.Restart();
		List<ProviderEpisode>? episodes = null;
		PluginStageResult episodesStage;

		try
		{
			episodes = await ExecuteWithTimeoutAsync(token => provider.GetEpisodesAsync(targetShow.Id, token), effectiveTimeout, ct);
			stageSw.Stop();

			if (episodes is null || episodes.Count == 0)
			{
				episodesStage = new PluginStageResult("GetEpisodes", PluginTestStageStatus.Failed, $"No episodes returned for \"{targetShow.Title}\".", stageSw.Elapsed);
				progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetEpisodes", PluginTestStageStatus.Failed, episodesStage.Message));

				totalStopwatch.Stop();
				return new AnimePluginTestResult(
					provider.Id,
					provider.Name,
					query,
					Passed: false,
					SearchStage: searchStage,
					EpisodesStage: episodesStage,
					ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: GetEpisodes returned no episodes.", TimeSpan.Zero),
					TotalDuration: totalStopwatch.Elapsed,
					Shows: shows,
					Episodes: episodes ?? []);
			}

			var testEpisode = episodes.FirstOrDefault(e => Math.Abs(e.Number - 1f) < 0.01f)
							  ?? episodes.OrderBy(e => e.Number).FirstOrDefault()
							  ?? episodes[0];

			episodesStage = new PluginStageResult("GetEpisodes", PluginTestStageStatus.Passed, $"Found {episodes.Count} episode(s). Selected: Episode {testEpisode.Number}", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetEpisodes", PluginTestStageStatus.Passed, episodesStage.Message));
		}
		catch (TimeoutException)
		{
			stageSw.Stop();
			_logger.LogWarning("Provider {Provider} GetEpisodesAsync timed out after {Timeout}s for show {ShowId}", provider.Name, effectiveTimeout.TotalSeconds, targetShow.Id);
			episodesStage = new PluginStageResult("GetEpisodes", PluginTestStageStatus.Failed, $"GetEpisodes timed out after {effectiveTimeout.TotalSeconds:0}s.", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetEpisodes", PluginTestStageStatus.Failed, episodesStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: GetEpisodes timed out.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			stageSw.Stop();
			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: new PluginStageResult("GetEpisodes", PluginTestStageStatus.Failed, "GetEpisodes cancelled.", stageSw.Elapsed),
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: Cancelled.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows);
		}
		catch (Exception ex)
		{
			stageSw.Stop();
			_logger.LogWarning(ex, "Provider {Provider} GetEpisodesAsync failed for show {ShowId}", provider.Name, targetShow.Id);
			episodesStage = new PluginStageResult("GetEpisodes", PluginTestStageStatus.Failed, $"GetEpisodes error: {ex.Message}", stageSw.Elapsed, ex.ToString());
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetEpisodes", PluginTestStageStatus.Failed, episodesStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Skipped, "Skipped: GetEpisodes failed.", TimeSpan.Zero),
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows,
				Episodes: episodes);
		}

		var targetEpisode = episodes.FirstOrDefault(e => Math.Abs(e.Number - 1f) < 0.01f)
							?? episodes.OrderBy(e => e.Number).FirstOrDefault()
							?? episodes[0];

		// Stage 3: GetServers
		progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetServers", PluginTestStageStatus.Running, $"Fetching video servers for Episode {targetEpisode.Number}..."));
		stageSw.Restart();
		List<VideoServer>? servers = null;
		PluginStageResult serversStage;

		try
		{
			servers = await ExecuteWithTimeoutAsync(token => provider.GetServersAsync(targetEpisode, token), effectiveTimeout, ct);
			stageSw.Stop();

			if (servers is null || servers.Count == 0)
			{
				serversStage = new PluginStageResult("GetServers", PluginTestStageStatus.Failed, $"No video servers returned for Episode {targetEpisode.Number}.", stageSw.Elapsed);
				progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetServers", PluginTestStageStatus.Failed, serversStage.Message));

				totalStopwatch.Stop();
				return new AnimePluginTestResult(
					provider.Id,
					provider.Name,
					query,
					Passed: false,
					SearchStage: searchStage,
					EpisodesStage: episodesStage,
					ServersStage: serversStage,
					TotalDuration: totalStopwatch.Elapsed,
					Shows: shows,
					Episodes: episodes,
					Servers: servers ?? []);
			}

			var serverNames = string.Join(", ", servers.Take(3).Select(s => $"{s.Name} ({s.Kind})"));
			if (servers.Count > 3)
			{
				serverNames += $", +{servers.Count - 3} more";
			}

			serversStage = new PluginStageResult("GetServers", PluginTestStageStatus.Passed, $"Found {servers.Count} server(s): {serverNames}", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetServers", PluginTestStageStatus.Passed, serversStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: true,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: serversStage,
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows,
				Episodes: episodes,
				Servers: servers);
		}
		catch (TimeoutException)
		{
			stageSw.Stop();
			_logger.LogWarning("Provider {Provider} GetServersAsync timed out after {Timeout}s for episode {EpisodeId}", provider.Name, effectiveTimeout.TotalSeconds, targetEpisode.Id);
			serversStage = new PluginStageResult("GetServers", PluginTestStageStatus.Failed, $"GetServers timed out after {effectiveTimeout.TotalSeconds:0}s.", stageSw.Elapsed);
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetServers", PluginTestStageStatus.Failed, serversStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: serversStage,
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows,
				Episodes: episodes);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			stageSw.Stop();
			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: new PluginStageResult("GetServers", PluginTestStageStatus.Failed, "GetServers cancelled.", stageSw.Elapsed),
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows,
				Episodes: episodes);
		}
		catch (Exception ex)
		{
			stageSw.Stop();
			_logger.LogWarning(ex, "Provider {Provider} GetServersAsync failed for episode {EpisodeId}", provider.Name, targetEpisode.Id);
			serversStage = new PluginStageResult("GetServers", PluginTestStageStatus.Failed, $"GetServers error: {ex.Message}", stageSw.Elapsed, ex.ToString());
			progress?.Report(new PluginTestProgress(provider.Id, provider.Name, "GetServers", PluginTestStageStatus.Failed, serversStage.Message));

			totalStopwatch.Stop();
			return new AnimePluginTestResult(
				provider.Id,
				provider.Name,
				query,
				Passed: false,
				SearchStage: searchStage,
				EpisodesStage: episodesStage,
				ServersStage: serversStage,
				TotalDuration: totalStopwatch.Elapsed,
				Shows: shows,
				Episodes: episodes,
				Servers: servers);
		}
	}

	public async Task<AnimePluginsTestSummary> TestAllProvidersAsync(
		string showTitle,
		IProgress<PluginTestProgress>? progress = null,
		TimeSpan timeout = default,
		CancellationToken ct = default)
	{
		var totalStopwatch = Stopwatch.StartNew();
		var providers = GetAvailableProviders();
		var results = new List<AnimePluginTestResult>();

		foreach (var provider in providers)
		{
			if (ct.IsCancellationRequested)
			{
				break;
			}

			var result = await TestProviderAsync(provider, showTitle, progress, timeout, ct);
			results.Add(result);
		}

		totalStopwatch.Stop();
		return new AnimePluginsTestSummary(showTitle, results, totalStopwatch.Elapsed);
	}

	private static async Task<T> ExecuteWithTimeoutAsync<T>(
		Func<CancellationToken, Task<T>> action,
		TimeSpan timeout,
		CancellationToken ct)
	{
		using var timeoutCts = new CancellationTokenSource();
		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

		var workTask = action(linkedCts.Token);
		var delayTask = Task.Delay(timeout, ct);

		var completed = await Task.WhenAny(workTask, delayTask);
		if (completed == delayTask)
		{
			timeoutCts.Cancel();
			ct.ThrowIfCancellationRequested();
			throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s.");
		}

		try
		{
			return await workTask;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s.");
		}
	}
}

using Totoro.Core.Modules.Streaming;

namespace Totoro.Core.Plugins;

public enum PluginTestStageStatus
{
	NotStarted,
	Running,
	Passed,
	Failed,
	Skipped
}

public sealed record PluginStageResult(
	string StageName,
	PluginTestStageStatus Status,
	string Message,
	TimeSpan Duration,
	string? ErrorDetails = null);

public sealed record AnimePluginTestResult(
	Guid ProviderId,
	string ProviderName,
	string Query,
	bool Passed,
	PluginStageResult SearchStage,
	PluginStageResult EpisodesStage,
	PluginStageResult ServersStage,
	TimeSpan TotalDuration,
	IReadOnlyList<ProviderShow>? Shows = null,
	IReadOnlyList<ProviderEpisode>? Episodes = null,
	IReadOnlyList<VideoServer>? Servers = null)
{
	public string Summary => Passed
		? $"{ProviderName}: Passed all stages (Search, Episodes, Servers) in {TotalDuration.TotalSeconds:F1}s"
		: $"{ProviderName}: Failed at {(SearchStage.Status == PluginTestStageStatus.Failed ? "Search" : EpisodesStage.Status == PluginTestStageStatus.Failed ? "Episodes" : "Servers")}";
}

public sealed record AnimePluginsTestSummary(
	string Query,
	IReadOnlyList<AnimePluginTestResult> Results,
	TimeSpan TotalDuration)
{
	public int TotalCount => Results.Count;
	public int PassedCount => Results.Count(r => r.Passed);
	public int FailedCount => Results.Count(r => !r.Passed);
	public bool AllPassed => TotalCount > 0 && FailedCount == 0;
}

public sealed record PluginTestProgress(
	Guid ProviderId,
	string ProviderName,
	string StageName,
	PluginTestStageStatus Status,
	string Message);

public interface IAnimePluginTesterService
{
	/// <summary>
	/// Returns all discovered/registered anime provider plugins.
	/// </summary>
	IReadOnlyList<IAnimeProvider> GetAvailableProviders();

	/// <summary>
	/// Tests an anime provider plugin by executing Search, GetEpisodes, and GetServers against a show title.
	/// </summary>
	Task<AnimePluginTestResult> TestProviderAsync(
		IAnimeProvider provider,
		string showTitle,
		IProgress<PluginTestProgress>? progress = null,
		TimeSpan timeout = default,
		CancellationToken ct = default);

	/// <summary>
	/// Tests all available anime provider plugins.
	/// </summary>
	Task<AnimePluginsTestSummary> TestAllProvidersAsync(
		string showTitle,
		IProgress<PluginTestProgress>? progress = null,
		TimeSpan timeout = default,
		CancellationToken ct = default);
}

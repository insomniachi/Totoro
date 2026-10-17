using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Plugins;

namespace Totoro.Agent;

/// <summary>Agent tools for testing anime provider plugins.</summary>
public sealed class AnimePluginTools(IAnimePluginTesterService tester)
{
	[AgentTool("test_anime_plugins")]
	[Description("Tests anime provider plugins by running Search, GetEpisodes, and GetServers against a show title, reporting which plugins passed and which failed.")]
	public async Task<AnimePluginTestReport> TestAnimePluginsAsync(
		[Description("The title of the anime to test with (e.g. 'Frieren', 'One Piece').")] string showTitle,
		[Description("Optional name of a specific provider plugin to test. If omitted or empty, all installed anime provider plugins are tested.")] string? providerName = null)
	{
		var providers = tester.GetAvailableProviders();
		if (providers.Count == 0)
		{
			throw new AgentToolException("No anime provider plugins are currently installed or loaded.");
		}

		if (!string.IsNullOrWhiteSpace(providerName))
		{
			var provider = providers.FirstOrDefault(p => p.Name.Equals(providerName.Trim(), StringComparison.OrdinalIgnoreCase))
				?? throw new AgentToolException($"No anime provider plugin named '{providerName}' was found. Available: {string.Join(", ", providers.Select(p => p.Name))}.");

			var result = await tester.TestProviderAsync(provider, showTitle, timeout: AnimePluginTesterService.DefaultTimeout, ct: CancellationToken.None);
			var reportItem = new AnimePluginProviderReport(
				result.ProviderName,
				result.Passed,
				result.SearchStage.Message,
				result.EpisodesStage.Message,
				result.ServersStage.Message,
				result.Passed ? null : (result.SearchStage.ErrorDetails ?? result.EpisodesStage.ErrorDetails ?? result.ServersStage.ErrorDetails ?? result.SearchStage.Message));

			return new AnimePluginTestReport(
				showTitle,
				TotalTested: 1,
				PassedCount: result.Passed ? 1 : 0,
				FailedCount: result.Passed ? 0 : 1,
				Plugins: [reportItem]);
		}

		var summary = await tester.TestAllProvidersAsync(showTitle, timeout: AnimePluginTesterService.DefaultTimeout, ct: CancellationToken.None);
		var reports = summary.Results.Select(r => new AnimePluginProviderReport(
			r.ProviderName,
			r.Passed,
			r.SearchStage.Message,
			r.EpisodesStage.Message,
			r.ServersStage.Message,
			r.Passed ? null : (r.SearchStage.ErrorDetails ?? r.EpisodesStage.ErrorDetails ?? r.ServersStage.ErrorDetails ?? r.SearchStage.Message))).ToList();

		return new AnimePluginTestReport(
			showTitle,
			summary.TotalCount,
			summary.PassedCount,
			summary.FailedCount,
			reports);
	}
}

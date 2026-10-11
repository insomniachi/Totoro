using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Navigation;
using Totoro.UI.Navigation;

namespace Totoro.Agent;

/// <summary>Agent tools for showing things in the app, e.g. what the agent is talking about.</summary>
public sealed class NavigationTools(AnimeLookup lookup, IEnumerable<NavigationItemViewModel> pages)
{
	// Navigator keys of the main window's page area and side pane (see MainWindow.xaml).
	private const string MainNavigatorKey = "main";
	private const string PaneNavigatorKey = "pane";

	[AgentTool("show_anime")]
	[Description("Opens an anime's details in the app's side pane, for the user to see.")]
	public async Task<string> ShowAnimeAsync(long animeId)
	{
		var anime = await lookup.GetAsync(animeId);
		if (!Navigators.TryGetNavigator(PaneNavigatorKey, out var pane))
		{
			throw new AgentToolException("The app's window isn't ready.");
		}

		await pane.NavigateToDataAsync(anime);
		return $"Showing {anime.Title}.";
	}

	[AgentTool("open_page")]
	[Description("Opens one of the app's pages.")]
	public async Task<string> OpenPageAsync([Description("The page: one of get_pages.")] string page)
	{
		var item = pages.FirstOrDefault(x => x.Label.Equals(page, StringComparison.OrdinalIgnoreCase))
			?? throw new AgentToolException($"There's no page called {page}; they're {string.Join(", ", pages.Select(x => x.Label))}.");
		if (!Navigators.TryGetNavigator(MainNavigatorKey, out var main))
		{
			throw new AgentToolException("The app's window isn't ready.");
		}

		await main.NavigateToViewModelAsync(item.ViewModelType);
		return $"Opened {item.Label}.";
	}

	[AgentTool("get_pages")]
	[Description("The app's pages, which open_page opens.")]
	public IReadOnlyList<string> GetPages() => [.. pages.Select(x => x.Label)];
}

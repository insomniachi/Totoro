using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Navigation;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

namespace Totoro.Agent;

/// <summary>Agent tools for showing things in the app, e.g. what the agent is talking about.</summary>
public sealed class NavigationTools(AnimeLookup lookup, IEnumerable<NavigationItemViewModel> pages, MainWindowViewModel window)
{
	// Navigator keys of the main window's page area and side pane (see MainWindow.xaml).
	private const string MainNavigatorKey = "main";
	private const string PaneNavigatorKey = "pane";

	[AgentTool("open_anime_details")]
	[Description("Opens an anime's details page in the app's side pane. Only when the user asks to open it or see its details: anime the other tools return are already shown to them in the chat.")]
	public async Task<string> OpenAnimeDetailsAsync(long animeId)
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
		var item = pages.FirstOrDefault(x => x.Name.Equals(page, StringComparison.OrdinalIgnoreCase))
			?? throw new AgentToolException($"There's no page called {page}; they're {string.Join(", ", pages.Select(x => x.Name))}.");
		if (!Navigators.TryGetNavigator(MainNavigatorKey, out var main))
		{
			throw new AgentToolException("The app's window isn't ready.");
		}

		// A page of the other mode: the app switches to it, so the menu shows the page.
		if (item.Mode is { } mode && mode != window.Mode)
		{
			window.SwitchMode(mode, item);
			return $"Opened {item.Name}.";
		}

		await main.NavigateToViewModelAsync(item.ViewModelType);
		return $"Opened {item.Name}.";
	}

	[AgentTool("get_pages")]
	[Description("The app's pages, which open_page opens.")]
	public IReadOnlyList<string> GetPages() => [.. pages.Select(x => x.Name)];
}

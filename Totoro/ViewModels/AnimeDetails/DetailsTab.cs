using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.ViewModels.AnimeDetails;

/// <summary>
/// A tab of the anime details pane. A tab whose content comes with the details fills itself in directly; one that
/// fetches its own data derives from <see cref="LazyDetailsTab"/> instead, which loads the first time it's shown.
/// Add a tab by deriving from either, exposing it from <see cref="AnimeDetailsViewModel"/>, adding a view for it
/// under Views/AnimeDetails, and adding a SelectorBarItem and a matching case (hosting the view) to the page.
/// </summary>
public abstract partial class DetailsTab : ObservableObject
{
	/// <summary>False while the tab's content is loading; the view shows a placeholder until then.</summary>
	[ObservableProperty]
	public partial bool IsLoaded { get; protected set; } = true;

	/// <summary>Shown when there's nothing to list, or loading failed; null when there's content.</summary>
	[ObservableProperty]
	public partial string? Message { get; protected set; }

	/// <summary>Called whenever the tab is selected; loads the tab's own data, if it has any.</summary>
	public virtual Task EnsureLoadedAsync() => Task.CompletedTask;

	protected static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

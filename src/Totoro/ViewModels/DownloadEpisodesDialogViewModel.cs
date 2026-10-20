using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.Playback;

namespace Totoro.ViewModels;

/// <param name="Provider">The provider to download from, set to use the settings chosen for this download.</param>
public sealed record DownloadRequest(EpisodeRange Episodes, IAnimeProvider Provider);

/// <summary>
/// Asks which of an anime's episodes to download (from the next one the user hasn't watched to the latest that's aired),
/// from which provider, and with what settings for it: the anime's own for that provider to start with, changed for this
/// download only. Checks the provider has the anime before closing. Downloading them is up to the caller (see
/// <see cref="DownloadService.DownloadEpisodesAsync"/>).
/// </summary>
public sealed partial class DownloadEpisodesDialogViewModel : DialogViewModel<DownloadRequest>
{
	private readonly AnimeModel _anime;
	private readonly WatchService _watch;
	private readonly IFactory<IAnimeProvider, Guid> _providers;

	// Each provider's settings as edited, by provider id, so switching provider and back keeps the edits.
	private readonly Dictionary<Guid, (DataContainerConfig Config, DataContainer Container)?> _drafts = [];

	// The provider it was said to have nothing for: Download again searches it by hand (the show picker) anyway.
	private Guid? _warnedProvider;

	public DownloadEpisodesDialogViewModel(AnimeModel anime, WatchService watch, IFactory<IAnimeProvider, Guid> providers, IEnumerable<Descriptor> modules)
	{
		_anime = anime;
		_watch = watch;
		_providers = providers;
		Title = $"Download {anime.Title}";
		PrimaryButtonText = "Download";

		// The aired episodes when known; otherwise any number (the provider says which it has).
		var last = EpisodeAvailability.AiredEpisodes(anime, DateTime.Now) is > 0 and var aired ? aired : anime.TotalEpisodes ?? 0;
		MaxEpisode = last > 0 ? last : double.MaxValue;
		var next = WatchService.NextEpisode(anime);
		From = last > 0 ? Math.Min(next, last) : next;
		To = last > 0 ? last : From;

		// Not the Downloads provider: what it has is downloaded already.
		Providers = [.. providers.CreateAll().Where(x => x.Id != DownloadsModule.Id)
								 .Select(x => new ProviderChoice(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		HasProviders = Providers.Count > 0;
		var own = watch.GetProvider(anime)?.Id;
		SelectedProvider = Providers.FirstOrDefault(x => x.Id == own) ?? Providers.FirstOrDefault();
		Validate();
	}

	/// <summary>The latest episode that's aired, or unbounded when that isn't known.</summary>
	public double MaxEpisode { get; }

	/// <summary>The providers to download from: the anime's own selected to start with.</summary>
	public IReadOnlyList<ProviderChoice> Providers { get; }

	public bool HasProviders { get; }

	[ObservableProperty] public partial ProviderChoice? SelectedProvider { get; set; }

	/// <summary>The provider's settings for this download; null if it has none.</summary>
	[ObservableProperty] public partial DataContainer? ProviderSettings { get; private set; }

	[ObservableProperty] public partial bool HasProviderSettings { get; private set; }

	// double rather than int: NumberBox works in doubles and uses NaN for an empty box.
	[ObservableProperty]
	public partial double From { get; set; }

	[ObservableProperty]
	public partial double To { get; set; }

	/// <summary>e.g. "Episodes 5 to 12 (8)", or why the range can't be downloaded; never null.</summary>
	[ObservableProperty]
	public partial string Summary { get; private set; } = "";

	/// <summary>Whether it's looking for the anime on the provider.</summary>
	[ObservableProperty] public partial bool IsChecking { get; private set; }

	/// <summary>Why it can't download from the provider; "" when it can.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string Error { get; private set; } = "";

	public bool HasError => Error.Length > 0;

	partial void OnFromChanged(double value) => Validate();

	partial void OnToChanged(double value) => Validate();

	partial void OnSelectedProviderChanged(ProviderChoice? value)
	{
		Error = "";
		var draft = value?.Id is { } id ? GetDraft(id) : null;
		ProviderSettings = draft?.Container;
		HasProviderSettings = draft is not null;
	}

	private (DataContainerConfig Config, DataContainer Container)? GetDraft(Guid id)
	{
		if (!_drafts.TryGetValue(id, out var draft))
		{
			var config = _watch.CopyProviderSettings(_anime, id);
			var container = config?.ToDataContainer();
			_drafts[id] = draft = config is not null && container is { Count: > 0 } ? (config, container) : null;
		}

		return draft;
	}

	private void Validate()
	{
		var range = !double.IsNaN(From) && !double.IsNaN(To) && From >= 1 && To >= From;
		CanConfirm = range && HasProviders && !IsChecking;
		Summary = !HasProviders
			? "There's no anime provider to download from: install one from the Plugins page."
			: !range
				? "Choose the first episode, then a last one that isn't before it."
				: From == To
					? $"Episode {From}"
					: $"Episodes {From} to {To} ({To - From + 1}), one after the other";
	}

	public override async Task<bool> ConfirmAsync()
	{
		if (SelectedProvider?.Id is not { } id || _providers.Create(id) is not { } provider)
		{
			return false;
		}

		if (GetDraft(id) is var (config, container))
		{
			config.UpdateValues(container);
			provider.UseSettings(config);
		}

		Error = "";
		IsChecking = true;
		Validate();
		try
		{
			var (show, candidates) = await _watch.FindShowAsync(provider, _anime, CancellationToken.None);
			if (show is null && candidates.Count == 0 && _warnedProvider != id)
			{
				_warnedProvider = id;
				Error = $"{provider.Name} has nothing called {_anime.Title}. Choose another provider, or press Download again to search {provider.Name} yourself.";
				return false;
			}
		}
		catch (Exception ex)
		{
			Error = $"Couldn't search {provider.Name}: {ex.Message}. Choose another provider, or try again.";
			return false;
		}
		finally
		{
			IsChecking = false;
			Validate();
		}

		Result = new DownloadRequest(new EpisodeRange((int)From, (int)To), provider);
		return true;
	}
}

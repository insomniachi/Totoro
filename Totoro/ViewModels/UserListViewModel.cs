using System.Collections.ObjectModel;
using DynamicData;
using DynamicData.Binding;
using Totoro.Core.Filtering;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

public partial class UserListViewModel : NavigatableViewModel
{
	private readonly IFactory<IAnimeTracker, Guid> _factory;
	private readonly SourceCache<AnimeModel, long> _source = new(x => x.Id);
	private readonly ReadOnlyObservableCollection<AnimeModel> _anime;

	// Position of each item in the list the tracker returned, so filtering keeps that order.
	private readonly Dictionary<long, int> _order = [];

	public UserListViewModel(IFactory<IAnimeTracker, Guid> factory)
	{
		_factory = factory;

		// Changes are pushed on the UI thread (the list is loaded from OnNavigatedToAsync and the
		// filters change from bindings), so no ObserveOn is needed before binding.
		// ResetThreshold is maxed so the view always gets individual adds/removes, never a Reset:
		// the ItemsRepeater can only animate cards moving into place for individual changes.
		_source.Connect()
			   .AutoRefresh(x => x.Tracking)
			   .Filter(Filters.Combine())
			   .SortAndBind(out _anime,
							SortExpressionComparer<AnimeModel>.Ascending(x => _order.GetValueOrDefault(x.Id, int.MaxValue)),
							new SortAndBindOptions { ResetThreshold = int.MaxValue })
			   .Subscribe();
	}

	public ListStatusFilter StatusFilter { get; } = new();

	/// <summary>Filtered view of the user's list, for the view to bind to.</summary>
	public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;

	/// <summary>Every filter applied to the list; an item has to pass all of them. Add new filters here.</summary>
	private IEnumerable<IFilter<AnimeModel>> Filters => [StatusFilter];

	protected override async Task OnNavigatedToInternalAsync()
	{
		var tracker = _factory.CreateDefault();

		if (tracker is null)
		{
			return;
		}

		var anime = await tracker.GetUserListAsync(CancellationToken.None);

		_order.Clear();
		foreach (var item in anime)
		{
			_order.TryAdd(item.Id, _order.Count);
		}

		_source.Edit(cache =>
		{
			cache.Clear();
			cache.AddOrUpdate(anime);
		});
	}
}

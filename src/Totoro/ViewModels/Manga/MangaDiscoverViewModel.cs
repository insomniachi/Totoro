using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Manga;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels.Manga;

/// <summary>Manga mode's Discover: what's trending and what's popular in the manga database.</summary>
public partial class MangaDiscoverViewModel(IFactory<IMangaMetadataProvider, Guid> databases) : NavigatableViewModel
{
	[ObservableProperty]
	public partial List<MangaModel> Trending { get; set; } = [];

	[ObservableProperty]
	public partial List<MangaModel> Popular { get; set; } = [];

	/// <summary>Why nothing loaded; "" if it did.</summary>
	[ObservableProperty]
	public partial string Error { get; set; } = "";

	protected override Task OnNavigatedToInternalAsync() => LoadAsync();

	[RelayCommand]
	private async Task LoadAsync()
	{
		Error = "";
		if (databases.CreateDefault() is not { } database)
		{
			Error = "There's no manga database. Pick one in Settings › Accounts.";
			return;
		}

		try
		{
			var trending = database.GetTrendingMangaAsync(CancellationToken.None);
			var popular = database.GetPopularMangaAsync(CancellationToken.None);
			Trending = await trending;
			Popular = await popular;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Error = $"Couldn't reach {database.Name}; it may be down, or you may be offline. ({ex.Message})";
		}
	}
}

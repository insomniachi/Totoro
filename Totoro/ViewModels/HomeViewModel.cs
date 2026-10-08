using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Navigation;

namespace Totoro.ViewModels;

public partial class HomeViewModel(IFactory<IAnimeTracker, Guid> factory) : ObservableObject, INavigationAware
{
	public Task OnNavigatedFrom()
	{
		return Task.CompletedTask;
	}

	public async Task OnNavigatedTo()
	{
		var tracker = factory.CreateDefault()!;
		Anime = await tracker.GetUserList(CancellationToken.None);
	}

	[ObservableProperty]
	public partial List<AnimeModel> Anime { get; set; }
}

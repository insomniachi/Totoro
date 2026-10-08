using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

public partial class UserListViewModel(IFactory<IAnimeTracker, Guid> factory) : NavigatableViewModel
{
	protected override async Task OnNavigatedToInternal()
	{
		var tracker = factory.CreateDefault();

		if(tracker is null)
		{
			return;
		}

		Anime = await tracker.GetUserList(CancellationToken.None);
	}


	[ObservableProperty]
	public partial List<AnimeModel> Anime { get; set; }
}

using ReactiveUI.Maui;
using Totoro.Core.Models;
using Totoro.Core.ViewModels;

namespace Totoro.Maui.Views;

[QueryProperty(nameof(View), "View")]
public partial class UserListPage : ReactiveContentPage<UserListViewModel>
{
	public AnimeStatus View
	{
		get => ViewModel.CurrentView;
		set => ViewModel.CurrentView = value;
	}

	public UserListPage(UserListViewModel vm)
	{
		InitializeComponent();
		ViewModel = vm;
	}

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
		await ViewModel.SetInitialState();
    }
}
using ReactiveUI.Maui;
using Totoro.Core.ViewModels;

namespace Totoro.Maui.Views;


public partial class DiscoverPage : ReactiveContentPage<DiscoverViewModel>
{
	public DiscoverPage(DiscoverViewModel vm)
	{
		InitializeComponent();
		ViewModel = vm;
	}

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
		await ViewModel.OnNavigatedTo(new Dictionary<string, object>());
    }
}
using ReactiveUI.Maui;
using Totoro.Maui.ViewModels;

namespace Totoro.Maui.Views;

public partial class LoginMALPage : ReactiveContentPage<LoginMALViewModel>
{
	public LoginMALPage(LoginMALViewModel vm)
	{
		InitializeComponent();
		ViewModel = vm;
	}

    private void WebView_Navigated(object sender, WebNavigatedEventArgs e)
    {
		ViewModel.AuthUrl = e.Url;
    }
}
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Totoro.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace Totoro.Views;

public sealed partial class AdvancedSearchPage : Page
{
	public AdvancedSearchPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AdvancedSearchViewModel? ViewModel { get; set; }

	private bool IsImageTab => ReferenceEquals(Tabs.SelectedItem, ImageTab);

	private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		TitleView.Visibility = IsImageTab ? Visibility.Collapsed : Visibility.Visible;
		ImageView.Visibility = PasteButton.Visibility = IsImageTab ? Visibility.Visible : Visibility.Collapsed;
	}

	// An image searches with it, from either tab; anything else is only the Image tab's to say there's no image. A paste
	// into a text box (the title filter, the title bar's search) is the text box's.
	private void OnPasteInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
	{
		if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or RichEditBox or PasswordBox || ViewModel is null ||
			!IsImageTab && !Clipboard.GetContent().Contains(StandardDataFormats.Bitmap))
		{
			return;
		}

		args.Handled = true;
		Tabs.SelectedItem = ImageTab;
		ViewModel.ImageSearch.PasteCommand.Execute(null);
	}
}

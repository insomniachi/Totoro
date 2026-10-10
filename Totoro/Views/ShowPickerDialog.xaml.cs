using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.Playback;

namespace Totoro.Views;

public sealed partial class ShowPickerDialog : UserControl
{
	public ShowPickerDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ShowPickerDialogViewModel? ViewModel { get; set; }

	private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		ViewModel?.SearchCommand.Execute(null);
	}
}

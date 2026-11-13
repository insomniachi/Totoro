using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Playback;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DownloadsPage : Page
{
	public DownloadsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial DownloadsViewModel? ViewModel { get; set; }

	private void OnSectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		var library = sender.SelectedItem == LibrarySection;
		LibraryPanel.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
		SessionPanel.Visibility = library ? Visibility.Collapsed : Visibility.Visible;
	}

	/// <summary>Asks which downloaded episode to play, the one to continue from by its tracking selected.</summary>
	private async void OnPlayClick(object sender, RoutedEventArgs e)
	{
		if (ViewModel is not { } viewModel)
		{
			return;
		}

		var button = (Button)sender;
		var anime = (DownloadedAnime)button.Tag;
		// Its tracking may be fetched first, which takes a moment.
		button.IsEnabled = false;
		var next = await viewModel.NextDownloadedAsync(anime);
		button.IsEnabled = true;

		PlayDialog.Title = anime.Title;
		EpisodeGrid.ItemsSource = anime.EpisodeNumbers.Select(x => x.ToString(CultureInfo.CurrentCulture)).ToList();
		EpisodeGrid.SelectedIndex = next is { } number ? anime.EpisodeNumbers.ToList().IndexOf(number) : -1;
		PlayDialog.IsPrimaryButtonEnabled = EpisodeGrid.SelectedIndex >= 0;
		var result = await PlayDialog.ShowAsync();
		if (result == ContentDialogResult.Primary || _playOnHide)
		{
			_ = viewModel.PlayAsync(anime, anime.EpisodeNumbers[EpisodeGrid.SelectedIndex]);
		}
		else if (result == ContentDialogResult.Secondary)
		{
			_ = viewModel.ContinueAsync(anime);
		}

		_playOnHide = false;
	}

	// Set when an episode's double-clicked: the dialog's hidden, its result None, and the episode played all the same.
	private bool _playOnHide;

	private void OnEpisodeSelectionChanged(object sender, SelectionChangedEventArgs e) =>
		PlayDialog.IsPrimaryButtonEnabled = EpisodeGrid.SelectedIndex >= 0;

	private void OnEpisodeDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
	{
		if (EpisodeGrid.SelectedIndex >= 0)
		{
			_playOnHide = true;
			PlayDialog.Hide();
		}
	}

	private void OnEditInfoClick(object sender, RoutedEventArgs e) => _ = ViewModel?.EditInfoAsync((DownloadedAnime)((FrameworkElement)sender).Tag);

	private void OnFindSegmentsClick(object sender, RoutedEventArgs e) => _ = ViewModel?.FindSegmentsAsync((DownloadedAnime)((FrameworkElement)sender).Tag);

	private void OnDeleteClick(object sender, RoutedEventArgs e)
	{
		// The confirmation flyout the button's in: its anime is about to go from the list.
		foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
		{
			popup.IsOpen = false;
		}

		ViewModel?.Delete((DownloadedAnime)((FrameworkElement)sender).Tag);
	}

	public static Visibility VisibleIfNone(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsUnknown(double progress) => progress < 0;

	public static double Percent(double progress) => Math.Max(0, progress) * 100;
}

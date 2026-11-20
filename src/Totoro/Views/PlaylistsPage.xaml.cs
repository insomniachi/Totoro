using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Totoro.Core.Services;
using Totoro.ViewModels;
using Windows.System;

namespace Totoro.Views;

public sealed partial class PlaylistsPage : Page
{
	public PlaylistsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PlaylistsViewModel? ViewModel { get; set; }

	private void OnCreateClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.Create(CreateName.Text);
		CreateName.Text = "";
		CreateFlyout.Hide();
	}

	private void OnCreateKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter)
		{
			OnCreateClick(sender, e);
		}
	}

	private void OnRenameOpening(object sender, object e) => RenameName.Text = ViewModel?.Selected?.Name ?? "";

	private void OnRenameClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.Rename(RenameName.Text);
		RenameFlyout.Hide();
	}

	private void OnRenameKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter)
		{
			OnRenameClick(sender, e);
		}
	}

	private void OnDeleteClick(object sender, RoutedEventArgs e)
	{
		DeleteFlyout.Hide();
		ViewModel?.Delete();
	}

	private void OnPlayClick(object sender, RoutedEventArgs e) => ViewModel?.Play(shuffle: false);

	private void OnShuffleClick(object sender, RoutedEventArgs e) => ViewModel?.Play(shuffle: true);

	private void OnSongClick(object sender, ItemClickEventArgs e) => ViewModel?.PlayFrom((PlaylistSong)e.ClickedItem);

	private void OnRemoveClick(object sender, RoutedEventArgs e) => ViewModel?.Remove((PlaylistSong)((FrameworkElement)sender).Tag);

	private void OnAddSongTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			_ = ViewModel?.SearchAsync(sender.Text);
		}
	}

	/// <summary>Adds the song picked (or, on Enter, the first found), then clears the box for the next.</summary>
	private void OnAddSongSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (ViewModel is null || (args.ChosenSuggestion as SongMatch ?? ViewModel.Found.FirstOrDefault()) is not { } match)
		{
			return;
		}

		ViewModel.Add(match);
		sender.Text = "";
		_ = ViewModel.SearchAsync("");
	}

	private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
	{
		if (args.Items.FirstOrDefault() is PlaylistSong song)
		{
			ViewModel?.Moved(song);
		}
	}

	public static string SongCount(int count) => count == 1 ? "1 song" : $"{count} songs";

	/// <summary>e.g. "OP2 · Oshi no Ko · YOASOBI".</summary>
	public static string SongDetails(string label, string anime, string artist) =>
		string.Join(" · ", new[] { label, anime, artist }.Where(x => x.Length > 0));

	public static string SongName(string label, string title, string anime) => $"{label}: {title}, {anime}";

	public static Visibility VisibleIfNone(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
}

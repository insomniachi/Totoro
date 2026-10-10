using System.ComponentModel;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>
/// Shows a shimmer skeleton until the details and the banner and cover images have all loaded, then reveals everything at
/// once, so nothing visibly changes after the content appears.
/// </summary>
public sealed partial class AnimeDetailsPage : Page
{
	// Don't wait on a slow or stuck image forever; show what we have.
	private static readonly TimeSpan s_imageTimeout = TimeSpan.FromSeconds(3);

	private bool _bannerDone;
	private bool _coverDone;
	private bool _revealed;
	private DispatcherQueueTimer? _imageTimeout;

	public AnimeDetailsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimeDetailsViewModel? ViewModel { get; set; }

	partial void OnViewModelChanged(AnimeDetailsViewModel? newValue)
	{
		if (newValue is null)
		{
			return;
		}

		newValue.PropertyChanged += OnViewModelPropertyChanged;
		if (newValue.IsLoaded)
		{
			OnDetailsLoaded(newValue);
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AnimeDetailsViewModel.IsLoaded) && sender is AnimeDetailsViewModel { IsLoaded: true } viewModel)
		{
			OnDetailsLoaded(viewModel);
		}
	}

	private void OnDetailsLoaded(AnimeDetailsViewModel viewModel)
	{
		// The image sources were set just before IsLoaded; an empty one never raises ImageOpened/ImageFailed.
		_bannerDone |= string.IsNullOrEmpty(viewModel.BannerImage);
		_coverDone |= string.IsNullOrEmpty(viewModel.CoverImage);

		_imageTimeout = DispatcherQueue.CreateTimer();
		_imageTimeout.Interval = s_imageTimeout;
		_imageTimeout.IsRepeating = false;
		_imageTimeout.Tick += (_, _) => Reveal();
		_imageTimeout.Start();

		TryReveal();
	}

	private void OnImageDone(object sender, RoutedEventArgs e)
	{
		if (ReferenceEquals(sender, BannerImageElement))
		{
			_bannerDone = true;
		}
		else if (ReferenceEquals(sender, CoverImageElement))
		{
			_coverDone = true;
		}

		TryReveal();
	}

	private void TryReveal()
	{
		if (ViewModel is { IsLoaded: true } && _bannerDone && _coverDone)
		{
			Reveal();
		}
	}

	private void Reveal()
	{
		if (_revealed)
		{
			return;
		}

		_revealed = true;
		_imageTimeout?.Stop();
		DetailsContent.Opacity = 1;
		DetailsContent.IsHitTestVisible = true;
		LoadingSkeleton.Visibility = Visibility.Collapsed;
	}

	private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (ViewModel is not null && sender.SelectedItem?.Tag is string tab)
		{
			ViewModel.SelectedTab = tab;
		}
	}
}

using System.ComponentModel;
using System.Numerics;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>
/// Shows a shimmer of the page until the details and the banner and cover images have all loaded, then reveals everything at
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
		Loaded += (_, _) => AnimateShyHeader();
	}

	// How tall the header is once collapsed, and how small the cover gets.
	private const float CollapsedHeaderHeight = 112;
	private const float CollapsedCoverScale = 1f / 3;

	// The header collapses over the first `range` of scrolling (y, progress 0 to 1), then it and the tab bar stay at the top.
	// Composition expressions on the ScrollViewer's manipulation, the very transform that moves the content, so they keep
	// up with it however fast it scrolls (a ScrollView's ExpressionAnimationSources lag it by a frame: the header came
	// away from the top).
	private void AnimateShyHeader()
	{
		var scroll = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(PageScroller);
		var range = (float)Header.ActualHeight - CollapsedHeaderHeight;

		// Pinned: offset by however far past the collapse the page has scrolled.
		Animate(Header, "Translation.Y", "Max(0, y - range)");
		Animate(TabsBar, "Translation.Y", "Max(0, y - range)");
		// The banner holds still in the viewport (the header clips it), so it stays behind the collapsed header.
		Animate(BannerImageElement, "Translation.Y", "Min(y, range)");
		// The cover shrinks toward its bottom-left corner, and the title follows its right edge.
		ElementCompositionPreview.GetElementVisual(Cover).CenterPoint = new Vector3(0, (float)Cover.ActualHeight, 0);
		Animate(Cover, "Scale", "Vector3(Lerp(1, small, progress), Lerp(1, small, progress), 1)");
		Animate(TitleBlock, "Translation.X", "-width * (1 - small) * progress");

		void Animate(UIElement element, string property, string expression)
		{
			ElementCompositionPreview.SetIsTranslationEnabled(element, true);
			var visual = ElementCompositionPreview.GetElementVisual(element);
			expression = expression.Replace("progress", "Clamp(y / range, 0, 1)");
			var animation = visual.Compositor.CreateExpressionAnimation(Regex.Replace(expression, @"\by\b", "(-scroll.Translation.Y)"));
			animation.SetReferenceParameter("scroll", scroll);
			animation.SetScalarParameter("range", range);
			animation.SetScalarParameter("small", CollapsedCoverScale);
			animation.SetScalarParameter("width", (float)Cover.ActualWidth);
			visual.StartAnimation(property, animation);
		}
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
		LoadingShimmer.IsLoading = false;
	}

	private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (ViewModel is not null && sender.SelectedItem?.Tag is string tab)
		{
			ViewModel.SelectedTab = tab;
		}
	}
}

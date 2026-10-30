using System.Windows.Input;
using CommunityToolkit.WinUI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls;

public sealed partial class AnimeCard : UserControl
{
	public AnimeCard()
	{
		InitializeComponent();
		Tapped += OnTapped;
	}

	/// <summary>The anime shown, for overlays only anime have (e.g. the next episode); null on a manga's card.</summary>
	[GeneratedDependencyProperty]
	public partial AnimeModel? Anime { get; set; }

	/// <summary>The anime or manga shown: what the card itself and the overlays both have (score, place on the list...) show.</summary>
	[GeneratedDependencyProperty]
	public partial IMediaModel? Media { get; set; }

	/// <summary>Runs with the card's <see cref="Media"/> when the card is clicked, e.g. to show its details.</summary>
	[GeneratedDependencyProperty]
	public partial ICommand? Command { get; set; }

	// Either is set; each keeps the other in step (a manga's card has no anime, which mustn't clear its manga). Only on a
	// real change: setting one to what it already is still calls these, and they'd call each other forever.
	partial void OnAnimeChanged(AnimeModel? newValue)
	{
		if (!ReferenceEquals(Media, newValue) && (newValue is not null || Media is AnimeModel))
		{
			Media = newValue;
		}
	}

	// The poster showing (its URL).
	private string? _shownImage;

	// Recycled cards get a new item: hide the old poster until the new one has loaded. Only for another poster: a card
	// can be given no item and then the same one again, and x:Bind keeps the image it has meanwhile, which then opens no
	// more (no ImageOpened) and would stay hidden.
	partial void OnMediaChanged(IMediaModel? newValue)
	{
		if (newValue is not null && newValue.Image != _shownImage)
		{
			_shownImage = newValue.Image;
			PosterFade.Opacity = 0;
		}

		if (!ReferenceEquals(Anime, newValue as AnimeModel))
		{
			Anime = newValue as AnimeModel;
		}

		// A recycled card whose old and new titles are both trimmed gets no IsTextTrimmedChanged: the tooltip would keep
		// the old title. (If the new one fits, IsTextTrimmedChanged then clears it.)
		if (TitleText is { IsTextTrimmed: true })
		{
			ToolTipService.SetToolTip(TitleText, newValue?.Title);
		}
	}

	private void OnPosterOpened(object sender, RoutedEventArgs e) => PosterFade.Opacity = 1;

	partial void OnCommandChanged(ICommand? newValue)
	{
		ProtectedCursor = newValue is null ? null : InputSystemCursor.Create(InputSystemCursorShape.Hand);
	}

	private void OnTitleTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
	{
		ToolTipService.SetToolTip(sender, sender.IsTextTrimmed ? sender.Text : null);
	}

	private void OnTapped(object sender, TappedRoutedEventArgs e)
	{
		if (IsFromOverlay(e.OriginalSource) || Command is not { } command || !command.CanExecute(Media))
		{
			return;
		}

		command.Execute(Media);
		e.Handled = true;
	}

	/// <summary>
	/// Whether a tap came from an overlay on the card (e.g. the actions button) rather than the card itself.
	/// Buttons don't mark Tapped handled, so their taps bubble up here too and would otherwise also open the card.
	/// Overlays are hosted in OverlayLayer, which itself covers the whole card, so a tap on the layer itself is a card tap.
	/// </summary>
	private bool IsFromOverlay(object? source)
	{
		for (var element = source as DependencyObject; element is not null && element != this; element = VisualTreeHelper.GetParent(element))
		{
			if (VisualTreeHelper.GetParent(element) == OverlayLayer)
			{
				return true;
			}
		}

		return false;
	}
}

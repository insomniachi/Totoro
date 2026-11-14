using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Services;

namespace Totoro.UI.Controls;

/// <summary>A song's card, which plays it: in the Music tab, and in the chat. See <see cref="SongItem"/>.</summary>
public sealed partial class SongCard : UserControl
{
	public SongCard()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SongItem? Song { get; set; }

	// The choice ticked, English only where the song has English lyrics, and readings only for lyrics in Japanese script
	// (lyrics in romaji show as they are: "Original").
	private void OnReadingsOpening(object sender, object e)
	{
		var japanese = Song?.HasJapanese == true;
		foreach (var item in ((MenuFlyout)sender).Items.OfType<RadioMenuFlyoutItem>())
		{
			var choice = Enum.Parse<LyricReadings>((string)item.Tag);
			if (choice == LyricReadings.None)
			{
				item.Text = japanese ? "Japanese only" : "Original";
			}

			item.IsEnabled = choice switch
			{
				LyricReadings.English => Song?.HasEnglish == true,
				LyricReadings.None => true,
				_ => japanese,
			};
			item.IsChecked = item.Text == Song?.ReadingsName;
		}
	}

	private void OnReadingsClick(object sender, RoutedEventArgs e)
	{
		Song?.SetReadings(Enum.Parse<LyricReadings>((string)((FrameworkElement)sender).Tag));
	}

	public static Visibility VisibleIfAny(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
}

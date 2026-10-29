using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Manga;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaDetailsPage : Page
{
	public MangaDetailsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaDetailsViewModel? ViewModel { get; set; }

	private void OnSourceSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => ViewModel?.SearchCommand.Execute(null);

	private void OnMatchClick(object sender, ItemClickEventArgs e) => ViewModel?.PickCommand.Execute(e.ClickedItem);

	private void OnChapterClick(object sender, ItemClickEventArgs e) => ViewModel?.OpenChapterCommand.Execute(e.ClickedItem);

	public static string TrackingButtonText(MangaTracking? tracking) => tracking?.Status is null ? "Add to list" : "Edit tracking";

	/// <summary>E.g. "Reading · chapter 12 of 140"; "" when it isn't on the list.</summary>
	public static string ListText(MangaTracking? tracking) => tracking?.Status is null ? ""
		: tracking.ChaptersRead is > 0 and var read ? $"{MediaText.Status(tracking.Status, Core.Navigation.MediaMode.Manga)} · {read} chapters read" : MediaText.Status(tracking.Status, Core.Navigation.MediaMode.Manga);

	/// <summary>E.g. "Chapter 12: The Return", or "Chapter 12" when it has no name (or its name is just that).</summary>
	public static string ChapterText(float number, string title)
	{
		var chapter = $"Chapter {number}";
		return string.IsNullOrWhiteSpace(title) ? chapter : title.Contains(number.ToString()) ? title : $"{chapter}: {title}";
	}
}

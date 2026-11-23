using System.Globalization;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Totoro.Core.Services;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class NewsPage : Page
{
	public NewsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial NewsViewModel? ViewModel { get; set; }

	// Selection rather than ItemClick, so UI Automation's Select (winapp ui invoke) opens it too.
	private async void OnArticleSelected(object sender, SelectionChangedEventArgs e)
	{
		if (ViewModel is { } viewModel && Headlines.SelectedItem is NewsArticle article && article != viewModel.Article)
		{
			Reader.ChangeView(null, 0, null, disableAnimation: true);
			await viewModel.OpenAsync(article);
		}
	}

	// Names the list item itself, which UI Automation selects, rather than its content.
	private void OnHeadlineContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
	{
		if (args.Item is NewsArticle article)
		{
			AutomationProperties.SetName(args.ItemContainer, Describe(article.Title, article.Summary, article.Published));
		}
	}

	/// <summary>"5 min ago", "3 h ago", else the date; "" when the feed gave no date.</summary>
	public static string Ago(DateTimeOffset published)
	{
		if (published == default)
		{
			return "";
		}

		var age = DateTimeOffset.Now - published;
		return age.TotalMinutes < 1 ? "Just now"
			: age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
			: age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
			: published.LocalDateTime.ToString("d", CultureInfo.CurrentCulture);
	}

	public static string Describe(string title, string summary, DateTimeOffset published) =>
		string.Join(". ", new[] { title, summary, Ago(published) }.Where(x => x.Length > 0));

	// Each block shows its picture in one Image only: the still one, or the video's thumbnail. The other gets null, so it isn't decoded twice.
	public static ImageSource? StillPicture(byte[]? bytes, Uri? video) => video is null ? Decode(bytes) : null;

	public static ImageSource? VideoPicture(byte[]? bytes, Uri? video) => video is null ? null : Decode(bytes);

	// A downloaded picture, decoded for an Image; null if there's none or it isn't one the codecs read.
	private static BitmapImage? Decode(byte[]? bytes)
	{
		if (bytes is null)
		{
			return null;
		}

		try
		{
			var picture = new BitmapImage();
			picture.SetSource(new MemoryStream(bytes).AsRandomAccessStream());
			return picture;
		}
		catch (Exception)
		{
			// An error page or an unsupported format: the article reads fine without it.
			return null;
		}
	}

	public static string Title(NewsArticle? article) => article?.Title ?? "";

	public static string Byline(NewsArticle? article) =>
		article is null ? "" : string.Join(" · ", new[] { article.Category, Ago(article.Published) }.Where(x => x.Length > 0));

	public static Uri? Url(NewsArticle? article) => article?.Url;

	public static Visibility VisibleIfNone(NewsArticle? article) => article is null ? Visibility.Visible : Visibility.Collapsed;

	// A still image; a video's thumbnail is a link instead.
	public static Visibility StillImage(string image, Uri? video) => image.Length > 0 && video is null ? Visibility.Visible : Visibility.Collapsed;
}

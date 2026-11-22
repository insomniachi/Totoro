using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

	/// <summary>"5 min ago", "3 h ago", else the date.</summary>
	public static string Ago(DateTimeOffset published)
	{
		var age = DateTimeOffset.Now - published;
		return age.TotalMinutes < 1 ? "Just now"
			: age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
			: age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
			: published.LocalDateTime.ToString("d MMM yyyy");
	}

	public static string Describe(string title, string summary, DateTimeOffset published) =>
		summary.Length > 0 ? $"{title}. {summary}. {Ago(published)}" : $"{title}. {Ago(published)}";

	public static string Title(NewsArticle? article) => article?.Title ?? "";

	public static string Byline(NewsArticle? article) =>
		article is null ? "" : article.Category.Length > 0 ? $"{article.Category} · {Ago(article.Published)}" : Ago(article.Published);

	public static Uri? Url(NewsArticle? article) => article?.Url;

	public static Visibility VisibleIfNone(NewsArticle? article) => article is null ? Visibility.Visible : Visibility.Collapsed;

	// A still image; a video's thumbnail is a link instead.
	public static Visibility StillImage(string image, Uri? video) => image.Length > 0 && video is null ? Visibility.Visible : Visibility.Collapsed;
}

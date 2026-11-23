using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>A piece of the article being read, with its image's bytes once they're downloaded (the view decodes them).</summary>
public sealed partial class ReaderBlock(ArticleBlock block) : ObservableObject
{
	public ArticleBlock Block { get; } = block;

	[ObservableProperty]
	public partial byte[]? Picture { get; set; }
}

/// <summary>The latest anime news headlines (see <see cref="NewsService"/>), and the one picked, to read in the app.</summary>
public sealed partial class NewsViewModel(NewsService news, IHttpClientFactory httpClients) : NavigatableViewModel
{
	private CancellationTokenSource? _articleLoad;

	[ObservableProperty]
	public partial IReadOnlyList<NewsArticle> Articles { get; private set; } = [];

	/// <summary>Why loading failed; "" if it didn't.</summary>
	[ObservableProperty]
	public partial string Error { get; private set; } = "";

	/// <summary>The article being read; null until one's picked.</summary>
	[ObservableProperty]
	public partial NewsArticle? Article { get; private set; }

	[ObservableProperty]
	public partial IReadOnlyList<ReaderBlock> Blocks { get; private set; } = [];

	[ObservableProperty]
	public partial bool IsLoadingArticle { get; private set; }

	/// <summary>Why the article didn't load; "" if it did.</summary>
	[ObservableProperty]
	public partial string ArticleError { get; private set; } = "";

	protected override Task OnNavigatedToInternalAsync() => LoadAsync();

	protected override Task OnNetworkRestoredAsync() =>
		Error.Length > 0 ? LoadAsync() : ArticleError.Length > 0 && Article is { } article ? OpenAsync(article) : Task.CompletedTask;

	[RelayCommand]
	private Task RefreshAsync() => RunBusyAsync(LoadAsync);

	private async Task LoadAsync()
	{
		Error = "";
		try
		{
			Articles = await news.GetLatestAsync();
		}
		catch (InvalidOperationException ex)
		{
			Error = ex.Message;
		}
	}

	/// <summary>Shows the article's body; picking another while it loads drops this one.</summary>
	public async Task OpenAsync(NewsArticle article)
	{
		_articleLoad?.Cancel();
		var load = _articleLoad = new CancellationTokenSource(); // Not disposed: the next pick cancels it.
		Article = article;
		Blocks = [];
		ArticleError = "";
		IsLoadingArticle = true;
		try
		{
			var blocks = await news.GetArticleAsync(article.Url, load.Token);
			// Another article was picked while this one finished: don't show it under that one's title.
			load.Token.ThrowIfCancellationRequested();
			Blocks = [.. blocks.Select(x => new ReaderBlock(x))];
		}
		catch (Exception) when (load.IsCancellationRequested)
		{
			// Another article was picked: whatever this one ended in, it's not shown.
			return;
		}
		catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
		{
			ArticleError = ex.Message;
		}

		IsLoadingArticle = false;
		await Task.WhenAll(Blocks.Where(x => x.Block.Image.Length > 0).Select(x => LoadPictureAsync(x, load.Token)));
	}

	// Through HttpClient rather than the Image's own download: that one gives up on hosts whose certificate revocation
	// server is unreachable, as the news site's image host's often is.
	private async Task LoadPictureAsync(ReaderBlock block, CancellationToken ct)
	{
		try
		{
			using var http = httpClients.CreateClient();
			block.Picture = await http.GetByteArrayAsync(block.Block.Image, ct);
		}
		catch (Exception)
		{
			// Dropped for another article, or failed: the article reads fine without it.
		}
	}
}

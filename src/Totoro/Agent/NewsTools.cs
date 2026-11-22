using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Services;

namespace Totoro.Agent;

public sealed record NewsItem(
	string Title,
	[property: Description("A one-line summary.")]
	string Summary,
	[property: Description("What it's about, e.g. Anime, Manga, Industry; empty if unknown.")]
	string Category,
	[property: Description("The article's web page.")]
	string Url,
	[property: Description("When it was published, in the user's local time; null if the feed doesn't say.")]
	DateTime? Published);

/// <summary>Agent tools for the News page (see <see cref="NewsService"/>).</summary>
public sealed class NewsTools(NewsService news)
{
	[AgentTool("get_anime_news")]
	[Description("The latest anime and manga news headlines, newest first: what the News page shows. E.g. new announcements, trailers, casts and release dates.")]
	public async Task<IReadOnlyList<NewsItem>> GetAnimeNewsAsync(
		[Description("How many articles, from 1 to 50.")] int count = 10)
	{
		IReadOnlyList<NewsArticle> articles;
		try
		{
			articles = await news.GetLatestAsync();
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}

		return [.. articles.Take(Math.Clamp(count, 1, 50)).Select(x => new NewsItem(x.Title, x.Summary, x.Category, x.Url.ToString(), x.Published == default ? null : x.Published.LocalDateTime))];
	}

	[AgentTool("read_news_article")]
	[Description("The full text of a news article from get_anime_news: its paragraphs, separated by blank lines.")]
	public async Task<string> ReadNewsArticleAsync(
		[Description("The article's url, from get_anime_news.")] string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			throw new AgentToolException("That isn't a url. Get one from get_anime_news.");
		}

		try
		{
			var blocks = await news.GetArticleAsync(uri);
			return string.Join("\n\n", blocks.Select(x => x.Text).Where(x => x.Length > 0));
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			throw new AgentToolException(ex.Message);
		}
	}
}

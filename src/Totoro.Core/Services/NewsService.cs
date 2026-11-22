using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using Totoro.Core.Helpers;

namespace Totoro.Core.Services;

/// <summary>A news article: its headline, one-line summary, link and when it was published.</summary>
/// <param name="Category">What it's about, e.g. "Anime", "Manga", "Industry"; "" if the feed doesn't say.</param>
public sealed record NewsArticle(string Title, string Summary, string Category, Uri Url, DateTimeOffset Published);

/// <summary>A piece of an article's body: a paragraph (<see cref="Text"/>), or an image with its caption; a video is its thumbnail, opening <see cref="Video"/>.</summary>
public sealed record ArticleBlock(string Text, string Image, string Caption, Uri? Video);

/// <summary>The latest anime news, from Anime News Network's RSS feed.</summary>
public sealed class NewsService(IHttpClientFactory httpClients)
{
	// shortcut: one hard-coded feed, make it a setting if users ask for others.
	private const string FeedHost = "animenewsnetwork.com";
	private const string FeedUrl = "https://www.animenewsnetwork.com/all/rss.xml";

	/// <summary>The newest articles first.</summary>
	public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(CancellationToken ct = default)
	{
		using var http = httpClients.CreateClient();
		string xml;
		try
		{
			xml = await http.GetStringAsync(FeedUrl, ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
		{
			throw new InvalidOperationException(NetworkErrors.Describe(ex, "the news"), ex);
		}

		try
		{
			return Parse(xml);
		}
		catch (XmlException ex)
		{
			throw new InvalidOperationException($"Couldn't read the news ({ex.Message}).", ex);
		}
	}

	/// <summary>An article's body, from its page, to read in the app.</summary>
	/// <exception cref="ArgumentException">Not an article from the feed.</exception>
	public async Task<IReadOnlyList<ArticleBlock>> GetArticleAsync(Uri url, CancellationToken ct = default)
	{
		if (url.Scheme != Uri.UriSchemeHttps || !url.Host.EndsWith(FeedHost, StringComparison.OrdinalIgnoreCase))
		{
#pragma warning disable CA2208 // The message is shown to the user and the agent; a parameter name would trail it.
			throw new ArgumentException($"Only {FeedHost} articles can be read in the app.");
#pragma warning restore CA2208
		}

		using var http = httpClients.CreateClient();
		string html;
		try
		{
			html = await http.GetStringAsync(url, ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
		{
			throw new InvalidOperationException(NetworkErrors.Describe(ex, "the article"), ex);
		}

		var blocks = ParseArticle(html, url);
		return blocks.Count > 0 ? blocks : throw new InvalidOperationException("Couldn't find the article's text on its page.");
	}

	/// <summary>The article's paragraphs, images and videos, in order.</summary>
	internal static IReadOnlyList<ArticleBlock> ParseArticle(string html, Uri page)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(html);
		// News keeps its body in div.meat; reviews and columns in the element with the most paragraphs.
		var body = doc.DocumentNode.SelectSingleNode("//div[contains(concat(' ', @class, ' '), ' meat ')]")
			?? doc.DocumentNode.Descendants().Where(x => x.NodeType == HtmlNodeType.Element)
								.MaxBy(x => x.ChildNodes.Count(c => c.Name == "p"));
		var blocks = new List<ArticleBlock>();
		if (body is not null)
		{
			Walk(body, blocks, page);
		}

		return blocks;
	}

	private static readonly HashSet<string> s_textElements = ["p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "li"];

	private static void Walk(HtmlNode node, List<ArticleBlock> blocks, Uri page)
	{
		foreach (var child in node.ChildNodes)
		{
			switch (child.Name)
			{
				case "script" or "style" or "noscript" or "#comment":
					break;
				case "figure":
					if (Image(child.Descendants("img").FirstOrDefault(), page) is { } figure)
					{
						var caption = child.Descendants("figcaption").FirstOrDefault(x => !x.HasClass("copyright"));
						blocks.Add(new ArticleBlock("", figure, Text(caption?.InnerHtml), null));
					}

					break;
				case "img":
					if (Image(child, page) is { } image)
					{
						blocks.Add(new ArticleBlock("", image, "", null));
					}

					break;
				case "iframe":
					// YouTube embeds become their thumbnail, opening the video.
					var match = Regex.Match(child.GetAttributeValue("src", ""), @"youtube(?:-nocookie)?\.com/embed/([\w-]+)");
					if (match.Success)
					{
						var id = match.Groups[1].Value;
						blocks.Add(new ArticleBlock("", $"https://img.youtube.com/vi/{id}/hqdefault.jpg", "", new Uri($"https://www.youtube.com/watch?v={id}")));
					}

					break;
				case "#text":
					AddText(child.InnerHtml, "", blocks);
					break;
				default:
					// A paragraph with an image or video in it is walked like any other element, so they come out in order.
					if (s_textElements.Contains(child.Name) && !child.Descendants().Any(x => x.Name is "img" or "iframe" or "figure"))
					{
						AddText(child.InnerHtml, child.Name == "li" ? "• " : "", blocks);
					}
					else
					{
						Walk(child, blocks, page);
					}

					break;
			}
		}
	}

	private static void AddText(string html, string prefix, List<ArticleBlock> blocks)
	{
		var text = Regex.Replace(Text(html), @"\s+", " ");
		if (text.Length > 0)
		{
			blocks.Add(new ArticleBlock(prefix + text, "", "", null));
		}
	}

	// "//cdn…" and "/…" sources are made absolute against the page.
	private static string? Image(HtmlNode? img, Uri page)
	{
		var src = img?.GetAttributeValue("data-src", null) ?? img?.GetAttributeValue("src", null);
		return !string.IsNullOrEmpty(src) && Uri.TryCreate(page, src, out var url) && url.Scheme is "http" or "https" ? url.ToString() : null;
	}

	internal static IReadOnlyList<NewsArticle> Parse(string xml)
	{
		var articles = new List<NewsArticle>();
		foreach (var item in XDocument.Parse(xml).Descendants("item"))
		{
			if (!Uri.TryCreate((string?)item.Element("link"), UriKind.Absolute, out var url))
			{
				continue;
			}

			DateTimeOffset.TryParse((string?)item.Element("pubDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var published);
			articles.Add(new NewsArticle(
				Text((string?)item.Element("title")),
				Text((string?)item.Element("description")),
				Text((string?)item.Element("category")),
				url,
				published));
		}

		return [.. articles.OrderByDescending(x => x.Published)];
	}

	// Summaries can hold markup (<cite>) and entities.
	private static string Text(string? html)
	{
		if (string.IsNullOrWhiteSpace(html))
		{
			return "";
		}

		var doc = new HtmlDocument();
		doc.LoadHtml(html);
		return HtmlEntity.DeEntitize(doc.DocumentNode.InnerText).Trim();
	}
}

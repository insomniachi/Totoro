using Totoro.Core.Services;
using Xunit;

namespace Totoro.Tests;

public class NewsTests
{
	[Fact]
	public void Parse_ReadsItemsNewestFirst_AsPlainText()
	{
		const string Feed = """
			<?xml version="1.0" encoding="utf-8" ?>
			<rss version="2.0"><channel>
			  <item>
			    <title>Older &amp; wiser</title>
			    <link>https://example.com/a</link>
			    <description>Staff for &lt;cite&gt;Hidan no Aria&lt;/cite&gt; revealed</description>
			    <pubDate>Sat, 21 Nov 2026 23:59:00 -0500</pubDate>
			    <category>Anime</category>
			  </item>
			  <item>
			    <title>Newer</title>
			    <link>https://example.com/b</link>
			    <pubDate>Sun, 22 Nov 2026 05:00:00 -0500</pubDate>
			  </item>
			  <item><title>No link</title></item>
			</channel></rss>
			""";

		var articles = NewsService.Parse(Feed);

		Assert.Equal(2, articles.Count);
		Assert.Equal("Newer", articles[0].Title);
		Assert.Equal("", articles[0].Summary);
		Assert.Equal(new DateTimeOffset(2026, 11, 22, 5, 0, 0, TimeSpan.FromHours(-5)), articles[0].Published);
		Assert.Equal(new NewsArticle("Older & wiser", "Staff for Hidan no Aria revealed", "Anime", new Uri("https://example.com/a"),
			new DateTimeOffset(2026, 11, 21, 23, 59, 0, TimeSpan.FromHours(-5))), articles[1]);
	}

	private static readonly Uri s_page = new("https://www.animenewsnetwork.com/news/2026-11-22/x/.1");

	[Fact]
	public void ParseArticle_ReadsTheBody_InOrder()
	{
		const string Html = """
			<html><body><div class="sidebar"><p>Not this</p></div>
			<div class="meat"><p>The staff for <cite><a href="/e">Shōzen</a></cite>   revealed &amp; more.</p>
			<p></p><p align="center"><span><iframe src="https://www.youtube.com/embed/62V2jlvcVJY"></iframe></span></p>
			<figure><img src="//cdn.animenewsnetwork.com/a/kokuka.jpg"><figcaption class="credit">Image courtesy of Fuji TV</figcaption>
			<figcaption class="credit copyright"><div class="text">© someone</div></figcaption></figure>
			<ul><li>One</li></ul>
			<script>var x;</script></div></body></html>
			""";

		Assert.Equal(
			[
				new ArticleBlock("The staff for Shōzen revealed & more.", "", "", null),
				new ArticleBlock("", "https://img.youtube.com/vi/62V2jlvcVJY/hqdefault.jpg", "", new Uri("https://www.youtube.com/watch?v=62V2jlvcVJY")),
				new ArticleBlock("", "https://cdn.animenewsnetwork.com/a/kokuka.jpg", "Image courtesy of Fuji TV", null),
				new ArticleBlock("• One", "", "", null),
			],
			NewsService.ParseArticle(Html, s_page));
	}

	[Fact]
	public void ParseArticle_WithoutMeat_TakesTheElementWithTheMostParagraphs()
	{
		const string Html = """
			<html><body><div><p>Byline</p></div>
			<table><tr><td><div class="text-zone"><p>First.</p><p>Second.</p></div></td></tr></table></body></html>
			""";

		Assert.Equal(
			[new ArticleBlock("First.", "", "", null), new ArticleBlock("Second.", "", "", null)],
			NewsService.ParseArticle(Html, s_page));
	}

	[Fact]
	public void ParseArticle_WithoutParagraphs_FindsNothing_AndSkipsHttpImages()
	{
		const string Html = """<html><body><nav>Menu</nav><div>Footer</div><img src="http://example.com/a.jpg"></body></html>""";

		Assert.Empty(NewsService.ParseArticle(Html, s_page));
		Assert.Empty(NewsService.ParseArticle($"""<div class="meat">{Html}</div>""".Replace("Menu", "").Replace("Footer", ""), s_page));
	}

	[Theory]
	[InlineData("https://evilanimenewsnetwork.com/news/x")]
	[InlineData("http://www.animenewsnetwork.com/news/x")]
	[InlineData("https://animenewsnetwork.com.evil.example/news/x")]
	public async Task GetArticleAsync_RefusesOtherSites(string url) =>
		await Assert.ThrowsAsync<ArgumentException>(() => new NewsService(null!).GetArticleAsync(new Uri(url)));
}

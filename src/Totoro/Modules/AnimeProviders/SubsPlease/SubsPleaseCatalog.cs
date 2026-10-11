using System.Text.Json;
using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Streaming;

namespace Totoro.Modules.AnimeProviders.SubsPlease;

/// <summary>
/// Every show on SubsPlease (from its "all shows" page), cached on disk and refreshed daily, since new shows start
/// each season. Searching matches titles loosely: SubsPlease uses romaji titles, punctuated its own way.
/// </summary>
internal static partial class SubsPleaseCatalog
{
	private static readonly TimeSpan s_maxAge = TimeSpan.FromDays(1);
	private static readonly SemaphoreSlim s_lock = new(1, 1);
	private static List<CatalogShow>? s_shows;

	internal sealed record CatalogShow(string Id, string Title);

	public static async Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct)
	{
		var shows = await GetShowsAsync(ct);
		var normalizedQuery = Normalize(query);
		if (normalizedQuery.Length == 0)
		{
			return [];
		}

		return
		[
			.. shows.Select(x => new ProviderShow(x.Id, x.Title, Score(normalizedQuery, Normalize(x.Title))))
					.Where(x => x.Score >= 0.4)
					.OrderByDescending(x => x.Score)
					.Take(10)
		];
	}

	private static async Task<List<CatalogShow>> GetShowsAsync(CancellationToken ct)
	{
		await s_lock.WaitAsync(ct);
		try
		{
			var file = FileHelper.GetModulePath(SubsPleaseModule.Descriptor, "catalog.json");
			if (s_shows is { } shows && File.Exists(file) && DateTime.Now - File.GetLastWriteTime(file) < s_maxAge)
			{
				return shows;
			}

			if (File.Exists(file) && DateTime.Now - File.GetLastWriteTime(file) < s_maxAge)
			{
				return s_shows = JsonSerializer.Deserialize<List<CatalogShow>>(await File.ReadAllTextAsync(file, ct)) ?? [];
			}

			try
			{
				s_shows = await DownloadAsync(ct);
				Directory.CreateDirectory(Path.GetDirectoryName(file)!);
				await File.WriteAllTextAsync(file, JsonSerializer.Serialize(s_shows), ct);
				return s_shows;
			}
			catch when (File.Exists(file))
			{
				// Offline, or the site changed: an old list is better than none.
				return s_shows = JsonSerializer.Deserialize<List<CatalogShow>>(await File.ReadAllTextAsync(file, ct)) ?? [];
			}
		}
		finally
		{
			s_lock.Release();
		}
	}

	private static async Task<List<CatalogShow>> DownloadAsync(CancellationToken ct)
	{
		var html = await "https://subsplease.org/shows/".GetStringAsync(cancellationToken: ct);
		var document = new HtmlDocument();
		document.LoadHtml(html);

		return
		[
			// The links in the .all-shows-link blocks (by class, which may have others with it).
			.. (document.DocumentNode.SelectNodes("//*[contains(concat(' ', normalize-space(@class), ' '), ' all-shows-link ')]//a") ?? Enumerable.Empty<HtmlNode>())
					   .Select(x => new CatalogShow(x.GetAttributeValue("href", "").Replace("/shows/", "").Trim('/'),
													HtmlEntity.DeEntitize(x.GetAttributeValue("title", x.InnerText)).Trim()))
					   .Where(x => x.Id.Length > 0 && x.Title.Length > 0)
		];
	}

	/// <summary>1 for the same title; otherwise how many words they share, with a boost when one contains the other.</summary>
	private static double Score(string query, string title)
	{
		if (query == title)
		{
			return 1;
		}

		var queryWords = query.Split(' ');
		var titleWords = title.Split(' ');
		var shared = queryWords.Intersect(titleWords).Count();
		var score = (double)shared / queryWords.Union(titleWords).Count();

		if (title.Contains(query) || query.Contains(title))
		{
			score = Math.Max(score, 0.6 + 0.3 * Math.Min(query.Length, title.Length) / Math.Max(query.Length, title.Length));
		}

		return score;
	}

	/// <summary>Lowercase words only: "Sousou no Frieren: Season 2" and "sousou no frieren season 2" match.</summary>
	private static string Normalize(string title)
	{
		return Spaces().Replace(NotLetterOrDigit().Replace(title.ToLowerInvariant(), " "), " ").Trim();
	}

	[GeneratedRegex(@"[^\p{L}\p{N}]")]
	private static partial Regex NotLetterOrDigit();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}

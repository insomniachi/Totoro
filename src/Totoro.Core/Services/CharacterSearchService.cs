using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>A character in an image, and the work it's from: in English when the anime's cast has the character, else as AnimeTrace names them (often in Japanese).</summary>
/// <param name="NotConfident">Whether AnimeTrace was unsure, between several candidates.</param>
/// <param name="Anime">The work as an anime of the selected database; null if the search found none.</param>
public sealed record CharacterMatch(string Character, string Work, bool NotConfident, AnimeModel? Anime = null);

/// <summary>Names the characters in an image (fan art, a collage, a screenshot) and what they're from, with AnimeTrace.</summary>
public sealed class CharacterSearchService(IFactory<IAnimeMetadataProvider> databases, IHttpClientFactory httpClients)
{
	/// <summary>The characters found, left to right, each with its work looked up in the selected database.</summary>
	/// <param name="contentType">The image's type, e.g. "image/png".</param>
	public async Task<IReadOnlyList<CharacterMatch>> FindAsync(Stream image, string contentType, CancellationToken ct = default)
	{
		using var file = new StreamContent(image);
		file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
		using var form = new MultipartFormDataContent
		{
			{ file, "file", "image" + (contentType.EndsWith("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg") },
			// One result per character, not just the most prominent.
			{ new StringContent("1"), "is_multi" },
		};

		using var http = httpClients.CreateClient();
		http.Timeout = TimeSpan.FromSeconds(60);
		string response;
		try
		{
			using var reply = await http.PostAsync("https://api.animetrace.com/v1/search", form, ct);
			response = await reply.Content.ReadAsStringAsync(ct);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
		{
			throw new InvalidOperationException($"Couldn't reach AnimeTrace ({ex.Message}).", ex);
		}

		var found = Parse(response);
		if (found.Count == 0 || databases.CreateDefault() is not { } database)
		{
			return found;
		}

		// The same work is looked up once, e.g. two characters from one anime.
		var anime = new Dictionary<string, Task<AnimeModel?>>();
		foreach (var work in found.Select(x => x.Work).Distinct())
		{
			anime[work] = FindAnimeAsync(database, work);
		}

		await Task.WhenAll(anime.Values);

		// The anime's cast, to name each character in English: AnimeTrace names them as in Japanese.
		var casts = anime.Values.Select(x => x.Result).OfType<AnimeModel>().DistinctBy(x => x.Id)
			.ToDictionary(x => x.Id, x => GetCastAsync(database, x.Id));
		await Task.WhenAll(casts.Values);
		return [.. found.Select(x => anime[x.Work].Result is not { } match ? x : x with
		{
			Anime = match,
			Character = EnglishName(x.Character, casts[match.Id].Result) ?? x.Character,
		})];
	}

	private static async Task<IReadOnlyList<CharacterModel>> GetCastAsync(IAnimeMetadataProvider database, long animeId)
	{
		try
		{
			return await database.GetCharactersAsync(animeId);
		}
		catch (Exception)
		{
			// The Japanese name is still worth showing.
			return [];
		}
	}

	/// <summary>
	/// The cast member's name whose Japanese name is <paramref name="native"/>, e.g. "Jean Kirstein" for "ジャン・キルシュタイン";
	/// null if none is. Names are compared without spaces and dots, and a given name alone (e.g. "ヒンメル") matches too.
	/// </summary>
	internal static string? EnglishName(string native, IEnumerable<CharacterModel> cast)
	{
		static string Plain(string name) => new([.. name.Where(c => !char.IsWhiteSpace(c) && c is not ('・' or '･' or '·' or '=' or '＝'))]);
		var wanted = Plain(native);
		if (wanted.Length == 0)
		{
			return null;
		}

		var known = cast.Where(x => x.Name.Length > 0 && x.NativeName.Length > 0).Select(x => (x.Name, Native: Plain(x.NativeName))).ToList();
		return known.FirstOrDefault(x => x.Native == wanted).Name
			?? known.FirstOrDefault(x => x.Native.Contains(wanted, StringComparison.Ordinal) || wanted.Contains(x.Native, StringComparison.Ordinal)).Name;
	}

	private static async Task<AnimeModel?> FindAnimeAsync(IAnimeMetadataProvider database, string work)
	{
		try
		{
			return (await database.SearchAnimeAsync(work)).FirstOrDefault();
		}
		catch (Exception)
		{
			// The name is still worth showing without the anime.
			return null;
		}
	}

	/// <summary>AnimeTrace's reply as the characters found, left to right (each its most likely candidate).</summary>
	/// <exception cref="InvalidOperationException">It reported an error, e.g. that the image is too large.</exception>
	internal static IReadOnlyList<CharacterMatch> Parse(string response)
	{
		JsonNode? root;
		try
		{
			root = JsonNode.Parse(response);
		}
		catch (System.Text.Json.JsonException ex)
		{
			throw new InvalidOperationException("AnimeTrace's reply couldn't be read.", ex);
		}

		var code = root?["code"]?.GetValue<int>() ?? -1;
		if (code is not (0 or 200 or 17720))
		{
			throw new InvalidOperationException(code switch
			{
				17701 => "The image is too large for AnimeTrace.",
				17705 => "AnimeTrace can't read this image format.",
				17708 => "There are too many people in the image for AnimeTrace.",
				17728 or 17737 => "AnimeTrace's usage limit is reached. Try again later.",
				17702 or 17731 => "AnimeTrace is busy. Try again in a moment.",
				17704 => "AnimeTrace is under maintenance.",
				_ => $"AnimeTrace couldn't search ({code}).",
			});
		}

		return [.. (root?["data"] as JsonArray ?? [])
			.Select(x => (Entry: x, Best: x?["character"]?.AsArray().FirstOrDefault()))
			.Where(x => x.Best?["character"]?.GetValue<string>() is { Length: > 0 })
			.OrderBy(x => x.Entry?["box"]?.AsArray().FirstOrDefault()?.GetValue<double>() ?? 0)
			.Select(x => new CharacterMatch(
				x.Best!["character"]!.GetValue<string>(),
				x.Best["work"]?.GetValue<string>() ?? "",
				x.Entry?["not_confident"]?.GetValue<bool>() == true))];
	}
}

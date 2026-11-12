using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>An anime a screenshot is from: the episode and time of the frame, and how alike they are (0 to 1).</summary>
/// <param name="Video">A few seconds of the scene, muted, to compare with the image; empty if trace.moe gave none.</param>
/// <param name="FileName">The release the scene was found in, e.g. "[Ohys-Raws] Relife - 01 (BS11 1280x720 x264 AAC).mp4".</param>
public sealed record ImageMatch(AnimeModel Anime, int? Episode, TimeSpan At, double Similarity, string Video = "", string FileName = "")
{
	/// <summary>Where to play the episode from to see the scene: a couple of seconds before it, to get into it.</summary>
	public TimeSpan PlayFrom => At > TimeSpan.FromSeconds(2) ? At - TimeSpan.FromSeconds(2) : TimeSpan.Zero;
}

/// <summary>Finds which anime a screenshot is from, with trace.moe, as anime of the selected database.</summary>
/// <remarks>trace.moe's free use is 100 searches a month per IP address.</remarks>
public sealed class ImageSearchService(IFactory<IAnimeMetadataProvider, Guid> databases, IHttpClientFactory httpClients)
{
	// trace.moe's advice: matches below 90% are usually wrong.
	private const double MinSimilarity = 0.9;

	/// <summary>The anime matched, best first, one match each; empty if none is close enough.</summary>
	/// <param name="contentType">The image's type, e.g. "image/png".</param>
	public async Task<IReadOnlyList<ImageMatch>> FindAsync(Stream image, string contentType, CancellationToken ct = default)
	{
		var database = databases.CreateDefault() ?? throw new InvalidOperationException("No anime database is set up.");
		using var content = new StreamContent(image);
		content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

		// The app's client, which connects over IPv4: over IPv6 trace.moe can hang until the timeout.
		using var http = httpClients.CreateClient();
		http.Timeout = TimeSpan.FromSeconds(60);
		string response;
		try
		{
			// anilistInfo: its MAL id too; cutBorders: ignores black bars around the frame.
			using var reply = await http.PostAsync("https://api.trace.moe/search?anilistInfo&cutBorders", content, ct);
			response = await reply.Content.ReadAsStringAsync(ct);
			if (!reply.IsSuccessStatusCode)
			{
				// It says why in its JSON, e.g. that the month's searches are used up.
				var error = JsonNode.Parse(response)?["error"]?.GetValue<string>();
				throw new InvalidOperationException(string.IsNullOrEmpty(error) ? $"trace.moe couldn't search ({(int)reply.StatusCode})." : error);
			}
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
		{
			throw new InvalidOperationException($"Couldn't reach trace.moe ({ex.Message}).", ex);
		}

		var matches = new List<ImageMatch>();
		var seen = new HashSet<long>();
		foreach (var result in JsonNode.Parse(response)?["result"]?.AsArray() ?? [])
		{
			if (result?["similarity"]?.GetValue<double>() is not { } similarity || similarity < MinSimilarity ||
				result["anilist"]?["id"]?.GetValue<long>() is not { } anilistId || !seen.Add(anilistId))
			{
				continue;
			}

			// Its id in the selected database: AniList's or MAL's as trace.moe gives them, else looked up on ani.zip.
			var known = new AnimeModel
			{
				Id = anilistId,
				ServiceName = nameof(AnimeId.Anilist),
				ExternalIds = { Anilist = anilistId, MyAnimeList = result["anilist"]?["idMal"]?.GetValue<long?>() ?? 0 },
			};
			if (await known.FindIdAsync(database.Name, ct) is not { } id)
			{
				continue;
			}

			matches.Add(new ImageMatch(
				await database.GetAnimeAsync(id),
				result["episode"] is JsonValue episode && episode.TryGetValue<int>(out var number) ? number : null,
				TimeSpan.FromSeconds(result["from"]?.GetValue<double>() ?? 0),
				similarity,
				// Its larger size, without sound.
				result["video"]?.GetValue<string>() is { Length: > 0 } video ? $"{video}{(video.Contains('?') ? '&' : '?')}size=l&mute" : "",
				result["filename"]?.GetValue<string>() ?? ""));
		}

		return matches;
	}

	/// <summary>A match's scene (an MP4 of a few seconds), through the app's client: it connects over IPv4, as trace.moe needs here.</summary>
	public async Task<byte[]> GetClipAsync(ImageMatch match, CancellationToken ct = default)
	{
		using var http = httpClients.CreateClient();
		return await http.GetByteArrayAsync(match.Video, ct);
	}
}

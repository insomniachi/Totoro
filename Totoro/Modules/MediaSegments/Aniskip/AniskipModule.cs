using System.Globalization;
using System.Text.Json.Serialization;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module;
using Totoro.Core.Module.Media;

namespace Totoro.Modules.MediaSegments.Aniskip;

internal class AniskipModule : IModule
{
	public static Guid Id { get; } = new("5ccd59c9-7fd1-485e-b542-e4b8cfaf5655");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Aniskip",
		Description = "A community database of where episodes' openings, endings and recaps are.",
		EntryPoint = "Aniskip"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		// Not keyed: playback takes whichever is registered (there's no setting to pick one).
		services.AddTransient<IMediaSegmentsProvider, AniskipSegmentsProvider>();
	}
}

/// <summary>Segments from Aniskip (api.aniskip.com), by MyAnimeList id.</summary>
internal sealed class AniskipSegmentsProvider(ILogger<AniskipSegmentsProvider> logger) : IMediaSegmentsProvider
{
	public async Task<List<MediaSegment>> GetSegmentsAsync(long myAnimeListId, int episode, TimeSpan duration, CancellationToken ct)
	{
		try
		{
			// Submissions are tied to a cut of the episode (its length); with the length, only matching ones come back.
			// If none match, any cut's are close enough to be useful.
			return await GetAsync(duration) is { Count: > 0 } segments ? segments : await GetAsync(TimeSpan.Zero);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't get skip times for MAL {Id} episode {Episode}", myAnimeListId, episode);
			return [];
		}

		async Task<List<MediaSegment>> GetAsync(TimeSpan length)
		{
			var response = await $"https://api.aniskip.com/v2/skip-times/{myAnimeListId}/{episode}"
								 .AppendQueryParam("types", new[] { "op", "ed", "recap" })
								 .AppendQueryParam("episodeLength", ((int)length.TotalSeconds).ToString(CultureInfo.InvariantCulture))
								 .AllowHttpStatus(404)
								 .GetJsonAsync<Response>(cancellationToken: ct);

			return
			[
				.. response.Results
						   .Select(x => (Type: ToType(x.SkipType), x.Interval))
						   .Where(x => x.Type is not null)
						   .Select(x => new MediaSegment(x.Type!.Value, TimeSpan.FromSeconds(x.Interval.StartTime),
														 TimeSpan.FromSeconds(x.Interval.EndTime)))
						   .OrderBy(x => x.Start)
			];
		}
	}

	private static MediaSegmentType? ToType(string type) => type switch
	{
		"op" => MediaSegmentType.Opening,
		"ed" => MediaSegmentType.Ending,
		"recap" => MediaSegmentType.Recap,
		_ => null,
	};

	private sealed class Response
	{
		[JsonPropertyName("results")] public List<Result> Results { get; set; } = [];
	}

	private sealed class Result
	{
		[JsonPropertyName("skipType")] public string SkipType { get; set; } = "";
		[JsonPropertyName("interval")] public Interval Interval { get; set; } = new();
	}

	private sealed class Interval
	{
		[JsonPropertyName("startTime")] public double StartTime { get; set; }
		[JsonPropertyName("endTime")] public double EndTime { get; set; }
	}
}

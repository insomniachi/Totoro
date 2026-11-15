namespace Totoro.Modules.AnimeDatabase.Anilist;

/// <summary>
/// Keeps AniList requests under its rate limit (90 a minute, 30 when it's degraded): each response says how many are
/// left this minute, and once none are, requests wait for the minute to pass rather than be refused with a 429.
/// </summary>
internal sealed class AnilistRateLimitHandler : DelegatingHandler
{
	// Shared by every client: the limit is AniList's, per user.
	private static readonly object s_lock = new();
	private static DateTimeOffset s_resumeAt;

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		TimeSpan wait;
		lock (s_lock)
		{
			wait = s_resumeAt - DateTimeOffset.UtcNow;
		}

		if (wait > TimeSpan.Zero)
		{
			await Task.Delay(wait, ct);
		}

		var response = await base.SendAsync(request, ct);
		if (ResumeAfter(response) is { } after)
		{
			lock (s_lock)
			{
				s_resumeAt = DateTimeOffset.UtcNow + after;
			}
		}

		return response;
	}

	/// <summary>How long to hold off after this response, if at all: AniList's Retry-After, or a minute once none are left.</summary>
	internal static TimeSpan? ResumeAfter(HttpResponseMessage response)
	{
		if (response.Headers.RetryAfter?.Delta is { } delta)
		{
			return delta;
		}

		return response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && int.TryParse(values.FirstOrDefault(), out var left) && left <= 0
			? TimeSpan.FromMinutes(1)
			: null;
	}
}

using System.Net.Http.Headers;
using Totoro.Modules.AnimeDatabase.Anilist;
using Xunit;

namespace Totoro.Tests;

public class AnilistRateLimitHandlerTests
{
	private static HttpResponseMessage Response(string? remaining, TimeSpan? retryAfter = null)
	{
		var response = new HttpResponseMessage();
		if (remaining is not null)
		{
			response.Headers.Add("X-RateLimit-Remaining", remaining);
		}

		if (retryAfter is { } delta)
		{
			response.Headers.RetryAfter = new RetryConditionHeaderValue(delta);
		}

		return response;
	}

	[Fact]
	public void ResumeAfter_HoldsOffOnlyOnceNoneAreLeft()
	{
		Assert.Null(AnilistRateLimitHandler.ResumeAfter(Response("29")));
		Assert.Null(AnilistRateLimitHandler.ResumeAfter(Response(null)));
		Assert.Equal(TimeSpan.FromMinutes(1), AnilistRateLimitHandler.ResumeAfter(Response("0")));
		Assert.Equal(TimeSpan.FromSeconds(42), AnilistRateLimitHandler.ResumeAfter(Response("0", TimeSpan.FromSeconds(42))));
	}
}

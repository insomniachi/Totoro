using System.Net.Http;
using System.Net.Sockets;
using Totoro.Core.Helpers;
using Xunit;

namespace Totoro.Tests;

public class NetworkErrorsTests
{
	[Fact]
	public void Describe_NoConnection_SaysCheckTheConnection()
	{
		var ex = new HttpRequestException("No such host is known. (graphql.anilist.co:443)", new SocketException(11001));

		Assert.Equal("Couldn't reach Anilist. Check your internet connection, then try again.", NetworkErrors.Describe(ex, "Anilist"));
	}

	[Fact]
	public void Describe_NameNotResolved_SaysCheckTheConnection()
	{
		var ex = new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.");

		Assert.True(NetworkErrors.IsConnectionFailure(ex));
	}

	[Fact]
	public void Describe_ServerError_KeepsTheReason()
	{
		var ex = new HttpRequestException("Response status code does not indicate success: 500.");

		Assert.Equal("Couldn't reach Anilist; it may be down. Try again in a bit. (Response status code does not indicate success: 500.)", NetworkErrors.Describe(ex, "Anilist"));
	}
}

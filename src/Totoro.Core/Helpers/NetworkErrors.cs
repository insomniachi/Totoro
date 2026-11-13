using System.Net.Http;
using System.Net.Sockets;

namespace Totoro.Core.Helpers;

/// <summary>Says why a request to a site failed in words a user can act on, rather than the socket's own.</summary>
public static class NetworkErrors
{
	/// <summary>"Couldn't reach {site}…", telling the user to check their connection when it never got there.</summary>
	public static string Describe(Exception ex, string site)
	{
		return IsConnectionFailure(ex)
			? $"Couldn't reach {site}. Check your internet connection, then try again."
			: $"Couldn't reach {site}; it may be down. Try again in a bit. ({ex.Message})";
	}

	/// <summary>Whether the request never reached the site: no connection, the name didn't resolve, or it was refused.</summary>
	public static bool IsConnectionFailure(Exception ex)
	{
		for (Exception? e = ex; e is not null; e = e.InnerException)
		{
			if (e is SocketException
				|| e is HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError })
			{
				return true;
			}
		}

		return false;
	}
}

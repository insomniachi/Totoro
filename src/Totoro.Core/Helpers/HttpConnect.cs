using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Totoro.Core.Helpers;

/// <summary>
/// How the app's HTTP clients open connections. .NET tries a host's addresses in DNS order, IPv6 first, waiting out
/// the OS connect timeout (~21 s) on each: on a network where IPv6 is broken, a host with three IPv6 addresses (like
/// AniList's) takes over a minute to answer, and the pages waiting on it show their placeholders all that time.
/// </summary>
public static class HttpConnect
{
	/// <summary>A <see cref="SocketsHttpHandler.ConnectCallback"/> that tries the host's IPv4 addresses before its IPv6 ones.</summary>
	public static async ValueTask<Stream> PreferIPv4Async(SocketsHttpConnectionContext context, CancellationToken ct)
	{
		var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
		var socket = await ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
		return new NetworkStream(socket, ownsSocket: true);
	}

	// shortcut: IPv4 first rather than Happy Eyeballs (both at once); a network whose IPv4 is broken but IPv6 works
	// still waits per IPv4 address. Upgrade if that shows up.
	public static async Task<Socket> ConnectAsync(IEnumerable<IPAddress> addresses, int port, CancellationToken ct)
	{
		// Dual mode where the OS has IPv6, so one socket can try both families.
		var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
		try
		{
			await socket.ConnectAsync([.. addresses.OrderBy(x => x.AddressFamily != AddressFamily.InterNetwork)], port, ct);
			return socket;
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}
}

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

	/// <summary>
	/// A <see cref="SocketsHttpHandler.ConnectCallback"/> that connects as players and browsers do: over IPv6 when it works,
	/// falling back to IPv4 after a few seconds rather than the OS timeout. For links that only work from the address that
	/// asked for them (FlixCloud's), fetched by whatever the player connects with.
	/// </summary>
	public static async ValueTask<Stream> PreferIPv6Async(SocketsHttpConnectionContext context, CancellationToken ct)
	{
		var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
		var ipv4 = addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray();
		if (ipv4.Length > 0 && addresses.Any(x => x.AddressFamily == AddressFamily.InterNetworkV6) && Socket.OSSupportsIPv6)
		{
			using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
			attempt.CancelAfter(TimeSpan.FromSeconds(3));
			try
			{
				return new NetworkStream(await ConnectAsync(addresses.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6), context.DnsEndPoint.Port, attempt.Token), ownsSocket: true);
			}
			catch (Exception ex) when (ex is SocketException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
			{
				addresses = ipv4;
			}
		}

		return new NetworkStream(await ConnectAsync(addresses, context.DnsEndPoint.Port, ct), ownsSocket: true);
	}

	/// <summary>How long an address gets before the next one is tried, rather than the OS's ~21 s.</summary>
	private static readonly TimeSpan s_perAddressTimeout = TimeSpan.FromSeconds(3);

	// shortcut: IPv4 first, one address at a time, rather than Happy Eyeballs (both at once); a dead address costs up to
	// 3 s before the next. Upgrade if that shows up.
	public static async Task<Socket> ConnectAsync(IEnumerable<IPAddress> addresses, int port, CancellationToken ct)
	{
		IPAddress[] ordered = [.. addresses.OrderBy(x => x.AddressFamily != AddressFamily.InterNetwork)];
		Exception? last = null;
		for (var i = 0; i < ordered.Length; i++)
		{
			// Dual mode where the OS has IPv6, so the socket takes either family. A new one each try: a cancelled connect leaves it unusable.
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			using var attempt = CancellationTokenSource.CreateLinkedTokenSource(ct);
			// The last address gets as long as it needs: a slow network may only have that one that works.
			if (i < ordered.Length - 1)
			{
				attempt.CancelAfter(s_perAddressTimeout);
			}

			try
			{
				await socket.ConnectAsync(ordered[i], port, attempt.Token);
				return socket;
			}
			catch (Exception ex) when (ex is SocketException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
			{
				socket.Dispose();
				last = ex;
			}
			catch
			{
				socket.Dispose();
				throw;
			}
		}

		throw last ?? new SocketException((int)SocketError.HostNotFound);
	}
}

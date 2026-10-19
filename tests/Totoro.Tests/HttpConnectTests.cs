using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Totoro.Core.Helpers;
using Xunit;

namespace Totoro.Tests;

public class HttpConnectTests
{
	[Fact]
	public async Task ConnectAsync_TriesIPv4BeforeAnIPv6AddressListedFirst()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		// 100::/64 is the IPv6 discard prefix: never answers, so trying it first would hang or fail.
		IPAddress[] addresses = [IPAddress.Parse("100::1"), IPAddress.Parse("100::2"), IPAddress.Loopback];

		var watch = Stopwatch.StartNew();
		using var socket = await HttpConnect.ConnectAsync(addresses, port, CancellationToken.None);

		Assert.Equal(IPAddress.Loopback, ((IPEndPoint)socket.RemoteEndPoint!).Address.MapToIPv4());
		Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Took {watch.Elapsed}");
	}
}

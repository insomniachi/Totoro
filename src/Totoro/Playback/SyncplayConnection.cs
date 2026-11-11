using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Totoro.Playback;

/// <summary>A connection to a Syncplay server: JSON messages, a line each, encrypted where the server can be.</summary>
internal sealed class SyncplayConnection : IDisposable
{
	private readonly TcpClient _tcp;
	private readonly SemaphoreSlim _writeLock = new(1, 1);

	private SyncplayConnection(TcpClient tcp, Stream stream, bool isEncrypted)
	{
		(_tcp, Stream, IsEncrypted) = (tcp, stream, isEncrypted);
	}

	/// <summary>What's said and heard over it: the TCP stream, or the encryption over it.</summary>
	public Stream Stream { get; }

	/// <summary>Whether the connection is encrypted, so the room, chat and what's watched aren't sent in the open.</summary>
	public bool IsEncrypted { get; }

	/// <summary>
	/// Connects, encrypted where the server can be (syncplay.pl's can): it's asked first, and turns it on or says it can't.
	/// A server from before Syncplay had encryption turns the question down and closes: then it's asked again without it.
	/// </summary>
	public static async Task<SyncplayConnection> OpenAsync(string host, int port, CancellationToken ct)
	{
		var tcp = new TcpClient();
		try
		{
			await tcp.ConnectAsync(host, port, ct);
			Stream stream = tcp.GetStream();
			await WriteLineAsync(stream, new JsonObject { ["TLS"] = new JsonObject { ["startTLS"] = "send" } }, ct);
			var reply = await ReadLineAsync(stream, ct);
			var answer = reply is null ? null : (string?)JsonNode.Parse(reply)?["TLS"]?["startTLS"];
			if (answer == "true")
			{
				var tls = new SslStream(stream);
				// Checked as any HTTPS site's is: a server whose certificate isn't valid isn't joined.
				await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
				return new SyncplayConnection(tcp, tls, true);
			}

			if (answer is null)
			{
				tcp.Dispose();
				tcp = new TcpClient();
				await tcp.ConnectAsync(host, port, ct);
				stream = tcp.GetStream();
			}

			return new SyncplayConnection(tcp, stream, false);
		}
		catch
		{
			tcp.Dispose();
			throw;
		}
	}

	/// <summary>Sends a message; lost connections are left to the read loop to notice.</summary>
	public async Task SendAsync(JsonObject message)
	{
		await _writeLock.WaitAsync();
		try
		{
			await WriteLineAsync(Stream, message);
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
		{
			// Lost: the read loop says so.
		}
		finally
		{
			_writeLock.Release();
		}
	}

	public void Dispose()
	{
		Stream.Dispose();
		_tcp.Dispose();
	}

	/// <summary>A line from <paramref name="stream"/> a byte at a time: before encryption starts, nothing after it may be read.</summary>
	private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
	{
		var line = new List<byte>();
		var b = new byte[1];
		while (await stream.ReadAsync(b, ct) == 1)
		{
			if (b[0] == '\n')
			{
				return Encoding.UTF8.GetString([.. line]).TrimEnd('\r');
			}

			line.Add(b[0]);
		}

		return null;
	}

	// Syncplay's server reads lines ending in CRLF.
	private static Task WriteLineAsync(Stream stream, JsonObject message, CancellationToken ct = default) =>
		stream.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\r\n"), ct).AsTask();
}

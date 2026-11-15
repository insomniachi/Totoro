using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Flurl.Http;
using NAudio.Wave;

namespace Totoro.Playback;

/// <summary>
/// Names the song playing on the PC: listens to what the speakers play (WASAPI loopback) and asks Shazam, as its app does
/// (after shazam-for-real). Shazam's endpoint is unofficial: it may change or refuse without notice.
/// </summary>
public sealed class SongRecognizer
{
	// Shazam wants one per install; per run is enough.
	private static readonly string s_installation = Guid.NewGuid().ToString();

	// shortcut: gives up after this long, playing or not; make it a setting if songs with long quiet intros are missed.
	private static readonly TimeSpan s_giveUp = TimeSpan.FromSeconds(20);

	/// <summary>The song playing, as Shazam names it (in English where it has an English name); null if it doesn't know it, or nothing plays.</summary>
	public async Task<(string Title, string Artist)?> RecognizeAsync(CancellationToken ct)
	{
		var signature = new ShazamSignature();
		List<float> heard = [];
		using var recorder = new WasapiRecorderBuilder()
			.WithLoopbackCapture()
			.WithFormat(new WaveFormat(ShazamSignature.SampleRate, 16, 1))
			.Build();
		recorder.DataAvailable += (buffer, _, _, _) =>
		{
			var samples = MemoryMarshal.Cast<byte, short>(buffer);
			lock (heard)
			{
				foreach (var sample in samples)
				{
					heard.Add(sample / 32768f);
				}
			}
		};
		recorder.StartRecording();

		var tag = Guid.NewGuid().ToString();
		var clock = Stopwatch.StartNew();
		var sendAt = 3000;
		while (true)
		{
			await Task.Delay(100, ct);
			lock (heard)
			{
				signature.Add(CollectionsMarshal.AsSpan(heard));
				heard.Clear();
			}

			var timeUp = clock.Elapsed > s_giveUp;
			if (signature.Milliseconds < sendAt && !timeUp)
			{
				continue;
			}

			// Loopback hears nothing at all while nothing plays.
			if (signature.SampleCount == 0)
			{
				return null;
			}

			var (song, retry) = ParseMatch(await MatchAsync(signature.Write(), signature.Milliseconds, tag, ct));
			if (song is not null || timeUp || retry == 0)
			{
				return song;
			}

			// Shazam says how much audio (in all) to send next.
			sendAt = retry;
		}
	}

	/// <summary>Shazam's match response for <paramref name="signature"/>, of <paramref name="milliseconds"/> of audio.</summary>
	internal static async Task<string> MatchAsync(byte[] signature, int milliseconds, string tag, CancellationToken ct)
	{
		return await $"https://amp.shazam.com/match/v1/en/US/android/{s_installation}/{tag}"
					 .WithHeader("User-Agent", "curl/7")
					 .WithTimeout(10)
					 .PostJsonAsync(new
					 {
						 signatures = new[] { new { uri = "data:audio/vnd.shazam.sig;base64," + Convert.ToBase64String(signature), samplems = milliseconds } },
						 timezone = "GMT",
					 }, cancellationToken: ct)
					 .ReceiveString();
	}

	/// <summary>
	/// A match response as the song found (null if none), and if none, how much audio (in all, in ms) to send next; 0 to give up.
	/// </summary>
	public static ((string Title, string Artist)? Song, int RetryMs) ParseMatch(string response)
	{
		var root = JsonNode.Parse(response);
		var results = root?["results"];
		if (results?["matches"] is JsonArray { Count: > 0 } matches && matches[0]?["id"]?.GetValue<string>() is { } id)
		{
			var song = root?["resources"]?["shazam-songs"]?[id]?["attributes"];
			return ((song?["title"]?.GetValue<string>() ?? "", song?["artist"]?.GetValue<string>() ?? ""), 0);
		}

		return (null, results?["retry"]?["retryInMilliseconds"]?.GetValue<int>() ?? 0);
	}
}

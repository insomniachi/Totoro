using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Services;
using Totoro.Helpers;
using Totoro.Modules.MediaPlayers.Mpv;

namespace Totoro.Playback;

/// <summary>
/// Finds a downloaded episode's opening and ending by the sound of the anime's songs on AnimeThemes (see
/// <see cref="AudioFingerprint"/>), and keeps them in the anime's info file, for the Downloads provider to play it with.
/// </summary>
internal sealed class SegmentDetector(HttpClient http, AnimeSongService songs, IEnumerable<IMediaSegmentsProvider> segmentProviders, ILogger logger)
{
	// Finding openings and endings, one episode at a time, so info files are written one at a time.
	private readonly SemaphoreSlim _detecting = new(1, 1);

	// Songs' fingerprints and lengths, by their audio's URL: a season's episodes share them.
	private readonly ConcurrentDictionary<Uri, (uint[] Print, TimeSpan Length)> _songPrints = new();

	public static string FfmpegPath => Path.Combine(MpvInstaller.Folder, "ffmpeg.exe");

	/// <summary>
	/// Finds the episode's opening and ending and keeps them in the anime's info file. Needs mpv's ffmpeg and the anime's
	/// MyAnimeList id in its info file; without them, or if the songs aren't found, the episode has none (and Aniskip's are
	/// used). Returns whether any were found.
	/// </summary>
	public async Task<bool> DetectAsync(int number, string file, string folder)
	{
		var ffmpeg = FfmpegPath;
		if (!File.Exists(ffmpeg))
		{
			return false;
		}

		await _detecting.WaitAsync();
		var title = Path.GetFileName(folder);
		try
		{
			if (DownloadNames.ReadInfo(folder)?.MalId is not { } malId)
			{
				return false;
			}

			var candidates = (await songs.GetSongsAsync(malId))
				.Where(x => x.Kind is AnimeSongKind.Opening or AnimeSongKind.Ending && x.Audio is not null && AudioFingerprint.Covers(x.Episodes, number))
				.ToList();
			if (candidates.Count == 0)
			{
				return false;
			}

			var samples = await AudioFingerprint.DecodeAsync(ffmpeg, file, CancellationToken.None);
			var (episode, duration) = await Task.Run(() => (AudioFingerprint.Compute(samples), AudioFingerprint.Duration(samples)));
			List<MediaSegment> segments = [];
			foreach (var kind in candidates.GroupBy(x => x.Kind))
			{
				// Of a kind's songs (when the episodes they're in aren't known), the one that matches best.
				(MediaSegment Segment, double ErrorRate)? best = null;
				foreach (var song in kind)
				{
					var (print, length) = await GetSongPrintAsync(ffmpeg, song.Audio!);
					if (await Task.Run(() => AudioFingerprint.Find(print, episode)) is { } found && (best is null || found.ErrorRate < best.Value.ErrorRate))
					{
						var type = kind.Key == AnimeSongKind.Opening ? MediaSegmentType.Opening : MediaSegmentType.Ending;
						best = (new MediaSegment(type, found.Start, found.Start + length), found.ErrorRate);
					}
				}

				if (best is { } match)
				{
					segments.Add(match.Segment);
				}
			}

			logger.LogInformation("Found {Count} of {Anime} episode {Number}'s opening and ending", segments.Count, title, number);
			// Read again: the info may have been edited while the songs were looked for.
			if (segments.Count == 0 || DownloadNames.ReadInfo(folder) is not { } info)
			{
				return false;
			}

			var all = new Dictionary<int, IReadOnlyList<MediaSegment>>(info.Segments ?? new Dictionary<int, IReadOnlyList<MediaSegment>>())
			{
				[number] = [.. segments.OrderBy(x => x.Start)],
			};
			DownloadNames.WriteInfo(folder, info with { Segments = all });
			foreach (var provider in segmentProviders)
			{
				_ = provider.SubmitSegmentsAsync(malId, number, duration, all[number]);
			}

			return true;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't find {Anime} episode {Number}'s opening and ending", title, number);
			return false;
		}
		finally
		{
			_detecting.Release();
		}
	}

	private async Task<(uint[] Print, TimeSpan Length)> GetSongPrintAsync(string ffmpeg, Uri audio)
	{
		if (_songPrints.TryGetValue(audio, out var known))
		{
			return known;
		}

		// Kept across restarts: AnimeThemes' files don't change. The folder's version changes with how prints are taken.
		var saved = FileHelper.GetPath(Path.Combine("Fingerprints", "v1",
			Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(audio.AbsoluteUri)))[..32] + ".fp"));
		if (AudioFingerprint.Load(saved) is { } loaded)
		{
			return _songPrints[audio] = loaded;
		}

		var file = Path.GetTempFileName();
		try
		{
			using (var request = new HttpRequestMessage(HttpMethod.Get, audio))
			{
				// AnimeThemes refuses requests without one of its own.
				request.Headers.UserAgent.ParseAdd("Totoro/1.0 (anime tracker)");
				using var response = await http.SendAsync(request);
				await File.WriteAllBytesAsync(file, await response.EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
			}

			var samples = await AudioFingerprint.DecodeAsync(ffmpeg, file, CancellationToken.None);
			var (print, length) = await Task.Run(() => (AudioFingerprint.Compute(samples), AudioFingerprint.Duration(samples)));
			AudioFingerprint.Save(saved, print, length);
			return _songPrints[audio] = (print, length);
		}
		finally
		{
			File.Delete(file);
		}
	}
}

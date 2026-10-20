using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Totoro.Core.Helpers;

/// <summary>A file attached to a GitHub release.</summary>
/// <param name="Sha256">Its SHA-256, as GitHub has it (hex); null if it doesn't.</param>
public sealed record GitHubAsset(string Name, Uri Url, long Size, string? Sha256);

/// <summary>A release on GitHub.</summary>
public sealed record GitHubRelease(string Tag, string Name, Uri Page, string Notes, IReadOnlyList<GitHubAsset> Assets);

/// <summary>Reads GitHub releases and downloads their files, checked against GitHub's hashes.</summary>
public static class GitHubReleases
{
	public static HttpClient Http { get; } = CreateClient();

	/// <summary>The repository's latest release that isn't a pre-release; null if it has none.</summary>
	public static async Task<GitHubRelease?> GetLatestAsync(string repository, CancellationToken ct = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		using var response = await Http.SendAsync(request, ct);
		if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();
		using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
		var release = json.RootElement;
		var tag = release.GetProperty("tag_name").GetString() ?? "";
		var assets = release.GetProperty("assets").EnumerateArray().Select(asset =>
		{
			var digest = asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
			return new GitHubAsset(
				asset.GetProperty("name").GetString() ?? "",
				new Uri(asset.GetProperty("browser_download_url").GetString()!),
				asset.GetProperty("size").GetInt64(),
				digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest["sha256:".Length..] : null);
		}).ToList();

		return new GitHubRelease(
			tag,
			release.GetProperty("name").GetString() is { Length: > 0 } title ? title : tag,
			new Uri(release.GetProperty("html_url").GetString()!),
			release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
			assets);
	}

	/// <summary>Downloads <paramref name="asset"/> to <paramref name="path"/>, reporting progress from 0 to 1, and checks it against GitHub's hash.</summary>
	public static async Task DownloadAsync(GitHubAsset asset, string path, IProgress<double>? progress, CancellationToken ct = default)
	{
		using (var response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct))
		{
			response.EnsureSuccessStatusCode();
			var length = response.Content.Headers.ContentLength ?? asset.Size;
			await using var source = await response.Content.ReadAsStreamAsync(ct);
			await using var file = File.Create(path);
			var buffer = new byte[81920];
			long read = 0;
			double reported = 0;
			int count;
			while ((count = await source.ReadAsync(buffer, ct)) > 0)
			{
				await file.WriteAsync(buffer.AsMemory(0, count), ct);
				read += count;
				// In steps, not every few kilobytes: it's usually bound to the UI.
				var done = length > 0 ? (double)read / length : 0;
				if (done - reported >= 0.005 || done >= 1)
				{
					reported = done;
					progress?.Report(done);
				}
			}
		}

		if (asset.Sha256 is { } expected)
		{
			string actual;
			await using (var file = File.OpenRead(path))
			{
				actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
			}

			if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(path);
				throw new InvalidDataException($"The download of {asset.Name} doesn't match the release's file.");
			}
		}
	}

	private static HttpClient CreateClient()
	{
		var client = new HttpClient();
		// GitHub's API refuses requests without one.
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Totoro", AppInfo.Version.ToString()));
		return client;
	}
}

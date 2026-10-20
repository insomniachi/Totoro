using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Modules.AnimeProviders.Downloads;

internal class DownloadsModule : IModule
{
	public static Guid Id { get; } = new("8d3f6b2a-41c7-4e5b-9a0d-7c2e1f8b6d43");

	public Descriptor Descriptor { get; } = new()
	{
		Id = Id,
		Name = "Downloads",
		Description = "Plays the episodes you've downloaded, from the download folder, without going online.",
		EntryPoint = "Downloads",
		Image = @"Assets\Modules\Downloads.png"
	};

	public void ConfigureServices(IServiceCollection services)
	{
		services.AddTransient(_ => Descriptor);
		services.AddKeyedTransient<IAnimeProvider, DownloadsProvider>(Id);
	}
}

/// <summary>
/// The episodes <c>DownloadService</c> downloaded, read back from the download folder (see <see cref="DownloadNames"/>):
/// a show for each anime's folder, its id the folder's name. Nothing can be downloaded from it: it's already here.
/// </summary>
internal sealed class DownloadsProvider(IPreferences preferences, ILogger<DownloadsProvider> logger) : IAnimeProvider
{
	public Guid Id => DownloadsModule.Id;

	public string Name => "Downloads";

	public Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct) => Task.Run(() =>
		Folders().Select(x => ToShow(x.Folder, x.Info, x.Info.OtherTitles.Append(x.Info.Title).Max(title => TitleMatch.Score(query, title))))
				 .Where(x => x.Score >= 0.4)
				 .OrderByDescending(x => x.Score)
				 .ToList(), ct);

	// Folders downloaded before info.json had ids (or whose ids couldn't be had) are only found by SearchAsync.
	public Task<ProviderShow?> FindShowAsync(AnimeId ids, CancellationToken ct) => Task.Run(() =>
		Folders().Where(x => (ids.Anilist > 0 && x.Info.AnilistId == ids.Anilist) || (ids.MyAnimeList > 0 && x.Info.MalId == ids.MyAnimeList))
				 .Select(x => ToShow(x.Folder, x.Info, 1))
				 .FirstOrDefault(), ct);

	private IEnumerable<(DirectoryInfo Folder, DownloadInfo Info)> Folders()
	{
		var root = new DirectoryInfo(preferences.DownloadFolder);
		return root.Exists ? root.EnumerateDirectories().Select(x => (x, ReadInfo(x))) : [];
	}

	private static ProviderShow ToShow(DirectoryInfo folder, DownloadInfo info, double score)
	{
		var cover = folder.EnumerateFiles(DownloadNames.CoverFile + ".*").FirstOrDefault();
		return new ProviderShow(folder.Name, info.Title, score)
		{
			Image = cover is null ? "" : new Uri(cover.FullName).AbsoluteUri,
			Ids = info.AnilistId is null && info.MalId is null ? null : new AnimeId { Anilist = info.AnilistId ?? 0, MyAnimeList = info.MalId ?? 0 },
		};
	}

	public Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct) => Task.Run(() =>
	{
		var folder = Folder(showId);
		if (!folder.Exists)
		{
			return [];
		}

		return folder.EnumerateFiles()
					 .Where(x => !x.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase))
					 .Select(x => (File: x, Number: DownloadNames.ParseEpisode(x.Name)))
					 .Where(x => x.Number is not null)
					 .Select(x => new ProviderEpisode(showId, x.File.Name, x.Number!.Value))
					 .ToList();
	}, ct);

	public Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct)
	{
		var file = Path.Combine(Folder(episode.ShowId).FullName, Path.GetFileName(episode.Id));
		return Task.FromResult<List<VideoServer>>(File.Exists(file) ? [new VideoServer("Downloaded", new Uri(file))] : []);
	}

	// GetFileName: a show id is a folder's name, never a path out of the download folder.
	private DirectoryInfo Folder(string showId) => new(Path.Combine(preferences.DownloadFolder, Path.GetFileName(showId)));

	/// <summary>What the folder's info file says; for a folder without one (or a broken one), its name as the title.</summary>
	private DownloadInfo ReadInfo(DirectoryInfo folder)
	{
		try
		{
			if (DownloadNames.ReadInfo(folder.FullName) is { } info)
			{
				return info;
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Couldn't read {Folder}'s info", folder.FullName);
		}

		return new DownloadInfo(folder.Name, []);
	}
}

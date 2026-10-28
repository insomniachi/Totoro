using System.Text.Json;
using System.Text.Json.Serialization;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;

namespace Totoro.Modules.AnimeDatabase.Anilist;

/// <summary>
/// The user's list while they aren't signed in to AniList, kept on this PC so the app is usable without an account: only
/// their tracking, by AniList id. The anime themselves are fetched from AniList (see <see cref="AnilistTracker"/>).
/// </summary>
internal sealed class AnilistLocalList
{
	private static readonly JsonSerializerOptions s_json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
	private readonly Lock _lock = new();
	private Dictionary<long, Tracking>? _entries;

	internal static string FilePath => FileHelper.GetPath("list.json");

	public Dictionary<long, Tracking> GetAll()
	{
		lock (_lock)
		{
			return new(Entries);
		}
	}

	public Tracking? Get(long id)
	{
		lock (_lock)
		{
			return Entries.GetValueOrDefault(id);
		}
	}

	/// <summary>Adds the anime to the list or changes its entry; only the fields set in <paramref name="changes"/> change.</summary>
	public Tracking Update(long id, Tracking changes)
	{
		lock (_lock)
		{
			var current = Entries.GetValueOrDefault(id);
			var tracking = new Tracking
			{
				Status = changes.Status ?? current?.Status,
				Score = changes.Score ?? current?.Score,
				WatchedEpisodes = changes.WatchedEpisodes ?? current?.WatchedEpisodes,
				StartDate = changes.StartDate ?? current?.StartDate,
				FinishDate = changes.FinishDate ?? current?.FinishDate,
				UpdatedAt = DateTime.Now,
			};
			Entries[id] = tracking;
			Write();
			return tracking.Clone();
		}
	}

	public bool Remove(long id)
	{
		lock (_lock)
		{
			if (!Entries.Remove(id))
			{
				return false;
			}

			Write();
			return true;
		}
	}

	// A file that doesn't parse throws, rather than being overwritten by an empty list on the next save.
	private Dictionary<long, Tracking> Entries => _entries ??= File.Exists(FilePath)
		? JsonSerializer.Deserialize<Dictionary<long, Tracking>>(File.ReadAllText(FilePath), s_json) ?? []
		: [];

	private void Write()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
		var temp = FilePath + ".tmp";
		File.WriteAllText(temp, JsonSerializer.Serialize(_entries, s_json));
		File.Move(temp, FilePath, true);
	}
}

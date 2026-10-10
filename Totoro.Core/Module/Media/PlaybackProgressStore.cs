using System.Text.Json;
using Totoro.Core.Helpers;
using Totoro.Core.Module.Anime;

namespace Totoro.Core.Module.Media;

/// <param name="Position">Where playback stopped.</param>
/// <param name="Duration">The episode's length, to show how far through it is.</param>
public sealed record EpisodeProgress(TimeSpan Position, TimeSpan Duration)
{
	/// <summary>How far through the episode, from 0 to 1.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public double Fraction => Duration > TimeSpan.Zero ? Math.Clamp(Position / Duration, 0, 1) : 0;
}

/// <summary>
/// Where the user stopped in each episode they didn't finish, to resume from there. Kept in a file, by anime (its
/// StoreKey, so it's the same whichever database the anime came from) and
/// episode number; an episode's entry is removed once it's watched.
/// </summary>
public interface IPlaybackProgressStore
{
	EpisodeProgress? Get(AnimeModel anime, int episode);

	/// <summary>Every episode of the anime with saved progress, by number.</summary>
	IReadOnlyDictionary<int, EpisodeProgress> GetAll(AnimeModel anime);

	void Save(AnimeModel anime, int episode, EpisodeProgress progress);

	void Remove(AnimeModel anime, int episode);
}

internal sealed class PlaybackProgressStore : IPlaybackProgressStore
{
	private readonly string _file = FileHelper.GetPath("progress.json");
	private readonly Lock _lock = new();
	private readonly Dictionary<string, Dictionary<int, EpisodeProgress>> _progress;

	public PlaybackProgressStore()
	{
		try
		{
			_progress = File.Exists(_file)
				? JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, EpisodeProgress>>>(File.ReadAllText(_file)) ?? []
				: [];
		}
		catch (JsonException)
		{
			// A damaged file: start over rather than fail.
			_progress = [];
		}
	}

	public EpisodeProgress? Get(AnimeModel anime, int episode)
	{
		lock (_lock)
		{
			return _progress.GetValueOrDefault(GetKey(anime))?.GetValueOrDefault(episode);
		}
	}

	public IReadOnlyDictionary<int, EpisodeProgress> GetAll(AnimeModel anime)
	{
		lock (_lock)
		{
			return _progress.TryGetValue(GetKey(anime), out var episodes) ? new Dictionary<int, EpisodeProgress>(episodes) : [];
		}
	}

	public void Save(AnimeModel anime, int episode, EpisodeProgress progress)
	{
		lock (_lock)
		{
			var key = GetKey(anime);
			if (!_progress.TryGetValue(key, out var episodes))
			{
				_progress[key] = episodes = [];
			}

			episodes[episode] = progress;
			Write();
		}
	}

	public void Remove(AnimeModel anime, int episode)
	{
		lock (_lock)
		{
			var key = GetKey(anime);
			if (_progress.TryGetValue(key, out var episodes) && episodes.Remove(episode))
			{
				if (episodes.Count == 0)
				{
					_progress.Remove(key);
				}

				Write();
			}
		}
	}

	/// <summary>The anime's key, moving what was saved under its old key first. Call with the lock held.</summary>
	private string GetKey(AnimeModel anime)
	{
		if (_progress.MoveToStoreKey(anime, out var key))
		{
			Write();
		}

		return key;
	}

	private void Write()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
		File.WriteAllText(_file, JsonSerializer.Serialize(_progress));
	}
}

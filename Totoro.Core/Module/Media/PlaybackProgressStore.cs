using System.Text.Json;
using Totoro.Core.Helpers;

namespace Totoro.Core.Module.Media;

/// <param name="Position">Where playback stopped.</param>
/// <param name="Duration">The episode's length, to show how far through it is.</param>
public sealed record EpisodeProgress(TimeSpan Position, TimeSpan Duration)
{
	/// <summary>How far through the episode, from 0 to 1.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public double Fraction => Duration > TimeSpan.Zero ? Math.Clamp(Position / Duration, 0, 1) : 0;
}

public static class PlaybackProgressExtensions
{
	extension(Anime.AnimeModel anime)
	{
		/// <summary>The anime's key in <see cref="IPlaybackProgressStore"/>: its database and its id there.</summary>
		public string ProgressKey => $"{anime.ServiceId}/{anime.Id}";
	}
}

/// <summary>
/// Where the user stopped in each episode they didn't finish, to resume from there. Kept in a file, by anime (the
/// database's id) and episode number; an episode's entry is removed once it's watched.
/// </summary>
public interface IPlaybackProgressStore
{
	EpisodeProgress? Get(string animeKey, int episode);

	/// <summary>Every episode of the anime with saved progress, by number.</summary>
	IReadOnlyDictionary<int, EpisodeProgress> GetAll(string animeKey);

	void Save(string animeKey, int episode, EpisodeProgress progress);

	void Remove(string animeKey, int episode);
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

	public EpisodeProgress? Get(string animeKey, int episode)
	{
		lock (_lock)
		{
			return _progress.GetValueOrDefault(animeKey)?.GetValueOrDefault(episode);
		}
	}

	public IReadOnlyDictionary<int, EpisodeProgress> GetAll(string animeKey)
	{
		lock (_lock)
		{
			return _progress.TryGetValue(animeKey, out var episodes) ? new Dictionary<int, EpisodeProgress>(episodes) : [];
		}
	}

	public void Save(string animeKey, int episode, EpisodeProgress progress)
	{
		lock (_lock)
		{
			if (!_progress.TryGetValue(animeKey, out var episodes))
			{
				_progress[animeKey] = episodes = [];
			}

			episodes[episode] = progress;
			Write();
		}
	}

	public void Remove(string animeKey, int episode)
	{
		lock (_lock)
		{
			if (_progress.TryGetValue(animeKey, out var episodes) && episodes.Remove(episode))
			{
				if (episodes.Count == 0)
				{
					_progress.Remove(animeKey);
				}

				Write();
			}
		}
	}

	private void Write()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
		File.WriteAllText(_file, JsonSerializer.Serialize(_progress));
	}
}

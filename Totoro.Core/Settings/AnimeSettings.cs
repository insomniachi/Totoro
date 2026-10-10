using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Totoro.Core.Helpers;
using Totoro.Core.Module.Anime;
using Totoro.Core.Module.Media;
using Totoro.Core.Module.Streaming;

namespace Totoro.Core.Settings;

/// <summary>
/// One anime's own settings for watching it, used instead of the app-wide ones where set: which anime provider to
/// watch it from, that provider's settings, and how to skip its openings and endings.
/// </summary>
public sealed class AnimeSettings
{
	/// <summary>The anime provider to watch it from; null for the selected one.</summary>
	public Guid? ProviderId { get; set; }

	/// <summary>
	/// Settings for watching it from a provider, by the provider's (module's) id, instead of the module's own: its whole
	/// settings object, as JSON (see <see cref="IModuleSettings.CreateCopy"/>). Kept per provider, so switching provider
	/// and back doesn't lose them.
	/// </summary>
	public Dictionary<Guid, JsonObject> ProviderSettings { get; set; } = [];

	/// <summary>The show it is on each provider, by the provider's id: found (or picked) the first time it's watched.</summary>
	public Dictionary<Guid, ProviderShow> Shows { get; set; } = [];

	/// <summary>What to do when an opening (or recap) starts; null to follow the app-wide setting.</summary>
	public SkipMode? OpeningSkipMode { get; set; }

	/// <summary>What to do when an ending starts; null to follow the app-wide setting.</summary>
	public SkipMode? EndingSkipMode { get; set; }

	/// <summary>Nothing set: the app-wide settings apply throughout.</summary>
	[JsonIgnore]
	public bool IsEmpty => ProviderId is null && ProviderSettings.Count == 0 && Shows.Count == 0 && OpeningSkipMode is null && EndingSkipMode is null;
}

/// <summary>Each anime's <see cref="AnimeSettings"/>, kept in a file, by anime (its database and its id there).</summary>
public interface IAnimeSettingsStore
{
	/// <summary>The anime's settings: a copy to change and <see cref="Save"/>; empty if it has none.</summary>
	AnimeSettings Get(AnimeModel anime);

	void Save(AnimeModel anime, AnimeSettings settings);
}

internal sealed class AnimeSettingsStore : IAnimeSettingsStore
{
	private static readonly JsonSerializerOptions s_options = new() { Converters = { new JsonStringEnumConverter() } };

	private readonly string _file = FileHelper.GetPath("anime-settings.json");
	private readonly Lock _lock = new();
	private readonly Dictionary<string, AnimeSettings> _settings;

	/// <summary>Preference the shows chosen for anime were kept in before <see cref="AnimeSettings.Shows"/>.</summary>
	private const string LegacyShowsKey = "ProviderShows";

	public AnimeSettingsStore(IPreferences preferences)
	{
		try
		{
			_settings = File.Exists(_file)
				? JsonSerializer.Deserialize<Dictionary<string, AnimeSettings>>(File.ReadAllText(_file), s_options) ?? []
				: [];
		}
		catch (JsonException)
		{
			// A damaged file: start over rather than fail.
			_settings = [];
		}

		MoveLegacyShows(preferences);
	}

	/// <summary>Moves the shows from the old preference, keyed "provider id/anime key", into the anime's settings.</summary>
	private void MoveLegacyShows(IPreferences preferences)
	{
		if (preferences.ReadKey<Dictionary<string, ProviderShow>>(LegacyShowsKey) is not { Count: > 0 } shows)
		{
			preferences.RemoveKey(LegacyShowsKey);
			return;
		}

		foreach (var (key, show) in shows)
		{
			var separator = key.IndexOf('/');
			if (separator > 0 && Guid.TryParse(key[..separator], out var providerId))
			{
				var animeKey = key[(separator + 1)..];
				if (!_settings.TryGetValue(animeKey, out var settings))
				{
					_settings[animeKey] = settings = new AnimeSettings();
				}

				settings.Shows.TryAdd(providerId, show);
			}
		}

		Write();
		preferences.RemoveKey(LegacyShowsKey);
	}

	public AnimeSettings Get(AnimeModel anime)
	{
		lock (_lock)
		{
			return _settings.TryGetValue(anime.ProgressKey, out var settings) ? Copy(settings) : new AnimeSettings();
		}
	}

	public void Save(AnimeModel anime, AnimeSettings settings)
	{
		lock (_lock)
		{
			if (settings.IsEmpty)
			{
				_settings.Remove(anime.ProgressKey);
			}
			else
			{
				_settings[anime.ProgressKey] = Copy(settings);
			}

			Write();
		}
	}

	private void Write()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
		File.WriteAllText(_file, JsonSerializer.Serialize(_settings, s_options));
	}

	/// <summary>A deep copy, so changes to one given out don't reach the stored one until saved.</summary>
	private static AnimeSettings Copy(AnimeSettings settings)
	{
		return JsonSerializer.Deserialize<AnimeSettings>(JsonSerializer.Serialize(settings, s_options), s_options)!;
	}
}

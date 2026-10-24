using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeProviders.Downloads;

namespace Totoro.Agent;

[Description("An anime's own settings for watching it; where one isn't set, the app-wide setting applies.")]
public sealed record AnimeWatchSettings(
	[property: Description("The anime provider its episodes are streamed from, or null for the app's selected provider.")]
	string? Provider,
	IReadOnlyList<string> AvailableProviders,
	[property: Description("What happens at its openings, or null to follow the app-wide setting.")]
	SkipMode? OpeningSkipMode,
	[property: Description("What happens at its endings, or null to follow the app-wide setting.")]
	SkipMode? EndingSkipMode,
	[property: Description("The show it's matched with on each provider, by provider name.")]
	IReadOnlyDictionary<string, string> Shows,
	[property: Description("Providers it has its own settings for (e.g. video quality), set in the app.")]
	IReadOnlyList<string> OwnProviderSettings,
	[property: Description("How its provider numbers its episodes: Season (each season from 1), Absolute (carrying on across seasons, placed by numbering rules), or null for automatic.")]
	EpisodeNumbering? EpisodeNumbering,
	[property: Description("Whether watching it is kept off Discord, where the episode playing otherwise shows as the user's activity (e.g. for NSFW anime).")]
	bool HideFromDiscord);

/// <summary>Agent tools for an anime's own settings (see <see cref="AnimeSettings"/>).</summary>
public sealed class AnimeSettingsTools(AnimeLookup lookup, IAnimeSettingsStore store, IFactory<IAnimeProvider, Guid> providers)
{
	[AgentTool("get_anime_settings")]
	[Description("An anime's own settings for watching it: its provider, how its openings and endings are skipped, and whether it's hidden from Discord.")]
	public async Task<AnimeWatchSettings> GetAnimeSettingsAsync(long animeId)
	{
		var anime = await lookup.GetAsync(animeId);
		return Describe(store.Get(anime));
	}

	[AgentTool("update_anime_settings")]
	[Description("Changes an anime's own settings for watching it. Only the values given change.")]
	public async Task<AnimeWatchSettings> UpdateAnimeSettingsAsync(
		long animeId,
		[Description("The provider to stream it from (one of availableProviders), or \"default\" for the app's selected provider.")]
		string? provider = null,
		[Description("Off (don't skip), Ask (offer to skip) or Automatic (skip), or Default to follow the app-wide setting.")]
		AnimeSkipMode? openingSkipMode = null,
		[Description("As openingSkipMode, for endings.")] AnimeSkipMode? endingSkipMode = null,
		[Description("How its provider numbers its episodes: Season (each season from 1), Absolute (carrying on across seasons, as numbering rules say), or Automatic to work it out from the provider's episodes.")]
		AnimeEpisodeNumbering? episodeNumbering = null,
		[Description("Whether watching it is kept off Discord, where the episode playing otherwise shows as the user's activity.")]
		bool? hideFromDiscord = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var settings = store.Get(anime);
		if (provider is not null)
		{
			settings.ProviderId = provider.Equals("default", StringComparison.OrdinalIgnoreCase)
				? null
				: Selectable().FirstOrDefault(x => x.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))?.Id
				  ?? throw new AgentToolException($"There's no provider called {provider}; the providers are {string.Join(", ", ProviderNames())}.");
		}

		if (openingSkipMode is { } opening)
		{
			settings.OpeningSkipMode = ToSkipMode(opening);
		}

		if (endingSkipMode is { } ending)
		{
			settings.EndingSkipMode = ToSkipMode(ending);
		}

		if (episodeNumbering is { } numbering)
		{
			settings.EpisodeNumbering = numbering switch
			{
				AnimeEpisodeNumbering.Season => EpisodeNumbering.Season,
				AnimeEpisodeNumbering.Absolute => EpisodeNumbering.Absolute,
				_ => null,
			};
		}

		if (hideFromDiscord is { } hide)
		{
			settings.HideFromDiscord = hide;
		}

		store.Save(anime, settings);
		return Describe(settings);
	}

	private AnimeWatchSettings Describe(AnimeSettings settings)
	{
		var all = providers.CreateAll().ToList();
		string NameOf(Guid id) => all.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString();
		return new AnimeWatchSettings(
			settings.ProviderId is { } id ? NameOf(id) : null,
			[.. ProviderNames()],
			settings.OpeningSkipMode,
			settings.EndingSkipMode,
			settings.Shows.ToDictionary(x => NameOf(x.Key), x => x.Value.Title),
			[.. settings.ProviderSettings.Keys.Select(NameOf)],
			settings.EpisodeNumbering,
			settings.HideFromDiscord);
	}

	// Not Downloads: what's downloaded is played first, whatever the provider.
	private IEnumerable<IAnimeProvider> Selectable() => providers.CreateAll().Where(x => x.Id != DownloadsModule.Id);

	private IEnumerable<string> ProviderNames() => Selectable().Select(x => x.Name);

	private static SkipMode? ToSkipMode(AnimeSkipMode mode) => mode switch
	{
		AnimeSkipMode.Off => SkipMode.Off,
		AnimeSkipMode.Ask => SkipMode.Ask,
		AnimeSkipMode.Automatic => SkipMode.Automatic,
		_ => null,
	};
}

/// <summary>A <see cref="SkipMode"/>, or the app-wide one.</summary>
public enum AnimeSkipMode
{
	Default,
	Off,
	Ask,
	Automatic,
}

/// <summary>An <see cref="EpisodeNumbering"/>, or worked out.</summary>
public enum AnimeEpisodeNumbering
{
	Automatic,
	Season,
	Absolute,
}

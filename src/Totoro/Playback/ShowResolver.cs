using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.ViewModels;

namespace Totoro.Playback;

/// <summary>
/// Finds where to get an anime's episodes: the provider to watch it from, the provider's show for it, and the show's
/// episode for each of the anime's. Watching, downloading and the agent's tools all find them so.
/// </summary>
public sealed class ShowResolver(
	IFactory<IAnimeProvider> providers,
	IAnimeSettingsStore animeSettings,
	IAnimeRelations relations,
	IEnumerable<IModuleSettings> moduleSettings,
	IDialogService dialogs)
{
	/// <summary>
	/// The provider to watch the anime from: its own (see <see cref="AnimeSettings"/>), or the selected one; set to use
	/// the anime's own settings for it, if it has any.
	/// </summary>
	public IAnimeProvider? GetProvider(AnimeModel anime) => GetProvider(animeSettings.Get(anime));

	/// <inheritdoc cref="GetProvider(AnimeModel)"/>
	public IAnimeProvider? GetProvider(AnimeSettings settings)
	{
		var provider = providers.CreateStreaming(settings.ProviderId);
		if (provider is not null && settings.ProviderSettings.TryGetValue(provider.Id, out var own) &&
			moduleSettings.FirstOrDefault(x => x.Descriptor.Id == provider.Id) is { } module)
		{
			provider.UseSettings(module.CreateCopy(own));
		}

		return provider;
	}

	/// <summary>Matches the anime with a show on the provider: it's watched from that show from now on.</summary>
	public void SetShow(AnimeModel anime, IAnimeProvider provider, ProviderShow show)
	{
		var settings = animeSettings.Get(anime);
		settings.Shows[provider.Id] = show;
		animeSettings.Save(anime, settings);
	}

	/// <summary>
	/// The show to watch the anime from on the provider: the one it's matched with, or else the only exact title match
	/// (which becomes its match). Otherwise no show, and the shows it may be, best match first, to choose from.
	/// </summary>
	public async Task<(ProviderShow? Show, IReadOnlyList<ProviderShow> Candidates)> FindShowAsync(IAnimeProvider provider, AnimeModel anime,
																							   CancellationToken ct)
	{
		if (animeSettings.Get(anime).Shows.TryGetValue(provider.Id, out var known))
		{
			// A download's folder is renamed when its naming changes: one its ids find now replaces the old match.
			if (provider.Id != DownloadsModule.Id || await provider.FindShowAsync(anime.ExternalIds, ct) is not { } byId || byId.Id == known.Id)
			{
				return (known, [known]);
			}
		}

		var results = await ShowMatching.SearchShowsAsync(provider, anime, ct);
		var exact = results.Where(x => x.Score >= 1).ToList();
		if (exact.Count != 1)
		{
			return (null, results);
		}

		SetShow(anime, provider, exact[0]);
		return (exact[0], results);
	}

	/// <summary>The provider's show for the anime: as <see cref="FindShowAsync"/> finds it, or else one the user picks.</summary>
	public async Task<ProviderShow?> FindOrPickShowAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
	{
		var (show, candidates) = await FindShowAsync(provider, anime, ct);
		if (show is not null)
		{
			return show;
		}

		var picked = await dialogs.ShowAsync<ShowPickerDialogViewModel, ProviderShow>(new ShowPickerRequest(provider, anime.Title, candidates));
		if (picked is not null)
		{
			SetShow(anime, provider, picked);
		}

		return picked;
	}

	/// <summary>
	/// A copy of the provider's settings for the anime, to change for once (e.g. for a download) and pass to
	/// <see cref="IAnimeProvider.UseSettings"/>: its own, or else the module's. Null if the provider has none.
	/// </summary>
	public DataContainerConfig? CopyProviderSettings(AnimeModel anime, Guid providerId) =>
		moduleSettings.FirstOrDefault(x => x.Descriptor.Id == providerId)?.CreateCopy(animeSettings.Get(anime).ProviderSettings.GetValueOrDefault(providerId));

	/// <summary>
	/// The provider's episode for the anime's episode <paramref name="number"/>, skipping specials like 12.5. Providers may
	/// use absolute numbering, carrying on across seasons (one show for both seasons, the second from 13): then
	/// anime-relations (and the user's rules) say where the episode is. <paramref name="numbering"/> is the anime's
	/// setting: by season, absolute, or (null) worked out from the provider's episode numbers.
	/// </summary>
	public ProviderEpisode? PickEpisode(List<ProviderEpisode> episodes, AnimeModel anime, int number, EpisodeNumbering? numbering) =>
		ShowMatching.PickEpisode(episodes, anime, number, numbering, relations);

	/// <summary>
	/// The anime's episode that's the provider's <paramref name="episode"/>, numbered as <paramref name="numbering"/> says:
	/// the one playing the anime from that show would find it as (see <see cref="PickEpisode(List{ProviderEpisode}, AnimeModel, int, EpisodeNumbering?)"/>),
	/// so a later season a provider numbers on from the first (29 for its 1) is its own episode. The provider's number when
	/// none of the anime's episodes is that one.
	/// </summary>
	public async Task<int> GetAnimeEpisodeAsync(AnimeModel anime, IReadOnlyList<ProviderEpisode> episodes, ProviderEpisode episode,
												EpisodeNumbering? numbering, CancellationToken ct)
	{
		await relations.EnsureAvailableAsync(ct);
		return ShowMatching.AnimeEpisode([.. episodes], anime, episode, numbering, relations);
	}
}

/// <summary>Picks the torrent streamer, for playing and downloading alike.</summary>
public static class TorrentStreamers
{
	/// <summary>The selected torrent streamer, if it's set up; null otherwise.</summary>
	public static ITorrentStreamer? CreateAvailable(this IFactory<ITorrentStreamer> streamers) =>
		streamers.CreateDefault() is { } streamer && streamer.IsAvailable() ? streamer : null;
}

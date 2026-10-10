using System.Globalization;

namespace Totoro.Core.Module.Anime;

public static class AnimeStoreKeyExtensions
{
	extension(AnimeModel anime)
	{
		/// <summary>
		/// The anime's key in what's saved about it locally (playback progress, its settings): its MyAnimeList id, which
		/// every anime database has (AniList gives it too), so what's saved stays with the anime whichever database it
		/// came from. Anime without one (a few AniList entries) are keyed by their database and their id there.
		/// </summary>
		public string StoreKey => anime.ExternalIds.MyAnimeList > 0
			? anime.ExternalIds.MyAnimeList.ToString(CultureInfo.InvariantCulture)
			: anime.DatabaseKey;

		/// <summary>The anime's database and its id there; what was saved was keyed by this before <see cref="StoreKey"/>.</summary>
		public string DatabaseKey => $"{anime.ServiceId}/{anime.Id}";
	}

	/// <summary>
	/// The anime's <see cref="StoreKey"/> in <paramref name="entries"/>, after moving its entry there from its
	/// <see cref="DatabaseKey"/> (how it was keyed before), if it's still under that. Returns whether it moved one, for
	/// the caller to save. An entry that's already under the new key is kept, and an old one left where it is.
	/// </summary>
	internal static bool MoveToStoreKey<T>(this Dictionary<string, T> entries, AnimeModel anime, out string key)
	{
		key = anime.StoreKey;
		var old = anime.DatabaseKey;
		if (key == old || entries.ContainsKey(key) || !entries.Remove(old, out var entry))
		{
			return false;
		}

		entries[key] = entry;
		return true;
	}
}
